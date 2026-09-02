using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using RemoteFileMonitor.Options;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Tool.Extension;

namespace SUNWODA_SEVB.Services.Project.CTOL
{
    /// <summary>
    /// 配置文件持久化服务。
    ///
    /// 负责将界面修改的监控参数写回 remote_file_monitor_config.json，
    /// 只更新可编辑字段，其余字段（AutoStart、WatcherBufferSizeBytes 等）保持原值。
    /// </summary>
    public class RemoteFileMonitorConfigService
    {
        private readonly string _configPath;
        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

        private readonly ILoggerService<RemoteFileMonitorConfigService> _logger;
        private readonly IProjectSettingRepository _projectSettingRepository;

        public RemoteFileMonitorConfigService(
            ILoggerService<RemoteFileMonitorConfigService> logger,
            IProjectSettingRepository projectSettingRepository
        )
        {
            _logger = logger;
            _projectSettingRepository = projectSettingRepository;
            _configPath = Path.GetFullPath("remote_file_monitor_config.json");
        }

        /// <summary>将指定监控路的可编辑参数更新到配置文件。</summary>
        public void UpdateTarget(MonitorTargetOptions opts)
        {
            if (!File.Exists(_configPath))
            {
                _logger.Warn(
                    "[更新远程监控配置文件] remote_file_monitor_config.json 配置文件不存在"
                );
                return;
            }

            var json = File.ReadAllText(_configPath);
            var root = JsonNode.Parse(json)!.AsObject();
            var targets = root["Monitor"]?["Targets"]?.AsArray();
            if (targets is null)
            {
                _logger.Warn(
                    "[更新远程监控配置文件] 未获取到 remote_file_monitor_config.json 中的 Targets"
                );
                return;
            }

            foreach (var node in targets)
            {
                if (node?["Id"]?.GetValue<string>() != opts.Id)
                    continue;

                var t = node.AsObject();
                t["RemotePath"] = opts.RemotePath;
                t["OutputDirectory"] = opts.OutputDirectory ?? string.Empty;
                t["Filter"] = opts.Filter;
                t["IncludeSubdirectories"] = opts.IncludeSubdirectories;
                t["PollingIntervalSeconds"] = opts.PollingIntervalSeconds;
                t["PollingOverlapSeconds"] = opts.PollingOverlapSeconds;
                break;
            }

            File.WriteAllText(_configPath, root.ToJsonString(WriteOptions));
            _logger.Info("[更新远程监控配置文件] remote_file_monitor_config.json 更新成功");
        }

        /// <summary>
        /// 将指定通道的间隙阈值持久化
        /// </summary>
        public void SaveThresholds(string monitorId, Dictionary<string, double> thresholds)
        {
            foreach (var (key, val) in thresholds)
            {
                if (
                    _projectSettingRepository.Exists(model =>
                        model.Name == $"{monitorId}{key}"
                        && model.BelongToVM == "VM_CTOLAutoDetectionPage"
                    )
                )
                {
                    _projectSettingRepository.UpdateSettingValue(
                        $"{monitorId}{key}",
                        "VM_CTOLAutoDetectionPage",
                        val
                    );
                }
                else
                {
                    _logger.Warn($"[保存阈值] {monitorId}{key} 参数在项目设置中不存在");
                    _projectSettingRepository.Add(
                        new ProjectSettingModel(
                            $"{monitorId}{key}",
                            "VM_CTOLAutoDetectionPage",
                            $"{val}",
                            "double",
                            1
                        )
                    );
                    _logger.Info($"[保存阈值] {monitorId}{key} 参数在项目设置中创建并保存当前值");
                }
            }
        }

        /// <summary>
        /// 从数据库读取指定通道的阈值；
        /// 节点不存在时返回 null（处理器将使用内置默认值）。
        /// </summary>
        public Dictionary<string, double>? LoadThresholds(string monitorId)
        {
            var thresholds = _projectSettingRepository.GetList(model =>
                model.BelongToVM == "VM_CTOLAutoDetectionPage"
                && model.Name.StartsWith($"{monitorId}Gap", StringComparison.OrdinalIgnoreCase)
            );
            var dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var threshold in thresholds)
            {
                if (threshold.Value is not null && threshold.Value.ToDouble() is double v)
                    dict[threshold.Name.Replace($"{monitorId}", "")] = v;
            }

            return dict.Count > 0 ? dict : null;
        }

        // ── 四路共享阈值 ──────────────────────────────────────────────

