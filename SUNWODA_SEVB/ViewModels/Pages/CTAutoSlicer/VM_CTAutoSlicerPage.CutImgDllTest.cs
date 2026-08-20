using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Services.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.ViewModels.Pages.CTAutoSlicer
{
    /// <summary>
    /// CT 自动化切片页面 ViewModel（partial：CutImgDllTest）。
    /// </summary>
    public partial class VM_CTAutoSlicerPage
    {
        private void ExecuteBrowseCutImgDllTestSectionFolder(object? _)
        {
            using var dialog = new CommonOpenFileDialog
            {
                Title = "选择 3D侧视图测试文件夹（GetSectionReferencePoints）",
                IsFolderPicker = true,
            };
            var initial = ResolveExistingDirectoryOrParent(CutImgDllTestSectionFolder)
                ?? (Directory.Exists(CTAutoSlicerSettings.DefaultVisionCaptureDir)
                    ? CTAutoSlicerSettings.DefaultVisionCaptureDir
                    : null);
            if (!string.IsNullOrWhiteSpace(initial))
            {
                dialog.InitialDirectory = initial;
            }

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                CutImgDllTestSectionFolder = dialog.FileName;
            }
        }

        private void ExecuteBrowseCutImgDllTestVectorFolder(object? _)
        {
            using var dialog = new CommonOpenFileDialog
            {
                Title = "选择 2D侧视图测试文件夹（GetObjectTopBottomPoints）",
                IsFolderPicker = true,
            };
            var initial = ResolveExistingDirectoryOrParent(CutImgDllTestVectorFolder)
                ?? (Directory.Exists(CTAutoSlicerSettings.DefaultVision2dVectorCaptureDir)
                    ? CTAutoSlicerSettings.DefaultVision2dVectorCaptureDir
                    : null);
            if (!string.IsNullOrWhiteSpace(initial))
            {
                dialog.InitialDirectory = initial;
            }

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                CutImgDllTestVectorFolder = dialog.FileName;
            }
        }

        private void ExecuteBrowseCutImgDllTestOutputDir(object? _)
        {
            using var dialog = new CommonOpenFileDialog
            {
                Title = "选择 CTAutoCutImg 标注输出目录",
                IsFolderPicker = true,
            };
            var initial = string.IsNullOrWhiteSpace(CutImgDllTestOutputDir)
                ? CTAutoSlicerSettings.DefaultCutImgDllTestOutputDir
                : CutImgDllTestOutputDir.Trim();
            if (Directory.Exists(initial))
            {
                dialog.InitialDirectory = initial;
            }

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                CutImgDllTestOutputDir = dialog.FileName;
            }
        }

        private void EnsureCutImgDllReadyForOfflineTest()
        {
            if (_cutImgAlgorithmService.IsInitialized)
            {
                return;
            }

            _cutImgAlgorithmService.Initialize();
        }

        private string ResolveCutImgDllTestOutputDir()
        {
            var dir = string.IsNullOrWhiteSpace(CutImgDllTestOutputDir)
                ? CTAutoSlicerSettings.DefaultCutImgDllTestOutputDir
                : CutImgDllTestOutputDir.Trim();
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// 读取文件夹配置；兼容旧版单图路径（自动取所在目录）。
        /// </summary>
        private string LoadCutImgDllTestFolderSetting(string key, string legacyKey)
        {
            var raw = LoadStringSetting(key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = LoadStringSetting(legacyKey, string.Empty);
            }

            return NormalizeCutImgDllTestFolderPath(raw);
        }

        /// <summary>
        /// 若曾保存为单图路径，归一化为所在文件夹；已是目录则原样返回。
        /// </summary>
        private static string NormalizeCutImgDllTestFolderPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var trimmed = path.Trim();
            if (Directory.Exists(trimmed))
            {
                return trimmed;
            }

            if (File.Exists(trimmed))
            {
                return Path.GetDirectoryName(trimmed) ?? string.Empty;
            }

            var ext = Path.GetExtension(trimmed);
            if (!string.IsNullOrEmpty(ext)
                && CutImgDllTestImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(trimmed) ?? string.Empty;
            }

            return trimmed;
        }

        private static string? ResolveExistingDirectoryOrParent(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var trimmed = path.Trim();
            if (Directory.Exists(trimmed))
            {
                return trimmed;
            }

            if (File.Exists(trimmed))
            {
                var parent = Path.GetDirectoryName(trimmed);
                return Directory.Exists(parent) ? parent : null;
            }

            var dir = Path.GetDirectoryName(trimmed);
            return Directory.Exists(dir) ? dir : null;
        }

        /// <summary>
        /// 枚举文件夹顶层图片（不递归），按文件名排序。
        /// </summary>
        private static IReadOnlyList<string> EnumerateCutImgDllTestImages(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return [];
            }

            return Directory
                .EnumerateFiles(folder)
                .Where(p =>
                {
                    var ext = Path.GetExtension(p);
                    return !string.IsNullOrEmpty(ext)
                           && CutImgDllTestImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
                })
                .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 离线批量测 GetSectionReferencePoints：不跑 VG，对 3D侧视图文件夹内全部图片 → DLL → 标注四点落盘。
        /// 单张失败不中断，继续后续图片。
        /// </summary>
        private async Task ExecuteTestCutImgGetSectionReferencePointsAsync()
        {
            var folder = CutImgDllTestSectionFolder?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageShower = "请先选择 3D侧视图测试文件夹";
                return;
            }

            var images = EnumerateCutImgDllTestImages(folder);
            if (images.Count == 0)
            {
                MessageShower = $"3D侧视图文件夹内未找到图片：{folder}";
                return;
            }

            if (!TryBeginStageTest())
            {
                return;
            }

            try
            {
                _lastFlowLogMessage = null;
                AppendFlowLog("——— CTAutoCutImg 离线批量测：GetSectionReferencePoints（3D侧视图）开始 ———");
                AppendFlowLog($"输入目录：{folder}（共 {images.Count} 张）");

                var (logLines, okCount, failCount) = await Task.Run(() =>
                {
                    EnsureCutImgDllReadyForOfflineTest();
                    var outDir = ResolveCutImgDllTestOutputDir();
                    var lines = new List<string>(images.Count * 3);
                    var ok = 0;
                    var fail = 0;
                    var labels = new[] { "上盖1", "上盖2", "上盖3", "原点" };

                    for (var i = 0; i < images.Count; i++)
                    {
                        var imagePath = images[i];
                        var indexTag = $"{i + 1}/{images.Count}";
                        try
                        {
                            var points = _cutImgAlgorithmService.GetSectionReferencePoints(imagePath);
                            if (points.Length < 4)
                            {
                                throw new InvalidOperationException(
                                    $"返回点数不足：期望 4，实际 {points.Length}");
                            }

                            var annotate = new (int X, int Y, string Label)[4];
                            for (var p = 0; p < 4; p++)
                            {
                                annotate[p] = (points[p].X, points[p].Y, labels[p]);
                            }

                            var stamp = DateTime.Now.ToString("HHmmss_fff");
                            var dest = Path.Combine(
                                outDir,
                                Path.GetFileNameWithoutExtension(imagePath) + $"_section_{stamp}.png");
                            ScreenCaptureHelper.SaveAnnotatedPointsImage(imagePath, dest, annotate);

                            ok++;
                            lines.Add($"OK [{indexTag}] {Path.GetFileName(imagePath)}");
                            for (var p = 0; p < 4; p++)
                            {
                                lines.Add($"  {labels[p]}：({points[p].X},{points[p].Y})");
                            }

                            lines.Add($"  标注图：{dest}");
                        }
                        catch (Exception ex)
                        {
                            fail++;
                            lines.Add($"NG [{indexTag}] {Path.GetFileName(imagePath)}：{ex.Message}");
                        }
                    }

                    return (lines, ok, fail);
                }).ConfigureAwait(true);

                foreach (var line in logLines)
                {
                    AppendFlowLog(line);
                }

                MessageShower = $"GetSectionReferencePoints 批量完成：成功 {okCount}，失败 {failCount}，共 {images.Count}";
                AppendFlowLog(
                    $"汇总：成功 {okCount}，失败 {failCount}，共 {images.Count}");
                AppendFlowLog("——— CTAutoCutImg 离线批量测：GetSectionReferencePoints 结束 ———");
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"GetSectionReferencePoints 离线批量测失败：{ex.Message}");
                _logger.Error("GetSectionReferencePoints 离线批量测失败", ex);
            }
            finally
            {
                EndStageTest();
            }
        }

        /// <summary>
        /// 离线批量测 GetObjectTopBottomPoints：不跑 VG，对 2D侧视图文件夹内全部图片 → DLL → 标注起点/终点落盘。
        /// [0]=起点，[1]=终点。单张失败不中断。
        /// </summary>
        private async Task ExecuteTestCutImgGetObjectTopBottomPointsAsync()
        {
            var folder = CutImgDllTestVectorFolder?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageShower = "请先选择 2D侧视图测试文件夹";
                return;
            }

            var images = EnumerateCutImgDllTestImages(folder);
            if (images.Count == 0)
            {
                MessageShower = $"2D侧视图文件夹内未找到图片：{folder}";
                return;
            }

            if (!TryBeginStageTest())
            {
                return;
            }

            try
            {
                _lastFlowLogMessage = null;
                AppendFlowLog("——— CTAutoCutImg 离线批量测：GetObjectTopBottomPoints（2D侧视图）开始 ———");
                AppendFlowLog($"输入目录：{folder}（共 {images.Count} 张）");

                var (logLines, okCount, failCount) = await Task.Run(() =>
                {
                    EnsureCutImgDllReadyForOfflineTest();
                    var outDir = ResolveCutImgDllTestOutputDir();
                    var lines = new List<string>(images.Count * 3);
                    var ok = 0;
                    var fail = 0;

                    for (var i = 0; i < images.Count; i++)
                    {
                        var imagePath = images[i];
                        var indexTag = $"{i + 1}/{images.Count}";
                        try
                        {
                            var points = _cutImgAlgorithmService.GetObjectTopBottomPoints(imagePath);
                            if (points.Length < 2)
                            {
                                throw new InvalidOperationException(
                                    $"返回点数不足：期望 2，实际 {points.Length}");
                            }

                            // [0]=起点，[1]=终点
                            var start = points[0];
                            var end = points[1];
                            var annotate = new (int X, int Y, string Label)[]
                            {
                                (start.X, start.Y, "起点"),
                                (end.X, end.Y, "终点"),
                            };

                            var stamp = DateTime.Now.ToString("HHmmss_fff");
                            var dest = Path.Combine(
                                outDir,
                                Path.GetFileNameWithoutExtension(imagePath) + $"_topbottom_{stamp}.png");
                            ScreenCaptureHelper.SaveAnnotatedPointsImage(imagePath, dest, annotate);

                            ok++;
                            lines.Add($"OK [{indexTag}] {Path.GetFileName(imagePath)}");
                            lines.Add($"  起点[0]：({start.X},{start.Y})");
                            lines.Add($"  终点[1]：({end.X},{end.Y})");
                            lines.Add($"  标注图：{dest}");
                        }
                        catch (Exception ex)
                        {
                            fail++;
                            lines.Add($"NG [{indexTag}] {Path.GetFileName(imagePath)}：{ex.Message}");
                        }
                    }

                    return (lines, ok, fail);
                }).ConfigureAwait(true);

                foreach (var line in logLines)
                {
                    AppendFlowLog(line);
                }

                MessageShower = $"GetObjectTopBottomPoints 批量完成：成功 {okCount}，失败 {failCount}，共 {images.Count}";
                AppendFlowLog(
                    $"汇总：成功 {okCount}，失败 {failCount}，共 {images.Count}");
                AppendFlowLog("——— CTAutoCutImg 离线批量测：GetObjectTopBottomPoints 结束 ———");
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"GetObjectTopBottomPoints 离线批量测失败：{ex.Message}");
                _logger.Error("GetObjectTopBottomPoints 离线批量测失败", ex);
            }
            finally
            {
                EndStageTest();
            }
        }
    }
}
