using ControlzEx.Standard;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools.Extension;
using HslCommunication;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Axes;
using Python.Runtime;
using SqlSugar;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Attributes.Project.JRCW;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Common.ObjectModel;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.JRCW;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.JRCW.ArithmeticModels;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Business1Models;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Business2Models;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Core;
using SUNWODA_SEVB.Core.Models.Project.JRCW.FormModels;
using SUNWODA_SEVB.Services.Project.JRCW.Business;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.JRCWHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using SUNWODA_SEVB.ViewModels.Dialogs.CoatingOPMM;
using SUNWODA_SEVB.ViewModels.Dialogs.JRCW;
using SUNWODA_SEVB.ViewModels.Dialogs.JRCW.BusinessDialogs;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Input;
using System.Windows.Interop;

namespace SUNWODA_SEVB.ViewModels.Pages.JRCW
{
    /// <summary>
    /// 导航&按钮等界面功能（性能优化版）
    /// </summary>
    [Module("VM_JRCWClosedLoopPage", "卷绕极耳错位闭环", Order = 0, Category = "VM_JRCWClosedLoopPage")]
    partial class VM_JRCWClosedLoopPage : ViewModelBase
    {
        private readonly ILoggerService<VM_JRCWClosedLoopPage> _logger;
        private readonly IPLCService _plcService;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;
        private readonly BusinessServiceFactory _businessFactory;
        private readonly BusinessServiceManager _businessManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_JRCWClosedLoopPage(
            ILoggerService<VM_JRCWClosedLoopPage> logger,
            IPLCService pLCService,
            IGlobalSettingRepository globalSettingRepository,
            IUsersRepository usersRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager,
            BusinessServiceFactory factory,
            BusinessServiceManager businessManager)
        {
            _logger = logger;
            _plcService = pLCService;
            _globalSettingRepository = globalSettingRepository;
            _usersRepository = usersRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
            _businessFactory = factory;
            _businessManager = businessManager;

            ILoggerService.OnLoggerReportGlobalEvent -= OnLoggerReport;
            ILoggerService.OnLoggerReportGlobalEvent += OnLoggerReport;

            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
            UserStateChangedEvent.OnUserStateChanged += OnUserStateChanged;
        }

        #region 生命周期管理
        private CancellationTokenSource _globalCts = new();
        private bool _isDisposed;

        /// <summary>
        /// 异步初始化
        /// </summary>
        public override void OnInitialize()
        {
            InitializeLineSeriersModel();
            base.OnInitialize();
        }

