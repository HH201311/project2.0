using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
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
        private readonly CTFolderMonitorService _folderMonitorService;
        private readonly CTAutoSlicerInputHookService _inputHookService;
        private readonly ProcessMonitorService _processMonitorService;
        private CancellationTokenSource? _activeFlowCts;

        private string _messageShower = "CT自动化切片模块已加载";
        private bool _isEnableSimulationBtn = true;
        private string _vgAppPath = @"D:\App\VG\vgstudiomax20241.exe";
        private int _vgOpenWaitTime = CTAutoSlicerSettings.DefaultVGOpenWaitTime;
        private int _vgShowFrontWaitTime = 2000;
        private string _ctFileFolder = string.Empty;
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
        private double _sliceRangeStartMm = 20;
        private double _sliceRangeEndMm = 70;
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
        private string _histogramMaskColor = "#6E6E6E";
        private int _histogramMaskTolerance = 12;
        private string _histogramRoiDisplayText = "未标定";
        private System.Windows.Media.Brush _histogramMaskColorBrush =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x6E, 0x6E));

        public string MessageShower
        {
            get => _messageShower;
            set => SetProperty(ref _messageShower, value);
        }

        public bool IsEnableSimulationBtn
        {
            get => _isEnableSimulationBtn;
            set => SetProperty(ref _isEnableSimulationBtn, value);
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

        public double SliceRangeStartMm
        {
            get => _sliceRangeStartMm;
            set
            {
                if (SetProperty(ref _sliceRangeStartMm, value))
                {
                    SaveSetting(nameof(SliceRangeStartMm), value);
                }
            }
        }

        public double SliceRangeEndMm
        {
            get => _sliceRangeEndMm;
            set
            {
                if (SetProperty(ref _sliceRangeEndMm, value))
                {
                    SaveSetting(nameof(SliceRangeEndMm), value);
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

        public ICommand OpenVGAppPathCommand { get; }
        public ICommand OpenCTFileFolderCommand { get; }
        public ICommand OpenSliceExportFolderCommand { get; }
        public ICommand OpenHistogramCaptureDirCommand { get; }
        public ICommand CalibrateHistogramRoiCommand { get; }
        public ICommand PickHistogramMaskColorCommand { get; }
        public ICommand TestRenderingPresetCommand { get; }
        public ICommand TestThickPlateCommand { get; }
        public ICommand TestSliceExportCommand { get; }
        public ICommand TestHistogramGrayScaleCommand { get; }
        public ICommand RunSimulationCommand { get; }
        public ICommand ClearImportLogCommand { get; }

        public event EventHandler? ImportLogScrollToEndRequested;

        public VM_CTAutoSlicerPage(
            ILoggerService<VM_CTAutoSlicerPage> logger,
            IProjectSettingRepository projectSettingRepository,
            ICTAutoSlicerService ctAutoSlicerService,
            IAngleAlgorithmService angleAlgorithmService,
            CTFolderMonitorService folderMonitorService,
            CTAutoSlicerInputHookService inputHookService,
            ProcessMonitorService processMonitorService)
        {
            _logger = logger;
            _projectSettingRepository = projectSettingRepository;
            _ctAutoSlicerService = ctAutoSlicerService;
            _angleAlgorithmService = angleAlgorithmService;
            _folderMonitorService = folderMonitorService;
            _inputHookService = inputHookService;
            _processMonitorService = processMonitorService;

            OpenVGAppPathCommand = new RelayCommand(OpenVGAppPath);
            OpenCTFileFolderCommand = new RelayCommand(OpenCTFileFolder);
            OpenSliceExportFolderCommand = new RelayCommand(OpenSliceExportFolder);
            OpenHistogramCaptureDirCommand = new RelayCommand(OpenHistogramCaptureDir);
            CalibrateHistogramRoiCommand = new RelayCommand(ExecuteCalibrateHistogramRoi);
            PickHistogramMaskColorCommand = new RelayCommand(ExecutePickHistogramMaskColor);
            TestRenderingPresetCommand = new RelayCommandAsync(ExecuteTestRenderingPresetAsync);
            TestThickPlateCommand = new RelayCommandAsync(ExecuteTestThickPlateAsync);
            TestSliceExportCommand = new RelayCommandAsync(ExecuteTestSliceExportAsync);
            TestHistogramGrayScaleCommand = new RelayCommandAsync(ExecuteTestHistogramGrayScaleAsync);
            RunSimulationCommand = new RelayCommandAsync(ExecuteRunSimulationAsync);
            ClearImportLogCommand = new RelayCommand(ClearImportLog);
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
            _inputHookService.Start(OnDoubleEscPressed, OnSingleEscPressed, OnCtrlXPressed);
            _folderMonitorService.Start(CTFileFolder);
            base.OnNavigatedTo(parameter);
        }

        public override void OnNavigatedFrom()
        {
            _inputHookService.Stop();
            _ctAutoSlicerService.Cancel();
            base.OnNavigatedFrom();
        }

        public override void OnCleanup()
        {
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
            SliceRangeStartMm = LoadDoubleSetting(nameof(SliceRangeStartMm), 20);
            SliceRangeEndMm = LoadDoubleSetting(nameof(SliceRangeEndMm), 70);
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
            HistogramMaskColor = LoadStringSetting(nameof(HistogramMaskColor), "#6E6E6E");
            HistogramMaskTolerance = LoadIntSetting(nameof(HistogramMaskTolerance), 12);
            _dllDownloadBaseUrl = LoadStringSetting(nameof(CTAutoSlicerSettings.DllDownloadBaseUrl), CTAutoSlicerSettings.DefaultDllDownloadBaseUrl);
            RefreshHistogramRoiDisplay();
            RefreshMaskColorBrush();
            _isLoadingSettings = false;
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
                SliceRangeStartMm = SliceRangeStartMm,
                SliceRangeEndMm = SliceRangeEndMm,
                SliceExportDialogWaitTime = SliceExportDialogWaitTime,
                SliceExportWriteTimeoutMs = SliceExportWriteTimeoutMs,
                HistogramRoiX = HistogramRoiX,
                HistogramRoiY = HistogramRoiY,
                HistogramRoiWidth = HistogramRoiWidth,
                HistogramRoiHeight = HistogramRoiHeight,
                HistogramCaptureDir = HistogramCaptureDir,
                HistogramMaskColor = HistogramMaskColor,
                HistogramMaskTolerance = HistogramMaskTolerance,
                DllDownloadBaseUrl = _dllDownloadBaseUrl,
            };
        }

        private void RefreshHistogramRoiDisplay()
        {
            HistogramRoiDisplayText = _histogramRoiWidth > 0 && _histogramRoiHeight > 0
                ? $"X={_histogramRoiX}, Y={_histogramRoiY}, W={_histogramRoiWidth}, H={_histogramRoiHeight}"
                : "未标定";
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
                    overlay = new HistogramRoiCalibrationOverlayWindow();
                    var ok = overlay.ShowDialog() == true;
                    if (!ok || overlay.SelectedScreenRect is not { } rect)
                    {
                        MessageShower = "已取消直方图 ROI 标定";
                        AppendFlowLog("直方图 ROI 标定已取消");
                        return;
                    }

                    _isLoadingSettings = true;
                    _histogramRoiX = rect.X;
                    _histogramRoiY = rect.Y;
                    _histogramRoiWidth = rect.Width;
                    _histogramRoiHeight = rect.Height;
                    OnPropertyChanged(nameof(HistogramRoiX));
                    OnPropertyChanged(nameof(HistogramRoiY));
                    OnPropertyChanged(nameof(HistogramRoiWidth));
                    OnPropertyChanged(nameof(HistogramRoiHeight));
                    _isLoadingSettings = false;

                    SaveSetting(nameof(HistogramRoiX), rect.X);
                    SaveSetting(nameof(HistogramRoiY), rect.Y);
                    SaveSetting(nameof(HistogramRoiWidth), rect.Width);
                    SaveSetting(nameof(HistogramRoiHeight), rect.Height);
                    RefreshHistogramRoiDisplay();

                    var msg =
                        $"直方图 ROI 已标定：X={rect.X}, Y={rect.Y}, W={rect.Width}, H={rect.Height}";
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
                MessageShower = $"直方图 ROI 标定失败：{ex.Message}";
                AppendFlowLog(MessageShower);
                _logger.Error("直方图 ROI 标定失败", ex);
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
                MessageShower = "请先点击「截图标定」框选直方图区域";
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

            var vglCount = Directory
                .EnumerateFiles(CTFileFolder, $"*{CTAutoSlicerSettings.VglFileExtension}")
                .Count();
            if (vglCount == 0)
            {
                MessageShower = "所选文件夹内未找到 .vgl 文件";
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

            if (message.StartsWith("@@IMPORT_FAIL@@", StringComparison.Ordinal))
            {
                var payload = message["@@IMPORT_FAIL@@".Length..];
                var separatorIndex = payload.IndexOf('|', StringComparison.Ordinal);
                var fileName = separatorIndex >= 0 ? payload[..separatorIndex] : payload;
                var reason = separatorIndex >= 0 ? payload[(separatorIndex + 1)..] : "未知原因";
                Dispatcher.Invoke(() =>
                {
                    MessageShower = $"导入失败：{fileName}";
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

        private void OnSingleEscPressed()
        {
            Dispatcher.Invoke(() => WinAPIHelper.BlockInput(true));
        }
    }
}
