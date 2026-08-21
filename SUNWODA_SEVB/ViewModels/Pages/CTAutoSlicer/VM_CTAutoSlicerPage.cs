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
    [Module("VM_CTAutoSlicerPage", "CT自动化切片")]
    public partial class VM_CTAutoSlicerPage : ViewModelBase
    {
        private const string VmName = "VM_CTAutoSlicerPage";

        private readonly ILoggerService<VM_CTAutoSlicerPage> _logger;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly ICTAutoSlicerService _ctAutoSlicerService;
        private readonly IAngleAlgorithmService _angleAlgorithmService;
        private readonly ICutImgAlgorithmService _cutImgAlgorithmService;
        private readonly CTFolderMonitorService _folderMonitorService;
        private readonly CTAutoSlicerInputHookService _inputHookService;
        private readonly ProcessMonitorService _processMonitorService;
        private readonly CTAutoSlicerLocalConfigService _localConfigService;
        private CancellationTokenSource? _activeFlowCts;
        private CancellationTokenSource? _stageTestCts;
        private DispatcherTimer? _manualInterventionCountdownTimer;
        private DateTime _manualInterventionDeadlineUtc;

        private string _messageShower = "CT自动化切片模块已加载";
        private bool _isEnableSimulationBtn = true;
        private bool _isStageTestRunning;
        private bool _isFlowPaused;
        private bool _isInputBlockEnabled = true;
        private bool _isManualInterventionVisible;
        private string _manualInterventionFileName = string.Empty;
        private string _manualInterventionReason = string.Empty;
        private string _manualInterventionCountdownText = string.Empty;
        private string _vgAppPath = @"D:\App\VG\vgstudiomax20241.exe";
        private int _vgOpenWaitTime = CTAutoSlicerSettings.DefaultVGOpenWaitTime;
        private int _vgShowFrontWaitTime = 2000;
        private string _ctFileFolder = string.Empty;
        private int _maxVgProcesses = CTAutoSlicerSettings.DefaultMaxVgProcesses;
        private int _quickImportDialogWaitTime = 10000;
        private int _newProjectDialogWaitTime = 5000;
        private int _fileSelectionDialogWaitTime = 15000;
        private int _importTimeoutMinutes = 30;
        private int _postImportWaitTime = 3000;
        private int _stepRetryCount = 3;
        private int _stepRetryIntervalMs = 2000;
        private string _importLogText = string.Empty;
        private bool _isUserScrolledUp;
        private string? _lastFlowLogMessage;
        private string _renderingPresetNamePositive = "testlut_Z";
        private string _renderingPresetNameNegative = "testlut_F";
        private int _presetApplyWaitTime = 2000;
        private int _presetDropdownWaitTime = 5000;
        private string _dllDownloadBaseUrl = CTAutoSlicerSettings.DefaultDllDownloadBaseUrl;
        private int _thickPlateMenuOffsetX;
        private int _thickPlateMenuOffsetY = 28;
        private int _viewportClickOffsetY;
        private string _sliceExportFolder = string.Empty;
        private string _topViewHandoffFolder = string.Empty;
        private bool _enableAiAnnotationAutoRun;
        private DispatcherTimer? _aiAnnotationAutoRunTimer;
        private int _aiAnnotationAutoRunGate;
        private int _pipelineRunMode = (int)Core.Models.Project.CTAutoSlicer.PipelineRunMode.SliceOnly;
        private int _sliceExportImageWidth = 3000;
        private int _sliceExportQuantity = 100;
        private int _sliceExportQuantityQj = 150;
        private double _sliceRangeFrontStartMm = 20;
        private double _sliceRangeFrontEndMm = 70;
        private double _sliceRangeRightStartMm = 20;
        private double _sliceRangeRightEndMm = 70;
        private double _sliceRangeTopStartMm = 20;
        private double _sliceRangeTopEndMm = 70;
        private int _sliceExportDialogWaitTime = 8000;
        private int _sliceExportWriteTimeoutMs = 60000;
        private int _thickPlateStepDelayMs = 500;
        private int _thickPlateDialogWaitTime = 3000;
        private int _thickPlateDropdownWaitTime = 3000;
        private int _viewportQuadrant3ClickOffsetY = -100;
        private int _viewportMinWidth = 400;
        private int _viewportMinHeight = 300;
        private int _thickPlateStepRetryCount = 1;
        private int _thickPlateMenuAfterRightClickDelayMs = 200;
        private int _histogramRoiX;
        private int _histogramRoiY;
        private int _histogramRoiWidth;
        private int _histogramRoiHeight;
        private string _histogramCaptureDir = string.Empty;
        private string _visionCaptureDir = CTAutoSlicerSettings.DefaultVisionCaptureDir;
        private string _vision2dVectorCaptureDir = CTAutoSlicerSettings.DefaultVision2dVectorCaptureDir;
        private string _vision3dInitialCaptureDir = CTAutoSlicerSettings.DefaultVision3dInitialCaptureDir;
        private string _visionLearn3dSampleDir = CTAutoSlicerSettings.DefaultVisionLearn3dSampleDir;
        private string _visionLearnTopViewSampleDir = CTAutoSlicerSettings.DefaultVisionLearnTopViewSampleDir;
        private string _histogramMaskColor = "#6E6E6E";
        private int _histogramMaskTolerance = 2;
        private string _histogramRoiDisplayText = "未标定";
        private int _lutSegmentPoint1XPositive;
        private int _lutSegmentPoint1YPositive;
        private int _lutSegmentPoint2XPositive;
        private int _lutSegmentPoint2YPositive;
        private bool _lutSegmentPointsCalibratedPositive;
        private string _lutSegmentPointsDisplayTextPositive = "未标定";
        private int _lutSegmentPoint1XNegative;
        private int _lutSegmentPoint1YNegative;
        private int _lutSegmentPoint2XNegative;
        private int _lutSegmentPoint2YNegative;
        private bool _lutSegmentPointsCalibratedNegative;
        private string _lutSegmentPointsDisplayTextNegative = "未标定";
        private double _lutExpandLeftYFactor = 2.0;
        private double _lutExpandRightYFactor = 3.0;
        private string? _lutExpandLeftYFactorText;
        private string? _lutExpandRightYFactorText;
        private System.Windows.Media.Brush _histogramMaskColorBrush =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x6E, 0x6E));
        private int _vision3dRoiX;
        private int _vision3dRoiY;
        private int _vision3dRoiWidth;
        private int _vision3dRoiHeight;
        private string _vision3dRoiDisplayText = "未标定";
        private int _visionTopViewRoiX;
        private int _visionTopViewRoiY;
        private int _visionTopViewRoiWidth;
        private int _visionTopViewRoiHeight;
        private string _visionTopViewRoiDisplayText = "未标定";
        private bool _isPreviewHistogramRoi;
        private bool _isPreviewVision3dRoi;
        private bool _isPreviewVisionTopViewRoi;
        private RoiPreviewOverlayWindow? _activeRoiPreviewOverlay;
        private WindowState? _roiPreviewPreviousMainWindowState;
        private bool _isUpdatingRoiPreviewFlags;
        private int _simple321ToolbarClickX;
        private int _simple321ToolbarClickY;
        private bool _simple321ToolbarClickCalibrated;
        private string _simple321ToolbarClickDisplayText = "未标定";
        private int _simple321OffsetEditX;
        private int _simple321OffsetEditY;
        private bool _simple321OffsetEditCalibrated;
        private string _simple321OffsetEditDisplayText = "未标定";
        // 使用完全限定枚举名，避免与同名属性 Simple321PointSource 冲突（CS0236）
        private int _simple321PointSource = (int)Core.Models.Project.CTAutoSlicer.Simple321PointSource.Local;
        private int _postAlignCaptureButtonX;
        private int _postAlignCaptureButtonY;
        private bool _postAlignCaptureButtonCalibrated;
        private string _postAlignCaptureButtonDisplayText = "未标定";
        private int _postAlignCaptureButtonIndex;
        private int _postAlignDragStartX;
        private int _postAlignDragStartY;
        private int _postAlignDragEndX;
        private int _postAlignDragEndY;
        private bool _postAlignDragCalibrated;
        private string _postAlignDragDisplayText = "未标定";
        private bool _enablePostAlignCaptureDragSteps = true;
        private bool _enableOfflineCtVisionAnnotation;
        private string _offlineCtModelPath = string.Empty;
        private bool _offlineCtIsRunCuda = true;
        private int _offlineCtBlackHeight = CTAutoSlicerSettings.DefaultOfflineCtBlackHeight;
        private int _offlineCtContentShiftUpPx = CTAutoSlicerSettings.DefaultOfflineCtContentShiftUpPx;
        private bool _enableOfflineCtLRangeCheck;
        private double _offlineCtL1MinMm = CTAutoSlicerSettings.DefaultOfflineCtLMinMm;
        private double _offlineCtL1MaxMm = CTAutoSlicerSettings.DefaultOfflineCtLMaxMm;
        private double _offlineCtL2MinMm = CTAutoSlicerSettings.DefaultOfflineCtLMinMm;
        private double _offlineCtL2MaxMm = CTAutoSlicerSettings.DefaultOfflineCtLMaxMm;
        private double _offlineCtL3MinMm = CTAutoSlicerSettings.DefaultOfflineCtLMinMm;
        private double _offlineCtL3MaxMm = CTAutoSlicerSettings.DefaultOfflineCtLMaxMm;
        private double _offlineCtL4MinMm = CTAutoSlicerSettings.DefaultOfflineCtLMinMm;
        private double _offlineCtL4MaxMm = CTAutoSlicerSettings.DefaultOfflineCtLMaxMm;
        private double _offlineCtL5MinMm = CTAutoSlicerSettings.DefaultOfflineCtLMinMm;
        private double _offlineCtL5MaxMm = CTAutoSlicerSettings.DefaultOfflineCtLMaxMm;
        private string? _offlineCtL1MinMmText;
        private string? _offlineCtL1MaxMmText;
        private string? _offlineCtL2MinMmText;
        private string? _offlineCtL2MaxMmText;
        private string? _offlineCtL3MinMmText;
        private string? _offlineCtL3MaxMmText;
        private string? _offlineCtL4MinMmText;
        private string? _offlineCtL4MaxMmText;
        private string? _offlineCtL5MinMmText;
        private string? _offlineCtL5MaxMmText;
        private int _offlineCtTopViewSliceIndex = CTAutoSlicerSettings.DefaultOfflineCtTopViewSliceIndex;
        private double _offlineCtPixelSizeUm = CTAutoSlicerSettings.DefaultOfflineCtPixelSizeUm;
        /// <summary>像素精度文本框编辑缓冲（允许输入「1.」「0.」等中间态）。</summary>
        private string? _offlineCtPixelSizeUmText;
        // 使用完全限定枚举名，避免与同名属性 OfflineCtResultImageMode 冲突（CS0236）
        private int _offlineCtResultImageMode = (int)Core.Models.Project.CTAutoSlicer.OfflineCtResultImageMode.Enhance;
        private bool _offlineCtExportJson;
        private bool _offlineCtExportCsv;
        private bool _offlineCtExportInputForNjct;
        private bool _offlineCtRotateTopViewCw90 = true;
        private bool _enableOfflineCtPreBrighten = true;
        private double _offlineCtPreBrightenMeanTrigger = 120.0;
        private double _offlineCtPreBrightenP50Trigger = 110.0;
        private double _offlineCtPreBrightenTargetMean = 132.0;
        private string _offlineCtManualInputImagePath = string.Empty;
        private string _offlineCtManualOutputFolder = string.Empty;
        private string _offlineCtDllTestInputFolder = string.Empty;
        private string _offlineCtDllTestOutputFolder = string.Empty;
        private int _offlineCtDllTestBlackHeight;
        private ImageSource? _latestAiAnnotationPreview;
        private string _latestAiAnnotationPreviewPath = string.Empty;
        private string _latestAiAnnotationPreviewInfo = "暂无 AI 标注结果预览";
        private string _latestAiAnnotationSummaryText = string.Empty;
        private double _latestAiAnnotationPreviewPixelWidth = 1;
        private double _latestAiAnnotationPreviewPixelHeight = 1;
        // 检测统计 Demo（假数据；后续接真实判定）
        private bool _statsDemoInitialized;
        private int _statsTodayTotal;
        private int _statsTodayOk;
        private int _statsTodayNg;
        private string _statsDemoDateText = string.Empty;
        private string _statsDemoNote = "当前为 Demo 假数据，尚未接入真实检测判定。";
        private PlotModel? _statsOkNgPieModel;
        private PlotModel? _statsHourlyBarModel;
        private string _cutImgDllTestSectionFolder = string.Empty;
        private string _cutImgDllTestVectorFolder = string.Empty;
        private string _cutImgDllTestOutputDir = CTAutoSlicerSettings.DefaultCutImgDllTestOutputDir;
        private static readonly string[] CutImgDllTestImageExtensions =
            [".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"];
        private int _postAlignClickWaitMs = 400;
        private int _postAlignDragStepDelayMs = 8;
        private int _pre321ViewClickX;
        private int _pre321ViewClickY;
        private bool _pre321ViewClickCalibrated;
        private string _pre321ViewClickDisplayText = "未标定";
        private int _pre321ViewDragStartX;
        private int _pre321ViewDragStartY;
        private int _pre321ViewDragEndX;
        private int _pre321ViewDragEndY;
        private bool _pre321ViewDragCalibrated;
        private string _pre321ViewDragDisplayText = "未标定";
        private int _pre321ViewClickWaitMs = 400;
        private int _pre321ViewDragStepDelayMs = 8;
        private bool _mainWindowRememberSize;
        private int _mainWindowWidth = 1280;
        private int _mainWindowHeight = 720;
        private int _mainWindowLeft = 80;
        private int _mainWindowTop = 80;
        private string _mainWindowSizeDisplayText = "未保存自定义尺寸（默认全屏）";
        /// <summary>false=生产模式（默认）；true=调试模式（显示单步测试）。</summary>
        private bool _isDebugMode;

        /// <summary>左侧导航选中项：0总览…8AI标注 9单测 10偏好。</summary>
        private int _selectedNavIndex;
        private int _selectedPrefSubIndex;
        /// <summary>工作区预览色（实时作用于界面，未点保存则不落盘）。</summary>
        private string _themePageBgColor = CTAutoSlicerSettings.DefaultThemePageBgColor;
        private string _themeOuterCardColor = CTAutoSlicerSettings.DefaultThemeOuterCardColor;
        private string _themeInnerCardColor = CTAutoSlicerSettings.DefaultThemeInnerCardColor;
        private string _themeSidebarColor = CTAutoSlicerSettings.DefaultThemeSidebarColor;
        /// <summary>已持久化的主题色（下次启动加载）。</summary>
        private string _savedThemePageBgColor = CTAutoSlicerSettings.DefaultThemePageBgColor;
        private string _savedThemeOuterCardColor = CTAutoSlicerSettings.DefaultThemeOuterCardColor;
        private string _savedThemeInnerCardColor = CTAutoSlicerSettings.DefaultThemeInnerCardColor;
        private string _savedThemeSidebarColor = CTAutoSlicerSettings.DefaultThemeSidebarColor;
        private bool _hasUnsavedThemeChanges;
        private string _newThemePresetName = string.Empty;
        private int _themeEditSlot;
        private int _themeEditR = 247;
        private int _themeEditG = 228;
        private int _themeEditB = 229;
        private bool _themeEditSyncing;

        public const int NavOverview = 0;
        public const int NavApp = 1;
        public const int NavImport = 2;
        public const int NavRender = 3;
        public const int NavHistogram = 4;
        public const int NavVision = 5;
        public const int NavPostAlign = 6;
        public const int NavSlice = 7;
        public const int NavAiAnnotate = 8;
        public const int NavManualMeasure = 9;
        public const int NavStepTest = 10;
        public const int NavPreferences = 11;
        /// <summary>当日检测统计（AI 模式侧栏；当前为 Demo 假数据）。</summary>
        public const int NavStats = 12;

        public const int PrefSubColors = 0;

        public string MessageShower
        {
            get => _messageShower;
            set => SetProperty(ref _messageShower, value);
        }

        /// <summary>调试模式：显示「单步测试」等调试控件。默认关闭（生产模式）。</summary>
        public bool IsDebugMode
        {
            get => _isDebugMode;
            set
            {
                if (SetProperty(ref _isDebugMode, value))
                {
                    OnPropertyChanged(nameof(IsProductionMode));
                    OnPropertyChanged(nameof(RunModeMenuHeader));
                    OnPropertyChanged(nameof(RunModeDisplayText));
                    OnPropertyChanged(nameof(PrimaryActionToolTip));
                    if (!value && SelectedNavIndex == NavStepTest)
                    {
                        SelectedNavIndex = NavOverview;
                    }

                    MessageShower = value
                        ? "已切换为调试模式（可切换切图/AI 模式，并可使用单步测试）"
                        : "已切换为生产模式（单步测试已隐藏；流水线模式保持上次选择）";
                }
            }
        }

        /// <summary>生产模式（默认）。与 <see cref="IsDebugMode"/> 互斥。</summary>
        public bool IsProductionMode
        {
            get => !_isDebugMode;
            set => IsDebugMode = !value;
        }

        /// <summary>顶部菜单「运行模式」标题，显示当前模式。</summary>
        public string RunModeMenuHeader =>
            IsDebugMode ? "运行模式：调试" : "运行模式：生产";

        /// <summary>顶栏 pill 显示的模式文案（生产/调试 · 切图/AI）。</summary>
        public string RunModeDisplayText
        {
            get
            {
                var ui = IsDebugMode ? "调试" : "生产";
                var pipe = IsAiPipelineMode ? "AI标注" : "切图";
                return $"{ui} · {pipe}";
            }
        }

        /// <summary>流水线模式：切图 / AI 标注（持久化；调试胶囊切换；重启后恢复）。</summary>
        public int PipelineRunMode
        {
            get => _pipelineRunMode;
            set
            {
                var clamped = Math.Clamp(value, 0, 1);
                if (!SetProperty(ref _pipelineRunMode, clamped))
                {
                    return;
                }

                SavePipelineRunModeToLocalConfig(clamped);
                NotifyPipelineModeUiChanged();
                if (!_isLoadingSettings)
                {
                    MessageShower = IsAiPipelineMode
                        ? "已切换为 AI 标注模式（开始流程将扫描交接目录批量推理）"
                        : "已切换为切图模式（开始流程导出后复制上视图送检图到交接目录）";
                }
            }
        }

        public bool IsSlicePipelineMode
        {
            get => _pipelineRunMode == (int)Core.Models.Project.CTAutoSlicer.PipelineRunMode.SliceOnly;
            set
            {
                if (value)
                {
                    PipelineRunMode = (int)Core.Models.Project.CTAutoSlicer.PipelineRunMode.SliceOnly;
                }
            }
        }

        public bool IsAiPipelineMode
        {
            get => _pipelineRunMode == (int)Core.Models.Project.CTAutoSlicer.PipelineRunMode.AiAnnotateOnly;
            set
            {
                if (value)
                {
                    PipelineRunMode = (int)Core.Models.Project.CTAutoSlicer.PipelineRunMode.AiAnnotateOnly;
                }
            }
        }

        /// <summary>切图侧栏项（应用/导入/…/切片）可见。</summary>
        public bool ShowSlicePipelineNav => IsSlicePipelineMode;

        /// <summary>AI 侧栏「AI图像标注」可见。</summary>
        public bool ShowAiPipelineNav => IsAiPipelineMode;

        public string OverviewHeroTitle =>
            IsAiPipelineMode ? "CT 离线 AI 标注" : "CT 自动化切片";

        /// <summary>切片导出目录在 AI 模式下显示为标注结果根目录。</summary>
        public string SliceExportFolderLabel =>
            IsAiPipelineMode ? "标注结果根目录" : "切片导出目录";

        public string PrimaryActionToolTip =>
            IsAiAnnotationAutoModeActive
                ? "当前为自动模式：已锁定手动开始；检测到交接目录有 jpg/jpeg 将自动批处理"
                : IsAiPipelineMode
                    ? "AI 标注入口：扫描上视图交接目录，批量调用 NJCT（不启 VG）"
                    : "切图入口：按 CT 体素文件夹排队执行导入→对齐→三视图导出→交接上视图送检图";

        private void NotifyPipelineModeUiChanged()
        {
            OnPropertyChanged(nameof(IsSlicePipelineMode));
            OnPropertyChanged(nameof(IsAiPipelineMode));
            OnPropertyChanged(nameof(ShowSlicePipelineNav));
            OnPropertyChanged(nameof(ShowAiPipelineNav));
            OnPropertyChanged(nameof(RunModeDisplayText));
            OnPropertyChanged(nameof(PrimaryActionToolTip));
            OnPropertyChanged(nameof(PrimaryActionButtonText));
            OnPropertyChanged(nameof(ShowAiAnnotationAutoRunOption));
            OnPropertyChanged(nameof(IsAiAnnotationAutoModeActive));
            OnPropertyChanged(nameof(IsPrimaryActionEnabled));
            OnPropertyChanged(nameof(OverviewHeroTitle));
            OnPropertyChanged(nameof(SliceExportFolderLabel));
            EnsureNavValidForPipelineMode();
            SyncAiAnnotationAutoRunMonitor();

            // AI 模式默认进入带结果图预览的「二次手动标注」页
            if (IsAiPipelineMode && SelectedNavIndex == NavOverview)
            {
                SelectedNavIndex = NavManualMeasure;
            }
        }

        public int SelectedNavIndex
        {
            get => _selectedNavIndex;
            set
            {
                if (value == NavStepTest && !IsDebugMode)
                {
                    value = IsAiPipelineMode ? NavManualMeasure : NavOverview;
                }

                if (!IsNavAllowedForCurrentPipeline(value))
                {
                    // AI 模式主界面为带图预览的「二次手动标注」
                    value = IsAiPipelineMode ? NavManualMeasure : NavOverview;
                }

                if (value == _selectedNavIndex)
                {
                    return;
                }

                // 离开偏好且未保存：丢弃预览，恢复已保存配色
                if (_selectedNavIndex == NavPreferences && value != NavPreferences && HasUnsavedThemeChanges)
                {
                    RevertThemeColorsToSaved(showMessage: true);
                }

                if (SetProperty(ref _selectedNavIndex, value))
                {
                    NotifyNavSectionChanged();
                    if (value == NavManualMeasure)
                    {
                        RefreshLatestAiAnnotationPreview();
                    }

                    if (value == NavStats)
                    {
                        EnsureStatsDemoInitialized();
                    }
                }
            }
        }

        public bool IsNavOverview => SelectedNavIndex == NavOverview;
        public bool IsNavApp => SelectedNavIndex == NavApp;
        public bool IsNavImport => SelectedNavIndex == NavImport;
        public bool IsNavRender => SelectedNavIndex == NavRender;
        public bool IsNavHistogram => SelectedNavIndex == NavHistogram;
        public bool IsNavVision => SelectedNavIndex == NavVision;
        public bool IsNavPostAlign => SelectedNavIndex == NavPostAlign;
        public bool IsNavSlice => SelectedNavIndex == NavSlice;
        public bool IsNavAiAnnotate => SelectedNavIndex == NavAiAnnotate;
        public bool IsNavManualMeasure => SelectedNavIndex == NavManualMeasure;
        public bool IsNavStepTest => SelectedNavIndex == NavStepTest;
        public bool IsNavPreferences => SelectedNavIndex == NavPreferences;
        public bool IsNavStats => SelectedNavIndex == NavStats;
        public bool IsPrefSubColors => IsNavPreferences && SelectedPrefSubIndex == PrefSubColors;

        public string CurrentSectionTitle => SelectedNavIndex switch
        {
            NavOverview => "运行总览",
            NavApp => "应用参数",
            NavImport => "导入参数",
            NavRender => "渲染与厚板",
            NavHistogram => "直方图标定",
            NavVision => "视觉对齐",
            NavPostAlign => "对齐后拖拽",
            NavSlice => "切片导出",
            NavAiAnnotate => "AI图像标注",
            NavManualMeasure => "二次手动标注",
            NavStepTest => "单步测试",
            NavPreferences => "偏好设置",
            NavStats => "检测统计",
            _ => "CT自动化切片",
        };

        public int SelectedPrefSubIndex
        {
            get => _selectedPrefSubIndex;
            set
            {
                if (SetProperty(ref _selectedPrefSubIndex, value))
                {
                    OnPropertyChanged(nameof(IsPrefSubColors));
                }
            }
        }

        /// <summary>0页面底色 1外层卡片 2内部卡片 3左侧菜单</summary>
        public int ThemeEditSlot
        {
            get => _themeEditSlot;
            set
            {
                if (SetProperty(ref _themeEditSlot, value))
                {
                    LoadThemeEditFromSlot();
                    OnPropertyChanged(nameof(IsThemeSlotPageBg));
                    OnPropertyChanged(nameof(IsThemeSlotOuterCard));
                    OnPropertyChanged(nameof(IsThemeSlotInnerCard));
                    OnPropertyChanged(nameof(IsThemeSlotSidebar));
                    OnPropertyChanged(nameof(ThemeEditSlotTitle));
                }
            }
        }

        public bool IsThemeSlotPageBg => ThemeEditSlot == 0;
        public bool IsThemeSlotOuterCard => ThemeEditSlot == 1;
        public bool IsThemeSlotInnerCard => ThemeEditSlot == 2;
        public bool IsThemeSlotSidebar => ThemeEditSlot == 3;

        public string ThemeEditSlotTitle => ThemeEditSlot switch
        {
            0 => "页面底色",
            1 => "最外层卡片",
            2 => "内部分组卡片",
            3 => "左侧菜单栏",
            _ => "颜色",
        };

        public string ThemePageBgColor
        {
            get => _themePageBgColor;
            set => SetThemeColor(ref _themePageBgColor, value, nameof(ThemePageBgColor), nameof(ThemePageBgBrush));
        }

        public string ThemeOuterCardColor
        {
            get => _themeOuterCardColor;
            set => SetThemeColor(ref _themeOuterCardColor, value, nameof(ThemeOuterCardColor), nameof(ThemeOuterCardBrush));
        }

        public string ThemeInnerCardColor
        {
            get => _themeInnerCardColor;
            set => SetThemeColor(ref _themeInnerCardColor, value, nameof(ThemeInnerCardColor), nameof(ThemeInnerCardBrush));
        }

        public string ThemeSidebarColor
        {
            get => _themeSidebarColor;
            set => SetThemeColor(ref _themeSidebarColor, value, nameof(ThemeSidebarColor), nameof(ThemeSidebarBrush));
        }

        public Brush ThemePageBgBrush => CreateBrush(ThemePageBgColor);
        public Brush ThemeOuterCardBrush => CreateBrush(ThemeOuterCardColor);
        public Brush ThemeInnerCardBrush => CreateBrush(ThemeInnerCardColor);
        public Brush ThemeSidebarBrush => CreateBrush(ThemeSidebarColor);
        public Brush ThemeEditPreviewBrush => CreateBrush(ThemeEditHex);

        /// <summary>预览色与已保存色不一致时为 true，需点「保存主题」才落盘。</summary>
        public bool HasUnsavedThemeChanges
        {
            get => _hasUnsavedThemeChanges;
            private set => SetProperty(ref _hasUnsavedThemeChanges, value);
        }

        /// <summary>内置 + 自定义配色方案列表。</summary>
        public ObservableCollection<ThemePresetItem> ThemeColorPresets { get; } = new();

        public string NewThemePresetName
        {
            get => _newThemePresetName;
            set => SetProperty(ref _newThemePresetName, value ?? string.Empty);
        }

        public sealed class ThemePresetItem
        {
            public ThemePresetItem(CTAutoSlicerSettings.ThemeColorPreset preset)
            {
                Id = preset.Id;
                Name = preset.Name;
                Description = preset.Description;
                PageBg = preset.PageBg;
                OuterCard = preset.OuterCard;
                InnerCard = preset.InnerCard;
                Sidebar = preset.Sidebar;
                IsBuiltIn = preset.IsBuiltIn;
                RefreshBrushes();
            }

            public string Id { get; }
            public string Name { get; }
            public string Description { get; }
            /// <summary>单行展示：名称 · 说明，避免双行被裁切。</summary>
            public string TitleLine =>
                string.IsNullOrWhiteSpace(Description) ? Name : $"{Name}  ·  {Description}";
            public string PageBg { get; }
            public string OuterCard { get; }
            public string InnerCard { get; }
            public string Sidebar { get; }
            public bool IsBuiltIn { get; }
            public bool CanDelete => !IsBuiltIn;
            public Brush PageBgBrush { get; private set; } = Brushes.White;
            public Brush OuterCardBrush { get; private set; } = Brushes.White;
            public Brush InnerCardBrush { get; private set; } = Brushes.White;
            public Brush SidebarBrush { get; private set; } = Brushes.White;

            public void RefreshBrushes()
            {
                PageBgBrush = CreateBrush(PageBg);
                OuterCardBrush = CreateBrush(OuterCard);
                InnerCardBrush = CreateBrush(InnerCard);
                SidebarBrush = CreateBrush(Sidebar);
            }
        }

        public int ThemeEditR
        {
            get => _themeEditR;
            set
            {
                var clamped = Math.Clamp(value, 0, 255);
                if (SetProperty(ref _themeEditR, clamped) && !_themeEditSyncing)
                {
                    CommitThemeEditRgb();
                }
            }
        }

        public int ThemeEditG
        {
            get => _themeEditG;
            set
            {
                var clamped = Math.Clamp(value, 0, 255);
                if (SetProperty(ref _themeEditG, clamped) && !_themeEditSyncing)
                {
                    CommitThemeEditRgb();
                }
            }
        }

        public int ThemeEditB
        {
            get => _themeEditB;
            set
            {
                var clamped = Math.Clamp(value, 0, 255);
                if (SetProperty(ref _themeEditB, clamped) && !_themeEditSyncing)
                {
                    CommitThemeEditRgb();
                }
            }
        }

        public string ThemeEditHex
        {
            get => $"#{ThemeEditR:X2}{ThemeEditG:X2}{ThemeEditB:X2}";
            set
            {
                if (!TryParseHexColor(value, out var color))
                {
                    return;
                }

                _themeEditSyncing = true;
                try
                {
                    ThemeEditR = color.R;
                    ThemeEditG = color.G;
                    ThemeEditB = color.B;
                }
                finally
                {
                    _themeEditSyncing = false;
                }

                CommitThemeEditRgb();
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThemeEditPreviewBrush));
                OnPropertyChanged(nameof(ThemeEditColor));
            }
        }

        public string ThemeEditRgbText => $"RGB({ThemeEditR}, {ThemeEditG}, {ThemeEditB})";

        /// <summary>圆环取色绑定色（与 RGB/HEX 同步）。</summary>
        public Color ThemeEditColor
        {
            get => Color.FromRgb((byte)ThemeEditR, (byte)ThemeEditG, (byte)ThemeEditB);
            set
            {
                if (_themeEditSyncing)
                {
                    return;
                }

                _themeEditSyncing = true;
                try
                {
                    _themeEditR = value.R;
                    _themeEditG = value.G;
                    _themeEditB = value.B;
                }
                finally
                {
                    _themeEditSyncing = false;
                }

                OnPropertyChanged(nameof(ThemeEditR));
                OnPropertyChanged(nameof(ThemeEditG));
                OnPropertyChanged(nameof(ThemeEditB));
                OnPropertyChanged(nameof(ThemeEditHex));
                OnPropertyChanged(nameof(ThemeEditRgbText));
                OnPropertyChanged(nameof(ThemeEditPreviewBrush));
                OnPropertyChanged();
                CommitThemeEditRgb();
            }
        }

        public string PrimaryActionButtonText =>
            IsAiAnnotationAutoModeActive
                ? "自动模式"
                : IsEnableSimulationBtn ? "开始流程" : "运行中…";

        /// <summary>
        /// 「开始流程」是否可点：运行中不可点；AI 自动模式下锁定为灰色不可点。
        /// </summary>
        public bool IsPrimaryActionEnabled =>
            IsEnableSimulationBtn && !IsAiAnnotationAutoModeActive;

        /// <summary>AI 模式且已勾选「自动执行」。</summary>
        public bool IsAiAnnotationAutoModeActive =>
            IsAiPipelineMode && EnableAiAnnotationAutoRun;

        /// <summary>仅 AI 流水线显示「自动执行」勾选。</summary>
        public bool ShowAiAnnotationAutoRunOption => IsAiPipelineMode;

        /// <summary>
        /// 阶段单测是否可点：空闲可点；主流程 Alt+P 暂停时可点；运行中未暂停不可点。
        /// </summary>
        public bool IsStageTestEnabled =>
            !_isStageTestRunning && (IsEnableSimulationBtn || IsFlowPaused);

        /// <summary>流程暂停中（Alt+P 切换）；用于主界面醒目横幅。</summary>
        public bool IsFlowPaused
        {
            get => _isFlowPaused;
            private set
            {
                if (SetProperty(ref _isFlowPaused, value))
                {
                    OnPropertyChanged(nameof(IsStageTestEnabled));
                }
            }
        }

        /// <summary>键鼠是否处于锁定期望（开始流程默认开；Alt+O 切换）。</summary>
        public bool IsInputBlockEnabled
        {
            get => _isInputBlockEnabled;
            private set
            {
                if (SetProperty(ref _isInputBlockEnabled, value))
                {
                    OnPropertyChanged(nameof(InputBlockStatusText));
                }
            }
        }

        /// <summary>键鼠锁定状态文案（总览/底部栏）。</summary>
        public string InputBlockStatusText
        {
            get
            {
                // 空闲：键鼠未锁；开始流程后才会默认锁定
                if (IsEnableSimulationBtn && !_ctAutoSlicerService.IsRunning)
                {
                    return "键鼠：空闲";
                }

                return IsInputBlockEnabled ? "键鼠：已锁定" : "键鼠：已解锁";
            }
        }

        /// <summary>主页展示的全局快捷键说明。</summary>
        public string HotkeyHelpText { get; } =
            "Alt+O 锁定/解锁键鼠（开始流程默认锁定）　|　Alt+P 暂停/继续　|　Alt+X 中断流程";

        /// <summary>流程异常/超时：等待人工处理的醒目面板。</summary>
        public bool IsManualInterventionVisible
        {
            get => _isManualInterventionVisible;
            private set => SetProperty(ref _isManualInterventionVisible, value);
        }

        public string ManualInterventionFileName
        {
            get => _manualInterventionFileName;
            private set => SetProperty(ref _manualInterventionFileName, value);
        }

        public string ManualInterventionReason
        {
            get => _manualInterventionReason;
            private set => SetProperty(ref _manualInterventionReason, value);
        }

        public string ManualInterventionCountdownText
        {
            get => _manualInterventionCountdownText;
            private set => SetProperty(ref _manualInterventionCountdownText, value);
        }

        public bool IsEnableSimulationBtn
        {
            get => _isEnableSimulationBtn;
            set
            {
                if (SetProperty(ref _isEnableSimulationBtn, value))
                {
                    OnPropertyChanged(nameof(PrimaryActionButtonText));
                    OnPropertyChanged(nameof(IsPrimaryActionEnabled));
                    OnPropertyChanged(nameof(IsStageTestEnabled));
                    OnPropertyChanged(nameof(InputBlockStatusText));
                    if (value)
                    {
                        IsFlowPaused = false;
                        HideManualInterventionPanel();
                    }
                }
            }
        }

        public string VGAppPath
        {
            get => _vgAppPath;
            set
            {
                if (SetProperty(ref _vgAppPath, value))
                {
                    SaveSetting(nameof(VGAppPath), value);
                }
            }
        }

        public int VGOpenWaitTime
        {
            get => _vgOpenWaitTime;
            set
            {
                if (SetProperty(ref _vgOpenWaitTime, value))
                {
                    SaveSetting(nameof(VGOpenWaitTime), value);
                }
            }
        }

        public string CTFileFolder
        {
            get => _ctFileFolder;
            set
            {
                if (SetProperty(ref _ctFileFolder, value))
                {
                    SaveSetting(nameof(CTFileFolder), value);
                }
            }
        }

        /// <summary>VG 进程池最大并发数（导入重叠；后处理串行）。</summary>
        public int MaxVgProcesses
        {
            get => _maxVgProcesses;
            set
            {
                var clamped = value < 1
                    ? 1
                    : (value > CTAutoSlicerSettings.AbsoluteMaxVgProcesses
                        ? CTAutoSlicerSettings.AbsoluteMaxVgProcesses
                        : value);
                if (SetProperty(ref _maxVgProcesses, clamped))
                {
                    SaveSetting(nameof(MaxVgProcesses), clamped);
                }
            }
        }

        public int QuickImportDialogWaitTime
        {
            get => _quickImportDialogWaitTime;
            set
            {
                if (SetProperty(ref _quickImportDialogWaitTime, value))
                {
                    SaveSetting(nameof(QuickImportDialogWaitTime), value);
                }
            }
        }

        public int NewProjectDialogWaitTime
        {
            get => _newProjectDialogWaitTime;
            set
            {
                if (SetProperty(ref _newProjectDialogWaitTime, value))
                {
                    SaveSetting(nameof(NewProjectDialogWaitTime), value);
                }
            }
        }

        public int FileSelectionDialogWaitTime
        {
            get => _fileSelectionDialogWaitTime;
            set
            {
                if (SetProperty(ref _fileSelectionDialogWaitTime, value))
                {
                    SaveSetting(nameof(FileSelectionDialogWaitTime), value);
                }
            }
        }

        public int ImportTimeoutMinutes
        {
            get => _importTimeoutMinutes;
            set
            {
                if (SetProperty(ref _importTimeoutMinutes, value))
                {
                    SaveSetting(nameof(ImportTimeoutMinutes), value);
                }
            }
        }

        public int PostImportWaitTime
        {
            get => _postImportWaitTime;
            set
            {
                if (SetProperty(ref _postImportWaitTime, value))
                {
                    SaveSetting(nameof(PostImportWaitTime), value);
                }
            }
        }

        public int StepRetryCount
        {
            get => _stepRetryCount;
            set
            {
                if (SetProperty(ref _stepRetryCount, value))
                {
                    SaveSetting(nameof(StepRetryCount), value);
                }
            }
        }

        public int StepRetryIntervalMs
        {
            get => _stepRetryIntervalMs;
            set
            {
                if (SetProperty(ref _stepRetryIntervalMs, value))
                {
                    SaveSetting(nameof(StepRetryIntervalMs), value);
                }
            }
        }

        public string ImportLogText
        {
            get => _importLogText;
            private set => SetProperty(ref _importLogText, value);
        }

        /// <summary>正极 LUT 预设名（文件名含 -Z；对应 VG 中 testlut_Z 等）</summary>
        public string RenderingPresetNamePositive
        {
            get => _renderingPresetNamePositive;
            set
            {
                if (SetProperty(ref _renderingPresetNamePositive, value))
                {
                    SaveSetting(nameof(RenderingPresetNamePositive), value);
                }
            }
        }

        /// <summary>负极 LUT 预设名（文件名含 -F 或默认；对应 VG 中 testlut_F 等）</summary>
        public string RenderingPresetNameNegative
        {
            get => _renderingPresetNameNegative;
            set
            {
                if (SetProperty(ref _renderingPresetNameNegative, value))
                {
                    SaveSetting(nameof(RenderingPresetNameNegative), value);
                }
            }
        }

        public int PresetApplyWaitTime
        {
            get => _presetApplyWaitTime;
            set
            {
                if (SetProperty(ref _presetApplyWaitTime, value))
                {
                    SaveSetting(nameof(PresetApplyWaitTime), value);
                }
            }
        }

        public int PresetDropdownWaitTime
        {
            get => _presetDropdownWaitTime;
            set
            {
                if (SetProperty(ref _presetDropdownWaitTime, value))
                {
                    SaveSetting(nameof(PresetDropdownWaitTime), value);
                }
            }
        }

        public int ThickPlateMenuOffsetX
        {
            get => _thickPlateMenuOffsetX;
            set
            {
                if (SetProperty(ref _thickPlateMenuOffsetX, value))
                {
                    SaveSetting(nameof(ThickPlateMenuOffsetX), value);
                }
            }
        }

        public int ThickPlateMenuOffsetY
        {
            get => _thickPlateMenuOffsetY;
            set
            {
                if (SetProperty(ref _thickPlateMenuOffsetY, value))
                {
                    SaveSetting(nameof(ThickPlateMenuOffsetY), value);
                }
            }
        }

        public int ViewportClickOffsetY
        {
            get => _viewportClickOffsetY;
            set
            {
                if (SetProperty(ref _viewportClickOffsetY, value))
                {
                    SaveSetting(nameof(ViewportClickOffsetY), value);
                }
            }
        }

        public string SliceExportFolder
        {
            get => _sliceExportFolder;
            set
            {
                if (SetProperty(ref _sliceExportFolder, value))
                {
                    SaveSetting(nameof(SliceExportFolder), value);
                    if (IsNavManualMeasure)
                    {
                        RefreshLatestAiAnnotationPreview();
                    }
                }
            }
        }

        /// <summary>上视图送检图交接根目录（切图写入 / AI 读取）。</summary>
        public string TopViewHandoffFolder
        {
            get => _topViewHandoffFolder;
            set
            {
                if (SetProperty(ref _topViewHandoffFolder, value ?? string.Empty))
                {
                    SaveSetting(nameof(TopViewHandoffFolder), value ?? string.Empty);
                    SyncAiAnnotationAutoRunMonitor();
                }
            }
        }

        /// <summary>
        /// AI 标注「自动执行」：勾选后监控交接目录 jpg，有则自动批处理；开始按钮锁定为自动模式。
        /// </summary>
        public bool EnableAiAnnotationAutoRun
        {
            get => _enableAiAnnotationAutoRun;
            set
            {
                if (!SetProperty(ref _enableAiAnnotationAutoRun, value))
                {
                    return;
                }

                SaveSetting(nameof(EnableAiAnnotationAutoRun), value ? 1 : 0);
                OnPropertyChanged(nameof(IsAiAnnotationAutoModeActive));
                OnPropertyChanged(nameof(IsPrimaryActionEnabled));
                OnPropertyChanged(nameof(PrimaryActionButtonText));
                OnPropertyChanged(nameof(PrimaryActionToolTip));
                SyncAiAnnotationAutoRunMonitor();

                if (_isLoadingSettings)
                {
                    return;
                }

                if (value && IsAiPipelineMode)
                {
                    MessageShower = "当前为自动模式：检测到交接目录有 jpg 将自动执行；「开始流程」已锁定";
                }
                else if (!value && IsAiPipelineMode)
                {
                    MessageShower = "已关闭自动模式，可手动点击「开始流程」";
                }
            }
        }

        public int SliceExportImageWidth
        {
            get => _sliceExportImageWidth;
            set
            {
                if (SetProperty(ref _sliceExportImageWidth, value))
                {
                    SaveSetting(nameof(SliceExportImageWidth), value);
                }
            }
        }

        public int SliceExportQuantity
        {
            get => _sliceExportQuantity;
            set
            {
                if (SetProperty(ref _sliceExportQuantity, value))
                {
                    SaveSetting(nameof(SliceExportQuantity), value);
                }
            }
        }

        public int SliceExportQuantityQj
        {
            get => _sliceExportQuantityQj;
            set
            {
                if (SetProperty(ref _sliceExportQuantityQj, value))
                {
                    SaveSetting(nameof(SliceExportQuantityQj), value);
                }
            }
        }

        public double SliceRangeFrontStartMm
        {
            get => _sliceRangeFrontStartMm;
            set
            {
                if (SetProperty(ref _sliceRangeFrontStartMm, value))
                {
                    SaveSetting(nameof(SliceRangeFrontStartMm), value);
                }
            }
        }

        public double SliceRangeFrontEndMm
        {
            get => _sliceRangeFrontEndMm;
            set
            {
                if (SetProperty(ref _sliceRangeFrontEndMm, value))
                {
                    SaveSetting(nameof(SliceRangeFrontEndMm), value);
                }
            }
        }

        public double SliceRangeRightStartMm
        {
            get => _sliceRangeRightStartMm;
            set
            {
                if (SetProperty(ref _sliceRangeRightStartMm, value))
                {
                    SaveSetting(nameof(SliceRangeRightStartMm), value);
                }
            }
        }

        public double SliceRangeRightEndMm
        {
            get => _sliceRangeRightEndMm;
            set
            {
                if (SetProperty(ref _sliceRangeRightEndMm, value))
                {
                    SaveSetting(nameof(SliceRangeRightEndMm), value);
                }
            }
        }

        public double SliceRangeTopStartMm
        {
            get => _sliceRangeTopStartMm;
            set
            {
                if (SetProperty(ref _sliceRangeTopStartMm, value))
                {
                    SaveSetting(nameof(SliceRangeTopStartMm), value);
                }
            }
        }

        public double SliceRangeTopEndMm
        {
            get => _sliceRangeTopEndMm;
            set
            {
                if (SetProperty(ref _sliceRangeTopEndMm, value))
                {
                    SaveSetting(nameof(SliceRangeTopEndMm), value);
                }
            }
        }

        public int SliceExportDialogWaitTime
        {
            get => _sliceExportDialogWaitTime;
            set
            {
                if (SetProperty(ref _sliceExportDialogWaitTime, value))
                {
                    SaveSetting(nameof(SliceExportDialogWaitTime), value);
                }
            }
        }

        public int SliceExportWriteTimeoutMs
        {
            get => _sliceExportWriteTimeoutMs;
            set
            {
                if (SetProperty(ref _sliceExportWriteTimeoutMs, value))
                {
                    SaveSetting(nameof(SliceExportWriteTimeoutMs), value);
                }
            }
        }

        public int HistogramRoiX
        {
            get => _histogramRoiX;
            set
            {
                if (SetProperty(ref _histogramRoiX, value))
                {
                    SaveSetting(nameof(HistogramRoiX), value);
                    RefreshHistogramRoiDisplay();
                }
            }
        }

        public int HistogramRoiY
        {
            get => _histogramRoiY;
            set
            {
                if (SetProperty(ref _histogramRoiY, value))
                {
                    SaveSetting(nameof(HistogramRoiY), value);
                    RefreshHistogramRoiDisplay();
                }
            }
        }

        public int HistogramRoiWidth
        {
            get => _histogramRoiWidth;
            set
            {
                if (SetProperty(ref _histogramRoiWidth, value))
                {
                    SaveSetting(nameof(HistogramRoiWidth), value);
                    RefreshHistogramRoiDisplay();
                }
            }
        }

        public int HistogramRoiHeight
        {
            get => _histogramRoiHeight;
            set
            {
                if (SetProperty(ref _histogramRoiHeight, value))
                {
                    SaveSetting(nameof(HistogramRoiHeight), value);
                    RefreshHistogramRoiDisplay();
                }
            }
        }

        public string HistogramCaptureDir
        {
            get => _histogramCaptureDir;
            set
            {
                if (SetProperty(ref _histogramCaptureDir, value))
                {
                    SaveSetting(nameof(HistogramCaptureDir), value);
                }
            }
        }

        public string VisionCaptureDir
        {
            get => _visionCaptureDir;
            set
            {
                if (SetProperty(ref _visionCaptureDir, value))
                {
                    SaveSetting(nameof(VisionCaptureDir), value);
                }
            }
        }

        public string Vision2dVectorCaptureDir
        {
            get => _vision2dVectorCaptureDir;
            set
            {
                if (SetProperty(ref _vision2dVectorCaptureDir, value))
                {
                    SaveSetting(nameof(Vision2dVectorCaptureDir), value);
                }
            }
        }

        /// <summary>本地算法：直方图后、Pre321 前保存的默认视角 3D 原图目录。</summary>
        public string Vision3dInitialCaptureDir
        {
            get => _vision3dInitialCaptureDir;
            set
            {
                if (SetProperty(ref _vision3dInitialCaptureDir, value))
                {
                    SaveSetting(nameof(Vision3dInitialCaptureDir), value);
                }
            }
        }

        public string HistogramMaskColor
        {
            get => _histogramMaskColor;
            set
            {
                var normalized = NormalizeMaskColor(value);
                if (SetProperty(ref _histogramMaskColor, normalized))
                {
                    SaveSetting(nameof(HistogramMaskColor), normalized);
                    RefreshMaskColorBrush();
                }
            }
        }

        public int HistogramMaskTolerance
        {
            get => _histogramMaskTolerance;
            set
            {
                var clamped = Math.Clamp(value, 0, 64);
                if (SetProperty(ref _histogramMaskTolerance, clamped))
                {
                    SaveSetting(nameof(HistogramMaskTolerance), clamped);
                }
            }
        }

        public System.Windows.Media.Brush HistogramMaskColorBrush
        {
            get => _histogramMaskColorBrush;
            private set => SetProperty(ref _histogramMaskColorBrush, value);
        }

        public string HistogramRoiDisplayText
        {
            get => _histogramRoiDisplayText;
            private set => SetProperty(ref _histogramRoiDisplayText, value);
        }

        public int LutSegmentPoint1XPositive
        {
            get => _lutSegmentPoint1XPositive;
            set
            {
                if (SetProperty(ref _lutSegmentPoint1XPositive, value))
                {
                    SaveSetting(nameof(LutSegmentPoint1XPositive), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public int LutSegmentPoint1YPositive
        {
            get => _lutSegmentPoint1YPositive;
            set
            {
                if (SetProperty(ref _lutSegmentPoint1YPositive, value))
                {
                    SaveSetting(nameof(LutSegmentPoint1YPositive), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public int LutSegmentPoint2XPositive
        {
            get => _lutSegmentPoint2XPositive;
            set
            {
                if (SetProperty(ref _lutSegmentPoint2XPositive, value))
                {
                    SaveSetting(nameof(LutSegmentPoint2XPositive), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public int LutSegmentPoint2YPositive
        {
            get => _lutSegmentPoint2YPositive;
            set
            {
                if (SetProperty(ref _lutSegmentPoint2YPositive, value))
                {
                    SaveSetting(nameof(LutSegmentPoint2YPositive), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public string LutSegmentPointsDisplayTextPositive
        {
            get => _lutSegmentPointsDisplayTextPositive;
            private set => SetProperty(ref _lutSegmentPointsDisplayTextPositive, value);
        }

        public int LutSegmentPoint1XNegative
        {
            get => _lutSegmentPoint1XNegative;
            set
            {
                if (SetProperty(ref _lutSegmentPoint1XNegative, value))
                {
                    SaveSetting(nameof(LutSegmentPoint1XNegative), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public int LutSegmentPoint1YNegative
        {
            get => _lutSegmentPoint1YNegative;
            set
            {
                if (SetProperty(ref _lutSegmentPoint1YNegative, value))
                {
                    SaveSetting(nameof(LutSegmentPoint1YNegative), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public int LutSegmentPoint2XNegative
        {
            get => _lutSegmentPoint2XNegative;
            set
            {
                if (SetProperty(ref _lutSegmentPoint2XNegative, value))
                {
                    SaveSetting(nameof(LutSegmentPoint2XNegative), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public int LutSegmentPoint2YNegative
        {
            get => _lutSegmentPoint2YNegative;
            set
            {
                if (SetProperty(ref _lutSegmentPoint2YNegative, value))
                {
                    SaveSetting(nameof(LutSegmentPoint2YNegative), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public string LutSegmentPointsDisplayTextNegative
        {
            get => _lutSegmentPointsDisplayTextNegative;
            private set => SetProperty(ref _lutSegmentPointsDisplayTextNegative, value);
        }

        /// <summary>LUT 按峰谷 Y 扩展：左端点向左倍数（默认 2）</summary>
        public double LutExpandLeftYFactor
        {
            get => _lutExpandLeftYFactor;
            set
            {
                var clamped = Math.Max(0, value);
                if (SetProperty(ref _lutExpandLeftYFactor, clamped))
                {
                    _lutExpandLeftYFactorText = null;
                    SaveDoubleSetting(nameof(LutExpandLeftYFactor), clamped);
                    OnPropertyChanged(nameof(LutExpandLeftYFactorText));
                }
            }
        }

        /// <summary>
        /// 左×Y 界面编辑（字符串）：支持小数点与逗号，合法数值才写回 <see cref="LutExpandLeftYFactor"/>。
        /// </summary>
        public string LutExpandLeftYFactorText
        {
            get => _lutExpandLeftYFactorText
                ?? _lutExpandLeftYFactor.ToString("G", CultureInfo.InvariantCulture);
            set
            {
                if (SetProperty(ref _lutExpandLeftYFactorText, value))
                {
                    TryCommitLutExpandYFactorText(
                        value,
                        ref _lutExpandLeftYFactor,
                        nameof(LutExpandLeftYFactor));
                }
            }
        }

        /// <summary>LUT 按峰谷 Y 扩展：右端点向右倍数（默认 3）</summary>
        public double LutExpandRightYFactor
        {
            get => _lutExpandRightYFactor;
            set
            {
                var clamped = Math.Max(0, value);
                if (SetProperty(ref _lutExpandRightYFactor, clamped))
                {
                    _lutExpandRightYFactorText = null;
                    SaveDoubleSetting(nameof(LutExpandRightYFactor), clamped);
                    OnPropertyChanged(nameof(LutExpandRightYFactorText));
                }
            }
        }

        /// <summary>
        /// 右×Y 界面编辑（字符串）：支持小数点与逗号，合法数值才写回 <see cref="LutExpandRightYFactor"/>。
        /// </summary>
        public string LutExpandRightYFactorText
        {
            get => _lutExpandRightYFactorText
                ?? _lutExpandRightYFactor.ToString("G", CultureInfo.InvariantCulture);
            set
            {
                if (SetProperty(ref _lutExpandRightYFactorText, value))
                {
                    TryCommitLutExpandYFactorText(
                        value,
                        ref _lutExpandRightYFactor,
                        nameof(LutExpandRightYFactor));
                }
            }
        }

        /// <summary>尝试提交左右扩展因子文本；中间态（如「2.」）不强制回滚。</summary>
        private void TryCommitLutExpandYFactorText(
            string? text,
            ref double backingField,
            string valuePropName)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var raw = text.Trim().Replace(',', '.');
            if (raw is "." or "-" or "-." || raw.EndsWith('.'))
            {
                return;
            }

            if (!double.TryParse(
                    raw,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                || parsed < 0
                || double.IsNaN(parsed)
                || double.IsInfinity(parsed))
            {
                return;
            }

            if (Math.Abs(backingField - parsed) < 1e-15)
            {
                return;
            }

            backingField = parsed;
            SaveDoubleSetting(valuePropName, parsed);
            OnPropertyChanged(valuePropName);
        }

        public int Vision3dRoiX
        {
            get => _vision3dRoiX;
            set
            {
                if (SetProperty(ref _vision3dRoiX, value))
                {
                    SaveSetting(nameof(Vision3dRoiX), value);
                    RefreshVision3dRoiDisplay();
                }
            }
        }

        public int Vision3dRoiY
        {
            get => _vision3dRoiY;
            set
            {
                if (SetProperty(ref _vision3dRoiY, value))
                {
                    SaveSetting(nameof(Vision3dRoiY), value);
                    RefreshVision3dRoiDisplay();
                }
            }
        }

        public int Vision3dRoiWidth
        {
            get => _vision3dRoiWidth;
            set
            {
                if (SetProperty(ref _vision3dRoiWidth, value))
                {
                    SaveSetting(nameof(Vision3dRoiWidth), value);
                    RefreshVision3dRoiDisplay();
                }
            }
        }

        public int Vision3dRoiHeight
        {
            get => _vision3dRoiHeight;
            set
            {
                if (SetProperty(ref _vision3dRoiHeight, value))
                {
                    SaveSetting(nameof(Vision3dRoiHeight), value);
                    RefreshVision3dRoiDisplay();
                }
            }
        }

        public string Vision3dRoiDisplayText
        {
            get => _vision3dRoiDisplayText;
            private set => SetProperty(ref _vision3dRoiDisplayText, value);
        }

        public int VisionTopViewRoiX
        {
            get => _visionTopViewRoiX;
            set
            {
                if (SetProperty(ref _visionTopViewRoiX, value))
                {
                    SaveSetting(nameof(VisionTopViewRoiX), value);
                    RefreshVisionTopViewRoiDisplay();
                }
            }
        }

        public int VisionTopViewRoiY
        {
            get => _visionTopViewRoiY;
            set
            {
                if (SetProperty(ref _visionTopViewRoiY, value))
                {
                    SaveSetting(nameof(VisionTopViewRoiY), value);
                    RefreshVisionTopViewRoiDisplay();
                }
            }
        }

        public int VisionTopViewRoiWidth
        {
            get => _visionTopViewRoiWidth;
            set
            {
                if (SetProperty(ref _visionTopViewRoiWidth, value))
                {
                    SaveSetting(nameof(VisionTopViewRoiWidth), value);
                    RefreshVisionTopViewRoiDisplay();
                }
            }
        }

        public int VisionTopViewRoiHeight
        {
            get => _visionTopViewRoiHeight;
            set
            {
                if (SetProperty(ref _visionTopViewRoiHeight, value))
                {
                    SaveSetting(nameof(VisionTopViewRoiHeight), value);
                    RefreshVisionTopViewRoiDisplay();
                }
            }
        }

        public string VisionTopViewRoiDisplayText
        {
            get => _visionTopViewRoiDisplayText;
            private set => SetProperty(ref _visionTopViewRoiDisplayText, value);
        }

        /// <summary>勾选后在屏幕上高亮预览直方图 ROI 范围。</summary>
        public bool IsPreviewHistogramRoi
        {
            get => _isPreviewHistogramRoi;
            set => SetRoiPreviewFlag(ref _isPreviewHistogramRoi, nameof(IsPreviewHistogramRoi), value, RoiPreviewKind.Histogram);
        }

        /// <summary>勾选后在屏幕上高亮预览 3D ROI 范围。</summary>
        public bool IsPreviewVision3dRoi
        {
            get => _isPreviewVision3dRoi;
            set => SetRoiPreviewFlag(ref _isPreviewVision3dRoi, nameof(IsPreviewVision3dRoi), value, RoiPreviewKind.Vision3d);
        }

        /// <summary>勾选后在屏幕上高亮预览「上」视图 ROI 范围。</summary>
        public bool IsPreviewVisionTopViewRoi
        {
            get => _isPreviewVisionTopViewRoi;
            set => SetRoiPreviewFlag(ref _isPreviewVisionTopViewRoi, nameof(IsPreviewVisionTopViewRoi), value, RoiPreviewKind.VisionTopView);
        }

        private enum RoiPreviewKind
        {
            Histogram,
            Vision3d,
            VisionTopView,
        }

        public int Simple321ToolbarClickX
        {
            get => _simple321ToolbarClickX;
            set
            {
                if (SetProperty(ref _simple321ToolbarClickX, value))
                {
                    SaveSetting(nameof(Simple321ToolbarClickX), value);
                    RefreshSimple321ToolbarClickDisplay();
                }
            }
        }

        public int Simple321ToolbarClickY
        {
            get => _simple321ToolbarClickY;
            set
            {
                if (SetProperty(ref _simple321ToolbarClickY, value))
                {
                    SaveSetting(nameof(Simple321ToolbarClickY), value);
                    RefreshSimple321ToolbarClickDisplay();
                }
            }
        }

        public string Simple321ToolbarClickDisplayText
        {
            get => _simple321ToolbarClickDisplayText;
            private set => SetProperty(ref _simple321ToolbarClickDisplayText, value);
        }

        public int Simple321OffsetEditX
        {
            get => _simple321OffsetEditX;
            set
            {
                if (SetProperty(ref _simple321OffsetEditX, value))
                {
                    SaveSetting(nameof(Simple321OffsetEditX), value);
                    RefreshSimple321OffsetEditDisplay();
                }
            }
        }

        public int Simple321OffsetEditY
        {
            get => _simple321OffsetEditY;
            set
            {
                if (SetProperty(ref _simple321OffsetEditY, value))
                {
                    SaveSetting(nameof(Simple321OffsetEditY), value);
                    RefreshSimple321OffsetEditDisplay();
                }
            }
        }

        public string Simple321OffsetEditDisplayText
        {
            get => _simple321OffsetEditDisplayText;
            private set => SetProperty(ref _simple321OffsetEditDisplayText, value);
        }

        /// <summary>简单 3-2-1 找点算法：0=上位机本地，1=视觉 DLL。</summary>
        public int Simple321PointSource
        {
            get => _simple321PointSource;
            set
            {
                if (SetProperty(ref _simple321PointSource, value))
                {
                    SaveSetting(nameof(Simple321PointSource), value);
                    OnPropertyChanged(nameof(Simple321PointSourceDisplay));
                    OnPropertyChanged(nameof(IsSimple321LocalPointSource));
                }
            }
        }

        /// <summary>当前是否选择「上位机本地算法」（本地专属配置区显隐用）。</summary>
        public bool IsSimple321LocalPointSource =>
            _simple321PointSource == (int)Core.Models.Project.CTAutoSlicer.Simple321PointSource.Local;

        public IReadOnlyList<string> Simple321PointSourceOptions { get; } =
        [
            "上位机本地算法",
            "视觉 DLL 算法",
        ];

        public string Simple321PointSourceDisplay
        {
            get
            {
                var idx = Math.Clamp(_simple321PointSource, 0, Simple321PointSourceOptions.Count - 1);
                return Simple321PointSourceOptions[idx];
            }
            set
            {
                var idx = Simple321PointSourceOptions.ToList().IndexOf(value);
                if (idx < 0)
                {
                    return;
                }

                Simple321PointSource = idx;
                OnPropertyChanged();
            }
        }

        public int PostAlignCaptureButtonX
        {
            get => _postAlignCaptureButtonX;
            set
            {
                if (SetProperty(ref _postAlignCaptureButtonX, value))
                {
                    SaveSetting(nameof(PostAlignCaptureButtonX), value);
                    RefreshPostAlignCaptureButtonDisplay();
                }
            }
        }

        public int PostAlignCaptureButtonY
        {
            get => _postAlignCaptureButtonY;
            set
            {
                if (SetProperty(ref _postAlignCaptureButtonY, value))
                {
                    SaveSetting(nameof(PostAlignCaptureButtonY), value);
                    RefreshPostAlignCaptureButtonDisplay();
                }
            }
        }

        public string PostAlignCaptureButtonDisplayText
        {
            get => _postAlignCaptureButtonDisplayText;
            private set => SetProperty(ref _postAlignCaptureButtonDisplayText, value);
        }

        public int PostAlignCaptureButtonIndex
        {
            get => _postAlignCaptureButtonIndex;
            set
            {
                if (SetProperty(ref _postAlignCaptureButtonIndex, value))
                {
                    SaveSetting(nameof(PostAlignCaptureButtonIndex), value);
                }
            }
        }

        public int PostAlignDragStartX
        {
            get => _postAlignDragStartX;
            set
            {
                if (SetProperty(ref _postAlignDragStartX, value))
                {
                    SaveSetting(nameof(PostAlignDragStartX), value);
                    RefreshPostAlignDragDisplay();
                }
            }
        }

        public int PostAlignDragStartY
        {
            get => _postAlignDragStartY;
            set
            {
                if (SetProperty(ref _postAlignDragStartY, value))
                {
                    SaveSetting(nameof(PostAlignDragStartY), value);
                    RefreshPostAlignDragDisplay();
                }
            }
        }

        public int PostAlignDragEndX
        {
            get => _postAlignDragEndX;
            set
            {
                if (SetProperty(ref _postAlignDragEndX, value))
                {
                    SaveSetting(nameof(PostAlignDragEndX), value);
                    RefreshPostAlignDragDisplay();
                }
            }
        }

        public int PostAlignDragEndY
        {
            get => _postAlignDragEndY;
            set
            {
                if (SetProperty(ref _postAlignDragEndY, value))
                {
                    SaveSetting(nameof(PostAlignDragEndY), value);
                    RefreshPostAlignDragDisplay();
                }
            }
        }

        public string PostAlignDragDisplayText
        {
            get => _postAlignDragDisplayText;
            private set => SetProperty(ref _postAlignDragDisplayText, value);
        }

        public bool EnablePostAlignCaptureDragSteps
        {
            get => _enablePostAlignCaptureDragSteps;
            set
            {
                if (SetProperty(ref _enablePostAlignCaptureDragSteps, value))
                {
                    SaveSetting(nameof(EnablePostAlignCaptureDragSteps), value ? 1 : 0);
                }
            }
        }

        public bool EnableOfflineCtVisionAnnotation
        {
            get => _enableOfflineCtVisionAnnotation;
            set
            {
                if (SetProperty(ref _enableOfflineCtVisionAnnotation, value))
                {
                    SaveSetting(nameof(EnableOfflineCtVisionAnnotation), value ? 1 : 0);
                }
            }
        }

        public string OfflineCtModelPath
        {
            get => _offlineCtModelPath;
            set
            {
                if (SetProperty(ref _offlineCtModelPath, value))
                {
                    SaveSetting(nameof(OfflineCtModelPath), value ?? string.Empty);
                    OnPropertyChanged(nameof(OverviewOfflineCtModelPathDisplay));
                }
            }
        }

        /// <summary>总览 Hero：模型路径（空则显示默认部署路径）。</summary>
        public string OverviewOfflineCtModelPathDisplay =>
            string.IsNullOrWhiteSpace(OfflineCtModelPath)
                ? CTAutoSlicerSettings.GetDefaultOfflineCtModelPath()
                : OfflineCtModelPath;

        public bool OfflineCtIsRunCuda
        {
            get => _offlineCtIsRunCuda;
            set
            {
                if (SetProperty(ref _offlineCtIsRunCuda, value))
                {
                    SaveSetting(nameof(OfflineCtIsRunCuda), value ? 1 : 0);
                }
            }
        }

        public int OfflineCtBlackHeight
        {
            get => _offlineCtBlackHeight;
            set
            {
                if (SetProperty(ref _offlineCtBlackHeight, value))
                {
                    SaveSetting(nameof(OfflineCtBlackHeight), value);
                    OnPropertyChanged(nameof(OfflineCtBlackHeightHint));
                }
            }
        }

        /// <summary>界面提示：配置值为增强图打黑高度；原图按尺寸比例自动减半。</summary>
        public string OfflineCtBlackHeightHint =>
            $"增强图 {Math.Max(0, OfflineCtBlackHeight)} px；原图尺寸约一半 → {CTAutoSlicerSettings.ResolveOriginBottomBlackHeight(Math.Max(0, OfflineCtBlackHeight))} px；传 NJCT=0，本程序覆盖打黑；手动标注占右 1/4";

        /// <summary>
        /// 喂给视觉前内容上移像素：画布不变、底部黑填；0=不上移。
        /// </summary>
        public int OfflineCtContentShiftUpPx
        {
            get => _offlineCtContentShiftUpPx;
            set
            {
                var clamped = Math.Max(0, value);
                if (SetProperty(ref _offlineCtContentShiftUpPx, clamped))
                {
                    SaveSetting(nameof(OfflineCtContentShiftUpPx), clamped);
                }
            }
        }

        /// <summary>开启后对 L1~L5 做 mm 范围判断：合格绿 / 超差红。</summary>
        public bool EnableOfflineCtLRangeCheck
        {
            get => _enableOfflineCtLRangeCheck;
            set
            {
                if (SetProperty(ref _enableOfflineCtLRangeCheck, value))
                {
                    SaveSetting(nameof(EnableOfflineCtLRangeCheck), value ? 1 : 0);
                }
            }
        }

        public double OfflineCtL1MinMm
        {
            get => _offlineCtL1MinMm;
            set => SetOfflineCtLRangeMm(ref _offlineCtL1MinMm, ref _offlineCtL1MinMmText, value, nameof(OfflineCtL1MinMm), nameof(OfflineCtL1MinMmText));
        }

        public string OfflineCtL1MinMmText
        {
            get => GetDecimalEditText(_offlineCtL1MinMmText, _offlineCtL1MinMm);
            set => SetDecimalEditText(ref _offlineCtL1MinMmText, ref _offlineCtL1MinMm, value, nameof(OfflineCtL1MinMm), nameof(OfflineCtL1MinMmText));
        }

        public double OfflineCtL1MaxMm
        {
            get => _offlineCtL1MaxMm;
            set => SetOfflineCtLRangeMm(ref _offlineCtL1MaxMm, ref _offlineCtL1MaxMmText, value, nameof(OfflineCtL1MaxMm), nameof(OfflineCtL1MaxMmText));
        }

        public string OfflineCtL1MaxMmText
        {
            get => GetDecimalEditText(_offlineCtL1MaxMmText, _offlineCtL1MaxMm);
            set => SetDecimalEditText(ref _offlineCtL1MaxMmText, ref _offlineCtL1MaxMm, value, nameof(OfflineCtL1MaxMm), nameof(OfflineCtL1MaxMmText));
        }

        public double OfflineCtL2MinMm
        {
            get => _offlineCtL2MinMm;
            set => SetOfflineCtLRangeMm(ref _offlineCtL2MinMm, ref _offlineCtL2MinMmText, value, nameof(OfflineCtL2MinMm), nameof(OfflineCtL2MinMmText));
        }

        public string OfflineCtL2MinMmText
        {
            get => GetDecimalEditText(_offlineCtL2MinMmText, _offlineCtL2MinMm);
            set => SetDecimalEditText(ref _offlineCtL2MinMmText, ref _offlineCtL2MinMm, value, nameof(OfflineCtL2MinMm), nameof(OfflineCtL2MinMmText));
        }

        public double OfflineCtL2MaxMm
        {
            get => _offlineCtL2MaxMm;
            set => SetOfflineCtLRangeMm(ref _offlineCtL2MaxMm, ref _offlineCtL2MaxMmText, value, nameof(OfflineCtL2MaxMm), nameof(OfflineCtL2MaxMmText));
        }

        public string OfflineCtL2MaxMmText
        {
            get => GetDecimalEditText(_offlineCtL2MaxMmText, _offlineCtL2MaxMm);
            set => SetDecimalEditText(ref _offlineCtL2MaxMmText, ref _offlineCtL2MaxMm, value, nameof(OfflineCtL2MaxMm), nameof(OfflineCtL2MaxMmText));
        }

        public double OfflineCtL3MinMm
        {
            get => _offlineCtL3MinMm;
            set => SetOfflineCtLRangeMm(ref _offlineCtL3MinMm, ref _offlineCtL3MinMmText, value, nameof(OfflineCtL3MinMm), nameof(OfflineCtL3MinMmText));
        }

        public string OfflineCtL3MinMmText
        {
            get => GetDecimalEditText(_offlineCtL3MinMmText, _offlineCtL3MinMm);
            set => SetDecimalEditText(ref _offlineCtL3MinMmText, ref _offlineCtL3MinMm, value, nameof(OfflineCtL3MinMm), nameof(OfflineCtL3MinMmText));
        }

        public double OfflineCtL3MaxMm
        {
            get => _offlineCtL3MaxMm;
            set => SetOfflineCtLRangeMm(ref _offlineCtL3MaxMm, ref _offlineCtL3MaxMmText, value, nameof(OfflineCtL3MaxMm), nameof(OfflineCtL3MaxMmText));
        }

        public string OfflineCtL3MaxMmText
        {
            get => GetDecimalEditText(_offlineCtL3MaxMmText, _offlineCtL3MaxMm);
            set => SetDecimalEditText(ref _offlineCtL3MaxMmText, ref _offlineCtL3MaxMm, value, nameof(OfflineCtL3MaxMm), nameof(OfflineCtL3MaxMmText));
        }

        public double OfflineCtL4MinMm
        {
            get => _offlineCtL4MinMm;
            set => SetOfflineCtLRangeMm(ref _offlineCtL4MinMm, ref _offlineCtL4MinMmText, value, nameof(OfflineCtL4MinMm), nameof(OfflineCtL4MinMmText));
        }

        public string OfflineCtL4MinMmText
        {
            get => GetDecimalEditText(_offlineCtL4MinMmText, _offlineCtL4MinMm);
            set => SetDecimalEditText(ref _offlineCtL4MinMmText, ref _offlineCtL4MinMm, value, nameof(OfflineCtL4MinMm), nameof(OfflineCtL4MinMmText));
        }

        public double OfflineCtL4MaxMm
        {
            get => _offlineCtL4MaxMm;
            set => SetOfflineCtLRangeMm(ref _offlineCtL4MaxMm, ref _offlineCtL4MaxMmText, value, nameof(OfflineCtL4MaxMm), nameof(OfflineCtL4MaxMmText));
        }

        public string OfflineCtL4MaxMmText
        {
            get => GetDecimalEditText(_offlineCtL4MaxMmText, _offlineCtL4MaxMm);
            set => SetDecimalEditText(ref _offlineCtL4MaxMmText, ref _offlineCtL4MaxMm, value, nameof(OfflineCtL4MaxMm), nameof(OfflineCtL4MaxMmText));
        }

        public double OfflineCtL5MinMm
        {
            get => _offlineCtL5MinMm;
            set => SetOfflineCtLRangeMm(ref _offlineCtL5MinMm, ref _offlineCtL5MinMmText, value, nameof(OfflineCtL5MinMm), nameof(OfflineCtL5MinMmText));
        }

        public string OfflineCtL5MinMmText
        {
            get => GetDecimalEditText(_offlineCtL5MinMmText, _offlineCtL5MinMm);
            set => SetDecimalEditText(ref _offlineCtL5MinMmText, ref _offlineCtL5MinMm, value, nameof(OfflineCtL5MinMm), nameof(OfflineCtL5MinMmText));
        }

        public double OfflineCtL5MaxMm
        {
            get => _offlineCtL5MaxMm;
            set => SetOfflineCtLRangeMm(ref _offlineCtL5MaxMm, ref _offlineCtL5MaxMmText, value, nameof(OfflineCtL5MaxMm), nameof(OfflineCtL5MaxMmText));
        }

        public string OfflineCtL5MaxMmText
        {
            get => GetDecimalEditText(_offlineCtL5MaxMmText, _offlineCtL5MaxMm);
            set => SetDecimalEditText(ref _offlineCtL5MaxMmText, ref _offlineCtL5MaxMm, value, nameof(OfflineCtL5MaxMm), nameof(OfflineCtL5MaxMmText));
        }

        private static string GetDecimalEditText(string? editBuf, double value) =>
            editBuf ?? value.ToString("G", CultureInfo.InvariantCulture);

        private void SetOfflineCtLRangeMm(
            ref double field,
            ref string? editBuf,
            double value,
            string valuePropName,
            string textPropName)
        {
            if (!SetProperty(ref field, value, valuePropName))
            {
                return;
            }

            editBuf = null;
            SaveDoubleSetting(valuePropName, value);
            OnPropertyChanged(textPropName);
        }

        /// <summary>
        /// 小数文本框提交：允许「1.」「-」等中间态；合法有限数值才写回并持久化。
        /// 范围限允许 0 与负数。
        /// </summary>
        private void SetDecimalEditText(
            ref string? editBuf,
            ref double field,
            string? text,
            string valuePropName,
            string textPropName)
        {
            if (!SetProperty(ref editBuf, text, textPropName))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var raw = text.Trim().Replace(',', '.');
            if (raw is "." or "-" or "-." || raw.EndsWith('.'))
            {
                return;
            }

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                || double.IsNaN(parsed)
                || double.IsInfinity(parsed))
            {
                return;
            }

            if (Math.Abs(field - parsed) < 1e-15)
            {
                return;
            }

            field = parsed;
            SaveDoubleSetting(valuePropName, parsed);
            OnPropertyChanged(valuePropName);
        }

        /// <summary>
        /// AI 标注选用「上」视图第几张（1～100）。
        /// 输入 50 = 第 50 张；实际查找文件名 = 前缀0 + 50 → 050.jpg；默认 50。
        /// </summary>
        public int OfflineCtTopViewSliceIndex
        {
            get => _offlineCtTopViewSliceIndex;
            set
            {
                var clamped = Math.Clamp(
                    value,
                    CTAutoSlicerSettings.MinOfflineCtTopViewSliceIndex,
                    CTAutoSlicerSettings.MaxOfflineCtTopViewSliceIndex);
                if (SetProperty(ref _offlineCtTopViewSliceIndex, clamped))
                {
                    SaveSetting(nameof(OfflineCtTopViewSliceIndex), clamped);
                }
                else if (value != clamped)
                {
                    // 输入越界被钳制时强制刷新绑定，避免文本框仍显示非法值
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 像素精度（μm/px）：一个像素的物理尺寸，供后续尺寸测量换算。
        /// </summary>
        public double OfflineCtPixelSizeUm
        {
            get => _offlineCtPixelSizeUm;
            set
            {
                var normalized = value > 0 ? value : CTAutoSlicerSettings.DefaultOfflineCtPixelSizeUm;
                if (SetProperty(ref _offlineCtPixelSizeUm, normalized))
                {
                    _offlineCtPixelSizeUmText = null;
                    SaveDoubleSetting(nameof(OfflineCtPixelSizeUm), normalized);
                    OnPropertyChanged(nameof(OfflineCtPixelSizeUmText));
                    OnPropertyChanged(nameof(OfflineCtPixelSizeMmPerPx));
                    OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaUm));
                    OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaMm));
                }
            }
        }

        /// <summary>
        /// 像素精度界面编辑（字符串）：支持小数点与逗号输入，合法数值才写回 <see cref="OfflineCtPixelSizeUm"/>。
        /// </summary>
        public string OfflineCtPixelSizeUmText
        {
            get => _offlineCtPixelSizeUmText
                ?? _offlineCtPixelSizeUm.ToString("G", CultureInfo.InvariantCulture);
            set
            {
                if (SetProperty(ref _offlineCtPixelSizeUmText, value))
                {
                    TryCommitOfflineCtPixelSizeUmText(value);
                }
            }
        }

        /// <summary>尝试把文本框内容提交为像素精度；中间态（如「1.」）不强制回滚。</summary>
        private void TryCommitOfflineCtPixelSizeUmText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var raw = text.Trim().Replace(',', '.');
            // 允许正在输入小数点 / 负号前缀
            if (raw is "." or "-" or "-." || raw.EndsWith('.'))
            {
                return;
            }

            if (!double.TryParse(
                    raw,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                || parsed <= 0
                || double.IsNaN(parsed)
                || double.IsInfinity(parsed))
            {
                return;
            }

            if (Math.Abs(_offlineCtPixelSizeUm - parsed) < 1e-15)
            {
                return;
            }

            _offlineCtPixelSizeUm = parsed;
            SaveDoubleSetting(nameof(OfflineCtPixelSizeUm), parsed);
            OnPropertyChanged(nameof(OfflineCtPixelSizeUm));
            OnPropertyChanged(nameof(OfflineCtPixelSizeMmPerPx));
            OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaUm));
            OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaMm));
        }

        /// <summary>像素精度换算为 mm/px（1 mm = 1000 μm）。</summary>
        public double OfflineCtPixelSizeMmPerPx => OfflineCtPixelSizeUm / 1000.0;

        /// <summary>界面：物理尺寸(μm) 公式展示用。</summary>
        public string OfflineCtPixelSizeFormulaUm =>
            $"L_μm = N_px × {OfflineCtPixelSizeUm:G} μm/px";

        /// <summary>界面：物理尺寸(mm) 公式展示用。</summary>
        public string OfflineCtPixelSizeFormulaMm =>
            $"L_mm = N_px × {OfflineCtPixelSizeUm:G} / 1000 = N_px × {OfflineCtPixelSizeMmPerPx:G} mm/px";

        /// <summary>0=增强图 1=原图 2=两者</summary>
        public int OfflineCtResultImageMode
        {
            get => _offlineCtResultImageMode;
            set
            {
                if (SetProperty(ref _offlineCtResultImageMode, value))
                {
                    SaveSetting(nameof(OfflineCtResultImageMode), value);
                    OnPropertyChanged(nameof(OfflineCtResultImageModeDisplay));
                }
            }
        }

        public IReadOnlyList<string> OfflineCtResultImageModeOptions { get; } =
        [
            "增强图",
            "原图",
            "增强图和原图",
        ];

        public string OfflineCtResultImageModeDisplay
        {
            get
            {
                var idx = Math.Clamp(_offlineCtResultImageMode, 0, OfflineCtResultImageModeOptions.Count - 1);
                return OfflineCtResultImageModeOptions[idx];
            }
            set
            {
                var idx = OfflineCtResultImageModeOptions.ToList().IndexOf(value);
                if (idx < 0)
                {
                    return;
                }

                OfflineCtResultImageMode = idx;
                OnPropertyChanged();
            }
        }

        public bool OfflineCtExportJson
        {
            get => _offlineCtExportJson;
            set
            {
                if (SetProperty(ref _offlineCtExportJson, value))
                {
                    SaveSetting(nameof(OfflineCtExportJson), value ? 1 : 0);
                }
            }
        }

        public bool OfflineCtExportCsv
        {
            get => _offlineCtExportCsv;
            set
            {
                if (SetProperty(ref _offlineCtExportCsv, value))
                {
                    SaveSetting(nameof(OfflineCtExportCsv), value ? 1 : 0);
                }
            }
        }

        /// <summary>历史配置：现主流程用临时送检图，本开关不再影响主流程。</summary>
        public bool OfflineCtExportInputForNjct
        {
            get => _offlineCtExportInputForNjct;
            set
            {
                if (SetProperty(ref _offlineCtExportInputForNjct, value))
                {
                    SaveSetting(nameof(OfflineCtExportInputForNjct), value ? 1 : 0);
                }
            }
        }

        /// <summary>历史配置：现按宽&gt;高自动竖图，本开关不再影响主流程。</summary>
        public bool OfflineCtRotateTopViewCw90
        {
            get => _offlineCtRotateTopViewCw90;
            set
            {
                if (SetProperty(ref _offlineCtRotateTopViewCw90, value))
                {
                    SaveSetting(nameof(OfflineCtRotateTopViewCw90), value ? 1 : 0);
                }
            }
        }

        /// <summary>调用 NJCT 前是否按 ROI 亮度条件提亮。</summary>
        public bool EnableOfflineCtPreBrighten
        {
            get => _enableOfflineCtPreBrighten;
            set
            {
                if (SetProperty(ref _enableOfflineCtPreBrighten, value))
                {
                    SaveSetting(nameof(EnableOfflineCtPreBrighten), value ? 1 : 0);
                }
            }
        }

        /// <summary>预提亮触发：ROI 均值阈值。</summary>
        public double OfflineCtPreBrightenMeanTrigger
        {
            get => _offlineCtPreBrightenMeanTrigger;
            set
            {
                if (SetProperty(ref _offlineCtPreBrightenMeanTrigger, value))
                {
                    SaveSetting(nameof(OfflineCtPreBrightenMeanTrigger), value);
                }
            }
        }

        /// <summary>预提亮触发：ROI P50 阈值。</summary>
        public double OfflineCtPreBrightenP50Trigger
        {
            get => _offlineCtPreBrightenP50Trigger;
            set
            {
                if (SetProperty(ref _offlineCtPreBrightenP50Trigger, value))
                {
                    SaveSetting(nameof(OfflineCtPreBrightenP50Trigger), value);
                }
            }
        }

        /// <summary>预提亮目标 ROI 均值。</summary>
        public double OfflineCtPreBrightenTargetMean
        {
            get => _offlineCtPreBrightenTargetMean;
            set
            {
                if (SetProperty(ref _offlineCtPreBrightenTargetMean, value))
                {
                    SaveSetting(nameof(OfflineCtPreBrightenTargetMean), value);
                }
            }
        }

        /// <summary>AI 标注页：独立标注输入图片路径。</summary>
        public string OfflineCtManualInputImagePath
        {
            get => _offlineCtManualInputImagePath;
            set
            {
                if (SetProperty(ref _offlineCtManualInputImagePath, value ?? string.Empty))
                {
                    SaveSetting(nameof(OfflineCtManualInputImagePath), value ?? string.Empty);
                }
            }
        }

        /// <summary>AI 标注页：独立标注输出目录；空则输出到输入图所在目录。</summary>
        public string OfflineCtManualOutputFolder
        {
            get => _offlineCtManualOutputFolder;
            set
            {
                if (SetProperty(ref _offlineCtManualOutputFolder, value ?? string.Empty))
                {
                    SaveSetting(nameof(OfflineCtManualOutputFolder), value ?? string.Empty);
                }
            }
        }

        /// <summary>NJCT DLL 直调批量：输入图片文件夹。</summary>
        public string OfflineCtDllTestInputFolder
        {
            get => _offlineCtDllTestInputFolder;
            set
            {
                if (SetProperty(ref _offlineCtDllTestInputFolder, value ?? string.Empty))
                {
                    SaveSetting(nameof(OfflineCtDllTestInputFolder), value ?? string.Empty);
                }
            }
        }

        /// <summary>NJCT DLL 直调批量：输出目录。</summary>
        public string OfflineCtDllTestOutputFolder
        {
            get => _offlineCtDllTestOutputFolder;
            set
            {
                if (SetProperty(ref _offlineCtDllTestOutputFolder, value ?? string.Empty))
                {
                    SaveSetting(nameof(OfflineCtDllTestOutputFolder), value ?? string.Empty);
                }
            }
        }

        /// <summary>
        /// NJCT DLL 直调批量：传给 ModelAction 的 BlackHeight（与主流程 OfflineCtBlackHeight 无关）。
        /// </summary>
        public int OfflineCtDllTestBlackHeight
        {
            get => _offlineCtDllTestBlackHeight;
            set
            {
                var clamped = Math.Max(0, value);
                if (SetProperty(ref _offlineCtDllTestBlackHeight, clamped))
                {
                    SaveSetting(nameof(OfflineCtDllTestBlackHeight), clamped);
                    OnPropertyChanged(nameof(OfflineCtDllTestBlackHeightHint));
                }
            }
        }

        /// <summary>DLL 直调：原图尺寸约一半，BlackHeight 同步 ÷2。</summary>
        public string OfflineCtDllTestBlackHeightHint =>
            $"增强图传 {Math.Max(0, OfflineCtDllTestBlackHeight)} px；原图尺寸约一半 → {CTAutoSlicerSettings.ResolveOriginBottomBlackHeight(Math.Max(0, OfflineCtDllTestBlackHeight))} px（DLL 直调专用，无上位机叠加）";

        /// <summary>CTAutoCutImg 离线测：3D侧视图输入文件夹（GetSectionReferencePoints 批量）。</summary>
        public string CutImgDllTestSectionFolder
        {
            get => _cutImgDllTestSectionFolder;
            set
            {
                if (SetProperty(ref _cutImgDllTestSectionFolder, value ?? string.Empty))
                {
                    SaveSetting(nameof(CutImgDllTestSectionFolder), value ?? string.Empty);
                }
            }
        }

        /// <summary>CTAutoCutImg 离线测：2D侧视图输入文件夹（GetObjectTopBottomPoints 批量）。</summary>
        public string CutImgDllTestVectorFolder
        {
            get => _cutImgDllTestVectorFolder;
            set
            {
                if (SetProperty(ref _cutImgDllTestVectorFolder, value ?? string.Empty))
                {
                    SaveSetting(nameof(CutImgDllTestVectorFolder), value ?? string.Empty);
                }
            }
        }

        /// <summary>CTAutoCutImg 离线测：标注结果输出目录。</summary>
        public string CutImgDllTestOutputDir
        {
            get => _cutImgDllTestOutputDir;
            set
            {
                if (SetProperty(ref _cutImgDllTestOutputDir, value ?? string.Empty))
                {
                    SaveSetting(nameof(CutImgDllTestOutputDir), value ?? string.Empty);
                }
            }
        }

        /// <summary>单步测试：截取 3D 标定图保存目录。</summary>
        public string VisionLearn3dSampleDir
        {
            get => _visionLearn3dSampleDir;
            set
            {
                if (SetProperty(ref _visionLearn3dSampleDir, value ?? string.Empty))
                {
                    SaveSetting(nameof(VisionLearn3dSampleDir), value ?? string.Empty);
                }
            }
        }

        /// <summary>单步测试：截取上视图标定图保存目录。</summary>
        public string VisionLearnTopViewSampleDir
        {
            get => _visionLearnTopViewSampleDir;
            set
            {
                if (SetProperty(ref _visionLearnTopViewSampleDir, value ?? string.Empty))
                {
                    SaveSetting(nameof(VisionLearnTopViewSampleDir), value ?? string.Empty);
                }
            }
        }

        public int PostAlignClickWaitMs
        {
            get => _postAlignClickWaitMs;
            set
            {
                if (SetProperty(ref _postAlignClickWaitMs, value))
                {
                    SaveSetting(nameof(PostAlignClickWaitMs), value);
                }
            }
        }

        public int PostAlignDragStepDelayMs
        {
            get => _postAlignDragStepDelayMs;
            set
            {
                if (SetProperty(ref _postAlignDragStepDelayMs, value))
                {
                    SaveSetting(nameof(PostAlignDragStepDelayMs), value);
                }
            }
        }

        public int Pre321ViewClickX
        {
            get => _pre321ViewClickX;
            set
            {
                if (SetProperty(ref _pre321ViewClickX, value))
                {
                    SaveSetting(nameof(Pre321ViewClickX), value);
                    RefreshPre321ViewClickDisplay();
                }
            }
        }

        public int Pre321ViewClickY
        {
            get => _pre321ViewClickY;
            set
            {
                if (SetProperty(ref _pre321ViewClickY, value))
                {
                    SaveSetting(nameof(Pre321ViewClickY), value);
                    RefreshPre321ViewClickDisplay();
                }
            }
        }

        public string Pre321ViewClickDisplayText
        {
            get => _pre321ViewClickDisplayText;
            private set => SetProperty(ref _pre321ViewClickDisplayText, value);
        }

        public int Pre321ViewDragStartX
        {
            get => _pre321ViewDragStartX;
            set
            {
                if (SetProperty(ref _pre321ViewDragStartX, value))
                {
                    SaveSetting(nameof(Pre321ViewDragStartX), value);
                    RefreshPre321ViewDragDisplay();
                }
            }
        }

        public int Pre321ViewDragStartY
        {
            get => _pre321ViewDragStartY;
            set
            {
                if (SetProperty(ref _pre321ViewDragStartY, value))
                {
                    SaveSetting(nameof(Pre321ViewDragStartY), value);
                    RefreshPre321ViewDragDisplay();
                }
            }
        }

        public int Pre321ViewDragEndX
        {
            get => _pre321ViewDragEndX;
            set
            {
                if (SetProperty(ref _pre321ViewDragEndX, value))
                {
                    SaveSetting(nameof(Pre321ViewDragEndX), value);
                    RefreshPre321ViewDragDisplay();
                }
            }
        }

        public int Pre321ViewDragEndY
        {
            get => _pre321ViewDragEndY;
            set
            {
                if (SetProperty(ref _pre321ViewDragEndY, value))
                {
                    SaveSetting(nameof(Pre321ViewDragEndY), value);
                    RefreshPre321ViewDragDisplay();
                }
            }
        }

        public string Pre321ViewDragDisplayText
        {
            get => _pre321ViewDragDisplayText;
            private set => SetProperty(ref _pre321ViewDragDisplayText, value);
        }

        /// <summary>找点前目标视角参考图（效果图）。</summary>
        public ImageSource? Pre321ViewReferenceImage
        {
            get
            {
                try
                {
                    var path = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Assets",
                        "images",
                        "pre321_view_ref.png");
                    if (!File.Exists(path))
                    {
                        return null;
                    }

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch
                {
                    return null;
                }
            }
        }

        public int Pre321ViewClickWaitMs
        {
            get => _pre321ViewClickWaitMs;
            set
            {
                if (SetProperty(ref _pre321ViewClickWaitMs, value))
                {
                    SaveSetting(nameof(Pre321ViewClickWaitMs), value);
                }
            }
        }

        public int Pre321ViewDragStepDelayMs
        {
            get => _pre321ViewDragStepDelayMs;
            set
            {
                if (SetProperty(ref _pre321ViewDragStepDelayMs, value))
                {
                    SaveSetting(nameof(Pre321ViewDragStepDelayMs), value);
                }
            }
        }

        /// <summary>下次进入本项目时是否沿用已保存的主窗尺寸。</summary>
        public bool MainWindowRememberSize
        {
            get => _mainWindowRememberSize;
            set
            {
                if (SetProperty(ref _mainWindowRememberSize, value))
                {
                    SaveSetting(nameof(MainWindowRememberSize), value ? 1 : 0);
                    RefreshMainWindowSizeDisplay();
                }
            }
        }

        public int MainWindowWidth
        {
            get => _mainWindowWidth;
            set
            {
                if (SetProperty(ref _mainWindowWidth, value))
                {
                    SaveSetting(nameof(MainWindowWidth), value);
                    RefreshMainWindowSizeDisplay();
                }
            }
        }

        public int MainWindowHeight
        {
            get => _mainWindowHeight;
            set
            {
                if (SetProperty(ref _mainWindowHeight, value))
                {
                    SaveSetting(nameof(MainWindowHeight), value);
                    RefreshMainWindowSizeDisplay();
                }
            }
        }

        public int MainWindowLeft
        {
            get => _mainWindowLeft;
            set
            {
                if (SetProperty(ref _mainWindowLeft, value))
                {
                    SaveSetting(nameof(MainWindowLeft), value);
                    RefreshMainWindowSizeDisplay();
                }
            }
        }

        public int MainWindowTop
        {
            get => _mainWindowTop;
            set
            {
                if (SetProperty(ref _mainWindowTop, value))
                {
                    SaveSetting(nameof(MainWindowTop), value);
                    RefreshMainWindowSizeDisplay();
                }
            }
        }

        public string MainWindowSizeDisplayText
        {
            get => _mainWindowSizeDisplayText;
            private set => SetProperty(ref _mainWindowSizeDisplayText, value);
        }

        public ICommand OpenVGAppPathCommand { get; }
        public ICommand OpenCTFileFolderCommand { get; }
        public ICommand OpenSliceExportFolderCommand { get; }
        public ICommand OpenTopViewHandoffFolderCommand { get; }
        public ICommand OpenManualMeasureFolderCommand { get; }
        public ICommand ClearSliceExportFolderCommand { get; }
        public ICommand RefreshLatestAiAnnotationPreviewCommand { get; }
        public ICommand RefreshStatsDemoCommand { get; }

        /// <summary>二次手动标注页：最新 AI 标注结果图预览。</summary>
        public ImageSource? LatestAiAnnotationPreview
        {
            get => _latestAiAnnotationPreview;
            private set => SetProperty(ref _latestAiAnnotationPreview, value);
        }

        /// <summary>最新 AI 结果图完整路径。</summary>
        public string LatestAiAnnotationPreviewPath
        {
            get => _latestAiAnnotationPreviewPath;
            private set => SetProperty(ref _latestAiAnnotationPreviewPath, value);
        }

        /// <summary>预览区说明（路径/时间或空状态）。</summary>
        public string LatestAiAnnotationPreviewInfo
        {
            get => _latestAiAnnotationPreviewInfo;
            private set => SetProperty(ref _latestAiAnnotationPreviewInfo, value);
        }

        /// <summary>从 annotation.json 解析的 AI 尺寸汇总（标注界面再显示一遍）。</summary>
        public string LatestAiAnnotationSummaryText
        {
            get => _latestAiAnnotationSummaryText;
            private set => SetProperty(ref _latestAiAnnotationSummaryText, value);
        }

        public bool HasLatestAiAnnotationSummary =>
            !string.IsNullOrWhiteSpace(LatestAiAnnotationSummaryText);

        public bool HasLatestAiAnnotationPreview => LatestAiAnnotationPreview is not null;

        /// <summary>当日检测总数（Demo）。</summary>
        public int StatsTodayTotal
        {
            get => _statsTodayTotal;
            private set
            {
                if (SetProperty(ref _statsTodayTotal, value))
                {
                    OnPropertyChanged(nameof(StatsOkRateText));
                    OnPropertyChanged(nameof(StatsNgRateText));
                }
            }
        }

        /// <summary>当日 OK 数（Demo）。</summary>
        public int StatsTodayOk
        {
            get => _statsTodayOk;
            private set
            {
                if (SetProperty(ref _statsTodayOk, value))
                {
                    OnPropertyChanged(nameof(StatsOkRateText));
                }
            }
        }

        /// <summary>当日 NG 数（Demo）。</summary>
        public int StatsTodayNg
        {
            get => _statsTodayNg;
            private set
            {
                if (SetProperty(ref _statsTodayNg, value))
                {
                    OnPropertyChanged(nameof(StatsNgRateText));
                }
            }
        }

        public string StatsOkRateText =>
            StatsTodayTotal <= 0
                ? "—"
                : $"{100.0 * StatsTodayOk / StatsTodayTotal:F1}%";

        public string StatsNgRateText =>
            StatsTodayTotal <= 0
                ? "—"
                : $"{100.0 * StatsTodayNg / StatsTodayTotal:F1}%";

        public string StatsDemoDateText
        {
            get => _statsDemoDateText;
            private set => SetProperty(ref _statsDemoDateText, value);
        }

        public string StatsDemoNote
        {
            get => _statsDemoNote;
            private set => SetProperty(ref _statsDemoNote, value);
        }

        /// <summary>OK/NG 占比饼图（OxyPlot）。</summary>
        public PlotModel? StatsOkNgPieModel
        {
            get => _statsOkNgPieModel;
            private set => SetProperty(ref _statsOkNgPieModel, value);
        }

        /// <summary>当日各时段产量柱状图（OxyPlot）。</summary>
        public PlotModel? StatsHourlyBarModel
        {
            get => _statsHourlyBarModel;
            private set => SetProperty(ref _statsHourlyBarModel, value);
        }

        /// <summary>预览原图像素宽（供 Viewbox 按原图比例缩放）。</summary>
        public double LatestAiAnnotationPreviewPixelWidth
        {
            get => _latestAiAnnotationPreviewPixelWidth;
            private set => SetProperty(ref _latestAiAnnotationPreviewPixelWidth, value);
        }

        /// <summary>预览原图像素高（供 Viewbox 按原图比例缩放）。</summary>
        public double LatestAiAnnotationPreviewPixelHeight
        {
            get => _latestAiAnnotationPreviewPixelHeight;
            private set => SetProperty(ref _latestAiAnnotationPreviewPixelHeight, value);
        }

        public ICommand OpenHistogramCaptureDirCommand { get; }
        public ICommand OpenVisionCaptureDirCommand { get; }
        public ICommand OpenVision2dVectorCaptureDirCommand { get; }
        public ICommand OpenVision3dInitialCaptureDirCommand { get; }
        public ICommand CalibrateHistogramRoiCommand { get; }
        public ICommand CalibrateLutSegmentPointsCommand { get; }
        public ICommand CalibrateVision3dRoiCommand { get; }
        public ICommand CalibrateVisionTopViewRoiCommand { get; }
        public ICommand CalibrateSimple321ToolbarClickCommand { get; }
        public ICommand CalibrateSimple321OffsetEditCommand { get; }
        public ICommand CalibratePostAlignCaptureButtonCommand { get; }
        public ICommand CalibratePostAlignDragArrowCommand { get; }
        public ICommand CalibratePre321ViewClickCommand { get; }
        public ICommand CalibratePre321ViewDragArrowCommand { get; }
        public ICommand PickHistogramMaskColorCommand { get; }
        public ICommand TestRenderingPresetCommand { get; }
        public ICommand TestThickPlateCommand { get; }
        public ICommand TestSliceExportCommand { get; }
        public ICommand TestHistogramGrayScaleCommand { get; }
        public ICommand TestSimple321AlignmentCommand { get; }
        public ICommand TestPostAlignCaptureDragCommand { get; }
        public ICommand TestPre321ViewPrepCommand { get; }
        public ICommand TestOfflineCtVisionAnnotationCommand { get; }
        public ICommand BrowseCutImgDllTestSectionFolderCommand { get; }
        public ICommand BrowseCutImgDllTestVectorFolderCommand { get; }
        public ICommand BrowseCutImgDllTestOutputDirCommand { get; }
        public ICommand TestCutImgGetSectionReferencePointsCommand { get; }
        public ICommand TestCutImgGetObjectTopBottomPointsCommand { get; }
        public ICommand BrowseOfflineCtModelPathCommand { get; }
        public ICommand ResetOfflineCtModelPathCommand { get; }
        public ICommand BrowseOfflineCtManualInputImageCommand { get; }
        public ICommand BrowseOfflineCtManualOutputFolderCommand { get; }
        public ICommand ClearOfflineCtManualOutputFolderCommand { get; }
        public ICommand RunOfflineCtManualAnnotationCommand { get; }
        public ICommand BrowseOfflineCtDllTestInputFolderCommand { get; }
        public ICommand BrowseOfflineCtDllTestOutputFolderCommand { get; }
        public ICommand RunOfflineCtDllBatchTestCommand { get; }
        /// <summary>单步测试：最小化本软件后截取 3D ROI 标定图。</summary>
        public ICommand CaptureVision3dSampleCommand { get; }
        /// <summary>单步测试：最小化本软件后截取「上」视图 ROI 标定图。</summary>
        public ICommand CaptureVisionTopViewSampleCommand { get; }
        public ICommand BrowseVisionLearn3dSampleDirCommand { get; }
        public ICommand BrowseVisionLearnTopViewSampleDirCommand { get; }
        public ICommand RunSimulationCommand { get; }
        public ICommand ClearImportLogCommand { get; }
        public ICommand ConfirmManualInterventionCommand { get; }
        public ICommand SwitchToProductionModeCommand { get; }
        public ICommand SwitchToDebugModeCommand { get; }
        public ICommand SwitchToSlicePipelineModeCommand { get; }
        public ICommand SwitchToAiPipelineModeCommand { get; }
        public ICommand SelectNavCommand { get; }
        public ICommand SelectPrefSubCommand { get; }
        public ICommand SelectThemeColorSlotCommand { get; }
        public ICommand ApplyThemePresetCommand { get; }
        public ICommand SaveThemeColorsCommand { get; }
        public ICommand RevertThemeColorsCommand { get; }
        public ICommand ResetThemeColorsCommand { get; }
        public ICommand SaveAsThemePresetCommand { get; }
        public ICommand DeleteThemePresetCommand { get; }
        public ICommand ApplyMainWindowSizeCommand { get; }
        public ICommand SaveCurrentMainWindowSizeCommand { get; }
        public ICommand RestoreDefaultMainWindowSizeCommand { get; }

        public event EventHandler? ImportLogScrollToEndRequested;
        /// <summary>主题色变更后由页面刷新 ResourceDictionary。</summary>
        public event EventHandler? ThemeColorsChanged;

        public VM_CTAutoSlicerPage(
            ILoggerService<VM_CTAutoSlicerPage> logger,
            IProjectSettingRepository projectSettingRepository,
            ICTAutoSlicerService ctAutoSlicerService,
            IAngleAlgorithmService angleAlgorithmService,
            ICutImgAlgorithmService cutImgAlgorithmService,
            CTFolderMonitorService folderMonitorService,
            CTAutoSlicerInputHookService inputHookService,
            ProcessMonitorService processMonitorService,
            CTAutoSlicerLocalConfigService localConfigService)
        {
            _logger = logger;
            _projectSettingRepository = projectSettingRepository;
            _ctAutoSlicerService = ctAutoSlicerService;
            _angleAlgorithmService = angleAlgorithmService;
            _cutImgAlgorithmService = cutImgAlgorithmService;
            _folderMonitorService = folderMonitorService;
            _inputHookService = inputHookService;
            _processMonitorService = processMonitorService;
            _localConfigService = localConfigService;

            OpenVGAppPathCommand = new RelayCommand(OpenVGAppPath);
            OpenCTFileFolderCommand = new RelayCommand(OpenCTFileFolder);
            OpenSliceExportFolderCommand = new RelayCommand(OpenSliceExportFolder);
            OpenTopViewHandoffFolderCommand = new RelayCommand(OpenTopViewHandoffFolder);
            OpenManualMeasureFolderCommand = new RelayCommand(OpenManualMeasureFolder);
            ClearSliceExportFolderCommand = new RelayCommand(_ =>
            {
                SliceExportFolder = string.Empty;
                MessageShower = "已清空标注结果根目录；二次手动标注将弹窗自选";
            });
            RefreshLatestAiAnnotationPreviewCommand = new RelayCommand(_ => RefreshLatestAiAnnotationPreview());
            RefreshStatsDemoCommand = new RelayCommand(_ => RefreshStatsDemoData(randomize: true));
            OpenHistogramCaptureDirCommand = new RelayCommand(OpenHistogramCaptureDir);
            OpenVisionCaptureDirCommand = new RelayCommand(OpenVisionCaptureDir);
            OpenVision2dVectorCaptureDirCommand = new RelayCommand(OpenVision2dVectorCaptureDir);
            OpenVision3dInitialCaptureDirCommand = new RelayCommand(OpenVision3dInitialCaptureDir);
            CalibrateHistogramRoiCommand = new RelayCommand(ExecuteCalibrateHistogramRoi);
            CalibrateLutSegmentPointsCommand = new RelayCommand(ExecuteCalibrateLutSegmentPoints);
            CalibrateVision3dRoiCommand = new RelayCommand(ExecuteCalibrateVision3dRoi);
            CalibrateVisionTopViewRoiCommand = new RelayCommand(ExecuteCalibrateVisionTopViewRoi);
            CalibrateSimple321ToolbarClickCommand = new RelayCommand(ExecuteCalibrateSimple321ToolbarClick);
            CalibrateSimple321OffsetEditCommand = new RelayCommand(ExecuteCalibrateSimple321OffsetEdit);
            CalibratePostAlignCaptureButtonCommand = new RelayCommand(ExecuteCalibratePostAlignCaptureButton);
            CalibratePostAlignDragArrowCommand = new RelayCommand(ExecuteCalibratePostAlignDragArrow);
            CalibratePre321ViewClickCommand = new RelayCommand(ExecuteCalibratePre321ViewClick);
            CalibratePre321ViewDragArrowCommand = new RelayCommand(ExecuteCalibratePre321ViewDragArrow);
            PickHistogramMaskColorCommand = new RelayCommand(ExecutePickHistogramMaskColor);
            TestRenderingPresetCommand = new RelayCommandAsync(ExecuteTestRenderingPresetAsync);
            TestThickPlateCommand = new RelayCommandAsync(ExecuteTestThickPlateAsync);
            TestSliceExportCommand = new RelayCommandAsync(ExecuteTestSliceExportAsync);
            TestHistogramGrayScaleCommand = new RelayCommandAsync(ExecuteTestHistogramGrayScaleAsync);
            TestSimple321AlignmentCommand = new RelayCommandAsync(ExecuteTestSimple321AlignmentAsync);
            TestPostAlignCaptureDragCommand = new RelayCommandAsync(ExecuteTestPostAlignCaptureDragAsync);
            TestPre321ViewPrepCommand = new RelayCommandAsync(ExecuteTestPre321ViewPrepAsync);
            TestOfflineCtVisionAnnotationCommand = new RelayCommandAsync(ExecuteTestOfflineCtVisionAnnotationAsync);
            BrowseCutImgDllTestSectionFolderCommand = new RelayCommand(ExecuteBrowseCutImgDllTestSectionFolder);
            BrowseCutImgDllTestVectorFolderCommand = new RelayCommand(ExecuteBrowseCutImgDllTestVectorFolder);
            BrowseCutImgDllTestOutputDirCommand = new RelayCommand(ExecuteBrowseCutImgDllTestOutputDir);
            TestCutImgGetSectionReferencePointsCommand = new RelayCommandAsync(ExecuteTestCutImgGetSectionReferencePointsAsync);
            TestCutImgGetObjectTopBottomPointsCommand = new RelayCommandAsync(ExecuteTestCutImgGetObjectTopBottomPointsAsync);
            BrowseOfflineCtModelPathCommand = new RelayCommand(ExecuteBrowseOfflineCtModelPath);
            ResetOfflineCtModelPathCommand = new RelayCommand(ExecuteResetOfflineCtModelPath);
            BrowseOfflineCtManualInputImageCommand = new RelayCommand(ExecuteBrowseOfflineCtManualInputImage);
            BrowseOfflineCtManualOutputFolderCommand = new RelayCommand(ExecuteBrowseOfflineCtManualOutputFolder);
            ClearOfflineCtManualOutputFolderCommand = new RelayCommand(_ => OfflineCtManualOutputFolder = string.Empty);
            RunOfflineCtManualAnnotationCommand = new RelayCommandAsync(ExecuteRunOfflineCtManualAnnotationAsync);
            BrowseOfflineCtDllTestInputFolderCommand = new RelayCommand(ExecuteBrowseOfflineCtDllTestInputFolder);
            BrowseOfflineCtDllTestOutputFolderCommand = new RelayCommand(ExecuteBrowseOfflineCtDllTestOutputFolder);
            RunOfflineCtDllBatchTestCommand = new RelayCommandAsync(ExecuteRunOfflineCtDllBatchTestAsync);
            CaptureVision3dSampleCommand = new RelayCommand(ExecuteCaptureVision3dSample);
            CaptureVisionTopViewSampleCommand = new RelayCommand(ExecuteCaptureVisionTopViewSample);
            BrowseVisionLearn3dSampleDirCommand = new RelayCommand(BrowseVisionLearn3dSampleDir);
            BrowseVisionLearnTopViewSampleDirCommand = new RelayCommand(BrowseVisionLearnTopViewSampleDir);
            RunSimulationCommand = new RelayCommandAsync(ExecuteRunSimulationAsync);
            ClearImportLogCommand = new RelayCommand(ClearImportLog);
            ConfirmManualInterventionCommand = new RelayCommand(ExecuteConfirmManualIntervention);
            SwitchToProductionModeCommand = new RelayCommand(_ => IsProductionMode = true);
            SwitchToDebugModeCommand = new RelayCommand(_ => IsDebugMode = true);
            SwitchToSlicePipelineModeCommand = new RelayCommand(_ => IsSlicePipelineMode = true);
            SwitchToAiPipelineModeCommand = new RelayCommand(_ => IsAiPipelineMode = true);
            SelectNavCommand = new RelayCommand(ExecuteSelectNav);
            SelectPrefSubCommand = new RelayCommand(ExecuteSelectPrefSub);
            SelectThemeColorSlotCommand = new RelayCommand(ExecuteSelectThemeColorSlot);
            ApplyThemePresetCommand = new RelayCommand(ExecuteApplyThemePreset);
            SaveThemeColorsCommand = new RelayCommand(_ => SaveThemeColors());
            RevertThemeColorsCommand = new RelayCommand(_ => RevertThemeColorsToSaved(showMessage: true));
            ResetThemeColorsCommand = new RelayCommand(_ => ResetThemeColorsToDefault());
            SaveAsThemePresetCommand = new RelayCommand(_ => SaveCurrentColorsAsPreset());
            DeleteThemePresetCommand = new RelayCommand(ExecuteDeleteThemePreset);
            ApplyMainWindowSizeCommand = new RelayCommand(_ => ExecuteApplyMainWindowSize());
            SaveCurrentMainWindowSizeCommand = new RelayCommand(_ => ExecuteSaveCurrentMainWindowSize());
            RestoreDefaultMainWindowSizeCommand = new RelayCommand(_ => ExecuteRestoreDefaultMainWindowSize());

            _ctAutoSlicerService.PauseStateChanged += (_, _) =>
            {
                Dispatcher.Invoke(() => IsFlowPaused = _ctAutoSlicerService.IsPaused);
            };
            _ctAutoSlicerService.InputBlockStateChanged += (_, _) =>
            {
                Dispatcher.Invoke(RefreshInputBlockStatus);
            };
            IsInputBlockEnabled = _ctAutoSlicerService.IsInputBlockEnabled;
        }

        private void ExecuteSelectNav(object? parameter)
        {
            if (parameter == null)
            {
                return;
            }

            var index = parameter switch
            {
                int i => i,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => -1
            };
            if (index < 0)
            {
                return;
            }

            SelectedNavIndex = index;
        }

        private void ExecuteSelectPrefSub(object? parameter)
        {
            var index = parameter switch
            {
                int i => i,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => -1
            };
            if (index < 0)
            {
                return;
            }

            SelectedPrefSubIndex = index;
        }


        private void NotifyNavSectionChanged()
        {
            OnPropertyChanged(nameof(IsNavOverview));
            OnPropertyChanged(nameof(IsNavApp));
            OnPropertyChanged(nameof(IsNavImport));
            OnPropertyChanged(nameof(IsNavRender));
            OnPropertyChanged(nameof(IsNavHistogram));
            OnPropertyChanged(nameof(IsNavVision));
            OnPropertyChanged(nameof(IsNavPostAlign));
            OnPropertyChanged(nameof(IsNavSlice));
            OnPropertyChanged(nameof(IsNavAiAnnotate));
            OnPropertyChanged(nameof(IsNavManualMeasure));
            OnPropertyChanged(nameof(IsNavStats));
            OnPropertyChanged(nameof(IsNavStepTest));
            OnPropertyChanged(nameof(IsNavPreferences));
            OnPropertyChanged(nameof(IsPrefSubColors));
            OnPropertyChanged(nameof(CurrentSectionTitle));
        }

        public void NotifyImportLogScrollChanged(bool isAtBottom)
        {
            _isUserScrolledUp = !isAtBottom;
        }


        public override async void OnInitialize()
        {
            LoadSettings();
            _processMonitorService.StartIfNotRunning();
            try
            {
                await _angleAlgorithmService.InitializeAsync(_dllDownloadBaseUrl).ConfigureAwait(false);
                try
                {
                    _cutImgAlgorithmService.Initialize();
                }
                catch (Exception cutEx)
                {
                    _logger.Error("CT 切图算法初始化失败", cutEx);
                    var hint = Simple321PointSource == (int)Core.Models.Project.CTAutoSlicer.Simple321PointSource.VisionDll
                        ? "当前已选「视觉 DLL」找点，单测/主流程将失败，请检查 Dlls\\CTAutoCutImg.dll"
                        : "当前为「上位机本地」找点，不影响本地算法；若改选视觉 DLL 需先修复";
                    await Dispatcher.InvokeAsync(() =>
                        MessageShower = $"切图算法初始化失败：{cutEx.Message}（{hint}）");
                }

                await Dispatcher.InvokeAsync(() => MessageShower = "CT自动化切片模块初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error("CT 角度算法初始化失败", ex);
                await Dispatcher.InvokeAsync(() => MessageShower = $"角度算法初始化失败：{ex.Message}");
            }

            base.OnInitialize();
        }

        public override void OnNavigatedTo(object? parameter)
        {
            _inputHookService.Start(OnAltXPressed, OnAltPPressed, OnAltOPressed);
            _folderMonitorService.Start(CTFileFolder);
            SyncAiAnnotationAutoRunMonitor();
            Dispatcher.BeginInvoke(new Action(TryApplyRememberedMainWindowSize), DispatcherPriority.Loaded);
            base.OnNavigatedTo(parameter);
        }

        public override void OnNavigatedFrom()
        {
            _inputHookService.Stop();
            StopAiAnnotationAutoRunMonitor();
            _ctAutoSlicerService.Cancel();
            // 离开本项目时恢复默认全屏，避免影响其它模块
            TryRestoreDefaultMainWindowMaximized();
            base.OnNavigatedFrom();
        }

        public override void OnCleanup()
        {
            CloseRoiPreviewOverlay(restoreMainWindow: true);
            StopAiAnnotationAutoRunMonitor();
            _inputHookService.Dispose();
            _folderMonitorService.Dispose();
            _activeFlowCts?.Cancel();
            _activeFlowCts?.Dispose();
            base.OnCleanup();
        }

        private bool _isLoadingSettings;


        private void SetSliceExportQuantityRuntime(int quantity)
        {
            if (_sliceExportQuantity == quantity)
            {
                return;
            }

            _sliceExportQuantity = quantity;
            OnPropertyChanged(nameof(SliceExportQuantity));
        }

        private void OpenVGAppPath(object? _)
        {
            var dialog = new OpenFileDialog
            {
                Title = "获取VG应用",
                Filter = "应用 (*.exe)|*.exe",
                RestoreDirectory = true,
            };
            if (!string.IsNullOrWhiteSpace(VGAppPath))
            {
                var fileInfo = new FileInfo(VGAppPath);
                if (fileInfo.Directory?.Exists == true)
                {
                    dialog.InitialDirectory = fileInfo.Directory.FullName;
                }
            }

            if (dialog.ShowDialog() == true)
            {
                VGAppPath = dialog.FileName;
            }
        }

        private void OpenCTFileFolder(object? _)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = string.IsNullOrWhiteSpace(CTFileFolder) ? null : CTFileFolder,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                CTFileFolder = dialog.FileName;
            }
        }

        private void OpenSliceExportFolder(object? _)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = IsAiPipelineMode ? "选择标注结果根目录" : "选择切片导出保存文件夹",
                InitialDirectory = string.IsNullOrWhiteSpace(SliceExportFolder) ? null : SliceExportFolder,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                SliceExportFolder = dialog.FileName;
            }
        }

        private void OpenTopViewHandoffFolder(object? _)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "选择上视图交接目录",
                InitialDirectory = string.IsNullOrWhiteSpace(TopViewHandoffFolder) ? null : TopViewHandoffFolder,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                TopViewHandoffFolder = dialog.FileName;
            }
        }

        /// <summary>扫描标注结果根目录，刷新最新 AI 结果图预览。</summary>


        private async Task ExecuteRunSimulationAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (IsAiPipelineMode)
            {
                await ExecuteAiAnnotationBatchAsync().ConfigureAwait(false);
                return;
            }

            await ExecuteSlicePipelineAsync().ConfigureAwait(false);
        }


        private async Task ExecuteSlicePipelineAsync()
        {
            if (string.IsNullOrWhiteSpace(TopViewHandoffFolder))
            {
                MessageShower = "请先配置上视图交接目录";
                return;
            }

            if (string.IsNullOrWhiteSpace(CTFileFolder) || !Directory.Exists(CTFileFolder))
            {
                MessageShower = "请先选择有效的 CT 体素文件夹";
                return;
            }

            var folderFiles = Directory.EnumerateFiles(CTFileFolder).ToList();
            var vglCount = folderFiles.Count(CTAutoSlicerSettings.IsImportVolumeQueueEntry);
            if (vglCount == 0)
            {
                var broken = folderFiles
                    .Where(CTAutoSlicerSettings.LooksLikeBrokenVolumeShortcutName)
                    .Select(Path.GetFileName)
                    .Take(3)
                    .ToList();
                if (broken.Count > 0)
                {
                    MessageShower =
                        $"检测到名称像快捷方式但不是 .lnk 文件：{string.Join("；", broken)}。"
                        + "请对源 .vgi 右键「创建快捷方式」，再把生成的 .lnk 拷入体素文件夹。";
                    return;
                }

                var sample = folderFiles
                    .Select(Path.GetFileName)
                    .Where(name =>
                        !string.IsNullOrEmpty(name)
                        && (name.Contains("快捷方式", StringComparison.Ordinal)
                            || name.Contains(".vgi", StringComparison.OrdinalIgnoreCase)
                            || name.Contains(".vgl", StringComparison.OrdinalIgnoreCase)
                            || name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)))
                    .Take(3)
                    .ToList();

                MessageShower = sample.Count > 0
                    ? $"未识别可导入体素。请确认是 Windows「快捷方式」(.lnk)。当前可疑文件：{string.Join("；", sample)}"
                    : "所选文件夹内未找到 .vgl/.vgi 文件（含「.vgi - 快捷方式」）";
                return;
            }

            var flowToken = BeginActiveFlow();

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 切图批次开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                await _ctAutoSlicerService
                    .RunSimulationAsync(BuildSettings(), flowToken, progress);
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => MessageShower = ex.Message);
                _logger.Error("CT 切图流程异常", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

    }
}
