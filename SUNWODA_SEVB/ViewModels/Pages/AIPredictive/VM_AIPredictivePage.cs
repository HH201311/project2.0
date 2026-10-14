using System.Windows.Input;
using System.Windows.Media;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive;
using SUNWODA_SEVB.Core.Models.Project.AIPredictive;
using SUNWODA_SEVB.Services.Project.AIPredictive;
using SUNWODA_SEVB.Services.Project.AIPredictive._TempBossReport;

namespace SUNWODA_SEVB.ViewModels.Pages.AIPredictive
{
    /// <summary>
    /// AI 预测分析页：启动后由 App 自动采集（无启停按钮）；
    /// 可选手导/赢合（互斥）与 PLC IP；端口固定 44818；保存后自动重连；状态灯。
    /// 另含【TEMP_BOSS_REPORT】临时资源监控（可删）。
    /// </summary>
    [Module("VM_AIPredictivePage", "AI预测分析")]
    public class VM_AIPredictivePage : ViewModelBase
    {
        private readonly ILoggerService<VM_AIPredictivePage> _logger;
        private readonly IAIPredictiveAcquisitionService _acquisition;
        private readonly IAIPredictivePlcEndpointStore _endpointStore;
        // TEMP_BOSS_REPORT — 删除监控功能时去掉本字段及全部 Monitor* 成员
        private readonly AIPredictiveTempRuntimeMonitor _tempMonitor;

        private string? _bindingString;
        private string? _statusText;
        private bool _isPlcConnected;
        private bool _isAcquisitionRunning;
        private bool _canTestConnection = true;
        private string _plcConnectionText = "未测试";
        private Brush _plcStatusBrush = Brushes.Red;
        private string _selectedVendor = AIPredictiveVendorNames.XianDao;
        private string _plcIpText = AIPredictivePlcEndpointStore.DefaultIp;
        private string _plcPortText = AIPredictiveVendorNames.FixedCipPort.ToString();
        private bool _canEditPlcEndpoint = true;
        private bool _canSavePlcEndpoint = true;
        private bool _suppressVendorIpSuggest;

        // TEMP_BOSS_REPORT
        private bool _canStartMonitor = true;
        private bool _canStopMonitor;
        private string _monitorStatusText = "临时监控未启动（汇报用，可删）";

        public string? BindingString
        {
            get => _bindingString;
            set => SetProperty(ref _bindingString, value);
        }

        public string? StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        /// <summary>PLC CIP 是否连通（采集运行中或最近测试成功）。</summary>
        public bool IsPlcConnected
        {
            get => _isPlcConnected;
            private set
            {
                if (!SetProperty(ref _isPlcConnected, value))
                    return;
                PlcConnectionText = value ? "已连接" : "未连接";
                PlcStatusBrush = value ? Brushes.LimeGreen : Brushes.Red;
            }
        }

        public string PlcConnectionText
        {
            get => _plcConnectionText;
            private set => SetProperty(ref _plcConnectionText, value);
        }

        public Brush PlcStatusBrush
        {
            get => _plcStatusBrush;
            private set => SetProperty(ref _plcStatusBrush, value);
        }

        /// <summary>厂家下拉选项（先导 / 赢合，互斥只跑一家）。</summary>
        public string[] VendorOptions => AIPredictiveVendorNames.All;

        public string SelectedVendor
        {
            get => _selectedVendor;
            set
            {
                var next = AIPredictiveVendorNames.Normalize(value);
                var previous = _selectedVendor;
                if (!SetProperty(ref _selectedVendor, next))
                    return;

                if (_suppressVendorIpSuggest)
                    return;

                // 切厂家时：若 IP 仍是旧厂家默认值，则换成新厂家默认 IP，避免误连错机
                var oldDefault = AIPredictiveVendorNames.DefaultIp(previous);
                if (string.IsNullOrWhiteSpace(PlcIpText)
                    || string.Equals(PlcIpText.Trim(), oldDefault, StringComparison.OrdinalIgnoreCase))
                {
                    PlcIpText = AIPredictiveVendorNames.DefaultIp(next);
                }

                PlcPortText = AIPredictiveVendorNames.FixedCipPort.ToString();
            }
        }

        public string PlcIpText
        {
            get => _plcIpText;
            set => SetProperty(ref _plcIpText, value);
        }

        /// <summary>只读展示；实际连接永远用 44818。</summary>
        public string PlcPortText
        {
            get => _plcPortText;
            set => SetProperty(ref _plcPortText, value);
        }

        public bool CanEditPlcEndpoint
        {
            get => _canEditPlcEndpoint;
            private set => SetProperty(ref _canEditPlcEndpoint, value);
        }

