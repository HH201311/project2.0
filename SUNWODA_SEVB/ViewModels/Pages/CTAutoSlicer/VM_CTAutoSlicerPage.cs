using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
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
        private string _visionLearn3dSampleDir = CTAutoSlicerSettings.DefaultVisionLearn3dSampleDir;
        private string _visionLearnTopViewSampleDir = CTAutoSlicerSettings.DefaultVisionLearnTopViewSampleDir;
        private string _histogramMaskColor = "#6E6E6E";
        private int _histogramMaskTolerance = 2;
        private string _histogramRoiDisplayText = "未标定";
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
        private bool _enableOfflineCtVisionAnnotation = true;
        private string _offlineCtModelPath = string.Empty;
        private bool _offlineCtIsRunCuda = true;
        private int _offlineCtBlackHeight = CTAutoSlicerSettings.DefaultOfflineCtBlackHeight;
        private int _offlineCtTopViewSliceIndex = CTAutoSlicerSettings.DefaultOfflineCtTopViewSliceIndex;
        private double _offlineCtPixelSizeUm = CTAutoSlicerSettings.DefaultOfflineCtPixelSizeUm;
        // 使用完全限定枚举名，避免与同名属性 OfflineCtResultImageMode 冲突（CS0236）
        private int _offlineCtResultImageMode = (int)Core.Models.Project.CTAutoSlicer.OfflineCtResultImageMode.Enhance;
        private bool _offlineCtExportJson = true;
        private bool _offlineCtExportCsv = true;
        private bool _offlineCtRotateTopViewCw90 = true;
        private bool _enableOfflineCtPreBrighten = true;
        private double _offlineCtPreBrightenMeanTrigger = 120.0;
        private double _offlineCtPreBrightenP50Trigger = 110.0;
        private double _offlineCtPreBrightenTargetMean = 132.0;
        private string _offlineCtManualInputImagePath = string.Empty;
        private string _offlineCtManualOutputFolder = string.Empty;
        private int _postAlignClickWaitMs = 400;
        private int _postAlignDragStepDelayMs = 8;
        private bool _mainWindowRememberSize;
        private int _mainWindowWidth = 1280;
        private int _mainWindowHeight = 720;
        private int _mainWindowLeft = 80;
        private int _mainWindowTop = 80;
        private string _mainWindowSizeDisplayText = "未保存自定义尺寸（默认全屏）";
        /// <summary>false=生产模式（默认）；true=调试模式（显示单步测试）。</summary>
        private bool _isDebugMode;

        /// <summary>左侧导航选中项：0总览 1应用 2导入 3渲染 4直方 5对齐 6拖拽 7导出 8AI标注 9单测。</summary>
        private int _selectedNavIndex;

        public const int NavOverview = 0;
        public const int NavApp = 1;
        public const int NavImport = 2;
        public const int NavRender = 3;
        public const int NavHistogram = 4;
        public const int NavVision = 5;
        public const int NavPostAlign = 6;
        public const int NavSlice = 7;
        public const int NavAiAnnotate = 8;
        public const int NavStepTest = 9;

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
                    if (!value && SelectedNavIndex == NavStepTest)
                    {
                        SelectedNavIndex = NavOverview;
                    }

                    MessageShower = value
                        ? "已切换为调试模式（可使用单步测试）"
                        : "已切换为生产模式（单步测试已隐藏）";
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

        /// <summary>顶栏 pill 显示的模式文案。</summary>
        public string RunModeDisplayText => IsDebugMode ? "调试模式" : "生产模式";

        public int SelectedNavIndex
        {
            get => _selectedNavIndex;
            set
            {
                if (value == NavStepTest && !IsDebugMode)
                {
                    value = NavOverview;
                }

                if (SetProperty(ref _selectedNavIndex, value))
                {
                    NotifyNavSectionChanged();
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
        public bool IsNavStepTest => SelectedNavIndex == NavStepTest;

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
            NavStepTest => "单步测试",
            _ => "CT自动化切片",
        };

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
                }
            }
        }

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

        /// <summary>
        /// AI 标注选用「上」视图切片序号（1～100，1-based）。
        /// 与导出文件名数字对应，如 50 → 50.jpg；默认 50（原中间张）。
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

        /// <summary>调用 NJCT 前是否按 ROI 亮度条件提亮并生成 input_for_njct。</summary>
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
        public ICommand OpenHistogramCaptureDirCommand { get; }
        public ICommand OpenVisionCaptureDirCommand { get; }
        public ICommand OpenVision2dVectorCaptureDirCommand { get; }
        public ICommand CalibrateHistogramRoiCommand { get; }
        public ICommand CalibrateVision3dRoiCommand { get; }
        public ICommand CalibrateVisionTopViewRoiCommand { get; }
        public ICommand CalibrateSimple321ToolbarClickCommand { get; }
        public ICommand CalibrateSimple321OffsetEditCommand { get; }
        public ICommand CalibratePostAlignCaptureButtonCommand { get; }
        public ICommand CalibratePostAlignDragArrowCommand { get; }
        public ICommand PickHistogramMaskColorCommand { get; }
        public ICommand TestRenderingPresetCommand { get; }
        public ICommand TestThickPlateCommand { get; }
        public ICommand TestSliceExportCommand { get; }
        public ICommand TestHistogramGrayScaleCommand { get; }
        public ICommand TestSimple321AlignmentCommand { get; }
        public ICommand TestPostAlignCaptureDragCommand { get; }
        public ICommand TestOfflineCtVisionAnnotationCommand { get; }
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
        public ICommand SelectNavCommand { get; }
        public ICommand ApplyMainWindowSizeCommand { get; }
        public ICommand SaveCurrentMainWindowSizeCommand { get; }
        public ICommand RestoreDefaultMainWindowSizeCommand { get; }

        public event EventHandler? ImportLogScrollToEndRequested;

        public VM_CTAutoSlicerPage(
            ILoggerService<VM_CTAutoSlicerPage> logger,
            IProjectSettingRepository projectSettingRepository,
            ICTAutoSlicerService ctAutoSlicerService,
            IAngleAlgorithmService angleAlgorithmService,
            ICutImgAlgorithmService cutImgAlgorithmService,
            CTFolderMonitorService folderMonitorService,
            CTAutoSlicerInputHookService inputHookService,
            ProcessMonitorService processMonitorService)
        {
            _logger = logger;
            _projectSettingRepository = projectSettingRepository;
            _ctAutoSlicerService = ctAutoSlicerService;
            _angleAlgorithmService = angleAlgorithmService;
            _cutImgAlgorithmService = cutImgAlgorithmService;
            _folderMonitorService = folderMonitorService;
            _inputHookService = inputHookService;
            _processMonitorService = processMonitorService;

            OpenVGAppPathCommand = new RelayCommand(OpenVGAppPath);
            OpenCTFileFolderCommand = new RelayCommand(OpenCTFileFolder);
            OpenSliceExportFolderCommand = new RelayCommand(OpenSliceExportFolder);
            OpenHistogramCaptureDirCommand = new RelayCommand(OpenHistogramCaptureDir);
            OpenVisionCaptureDirCommand = new RelayCommand(OpenVisionCaptureDir);
            OpenVision2dVectorCaptureDirCommand = new RelayCommand(OpenVision2dVectorCaptureDir);
            CalibrateHistogramRoiCommand = new RelayCommand(ExecuteCalibrateHistogramRoi);
            CalibrateVision3dRoiCommand = new RelayCommand(ExecuteCalibrateVision3dRoi);
            CalibrateVisionTopViewRoiCommand = new RelayCommand(ExecuteCalibrateVisionTopViewRoi);
            CalibrateSimple321ToolbarClickCommand = new RelayCommand(ExecuteCalibrateSimple321ToolbarClick);
            CalibrateSimple321OffsetEditCommand = new RelayCommand(ExecuteCalibrateSimple321OffsetEdit);
            CalibratePostAlignCaptureButtonCommand = new RelayCommand(ExecuteCalibratePostAlignCaptureButton);
            CalibratePostAlignDragArrowCommand = new RelayCommand(ExecuteCalibratePostAlignDragArrow);
            PickHistogramMaskColorCommand = new RelayCommand(ExecutePickHistogramMaskColor);
            TestRenderingPresetCommand = new RelayCommandAsync(ExecuteTestRenderingPresetAsync);
            TestThickPlateCommand = new RelayCommandAsync(ExecuteTestThickPlateAsync);
            TestSliceExportCommand = new RelayCommandAsync(ExecuteTestSliceExportAsync);
            TestHistogramGrayScaleCommand = new RelayCommandAsync(ExecuteTestHistogramGrayScaleAsync);
            TestSimple321AlignmentCommand = new RelayCommandAsync(ExecuteTestSimple321AlignmentAsync);
            TestPostAlignCaptureDragCommand = new RelayCommandAsync(ExecuteTestPostAlignCaptureDragAsync);
            TestOfflineCtVisionAnnotationCommand = new RelayCommandAsync(ExecuteTestOfflineCtVisionAnnotationAsync);
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
            SelectNavCommand = new RelayCommand(ExecuteSelectNav);
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
            OnPropertyChanged(nameof(IsNavStepTest));
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
                    await Dispatcher.InvokeAsync(() =>
                        MessageShower = $"切图算法初始化失败：{cutEx.Message}（阶段四单测将不可用）");
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
            HistogramMaskColor = LoadStringSetting(nameof(HistogramMaskColor), "#6E6E6E");
            HistogramMaskTolerance = LoadIntSetting(nameof(HistogramMaskTolerance), 2);
            _dllDownloadBaseUrl = LoadStringSetting(nameof(CTAutoSlicerSettings.DllDownloadBaseUrl), CTAutoSlicerSettings.DefaultDllDownloadBaseUrl);
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
                LoadIntSetting(nameof(EnableOfflineCtVisionAnnotation), 1) != 0;
            OfflineCtModelPath = LoadStringSetting(nameof(OfflineCtModelPath), string.Empty);
            _offlineCtIsRunCuda = LoadIntSetting(nameof(OfflineCtIsRunCuda), 1) != 0;
            _offlineCtBlackHeight = LoadIntSetting(
                nameof(OfflineCtBlackHeight),
                CTAutoSlicerSettings.DefaultOfflineCtBlackHeight);
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
            _offlineCtRotateTopViewCw90 = LoadIntSetting(nameof(OfflineCtRotateTopViewCw90), 1) != 0;
            _enableOfflineCtPreBrighten = LoadIntSetting(nameof(EnableOfflineCtPreBrighten), 1) != 0;
            _offlineCtPreBrightenMeanTrigger = LoadDoubleSetting(nameof(OfflineCtPreBrightenMeanTrigger), 120.0);
            _offlineCtPreBrightenP50Trigger = LoadDoubleSetting(nameof(OfflineCtPreBrightenP50Trigger), 110.0);
            _offlineCtPreBrightenTargetMean = LoadDoubleSetting(nameof(OfflineCtPreBrightenTargetMean), 132.0);
            OfflineCtManualInputImagePath = LoadStringSetting(nameof(OfflineCtManualInputImagePath), string.Empty);
            OfflineCtManualOutputFolder = LoadStringSetting(nameof(OfflineCtManualOutputFolder), string.Empty);
            VisionLearn3dSampleDir = LoadStringSetting(
                nameof(VisionLearn3dSampleDir),
                CTAutoSlicerSettings.DefaultVisionLearn3dSampleDir);
            VisionLearnTopViewSampleDir = LoadStringSetting(
                nameof(VisionLearnTopViewSampleDir),
                CTAutoSlicerSettings.DefaultVisionLearnTopViewSampleDir);
            OnPropertyChanged(nameof(EnableOfflineCtVisionAnnotation));
            OnPropertyChanged(nameof(OfflineCtIsRunCuda));
            OnPropertyChanged(nameof(OfflineCtBlackHeight));
            OnPropertyChanged(nameof(OfflineCtTopViewSliceIndex));
            OnPropertyChanged(nameof(OfflineCtPixelSizeUm));
            OnPropertyChanged(nameof(OfflineCtPixelSizeMmPerPx));
            OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaUm));
            OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaMm));
            OnPropertyChanged(nameof(OfflineCtResultImageMode));
            OnPropertyChanged(nameof(OfflineCtResultImageModeDisplay));
            OnPropertyChanged(nameof(OfflineCtExportJson));
            OnPropertyChanged(nameof(OfflineCtExportCsv));
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

        private void SaveSetting(string name, object value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            if (_projectSettingRepository.Exists(model => model.Name == name && model.BelongToVM == VmName))
            {
                _projectSettingRepository.UpdateSettingValue(name, VmName, value);
            }
        }

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
                VisionCaptureDir = VisionCaptureDir,
                VisionCaptureKeepDays = CTAutoSlicerSettings.DefaultCaptureKeepDays,
                Vision2dVectorCaptureDir = Vision2dVectorCaptureDir,
                Vision2dVectorCaptureKeepDays = CTAutoSlicerSettings.DefaultCaptureKeepDays,
                EnableVisionAlignmentSteps = true,
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
                MainWindowRememberSize = MainWindowRememberSize,
                MainWindowWidth = MainWindowWidth,
                MainWindowHeight = MainWindowHeight,
                MainWindowLeft = MainWindowLeft,
                MainWindowTop = MainWindowTop,
                EnableOfflineCtVisionAnnotation = EnableOfflineCtVisionAnnotation,
                OfflineCtModelPath = OfflineCtModelPath,
                OfflineCtIsRunCuda = OfflineCtIsRunCuda,
                OfflineCtBlackHeight = OfflineCtBlackHeight,
                OfflineCtTopViewSliceIndex = OfflineCtTopViewSliceIndex,
                OfflineCtPixelSizeUm = OfflineCtPixelSizeUm,
                OfflineCtResultImageMode = (Core.Models.Project.CTAutoSlicer.OfflineCtResultImageMode)Math.Clamp(
                    _offlineCtResultImageMode,
                    0,
                    2),
                OfflineCtExportJson = OfflineCtExportJson,
                OfflineCtExportCsv = OfflineCtExportCsv,
                OfflineCtRotateTopViewCw90 = OfflineCtRotateTopViewCw90,
                EnableOfflineCtPreBrighten = EnableOfflineCtPreBrighten,
                OfflineCtPreBrightenMeanTrigger = OfflineCtPreBrightenMeanTrigger,
                OfflineCtPreBrightenP50Trigger = OfflineCtPreBrightenP50Trigger,
                OfflineCtPreBrightenTargetMean = OfflineCtPreBrightenTargetMean,
                VisionClickIntervalMs = 300,
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
                InitialDirectory = string.IsNullOrWhiteSpace(SliceExportFolder) ? null : SliceExportFolder,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                SliceExportFolder = dialog.FileName;
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

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 直方图灰度调整测试开始 ———");
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

            // 测试阶段使用本地剪影找点，不依赖 CTAutoCutImg.dll

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 简单 3-2-1 对齐测试开始（三点→偏移置0→向量两点→原点，本地找点） ———");
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
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
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
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteRunSimulationAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
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
                AppendFlowLog("——— 新批次开始 ———");
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
                _logger.Error("CT 切片流程异常", ex);
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
