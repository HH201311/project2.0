using System.Globalization;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.YWSIX;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.YWSIX;

namespace SUNWODA_SEVB.Services.Project.YWSIX
{
    /// <summary>从 project_setting 读写义乌 PACK 六面检运行参数。</summary>
    public sealed class Pack6ConfigService : IYWSIXPack6ConfigService
    {
        public const string SettingLocalPath = "LocalPathPre";
        public const string SettingYOk = "YOkRootPath";
        public const string SettingZOk = "ZOkRootPath";
        public const string SettingSidePath = "SideCamPath";
        public const string SettingSimulation = "UseSimulation";
        public const string SettingTimeout = "PackTimeoutMinutes";
        public const string SettingMissingTimeoutSeconds = "PackMissingTimeoutSeconds";
        public const string SettingCellType = "CellType";
        public const string SettingLine = "ProductionLine";
        public const string SettingWorkshop = "workship";
        public const string SettingProductSNRule = "ProductSNRule";
        public const string SettingExpectedWhiteLabelVersion = "ExpectedWhiteLabelVersion";
        public const string SettingPartSNMinLen = "PartSNMinLength";
        public const string SettingProductSNExactLen = "ProductSNExactLength";
        public const string SettingWhiteKeywords = "WhiteLabelKeywords";
        public const string SettingNameplateKeywords = "NameplateKeywords";
        public const string SettingProductCodeProfile = "ProductCodeProfile";
        public const string SettingIngressLookbackDays = "IngressLookbackDays";
        public const string SettingIngressPollSeconds = "IngressPollSeconds";
        public const string SettingStoreResultImageOK = "StoreResultImageOK";
        public const string SettingStoreResultImageNG = "StoreResultImageNG";
        public const string SettingImageArchiveRetainYears = "ImageArchiveRetainYears";
        public const string SettingImageArchiveDiskUsagePercent = "ImageArchiveDiskUsagePercent";
        public const string SettingPlcDeviceName = "PlcDeviceName";
        public const string SettingPlcNgAlarmBitAddress = "PlcNgAlarmBitAddress";
        public const string SettingPlcHeartbeatOutBitAddress = "PlcHeartbeatOutBitAddress";
        public const string SettingPlcHeartbeatInBitAddress = "PlcHeartbeatInBitAddress";
        public const string SettingPlcHeartbeatIntervalMs = "PlcHeartbeatIntervalMs";
        public const string SettingPlcHeartbeatTimeoutMs = "PlcHeartbeatTimeoutMs";
        public const string SettingTopCoverModelPath = "TopCoverModelPath";
        public const string SettingTopCoverUseGpu = "TopCoverUseGpu";
        public const string SettingPinLongAngleMax = "PinLongAngleMax";
        public const string SettingPinLongColDiffMax = "PinLongColDiffMax";
        public const string SettingPinLongRowDiffMin = "PinLongRowDiffMin";
        public const string SettingPinLongRowDiffMax = "PinLongRowDiffMax";
        public const string SettingPinShortRowDiffMax = "PinShortRowDiffMax";
        public const string SettingPinShortColDiffMin = "PinShortColDiffMin";
        public const string SettingPinShortColDiffMax = "PinShortColDiffMax";

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
                config.SaveImageRoot = ReadString(map, SettingYOk, config.SaveImageRoot);
                // 三键同步到同一 SaveImage 根
                config.YOkRootPath = config.SaveImageRoot;
                config.ZOkRootPath = config.SaveImageRoot;
                config.SideCamPath = config.SaveImageRoot;

                config.UseSimulation = ReadBool(map, SettingSimulation, config.UseSimulation);
                config.PackTimeoutMinutes = Clamp(ReadInt(map, SettingTimeout, config.PackTimeoutMinutes), 1, 120);
                config.PackMissingTimeoutSeconds = Clamp(
                    ReadInt(map, SettingMissingTimeoutSeconds, config.PackMissingTimeoutSeconds), 5, 3600);
                if (config.PackMissingTimeoutSeconds is 30 or 300)
                {
                    config.PackMissingTimeoutSeconds = 600;
                    await UpsertAsync(SettingMissingTimeoutSeconds, vm, "600", "int", "缺张最长等待秒");
                }