        public bool CanSavePlcEndpoint
        {
            get => _canSavePlcEndpoint;
            private set => SetProperty(ref _canSavePlcEndpoint, value);
        }

        public bool IsAcquisitionRunning
        {
            get => _isAcquisitionRunning;
            private set => SetProperty(ref _isAcquisitionRunning, value);
        }

        public bool CanTestConnection
        {
            get => _canTestConnection;
            private set => SetProperty(ref _canTestConnection, value);
        }

        // —— TEMP_BOSS_REPORT begin ——
        public bool CanStartMonitor
        {
            get => _canStartMonitor;
            private set => SetProperty(ref _canStartMonitor, value);
        }

        public bool CanStopMonitor
        {
            get => _canStopMonitor;
            private set => SetProperty(ref _canStopMonitor, value);
        }

        public string MonitorStatusText
        {
            get => _monitorStatusText;
            private set => SetProperty(ref _monitorStatusText, value);
        }

        public ICommand StartMonitorCommand { get; }
        public ICommand StopMonitorCommand { get; }
        // —— TEMP_BOSS_REPORT end ——

        public ICommand TestConnectionCommand { get; }
        public ICommand SavePlcEndpointCommand { get; }

        public VM_AIPredictivePage(
            ILoggerService<VM_AIPredictivePage> logger,
            IAIPredictiveAcquisitionService acquisition,
            IAIPredictivePlcEndpointStore endpointStore,
            AIPredictiveTempRuntimeMonitor tempMonitor)
        {
            _logger = logger;
            _acquisition = acquisition;
            _endpointStore = endpointStore;
            _tempMonitor = tempMonitor;
            _acquisition.StateChanged += OnAcquisitionStateChanged;
            _tempMonitor.StateChanged += OnTempMonitorStateChanged;

            TestConnectionCommand = new RelayCommandAsync(OnTestConnectionAsync);
            SavePlcEndpointCommand = new RelayCommandAsync(OnSavePlcEndpointAsync);
            StartMonitorCommand = new RelayCommandAsync(OnStartMonitorAsync);
            StopMonitorCommand = new RelayCommandAsync(OnStopMonitorAsync);
        }

        public override void OnInitialize()
        {
            BindingString = "AI预测分析";
            LoadPlcEndpointToUi();
            StatusText = "启动后自动采集；选厂家（先导/赢合互斥）+ IP，端口固定 44818；保存后自动重连";
            RefreshStatus();
            RefreshMonitorStatus();
            if (!_acquisition.IsRunning && !_acquisition.IsPlcConnected)
            {
                PlcConnectionText = "未测试";
                PlcStatusBrush = Brushes.Red;
            }

            _logger.Info("AIPredictive 页面初始化完成");
            base.OnInitialize();
        }

        protected override async Task OnNavigatedToAsync(object? parameter)
        {
            LoadPlcEndpointToUi();
            RefreshStatus();
            RefreshMonitorStatus();
            // 兜底：若 App 侧尚未拉起采集，进页时再试一次
            if (!_acquisition.IsRunning)
            {
                StatusText = "正在自动启动采集…";
                await _acquisition.StartAsync().ConfigureAwait(true);
                RefreshStatus();
            }

            await base.OnNavigatedToAsync(parameter).ConfigureAwait(false);
        }

        protected override async Task OnNavigatedFromAsync()
        {
            // 离页不停采（现场开机自启后台采集）
            // TEMP_BOSS_REPORT：离页仍停监控
            if (_tempMonitor.IsRunning)
                await _tempMonitor.StopAsync().ConfigureAwait(false);
            RefreshStatus();
            RefreshMonitorStatus();
            await base.OnNavigatedFromAsync().ConfigureAwait(false);
        }

        protected override async Task OnCleanupAsync()
        {
            _acquisition.StateChanged -= OnAcquisitionStateChanged;
            _tempMonitor.StateChanged -= OnTempMonitorStateChanged;
            if (_tempMonitor.IsRunning)
                await _tempMonitor.StopAsync().ConfigureAwait(false);
            await base.OnCleanupAsync().ConfigureAwait(false);
        }

        private void LoadPlcEndpointToUi()
        {
            var ep = _endpointStore.Load();
            _suppressVendorIpSuggest = true;
            try
            {
                SelectedVendor = AIPredictiveVendorNames.Normalize(ep.Vendor);
                PlcIpText = ep.Ip;
                PlcPortText = AIPredictiveVendorNames.FixedCipPort.ToString();
            }
            finally
            {
                _suppressVendorIpSuggest = false;
            }
        }

