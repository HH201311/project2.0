using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    /// <summary>从 project_setting 读写 PACK 六面检运行参数。</summary>
    public sealed class Pack6ConfigService : IZZSIXPack6ConfigService
    {
        public const string SettingLocalPath = "LocalPathPre";
        public const string SettingTopPath = "TopCamPath";
        public const string SettingBottomPath = "BottomCamPath";
        public const string SettingSidePath = "SideCamPath";
        public const string SettingSimulation = "UseSimulation";
        public const string SettingTimeout = "PackTimeoutMinutes";
        public const string SettingCellType = "CellType";
        public const string SettingLine = "ProductionLine";
        public const string SettingWorkshop = "workship";

        private readonly IProjectSettingRepository _repo;
        private readonly ILoggerService<Pack6ConfigService> _logger;
        private readonly SemaphoreSlim _dbLock = new(1, 1);

        public Pack6Config Current { get; private set; } = Pack6DefaultConfig.Create();

        public Pack6ConfigService(
            IProjectSettingRepository repo,
            ILoggerService<Pack6ConfigService> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public async Task<Pack6Config> LoadAsync()
        {
            await _dbLock.WaitAsync();
            try
            {
                var config = Pack6DefaultConfig.Create();
                var vm = Pack6Config.VmClassName;
                var rows = await _repo.GetByVMNameAsync(vm) ?? [];
                var map = rows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                    .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                config.LocalPathPre = ReadString(map, SettingLocalPath, config.LocalPathPre);
                config.TopCamPath = ReadString(map, SettingTopPath, config.TopCamPath);
                config.BottomCamPath = ReadString(map, SettingBottomPath, config.BottomCamPath);
                config.SideCamPath = ReadString(map, SettingSidePath, config.SideCamPath);
                config.UseSimulation = ReadBool(map, SettingSimulation, config.UseSimulation);
                config.PackTimeoutMinutes = Clamp(ReadInt(map, SettingTimeout, config.PackTimeoutMinutes), 1, 120);
                config.CellType = ReadString(map, SettingCellType, config.CellType);
                config.ProductionLine = ReadString(map, SettingLine, config.ProductionLine);
                config.Workshop = ReadString(map, SettingWorkshop, config.Workshop);

                await EnsureAsync(map, SettingLocalPath, vm, config.LocalPathPre, "string", "结果根目录");
                await EnsureAsync(map, SettingTopPath, vm, config.TopCamPath, "string", "上盖相机路径");
                await EnsureAsync(map, SettingBottomPath, vm, config.BottomCamPath, "string", "底部相机路径");
                await EnsureAsync(map, SettingSidePath, vm, config.SideCamPath, "string", "侧面相机路径");
                await EnsureAsync(map, SettingSimulation, vm, config.UseSimulation ? "true" : "false", "bool", "模拟检测");
                await EnsureAsync(map, SettingTimeout, vm, config.PackTimeoutMinutes.ToString(), "int", "Pack超时分钟");
                await EnsureAsync(map, SettingCellType, vm, config.CellType, "string", "电芯型号");
                await EnsureAsync(map, SettingLine, vm, config.ProductionLine, "string", "产线");
                await EnsureAsync(map, SettingWorkshop, vm, config.Workshop, "string", "车间");

                Current = config;
                _logger.Info(
                    $"Pack6配置已加载：模拟={config.UseSimulation}，根目录={config.LocalPathPre}，型号={config.CellType}",
                    false);
                return config;
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task SaveRuntimeSettingsAsync(Pack6Config config)
        {
            Current = config;
            await _dbLock.WaitAsync();
            try
            {
                var vm = Pack6Config.VmClassName;
                await UpsertAsync(SettingLocalPath, vm, config.LocalPathPre, "string", "结果根目录");
                await UpsertAsync(SettingTopPath, vm, config.TopCamPath, "string", "上盖相机路径");
                await UpsertAsync(SettingBottomPath, vm, config.BottomCamPath, "string", "底部相机路径");
                await UpsertAsync(SettingSidePath, vm, config.SideCamPath, "string", "侧面相机路径");
                await UpsertAsync(SettingSimulation, vm, config.UseSimulation ? "true" : "false", "bool", "模拟检测");
                await UpsertAsync(SettingTimeout, vm, config.PackTimeoutMinutes.ToString(), "int", "Pack超时分钟");
                await UpsertAsync(SettingCellType, vm, config.CellType, "string", "电芯型号");
                await UpsertAsync(SettingLine, vm, config.ProductionLine, "string", "产线");
                await UpsertAsync(SettingWorkshop, vm, config.Workshop, "string", "车间");
                _logger.Info("Pack6运行参数已保存", false);
            }
            finally
            {
                _dbLock.Release();
            }
        }

        private async Task EnsureAsync(
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
            await _repo.AddAsync(model);
            map[name] = model;
        }

        private async Task UpsertAsync(string name, string vm, string value, string type, string remark)
        {
            var exist = await _repo.GetByNameAndVMAsync(name, vm);
            if (exist is null)
            {
                await _repo.AddAsync(new ProjectSettingModel
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
            exist.Remark = remark;
            await _repo.UpdateAsync(exist);
        }

        private static string ReadString(Dictionary<string, ProjectSettingModel> map, string key, string fallback) =>
            map.TryGetValue(key, out var row) && !string.IsNullOrWhiteSpace(row.Value) ? row.Value.Trim() : fallback;

        private static int ReadInt(Dictionary<string, ProjectSettingModel> map, string key, int fallback) =>
            map.TryGetValue(key, out var row) && int.TryParse(row.Value, out var v) ? v : fallback;

        private static bool ReadBool(Dictionary<string, ProjectSettingModel> map, string key, bool fallback)
        {
            if (!map.TryGetValue(key, out var row) || row.Value is null)
                return fallback;
            if (bool.TryParse(row.Value, out var flag))
                return flag;
            return row.Value is "1" or "true" or "True";
        }

        private static int Clamp(int v, int min, int max) => Math.Min(Math.Max(v, min), max);
    }
}
