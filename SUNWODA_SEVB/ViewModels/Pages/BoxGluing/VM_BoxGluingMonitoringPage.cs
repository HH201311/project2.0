using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.BoxGluing;
using SUNWODA_SEVB.Core.Models.Project.BoxGluing;

namespace SUNWODA_SEVB.ViewModels.Pages.BoxGluing
{
    /// <summary>
    /// 口琴管涂胶监测：出胶触发采集 / 曲线 / CSV。
    /// </summary>
    [Module("VM_BoxGluingMonitoringPage", "口琴管涂胶监测")]
    public class VM_BoxGluingMonitoringPage : ViewModelBase
    {
        private readonly ILoggerService<VM_BoxGluingMonitoringPage> _logger;
        private readonly IBoxGluingConfigService _configService;
        private readonly IBoxGluingAcquisitionService _acquisitionService;
        private readonly IBoxGluingChartDataService _chartDataService;

        private string? _statusText;
        private string _csvRootPath = @"D:\BoxGluingData";
        private int _sampleIntervalMs = 80;
        private bool _useSimulation;
        private bool _isRunning;
        private string _glueStateText = "出胶1:关  出胶2:关  出胶3:关";
        private int _plotFocus;
        private bool _suppressPersist;
        private bool _initialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public string? StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string CsvRootPath
        {
            get => _csvRootPath;
            set => SetProperty(ref _csvRootPath, value);
        }

        public int SampleIntervalMs
        {
            get => _sampleIntervalMs;
            set => SetProperty(ref _sampleIntervalMs, value);
        }

        public bool UseSimulation
        {
            get => _useSimulation;
            set
            {
                if (!SetProperty(ref _useSimulation, value))
                    return;
                _configService.Current.UseSimulation = value;
                if (!_suppressPersist)
                    _ = PersistRuntimeAsync();
                if (!value)
                    _acquisitionService.SetSimulatedStations(new HashSet<int>());
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        public string GlueStateText
        {
            get => _glueStateText;
            set => SetProperty(ref _glueStateText, value);
        }

        /// <summary>0=三图并排，1/2/3=该出胶全屏。</summary>
        public int PlotFocus
        {
            get => _plotFocus;
            set
            {
                if (SetProperty(ref _plotFocus, value))
                {
                    OnPropertiesChanged(nameof(MaximizeOverlayVisibility), nameof(MaximizedTitle));
                }
            }
        }

        public Visibility MaximizeOverlayVisibility =>
            PlotFocus == 0 ? Visibility.Collapsed : Visibility.Visible;

        public string MaximizedTitle => PlotFocus == 0 ? string.Empty : $"出胶{PlotFocus}";

        public IReadOnlyList<BoxGluingStationRealtime> RealtimeStations { get; } =
        [
            new BoxGluingStationRealtime(1),
            new BoxGluingStationRealtime(2),
            new BoxGluingStationRealtime(3)
        ];

        public ICommand SaveConfigCommand { get; }
        public ICommand OpenCsvFolderCommand { get; }
        public ICommand GlueTriggerCommand { get; }
        public ICommand MaximizePlotCommand { get; }
        public ICommand RestorePlotCommand { get; }

        public VM_BoxGluingMonitoringPage(
            ILoggerService<VM_BoxGluingMonitoringPage> logger,
            IBoxGluingConfigService configService,
            IBoxGluingAcquisitionService acquisitionService,
            IBoxGluingChartDataService chartDataService)
        {
            _logger = logger;
            _configService = configService;
            _acquisitionService = acquisitionService;
            _chartDataService = chartDataService;

            SaveConfigCommand = new RelayCommand(async () => await SaveConfigAsync());
            OpenCsvFolderCommand = new RelayCommand(OpenCsvFolder);
            GlueTriggerCommand = new RelayCommand(TriggerGlue, _ => UseSimulation);
            MaximizePlotCommand = new RelayCommand(MaximizePlot);
            RestorePlotCommand = new RelayCommand(() => PlotFocus = 0);

            _acquisitionService.StateChanged += OnAcquisitionStateChanged;
        }

        public override async void OnInitialize()
        {
            await InitializeMonitorAsync();
            base.OnInitialize();
        }

        public override async void OnNavigatedTo(object? parameter)
        {
            _acquisitionService.StateChanged -= OnAcquisitionStateChanged;
            _acquisitionService.StateChanged += OnAcquisitionStateChanged;
            await InitializeMonitorAsync();
            RefreshRuntimeFlags();
            base.OnNavigatedTo(parameter);
        }

        public override async void OnCleanup()
        {
            _acquisitionService.StateChanged -= OnAcquisitionStateChanged;
            await _acquisitionService.StopAsync();
            base.OnCleanup();
        }

        public BoxGluingChartSnapshot CaptureChart() => _chartDataService.CaptureSnapshot();

        /// <summary>
        /// 用曲线快照最后一点刷新底部实时数值，与绘图同一拍。
        /// </summary>
        public void RefreshRealtime(BoxGluingChartSnapshot snapshot)
        {
            var active = _acquisitionService.StationActive;
            foreach (var row in RealtimeStations)
            {
                var on = active.TryGetValue(row.Station, out var flag) && flag;
                row.Apply(
                    on,
                    LastY(snapshot, row.Station, "Torque", "A"),
                    LastY(snapshot, row.Station, "Torque", "B"),
                    LastY(snapshot, row.Station, "Pressure", "A"),
                    LastY(snapshot, row.Station, "Pressure", "B"),
                    LastY(snapshot, row.Station, "Level", "A"),
                    LastY(snapshot, row.Station, "Level", "B"));
            }
        }

        private static double? LastY(BoxGluingChartSnapshot snapshot, int station, string group, string side)
        {
            var series = snapshot.Series.FirstOrDefault(s =>
                s.Meter == station
                && string.Equals(s.Group, group, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.Side, side, StringComparison.OrdinalIgnoreCase));
            if (series is null || series.Ys.Length == 0)
                return null;
            return series.Ys[^1];
        }

        private async Task LoadConfigAsync()
        {
            var config = await _configService.LoadAsync();
            await RunOnUIThreadAsync(() =>
            {
                _suppressPersist = true;
                try
                {
                    CsvRootPath = config.CsvRootPath;
                    SampleIntervalMs = config.SampleIntervalMs;
                    UseSimulation = config.UseSimulation;
                }
                finally
                {
                    _suppressPersist = false;
                }
            });
        }

        private async Task SaveConfigAsync()
        {
            await _configService.SaveRuntimeSettingsAsync(CsvRootPath, SampleIntervalMs, UseSimulation);
            StatusText = "已保存采样周期 / CSV 根目录 / 模拟开关";
        }

        private async Task PersistRuntimeAsync()
        {
            try
            {
                await _configService.SaveRuntimeSettingsAsync(CsvRootPath, SampleIntervalMs, UseSimulation);
            }
            catch (Exception ex)
            {
                _logger.Error("保存BoxGluing运行参数失败", ex, false);
            }
        }

        private async Task InitializeMonitorAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                if (!_initialized)
                {
                    await LoadConfigAsync();
                    _initialized = true;
                }

                if (!_acquisitionService.IsRunning)
                    await _acquisitionService.StartAsync();
            }
            finally
            {
                _initLock.Release();
            }
        }