                config.CellType = ReadString(map, SettingCellType, config.CellType);
                config.ProductionLine = ReadString(map, SettingLine, config.ProductionLine);
                config.Workshop = ReadString(map, SettingWorkshop, config.Workshop);
                if (!string.Equals(config.Workshop, "YW", StringComparison.OrdinalIgnoreCase))
                {
                    config.Workshop = "YW";
                    await UpsertAsync(SettingWorkshop, vm, "YW", "string", "车间");
                }

                config.ProductSNRule = ReadString(map, SettingProductSNRule, config.ProductSNRule);
                config.ExpectedWhiteLabelVersion = ReadString(
                    map, SettingExpectedWhiteLabelVersion, config.ExpectedWhiteLabelVersion);
                if (string.Equals(config.ExpectedWhiteLabelVersion, "M06", StringComparison.OrdinalIgnoreCase))
                {
                    config.ExpectedWhiteLabelVersion = "M00";
                    await UpsertAsync(SettingExpectedWhiteLabelVersion, vm, "M00", "string", "白标签版本期望(M00)");
                }

                config.PartSNMinLength = Clamp(ReadInt(map, SettingPartSNMinLen, config.PartSNMinLength), 0, 64);
                config.ProductSNExactLength = Clamp(
                    ReadInt(map, SettingProductSNExactLen, config.ProductSNExactLength), 0, 64);
                config.WhiteLabelKeywords = ReadString(map, SettingWhiteKeywords, config.WhiteLabelKeywords);
                config.NameplateKeywords = ReadString(map, SettingNameplateKeywords, config.NameplateKeywords);
                config.ProductCodeProfile = Pack6ProductCodeCatalog.Resolve(
                    ReadString(map, SettingProductCodeProfile, config.ProductCodeProfile)).Name;
                config.IngressLookbackDays = Clamp(ReadInt(map, SettingIngressLookbackDays, config.IngressLookbackDays), 1, 30);
                config.IngressPollSeconds = Clamp(ReadInt(map, SettingIngressPollSeconds, config.IngressPollSeconds), 0, 3600);
                config.StoreResultImageOK = ReadBool(map, SettingStoreResultImageOK, config.StoreResultImageOK);
                config.StoreResultImageNG = ReadBool(map, SettingStoreResultImageNG, config.StoreResultImageNG);
                config.ImageArchiveRetainYears = Clamp(
                    ReadInt(map, SettingImageArchiveRetainYears, config.ImageArchiveRetainYears), 1, 10);
                config.ImageArchiveDiskUsagePercent = Clamp(
                    ReadInt(map, SettingImageArchiveDiskUsagePercent, config.ImageArchiveDiskUsagePercent), 50, 99);
                config.PlcDeviceName = ReadString(map, SettingPlcDeviceName, config.PlcDeviceName);
                config.PlcNgAlarmBitAddress = ReadString(map, SettingPlcNgAlarmBitAddress, config.PlcNgAlarmBitAddress);
                config.PlcHeartbeatOutBitAddress = ReadString(map, SettingPlcHeartbeatOutBitAddress, config.PlcHeartbeatOutBitAddress);
                config.PlcHeartbeatInBitAddress = ReadString(map, SettingPlcHeartbeatInBitAddress, config.PlcHeartbeatInBitAddress);
                config.PlcHeartbeatIntervalMs = Clamp(
                    ReadInt(map, SettingPlcHeartbeatIntervalMs, config.PlcHeartbeatIntervalMs), 200, 10000);
                config.PlcHeartbeatTimeoutMs = Clamp(
                    ReadInt(map, SettingPlcHeartbeatTimeoutMs, config.PlcHeartbeatTimeoutMs), 1000, 60000);

                config.TopCoverModelPath = ReadString(map, SettingTopCoverModelPath, config.TopCoverModelPath);
                config.TopCoverUseGpu = ReadBool(map, SettingTopCoverUseGpu, config.TopCoverUseGpu);
                config.PinLongAngleMax = ReadDouble(map, SettingPinLongAngleMax, config.PinLongAngleMax);
                config.PinLongColDiffMax = ReadDouble(map, SettingPinLongColDiffMax, config.PinLongColDiffMax);
                config.PinLongRowDiffMin = ReadDouble(map, SettingPinLongRowDiffMin, config.PinLongRowDiffMin);
                config.PinLongRowDiffMax = ReadDouble(map, SettingPinLongRowDiffMax, config.PinLongRowDiffMax);
                config.PinShortRowDiffMax = ReadDouble(map, SettingPinShortRowDiffMax, config.PinShortRowDiffMax);
                config.PinShortColDiffMin = ReadDouble(map, SettingPinShortColDiffMin, config.PinShortColDiffMin);
                config.PinShortColDiffMax = ReadDouble(map, SettingPinShortColDiffMax, config.PinShortColDiffMax);

