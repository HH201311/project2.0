using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 惠州离线 CT AI 标注：上视图中间图选取与 JSON/CSV 落盘。
    /// </summary>
    public static class OfflineCtAnnotationHelper
    {
        /// <summary>历史子目录名；现已不再创建，结果直接落在产品文件夹下。</summary>
        [Obsolete("不再生成 AI标注 子目录，请用产品文件夹本身作为输出目录")]
        public const string AnnotationFolderName = "AI标注";

        /// <summary>已废弃中间落盘名；最终推理图请用 OfflineCtPreBrightenHelper（横图 CW90 → input/）。</summary>
        [Obsolete("不再单独落盘旋转中间图，请使用 OfflineCtPreBrightenHelper.PrepareFinalInputImage")]
        public const string RotatedInputFileNameWithoutExt = "input_rotated_cw90";

        private const string LegacyAnnotationFolderName = "AI标注";

        private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"];

        private static bool IsImageFile(string path) =>
            ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 「上」视图文件夹：{exportRoot}/{exportBaseName}/{exportBaseName} 上
        /// </summary>
        public static string BuildTopViewFolder(string sliceExportFolder, string exportBaseName) =>
            Path.Combine(
                sliceExportFolder,
                SliceExportNamingHelper.BuildExportFileRootFolderName(exportBaseName),
                SliceExportNamingHelper.BuildExportFolderName(exportBaseName, "上"));

        /// <summary>
        /// AI 标注输出目录：{exportRoot}/{exportBaseName}（不再套一层 AI标注）。
        /// </summary>
        public static string BuildAnnotationFolder(string sliceExportFolder, string exportBaseName) =>
            Path.Combine(
                sliceExportFolder,
                SliceExportNamingHelper.BuildExportFileRootFolderName(exportBaseName));

        /// <summary>
        /// 交接文件名（扁平，无子目录）：{导出名}.{源扩展名}，例如「1161-F.jpg」。
        /// 用产品名作文件名，避免多产品共用 050.jpg 冲突。
        /// </summary>
        public static string BuildHandoffFileName(string exportBaseName, string sourceImagePath)
        {
            var safeBase = string.IsNullOrWhiteSpace(exportBaseName)
                ? "export"
                : Path.GetFileName(exportBaseName.Trim());
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                safeBase = safeBase.Replace(c, '_');
            }

            if (string.IsNullOrWhiteSpace(safeBase))
            {
                safeBase = "export";
            }

            var ext = Path.GetExtension(sourceImagePath);
            if (string.IsNullOrWhiteSpace(ext)
                || !ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                ext = ".jpg";
            }

            return safeBase + ext.ToLowerInvariant();
        }

        /// <summary>
        /// 兼容旧签名：忽略 <paramref name="sliceIndex"/>，等同 <see cref="BuildHandoffFileName(string, string)"/>。
        /// </summary>
        [Obsolete("请使用 BuildHandoffFileName(exportBaseName, sourceImagePath)：扁平 {产品名}.jpg")]
        public static string BuildHandoffFileName(string exportBaseName, int sliceIndex, string sourceImagePath)
        {
            _ = sliceIndex;
            return BuildHandoffFileName(exportBaseName, sourceImagePath);
        }

        /// <summary>
        /// 兼容旧相对路径 API；现行返回扁平文件名（无子目录）。
        /// </summary>
        [Obsolete("请使用 BuildHandoffFileName：交接目录根下直接放 {产品名}.jpg")]
        public static string BuildHandoffRelativePath(string exportBaseName, string sourceImagePath) =>
            BuildHandoffFileName(exportBaseName, sourceImagePath);

        /// <summary>
        /// 将上视图送检图写入交接目录根，文件名改为产品名（如 1161-F.jpg）。
        /// 若源图宽 &gt; 高（横图），落盘前顺时针 90°，保证中转目录给视觉的是竖图。
        /// </summary>
        /// <param name="rotatedCw90ForPortrait">true=本次因横图执行了 CW90°。</param>
        public static string CopyTopViewImageToHandoffFolder(
            string sourceImagePath,
            string handoffFolder,
            string exportBaseName,
            int sliceIndex,
            out bool rotatedCw90ForPortrait)
        {
            _ = sliceIndex; // 选图序号仅影响源图，交接落盘名用产品名
            rotatedCw90ForPortrait = false;
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                throw new FileNotFoundException("待交接的上视图送检图不存在", sourceImagePath);
            }

            if (string.IsNullOrWhiteSpace(handoffFolder))
            {
                throw new InvalidOperationException("请先配置上视图交接目录（TopViewHandoffFolder）");
            }

            Directory.CreateDirectory(handoffFolder);
            var fileName = BuildHandoffFileName(exportBaseName, sourceImagePath);
            var destPath = Path.Combine(handoffFolder, fileName);

            // 不能只 File.Copy：VG 导出上视图常为横图，视觉要求竖图
            using var bmp = LoadBitmapUnlocked(sourceImagePath);
            rotatedCw90ForPortrait = EnsurePortraitByClockwise90IfLandscape(bmp);
            bmp.Save(destPath, ResolveImageFormat(destPath));
            return destPath;
        }

        /// <summary>
        /// 兼容旧签名（忽略是否旋转）。
        /// </summary>
        public static string CopyTopViewImageToHandoffFolder(
            string sourceImagePath,
            string handoffFolder,
            string exportBaseName,
            int sliceIndex) =>
            CopyTopViewImageToHandoffFolder(
                sourceImagePath,
                handoffFolder,
                exportBaseName,
                sliceIndex,
                out _);

        /// <summary>
        /// 若宽 &gt; 高（横图），顺时针 90° 转为竖图；已竖（高 ≥ 宽）则不动。
        /// 切图交接与阶段七送检共用，避免固定旋转导致竖图再转成横图。
        /// </summary>
        /// <returns>true 表示执行了旋转。</returns>
        public static bool EnsurePortraitByClockwise90IfLandscape(Bitmap bitmap)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            if (bitmap.Width > bitmap.Height)
            {
                bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 备份根目录名：落在「标注结果根目录」上级 →
        /// {Parent(SliceExportFolder)}/交接备份/{yyyy-MM-dd}/{产品名}.jpg。
        /// </summary>
        public const string HandoffBackupFolderName = "交接备份";

        /// <summary>交接备份默认保留天数（一个月）。</summary>
        public const int DefaultHandoffBackupKeepDays = 30;

        /// <summary>
        /// 枚举交接图像：仅扫描交接目录根下一层 jpg/png（扁平 {产品名}.jpg）。
        /// 不再扫描产品子目录。
        /// </summary>
        public static IReadOnlyList<string> EnumerateHandoffImages(string handoffFolder)
        {
            if (string.IsNullOrWhiteSpace(handoffFolder) || !Directory.Exists(handoffFolder))
            {
                return Array.Empty<string>();
            }

            return Directory.EnumerateFiles(handoffFolder)
                .Where(IsImageFile)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 交接备份根：以「标注结果根目录」为锚点 → {Parent(SliceExportFolder)}/交接备份
        /// </summary>
        /// <param name="annotationResultRoot">AI 标注界面的标注结果根目录（SliceExportFolder）。</param>
        public static string BuildHandoffBackupRoot(string annotationResultRoot)
        {
            if (string.IsNullOrWhiteSpace(annotationResultRoot))
            {
                throw new InvalidOperationException("标注结果根目录为空，无法解析备份路径");
            }

            var resultFull = Path.GetFullPath(annotationResultRoot.Trim());
            var parent = Path.GetDirectoryName(
                resultFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(parent))
            {
                throw new InvalidOperationException(
                    $"标注结果根目录无上级路径，无法创建「{HandoffBackupFolderName}」：{resultFull}");
            }

            return Path.Combine(parent, HandoffBackupFolderName);
        }

        /// <summary>
        /// 相对交接根的路径；现行扁平结构即为文件名本身。
        /// </summary>
        public static string GetHandoffRelativePath(string handoffFolder, string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                throw new ArgumentException("图像路径为空", nameof(imagePath));
            }

            var handoffFull = Path.GetFullPath(handoffFolder.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var imageFull = Path.GetFullPath(imagePath);
            var prefix = handoffFull + Path.DirectorySeparatorChar;
            if (imageFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return imageFull.Substring(prefix.Length);
            }

            return Path.GetFileName(imageFull);
        }

        /// <summary>
        /// 将已标注完成的交接图复制到「标注结果根目录」上级的
        /// 「交接备份/yyyy-MM-dd/{产品名}.jpg」，再删除交接源。
        /// </summary>
        /// <param name="handoffFolder">上视图交接目录（源图所在）。</param>
        /// <param name="annotationResultRoot">标注结果根目录（决定备份落在其上级）。</param>
        /// <returns>备份目标完整路径。</returns>
        public static string BackupAndRemoveHandoffImage(
            string handoffFolder,
            string imagePath,
            string annotationResultRoot,
            DateTime? now = null)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("待备份的交接图不存在", imagePath);
            }

            var relative = GetHandoffRelativePath(handoffFolder, imagePath);
            var backupRoot = BuildHandoffBackupRoot(annotationResultRoot);
            var dayName = (now ?? DateTime.Now).ToString("yyyy-MM-dd");
            var destPath = Path.Combine(backupRoot, dayName, relative);
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrWhiteSpace(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(imagePath, destPath, overwrite: true);

            try
            {
                File.Delete(imagePath);
            }
            catch (Exception ex)
            {
                throw new IOException(
                    $"交接图已备份到「{destPath}」，但删除交接源失败：{imagePath}",
                    ex);
            }

            // 兼容清理：若仍有旧版产品子目录且已空，则删除
            TryDeleteEmptyHandoffProductDir(handoffFolder, imagePath);
            return destPath;
        }

        /// <summary>
        /// 删除「交接备份」下超过保留天数的 yyyy-MM-dd 子目录（默认 30 天）。
        /// </summary>
        /// <param name="annotationResultRoot">标注结果根目录（与备份锚点一致）。</param>
        public static (int DeletedDirs, int DeletedFiles) CleanupExpiredHandoffBackups(
            string annotationResultRoot,
            int keepDays = DefaultHandoffBackupKeepDays)
        {
            if (string.IsNullOrWhiteSpace(annotationResultRoot))
            {
                return (0, 0);
            }

            string backupRoot;
            try
            {
                backupRoot = BuildHandoffBackupRoot(annotationResultRoot);
            }
            catch
            {
                return (0, 0);
            }

            return CTAutoSlicerSettings.CleanupExpiredCaptureDayFolders(backupRoot, keepDays);
        }

        /// <summary>产品子目录内图删空后移除该空文件夹（不删交接根）。</summary>
        private static void TryDeleteEmptyHandoffProductDir(string handoffFolder, string imagePath)
        {
            try
            {
                var handoffFull = Path.GetFullPath(handoffFolder.Trim())
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dir = Path.GetDirectoryName(Path.GetFullPath(imagePath));
                if (string.IsNullOrWhiteSpace(dir))
                {
                    return;
                }

                var dirFull = Path.GetFullPath(dir)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(dirFull, handoffFull, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var parentOfDir = Path.GetDirectoryName(dirFull);
                if (!string.Equals(parentOfDir, handoffFull, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (Directory.Exists(dirFull)
                    && !Directory.EnumerateFileSystemEntries(dirFull).Any())
                {
                    Directory.Delete(dirFull);
                }
            }
            catch
            {
                // 占用中则跳过，不影响主流程
            }
        }

        /// <summary>
        /// AI 批处理输出目录：{resultRoot}/{产品名}。
        /// 现行扁平交接：产品名=文件名去扩展（…/1161-F.jpg → 1161-F）；
        /// 兼容旧路径：…/产品名/050.jpg → 产品名取父目录。
        /// </summary>
        public static string BuildBatchAnnotationFolder(string resultRoot, string sourceImagePath)
        {
            var parentDir = Path.GetDirectoryName(sourceImagePath);
            var parentName = string.IsNullOrWhiteSpace(parentDir)
                ? null
                : Path.GetFileName(parentDir);
            var fileStem = Path.GetFileNameWithoutExtension(sourceImagePath);

            string productName;
            if (!string.IsNullOrWhiteSpace(parentName)
                && IsExactNumericSliceFileName(fileStem))
            {
                // 兼容旧：…/导出名/050.jpg → 产品名=导出名
                productName = parentName;
            }
            else
            {
                // 现行扁平：…/1161-F.jpg → 产品名=1161-F
                productName = string.IsNullOrWhiteSpace(fileStem) ? "annotation" : fileStem;
            }

            return Path.Combine(resultRoot, productName);
        }

        /// <summary>
        /// 结果图主文件名（无扩展名）：与导入时产品文件夹名一致，避免全是 result_enhance 无法对照。
        /// 优先 exportBaseName；其次输出目录叶名（兼容旧 …/AI标注 取其父目录）；再回退交接图父目录或文件名主干。
        /// </summary>
        public static string ResolveAnnotationResultBaseName(
            string annotationFolder,
            string? sourceImagePath = null,
            string? exportBaseName = null)
        {
            if (!string.IsNullOrWhiteSpace(exportBaseName))
            {
                var fromExport = Path.GetFileName(exportBaseName.Trim());
                if (!string.IsNullOrWhiteSpace(fromExport))
                {
                    return fromExport;
                }
            }

            if (!string.IsNullOrWhiteSpace(annotationFolder))
            {
                var trimmed = annotationFolder.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                var leaf = Path.GetFileName(trimmed);
                // 兼容旧路径 …/{产品名}/AI标注
                if (string.Equals(leaf, LegacyAnnotationFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    var parent = Path.GetDirectoryName(trimmed);
                    var product = string.IsNullOrWhiteSpace(parent) ? null : Path.GetFileName(parent);
                    if (!string.IsNullOrWhiteSpace(product))
                    {
                        return product;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(leaf))
                {
                    // 现行：…/{产品名}
                    return leaf;
                }
            }

            if (!string.IsNullOrWhiteSpace(sourceImagePath))
            {
                var parentDir = Path.GetDirectoryName(sourceImagePath);
                var parentName = string.IsNullOrWhiteSpace(parentDir)
                    ? null
                    : Path.GetFileName(parentDir);
                var fileStem = Path.GetFileNameWithoutExtension(sourceImagePath);
                if (!string.IsNullOrWhiteSpace(parentName)
                    && IsExactNumericSliceFileName(fileStem))
                {
                    return parentName;
                }

                if (!string.IsNullOrWhiteSpace(fileStem))
                {
                    return fileStem;
                }
            }

            return "annotation";
        }

        /// <summary>
        /// 在标注结果根目录下查找最新一次 AI 标注结果图（优先 annotation.json 同目录的产品结果图）。
        /// </summary>
        public static string? TryFindLatestAiResultImage(string? resultRoot)
        {
            if (string.IsNullOrWhiteSpace(resultRoot) || !Directory.Exists(resultRoot))
            {
                return null;
            }

            try
            {
                var rootFull = Path.GetFullPath(resultRoot);
                var jsonDirs = Directory
                    .EnumerateFiles(rootFull, "annotation.json", SearchOption.AllDirectories)
                    .Select(p => new
                    {
                        Dir = Path.GetDirectoryName(p)!,
                        Time = File.GetLastWriteTimeUtc(p),
                    })
                    .Where(x => !IsUnderManualOutputFolder(rootFull, x.Dir))
                    .OrderByDescending(x => x.Time)
                    .ToList();

                foreach (var item in jsonDirs)
                {
                    var hit = PickResultImageInFolder(item.Dir);
                    if (hit is not null)
                    {
                        return hit;
                    }
                }

                // 无 annotation.json 时：取最新 jpg/png（排除 input_for_njct / input/ / 手动 result|output）
                return Directory
                    .EnumerateFiles(rootFull, "*.*", SearchOption.AllDirectories)
                    .Where(IsImageFile)
                    .Where(p => !IsUnderManualOutputFolder(rootFull, p))
                    .Where(p =>
                    {
                        var name = Path.GetFileName(p);
                        return !name.StartsWith("input_for_njct", StringComparison.OrdinalIgnoreCase)
                               && !name.StartsWith("prebrighten", StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 是否位于结果根下的二次手动标注导出子目录 <c>result/</c>、历史 <c>output/</c>，
        /// 或 AI 预处理送检子目录 <c>input/</c>。
        /// 注意：结果根本身可以叫 output/result，不能用绝对路径 Contains 误杀。
        /// </summary>
        private static bool IsUnderManualOutputFolder(string rootFull, string path)
        {
            try
            {
                var root = Path.GetFullPath(rootFull)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var full = Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                string relative;
                if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                relative = Path.GetRelativePath(root, full);
                if (string.IsNullOrWhiteSpace(relative)
                    || relative == "."
                    || relative.StartsWith("..", StringComparison.Ordinal))
                {
                    return false;
                }

                // 相对路径中出现独立段 result / output / input 时视为导出或预处理目录
                var segments = relative.Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries);
                return segments.Any(s =>
                    string.Equals(s, ManualMeasureExportHelper.DefaultExportFolderName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s, ManualMeasureExportHelper.LegacyExportFolderName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s, OfflineCtPreBrightenHelper.InputSubFolderName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private static string? PickResultImageInFolder(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return null;
            }

            var leaf = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            // 旧路径 …/产品名/AI标注 → 产品名取父目录
            string productName = leaf;
            if (string.Equals(leaf, LegacyAnnotationFolderName, StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(folder);
                productName = string.IsNullOrWhiteSpace(parent) ? leaf : Path.GetFileName(parent);
            }

            var preferred = new[]
            {
                Path.Combine(folder, $"{productName}.jpg"),
                Path.Combine(folder, $"{productName}.png"),
                Path.Combine(folder, $"{productName}_enhance.jpg"),
                Path.Combine(folder, "result_enhance.jpg"),
                Path.Combine(folder, "result_origin.jpg"),
            };
            foreach (var p in preferred)
            {
                if (File.Exists(p))
                {
                    return p;
                }
            }

            return Directory
                .EnumerateFiles(folder)
                .Where(IsImageFile)
                .Where(p =>
                {
                    var name = Path.GetFileName(p);
                    return !name.StartsWith("input_for_njct", StringComparison.OrdinalIgnoreCase);
                })
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        /// <summary>
        /// 构建 AI 结果图路径。
        /// 增强图：<c>{产品名}.jpg</c>；原图：始终 <c>{产品名}_origin.jpg</c>
        ///（含「仅原图」模式，便于手动标注按同一规则识别并 ÷2）。
        /// </summary>
        public static string BuildAnnotationResultImagePath(
            string annotationFolder,
            string resultBaseName,
            string modeTag,
            bool bothModes)
        {
            var safe = string.IsNullOrWhiteSpace(resultBaseName)
                ? "annotation"
                : resultBaseName.Trim();
            var isOrigin = string.Equals(modeTag, "origin", StringComparison.OrdinalIgnoreCase);
            // 原图始终带 _origin；bothModes 参数保留以兼容调用方，命名不再依赖它
            _ = bothModes;
            var fileName = isOrigin
                ? $"{safe}_origin.jpg"
                : $"{safe}.jpg";
            return Path.Combine(annotationFolder, fileName);
        }

        /// <summary>
        /// 将上视图按「竖图约定」保存到 outputPath（原图不变）：横图则 CW90°，已竖则原样写入。
        /// </summary>
        public static string SaveClockwiseRotated90(string sourceImagePath, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                throw new FileNotFoundException("待旋转的上视图不存在", sourceImagePath);
            }

            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 先读入内存再释放文件锁，避免 Image.FromFile 长期占用源文件
            using var source = LoadBitmapUnlocked(sourceImagePath);
            EnsurePortraitByClockwise90IfLandscape(source);

            var format = ResolveImageFormat(outputPath);
            source.Save(outputPath, format);
            return outputPath;
        }

        /// <summary>
        /// 已废弃：勿再生成 input_rotated_cw90；请用 OfflineCtPreBrightenHelper.PrepareFinalInputImage（横图 CW90 → input/）。
        /// </summary>
        [Obsolete("不再落盘 input_rotated_cw90，请使用 OfflineCtPreBrightenHelper.PrepareFinalInputImage")]
        public static string CreateRotatedInputForAnnotation(string sourceImagePath, string annotationFolder)
        {
            var ext = Path.GetExtension(sourceImagePath);
            if (string.IsNullOrWhiteSpace(ext) || !ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                ext = ".jpg";
            }

            var outputPath = Path.Combine(annotationFolder, RotatedInputFileNameWithoutExt + ext.ToLowerInvariant());
            return SaveClockwiseRotated90(sourceImagePath, outputPath);
        }

        private static Bitmap LoadBitmapUnlocked(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var temp = new Bitmap(fs);
            return new Bitmap(temp);
        }

        private static ImageFormat ResolveImageFormat(string path)
        {
            var ext = Path.GetExtension(path);
            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Png;
            }

            if (ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Bmp;
            }

            if (ext.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Tiff;
            }

            return ImageFormat.Jpeg;
        }

        /// <summary>
        /// 按<strong>精确文件名</strong>选取「上」视图切片图。
        /// 例如 sliceIndex=50、前缀 <see cref="SliceExportNamingHelper.ImageStackBaseFileName"/>=<c>0</c>
        /// → 查找 <c>050.jpg</c> / <c>050.png</c> 等。
        /// 找不到则返回 null（不再用“最接近序号”兜底）。
        /// </summary>
        /// <param name="topViewFolder">「上」视图导出目录</param>
        /// <param name="sliceIndex">第几张（配置值；实际文件名=前缀+该数字）</param>
        public static string? TryPickTopViewImageByIndex(string topViewFolder, int sliceIndex)
        {
            if (string.IsNullOrWhiteSpace(topViewFolder) || !Directory.Exists(topViewFolder))
            {
                return null;
            }

            var targetIndex = Math.Max(0, sliceIndex);
            var expectedStem = SliceExportNamingHelper.BuildImageStackFileStem(targetIndex);

            // 1) 直接拼路径：050.jpg / 050.jpeg / …
            foreach (var ext in ImageExtensions)
            {
                var direct = Path.Combine(topViewFolder, expectedStem + ext);
                if (File.Exists(direct))
                {
                    return direct;
                }
            }

            // 2) 目录枚举：主干名精确等于 "050"（忽略扩展名大小写）
            foreach (var path in Directory.EnumerateFiles(topViewFolder))
            {
                if (!ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var stem = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(stem, expectedStem, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>文件名主干是否为纯数字切片名（如 50、050、1、100）。</summary>
        public static bool IsExactNumericSliceFileName(string? fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return false;
            }

            var stem = fileNameWithoutExtension.Trim();
            return stem.Length > 0 && stem.All(char.IsDigit);
        }

        /// <summary>
        /// 兼容旧调用：取中间图（quantity/2）。新代码请用 <see cref="TryPickTopViewImageByIndex"/>。
        /// </summary>
        [Obsolete("请使用 TryPickTopViewImageByIndex，并由 OfflineCtTopViewSliceIndex 配置目标文件名序号")]
        public static string? TryPickMiddleTopViewImage(string topViewFolder, int quantity)
        {
            var n = quantity > 0 ? quantity : 0;
            var targetIndex = n > 0 ? Math.Max(1, n / 2) : CTAutoSlicerSettings.DefaultOfflineCtTopViewSliceIndex;
            return TryPickTopViewImageByIndex(topViewFolder, targetIndex);
        }

        public static void WriteAnnotationJson(
            string outputPath,
            string sourceImagePath,
            IReadOnlyList<OfflineCtInferResult> results,
            double? pixelSizeUm = null)
        {
            var payload = new
            {
                sourceImage = sourceImagePath,
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                pixelSizeUm = pixelSizeUm,
                pixelSizeUnit = "μm/px",
                unitConversion = "1 mm = 1000 μm; L_μm = N_px × pixelSizeUm; L_mm = N_px × pixelSizeUm / 1000",
                results = results.Select(r => new
                {
                    mode = r.ModeTag,
                    success = r.IsSuccess,
                    runInfo = r.RunInfo,
                    resultImage = r.ResultImagePath,
                    cells = Enumerable.Range(0, Math.Max(r.RegionRows.Count, Math.Max(r.RegionCols.Count, r.PrintfValues.Count)))
                        .Select(i => new
                        {
                            cellIndex = i + 1,
                            rows = i < r.RegionRows.Count ? r.RegionRows[i] : Array.Empty<double>(),
                            cols = i < r.RegionCols.Count ? r.RegionCols[i] : Array.Empty<double>(),
                            printfValues = i < r.PrintfValues.Count ? r.PrintfValues[i] : Array.Empty<double>(),
                        })
                        .ToList(),
                }).ToList(),
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(outputPath, json, Encoding.UTF8);
        }

        public static void WriteAnnotationCsv(
            string outputPath,
            string sourceImagePath,
            IReadOnlyList<OfflineCtInferResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Mode,Success,RunInfo,ResultImage,SourceImage,CellIndex,PointIndex,Row,Col,PrintfValue");
            foreach (var r in results)
            {
                var cellCount = Math.Max(r.RegionRows.Count, Math.Max(r.RegionCols.Count, r.PrintfValues.Count));
                if (cellCount == 0)
                {
                    sb.AppendLine(string.Join(',',
                        Csv(r.ModeTag),
                        r.IsSuccess ? "1" : "0",
                        Csv(r.RunInfo),
                        Csv(r.ResultImagePath ?? string.Empty),
                        Csv(sourceImagePath),
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty));
                    continue;
                }

                for (var c = 0; c < cellCount; c++)
                {
                    var rows = c < r.RegionRows.Count ? r.RegionRows[c] : Array.Empty<double>();
                    var cols = c < r.RegionCols.Count ? r.RegionCols[c] : Array.Empty<double>();
                    var vals = c < r.PrintfValues.Count ? r.PrintfValues[c] : Array.Empty<double>();
                    var pointCount = Math.Max(rows.Length, Math.Max(cols.Length, vals.Length));
                    if (pointCount == 0)
                    {
                        sb.AppendLine(string.Join(',',
                            Csv(r.ModeTag),
                            r.IsSuccess ? "1" : "0",
                            Csv(r.RunInfo),
                            Csv(r.ResultImagePath ?? string.Empty),
                            Csv(sourceImagePath),
                            (c + 1).ToString(CultureInfo.InvariantCulture),
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty));
                        continue;
                    }

                    for (var p = 0; p < pointCount; p++)
                    {
                        sb.AppendLine(string.Join(',',
                            Csv(r.ModeTag),
                            r.IsSuccess ? "1" : "0",
                            Csv(r.RunInfo),
                            Csv(r.ResultImagePath ?? string.Empty),
                            Csv(sourceImagePath),
                            (c + 1).ToString(CultureInfo.InvariantCulture),
                            (p + 1).ToString(CultureInfo.InvariantCulture),
                            p < rows.Length ? rows[p].ToString(CultureInfo.InvariantCulture) : string.Empty,
                            p < cols.Length ? cols[p].ToString(CultureInfo.InvariantCulture) : string.Empty,
                            p < vals.Length ? vals[p].ToString(CultureInfo.InvariantCulture) : string.Empty));
                    }
                }
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// AI 出图后进程内缓存（按结果图路径 / 所在目录），标注左栏直接取，不落中间文件。
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> AiSummaryByPath =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, string> AiSummaryByDir =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 底部遮罩字号（图像像素）。左中右三栏布局用：比旧预览 bandH/10 更小，避免越界。
        /// </summary>
        public static int ComputeBottomBandFontSizeImagePx(int bandHeightPx, int imageWidthPx = 0)
        {
            var byHeight = Math.Clamp(bandHeightPx / 16, 36, 120);
            if (imageWidthPx <= 0)
            {
                return byHeight;
            }

            // 单栏约 1/3 宽，按「L1: 0.0200mm」约 14 字符估上限
            var colW = imageWidthPx / 3.0;
            var byWidth = Math.Max(28, (int)(colW / 10.0));
            return Math.Min(byHeight, byWidth);
        }

        /// <summary>
        /// 结果图路径是否为原图模式（文件名含 _origin / result_origin）。
        /// </summary>
        public static bool IsOriginResultImagePath(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return false;
            }

            var name = Path.GetFileName(imagePath);
            return name.Contains("_origin", StringComparison.OrdinalIgnoreCase)
                || name.Contains("result_origin", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 按结果图类型把「增强图基准」配置高度换算为该图应用高度。
        /// 规则：文件名含 _origin → ÷2；无后缀的历史仅原图文件若配置高度 ≥ 图高 → 亦按原图 ÷2。
        /// </summary>
        public static int ResolveConfiguredBottomBlackHeightForPath(
            string? imagePath,
            int enhanceConfiguredPx,
            int imageHeightPx = 0)
        {
            var enhance = Math.Max(0, enhanceConfiguredPx);
            if (IsOriginResultImagePath(imagePath))
            {
                return CTAutoSlicerSettings.ResolveOriginBottomBlackHeight(enhance);
            }

            // 兼容旧「仅原图」落盘名 {产品}.jpg（无 _origin）：
            // 增强基准高度已 ≥ 当前图高时，全高无法合理落在该图上，按原图比例 ÷2
            if (imageHeightPx > 0 && enhance >= imageHeightPx)
            {
                return CTAutoSlicerSettings.ResolveOriginBottomBlackHeight(enhance);
            }

            return enhance;
        }

        /// <summary>
        /// 解析底栏实际高度（图像像素）。
        /// 优先对齐结果图中已烧录的 AI 底栏；检测失败则按路径/尺寸换算配置值；再钳制到图像高度。
        /// </summary>
        public static int ResolveDisplayBottomBlackHeight(
            int configuredEnhancePx,
            int detectedBurnedPx,
            int imageHeightPx = 0,
            string? imagePath = null)
        {
            var configured = ResolveConfiguredBottomBlackHeightForPath(
                imagePath,
                configuredEnhancePx,
                imageHeightPx);
            var h = detectedBurnedPx > 0
                ? detectedBurnedPx
                : configured;
            if (imageHeightPx <= 0)
            {
                return Math.Max(0, h);
            }

            return Math.Clamp(h, 0, imageHeightPx);
        }

        /// <summary>
        /// 自下而上检测结果图已烧录的底部打黑高度（图像像素）。
        /// 采样左 3/4（AI 汇总区），忽略彩色文字像素；检测失败返回 0。
        /// </summary>
        public static int DetectBottomBlackBandHeight(Bitmap bitmap)
        {
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return 0;
            }

            var w = bitmap.Width;
            var h = bitmap.Height;
            var sampleRight = Math.Max(1, w * 3 / 4);
            var stepX = Math.Max(1, sampleRight / 64);
            var darkRowMinRatio = 0.72;
            var maxBand = Math.Min(h, Math.Max(h * 2 / 3, 1));

            var band = 0;
            for (var y = h - 1; y >= h - maxBand; y--)
            {
                var dark = 0;
                var total = 0;
                for (var x = 0; x < sampleRight; x += stepX)
                {
                    var p = bitmap.GetPixel(x, y);
                    total++;
                    if (IsBottomBandDarkPixel(p.R, p.G, p.B))
                    {
                        dark++;
                    }
                }

                if (total <= 0 || dark < total * darkRowMinRatio)
                {
                    break;
                }

                band++;
            }

            // 过矮更像噪点/单行黑边，不作打黑底栏
            return band >= 40 ? band : 0;
        }

        /// <summary>
        /// WPF <see cref="System.Windows.Media.Imaging.BitmapSource"/> 版本的底部打黑高度检测。
        /// </summary>
        public static int DetectBottomBlackBandHeight(System.Windows.Media.Imaging.BitmapSource? source)
        {
            if (source is null || source.PixelWidth <= 0 || source.PixelHeight <= 0)
            {
                return 0;
            }

            BitmapSource converted = source;
            if (source.Format != PixelFormats.Bgra32
                && source.Format != PixelFormats.Bgr32
                && source.Format != PixelFormats.Pbgra32)
            {
                converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }

            var w = converted.PixelWidth;
            var h = converted.PixelHeight;
            var stride = w * 4;
            var pixels = new byte[stride * h];
            converted.CopyPixels(pixels, stride, 0);

            var sampleRight = Math.Max(1, w * 3 / 4);
            var stepX = Math.Max(1, sampleRight / 64);
            var darkRowMinRatio = 0.72;
            var maxBand = Math.Min(h, Math.Max(h * 2 / 3, 1));

            var band = 0;
            for (var y = h - 1; y >= h - maxBand; y--)
            {
                var dark = 0;
                var total = 0;
                var row = y * stride;
                for (var x = 0; x < sampleRight; x += stepX)
                {
                    var i = row + x * 4;
                    var b = pixels[i];
                    var g = pixels[i + 1];
                    var r = pixels[i + 2];
                    total++;
                    if (IsBottomBandDarkPixel(r, g, b))
                    {
                        dark++;
                    }
                }

                if (total <= 0 || dark < total * darkRowMinRatio)
                {
                    break;
                }

                band++;
            }

            return band >= 40 ? band : 0;
        }

        /// <summary>
        /// 底栏像素：纯黑/近黑，或暗底上的深色文字（仍算在打黑带内）。
        /// 亮色旁注（绿/红/黄/白）不计入「非黑」，靠同行暗像素占比判断。
        /// </summary>
        private static bool IsBottomBandDarkPixel(int r, int g, int b)
        {
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            // 近黑
            if (max <= 48)
            {
                return true;
            }

            // 暗底上的低亮度彩色字（仍属底栏）
            return max <= 96 && (max - min) <= 40;
        }

        /// <summary>
        /// 标注界面屏幕字号：与出图字号同步（×zoom），介于旧预览与旧标注页之间。
        /// </summary>
        public static double ComputeBottomBandFontSizeScreen(int bandHeightPx, double zoom, int imageWidthPx = 0)
        {
            var imgPx = ComputeBottomBandFontSizeImagePx(bandHeightPx, imageWidthPx);
            return Math.Clamp(imgPx * Math.Max(zoom, 0.05), 16, 42);
        }

        /// <summary>按 AllPrintfValue 拼汇总文字（与结果图底部一致：仅 mm，不显 px）。</summary>
        public static string FormatAiDimensionSummaryText(
            IReadOnlyList<double[]>? printfValues,
            double pixelSizeUm)
        {
            var umPerPx = pixelSizeUm > 0 ? pixelSizeUm : 1.0;
            if (printfValues is null || printfValues.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("AI尺寸汇总");
            var any = false;
            for (var i = 0; i < printfValues.Count; i++)
            {
                var vals = printfValues[i];
                if (vals is null || vals.Length == 0)
                {
                    continue;
                }

                any = true;
                sb.Append(CultureInfo.InvariantCulture, $"电芯{i + 1}");
                for (var k = 0; k < vals.Length; k++)
                {
                    var px = vals[k];
                    var mm = px * umPerPx / 1000.0;
                    // 统一 mm（与结果图底部/旁注一致，不再按阈值切 μm）
                    sb.Append(CultureInfo.InvariantCulture, $"  L{k + 1}:{mm:F4}mm");
                }

                sb.AppendLine();
            }

            if (!any)
            {
                return string.Empty;
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>AI 出图成功后调用：把汇总记入内存，供二次标注左栏立刻显示。</summary>
        public static void RememberAiDimensionSummary(string? resultImagePath, string summaryText)
        {
            if (string.IsNullOrWhiteSpace(resultImagePath) || string.IsNullOrWhiteSpace(summaryText))
            {
                return;
            }

            try
            {
                var full = Path.GetFullPath(resultImagePath);
                AiSummaryByPath[full] = summaryText;
                var dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir))
                {
                    AiSummaryByDir[dir] = summaryText;
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// 取 AI 尺寸汇总：优先内存（出图时写入），其次 annotation.json（若已导出）。
        /// </summary>
        public static string TryBuildAiDimensionSummaryText(
            string resultImageOrFolderPath,
            double pixelSizeUmFallback = 1.0)
        {
            if (TryGetRememberedAiSummary(resultImageOrFolderPath, out var remembered))
            {
                return remembered;
            }

            var fromJson = TryBuildAiDimensionSummaryTextFromJson(
                resultImageOrFolderPath,
                pixelSizeUmFallback);
            if (!string.IsNullOrWhiteSpace(fromJson))
            {
                RememberAiDimensionSummary(resultImageOrFolderPath, fromJson);
            }

            return fromJson;
        }

        /// <summary>打开标注目录时：把各产品 annotation.json 载入内存，左栏可显示。</summary>
        public static void PreloadAiSummariesUnderFolder(string? rootFolder, double pixelSizeUmFallback = 1.0)
        {
            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            {
                return;
            }

            try
            {
                foreach (var jsonPath in Directory.EnumerateFiles(
                             Path.GetFullPath(rootFolder),
                             "annotation.json",
                             SearchOption.AllDirectories))
                {
                    var text = TryBuildAiDimensionSummaryTextFromJson(jsonPath, pixelSizeUmFallback);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    var dir = Path.GetDirectoryName(jsonPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        AiSummaryByDir[dir] = text;
                    }

                    // 若 json 内带了 resultImage，再按文件路径记一份
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath, Encoding.UTF8));
                        if (!doc.RootElement.TryGetProperty("results", out var results)
                            || results.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var r in results.EnumerateArray())
                        {
                            if (!r.TryGetProperty("resultImage", out var img)
                                || img.ValueKind != JsonValueKind.String)
                            {
                                continue;
                            }

                            var p = img.GetString();
                            if (!string.IsNullOrWhiteSpace(p))
                            {
                                RememberAiDimensionSummary(p, text);
                            }
                        }
                    }
                    catch
                    {
                        // dir 级已缓存即可
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private static bool TryGetRememberedAiSummary(string resultImageOrFolderPath, out string text)
        {
            text = string.Empty;
            if (string.IsNullOrWhiteSpace(resultImageOrFolderPath))
            {
                return false;
            }

            try
            {
                var full = Path.GetFullPath(resultImageOrFolderPath.Trim());
                if (AiSummaryByPath.TryGetValue(full, out var byPath)
                    && !string.IsNullOrWhiteSpace(byPath))
                {
                    text = byPath;
                    return true;
                }

                var dir = Directory.Exists(full) ? full : Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir)
                    && AiSummaryByDir.TryGetValue(dir, out var byDir)
                    && !string.IsNullOrWhiteSpace(byDir))
                {
                    text = byDir;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static string TryBuildAiDimensionSummaryTextFromJson(
            string resultImageOrFolderPath,
            double pixelSizeUmFallback)
        {
            var jsonPath = ResolveAnnotationJsonPath(resultImageOrFolderPath);
            if (jsonPath is null)
            {
                return string.Empty;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath, Encoding.UTF8));
                var root = doc.RootElement;
                var umPerPx = pixelSizeUmFallback > 0 ? pixelSizeUmFallback : 1.0;
                if (root.TryGetProperty("pixelSizeUm", out var umNode)
                    && umNode.ValueKind == JsonValueKind.Number
                    && umNode.TryGetDouble(out var umFromJson)
                    && umFromJson > 0)
                {
                    umPerPx = umFromJson;
                }

                if (!root.TryGetProperty("results", out var results)
                    || results.ValueKind != JsonValueKind.Array
                    || results.GetArrayLength() == 0)
                {
                    return string.Empty;
                }

                JsonElement? chosen = null;
                foreach (var r in results.EnumerateArray())
                {
                    var ok = !r.TryGetProperty("success", out var s) || s.ValueKind != JsonValueKind.False;
                    if (ok)
                    {
                        chosen = r;
                    }
                }

                chosen ??= results[results.GetArrayLength() - 1];
                if (!chosen.Value.TryGetProperty("cells", out var cells)
                    || cells.ValueKind != JsonValueKind.Array
                    || cells.GetArrayLength() == 0)
                {
                    return string.Empty;
                }

                var lists = new List<double[]>();
                foreach (var cell in cells.EnumerateArray())
                {
                    if (!cell.TryGetProperty("printfValues", out var vals)
                        || vals.ValueKind != JsonValueKind.Array)
                    {
                        lists.Add([]);
                        continue;
                    }

                    var arr = new double[vals.GetArrayLength()];
                    for (var k = 0; k < arr.Length; k++)
                    {
                        arr[k] = vals[k].ValueKind == JsonValueKind.Number && vals[k].TryGetDouble(out var d)
                            ? d
                            : 0;
                    }

                    lists.Add(arr);
                }

                return FormatAiDimensionSummaryText(lists, umPerPx);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string? ResolveAnnotationJsonPath(string resultImageOrFolderPath)
        {
            if (string.IsNullOrWhiteSpace(resultImageOrFolderPath))
            {
                return null;
            }

            string? dir;
            try
            {
                var full = Path.GetFullPath(resultImageOrFolderPath.Trim());
                // 若本身就是 annotation.json
                if (File.Exists(full)
                    && string.Equals(Path.GetFileName(full), "annotation.json", StringComparison.OrdinalIgnoreCase))
                {
                    return full;
                }

                dir = Directory.Exists(full) ? full : Path.GetDirectoryName(full);
            }
            catch
            {
                return null;
            }

            for (var i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
            {
                var beside = Path.Combine(dir, "annotation.json");
                if (File.Exists(beside))
                {
                    return beside;
                }

                var parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent)
                    || string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                dir = parent;
            }

            return null;
        }

        private static string Csv(string value)
        {
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
