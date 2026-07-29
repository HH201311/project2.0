using ControlzEx.Standard;
using Dm.util;
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
using System.Reflection;
using System.Text;
using System.Windows.Input;
using System.Windows.Interop;

namespace SUNWODA_SEVB.ViewModels.Pages.JRCW
{
    /// <summary>
    /// 导航&按钮等界面功能
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
            _businessManager = businessManager;

            //_logger.OnLoggerReportEvent -= OnLoggerReport;
            //_logger.OnLoggerReportEvent += OnLoggerReport;

            ILoggerService.OnLoggerReportGlobalEvent -= OnLoggerReport;
            ILoggerService.OnLoggerReportGlobalEvent += OnLoggerReport;

            // 用户角色更新时触发
            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
            UserStateChangedEvent.OnUserStateChanged += OnUserStateChanged;
        }

        #region 
        /// <summary>
        /// 异步初始化
        /// </summary>
        /// <returns></returns>
        public override void OnInitialize()
        {
            InitializeLineSeriersModel();
            base.OnInitialize();
        }

        /// <summary>
        /// 异步初始化
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        protected override async Task OnInitializeAsync()
        {
            // 初始化PLC地址表
            await InitializePLCAddressParametersAsync();
            RefreshProductionInfiLoopAsync();
            RefreshPLCAddressesLoopAsync();

            #region 业务流程模块
            var businessType = _projectSettingRepository.LoadBusinessTypeProjectParameter();
            if (businessType == EBusinessType.BusinessType1) await _businessManager.StartBusinessAsync(EBusinessType.BusinessType1);
            else if (businessType == EBusinessType.BusinessType2) ;
            else if (businessType == EBusinessType.BusinessType2) ;
            else throw new Exception($"未实现业务类型 {businessType} !");

            BusinessType = businessType;
            Business1ParamModel = _projectSettingRepository.LoadBusiness1ParamProjectParameters();
            Business2ParamModel = _projectSettingRepository.LoadBusiness2ParamProjectParameters();
            #endregion

            await base.OnInitializeAsync();
        }

        /// <summary>
        /// 界面切换时触发
        /// </summary>
        /// <param name="parameter"></param>
        public override void OnNavigatedTo(object? parameter)
        {
            base.OnNavigatedTo(parameter);
        }
        #endregion

        #region PLC地址监控
        #region 曲线
        public PlotModel LineSeriersModel { get; set; } = new();

        private void InitializeLineSeriersModel()
        {
            LineSeriersModel.Title = "";
            LineSeriersModel.Subtitle = "";
            LineSeriersModel.IsLegendVisible = false;
            LineSeriersModel.PlotAreaBorderColor = OxyColors.Transparent;

            var series1 = new OxyPlot.Series.LineSeries
            {
                Title = "1",
                MarkerType = MarkerType.Circle,
                Color = OxyColor.Parse("#3355FF"),
                LabelFormatString = "{1}",
            };
            var series2 = new OxyPlot.Series.LineSeries
            {
                Title = "2",
                MarkerType = MarkerType.Circle,
                LabelFormatString = "{1}",
            };
            var series3 = new OxyPlot.Series.LineSeries
            {
                Title = "3",
                MarkerType = MarkerType.Circle,
                LabelFormatString = "{1}",
            };
            var series4 = new OxyPlot.Series.LineSeries
            {
                Title = "4",
                MarkerType = MarkerType.Circle,
                LabelFormatString = "{1}"
            };

            var dateTimeAxis1 = new OxyPlot.Axes.DateTimeAxis
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

            var ay1 = new LinearAxis()
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

            LineSeriersModel.Axes.Add(dateTimeAxis1);
            LineSeriersModel.Axes.Add(ay1);
            LineSeriersModel.Series.Add(series1);
            LineSeriersModel.Series.Add(series2);
            LineSeriersModel.Series.Add(series3);
            LineSeriersModel.Series.Add(series4);
        }

        private void AddLineSeriersModelPoint(int index, double value)
        {
            if (index >= LineSeriersModel.Series.Count) return;
            var series = LineSeriersModel.Series[index] as OxyPlot.Series.LineSeries;
            if (series == null) return;
            series.Points.Add(DateTimeAxis.CreateDataPoint(DateTime.Now, value));
            if (series.Points.Count > 8) series.Points.RemoveAt(0);
            //LineSeriersModel.InvalidatePlot(true);
        }

        private void ClearLineSeriersModelPoint(int index)
        {
            if (index >= LineSeriersModel.Series.Count) return;
            var series = LineSeriersModel.Series[index] as OxyPlot.Series.LineSeries;
            if (series == null || series.Points.Count <= 0) return;
            series.Points.Clear();
            //LineSeriersModel.InvalidatePlot(true);
        }
        #endregion

        #region PLC地址列表
        private List<string> lineNames = new List<string>() { "", "", "", "" };
        public ObservableCollection<ParameterModel> PLCAddressModels { get; set; } = [];
        public ICommand? SelectionChangedCommand => new RelayCommand(OnSelectionChanged);

        private void OnSelectionChanged(object? arg)
        {
            if (!(arg is IList args)) return;
            var names = new List<string>();
            foreach (ParameterModel model in args)
            {
                if (model == null || model.PName == null) continue;
                names.Add(model.PName);
            }

            for (int i = 0; i < 4; i++)
            {
                if (names.Contains(lineNames[i])) continue;
                lineNames[i] = string.Empty;
                ClearLineSeriersModelPoint(i);
            }
            LineSeriersModel.InvalidatePlot(true);
            for (int i = 0; i < 4; i++)
            {
                if (!string.IsNullOrEmpty(lineNames[i])) continue;
                foreach (var name in names)
                {
                    if (lineNames.Contains(name)) continue;
                    lineNames[i] = name;
                }
            }
        }

        /// <summary>
        /// 初始化界面PLC参数
        /// </summary>
        /// <returns></returns>
        private async Task InitializePLCAddressParametersAsync()
        {
            try
            {
                // CategoryId为1则显示在界面
                var addresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 1 && !string.IsNullOrEmpty(o.ParameterName)).ToList();
                foreach (var item in addresses)
                {
                    var model = new ParameterModel(item.ParameterName ?? "", item.MonitorValue?.ToString() ?? "");
                    await RunOnUIThreadAsync(() => PLCAddressModels.Add(model));
                }
            }
            catch (Exception ex)
            {
                _logger.Error("实时监控[PLC地址列表]初始化异常！", ex);
            }
        }

        /// <summary>
        /// 更新界面PLC参数
        /// </summary>
        /// <returns></returns>
        private async void RefreshPLCAddressesLoopAsync(CancellationToken ct = default)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    foreach (var plcAddressModel in PLCAddressModels)
                    {
                        if (plcAddressModel == null || plcAddressModel.PName == null) continue;

                        // 更新PLC地址列表
                        var value = _plcService.RWAddresses.Values.FirstOrDefault(o => o.CategoryId == 1 && o.ParameterName == plcAddressModel.PName)?.MonitorValue?.ToString();
                        // 只在值实际发生变化时更新UI
                        if (plcAddressModel.PValue != value) await RunOnUIThreadAsync(() => plcAddressModel.PValue = value);

                        // 更新PLC地址曲线
                        if (!lineNames.Contains(plcAddressModel.PName)) continue;
                        if (!(double.TryParse(value, out double valueDouble))) continue;
                        var index = lineNames.IndexOf(plcAddressModel.PName);
                        AddLineSeriersModelPoint(index, valueDouble);
                    }

                    LineSeriersModel.InvalidatePlot(true);
                }
                catch (Exception ex)
                {
                    _logger.Error("实时监控[PLC地址列表/折线图]更新异常！", ex);
                }
            }
        }
        #endregion
        #endregion

        #region 设备信息
        private ProductInfoModel productionInfoModel = new();
        public ProductInfoModel ProductionInfoModel { get => productionInfoModel; set => SetProperty(ref productionInfoModel, value); }

        /// <summary>
        /// 初始化设备状态信息
        /// </summary>
        /// <returns></returns>
        private async void RefreshProductionInfiLoopAsync(CancellationToken ct = default)
        {
            productionInfoModel.DeviceSn = _globalSettingRepository.GetByName("DeviceSn")?.Value ?? "";
            productionInfoModel.DeviceName = _globalSettingRepository.GetByName("DeviceName")?.Value ?? "";
            productionInfoModel.LineName = _globalSettingRepository.GetByName("workship")?.Value ?? "";

            var model = _plcConfigRepository.GetList(o => o.Name == CCTBCore.PLCName).FirstOrDefault();
            if (model == null || !model.IsEnable) productionInfoModel.PLCConnectStatus = "未启用";

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    var isConnected = _plcService.ConnectionStatus.ContainsKey(JRCWCore.PLCName) && _plcService.ConnectionStatus[JRCWCore.PLCName].Status;
                    productionInfoModel.PLCConnectStatus = isConnected ? "已连接" : "离线";
                }
                catch (Exception ex)
                {
                    _logger.Error("设备连接状态更新异常！", ex);
                }
            }
        }
        #endregion

        #region 日志
        public ObservableCollection<AppLogModel> AppLogs { get; set; } = [];

        /// <summary>
        /// 当log更新时触发
        /// </summary>
        /// <param name="logModel"></param>
        private async void OnLoggerReport(AppLogModel logModel)
        {
            if (logModel.LogLevel.ToUpper() == "DEBUG") return;

            await RunOnUIThreadAsync(() =>
            {
                AppLogs.Add(logModel);
                if (AppLogs.Count > 20) AppLogs.RemoveAt(0);
            });
        }
        #endregion

        #region 卷绕极耳错位闭环参数
        private UserRole userRole;
        private EBusinessType businessType;
        private Business1ParamModel business1Param = new();
        private Business2ParamModel business2Param = new();

        /// <summary>
        /// 当前的锅炉账号权限
        /// </summary>
        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        /// <summary>
        /// 业务类型
        /// </summary>
        public EBusinessType BusinessType { get => businessType; set => SetProperty(ref businessType, value); }

        /// <summary>
        /// 配置参数：业务类型1
        /// </summary>
        public Business1ParamModel Business1ParamModel { get => business1Param; set => SetProperty(ref business1Param, value); }

        /// <summary>
        /// 配置参数：业务类型2
        /// </summary>
        public Business2ParamModel Business2ParamModel { get => business2Param; set => SetProperty(ref business2Param, value); }

        public ICommand? BusinessParamCommand => new RelayCommand(OnBusinessParamCommandAsync);

        /// <summary>
        /// 登录用户
        /// </summary>
        /// <param name="userAccount"></param>
        private void OnUserStateChanged(string userAccount)
        {
            var roleId = _usersRepository.GetByUserAccount(userAccount)?.RoleId ?? 0;

            UserRole = (UserRole)roleId;
        }

        /// <summary>
        /// 当修改参数时触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnBusinessParamCommandAsync(object? arg)
        {
            if (!(arg is EBusinessType businessType)) return;

            var result = await ShowBusiness1ConfigDialog(businessType);

            if (!result) return;
        }

        /// <summary>
        /// 参数配置弹窗
        /// </summary>
        /// <param name="businessType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private async Task<bool> ShowBusiness1ConfigDialog(EBusinessType businessType)
        {
            var result = false;
            switch (businessType)
            {
                case EBusinessType.BusinessType1:
                    {
                        Business1ParamModel = _projectSettingRepository.LoadBusiness1ParamProjectParameters();
                        result = await Dialog.Show(new VM_Business1ParamDialog().UIElement)
                                .Initialize<VM_Business1ParamDialog>(vm =>
                                {
                                    vm.UserRole = UserRole;
                                    vm.ItemSource = Business1ParamModel;
                                }).GetResultAsync<bool>();
                        break;
                    }
                case EBusinessType.BusinessType2:
                    {
                        Business2ParamModel = _projectSettingRepository.LoadBusiness2ParamProjectParameters();
                        result = await Dialog.Show(new VM_Business2ParamDialog().UIElement)
                           .Initialize<VM_Business2ParamDialog>(vm =>
                           {
                               vm.UserRole = UserRole;
                               vm.ItemSource = Business2ParamModel;
                           }).GetResultAsync<bool>();
                        break;
                    }
                case EBusinessType.BusinessType3:
                    {
                        // -TODO
                        break;
                    }
                default: throw new ArgumentException($"未实现的业务类型 {businessType}");
            }

            return result;
        }
        #endregion
    }
}
