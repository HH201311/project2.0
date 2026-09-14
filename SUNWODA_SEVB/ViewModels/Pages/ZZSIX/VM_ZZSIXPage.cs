using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.ViewModels.Pages.ZZSIX
{
    /// <summary>
    /// ZZSIX · PACK 六面检（自旧 LrzWork / VM_PagePack6Surface 迁移 P0）。
    /// </summary>
    [Module("VM_ZZSIXPage", "PACK六面检")]
    public class VM_ZZSIXPage : ViewModelBase
    {
        private readonly ILoggerService<VM_ZZSIXPage> _logger;
        private readonly IZZSIXPack6ConfigService _configService;
        private readonly IZZSIXPack6IngressService _ingress;
        private readonly IZZSIXPack6PipelineService _pipeline;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _initialized;
        private bool _isRunning;
        private bool _useSimulation = true;
        private string _statusText = "未启动";
        private string _productId = "-";
        private string _cellType = "X04C";
        private string _localPathPre = @"D:\ZZSIX_Pack6";
        private string _topCamPath = @"D:\Pack6Images\Top";
        private string _bottomCamPath = @"D:\Pack6Images\Bottom";
        private string _sideCamPath = @"D:\Pack6Images\Side";
        private string _lastFrameInfo = "-";

        public ObservableCollection<string> LogMessages { get; } = [];
        public IReadOnlyList<Pack6StationStat> StationStats => _pipeline.StationStats;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string ProductId
        {
            get => _productId;
            set => SetProperty(ref _productId, value);
        }

        public string LastFrameInfo
        {
            get => _lastFrameInfo;
            set => SetProperty(ref _lastFrameInfo, value);
        }

        public string CellType
        {
            get => _cellType;
            set => SetProperty(ref _cellType, value);
        }

        public string LocalPathPre
        {
            get => _localPathPre;
            set => SetProperty(ref _localPathPre, value);
        }

        public string TopCamPath
        {
            get => _topCamPath;
            set => SetProperty(ref _topCamPath, value);
        }

        public string BottomCamPath
        {
            get => _bottomCamPath;
            set => SetProperty(ref _bottomCamPath, value);
        }

        public string SideCamPath
        {
            get => _sideCamPath;
            set => SetProperty(ref _sideCamPath, value);
        }

        public bool UseSimulation
        {
            get => _useSimulation;
            set
            {
                if (SetProperty(ref _useSimulation, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    StatusText = value ? "运行中" : "已停止";
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand OpenResultFolderCommand { get; }
        public ICommand SimulateOnePackCommand { get; }

        public VM_ZZSIXPage(
            ILoggerService<VM_ZZSIXPage> logger,
            IZZSIXPack6ConfigService configService,
            IZZSIXPack6IngressService ingress,
            IZZSIXPack6PipelineService pipeline)
        {
            _logger = logger;
            _configService = configService;
            _ingress = ingress;
            _pipeline = pipeline;

            StartCommand = new RelayCommand(async () => await StartAsync(), _ => !IsRunning);
            StopCommand = new RelayCommand(async () => await StopAsync(), _ => IsRunning);
            ResetCommand = new RelayCommand(() => _pipeline.ResetStatistics());
            SaveConfigCommand = new RelayCommand(async () => await SaveConfigAsync());
            OpenResultFolderCommand = new RelayCommand(OpenResultFolder);
            SimulateOnePackCommand = new RelayCommand(
                SimulateOnePack,
                _ => IsRunning && UseSimulation);

            _pipeline.LogEmitted += OnLog;
            _pipeline.FrameCompleted += OnFrame;
            _pipeline.ProductCompleted += OnProduct;
            _ingress.ItemEnqueued += OnIngress;
        }

        public override async void OnInitialize()
        {
            await EnsureLoadedAsync();
            base.OnInitialize();
        }

        public override async void OnNavigatedTo(object? parameter)
        {
            await EnsureLoadedAsync();
            base.OnNavigatedTo(parameter);
        }

        public override async void OnCleanup()
        {
            await StopAsync();
            _pipeline.LogEmitted -= OnLog;
            _pipeline.FrameCompleted -= OnFrame;
            _pipeline.ProductCompleted -= OnProduct;
            _ingress.ItemEnqueued -= OnIngress;
            base.OnCleanup();
        }

        private async Task EnsureLoadedAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                if (_initialized)
                    return;
                var cfg = await _configService.LoadAsync();
                ApplyConfigToUi(cfg);
                AppendLog("配置已加载（P0：模拟视觉引擎，未接 ZZPack.AI）");
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task StartAsync()
        {
            try
            {
                await PushUiToConfigAndSaveAsync(persist: false);
                await _pipeline.StartAsync();
                await _ingress.StartAsync(_configService.Current);
                IsRunning = true;
                AppendLog("检测已启动");
            }
            catch (Exception ex)
            {
                _logger.Warn($"启动失败: {ex.Message}", false);
                AppendLog($"启动失败: {ex.Message}");
                IsRunning = false;
            }
        }

        private async Task StopAsync()
        {
            try
            {
                await _ingress.StopAsync();
                await _pipeline.StopAsync();
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task SaveConfigAsync()
        {
            await PushUiToConfigAndSaveAsync(persist: true);
            AppendLog("配置已保存");
        }

        private async Task PushUiToConfigAndSaveAsync(bool persist)
        {
            var cfg = _configService.Current;
            cfg.LocalPathPre = LocalPathPre.Trim();
            cfg.TopCamPath = TopCamPath.Trim();
            cfg.BottomCamPath = BottomCamPath.Trim();
            cfg.SideCamPath = SideCamPath.Trim();
            cfg.UseSimulation = UseSimulation;
            cfg.CellType = CellType.Trim();
            if (persist)
                await _configService.SaveRuntimeSettingsAsync(cfg);
        }

        private void ApplyConfigToUi(Pack6Config cfg)
        {
            LocalPathPre = cfg.LocalPathPre;
            TopCamPath = cfg.TopCamPath;
            BottomCamPath = cfg.BottomCamPath;
            SideCamPath = cfg.SideCamPath;
            UseSimulation = cfg.UseSimulation;
            CellType = cfg.CellType;
        }

        private void SimulateOnePack()
        {
            var code = $"SIM{DateTime.Now:HHmmss}";
            ProductId = code;
            foreach (var queue in Pack6DefaultConfig.QueueRequirements.Keys)
            {
                var need = Pack6DefaultConfig.QueueRequirements[queue];
                for (var i = 0; i < need; i++)
                    _ingress.EnqueueSimulated(queue, code);
            }
            AppendLog($"已注入模拟 Pack：{code}");
        }

        private void OpenResultFolder()
        {
            try
            {
                var dir = Path.Combine(LocalPathPre, "检测结果");
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppendLog($"打开目录失败: {ex.Message}");
            }
        }

        private void OnIngress(Pack6IngressItem item) =>
            _pipeline.Enqueue(item);

        private void OnLog(string msg) =>
            RunOnUi(() => AppendLog(msg));

        private void OnFrame(Pack6FrameResult frame) =>
            RunOnUi(() =>
            {
                ProductId = frame.ProductCode;
                LastFrameInfo = $"{frame.StationName} | {(frame.IsOk ? "OK" : "NG")} | {Path.GetFileName(frame.FilePath)}";
            });

        private void OnProduct(Pack6ProductResult result) =>
            RunOnUi(() =>
            {
                ProductId = result.ProductCode;
                AppendLog($"产品汇总 {result.ProductCode}: {(result.IsOk ? "OK" : "NG")}");
            });

        private void AppendLog(string msg)
        {
            LogMessages.Insert(0, msg.StartsWith("0") || msg.Contains(':')
                ? msg
                : $"{DateTime.Now:HH:mm:ss} {msg}");
            while (LogMessages.Count > 300)
                LogMessages.RemoveAt(LogMessages.Count - 1);
        }

        private static void RunOnUi(Action action)
        {
            var d = System.Windows.Application.Current?.Dispatcher;
            if (d is null || d.CheckAccess())
            {
                action();
                return;
            }
            d.Invoke(action);
        }
    }
}
