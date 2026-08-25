using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SUNWODA_SEVB.Core.Interfaces;

namespace SUNWODA_SEVB.Services.Project.CTOL
{
    /// <summary>
    /// Barcode 分组聚合选项。
    /// 对应配置文件中的 "BarcodeGrouping" 节（独立于 "Monitor" 节）。
    /// </summary>
    public sealed class BarcodeGroupingOptions
    {
        /// <summary>
        /// 每个 barcode 期望的图片总数，达到后触发分类移动。默认 8。
        /// （当前格式：4 拍位 × X/Y 两方向 = 8 张）
        /// </summary>
        public int ImagesPerBarcode { get; set; } = 8;

        /// <summary>
        /// Barcode 超时时长（秒）。
        /// 自第一张图片处理完成起，超出此时长仍未达到 ImagesPerBarcode 张，
        /// 则将 barcode 输出目录整体移至 TimeoutFolderName 目录。默认 60 秒。
        /// </summary>
        public int BarcodeTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// 缺陷类型优先级列表，按从高到低顺序排列。
        /// 8 张图片中优先级最高的缺陷类型决定 barcode 的最终分类文件夹名称。
        /// （优先级从高到低）：["OH不良", "正极多片", "正极少片", "负极多片", "负极少片", "负极边缘间隙不良", "正极边缘间隙不良", "正极中间间隙不良", "负极差不良", "负极压伤", "正极Y方向掉片"]
        /// </summary>
        public List<string> DefectTypePriorities { get; set; } = new();

        /// <summary>
        /// 超时未满 ImagesPerBarcode 张时使用的目录名。默认值：超时未达八张
        /// </summary>
        public string TimeoutFolderName { get; set; } = "超时未达八张";

        /// <summary>
        /// 任意一张图片算法处理异常时，整个 barcode 原始图片归档目录名。默认值：算法异常
        /// </summary>
        public string AlgorithmExceptionFolderName { get; set; } = "算法异常";
    }

    /// <summary>
    /// Barcode 分组聚合后台服务
    ///
    /// 功能：
    ///   1. 收集同一 barcode 输出目录下每张图片的检测结果。
    ///   2. 当累计张数达到 BarcodeGroupingOptions.ImagesPerBarcode 时，
    ///      按最高优先级缺陷类型将整个 barcode 输出目录移至
    ///      {OutputRootDir}/{DefectType}/{barcode}/ 下。
    ///   3. 后台定时检查（每 10 秒）：若某 barcode 自第一张处理起超过
    ///      BarcodeGroupingOptions.BarcodeTimeoutSeconds 仍未满张，
    ///      则将其整体移至 {OutputRootDir}/{TimeoutFolderName}/{barcode}/ 下。
    /// </summary>
    public sealed class BarcodeGroupingService : BackgroundService
    {
        // ==================== 内部状态 ====================

        private sealed class BarcodeState
        {
            public required string MonitorId { get; init; }
            public DateTime FirstSeenAt { get; } = DateTime.Now;

            /// <summary>true 表示至少有一张图片算法处理时抛出异常。</summary>
            public bool HasAlgorithmError { get; private set; }

            /// <summary>来源文件所在目录（所有图片同属同一 barcode 目录，记录一次即可）。</summary>
            public string? SourceDirectory { get; private set; }

            private readonly List<(bool isOk, string defectType)> _results = new();

            /// <summary>
            /// Y 方向各视野的完整 ohInner 数组，用于 8 张聚合后的逐层跨图掉片判断。
            /// Key = 视野类型（"1-Y"、"2-Y"、"3-Y"、"4-Y"），Value = ohInner double[] 数组。
            /// </summary>
            private readonly Dictionary<string, double[]> _yOhArrays = new(
                4,
                StringComparer.OrdinalIgnoreCase
            );

            private readonly object _lock = new();

