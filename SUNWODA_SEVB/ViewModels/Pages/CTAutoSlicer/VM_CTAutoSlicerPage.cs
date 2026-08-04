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
    public class VM_CTAutoSlicerPage : ViewModelBase
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
        private DispatcherTimer? _manualInterventionCountdownTimer;
        private DateTime _manualInterventionDeadlineUtc;

        private string _messageShower = "CT自动化切片模块已加载";
        private bool _isEnableSimulationBtn = true;
        private bool _isFlowPaused;
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
        private string _renderingPresetName = "testlut";
        private int _presetApplyWaitTime = 2000;
        private int _presetDropdownWaitTime = 5000;
        private string _dllDownloadBaseUrl = CTAutoSlicerSettings.DefaultDllDownloadBaseUrl;
        private int _thickPlateMenuOffsetX;
        private int _thickPlateMenuOffsetY = 28;
        private int _viewportClickOffsetY;
        private string _sliceExportFolder = string.Empty;
        private string _topViewHandoffFolder = string.Empty;
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
        private int _lutSegmentPoint1X;
        private int _lutSegmentPoint1Y;
        private int _lutSegmentPoint2X;
        private int _lutSegmentPoint2Y;
        private bool _lutSegmentPointsCalibrated;
        private string _lutSegmentPointsDisplayText = "未标定";
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
        private int _offlineCtTopViewSliceIndex = CTAutoSlicerSettings.DefaultOfflineCtTopViewSliceIndex;
        private double _offlineCtPixelSizeUm = CTAutoSlicerSettings.DefaultOfflineCtPixelSizeUm;
        // 使用完全限定枚举名，避免与同名属性 OfflineCtResultImageMode 冲突（CS0236）
        private int _offlineCtResultImageMode = (int)Core.Models.Project.CTAutoSlicer.OfflineCtResultImageMode.Enhance;
        private bool _offlineCtExportJson = true;
        private bool _offlineCtExportCsv = true;
        private bool _offlineCtExportInputForNjct;
        private bool _offlineCtRotateTopViewCw90 = true;
        private bool _enableOfflineCtPreBrighten = true;
        private double _offlineCtPreBrightenMeanTrigger = 120.0;
        private double _offlineCtPreBrightenP50Trigger = 110.0;
        private double _offlineCtPreBrightenTargetMean = 132.0;
        private string _offlineCtManualInputImagePath = string.Empty;
        private string _offlineCtManualOutputFolder = string.Empty;
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
            IsAiPipelineMode
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
            OnPropertyChanged(nameof(OverviewHeroTitle));
            OnPropertyChanged(nameof(SliceExportFolderLabel));
            EnsureNavValidForPipelineMode();

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
            IsEnableSimulationBtn ? "开始流程" : "运行中…";

        /// <summary>流程暂停中（Alt+P 切换）；用于主界面醒目横幅。</summary>
        public bool IsFlowPaused
        {
            get => _isFlowPaused;
            private set => SetProperty(ref _isFlowPaused, value);
        }

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

        public string RenderingPresetName
        {
            get => _renderingPresetName;
            set
            {
                if (SetProperty(ref _renderingPresetName, value))
                {
                    SaveSetting(nameof(RenderingPresetName), value);
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

        public int LutSegmentPoint1X
        {
            get => _lutSegmentPoint1X;
            set
            {
                if (SetProperty(ref _lutSegmentPoint1X, value))
                {
                    SaveSetting(nameof(LutSegmentPoint1X), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public int LutSegmentPoint1Y
        {
            get => _lutSegmentPoint1Y;
            set
            {
                if (SetProperty(ref _lutSegmentPoint1Y, value))
                {
                    SaveSetting(nameof(LutSegmentPoint1Y), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public int LutSegmentPoint2X
        {
            get => _lutSegmentPoint2X;
            set
            {
                if (SetProperty(ref _lutSegmentPoint2X, value))
                {
                    SaveSetting(nameof(LutSegmentPoint2X), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public int LutSegmentPoint2Y
        {
            get => _lutSegmentPoint2Y;
            set
            {
                if (SetProperty(ref _lutSegmentPoint2Y, value))
                {
                    SaveSetting(nameof(LutSegmentPoint2Y), value);
                    RefreshLutSegmentPointsDisplay();
                }
            }
        }

        public string LutSegmentPointsDisplayText
        {
            get => _lutSegmentPointsDisplayText;
            private set => SetProperty(ref _lutSegmentPointsDisplayText, value);
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
            set
            {
                if (SetProperty(ref _offlineCtL1MinMm, value))
                {
                    SaveSetting(nameof(OfflineCtL1MinMm), value);
                }
            }
        }

        public double OfflineCtL1MaxMm
        {
            get => _offlineCtL1MaxMm;
            set
            {
                if (SetProperty(ref _offlineCtL1MaxMm, value))
                {
                    SaveSetting(nameof(OfflineCtL1MaxMm), value);
                }
            }
        }

        public double OfflineCtL2MinMm
        {
            get => _offlineCtL2MinMm;
            set
            {
                if (SetProperty(ref _offlineCtL2MinMm, value))
                {
                    SaveSetting(nameof(OfflineCtL2MinMm), value);
                }
            }
        }

        public double OfflineCtL2MaxMm
        {
            get => _offlineCtL2MaxMm;
            set
            {
                if (SetProperty(ref _offlineCtL2MaxMm, value))
                {
                    SaveSetting(nameof(OfflineCtL2MaxMm), value);
                }
            }
        }

        public double OfflineCtL3MinMm
        {
            get => _offlineCtL3MinMm;
            set
            {
                if (SetProperty(ref _offlineCtL3MinMm, value))
                {
                    SaveSetting(nameof(OfflineCtL3MinMm), value);
                }
            }
        }

        public double OfflineCtL3MaxMm
        {
            get => _offlineCtL3MaxMm;
            set
            {
                if (SetProperty(ref _offlineCtL3MaxMm, value))
                {
                    SaveSetting(nameof(OfflineCtL3MaxMm), value);
                }
            }
        }

        public double OfflineCtL4MinMm
        {
            get => _offlineCtL4MinMm;
            set
            {
                if (SetProperty(ref _offlineCtL4MinMm, value))
                {
                    SaveSetting(nameof(OfflineCtL4MinMm), value);
                }
            }
        }

        public double OfflineCtL4MaxMm
        {
            get => _offlineCtL4MaxMm;
            set
            {
                if (SetProperty(ref _offlineCtL4MaxMm, value))
                {
                    SaveSetting(nameof(OfflineCtL4MaxMm), value);
                }
            }
        }

        public double OfflineCtL5MinMm
        {
            get => _offlineCtL5MinMm;
            set
            {
                if (SetProperty(ref _offlineCtL5MinMm, value))
                {
                    SaveSetting(nameof(OfflineCtL5MinMm), value);
                }
            }
        }

        public double OfflineCtL5MaxMm
        {
            get => _offlineCtL5MaxMm;
            set
            {
                if (SetProperty(ref _offlineCtL5MaxMm, value))
                {
                    SaveSetting(nameof(OfflineCtL5MaxMm), value);
                }
            }
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
                    SaveSetting(nameof(OfflineCtPixelSizeUm), normalized);
                    OnPropertyChanged(nameof(OfflineCtPixelSizeMmPerPx));
                    OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaUm));
                    OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaMm));
                }
            }
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

        /// <summary>是否落盘 input_for_njct（排查对照用，默认关）。</summary>
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

        /// <summary>送入 NJCT 前是否将上视图顺时针旋转 90°。</summary>
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
        public ICommand OpenVision3dPointsStepDebugCommand { get; }
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
            OpenVision3dPointsStepDebugCommand = new RelayCommand(_ => ExecuteOpenVision3dPointsStepDebug());
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

        private void ExecuteSelectThemeColorSlot(object? parameter)
        {
            var index = parameter switch
            {
                int i => i,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => -1
            };
            if (index is < 0 or > 3)
            {
                return;
            }

            ThemeEditSlot = index;
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

        private void SetThemeColor(ref string field, string value, string propName, string brushPropName)
        {
            var normalized = NormalizeHexColor(value);
            if (string.Equals(field, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            field = normalized;
            OnPropertyChanged(propName);
            OnPropertyChanged(brushPropName);
            if (!_themeEditSyncing && GetHexForSlot(ThemeEditSlot) == normalized)
            {
                OnPropertyChanged(nameof(ThemeEditHex));
                OnPropertyChanged(nameof(ThemeEditRgbText));
                OnPropertyChanged(nameof(ThemeEditPreviewBrush));
            }

            RefreshThemeDirtyState();
            if (!_themeEditSyncing)
            {
                ThemeColorsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void LoadThemeEditFromSlot()
        {
            if (!TryParseHexColor(GetHexForSlot(ThemeEditSlot), out var color))
            {
                return;
            }

            _themeEditSyncing = true;
            try
            {
                _themeEditR = color.R;
                _themeEditG = color.G;
                _themeEditB = color.B;
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
            OnPropertyChanged(nameof(ThemeEditColor));
        }

        private void CommitThemeEditRgb()
        {
            if (_themeEditSyncing)
            {
                return;
            }

            var hex = ThemeEditHex;
            _themeEditSyncing = true;
            try
            {
                switch (ThemeEditSlot)
                {
                    case 0:
                        ThemePageBgColor = hex;
                        break;
                    case 1:
                        ThemeOuterCardColor = hex;
                        break;
                    case 2:
                        ThemeInnerCardColor = hex;
                        break;
                    case 3:
                        ThemeSidebarColor = hex;
                        break;
                }
            }
            finally
            {
                _themeEditSyncing = false;
            }

            OnPropertyChanged(nameof(ThemeEditHex));
            OnPropertyChanged(nameof(ThemeEditRgbText));
            OnPropertyChanged(nameof(ThemeEditPreviewBrush));
            OnPropertyChanged(nameof(ThemeEditColor));
            ThemeColorsChanged?.Invoke(this, EventArgs.Empty);
        }

        private string GetHexForSlot(int slot) => slot switch
        {
            1 => ThemeOuterCardColor,
            2 => ThemeInnerCardColor,
            3 => ThemeSidebarColor,
            _ => ThemePageBgColor,
        };

        private void ExecuteApplyThemePreset(object? parameter)
        {
            var id = parameter as string;
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var preset = ThemeColorPresets.FirstOrDefault(p =>
                string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (preset == null)
            {
                return;
            }

            ApplyThemePresetPreview(new CTAutoSlicerSettings.ThemeColorPreset(
                preset.Id,
                preset.Name,
                preset.Description,
                preset.PageBg,
                preset.OuterCard,
                preset.InnerCard,
                preset.Sidebar,
                preset.IsBuiltIn));
            MessageShower = $"已预览「{preset.Name}」，请点「保存主题」后下次启动生效";
        }

        private void ApplyThemePresetPreview(CTAutoSlicerSettings.ThemeColorPreset preset)
        {
            _themeEditSyncing = true;
            try
            {
                ThemePageBgColor = preset.PageBg;
                ThemeOuterCardColor = preset.OuterCard;
                ThemeInnerCardColor = preset.InnerCard;
                ThemeSidebarColor = preset.Sidebar;
            }
            finally
            {
                _themeEditSyncing = false;
            }

            LoadThemeEditFromSlot();
            RefreshThemeDirtyState();
            ThemeColorsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SaveThemeColors()
        {
            _savedThemePageBgColor = ThemePageBgColor;
            _savedThemeOuterCardColor = ThemeOuterCardColor;
            _savedThemeInnerCardColor = ThemeInnerCardColor;
            _savedThemeSidebarColor = ThemeSidebarColor;

            SaveSetting(nameof(ThemePageBgColor), _savedThemePageBgColor);
            SaveSetting(nameof(ThemeOuterCardColor), _savedThemeOuterCardColor);
            SaveSetting(nameof(ThemeInnerCardColor), _savedThemeInnerCardColor);
            SaveSetting(nameof(ThemeSidebarColor), _savedThemeSidebarColor);

            RefreshThemeDirtyState();
            MessageShower = "主题色已保存，下次启动将沿用当前配色";
        }

        private void RevertThemeColorsToSaved(bool showMessage)
        {
            _themeEditSyncing = true;
            try
            {
                ThemePageBgColor = _savedThemePageBgColor;
                ThemeOuterCardColor = _savedThemeOuterCardColor;
                ThemeInnerCardColor = _savedThemeInnerCardColor;
                ThemeSidebarColor = _savedThemeSidebarColor;
            }
            finally
            {
                _themeEditSyncing = false;
            }

            LoadThemeEditFromSlot();
            RefreshThemeDirtyState();
            ThemeColorsChanged?.Invoke(this, EventArgs.Empty);
            if (showMessage)
            {
                MessageShower = "已还原为上次保存的主题色";
            }
        }

        private void ResetThemeColorsToDefault()
        {
            var preset = CTAutoSlicerSettings.ThemeColorPresets[0];
            ApplyThemePresetPreview(preset);
            MessageShower = $"已预览「{preset.Name}」，请点「保存主题」后下次启动生效";
        }

        private void SaveCurrentColorsAsPreset()
        {
            var name = (NewThemePresetName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageShower = "请先填写新预设名称";
                return;
            }

            if (ThemeColorPresets.Any(p =>
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageShower = "预设名称已存在，请换一个名称";
                return;
            }

            var id = "custom-" + Guid.NewGuid().ToString("N")[..8];
            var preset = new CTAutoSlicerSettings.ThemeColorPreset(
                id,
                name,
                "自定义预设",
                ThemePageBgColor,
                ThemeOuterCardColor,
                ThemeInnerCardColor,
                ThemeSidebarColor,
                isBuiltIn: false);
            ThemeColorPresets.Add(new ThemePresetItem(preset));
            PersistCustomThemePresets();
            NewThemePresetName = string.Empty;
            MessageShower = $"已保存自定义预设「{name}」";
        }

        private void ExecuteDeleteThemePreset(object? parameter)
        {
            var id = parameter as string;
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var item = ThemeColorPresets.FirstOrDefault(p =>
                string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                return;
            }

            if (item.IsBuiltIn)
            {
                MessageShower = "内置预设不可删除";
                return;
            }

            ThemeColorPresets.Remove(item);
            PersistCustomThemePresets();
            MessageShower = $"已删除自定义预设「{item.Name}」";
        }

        private void RebuildThemePresetList(IEnumerable<CTAutoSlicerSettings.ThemeCustomPresetDto>? customs = null)
        {
            ThemeColorPresets.Clear();
            foreach (var builtIn in CTAutoSlicerSettings.ThemeColorPresets)
            {
                ThemeColorPresets.Add(new ThemePresetItem(builtIn));
            }

            if (customs == null)
            {
                return;
            }

            foreach (var dto in customs)
            {
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Name))
                {
                    continue;
                }

                if (CTAutoSlicerSettings.ThemeColorPresets.Any(p =>
                        string.Equals(p.Id, dto.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                ThemeColorPresets.Add(new ThemePresetItem(new CTAutoSlicerSettings.ThemeColorPreset(
                    dto.Id,
                    dto.Name,
                    string.IsNullOrWhiteSpace(dto.Description) ? "自定义预设" : dto.Description,
                    NormalizeHexColor(dto.PageBg),
                    NormalizeHexColor(dto.OuterCard),
                    NormalizeHexColor(dto.InnerCard),
                    NormalizeHexColor(dto.Sidebar),
                    isBuiltIn: false)));
            }
        }

        private void PersistCustomThemePresets()
        {
            var customs = ThemeColorPresets
                .Where(p => !p.IsBuiltIn)
                .Select(p => new CTAutoSlicerSettings.ThemeCustomPresetDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    PageBg = p.PageBg,
                    OuterCard = p.OuterCard,
                    InnerCard = p.InnerCard,
                    Sidebar = p.Sidebar,
                })
                .ToList();
            var json = JsonSerializer.Serialize(customs);
            SaveSetting("ThemeCustomPresetsJson", json);
        }

        private void LoadCustomThemePresets()
        {
            var json = LoadStringSetting("ThemeCustomPresetsJson", "[]");
            List<CTAutoSlicerSettings.ThemeCustomPresetDto>? customs = null;
            try
            {
                customs = JsonSerializer.Deserialize<List<CTAutoSlicerSettings.ThemeCustomPresetDto>>(json);
            }
            catch
            {
                customs = null;
            }

            RebuildThemePresetList(customs);
        }

        private void RefreshThemeDirtyState()
        {
            HasUnsavedThemeChanges =
                !HexEquals(_themePageBgColor, _savedThemePageBgColor)
                || !HexEquals(_themeOuterCardColor, _savedThemeOuterCardColor)
                || !HexEquals(_themeInnerCardColor, _savedThemeInnerCardColor)
                || !HexEquals(_themeSidebarColor, _savedThemeSidebarColor);
        }

        private static bool HexEquals(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static Brush CreateBrush(string hex)
        {
            var brush = new SolidColorBrush(TryParseHexColor(hex, out var c) ? c : Colors.White);
            brush.Freeze();
            return brush;
        }

        private static string NormalizeHexColor(string? value)
        {
            if (TryParseHexColor(value, out var c))
            {
                return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }

            return CTAutoSlicerSettings.DefaultThemePageBgColor;
        }

        private static bool TryParseHexColor(string? value, out Color color)
        {
            color = Colors.White;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var s = value.Trim();
            if (!s.StartsWith('#'))
            {
                s = "#" + s;
            }

            try
            {
                var converted = ColorConverter.ConvertFromString(s);
                if (converted is Color c)
                {
                    color = Color.FromRgb(c.R, c.G, c.B);
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
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
            _inputHookService.Start(OnDoubleEscPressed, OnSingleEscPressed, OnCtrlXPressed, OnAltPPressed);
            _folderMonitorService.Start(CTFileFolder);
            Dispatcher.BeginInvoke(new Action(TryApplyRememberedMainWindowSize), DispatcherPriority.Loaded);
            base.OnNavigatedTo(parameter);
        }

        public override void OnNavigatedFrom()
        {
            _inputHookService.Stop();
            _ctAutoSlicerService.Cancel();
            // 离开本项目时恢复默认全屏，避免影响其它模块
            TryRestoreDefaultMainWindowMaximized();
            base.OnNavigatedFrom();
        }

        public override void OnCleanup()
        {
            CloseRoiPreviewOverlay(restoreMainWindow: true);
            _inputHookService.Dispose();
            _folderMonitorService.Dispose();
            _activeFlowCts?.Cancel();
            _activeFlowCts?.Dispose();
            base.OnCleanup();
        }

        private bool _isLoadingSettings;

        private void LoadSettings()
        {
            _isLoadingSettings = true;
            VGAppPath = LoadStringSetting(nameof(VGAppPath), @"D:\App\VG\vgstudiomax20241.exe");
            VGOpenWaitTime = LoadIntSetting(nameof(VGOpenWaitTime), CTAutoSlicerSettings.DefaultVGOpenWaitTime);
            if (VGOpenWaitTime <= CTAutoSlicerSettings.LegacyVGOpenWaitTimeThreshold)
            {
                _logger.Info(
                    $"检测到旧版 VGOpenWaitTime={VGOpenWaitTime}ms，自动升级为 {CTAutoSlicerSettings.DefaultVGOpenWaitTime}ms");
                _projectSettingRepository.UpdateSettingValue(
                    nameof(VGOpenWaitTime),
                    VmName,
                    CTAutoSlicerSettings.DefaultVGOpenWaitTime);
                VGOpenWaitTime = CTAutoSlicerSettings.DefaultVGOpenWaitTime;
            }
            _vgShowFrontWaitTime = LoadIntSetting("VGShowFrontWaitTime", 2000);
            CTFileFolder = LoadStringSetting(nameof(CTFileFolder), string.Empty);
            MaxVgProcesses = LoadIntSetting(
                nameof(MaxVgProcesses),
                CTAutoSlicerSettings.DefaultMaxVgProcesses);
            QuickImportDialogWaitTime = LoadIntSetting(nameof(QuickImportDialogWaitTime), 10000);
            NewProjectDialogWaitTime = LoadIntSetting(nameof(NewProjectDialogWaitTime), 5000);
            FileSelectionDialogWaitTime = LoadIntSetting(nameof(FileSelectionDialogWaitTime), 15000);
            ImportTimeoutMinutes = LoadIntSetting(nameof(ImportTimeoutMinutes), 30);
            PostImportWaitTime = LoadIntSetting(nameof(PostImportWaitTime), 3000);
            StepRetryCount = LoadIntSetting(nameof(StepRetryCount), 3);
            StepRetryIntervalMs = LoadIntSetting(nameof(StepRetryIntervalMs), 2000);
            RenderingPresetName = LoadStringSetting(nameof(RenderingPresetName), "testlut");
            PresetApplyWaitTime = LoadIntSetting(nameof(PresetApplyWaitTime), 2000);
            PresetDropdownWaitTime = LoadIntSetting(nameof(PresetDropdownWaitTime), 5000);
            ThickPlateMenuOffsetX = LoadIntSetting(nameof(ThickPlateMenuOffsetX), 90);
            ThickPlateMenuOffsetY = LoadIntSetting(nameof(ThickPlateMenuOffsetY), 127);
            ViewportClickOffsetY = LoadIntSetting(nameof(ViewportClickOffsetY), 0);
            _thickPlateStepDelayMs = LoadIntSetting("ThickPlateStepDelayMs", 500);
            _thickPlateDialogWaitTime = LoadIntSetting("ThickPlateDialogWaitTime", 3000);
            _thickPlateDropdownWaitTime = LoadIntSetting("ThickPlateDropdownWaitTime", 3000);
            _viewportQuadrant3ClickOffsetY = LoadIntSetting("ViewportQuadrant3ClickOffsetY", -100);
            _viewportMinWidth = LoadIntSetting("ViewportMinWidth", 400);
            _viewportMinHeight = LoadIntSetting("ViewportMinHeight", 300);
            _thickPlateStepRetryCount = LoadIntSetting("ThickPlateStepRetryCount", 1);
            _thickPlateMenuAfterRightClickDelayMs = LoadIntSetting("ThickPlateMenuAfterRightClickDelayMs", 200);
            SliceExportFolder = LoadStringSetting(nameof(SliceExportFolder), string.Empty);
            TopViewHandoffFolder = LoadStringSetting(nameof(TopViewHandoffFolder), string.Empty);
            // 机台角色存本机 JSON（Config/CTAutoSlicer/local.json），不落共享 DB
            PipelineRunMode = (int)_localConfigService.LoadPipelineRunMode();
            _logger.Info(
                $"已从本机配置恢复流水线模式：{(IsAiPipelineMode ? "AI标注" : "切图")}（{CTAutoSlicerLocalConfigService.ConfigFilePath}）");
            SliceExportImageWidth = LoadIntSetting(nameof(SliceExportImageWidth), 3000);
            SliceExportQuantity = LoadIntSetting(nameof(SliceExportQuantity), 100);
            SliceExportQuantityQj = LoadIntSetting(nameof(SliceExportQuantityQj), 150);
            // 三视角切片范围；若无新键则回填旧的单组 SliceRangeStart/End
            var legacyStart = LoadDoubleSetting(nameof(CTAutoSlicerSettings.SliceRangeStartMm), 20);
            var legacyEnd = LoadDoubleSetting(nameof(CTAutoSlicerSettings.SliceRangeEndMm), 70);
            SliceRangeFrontStartMm = LoadDoubleSetting(nameof(SliceRangeFrontStartMm), legacyStart);
            SliceRangeFrontEndMm = LoadDoubleSetting(nameof(SliceRangeFrontEndMm), legacyEnd);
            SliceRangeRightStartMm = LoadDoubleSetting(nameof(SliceRangeRightStartMm), legacyStart);
            SliceRangeRightEndMm = LoadDoubleSetting(nameof(SliceRangeRightEndMm), legacyEnd);
            SliceRangeTopStartMm = LoadDoubleSetting(nameof(SliceRangeTopStartMm), legacyStart);
            SliceRangeTopEndMm = LoadDoubleSetting(nameof(SliceRangeTopEndMm), legacyEnd);
            SliceExportDialogWaitTime = LoadIntSetting(nameof(SliceExportDialogWaitTime), 8000);
            SliceExportWriteTimeoutMs = LoadIntSetting(nameof(SliceExportWriteTimeoutMs), 60000);
            _histogramRoiX = LoadIntSetting(nameof(HistogramRoiX), 0);
            _histogramRoiY = LoadIntSetting(nameof(HistogramRoiY), 0);
            _histogramRoiWidth = LoadIntSetting(nameof(HistogramRoiWidth), 0);
            _histogramRoiHeight = LoadIntSetting(nameof(HistogramRoiHeight), 0);
            OnPropertyChanged(nameof(HistogramRoiX));
            OnPropertyChanged(nameof(HistogramRoiY));
            OnPropertyChanged(nameof(HistogramRoiWidth));
            OnPropertyChanged(nameof(HistogramRoiHeight));
            HistogramCaptureDir = LoadStringSetting(nameof(HistogramCaptureDir), string.Empty);
            VisionCaptureDir = LoadStringSetting(
                nameof(VisionCaptureDir),
                CTAutoSlicerSettings.DefaultVisionCaptureDir);
            Vision2dVectorCaptureDir = LoadStringSetting(
                nameof(Vision2dVectorCaptureDir),
                CTAutoSlicerSettings.DefaultVision2dVectorCaptureDir);
            Vision3dInitialCaptureDir = LoadStringSetting(
                nameof(Vision3dInitialCaptureDir),
                CTAutoSlicerSettings.DefaultVision3dInitialCaptureDir);
            HistogramMaskColor = LoadStringSetting(nameof(HistogramMaskColor), "#6E6E6E");
            HistogramMaskTolerance = LoadIntSetting(nameof(HistogramMaskTolerance), 2);
            _dllDownloadBaseUrl = LoadStringSetting(nameof(CTAutoSlicerSettings.DllDownloadBaseUrl), CTAutoSlicerSettings.DefaultDllDownloadBaseUrl);
            _lutSegmentPoint1X = LoadIntSetting(nameof(LutSegmentPoint1X), 0);
            _lutSegmentPoint1Y = LoadIntSetting(nameof(LutSegmentPoint1Y), 0);
            _lutSegmentPoint2X = LoadIntSetting(nameof(LutSegmentPoint2X), 0);
            _lutSegmentPoint2Y = LoadIntSetting(nameof(LutSegmentPoint2Y), 0);
            _lutSegmentPointsCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.LutSegmentPointsCalibrated), 0) != 0;
            OnPropertyChanged(nameof(LutSegmentPoint1X));
            OnPropertyChanged(nameof(LutSegmentPoint1Y));
            OnPropertyChanged(nameof(LutSegmentPoint2X));
            OnPropertyChanged(nameof(LutSegmentPoint2Y));
            RefreshLutSegmentPointsDisplay();
            RefreshHistogramRoiDisplay();
            RefreshMaskColorBrush();

            _vision3dRoiX = LoadIntSetting(nameof(Vision3dRoiX), 0);
            _vision3dRoiY = LoadIntSetting(nameof(Vision3dRoiY), 0);
            _vision3dRoiWidth = LoadIntSetting(nameof(Vision3dRoiWidth), 0);
            _vision3dRoiHeight = LoadIntSetting(nameof(Vision3dRoiHeight), 0);
            OnPropertyChanged(nameof(Vision3dRoiX));
            OnPropertyChanged(nameof(Vision3dRoiY));
            OnPropertyChanged(nameof(Vision3dRoiWidth));
            OnPropertyChanged(nameof(Vision3dRoiHeight));
            RefreshVision3dRoiDisplay();

            _visionTopViewRoiX = LoadIntSetting(nameof(VisionTopViewRoiX), 0);
            _visionTopViewRoiY = LoadIntSetting(nameof(VisionTopViewRoiY), 0);
            _visionTopViewRoiWidth = LoadIntSetting(nameof(VisionTopViewRoiWidth), 0);
            _visionTopViewRoiHeight = LoadIntSetting(nameof(VisionTopViewRoiHeight), 0);
            OnPropertyChanged(nameof(VisionTopViewRoiX));
            OnPropertyChanged(nameof(VisionTopViewRoiY));
            OnPropertyChanged(nameof(VisionTopViewRoiWidth));
            OnPropertyChanged(nameof(VisionTopViewRoiHeight));
            RefreshVisionTopViewRoiDisplay();

            _simple321ToolbarClickX = LoadIntSetting(nameof(Simple321ToolbarClickX), 0);
            _simple321ToolbarClickY = LoadIntSetting(nameof(Simple321ToolbarClickY), 0);
            _simple321ToolbarClickCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.Simple321ToolbarClickCalibrated), 0) != 0;
            OnPropertyChanged(nameof(Simple321ToolbarClickX));
            OnPropertyChanged(nameof(Simple321ToolbarClickY));
            RefreshSimple321ToolbarClickDisplay();

            _simple321OffsetEditX = LoadIntSetting(nameof(Simple321OffsetEditX), 0);
            _simple321OffsetEditY = LoadIntSetting(nameof(Simple321OffsetEditY), 0);
            _simple321OffsetEditCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.Simple321OffsetEditCalibrated), 0) != 0;
            OnPropertyChanged(nameof(Simple321OffsetEditX));
            OnPropertyChanged(nameof(Simple321OffsetEditY));
            RefreshSimple321OffsetEditDisplay();

            _simple321PointSource = Math.Clamp(
                LoadIntSetting(
                    nameof(Simple321PointSource),
                    (int)Core.Models.Project.CTAutoSlicer.Simple321PointSource.Local),
                0,
                1);
            OnPropertyChanged(nameof(Simple321PointSource));
            OnPropertyChanged(nameof(Simple321PointSourceDisplay));
            OnPropertyChanged(nameof(IsSimple321LocalPointSource));

            _postAlignCaptureButtonX = LoadIntSetting(nameof(PostAlignCaptureButtonX), 0);
            _postAlignCaptureButtonY = LoadIntSetting(nameof(PostAlignCaptureButtonY), 0);
            _postAlignCaptureButtonCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.PostAlignCaptureButtonCalibrated), 0) != 0;
            _postAlignCaptureButtonIndex = LoadIntSetting(nameof(PostAlignCaptureButtonIndex), 0);
            OnPropertyChanged(nameof(PostAlignCaptureButtonX));
            OnPropertyChanged(nameof(PostAlignCaptureButtonY));
            OnPropertyChanged(nameof(PostAlignCaptureButtonIndex));
            RefreshPostAlignCaptureButtonDisplay();

            _postAlignDragStartX = LoadIntSetting(nameof(PostAlignDragStartX), 0);
            _postAlignDragStartY = LoadIntSetting(nameof(PostAlignDragStartY), 0);
            _postAlignDragEndX = LoadIntSetting(nameof(PostAlignDragEndX), 0);
            _postAlignDragEndY = LoadIntSetting(nameof(PostAlignDragEndY), 0);
            _postAlignDragCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.PostAlignDragCalibrated), 0) != 0;
            OnPropertyChanged(nameof(PostAlignDragStartX));
            OnPropertyChanged(nameof(PostAlignDragStartY));
            OnPropertyChanged(nameof(PostAlignDragEndX));
            OnPropertyChanged(nameof(PostAlignDragEndY));
            RefreshPostAlignDragDisplay();

            _enablePostAlignCaptureDragSteps =
                LoadIntSetting(nameof(EnablePostAlignCaptureDragSteps), 1) != 0;
            _postAlignClickWaitMs = LoadIntSetting(nameof(PostAlignClickWaitMs), 400);
            _postAlignDragStepDelayMs = LoadIntSetting(nameof(PostAlignDragStepDelayMs), 8);
            OnPropertyChanged(nameof(EnablePostAlignCaptureDragSteps));
            OnPropertyChanged(nameof(PostAlignClickWaitMs));
            OnPropertyChanged(nameof(PostAlignDragStepDelayMs));

            _pre321ViewClickX = LoadIntSetting(nameof(Pre321ViewClickX), 0);
            _pre321ViewClickY = LoadIntSetting(nameof(Pre321ViewClickY), 0);
            _pre321ViewClickCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.Pre321ViewClickCalibrated), 0) != 0;
            OnPropertyChanged(nameof(Pre321ViewClickX));
            OnPropertyChanged(nameof(Pre321ViewClickY));
            RefreshPre321ViewClickDisplay();

            _pre321ViewDragStartX = LoadIntSetting(nameof(Pre321ViewDragStartX), 0);
            _pre321ViewDragStartY = LoadIntSetting(nameof(Pre321ViewDragStartY), 0);
            _pre321ViewDragEndX = LoadIntSetting(nameof(Pre321ViewDragEndX), 0);
            _pre321ViewDragEndY = LoadIntSetting(nameof(Pre321ViewDragEndY), 0);
            _pre321ViewDragCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.Pre321ViewDragCalibrated), 0) != 0;
            OnPropertyChanged(nameof(Pre321ViewDragStartX));
            OnPropertyChanged(nameof(Pre321ViewDragStartY));
            OnPropertyChanged(nameof(Pre321ViewDragEndX));
            OnPropertyChanged(nameof(Pre321ViewDragEndY));
            RefreshPre321ViewDragDisplay();

            _pre321ViewClickWaitMs = LoadIntSetting(nameof(Pre321ViewClickWaitMs), 400);
            _pre321ViewDragStepDelayMs = LoadIntSetting(nameof(Pre321ViewDragStepDelayMs), 8);
            OnPropertyChanged(nameof(Pre321ViewClickWaitMs));
            OnPropertyChanged(nameof(Pre321ViewDragStepDelayMs));

            _mainWindowRememberSize =
                LoadIntSetting(nameof(MainWindowRememberSize), 0) != 0;
            _mainWindowWidth = LoadIntSetting(nameof(MainWindowWidth), 1280);
            _mainWindowHeight = LoadIntSetting(nameof(MainWindowHeight), 720);
            _mainWindowLeft = LoadIntSetting(nameof(MainWindowLeft), 80);
            _mainWindowTop = LoadIntSetting(nameof(MainWindowTop), 80);
            OnPropertyChanged(nameof(MainWindowRememberSize));
            OnPropertyChanged(nameof(MainWindowWidth));
            OnPropertyChanged(nameof(MainWindowHeight));
            OnPropertyChanged(nameof(MainWindowLeft));
            OnPropertyChanged(nameof(MainWindowTop));
            RefreshMainWindowSizeDisplay();

            _enableOfflineCtVisionAnnotation =
                LoadIntSetting(nameof(EnableOfflineCtVisionAnnotation), 0) != 0;
            OfflineCtModelPath = LoadStringSetting(nameof(OfflineCtModelPath), string.Empty);
            _offlineCtIsRunCuda = LoadIntSetting(nameof(OfflineCtIsRunCuda), 1) != 0;
            _offlineCtBlackHeight = LoadIntSetting(
                nameof(OfflineCtBlackHeight),
                CTAutoSlicerSettings.DefaultOfflineCtBlackHeight);
            _enableOfflineCtLRangeCheck =
                LoadIntSetting(nameof(EnableOfflineCtLRangeCheck), 0) != 0;
            _offlineCtL1MinMm = LoadDoubleSetting(
                nameof(OfflineCtL1MinMm), CTAutoSlicerSettings.DefaultOfflineCtLMinMm);
            _offlineCtL1MaxMm = LoadDoubleSetting(
                nameof(OfflineCtL1MaxMm), CTAutoSlicerSettings.DefaultOfflineCtLMaxMm);
            _offlineCtL2MinMm = LoadDoubleSetting(
                nameof(OfflineCtL2MinMm), CTAutoSlicerSettings.DefaultOfflineCtLMinMm);
            _offlineCtL2MaxMm = LoadDoubleSetting(
                nameof(OfflineCtL2MaxMm), CTAutoSlicerSettings.DefaultOfflineCtLMaxMm);
            _offlineCtL3MinMm = LoadDoubleSetting(
                nameof(OfflineCtL3MinMm), CTAutoSlicerSettings.DefaultOfflineCtLMinMm);
            _offlineCtL3MaxMm = LoadDoubleSetting(
                nameof(OfflineCtL3MaxMm), CTAutoSlicerSettings.DefaultOfflineCtLMaxMm);
            _offlineCtL4MinMm = LoadDoubleSetting(
                nameof(OfflineCtL4MinMm), CTAutoSlicerSettings.DefaultOfflineCtLMinMm);
            _offlineCtL4MaxMm = LoadDoubleSetting(
                nameof(OfflineCtL4MaxMm), CTAutoSlicerSettings.DefaultOfflineCtLMaxMm);
            _offlineCtL5MinMm = LoadDoubleSetting(
                nameof(OfflineCtL5MinMm), CTAutoSlicerSettings.DefaultOfflineCtLMinMm);
            _offlineCtL5MaxMm = LoadDoubleSetting(
                nameof(OfflineCtL5MaxMm), CTAutoSlicerSettings.DefaultOfflineCtLMaxMm);
            _offlineCtTopViewSliceIndex = Math.Clamp(
                LoadIntSetting(
                    nameof(OfflineCtTopViewSliceIndex),
                    CTAutoSlicerSettings.DefaultOfflineCtTopViewSliceIndex),
                CTAutoSlicerSettings.MinOfflineCtTopViewSliceIndex,
                CTAutoSlicerSettings.MaxOfflineCtTopViewSliceIndex);
            var loadedPixelSizeUm = LoadDoubleSetting(
                nameof(OfflineCtPixelSizeUm),
                CTAutoSlicerSettings.DefaultOfflineCtPixelSizeUm);
            _offlineCtPixelSizeUm = loadedPixelSizeUm > 0
                ? loadedPixelSizeUm
                : CTAutoSlicerSettings.DefaultOfflineCtPixelSizeUm;
            _offlineCtResultImageMode = LoadIntSetting(
                nameof(OfflineCtResultImageMode),
                (int)Core.Models.Project.CTAutoSlicer.OfflineCtResultImageMode.Enhance);
            _offlineCtExportJson = LoadIntSetting(nameof(OfflineCtExportJson), 1) != 0;
            _offlineCtExportCsv = LoadIntSetting(nameof(OfflineCtExportCsv), 1) != 0;
            _offlineCtExportInputForNjct = LoadIntSetting(nameof(OfflineCtExportInputForNjct), 0) != 0;
            _offlineCtRotateTopViewCw90 = LoadIntSetting(nameof(OfflineCtRotateTopViewCw90), 1) != 0;
            _enableOfflineCtPreBrighten = LoadIntSetting(nameof(EnableOfflineCtPreBrighten), 1) != 0;
            _offlineCtPreBrightenMeanTrigger = LoadDoubleSetting(nameof(OfflineCtPreBrightenMeanTrigger), 120.0);
            _offlineCtPreBrightenP50Trigger = LoadDoubleSetting(nameof(OfflineCtPreBrightenP50Trigger), 110.0);
            _offlineCtPreBrightenTargetMean = LoadDoubleSetting(nameof(OfflineCtPreBrightenTargetMean), 132.0);
            OfflineCtManualInputImagePath = LoadStringSetting(nameof(OfflineCtManualInputImagePath), string.Empty);
            OfflineCtManualOutputFolder = LoadStringSetting(nameof(OfflineCtManualOutputFolder), string.Empty);
            CutImgDllTestSectionFolder = LoadCutImgDllTestFolderSetting(
                nameof(CutImgDllTestSectionFolder),
                legacyKey: "CutImgDllTestSectionImagePath");
            CutImgDllTestVectorFolder = LoadCutImgDllTestFolderSetting(
                nameof(CutImgDllTestVectorFolder),
                legacyKey: "CutImgDllTestVectorImagePath");
            CutImgDllTestOutputDir = LoadStringSetting(
                nameof(CutImgDllTestOutputDir),
                CTAutoSlicerSettings.DefaultCutImgDllTestOutputDir);
            VisionLearn3dSampleDir = LoadStringSetting(
                nameof(VisionLearn3dSampleDir),
                CTAutoSlicerSettings.DefaultVisionLearn3dSampleDir);
            VisionLearnTopViewSampleDir = LoadStringSetting(
                nameof(VisionLearnTopViewSampleDir),
                CTAutoSlicerSettings.DefaultVisionLearnTopViewSampleDir);

            _themePageBgColor = NormalizeHexColor(LoadStringSetting(
                nameof(ThemePageBgColor),
                CTAutoSlicerSettings.DefaultThemePageBgColor));
            _themeOuterCardColor = NormalizeHexColor(LoadStringSetting(
                nameof(ThemeOuterCardColor),
                CTAutoSlicerSettings.DefaultThemeOuterCardColor));
            _themeInnerCardColor = NormalizeHexColor(LoadStringSetting(
                nameof(ThemeInnerCardColor),
                CTAutoSlicerSettings.DefaultThemeInnerCardColor));
            _themeSidebarColor = NormalizeHexColor(LoadStringSetting(
                nameof(ThemeSidebarColor),
                CTAutoSlicerSettings.DefaultThemeSidebarColor));
            _savedThemePageBgColor = _themePageBgColor;
            _savedThemeOuterCardColor = _themeOuterCardColor;
            _savedThemeInnerCardColor = _themeInnerCardColor;
            _savedThemeSidebarColor = _themeSidebarColor;
            HasUnsavedThemeChanges = false;
            LoadCustomThemePresets();
            LoadThemeEditFromSlot();
            OnPropertyChanged(nameof(ThemePageBgColor));
            OnPropertyChanged(nameof(ThemeOuterCardColor));
            OnPropertyChanged(nameof(ThemeInnerCardColor));
            OnPropertyChanged(nameof(ThemeSidebarColor));
            OnPropertyChanged(nameof(ThemePageBgBrush));
            OnPropertyChanged(nameof(ThemeOuterCardBrush));
            OnPropertyChanged(nameof(ThemeInnerCardBrush));
            OnPropertyChanged(nameof(ThemeSidebarBrush));
            ThemeColorsChanged?.Invoke(this, EventArgs.Empty);

            OnPropertyChanged(nameof(EnableOfflineCtVisionAnnotation));
            OnPropertyChanged(nameof(OfflineCtIsRunCuda));
            OnPropertyChanged(nameof(OfflineCtBlackHeight));
            OnPropertyChanged(nameof(EnableOfflineCtLRangeCheck));
            OnPropertyChanged(nameof(OfflineCtL1MinMm));
            OnPropertyChanged(nameof(OfflineCtL1MaxMm));
            OnPropertyChanged(nameof(OfflineCtL2MinMm));
            OnPropertyChanged(nameof(OfflineCtL2MaxMm));
            OnPropertyChanged(nameof(OfflineCtL3MinMm));
            OnPropertyChanged(nameof(OfflineCtL3MaxMm));
            OnPropertyChanged(nameof(OfflineCtL4MinMm));
            OnPropertyChanged(nameof(OfflineCtL4MaxMm));
            OnPropertyChanged(nameof(OfflineCtL5MinMm));
            OnPropertyChanged(nameof(OfflineCtL5MaxMm));
            OnPropertyChanged(nameof(OfflineCtTopViewSliceIndex));
            OnPropertyChanged(nameof(OfflineCtPixelSizeUm));
            OnPropertyChanged(nameof(OfflineCtPixelSizeMmPerPx));
            OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaUm));
            OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaMm));
            OnPropertyChanged(nameof(OfflineCtResultImageMode));
            OnPropertyChanged(nameof(OfflineCtResultImageModeDisplay));
            OnPropertyChanged(nameof(OfflineCtExportJson));
            OnPropertyChanged(nameof(OfflineCtExportCsv));
            OnPropertyChanged(nameof(OfflineCtExportInputForNjct));
            OnPropertyChanged(nameof(OfflineCtRotateTopViewCw90));
            OnPropertyChanged(nameof(EnableOfflineCtPreBrighten));
            OnPropertyChanged(nameof(OfflineCtPreBrightenMeanTrigger));
            OnPropertyChanged(nameof(OfflineCtPreBrightenP50Trigger));
            OnPropertyChanged(nameof(OfflineCtPreBrightenTargetMean));

            _isLoadingSettings = false;

            // 首次进入模块：设置加载完成后尝试恢复自定义主窗尺寸
            Dispatcher.BeginInvoke(new Action(TryApplyRememberedMainWindowSize), DispatcherPriority.Loaded);
        }

        private string LoadStringSetting(string name, string defaultValue)
        {
            EnsureSettingExists(name, defaultValue, "string", name);
            var value = _projectSettingRepository.GetSettingValue(name, VmName);
            return value?.ToString() ?? defaultValue;
        }

        private int LoadIntSetting(string name, int defaultValue)
        {
            EnsureSettingExists(name, defaultValue, "int", name);
            var value = _projectSettingRepository.GetSettingValue(name, VmName);
            return value == null ? defaultValue : Convert.ToInt32(value);
        }

        private double LoadDoubleSetting(string name, double defaultValue)
        {
            EnsureSettingExists(name, defaultValue, "double", name);
            var value = _projectSettingRepository.GetSettingValue(name, VmName);
            return value == null ? defaultValue : Convert.ToDouble(value);
        }

        private void EnsureSettingExists(string name, object defaultValue, string type, string remark)
        {
            if (!_projectSettingRepository.Exists(model => model.Name == name && model.BelongToVM == VmName))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(name, VmName, $"{defaultValue}", type, 1) { Remark = remark }
                );
            }
        }

        private void SavePipelineRunModeToLocalConfig(int mode)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            try
            {
                _localConfigService.SavePipelineRunMode(
                    (Core.Models.Project.CTAutoSlicer.PipelineRunMode)Math.Clamp(mode, 0, 1));
            }
            catch (Exception ex)
            {
                _logger.Error($"保存本机流水线模式失败：{CTAutoSlicerLocalConfigService.ConfigFilePath}", ex);
                MessageShower = $"保存本机流水线模式失败：{ex.Message}";
            }
        }

        private void SaveSetting(string name, object value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            // 键不存在时补建（类型用仓库可识别的短名，避免 Int32 导致下次加载失败）
            if (!_projectSettingRepository.Exists(model => model.Name == name && model.BelongToVM == VmName))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        name,
                        VmName,
                        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
                        ResolveSettingTypeName(value),
                        1) { Remark = name });
                return;
            }

            _projectSettingRepository.UpdateSettingValue(name, VmName, value);
        }

        /// <summary>与 EnsureSettingExists / DataTypeConverter 对齐的类型短名。</summary>
        private static string ResolveSettingTypeName(object value) =>
            value switch
            {
                int or long or short or byte or sbyte or uint or ulong or ushort => "int",
                double or float or decimal => "double",
                bool => "bool",
                _ => "string",
            };

        private CTAutoSlicerSettings BuildSettings()
        {
            return new CTAutoSlicerSettings
            {
                VGAppPath = VGAppPath,
                VGOpenWaitTime = VGOpenWaitTime,
                VGShowFrontWaitTime = _vgShowFrontWaitTime,
                CTFileFolder = CTFileFolder,
                MaxVgProcesses = MaxVgProcesses,
                QuickImportDialogWaitTime = QuickImportDialogWaitTime,
                NewProjectDialogWaitTime = NewProjectDialogWaitTime,
                FileSelectionDialogWaitTime = FileSelectionDialogWaitTime,
                ImportTimeoutMinutes = ImportTimeoutMinutes,
                PostImportWaitTime = PostImportWaitTime,
                StepRetryCount = StepRetryCount,
                StepRetryIntervalMs = StepRetryIntervalMs,
                RenderingPresetName = RenderingPresetName,
                PresetApplyWaitTime = PresetApplyWaitTime,
                PresetDropdownWaitTime = PresetDropdownWaitTime,
                ThickPlateMenuOffsetX = ThickPlateMenuOffsetX,
                ThickPlateMenuOffsetY = ThickPlateMenuOffsetY,
                ViewportClickOffsetY = ViewportClickOffsetY,
                ThickPlateStepDelayMs = _thickPlateStepDelayMs,
                ThickPlateDialogWaitTime = _thickPlateDialogWaitTime,
                ThickPlateDropdownWaitTime = _thickPlateDropdownWaitTime,
                ViewportQuadrant3ClickOffsetY = _viewportQuadrant3ClickOffsetY,
                ViewportMinWidth = _viewportMinWidth,
                ViewportMinHeight = _viewportMinHeight,
                ThickPlateStepRetryCount = _thickPlateStepRetryCount,
                ThickPlateMenuAfterRightClickDelayMs = _thickPlateMenuAfterRightClickDelayMs,
                SliceExportFolder = SliceExportFolder,
                TopViewHandoffFolder = TopViewHandoffFolder,
                PipelineRunMode = (Core.Models.Project.CTAutoSlicer.PipelineRunMode)Math.Clamp(
                    _pipelineRunMode,
                    0,
                    1),
                SliceExportImageWidth = SliceExportImageWidth,
                SliceExportQuantity = SliceExportQuantity,
                SliceExportQuantityQj = SliceExportQuantityQj,
                SliceRangeFrontStartMm = SliceRangeFrontStartMm,
                SliceRangeFrontEndMm = SliceRangeFrontEndMm,
                SliceRangeRightStartMm = SliceRangeRightStartMm,
                SliceRangeRightEndMm = SliceRangeRightEndMm,
                SliceRangeTopStartMm = SliceRangeTopStartMm,
                SliceRangeTopEndMm = SliceRangeTopEndMm,
                SliceExportDialogWaitTime = SliceExportDialogWaitTime,
                SliceExportWriteTimeoutMs = SliceExportWriteTimeoutMs,
                HistogramRoiX = HistogramRoiX,
                HistogramRoiY = HistogramRoiY,
                HistogramRoiWidth = HistogramRoiWidth,
                HistogramRoiHeight = HistogramRoiHeight,
                HistogramCaptureDir = HistogramCaptureDir,
                HistogramCaptureKeepDays = CTAutoSlicerSettings.DefaultCaptureKeepDays,
                HistogramMaskColor = HistogramMaskColor,
                HistogramMaskTolerance = HistogramMaskTolerance,
                LutSegmentPoint1X = LutSegmentPoint1X,
                LutSegmentPoint1Y = LutSegmentPoint1Y,
                LutSegmentPoint2X = LutSegmentPoint2X,
                LutSegmentPoint2Y = LutSegmentPoint2Y,
                LutSegmentPointsCalibrated = _lutSegmentPointsCalibrated,
                VisionCaptureDir = VisionCaptureDir,
                VisionCaptureKeepDays = CTAutoSlicerSettings.DefaultCaptureKeepDays,
                Vision2dVectorCaptureDir = Vision2dVectorCaptureDir,
                Vision2dVectorCaptureKeepDays = CTAutoSlicerSettings.DefaultCaptureKeepDays,
                Vision3dInitialCaptureDir = Vision3dInitialCaptureDir,
                Vision3dInitialCaptureKeepDays = CTAutoSlicerSettings.DefaultCaptureKeepDays,
                Vision3dInitialCaptureWaitMs = CTAutoSlicerSettings.DefaultVision3dInitialCaptureWaitMs,
                EnableVisionAlignmentSteps = true,
                Simple321PointSource = (Core.Models.Project.CTAutoSlicer.Simple321PointSource)Math.Clamp(
                    _simple321PointSource,
                    0,
                    1),
                Vision3dRoiX = Vision3dRoiX,
                Vision3dRoiY = Vision3dRoiY,
                Vision3dRoiWidth = Vision3dRoiWidth,
                Vision3dRoiHeight = Vision3dRoiHeight,
                VisionTopViewRoiX = VisionTopViewRoiX,
                VisionTopViewRoiY = VisionTopViewRoiY,
                VisionTopViewRoiWidth = VisionTopViewRoiWidth,
                VisionTopViewRoiHeight = VisionTopViewRoiHeight,
                Simple321ToolbarClickX = Simple321ToolbarClickX,
                Simple321ToolbarClickY = Simple321ToolbarClickY,
                Simple321ToolbarClickCalibrated = _simple321ToolbarClickCalibrated,
                Simple321OffsetEditX = Simple321OffsetEditX,
                Simple321OffsetEditY = Simple321OffsetEditY,
                Simple321OffsetEditCalibrated = _simple321OffsetEditCalibrated,
                PostAlignCaptureButtonX = PostAlignCaptureButtonX,
                PostAlignCaptureButtonY = PostAlignCaptureButtonY,
                PostAlignCaptureButtonCalibrated = _postAlignCaptureButtonCalibrated,
                PostAlignCaptureButtonIndex = PostAlignCaptureButtonIndex,
                PostAlignDragStartX = PostAlignDragStartX,
                PostAlignDragStartY = PostAlignDragStartY,
                PostAlignDragEndX = PostAlignDragEndX,
                PostAlignDragEndY = PostAlignDragEndY,
                PostAlignDragCalibrated = _postAlignDragCalibrated,
                EnablePostAlignCaptureDragSteps = EnablePostAlignCaptureDragSteps,
                PostAlignClickWaitMs = PostAlignClickWaitMs,
                PostAlignDragStepDelayMs = PostAlignDragStepDelayMs,
                Pre321ViewClickX = Pre321ViewClickX,
                Pre321ViewClickY = Pre321ViewClickY,
                Pre321ViewClickCalibrated = _pre321ViewClickCalibrated,
                Pre321ViewDragStartX = Pre321ViewDragStartX,
                Pre321ViewDragStartY = Pre321ViewDragStartY,
                Pre321ViewDragEndX = Pre321ViewDragEndX,
                Pre321ViewDragEndY = Pre321ViewDragEndY,
                Pre321ViewDragCalibrated = _pre321ViewDragCalibrated,
                Pre321ViewClickWaitMs = Pre321ViewClickWaitMs,
                Pre321ViewDragStepDelayMs = Pre321ViewDragStepDelayMs,
                MainWindowRememberSize = MainWindowRememberSize,
                MainWindowWidth = MainWindowWidth,
                MainWindowHeight = MainWindowHeight,
                MainWindowLeft = MainWindowLeft,
                MainWindowTop = MainWindowTop,
                EnableOfflineCtVisionAnnotation = EnableOfflineCtVisionAnnotation,
                OfflineCtModelPath = OfflineCtModelPath,
                OfflineCtIsRunCuda = OfflineCtIsRunCuda,
                OfflineCtBlackHeight = OfflineCtBlackHeight,
                EnableOfflineCtLRangeCheck = EnableOfflineCtLRangeCheck,
                OfflineCtL1MinMm = OfflineCtL1MinMm,
                OfflineCtL1MaxMm = OfflineCtL1MaxMm,
                OfflineCtL2MinMm = OfflineCtL2MinMm,
                OfflineCtL2MaxMm = OfflineCtL2MaxMm,
                OfflineCtL3MinMm = OfflineCtL3MinMm,
                OfflineCtL3MaxMm = OfflineCtL3MaxMm,
                OfflineCtL4MinMm = OfflineCtL4MinMm,
                OfflineCtL4MaxMm = OfflineCtL4MaxMm,
                OfflineCtL5MinMm = OfflineCtL5MinMm,
                OfflineCtL5MaxMm = OfflineCtL5MaxMm,
                OfflineCtTopViewSliceIndex = OfflineCtTopViewSliceIndex,
                OfflineCtPixelSizeUm = OfflineCtPixelSizeUm,
                OfflineCtResultImageMode = (Core.Models.Project.CTAutoSlicer.OfflineCtResultImageMode)Math.Clamp(
                    _offlineCtResultImageMode,
                    0,
                    2),
                OfflineCtExportJson = OfflineCtExportJson,
                OfflineCtExportCsv = OfflineCtExportCsv,
                OfflineCtExportInputForNjct = OfflineCtExportInputForNjct,
                OfflineCtRotateTopViewCw90 = OfflineCtRotateTopViewCw90,
                EnableOfflineCtPreBrighten = EnableOfflineCtPreBrighten,
                OfflineCtPreBrightenMeanTrigger = OfflineCtPreBrightenMeanTrigger,
                OfflineCtPreBrightenP50Trigger = OfflineCtPreBrightenP50Trigger,
                OfflineCtPreBrightenTargetMean = OfflineCtPreBrightenTargetMean,
                VisionClickIntervalMs = 300,
                VisionCaptureWaitMs = 2000,
                VisionAlignmentDialogWaitTime = 8000,
                DllDownloadBaseUrl = _dllDownloadBaseUrl,
            };
        }

        private void RefreshHistogramRoiDisplay()
        {
            HistogramRoiDisplayText = _histogramRoiWidth > 0 && _histogramRoiHeight > 0
                ? $"X={_histogramRoiX}, Y={_histogramRoiY}, W={_histogramRoiWidth}, H={_histogramRoiHeight}"
                : "未标定";
        }

        private void RefreshLutSegmentPointsDisplay()
        {
            if (!_lutSegmentPointsCalibrated)
            {
                LutSegmentPointsDisplayText = "未标定";
                return;
            }

            LutSegmentPointsDisplayText =
                $"P1=({_lutSegmentPoint1X},{_lutSegmentPoint1Y}) P2=({_lutSegmentPoint2X},{_lutSegmentPoint2Y})";
        }

        private void RefreshVision3dRoiDisplay()
        {
            Vision3dRoiDisplayText = _vision3dRoiWidth > 0 && _vision3dRoiHeight > 0
                ? $"X={_vision3dRoiX}, Y={_vision3dRoiY}, W={_vision3dRoiWidth}, H={_vision3dRoiHeight}"
                : "未标定";
        }

        private void RefreshVisionTopViewRoiDisplay()
        {
            VisionTopViewRoiDisplayText = _visionTopViewRoiWidth > 0 && _visionTopViewRoiHeight > 0
                ? $"X={_visionTopViewRoiX}, Y={_visionTopViewRoiY}, W={_visionTopViewRoiWidth}, H={_visionTopViewRoiHeight}"
                : "未标定";
        }

        private void SetRoiPreviewFlag(
            ref bool field,
            string propertyName,
            bool value,
            RoiPreviewKind kind)
        {
            if (_isUpdatingRoiPreviewFlags)
            {
                field = value;
                OnPropertyChanged(propertyName);
                return;
            }

            if (field == value)
            {
                return;
            }

            if (value)
            {
                if (!TryGetRoiRect(kind, out var rect, out var label, out var error))
                {
                    MessageShower = error;
                    field = false;
                    OnPropertyChanged(propertyName);
                    return;
                }

                // 互斥：只预览当前勾选的一块 ROI
                ClearOtherRoiPreviewFlags(kind);
                field = true;
                OnPropertyChanged(propertyName);
                ShowRoiPreviewOverlay(rect, label);
            }
            else
            {
                field = false;
                OnPropertyChanged(propertyName);
                CloseRoiPreviewOverlay(restoreMainWindow: true);
            }
        }

        private void ClearOtherRoiPreviewFlags(RoiPreviewKind keep)
        {
            _isUpdatingRoiPreviewFlags = true;
            try
            {
                if (keep != RoiPreviewKind.Histogram && _isPreviewHistogramRoi)
                {
                    _isPreviewHistogramRoi = false;
                    OnPropertyChanged(nameof(IsPreviewHistogramRoi));
                }

                if (keep != RoiPreviewKind.Vision3d && _isPreviewVision3dRoi)
                {
                    _isPreviewVision3dRoi = false;
                    OnPropertyChanged(nameof(IsPreviewVision3dRoi));
                }

                if (keep != RoiPreviewKind.VisionTopView && _isPreviewVisionTopViewRoi)
                {
                    _isPreviewVisionTopViewRoi = false;
                    OnPropertyChanged(nameof(IsPreviewVisionTopViewRoi));
                }
            }
            finally
            {
                _isUpdatingRoiPreviewFlags = false;
            }
        }

        private bool TryGetRoiRect(
            RoiPreviewKind kind,
            out System.Drawing.Rectangle rect,
            out string label,
            out string error)
        {
            rect = default;
            label = string.Empty;
            error = string.Empty;

            switch (kind)
            {
                case RoiPreviewKind.Histogram:
                    if (_histogramRoiWidth <= 0 || _histogramRoiHeight <= 0)
                    {
                        error = "直方图 ROI 尚未标定，无法预览";
                        return false;
                    }

                    rect = new System.Drawing.Rectangle(
                        _histogramRoiX, _histogramRoiY, _histogramRoiWidth, _histogramRoiHeight);
                    label = "直方图 ROI";
                    return true;

                case RoiPreviewKind.Vision3d:
                    if (_vision3dRoiWidth <= 0 || _vision3dRoiHeight <= 0)
                    {
                        error = "3D ROI 尚未标定，无法预览";
                        return false;
                    }

                    rect = new System.Drawing.Rectangle(
                        _vision3dRoiX, _vision3dRoiY, _vision3dRoiWidth, _vision3dRoiHeight);
                    label = "3D 视图 ROI";
                    return true;

                case RoiPreviewKind.VisionTopView:
                    if (_visionTopViewRoiWidth <= 0 || _visionTopViewRoiHeight <= 0)
                    {
                        error = "「上」视图 ROI 尚未标定，无法预览";
                        return false;
                    }

                    rect = new System.Drawing.Rectangle(
                        _visionTopViewRoiX, _visionTopViewRoiY, _visionTopViewRoiWidth, _visionTopViewRoiHeight);
                    label = "「上」视图 ROI";
                    return true;

                default:
                    error = "未知 ROI 类型";
                    return false;
            }
        }

        private void ShowRoiPreviewOverlay(System.Drawing.Rectangle rect, string label)
        {
            CloseRoiPreviewOverlay(restoreMainWindow: false);

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null && mainWindow.WindowState != WindowState.Minimized)
            {
                _roiPreviewPreviousMainWindowState = mainWindow.WindowState;
                mainWindow.WindowState = WindowState.Minimized;
                Thread.Sleep(250);
            }

            var overlay = new RoiPreviewOverlayWindow(rect, label);
            _activeRoiPreviewOverlay = overlay;
            overlay.Closed += (_, _) =>
            {
                if (!ReferenceEquals(_activeRoiPreviewOverlay, overlay))
                {
                    return;
                }

                _activeRoiPreviewOverlay = null;
                _isUpdatingRoiPreviewFlags = true;
                try
                {
                    _isPreviewHistogramRoi = false;
                    _isPreviewVision3dRoi = false;
                    _isPreviewVisionTopViewRoi = false;
                    OnPropertyChanged(nameof(IsPreviewHistogramRoi));
                    OnPropertyChanged(nameof(IsPreviewVision3dRoi));
                    OnPropertyChanged(nameof(IsPreviewVisionTopViewRoi));
                }
                finally
                {
                    _isUpdatingRoiPreviewFlags = false;
                }

                RestoreMainWindowAfterRoiPreview();
            };

            overlay.Show();
        }

        private void CloseRoiPreviewOverlay(bool restoreMainWindow)
        {
            if (_activeRoiPreviewOverlay == null)
            {
                if (restoreMainWindow)
                {
                    RestoreMainWindowAfterRoiPreview();
                }

                return;
            }

            var overlay = _activeRoiPreviewOverlay;
            _activeRoiPreviewOverlay = null;
            try
            {
                overlay.Close();
            }
            catch
            {
                // 忽略关闭异常
            }

            if (restoreMainWindow)
            {
                RestoreMainWindowAfterRoiPreview();
            }
        }

        private void RestoreMainWindowAfterRoiPreview()
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null || _roiPreviewPreviousMainWindowState is not { } previous)
            {
                _roiPreviewPreviousMainWindowState = null;
                return;
            }

            _roiPreviewPreviousMainWindowState = null;
            mainWindow.WindowState = previous;
            mainWindow.Activate();
        }

        private void RefreshMainWindowSizeDisplay()
        {
            MainWindowSizeDisplayText = _mainWindowRememberSize
                ? $"已启用沿用：{_mainWindowWidth}×{_mainWindowHeight} @ ({_mainWindowLeft},{_mainWindowTop})"
                : $"未启用沿用（参数 {_mainWindowWidth}×{_mainWindowHeight}）；启动仍默认全屏";
        }

        /// <summary>按参数区宽高立即调整主窗（不强制改 Remember 开关）。</summary>
        private void ExecuteApplyMainWindowSize()
        {
            try
            {
                ApplyMainWindowBounds(
                    MainWindowWidth,
                    MainWindowHeight,
                    MainWindowLeft,
                    MainWindowTop);
                MessageShower = $"已应用窗体大小：{MainWindowWidth}×{MainWindowHeight}";
                AppendFlowLog(MessageShower);
            }
            catch (Exception ex)
            {
                MessageShower = $"应用窗体大小失败：{ex.Message}";
                _logger.Error("应用窗体大小失败", ex);
            }
        }

        /// <summary>读取当前主窗实际尺寸并持久化，下次进入本项目自动沿用。</summary>
        private void ExecuteSaveCurrentMainWindowSize()
        {
            try
            {
                if (!TryReadMainWindowBounds(out var width, out var height, out var left, out var top))
                {
                    MessageShower = "无法读取主窗尺寸";
                    return;
                }

                EnsureSettingExists(nameof(MainWindowRememberSize), 0, "int", nameof(MainWindowRememberSize));
                EnsureSettingExists(nameof(MainWindowWidth), 1280, "int", nameof(MainWindowWidth));
                EnsureSettingExists(nameof(MainWindowHeight), 720, "int", nameof(MainWindowHeight));
                EnsureSettingExists(nameof(MainWindowLeft), 80, "int", nameof(MainWindowLeft));
                EnsureSettingExists(nameof(MainWindowTop), 80, "int", nameof(MainWindowTop));

                _isLoadingSettings = true;
                _mainWindowWidth = Math.Max(800, width);
                _mainWindowHeight = Math.Max(450, height);
                _mainWindowLeft = left;
                _mainWindowTop = top;
                _mainWindowRememberSize = true;
                OnPropertyChanged(nameof(MainWindowWidth));
                OnPropertyChanged(nameof(MainWindowHeight));
                OnPropertyChanged(nameof(MainWindowLeft));
                OnPropertyChanged(nameof(MainWindowTop));
                OnPropertyChanged(nameof(MainWindowRememberSize));
                RefreshMainWindowSizeDisplay();
                _isLoadingSettings = false;

                SaveSetting(nameof(MainWindowWidth), _mainWindowWidth);
                SaveSetting(nameof(MainWindowHeight), _mainWindowHeight);
                SaveSetting(nameof(MainWindowLeft), _mainWindowLeft);
                SaveSetting(nameof(MainWindowTop), _mainWindowTop);
                SaveSetting(nameof(MainWindowRememberSize), 1);

                ApplyMainWindowBounds(_mainWindowWidth, _mainWindowHeight, _mainWindowLeft, _mainWindowTop);
                MessageShower =
                    $"已保存当前窗体大小：{_mainWindowWidth}×{_mainWindowHeight} @ ({_mainWindowLeft},{_mainWindowTop})，下次启动本项目将沿用";
                AppendFlowLog(MessageShower);
                _logger.Info(MessageShower);
            }
            catch (Exception ex)
            {
                _isLoadingSettings = false;
                MessageShower = $"保存窗体大小失败：{ex.Message}";
                _logger.Error("保存窗体大小失败", ex);
            }
        }

        /// <summary>关闭自定义尺寸沿用，并恢复默认最大化。</summary>
        private void ExecuteRestoreDefaultMainWindowSize()
        {
            try
            {
                MainWindowRememberSize = false;
                TryRestoreDefaultMainWindowMaximized();
                MessageShower = "已恢复默认全屏，并关闭下次启动沿用";
                AppendFlowLog(MessageShower);
            }
            catch (Exception ex)
            {
                MessageShower = $"恢复默认全屏失败：{ex.Message}";
                _logger.Error("恢复默认全屏失败", ex);
            }
        }

        private void TryApplyRememberedMainWindowSize()
        {
            if (!_mainWindowRememberSize)
            {
                return;
            }

            try
            {
                ApplyMainWindowBounds(
                    _mainWindowWidth,
                    _mainWindowHeight,
                    _mainWindowLeft,
                    _mainWindowTop);
                _logger.Info(
                    $"已按项目设置恢复主窗：{_mainWindowWidth}×{_mainWindowHeight} @ ({_mainWindowLeft},{_mainWindowTop})");
            }
            catch (Exception ex)
            {
                _logger.Warn($"恢复主窗尺寸失败：{ex.Message}");
            }
        }

        private static void TryRestoreDefaultMainWindowMaximized()
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null)
            {
                return;
            }

            mainWindow.WindowState = WindowState.Maximized;
        }

        private static bool TryReadMainWindowBounds(
            out int width,
            out int height,
            out int left,
            out int top)
        {
            width = 0;
            height = 0;
            left = 0;
            top = 0;
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null)
            {
                return false;
            }

            Rect bounds;
            if (mainWindow.WindowState == WindowState.Maximized
                || mainWindow.WindowState == WindowState.Minimized)
            {
                bounds = mainWindow.RestoreBounds;
                if (bounds.Width < 1 || bounds.Height < 1)
                {
                    bounds = new Rect(
                        mainWindow.Left,
                        mainWindow.Top,
                        mainWindow.ActualWidth > 0 ? mainWindow.ActualWidth : mainWindow.Width,
                        mainWindow.ActualHeight > 0 ? mainWindow.ActualHeight : mainWindow.Height);
                }
            }
            else
            {
                bounds = new Rect(
                    mainWindow.Left,
                    mainWindow.Top,
                    mainWindow.ActualWidth > 0 ? mainWindow.ActualWidth : mainWindow.Width,
                    mainWindow.ActualHeight > 0 ? mainWindow.ActualHeight : mainWindow.Height);
            }

            width = Math.Max(800, (int)Math.Round(bounds.Width));
            height = Math.Max(450, (int)Math.Round(bounds.Height));
            left = (int)Math.Round(bounds.Left);
            top = (int)Math.Round(bounds.Top);
            return true;
        }

        private static void ApplyMainWindowBounds(int width, int height, int left, int top)
        {
            var mainWindow = Application.Current?.MainWindow
                ?? throw new InvalidOperationException("主窗未就绪");

            var w = Math.Max(800, width);
            var h = Math.Max(450, height);

            // 先切 Normal，再设尺寸；避免 Maximized 下改 Width/Height 无效
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Width = w;
            mainWindow.Height = h;
            mainWindow.Left = left;
            mainWindow.Top = top;

            // 尽量落在当前屏幕工作区内，避免拖出屏幕
            try
            {
                var work = SystemParameters.WorkArea;
                if (mainWindow.Left + mainWindow.Width > work.Right)
                {
                    mainWindow.Left = Math.Max(work.Left, work.Right - mainWindow.Width);
                }

                if (mainWindow.Top + mainWindow.Height > work.Bottom)
                {
                    mainWindow.Top = Math.Max(work.Top, work.Bottom - mainWindow.Height);
                }

                if (mainWindow.Left < work.Left)
                {
                    mainWindow.Left = work.Left;
                }

                if (mainWindow.Top < work.Top)
                {
                    mainWindow.Top = work.Top;
                }
            }
            catch
            {
                // 忽略屏幕边界校正失败
            }

            mainWindow.Activate();
        }

        private void RefreshSimple321ToolbarClickDisplay()
        {
            Simple321ToolbarClickDisplayText = _simple321ToolbarClickCalibrated
                ? $"X={_simple321ToolbarClickX}, Y={_simple321ToolbarClickY}"
                : "未标定";
        }

        private void RefreshSimple321OffsetEditDisplay()
        {
            Simple321OffsetEditDisplayText = _simple321OffsetEditCalibrated
                ? $"X={_simple321OffsetEditX}, Y={_simple321OffsetEditY}"
                : "未标定";
        }

        private void RefreshPostAlignCaptureButtonDisplay()
        {
            PostAlignCaptureButtonDisplayText = _postAlignCaptureButtonCalibrated
                ? $"X={_postAlignCaptureButtonX}, Y={_postAlignCaptureButtonY}（UIA 索引={_postAlignCaptureButtonIndex}）"
                : "未标定";
        }

        private void RefreshPostAlignDragDisplay()
        {
            if (!_postAlignDragCalibrated)
            {
                PostAlignDragDisplayText = "未标定";
                return;
            }

            var dx = _postAlignDragEndX - _postAlignDragStartX;
            var dy = _postAlignDragEndY - _postAlignDragStartY;
            var len = Math.Sqrt(dx * dx + dy * dy);
            PostAlignDragDisplayText =
                $"({_postAlignDragStartX},{_postAlignDragStartY}) → ({_postAlignDragEndX},{_postAlignDragEndY}) Δ=({dx},{dy}) L={len:F0}px";
        }

        private void RefreshPre321ViewClickDisplay()
        {
            Pre321ViewClickDisplayText = _pre321ViewClickCalibrated
                ? $"X={_pre321ViewClickX}, Y={_pre321ViewClickY}"
                : "未标定";
        }

        private void RefreshPre321ViewDragDisplay()
        {
            if (!_pre321ViewDragCalibrated)
            {
                Pre321ViewDragDisplayText = "未标定";
                return;
            }

            var dx = _pre321ViewDragEndX - _pre321ViewDragStartX;
            var dy = _pre321ViewDragEndY - _pre321ViewDragStartY;
            var len = Math.Sqrt(dx * dx + dy * dy);
            Pre321ViewDragDisplayText =
                $"({_pre321ViewDragStartX},{_pre321ViewDragStartY}) → ({_pre321ViewDragEndX},{_pre321ViewDragEndY}) Δ=({dx},{dy}) L={len:F0}px";
        }

        private static string NormalizeMaskColor(string? value)
        {
            if (HistogramGrayScaleAnalyzer.TryParseHexColor(value, out var c))
            {
                return HistogramGrayScaleAnalyzer.ToHex(c);
            }

            return string.IsNullOrWhiteSpace(value) ? "#6E6E6E" : value.Trim();
        }

        private void RefreshMaskColorBrush()
        {
            if (HistogramGrayScaleAnalyzer.TryParseHexColor(_histogramMaskColor, out var c))
            {
                HistogramMaskColorBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(c.R, c.G, c.B));
            }
        }

        private void ExecutePickHistogramMaskColor(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再取色";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenColorPickerOverlayWindow();
                    var ok = picker.ShowDialog() == true;
                    if (!ok || string.IsNullOrWhiteSpace(picker.SelectedHex))
                    {
                        MessageShower = "已取消掩膜取色";
                        AppendFlowLog("直方图掩膜取色已取消");
                        return;
                    }

                    HistogramMaskColor = picker.SelectedHex;
                    var msg = $"直方图掩膜颜色已取色：{HistogramMaskColor}";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"掩膜取色失败：{ex.Message}";
                AppendFlowLog(MessageShower);
                _logger.Error("直方图掩膜取色失败", ex);
            }
        }

        private void ExecuteCalibrateHistogramRoi(object? _)
        {
            ExecuteCalibrateScreenRoi(
                regionName: "直方图 ROI",
                windowTitle: "直方图 ROI 标定",
                hintText: "拖拽框选直方图区域；框下方 ✓ 提交 / ✕ 重选；Esc 取消标定",
                confirmBarText: "确认框选直方图区域",
                applyRect: rect =>
                {
                    _histogramRoiX = rect.X;
                    _histogramRoiY = rect.Y;
                    _histogramRoiWidth = rect.Width;
                    _histogramRoiHeight = rect.Height;
                    OnPropertyChanged(nameof(HistogramRoiX));
                    OnPropertyChanged(nameof(HistogramRoiY));
                    OnPropertyChanged(nameof(HistogramRoiWidth));
                    OnPropertyChanged(nameof(HistogramRoiHeight));
                    RefreshHistogramRoiDisplay();
                },
                persistRect: rect =>
                {
                    SaveSetting(nameof(HistogramRoiX), rect.X);
                    SaveSetting(nameof(HistogramRoiY), rect.Y);
                    SaveSetting(nameof(HistogramRoiWidth), rect.Width);
                    SaveSetting(nameof(HistogramRoiHeight), rect.Height);
                });
        }

        /// <summary>
        /// 一次性标定 LUT 预设关键段两端点（应用预设后在直方图折线上点选）。
        /// </summary>
        private void ExecuteCalibrateLutSegmentPoints(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenTwoPointPickerOverlayWindow(
                        "第 1 步：对准 LUT 预设关键段的第一个控制点（空心圆），左键确认；右键/Esc 取消",
                        "第 2 步：对准同一关键段的第二个控制点，左键确认；右键/Esc 取消");
                    var ok = picker.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消 LUT 关键段两点标定";
                        AppendFlowLog("LUT 关键段两点标定已取消");
                        return;
                    }

                    if (picker.Point1X == picker.Point2X && picker.Point1Y == picker.Point2Y)
                    {
                        MessageShower = "两端点重合，请重新标定两个不同的控制点";
                        AppendFlowLog("LUT 关键段两点标定失败：两端点重合");
                        return;
                    }

                    EnsureSettingExists(nameof(LutSegmentPoint1X), 0, "int", nameof(LutSegmentPoint1X));
                    EnsureSettingExists(nameof(LutSegmentPoint1Y), 0, "int", nameof(LutSegmentPoint1Y));
                    EnsureSettingExists(nameof(LutSegmentPoint2X), 0, "int", nameof(LutSegmentPoint2X));
                    EnsureSettingExists(nameof(LutSegmentPoint2Y), 0, "int", nameof(LutSegmentPoint2Y));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.LutSegmentPointsCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.LutSegmentPointsCalibrated));

                    _isLoadingSettings = true;
                    _lutSegmentPoint1X = picker.Point1X;
                    _lutSegmentPoint1Y = picker.Point1Y;
                    _lutSegmentPoint2X = picker.Point2X;
                    _lutSegmentPoint2Y = picker.Point2Y;
                    _lutSegmentPointsCalibrated = true;
                    OnPropertyChanged(nameof(LutSegmentPoint1X));
                    OnPropertyChanged(nameof(LutSegmentPoint1Y));
                    OnPropertyChanged(nameof(LutSegmentPoint2X));
                    OnPropertyChanged(nameof(LutSegmentPoint2Y));
                    RefreshLutSegmentPointsDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(LutSegmentPoint1X), picker.Point1X);
                    SaveSetting(nameof(LutSegmentPoint1Y), picker.Point1Y);
                    SaveSetting(nameof(LutSegmentPoint2X), picker.Point2X);
                    SaveSetting(nameof(LutSegmentPoint2Y), picker.Point2Y);
                    SaveSetting(nameof(CTAutoSlicerSettings.LutSegmentPointsCalibrated), 1);

                    var midX = (picker.Point1X + picker.Point2X) / 2;
                    var midY = (picker.Point1Y + picker.Point2Y) / 2;
                    var msg =
                        $"LUT 关键段两点已标定：P1=({picker.Point1X},{picker.Point1Y}) " +
                        $"P2=({picker.Point2X},{picker.Point2Y}) 中点=({midX},{midY})";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"LUT 关键段两点标定失败：{ex.Message}";
                _logger.Error("LUT 关键段两点标定失败", ex);
            }
        }

        private void ExecuteCalibrateVision3dRoi(object? _)
        {
            ExecuteCalibrateScreenRoi(
                regionName: "3D 视图 ROI",
                windowTitle: "3D 视图 ROI 标定",
                hintText: "拖拽框选第四象限 3D 视图截图区域；框下方 ✓ 提交 / ✕ 重选；Esc 取消标定",
                confirmBarText: "确认框选 3D 视图区域",
                applyRect: rect =>
                {
                    _vision3dRoiX = rect.X;
                    _vision3dRoiY = rect.Y;
                    _vision3dRoiWidth = rect.Width;
                    _vision3dRoiHeight = rect.Height;
                    OnPropertyChanged(nameof(Vision3dRoiX));
                    OnPropertyChanged(nameof(Vision3dRoiY));
                    OnPropertyChanged(nameof(Vision3dRoiWidth));
                    OnPropertyChanged(nameof(Vision3dRoiHeight));
                    RefreshVision3dRoiDisplay();
                },
                persistRect: rect =>
                {
                    SaveSetting(nameof(Vision3dRoiX), rect.X);
                    SaveSetting(nameof(Vision3dRoiY), rect.Y);
                    SaveSetting(nameof(Vision3dRoiWidth), rect.Width);
                    SaveSetting(nameof(Vision3dRoiHeight), rect.Height);
                });
        }

        private void ExecuteCalibrateVisionTopViewRoi(object? _)
        {
            ExecuteCalibrateScreenRoi(
                regionName: "「上」视图 ROI",
                windowTitle: "「上」视图 ROI 标定",
                hintText: "拖拽框选第二象限「上」视图截图区域；框下方 ✓ 提交 / ✕ 重选；Esc 取消标定",
                confirmBarText: "确认框选「上」视图区域",
                applyRect: rect =>
                {
                    _visionTopViewRoiX = rect.X;
                    _visionTopViewRoiY = rect.Y;
                    _visionTopViewRoiWidth = rect.Width;
                    _visionTopViewRoiHeight = rect.Height;
                    OnPropertyChanged(nameof(VisionTopViewRoiX));
                    OnPropertyChanged(nameof(VisionTopViewRoiY));
                    OnPropertyChanged(nameof(VisionTopViewRoiWidth));
                    OnPropertyChanged(nameof(VisionTopViewRoiHeight));
                    RefreshVisionTopViewRoiDisplay();
                },
                persistRect: rect =>
                {
                    SaveSetting(nameof(VisionTopViewRoiX), rect.X);
                    SaveSetting(nameof(VisionTopViewRoiY), rect.Y);
                    SaveSetting(nameof(VisionTopViewRoiWidth), rect.Width);
                    SaveSetting(nameof(VisionTopViewRoiHeight), rect.Height);
                });
        }

        /// <summary>
        /// 标定测量工具栏「简单对齐」下拉的屏幕绝对点击位置（Qt 控件 UIA 不可靠）。
        /// </summary>
        private void ExecuteCalibrateSimple321ToolbarClick(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenPointPickerOverlayWindow(
                        "请将十字准星对准 VG 测量工具栏「简单对齐」下拉按钮中心，左键确认；右键/Esc 取消");
                    var ok = picker.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消「简单对齐」下拉标定";
                        AppendFlowLog("「简单对齐」下拉点击标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(Simple321ToolbarClickX), 0, "int", nameof(Simple321ToolbarClickX));
                    EnsureSettingExists(nameof(Simple321ToolbarClickY), 0, "int", nameof(Simple321ToolbarClickY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.Simple321ToolbarClickCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.Simple321ToolbarClickCalibrated));

                    _isLoadingSettings = true;
                    _simple321ToolbarClickX = picker.SelectedX;
                    _simple321ToolbarClickY = picker.SelectedY;
                    _simple321ToolbarClickCalibrated = true;
                    OnPropertyChanged(nameof(Simple321ToolbarClickX));
                    OnPropertyChanged(nameof(Simple321ToolbarClickY));
                    RefreshSimple321ToolbarClickDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(Simple321ToolbarClickX), picker.SelectedX);
                    SaveSetting(nameof(Simple321ToolbarClickY), picker.SelectedY);
                    SaveSetting(nameof(CTAutoSlicerSettings.Simple321ToolbarClickCalibrated), 1);

                    var msg =
                        $"「简单对齐」下拉已标定：X={picker.SelectedX}, Y={picker.SelectedY}";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"「简单对齐」下拉标定失败：{ex.Message}";
                _logger.Error("「简单对齐」下拉点击标定失败", ex);
            }
        }

        /// <summary>
        /// 标定三点定平面后、向量两点前左侧无名偏移 Edit（常显示「x.xx mm」）的点击位置。
        /// </summary>
        private void ExecuteCalibrateSimple321OffsetEdit(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenPointPickerOverlayWindow(
                        "请对准三点后左侧显示「x.xx mm」的无名编辑框中心，左键确认；右键/Esc 取消（置 0 后「上」视图才正确）");
                    var ok = picker.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消偏移 Edit 标定";
                        AppendFlowLog("三点后偏移 Edit 标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(Simple321OffsetEditX), 0, "int", nameof(Simple321OffsetEditX));
                    EnsureSettingExists(nameof(Simple321OffsetEditY), 0, "int", nameof(Simple321OffsetEditY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.Simple321OffsetEditCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.Simple321OffsetEditCalibrated));

                    _isLoadingSettings = true;
                    _simple321OffsetEditX = picker.SelectedX;
                    _simple321OffsetEditY = picker.SelectedY;
                    _simple321OffsetEditCalibrated = true;
                    OnPropertyChanged(nameof(Simple321OffsetEditX));
                    OnPropertyChanged(nameof(Simple321OffsetEditY));
                    RefreshSimple321OffsetEditDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(Simple321OffsetEditX), picker.SelectedX);
                    SaveSetting(nameof(Simple321OffsetEditY), picker.SelectedY);
                    SaveSetting(nameof(CTAutoSlicerSettings.Simple321OffsetEditCalibrated), 1);

                    var msg = $"偏移 Edit 已标定：X={picker.SelectedX}, Y={picker.SelectedY}";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"偏移 Edit 标定失败：{ex.Message}";
                _logger.Error("三点后偏移 Edit 标定失败", ex);
            }
        }

        /// <summary>
        /// 标定 3-2-1 完成后、导出前「3D图正视图切换按钮」的屏幕绝对点击位置（UIA 匹配 + 兜底坐标）。
        /// </summary>
        private void ExecuteCalibratePostAlignCaptureButton(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenPointPickerOverlayWindow(
                        "请将十字准星对准 VG「3D图正视图切换按钮」中心，左键确认；右键/Esc 取消");
                    var ok = picker.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消按钮位置标定";
                        AppendFlowLog("对齐后拖拽：按钮位置标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(PostAlignCaptureButtonX), 0, "int", nameof(PostAlignCaptureButtonX));
                    EnsureSettingExists(nameof(PostAlignCaptureButtonY), 0, "int", nameof(PostAlignCaptureButtonY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.PostAlignCaptureButtonCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.PostAlignCaptureButtonCalibrated));
                    EnsureSettingExists(nameof(PostAlignCaptureButtonIndex), 0, "int", nameof(PostAlignCaptureButtonIndex));

                    _isLoadingSettings = true;
                    _postAlignCaptureButtonX = picker.SelectedX;
                    _postAlignCaptureButtonY = picker.SelectedY;
                    _postAlignCaptureButtonCalibrated = true;
                    OnPropertyChanged(nameof(PostAlignCaptureButtonX));
                    OnPropertyChanged(nameof(PostAlignCaptureButtonY));
                    RefreshPostAlignCaptureButtonDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(PostAlignCaptureButtonX), picker.SelectedX);
                    SaveSetting(nameof(PostAlignCaptureButtonY), picker.SelectedY);
                    SaveSetting(nameof(CTAutoSlicerSettings.PostAlignCaptureButtonCalibrated), 1);

                    var msg = $"3D图正视图切换按钮位置已标定：X={picker.SelectedX}, Y={picker.SelectedY}";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"按钮位置标定失败：{ex.Message}";
                _logger.Error("对齐后拖拽：按钮位置标定失败", ex);
            }
        }

        /// <summary>
        /// 标定点击 3D图正视图切换按钮后的鼠标拖拽方向与距离（全屏箭头）。
        /// </summary>
        private void ExecuteCalibratePostAlignDragArrow(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var overlay = new ScreenArrowCalibrationOverlayWindow(
                        "在 VG 上按住左键拖拽 3D 图：起点→终点；需拖至可同时看见3个面；Esc/右键取消");
                    var ok = overlay.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消拖拽箭头标定";
                        AppendFlowLog("对齐后拖拽箭头标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(PostAlignDragStartX), 0, "int", nameof(PostAlignDragStartX));
                    EnsureSettingExists(nameof(PostAlignDragStartY), 0, "int", nameof(PostAlignDragStartY));
                    EnsureSettingExists(nameof(PostAlignDragEndX), 0, "int", nameof(PostAlignDragEndX));
                    EnsureSettingExists(nameof(PostAlignDragEndY), 0, "int", nameof(PostAlignDragEndY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.PostAlignDragCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.PostAlignDragCalibrated));

                    _isLoadingSettings = true;
                    _postAlignDragStartX = overlay.StartX;
                    _postAlignDragStartY = overlay.StartY;
                    _postAlignDragEndX = overlay.EndX;
                    _postAlignDragEndY = overlay.EndY;
                    _postAlignDragCalibrated = true;
                    OnPropertyChanged(nameof(PostAlignDragStartX));
                    OnPropertyChanged(nameof(PostAlignDragStartY));
                    OnPropertyChanged(nameof(PostAlignDragEndX));
                    OnPropertyChanged(nameof(PostAlignDragEndY));
                    RefreshPostAlignDragDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(PostAlignDragStartX), overlay.StartX);
                    SaveSetting(nameof(PostAlignDragStartY), overlay.StartY);
                    SaveSetting(nameof(PostAlignDragEndX), overlay.EndX);
                    SaveSetting(nameof(PostAlignDragEndY), overlay.EndY);
                    SaveSetting(nameof(CTAutoSlicerSettings.PostAlignDragCalibrated), 1);

                    var msg =
                        $"拖拽箭头已标定：({overlay.StartX},{overlay.StartY}) → ({overlay.EndX},{overlay.EndY})";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"拖拽箭头标定失败：{ex.Message}";
                _logger.Error("对齐后拖拽箭头标定失败", ex);
            }
        }

        /// <summary>
        /// 标定 3D 找点前「切换视图」屏幕绝对点击位置。
        /// </summary>
        private void ExecuteCalibratePre321ViewClick(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenPointPickerOverlayWindow(
                        "请将十字准星对准「切换视图」按钮中心，左键确认；右键/Esc 取消");
                    var ok = picker.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消切换视图点击标定";
                        AppendFlowLog("3D找点前视角：切换视图点击标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(Pre321ViewClickX), 0, "int", nameof(Pre321ViewClickX));
                    EnsureSettingExists(nameof(Pre321ViewClickY), 0, "int", nameof(Pre321ViewClickY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.Pre321ViewClickCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.Pre321ViewClickCalibrated));

                    _isLoadingSettings = true;
                    _pre321ViewClickX = picker.SelectedX;
                    _pre321ViewClickY = picker.SelectedY;
                    _pre321ViewClickCalibrated = true;
                    OnPropertyChanged(nameof(Pre321ViewClickX));
                    OnPropertyChanged(nameof(Pre321ViewClickY));
                    RefreshPre321ViewClickDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(Pre321ViewClickX), picker.SelectedX);
                    SaveSetting(nameof(Pre321ViewClickY), picker.SelectedY);
                    SaveSetting(nameof(CTAutoSlicerSettings.Pre321ViewClickCalibrated), 1);

                    var msg = $"切换视图点击已标定：X={picker.SelectedX}, Y={picker.SelectedY}";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"切换视图点击标定失败：{ex.Message}";
                _logger.Error("3D找点前视角：切换视图点击标定失败", ex);
            }
        }

        /// <summary>
        /// 标定 3D 找点前视角拖拽方向与距离（全屏箭头）。
        /// </summary>
        private void ExecuteCalibratePre321ViewDragArrow(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var overlay = new ScreenArrowCalibrationOverlayWindow(
                        "在 VG 上按住左键拖拽 3D 图：起点→终点；拖至可清晰看见上平面；Esc/右键取消");
                    var ok = overlay.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消找点前拖拽箭头标定";
                        AppendFlowLog("3D找点前视角：拖拽箭头标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(Pre321ViewDragStartX), 0, "int", nameof(Pre321ViewDragStartX));
                    EnsureSettingExists(nameof(Pre321ViewDragStartY), 0, "int", nameof(Pre321ViewDragStartY));
                    EnsureSettingExists(nameof(Pre321ViewDragEndX), 0, "int", nameof(Pre321ViewDragEndX));
                    EnsureSettingExists(nameof(Pre321ViewDragEndY), 0, "int", nameof(Pre321ViewDragEndY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.Pre321ViewDragCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.Pre321ViewDragCalibrated));

                    _isLoadingSettings = true;
                    _pre321ViewDragStartX = overlay.StartX;
                    _pre321ViewDragStartY = overlay.StartY;
                    _pre321ViewDragEndX = overlay.EndX;
                    _pre321ViewDragEndY = overlay.EndY;
                    _pre321ViewDragCalibrated = true;
                    OnPropertyChanged(nameof(Pre321ViewDragStartX));
                    OnPropertyChanged(nameof(Pre321ViewDragStartY));
                    OnPropertyChanged(nameof(Pre321ViewDragEndX));
                    OnPropertyChanged(nameof(Pre321ViewDragEndY));
                    RefreshPre321ViewDragDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(Pre321ViewDragStartX), overlay.StartX);
                    SaveSetting(nameof(Pre321ViewDragStartY), overlay.StartY);
                    SaveSetting(nameof(Pre321ViewDragEndX), overlay.EndX);
                    SaveSetting(nameof(Pre321ViewDragEndY), overlay.EndY);
                    SaveSetting(nameof(CTAutoSlicerSettings.Pre321ViewDragCalibrated), 1);

                    var msg =
                        $"找点前拖拽箭头已标定：({overlay.StartX},{overlay.StartY}) → ({overlay.EndX},{overlay.EndY})";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"找点前拖拽箭头标定失败：{ex.Message}";
                _logger.Error("3D找点前视角：拖拽箭头标定失败", ex);
            }
        }

        /// <summary>
        /// 通用屏幕 ROI 框选标定：最小化上位机 → 全屏遮罩拖拽 → 写入 DB。
        /// 标定得到的 (X,Y) 为截图区域左上角物理像素，供 DLL 相对坐标换算：screen = ROI起点 + 相对点。
        /// </summary>
        private void ExecuteCalibrateScreenRoi(
            string regionName,
            string windowTitle,
            string hintText,
            string confirmBarText,
            Action<System.Drawing.Rectangle> applyRect,
            Action<System.Drawing.Rectangle> persistRect)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                // 标定需手动框选，必须解除 BlockInput
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                HistogramRoiCalibrationOverlayWindow? overlay = null;
                try
                {
                    // 稍等主窗口最小化完成，避免遮罩盖住上位机而非 VG
                    Thread.Sleep(250);
                    overlay = new HistogramRoiCalibrationOverlayWindow(windowTitle, hintText, confirmBarText);
                    var ok = overlay.ShowDialog() == true;
                    if (!ok || overlay.SelectedScreenRect is not { } rect)
                    {
                        MessageShower = $"已取消{regionName}标定";
                        AppendFlowLog($"{regionName}标定已取消");
                        return;
                    }

                    _isLoadingSettings = true;
                    applyRect(rect);
                    _isLoadingSettings = false;
                    persistRect(rect);

                    var msg =
                        $"{regionName}已标定：X={rect.X}, Y={rect.Y}, W={rect.Width}, H={rect.Height}（起点用于相对坐标换算）";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"{regionName}标定失败：{ex.Message}";
                AppendFlowLog(MessageShower);
                _logger.Error($"{regionName}标定失败", ex);
            }
        }

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
        private void RefreshLatestAiAnnotationPreview()
        {
            try
            {
                var path = OfflineCtAnnotationHelper.TryFindLatestAiResultImage(SliceExportFolder);
                ApplyAiAnnotationPreviewPath(path, allowClearWhenMissing: true);
            }
            catch (Exception ex)
            {
                LatestAiAnnotationPreview = null;
                LatestAiAnnotationPreviewPath = string.Empty;
                LatestAiAnnotationPreviewPixelWidth = 1;
                LatestAiAnnotationPreviewPixelHeight = 1;
                LatestAiAnnotationPreviewInfo = $"预览加载失败：{ex.Message}";
                LatestAiAnnotationSummaryText = string.Empty;
                OnPropertyChanged(nameof(HasLatestAiAnnotationPreview));
                OnPropertyChanged(nameof(HasLatestAiAnnotationSummary));
                _logger.Warn($"最新 AI 标注预览加载失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 用指定结果图路径立即更新预览（批处理逐张推送）；路径无效时按 <paramref name="allowClearWhenMissing"/> 决定是否清空。
        /// </summary>
        private void ApplyAiAnnotationPreviewPath(string? path, bool allowClearWhenMissing)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                if (!allowClearWhenMissing)
                {
                    return;
                }

                LatestAiAnnotationPreview = null;
                LatestAiAnnotationPreviewPath = string.Empty;
                LatestAiAnnotationPreviewPixelWidth = 1;
                LatestAiAnnotationPreviewPixelHeight = 1;
                LatestAiAnnotationPreviewInfo = string.IsNullOrWhiteSpace(SliceExportFolder)
                    ? "请先配置标注结果根目录，并完成 AI 标注"
                    : "暂无 AI 标注结果图（请先「开始流程」生成标注）";
                LatestAiAnnotationSummaryText = string.Empty;
                OnPropertyChanged(nameof(HasLatestAiAnnotationPreview));
                OnPropertyChanged(nameof(HasLatestAiAnnotationSummary));
                return;
            }

            var source = LoadAiAnnotationPreviewBitmap(path);
            LatestAiAnnotationPreview = source;
            LatestAiAnnotationPreviewPath = path;
            LatestAiAnnotationPreviewPixelWidth = Math.Max(1, source.PixelWidth);
            LatestAiAnnotationPreviewPixelHeight = Math.Max(1, source.PixelHeight);
            var time = File.GetLastWriteTime(path);
            LatestAiAnnotationPreviewInfo =
                $"{Path.GetFileName(path)}  ·  {source.PixelWidth}×{source.PixelHeight}  ·  {time:yyyy-MM-dd HH:mm:ss}";
            LatestAiAnnotationSummaryText = OfflineCtAnnotationHelper.TryBuildAiDimensionSummaryText(
                path,
                OfflineCtPixelSizeUm);
            OnPropertyChanged(nameof(HasLatestAiAnnotationPreview));
            OnPropertyChanged(nameof(HasLatestAiAnnotationSummary));
        }

        /// <summary>
        /// 加载预览图：按像素尺寸显示，并将 DPI 归一为 96，避免元数据导致宽高比失真。
        /// </summary>
        private static BitmapSource LoadAiAnnotationPreviewBitmap(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();

            if (Math.Abs(bmp.DpiX - 96.0) < 0.01
                && Math.Abs(bmp.DpiY - 96.0) < 0.01)
            {
                return bmp;
            }

            var stride = (bmp.PixelWidth * bmp.Format.BitsPerPixel + 31) / 32 * 4;
            var pixels = new byte[stride * bmp.PixelHeight];
            bmp.CopyPixels(pixels, stride, 0);
            var normalized = BitmapSource.Create(
                bmp.PixelWidth,
                bmp.PixelHeight,
                96,
                96,
                bmp.Format,
                bmp.Palette,
                pixels,
                stride);
            normalized.Freeze();
            return normalized;
        }

        /// <summary>
        /// 二次手动标注：与「标注结果根目录」(SliceExportFolder) 同步；
        /// 有路径则直开，无路径则弹窗自选并写回结果根目录。
        /// </summary>
        private void OpenManualMeasureFolder(object? _)
        {
            string? root = null;
            if (!string.IsNullOrWhiteSpace(SliceExportFolder))
            {
                root = SliceExportFolder.Trim();
                if (!Directory.Exists(root))
                {
                    MessageBox.Show(
                        $"标注结果根目录不存在：\n{root}\n\n请在「AI图像标注」中重新选择，或清空后自选。",
                        "二次手动标注",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                var dialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    Title = "选择标注结果根目录（二次手动标注将打开此文件夹）",
                    InitialDirectory = Directory.Exists(@"D:\3Ddeliever\output")
                        ? @"D:\3Ddeliever\output"
                        : null,
                };

                if (dialog.ShowDialog() != CommonFileDialogResult.Ok
                    || string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    return;
                }

                root = dialog.FileName;
                SliceExportFolder = root;
            }

            var images = ManualMeasureExportHelper.EnumerateImagesRecursive(root);
            if (images.Count == 0)
            {
                MessageBox.Show(
                    "所选文件夹（含子目录）未找到图片（jpg/png/bmp/tif）。",
                    "二次手动标注",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var win = new ManualMeasureAnnotateWindow(
                root,
                OfflineCtPixelSizeUm,
                OfflineCtBlackHeight)
            {
                Owner = Application.Current?.MainWindow,
            };
            win.ShowDialog();
            MessageShower = $"二次手动标注已结束：{root}（共 {images.Count} 张）";
            RefreshLatestAiAnnotationPreview();
        }

        private void EnsureStatsDemoInitialized()
        {
            if (_statsDemoInitialized)
            {
                return;
            }

            RefreshStatsDemoData(randomize: false);
            _statsDemoInitialized = true;
        }

        /// <summary>
        /// 刷新检测统计 Demo 假数据（后续可替换为真实 OK/NG 判定落库查询）。
        /// </summary>
        private void RefreshStatsDemoData(bool randomize)
        {
            var today = DateTime.Today;
            StatsDemoDateText = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            int total;
            int ok;
            int ng;
            int[] hourlyOk;
            int[] hourlyNg;

            if (randomize)
            {
                var rng = new Random();
                total = rng.Next(80, 180);
                ng = rng.Next(5, Math.Max(6, total / 8));
                ok = Math.Max(0, total - ng);
                hourlyOk = new int[8];
                hourlyNg = new int[8];
                var remainOk = ok;
                var remainNg = ng;
                for (var i = 0; i < 8; i++)
                {
                    var last = i == 7;
                    hourlyOk[i] = last ? remainOk : rng.Next(0, remainOk + 1);
                    hourlyNg[i] = last ? remainNg : rng.Next(0, remainNg + 1);
                    remainOk -= hourlyOk[i];
                    remainNg -= hourlyNg[i];
                }
            }
            else
            {
                // 固定种子 Demo，便于首次打开对照
                total = 128;
                ok = 115;
                ng = 13;
                hourlyOk = [8, 12, 15, 18, 16, 14, 17, 15];
                hourlyNg = [1, 2, 1, 2, 1, 2, 2, 2];
            }

            StatsTodayTotal = total;
            StatsTodayOk = ok;
            StatsTodayNg = ng;
            StatsDemoNote = randomize
                ? $"已刷新 Demo 假数据（{DateTime.Now:HH:mm:ss}）。真实检测判定接入后将改为读库。"
                : "当前为 Demo 假数据，尚未接入真实检测判定。";

            StatsOkNgPieModel = BuildOkNgPieModel(ok, ng);
            StatsHourlyBarModel = BuildHourlyBarModel(hourlyOk, hourlyNg);

            if (randomize)
            {
                MessageShower = $"统计 Demo 已刷新：今日 {total}（OK {ok} / NG {ng}）";
            }
        }

        private static PlotModel BuildOkNgPieModel(int ok, int ng)
        {
            var model = new PlotModel
            {
                Title = "当日 OK / NG 占比",
                TitleFontSize = 14,
                PlotMargins = new OxyThickness(8, 8, 8, 8),
            };
            model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.BottomCenter,
                LegendOrientation = LegendOrientation.Horizontal,
                LegendPlacement = LegendPlacement.Outside,
            });

            var pie = new PieSeries
            {
                StrokeThickness = 1,
                Stroke = OxyColors.White,
                InsideLabelFormat = "{1}",
                OutsideLabelFormat = "{0}: {1} ({2:0.0}%)",
                AngleSpan = 360,
                StartAngle = 90,
                FontSize = 12,
            };

            if (ok + ng <= 0)
            {
                pie.Slices.Add(new PieSlice("无数据", 1) { Fill = OxyColors.LightGray });
            }
            else
            {
                if (ok > 0)
                {
                    pie.Slices.Add(new PieSlice("OK", ok)
                    {
                        Fill = OxyColor.FromRgb(0x2E, 0xA8, 0x5A),
                    });
                }

                if (ng > 0)
                {
                    pie.Slices.Add(new PieSlice("NG", ng)
                    {
                        Fill = OxyColor.FromRgb(0xD3, 0x2F, 0x2F),
                    });
                }
            }

            model.Series.Add(pie);
            return model;
        }

        private static PlotModel BuildHourlyBarModel(IReadOnlyList<int> hourlyOk, IReadOnlyList<int> hourlyNg)
        {
            var model = new PlotModel
            {
                Title = "当日分时段产量（Demo）",
                TitleFontSize = 14,
                PlotMargins = new OxyThickness(56, 8, 16, 16),
            };
            model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.TopRight,
                LegendPlacement = LegendPlacement.Inside,
            });

            // OxyPlot 2.x：BarSeries 为水平条；类别轴放左侧
            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "时段",
                FontSize = 11,
                GapWidth = 0.25,
            };
            var nowHour = DateTime.Now.Hour;
            var startHour = Math.Max(0, nowHour - 7);
            for (var i = 0; i < 8; i++)
            {
                var h = startHour + i;
                categoryAxis.Labels.Add($"{h:00}:00");
            }

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "数量",
                MinimumPadding = 0.05,
                AbsoluteMinimum = 0,
                FontSize = 11,
            };

            var okSeries = new BarSeries
            {
                Title = "OK",
                FillColor = OxyColor.FromRgb(0x2E, 0xA8, 0x5A),
                StrokeThickness = 0,
                IsStacked = true,
            };
            var ngSeries = new BarSeries
            {
                Title = "NG",
                FillColor = OxyColor.FromRgb(0xD3, 0x2F, 0x2F),
                StrokeThickness = 0,
                IsStacked = true,
            };

            var n = Math.Min(8, Math.Min(hourlyOk.Count, hourlyNg.Count));
            for (var i = 0; i < n; i++)
            {
                okSeries.Items.Add(new BarItem(hourlyOk[i]));
                ngSeries.Items.Add(new BarItem(hourlyNg[i]));
            }

            model.Axes.Add(categoryAxis);
            model.Axes.Add(valueAxis);
            model.Series.Add(okSeries);
            model.Series.Add(ngSeries);
            return model;
        }

        private bool IsNavAllowedForCurrentPipeline(int navIndex) =>
            navIndex switch
            {
                NavOverview or NavPreferences or NavStepTest => true,
                NavAiAnnotate or NavManualMeasure or NavStats => IsAiPipelineMode,
                NavApp or NavImport or NavRender or NavHistogram or NavVision or NavPostAlign or NavSlice
                    => IsSlicePipelineMode,
                _ => true,
            };

        private void EnsureNavValidForPipelineMode()
        {
            if (!IsNavAllowedForCurrentPipeline(SelectedNavIndex))
            {
                // AI 模式回落带预览的主界面；切图模式回落总览
                SelectedNavIndex = IsAiPipelineMode ? NavManualMeasure : NavOverview;
            }
        }

        private void OpenHistogramCaptureDir(object? _)
        {
            var initial = HistogramCaptureDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "histogram");
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                HistogramCaptureDir = dialog.FileName;
            }
        }

        private void OpenVisionCaptureDir(object? _)
        {
            var initial = VisionCaptureDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = CTAutoSlicerSettings.DefaultVisionCaptureDir;
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                VisionCaptureDir = dialog.FileName;
            }
        }

        private void OpenVision2dVectorCaptureDir(object? _)
        {
            var initial = Vision2dVectorCaptureDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = CTAutoSlicerSettings.DefaultVision2dVectorCaptureDir;
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                Vision2dVectorCaptureDir = dialog.FileName;
            }
        }

        private void OpenVision3dInitialCaptureDir(object? _)
        {
            var initial = Vision3dInitialCaptureDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = CTAutoSlicerSettings.DefaultVision3dInitialCaptureDir;
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "选择 3D 初始原图保存文件夹（未切视角）",
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                Vision3dInitialCaptureDir = dialog.FileName;
            }
        }

        /// <summary>单步测试：截取 3D 标定图保存目录选择。</summary>
        private void BrowseVisionLearn3dSampleDir(object? _)
        {
            var initial = VisionLearn3dSampleDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = CTAutoSlicerSettings.DefaultVisionLearn3dSampleDir;
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                VisionLearn3dSampleDir = dialog.FileName;
            }
        }

        /// <summary>单步测试：截取上视图标定图保存目录选择。</summary>
        private void BrowseVisionLearnTopViewSampleDir(object? _)
        {
            var initial = VisionLearnTopViewSampleDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = CTAutoSlicerSettings.DefaultVisionLearnTopViewSampleDir;
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                VisionLearnTopViewSampleDir = dialog.FileName;
            }
        }

        /// <summary>单步测试：缩小本软件后按 3D ROI 截图。</summary>
        private void ExecuteCaptureVision3dSample(object? _)
        {
            if (_vision3dRoiWidth <= 0 || _vision3dRoiHeight <= 0)
            {
                MessageShower = "尚未标定 3D 区域，请先标定后再截图";
                return;
            }

            var saveDir = string.IsNullOrWhiteSpace(VisionLearn3dSampleDir)
                ? CTAutoSlicerSettings.DefaultVisionLearn3dSampleDir
                : VisionLearn3dSampleDir.Trim();

            CaptureSimpleRoiSample(
                "3D 标定图",
                saveDir,
                "vision_3d",
                _vision3dRoiX,
                _vision3dRoiY,
                _vision3dRoiWidth,
                _vision3dRoiHeight);
        }

        /// <summary>单步测试：缩小本软件后按「上」视图 ROI 截图。</summary>
        private void ExecuteCaptureVisionTopViewSample(object? _)
        {
            if (_visionTopViewRoiWidth <= 0 || _visionTopViewRoiHeight <= 0)
            {
                MessageShower = "尚未标定「上」视图区域，请先标定后再截图";
                return;
            }

            var saveDir = string.IsNullOrWhiteSpace(VisionLearnTopViewSampleDir)
                ? CTAutoSlicerSettings.DefaultVisionLearnTopViewSampleDir
                : VisionLearnTopViewSampleDir.Trim();

            CaptureSimpleRoiSample(
                "上视图标定图",
                saveDir,
                "vision_top",
                _visionTopViewRoiX,
                _visionTopViewRoiY,
                _visionTopViewRoiWidth,
                _visionTopViewRoiHeight);
        }

        /// <summary>
        /// 基础截图：最小化本软件 → 按 ROI 截图保存；截图后保持最小化。
        /// </summary>
        private void CaptureSimpleRoiSample(
            string regionName,
            string saveDir,
            string filePrefix,
            int x,
            int y,
            int width,
            int height)
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.WindowState = WindowState.Minimized;
                }

                Thread.Sleep(400);
                Directory.CreateDirectory(saveDir);
                var filePath = Path.Combine(saveDir, $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                ScreenCaptureHelper.CaptureRegionAndSave(filePath, x, y, width, height);

                // 截图后保持最小化，方便连续采图；用户自行还原窗口
                var msg = $"{regionName}已保存：{filePath}";
                MessageShower = msg;
                AppendFlowLog(msg);
                _logger.Info(msg);
            }
            catch (Exception ex)
            {
                MessageShower = $"{regionName}截取失败：{ex.Message}";
                _logger.Error($"{regionName}临时截图失败", ex);
            }
        }

        private async Task ExecuteTestRenderingPresetAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(RenderingPresetName))
            {
                MessageShower = "请先填写渲染预设名称";
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 渲染预设单步测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService
                    .TestRenderingPresetAsync(BuildSettings(), flowToken, progress);
                AppendFlowLog("——— 渲染预设单步测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"渲染预设测试失败：{ex.Message}");
                _logger.Error("渲染预设单步测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteTestThickPlateAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 厚板三象限测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestThickPlateAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 厚板三象限测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"厚板测试失败：{ex.Message}");
                _logger.Error("厚板三象限测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteTestSliceExportAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            if (string.IsNullOrWhiteSpace(SliceExportFolder))
            {
                MessageShower = "请先配置切片导出目录";
                return;
            }

            if (string.IsNullOrWhiteSpace(CTFileFolder) || !Directory.Exists(CTFileFolder))
            {
                MessageShower = "请先选择有效的 CT 体素文件夹（用于命名规则）";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 切片导出三视图测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestSliceExportAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 切片导出三视图测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"切片导出测试失败：{ex.Message}");
                _logger.Error("切片导出单步测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteTestHistogramGrayScaleAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (_histogramRoiWidth <= 0 || _histogramRoiHeight <= 0)
            {
                MessageShower = "请先点击「标定直方图区域」框选直方图 ROI";
                return;
            }

            if (!_lutSegmentPointsCalibrated
                || (_lutSegmentPoint1X == _lutSegmentPoint2X && _lutSegmentPoint1Y == _lutSegmentPoint2Y))
            {
                MessageShower = "请先在「直方图标定」中点击「标定 LUT 关键段两点」";
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 直方图灰度调整测试开始（拖红线 + LUT中点对齐） ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestHistogramGrayScaleAdjustAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 直方图灰度调整测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"直方图灰度测试失败：{ex.Message}");
                _logger.Error("直方图灰度单步测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteTestSimple321AlignmentAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (_vision3dRoiWidth <= 0 || _vision3dRoiHeight <= 0)
            {
                MessageShower = "尚未标定 3D 视图区域，请先在「阶段四视觉对齐」中标定 3D 区域";
                return;
            }

            if (_visionTopViewRoiWidth <= 0 || _visionTopViewRoiHeight <= 0)
            {
                MessageShower = "尚未标定「上」视图区域，请先在「阶段四视觉对齐」中标定「上」视图（向量两点需要）";
                return;
            }

            if (!_simple321ToolbarClickCalibrated)
            {
                MessageShower = "尚未标定「简单对齐」下拉点击位置，请先在「阶段四视觉对齐」中标定下拉框";
                return;
            }

            if (!_simple321OffsetEditCalibrated)
            {
                MessageShower = "尚未标定三点后偏移 Edit，请先在「阶段四视觉对齐」中标定「偏移置0」编辑框";
                return;
            }

            // 找点算法由 Simple321PointSource 配置（本地 / 视觉 DLL）

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                var sourceLabel = Simple321PointSourceDisplay;
                AppendFlowLog(
                    $"——— 简单 3-2-1 对齐测试开始（三点→偏移置0→向量两点→原点） ———");
                AppendFlowLog($"【找点算法】当前调用：{sourceLabel}");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestSimple321AlignmentAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 简单 3-2-1 对齐测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"简单 3-2-1 对齐测试失败：{ex.Message}");
                _logger.Error("简单 3-2-1 对齐测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        /// <summary>
        /// 打开本地 3D 斜俯视找点分步调试窗：对 vision_3d 截图逐步执行算法并显示中间图。
        /// </summary>
        private void ExecuteOpenVision3dPointsStepDebug()
        {
            string? initial = null;
            try
            {
                var root = string.IsNullOrWhiteSpace(VisionCaptureDir)
                    ? CTAutoSlicerSettings.DefaultVisionCaptureDir
                    : VisionCaptureDir.Trim();
                if (Directory.Exists(root))
                {
                    var latest = Directory.EnumerateFiles(root, "vision_3d_*.png", SearchOption.AllDirectories)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();
                    initial = latest;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"查找最新 vision_3d 截图失败：{ex.Message}");
            }

            var win = new Vision3dPointsStepDebugWindow(initial)
            {
                Owner = Application.Current?.MainWindow,
            };
            win.Show();
            MessageShower = initial == null
                ? "已打开本地 3D 找点分步调试（请自行浏览截图）"
                : $"已打开本地 3D 找点分步调试：{Path.GetFileName(initial)}";
        }

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
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

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

            try
            {
                IsEnableSimulationBtn = false;
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
                IsEnableSimulationBtn = true;
            }
        }

        /// <summary>
        /// 离线批量测 GetObjectTopBottomPoints：不跑 VG，对 2D侧视图文件夹内全部图片 → DLL → 标注起点/终点落盘。
        /// [0]=起点，[1]=终点。单张失败不中断。
        /// </summary>
        private async Task ExecuteTestCutImgGetObjectTopBottomPointsAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

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

            try
            {
                IsEnableSimulationBtn = false;
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
                IsEnableSimulationBtn = true;
            }
        }

        private async Task ExecuteTestPostAlignCaptureDragAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (!_postAlignCaptureButtonCalibrated)
            {
                MessageShower = "尚未标定 3D图正视图切换按钮位置，请先在「对齐后拖拽」中点击「标定按钮位置」";
                return;
            }

            if (!_postAlignDragCalibrated
                || (_postAlignDragStartX == _postAlignDragEndX && _postAlignDragStartY == _postAlignDragEndY))
            {
                MessageShower = "尚未标定拖拽箭头，请先「标定拖拽箭头」";
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 对齐后拖拽测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestPostAlignCaptureDragAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 对齐后拖拽测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"对齐后拖拽测试失败：{ex.Message}");
                _logger.Error("对齐后拖拽测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteTestPre321ViewPrepAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (!_pre321ViewClickCalibrated)
            {
                MessageShower = "尚未标定切换视图点击，请先在「视觉对齐」中点击「标定切换视图点击」";
                return;
            }

            if (!_pre321ViewDragCalibrated
                || (_pre321ViewDragStartX == _pre321ViewDragEndX && _pre321ViewDragStartY == _pre321ViewDragEndY))
            {
                MessageShower = "尚未标定找点前拖拽箭头，请先「标定拖拽箭头」";
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 临时·3D找点前视角准备测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestPre321ViewPrepAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 临时·3D找点前视角准备测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"3D找点前视角准备测试失败：{ex.Message}");
                _logger.Error("3D找点前视角准备测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private void ExecuteBrowseOfflineCtModelPath()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择离线 CT ONNX 模型",
                Filter = "ONNX 模型 (*.onnx)|*.onnx|所有文件 (*.*)|*.*",
                CheckFileExists = true,
            };
            if (!string.IsNullOrWhiteSpace(OfflineCtModelPath))
            {
                try
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(OfflineCtModelPath);
                    dialog.FileName = Path.GetFileName(OfflineCtModelPath);
                }
                catch
                {
                    // ignore
                }
            }

            if (dialog.ShowDialog() == true)
            {
                OfflineCtModelPath = dialog.FileName;
            }
        }

        private void ExecuteResetOfflineCtModelPath()
        {
            OfflineCtModelPath = string.Empty;
            MessageShower = $"已恢复默认模型路径：{CTAutoSlicerSettings.GetDefaultOfflineCtModelPath()}";
        }

        private void ExecuteBrowseOfflineCtManualInputImage()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择 AI 标注输入图片",
                Filter = "图像 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*",
                CheckFileExists = true,
            };
            if (!string.IsNullOrWhiteSpace(OfflineCtManualInputImagePath))
            {
                var dir = Path.GetDirectoryName(OfflineCtManualInputImagePath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    dialog.InitialDirectory = dir;
                    dialog.FileName = Path.GetFileName(OfflineCtManualInputImagePath);
                }
            }

            if (dialog.ShowDialog() == true)
            {
                OfflineCtManualInputImagePath = dialog.FileName;
            }
        }

        private void ExecuteBrowseOfflineCtManualOutputFolder()
        {
            using var dialog = new CommonOpenFileDialog
            {
                Title = "选择 AI 标注输出文件夹（可留空则输出到输入图目录）",
                IsFolderPicker = true,
            };
            if (!string.IsNullOrWhiteSpace(OfflineCtManualOutputFolder)
                && Directory.Exists(OfflineCtManualOutputFolder))
            {
                dialog.InitialDirectory = OfflineCtManualOutputFolder;
            }
            else if (!string.IsNullOrWhiteSpace(OfflineCtManualInputImagePath))
            {
                var dir = Path.GetDirectoryName(OfflineCtManualInputImagePath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    dialog.InitialDirectory = dir;
                }
            }

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                OfflineCtManualOutputFolder = dialog.FileName;
            }
        }

        /// <summary>AI 标注配置页：按指定输入图独立执行标注。</summary>
        private async Task ExecuteRunOfflineCtManualAnnotationAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(OfflineCtManualInputImagePath)
                || !File.Exists(OfflineCtManualInputImagePath))
            {
                MessageShower = "请先选择有效的输入图片路径";
                return;
            }

            var outputFolder = string.IsNullOrWhiteSpace(OfflineCtManualOutputFolder)
                ? null
                : OfflineCtManualOutputFolder.Trim();
            if (outputFolder != null && !Directory.Exists(outputFolder))
            {
                try
                {
                    Directory.CreateDirectory(outputFolder);
                }
                catch (Exception ex)
                {
                    MessageShower = $"无法创建输出目录：{ex.Message}";
                    return;
                }
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— AI 独立标注开始 ———");
                AppendFlowLog($"输入图：{OfflineCtManualInputImagePath}");
                AppendFlowLog(
                    string.IsNullOrWhiteSpace(outputFolder)
                        ? "输出目录：默认（输入图所在文件夹）"
                        : $"输出目录：{outputFolder}");

                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestOfflineCtVisionAnnotationAsync(
                    BuildSettings(),
                    flowToken,
                    progress,
                    OfflineCtManualInputImagePath.Trim(),
                    outputFolder);
                AppendFlowLog("——— AI 独立标注结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"AI 独立标注失败：{ex.Message}");
                _logger.Error("AI 独立标注失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    IsEnableSimulationBtn = true;
                    RefreshLatestAiAnnotationPreview();
                });
            }
        }

        private async Task ExecuteTestOfflineCtVisionAnnotationAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            string? explicitImage = null;
            var pick = MessageBox.Show(
                "是 = 手动选择一张图片测试\n否 = 按切片导出目录规则取「上」视图中间图\n取消 = 退出",
                "AI 图像标注测试",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (pick == MessageBoxResult.Cancel)
            {
                return;
            }

            if (pick == MessageBoxResult.Yes)
            {
                var dialog = new OpenFileDialog
                {
                    Title = "选择 AI 标注输入图",
                    Filter = "图像 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*",
                    CheckFileExists = true,
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                explicitImage = dialog.FileName;
            }
            else if (string.IsNullOrWhiteSpace(SliceExportFolder))
            {
                MessageShower = "请先配置切片导出目录，或改为手动选图测试";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— AI 图像标注测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestOfflineCtVisionAnnotationAsync(
                    BuildSettings(),
                    flowToken,
                    progress,
                    explicitImage);
                AppendFlowLog("——— AI 图像标注测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"AI 图像标注测试失败：{ex.Message}");
                _logger.Error("AI 图像标注单步测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    IsEnableSimulationBtn = true;
                    RefreshLatestAiAnnotationPreview();
                });
            }
        }

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

        private async Task ExecuteAiAnnotationBatchAsync()
        {
            if (string.IsNullOrWhiteSpace(TopViewHandoffFolder) || !Directory.Exists(TopViewHandoffFolder))
            {
                MessageShower = "请先选择有效的上视图交接目录";
                return;
            }

            if (string.IsNullOrWhiteSpace(SliceExportFolder))
            {
                MessageShower = "请先配置标注结果根目录";
                return;
            }

            var images = OfflineCtAnnotationHelper.EnumerateHandoffImages(TopViewHandoffFolder);
            if (images.Count == 0)
            {
                MessageShower = $"交接目录内未找到可标注图像：{TopViewHandoffFolder}";
                return;
            }

            var flowToken = BeginActiveFlow();
            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— AI 标注批次开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                await _ctAutoSlicerService
                    .RunAiAnnotationBatchAsync(BuildSettings(), flowToken, progress);
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => MessageShower = ex.Message);
                _logger.Error("AI 标注批处理异常", ex);
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    IsEnableSimulationBtn = true;
                    RefreshLatestAiAnnotationPreview();
                });
            }
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

        private void HandleSimulationProgress(string message)
        {
            try
            {
                if (message.StartsWith("@@SLICE_QTY@@", StringComparison.Ordinal))
                {
                    var qtyText = message["@@SLICE_QTY@@".Length..];
                    if (int.TryParse(qtyText, out var quantity))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            SetSliceExportQuantityRuntime(quantity);
                            MessageShower = $"检测到 QJ 全局图像，导出数量已同步为 {quantity}";
                        });
                    }

                    return;
                }

                if (string.Equals(message, "@@MANUAL_INTERVENE_END@@", StringComparison.Ordinal))
                {
                    Dispatcher.Invoke(HideManualInterventionPanel);
                    return;
                }

                if (message.StartsWith("@@MANUAL_INTERVENE@@", StringComparison.Ordinal))
                {
                    var payload = message["@@MANUAL_INTERVENE@@".Length..];
                    var separatorIndex = payload.IndexOf('|', StringComparison.Ordinal);
                    var fileName = separatorIndex >= 0 ? payload[..separatorIndex] : payload;
                    var reason = separatorIndex >= 0 ? payload[(separatorIndex + 1)..] : "未知原因";
                    Dispatcher.Invoke(() => ShowManualInterventionPanel(fileName, reason));
                    return;
                }

                if (message.StartsWith("@@IMPORT_FAIL@@", StringComparison.Ordinal))
                {
                    var payload = message["@@IMPORT_FAIL@@".Length..];
                    var separatorIndex = payload.IndexOf('|', StringComparison.Ordinal);
                    var fileName = separatorIndex >= 0 ? payload[..separatorIndex] : payload;
                    var reason = separatorIndex >= 0 ? payload[(separatorIndex + 1)..] : "未知原因";
                    Dispatcher.Invoke(() =>
                    {
                        MessageShower = $"导入失败：{fileName}（等待人工处理）";
                        AppendFlowLog($"{fileName} — {reason}", isFailure: true);
                    });
                    return;
                }

                // 单张结果图已落盘：立刻刷新「最新 AI 标注预览」，不必等整批结束
                if (message.StartsWith("@@AI_RESULT_PREVIEW@@", StringComparison.Ordinal))
                {
                    var previewPath = message["@@AI_RESULT_PREVIEW@@".Length..].Trim();
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            ApplyAiAnnotationPreviewPath(previewPath, allowClearWhenMissing: false);
                            if (!string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath))
                            {
                                MessageShower = $"已更新预览：{Path.GetFileName(previewPath)}";
                                AppendFlowLog($"预览已更新：{previewPath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"AI 结果预览即时刷新失败：{ex.Message}");
                        }
                    });
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    MessageShower = message;
                    AppendFlowLog(message);
                });
            }
            catch (Exception ex)
            {
                // Progress 回调异常若上抛，可能打断后台流程；仅记日志
                _logger.Warn($"流程进度回调异常（已忽略）：{ex.Message}");
            }
        }

        private void ShowManualInterventionPanel(string fileName, string reason)
        {
            ManualInterventionFileName = string.IsNullOrWhiteSpace(fileName) ? "(未知文件)" : fileName;
            ManualInterventionReason = string.IsNullOrWhiteSpace(reason) ? "流程异常或超时" : reason;
            MessageShower = $"需要人工处理：{ManualInterventionFileName}";
            AppendFlowLog($"需要人工处理：{ManualInterventionFileName} — {ManualInterventionReason}", isFailure: true);

            _manualInterventionDeadlineUtc =
                DateTime.UtcNow + CTAutoSlicerSettings.DefaultManualInterventionTimeout;
            UpdateManualInterventionCountdownText();
            IsManualInterventionVisible = true;

            // 关键：把本软件从 VG 后面拉到前台，否则操作员看不到干预面板
            BringAutomationAppToForeground();

            _manualInterventionCountdownTimer?.Stop();
            _manualInterventionCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _manualInterventionCountdownTimer.Tick += (_, _) => UpdateManualInterventionCountdownText();
            _manualInterventionCountdownTimer.Start();
        }

        /// <summary>
        /// 人工干预时强制置前主窗口（恢复最小化 + Topmost 脉冲 + Win32 ForceForeground）。
        /// </summary>
        private void BringAutomationAppToForeground()
        {
            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow == null)
                {
                    return;
                }

                if (!mainWindow.IsVisible)
                {
                    mainWindow.Show();
                }

                if (mainWindow.WindowState == WindowState.Minimized)
                {
                    mainWindow.WindowState = WindowState.Normal;
                }

                mainWindow.Activate();
                mainWindow.Topmost = true;
                mainWindow.Topmost = false;
                mainWindow.Focus();

                var hwnd = new System.Windows.Interop.WindowInteropHelper(mainWindow).EnsureHandle();
                if (hwnd != IntPtr.Zero)
                {
                    WinAPIHelper.ForceForegroundWindow(hwnd);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"人工干预置前本软件失败（忽略）：{ex.Message}");
            }
        }

        private void HideManualInterventionPanel()
        {
            _manualInterventionCountdownTimer?.Stop();
            _manualInterventionCountdownTimer = null;
            IsManualInterventionVisible = false;
            ManualInterventionCountdownText = string.Empty;
        }

        private void UpdateManualInterventionCountdownText()
        {
            var remain = _manualInterventionDeadlineUtc - DateTime.UtcNow;
            if (remain < TimeSpan.Zero)
            {
                remain = TimeSpan.Zero;
            }

            ManualInterventionCountdownText =
                $"剩余等待 {remain.Hours:00}:{remain.Minutes:00}:{remain.Seconds:00}"
                + "（超时将关闭 VG，归档失败文件并继续下一文件）";
        }

        private void ExecuteConfirmManualIntervention(object? _)
        {
            try
            {
                AppendFlowLog("操作员点击「处理完成」");
                MessageShower = "已确认人工处理，继续流程…";
                _ctAutoSlicerService.ConfirmManualIntervention();
            }
            catch (Exception ex)
            {
                _logger.Error("确认人工干预失败", ex);
                MessageShower = $"确认失败：{ex.Message}";
            }
        }

        private void AppendFlowLog(string message, bool isFailure = false)
        {
            if (!isFailure && message == _lastFlowLogMessage)
            {
                return;
            }

            _lastFlowLogMessage = message;
            var prefix = isFailure ? "失败" : "信息";
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {prefix}：{message}";
            var lines = string.IsNullOrWhiteSpace(ImportLogText)
                ? new List<string>()
                : ImportLogText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();

            lines.Add(line);
            if (lines.Count > 200)
            {
                lines = lines.Skip(lines.Count - 200).ToList();
            }

            ImportLogText = string.Join(Environment.NewLine, lines);
            WriteFlowLogToFile(line);
            _logger.Info($"[CT导入] {message}");

            if (!_isUserScrolledUp)
            {
                ImportLogScrollToEndRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ClearImportLog(object? _)
        {
            ImportLogText = string.Empty;
            _lastFlowLogMessage = null;
        }

        private static void WriteFlowLogToFile(string line)
        {
            var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "CTAutoSlicer");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "import_log.txt");
            File.AppendAllText(logPath, line + Environment.NewLine);
        }

        private CancellationToken BeginActiveFlow()
        {
            _activeFlowCts?.Cancel();
            _activeFlowCts?.Dispose();
            _activeFlowCts = new CancellationTokenSource();
            return _activeFlowCts.Token;
        }

        private async Task HandleFlowCanceledAsync()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                MessageShower = "流程已取消";
                AppendFlowLog("流程已取消");
            });
            _logger.Info("CT 切片流程已取消");
        }

        private void InterruptActiveFlow(string trigger)
        {
            _ctAutoSlicerService.Cancel();
            _activeFlowCts?.Cancel();
            Dispatcher.Invoke(() =>
            {
                WinAPIHelper.BlockInput(false);
                IsFlowPaused = false;
                HideManualInterventionPanel();
                IsEnableSimulationBtn = true;
                MessageShower = $"流程已通过 {trigger} 中断";
                AppendFlowLog($"流程已通过 {trigger} 中断");
            });
        }

        private void OnDoubleEscPressed()
        {
            InterruptActiveFlow("Esc");
        }

        private void OnCtrlXPressed()
        {
            InterruptActiveFlow("Ctrl+X");
        }

        private void OnAltPPressed()
        {
            if (!_ctAutoSlicerService.IsRunning)
            {
                Dispatcher.Invoke(() => MessageShower = "当前没有运行中的流程，Alt+P 无效");
                return;
            }

            var paused = _ctAutoSlicerService.TogglePause();
            Dispatcher.Invoke(() =>
            {
                IsFlowPaused = paused;
                if (paused)
                {
                    MessageShower = "暂停中 — 再按 Alt+P 继续";
                    AppendFlowLog("流程已暂停（Alt+P）");
                }
                else
                {
                    MessageShower = "流程已继续（Alt+P）";
                    AppendFlowLog("流程已继续（Alt+P）");
                }
            });
        }

        private void OnSingleEscPressed()
        {
            Dispatcher.Invoke(() => WinAPIHelper.BlockInput(true));
        }
    }
}
