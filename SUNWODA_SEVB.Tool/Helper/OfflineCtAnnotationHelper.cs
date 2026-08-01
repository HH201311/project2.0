using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
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

        /// <summary>已废弃中间落盘名；最终推理图请用 OfflineCtPreBrightenHelper.FinalInputFileNameWithoutExt。</summary>
        [Obsolete("不再单独落盘旋转中间图，请使用 PrepareFinalInputImage(rotateCw90: true)")]
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
        /// 交接相对路径：{导出名产品文件夹}/{源文件名}，例如「0013-Z …/50.jpg」。
        /// 保留原切片文件名，用产品子目录避免多产品 50.jpg 互相覆盖。
        /// </summary>
        public static string BuildHandoffRelativePath(string exportBaseName, string sourceImagePath)
        {
            var productFolder = string.IsNullOrWhiteSpace(exportBaseName)
                ? "export"
                : Path.GetFileName(exportBaseName.Trim());
            var fileName = Path.GetFileName(sourceImagePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "slice.jpg";
            }

            return Path.Combine(productFolder, fileName);
        }

        /// <summary>
        /// 兼容旧调用；新交接请用 <see cref="BuildHandoffRelativePath"/>（产品子目录 + 原文件名）。
        /// </summary>
        [Obsolete("请使用 BuildHandoffRelativePath：产品文件夹/原文件名，避免 50.jpg 冲突")]
        public static string BuildHandoffFileName(string exportBaseName, int sliceIndex, string sourceImagePath)
        {
            var safeBase = string.IsNullOrWhiteSpace(exportBaseName)
                ? "export"
                : Path.GetFileName(exportBaseName.Trim());
            var ext = Path.GetExtension(sourceImagePath);
            if (string.IsNullOrWhiteSpace(ext)
                || !ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                ext = ".jpg";
            }

            return $"{safeBase}_{Math.Max(1, sliceIndex):D3}{ext.ToLowerInvariant()}";
        }

        /// <summary>
        /// 将上视图送检图复制到交接目录「产品名」子文件夹，文件名保持为源名（如 50.jpg）。
        /// </summary>
        public static string CopyTopViewImageToHandoffFolder(
            string sourceImagePath,
            string handoffFolder,
            string exportBaseName,
            int sliceIndex)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                throw new FileNotFoundException("待交接的上视图送检图不存在", sourceImagePath);
            }

            if (string.IsNullOrWhiteSpace(handoffFolder))
            {
                throw new InvalidOperationException("请先配置上视图交接目录（TopViewHandoffFolder）");
            }

            Directory.CreateDirectory(handoffFolder);
            var relative = BuildHandoffRelativePath(exportBaseName, sourceImagePath);
            var destPath = Path.Combine(handoffFolder, relative);
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrWhiteSpace(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(sourceImagePath, destPath, overwrite: true);
            return destPath;
        }

        /// <summary>
        /// 备份根目录名：落在「标注结果根目录」上级 →
        /// {Parent(SliceExportFolder)}/交接备份/{yyyy-MM-dd}/{产品名}/图。
        /// </summary>
        public const string HandoffBackupFolderName = "交接备份";

        /// <summary>交接备份默认保留天数（一个月）。</summary>
        public const int DefaultHandoffBackupKeepDays = 30;

        /// <summary>
        /// 枚举交接图像：优先「产品子目录」内一层图片；兼容根目录遗留的扁平文件。
        /// </summary>
        public static IReadOnlyList<string> EnumerateHandoffImages(string handoffFolder)
        {
            if (string.IsNullOrWhiteSpace(handoffFolder) || !Directory.Exists(handoffFolder))
            {
                return Array.Empty<string>();
            }

            var fromProductDirs = Directory.EnumerateDirectories(handoffFolder)
                .SelectMany(dir => Directory.EnumerateFiles(dir).Where(IsImageFile));

            var fromRoot = Directory.EnumerateFiles(handoffFolder).Where(IsImageFile);

            return fromProductDirs
                .Concat(fromRoot)
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
        /// 相对交接根的路径，保持「产品文件夹/文件名」结构；扁平遗留文件则为文件名本身。
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

            // 路径不在交接根下时回退：父目录名/文件名 或仅文件名
            var parentDir = Path.GetDirectoryName(imageFull);
            var fileName = Path.GetFileName(imageFull);
            if (!string.IsNullOrWhiteSpace(parentDir)
                && !string.Equals(
                    Path.GetPathRoot(parentDir),
                    parentDir,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(Path.GetFileName(parentDir), fileName);
            }

            return fileName;
        }

        /// <summary>
        /// 将已标注完成的交接图按原结构复制到「标注结果根目录」上级的
        /// 「交接备份/yyyy-MM-dd/…」，再删除交接源（空产品夹一并删）。
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
        /// AI 批处理输出目录：{resultRoot}/{产品名}（不再套一层 AI标注）。
        /// 产品名优先取交接图父目录名（…/产品名/50.jpg）；扁平遗留文件则用文件名去扩展。
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
                // …/导出名/50.jpg → 产品名=导出名
                productName = parentName;
            }
            else if (!string.IsNullOrWhiteSpace(parentName)
                     && !string.Equals(
                         Path.GetPathRoot(parentDir),
                         parentDir,
                         StringComparison.OrdinalIgnoreCase))
            {
                // 父目录不是盘符根时，仍优先用父目录名（新产品结构）
                productName = parentName;
            }
            else
            {
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

                // 无 annotation.json 时：取最新 jpg/png（排除 input_for_njct / 手动 output）
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
        /// 是否位于结果根下的二次手动标注导出子目录 <c>output/</c>。
        /// 注意：结果根本身可以叫 output（如 D:\3Ddeliever\output），不能用绝对路径 Contains("\output\") 误杀。
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

                // 仅当相对路径中出现独立段「output」时视为手动导出目录（…/output/…）
                var segments = relative.Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries);
                return segments.Any(s =>
                    string.Equals(s, "output", StringComparison.OrdinalIgnoreCase));
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
        /// 构建 AI 结果图路径。默认 <c>{产品名}.jpg</c>；
        /// 同时导出 enhance+origin 时，origin 为 <c>{产品名}_origin.jpg</c>。
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
            var fileName = isOrigin && bothModes
                ? $"{safe}_origin.jpg"
                : $"{safe}.jpg";
            return Path.Combine(annotationFolder, fileName);
        }

        /// <summary>
        /// 将上视图顺时针旋转 90° 后保存到 outputPath（原图不变）。主流程已改为内存旋转+input_for_njct，一般不再调用。
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
            source.RotateFlip(RotateFlipType.Rotate90FlipNone);

            var format = ResolveImageFormat(outputPath);
            source.Save(outputPath, format);
            return outputPath;
        }

        /// <summary>
        /// 已废弃：勿再生成 input_rotated_cw90；请用 OfflineCtPreBrightenHelper.PrepareFinalInputImage(..., rotateCw90: true)。
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