            public (bool completed, List<(bool isOk, string defectType)> snapshot) TryAdd(
                bool isOk,
                string defectType,
                int maxCount,
                string sourceFilePath,
                bool algorithmError,
                string? imageType = null,
                double[]? yOhValues = null
            )
            {
                lock (_lock)
                {
                    _results.Add((isOk, defectType));
                    if (algorithmError)
                        HasAlgorithmError = true;
                    SourceDirectory ??= Path.GetDirectoryName(sourceFilePath);
                    if (imageType is not null && yOhValues is { Length: > 0 })
                        _yOhArrays[imageType] = yOhValues;
                    if (_results.Count >= maxCount)
                        return (true, new List<(bool, string)>(_results));
                    return (false, new List<(bool, string)>());
                }
            }

            public (int count, List<(bool isOk, string defectType)> snapshot) GetSnapshot()
            {
                lock (_lock)
                    return (_results.Count, new List<(bool, string)>(_results));
            }

            /// <summary>
            /// 返回已收集的 Y 方向各视野 ohInner 数组副本。
            /// </summary>
            public IReadOnlyDictionary<string, double[]> GetYOhArrays()
            {
                lock (_lock)
                    return new Dictionary<string, double[]>(
                        _yOhArrays,
                        StringComparer.OrdinalIgnoreCase
                    );
            }
        }

        // key = 输出 barcode 目录绝对路径（大小写不敏感）
        private readonly ConcurrentDictionary<string, BarcodeState> _states = new(
            StringComparer.OrdinalIgnoreCase
        );

        private readonly BarcodeGroupingOptions _options;
        private readonly GapThresholdService _gapThresholds;
        private readonly ILoggerService<BarcodeGroupingService> _logger;

        /// <summary>barcode 完成分类时触发：(monitorId, barcode, category)。
        /// category 为 "OK" / 缺陷类型名 / TimeoutFolderName / AlgorithmExceptionFolderName。</summary>
        public event Action<string, string, string>? BarcodeFinalized;

        public BarcodeGroupingService(
            IOptions<BarcodeGroupingOptions> options,
            GapThresholdService gapThresholds,
            ILoggerService<BarcodeGroupingService> logger
        )
        {
            _options = options.Value;
            _gapThresholds = gapThresholds;
            _logger = logger;
        }

        // ==================== 供 PngProcessorBase 调用 ====================

        /// <param name="sourceFilePath">原始来源文件的绝对路径，用于算法异常时定位 barcode 原图目录。</param>
        /// <param name="isAlgorithmError">true 表示本张图片算法处理时抛出了异常。</param>
        /// /// <param name="imageType">
        /// 视野类型字符串（如 "1-Y"、"2-Y"）。非空且 <paramref name="yOhValues"/> 非空时，
        /// 将存入 BarcodeState 供 8 张聚合后执行逐层跨图 Y 方向掉片二次判断。
        /// </param>
        /// <param name="yOhValues">Y 视野图片的完整 ohInner 数组（每层一个值）；非 Y 视野或算法异常时传 null。</param>
        public async Task RecordAsync(
            string outputBarcodeDir,
            string monitorId,
            bool isOk,
            string defectType,
            string sourceFilePath,
            bool isAlgorithmError = false,
            string? imageType = null,
            double[]? yOhValues = null,
            CancellationToken ct = default
        )
        {
            var state = _states.GetOrAdd(
                outputBarcodeDir,
                _ => new BarcodeState { MonitorId = monitorId }
            );

            var (completed, snapshot) = state.TryAdd(
                isOk,
                defectType,
                _options.ImagesPerBarcode,
                sourceFilePath,
                isAlgorithmError,
                imageType,
                yOhValues
            );

            if (completed && _states.TryRemove(outputBarcodeDir, out var removed))
            {
                await FinalizeAsync(outputBarcodeDir, removed.MonitorId, snapshot, removed)
                    .ConfigureAwait(false);
            }
        }

