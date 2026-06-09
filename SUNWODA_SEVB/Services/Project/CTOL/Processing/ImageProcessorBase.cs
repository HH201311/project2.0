#define C01

using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml.Drawing.Charts;
using HalconDotNet;
using RemoteFileMonitor.Abstractions;
using RemoteFileMonitor.Models;
using RemoteFileMonitor.Options;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models.Project.CoatingOPMM;
using Xceed.Wpf.Toolkit;
#if C06
using Yolo11seg_CT06;
using ZZC6OnlineCTModel;
#else
using Yolo11seg_CT01;
using ZZC1OnlineCTModel;
#endif

namespace SUNWODA_SEVB.Services.Project.CTOL.Processing
{
    /// <summary>
    /// PNG 处理器公共基类，封装：
    ///   - CanProcess 路由规则（匹配指定 monitorId + .png 扩展名 + 排除已处理文件）
    ///   - 输出路径镜像目录结构构建（将 _origin 替换为 _result）
    ///   - 文件不存在检测
    ///   - 统一日志格式
    ///   - UI 预览通知（ProcessingResultService）
    ///   - 向 BarcodeGroupingService 汇报检测结果（通过 FileProcessingResult 返回值）
    ///
    /// <b>路径来源：</b>从 <see cref="IMonitorManager.Controls"/> 实时读取 Options，
    /// 确保 UI 修改并调用 UpdateOptions 后输出路径立即生效（不依赖 IOptionsMonitor 快照）。
    ///
    /// 子类只需实现 <see cref="ProcessImage"/> 中的图像变换与检测逻辑。
    /// </summary>
    public abstract class ImageProcessorBase : IFileProcessor
    {
        /// <summary>本处理器只响应此 monitorId 的文件</summary>
        protected abstract string TargetMonitorId { get; }

        // 每路工位独立信号量，站内串行、跨站并行。
        // 同一通道的多张图按队列顺序依次推理，保证 ZZC6CTModel/ZZC1CTModel 实例内 OpenCV Mat 不被并发访问；
        // 不同通道各自持有独立 ZZC6CTModel/ZZC1CTModel 实例，可安全并行，不互相阻塞。
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _aiChannelSemaphores = new();

#if C06
        protected ZZC6CTModel AI { get; } = new ZZC6CTModel();
#else
        protected ZZC1CTModel AI { get; } = new ZZC1CTModel();
#endif

        protected IPredictor? Model;

        // 直接持有 Manager，从 Controls[].Options 读取运行时参数，而非配置快照
        private readonly IMonitorManager _manager;
        private readonly ProcessingResultService _resultService;
        private readonly BarcodeGroupingService _groupingService;
        private readonly GapThresholdService _gapThresholds;
        private readonly GapCsvService _gapCsv;
        protected ILoggerService Logger { get; }

        protected ImageProcessorBase(
            IMonitorManager manager,
            ProcessingResultService resultService,
            BarcodeGroupingService groupingService,
            GapThresholdService gapThresholds,
            GapCsvService gapCsv,
            ILoggerService logger
        )
        {
            _manager = manager;
            _resultService = resultService;
            _groupingService = groupingService;
            _gapThresholds = gapThresholds;
            _gapCsv = gapCsv;
            Logger = logger;
        }

        // ==================== 间隙判断阈值 ====================
        // 说明：视野编号 1-4 对应电芯四个扫描位置；X/Y 对应拍摄方向。
        // 运行时覆盖值由 GapThresholdService 在每次 ProcessAsync 开始时注入（界面停机期间可修改）。

        // --- 视野 1-X ---
        protected double Gap1Limit = 0.5; // 1-X OH 下限
        protected double Gap2Limit = 2.75; // 1-X OH 上限
        protected double Gap3Limit = 2.2; // 1-X 负极差阈值
        protected double Gap4Limit = 0.64; // 1-X 负极边缘间隙
        protected double Gap5Limit = 0.45; // 1-X 正极边缘间隙
        protected double Gap6Limit = 0.45; // 1-X 正极中间间隙

        // --- 视野 1-Y ---
        protected double Gap7Limit = 0.5; // 1-Y OH 下限
        protected double Gap8Limit = 2.4; // 1-Y OH 上限
        protected double Gap9Limit = 2.4; // 1-Y 负极差阈值
        protected double Gap10Limit = 0.64; // 1-Y 负极边缘间隙
        protected double Gap11Limit = 0.45; // 1-Y 正极边缘间隙
        protected double Gap12Limit = 0.45; // 1-Y 正极中间间隙

        // --- 视野 2-X ---
        protected double Gap13Limit = 0.5; // 2-X OH 下限
        protected double Gap14Limit = 2.0; // 2-X OH 上限
        protected double Gap15Limit = 0.64; // 2-X 负极边缘间隙
        protected double Gap16Limit = 0.45; // 2-X 正极边缘间隙
        protected double Gap17Limit = 0.45; // 2-X 正极中间间隙