        /// <summary>
        /// 异步初始化
        /// </summary>
        protected override async Task OnInitializeAsync()
        {
            // 初始化PLC地址表
            await InitializePLCAddressParametersAsync();

            // 启动监控循环（传入取消令牌）
            _ = RefreshProductionInfoLoopAsync(_globalCts.Token);
            _ = RefreshPLCAddressesLoopAsync(_globalCts.Token);
            _ = RefreshPLCAddressLinesLoopAsync(_globalCts.Token);
            _ = RefreshAppLogsLoopAsync(_globalCts.Token);

            #region 业务流程模块
            BusinessType = _projectSettingRepository.LoadBusinessTypeProjectParameter();
            await _businessManager.StartBusinessAsync(BusinessType);
            #endregion

            await base.OnInitializeAsync();
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        protected override async Task OnCleanupAsync()
        {
            _isDisposed = true;
            _globalCts?.Cancel();
            _globalCts?.Dispose();

            ILoggerService.OnLoggerReportGlobalEvent -= OnLoggerReport;
            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;

            await _businessManager.StopBusinessAsync(BusinessType);
            await base.OnCleanupAsync();
        }
        #endregion

        #region 曲线显示（优化版）
        private DateTime _lastPlotUpdate = DateTime.MinValue;
        private readonly TimeSpan _plotUpdateInterval = TimeSpan.FromMilliseconds(200);
        private readonly object _plotLock = new object();

        public PlotModel LineSeriersModel { get; set; } = new();

        private void InitializeLineSeriersModel()
        {
            LineSeriersModel.Title = "";
            LineSeriersModel.Subtitle = "";
            LineSeriersModel.IsLegendVisible = false;
            LineSeriersModel.PlotAreaBorderColor = OxyColors.Transparent;

            // 批量添加序列
            for (int i = 1; i <= 4; i++)
            {
                var series = new OxyPlot.Series.LineSeries
                {
                    Title = i.ToString(),
                    MarkerType = MarkerType.Circle,
                    Color = i == 1 ? OxyColor.Parse("#3355FF") : OxyColors.Automatic,
                    LabelFormatString = "{1}",
                };
                LineSeriersModel.Series.Add(series);
            }

            var dateTimeAxis = new OxyPlot.Axes.DateTimeAxis
            {
                TicklineColor = OxyColors.Transparent,
                AxislineColor = OxyColor.Parse("#666666"),
                AxislineStyle = LineStyle.Solid,
                AxislineThickness = 1,
                StringFormat = "mm:ss",
                FontSize = 12,
                MinorStep = 1,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.Parse("#E6E6E6"),
                TextColor = OxyColor.Parse("#666666")
            };

            var linearAxis = new LinearAxis()
            {
                TicklineColor = OxyColors.Transparent,
                AxislineColor = OxyColors.Transparent,
                AxislineThickness = 1,
                MajorGridlineColor = OxyColor.Parse("#E6E6E6"),
                MajorGridlineStyle = LineStyle.Solid,
                FontSize = 12,
                TextColor = OxyColor.Parse("#666666"),
                Position = OxyPlot.Axes.AxisPosition.Left,
            };

            LineSeriersModel.Axes.Add(dateTimeAxis);
            LineSeriersModel.Axes.Add(linearAxis);
        }

        /// <summary>
        /// 添加曲线点数据（优化版）
        /// </summary>
        private void AddLineSeriersModelPoint(int index, double value)
        {
            if (index >= LineSeriersModel.Series.Count) return;
            if (LineSeriersModel.Series[index] is not OxyPlot.Series.LineSeries series) return;

            lock (_plotLock)
            {
                series.Points.Add(DateTimeAxis.CreateDataPoint(DateTime.Now, value));
                if (series.Points.Count > 8)
                {
                    series.Points.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// 清除曲线数据（优化版）
        /// </summary>
        private void ClearLineSeriersModelPoint(int index)
        {
            if (index >= LineSeriersModel.Series.Count) return;
            if (LineSeriersModel.Series[index] is not OxyPlot.Series.LineSeries series || series.Points.Count <= 0) return;

            lock (_plotLock)
            {
                series.Points.Clear();
            }
        }

        /// <summary>
        /// 带节流的曲线更新（优化版）
        /// </summary>
        private void UpdatePlotWithThrottle()
        {
            var now = DateTime.Now;
            if (now - _lastPlotUpdate < _plotUpdateInterval) return;

            _lastPlotUpdate = now;
            LineSeriersModel.InvalidatePlot(true);
        }
        #endregion

        #region PLC地址监控（优化版）
        private List<string> lineNames = new List<string>() { "", "", "", "" };
        public RangeObservableCollection<ParameterModel> PLCAddressModels { get; set; } = [];
        public ICommand? SelectionChangedCommand => new RelayCommand(OnSelectionChanged);

        private void OnSelectionChanged(object? arg)
        {
            if (arg is not IList args) return;
            var names = new List<string>();
            foreach (ParameterModel model in args)
            {
                if (model?.PName == null) continue;
                names.Add(model.PName);
            }

            // 优化：减少不必要的清除操作
            var changed = false;
            for (int i = 0; i < 4; i++)
            {
                if (names.Contains(lineNames[i])) continue;
                lineNames[i] = string.Empty;
                ClearLineSeriersModelPoint(i);
                changed = true;
            }

            if (changed) UpdatePlotWithThrottle();

            // 分配新的曲线名称
            for (int i = 0; i < 4; i++)
            {
                if (!string.IsNullOrEmpty(lineNames[i])) continue;
                foreach (var name in names)
                {
                    if (lineNames.Contains(name)) continue;
                    lineNames[i] = name;
                    break; // 找到一个就跳出内层循环
                }
            }
        }

        /// <summary>
        /// 初始化界面PLC参数（优化版）
        /// </summary>
        private async Task InitializePLCAddressParametersAsync()
        {
            try
            {
                var addresses = _plcService.RWAddresses.Values
                    .Where(o => o.CategoryId == 1 && !string.IsNullOrEmpty(o.ParameterName))
                    .ToList();

                // 批量添加到UI集合
                await RunOnUIThreadAsync(() =>
                {
                    using (PLCAddressModels.DelayNotifications())
                    {
                        PLCAddressModels.Clear();
                        foreach (var item in addresses)
                        {
                            var model = new ParameterModel(item.ParameterName ?? "", (string)Convert.ToString(item.MonitorValue));
                            PLCAddressModels.Add(model);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error("实时监控[PLC地址列表]初始化异常！", ex);
            }
        }

        // 缓存字典，避免重复构建
        private Dictionary<string, PLCRWAddress>? _cachedAddressDictionary;
        private DateTime _lastDictionaryUpdate = DateTime.MinValue;
        private readonly TimeSpan _dictionaryUpdateInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 获取或更新地址字典缓存
        /// </summary>
        private Dictionary<string, PLCRWAddress> GetAddressDictionary()
        {
            var now = DateTime.Now;
            if (_cachedAddressDictionary != null && now - _lastDictionaryUpdate < _dictionaryUpdateInterval)
            {
                return _cachedAddressDictionary;
            }

            _cachedAddressDictionary = _plcService.RWAddresses.Values
                .Where(o => o.CategoryId == 1 && !string.IsNullOrEmpty(o.ParameterName))
                .ToDictionary(o => o.ParameterName!, o => o);
            _lastDictionaryUpdate = now;

            return _cachedAddressDictionary;
        }

        /// <summary>
        /// 更新界面PLC列表数据（优化版）
        /// </summary>
        private async Task RefreshPLCAddressesLoopAsync(CancellationToken ct)
        {
            // 预构建查找字典
            var addressDictionary = GetAddressDictionary();

            // 使用更长的更新间隔，减少UI更新频率
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500)); // 从1000ms改为500ms，更平滑
            var lastUIUpdate = DateTime.MinValue;
            var uiUpdateInterval = TimeSpan.FromMilliseconds(200); // UI更新间隔200ms

            var updateBuffer = new Dictionary<ParameterModel, string>();
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    if (_isDisposed) break;
                    updateBuffer.Clear();

                    // 收集所有需要更新的数据
                    foreach (var plcAddressModel in PLCAddressModels)
                    {
                        if (plcAddressModel?.PName == null) continue;
                        if (!addressDictionary.TryGetValue(plcAddressModel.PName, out var address)) continue;

                        var valueStr = (string)Convert.ToString(address.MonitorValue);
                        if (plcAddressModel.PValue != valueStr)
                        {
                            updateBuffer[plcAddressModel] = valueStr;
                        }
                    }

                    // 批量更新UI（带节流）
                    if (updateBuffer.Count > 0)
                    {
                        var now = DateTime.Now;
                        if (now - lastUIUpdate >= uiUpdateInterval)
                        {
                            lastUIUpdate = now;
                            var snapshot = updateBuffer.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                            await RunOnUIThreadAsync(() =>
                            {
                                foreach (var kvp in snapshot)
                                {
                                    kvp.Key.PValue = kvp.Value;
                                }
                            });
                        }
                    }

                    // 定期更新缓存字典
                    if (DateTime.Now - _lastDictionaryUpdate >= _dictionaryUpdateInterval)
                    {
                        addressDictionary = GetAddressDictionary();
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，不需要处理
                    _logger.Info("实时监控[PLC地址列表]更新任务已退出！");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("实时监控[PLC地址列表]更新异常！", ex);
                }
            }
        }

        /// <summary>
        /// 更新界面PLC折线图数据（优化版）
        /// </summary>
        private async Task RefreshPLCAddressLinesLoopAsync(CancellationToken ct)
        {
            var addressDictionary = GetAddressDictionary();

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500)); // 从1000ms改为500ms
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    if (_isDisposed) break;
                    var hasUpdate = false;
                    var lineNamesSnapshot = lineNames.ToList(); // 创建快照避免并发问题

                    for (int i = 0; i < lineNamesSnapshot.Count; i++)
                    {
                        var lineName = lineNamesSnapshot[i];
                        if (string.IsNullOrEmpty(lineName)) continue;
                        if (!addressDictionary.TryGetValue(lineName, out var address)) continue;

                        var valueStr = (string)Convert.ToString(address.MonitorValue);
                        if (!double.TryParse(valueStr, out double valueDouble)) continue;

                        AddLineSeriersModelPoint(i, valueDouble);
                        hasUpdate = true;
                    }

                    if (hasUpdate) UpdatePlotWithThrottle();

                    // 定期更新缓存字典
                    if (DateTime.Now - _lastDictionaryUpdate >= _dictionaryUpdateInterval)
                    {
                        addressDictionary = GetAddressDictionary();
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，不需要处理
                    _logger.Info("实时监控[PLC地址折线图]更新任务已退出！");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("实时监控[PLC地址折线图]更新异常！", ex);
                }
            }
        }
        #endregion

        #region 设备信息（优化版）
        private ProductInfoModel productionInfoModel = new();
        public ProductInfoModel ProductionInfoModel { get => productionInfoModel; set => SetProperty(ref productionInfoModel, value); }

        /// <summary>
        /// 初始化设备状态信息（优化版）
        /// </summary>
        private async Task RefreshProductionInfoLoopAsync(CancellationToken ct)
        {
            // 初始化静态信息
            productionInfoModel.DeviceSn = _globalSettingRepository.GetByName("DeviceSn")?.Value ?? "";
            productionInfoModel.DeviceName = _globalSettingRepository.GetByName("DeviceName")?.Value ?? "";
            productionInfoModel.LineName = _globalSettingRepository.GetByName("workship")?.Value ?? "";

            var model = _plcConfigRepository.GetList(o => o.Name == CCTBCore.PLCName).FirstOrDefault();
            if (model == null || !model.IsEnable)
            {
                productionInfoModel.PLCConnectStatus = "未启用";
            }

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2)); // 从1秒改为2秒，减少查询频率
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    if (_isDisposed) break;
                    var isConnected = _plcService.ConnectionStatus.ContainsKey(JRCWCore.PLCName)
                            && _plcService.ConnectionStatus[JRCWCore.PLCName].Status;
                    var newStatus = isConnected ? "已连接" : "离线";

                    // 只在状态变化时更新UI
                    if (productionInfoModel.PLCConnectStatus != newStatus)
                    {
                        await RunOnUIThreadAsync(() =>
                        {
                            productionInfoModel.PLCConnectStatus = newStatus;
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，不需要处理
                    _logger.Info("设备连接状态监控任务已退出！");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("设备连接状态更新异常！", ex);
                }
            }
        }
        #endregion

        #region 日志（优化版）
        private const int MaxLogEntries = 20;
        private readonly object _logLock = new object();
        private List<AppLogModel> appLogsCache = new();
        public RangeObservableCollection<AppLogModel> AppLogs { get; set; } = [];

        private async void OnLoggerReport(AppLogModel logModel)
        {
            if (logModel.LogLevel.ToUpper() == "DEBUG" || _isDisposed) return;

            lock (_logLock) appLogsCache.Add(logModel);

        }

        /// <summary>
        /// 批量更新日志
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task RefreshAppLogsLoopAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    if (_isDisposed) break;
                    var cache = new List<AppLogModel>();
                    lock (_logLock)
                    {
                        if (appLogsCache.Count > 0)
                        {
                            cache.AddRange(appLogsCache);
                            appLogsCache.Clear();
                        }
                    }
                    if (cache.Count <= 0) continue;
                    // 直接添加日志，避免UI线程阻塞
                    await RunOnUIThreadAsync(() =>
                    {
                        using (AppLogs.DelayNotifications())
                        {
                            AppLogs.AddRange(cache);
                            while (AppLogs.Count > 50) AppLogs.RemoveAt(0);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，不需要处理
                    _logger.Info("日志界面刷新任务已退出！");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("日志刷新异常！", ex);
                }
            }
        }
        #endregion

        #region 业务参数（保持原样）
        private UserRole userRole;
        private EBusinessType businessType;

        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }
        public EBusinessType BusinessType { get => businessType; set => SetProperty(ref businessType, value); }
        public ICommand? BusinessParamCommand => new RelayCommand(OnBusinessParamCommandAsync);

        private void OnUserStateChanged(string userAccount)
        {
            var roleId = _usersRepository.GetByUserAccount(userAccount)?.RoleId ?? 0;
            UserRole = (UserRole)roleId;
        }

        private async void OnBusinessParamCommandAsync(object? arg)
        {
            if (arg is not EBusinessType businessType) return;
            var service = _businessFactory.GetService(businessType);
            await service.ShowBusinessParamConfigDialogAsync();
        }
        #endregion
    }
}