                await EnsureAsync(map, SettingLocalPath, vm, config.LocalPathPre, "string", "结果根目录");
                await EnsureAsync(map, SettingYOk, vm, config.SaveImageRoot, "string", "SaveImage收图根");
                await EnsureAsync(map, SettingZOk, vm, config.SaveImageRoot, "string", "SaveImage收图根(兼容)");
                await EnsureAsync(map, SettingSidePath, vm, config.SaveImageRoot, "string", "SaveImage收图根(兼容)");
                await EnsureAsync(map, SettingSimulation, vm, config.UseSimulation ? "true" : "false", "bool", "模拟检测");
                await EnsureAsync(map, SettingTimeout, vm, config.PackTimeoutMinutes.ToString(), "int", "Pack超时分钟");
                await EnsureAsync(map, SettingMissingTimeoutSeconds, vm, config.PackMissingTimeoutSeconds.ToString(), "int", "缺张最长等待秒");
                await EnsureAsync(map, SettingCellType, vm, config.CellType, "string", "电芯型号");
                await EnsureAsync(map, SettingLine, vm, config.ProductionLine, "string", "产线");
                await EnsureAsync(map, SettingWorkshop, vm, config.Workshop, "string", "车间");
                await EnsureAsync(map, SettingProductSNRule, vm, config.ProductSNRule, "string", "产品编码规则");
                await EnsureAsync(map, SettingExpectedWhiteLabelVersion, vm, config.ExpectedWhiteLabelVersion, "string", "白标签版本期望(M00)");
                await EnsureAsync(map, SettingPartSNMinLen, vm, config.PartSNMinLength.ToString(), "int", "序列码最短长度");
                await EnsureAsync(map, SettingProductSNExactLen, vm, config.ProductSNExactLength.ToString(), "int", "产品编码必须位数");
                await EnsureAsync(map, SettingWhiteKeywords, vm, config.WhiteLabelKeywords, "string", "白标签关键词");
                await EnsureAsync(map, SettingNameplateKeywords, vm, config.NameplateKeywords, "string", "铭牌关键词");
                await EnsureAsync(map, SettingProductCodeProfile, vm, config.ProductCodeProfile, "string", "产品编码档位");
                await EnsureAsync(map, SettingIngressLookbackDays, vm, config.IngressLookbackDays.ToString(), "int", "回看天数");
                await EnsureAsync(map, SettingIngressPollSeconds, vm, config.IngressPollSeconds.ToString(), "int", "补扫间隔秒");
                await EnsureAsync(map, SettingStoreResultImageOK, vm, config.StoreResultImageOK ? "true" : "false", "bool", "保存OK结果图");
                await EnsureAsync(map, SettingStoreResultImageNG, vm, config.StoreResultImageNG ? "true" : "false", "bool", "保存NG结果图");
                await EnsureAsync(map, SettingImageArchiveRetainYears, vm, config.ImageArchiveRetainYears.ToString(), "int", "图像保留年数");
                await EnsureAsync(map, SettingImageArchiveDiskUsagePercent, vm, config.ImageArchiveDiskUsagePercent.ToString(), "int", "磁盘清理阈值%");
                await EnsureAsync(map, SettingPlcDeviceName, vm, config.PlcDeviceName, "string", "PLC设备名");
                await EnsureAsync(map, SettingPlcNgAlarmBitAddress, vm, config.PlcNgAlarmBitAddress, "string", "NG报警位");
                await EnsureAsync(map, SettingPlcHeartbeatOutBitAddress, vm, config.PlcHeartbeatOutBitAddress, "string", "本机心跳位");
                await EnsureAsync(map, SettingPlcHeartbeatInBitAddress, vm, config.PlcHeartbeatInBitAddress, "string", "对方心跳位");
                await EnsureAsync(map, SettingPlcHeartbeatIntervalMs, vm, config.PlcHeartbeatIntervalMs.ToString(), "int", "心跳间隔ms");
                await EnsureAsync(map, SettingPlcHeartbeatTimeoutMs, vm, config.PlcHeartbeatTimeoutMs.ToString(), "int", "心跳超时ms");
                await EnsureAsync(map, SettingTopCoverModelPath, vm, config.TopCoverModelPath, "string", "上盖YOLO模型路径");
                await EnsureAsync(map, SettingTopCoverUseGpu, vm, config.TopCoverUseGpu ? "true" : "false", "bool", "上盖GPU");
                await EnsureAsync(map, SettingPinLongAngleMax, vm, F(config.PinLongAngleMax), "double", "PIN长角度差上限");
                await EnsureAsync(map, SettingPinLongColDiffMax, vm, F(config.PinLongColDiffMax), "double", "PIN长列差上限");
                await EnsureAsync(map, SettingPinLongRowDiffMin, vm, F(config.PinLongRowDiffMin), "double", "PIN长行差下限");
                await EnsureAsync(map, SettingPinLongRowDiffMax, vm, F(config.PinLongRowDiffMax), "double", "PIN长行差上限");
                await EnsureAsync(map, SettingPinShortRowDiffMax, vm, F(config.PinShortRowDiffMax), "double", "PIN短行差上限");
                await EnsureAsync(map, SettingPinShortColDiffMin, vm, F(config.PinShortColDiffMin), "double", "PIN短列差下限");
                await EnsureAsync(map, SettingPinShortColDiffMax, vm, F(config.PinShortColDiffMax), "double", "PIN短列差上限");