        // --- 视野 2-Y ---
        protected double Gap18Limit = 0.5; // 2-Y OH 下限
        protected double Gap19Limit = 2.4; // 2-Y OH 上限
        protected double Gap20Limit = 0.64; // 2-Y 负极边缘间隙
        protected double Gap21Limit = 0.45; // 2-Y 正极边缘间隙
        protected double Gap22Limit = 0.45; // 2-Y 正极中间间隙

        // --- 视野 3-X ---
        protected double Gap23Limit = 0.5; // 3-X OH 下限
        protected double Gap24Limit = 2.75; // 3-X OH 上限
        protected double Gap25Limit = 0.64; // 3-X 负极边缘间隙
        protected double Gap26Limit = 0.45; // 3-X 正极边缘间隙
        protected double Gap27Limit = 0.45; // 3-X 正极中间间隙

        // --- 视野 3-Y ---
        protected double Gap28Limit = 0.5; // 3-Y OH 下限
        protected double Gap29Limit = 2.4; // 3-Y OH 上限
        protected double Gap30Limit = 0.64; // 3-Y 负极边缘间隙
        protected double Gap31Limit = 0.45; // 3-Y 正极边缘间隙
        protected double Gap32Limit = 0.45; // 3-Y 正极中间间隙

        // --- 视野 4-X ---
        protected double Gap33Limit = 0.5; // 4-X OH 下限
        protected double Gap34Limit = 2.0; // 4-X OH 上限
        protected double Gap35Limit = 2.2; // 4-X 负极差阈值
        protected double Gap36Limit = 0.64; // 4-X 负极边缘间隙
        protected double Gap37Limit = 0.45; // 4-X 正极边缘间隙
        protected double Gap38Limit = 0.45; // 4-X 正极中间间隙

        // --- 视野 4-Y ---
        protected double Gap39Limit = 0.5; // 4-Y OH 下限
        protected double Gap40Limit = 2.4; // 4-Y OH 上限
        protected double Gap41Limit = 2.4; // 4-Y 负极差阈值
        protected double Gap42Limit = 0.64; // 4-Y 负极边缘间隙
        protected double Gap43Limit = 0.45; // 4-Y 正极边缘间隙
        protected double Gap44Limit = 0.45; // 4-Y 正极中间间隙

        // --- 掉片判断专用阈值（跨视野计算）---
        protected double Gap45Limit = 0.3; // 单张图内相邻正极层 OH 差值上限（单图掉片初判）
        protected double Gap46Limit = 0.3; // 四张 Y 图联合：|1Y+3Y−2Y−4Y| 上限
        protected double Gap47Limit = 0.5; // 四张 Y 图联合：|1Y−3Y| 或 |2Y−4Y| 上限

        // ==================== IFileProcessor ====================

        public bool CanProcess(string monitorId, string filePath) =>
            monitorId.Equals(TargetMonitorId, StringComparison.OrdinalIgnoreCase)
            && filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            && !Path.GetFileNameWithoutExtension(filePath)
                .Contains("_result_", StringComparison.OrdinalIgnoreCase);

        public async Task ProcessAsync(MonitoredFile file, CancellationToken ct)
        {
            if (!File.Exists(file.FilePath))
            {
                Logger.Warn($"[{file.MonitorId}] 源文件已消失，跳过：{file.FilePath}");
                return;
            }

            var target = FindTarget(file.MonitorId);
            ApplyThresholds(); // 运行前将界面配置的阈值写入本实例字段

            if (!IsOKDir(file.FilePath, target))
            {
                Logger.Warn($"[{file.MonitorId}] 非OK文件下图片，跳过：{file.FilePath}");
                return;
            }

            // outputBarcodeDir 在 ProcessImage 之前确定，确保算法异常时也能正确聚合
            var outputBarcodeDir = BuildOutputBarcodeDir(file.FilePath, target);

            // 提取视野类型（文件名第二段，如 "1-X""2-Y"），用于跨图 Y 掉片判断
            var (_, imageType) = ParseImageFileName(file.FilePath);

            bool isOk = true;
            string defectType = "OK";
            string outputPath = "";
            bool algorithmError = false;
            double[]? yOhValues = null;
            double[]? gapValues = null;

            // 取本通道专用信号量（同通道串行、跨通道并行），防止同一 ZZC6CTModel/ZZC1CTModel 实例并发推理
            var channelSemaphore = _aiChannelSemaphores.GetOrAdd(TargetMonitorId, _ => new SemaphoreSlim(1, 1));
            await channelSemaphore.WaitAsync(ct);
            try
            {
                (isOk, defectType, outputPath, yOhValues, gapValues) = ProcessImage(file.FilePath);

                var result = isOk ? "OK" : $"NG /{defectType}";
                algorithmError = (defectType == "算法异常") ? true : false;
                Logger.Info(
                    $"[{file.MonitorId}] 图片处理完成 [{result}]：{Path.GetFileName(file.FilePath)} → {outputPath}"
                );

                // 通知 UI 更新预览图和计数
                _resultService.NotifyProcessed(file.MonitorId, outputPath);
            }
            catch (Exception ex)
            {
                algorithmError = true;
                Logger.Error($"[{file.MonitorId}] 算法处理异常：{file.FilePath}", ex);
            }
            finally
            {
                channelSemaphore.Release();
            }

            // 通知业务层聚合服务记录本张结果（含算法异常标志，以触发整个 barcode 的异常归档）
            // imageType 与 yOhValues 供 BarcodeGroupingService 在 8 张聚合后逐层执行跨图 Y 掉片二次判断
            await _groupingService.RecordAsync(
                outputBarcodeDir,
                file.MonitorId,
                isOk,
                defectType,
                file.FilePath,
                algorithmError,
                imageType,
                algorithmError ? null : yOhValues,
                ct
            );

            // 将本张图片的间隙判断値写入当天 CSV
            var barcode = Path.GetFileNameWithoutExtension(file.FilePath).Split('_')[0];
            var imageName = Path.GetFileName(file.FilePath);
            var resultStr = isOk ? "OK" : $"NG_{defectType}";
            await _gapCsv.AppendAsync(
                barcode,
                file.MonitorId,
                imageName,
                resultStr,
                algorithmError ? null : gapValues,
                ct
            );
        }

