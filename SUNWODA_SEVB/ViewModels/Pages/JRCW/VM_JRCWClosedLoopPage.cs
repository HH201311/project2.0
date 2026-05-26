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
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.JRCW;
using SUNWODA_SEVB.Core.Models.Project.JRCW.ArithmeticModels;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Core;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.JRCWHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CoatingOPMM;
using SUNWODA_SEVB.ViewModels.Dialogs.JRCW;
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
            IModuleManager moduleManager
        )
        {
            _logger = logger;
            _plcService = pLCService;
            _globalSettingRepository = globalSettingRepository;
            _usersRepository = usersRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;

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
            SelectionChangedCommand = new RelayCommand(OnSelectionChanged);
            InitializeLineSeriersModel();
            InitializeJRCWParamProjectSettings();
            base.OnInitialize();
        }

        protected override async Task OnInitializeAsync()
        {
            // 初始化PLC地址表
            await InitializePLCAddressParametersAsync();
            await InitializeProductInfoParametersAsync();
            _ = RunInBackgroundAsync(RefreshConnectStatusLoopAsync);
            _ = RunInBackgroundAsync(RefreshPLCAddressesLoopAsync);

            #region 业务流程模块
            await InitializeClosedLoopAsync();
            #endregion

            await base.OnInitializeAsync();
        }

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
        public ICommand? SelectionChangedCommand { get; set; }

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
        private async Task RefreshPLCAddressesLoopAsync()
        {
            while (true)
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
                finally { await Task.Delay(1000); }
            }
        }
        #endregion
        #endregion

        #region 设备信息
        #region 设备连接状态信息
        private string plcConnectStatus = "离线", mesConnectStatus = "离线", webConnectStatus = "离线";
        public string PLCConnectStatus { get => plcConnectStatus; set => SetProperty(ref plcConnectStatus, value); }
        public string MESConnectStatus { get => mesConnectStatus; set => SetProperty(ref mesConnectStatus, value); }
        public string WEBConnectStatus { get => webConnectStatus; set => SetProperty(ref webConnectStatus, value); }

        /// <summary>
        /// 初始化设备状态信息
        /// </summary>
        /// <returns></returns>
        private async Task RefreshConnectStatusLoopAsync()
        {
            while (true)
            {
                try
                {
                    await RunOnUIThreadAsync(() =>
                    {
                        if (!_plcService.ConnectionStatus.ContainsKey(JRCWCore.PLCName))
                        {
                            PLCConnectStatus = "离线";
                            return;
                        }
                        PLCConnectStatus = _plcService.ConnectionStatus[JRCWCore.PLCName].Status ? "已连接" : "离线";
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error("设备连接状态更新异常！", ex);
                }
                finally { await Task.Delay(500); }
            }
        }
        #endregion
        public ObservableCollection<ParameterModel> ProductInfoModel { get; set; } = [];

        /// <summary>
        /// 初始化生产信息参数
        /// </summary>
        /// <returns></returns>
        private async Task InitializeProductInfoParametersAsync()
        {
            try
            {
                var deviceSn = _globalSettingRepository.GetByName("DeviceSn")?.Value ?? "";
                var deviceName = _globalSettingRepository.GetByName("DeviceName")?.Value ?? "";
                var workship = _globalSettingRepository.GetByName("workship")?.Value ?? "";
                await RunOnUIThreadAsync(() =>
                {
                    ProductInfoModel.Add(new ParameterModel("设备编号", deviceSn));
                    ProductInfoModel.Add(new ParameterModel("设备名称", deviceName));
                    ProductInfoModel.Add(new ParameterModel("拉线名", workship));
                    ProductInfoModel.Add(new ParameterModel("制令单", "---"));
                    ProductInfoModel.Add(new ParameterModel("工序代码", "---"));
                    ProductInfoModel.Add(new ParameterModel("物料信息", "---"));
                    ProductInfoModel.Add(new ParameterModel("操作单", "---"));
                });
            }
            catch (Exception ex)
            {
                _logger.Error("设备信息初始化异常！", ex);
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
        /// <summary>
        /// 当前的锅炉账号权限
        /// </summary>
        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        private string jrcwSetting = string.Empty;
        /// <summary>
        /// 项目归属
        /// </summary>
        public string JRCWSetting { get => jrcwSetting; set => SetProperty(ref jrcwSetting, value); }

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
        /// 配置参数
        /// </summary>
        public JRCWParamSettingModel ParamSettingModel { get; set; } = new();

        private void InitializeJRCWParamProjectSettings()
        {
            var projectSettingName = "JRCWSetting";
            InitializeProjectSetting(projectSettingName, "义乌", "string", 2);

            JRCWSetting = _projectSettingRepository.GetByNameAndVM(projectSettingName, JRCWCore.BelongToVM)?.Value ?? "";

            InitializeProjectSetting("正极极耳层数", "0", "int", 1);
            InitializeProjectSetting("负极极耳层数", "0", "int", 1);
            InitializeProjectSetting("错位异常偏大阈值", "0", "int", 1);
            InitializeProjectSetting("单次调节量上限", "0", "int", 1);
            InitializeProjectSetting("0.1变径与周长对应变化关系", "0", "double", 1);
            InitializeProjectSetting("闭环判定调节阈值t1", "0", "int", 2);
            InitializeProjectSetting("闭环判定超规格阈值t2", "0", "int", 2);
            InitializeProjectSetting("闭环判定调节阈值t3", "0", "int", 2);
            InitializeProjectSetting("变径调节精度", "0", "int", 2);
            if (JRCWSetting == "德阳")
            {
                InitializeProjectSetting("完美电芯的首极耳角度-正极", "0", "double", 1);
                InitializeProjectSetting("完美电芯的尾极耳角度-正极", "0", "double", 1);
                InitializeProjectSetting("完美电芯的首极耳角度-负极", "0", "double", 1);
                InitializeProjectSetting("完美电芯的尾极耳角度-负极", "0", "double", 1);
                InitializeProjectSetting("完美电芯错位量-正极", "0", "double", 1);
                InitializeProjectSetting("完美电芯错位量-负极", "0", "double", 1);
                InitializeProjectSetting("首极耳第几层-正极", "0", "int", 1);
                InitializeProjectSetting("尾极耳第几层-正极", "0", "int", 1);
                InitializeProjectSetting("首极耳第几层-负极", "0", "int", 1);
                InitializeProjectSetting("尾极耳第几层-负极", "0", "int", 1);
                InitializeProjectSetting("稳定首极耳圈周长-正极", "0", "double", 2);
                InitializeProjectSetting("稳定尾极耳圈周长-正极", "0", "double", 2);
                InitializeProjectSetting("稳定首极耳圈周长-负极", "0", "double", 2);
                InitializeProjectSetting("稳定尾极耳圈周长-负极", "0", "double", 2);
            }
            if (JRCWSetting == "义乌")
            {
                InitializeProjectSetting("压花调节阈值", "0", "double", 2);
                InitializeProjectSetting("压花调节系数", "0", "double", 2);
                InitializeProjectSetting("压花调节精度", "0", "int", 2);
            }
        }

        private void InitializeProjectSetting(string settingName, string value, string type, int roleRank)
        {
            if (!_projectSettingRepository.Exists(model => model.Name == settingName && model.BelongToVM == JRCWCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(settingName, JRCWCore.BelongToVM, value, type, roleRank)
                    {
                        Remark = "卷绕极耳错位闭环参数设置"
                    }
                );
            }
        }

        /// <summary>
        /// 加载卷绕极耳错位闭环参数
        /// </summary>
        /// <returns></returns>
        private async Task LoadJRCWParamSettingModel()
        {
            var valueInt = 0;
            var valueDouble = 0.0d;

            var projectSetting = _projectSettingRepository.GetByNameAndVM("JRCWSetting", JRCWCore.BelongToVM)?.Value ?? "";
            ParamSettingModel.Nb = int.TryParse(_projectSettingRepository.GetByNameAndVM("正极极耳层数", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            ParamSettingModel.Na = int.TryParse(_projectSettingRepository.GetByNameAndVM("负极极耳层数", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            ParamSettingModel.T4 = int.TryParse(_projectSettingRepository.GetByNameAndVM("错位异常偏大阈值", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            ParamSettingModel.U_Limit = int.TryParse(_projectSettingRepository.GetByNameAndVM("单次调节量上限", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            ParamSettingModel.K = double.TryParse(_projectSettingRepository.GetByNameAndVM("0.1变径与周长对应变化关系", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
            ParamSettingModel.T1 = int.TryParse(_projectSettingRepository.GetByNameAndVM("闭环判定调节阈值t1", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            ParamSettingModel.T2 = int.TryParse(_projectSettingRepository.GetByNameAndVM("闭环判定超规格阈值t2", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            ParamSettingModel.T3 = int.TryParse(_projectSettingRepository.GetByNameAndVM("闭环判定调节阈值t3", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            ParamSettingModel.J_d = int.TryParse(_projectSettingRepository.GetByNameAndVM("变径调节精度", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;

            if (projectSetting == "德阳")
            {
                ParamSettingModel.P_θb1 = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯的首极耳角度-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                ParamSettingModel.P_θbn = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯的尾极耳角度-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                ParamSettingModel.P_θa1 = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯的首极耳角度-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                ParamSettingModel.P_θan = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯的尾极耳角度-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                ParamSettingModel.B120 = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯错位量-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                ParamSettingModel.B121 = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯错位量-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                ParamSettingModel.S_Nb1 = int.TryParse(_projectSettingRepository.GetByNameAndVM("首极耳第几层-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
                ParamSettingModel.S_Nbn = int.TryParse(_projectSettingRepository.GetByNameAndVM("尾极耳第几层-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
                ParamSettingModel.S_Na1 = int.TryParse(_projectSettingRepository.GetByNameAndVM("首极耳第几层-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
                ParamSettingModel.S_Nan = int.TryParse(_projectSettingRepository.GetByNameAndVM("尾极耳第几层-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
                ParamSettingModel.Cb1 = double.TryParse(_projectSettingRepository.GetByNameAndVM("稳定首极耳圈周长-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                ParamSettingModel.Cbn = double.TryParse(_projectSettingRepository.GetByNameAndVM("稳定尾极耳圈周长-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                ParamSettingModel.Ca1 = double.TryParse(_projectSettingRepository.GetByNameAndVM("稳定首极耳圈周长-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                ParamSettingModel.Can = double.TryParse(_projectSettingRepository.GetByNameAndVM("稳定尾极耳圈周长-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
            }

            if (projectSetting == "义乌")
            {
                ParamSettingModel.P_Switch = double.TryParse(_projectSettingRepository.GetByNameAndVM("压花调节阈值", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                ParamSettingModel.P_t = double.TryParse(_projectSettingRepository.GetByNameAndVM("压花调节系数", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                ParamSettingModel.P_j_d = int.TryParse(_projectSettingRepository.GetByNameAndVM("压花调节精度", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            }
        }
        #endregion

        #region 闭环逻辑开发
        #region 闭环全局参数
        /// <summary>
        /// 文件保存根目录
        /// </summary>
        private string fileSaveRootPath = string.Empty;

        /// <summary>
        /// PLC采集数据缓存
        /// </summary>
        private BlockingCollection<GatherPLCDataModel> gatherPLCDataModels = new();

        /// <summary>
        /// CCD采集数据缓存
        /// </summary>
        private List<GatherCCDDataModel> gatherCCDDataModels = new();
        #endregion

        /// <summary>
        /// 初始化闭环相关任务
        /// </summary>
        /// <returns></returns>
        private async Task InitializeClosedLoopAsync()
        {
            var projectSettingName = "FileSaveRootPath";
            InitializeProjectSetting(projectSettingName, "D:\\通用上位机\\JRCWDatas", "string", 2);
            fileSaveRootPath = _projectSettingRepository.GetByNameAndVM(projectSettingName, JRCWCore.BelongToVM)?.Value ?? "D:\\通用上位机\\JRCWDatas";

            _ = RunInBackgroundAsync(InitializePLCHeartLoopAsync);
            _ = RunInBackgroundAsync(CheckOfClosedLoopSwitchAsync);
            _ = RunInBackgroundAsync(JRCWCalculateAsync_YWC05);
            _ = RunInBackgroundAsync(InitializePLCSigalListenerLoopAsync);
        }

        #region 开机检测闭环开关
        /// <summary>
        /// 开机检测闭环开关
        /// </summary>
        private async Task CheckOfClosedLoopSwitchAsync()
        {
            await Task.Delay(50000);
            var value = GetPLCAddressValue("PLC闭环开启信号");
            if (value == "1")
            {
                _logger.Info("开机检测:卷绕极耳错位闭环PLC开关已开启！");
                return;
            }

            var messageText = $"开机检测:卷绕极耳错位闭环PLC开关[{value}]未开启，请人工确认！";
            await MessageBoxShowDialog(messageText, "警告");
        }
        #endregion

        #region 心跳
        /// <summary>
        /// 初始化PLC心跳监控
        /// </summary>
        private async Task InitializePLCHeartLoopAsync()
        {
            var parameterName = "闭环心跳信号";
            var plcAddress = GetPLCAddressModel(parameterName);
            while (true)
            {
                try
                {
                    if (plcAddress == null) plcAddress = GetPLCAddressModel(parameterName);
                    if (plcAddress == null) throw new Exception($"未查询到[{parameterName}]PLC地址配置！");

                    var value = GetPLCAddressValue("闭环心跳信号");
                    var re = await WritePLCAsync(plcAddress.Address ?? string.Empty, plcAddress.Type ?? string.Empty, value == "0" ? (short)1 : (short)0);
                    if (!re.IsSuccess) throw new Exception($"PLC心跳信号[{parameterName}]写入异常！原因：{re.Message}");
                    await Task.Delay(1500);
                }
                catch (Exception ex)
                {
                    _logger.Error($"PLC心跳监听线程异常[60s后重试]", ex);
                    await Task.Delay(60000);
                }
            }
        }
        #endregion

        #region 信号监听/数据采集
        private ValueListener? plcDataListener, ccdDataListener, yieldListener;

        /// <summary>
        /// 初始化PLC信号监听循环
        /// </summary>
        /// <returns></returns>
        private async Task InitializePLCSigalListenerLoopAsync()
        {
            plcDataListener = new ValueListener() { Name = "客户端采集电芯数据信号", Value = "0" };
            plcDataListener.OnValueChangedEven -= OnPLCDataListenerValueChanged;
            plcDataListener.OnValueChangedEven += OnPLCDataListenerValueChanged;
            ccdDataListener = new ValueListener() { Name = "电芯扫码/ccd扫描信号", Value = "0" };
            ccdDataListener.OnValueChangedEven -= OnCCDDataListenerValueChanged;
            ccdDataListener.OnValueChangedEven += OnCCDDataListenerValueChanged;
            yieldListener = new ValueListener() { Name = "6h良率80警告", Value = "0" };
            yieldListener.OnValueChangedEven -= OnYieldListenerValueChanged;
            yieldListener.OnValueChangedEven += OnYieldListenerValueChanged;
            while (true)
            {
                try
                {
                    plcDataListener.Value = GetPLCAddressValue(plcDataListener.Name);
                    ccdDataListener.Value = GetPLCAddressValue(ccdDataListener.Name);
                    yieldListener.Value = GetPLCAddressValue(yieldListener.Name);
                }
                catch (Exception ex)
                {
                    _logger.Error("PLC信号监听线程异常", ex);
                }
                finally { await Task.Delay(100); }
            }
        }

        /// <summary>
        /// PLC数据采集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnPLCDataListenerValueChanged(object? sender, EventArgs<string> args)
        {
            if (!(sender is ValueListener listener)) return;
            if (listener.Name != "客户端采集电芯数据信号") return;
            if (args.OldValue == args.NewValue) return;

            if (args.NewValue == "0")
            {
                _logger.Info($"[{listener.Name}]读取数据信号复位0");
                return;
            }

            if (args.NewValue == "1")
            {
                _logger.Debug($"[{listener.Name}]读取数据信号置位1");

                _ = RunInBackgroundAsync(async () =>
                {
                    await Task.Delay(200);
                    short valueShort;
                    float valueFloat;
                    var plcDataModel = new GatherPLCDataModel();
                    plcDataModel.GatherTime = DateTime.Now;
                    plcDataModel.ZJData_XNM = GetPLCAddressValue("卷针处虚拟码");
                    plcDataModel.ZJData_JZH = short.TryParse(GetPLCAddressValue("卷针号"), out valueShort) ? valueShort : (short)0;
                    plcDataModel.MalpositionValueOfPositive = float.TryParse(GetPLCAddressValue("正极错位量"), out valueFloat) ? valueFloat : 0f;
                    plcDataModel.MalpositionValueOfNegative = float.TryParse(GetPLCAddressValue("负极错位量"), out valueFloat) ? valueFloat : 0f;
                    for (int i = 0; i < 35; i++)
                    {
                        var value = float.TryParse(GetPLCAddressValue($"正极第{i + 1}个极耳错位偏差量"), out valueFloat) ? valueFloat : 0f;
                        plcDataModel.MalpositionDeviationValueOfPositiveBatteryTab.Add(value);
                    }
                    for (int i = 0; i < 36; i++)
                    {
                        var value = float.TryParse(GetPLCAddressValue($"负极第{i + 1}个极耳错位偏差量"), out valueFloat) ? valueFloat : 0f;
                        plcDataModel.MalpositionDeviationValueOfNegativeBatteryTab.Add(value);
                    }
                    plcDataModel.SV_Pos1 = float.TryParse(GetPLCAddressValue("卷针1变径位置"), out valueFloat) ? valueFloat : 0f;
                    plcDataModel.SV_Pos2 = float.TryParse(GetPLCAddressValue("卷针2变径位置"), out valueFloat) ? valueFloat : 0f;
                    plcDataModel.StampingAirPressure = float.TryParse(GetPLCAddressValue("实时压花气压"), out valueFloat) ? valueFloat : 0f;

                    plcDataModel.T1 = ParamSettingModel.T1;
                    plcDataModel.T2 = ParamSettingModel.T2;
                    plcDataModel.T3 = ParamSettingModel.T3;
                    plcDataModel.T4 = ParamSettingModel.T4;
                    plcDataModel.K = ParamSettingModel.K;

                    // 缓存
                    gatherPLCDataModels.Add(plcDataModel);

                    // 保存为CSV
                    var savePath = Path.Combine(fileSaveRootPath, $"卷绕错位PLC数据\\{DateTime.Now:yyyyMMdd}");
                    var dic = new Dictionary<string, string>();
                    ToDictionalByDataColumn(plcDataModel, ref dic);
                    var dt = JRCWCsvHelper.DicToTable(dic);
                    lock (JRCWCsvHelper.WriteLocker) JRCWCsvHelper.WriteCSV(savePath, dt);
                    return Task.CompletedTask;
                });
                return;
            }

            _logger.Error($"{listener.Name}读取数据信号为异常值[{args.NewValue}]");
        }

        /// <summary>
        /// CCD数据采集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnCCDDataListenerValueChanged(object? sender, EventArgs<string> args)
        {
            if (!(sender is ValueListener listener)) return;
            if (listener.Name != "电芯扫码/ccd扫描信号") return;
            if (args.OldValue == args.NewValue) return;

            if (args.NewValue == "0")
            {
                _logger.Info($"[{listener.Name}]读取数据信号复位0");
                return;
            }

            if (args.NewValue == "1")
            {
                _logger.Debug($"[{listener.Name}]读取数据信号置位1");

                _ = RunInBackgroundAsync(async () =>
                {
                    await Task.Delay(200);
                    float valueFloat;
                    var ccdDataModel = new GatherCCDDataModel();
                    ccdDataModel.GatherTime = DateTime.Now;
                    ccdDataModel.CCD_ScanCode = GetPLCAddressValue("扫码处虚拟码");
                    ccdDataModel.CCD_Barcode = GetPLCAddressValue("Barcode码");
                    ccdDataModel.FeedInletPositionOfPositive = float.TryParse(GetPLCAddressValue("入料位置-正极"), out valueFloat) ? valueFloat : 0f;
                    ccdDataModel.FeedInletPositionOfNegative = float.TryParse(GetPLCAddressValue("入料位置-负极"), out valueFloat) ? valueFloat : 0f;
                    ccdDataModel.FarDistanceOfPositiveBatteryTab = float.TryParse(GetPLCAddressValue("正极极耳远边距"), out valueFloat) ? valueFloat : 0f;
                    ccdDataModel.NearDistanceOfPositiveBatteryTab = float.TryParse(GetPLCAddressValue("正极极耳近边距"), out valueFloat) ? valueFloat : 0f;
                    ccdDataModel.FarDistanceOfNegativeBatteryTab = float.TryParse(GetPLCAddressValue("负极极耳远边距"), out valueFloat) ? valueFloat : 0f;
                    ccdDataModel.NearDistanceOfNegativeBatteryTab = float.TryParse(GetPLCAddressValue("负极极耳近边距"), out valueFloat) ? valueFloat : 0f;

                    // 缓存
                    lock (gatherCCDDataModels)
                    {
                        gatherCCDDataModels.Add(ccdDataModel);
                        if (gatherCCDDataModels.Count > 40) gatherCCDDataModels.RemoveAt(0);
                    }

                    // 保存为CSV
                    var savePath = Path.Combine(fileSaveRootPath, $"卷绕错位CCD数据\\{DateTime.Now:yyyyMMdd}");
                    var dic = new Dictionary<string, string>();
                    ToDictionalByDataColumn(ccdDataModel, ref dic);
                    var dt = JRCWCsvHelper.DicToTable(dic);
                    lock (JRCWCsvHelper.WriteLocker) JRCWCsvHelper.WriteCSV(savePath, dt);
                    return Task.CompletedTask;
                });
                return;
            }

            _logger.Error($"{listener.Name}读取数据信号为异常值[{args.NewValue}]");
        }

        /// <summary>
        /// 6h良率80警告 监控
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnYieldListenerValueChanged(object? sender, EventArgs<string> args)
        {
            if (!(sender is ValueListener listener)) return;
            if (listener.Name != "6h良率80警告") return;
            if (args.OldValue == args.NewValue) return;

            if (args.NewValue == "0")
            {
                _logger.Info($"[{listener.Name}]读取数据信号复位0");
                return;
            }

            if (args.NewValue == "1")
            {
                _logger.Info($"[{listener.Name}]读取数据信号值为1");
                // -TODO    飞书推送

                return;
            }

            _logger.Error($"{listener.Name}读取数据信号为异常值[{args.NewValue}]");
        }
        #endregion

        #region 闭环计算
        private VM_MessageBoxDialog mb = new VM_MessageBoxDialog();
        /// <summary>
        /// 卷绕极耳错位闭环计算逻辑
        /// </summary>
        /// <returns></returns>
        private async Task JRCWCalculateAsync_YWC05()
        {
            try
            {
                // 缓存
                int jSingleCache = 0, overJNumCache = 0;
                float j1Cache = 0, j2Cache = 0;
                var calculateDatasCache = new List<(JRCWCalculateModel, GatherPLCDataModel)>();
                var ccdData = new GatherCCDDataModel();
                foreach (var plcData in gatherPLCDataModels.GetConsumingEnumerable())
                {
                    var calculateModel = new JRCWCalculateModel();
                    calculateModel.CalculateTime = DateTime.Now;
                    calculateModel.JSingleCache = jSingleCache;
                    calculateModel.OverJNumCache = overJNumCache;
                    calculateModel.J1Cache = j1Cache;
                    calculateModel.J2Cache = j2Cache;
                    try
                    {
                        #region 1.接帯/换卷信号处理
                        calculateModel.Host_TapeSplicing = await CheckOfChangeOrRewindSignalAsync("接带信号");
                        calculateModel.Host_ReelAlternatioan = await CheckOfChangeOrRewindSignalAsync("换卷信号");
                        calculateModel.ChangeOfArithmetic = calculateModel.Host_ReelAlternatioan == 1 ? 2 : (calculateModel.Host_TapeSplicing == 1 ? 1 : 0);
                        if (calculateModel.ChangeOfArithmetic != 0) calculateDatasCache.Clear();
                        #endregion

                        #region 2.CCD数据匹配
                        ccdData = gatherCCDDataModels.FirstOrDefault(o => o.CCD_ScanCode == plcData.ZJData_XNM);
                        if (ccdData == null)
                        {
                            _logger.Error($"未查询到[{plcData.ZJData_XNM}]对应的CCD采集数据！");
                            ccdData = new GatherCCDDataModel();
                        }
                        else lock (gatherCCDDataModels) gatherCCDDataModels.Remove(ccdData);
                        calculateDatasCache.add((calculateModel, plcData));
                        // 闭环参数最多12条
                        if (calculateDatasCache.Count > 12) calculateDatasCache.RemoveAt(0);
                        calculateModel.PLCBorcode = plcData.ZJData_XNM;
                        calculateModel.JZH = plcData.ZJData_JZH;
                        #endregion

                        #region 3.参数加载:每次重新加载一下参数
                        await LoadJRCWParamSettingModel();
                        #endregion

                        #region 4.正/负极错位量处理
                        var reOfMV = await CheckOfMalpositionValueAsync(plcData.MalpositionValueOfPositive, plcData.MalpositionValueOfNegative, ParamSettingModel);
                        if (!reOfMV.result)
                        {
                            calculateModel.M = reOfMV.m;
                            calculateModel.EndMessage = reOfMV.message;
                            continue;
                        }
                        #endregion

                        #region 5.算法入参处理(错位分类算法&错位分解算法)
                        var mdValuesOfPLis = plcData.MalpositionDeviationValueOfPositiveBatteryTab;
                        var mdValuesOfPArr = mdValuesOfPLis.Take(35).Skip(2).Take(mdValuesOfPLis.Count - 4).ToArray();
                        var mdValuesOfNLis = plcData.MalpositionDeviationValueOfNegativeBatteryTab;
                        var mdValuesOfNArr = mdValuesOfNLis.Take(36).Skip(2).Take(mdValuesOfNLis.Count - 4).ToArray();
                        #endregion

                        #region 6.错位分类算法
                        var cOfM = ExcuteDisclass(mdValuesOfPArr, mdValuesOfNArr);
                        calculateModel.CategoryOfPositiveMalposition = cOfM.COfPM;
                        calculateModel.CategoryOfNegativeMalposition = cOfM.COfNM;
                        #endregion

                        #region 7.错位分解算法
                        // 两个编码是否存在非0值：若存在，则执行错位分解算法
                        if (!(cOfM.COfPM == 0 && cOfM.COfNM == 0))
                        {
                            var dOfEC = ExcuteErrorCompensation(mdValuesOfPArr, mdValuesOfNArr);
                            calculateModel.DeltaCOfPositive = dOfEC.DeltaCOfP;
                            calculateModel.DeltaTCOfPositive = dOfEC.DeltaTCOfP;
                            calculateModel.DeltaCOfNegative = dOfEC.DeltaCOfN;
                            calculateModel.DeltaTCOfNegative = dOfEC.DeltaTCOfN;
                        }
                        #endregion

                        #region 8.闭环算法
                        var data = GetDataOfMisJudge(calculateDatasCache.ToArray());
                        var t = ParamSettingModel.K * (ParamSettingModel.Na + ParamSettingModel.Nb) / 2;
                        var reOfMJ = ExcuteMisJudge(data, ParamSettingModel, calculateModel.ChangeOfArithmetic, t, jSingleCache, overJNumCache);
                        jSingleCache = reOfMJ.J_Signal;
                        overJNumCache = reOfMJ.Over_J_Num;
                        calculateModel.J1 = reOfMJ.J1;
                        calculateModel.J2 = reOfMJ.J2;
                        calculateModel.PressJ = reOfMJ.Press_J;
                        calculateModel.Notes = reOfMJ.Notes;
                        calculateModel.Dialog = reOfMJ.Dialog;
                        calculateModel.JSingle = reOfMJ.J_Signal;
                        calculateModel.OverJNum = reOfMJ.Over_J_Num;
                        // 清空数据表data
                        if (reOfMJ.J1 != 0 || reOfMJ.J2 != 0 || reOfMJ.Press_J != 0) calculateDatasCache.clear();
                        #endregion
                    }
                    catch (Exception ex)
                    {
                        calculateModel.EndMessage += ex.Message;
                        _logger.Error("卷绕极耳错位闭环计算异常！", ex);
                    }
                    finally
                    {
                        try
                        {
                            #region 9.调机下发  闭环执行异常，但有缓存值则依旧下发
                            var jOfAdjust = await JJPAdjustAsync(plcData.ZJData_JZH, calculateModel.J1, calculateModel.J2, calculateModel.PressJ, calculateModel.Dialog, j1Cache, j2Cache);
                            // 缓存
                            j1Cache = jOfAdjust.j1Cache;
                            j2Cache = jOfAdjust.j2Cache;
                            calculateModel.EndMessage = jOfAdjust.message;
                            #endregion
                        }
                        catch (Exception ex)
                        {
                            calculateModel.EndMessage += ex.Message;
                            _logger.Error("卷绕极耳错位闭环下发异常！", ex);
                        }

                        // 计算过程模型异步保存CSV
                        _ = RunInBackgroundAsync(() =>
                        {
                            var savePath = Path.Combine(fileSaveRootPath, $"卷绕错位CCD及闭环过程数据\\{DateTime.Now:yyyyMMdd}");
                            var dic = new Dictionary<string, string>();
                            ToDictionalByDataColumn(ccdData, ref dic);
                            ToDictionalByDataColumn(calculateModel, ref dic);
                            //var dics = ToDictionalsByDataColumn(ccdData, calculateModel);
                            var dt = JRCWCsvHelper.DicToTable(dic);
                            lock (JRCWCsvHelper.WriteLocker) JRCWCsvHelper.WriteCSV(savePath, dt);
                            return Task.CompletedTask;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("卷绕极耳错位闭环异常！", ex);
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// 接帯/换卷信号处理
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        private async Task<short> CheckOfChangeOrRewindSignalAsync(string parameterName)
        {
            var plcAddress = GetPLCAddressModel(parameterName);
            if (plcAddress == null)
            {
                _logger.Error($"未查询到[{parameterName}]PLC地址配置！");
                return 0;
            }
            var monitorValue = plcAddress.MonitorValue?.ToString();
            if (!short.TryParse(monitorValue, out short value))
            {
                _logger.Warn($"[{parameterName}]PLC地址数据读取解析异常[{monitorValue}]！");
                return 0;
            }
            if (value == 0) return 0;
            if (value == 1)
            {
                var re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, (short)0);
                if (re.IsSuccess) _logger.Info($"[{parameterName}]PLC地址复位完成！");
                else _logger.Error($"[{parameterName}]PLC地址复位失败:[{re.Message}]");
                return 1;
            }

            _logger.Warn($"[{parameterName}]PLC地址数据[{monitorValue}]不为0或1！");
            return value;
        }

        /// <summary>
        /// 正/负极错位量处理
        /// </summary>
        /// <param name="mvOfP"></param>
        /// <param name="mvOfN"></param>
        /// <param name="pm"></param>
        /// <returns></returns>
        private async Task<(bool result, double m, string message)> CheckOfMalpositionValueAsync(float mvOfP, float mvOfN, JRCWParamSettingModel pm)
        {
            var msg = "success";

            // 正/负极错位量绝对值>t4
            var mAbsOfP = Math.Abs(mvOfP);
            var mAbsOfN = Math.Abs(mvOfN);
            if (!(mAbsOfP > pm.T4 || mAbsOfN > pm.T4)) return (true, 0, msg);

            // 正/负极错位量绝对值>=200
            if (!(mAbsOfP < 200 || mAbsOfN < 200))
            {
                msg = $"正/负极错位量[{mvOfP},{mvOfN}]绝对值大于等于[200]且大于[{pm.T4}]!";
                return (false, 0, msg);
            }

            // 错位异常警告信号下发&弹窗
            var parameterName = "错位异常偏大警告信号";
            var plcAddress = GetPLCAddressModel(parameterName);
            var re = await WritePLCAsync(parameterName, plcAddress?.Type ?? string.Empty, (short)1);
            if (!re.IsSuccess) _logger.Error($"[{parameterName}]PLC地址写入失败:[{re.Message}]");

            // 推荐“变径调节量”，保留一位小数
            var m = Math.Round((mvOfP + mvOfN) / ((pm.Nb + pm.Na - 2) * pm.K * 10), 1);

            // 弹窗
            msg = $"错位异常偏大，推荐变径调整量：{m}，请结合实际情况进行调整！";
            _ = MessageBoxShowDialog(msg, "警告");

            return (false, m, msg);
        }

        #region Python算法相关
        #region 错位分类算法
        /// <summary>
        /// 执行错位分类算法
        /// </summary>
        /// <param name="mdValuesOfP"></param>
        /// <param name="mdValuesOfN"></param>
        /// <returns></returns>
        private DisclassOutputModel ExcuteDisclass(float[] mdValuesOfP, float[] mdValuesOfN)
        {
            var pyResoult = new PyResultModel<DisclassInputModel, DisclassOutputModel>();
            pyResoult.PyInPut = new DisclassInputModel();
            pyResoult.PyInPut.MDValuesOfP = mdValuesOfP;
            pyResoult.PyInPut.MDValuesOfN = mdValuesOfN;
            var sw = new Stopwatch();
            sw.Start();
            using var mr = new ManualResetEvent(false);
            var args = new object[] { mr, pyResoult };
            PythonConfigHelper.AddAction((ExcutePython_Disclass, args));
            mr.WaitOne();
            sw.Stop();

            if (pyResoult.IsSuccess)
            {
                _logger.Info($"'错位分类算法'执行完毕[耗时:{sw.ElapsedMilliseconds}ms]");
                return pyResoult.Result ?? new DisclassOutputModel();
            }

            throw new Exception($"'错位分类算法'执行异常[耗时:{sw.ElapsedMilliseconds}ms]:[{pyResoult.Message}]\r\n,入参[{JsonConvert.SerializeObject(pyResoult.PyInPut)}]\r\n,出参[{pyResoult.PyOutPut}]");
        }

        /// <summary>
        /// 错位分类算法
        /// </summary>
        /// <param name="args"></param>
        private void ExcutePython_Disclass(object[] args)
        {
            if (args.Length != 2 ||
                !(args[0] is ManualResetEvent mr) ||
                !(args[1] is PyResultModel<DisclassInputModel, DisclassOutputModel> pyResoult))
            {
                _logger.Error($"'错位分类算法'参数解析异常[{JsonConvert.SerializeObject(args)}]");
                throw new ArgumentException(nameof(args), $"参数异常[{JsonConvert.SerializeObject(args)}]");
            }

            try
            {
                var inputModel = pyResoult.PyInPut;
                if (inputModel == null) throw new ArgumentException(nameof(inputModel), $"参数异常[{JsonConvert.SerializeObject(inputModel)}]");

                // 算法导入&执行
                using var gs = Py.GIL();
                dynamic pm = PyModule.Import("Disclass");
                dynamic po = pm.Disclass(inputModel.MDValuesOfP, inputModel.MDValuesOfN);
                dynamic re = po.disclass();

                // 算法结果解析
                var reTp = PyTuple.AsTuple(re);
                var pyRes = new DisclassOutputModel();
                pyRes.COfPM = (int)reTp[0];
                pyRes.COfNM = (int)reTp[1];

                // 返回算法结果
                pyResoult.IsSuccess = true;
                pyResoult.PyOutPut = re;
                pyResoult.Result = pyRes;
                pyResoult.Message = "success";
            }
            catch (Exception ex)
            {
                pyResoult.IsSuccess = false;
                pyResoult.Message = ex.Message;
            }
            finally { mr.Set(); }
        }
        #endregion

        #region 错位分解算法
        /// <summary>
        /// 执行错位分解算法
        /// </summary>
        /// <param name="mdValuesOfP"></param>
        /// <param name="mdValuesOfN"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ErrorCompensationOutputModel ExcuteErrorCompensation(float[] mdValuesOfP, float[] mdValuesOfN)
        {
            var pyResoult = new PyResultModel<ErrorCompensationInputModel, ErrorCompensationOutputModel>();
            pyResoult.PyInPut = new ErrorCompensationInputModel();
            pyResoult.PyInPut.MDValuesOfP = mdValuesOfP;
            pyResoult.PyInPut.MDValuesOfN = mdValuesOfN;
            var sw = new Stopwatch();
            sw.Start();
            using var mr = new ManualResetEvent(false);
            var args = new object[] { mr, pyResoult };
            PythonConfigHelper.AddAction((ExcutePython_ErrorCompensation, args));
            mr.WaitOne();
            sw.Stop();

            if (pyResoult.IsSuccess)
            {
                _logger.Info($"'错位分解算法'执行完毕[耗时:{sw.ElapsedMilliseconds}ms]");
                return pyResoult.Result ?? new ErrorCompensationOutputModel();
            }

            throw new Exception($"'错位分解算法'执行异常[耗时:{sw.ElapsedMilliseconds}ms]:[{pyResoult.Message}]\r\n,入参[{JsonConvert.SerializeObject(pyResoult.PyInPut)}]\r\n,出参[{pyResoult.PyOutPut}]");
        }

        /// <summary>
        /// 错位分解算法
        /// </summary>
        /// <param name="args"></param>
        private void ExcutePython_ErrorCompensation(object[] args)
        {
            if (args.Length != 2 ||
              !(args[0] is ManualResetEvent mr) ||
              !(args[1] is PyResultModel<ErrorCompensationInputModel, ErrorCompensationOutputModel> pyResoult))
            {
                _logger.Error($"'错位分解算法'参数解析异常[{JsonConvert.SerializeObject(args)}]");
                throw new ArgumentException(nameof(args), $"参数异常[{JsonConvert.SerializeObject(args)}]");
            }

            try
            {
                var inputModel = pyResoult.PyInPut;
                if (inputModel == null) throw new ArgumentException(nameof(inputModel), $"参数异常[{JsonConvert.SerializeObject(inputModel)}]");

                // 算法导入&执行
                using var gs = Py.GIL();
                dynamic pm = PyModule.Import("Error_Compensation");
                dynamic po = pm.DiamagneticAnalyzer(inputModel.MDValuesOfP, inputModel.MDValuesOfN);
                dynamic re = po.analyze();

                // 算法结果解析
                var reTp = PyTuple.AsTuple(re);
                var pyRes = new ErrorCompensationOutputModel();
                pyRes.DeltaCOfP = (float)reTp[0];
                pyRes.DeltaTCOfP = (float)reTp[1];
                pyRes.DeltaCOfN = (float)reTp[2];
                pyRes.DeltaTCOfN = (float)reTp[3];

                // 返回算法结果
                pyResoult.IsSuccess = true;
                pyResoult.PyOutPut = re;
                pyResoult.Result = pyRes;
                pyResoult.Message = "success";
            }
            catch (Exception ex)
            {
                pyResoult.IsSuccess = false;
                pyResoult.Message = ex.Message;
            }
            finally { mr.Set(); }
        }
        #endregion

        #region 闭环算法
        /// <summary>
        /// 执行闭环算法
        /// </summary>
        /// <param name="data"></param>
        /// <param name="pm"></param>
        /// <param name="change"></param>
        /// <param name="t"></param>
        /// <param name="j_single"></param>
        /// <param name="over_j_num"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private MisJudgeOutputModel ExcuteMisJudge(string data, JRCWParamSettingModel pm, int change, double t, int j_single, int over_j_num)
        {
            var pyResoult = new PyResultModel<MisJudgeInputModel, MisJudgeOutputModel>();
            pyResoult.PyInPut = new MisJudgeInputModel();
            pyResoult.PyInPut.Data = data;
            pyResoult.PyInPut.P_Switch = pm.P_Switch;
            pyResoult.PyInPut.Change = change;
            pyResoult.PyInPut.T = t;
            pyResoult.PyInPut.P_t = pm.P_t;
            pyResoult.PyInPut.U_Limit = pm.U_Limit;
            pyResoult.PyInPut.J_d = pm.J_d;
            pyResoult.PyInPut.P_j_d = pm.P_j_d;
            pyResoult.PyInPut.J_Single = j_single;
            pyResoult.PyInPut.Over_J_Num = over_j_num;
            var sw = new Stopwatch();
            sw.Start();
            using var mr = new ManualResetEvent(false);
            var args = new object[] { mr, pyResoult };
            PythonConfigHelper.AddAction((ExcutePython_MisJudge, args));
            mr.WaitOne();
            sw.Stop();

            if (pyResoult.IsSuccess)
            {
                _logger.Info($"'闭环算法'执行完毕[耗时:{sw.ElapsedMilliseconds}ms]");
                return pyResoult.Result ?? new MisJudgeOutputModel();
            }

            throw new Exception($"'闭环算法'执行异常[耗时:{sw.ElapsedMilliseconds}ms]:[{pyResoult.Message}]\r\n,入参[{JsonConvert.SerializeObject(pyResoult.PyInPut)}]\r\n,出参[{pyResoult.PyOutPut}]");
        }

        /// <summary>
        /// 闭环算法
        /// </summary>
        /// <param name="args"></param>
        private void ExcutePython_MisJudge(object[] args)
        {
            if (args.Length != 2 ||
              !(args[0] is ManualResetEvent mr) ||
              !(args[1] is PyResultModel<MisJudgeInputModel, MisJudgeOutputModel> pyResoult))
            {
                _logger.Error($"'闭环算法'参数解析异常[{JsonConvert.SerializeObject(args)}]");
                throw new ArgumentException(nameof(args), $"参数异常[{JsonConvert.SerializeObject(args)}]");
            }

            try
            {
                var inputModel = pyResoult.PyInPut;
                if (inputModel == null) throw new ArgumentException(nameof(inputModel), $"参数异常[{JsonConvert.SerializeObject(inputModel)}]");

                // 算法导入&执行
                using var gs = Py.GIL();
                dynamic pm = PyModule.Import("mis_judge");
                dynamic po = pm.Judge(inputModel.Data, inputModel.P_Switch, inputModel.Change, inputModel.T, inputModel.P_t, inputModel.U_Limit, inputModel.J_d, inputModel.P_j_d, inputModel.J_Single, inputModel.Over_J_Num);
                dynamic re = po.mis_judge();

                // 算法结果解析
                var reTp = PyTuple.AsTuple(re);
                var pyRes = new MisJudgeOutputModel();
                pyRes.J1 = (float)reTp[0];
                pyRes.J2 = (float)reTp[1];
                pyRes.Press_J = (float)reTp[2];
                pyRes.Notes = (string)reTp[3];
                pyRes.Dialog = (string)reTp[4];
                pyRes.J_Signal = (int)reTp[5];
                pyRes.Over_J_Num = (int)reTp[6];

                // 返回算法结果
                pyResoult.IsSuccess = true;
                pyResoult.PyOutPut = re;
                pyResoult.Result = pyRes;
                pyResoult.Message = "success";
            }
            catch (Exception ex)
            {
                pyResoult.IsSuccess = false;
                pyResoult.Message = ex.Message;
            }
            finally { mr.Set(); }
        }

        /// <summary>
        /// 获取闭环算法的参数data
        /// </summary>
        /// <param name="objs"></param>
        /// <returns></returns>
        public static string GetDataOfMisJudge(params (JRCWCalculateModel cm, GatherPLCDataModel pd)[] objs)
        {
            if (objs == null || objs.Length <= 0) return string.Empty;

            var lis = new List<Dictionary<string, string>>();
            foreach (var obj in objs)
            {
                var dic = new Dictionary<string, string>();
                dic["正极错位分类类型"] = obj.cm.CategoryOfPositiveMalposition.ToString();
                dic["负极错位分类类型"] = obj.cm.CategoryOfNegativeMalposition.ToString();
                dic["正极卷针周长波动"] = obj.cm.DeltaCOfPositive.ToString();
                dic["正极极片厚度波动"] = obj.cm.DeltaTCOfPositive.ToString();
                dic["负极卷针周长波动"] = obj.cm.DeltaCOfNegative.ToString();
                dic["负极极片厚度波动"] = obj.cm.DeltaTCOfNegative.ToString();
                ToDictionalByDataColumn(obj.pd, ref dic);
                lis.Add(dic);
            }

            var headers = lis.SelectMany(d => d.Keys).Distinct().ToList();
            var sb = new StringBuilder();
            sb.Append(string.Join(',', headers));

            foreach (var dic in lis)
            {
                sb.Append(Environment.NewLine);
                foreach (var header in headers)
                {
                    var value = dic.ContainsKey(header) ? dic[header] : string.Empty;
                    sb.Append(value + ",");
                }
                sb.Remove(sb.Length - 1, 1);// 去除最后一个逗号
            }
            return sb.ToString();
        }
        #endregion
        #endregion

        /// <summary>
        /// 闭环算法调机值下发
        /// </summary>
        /// <param name="jzh"></param>
        /// <param name="j1"></param>
        /// <param name="j2"></param>
        /// <param name="press_j"></param>
        /// <param name="dialog"></param>
        /// <param name="j1Cache"></param>
        /// <param name="j2Cache"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private async Task<(float j1Cache, float j2Cache, string message)> JJPAdjustAsync(short jzh, float j1, float j2, float press_j, string? dialog, float j1Cache, float j2Cache)
        {
            if (jzh != 1 && jzh != 2) throw new ArgumentException(nameof(jzh), $"PLC卷针号[{jzh}]信息异常:不为1或2!");

            string parameterName, message = "success";
            var messageSB = new StringBuilder();
            PLCRWAddress? plcAddress;
            OperateResult re;
            // 算法输出下发值全为0
            if (!(j1 != 0 || j2 != 0 || press_j != 0))
            {
                if (jzh == 1 && j2Cache != 0)
                {
                    // 下发J2Cache
                    parameterName = "卷针2变径位置调整量";
                    plcAddress = GetPLCAddressModel(parameterName);
                    re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, j2Cache);

                    messageSB.AppendLine(re.IsSuccess ? $"success(J2下发[{j2Cache}]成功)" : $"[{parameterName}]PLC地址写入[{j2Cache}]失败:[{re.Message}]");
                    message = messageSB.ToString();
                    if (!re.IsSuccess) _logger.Error(message);
                    return (j1Cache, 0, message);
                }
                if (jzh == 2 && j1Cache != 0)
                {
                    // 下发J1Cache
                    parameterName = "卷针1变径位置调整量";
                    plcAddress = GetPLCAddressModel(parameterName);
                    re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, j1Cache);

                    messageSB.AppendLine(re.IsSuccess ? $"success(J1下发[{j1Cache}]成功)" : $"[{parameterName}]PLC地址写入[{j1Cache}]失败:[{re.Message}]");
                    message = messageSB.ToString();
                    if (!re.IsSuccess) _logger.Error(message);
                    return (0, j2Cache, message);
                }
                message = "success(不执行下发操作)";
                return (j1Cache, j2Cache, message);
            }

            switch (jzh)
            {
                case 1: // 下发j2
                    {
                        j2 += j2Cache;
                        parameterName = "卷针2变径位置调整量";
                        plcAddress = GetPLCAddressModel(parameterName);
                        re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, j2);

                        messageSB.AppendLine(re.IsSuccess ? $"success(J2下发[{j2}]成功)" : $"[{parameterName}]PLC地址写入[{j2}]失败:[{re.Message}]");
                        message = messageSB.ToString();
                        if (!re.IsSuccess) _logger.Error(message);
                        j1Cache += j1;
                        j2Cache = 0;
                        break;
                    }
                case 2: // 下发j1
                    {
                        j1 += j1Cache;
                        parameterName = "卷针1变径位置调整量";
                        plcAddress = GetPLCAddressModel(parameterName);
                        re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, j1);

                        messageSB.AppendLine(re.IsSuccess ? $"success(J1下发[{j1}]成功)" : $"[{parameterName}]PLC地址写入[{j1}]失败:[{re.Message}]");
                        message = messageSB.ToString();
                        if (!re.IsSuccess) _logger.Error(message);
                        j1Cache = 0;
                        j2Cache += j2;
                        break;
                    }
                default:
                    {
                        messageSB.AppendLine($"false(卷针号[{jzh}]不为1或2，j1、j2不执行下发)");
                        break;
                    }
            }

            // 下发press_j
            if (press_j != 0)
            {
                parameterName = "压花气压调整量";
                plcAddress = GetPLCAddressModel(parameterName);
                re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, press_j);
                if (!re.IsSuccess) _logger.Error($"[{parameterName}]PLC地址写入[{press_j}]失败:[{re.Message}]");

                messageSB.AppendLine(re.IsSuccess ? $"success(PressJ下发[{press_j}]成功)" : $"[{parameterName}]PLC地址写入[{press_j}]失败:[{re.Message}]");
                message = messageSB.ToString();
                if (!re.IsSuccess) _logger.Error(message);
            }

            // 弹窗提示
            _logger.Info(dialog ?? string.Empty);
            var msg = dialog?.replace(";", ";\r\n");
            //_ = MessageBoxShowDialog(msg, "调节提示");
            Growl.InfoGlobal(msg);
            //MessageBox.Show(msg, "调节提示");

            return (j1Cache, j2Cache, message);
        }

        #region 私有方法
        #region PLC读写操作
        /// <summary>
        /// PLC读值
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        private string? GetPLCAddressValue(string parameterName)
        {
            var value = _plcService.RWAddresses.Values.FirstOrDefault(o => o.ParameterName == parameterName)?.MonitorValue;
            return value?.ToString();
        }

        /// <summary>
        /// PLC读值
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        private PLCRWAddress? GetPLCAddressModel(string parameterName)
        {
            var value = _plcService.RWAddresses.Values.FirstOrDefault(o => o.ParameterName == parameterName);
            return value;
        }

        /// <summary>
        /// PLC写值
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private async Task<OperateResult> WritePLCAsync(string address, string type, dynamic value)
        {
            if (!(_plcService.GetPLCByName(JRCWCore.PLCName) is PLCDevice plc))
                throw new ArgumentException(nameof(JRCWCore.PLCName), $"未查询到已连接的PLC设备[{JRCWCore.PLCName}]！");

            var result = await plc.WriteAsync(address, type, value);
            _logger.Debug($"PLC执行写入:地址[{address}]类型[{type}]值[{value}]，写入结果[{result.Message}]");
            return result;
        }
        #endregion

        #region 弹窗
        /// <summary>
        /// 弹窗
        /// </summary>
        /// <param name="messageText"></param>
        /// <param name="caption"></param>
        /// <param name="countdownTime"></param>
        /// <returns></returns>
        private async Task<bool> MessageBoxShowDialog(string messageText, string caption, int countdownTime = 5)
        {
            var tcs = new TaskCompletionSource<bool>();
            await RunOnUIThreadAsync(async () =>
            {
                try
                {
                    var mb = new VM_MessageBoxDialog();
                    var re = await Dialog.Show(mb.UIElement, "DialogMainWindow").Initialize<VM_MessageBoxDialog>(vm =>
                     {
                         vm.MessageText = messageText;
                         vm.Caption = caption;
                         vm.CountdownTime = countdownTime;
                         vm.CountdownTimeStart();
                     }).GetResultAsync<bool>();
                    tcs.SetResult(re);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return await tcs.Task;
        }
        #endregion

        #region 根据特性将对象转换为字典/列表/字符串等
        /// <summary>
        /// 多个obj转换为字典列表
        /// </summary>
        /// <param name="objs"></param>
        /// <returns></returns>
        private static List<Dictionary<string, string>> ToDictionalsByDataColumn<T>(params T[] objs) where T : class
        {
            if (objs == null || objs.Length <= 0) return new List<Dictionary<string, string>>();

            var lis = new List<Dictionary<string, string>>();
            foreach (var obj in objs)
            {
                var dic = new Dictionary<string, string>();
                ToDictionalByDataColumn(obj, ref dic);
                lis.add(dic);
            }

            return lis;
        }

        /// <summary>
        /// obj转换为字典
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="dic"></param>
        private static void ToDictionalByDataColumn<T>(T? obj, ref Dictionary<string, string> dic) where T : class
        {
            if (obj == null) return;
            if (dic == null) dic = new Dictionary<string, string>();

            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                var cdAttr = property.GetCustomAttribute<DataColumnAttribute>();
                if (cdAttr == null || cdAttr.IsIgnore) continue;

                var key = string.IsNullOrEmpty(cdAttr.ColumnName) ? property.Name : cdAttr.ColumnName;
                var value = property.GetValue(obj);
                if (value == null)
                {
                    ProcessDictionary(ref dic, key, string.Empty);
                    continue;
                }
                if (value is string vStr)
                {
                    ProcessDictionary(ref dic, key, vStr);
                    continue;
                }
                if (value is DateTime vDateTime)
                {
                    var v = vDateTime.ToString(cdAttr.DataTimeFormat);
                    ProcessDictionary(ref dic, key, v);
                    continue;
                }
                if (value is IEnumerable vs)// 可完善:深层递归
                {
                    var index = 0;
                    foreach (var v in vs)
                    {
                        index++;
                        var cnf = cdAttr.ColumnNameFormat;
                        var newKey = string.IsNullOrEmpty(cnf) ? key : string.Format(cnf, index, key);
                        ProcessDictionary(ref dic, newKey, v.ToString());
                    }
                    continue;
                }
                ProcessDictionary(ref dic, key, value.ToString());
                // 可完善:其他类型
            }
        }

        /// <summary>
        /// 字典插入
        /// </summary>
        /// <param name="dic"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private static void ProcessDictionary(ref Dictionary<string, string> dic, string key, string? value)
        {
            if (dic == null) return;
            var count = dic.Keys.Count(o => o == key);
            var newKey = key;
            while (dic.ContainsKey(newKey)) newKey = $"{key}_{count++}";

            dic[newKey] = value ?? string.Empty;
        }
        #endregion
        #endregion
        #endregion
        #endregion
    }
}
