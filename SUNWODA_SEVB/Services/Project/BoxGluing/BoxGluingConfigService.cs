using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.BoxGluing;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.BoxGluing;

namespace SUNWODA_SEVB.Services.Project.BoxGluing
{
    /// <summary>
    /// 从 project_setting 读取可改运行参数；通道/点位以代码默认值为准。
    /// 平台 SqlSugar 单例在 WPF 同步上下文中不能并发使用同一连接，这里串行访问。
    /// </summary>
    public class BoxGluingConfigService : IBoxGluingConfigService
    {
        public const string SettingCsvRoot = "CsvRootPath";
        public const string SettingSampleMs = "SampleIntervalMs";
        public const string SettingPollMs = "TriggerPollIntervalMs";
        public const string SettingSimulation = "UseSimulation";
        public const string SettingChartWindow = "ChartWindowPoints";

        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly ILoggerService<BoxGluingConfigService> _logger;
        private readonly SemaphoreSlim _dbLock = new(1, 1);

        public BoxGluingAcquisitionConfig Current { get; private set; } = BoxGluingDefaultConfig.Create();

        public BoxGluingConfigService(
            IProjectSettingRepository projectSettingRepository,
            ILoggerService<BoxGluingConfigService> logger)
        {
            _projectSettingRepository = projectSettingRepository;
            _logger = logger;
        }

        public async Task<BoxGluingAcquisitionConfig> LoadAsync()
        {
            await _dbLock.WaitAsync();
            try
            {
                var config = BoxGluingDefaultConfig.Create();
                var vm = BoxGluingAcquisitionConfig.VmClassName;
                var rows = await _projectSettingRepository.GetByVMNameAsync(vm) ?? [];
                var map = rows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                    .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                config.CsvRootPath = ReadString(map, SettingCsvRoot, config.CsvRootPath);
                config.SampleIntervalMs = Clamp(ReadInt(map, SettingSampleMs, config.SampleIntervalMs), 20, 1000);
                config.TriggerPollIntervalMs = Clamp(ReadInt(map, SettingPollMs, config.TriggerPollIntervalMs), 20, 500);
                config.UseSimulation = ReadBool(map, SettingSimulation, config.UseSimulation);
                config.ChartWindowPoints = Clamp(ReadInt(map, SettingChartWindow, config.ChartWindowPoints), 200, 20000);

                await EnsureSettingAsync(map, SettingCsvRoot, vm, config.CsvRootPath, "string", "CSV根目录");
                await EnsureSettingAsync(map, SettingSampleMs, vm, config.SampleIntervalMs.ToString(), "int", "采样周期毫秒");
                await EnsureSettingAsync(map, SettingPollMs, vm, config.TriggerPollIntervalMs.ToString(), "int", "触发轮询毫秒");
                await EnsureSettingAsync(map, SettingSimulation, vm, config.UseSimulation ? "true" : "false", "bool", "模拟采集");
                await EnsureSettingAsync(map, SettingChartWindow, vm, config.ChartWindowPoints.ToString(), "int", "曲线窗口点数");

                Current = config;
                _logger.Info(
                    $"BoxGluing配置已加载：周期={config.SampleIntervalMs}ms，CSV={config.CsvRootPath}，模拟={config.UseSimulation}",
                    false);
                return config;
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task SaveRuntimeSettingsAsync(string csvRootPath, int sampleIntervalMs, bool useSimulation)
        {
            Current.CsvRootPath = string.IsNullOrWhiteSpace(csvRootPath) ? Current.CsvRootPath : csvRootPath.Trim();
            Current.SampleIntervalMs = Clamp(sampleIntervalMs, 20, 1000);
            Current.UseSimulation = useSimulation;

            await _dbLock.WaitAsync();
            try
            {
                var vm = BoxGluingAcquisitionConfig.VmClassName;
                await UpsertAsync(SettingCsvRoot, vm, Current.CsvRootPath, "string", "CSV根目录");
                await UpsertAsync(SettingSampleMs, vm, Current.SampleIntervalMs.ToString(), "int", "采样周期毫秒");
                await UpsertAsync(SettingSimulation, vm, Current.UseSimulation ? "true" : "false", "bool", "模拟采集");
            }
            finally
            {
                _dbLock.Release();
            }
        }

        private static string ReadString(Dictionary<string, ProjectSettingModel> map, string name, string fallback) =>
            map.TryGetValue(name, out var model) && !string.IsNullOrWhiteSpace(model.Value)
                ? model.Value
                : fallback;

        private static int ReadInt(Dictionary<string, ProjectSettingModel> map, string name, int fallback) =>
            map.TryGetValue(name, out var model) && int.TryParse(model.Value, out var value)
                ? value
                : fallback;

        private static bool ReadBool(Dictionary<string, ProjectSettingModel> map, string name, bool fallback)
        {
            if (!map.TryGetValue(name, out var model) || model.Value is null)
                return fallback;
            if (bool.TryParse(model.Value, out var flag))
                return flag;
            return model.Value is "1" or "true" or "True";
        }

        private async Task EnsureSettingAsync(
            Dictionary<string, ProjectSettingModel> map,
            string name,
            string vm,
            string value,
            string type,
            string remark)
        {
            if (map.ContainsKey(name))
                return;

            var model = new ProjectSettingModel
            {
                Name = name,
                BelongToVM = vm,
                Value = value,
                Type = type,
                RoleRank = 2,
                Remark = remark
            };
            await _projectSettingRepository.AddAsync(model);
            map[name] = model;
        }

        private async Task UpsertAsync(string name, string vm, string value, string type, string remark)
        {
            var exist = await _projectSettingRepository.GetByNameAndVMAsync(name, vm);
            if (exist is null)
            {
                await _projectSettingRepository.AddAsync(new ProjectSettingModel
                {
                    Name = name,
                    BelongToVM = vm,
                    Value = value,
                    Type = type,
                    RoleRank = 2,
                    Remark = remark
                });
                return;
            }

            exist.Value = value;
            exist.Type = type;
            await _projectSettingRepository.UpdateAsync(exist);
        }

        private static int Clamp(int value, int min, int max) =>
            Math.Min(max, Math.Max(min, value));
    }
}
