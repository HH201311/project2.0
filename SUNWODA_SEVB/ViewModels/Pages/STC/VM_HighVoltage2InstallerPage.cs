using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Component.UserControls;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Project.STC;
using SUNWODA_SEVB.Core.Models.Project.STC.Results;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Tool.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.STC
{
    [Module("VM_HighVoltage2InstallerPage", "强电连接与复拧2")]
    public class VM_HighVoltage2InstallerPage : ViewModelBase
    {
        private readonly ISocketServer _socketServer;
        private readonly ILoggerService<VM_HighVoltage2InstallerPage> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IScrewControllerService _screwControllerService;

        private string VM_NAME = string.Empty;

        private bool _initialized;
        private bool _isServerRunning;
        private int _port;
        private string _currentPackCode = string.Empty;
        private string _selectedScrewLabel = string.Empty;
        private bool _isLoadingCurveData;
        private ScrewControllerSettings? _screwSettings;
        private string _curveDataDirectory = @"D:\螺丝曲线\CSVData";
        private string? _overallResult;
        private ScrewCurveEntry? _selectedRecord;
        private string _manualScrewInput = string.Empty;
        private string _diagramImagePath = "./Assets/Images/强电2示意图.png";
        private bool _isStarting;
        private bool _hasCurves;
        private string _channelCode = "-";

        /// <summary>批量曲线下载的取消令牌；新包到达时取消上一批未完成的下载。</summary>
        private CancellationTokenSource? _batchCts;

        #region 属性

        public int Port { get => _port; set => SetProperty(ref _port, value); }
        public bool IsServerRunning
        {
            get => _isServerRunning;
            set
            {
                if (SetProperty(ref _isServerRunning, value))
                    OnPropertyChanged(nameof(CanStart));
            }
        }

        /// <summary>服务启动中（点击启动后立即置 true，避免重复点击）</summary>
        public bool IsStarting
        {
            get => _isStarting;
            set
            {
                if (SetProperty(ref _isStarting, value))
                    OnPropertyChanged(nameof(CanStart));
            }
        }

        /// <summary>启动按钮可用：非运行中且非启动中</summary>
        public bool CanStart => !IsServerRunning && !IsStarting;

        /// <summary>是否已加载曲线（用于曲线区空态占位显隐）</summary>
        public bool HasCurves
        {
            get => _hasCurves;
            set => SetProperty(ref _hasCurves, value);
        }
        public string CurrentPackCode { get => _currentPackCode; set => SetProperty(ref _currentPackCode, value); }
        public string SelectedScrewLabel { get => _selectedScrewLabel; set => SetProperty(ref _selectedScrewLabel, value); }
        public bool IsLoadingCurveData { get => _isLoadingCurveData; set => SetProperty(ref _isLoadingCurveData, value); }
        public string OverallResultText => _overallResult ?? "-";

        /// <summary>当前选中的螺丝曲线条目</summary>
        public ScrewCurveEntry? SelectedRecord
        {
            get => _selectedRecord;
            set => SetProperty(ref _selectedRecord, value);
        }

        /// <summary>手动输入的螺丝编号或周期号</summary>
        public string ManualScrewInput
        {
            get => _manualScrewInput;
            set => SetProperty(ref _manualScrewInput, value);
        }
        /// <summary>示意图路径（从数据库 ProjectSetting 加载，可配置）</summary>
        public string DiagramImagePath
        {
            get => _diagramImagePath;
            set => SetProperty(ref _diagramImagePath, value);
        }
        /// <summary>
        /// 数据库配置的通道编码：A/B
        /// </summary>
        public string ChannelCode
        {
            get => _channelCode;
            set
            {
                if (SetProperty(ref _channelCode, value))
                {
                    OnPropertyChanged(nameof(ChannelDisplayText));
                }
            }
        }

        /// <summary>
        /// 界面显示的完整通道名称
        /// </summary>
        public string ChannelDisplayText =>
            ChannelCode is "A" or "B" ? $"强电2-{ChannelCode}通道" : "强电2-未知通道";
        #endregion

        #region 集合

        /// <summary>螺丝曲线条目列表（一条 = 一次拧紧/复拧）</summary>
        public ObservableCollection<ScrewCurveEntry> ScrewRecords { get; } = new();

        /// <summary>日志</summary>
        public ObservableCollection<LogEntry> Logs { get; } = new();

        /// <summary>曲线集合（多曲线叠加，按 OK/NG 着色：OK绿/NG红）</summary>
        public ObservableCollection<PlotDataSeries> SelectedPlotSeries { get; } = new();

        #endregion

        #region 命令

        public ICommand StartServerCommand { get; }
        public ICommand StopServerCommand { get; }
        public ICommand ClearChartsCommand { get; }
        public ICommand ResetAllStatusCommand { get; }
        /// <summary>点击列表行：选中并取该条曲线</summary>
        public ICommand SelectRecordCommand { get; }
        /// <summary>手动输入编号/周期号取曲线</summary>
        public ICommand FetchCurveManualCommand { get; }
        public ICommand LoadLocalCsvCommand { get; }

        #endregion

        public VM_HighVoltage2InstallerPage(
            ILoggerService<VM_HighVoltage2InstallerPage> logger,
            ISocketServer socketServer,
            IServiceProvider serviceProvider,
            IScrewControllerService screwControllerService)
        {
            _logger = logger;
            _socketServer = socketServer;
            _serviceProvider = serviceProvider;
            _screwControllerService = screwControllerService;

            StartServerCommand = new RelayCommand(async _ => await StartServerAsync(), _ => CanStart);
            StopServerCommand = new RelayCommand(async _ => await StopServerAsync(), _ => IsServerRunning);
            ClearChartsCommand = new RelayCommand(_ => ClearAllData(), _ => !IsServerRunning);
            ResetAllStatusCommand = new RelayCommand(_ => ResetAllStatus(), _ => true);
            SelectRecordCommand = new RelayCommand<ScrewCurveEntry>(async rec => await OnSelectRecordAsync(rec));
            FetchCurveManualCommand = new RelayCommand(async _ => await FetchCurveManualAsync(), _ => !IsLoadingCurveData);
            LoadLocalCsvCommand = new RelayCommand(async _ => await LoadLocalCsvFileAsync(), _ => !IsServerRunning);

            _socketServer.ClientConnected += OnClientConnected;
            _socketServer.ClientDisconnected += OnClientDisconnected;
            _socketServer.DataReceived += OnDataReceived;
            _socketServer.ErrorOccurred += OnErrorOccurred;
        }

        #region 初始化

        protected override async Task OnNavigatedToAsync(object? parameter)
        {
            await base.OnNavigatedToAsync(parameter);

            if (!_initialized)
            {
                // 先加载端口、项目名称、控制器等配置
                await LoadSettingsAsync();

                _initialized = true;

                // 配置加载成功后自动启动服务
                if (!string.IsNullOrWhiteSpace(VM_NAME) && CanStart)
                {
                    await StartServerAsync();
                }
                else
                {
                    LogWarn("强电2: 配置未加载完成，未自动启动服务");
                }
            }
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var globalrepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                var vmNameSetting = await globalrepo.GetByNameAsync("CurrentVMName");
                if (string.IsNullOrEmpty(vmNameSetting?.Value))
                {
                    MessageBox.Show("未配置项目！");
                    return;
                }
                VM_NAME = vmNameSetting.Value;
                var repo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();
                var dataPort = await repo.GetByNameAndVMAsync("Port", VM_NAME);
                Port = (dataPort != null && int.TryParse(dataPort.Value, out int p) && p > 0) ? p : 8082;
                var dataPath = await repo.GetByNameAndVMAsync("DataPath", VM_NAME);
                if (dataPath != null && !string.IsNullOrEmpty(dataPath.Value)) _curveDataDirectory = dataPath.Value;
                var imgPath = await repo.GetByNameAndVMAsync("DiagramImage", VM_NAME);
                DiagramImagePath = (imgPath != null && !string.IsNullOrEmpty(imgPath.Value)) ? imgPath.Value : "Assets/Images/强电2示意图.png";
                var channelSetting = await repo.GetByNameAndVMAsync("Channel", VM_NAME);

                ChannelCode = NormalizeChannel(channelSetting?.Value);

                if (ChannelCode == "-")
                {
                    LogWarn("强电2: Channel 配置无效，应配置为 A 或 B");
                }
                _screwSettings = await _screwControllerService.LoadSettingsAsync(VM_NAME);
                LogInfo("强电2: 配置加载完成");
            }
            catch (Exception ex) { LogError($"强电2: 配置加载失败 {ex.Message}"); }
        }

        private static string NormalizeChannel(string? value)
        {
            var channel = value?.Trim().ToUpperInvariant();

            return channel switch
            {
                "A" => "A",
                "A通道" => "A",
                "CHANNEL_A" => "A",

                "B" => "B",
                "B通道" => "B",
                "CHANNEL_B" => "B",

                _ => "-"
            };
        }
        #endregion

        #region Socket

        private async Task StartServerAsync()
        {
            if (!CanStart) return;
            IsStarting = true;
            try
            {
                var ok = await _socketServer.StartAsync(Port, VM_NAME, null);
                if (ok) { IsServerRunning = true; LogInfo($"强电2: 服务启动成功 端口={Port}"); }
                else LogError("强电2: 服务启动失败");
            }
            catch (Exception ex) { LogError($"强电2: 服务启动异常: {ex.Message}"); }
            finally { IsStarting = false; }
        }

        private async Task StopServerAsync()
        {
            if (!IsServerRunning) return;
            _socketServer.ErrorOccurred -= OnErrorOccurred;
            try { await _socketServer.StopAsync(); } finally { _socketServer.ErrorOccurred += OnErrorOccurred; }
            IsServerRunning = false; LogInfo("强电2: 服务已停止");
        }

        private void OnClientConnected(object? sender, ClientConnectionEventArgs e) => LogInfo($"强电2: 客户端连接 {e.EndPoint}");
        private void OnClientDisconnected(object? sender, ClientConnectionEventArgs e) => LogInfo($"强电2: 客户端断开 {e.EndPoint}");
        private void OnErrorOccurred(object? sender, ServerErrorEventArgs e) => LogError($"强电2: 服务错误 {e.Message}");

        private void OnDataReceived(object? sender, Core.Models.Socket.DataReceivedEventArgs e)
        {
            var msg = e.GetDataAsString();
            if (string.IsNullOrWhiteSpace(msg) || msg.Trim('\x02', '\x03', '\0').Equals("Send", StringComparison.OrdinalIgnoreCase)) return;
            LogInfo($"强电2: 收到数据包 ({msg.Length} 字节)");
            var pkg = ScrewDataProtocolParser.TryParse(msg);
            if (pkg == null) { LogWarn("强电2: 数据包解析失败"); return; }
            ProcessPackage(pkg);
        }

        #endregion

        #region 包处理

        /// <summary>
        /// 处理整包：把每条 ScrewTighteningInfoItem 投影成列表条目（含状态/扭矩/角度/类型），
        /// 并自动批量下载本包所有螺丝曲线；点击列表/手动输入仍可重新下载单条。
        /// </summary>
        private void ProcessPackage(ScrewTighteningPackageResponse pkg)
        {
            if (pkg.ScrewTighteningInfoLis == null || pkg.ScrewTighteningInfoLis.Count == 0)
            { LogWarn("强电2: 包中无螺丝数据"); return; }

            try
            {
                _overallResult = pkg.OverallResult;
                RunOnUIThread(() =>
                {
                    CurrentPackCode = pkg.ProductCode ?? CurrentPackCode;
                    OnPropertyChanged(nameof(OverallResultText));
                    PopulateScrewRecords(pkg.ScrewTighteningInfoLis);
                    SelectedPlotSeries.Clear();
                    HasCurves = false;
                    SelectedRecord = null;
                    SelectedScrewLabel = $"共 {pkg.ScrewTighteningInfoLis.Count} 条，整体 {pkg.OverallResult}";

                    // socket 解析后自动批量下载本包所有螺丝曲线（非阻塞，新包到达时自动取消上一批）
                    // Pack 编号优先用 ProductCode，缺失时回退 ProductModel，供 CSV 命名
                    var packCode = pkg.ProductCode ?? pkg.ProductModel;
                    _ = FetchAllCurvesAsync(ScrewRecords.ToList(), packCode);
                });

                var ok = pkg.ScrewTighteningInfoLis.Count(i => i.IsScrewOk);
                var ng = pkg.ScrewTighteningInfoLis.Count - ok;
                LogInfo($"强电2: Pack={pkg.ProductModel} 条目={pkg.ScrewTighteningInfoLis.Count} 整体={pkg.OverallResult} OK={ok} NG={ng}");
            }
            catch (Exception ex) { LogError($"强电2: 处理包异常: {ex.Message}"); }
        }

        /// <summary>把整包螺丝条目投影到列表（清空后重建）</summary>
        private void PopulateScrewRecords(List<ScrewTighteningInfoItem> items)
        {
            ScrewRecords.Clear();
            foreach (var item in items)
            {
                if (item.ScrewNumberInt <= 0) continue;
                var entry = new ScrewCurveEntry
                {
                    ScrewNumber = item.ScrewNumberInt,
                    ScrewType = item.ScrwType,
                    ScrewResult = item.ScrewResult,
                    EorProcessCycleNumber = item.EorProcessCycleNumber,
                    ScrewTime = item.ScrewTime,
                    TightenControllerName = item.TightenControllerName,
                    TightenControllerIP = item.TightenControllerIP,
                    TorqueUpLimit = item.TorqueUpLimit,
                    TorqueLowLimit = item.TorqueLowLimit,
                    TargetTorque = item.TorqueActual,
                    TorqueValue = item.TorqueActualDouble,
                    AngleValue = item.AngleActualDouble,
                    Status = item.IsScrewOk ? ScrewStatus.OK : ScrewStatus.NG
                };
                ScrewRecords.Add(entry);
            }
        }

        private static System.Drawing.Color GetCurveColor(int screwNumber, string? typeLabel)
        {
            // 同一颗螺丝的“拧紧”和“复拧”使用相邻但不同的颜色
            int typeOffset = string.Equals(
                typeLabel,
                "复拧",
                StringComparison.Ordinal)
                ? 1
                : 0;

            int colorIndex = Math.Max(
                0,
                (screwNumber - 1) * 2 + typeOffset);

            return ScottPlotControl.GetPredefinedColor(colorIndex);
        }
        #endregion

        #region 曲线按需获取

        /// <summary>点击列表行：选中并按需下载该条曲线</summary>
        private async Task OnSelectRecordAsync(ScrewCurveEntry? rec)
        {
            if (rec == null) return;
            try
            {
                RunOnUIThread(() =>
                {
                    foreach (var x in ScrewRecords) x.IsSelected = false;
                    rec.IsSelected = true;
                    SelectedRecord = rec;
                });
                await FetchCurveForRecordAsync(rec);
            }
            catch (Exception ex) { LogError($"强电2: 选中记录异常: {ex.Message}"); }
        }

        /// <summary>手动输入：按 ScrewNumber 或 EorProcessCycleNumber 查找并取曲线</summary>
        private async Task FetchCurveManualAsync()
        {
            var input = (ManualScrewInput ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(input))
            {
                LogWarn("强电2: 请输入螺丝编号或周期号");
                return;
            }

            ScrewCurveEntry? rec = null;
            if (int.TryParse(input, out var n))
            {
                rec = ScrewRecords.FirstOrDefault(r => r.ScrewNumber == n)
                      ?? ScrewRecords.FirstOrDefault(r => r.EorCycleNumberInt == n);
            }

            if (rec == null)
            {
                LogWarn($"强电2: 未找到匹配记录: {input}");
                return;
            }

            await OnSelectRecordAsync(rec);
        }

        /// <summary>
        /// 下载单条螺丝曲线并展示。EorProcessCycleNumber 即 TraceLogId，直接用于曲线下载。
        /// </summary>
        /// <summary>
        /// 下载单条螺丝曲线并构建绘图序列（纯数据获取，不涉及UI状态）。
        /// </summary>
        /// <returns>成功返回 (series, curve, null)；失败返回 (null, null, 错误信息)。</returns>
        private async Task<(PlotDataSeries? Series, ScrewCurveDataResult? Curve, string? Error)> DownloadCurveSeriesAsync(ScrewCurveEntry rec)
        {
            if (_screwSettings == null) return (null, null, "螺丝控制器配置未加载");
            if (rec.EorCycleNumberInt <= 0) return (null, null, "EorProcessCycleNumber 无效");

            var controllerIP = rec.TightenControllerIP;
            if (string.IsNullOrEmpty(controllerIP))
            {
                if (!string.IsNullOrEmpty(rec.TightenControllerName))
                    controllerIP = _screwControllerService.GetControllerIPByTightenGun(_screwSettings, rec.TightenControllerName);
                if (string.IsNullOrEmpty(controllerIP))
                    controllerIP = _screwSettings.ControllerIP ?? "192.168.5.5";
            }

            var tr = await _screwControllerService.GetTrValueAsync(controllerIP, VM_NAME);
            var trValue = tr.IsSuccess ? tr.TrValue : 1.0f;

            LogInfo($"强电2: #{rec.ScrewNumber} -> IP={controllerIP}, TraceLogId={rec.EorCycleNumberInt}, TR={trValue:F4}");

            ScrewCurveDataResult? curve = null;
            for (int r = 0; r <= _screwSettings.RetryCount; r++)
            {
                curve = await _screwControllerService.GetCurveDataAsync(controllerIP, rec.EorCycleNumberInt, trValue, VM_NAME);
                if (curve?.IsSuccess == true && curve.DataPoints?.Count > 0) break;
                if (r < _screwSettings.RetryCount) await Task.Delay(_screwSettings.RetryIntervalMs);
            }

            if (curve?.IsSuccess != true || curve.DataPoints == null || curve.DataPoints.Count == 0)
                return (null, null, curve?.Message ?? "未知错误");

            var series = new PlotDataSeries
            {
                // 加上周期号，避免同一螺丝多次执行时无法区分
                Name = $"#{rec.ScrewNumber} " + $"{rec.TypeLabel} " + $"[{rec.ScrewResult}] " + $"Cycle={rec.EorCycleNumberInt}",

                Color = GetCurveColor(rec.ScrewNumber, rec.TypeLabel),

                DataPoints = new ObservableCollection<PlotDataPoint>(curve.DataPoints.Select(
                    (point, index) =>
                        new PlotDataPoint(
                            index + 1,
                            point.Torque))),

                // NG 曲线加粗，但不再统一使用红色
                LineWidth = rec.IsScrewOk ? 1.6f : 2.8f
            };
            return (series, curve, null);
        }

        private async Task FetchCurveForRecordAsync(ScrewCurveEntry rec)
        {
            if (rec == null) return;
            if (_screwSettings == null) { LogWarn("强电2: 螺丝控制器配置未加载"); return; }
            if (rec.EorCycleNumberInt <= 0)
            {
                LogWarn($"强电2: #{rec.ScrewNumber} EorProcessCycleNumber 无效，无法取曲线");
                return;
            }

            IsLoadingCurveData = true;
            var sw = Stopwatch.StartNew();
            try
            {
                var (series, curve, error) = await DownloadCurveSeriesAsync(rec);
                sw.Stop();
                if (series != null && curve != null)
                {
                    RunOnUIThread(() =>
                    {
                        // 去重：同编号已有曲线则移除旧的，再叠加新曲线
                        for (int i = SelectedPlotSeries.Count - 1; i >= 0; i--)
                        {
                            if (SelectedPlotSeries[i].Name != null &&
                                SelectedPlotSeries[i].Name!.StartsWith($"#{rec.ScrewNumber} ", StringComparison.Ordinal))
                                SelectedPlotSeries.RemoveAt(i);
                        }
                        SelectedPlotSeries.Add(series);
                        rec.HasCurve = true;
                        HasCurves = true;
                        var torque = rec.TorqueValue.HasValue ? rec.TorqueValue.Value.ToString("F2") : "-";
                        var angle = rec.AngleValue.HasValue ? rec.AngleValue.Value.ToString("F1") : "-";
                        SelectedScrewLabel = $"#{rec.ScrewNumber} · {rec.TypeLabel} · {rec.ScrewResult} · 扭矩{torque} · 角度{angle}";
                    });
                    LogInfo($"✓ 强电2: #{rec.ScrewNumber} 曲线下载成功 ({series.DataPoints?.Count}点, 耗时{sw.ElapsedMilliseconds}ms)");
                    // 手动下载：单独保存到"手动下载"文件夹，文件名 Cycle_{周期号}_{时间}.csv
                    await SaveManualCurveToCsvAsync(rec, curve);
                }
                else
                {
                    LogWarn($"✗ 强电2: #{rec.ScrewNumber} 曲线下载失败 (耗时{sw.ElapsedMilliseconds}ms): {error}");
                }
            }
            catch (Exception ex) { LogError($"强电2: #{rec.ScrewNumber} 曲线异常: {ex.Message}"); }
            finally
            {
                IsLoadingCurveData = false;
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// socket 解析后批量下载本包所有螺丝曲线。
        /// 顺序下载（Express 共用 session，避免并发 400），逐条成功即叠加到图上；
        /// 新包到达时取消上一批未完成的下载。
        /// </summary>
        private async Task FetchAllCurvesAsync(List<ScrewCurveEntry> records, string? packCode)
        {
            if (_screwSettings == null) { LogWarn("强电2: 螺丝控制器配置未加载"); return; }
            if (records == null || records.Count == 0) return;

            // 取消上一批未完成的下载
            _batchCts?.Cancel();
            _batchCts = new CancellationTokenSource();
            var ct = _batchCts.Token;

            IsLoadingCurveData = true;
            int ok = 0, fail = 0;
            var batchSw = Stopwatch.StartNew();
            // 收集本批成功下载的曲线，用于批次结束后统一写盘
            var successList = new List<(ScrewCurveEntry Rec, ScrewCurveDataResult Curve)>();

            try
            {
                foreach (var rec in records)
                {
                    if (ct.IsCancellationRequested) break;
                    if (rec.EorCycleNumberInt <= 0) continue;

                    var curveSw = Stopwatch.StartNew();
                    try
                    {
                        var (series, curve, error) = await DownloadCurveSeriesAsync(rec);
                        curveSw.Stop();
                        if (ct.IsCancellationRequested) break;

                        if (series != null && curve != null)
                        {
                            var sp = series;
                            RunOnUIThread(() =>
                            {
                                if (ct.IsCancellationRequested) return; // 新包已到，丢弃这条
                                SelectedPlotSeries.Add(sp);
                                rec.HasCurve = true;
                                HasCurves = true;
                            });
                            ok++;
                            successList.Add((rec, curve));
                            LogInfo($"✓ 强电2: #{rec.ScrewNumber} 曲线下载成功 ({series.DataPoints?.Count}点, 耗时{curveSw.ElapsedMilliseconds}ms)");
                        }
                        else
                        {
                            fail++;
                            LogWarn($"✗ 强电2: #{rec.ScrewNumber} 曲线下载失败 (耗时{curveSw.ElapsedMilliseconds}ms): {error}");
                        }
                    }
                    catch (Exception ex) { LogError($"强电2: #{rec.ScrewNumber} 曲线异常: {ex.Message}"); }
                }

                batchSw.Stop();
                LogInfo($"强电2: 批量曲线下载完成 成功={ok} 失败={fail} 耗时{batchSw.ElapsedMilliseconds}ms" + (ct.IsCancellationRequested ? "（已被新包取消）" : ""));

                // 未被取消且有成功曲线时，按 pack 编号统一写盘
                if (!ct.IsCancellationRequested && successList.Count > 0)
                {
                    await SaveBatchCurvesToCsvAsync(packCode, successList);
                }
            }
            finally
            {
                // 仅当本批未被取消时复位加载状态（被取消时由新批接管）
                if (!ct.IsCancellationRequested)
                {
                    IsLoadingCurveData = false;
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// 手动单条下载：保存到"手动下载"子目录，文件名 Cycle_{周期号}_{yyyyMMdd_HHmmss}.csv。
        /// 时间优先取 rec.ScrewTime（协议中的拧紧时间），解析失败则用当前时间。
        /// </summary>
        private async Task SaveManualCurveToCsvAsync(ScrewCurveEntry rec, ScrewCurveDataResult curve)
        {
            try
            {
                var saveDir = Path.Combine(_curveDataDirectory, "手动下载");
                Directory.CreateDirectory(saveDir);

                // 时间来源：优先 rec.ScrewTime，失败回退到当前时间
                DateTime stamp;
                if (!DateTime.TryParse(rec.ScrewTime, out stamp))
                    stamp = DateTime.Now;

                var baseName = $"Cycle_{rec.EorCycleNumberInt}_{stamp:yyyyMMdd_HHmmss}";
                var filePath = Path.Combine(saveDir, $"{baseName}.csv");
                int seq = 1;
                while (File.Exists(filePath))
                {
                    filePath = Path.Combine(saveDir, $"{baseName}_{seq}.csv");
                    seq++;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"# 螺丝#{rec.ScrewNumber} 周期号={rec.EorCycleNumberInt} 结果={rec.ScrewResult} 时间={stamp:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("PointIndex,Torque,Angle,Current");
                if (curve.DataPoints != null)
                {
                    for (int i = 0; i < curve.DataPoints.Count; i++)
                    {
                        var p = curve.DataPoints[i];
                        sb.AppendLine($"{i + 1},{p.Torque:F4},{p.Angle:F2},{p.Current:F4}");
                    }
                }

                await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
                LogInfo($"强电2: 手动曲线CSV已保存 → {filePath} ({curve.DataPoints?.Count ?? 0}点)");
            }
            catch (Exception ex)
            {
                LogError($"强电2: 保存手动曲线CSV失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 按 pack 包编号将一批曲线数据保存为 CSV。
        /// 目录：{_curveDataDirectory}\{yyyyMMdd}\；文件名：{ProductCode}.csv，重名加时间戳。
        /// 横向格式：每颗螺丝三列（Torque/Angle/Current），行为采样点索引。
        /// </summary>
        private async Task SaveBatchCurvesToCsvAsync(
            string? packCode,
            List<(ScrewCurveEntry Rec, ScrewCurveDataResult Curve)> items)
        {
            try
            {
                var dateFolder = DateTime.Now.ToString("yyyyMMdd");
                var saveDir = Path.Combine(_curveDataDirectory, dateFolder);
                Directory.CreateDirectory(saveDir);

                // 文件名：pack 编号；无 pack 编号则用时间戳兜底
                var baseName = string.IsNullOrWhiteSpace(packCode)
                    ? $"Unknown_{DateTime.Now:yyyyMMdd_HHmmss}"
                    : GetSafeFileName(packCode!);
                var filePath = Path.Combine(saveDir, $"{baseName}.csv");
                if (File.Exists(filePath))
                {
                    // 同一 pack 重复发包：新文件名加时间戳，两次数据都保留
                    filePath = Path.Combine(saveDir, $"{baseName}_{DateTime.Now:HHmmss}.csv");
                }

                // 按螺丝序号排序
                var sortedItems = items.OrderBy(x => x.Rec.ScrewNumber).ToList();
                var maxPoints = sortedItems.Max(x => x.Curve.DataPoints?.Count ?? 0);
                if (maxPoints == 0) { LogWarn("强电2: 全部曲线均为空，跳过写盘"); return; }

                var sb = new StringBuilder();

                // 头部元信息
                sb.AppendLine($"# Pack={packCode ?? "-"} 时间={DateTime.Now:yyyy-MM-dd HH:mm:ss} 螺丝数={sortedItems.Count}");

                // 表头：PointIndex, #1_Torque, #1_Angle, #1_Current, #2_Torque, ...
                sb.Append("PointIndex");
                foreach (var (rec, _) in sortedItems)
                {
                    var tag = $"#{rec.ScrewNumber}({rec.ScrewType})";
                    sb.Append($",{tag}_Torque_{rec.TargetTorque},{tag}_Angle,{tag}_Current");
                }
                sb.AppendLine();

                // 数据行
                for (int i = 0; i < maxPoints; i++)
                {
                    sb.Append(i + 1);
                    foreach (var (_, curve) in sortedItems)
                    {
                        if (curve.DataPoints != null && i < curve.DataPoints.Count)
                        {
                            var p = curve.DataPoints[i];
                            sb.Append($",{p.Torque:F4},{p.Angle:F2},{p.Current:F4}");
                        }
                        else
                        {
                            sb.Append(",,,"); // 短于最长的螺丝，用空占位
                        }
                    }
                    sb.AppendLine();
                }

                await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
                LogInfo($"强电2: 曲线CSV已保存 → {filePath} ({sortedItems.Count}颗螺丝, {maxPoints}点)");
            }
            catch (Exception ex)
            {
                LogError($"强电2: 保存曲线CSV失败: {ex.Message}");
            }
        }

        /// <summary>移除文件名非法字符</summary>
        private static string GetSafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            return string.IsNullOrEmpty(safe) ? $"Unknown_{DateTime.Now:HHmmss}" : safe;
        }

        #endregion

        #region 本地CSV导入

        /// <summary>本地导入用中间结构：一颗螺丝的全部采样点</summary>
        private sealed class LocalCurveScrew
        {
            public int ScrewNumber;
            public string? ScrewType;
            public bool IsOk = true;
            public List<(double Torque, double Angle, double Current)> Points = new();
        }

        /// <summary>打开本地CSV（按包号命名）并导入到折线图。服务运行中禁用。</summary>
        private async Task LoadLocalCsvFileAsync()
        {
            // 防御性二次确认：CanExecute 已禁用按钮，此处兜底
            if (IsServerRunning)
            {
                LogWarn("强电2: 服务运行中，无法导入本地曲线，请先停止服务");
                MessageBox.Show("服务运行中，无法导入本地曲线。\n请先点击「停止」后再导入。", "提示");
                return;
            }

            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择本地曲线CSV（按包号命名）",
                Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                RestoreDirectory = true,
                Multiselect = false,
            };
            if (Directory.Exists(_curveDataDirectory)) ofd.InitialDirectory = _curveDataDirectory;

            if (ofd.ShowDialog() != true) return;
            var filePath = ofd.FileName;
            if (!File.Exists(filePath)) { LogWarn($"强电2: 文件不存在: {filePath}"); return; }

            IsLoadingCurveData = true;
            try
            {
                LogInfo($"强电2: 开始导入本地曲线 -> {filePath}");
                var screws = await ReadLocalCsvAsync(filePath);
                if (screws.Count == 0)
                {
                    LogWarn("强电2: CSV未解析到有效曲线数据");
                    MessageBox.Show("CSV文件中未解析到有效曲线数据。", "提示");
                    return;
                }

                RunOnUIThread(() =>
                {
                    // 清空现有数据，再重建
                    ScrewRecords.Clear();
                    SelectedPlotSeries.Clear();
                    SelectedRecord = null;
                    _overallResult = null;

                    // 文件即按包号命名
                    CurrentPackCode = Path.GetFileNameWithoutExtension(filePath);

                    bool anyNg = false;
                    foreach (var s in screws)
                    {
                        var entry = new ScrewCurveEntry
                        {
                            ScrewNumber = s.ScrewNumber,
                            ScrewType = s.ScrewType,
                            ScrewResult = s.IsOk ? "OK" : "NG",
                            Status = s.IsOk ? ScrewStatus.OK : ScrewStatus.NG,
                            TorqueValue = s.Points.Count > 0 ? s.Points[^1].Torque : (double?)null,
                            AngleValue = s.Points.Count > 0 ? s.Points[^1].Angle : (double?)null,
                            HasCurve = s.Points.Count > 0,
                        };
                        ScrewRecords.Add(entry);
                        if (!s.IsOk) anyNg = true;

                        // 与 DownloadCurveSeriesAsync 保持一致：X=采样序号, Y=扭矩
                        var series = new PlotDataSeries
                        {
                            Name = $"#{s.ScrewNumber} " + $"{entry.TypeLabel} " + $"[{entry.ScrewResult}] [本地]",

                            Color = GetCurveColor(s.ScrewNumber, entry.TypeLabel),

                            DataPoints = new ObservableCollection<PlotDataPoint>(
                                s.Points.Select(
                                    (point, index) =>
                                        new PlotDataPoint(
                                            index + 1,
                                            point.Torque))),

                            LineWidth = s.IsOk ? 1.6f : 2.8f
                        };
                        SelectedPlotSeries.Add(series);
                    }

                    HasCurves = SelectedPlotSeries.Count > 0;
                    _overallResult = anyNg ? "NG" : "OK";
                    OnPropertyChanged(nameof(OverallResultText));
                    SelectedScrewLabel = $"本地导入：共 {ScrewRecords.Count} 条，整体 {_overallResult}";
                });

                LogInfo($"强电2: 本地曲线导入完成 - {screws.Count} 条螺丝");
                MessageBox.Show($"成功导入 {screws.Count} 条螺丝的曲线数据。", "导入完成");
            }
            catch (Exception ex)
            {
                LogError($"强电2: 导入本地曲线失败: {ex.Message}");
                MessageBox.Show($"导入失败：{ex.Message}", "错误");
            }
            finally
            {
                IsLoadingCurveData = false;
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>解析本地CSV，自动识别「批量包格式」与「单条手动格式」。</summary>
        private async Task<List<LocalCurveScrew>> ReadLocalCsvAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var result = new List<LocalCurveScrew>();
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);

                // 表头行 = 第一个非空、非注释（#）行
                int headerIdx = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    var t = lines[i].Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    headerIdx = i; break;
                }
                if (headerIdx < 0) return result;

                var header = lines[headerIdx].Split(',');
                var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < header.Length; i++) colIndex[header[i].Trim()] = i;

                // 单条手动格式：列头含纯 "Torque"（批量格式的列头是 #n(类型)_Torque_目标）
                if (colIndex.ContainsKey("Torque"))
                    return ParseManualFormat(lines, headerIdx, colIndex);

                // 批量包格式：收集每颗螺丝的列前缀
                var screwCols = new List<(int ScrewNumber, string? ScrewType, string Prefix, int TorqueCol, int AngleCol, int CurrentCol, int IsOkCol)>();
                foreach (var hh in header)
                {
                    var name = hh.Trim();
                    // 形如 #1(拧紧)_Torque_5.5  ；兼容参考格式 Screw1_Torque
                    var m = System.Text.RegularExpressions.Regex.Match(name, @"#(\d+)\(([^)]*)\)_Torque");
                    if (!m.Success)
                        m = System.Text.RegularExpressions.Regex.Match(name, @"Screw(\d+)_Torque");
                    if (!m.Success) continue;

                    int sn = int.Parse(m.Groups[1].Value);
                    string? type = m.Groups.Count > 2 && m.Groups[2].Success ? m.Groups[2].Value : null;
                    string prefix = name.Contains('#') ? $"#{sn}({type})" : $"Screw{sn}";

                    int tCol = colIndex[name];
                    int aCol = colIndex.TryGetValue($"{prefix}_Angle", out var a) ? a : -1;
                    int cCol = colIndex.TryGetValue($"{prefix}_Current", out var c) ? c : -1;
                    int okCol = colIndex.TryGetValue($"{prefix}_IsOk", out var ok) ? ok : -1;

                    screwCols.Add((sn, type, prefix, tCol, aCol, cCol, okCol));
                }
                if (screwCols.Count == 0) return result;

                var map = screwCols.ToDictionary(
                    sc => sc.ScrewNumber,
                    sc => new LocalCurveScrew { ScrewNumber = sc.ScrewNumber, ScrewType = sc.ScrewType });

                for (int r = headerIdx + 1; r < lines.Length; r++)
                {
                    var line = lines[r];
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
                    var cells = line.Split(',');
                    foreach (var sc in screwCols)
                    {
                        double torque = TryCellDouble(cells, sc.TorqueCol);
                        if (double.IsNaN(torque)) continue; // 该螺丝此点无值（占位 ,,,）

                        var s = map[sc.ScrewNumber];
                        double angle = sc.AngleCol >= 0 ? TryCellDouble(cells, sc.AngleCol) : 0;
                        double current = sc.CurrentCol >= 0 ? TryCellDouble(cells, sc.CurrentCol) : 0;
                        s.Points.Add((torque, angle, current));

                        if (sc.IsOkCol >= 0 && bool.TryParse(TryCellString(cells, sc.IsOkCol), out var b))
                            s.IsOk = b;
                    }
                }

                result.AddRange(map.Values.Where(x => x.Points.Count > 0));
                return result;
            });
        }

        /// <summary>单条手动格式：从注释行取螺丝号/结果，PointIndex,Torque,Angle,Current 取点。</summary>
        private List<LocalCurveScrew> ParseManualFormat(string[] lines, int headerIdx, Dictionary<string, int> colIndex)
        {
            var tCol = colIndex["Torque"];
            int aCol = colIndex.TryGetValue("Angle", out var a) ? a : -1;
            int cCol = colIndex.TryGetValue("Current", out var c) ? c : -1;

            var screw = new LocalCurveScrew { ScrewNumber = 1 };
            for (int i = 0; i < headerIdx; i++)
            {
                var cm = lines[i].Trim();
                if (!cm.StartsWith("#")) continue;
                var mSn = System.Text.RegularExpressions.Regex.Match(cm, @"螺丝#(\d+)");
                if (mSn.Success && int.TryParse(mSn.Groups[1].Value, out var sn)) screw.ScrewNumber = sn;
                var mRes = System.Text.RegularExpressions.Regex.Match(cm, @"结果=([^\s]+)");
                if (mRes.Success) screw.IsOk = !mRes.Groups[1].Value.Equals("NG", StringComparison.OrdinalIgnoreCase);
            }

            for (int r = headerIdx + 1; r < lines.Length; r++)
            {
                var line = lines[r];
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
                var cells = line.Split(',');
                double torque = TryCellDouble(cells, tCol);
                if (double.IsNaN(torque)) continue;
                double angle = aCol >= 0 ? TryCellDouble(cells, aCol) : 0;
                double current = cCol >= 0 ? TryCellDouble(cells, cCol) : 0;
                screw.Points.Add((torque, angle, current));
            }

            return screw.Points.Count > 0 ? new List<LocalCurveScrew> { screw } : new List<LocalCurveScrew>();
        }

        private static double TryCellDouble(string[] cells, int idx)
        {
            if (idx < 0 || idx >= cells.Length) return double.NaN;
            return double.TryParse(cells[idx].Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : double.NaN;
        }
        private static string TryCellString(string[] cells, int idx) =>
            (idx >= 0 && idx < cells.Length) ? cells[idx].Trim() : string.Empty;

        #endregion

        #region 状态控制

        private void ResetAllStatus()
        {
            RunOnUIThread(() =>
            {
                foreach (var r in ScrewRecords)
                {
                    r.IsSelected = false;
                    r.HasCurve = false;
                }
                SelectedRecord = null;
                SelectedPlotSeries.Clear();
                HasCurves = false;
                _overallResult = null;
                OnPropertyChanged(nameof(OverallResultText));
                SelectedScrewLabel = string.Empty;
            });
            LogInfo("强电2: 已重置状态");
        }

        private void ClearAllData()
        {
            RunOnUIThread(() =>
            {
                ScrewRecords.Clear();
                SelectedPlotSeries.Clear();
                HasCurves = false;
                CurrentPackCode = string.Empty;
                SelectedRecord = null;
                _overallResult = null;
                OnPropertyChanged(nameof(OverallResultText));
                SelectedScrewLabel = string.Empty;
                ManualScrewInput = string.Empty;
            });
            LogInfo("强电2: 已清空所有数据");
        }

        #endregion

        #region 日志

        private void AddLog(string msg, CoreLogLevel lv, bool db = false, [CallerMemberName] string m = "", [CallerFilePath] string f = "", [CallerLineNumber] int ln = 0)
        {
            var e = new LogEntry { Timestamp = DateTime.Now, Level = lv, Message = msg };
            RunOnUIThread(() => { Logs.Insert(0, e); if (Logs.Count > 100) Logs.RemoveAt(Logs.Count - 1); });
            switch (lv) { case CoreLogLevel.Info: _logger.Info(msg, db, m, f, ln); break; case CoreLogLevel.Warn: _logger.Warn(msg, db, m, f, ln); break; case CoreLogLevel.Error: _logger.Error(msg, db, m, f, ln); break; }
        }
        public void LogInfo(string msg, [CallerMemberName] string m = "", [CallerFilePath] string f = "", [CallerLineNumber] int l = 0) => AddLog(msg, CoreLogLevel.Info, true, m, f, l);
        public void LogWarn(string msg, [CallerMemberName] string m = "", [CallerFilePath] string f = "", [CallerLineNumber] int l = 0) => AddLog(msg, CoreLogLevel.Warn, true, m, f, l);
        public void LogError(string msg, [CallerMemberName] string m = "", [CallerFilePath] string f = "", [CallerLineNumber] int l = 0) => AddLog(msg, CoreLogLevel.Error, true, m, f, l);

        #endregion

        public async ValueTask DisposeAsync()
        {
            _batchCts?.Cancel();
            _socketServer.ClientConnected -= OnClientConnected; _socketServer.ClientDisconnected -= OnClientDisconnected;
            _socketServer.DataReceived -= OnDataReceived; _socketServer.ErrorOccurred -= OnErrorOccurred;
            await _socketServer.DisposeAsync();
        }
    }
}