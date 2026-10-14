using System.IO;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    /// <summary>P0 模拟视觉引擎：默认全 OK，可按文件名含 NG 注入缺陷（联调用）。</summary>
    public sealed class Pack6SimulationVisionEngine : IZZSIXPack6VisionEngine
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
            _logger.Info("Pack6 模拟视觉引擎已初始化（未加载 ZZPack.AI）", false);
            return Task.CompletedTask;
        }

        public Task<Pack6FrameResult> DetectAsync(Pack6IngressItem item, CancellationToken ct = default)
        {
            var name = Path.GetFileName(item.FilePath) ?? string.Empty;
            var forceNg = name.Contains("NG", StringComparison.OrdinalIgnoreCase)
                          || name.Contains("缺陷", StringComparison.OrdinalIgnoreCase);
            var defects = forceNg
                ? new List<string> { $"{MapStation(item.QueueName)}模拟NG" }
                : new List<string>();

            var result = new Pack6FrameResult
            {
                FilePath = item.FilePath,
                QueueName = item.QueueName,
                ProductCode = item.ProductCode ?? "UNKNOWN",
                IsOk = defects.Count == 0,
                Defects = defects,
                StationName = MapStation(item.QueueName),
                FromSimulation = true
            };
            return Task.FromResult(result);
        }

        public void Shutdown()
        {
            _config = null;
        }

        internal static string MapStation(string queueName) => queueName switch
        {
            "UpperQueue" => "上盖",
            "Bottom1Queue" => "底部1",
            "Bottom2Queue" => "底部2",
            "Bottom3Queue" => "底部3",
            "FrontQueue" => "前面",
            "RearQueue" => "后面",
            "LeftQueue" => "左面",
            "RightQueue" => "右面",
            _ => queueName
        };
    }
}
