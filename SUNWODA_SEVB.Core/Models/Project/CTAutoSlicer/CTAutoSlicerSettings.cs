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
        /// 本地算法：直方图后、Pre321 切视角前，保存的默认视角 3D 原图目录（供视觉强化学习）。
        /// </summary>
        public const string DefaultVision3dInitialCaptureDir = @"C:\AutoSlice\cutpicture\3D初始";
        /// <summary>单步测试：截取 3D 标定图默认保存目录</summary>
        public const string DefaultVisionLearn3dSampleDir = @"C:\AutoSlice\cutpicture\3D";
        /// <summary>单步测试：截取上视图标定图默认保存目录</summary>
        public const string DefaultVisionLearnTopViewSampleDir = @"C:\AutoSlice\cutpicture\上视图";
        /// <summary>本地算法保存 3D 初始原图前的固定等待（毫秒）。</summary>
        public const int DefaultVision3dInitialCaptureWaitMs = 2000;

        /// <summary>体素文件夹内：运行失败文件归档子目录名（避免下次重复导入）。</summary>
        public const string FailedFilesSubFolderName = "运行失败的文件";

        /// <summary>流程异常/超时后等待人工处理的默认时长（超时后归档失败文件并继续下一文件）。</summary>
        public static readonly TimeSpan DefaultManualInterventionTimeout = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 角度算法 DLL 本地路径：{程序目录}\Dlls\{DefaultDllFileName}
        /// </summary>
        public static string GetAngleAlgorithmDllPath() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllsFolderName, DefaultDllFileName);

        public const string CutImgDllFileName = "CTAutoCutImg.dll";
        /// <summary>CTAutoCutImg DLL 离线单测标注图默认输出目录</summary>
        public const string DefaultCutImgDllTestOutputDir = @"C:\AutoSlice\cutpicture\CutImgDllTest";
        public const string OfflineCtDllFileName = "NJCT.dll";
        public const string OfflineCtModelRelativePath = @"Models\CTOffline\model_500000_900.onnx";
        /// <summary>
        /// 界面「打黑高度」未配置时的初始值（图像像素，仅上位机遮罩用）。
        /// 传给 NJCT 的 BlackHeight 始终为 0，与本常量无关。
        /// </summary>
        public const int DefaultOfflineCtBlackHeight = 1500;
        /// <summary>
        /// 送检图内容上移像素默认值：0=不上移。
        /// 用于适配 NJCT 偏上裁切：内容上移、画布不变、底部黑边填充。
        /// </summary>
        public const int DefaultOfflineCtContentShiftUpPx = 0;
        /// <summary>L1~L5 范围判断默认下限（mm）。</summary>
        public const double DefaultOfflineCtLMinMm = 0.0;
        /// <summary>L1~L5 范围判断默认上限（mm）。</summary>
        public const double DefaultOfflineCtLMaxMm = 10.0;
        /// <summary>AI 标注取「上」视图切片序号默认值（1-based；对应导出数量 100 时的中间张）。</summary>
        public const int DefaultOfflineCtTopViewSliceIndex = 50;
        /// <summary>「上」视图切片序号下限（含）。</summary>
        public const int MinOfflineCtTopViewSliceIndex = 1;
        /// <summary>「上」视图切片序号上限（含）；与 UI 1～100 一致。</summary>
        public const int MaxOfflineCtTopViewSliceIndex = 100;
        /// <summary>像素精度默认值（μm/px），供后续尺寸测量换算。</summary>
        public const double DefaultOfflineCtPixelSizeUm = 1.0;

        /// <summary>页面底色默认 #F7E4E5（RGB 247,228,229）</summary>
        public const string DefaultThemePageBgColor = "#F7E4E5";
        /// <summary>最外层卡片默认 #F7EDEE（RGB 247,237,238）</summary>
        public const string DefaultThemeOuterCardColor = "#F7EDEE";
        /// <summary>内部分组卡片默认 #EFEBF7</summary>
        public const string DefaultThemeInnerCardColor = "#EFEBF7";
        /// <summary>左侧菜单栏默认 #8E82C4</summary>
        public const string DefaultThemeSidebarColor = "#8E82C4";

        /// <summary>一键主题预设（页面底色 / 外层卡片 / 内部分组 / 侧栏）。</summary>
        public sealed class ThemeColorPreset
        {
            public ThemeColorPreset(
                string id,
                string name,
                string description,
                string pageBg,
                string outerCard,
                string innerCard,
                string sidebar,
                bool isBuiltIn = true)
            {
                Id = id;
                Name = name;
                Description = description;
                PageBg = pageBg;
                OuterCard = outerCard;
                InnerCard = innerCard;
                Sidebar = sidebar;
                IsBuiltIn = isBuiltIn;
            }

            public string Id { get; }
            public string Name { get; }
            public string Description { get; }
            public string PageBg { get; }
            public string OuterCard { get; }
            public string InnerCard { get; }
            public string Sidebar { get; }
            public bool IsBuiltIn { get; }
        }

        /// <summary>自定义主题预设持久化 DTO。</summary>
        public sealed class ThemeCustomPresetDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string PageBg { get; set; } = DefaultThemePageBgColor;
            public string OuterCard { get; set; } = DefaultThemeOuterCardColor;
            public string InnerCard { get; set; } = DefaultThemeInnerCardColor;
            public string Sidebar { get; set; } = DefaultThemeSidebarColor;
        }

        /// <summary>
        /// 内置 4 套配色（不可删除）。工业界面宜「侧栏有色 + 内容区中性浅底」。
        /// </summary>
        public static IReadOnlyList<ThemeColorPreset> ThemeColorPresets { get; } =
        [
            new(
                "industrial-blue",
                "工控蓝灰",
                "推荐：侧栏沉稳蓝，内容区中性灰白，层次清晰。",
                "#F3F5F8",
                "#FFFFFF",
                "#E8EDF3",
                "#3D5A80"),
            new(
                "mist-teal",
                "雾青清爽",
                "侧栏青绿点缀，内容区近白，偏现代。",
                "#F5FAF8",
                "#FFFFFF",
                "#E6F3EF",
                "#2F7D73"),
            new(
                "warm-paper",
                "暖纸炭灰",
                "暖纸底 + 炭灰侧栏，减少紫色疲劳。",
                "#F7F4EF",
                "#FFFEFB",
                "#EFEAE3",
                "#4A5560"),
            new(
                "contrast-slate",
                "高对比石板",
                "深侧栏 + 近白卡片，导航与表单分离最明显。",
                "#EEF1F5",
                "#FFFFFF",
                "#E2E8F0",
                "#334155"),
        ];

        /// <summary>切图算法 DLL：{程序目录}\Dlls\CTAutoCutImg.dll（与 halcon 原生库同目录）</summary>
        public static string GetCutImgAlgorithmDllPath() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllsFolderName, CutImgDllFileName);

        /// <summary>惠州离线 CT AI 业务 DLL：{程序目录}\Dlls\NJCT.dll</summary>
        public static string GetOfflineCtDllPath() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllsFolderName, OfflineCtDllFileName);

        /// <summary>默认 ONNX 模型：{程序目录}\Dlls\Models\CTOffline\model_500000_900.onnx</summary>
        public static string GetDefaultOfflineCtModelPath() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllsFolderName, "Models", "CTOffline", "model_500000_900.onnx");

        /// <summary>原生依赖目录：{程序目录}\Dlls（halcon.dll / libiomp5md.dll 等）</summary>
        public static string GetNativeDllsDirectory() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllsFolderName);

        public const string VglFileExtension = ".vgl";
        public const string VgiFileExtension = ".vgi";
        /// <summary>Windows 快捷方式扩展名（资源管理器中常显示为「xxx.vgi - 快捷方式」）。</summary>
        public const string ShortcutFileExtension = ".lnk";

        /// <summary>是否为可导入的体素文件扩展名（.vgl / .vgi，不区分大小写）。</summary>
        public static bool IsImportVolumeExtension(string? extension) =>
            string.Equals(extension, VglFileExtension, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, VgiFileExtension, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 文件名是否像体素快捷方式（磁盘上必须是 .lnk）。
        /// 资源管理器常显示为「0351-F ….vgi - 快捷方式」（隐藏了 .lnk 后缀）。
        /// </summary>
        public static bool LooksLikeImportVolumeShortcut(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName)
                || !fileName.EndsWith(ShortcutFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var nameWithoutLnk = fileName[..^ShortcutFileExtension.Length];
            return ContainsVolumeExtensionToken(nameWithoutLnk)
                || nameWithoutLnk.Contains("快捷方式", StringComparison.Ordinal)
                || nameWithoutLnk.Contains("Shortcut", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 名称像「xxx.vgi - 快捷方式」但不是 .lnk（无法作为 Windows 快捷方式解析）。
        /// </summary>
        public static bool LooksLikeBrokenVolumeShortcutName(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName)
                || fileName.EndsWith(ShortcutFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var extension = Path.GetExtension(fileName);
            var looksShortcutCaption = extension.Contains("快捷方式", StringComparison.Ordinal)
                || extension.Contains("Shortcut", StringComparison.OrdinalIgnoreCase)
                || fileName.Contains("快捷方式", StringComparison.Ordinal)
                || fileName.Contains("Shortcut", StringComparison.OrdinalIgnoreCase);

            return looksShortcutCaption && ContainsVolumeExtensionToken(fileName);
        }

        /// <summary>
        /// 是否可作为体素文件夹排队条目：真实 .vgl/.vgi，或体素快捷方式（.lnk）。
        /// </summary>
        public static bool IsImportVolumeQueueEntry(string? path) =>
            IsImportVolumeExtension(Path.GetExtension(path ?? string.Empty))
            || LooksLikeImportVolumeShortcut(path);

        private static bool ContainsVolumeExtensionToken(string text) =>
            text.Contains(VgiFileExtension, StringComparison.OrdinalIgnoreCase)
            || text.Contains(VglFileExtension, StringComparison.OrdinalIgnoreCase);

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
        /// <summary>VG 进程池最大并发数（导入可重叠等待；后处理串行）。默认 4，建议 1～8。</summary>
        public int MaxVgProcesses { get; set; } = DefaultMaxVgProcesses;
        /// <summary>进程池默认最大并发数</summary>
        public const int DefaultMaxVgProcesses = 4;
        /// <summary>进程池允许的最大上限（防止误配打爆内存）</summary>
        public const int AbsoluteMaxVgProcesses = 8;
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
        /// <summary>正极 LUT 预设名称（文件名含 -Z 时使用；须与 VG 渲染面板列表项 Name 一致）</summary>
        public string RenderingPresetNamePositive { get; set; } = "testlut_Z";
        /// <summary>负极 LUT 预设名称（文件名含 -F 或无法识别时默认使用；须与 VG 渲染面板列表项 Name 一致）</summary>
        public string RenderingPresetNameNegative { get; set; } = "testlut_F";
        /// <summary>应用预设前等待渲染工作区就绪的最大超时（毫秒）</summary>
        public int PresetApplyWaitTime { get; set; } = 2000;
        /// <summary>下拉列表项出现的最大超时（毫秒）</summary>
        public int PresetDropdownWaitTime { get; set; } = 5000;

        /// <summary>
        /// 按导入文件名判断是否正极：含 <c>-Z</c> 且不含 <c>-F</c> 为正极；其余（含默认）为负极。
        /// </summary>
        public bool IsPositiveElectrodeFile(string? pathOrFileName)
        {
            var fileName = string.IsNullOrWhiteSpace(pathOrFileName)
                ? string.Empty
                : Path.GetFileName(pathOrFileName.Trim());

            var hasZ = fileName.IndexOf("-Z", StringComparison.OrdinalIgnoreCase) >= 0;
            var hasF = fileName.IndexOf("-F", StringComparison.OrdinalIgnoreCase) >= 0;
            return hasZ && !hasF;
        }

        /// <summary>
        /// 按导入文件名选择 LUT 预设：含 <c>-Z</c>→正极，含 <c>-F</c>→负极；
        /// 均无、均有或路径为空时默认负极（F）。
        /// </summary>
        /// <returns>选定的预设名；对应配置为空则返回空串。</returns>
        public string ResolveRenderingPresetName(string? pathOrFileName)
        {
            var usePositive = IsPositiveElectrodeFile(pathOrFileName);
            var raw = usePositive ? RenderingPresetNamePositive : RenderingPresetNameNegative;
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
        }

        /// <summary>与 <see cref="ResolveRenderingPresetName"/> 配套的电极标签（日志/UI）。</summary>
        public string ResolveRenderingPresetPolarityLabel(string? pathOrFileName) =>
            IsPositiveElectrodeFile(pathOrFileName) ? "正极" : "负极";

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

        /// <summary>切片导出目标目录（AI 模式下复用为标注结果根目录）</summary>
        public string SliceExportFolder { get; set; } = string.Empty;

        /// <summary>
        /// 流水线模式：切图 / AI 标注（两机拆分；主流程不再串行阶段七）。
        /// </summary>
        public PipelineRunMode PipelineRunMode { get; set; } = PipelineRunMode.SliceOnly;

        /// <summary>
        /// 上视图送检图交接根目录：切图结束后复制 OfflineCtTopViewSliceIndex 指定图到此目录；
        /// AI 模式从此目录批量读取输入图。
        /// </summary>
        public string TopViewHandoffFolder { get; set; } = string.Empty;

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

        /// <summary>
        /// 兼容旧配置：历史主流程「导出后跑 AI」开关。
        /// 现行主链以 <see cref="PipelineRunMode"/> 为准，不再串行阶段七。
        /// </summary>
        public bool EnableOfflineCtVisionAnnotation { get; set; } = false;

        /// <summary>ONNX 模型绝对路径（空则使用默认 Dlls\Models\CTOffline\…）</summary>
        public string OfflineCtModelPath { get; set; } = string.Empty;

        /// <summary>是否启用 GPU CUDA（ModelInit IsRunCuda）</summary>
        public bool OfflineCtIsRunCuda { get; set; } = true;

        /// <summary>
        /// 底部信息遮罩高度（图像像素，以增强图为基准）：
        /// 完全由本程序控制——传给 NJCT 的 BlackHeight 固定 0；
        /// 上位机在 ResultImg 底部覆盖打黑（不扩展画布），
        /// 写入 AI 尺寸汇总；二次手动标注覆盖右 1/4。
        /// 原图 ResultImg 长宽约为增强图一半，故原图用
        /// <see cref="ResolveOriginBottomBlackHeight"/>（配置值÷2）保持视觉占比一致。
        /// </summary>
        public int OfflineCtBlackHeight { get; set; } = DefaultOfflineCtBlackHeight;

        /// <summary>
        /// 原图底部遮罩高度：原图长宽约为增强图一半，按比例取配置值整除减半。
        /// </summary>
        public static int ResolveOriginBottomBlackHeight(int enhanceBlackHeightPx) =>
            Math.Max(0, enhanceBlackHeightPx / 2);

        /// <summary>
        /// 按结果图类型解析上位机打黑高度。
        /// <paramref name="isOriginResult"/> 为 true 时按原图比例减半。
        /// </summary>
        public static int ResolveHostBottomBlackHeight(int configuredEnhancePx, bool isOriginResult) =>
            isOriginResult
                ? ResolveOriginBottomBlackHeight(Math.Max(0, configuredEnhancePx))
                : Math.Max(0, configuredEnhancePx);

        /// <summary>
        /// 将打黑高度钳制到结果图高度。
        /// </summary>
        public static int ClampBottomBlackHeight(int configuredPx, int imageHeightPx)
        {
            var h = Math.Max(0, configuredPx);
            if (imageHeightPx <= 0)
            {
                return h;
            }

            return Math.Clamp(h, 0, imageHeightPx);
        }

        /// <summary>是否开启 L1~L5 尺寸范围判断（mm）；开启后超差线/数值标红，合格标绿。</summary>
        public bool EnableOfflineCtLRangeCheck { get; set; }

        public double OfflineCtL1MinMm { get; set; } = DefaultOfflineCtLMinMm;
        public double OfflineCtL1MaxMm { get; set; } = DefaultOfflineCtLMaxMm;
        public double OfflineCtL2MinMm { get; set; } = DefaultOfflineCtLMinMm;
        public double OfflineCtL2MaxMm { get; set; } = DefaultOfflineCtLMaxMm;
        public double OfflineCtL3MinMm { get; set; } = DefaultOfflineCtLMinMm;
        public double OfflineCtL3MaxMm { get; set; } = DefaultOfflineCtLMaxMm;
        public double OfflineCtL4MinMm { get; set; } = DefaultOfflineCtLMinMm;
        public double OfflineCtL4MaxMm { get; set; } = DefaultOfflineCtLMaxMm;
        public double OfflineCtL5MinMm { get; set; } = DefaultOfflineCtLMinMm;
        public double OfflineCtL5MaxMm { get; set; } = DefaultOfflineCtLMaxMm;

        /// <summary>组装 L1~L5 范围判断选项，供叠加出图使用。</summary>
        public OfflineCtLRangeCheckOptions BuildOfflineCtLRangeCheckOptions() =>
            new()
            {
                Enabled = EnableOfflineCtLRangeCheck,
                MinMm =
                [
                    OfflineCtL1MinMm,
                    OfflineCtL2MinMm,
                    OfflineCtL3MinMm,
                    OfflineCtL4MinMm,
                    OfflineCtL5MinMm,
                ],
                MaxMm =
                [
                    OfflineCtL1MaxMm,
                    OfflineCtL2MaxMm,
                    OfflineCtL3MaxMm,
                    OfflineCtL4MaxMm,
                    OfflineCtL5MaxMm,
                ],
            };

        /// <summary>
        /// AI 标注选用「上」视图第几张（范围 1～100）。
        /// 输入 50 = 第 50 张；实际文件名 = 导出前缀 + 序号（前缀默认 <c>0</c> → <c>050.jpg</c>）。
        /// </summary>
        public int OfflineCtTopViewSliceIndex { get; set; } = DefaultOfflineCtTopViewSliceIndex;

        /// <summary>
        /// 像素精度：一个像素对应的物理尺寸，单位微米（μm/px）。
        /// 换算：1 mm = 1000 μm；物理尺寸(μm)=像素数×本值；物理尺寸(mm)=像素数×本值/1000。
        /// </summary>
        public double OfflineCtPixelSizeUm { get; set; } = DefaultOfflineCtPixelSizeUm;

        /// <summary>结果图模式：增强 / 原图 / 两者</summary>
        public OfflineCtResultImageMode OfflineCtResultImageMode { get; set; } = OfflineCtResultImageMode.Enhance;

        /// <summary>是否输出 annotation.json（默认关；产品目录默认只保留成功结果图）</summary>
        public bool OfflineCtExportJson { get; set; }

        /// <summary>是否输出 annotation.csv（默认关；产品目录默认只保留成功结果图）</summary>
        public bool OfflineCtExportCsv { get; set; }

        /// <summary>
        /// 历史项：是否将送入 NJCT 的预处理图落盘。现主流程用临时文件，本开关不再影响主流程。
        /// </summary>
        public bool OfflineCtExportInputForNjct { get; set; }

        /// <summary>
        /// 历史项：送入 NJCT 前是否 CW90°。现已固定旋转并写入 input/，本开关不再影响主流程。
        /// </summary>
        public bool OfflineCtRotateTopViewCw90 { get; set; } = true;

        /// <summary>方案 B：调用 NJCT 前是否按 ROI 亮度条件提亮（默认开；与是否落盘 input_for_njct 无关）</summary>
        public bool EnableOfflineCtPreBrighten { get; set; } = true;

        /// <summary>预提亮触发：ROI 均值低于此值</summary>
        public double OfflineCtPreBrightenMeanTrigger { get; set; } = 120.0;

        /// <summary>预提亮触发：ROI P50 低于此值</summary>
        public double OfflineCtPreBrightenP50Trigger { get; set; } = 110.0;

        /// <summary>预提亮目标 ROI 均值</summary>
        public double OfflineCtPreBrightenTargetMean { get; set; } = 132.0;

        /// <summary>
        /// 喂给 NJCT 前：将图像内容向上平移的像素数（画布高宽不变，底部黑边填充）。
        /// 0=不上移。用于主体偏下、DLL 裁底部时把有效区域抬到上方。
        /// </summary>
        public int OfflineCtContentShiftUpPx { get; set; } = DefaultOfflineCtContentShiftUpPx;

        /// <summary>钳制内容上移像素为非负。</summary>
        public int ResolveOfflineCtContentShiftUpPx() => Math.Max(0, OfflineCtContentShiftUpPx);

        /// <summary>解析模型路径：配置非空则用之，否则默认部署路径。</summary>
        public string ResolveOfflineCtModelPath()
        {
            if (!string.IsNullOrWhiteSpace(OfflineCtModelPath))
            {
                return OfflineCtModelPath.Trim();
            }

            return GetDefaultOfflineCtModelPath();
        }

        /// <summary>钳制「上」视图切片序号到 1～100。</summary>
        public int ResolveOfflineCtTopViewSliceIndex() =>
            Math.Clamp(OfflineCtTopViewSliceIndex, MinOfflineCtTopViewSliceIndex, MaxOfflineCtTopViewSliceIndex);

        /// <summary>钳制像素精度为非负；≤0 时回退默认值。</summary>
        public double ResolveOfflineCtPixelSizeUm() =>
            OfflineCtPixelSizeUm > 0 ? OfflineCtPixelSizeUm : DefaultOfflineCtPixelSizeUm;

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

        /// <summary>
        /// 正极 LUT 关键段端点 1 屏幕 X（物理像素）。
        /// 应用正极预设后标定；运行时取两端中点水平拖到红线目标列。
        /// </summary>
        public int LutSegmentPoint1XPositive { get; set; }
        /// <summary>正极 LUT 关键段端点 1 屏幕 Y</summary>
        public int LutSegmentPoint1YPositive { get; set; }
        /// <summary>正极 LUT 关键段端点 2 屏幕 X</summary>
        public int LutSegmentPoint2XPositive { get; set; }
        /// <summary>正极 LUT 关键段端点 2 屏幕 Y</summary>
        public int LutSegmentPoint2YPositive { get; set; }
        /// <summary>正极 LUT 关键段两端点是否已标定</summary>
        public bool LutSegmentPointsCalibratedPositive { get; set; }

        /// <summary>负极 LUT 关键段端点 1 屏幕 X（物理像素）</summary>
        public int LutSegmentPoint1XNegative { get; set; }
        /// <summary>负极 LUT 关键段端点 1 屏幕 Y</summary>
        public int LutSegmentPoint1YNegative { get; set; }
        /// <summary>负极 LUT 关键段端点 2 屏幕 X</summary>
        public int LutSegmentPoint2XNegative { get; set; }
        /// <summary>负极 LUT 关键段端点 2 屏幕 Y</summary>
        public int LutSegmentPoint2YNegative { get; set; }
        /// <summary>负极 LUT 关键段两端点是否已标定</summary>
        public bool LutSegmentPointsCalibratedNegative { get; set; }

        /// <summary>
        /// LUT 关键段按峰谷尺度扩展：左端点向左移动的 Y 倍数（默认 2）。
        /// Y = TargetX − ValleyX（ROI 列距，与屏幕拖拽像素 1:1）。
        /// </summary>
        public double LutExpandLeftYFactor { get; set; } = 2.0;

        /// <summary>
        /// LUT 关键段按峰谷尺度扩展：右端点向右移动的 Y 倍数（默认 3）。
        /// </summary>
        public double LutExpandRightYFactor { get; set; } = 3.0;

        /// <summary>正极 LUT 关键段两端点是否可用</summary>
        public bool HasLutSegmentPointsPositive =>
            LutSegmentPointsCalibratedPositive
            && (LutSegmentPoint1XPositive != LutSegmentPoint2XPositive
                || LutSegmentPoint1YPositive != LutSegmentPoint2YPositive);

        /// <summary>负极 LUT 关键段两端点是否可用</summary>
        public bool HasLutSegmentPointsNegative =>
            LutSegmentPointsCalibratedNegative
            && (LutSegmentPoint1XNegative != LutSegmentPoint2XNegative
                || LutSegmentPoint1YNegative != LutSegmentPoint2YNegative);

        /// <summary>按文件名（-Z/-F）选用对应电极的 LUT 端点是否已标定可用。</summary>
        public bool HasLutSegmentPointsFor(string? pathOrFileName) =>
            IsPositiveElectrodeFile(pathOrFileName)
                ? HasLutSegmentPointsPositive
                : HasLutSegmentPointsNegative;

        /// <summary>
        /// 按文件名解析当前电极的 LUT 关键段两端点（屏幕物理像素）。
        /// </summary>
        public bool TryResolveLutSegmentPoints(
            string? pathOrFileName,
            out int point1X,
            out int point1Y,
            out int point2X,
            out int point2Y)
        {
            if (IsPositiveElectrodeFile(pathOrFileName))
            {
                point1X = LutSegmentPoint1XPositive;
                point1Y = LutSegmentPoint1YPositive;
                point2X = LutSegmentPoint2XPositive;
                point2Y = LutSegmentPoint2YPositive;
                return HasLutSegmentPointsPositive;
            }

            point1X = LutSegmentPoint1XNegative;
            point1Y = LutSegmentPoint1YNegative;
            point2X = LutSegmentPoint2XNegative;
            point2Y = LutSegmentPoint2YNegative;
            return HasLutSegmentPointsNegative;
        }

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

        /// <summary>
        /// 视觉找点截图前等待（毫秒）。等 VG 画面坐标/标注自行消失后再截；
        /// Local / VisionDll 共用。默认 2000。
        /// </summary>
        public int VisionCaptureWaitMs { get; set; } = 2000;

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
        /// 三点定平面后、向量两点前：左侧无名厚度/偏移 Edit 的屏幕点击 X（物理像素）。
        /// Insights：Edit 无 Name，须标定后点击 → 输入 0 → Enter，否则「上」视图非预期。
        /// </summary>
        public int Simple321OffsetEditX { get; set; }

        /// <summary>三点后偏移 Edit 的屏幕点击 Y（物理像素）</summary>
        public int Simple321OffsetEditY { get; set; }

        /// <summary>是否已完成偏移 Edit 点击位置标定</summary>
        public bool Simple321OffsetEditCalibrated { get; set; }

        /// <summary>偏移 Edit 点击位置是否可用</summary>
        public bool HasSimple321OffsetEdit => Simple321OffsetEditCalibrated;

        /// <summary>
        /// 主流程是否执行阶段四简单 3-2-1。默认始终开启；未标定齐全时跳过并对该文件打日志。
        /// </summary>
        public bool EnableVisionAlignmentSteps { get; set; } = true;

        /// <summary>
        /// 简单 3-2-1 找点算法来源。默认 Local（上位机 OpenCV）；VisionDll 走 CTAutoCutImg.dll。
        /// </summary>
        public Simple321PointSource Simple321PointSource { get; set; } = Simple321PointSource.Local;

        /// <summary>是否使用视觉 CTAutoCutImg.dll 找点（与 <see cref="Simple321PointSource"/> 同步）。</summary>
        public bool UseVisionDllForSimple321 => Simple321PointSource == Simple321PointSource.VisionDll;

        /// <summary>简单 3-2-1 所需标定是否齐全（3D ROI +「上」ROI + 下拉 + 三点后偏移 Edit）</summary>
        public bool HasSimple321Prerequisites =>
            HasVision3dRoi && HasVisionTopViewRoi && HasSimple321ToolbarClick && HasSimple321OffsetEdit;

        /// <summary>
        /// 3-2-1 完成后、切片导出前：视口底部无名「捕捉」类按钮的绝对点击 X（物理像素，兜底）。
        /// 主路径优先 UIA（父组内按索引 Invoke）；失败再用本坐标 Win32 点击。
        /// </summary>
        public int PostAlignCaptureButtonX { get; set; }

        /// <summary>无名捕捉按钮绝对点击 Y（物理像素）</summary>
        public int PostAlignCaptureButtonY { get; set; }

        /// <summary>是否已完成捕捉按钮绝对坐标标定</summary>
        public bool PostAlignCaptureButtonCalibrated { get; set; }

        /// <summary>捕捉按钮绝对坐标是否可用</summary>
        public bool HasPostAlignCaptureButton => PostAlignCaptureButtonCalibrated;

        /// <summary>
        /// 同一工具栏父组内、空 Name 小按钮从左到右的索引（0 起）。
        /// UIA 方案一用此索引定位；若已标定绝对坐标则优先用坐标匹配矩形。
        /// </summary>
        public int PostAlignCaptureButtonIndex { get; set; }

        /// <summary>捕捉按钮点击后的拖拽起点 X（箭头标定）</summary>
        public int PostAlignDragStartX { get; set; }

        /// <summary>捕捉按钮点击后的拖拽起点 Y</summary>
        public int PostAlignDragStartY { get; set; }

        /// <summary>捕捉按钮点击后的拖拽终点 X（箭头方向与距离）</summary>
        public int PostAlignDragEndX { get; set; }

        /// <summary>捕捉按钮点击后的拖拽终点 Y</summary>
        public int PostAlignDragEndY { get; set; }

        /// <summary>是否已完成拖拽箭头标定</summary>
        public bool PostAlignDragCalibrated { get; set; }

        /// <summary>拖拽箭头是否可用</summary>
        public bool HasPostAlignDrag =>
            PostAlignDragCalibrated
            && (PostAlignDragStartX != PostAlignDragEndX || PostAlignDragStartY != PostAlignDragEndY);

        /// <summary>
        /// 主流程是否在 3-2-1 后、导出前执行「点捕捉按钮 → 沿箭头拖拽」。默认开；未标定齐全则跳过。
        /// </summary>
        public bool EnablePostAlignCaptureDragSteps { get; set; } = true;

        /// <summary>点按钮后、开始拖拽前的等待（毫秒）</summary>
        public int PostAlignClickWaitMs { get; set; } = 400;

        /// <summary>拖拽路径每步间隔（毫秒）</summary>
        public int PostAlignDragStepDelayMs { get; set; } = 8;

        /// <summary>
        /// 是否在进入本项目时恢复自定义上位机主窗尺寸（仅 CTAutoSlicer；其它项目仍默认最大化）。
        /// </summary>
        public bool MainWindowRememberSize { get; set; }

        /// <summary>自定义主窗宽度（逻辑像素）</summary>
        public int MainWindowWidth { get; set; } = 1280;

        /// <summary>自定义主窗高度（逻辑像素）</summary>
        public int MainWindowHeight { get; set; } = 720;

        /// <summary>自定义主窗 Left</summary>
        public int MainWindowLeft { get; set; } = 80;

        /// <summary>自定义主窗 Top</summary>
        public int MainWindowTop { get; set; } = 80;

        /// <summary>捕捉按钮 + 拖拽箭头标定是否齐全</summary>
        public bool HasPostAlignCaptureDragPrerequisites =>
            HasPostAlignCaptureButton && HasPostAlignDrag;

        // —— 3D 找点前视角准备（临时：点击切换视图 → 拖拽看清上平面；尚未接入主流程）——

        /// <summary>3D 找点前：切换视图的屏幕绝对点击 X（物理像素）</summary>
        public int Pre321ViewClickX { get; set; }

        /// <summary>3D 找点前：切换视图的屏幕绝对点击 Y（物理像素）</summary>
        public int Pre321ViewClickY { get; set; }

        /// <summary>是否已完成切换视图点击标定</summary>
        public bool Pre321ViewClickCalibrated { get; set; }

        /// <summary>切换视图点击坐标是否可用</summary>
        public bool HasPre321ViewClick => Pre321ViewClickCalibrated;

        /// <summary>切换视图后的拖拽起点 X</summary>
        public int Pre321ViewDragStartX { get; set; }

        /// <summary>切换视图后的拖拽起点 Y</summary>
        public int Pre321ViewDragStartY { get; set; }

        /// <summary>切换视图后的拖拽终点 X</summary>
        public int Pre321ViewDragEndX { get; set; }

        /// <summary>切换视图后的拖拽终点 Y</summary>
        public int Pre321ViewDragEndY { get; set; }

        /// <summary>是否已完成找点前拖拽箭头标定</summary>
        public bool Pre321ViewDragCalibrated { get; set; }

        /// <summary>找点前拖拽箭头是否可用</summary>
        public bool HasPre321ViewDrag =>
            Pre321ViewDragCalibrated
            && (Pre321ViewDragStartX != Pre321ViewDragEndX || Pre321ViewDragStartY != Pre321ViewDragEndY);

        /// <summary>点切换视图后、开始拖拽前的等待（毫秒）</summary>
        public int Pre321ViewClickWaitMs { get; set; } = 400;

        /// <summary>找点前拖拽路径每步间隔（毫秒）</summary>
        public int Pre321ViewDragStepDelayMs { get; set; } = 8;

        /// <summary>找点前视角准备（点击 + 拖拽）标定是否齐全</summary>
        public bool HasPre321ViewPrepPrerequisites =>
            HasPre321ViewClick && HasPre321ViewDrag;

        /// <summary>视觉对齐截图落盘目录（空则使用 DefaultVisionCaptureDir）</summary>
        public string VisionCaptureDir { get; set; } = DefaultVisionCaptureDir;

        /// <summary>视觉对齐截图按日目录保留天数（超过则删除）</summary>
        public int VisionCaptureKeepDays { get; set; } = DefaultCaptureKeepDays;

        /// <summary>「上」视图向量标点截图落盘目录（空则使用 DefaultVision2dVectorCaptureDir）</summary>
        public string Vision2dVectorCaptureDir { get; set; } = DefaultVision2dVectorCaptureDir;

        /// <summary>向量标点截图按日目录保留天数（超过则删除）</summary>
        public int Vision2dVectorCaptureKeepDays { get; set; } = DefaultCaptureKeepDays;

        /// <summary>
        /// 本地算法 3D 初始原图（未切 Pre321 视角）落盘目录；空则使用 DefaultVision3dInitialCaptureDir。
        /// </summary>
        public string Vision3dInitialCaptureDir { get; set; } = DefaultVision3dInitialCaptureDir;

        /// <summary>3D 初始原图按日目录保留天数（超过则删除）</summary>
        public int Vision3dInitialCaptureKeepDays { get; set; } = DefaultCaptureKeepDays;

        /// <summary>
        /// 本地算法保存 3D 初始原图前等待毫秒（直方图后、Pre321 前；默认 2000）。
        /// </summary>
        public int Vision3dInitialCaptureWaitMs { get; set; } = DefaultVision3dInitialCaptureWaitMs;

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

        public string ResolveVision3dInitialCaptureDir()
        {
            if (!string.IsNullOrWhiteSpace(Vision3dInitialCaptureDir))
            {
                return Vision3dInitialCaptureDir.Trim();
            }

            return DefaultVision3dInitialCaptureDir;
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
        /// 确保 3D 初始原图根目录与当日子目录存在，返回当日保存目录（yyyy-MM-dd）。
        /// </summary>
        public string EnsureVision3dInitialCaptureDayDir(DateTime? now = null)
        {
            return EnsureCaptureDayDir(ResolveVision3dInitialCaptureDir(), now);
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

        /// <summary>删除超过保留天数的 3D 初始原图按日文件夹。</summary>
        public (int DeletedDirs, int DeletedFiles) CleanupExpiredVision3dInitialCaptures(int? keepDays = null) =>
            CleanupExpiredCaptureDayFolders(
                ResolveVision3dInitialCaptureDir(),
                keepDays ?? Vision3dInitialCaptureKeepDays,
                rootFilePatterns: ["vision_3d_initial_*.png"]);

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

        /// <summary>将 MaxVgProcesses 限制在 [1, AbsoluteMaxVgProcesses]。</summary>
        public int GetClampedMaxVgProcesses()
        {
            if (MaxVgProcesses < 1)
            {
                return 1;
            }

            return MaxVgProcesses > AbsoluteMaxVgProcesses
                ? AbsoluteMaxVgProcesses
                : MaxVgProcesses;
        }

        public bool IsInputComplete()
        {
            return !string.IsNullOrWhiteSpace(VGAppPath)
                && VGOpenWaitTime != 0
                && VGShowFrontWaitTime != 0
                && !string.IsNullOrWhiteSpace(CTFileFolder)
                && !string.IsNullOrWhiteSpace(SliceExportFolder)
                && MaxVgProcesses > 0
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