        // ==================== BackgroundService（超时巡检） ====================

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await CheckTimeoutsAsync().ConfigureAwait(false);
            }
        }

        private async Task CheckTimeoutsAsync()
        {
            var now = DateTime.Now;
            foreach (var (key, state) in _states)
            {
                if ((now - state.FirstSeenAt).TotalSeconds < _options.BarcodeTimeoutSeconds)
                    continue;

                if (!_states.TryRemove(key, out var removed))
                    continue;

                var (count, _) = removed.GetSnapshot();
                _logger.Warn(
                    $"[文件归档服务] 超时未满 {_options.ImagesPerBarcode} 张（已收到 {count} 张），移至超时目录：{key}"
                );

                await HandleTimeoutAsync(key, removed.MonitorId, removed).ConfigureAwait(false);
            }
        }

        // ==================== 完成分类移动 ====================

        private Task FinalizeAsync(
            string outputBarcodeDir,
            string monitorId,
            List<(bool isOk, string defectType)> results,
            BarcodeState state
        )
        {
            var barcodeName = GetBarcodeName(outputBarcodeDir);
            var dateDir = Path.GetDirectoryName(Path.GetDirectoryName(outputBarcodeDir))!;

            // 优先处理算法异常：将整个 barcode 原始图片复制到算法异常目录
            if (state.HasAlgorithmError && state.SourceDirectory != null)
            {
                var exceptionDest = Path.Combine(
                    dateDir,
                    _options.AlgorithmExceptionFolderName,
                    barcodeName
                );
                _logger.Warn(
                    $"[文件归档服务] {barcodeName} 存在算法异常，原始图片复制至：{exceptionDest}"
                );
                CopySourceFiles(state.SourceDirectory, exceptionDest, barcodeName);
                // 同时将已生成的结果图（部分图片处理成功时存在）并入同一目录
                if (Directory.Exists(outputBarcodeDir))
                    MoveDirectory(outputBarcodeDir, exceptionDest);
                BarcodeFinalized?.Invoke(
                    monitorId,
                    barcodeName,
                    _options.AlgorithmExceptionFolderName
                );
                return Task.CompletedTask;
            }

            // 跨图 Y 掉片判断：单图初判仅为二次判断的前置条件，
            // 最终结论以 CheckCrossYDrop 为准：
            //   · crossYDrop = true  → 跨图确认掉片，将 (false, "正极Y方向掉片") 加入结果列表
            //   · crossYDrop = false → 跨图未确认，移除单图中所有 "正极Y方向掉片" 条目（视作 OK）
            bool crossYDrop = CheckCrossYDrop(state, monitorId);
            List<(bool isOk, string defectType)> effectiveResults;
            if (crossYDrop)
            {
                effectiveResults = new List<(bool, string)>(results) { (false, "正极Y方向掉片") };
            }
            else
            {
                effectiveResults = results
                    .Where(r =>
                        !r.defectType.Equals("正极Y方向掉片", StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }

            // 有效结果全部为 OK：不移动，保留在原输出目录
            if (effectiveResults.All(r => r.isOk))
            {
                _logger.Info(
                    $"[文件归档服务] {barcodeName} 全部 {results.Count} 张均为 OK，保留在原输出目录"
                );
                BarcodeFinalized?.Invoke(monitorId, barcodeName, "OK");
                return Task.CompletedTask;
            }

            var defectType = DetermineDefectType(effectiveResults);
            var dest = Path.Combine(dateDir, defectType, barcodeName);

            _logger.Info(
                $"[文件归档服务] {barcodeName} 检出缺陷「{defectType}」，{results.Count} 张处理完成，移至：{dest}"
            );

            // 结果图移至目标目录，同时将原始图片一并复制（原图 + 结果图同存）
            MoveDirectory(outputBarcodeDir, dest);
            if (state.SourceDirectory != null)
                CopySourceFiles(state.SourceDirectory, dest, barcodeName);
            BarcodeFinalized?.Invoke(monitorId, barcodeName, defectType);
            return Task.CompletedTask;
        }

        private Task HandleTimeoutAsync(
            string outputBarcodeDir,
            string monitorId,
            BarcodeState state
        )
        {
            var barcodeName = GetBarcodeName(outputBarcodeDir);
            var dateDir = Path.GetDirectoryName(Path.GetDirectoryName(outputBarcodeDir))!;

            // 存在算法异常时，将原始图片复制到算法异常目录（即使超时也执行）
            if (state.HasAlgorithmError && state.SourceDirectory != null)
            {
                var exceptionDest = Path.Combine(
                    dateDir,
                    _options.AlgorithmExceptionFolderName,
                    barcodeName
                );
                _logger.Warn(
                    $"[文件归档服务] {barcodeName} 超时且存在算法异常，原始图片复制至：{exceptionDest}"
                );
                CopySourceFiles(state.SourceDirectory, exceptionDest, barcodeName);
                // 同时将已生成的结果图（部分图片处理成功时存在）并入同一目录
                if (Directory.Exists(outputBarcodeDir))
                    MoveDirectory(outputBarcodeDir, exceptionDest);
                BarcodeFinalized?.Invoke(
                    monitorId,
                    barcodeName,
                    _options.AlgorithmExceptionFolderName
                );
                return Task.CompletedTask;
            }

            var timeoutDest = Path.Combine(dateDir, _options.TimeoutFolderName, barcodeName);

            if (Directory.Exists(outputBarcodeDir))
            {
                _logger.Warn($"[文件归档服务] {barcodeName} 超时，移至超时目录：{timeoutDest}");
                MoveDirectory(outputBarcodeDir, timeoutDest);
            }
            else
            {
                _logger.Debug($"[文件归档服务] {barcodeName} 超时但输出目录不存在，跳过移动");
            }

            // 超时目录始终保留原件；若含 NG，额外复制一份到对应缺陷目录
            var (_, snapshot) = state.GetSnapshot();
            if (snapshot.Any(r => !r.isOk))
            {
                var defectType = DetermineDefectType(snapshot);
                var defectDest = Path.Combine(dateDir, defectType, barcodeName);
                _logger.Info(
                    $"[文件归档服务] {barcodeName} 超时未满张且含缺陷「{defectType}」，额外复制至：{defectDest}"
                );
                CopyDirectory(timeoutDest, defectDest);
            }

            BarcodeFinalized?.Invoke(monitorId, barcodeName, _options.TimeoutFolderName);
            return Task.CompletedTask;
        }

        // ==================== 辅助方法 ====================

        /// <summary>
        /// 将 source 目录下所有文件复制到 dest 目录（已存在的同名文件覆盖）。
        /// </summary>
        private void CopyDirectory(string source, string dest)
        {
            if (!Directory.Exists(source))
            {
                _logger.Warn($"[文件归档服务] 源目录不存在，跳过复制：{source}");
                return;
            }

            try
            {
                Directory.CreateDirectory(dest);
                foreach (
                    var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                )
                {
                    var rel = Path.GetRelativePath(source, file);
                    var target = Path.Combine(dest, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[文件归档服务] 复制目录失败：{source} → {dest}", ex);
            }
        }

        /// <summary>
        /// 将 barcode 的所有原始来源文件从 sourceDir 复制到 dest 目录。
        /// 仅匹配 <c>{barcodePrefix}_*_origin.png</c> 格式，排除同目录中可能混入的结果图。
        /// </summary>
        private void CopySourceFiles(string sourceDir, string dest, string barcodePrefix)
        {
            try
            {
                Directory.CreateDirectory(dest);
                foreach (
                    var srcFile in Directory.EnumerateFiles(
                        sourceDir,
                        $"*_*_origin.png",
                        SearchOption.TopDirectoryOnly
                    )
                )
                {
                    var destFile = Path.Combine(dest, Path.GetFileName(srcFile));
                    File.Copy(srcFile, destFile, overwrite: true);
                    _logger.Debug($"[文件归档服务] 复制原图：{srcFile} → {destFile}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[文件归档服务] 复制原始图片失败：{sourceDir} → {dest}", ex);
            }
        }

        private string DetermineDefectType(List<(bool isOk, string defectType)> results)
        {
            foreach (var priority in _options.DefectTypePriorities)
            {
                if (
                    results.Any(r =>
                        r.defectType.Equals(priority, StringComparison.OrdinalIgnoreCase)
                    )
                )
                    return priority;
            }

            // 兜底：返回第一个 NG 的缺陷类型
            return results.FirstOrDefault(r => !r.isOk).defectType ?? "未知缺陷";
        }

        private void MoveDirectory(string source, string dest)
        {
            if (!Directory.Exists(source))
            {
                _logger.Debug($"[文件归档服务] 源目录不存在，跳过移动：{source}");
                return;
            }

            try
            {
                var parent = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                if (Directory.Exists(dest))
                {
                    // 目标已存在：逐文件合并
                    foreach (
                        var file in Directory.EnumerateFiles(
                            source,
                            "*",
                            SearchOption.AllDirectories
                        )
                    )
                    {
                        var rel = Path.GetRelativePath(source, file);
                        var target = Path.Combine(dest, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.Move(file, target, overwrite: true);
                    }
                    Directory.Delete(source, recursive: true);
                }
                else
                {
                    Directory.Move(source, dest);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[文件归档服务] 移动目录失败：{source} → {dest}", ex);
            }
        }

        private static string GetBarcodeName(string outputBarcodeDir) =>
            new DirectoryInfo(
                outputBarcodeDir.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                )
            ).Name;

        /// <summary>
        /// 跨四张 Y 图掉片二次判断（HandleZJYDP 移植），逐层检查，两个条件同时满足才判 NG。
        /// <para>
        /// 仅当 1-Y、2-Y、3-Y、4-Y 图片的 ohInner 数组均已记录时执行。
        /// 对每一层索引 i，需同时满足以下全部条件才判定为掉片：
        /// </para>
        /// <list type="bullet">
        ///   <item>① 该层在任一 Y 图中存在相邻层 OH 差值 ≥ Gap45Limit（单图内相邻掉片初判）</item>
        ///   <item>② |Y1[i]+Y3[i]−Y2[i]−Y4[i]| ≥ Gap46Limit（对角和差）</item>
        /// </list>
        /// </summary>
        private bool CheckCrossYDrop(BarcodeState state, string monitorId)
        {
            var yArrays = state.GetYOhArrays();
            if (
                !yArrays.TryGetValue("1-Y", out var y1)
                || !yArrays.TryGetValue("2-Y", out var y2)
                || !yArrays.TryGetValue("3-Y", out var y3)
                || !yArrays.TryGetValue("4-Y", out var y4)
            )
                return false; // 未集齐四张 Y 图数据，跳过跨图判断

            int length = Math.Min(Math.Min(y1.Length, y2.Length), Math.Min(y3.Length, y4.Length));
            if (length < 2)
                return false;

            var thresholds = _gapThresholds.Get(monitorId);
            double gap45 = 0.3,
                gap46 = 0.8;
            if (thresholds is { Count: > 0 })
            {
                if (thresholds.TryGetValue("Gap45Limit", out var v45))
                    gap45 = v45;
                if (thresholds.TryGetValue("Gap46Limit", out var v46))
                    gap46 = v46;
            }

            for (int i = 0; i < length; i++)
            {
                // ① 该层在任一 Y 图中存在相邻层 OH 差值 ≥ Gap45Limit
                if (
                    !HasAdjacentDiff(y1, i, gap45)
                    && !HasAdjacentDiff(y2, i, gap45)
                    && !HasAdjacentDiff(y3, i, gap45)
                    && !HasAdjacentDiff(y4, i, gap45)
                )
                    continue;

                // ② |Y1[i]+Y3[i]−Y2[i]−Y4[i]| ≥ Gap46Limit
                if (Math.Abs(y1[i] + y3[i] - y2[i] - y4[i]) < gap46)
                    continue;

                return true; // ①② 同时满足 → 掉片
            }

            return false;
        }

        /// <summary>判断数组中第 i 层是否与其相邻层（前或后）存在差值 ≥ threshold。</summary>
        private static bool HasAdjacentDiff(double[] arr, int i, double threshold)
        {
            bool prevDiff = i > 0 && Math.Abs(arr[i] - arr[i - 1]) >= threshold;
            bool nextDiff = i < arr.Length - 1 && Math.Abs(arr[i] - arr[i + 1]) >= threshold;
            return prevDiff || nextDiff;
        }
    }
}
