using System.IO;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    /// <summary>
    /// 本机 JSON 配置：{BaseDirectory}/Config/CTAutoSlicer/local.json（不落库）。
    /// </summary>
    public sealed class CTAutoSlicerLocalConfigService
    {
        private static readonly object SyncRoot = new();

        public static string ConfigFilePath =>
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config",
                "CTAutoSlicer",
                "local.json");

        public CTAutoSlicerLocalConfig Load()
        {
            lock (SyncRoot)
            {
                try
                {
                    var loaded = JsonHelper.ReadJsonFile<CTAutoSlicerLocalConfig>(ConfigFilePath);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
                catch
                {
                    // 损坏或格式异常时回退默认
                }

                return new CTAutoSlicerLocalConfig();
            }
        }

        public void Save(CTAutoSlicerLocalConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            lock (SyncRoot)
            {
                JsonHelper.WriteJsonFile(ConfigFilePath, config, encrypt: false);
            }
        }

        public PipelineRunMode LoadPipelineRunMode()
        {
            var mode = Load().PipelineRunMode;
            return mode is PipelineRunMode.SliceOnly or PipelineRunMode.AiAnnotateOnly
                ? mode
                : PipelineRunMode.SliceOnly;
        }

        public void SavePipelineRunMode(PipelineRunMode mode)
        {
            var config = Load();
            config.PipelineRunMode = mode is PipelineRunMode.SliceOnly or PipelineRunMode.AiAnnotateOnly
                ? mode
                : PipelineRunMode.SliceOnly;
            Save(config);
        }

        /// <summary>
        /// 读取手动标注导出目录；未配置时返回内置 UNC 默认路径。
        /// </summary>
        public string LoadManualMeasureExportFolder()
        {
            var saved = Load().ManualMeasureExportFolder?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(saved)
                ? ManualMeasureExportHelper.DefaultManualExportFolder
                : saved;
        }

        public void SaveManualMeasureExportFolder(string? folder)
        {
            var config = Load();
            var trimmed = folder?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                trimmed = ManualMeasureExportHelper.DefaultManualExportFolder;
            }

            if (string.Equals(
                    config.ManualMeasureExportFolder?.Trim(),
                    trimmed,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            config.ManualMeasureExportFolder = trimmed;
            Save(config);
        }

        public string? LoadOfflineCtDllCachedVersion()
        {
            var version = Load().OfflineCtDllCachedVersion?.Trim();
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }

        public void SaveOfflineCtDllCachedVersion(string? version)
        {
            var config = Load();
            var trimmed = version?.Trim() ?? string.Empty;
            if (string.Equals(
                    config.OfflineCtDllCachedVersion?.Trim(),
                    trimmed,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            config.OfflineCtDllCachedVersion = trimmed;
            Save(config);
        }
    }
}