                Current = config;
                _logger.Info(
                    $"YWSIX配置已加载：模拟={config.UseSimulation} SaveImage={config.SaveImageRoot} 版本={config.ExpectedWhiteLabelVersion}",
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
            // 三键同步
            config.YOkRootPath = config.SaveImageRoot;
            config.ZOkRootPath = config.SaveImageRoot;
            config.SideCamPath = config.SaveImageRoot;
            Current = config;
            await _dbLock.WaitAsync();
            try
            {
                var vm = Pack6Config.VmClassName;
                await UpsertAsync(SettingLocalPath, vm, config.LocalPathPre, "string", "结果根目录");
                await UpsertAsync(SettingYOk, vm, config.SaveImageRoot, "string", "SaveImage收图根");
                await UpsertAsync(SettingZOk, vm, config.SaveImageRoot, "string", "SaveImage收图根(兼容)");
                await UpsertAsync(SettingSidePath, vm, config.SaveImageRoot, "string", "SaveImage收图根(兼容)");
                await UpsertAsync(SettingSimulation, vm, config.UseSimulation ? "true" : "false", "bool", "模拟检测");
                await UpsertAsync(SettingTimeout, vm, config.PackTimeoutMinutes.ToString(), "int", "Pack超时分钟");
                await UpsertAsync(SettingMissingTimeoutSeconds, vm, config.PackMissingTimeoutSeconds.ToString(), "int", "缺张最长等待秒");
                await UpsertAsync(SettingCellType, vm, config.CellType, "string", "电芯型号");
                await UpsertAsync(SettingLine, vm, config.ProductionLine, "string", "产线");
                await UpsertAsync(SettingWorkshop, vm, config.Workshop, "string", "车间");
                await UpsertAsync(SettingProductSNRule, vm, config.ProductSNRule, "string", "产品编码规则");
                await UpsertAsync(SettingExpectedWhiteLabelVersion, vm, config.ExpectedWhiteLabelVersion, "string", "白标签版本期望(M00)");
                await UpsertAsync(SettingPartSNMinLen, vm, config.PartSNMinLength.ToString(), "int", "序列码最短长度");
                await UpsertAsync(SettingProductSNExactLen, vm, config.ProductSNExactLength.ToString(), "int", "产品编码必须位数");
                await UpsertAsync(SettingWhiteKeywords, vm, config.WhiteLabelKeywords, "string", "白标签关键词");
                await UpsertAsync(SettingNameplateKeywords, vm, config.NameplateKeywords, "string", "铭牌关键词");
                await UpsertAsync(SettingProductCodeProfile, vm, config.ProductCodeProfile, "string", "产品编码档位");
                await UpsertAsync(SettingIngressLookbackDays, vm, config.IngressLookbackDays.ToString(), "int", "回看天数");
                await UpsertAsync(SettingIngressPollSeconds, vm, config.IngressPollSeconds.ToString(), "int", "补扫间隔秒");
                await UpsertAsync(SettingStoreResultImageOK, vm, config.StoreResultImageOK ? "true" : "false", "bool", "保存OK结果图");
                await UpsertAsync(SettingStoreResultImageNG, vm, config.StoreResultImageNG ? "true" : "false", "bool", "保存NG结果图");
                await UpsertAsync(SettingImageArchiveRetainYears, vm, config.ImageArchiveRetainYears.ToString(), "int", "图像保留年数");
                await UpsertAsync(SettingImageArchiveDiskUsagePercent, vm, config.ImageArchiveDiskUsagePercent.ToString(), "int", "磁盘清理阈值%");
                await UpsertAsync(SettingPlcDeviceName, vm, config.PlcDeviceName, "string", "PLC设备名");
                await UpsertAsync(SettingPlcNgAlarmBitAddress, vm, config.PlcNgAlarmBitAddress, "string", "NG报警位");
                await UpsertAsync(SettingPlcHeartbeatOutBitAddress, vm, config.PlcHeartbeatOutBitAddress, "string", "本机心跳位");
                await UpsertAsync(SettingPlcHeartbeatInBitAddress, vm, config.PlcHeartbeatInBitAddress, "string", "对方心跳位");
                await UpsertAsync(SettingPlcHeartbeatIntervalMs, vm, config.PlcHeartbeatIntervalMs.ToString(), "int", "心跳间隔ms");
                await UpsertAsync(SettingPlcHeartbeatTimeoutMs, vm, config.PlcHeartbeatTimeoutMs.ToString(), "int", "心跳超时ms");
                await UpsertAsync(SettingTopCoverModelPath, vm, config.TopCoverModelPath, "string", "上盖YOLO模型路径");
                await UpsertAsync(SettingTopCoverUseGpu, vm, config.TopCoverUseGpu ? "true" : "false", "bool", "上盖GPU");
                await UpsertAsync(SettingPinLongAngleMax, vm, F(config.PinLongAngleMax), "double", "PIN长角度差上限");
                await UpsertAsync(SettingPinLongColDiffMax, vm, F(config.PinLongColDiffMax), "double", "PIN长列差上限");
                await UpsertAsync(SettingPinLongRowDiffMin, vm, F(config.PinLongRowDiffMin), "double", "PIN长行差下限");
                await UpsertAsync(SettingPinLongRowDiffMax, vm, F(config.PinLongRowDiffMax), "double", "PIN长行差上限");
                await UpsertAsync(SettingPinShortRowDiffMax, vm, F(config.PinShortRowDiffMax), "double", "PIN短行差上限");
                await UpsertAsync(SettingPinShortColDiffMin, vm, F(config.PinShortColDiffMin), "double", "PIN短列差下限");
                await UpsertAsync(SettingPinShortColDiffMax, vm, F(config.PinShortColDiffMax), "double", "PIN短列差上限");
                _logger.Info("YWSIX运行参数已保存", false);
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

        private static string F(double v) => v.ToString(CultureInfo.InvariantCulture);

        private static string ReadString(Dictionary<string, ProjectSettingModel> map, string key, string fallback) =>
            map.TryGetValue(key, out var row) && !string.IsNullOrWhiteSpace(row.Value) ? row.Value.Trim() : fallback;

        private static int ReadInt(Dictionary<string, ProjectSettingModel> map, string key, int fallback) =>
            map.TryGetValue(key, out var row) && int.TryParse(row.Value, out var v) ? v : fallback;

        private static double ReadDouble(Dictionary<string, ProjectSettingModel> map, string key, double fallback)
        {
            if (!map.TryGetValue(key, out var row) || string.IsNullOrWhiteSpace(row.Value))
                return fallback;
            if (double.TryParse(row.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
            return double.TryParse(row.Value, out v) ? v : fallback;
        }

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
