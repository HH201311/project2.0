using System.Globalization;
using System.IO;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    /// <summary>
    /// 对齐枣庄 <c>VM_PagePack6Surface.GetNewFilePath</c>：
    /// 原始图片 / 结果图片 → OKImage|NGImage → [缺陷大类/细分] → 日期起相对路径。
    /// </summary>
    public sealed class Pack6ImageArchiveService : IZZSIXPack6ImageArchiveService
    {
        public const string ImageDataFolder = "图像数据";
        public const string OriginalFolder = "原始图片";
        public const string ResultFolder = "结果图片";
        public const string OkFolder = "OKImage";
        public const string NgFolder = "NGImage";

        private readonly ILoggerService<Pack6ImageArchiveService> _logger;
        private readonly object _purgeLock = new();
        private readonly object _visionAlgoCsvLock = new();
        private DateTime _lastPurgeUtc = DateTime.MinValue;

        public Pack6ImageArchiveService(ILoggerService<Pack6ImageArchiveService> logger)
        {
            _logger = logger;
        }

        public void EnsureRoots(Pack6Config config)
        {
            try
            {
                var root = GetImageDataRoot(config);
                Directory.CreateDirectory(Path.Combine(root, OriginalFolder, OkFolder));
                Directory.CreateDirectory(Path.Combine(root, OriginalFolder, NgFolder));
                Directory.CreateDirectory(Path.Combine(root, ResultFolder, OkFolder));
                Directory.CreateDirectory(Path.Combine(root, ResultFolder, NgFolder));
                Directory.CreateDirectory(Path.Combine(
                    root, OriginalFolder, NgFolder, Pack6DefectFolderMapper.VisionAlgorithmCategory));
                Directory.CreateDirectory(Path.Combine(
                    root, ResultFolder, NgFolder, Pack6DefectFolderMapper.VisionAlgorithmCategory));
            }
            catch (Exception ex)
            {
                _logger.Warn($"创建图像数据目录失败: {ex.Message}", false);
            }
        }

        public void ArchiveFrame(Pack6Config config, Pack6FrameResult frame)
        {
            try
            {
                if (frame is null)
                    return;
                var source = frame.FilePath;
                if (string.IsNullOrWhiteSpace(source) ||
                    source.StartsWith("sim://", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith("timeout://", StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(source))
                    return;

                var relative = ExtractRelativePath(source);
                if (string.IsNullOrWhiteSpace(relative))
                    relative = Path.GetFileName(source);

                var isOk = frame.IsOk;
                var statusFolder = isOk ? OkFolder : NgFolder;
                // 视觉算法异常优先：只进「视觉算法异常」，不进三码等其它 NG 目录
                var isVisionAlgoError = !isOk && frame.IsVisionAlgorithmError;
                IReadOnlyList<Pack6DefectFolder> defectFolders;
                if (isOk)
                    defectFolders = Array.Empty<Pack6DefectFolder>();
                else if (isVisionAlgoError)
                    defectFolders =
                    [
                        new Pack6DefectFolder
                        {
                            Category = Pack6DefectFolderMapper.VisionAlgorithmCategory,
                            SubDefect = null
                        }
                    ];
                else if (frame.OcrMode != Pack6OcrMode.None)
                    defectFolders =
                    [
                        new Pack6DefectFolder
                        {
                            Category = Pack6DefectFolderMapper.ThreeCodeCategory,
                            SubDefect = null
                        }
                    ];
                else
                    defectFolders = Pack6DefectFolderMapper.Map(frame.Defects);

                // OK：无缺陷子目录；NG：有映射则各大类各存一份
                var bases = BuildStatusBases(config, statusFolder, defectFolders);
                string? firstResultDest = null;
                foreach (var statusBase in bases)
                {
                    var originalDest = BuildFinalPath(statusBase, relative, isResultImage: false);
                    if (originalDest is not null)
                        TryCopy(source, originalDest);

                    var storeResult = isOk ? config.StoreResultImageOK : config.StoreResultImageNG;
                    if (!storeResult)
                        continue;

                    // 视觉算法异常：结果也存原图（不叠异常横幅）；其它 NG/OK：优先叠图
                    var resultSrc = source;
                    if (!isVisionAlgoError &&
                        !string.IsNullOrWhiteSpace(frame.PreviewImagePath) &&
                        File.Exists(frame.PreviewImagePath))
                        resultSrc = frame.PreviewImagePath;
                    var resultDest = BuildFinalPath(statusBase, relative, isResultImage: true);
                    if (resultDest is not null)
                    {
                        TryCopy(resultSrc, resultDest);
                        firstResultDest ??= resultDest;
                    }
                }

                if (isVisionAlgoError)
                    AppendVisionAlgorithmCsv(config, frame, relative, firstResultDest ?? source);
            }
            catch (Exception ex)
            {
                _logger.Warn($"归档帧图像失败: {ex.Message}", false);
            }
        }

        /// <summary>
        /// 在「结果图片/NGImage/视觉算法异常/」下维护 CSV，记录每张图的具体报错。
        /// </summary>
        private void AppendVisionAlgorithmCsv(
            Pack6Config config,
            Pack6FrameResult frame,
            string relativePath,
            string savedImagePath)
        {
            try
            {
                var dir = Path.Combine(
                    GetImageDataRoot(config),
                    ResultFolder,
                    NgFolder,
                    Pack6DefectFolderMapper.VisionAlgorithmCategory);
                Directory.CreateDirectory(dir);
                var csvPath = Path.Combine(dir, "视觉算法异常.csv");

                var errorDetail = string.IsNullOrWhiteSpace(frame.EngineMessage)
                    ? string.Join(' ', frame.Defects.Where(d => !string.IsNullOrWhiteSpace(d)))
                    : frame.EngineMessage.Trim();
                if (string.IsNullOrWhiteSpace(errorDetail))
                    errorDetail = Pack6DefectFolderMapper.VisionAlgorithmCategory;

                var line =
                    $"{EscapeCsv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))}," +
                    $"{EscapeCsv(frame.ProductCode)}," +
                    $"{EscapeCsv(frame.CameraName)}," +
                    $"{frame.ShotIndex}," +
                    $"{EscapeCsv(Path.GetFileName(frame.FilePath))}," +
                    $"{EscapeCsv(relativePath)}," +
                    $"{EscapeCsv(errorDetail)}," +
                    $"{EscapeCsv(savedImagePath)}";

                lock (_visionAlgoCsvLock)
                {
                    if (!File.Exists(csvPath))
                    {
                        File.WriteAllText(
                            csvPath,
                            "DateTime,ProductCode,Camera,Shot,FileName,RelativePath,ErrorDetail,SavedImagePath" +
                            Environment.NewLine,
                            System.Text.Encoding.UTF8);
                    }

                    File.AppendAllText(csvPath, line + Environment.NewLine, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"写入视觉算法异常CSV失败: {ex.Message}", false);
            }
        }

        private static string EscapeCsv(string? value)
        {
            var s = value ?? string.Empty;
            if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public void PurgeIfNeeded(Pack6Config config)
        {
            // 启动/运行中节流，避免频繁扫盘
            lock (_purgeLock)
            {
                if ((DateTime.UtcNow - _lastPurgeUtc).TotalMinutes < 10)
                    return;
                _lastPurgeUtc = DateTime.UtcNow;
            }

            try
            {
                EnsureRoots(config);
                var root = GetImageDataRoot(config);
                if (!Directory.Exists(root))
                    return;

                var driveRoot = Path.GetPathRoot(Path.GetFullPath(root));
                if (string.IsNullOrWhiteSpace(driveRoot))
                    return;

                var di = new DriveInfo(driveRoot);
                if (!di.IsReady)
                    return;

                var usedRatio = 1.0 - (double)di.TotalFreeSpace / Math.Max(1, di.TotalSize);
                var threshold = Math.Clamp(config.ImageArchiveDiskUsagePercent, 50, 99) / 100.0;
                if (usedRatio < threshold)
                    return;

                var retainYears = Math.Clamp(config.ImageArchiveRetainYears, 1, 10);
                var keepAfter = DateTime.Now.Date.AddYears(-retainYears);

                var dated = CollectDatedFolders(root);
                if (dated.Count == 0)
                    return;

                // 先删超过保留年限的最早日期；若仍超限，再删保留期内最早日期
                foreach (var group in dated.OrderBy(x => x.Key))
                {
                    usedRatio = 1.0 - (double)di.TotalFreeSpace / Math.Max(1, di.TotalSize);
                    if (usedRatio < threshold)
                        break;

                    if (group.Key >= keepAfter)
                    {
                        // 仍超限才允许动保留期内数据
                        usedRatio = 1.0 - (double)di.TotalFreeSpace / Math.Max(1, di.TotalSize);
                        if (usedRatio < threshold)
                            break;
                    }

                    foreach (var dir in group)
                    {
                        try
                        {
                            if (Directory.Exists(dir))
                            {
                                Directory.Delete(dir, recursive: true);
                                _logger.Info($"磁盘占用高，已删除最早图像目录: {dir}", false);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"删除图像目录失败 {dir}: {ex.Message}", false);
                        }
                    }

                    // 删完该日期后清理空父目录
                    CleanupEmptyParents(root);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"图像归档清理失败: {ex.Message}", false);
            }
        }

        private static string GetImageDataRoot(Pack6Config config) =>
            Path.Combine(config.LocalPathPre.Trim(), ImageDataFolder);

        private static List<string> BuildStatusBases(
            Pack6Config config,
            string statusFolder,
            IReadOnlyList<Pack6DefectFolder> defectFolders)
        {
            var originalRoot = Path.Combine(GetImageDataRoot(config), OriginalFolder, statusFolder);
            var list = new List<string>();
            if (defectFolders.Count == 0)
            {
                list.Add(originalRoot);
                return list;
            }

            foreach (var d in defectFolders)
            {
                var p = Path.Combine(originalRoot, d.Category);
                if (!string.IsNullOrWhiteSpace(d.SubDefect))
                    p = Path.Combine(p, d.SubDefect);
                list.Add(p);
            }

            return list;
        }

        private static string? BuildFinalPath(string statusBaseAsOriginal, string relativePath, bool isResultImage)
        {
            try
            {
                var path = Path.Combine(statusBaseAsOriginal, relativePath.TrimStart('\\', '/'));
                path = Path.ChangeExtension(path, ".jpg");
                if (isResultImage)
                    path = path.Replace(OriginalFolder, ResultFolder);
                var dir = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(dir))
                    return null;
                Directory.CreateDirectory(dir);
                return path;
            }
            catch
            {
                return null;
            }
        }

        private void TryCopy(string source, string dest)
        {
            try
            {
                var dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(source, dest, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.Warn($"拷贝图像失败 {Path.GetFileName(source)} → {dest}: {ex.Message}", false);
            }
        }

        /// <summary>从源路径截取「日期夹」起的相对路径（对齐枣庄 ExtractRelativePath）。</summary>
        internal static string ExtractRelativePath(string filePath)
        {
            try
            {
                var normalized = filePath.Replace('/', '\\');
                var parts = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                var dateIndex = -1;
                for (var i = 0; i < parts.Length; i++)
                {
                    if (IsDateFolder(parts[i]))
                    {
                        dateIndex = i;
                        break;
                    }
                }

                if (dateIndex >= 0 && dateIndex < parts.Length)
                    return string.Join("\\", parts.Skip(dateIndex));

                return Path.GetFileName(filePath);
            }
            catch
            {
                return Path.GetFileName(filePath);
            }
        }

        private static bool IsDateFolder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            if (DateTime.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
                return true;
            if (name.Length == 8 &&
                DateTime.TryParseExact(name, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
                return true;
            return false;
        }

        private static List<IGrouping<DateTime, string>> CollectDatedFolders(string imageDataRoot)
        {
            var found = new List<(DateTime Day, string Path)>();
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(imageDataRoot, "*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(dir);
                    if (!TryParseDateFolder(name, out var day))
                        continue;
                    found.Add((day, dir));
                }
            }
            catch
            {
                // ignore
            }

            // 同一天可能多处；删除时以日期分组整批删
            return found
                .GroupBy(x => x.Day, x => x.Path)
                .OrderBy(g => g.Key)
                .ToList();
        }

        private static bool TryParseDateFolder(string name, out DateTime day)
        {
            day = default;
            if (DateTime.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out day))
                return true;
            if (name.Length == 8 &&
                DateTime.TryParseExact(name, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out day))
                return true;
            return false;
        }

        private static void CleanupEmptyParents(string root)
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                             .OrderByDescending(d => d.Length))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            Directory.Delete(dir, false);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
