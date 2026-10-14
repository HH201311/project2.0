using System.IO;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.YWSIX;
using SUNWODA_SEVB.Core.Models.Project.YWSIX;

namespace SUNWODA_SEVB.Services.Project.YWSIX
{
    /// <summary>
    /// 第一步模拟视觉引擎：默认全 OK；文件名含 NG/缺陷 注入 NG。
    /// 不调用 YWX01Pack（第二步再接真实 DLL / OCR 机位路由）。
    /// </summary>
    public sealed class Pack6SimulationVisionEngine : IYWSIXPack6VisionEngine
    {
        private readonly ILoggerService<Pack6SimulationVisionEngine> _logger;
        private Pack6Config? _config;

        public Pack6SimulationVisionEngine(ILoggerService<Pack6SimulationVisionEngine> logger)
        {
            _logger = logger;
        }

        public Task InitializeAsync(Pack6Config config, CancellationToken ct = default)
        {
            _config = config;
            _logger.Info("YWSIX 模拟视觉引擎已初始化（本步不调 YWX01Pack）", false);
            return Task.CompletedTask;
        }

        public void RefreshRuntimeConfig(Pack6Config config)
        {
            if (config is null)
                return;
            _config = config;
        }

        public Task<Pack6FrameResult> DetectAsync(Pack6IngressItem item, CancellationToken ct = default)
        {
            var name = Path.GetFileName(item.FilePath) ?? string.Empty;
            var forceNg = name.Contains("NG", StringComparison.OrdinalIgnoreCase)
                          || name.Contains("缺陷", StringComparison.OrdinalIgnoreCase);
            var station = MapStation(item.QueueName, item.CameraName);
            var defects = forceNg
                ? new List<string> { $"{station}模拟NG" }
                : new List<string>();

            var mode = ResolveOcrMode(item, _config ?? new Pack6Config());
            var result = new Pack6FrameResult
            {
                FilePath = item.FilePath,
                QueueName = item.QueueName,
                ProductCode = item.ProductCode ?? "UNKNOWN",
                CameraName = item.CameraName,
                ShotIndex = item.ShotIndex,
                FolderSn = Pack6FileNameParser.ResolveFolderSn(item.FilePath) ?? item.ProductCode ?? string.Empty,
                IsOk = defects.Count == 0,
                Defects = defects,
                StationName = station,
                FromSimulation = true,
                OcrMode = mode,
                EngineMessage = "模拟（本步不调 DLL）"
            };
            return Task.FromResult(result);
        }

        public void Shutdown()
        {
            _config = null;
        }

        /// <summary>
        /// 第一步：暂不按机位路由 OCR（第二步接 YWX01Pack 时再定白标签/铭牌/上盖/PIN 对应张）。
        /// </summary>
        internal static Pack6OcrMode ResolveOcrMode(Pack6IngressItem item, Pack6Config config)
        {
            _ = item;
            _ = config;
            return Pack6OcrMode.None;
        }

        internal static string MapStation(string queueName, string? cameraName = null)
        {
            if (!string.IsNullOrWhiteSpace(cameraName) &&
                Pack6CameraCatalog.TryGetByCameraName(cameraName, out var byCam))
                return byCam.DisplayTitle;

            return queueName switch
            {
                "TopQueue" => "顶面相机",
                "FrontQueue" => "前侧相机",
                "RearQueue" => "后侧相机",
                "LeftQueue" => "左侧相机",
                "RightQueue" => "右侧相机",
                "BottomQueue" => "底部相机",
                _ => queueName
            };
        }
    }
}
