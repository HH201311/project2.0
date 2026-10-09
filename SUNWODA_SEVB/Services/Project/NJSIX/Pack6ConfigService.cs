using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.NJSIX;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.Services.Project.NJSIX
{
    /// <summary>从 project_setting 读写 PACK 六面检运行参数。</summary>
    public sealed class Pack6ConfigService : INJSIXPack6ConfigService
    {
        public const string SettingLocalPath = "LocalPathPre";
        public const string SettingYOk = "YOkRootPath";
        public const string SettingZOk = "ZOkRootPath";
        public const string SettingTopPath = "TopCamPath"; // 兼容旧键 → Y
        public const string SettingBottomPath = "BottomCamPath"; // 兼容旧键 → Z
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
        public const string SettingProductSNMinLen = "ProductSNMinLength";
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
                // 旧默认路径迁移到 E: 现场目录，并写回库
                if (string.Equals(config.LocalPathPre, @"D:\NJSIX_Pack6", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(config.LocalPathPre, @"D:\六面检上位机软件", StringComparison.OrdinalIgnoreCase))
                {
                    config.LocalPathPre = @"E:\六面检上位机软件";
                    await UpsertAsync(SettingLocalPath, vm, config.LocalPathPre, "string", "结果根目录");
                }
                config.YOkRootPath = ReadString(map, SettingYOk,
                    ReadString(map, SettingTopPath, config.YOkRootPath));
                config.ZOkRootPath = ReadString(map, SettingZOk,
                    ReadString(map, SettingBottomPath, config.ZOkRootPath));
                config.SideCamPath = ReadString(map, SettingSidePath, config.SideCamPath);

                // 枣庄残留路径 → 南京 Z/T/S；写回库
                if (IsLegacyZzTopRoot(config.YOkRootPath))
                {
                    config.YOkRootPath = @"Z:\";
                    await UpsertAsync(SettingYOk, vm, config.YOkRootPath, "string", "上盖OK根(Z:)");
                }
                if (IsLegacyZzBottomRoot(config.ZOkRootPath))
                {
                    config.ZOkRootPath = @"T:\";
                    await UpsertAsync(SettingZOk, vm, config.ZOkRootPath, "string", "底部OK根(T:)");
                }
                if (string.IsNullOrWhiteSpace(config.SideCamPath) ||
                    IsLegacyZzSideRoot(config.SideCamPath))
                {
                    config.SideCamPath = @"S:\";
                    await UpsertAsync(SettingSidePath, vm, config.SideCamPath, "string", "四周OK根(S:)");
                }
                config.UseSimulation = ReadBool(map, SettingSimulation, config.UseSimulation);
                config.PackTimeoutMinutes = Clamp(ReadInt(map, SettingTimeout, config.PackTimeoutMinutes), 1, 120);
                config.PackMissingTimeoutSeconds = Clamp(
                    ReadInt(map, SettingMissingTimeoutSeconds, config.PackMissingTimeoutSeconds), 5, 3600);
                // 旧默认 30 秒 / 5 分钟(300)：升为 10 分钟
                if (config.PackMissingTimeoutSeconds is 30 or 300)
                {
                    config.PackMissingTimeoutSeconds = 600;
                    await UpsertAsync(SettingMissingTimeoutSeconds, vm, "600", "int", "缺张最长等待秒(默认600=10分钟)");
                }
                config.CellType = ReadString(map, SettingCellType, config.CellType);
                config.ProductionLine = ReadString(map, SettingLine, config.ProductionLine);
                config.Workshop = ReadString(map, SettingWorkshop, config.Workshop);
                if (string.Equals(config.Workshop, "ZZ", StringComparison.OrdinalIgnoreCase))
                {
                    config.Workshop = "NJ";
                    await UpsertAsync(SettingWorkshop, vm, config.Workshop, "string", "车间");
                }
                config.ProductSNRule = ReadString(map, SettingProductSNRule, config.ProductSNRule);
                config.ExpectedWhiteLabelVersion = ReadString(
                    map, SettingExpectedWhiteLabelVersion, config.ExpectedWhiteLabelVersion);
                // 南京现场当前数模为 M06；库内残留 M00 时自动迁移
                if (string.Equals(config.ExpectedWhiteLabelVersion, "M00", StringComparison.OrdinalIgnoreCase))
                {
                    config.ExpectedWhiteLabelVersion = "M06";
                    await UpsertAsync(SettingExpectedWhiteLabelVersion, vm, "M06", "string", "白标签数模版本号期望(M06)");
                }
                config.PartSNMinLength = Clamp(ReadInt(map, SettingPartSNMinLen, config.PartSNMinLength), 0, 64);
                if (map.ContainsKey(SettingProductSNExactLen))
                    config.ProductSNExactLength = Clamp(ReadInt(map, SettingProductSNExactLen, config.ProductSNExactLength), 0, 64);
                else if (map.ContainsKey(SettingProductSNMinLen))
                    config.ProductSNExactLength = Clamp(ReadInt(map, SettingProductSNMinLen, config.ProductSNExactLength), 0, 64);
                config.WhiteLabelKeywords = SanitizeOcrKeywords(
                    ReadString(map, SettingWhiteKeywords, config.WhiteLabelKeywords),
                    isNameplate: false);
                config.NameplateKeywords = SanitizeOcrKeywords(
                    ReadString(map, SettingNameplateKeywords, config.NameplateKeywords),
                    isNameplate: true);
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

                await EnsureAsync(map, SettingLocalPath, vm, config.LocalPathPre, "string", "结果根目录");
                await EnsureAsync(map, SettingYOk, vm, config.YOkRootPath, "string", "上盖OK根(Z:)");
                await EnsureAsync(map, SettingZOk, vm, config.ZOkRootPath, "string", "底部OK根(T:)");
                await EnsureAsync(map, SettingSidePath, vm, config.SideCamPath, "string", "四周OK根(S:)");
                await EnsureAsync(map, SettingSimulation, vm, config.UseSimulation ? "true" : "false", "bool", "模拟检测");
                await EnsureAsync(map, SettingTimeout, vm, config.PackTimeoutMinutes.ToString(), "int", "Pack超时分钟");
                await EnsureAsync(map, SettingMissingTimeoutSeconds, vm, config.PackMissingTimeoutSeconds.ToString(), "int", "缺张最长等待秒(默认600=10分钟)");
                await EnsureAsync(map, SettingCellType, vm, config.CellType, "string", "电芯型号");
                await EnsureAsync(map, SettingLine, vm, config.ProductionLine, "string", "产线");
                await EnsureAsync(map, SettingWorkshop, vm, config.Workshop, "string", "车间");
                await EnsureAsync(map, SettingProductSNRule, vm, config.ProductSNRule, "string", "产品编码规则");
                await EnsureAsync(map, SettingExpectedWhiteLabelVersion, vm, config.ExpectedWhiteLabelVersion, "string", "白标签数模版本号期望(M06)");
                await EnsureAsync(map, SettingPartSNMinLen, vm, config.PartSNMinLength.ToString(), "int", "序列码最短长度");
                await EnsureAsync(map, SettingProductSNExactLen, vm, config.ProductSNExactLength.ToString(), "int", "产品编码必须位数");
                await EnsureAsync(map, SettingWhiteKeywords, vm, config.WhiteLabelKeywords, "string", "白标签文件名关键词");
                await EnsureAsync(map, SettingNameplateKeywords, vm, config.NameplateKeywords, "string", "铭牌文件名关键词");
                await EnsureAsync(map, SettingProductCodeProfile, vm, config.ProductCodeProfile, "string", "产品编码档位");
                await EnsureAsync(map, SettingIngressLookbackDays, vm, config.IngressLookbackDays.ToString(), "int", "掉线补扫回看天数");
                await EnsureAsync(map, SettingIngressPollSeconds, vm, config.IngressPollSeconds.ToString(), "int", "定时补扫间隔秒");
                await EnsureAsync(map, SettingStoreResultImageOK, vm, config.StoreResultImageOK ? "true" : "false", "bool", "保存OK结果图");
                await EnsureAsync(map, SettingStoreResultImageNG, vm, config.StoreResultImageNG ? "true" : "false", "bool", "保存NG结果图");
                await EnsureAsync(map, SettingImageArchiveRetainYears, vm, config.ImageArchiveRetainYears.ToString(), "int", "图像保留年数");
                await EnsureAsync(map, SettingImageArchiveDiskUsagePercent, vm, config.ImageArchiveDiskUsagePercent.ToString(), "int", "磁盘占用清理阈值百分比");
                await EnsureAsync(map, SettingPlcDeviceName, vm, config.PlcDeviceName, "string", "平台plc_config设备名");
                await EnsureAsync(map, SettingPlcNgAlarmBitAddress, vm, config.PlcNgAlarmBitAddress, "string", "NG报警位D1996.0");
                await EnsureAsync(map, SettingPlcHeartbeatOutBitAddress, vm, config.PlcHeartbeatOutBitAddress, "string", "本机心跳位D1996.1");
                await EnsureAsync(map, SettingPlcHeartbeatInBitAddress, vm, config.PlcHeartbeatInBitAddress, "string", "对方心跳位D1996.2");
                await EnsureAsync(map, SettingPlcHeartbeatIntervalMs, vm, config.PlcHeartbeatIntervalMs.ToString(), "int", "本机心跳翻转间隔毫秒");
                await EnsureAsync(map, SettingPlcHeartbeatTimeoutMs, vm, config.PlcHeartbeatTimeoutMs.ToString(), "int", "对方心跳超时毫秒");

                Current = config;
                _logger.Info(
                    $"Pack6配置已加载：模拟={config.UseSimulation}，产品档位={config.ProductCodeProfile}，前缀={config.ExpectedPrefix10}",
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
                await UpsertAsync(SettingYOk, vm, config.YOkRootPath, "string", "上盖OK根(Z:)");
                await UpsertAsync(SettingZOk, vm, config.ZOkRootPath, "string", "底部OK根(T:)");
                await UpsertAsync(SettingSidePath, vm, config.SideCamPath, "string", "四周OK根(S:)");
                await UpsertAsync(SettingSimulation, vm, config.UseSimulation ? "true" : "false", "bool", "模拟检测");
                await UpsertAsync(SettingTimeout, vm, config.PackTimeoutMinutes.ToString(), "int", "Pack超时分钟");
                await UpsertAsync(SettingMissingTimeoutSeconds, vm, config.PackMissingTimeoutSeconds.ToString(), "int", "缺张最长等待秒(默认600=10分钟)");
                await UpsertAsync(SettingCellType, vm, config.CellType, "string", "电芯型号");
                await UpsertAsync(SettingLine, vm, config.ProductionLine, "string", "产线");
                await UpsertAsync(SettingWorkshop, vm, config.Workshop, "string", "车间");
                await UpsertAsync(SettingProductSNRule, vm, config.ProductSNRule, "string", "产品编码规则");
                await UpsertAsync(SettingExpectedWhiteLabelVersion, vm, config.ExpectedWhiteLabelVersion, "string", "白标签数模版本号期望(M06)");
                await UpsertAsync(SettingPartSNMinLen, vm, config.PartSNMinLength.ToString(), "int", "序列码最短长度");
                await UpsertAsync(SettingProductSNExactLen, vm, config.ProductSNExactLength.ToString(), "int", "产品编码必须位数");
                await UpsertAsync(SettingWhiteKeywords, vm, config.WhiteLabelKeywords, "string", "白标签文件名关键词");
                await UpsertAsync(SettingNameplateKeywords, vm, config.NameplateKeywords, "string", "铭牌文件名关键词");
                await UpsertAsync(SettingProductCodeProfile, vm, config.ProductCodeProfile, "string", "产品编码档位");
                await UpsertAsync(SettingIngressLookbackDays, vm, config.IngressLookbackDays.ToString(), "int", "掉线补扫回看天数");
                await UpsertAsync(SettingIngressPollSeconds, vm, config.IngressPollSeconds.ToString(), "int", "定时补扫间隔秒");
                await UpsertAsync(SettingStoreResultImageOK, vm, config.StoreResultImageOK ? "true" : "false", "bool", "保存OK结果图");
                await UpsertAsync(SettingStoreResultImageNG, vm, config.StoreResultImageNG ? "true" : "false", "bool", "保存NG结果图");
                await UpsertAsync(SettingImageArchiveRetainYears, vm, config.ImageArchiveRetainYears.ToString(), "int", "图像保留年数");
                await UpsertAsync(SettingImageArchiveDiskUsagePercent, vm, config.ImageArchiveDiskUsagePercent.ToString(), "int", "磁盘占用清理阈值百分比");
                await UpsertAsync(SettingPlcDeviceName, vm, config.PlcDeviceName, "string", "平台plc_config设备名");
                await UpsertAsync(SettingPlcNgAlarmBitAddress, vm, config.PlcNgAlarmBitAddress, "string", "NG报警位D1996.0");
                await UpsertAsync(SettingPlcHeartbeatOutBitAddress, vm, config.PlcHeartbeatOutBitAddress, "string", "本机心跳位D1996.1");
                await UpsertAsync(SettingPlcHeartbeatInBitAddress, vm, config.PlcHeartbeatInBitAddress, "string", "对方心跳位D1996.2");
                await UpsertAsync(SettingPlcHeartbeatIntervalMs, vm, config.PlcHeartbeatIntervalMs.ToString(), "int", "本机心跳翻转间隔毫秒");
                await UpsertAsync(SettingPlcHeartbeatTimeoutMs, vm, config.PlcHeartbeatTimeoutMs.ToString(), "int", "对方心跳超时毫秒");
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

        /// <summary>枣庄上盖盘：Y:\ 或 Y:\OK原图 等。</summary>
        private static bool IsLegacyZzTopRoot(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            path.TrimStart().StartsWith(@"Y:\", StringComparison.OrdinalIgnoreCase);

        /// <summary>枣庄底部曾写在 Z:\OK原图；南京 Z:\ 已用于上盖，故 ZOk 仍为 Z: 视为残留。</summary>
        private static bool IsLegacyZzBottomRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return true;
            var p = path.Trim();
            if (p.StartsWith(@"T:\", StringComparison.OrdinalIgnoreCase))
                return false;
            return p.StartsWith(@"Z:\", StringComparison.OrdinalIgnoreCase) ||
                   p.StartsWith(@"Y:\", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLegacyZzSideRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return true;
            var p = path.Trim();
            if (p.StartsWith(@"S:\", StringComparison.OrdinalIgnoreCase))
                return false;
            // 空/其它盘视为需纠正
            return true;
        }

        /// <summary>
        /// 去掉易撞 SN 的短关键词（F1/F3/white 等）。标准命名已按相机张序路由，不再依赖这些短码。
        /// </summary>
        private static string SanitizeOcrKeywords(string keywordsCsv, bool isNameplate)
        {
            if (string.IsNullOrWhiteSpace(keywordsCsv))
                return isNameplate
                    ? "铭牌;nameplate;前-3;前3;Front3"
                    : "白标签;whiteLabel;前-1;前1;Front1";

            var banned = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "F1", "F3", "white", "F2", "F4"
            };
            var kept = keywordsCsv
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(k => k.Length >= 2 && !banned.Contains(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (kept.Count == 0)
            {
                return isNameplate
                    ? "铭牌;nameplate;前-3;前3;Front3"
                    : "白标签;whiteLabel;前-1;前1;Front1";
            }

            return string.Join(';', kept);
        }
    }
}
