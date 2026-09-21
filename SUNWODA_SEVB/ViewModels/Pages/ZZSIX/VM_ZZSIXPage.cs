using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using OxyPlot;
using OxyPlot.Series;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.ViewModels.Pages.ZZSIX
{
    /// <summary>
    /// ZZSIX · PACK 六面检（12 相机 + 前1/前3 码检 + 条码规则）。
    /// 布局对齐旧「检测界面」：左预览+日志，右功能设置/统计。
    /// </summary>
    [Module("VM_ZZSIXPage", "PACK六面检")]
    public class VM_ZZSIXPage : ViewModelBase
    {
        private readonly ILoggerService<VM_ZZSIXPage> _logger;
        private readonly IZZSIXPack6ConfigService _configService;
        private readonly IZZSIXPack6IngressService _ingress;
        private readonly IZZSIXPack6PipelineService _pipeline;
        private readonly IZZSIXPack6VisionEngine _vision;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly SemaphoreSlim _offlineLock = new(1, 1);
        private readonly Dictionary<string, Pack6CameraSlotVm> _slotsByCamera;
        private bool _initialized;
        private bool _allowProductProfilePersist;
        private bool _offlineBusy;
        private string _offlineTestStatus = "请选择检测项并选图。";
        private bool _isRunning;
        private bool _useSimulation;
        private string _statusText = "未启动";
        private string _productId = "-";
        private string _cellType = "TW";
        private string _productionLine = "P05";
        private string _productSnRule = string.Empty;
        private string _localPathPre = @"D:\六面检上位机软件";
        private string _yOkRootPath = @"Y:\OK原图";
        private string _zOkRootPath = @"Z:\OK原图";
        private string _lastFrameInfo = "-";
        private string _lastOcrInfo = "-";
        private string _lastRuleInfo = "-";
        private string _yieldRateText = "—";
        private string _totalCountText = "0";
        private string _okCountText = "0";
        private string _ngCountText = "0";
        private string _okPctText = "—";
        private string _ngPctText = "—";
        private int _ingressLookbackDays = 2;
        private int _ingressPollSeconds = 60;
        private int _packMissingTimeoutMinutes = 10;
        private Pack6ProductCodeProfile _selectedProductCodeProfile = Pack6ProductCodeCatalog.Default;

        public event Action? ConfigSummaryChanged;
        /// <summary>由 View 订阅并弹出设置窗，避免 VM 依赖 Views。</summary>
        public event Action? RequestOpenSettings;
        /// <summary>打开离线测试窗（选图调 DLL）。</summary>
        public event Action? RequestOpenOfflineTest;
        /// <summary>扫码/整包 NG（非缺张）时通知 View。</summary>
        public event Action<string>? RequestNgAlarm;
        /// <summary>缺张超时：黄色报警窗，参数为产品码与缺张明细。</summary>
        public event Action<string, string>? RequestMissingShotAlarm;

        public ObservableCollection<string> LogMessages { get; } = [];
        public ObservableCollection<Pack6CameraSlotVm> CameraSlots { get; } = [];
        public ObservableCollection<Pack6ResultLogItem> ResultLogs { get; } = [];
        public IReadOnlyList<Pack6StationStat> StationStats => _pipeline.StationStats;
        /// <summary>右侧检测项统计（不含一次优率，优率走饼图）。</summary>
        public IEnumerable<Pack6StationStat> DetectionItemStats =>
            StationStats.Where(s => s.Station != "一次优率");

        public PlotModel PieModel { get; }

        /// <summary>未启动时可改产品类型 / 打开功能设置 / 离线测试。</summary>
        public bool CanEditRuntimeConfig => !IsRunning;

        /// <summary>离线测试状态文案。</summary>
        public string OfflineTestStatus
        {
            get => _offlineTestStatus;
            set => SetProperty(ref _offlineTestStatus, value);
        }

        /// <summary>五种产品编码档位（主界面「功能设置」上方下拉）。</summary>
        public IReadOnlyList<Pack6ProductCodeProfile> ProductCodeProfiles { get; } =
            Pack6ProductCodeCatalog.All;

        public Pack6ProductCodeProfile SelectedProductCodeProfile
        {
            get => _selectedProductCodeProfile;
            set
            {
                var next = value ?? Pack6ProductCodeCatalog.Default;
                if (IsRunning)
                    return;
                if (SetProperty(ref _selectedProductCodeProfile, next))
                {
                    OnPropertyChanged(nameof(ExpectedPrefix10));
                    OnPropertyChanged(nameof(ProductCodeProfileName));
                    RaiseConfigSummaryChanged();
                    if (_allowProductProfilePersist)
                    {
                        _configService.Current.ProductCodeProfile = next.Name;
                        _ = PersistProductProfileAsync(next.Name);
                    }
                }
            }
        }

        /// <summary>主界面摘要用：仅显示档位名称。</summary>
        public string ProductCodeProfileName => SelectedProductCodeProfile.Name;

        /// <summary>当前档位前十位（校验用；主界面不展示）。</summary>
        public string ExpectedPrefix10 => SelectedProductCodeProfile.Prefix10;

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

        public string LastOcrInfo
        {
            get => _lastOcrInfo;
            set => SetProperty(ref _lastOcrInfo, value);
        }

        public string LastRuleInfo
        {
            get => _lastRuleInfo;
            set => SetProperty(ref _lastRuleInfo, value);
        }

        public string YieldRateText
        {
            get => _yieldRateText;
            set => SetProperty(ref _yieldRateText, value);
        }

        public string TotalCountText
        {
            get => _totalCountText;
            set => SetProperty(ref _totalCountText, value);
        }

        public string OkCountText
        {
            get => _okCountText;
            set => SetProperty(ref _okCountText, value);
        }

        public string NgCountText
        {
            get => _ngCountText;
            set => SetProperty(ref _ngCountText, value);
        }

        public string OkPctText
        {
            get => _okPctText;
            set => SetProperty(ref _okPctText, value);
        }

        public string NgPctText
        {
            get => _ngPctText;
            set => SetProperty(ref _ngPctText, value);
        }

        public string CellType
        {
            get => _cellType;
            set
            {
                if (SetProperty(ref _cellType, value))
                    RaiseConfigSummaryChanged();
            }
        }

        public string ProductionLine
        {
            get => _productionLine;
            set
            {
                if (SetProperty(ref _productionLine, value))
                    RaiseConfigSummaryChanged();
            }
        }

        public string ProductSNRule
        {
            get => _productSnRule;
            set => SetProperty(ref _productSnRule, value);
        }

        public string LocalPathPre
        {
            get => _localPathPre;
            set
            {
                if (SetProperty(ref _localPathPre, value))
                    RaiseConfigSummaryChanged();
            }
        }

        public string YOkRootPath
        {
            get => _yOkRootPath;
            set
            {
                if (SetProperty(ref _yOkRootPath, value))
                    RaiseConfigSummaryChanged();
            }
        }

        public string ZOkRootPath
        {
            get => _zOkRootPath;
            set
            {
                if (SetProperty(ref _zOkRootPath, value))
                    RaiseConfigSummaryChanged();
            }
        }

        public bool UseSimulation
        {
            get => _useSimulation;
            set
            {
                if (SetProperty(ref _useSimulation, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                    RaiseConfigSummaryChanged();
                }
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
                    OnPropertyChanged(nameof(CanEditRuntimeConfig));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>标注预览保留天数基数（原回看天数；已不做补扫）。</summary>
        public int IngressLookbackDays
        {
            get => _ingressLookbackDays;
            set => SetProperty(ref _ingressLookbackDays, Math.Clamp(value, 1, 30));
        }

        /// <summary>已废弃：收图不再轮询补扫。</summary>
        public int IngressPollSeconds
        {
            get => _ingressPollSeconds;
            set => SetProperty(ref _ingressPollSeconds, Math.Clamp(value, 0, 3600));
        }

        /// <summary>缺张超时分钟（界面设置；落库为秒）。默认 10。</summary>
        public int PackMissingTimeoutMinutes
        {
            get => _packMissingTimeoutMinutes;
            set => SetProperty(ref _packMissingTimeoutMinutes, Math.Clamp(value, 1, 60));
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenResultFolderCommand { get; }
        public ICommand OpenOfflineTestCommand { get; }
        public ICommand OfflineTestWhiteLabelCommand { get; }
        public ICommand OfflineTestNameplateCommand { get; }
        public ICommand SimulateOnePackCommand { get; }

        public VM_ZZSIXPage(
            ILoggerService<VM_ZZSIXPage> logger,
            IZZSIXPack6ConfigService configService,
            IZZSIXPack6IngressService ingress,
            IZZSIXPack6PipelineService pipeline,
            IZZSIXPack6VisionEngine vision)
        {
            _logger = logger;
            _configService = configService;
            _ingress = ingress;
            _pipeline = pipeline;
            _vision = vision;
            PieModel = CreatePieModel();
            RefreshPieChart(0, 0);

            foreach (var cam in Pack6CameraCatalog.Cameras)
            {
                var slot = new Pack6CameraSlotVm(cam);
                CameraSlots.Add(slot);
            }

            _slotsByCamera = CameraSlots.ToDictionary(s => s.CameraName, StringComparer.Ordinal);

            StartCommand = new RelayCommand(async () => await StartAsync(), _ => !IsRunning);
            StopCommand = new RelayCommand(async () => await StopAsync(), _ => IsRunning);
            ResetCommand = new RelayCommand(() =>
            {
                _pipeline.ResetStatistics();
                ResultLogs.Clear();
                RefreshYieldSummary();
            });
            SaveConfigCommand = new RelayCommand(async () => await SaveConfigAsync());
            OpenSettingsCommand = new RelayCommand(
                () => RequestOpenSettings?.Invoke(),
                _ => CanEditRuntimeConfig);
            OpenResultFolderCommand = new RelayCommand(OpenResultFolder);
            OpenOfflineTestCommand = new RelayCommand(
                () => RequestOpenOfflineTest?.Invoke(),
                _ => CanEditRuntimeConfig && !_offlineBusy);
            OfflineTestWhiteLabelCommand = new RelayCommand(
                async () => await RunOfflineOcrAsync(Pack6OcrMode.WhiteLabelThreeCode),
                _ => CanEditRuntimeConfig && !_offlineBusy);
            OfflineTestNameplateCommand = new RelayCommand(
                async () => await RunOfflineOcrAsync(Pack6OcrMode.NameplateProductSN),
                _ => CanEditRuntimeConfig && !_offlineBusy);
            SimulateOnePackCommand = new RelayCommand(
                SimulateOnePack,
                _ => IsRunning && UseSimulation);

            _pipeline.LogEmitted += OnLog;
            _pipeline.FrameCompleted += OnFrame;
            _pipeline.ProductCompleted += OnProduct;
            _ingress.ItemEnqueued += OnIngress;
            _ingress.StatusEmitted += OnIngressStatus;
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
            _ingress.StatusEmitted -= OnIngressStatus;
            base.OnCleanup();
        }

        public async Task SaveConfigFromSettingsAsync()
        {
            await PushUiToConfigAndSaveAsync(persist: true);
            RaiseConfigSummaryChanged();
            AppendLog("功能设置已保存");
        }

        private async Task PersistProductProfileAsync(string profileName)
        {
            try
            {
                var cfg = _configService.Current;
                cfg.ProductCodeProfile = profileName;
                await _configService.SaveRuntimeSettingsAsync(cfg);
                AppendLog($"产品类型已切换：{profileName}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"保存产品类型失败: {ex.Message}", false);
            }
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
                _allowProductProfilePersist = true;
                RaiseConfigSummaryChanged();
                RefreshYieldSummary();
                AppendLog("配置已加载（12相机 / Y·Z OK原图 / 条码规则 / 仅监视新文件）");
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
                AppendLog($"检测已启动 | Y={YOkRootPath} | Z={ZOkRootPath} | 档位={ProductCodeProfileName} | 仅新文件监视（不超前跑下一包）");
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
            RaiseConfigSummaryChanged();
            AppendLog("配置已保存");
        }

        private async Task PushUiToConfigAndSaveAsync(bool persist)
        {
            var cfg = _configService.Current;
            cfg.LocalPathPre = LocalPathPre.Trim();
            cfg.YOkRootPath = YOkRootPath.Trim();
            cfg.ZOkRootPath = ZOkRootPath.Trim();
            cfg.UseSimulation = UseSimulation;
            cfg.CellType = CellType.Trim();
            cfg.ProductionLine = ProductionLine.Trim();
            cfg.ProductSNRule = ProductSNRule.Trim();
            cfg.IngressLookbackDays = IngressLookbackDays;
            cfg.IngressPollSeconds = IngressPollSeconds;
            cfg.PackMissingTimeoutSeconds = Math.Clamp(PackMissingTimeoutMinutes * 60, 5, 3600);
            cfg.ProductCodeProfile = SelectedProductCodeProfile.Name;

            if (persist)
                await _configService.SaveRuntimeSettingsAsync(cfg);
        }

        private void ApplyConfigToUi(Pack6Config cfg)
        {
            LocalPathPre = cfg.LocalPathPre;
            YOkRootPath = cfg.YOkRootPath;
            ZOkRootPath = cfg.ZOkRootPath;
            UseSimulation = cfg.UseSimulation;
            CellType = cfg.CellType;
            ProductionLine = cfg.ProductionLine;
            ProductSNRule = cfg.ProductSNRule;
            IngressLookbackDays = cfg.IngressLookbackDays;
            IngressPollSeconds = cfg.IngressPollSeconds;
            PackMissingTimeoutMinutes = Math.Clamp(
                (int)Math.Round(cfg.PackMissingTimeoutSeconds / 60.0), 1, 60);
            SelectedProductCodeProfile = Pack6ProductCodeCatalog.Resolve(cfg.ProductCodeProfile);
        }

        private void SimulateOnePack()
        {
            var prefix = ExpectedPrefix10;
            var serial = DateTime.Now.ToString("HHmmssfff")[^5..];
            var code = (prefix + "P0SDG77" + serial).PadRight(24, '0')[..24];

            ProductId = code;
            foreach (var cam in Pack6CameraCatalog.Cameras)
            {
                for (var i = 1; i <= cam.DesignShotCount; i++)
                {
                    var fakePath = $"sim://{cam.QueueName}/{code}-{cam.CameraName}-{i}-OK_2026-01-01-00-00-00-000.jpg";
                    _pipeline.Enqueue(new Pack6IngressItem
                    {
                        FilePath = fakePath,
                        QueueName = cam.QueueName,
                        ProductCode = code,
                        CameraName = cam.CameraName,
                        ShotIndex = i
                    });
                }
            }
            AppendLog($"已注入模拟 Pack：{code}（档位={ProductCodeProfileName}）");
        }

        /// <summary>
        /// 离线：选本地图 → 强制真实 DLL → 码规则校验 → 刷新预览。不入队产线。
        /// </summary>
        private async Task RunOfflineOcrAsync(Pack6OcrMode mode)
        {
            if (IsRunning)
            {
                OfflineTestStatus = "请先停止自动检测，再做离线测试。";
                MessageBox.Show(OfflineTestStatus, "离线测试", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!await _offlineLock.WaitAsync(0))
                return;

            _offlineBusy = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                var title = mode == Pack6OcrMode.WhiteLabelThreeCode
                    ? "选择白标签（前1）图片"
                    : "选择铭牌（前3）图片";
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = title,
                    Filter = "图像文件|*.jpg;*.jpeg;*.png;*.bmp|所有文件|*.*",
                    Multiselect = false
                };
                if (dlg.ShowDialog() != true)
                {
                    OfflineTestStatus = "已取消选图。";
                    return;
                }

                var path = dlg.FileName;
                OfflineTestStatus = $"检测中… {Path.GetFileName(path)}";
                AppendLog($"[离线] 开始 {title}: {path}");

                await PushUiToConfigAndSaveAsync(persist: false);
                var cfg = _configService.Current;
                var wasSim = cfg.UseSimulation;
                cfg.UseSimulation = false;
                try
                {
                    await _vision.InitializeAsync(cfg);
                    var shot = mode == Pack6OcrMode.WhiteLabelThreeCode ? 1 : 3;
                    var item = new Pack6IngressItem
                    {
                        FilePath = path,
                        QueueName = "FrontQueue",
                        ProductCode = Pack6FileNameParser.ResolveFolderSn(path),
                        CameraName = "前",
                        ShotIndex = shot
                    };

                    var frame = await _vision.DetectAsync(item);
                    frame = ApplyOfflineBarcodeRules(frame, item, cfg);
                    PresentFrameOnUi(frame, raiseNgAlarm: false);

                    var summary = frame.IsOk
                        ? $"OK | {frame.StationName} | ProductSN={frame.ProductSN}"
                        : $"NG | {frame.StationName} | {string.Join('；', frame.Defects)}";
                    OfflineTestStatus = summary;
                    AppendLog($"[离线] {summary}");
                    MessageBox.Show(summary, "离线测试结果", MessageBoxButton.OK,
                        frame.IsOk ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
                finally
                {
                    cfg.UseSimulation = wasSim;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"离线测试失败: {ex.Message}", false);
                OfflineTestStatus = $"失败: {ex.Message}";
                AppendLog($"[离线] 失败: {ex.Message}");
                MessageBox.Show(OfflineTestStatus, "离线测试", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _offlineBusy = false;
                _offlineLock.Release();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private static Pack6FrameResult ApplyOfflineBarcodeRules(
            Pack6FrameResult frame,
            Pack6IngressItem item,
            Pack6Config config)
        {
            if (frame.OcrMode == Pack6OcrMode.None || string.IsNullOrWhiteSpace(frame.ProductSN))
                return frame;

            var folder = !string.IsNullOrWhiteSpace(frame.FolderSn)
                ? frame.FolderSn
                : item.ProductCode ?? Pack6FileNameParser.ResolveFolderSn(item.FilePath) ?? string.Empty;
            folder = folder.Trim().ToUpperInvariant();

            var rule = Pack6BarcodeRuleValidator.Validate(
                frame.ProductSN,
                folder,
                siblingProductSn: null,
                frame.QrData,
                siblingQrData: null,
                frame.OcrMode,
                config);

            if (rule.IsOk)
                return frame;

            var defects = frame.Defects.Concat(rule.Failures).Distinct().ToList();
            var preview = frame.PreviewImagePath;
            if (!string.IsNullOrWhiteSpace(rule.NgBanner))
            {
                var rewritten = Services.Project.ZZSIX.Pack6ResultOverlayHelper.TryWrite(
                    config,
                    item,
                    item.FilePath,
                    frame.OverlayBoxes,
                    algorithmError: frame.IsVisionAlgorithmError,
                    detailMessage: frame.EngineMessage,
                    ngBanner: rule.NgBanner,
                    isOk: false);
                if (!string.IsNullOrWhiteSpace(rewritten))
                    preview = rewritten!;
            }

            return new Pack6FrameResult
            {
                FilePath = frame.FilePath,
                QueueName = frame.QueueName,
                ProductCode = string.IsNullOrWhiteSpace(folder) ? frame.ProductCode : folder,
                CameraName = frame.CameraName,
                ShotIndex = frame.ShotIndex,
                FolderSn = folder,
                IsOk = false,
                Defects = defects,
                StationName = frame.StationName,
                DetectedAt = frame.DetectedAt,
                FromSimulation = frame.FromSimulation,
                OcrMode = frame.OcrMode,
                QrData = frame.QrData,
                PartSN = frame.PartSN,
                ProductSN = frame.ProductSN,
                EngineMessage = frame.EngineMessage,
                PreviewImagePath = preview,
                IsVisionAlgorithmError = frame.IsVisionAlgorithmError,
                NgBanner = rule.NgBanner,
                OverlayBoxes = frame.OverlayBoxes
            };
        }

        private void PresentFrameOnUi(Pack6FrameResult frame, bool raiseNgAlarm)
        {
            RunOnUi(() =>
            {
                ProductId = frame.ProductCode;
                LastFrameInfo =
                    $"{frame.StationName} | {(frame.IsOk ? "OK" : "NG")} | {Path.GetFileName(frame.FilePath)}";
                if (frame.OcrMode != Pack6OcrMode.None)
                {
                    LastOcrInfo = frame.OcrMode == Pack6OcrMode.WhiteLabelThreeCode
                        ? $"QR={frame.QrData}  Part={frame.PartSN}  Product={frame.ProductSN}"
                        : $"Product={frame.ProductSN}";
                    LastRuleInfo = frame.Defects.Count == 0
                        ? "规则OK"
                        : string.Join("；", frame.Defects);
                }

                if (!string.IsNullOrWhiteSpace(frame.CameraName) &&
                    _slotsByCamera.TryGetValue(frame.CameraName, out var slot))
                    slot.ApplyFrame(frame);
                else if (Pack6CameraCatalog.ByQueueName.TryGetValue(frame.QueueName, out var def) &&
                         _slotsByCamera.TryGetValue(def.CameraName, out var byQ))
                    byQ.ApplyFrame(frame);

                if (raiseNgAlarm && !frame.IsOk && frame.OcrMode != Pack6OcrMode.None)
                    NotifyNgAlarm(frame.ProductCode);
            });
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

        private void OnIngressStatus(string msg) =>
            RunOnUi(() => AppendLog(msg));

        private void OnLog(string msg) =>
            RunOnUi(() => AppendLog(msg));

        private int _uiFrameCounter;

        private void OnFrame(Pack6FrameResult frame) =>
            RunOnUi(() =>
            {
                ProductId = frame.ProductCode;
                LastFrameInfo =
                    $"{frame.StationName} | {(frame.IsOk ? "OK" : "NG")} | {Path.GetFileName(frame.FilePath)}";
                if (frame.OcrMode != Pack6OcrMode.None)
                {
                    LastOcrInfo = frame.OcrMode == Pack6OcrMode.WhiteLabelThreeCode
                        ? $"QR={frame.QrData}  Part={frame.PartSN}  Product={frame.ProductSN}"
                        : $"Product={frame.ProductSN}";
                    LastRuleInfo = frame.Defects.Count == 0
                        ? "规则OK"
                        : string.Join("；", frame.Defects);
                }

                if (!string.IsNullOrWhiteSpace(frame.CameraName) &&
                    _slotsByCamera.TryGetValue(frame.CameraName, out var slot))
                    slot.ApplyFrame(frame);
                else if (Pack6CameraCatalog.ByQueueName.TryGetValue(frame.QueueName, out var def) &&
                         _slotsByCamera.TryGetValue(def.CameraName, out var byQ))
                    byQ.ApplyFrame(frame);

                // 每帧刷新饼图开销大；节流降低 UI/托管内存抖动
                if (++_uiFrameCounter >= 8 || frame.OcrMode != Pack6OcrMode.None)
                {
                    _uiFrameCounter = 0;
                    RefreshYieldSummary();
                }

                // 码检工位任一检查项 NG → 非模态警示（不阻断管线）
                if (!frame.IsOk && frame.OcrMode != Pack6OcrMode.None)
                    NotifyNgAlarm(frame.ProductCode);
            });

        private void OnProduct(Pack6ProductResult result) =>
            RunOnUi(() =>
            {
                ProductId = result.ProductCode;
                var ngDetail = result.Defects.Count == 0
                    ? (result.TimedOut ? "缺张超时" : string.Empty)
                    : string.Join('；', result.Defects);
                ResultLogs.Insert(0, new Pack6ResultLogItem
                {
                    TimeText = result.CompletedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    Sn = result.ProductCode,
                    Result = result.IsOk ? "OK" : (result.TimedOut ? "NG(缺张)" : "NG"),
                    NgDetail = ngDetail
                });
                while (ResultLogs.Count > 200)
                    ResultLogs.RemoveAt(ResultLogs.Count - 1);

                AppendLog($"产品汇总 {result.ProductCode}: {(result.IsOk ? "OK" : "NG")}" +
                          (string.IsNullOrEmpty(ngDetail) ? string.Empty : $" | {ngDetail}"));
                RefreshYieldSummary();

                if (result.TimedOut)
                {
                    RequestMissingShotAlarm?.Invoke(
                        result.ProductCode,
                        string.IsNullOrWhiteSpace(ngDetail) ? "缺张超时" : ngDetail);
                }
                else if (!result.IsOk)
                {
                    NotifyNgAlarm(result.ProductCode);
                }
            });

        /// <summary>通知 View 弹出/追加 NG；列表归属与关窗清空由 View 负责。</summary>
        private void NotifyNgAlarm(string? productCode)
        {
            var sn = (productCode ?? string.Empty).Trim();
            if (sn.Length == 0 ||
                sn.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
                return;

            RequestNgAlarm?.Invoke(sn);
        }

        private void RefreshYieldSummary()
        {
            var yieldRow = StationStats.FirstOrDefault(s => s.Station == "一次优率");
            if (yieldRow is null || yieldRow.Total <= 0)
            {
                YieldRateText = "—";
                TotalCountText = "0";
                OkCountText = "0";
                NgCountText = "0";
                OkPctText = "—";
                NgPctText = "—";
                RefreshPieChart(0, 0);
                return;
            }

            var okRate = (double)yieldRow.OK / yieldRow.Total;
            var ngRate = (double)yieldRow.NG / yieldRow.Total;
            YieldRateText = $"{okRate:P1}";
            TotalCountText = yieldRow.Total.ToString();
            OkCountText = yieldRow.OK.ToString();
            NgCountText = yieldRow.NG.ToString();
            OkPctText = $"{okRate:P1}";
            NgPctText = $"{ngRate:P1}";
            RefreshPieChart(yieldRow.OK, yieldRow.NG);
        }

        private static PlotModel CreatePieModel()
        {
            var model = new PlotModel
            {
                IsLegendVisible = false,
                PlotMargins = new OxyThickness(0),
                Padding = new OxyThickness(2)
            };
            // OxyPlot：{0}=标签 {1}=数值 {2}=百分比
            model.Series.Add(new PieSeries
            {
                StrokeThickness = 1,
                InsideLabelPosition = 0.55,
                InsideLabelFormat = "{2:0.0}%",
                OutsideLabelFormat = "{0}",
                AngleSpan = 360,
                StartAngle = 90,
                Diameter = 0.85,
                FontSize = 11,
                TickHorizontalLength = 4,
                TickRadialLength = 4
            });
            return model;
        }

        private void RefreshPieChart(int ok, int ng)
        {
            if (PieModel.Series[0] is not PieSeries pie)
                return;
            pie.Slices.Clear();
            var total = ok + ng;
            if (total <= 0)
            {
                pie.Slices.Add(new PieSlice("暂无", 1)
                {
                    Fill = OxyColor.FromRgb(220, 224, 230)
                });
            }
            else
            {
                var okPct = 100.0 * ok / total;
                var ngPct = 100.0 * ng / total;
                pie.Slices.Add(new PieSlice($"OK {okPct:0.0}%", Math.Max(ok, 0))
                {
                    Fill = OxyColor.FromRgb(39, 174, 96)
                });
                pie.Slices.Add(new PieSlice($"NG {ngPct:0.0}%", Math.Max(ng, 0))
                {
                    Fill = OxyColor.FromRgb(231, 76, 60)
                });
            }
            PieModel.InvalidatePlot(true);
            OnPropertyChanged(nameof(PieModel));
        }

        private void RaiseConfigSummaryChanged() =>
            ConfigSummaryChanged?.Invoke();

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
            var d = Application.Current?.Dispatcher;
            if (d is null || d.CheckAccess())
            {
                action();
                return;
            }
            d.Invoke(action);
        }
    }

    /// <summary>检测日志表行（对齐旧界面 时间/SN/结果/NG详情）。</summary>
    public sealed class Pack6ResultLogItem
    {
        public string TimeText { get; init; } = string.Empty;
        public string Sn { get; init; } = string.Empty;
        public string Result { get; init; } = string.Empty;
        public string NgDetail { get; init; } = string.Empty;
    }
}