        /// <summary>
        /// 将四路共享阈值字典持久化
        /// </summary>
        public void SaveSharedThresholds(Dictionary<string, double> thresholds)
        {
            foreach (var (key, val) in thresholds)
            {
                if (
                    _projectSettingRepository.Exists(model =>
                        model.Name == $"{key}" && model.BelongToVM == "VM_CTOLAutoDetectionPage"
                    )
                )
                {
                    _projectSettingRepository.UpdateSettingValue(
                        $"{key}",
                        "VM_CTOLAutoDetectionPage",
                        val
                    );
                }
                else
                {
                    _logger.Warn($"[保存阈值] {key}参数在项目设置中不存在");
                    _projectSettingRepository.Add(
                        new ProjectSettingModel(
                            $"{key}",
                            "VM_CTOLAutoDetectionPage",
                            $"{val}",
                            "double",
                            1
                        )
                    );
                    _logger.Info($"[保存阈值] {key} 参数在项目设置中创建并保存当前值");
                }
            }
        }

        /// <summary>
        /// 读取共享阈值字典。
        /// 注意：只加载 Name 以 "Gap" 开头的项（含 Gap45/46 掉片、Gap48 翻折面积、Gap49–51 压伤）。
        /// 新阈值键必须用 Gap 前缀，否则保存后重启读不回来。
        /// </summary>
        public Dictionary<string, double>? LoadSharedThresholds()
        {
            var thresholds = _projectSettingRepository.GetList(model =>
                model.BelongToVM == "VM_CTOLAutoDetectionPage"
                && model.Name.StartsWith("Gap", StringComparison.OrdinalIgnoreCase)
            );
            var dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var threshold in thresholds)
            {
                if (threshold.Value is not null && threshold.Value.ToDouble() is double v)
                    dict[threshold.Name] = v;
            }

            return dict.Count > 0 ? dict : null;
        }

        /// <summary>持久化四路共享模式开关</summary>
        public void SaveSharedMode(bool isSharedMode)
        {
            if (_projectSettingRepository.Exists(model => model.Name == "GapThresholdSharedMode"))
            {
                _projectSettingRepository.UpdateSettingValue(
                    "GapThresholdSharedMode",
                    "VM_CTOLAutoDetectionPage",
                    isSharedMode
                );
            }
            else
            {
                _logger.Warn(
                    $"[保存阈值共享模式开关] GapThresholdSharedMode 参数在项目设置中不存在"
                );
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        "GapThresholdSharedMode",
                        "VM_CTOLAutoDetectionPage",
                        $"{isSharedMode}",
                        "bool",
                        1
                    )
                );
                _logger.Info(
                    $"[保存阈值共享模式开关] GapThresholdSharedMode 参数在项目设置中创建并保存当前值"
                );
            }
        }

        /// <summary>从配置文件读取四路共享模式开关</summary>
        public bool LoadSharedMode()
        {
            var gapThresholdSharedModeModel = _projectSettingRepository.GetByNameAndVM(
                "GapThresholdSharedMode",
                "VM_CTOLAutoDetectionPage"
            );
            if (gapThresholdSharedModeModel == null)
                return true;
            return gapThresholdSharedModeModel.Value.ToBool();
        }

        // ── 每日上传配置 ──────────────────────────────────────────────

        /// <summary>将每日上传配置持久化到配置文件的 "DailyUpload" 顶级节。</summary>
        public void SaveDailyUploadOptions(DailyUploadOptions opts)
        {
            if (!File.Exists(_configPath)) return;

            var json = File.ReadAllText(_configPath);
            var root = JsonNode.Parse(json)!.AsObject();

            var node = new JsonObject
            {
                ["Enabled"] = opts.Enabled,
                ["UploadTargetDirectory"] = opts.UploadTargetDirectory,
                ["TriggerHour"] = opts.TriggerHour,
                ["TriggerMinute"] = opts.TriggerMinute,
            };

            var typesArray = new JsonArray();
            foreach (var t in opts.UploadDefectTypes)
                typesArray.Add(t);
            node["UploadDefectTypes"] = typesArray;

            root["DailyUpload"] = node;
            File.WriteAllText(_configPath, root.ToJsonString(WriteOptions));
        }

        /// <summary>
        /// 从配置文件的 "DailyUpload" 节读取每日上传配置；
        /// 节点不存在时返回 null（调用方应使用 IOptions 注入的默认值）。
        /// </summary>
        public DailyUploadOptions? LoadDailyUploadOptions()
        {
            if (!File.Exists(_configPath)) return null;

            var json = File.ReadAllText(_configPath);
            var root = JsonNode.Parse(json)?.AsObject();
            var section = root?["DailyUpload"]?.AsObject();
            if (section is null) return null;

            var opts = new DailyUploadOptions
            {
                Enabled = section["Enabled"]?.GetValue<bool>() ?? false,
                UploadTargetDirectory = section["UploadTargetDirectory"]?.GetValue<string>() ?? "upload",
                TriggerHour = section["TriggerHour"]?.GetValue<int>() ?? 0,
                TriggerMinute = section["TriggerMinute"]?.GetValue<int>() ?? 5,
            };

            if (section["UploadDefectTypes"]?.AsArray() is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    var s = item?.GetValue<string>();
                    if (s is not null) opts.UploadDefectTypes.Add(s);
                }
            }

            return opts;
        }
    }
}