        private void TriggerGlue(object? parameter)
        {
            if (!int.TryParse(parameter?.ToString(), out var station) || station is < 1 or > 3)
                return;

            if (!UseSimulation)
            {
                StatusText = "非模拟模式：等待 PLC 出胶信号";
                return;
            }

            _acquisitionService.TriggerSimulatedGlue(station);
        }

        private void MaximizePlot(object? parameter)
        {
            if (int.TryParse(parameter?.ToString(), out var station) && station is >= 1 and <= 3)
                PlotFocus = station;
        }

        private void OnAcquisitionStateChanged(object? sender, EventArgs e)
        {
            RunOnUIThread(RefreshRuntimeFlags);
        }

        private void RefreshRuntimeFlags()
        {
            IsRunning = _acquisitionService.IsRunning;
            StatusText = _acquisitionService.LastStatus;
            var map = _acquisitionService.StationActive;
            var s1 = map.TryGetValue(1, out var a1) && a1 ? "开" : "关";
            var s2 = map.TryGetValue(2, out var a2) && a2 ? "开" : "关";
            var s3 = map.TryGetValue(3, out var a3) && a3 ? "开" : "关";
            GlueStateText = $"出胶1:{s1}  出胶2:{s2}  出胶3:{s3}  已采 {_acquisitionService.SampleCount}";
            CommandManager.InvalidateRequerySuggested();
        }

        private void OpenCsvFolder()
        {
            try
            {
                var path = CsvRootPath;
                if (string.IsNullOrWhiteSpace(path))
                    path = @"D:\BoxGluingData";
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Error("打开CSV目录失败", ex, true);
                StatusText = $"无法打开目录：{ex.Message}";
            }
        }
    }
}