        private async Task OnSavePlcEndpointAsync()
        {
            if (string.IsNullOrWhiteSpace(PlcIpText))
            {
                StatusText = "PLC IP 不能为空";
                return;
            }

            var vendor = AIPredictiveVendorNames.Normalize(SelectedVendor);
            var port = AIPredictiveVendorNames.FixedCipPort;
            PlcPortText = port.ToString();

            CanSavePlcEndpoint = false;
            CanEditPlcEndpoint = false;
            try
            {
                _endpointStore.Save(new AIPredictivePlcEndpoint
                {
                    Vendor = vendor,
                    Ip = PlcIpText.Trim(),
                    Port = port,
                });

                if (_acquisition.IsRunning)
                    await _acquisition.StopAsync().ConfigureAwait(true);

                StatusText = $"已保存 厂家={vendor} {PlcIpText.Trim()}:{port}，正在重连采集…";
                await _acquisition.StartAsync().ConfigureAwait(true);
                StatusText = _acquisition.IsRunning
                    ? $"已按新配置启动采集 {_acquisition.LastStatus}"
                    : $"配置已保存，但启动失败：{_acquisition.LastStatus}";
            }
            catch (Exception ex)
            {
                StatusText = $"保存/重连失败：{ex.Message}";
                _logger.Error("AIPredictive 保存配置失败", ex, true);
            }
            finally
            {
                RefreshStatus();
            }
        }

        private async Task OnTestConnectionAsync()
        {
            // 采集运行中无法单独测连（会占用 CIP）；提示看状态灯
            if (_acquisition.IsRunning)
            {
                StatusText = "采集运行中，请看左侧连接灯/状态；改厂家/IP 请直接「保存」自动重连";
                return;
            }

            // 测试前先落盘当前 UI，保证测的是所选厂家
            if (string.IsNullOrWhiteSpace(PlcIpText))
            {
                StatusText = "PLC IP 不能为空";
                return;
            }

            try
            {
                _endpointStore.Save(new AIPredictivePlcEndpoint
                {
                    Vendor = AIPredictiveVendorNames.Normalize(SelectedVendor),
                    Ip = PlcIpText.Trim(),
                    Port = AIPredictiveVendorNames.FixedCipPort,
                });
            }
            catch (Exception ex)
            {
                StatusText = $"保存配置失败：{ex.Message}";
                return;
            }

            StatusText = "正在测试 PLC 连接…";
            CanTestConnection = false;
            CanSavePlcEndpoint = false;
            try
            {
                await _acquisition.TestConnectionAsync().ConfigureAwait(true);
            }
            finally
            {
                RefreshStatus();
            }
        }

        // TEMP_BOSS_REPORT
        private async Task OnStartMonitorAsync()
        {
            if (_tempMonitor.IsRunning)
                return;
            CanStartMonitor = false;
            await _tempMonitor.StartAsync().ConfigureAwait(true);
            RefreshMonitorStatus();
        }

        private async Task OnStopMonitorAsync()
        {
            if (!_tempMonitor.IsRunning)
                return;
            CanStopMonitor = false;
            await _tempMonitor.StopAsync().ConfigureAwait(true);
            RefreshMonitorStatus();
            if (!string.IsNullOrEmpty(_tempMonitor.LastResourceReportPath))
            {
                StatusText =
                    $"临时报告已写出：{_tempMonitor.LastResourceReportPath} ； {_tempMonitor.LastRateReportPath}";
            }
        }

        private void OnAcquisitionStateChanged(object? sender, EventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(RefreshStatus);
        }

        private void OnTempMonitorStateChanged(object? sender, EventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(RefreshMonitorStatus);
        }

        private void RefreshStatus()
        {
            IsPlcConnected = _acquisition.IsPlcConnected;
            IsAcquisitionRunning = _acquisition.IsRunning;
            // 采集中仍允许改厂家/IP 并保存（保存会停再启）；测试连接仅空闲时可用
            CanTestConnection = !_acquisition.IsRunning;
            CanEditPlcEndpoint = true;
            CanSavePlcEndpoint = true;

            var stats = _acquisition.LastRoundStats;
            if (stats is null || !_acquisition.IsRunning)
            {
                StatusText = _acquisition.LastStatus;
                return;
            }

            StatusText =
                $"{_acquisition.LastStatus}｜最近一轮总耗时 {stats.ElapsedMs} ms｜成功 {stats.SuccessCount} / 失败 {stats.FailCount}";
        }

        private void RefreshMonitorStatus()
        {
            CanStartMonitor = !_tempMonitor.IsRunning;
            CanStopMonitor = _tempMonitor.IsRunning;
            MonitorStatusText = _tempMonitor.LastStatus;
        }
    }
}
