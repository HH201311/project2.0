using System.Globalization;
using System.IO;

namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>
    /// CT 自动化切片运行参数
    /// </summary>
    public class CTAutoSlicerSettings
    {
        public const string AngleRecognitionAlgorithmVersion = "2024.09.24";
        public const string AppKey = "NJ_CT";
        public const string PythonCode = "NJ_CT_L246_DLL_V";
        public const string DefaultDllFileName = "V.NJ.L246.01.01.03.240924.dll";
        public const string DllsFolderName = "Dlls";
        public const string DefaultDllDownloadBaseUrl = "http://10.201.88.151:10100/";
        /// <summary>截图目录默认保留天数（一周）</summary>
        public const int DefaultCaptureKeepDays = 7;
        /// <summary>视觉对齐截图默认目录</summary>
        public const string DefaultVisionCaptureDir = @"C:\AutoSlice\cutpicture\jietu";
        /// <summary>简单 3-2-1「2」向量标点（「上」视图）截图默认目录</summary>
        public const string DefaultVision2dVectorCaptureDir = @"C:\AutoSlice\cutpicture\2Dxianglaing";

        /// <summary>
        /// 角度算法 DLL 本地路径：{程序目录}\Dlls\{DefaultDllFileName}
        /// </summary>
        public static string GetAngleAlgorithmDllPath() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllsFolderName, DefaultDllFileName);

        public const string CutImgDllFileName = "CTAutoCutImg.dll";

        /// <summary>切图算法 DLL：{程序目录}\Dlls\CTAutoCutImg.dll（与 halcon 原生库同目录）</summary>
        public static string GetCutImgAlgorithmDllPath() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllsFolderName, CutImgDllFileName);

        /// <summary>原生依赖目录：{程序目录}\Dlls（halcon.dll / libiomp5md.dll 等）</summary>
        public static string GetNativeDllsDirectory() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllsFolderName);

        public const string VglFileExtension = ".vgl";
        public const string VgiFileExtension = ".vgi";

        /// <summary>是否为可导入的体素文件扩展名（.vgl / .vgi，不区分大小写）。</summary>
        public static bool IsImportVolumeExtension(string? extension) =>
            string.Equals(extension, VglFileExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, VgiFileExtension, StringComparison.OrdinalIgnoreCase);

        /// <summary>VG 冷启动就绪检测默认最大等待（毫秒）</summary>
        public const int DefaultVGOpenWaitTime = 180000;
        /// <summary>旧版数据库中常见的过短等待（如 7000ms），加载时自动升级</summary>
        public const int LegacyVGOpenWaitTimeThreshold = 10000;

        public string VGAppPath { get; set; } = @"D:\App\VG\vgstudiomax20241.exe";
        /// <summary>VG 就绪检测最大等待（毫秒），非固定睡眠</summary>
        public int VGOpenWaitTime { get; set; } = DefaultVGOpenWaitTime;
        /// <summary>VG 置前后等待主界面可交互的最大超时（毫秒）</summary>
        public int VGShowFrontWaitTime { get; set; } = 2000;
        public string CTFileFolder { get; set; } = string.Empty;
        /// <summary>Ctrl+I 后等待快速导入对话框出现的最大超时（毫秒）</summary>
        public int QuickImportDialogWaitTime { get; set; } = 10000;
        /// <summary>Ctrl+N 后等待保存提示框出现的最大超时（毫秒）</summary>
        public int NewProjectDialogWaitTime { get; set; } = 5000;
        /// <summary>等待文件选择向导/导入进度框出现的最大超时（毫秒）</summary>
        public int FileSelectionDialogWaitTime { get; set; } = 15000;
        public int ImportTimeoutMinutes { get; set; } = 30;
        /// <summary>导入完成后等待工作区就绪的最大超时（毫秒）</summary>
        public int PostImportWaitTime { get; set; } = 3000;
        public int StepRetryCount { get; set; } = 3;
        public int StepRetryIntervalMs { get; set; } = 2000;
        /// <summary>渲染面板 LUT 预设名称（组合框列表项 Name）</summary>
        public string RenderingPresetName { get; set; } = "testlut";
        /// <summary>应用预设前等待渲染工作区就绪的最大超时（毫秒）</summary>
        public int PresetApplyWaitTime { get; set; } = 2000;
        /// <summary>下拉列表项出现的最大超时（毫秒）</summary>
        public int PresetDropdownWaitTime { get; set; } = 5000;
        public string DllDownloadBaseUrl { get; set; } = DefaultDllDownloadBaseUrl;

        /// <summary>右键菜单项 Name（厚板）</summary>
        public string ThickPlateMenuItemName { get; set; } = "厚板";
        /// <summary>厚板设置对话框窗口 Name</summary>
        public string ThickPlateDialogWindowName { get; set; } = "厚板设置";
        /// <summary>启用厚板模式复选框 Name</summary>
        public string ThickPlateEnableCheckboxName { get; set; } = "启用厚板模式";
        /// <summary>组合模式目标项 Name</summary>
        public string ThickPlateCombinationModeName { get; set; } = "平均值";
        /// <summary>右键菜单「厚板」相对右键点的 X 偏移</summary>
        public int ThickPlateMenuOffsetX { get; set; } = 90;
        /// <summary>右键菜单「厚板」相对右键点的 Y 偏移</summary>
        public int ThickPlateMenuOffsetY { get; set; } = 127;
        /// <summary>右键后等待「厚板设置」对话框出现的最大超时（毫秒）</summary>
        public int ThickPlateMenuAfterRightClickDelayMs { get; set; } = 200;
        /// <summary>厚板单步失败时的最大尝试次数（默认 1，不重试）</summary>
        public int ThickPlateStepRetryCount { get; set; } = 1;
        /// <summary>厚板流程等待下一步 UI 就绪的最大超时（毫秒）</summary>
        public int ThickPlateStepDelayMs { get; set; } = 500;
        /// <summary>视口右键点相对中心的 Y 偏移（避开顶栏工具条）</summary>
        public int ViewportClickOffsetY { get; set; } = 0;
        /// <summary>第三象限（左下视口）右键点额外 Y 偏移（负值=上移，避免靠近屏幕底缘菜单反向弹出）</summary>
        public int ViewportQuadrant3ClickOffsetY { get; set; } = -100;
        /// <summary>等待厚板设置对话框出现的超时（毫秒）</summary>
        public int ThickPlateDialogWaitTime { get; set; } = 3000;
        /// <summary>厚板组合模式下拉列表项出现超时（毫秒）</summary>
        public int ThickPlateDropdownWaitTime { get; set; } = 3000;
        /// <summary>视口候选最小宽度（像素）</summary>
        public int ViewportMinWidth { get; set; } = 400;
        /// <summary>视口候选最小高度（像素）</summary>
        public int ViewportMinHeight { get; set; } = 300;
        /// <summary>复选框 UIA 失败时相对对话框左上角的 X 偏移（0=不用）</summary>
        public int ThickPlateCheckboxFallbackOffsetX { get; set; } = 0;
        /// <summary>复选框 UIA 失败时相对对话框左上角的 Y 偏移（0=不用）</summary>
        public int ThickPlateCheckboxFallbackOffsetY { get; set; } = 0;
        /// <summary>组合模式下拉 UIA 失败时相对对话框左上角的 X 偏移（0=不用）</summary>
        public int ThickPlateComboFallbackOffsetX { get; set; } = 0;
        /// <summary>组合模式下拉 UIA 失败时相对对话框左上角的 Y 偏移（0=不用）</summary>
        public int ThickPlateComboFallbackOffsetY { get; set; } = 0;

        /// <summary>切片导出目标目录</summary>
        public string SliceExportFolder { get; set; } = string.Empty;
        /// <summary>切片导出图像宽度（像素）</summary>
        public int SliceExportImageWidth { get; set; } = 3000;
        /// <summary>局部图像（非 QJ）导出数量</summary>
        public int SliceExportQuantity { get; set; } = 100;
        /// <summary>全局图像（QJ）导出数量</summary>
        public int SliceExportQuantityQj { get; set; } = 150;
        /// <summary>「前」视图切片范围开始 [mm] — UI 配置</summary>
        public double SliceRangeFrontStartMm { get; set; } = 20;
        /// <summary>「前」视图切片范围结束 [mm] — UI 配置</summary>
        public double SliceRangeFrontEndMm { get; set; } = 70;
        /// <summary>「右」视图切片范围开始 [mm] — UI 配置</summary>
        public double SliceRangeRightStartMm { get; set; } = 20;
        /// <summary>「右」视图切片范围结束 [mm] — UI 配置</summary>
        public double SliceRangeRightEndMm { get; set; } = 70;
        /// <summary>「上」视图切片范围开始 [mm] — UI 配置</summary>
        public double SliceRangeTopStartMm { get; set; } = 20;
        /// <summary>「上」视图切片范围结束 [mm] — UI 配置</summary>
        public double SliceRangeTopEndMm { get; set; } = 70;

        /// <summary>
        /// 兼容旧配置：单组范围；若三视角未单独存过，加载时可回填到 Front/Right/Top。
        /// </summary>
        public double SliceRangeStartMm
        {
            get => SliceRangeFrontStartMm;
            set
            {
                SliceRangeFrontStartMm = value;
                SliceRangeRightStartMm = value;
                SliceRangeTopStartMm = value;
            }
        }

        /// <summary>兼容旧配置：单组范围结束。</summary>
        public double SliceRangeEndMm
        {
            get => SliceRangeFrontEndMm;
            set
            {
                SliceRangeFrontEndMm = value;
                SliceRangeRightEndMm = value;
                SliceRangeTopEndMm = value;
            }
        }

        /// <summary>按导出视图后缀（前/右/上）取切片起止 mm。</summary>
        public (double StartMm, double EndMm) GetSliceRangeForView(string viewSuffix) =>
            viewSuffix switch
            {
                "右" => (SliceRangeRightStartMm, SliceRangeRightEndMm),
                "上" => (SliceRangeTopStartMm, SliceRangeTopEndMm),
                _ => (SliceRangeFrontStartMm, SliceRangeFrontEndMm),
            };
        /// <summary>等待导出对话框出现的超时（毫秒）</summary>
        public int SliceExportDialogWaitTime { get; set; } = 8000;
        /// <summary>等待「正在写入图像」进度框消失的超时（毫秒），默认 1 分钟</summary>
        public int SliceExportWriteTimeoutMs { get; set; } = 60000;
        /// <summary>2D 视图下拉项 Name（前视图）</summary>
        public string SliceExportViewFrontName { get; set; } = "前 1";
        /// <summary>2D 视图下拉项 Name（右视图）</summary>
        public string SliceExportViewRightName { get; set; } = "右 1";
        /// <summary>2D 视图下拉项 Name（上视图）</summary>
        public string SliceExportViewTopName { get; set; } = "上 1";

        /// <summary>直方图 ROI 屏幕 X（物理像素，框选标定）</summary>
        public int HistogramRoiX { get; set; }
        /// <summary>直方图 ROI 屏幕 Y（物理像素，框选标定）</summary>
        public int HistogramRoiY { get; set; }
        /// <summary>直方图 ROI 宽度（物理像素）</summary>
        public int HistogramRoiWidth { get; set; }
        /// <summary>直方图 ROI 高度（物理像素）</summary>
        public int HistogramRoiHeight { get; set; }
        /// <summary>直方图截图落盘目录（空则使用程序目录 logs/histogram）</summary>
        public string HistogramCaptureDir { get; set; } = string.Empty;
        /// <summary>直方图截图保留天数（按日文件夹，超过则整夹删除）</summary>
        public int HistogramCaptureKeepDays { get; set; } = DefaultCaptureKeepDays;
        /// <summary>直方图填充绝对色（#RRGGBB），取色或手填</summary>
        public string HistogramMaskColor { get; set; } = "#6E6E6E";
        /// <summary>掩膜颜色容差（每通道 0～64）</summary>
        public int HistogramMaskTolerance { get; set; } = 12;

        /// <summary>直方图 ROI 是否已完成框选标定</summary>
        public bool HasHistogramRoi => HistogramRoiWidth > 0 && HistogramRoiHeight > 0;

        /// <summary>阶段四：第四象限 3D 视图截图 ROI — 屏幕 X（物理像素，框选标定起始坐标）</summary>
        public int Vision3dRoiX { get; set; }
        /// <summary>阶段四：3D 视图 ROI — 屏幕 Y</summary>
        public int Vision3dRoiY { get; set; }
        /// <summary>阶段四：3D 视图 ROI — 宽度</summary>
        public int Vision3dRoiWidth { get; set; }
        /// <summary>阶段四：3D 视图 ROI — 高度</summary>
        public int Vision3dRoiHeight { get; set; }

        /// <summary>阶段四：第二象限「上」视图截图 ROI — 屏幕 X（物理像素，框选标定起始坐标）</summary>
        public int VisionTopViewRoiX { get; set; }
        /// <summary>阶段四：「上」视图 ROI — 屏幕 Y</summary>
        public int VisionTopViewRoiY { get; set; }
        /// <summary>阶段四：「上」视图 ROI — 宽度</summary>
        public int VisionTopViewRoiWidth { get; set; }
        /// <summary>阶段四：「上」视图 ROI — 高度</summary>
        public int VisionTopViewRoiHeight { get; set; }

        /// <summary>3D 视图 ROI 是否已标定</summary>
        public bool HasVision3dRoi => Vision3dRoiWidth > 0 && Vision3dRoiHeight > 0;

        /// <summary>「上」视图 ROI 是否已标定</summary>
        public bool HasVisionTopViewRoi => VisionTopViewRoiWidth > 0 && VisionTopViewRoiHeight > 0;

        /// <summary>阶段四多点点击间隔（毫秒）</summary>
        public int VisionClickIntervalMs { get; set; } = 300;

        /// <summary>等待「简单 3-2-1 对齐」菜单/对话框出现的超时（毫秒）</summary>
        public int VisionAlignmentDialogWaitTime { get; set; } = 8000;

        /// <summary>
        /// 测量工具栏「简单对齐」下拉的屏幕绝对点击 X（物理像素）。
        /// Qt 下拉 UIA 常无法识别，须手动标定后用 Win32 点击。
        /// </summary>
        public int Simple321ToolbarClickX { get; set; }

        /// <summary>测量工具栏「简单对齐」下拉的屏幕绝对点击 Y（物理像素）</summary>
        public int Simple321ToolbarClickY { get; set; }

        /// <summary>是否已完成「简单对齐」下拉点击位置标定</summary>
        public bool Simple321ToolbarClickCalibrated { get; set; }

        /// <summary>「简单对齐」下拉点击位置是否可用</summary>
        public bool HasSimple321ToolbarClick => Simple321ToolbarClickCalibrated;

        /// <summary>
        /// 主流程是否执行阶段四简单 3-2-1。默认始终开启；未标定齐全时跳过并对该文件打日志。
        /// </summary>
        public bool EnableVisionAlignmentSteps { get; set; } = true;

        /// <summary>简单 3-2-1 所需标定是否齐全（3D ROI +「上」ROI + 下拉点击）</summary>
        public bool HasSimple321Prerequisites =>
            HasVision3dRoi && HasVisionTopViewRoi && HasSimple321ToolbarClick;

        /// <summary>视觉对齐截图落盘目录（空则使用 DefaultVisionCaptureDir）</summary>
        public string VisionCaptureDir { get; set; } = DefaultVisionCaptureDir;

        /// <summary>视觉对齐截图按日目录保留天数（超过则删除）</summary>
        public int VisionCaptureKeepDays { get; set; } = DefaultCaptureKeepDays;

        /// <summary>「上」视图向量标点截图落盘目录（空则使用 DefaultVision2dVectorCaptureDir）</summary>
        public string Vision2dVectorCaptureDir { get; set; } = DefaultVision2dVectorCaptureDir;

        /// <summary>向量标点截图按日目录保留天数（超过则删除）</summary>
        public int Vision2dVectorCaptureKeepDays { get; set; } = DefaultCaptureKeepDays;

        /// <summary>
        /// 将算法返回的相对像素坐标换算为屏幕物理像素点击位置。
        /// screen = (roiX + relativeX, roiY + relativeY)
        /// </summary>
        public static (int ScreenX, int ScreenY) ToScreenPoint(
            int roiX,
            int roiY,
            int relativeX,
            int relativeY) =>
            (roiX + relativeX, roiY + relativeY);

        public string ResolveVisionCaptureDir()
        {
            if (!string.IsNullOrWhiteSpace(VisionCaptureDir))
            {
                return VisionCaptureDir.Trim();
            }

            return DefaultVisionCaptureDir;
        }

        public string ResolveVision2dVectorCaptureDir()
        {
            if (!string.IsNullOrWhiteSpace(Vision2dVectorCaptureDir))
            {
                return Vision2dVectorCaptureDir.Trim();
            }

            return DefaultVision2dVectorCaptureDir;
        }

        public string ResolveHistogramCaptureDir()
        {
            if (!string.IsNullOrWhiteSpace(HistogramCaptureDir))
            {
                return HistogramCaptureDir.Trim();
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "histogram");
        }

        /// <summary>
        /// 确保视觉截图根目录与当日子目录存在，返回当日保存目录（yyyy-MM-dd）。
        /// </summary>
        public string EnsureVisionCaptureDayDir(DateTime? now = null)
        {
            return EnsureCaptureDayDir(ResolveVisionCaptureDir(), now);
        }

        /// <summary>
        /// 确保「上」视图向量标点截图根目录与当日子目录存在，返回当日保存目录（yyyy-MM-dd）。
        /// </summary>
        public string EnsureVision2dVectorCaptureDayDir(DateTime? now = null)
        {
            return EnsureCaptureDayDir(ResolveVision2dVectorCaptureDir(), now);
        }

        /// <summary>
        /// 确保直方图截图根目录与当日子目录存在，返回当日保存目录（yyyy-MM-dd）。
        /// </summary>
        public string EnsureHistogramCaptureDayDir(DateTime? now = null)
        {
            return EnsureCaptureDayDir(ResolveHistogramCaptureDir(), now);
        }

        private static string EnsureCaptureDayDir(string root, DateTime? now)
        {
            Directory.CreateDirectory(root);
            var dayName = (now ?? DateTime.Now).ToString("yyyy-MM-dd");
            var dayDir = Path.Combine(root, dayName);
            Directory.CreateDirectory(dayDir);
            return dayDir;
        }

        /// <summary>删除超过保留天数的视觉截图按日文件夹。</summary>
        public (int DeletedDirs, int DeletedFiles) CleanupExpiredVisionCaptures(int? keepDays = null) =>
            CleanupExpiredCaptureDayFolders(
                ResolveVisionCaptureDir(),
                keepDays ?? VisionCaptureKeepDays,
                rootFilePatterns: ["vision*.png", "*_points.png"]);

        /// <summary>删除超过保留天数的「上」视图向量标点截图按日文件夹。</summary>
        public (int DeletedDirs, int DeletedFiles) CleanupExpiredVision2dVectorCaptures(int? keepDays = null) =>
            CleanupExpiredCaptureDayFolders(
                ResolveVision2dVectorCaptureDir(),
                keepDays ?? Vision2dVectorCaptureKeepDays,
                rootFilePatterns: ["vision_top*.png", "*_vector_*.png", "*_click_points.png"]);

        /// <summary>
        /// 删除超过保留天数的直方图按日文件夹，以及根目录下过期的散落截图文件。
        /// </summary>
        public (int DeletedDirs, int DeletedFiles) CleanupExpiredHistogramCaptures(int? keepDays = null) =>
            CleanupExpiredCaptureDayFolders(
                ResolveHistogramCaptureDir(),
                keepDays ?? HistogramCaptureKeepDays,
                rootFilePatterns: ["histogram*.png"]);

        /// <summary>
        /// 删除根目录下超过保留天数的 yyyy-MM-dd 子目录，以及匹配模式的散落文件。
        /// </summary>
        public static (int DeletedDirs, int DeletedFiles) CleanupExpiredCaptureDayFolders(
            string root,
            int keepDays,
            IEnumerable<string>? rootFilePatterns = null)
        {
            var days = keepDays < 1 ? 1 : keepDays;
            if (!Directory.Exists(root))
            {
                return (0, 0);
            }

            var today = DateTime.Today;
            var deletedDirs = 0;
            var deletedFiles = 0;

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (!DateTime.TryParseExact(
                        name,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var folderDate))
                {
                    continue;
                }

                var ageDays = (today - folderDate.Date).Days;
                if (ageDays < days)
                {
                    continue;
                }

                try
                {
                    Directory.Delete(dir, recursive: true);
                    deletedDirs++;
                }
                catch
                {
                    // 占用中则跳过，下次再清
                }
            }

            if (rootFilePatterns != null)
            {
                foreach (var pattern in rootFilePatterns)
                {
                    foreach (var file in Directory.EnumerateFiles(root, pattern))
                    {
                        try
                        {
                            var ageDays = (today - File.GetLastWriteTime(file).Date).Days;
                            if (ageDays < days)
                            {
                                continue;
                            }

                            File.Delete(file);
                            deletedFiles++;
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }

            return (deletedDirs, deletedFiles);
        }

        public bool IsInputComplete()
        {
            return !string.IsNullOrWhiteSpace(VGAppPath)
                && VGOpenWaitTime != 0
                && VGShowFrontWaitTime != 0
                && !string.IsNullOrWhiteSpace(CTFileFolder)
                && !string.IsNullOrWhiteSpace(SliceExportFolder)
                && QuickImportDialogWaitTime != 0
                && NewProjectDialogWaitTime != 0
                && FileSelectionDialogWaitTime != 0
                && ImportTimeoutMinutes != 0
                && PostImportWaitTime != 0
                && StepRetryCount != 0
                && StepRetryIntervalMs != 0
                && SliceExportImageWidth > 0
                && SliceExportQuantity > 0;
        }
    }
}