        private protected bool IsOKDir(string sourcePath, MonitorTargetOptions? target)
        {
            if (target is null || string.IsNullOrWhiteSpace(target.OutputDirectory))
            {
                return false;
            }

            var sourceFullPath = Path.GetFullPath(sourcePath);

            var remoteRoot = string.IsNullOrWhiteSpace(target.RemotePath)
                ? string.Empty
                : Path.GetFullPath(target.RemotePath);

            var outputRoot = Path.GetFullPath(target.OutputDirectory);

            var relativeDir = string.Empty;

            if (!string.IsNullOrWhiteSpace(remoteRoot) &&
                sourceFullPath.StartsWith(
                    remoteRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = Path.GetRelativePath(remoteRoot, sourceFullPath);

                var parts = relativePath
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToList();

                // 目录结构：
                // 2026-04-25\OH不良\355126226K41300\file.png
                //
                // parts[0] = 2026-04-25
                // parts[1] = OH不良 / OK / 其他结果目录
                // parts[2] = barcode
                //
                // 判定是否是OK文件夹
                if (parts.Count >= 2)
                {
                    return parts[1] == "OK";
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        // ==================== 子类扩展点 ====================

        /// <summary>
        /// 对已加载的图像执行检测，返回判断结论。
        /// </summary>
        /// <param name="filePath">图像文件完整路径（用于提取视野类型标识，如 "1-X"、"2-Y"）</param>
        /// <returns>
        /// (isOk: true=OK / false=NG, defectType: 缺陷类型中文名，OK 时填 "OK")
        /// </returns>
        protected abstract (
            bool isOk,
            string defectType,
            string outputPath,
            double[]? yOhValues,
            double[]? gapValues
        ) ProcessImage(string filePath);

        // ==================== 路径构建 ====================

        /// <summary>
        /// 仅计算输出 barcode 目录（不含文件名），与结果无关，用于算法异常时也能正确聚合。
        /// </summary>
        private string BuildOutputBarcodeDir(string sourcePath, MonitorTargetOptions? target)
        {
            //if (target is null || string.IsNullOrWhiteSpace(target.OutputDirectory))
            //    return Path.GetDirectoryName(sourcePath) ?? ".";

            //var normalizedRoot = Path.GetFullPath(target.RemotePath)
            //    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            //var relativeSubPath = sourcePath.StartsWith(
            //    normalizedRoot,
            //    StringComparison.OrdinalIgnoreCase
            //)
            //    ? sourcePath[(normalizedRoot.Length + 1)..]
            //    : Path.GetFileName(sourcePath);

            //var relativeDir = Path.GetDirectoryName(relativeSubPath) ?? string.Empty;
            //var outputBase = Path.GetFullPath(target.OutputDirectory);
            //return string.IsNullOrEmpty(relativeDir)
            //    ? outputBase
            //    : Path.Combine(outputBase, relativeDir);

            if (target is null || string.IsNullOrWhiteSpace(target.OutputDirectory))
            {
                return Path.GetDirectoryName(sourcePath) ?? ".";
            }

            var sourceFullPath = Path.GetFullPath(sourcePath);

            var remoteRoot = string.IsNullOrWhiteSpace(target.RemotePath)
                ? string.Empty
                : Path.GetFullPath(target.RemotePath);

            var outputRoot = Path.GetFullPath(target.OutputDirectory);

            var relativeDir = string.Empty;

            if (!string.IsNullOrWhiteSpace(remoteRoot) &&
                sourceFullPath.StartsWith(
                    remoteRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = Path.GetRelativePath(remoteRoot, sourceFullPath);

                var parts = relativePath
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToList();

                // 目录结构：
                // 2026-04-25\OH不良\355126226K41300\file.png
                //
                // parts[0] = 2026-04-25
                // parts[1] = OH不良 / OK / 其他结果目录
                // parts[2] = barcode
                //
                // 初始输出统一改到 OK 文件夹下
                if (parts.Count >= 2)
                {
                    parts[1] = "OK";
                }

                var relativeDirParts = parts
                    .Take(parts.Count - 1)
                    .ToArray();

                relativeDir = relativeDirParts.Length == 0
                    ? string.Empty
                    : Path.Combine(relativeDirParts);
            }

            return string.IsNullOrWhiteSpace(relativeDir)
                ? outputRoot
                : Path.Combine(outputRoot, relativeDir);
        }

        private protected string BuildOutputPath(
            string sourcePath,
            MonitorTargetOptions? target,
            string resultCode
        )
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
            var ext = Path.GetExtension(sourcePath);

            // 将 _origin 后缀替换为 _result_{resultCode}；其他文件名直接追加 _result_{resultCode}
            // 示例：355125701G160046_1-Y_origin → 355125701G160046_1-Y_result_OK
            //       355125701G160046_1-Y        → 355125701G160046_1-Y_result_NG_OH不良
            var stem = nameWithoutExt.EndsWith("_origin", StringComparison.OrdinalIgnoreCase)
                ? nameWithoutExt[..^"_origin".Length]
                : nameWithoutExt;
            var outputFileName = $"{stem}_result_{resultCode}{ext}";

            if (target is null || string.IsNullOrWhiteSpace(target.OutputDirectory))
            {
                var sourceDir = Path.GetDirectoryName(sourcePath) ?? ".";
                return Path.Combine(sourceDir, outputFileName);
            }

            var sourceFullPath = Path.GetFullPath(sourcePath);

            var remoteRoot = string.IsNullOrWhiteSpace(target.RemotePath)
                ? string.Empty
                : Path.GetFullPath(target.RemotePath);

            var outputRoot = Path.GetFullPath(target.OutputDirectory);

            string relativeDir = string.Empty;

            if (!string.IsNullOrWhiteSpace(remoteRoot) &&
                sourceFullPath.StartsWith(
                    remoteRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = Path.GetRelativePath(remoteRoot, sourceFullPath);

                var parts = relativePath
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToList();

                // 目录结构：
                // 2026-04-25\OH不良\355126226K41300\file.png
                //
                // parts[0] = 2026-04-25
                // parts[1] = OH不良 / OK
                // parts[2] = 355126226K41300
                //
                // 初始都放到 OK 文件夹下
                if (parts.Count >= 2)
                {
                    parts[1] = "OK";
                }

                var relativeDirParts = parts
                    .Take(parts.Count - 1)
                    .ToArray();

                relativeDir = Path.Combine(relativeDirParts);
            }

            var outputDir = string.IsNullOrWhiteSpace(relativeDir)
                ? outputRoot
                : Path.Combine(outputRoot, relativeDir);

            return Path.Combine(outputDir, outputFileName);

            //var normalizedRoot = Path.GetFullPath(target.RemotePath)
            //    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            //var relativeSubPath = sourcePath.StartsWith(
            //    normalizedRoot,
            //    StringComparison.OrdinalIgnoreCase
            //)
            //    ? sourcePath[(normalizedRoot.Length + 1)..]
            //    : Path.GetFileName(sourcePath);

            //var relativeDir = Path.GetDirectoryName(relativeSubPath) ?? string.Empty;
            //var outputBase = Path.GetFullPath(target.OutputDirectory);
            //var outputDir = string.IsNullOrEmpty(relativeDir)
            //    ? outputBase
            //    : Path.Combine(outputBase, relativeDir);

            //return Path.Combine(outputDir, outputFileName);
        }

        ///// <summary>
        ///// 从文件名第二段提取视野类型标识（如 "1-X"、"2-Y"）。
        ///// 文件命名规则：{条码}_{视野}_{...}，拆分失败时返回空串。
        ///// </summary>
        //private static string ExtractImageType(string filePath)
        //{
        //    var parts = Path.GetFileNameWithoutExtension(filePath).Split('_');
        //    return parts.Length >= 2 ? parts[1] : string.Empty;
        //}

        // 从 MonitorControl.Options 读取——这是 UpdateOptions() 实际写入的对象
        private protected MonitorTargetOptions? FindTarget(string monitorId) =>
            _manager
                .Controls.FirstOrDefault(c =>
                    c.Id.Equals(monitorId, StringComparison.OrdinalIgnoreCase)
                )
                ?.Options;

        /// <summary>
        /// <summary>
        /// 将 <see cref="GapThresholdService"/> 中的覆盖值写入本实例的 Gap 字段。
        /// 在每次 <see cref="ProcessAsync"/> 开始时调用，确保界面修改的阈值即时生效。
        /// 字典中未包含的 Key 保持当前值（即类字段的默认值）。
        /// </summary>
        private void ApplyThresholds()
        {
            var d = _gapThresholds.Get(TargetMonitorId);
            if (d is null || d.Count == 0)
                return;

            if (d.TryGetValue("Gap1Limit", out var v))
                Gap1Limit = v;
            if (d.TryGetValue("Gap2Limit", out v))
                Gap2Limit = v;
            if (d.TryGetValue("Gap3Limit", out v))
                Gap3Limit = v;
            if (d.TryGetValue("Gap4Limit", out v))
                Gap4Limit = v;
            if (d.TryGetValue("Gap5Limit", out v))
                Gap5Limit = v;
            if (d.TryGetValue("Gap6Limit", out v))
                Gap6Limit = v;
            if (d.TryGetValue("Gap7Limit", out v))
                Gap7Limit = v;
            if (d.TryGetValue("Gap8Limit", out v))
                Gap8Limit = v;
            if (d.TryGetValue("Gap9Limit", out v))
                Gap9Limit = v;
            if (d.TryGetValue("Gap10Limit", out v))
                Gap10Limit = v;
            if (d.TryGetValue("Gap11Limit", out v))
                Gap11Limit = v;
            if (d.TryGetValue("Gap12Limit", out v))
                Gap12Limit = v;
            if (d.TryGetValue("Gap13Limit", out v))
                Gap13Limit = v;
            if (d.TryGetValue("Gap14Limit", out v))
                Gap14Limit = v;
            if (d.TryGetValue("Gap15Limit", out v))
                Gap15Limit = v;
            if (d.TryGetValue("Gap16Limit", out v))
                Gap16Limit = v;
            if (d.TryGetValue("Gap17Limit", out v))
                Gap17Limit = v;
            if (d.TryGetValue("Gap18Limit", out v))
                Gap18Limit = v;
            if (d.TryGetValue("Gap19Limit", out v))
                Gap19Limit = v;
            if (d.TryGetValue("Gap20Limit", out v))
                Gap20Limit = v;
            if (d.TryGetValue("Gap21Limit", out v))
                Gap21Limit = v;
            if (d.TryGetValue("Gap22Limit", out v))
                Gap22Limit = v;
            if (d.TryGetValue("Gap23Limit", out v))
                Gap23Limit = v;
            if (d.TryGetValue("Gap24Limit", out v))
                Gap24Limit = v;
            if (d.TryGetValue("Gap25Limit", out v))
                Gap25Limit = v;
            if (d.TryGetValue("Gap26Limit", out v))
                Gap26Limit = v;
            if (d.TryGetValue("Gap27Limit", out v))
                Gap27Limit = v;
            if (d.TryGetValue("Gap28Limit", out v))
                Gap28Limit = v;
            if (d.TryGetValue("Gap29Limit", out v))
                Gap29Limit = v;
            if (d.TryGetValue("Gap30Limit", out v))
                Gap30Limit = v;
            if (d.TryGetValue("Gap31Limit", out v))
                Gap31Limit = v;
            if (d.TryGetValue("Gap32Limit", out v))
                Gap32Limit = v;
            if (d.TryGetValue("Gap33Limit", out v))
                Gap33Limit = v;
            if (d.TryGetValue("Gap34Limit", out v))
                Gap34Limit = v;
            if (d.TryGetValue("Gap35Limit", out v))
                Gap35Limit = v;
            if (d.TryGetValue("Gap36Limit", out v))
                Gap36Limit = v;
            if (d.TryGetValue("Gap37Limit", out v))
                Gap37Limit = v;
            if (d.TryGetValue("Gap38Limit", out v))
                Gap38Limit = v;
            if (d.TryGetValue("Gap39Limit", out v))
                Gap39Limit = v;
            if (d.TryGetValue("Gap40Limit", out v))
                Gap40Limit = v;
            if (d.TryGetValue("Gap41Limit", out v))
                Gap41Limit = v;
            if (d.TryGetValue("Gap42Limit", out v))
                Gap42Limit = v;
            if (d.TryGetValue("Gap43Limit", out v))
                Gap43Limit = v;
            if (d.TryGetValue("Gap44Limit", out v))
                Gap44Limit = v;
            if (d.TryGetValue("Gap45Limit", out v))
                Gap45Limit = v;
            if (d.TryGetValue("Gap46Limit", out v))
                Gap46Limit = v;
            if (d.TryGetValue("Gap47Limit", out v))
                Gap47Limit = v;
        }

        // ==================== 检测判断辅助方法 ====================

        /// <summary>
        /// 核心 OK/NG 判断规则（移植自 NGOK 方法）。
        /// <para>判断优先级（由高到低）：</para>
        /// <list type="number">
        ///   <item>OH 越限（偏大 / 偏小）</item>
        ///   <item>极片层数异常（正极多片 / 少片；负极多片 / 少片）</item>
        ///   <item>间隙超差（负极边缘 / 正极边缘 / 正极中间）</item>
        ///   <item>负极差超差（仅 1-X / 1-Y / 4-X / 4-Y 视野参与）</item>
        ///   <item>负极压伤（neNGflag == 1）</item>
        ///   <item>Y 方向正极掉片（单图内相邻层 OH 差值超 Gap45Limit）</item>
        /// </list>
        /// </summary>
        /// <param name="positiveCount">AI 输出的正极实测层数（期望值：76）</param>
        /// <param name="negativeCount">AI 输出的负极实测层数（期望值：77）</param>
        /// <param name="ohInner">正极 OH 内侧值数组（distOHInt.SArr 转 double[]）</param>
        /// <param name="ohOuter">负极 OH 外侧值数组（distOHOut.SArr 转 double[]，暂未在本方法中直接用于越限判断）</param>
        /// <param name="negDiff">负极差值（Algorithm_Action 输出的 hDNe）</param>
        /// <param name="imageType">文件名第二段，如 "1-X"、"2-Y"（Path.GetFileName(filePath).Split('_')[1]）</param>
        /// <param name="gapMax">间隙最大值数组 [0]=负极边缘, [1]=正极边缘, [2]=正极中间（_gapMax.SArr 转 double[]）</param>
        /// <param name="negPressureFlag">负极压伤标志（Algorithm_Action 输出的 neNGflag，0=正常，1=压伤）</param>
        /// <param name="neTQflag">负极突起标志（Algorithm_Action 输出的 neTQflag，0=正常，1=突起）</param>
        /// <returns>"OK" 或 "NG_缺陷类型" 格式的字符串</returns>
        protected string NgOkJudge(
            int positiveCount,
            int negativeCount,
            double[] ohInner,
            double[] ohOuter,
            double negDiff,
            string imageType,
            double[] gapMax,
            int negPressureFlag,
            int neTQflag
        )
        {
            //// 1. 间隙最大值判断（负极边缘 / 正极边缘 / 正极中间，按优先级依次判断）
            //if (CheckGapMax(imageType, gapMax, out string? gapResult))
            //    return gapResult!;

            //// 2. 负极压伤判断
            //if (negPressureFlag == 1)
            //    return "NG_负极压伤";

            //// 3. 负极突起判断
            //if (neTQflag == 1)
            //    return "NG_负极突起";

#if C06
            // 1. 间隙最大值判断（负极边缘 / 正极边缘 / 正极中间，按优先级依次判断）
            if (negativeCount == 77 && positiveCount == 76 && CheckGapMax(imageType, gapMax, out string? gapResult))
                return gapResult!;

            // 2. 负极压伤判断
            if (negativeCount == 77 && positiveCount == 76 && negPressureFlag == 1)
                return "NG_负极压伤";

            // 3. 负极突起判断
            if (negativeCount == 77 && positiveCount == 76 && neTQflag == 1)
                return "NG_负极突起";

            // 4. 负极层数判断（期望 77 片）
            bool? negativeCountFlag = null;
            if (negativeCount != 77)
                negativeCountFlag = negativeCount > 77 ? true : false;

            bool? positiveCountFlag = null;
            // 5. 正极层数判断（期望 76 片）
            if (positiveCount != 76)
                positiveCountFlag = positiveCount > 76 ? true : false;
#else
            // 1. 间隙最大值判断（负极边缘 / 正极边缘 / 正极中间，按优先级依次判断）
            if (negativeCount == 108 && positiveCount == 107 && CheckGapMax(imageType, gapMax, out string? gapResult))
                return gapResult!;

            // 2. 负极压伤判断
            if (negativeCount == 108 && positiveCount == 107 && negPressureFlag == 1)
                return "NG_负极压伤";

            // 3. 负极突起判断
            if (negativeCount == 108 && positiveCount == 107 && neTQflag == 1)
                return "NG_负极突起";

            bool? negativeCountFlag = null;
            // 4. 负极层数判断（期望 108 片）
            if (negativeCount != 108)
                negativeCountFlag = negativeCount > 108 ? true : false;

            bool? positiveCountFlag = null;
            // 5. 正极层数判断（期望 107 片）
            if (positiveCount != 107)
                positiveCountFlag = positiveCount > 107 ? true : false;
#endif
            // 4.1 负极多片判断
            if (negativeCountFlag == true)
                return "NG_负极多片";

            // 6. Y 方向正极掉片判断（单图内相邻正极层 OH 差值是否超过 Gap45Limit）
            //    注：跨四张 Y 图的掉片二次判断（HandleZJYDP）需在上层聚合后执行，此处仅做单图初判。
            //    TODO: 在 BarcodeGroupingService 完成 8 张聚合后，使用 Y1~Y4 数据调用跨图掉片判断逻辑。
            if (negativeCountFlag == null && positiveCountFlag == null && imageType.Contains("-Y") && FindAdjacentDifferencesWithValues(ohInner, Gap45Limit))
                return "NG_正极Y方向掉片";

            // 7. OH 范围判断（正极 OH 内侧值是否在上下限之内）
            if (negativeCountFlag == null && positiveCountFlag == null && CheckOhRange(imageType, ohInner, out string? ohResult))
                return ohResult!;

            // 4.2 负极少片判断
            if (negativeCountFlag == false)
                return "NG_负极少片";

            // 5.1 正极少片判断
            if (positiveCountFlag == false)
                return "NG_正极少片";

            // 5.2 正极多片判断
            if (positiveCountFlag == true)
                return "NG_正极多片";

            // 8. 负极差判断（仅 1-X / 1-Y / 4-X / 4-Y 视野）
            if (CheckNegDiff(imageType, negDiff, out string? diffResult))
                return diffResult!;

            return "OK";
        }

        /// <summary>
        /// 将 <see cref="NgOkJudge"/> 返回值映射为 (isOk, defectType) 元组。
        /// <para>"NG_正极多片" → (false, "正极多片")；"OK" → (true, "OK")；"NG_负极压伤" → (false, "负极压伤")</para>
        /// </summary>
        protected static (bool isOk, string defectType) NormalizeResult(string? ngokResult)
        {
            if (string.IsNullOrEmpty(ngokResult) || ngokResult.StartsWith("OK"))
                return (true, "OK");

            int sep = ngokResult.IndexOf('_');
            string defect = sep >= 0 ? ngokResult[(sep + 1)..].Trim() : ngokResult.Trim();
            return (false, defect);
        }

        /// <summary>判断相邻层 OH 差值是否超过阈值（Y 方向掉片初判）。</summary>
        protected static bool FindAdjacentDifferencesWithValues(double[] array, double threshold)
        {
            if (array == null || array.Length < 2)
                return false;
            for (int i = 0; i < array.Length - 1; i++)
            {
                if (Math.Abs(array[i] - array[i + 1]) > threshold)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断正极 OH 内侧值是否越出上下限（由视野类型路由阈值）。
        /// </summary>
        private bool CheckOhRange(string imageType, double[] ohInner, out string? result)
        {
            double lower,
                upper;
            if (imageType.Contains("1-X"))
            {
                lower = Gap1Limit;
                upper = Gap2Limit;
            }
            else if (imageType.Contains("2-X"))
            {
                lower = Gap13Limit;
                upper = Gap14Limit;
            }
            else if (imageType.Contains("3-X"))
            {
                lower = Gap23Limit;
                upper = Gap24Limit;
            }
            else if (imageType.Contains("4-X"))
            {
                lower = Gap33Limit;
                upper = Gap34Limit;
            }
            else if (imageType.Contains("1-Y"))
            {
                lower = Gap7Limit;
                upper = Gap8Limit;
            }
            else if (imageType.Contains("2-Y"))
            {
                lower = Gap18Limit;
                upper = Gap19Limit;
            }
            else if (imageType.Contains("3-Y"))
            {
                lower = Gap28Limit;
                upper = Gap29Limit;
            }
            else if (imageType.Contains("4-Y"))
            {
                lower = Gap39Limit;
                upper = Gap40Limit;
            }
            else
            {
                result = null;
                return false;
            }

            bool tooHigh = ohInner.Any(v => v > upper);
            bool tooLow = ohInner.Any(v => v < lower);
            if (tooHigh || tooLow)
            {
                result = "NG_OH不良";
                return true;
            }
            result = null;
            return false;
        }

        /// <summary>
        /// 判断负极边缘、正极边缘、正极中间间隙最大值是否超差（由视野类型路由阈值）。
        /// </summary>
        /// <param name="gapMax">长度为 3 的数组：[0]=负极边缘, [1]=正极边缘, [2]=正极中间</param>
        private bool CheckGapMax(string imageType, double[] gapMax, out string? result)
        {
            result = null;
            if (gapMax == null || gapMax.Length < 3)
            {
                result = "NG_间隙最大值参数格式错误";
                return true;
            }

            double negEdge,
                posEdge,
                posMiddle;
            if (imageType.Contains("1-X"))
            {
                negEdge = Gap4Limit;
                posEdge = Gap5Limit;
                posMiddle = Gap6Limit;
            }
            else if (imageType.Contains("1-Y"))
            {
                negEdge = Gap10Limit;
                posEdge = Gap11Limit;
                posMiddle = Gap12Limit;
            }
            else if (imageType.Contains("2-X"))
            {
                negEdge = Gap15Limit;
                posEdge = Gap16Limit;
                posMiddle = Gap17Limit;
            }
            else if (imageType.Contains("2-Y"))
            {
                negEdge = Gap20Limit;
                posEdge = Gap21Limit;
                posMiddle = Gap22Limit;
            }
            else if (imageType.Contains("3-X"))
            {
                negEdge = Gap25Limit;
                posEdge = Gap26Limit;
                posMiddle = Gap27Limit;
            }
            else if (imageType.Contains("3-Y"))
            {
                negEdge = Gap30Limit;
                posEdge = Gap31Limit;
                posMiddle = Gap32Limit;
            }
            else if (imageType.Contains("4-X"))
            {
                negEdge = Gap36Limit;
                posEdge = Gap37Limit;
                posMiddle = Gap38Limit;
            }
            else if (imageType.Contains("4-Y"))
            {
                negEdge = Gap42Limit;
                posEdge = Gap43Limit;
                posMiddle = Gap44Limit;
            }
            else
            {
                result = "NG_未知图片类型，无法匹配间隙阈值";
                return true;
            }

            if (gapMax[0] > negEdge)
            {
                result = "NG_负极边缘间隙不良";
                return true;
            }
            if (gapMax[1] > posEdge)
            {
                result = "NG_正极边缘间隙不良";
                return true;
            }
            if (gapMax[2] > posMiddle)
            {
                result = "NG_正极中间间隙不良";
                return true;
            }
            return false;
        }

        /// <summary>
        /// 判断负极差值是否超差（仅 1-X / 1-Y / 4-X / 4-Y 视野参与判断）。
        /// </summary>
        private bool CheckNegDiff(string imageType, double negDiff, out string? result)
        {
            result = null;
            double threshold;
            if (imageType.Contains("1-X"))
                threshold = Gap3Limit;
            else if (imageType.Contains("1-Y"))
                threshold = Gap9Limit;
            else if (imageType.Contains("4-X"))
                threshold = Gap35Limit;
            else if (imageType.Contains("4-Y"))
                threshold = Gap41Limit;
            else
                return false;

            if (negDiff > threshold)
            {
                result = "NG_负极差不良";
                return true;
            }
            return false;
        }

        /// <summary>
        /// 根据视野类型字符串（文件名第二段，如 "1-X"、"2-Y"）解析绘图所需阈值，
        /// 供子类在调用 <see cref="ResultImageRenderer.GenerateResultImage"/> 前使用。
        /// </summary>
        /// <param name="imageType">视野类型（Path.GetFileName(filePath).Split('_')[1]）</param>
        private protected DrawingThresholds ResolveDrawingThresholds(string imageType) =>
            imageType switch
            {
                //          ohLower    ohUpper    fjEnd       posEnd      posMiddle   negDiff     showNegDiff
                "1-X" => new(
                    Gap1Limit,
                    Gap2Limit,
                    Gap4Limit,
                    Gap5Limit,
                    Gap6Limit,
                    Gap3Limit,
                    true
                ),
                "1-Y" => new(
                    Gap7Limit,
                    Gap8Limit,
                    Gap10Limit,
                    Gap11Limit,
                    Gap12Limit,
                    Gap9Limit,
                    true
                ),
                "2-X" => new(Gap13Limit, Gap14Limit, Gap15Limit, Gap16Limit, Gap17Limit, 0, false),
                "2-Y" => new(Gap18Limit, Gap19Limit, Gap20Limit, Gap21Limit, Gap22Limit, 0, false),
                "3-X" => new(Gap23Limit, Gap24Limit, Gap25Limit, Gap26Limit, Gap27Limit, 0, false),
                "3-Y" => new(Gap28Limit, Gap29Limit, Gap30Limit, Gap31Limit, Gap32Limit, 0, false),
                "4-X" => new(
                    Gap33Limit,
                    Gap34Limit,
                    Gap36Limit,
                    Gap37Limit,
                    Gap38Limit,
                    Gap35Limit,
                    true
                ),
                "4-Y" => new(
                    Gap39Limit,
                    Gap40Limit,
                    Gap42Limit,
                    Gap43Limit,
                    Gap44Limit,
                    Gap41Limit,
                    true
                ),
                _ => new(0.5, 2.0, 0.64, 0.45, 0.45, 0, false), // 未知视野类型的默认回退值
            };

        private protected List<int> GetAbnormalPositions(double[] array, string imageType)
        {
            double lower,
                upper;
            List<int> positions = new List<int>();
            if (imageType.Contains("1-X"))
            {
                lower = Gap1Limit;
                upper = Gap2Limit;
            }
            else if (imageType.Contains("2-X"))
            {
                lower = Gap13Limit;
                upper = Gap14Limit;
            }
            else if (imageType.Contains("3-X"))
            {
                lower = Gap23Limit;
                upper = Gap24Limit;
            }
            else if (imageType.Contains("4-X"))
            {
                lower = Gap33Limit;
                upper = Gap34Limit;
            }
            else if (imageType.Contains("1-Y"))
            {
                lower = Gap7Limit;
                upper = Gap8Limit;
            }
            else if (imageType.Contains("2-Y"))
            {
                lower = Gap18Limit;
                upper = Gap19Limit;
            }
            else if (imageType.Contains("3-Y"))
            {
                lower = Gap28Limit;
                upper = Gap29Limit;
            }
            else if (imageType.Contains("4-Y"))
            {
                lower = Gap39Limit;
                upper = Gap40Limit;
            }
            else
            {
                return positions;
            }

            // 检查数组中超出阈值范围（>upper 或 <lower）的位置并记录
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] > upper || array[i] < lower)
                    positions.Add(i);
            }
            return positions;
        }

        private protected void GetModel()
        {
#if C06
            AI.Model_init(@".\model\1222\C06_best.onnx", true, out Model, out string runinfo);
#else
            AI.Model_init(
                    @".\model\1119\C01_best.onnx",
                    true,
                    out Model,
                    out string runinfo
                );
#endif

            if (!string.IsNullOrEmpty(runinfo))
            {
                Logger.Error($"{TargetMonitorId} 模型初始化异常：{runinfo}");
            }
        }

        private protected (string Barcode, string ViewField) ParseImageFileName(string imageFilePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(imageFilePath);

            string barcode = "未知条码";
            string viewField = "?";

            if (fileName.EndsWith("_origin", StringComparison.OrdinalIgnoreCase))
            {
                string mainPart = fileName.Substring(0, fileName.Length - "_origin".Length);
                int lastUnderscoreIndex = mainPart.LastIndexOf('_');

                if (lastUnderscoreIndex > 0 && lastUnderscoreIndex < mainPart.Length - 1)
                {
                    barcode = mainPart.Substring(0, lastUnderscoreIndex);
                    viewField = mainPart.Substring(lastUnderscoreIndex + 1);
                }
                else
                {
                    barcode = mainPart;
                }
            }

            return (barcode, viewField);
        }
    }
}
