using Dm;
using Dm.filter;
using HalconDotNet;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools.Extension;
using Masuit.Tools.Models;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Axes;
using Python.Runtime;
using SqlSugar;
using SUNWODA_SEVB.Component.UserControls.Project.CCTB;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AssistanceModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CoatingOPMM;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Core;
using SUNWODA_SEVB.Core.Models.Project.PTO;
using SUNWODA_SEVB.Data.Repositories;
using SUNWODA_SEVB.PLC;
using SUNWODA_SEVB.Services;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using SUNWODA_SEVB.ViewModels.Dialogs.CoatingOPMM;
using SUNWODA_SEVB.ViewModels.Pages.HawkEye;
using SUNWODA_SEVB.Views.Pages.CCTB;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using Xceed.Wpf.Toolkit.Primitives;

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
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        )
        {
            _logger = logger;
            _plcService = pLCService;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
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
            await RunInBackgroundAsync(RefreshPLCAddressesLoopAsync);
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
            LineSeriersModel.InvalidatePlot(true);
        }

        private void ClearLineSeriersModelPoint(int index)
        {
            if (index >= LineSeriersModel.Series.Count) return;
            var series = LineSeriersModel.Series[index] as OxyPlot.Series.LineSeries;
            if (series == null || series.Points.Count <= 0) return;
            series.Points.Clear();
            LineSeriersModel.InvalidatePlot(true);
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

        private List<Core.Models.PLC.PLCRWAddress> asdasd = new();
        private async Task InitializePLCAddressParametersAsync()
        {
            #region test
            for (int i = 0; i < 25; i++)
            {
                var name = $"plcaddress{i}";
                asdasd.Add(new Core.Models.PLC.PLCRWAddress() { CategoryId = 1, ParameterName = name });
                PLCAddressModels.Add(new ParameterModel(name, ""));
            }
            _ = RunInBackgroundAsync(async () =>
            {
                var rd = new Random();
                while (true)
                {
                    foreach (var item in asdasd)
                    {
                        var value = rd.Next(0, 100);
                        item.MonitorValue = value;
                    }

                    await Task.Delay(1000);
                }
            });
            #endregion

            try
            {
                //var addresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 1 && !string.IsNullOrEmpty(o.ParameterName));
                var addresses = asdasd.Where(o => o.CategoryId == 1 && !string.IsNullOrEmpty(o.ParameterName)).ToList();
                foreach (var item in addresses)
                {
                    var asda = new ParameterModel(item.ParameterName ?? "", "");
                    await RunOnUIThreadAsync(() => { PLCAddressModels.Add(asda); });
                }
            }
            catch (Exception ex)
            {
                _logger.Error("实时监控[PLC地址列表]初始化异常！", ex);
            }
        }

        private async Task RefreshPLCAddressesLoopAsync()
        {
            while (true)
            {
                try
                {
                    foreach (var PLCAddressModel in PLCAddressModels)
                    {
                        if (PLCAddressModel == null || PLCAddressModel.PName == null) continue;

                        // 更新PLC地址列表
                        //var value = _plcService.RWAddresses.Values.FirstOrDefault(o => o.ParameterName == PLCAddressModel.PName)?.MonitorValue?.ToString();
                        var value = asdasd.FirstOrDefault(o => o.ParameterName == PLCAddressModel.PName)?.MonitorValue?.ToString();
                        await RunOnUIThreadAsync(() => { PLCAddressModel.PValue = value; });

                        // 更新PLC地址曲线
                        if (!lineNames.Contains(PLCAddressModel.PName)) continue;
                        if (!(double.TryParse(value, out double valueDouble))) continue;
                        var index = lineNames.IndexOf(PLCAddressModel.PName);
                        AddLineSeriersModelPoint(index, valueDouble);
                    }
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
        public ObservableCollection<ParameterModel> ProductInfoModel { get; set; } = [];

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

        #region 卷绕极耳错位闭环参数
        private JRCWParamSettingModel jrcwParamSettingModel = new();
        /// <summary>
        /// 配置参数
        /// </summary>
        public JRCWParamSettingModel JRCWParamSettingModel { get => jrcwParamSettingModel; set => SetProperty(ref jrcwParamSettingModel, value); }

        public ICommand? SummaryCommand { get; set; }
        private void InitializeJRCWParamProjectSettings()
        {
            var projectSettingName = "JRCWSetting";
            InitializeProjectSetting(projectSettingName, "义乌", "string", 2);

            var projectSetting = _projectSettingRepository.GetByNameAndVM(projectSettingName, JRCWCore.BelongToVM)?.Value ?? "";
            InitializeJRCWParamProjectSettings(projectSetting);
        }

        private void InitializeJRCWParamProjectSettings(string closedLoppType)
        {
            InitializeProjectSetting("正极极耳层数", "0", "int", 1);
            InitializeProjectSetting("负极极耳层数", "0", "int", 1);
            InitializeProjectSetting("错位异常偏大阈值", "0", "int", 1);
            InitializeProjectSetting("单次调节量上限", "0", "int", 1);
            InitializeProjectSetting("0.1变径与周长对应变化关系", "0", "double", 1);
            InitializeProjectSetting("闭环判定调节阈值t1", "0", "int", 2);
            InitializeProjectSetting("闭环判定超规格阈值t2", "0", "int", 2);
            InitializeProjectSetting("闭环判定调节阈值t3", "0", "int", 2);
            InitializeProjectSetting("变径调节精度", "0", "int", 2);
            if (closedLoppType == "德阳")
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
            if (closedLoppType == "义乌")
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

        private async Task LoadJRCWParamSettingModel()
        {
            var valueInt = 0;
            var valueDouble = 0.0d;

            var projectSetting = _projectSettingRepository.GetByNameAndVM("JRCWSetting", JRCWCore.BelongToVM)?.Value ?? "";
            JRCWParamSettingModel.Nb = int.TryParse(_projectSettingRepository.GetByNameAndVM("正极极耳层数", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            JRCWParamSettingModel.Na = int.TryParse(_projectSettingRepository.GetByNameAndVM("负极极耳层数", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            JRCWParamSettingModel.T4 = int.TryParse(_projectSettingRepository.GetByNameAndVM("错位异常偏大阈值", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            JRCWParamSettingModel.U_Limit = int.TryParse(_projectSettingRepository.GetByNameAndVM("单次调节量上限", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            JRCWParamSettingModel.K = double.TryParse(_projectSettingRepository.GetByNameAndVM("0.1变径与周长对应变化关系", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
            JRCWParamSettingModel.T1 = int.TryParse(_projectSettingRepository.GetByNameAndVM("闭环判定调节阈值t1", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            JRCWParamSettingModel.T2 = int.TryParse(_projectSettingRepository.GetByNameAndVM("闭环判定超规格阈值t2", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            JRCWParamSettingModel.T3 = int.TryParse(_projectSettingRepository.GetByNameAndVM("闭环判定调节阈值t3", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            JRCWParamSettingModel.J_d = int.TryParse(_projectSettingRepository.GetByNameAndVM("变径调节精度", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;

            if (projectSetting == "德阳")
            {
                JRCWParamSettingModel.P_θb1 = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯的首极耳角度-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                JRCWParamSettingModel.P_θbn = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯的尾极耳角度-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                JRCWParamSettingModel.P_θa1 = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯的首极耳角度-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                JRCWParamSettingModel.P_θan = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯的尾极耳角度-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                JRCWParamSettingModel.B120 = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯错位量-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                JRCWParamSettingModel.B121 = double.TryParse(_projectSettingRepository.GetByNameAndVM("完美电芯错位量-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                JRCWParamSettingModel.S_Nb1 = int.TryParse(_projectSettingRepository.GetByNameAndVM("首极耳第几层-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
                JRCWParamSettingModel.S_Nbn = int.TryParse(_projectSettingRepository.GetByNameAndVM("尾极耳第几层-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
                JRCWParamSettingModel.S_Na1 = int.TryParse(_projectSettingRepository.GetByNameAndVM("首极耳第几层-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
                JRCWParamSettingModel.S_Nan = int.TryParse(_projectSettingRepository.GetByNameAndVM("尾极耳第几层-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
                JRCWParamSettingModel.Cb1 = double.TryParse(_projectSettingRepository.GetByNameAndVM("稳定首极耳圈周长-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                JRCWParamSettingModel.Cbn = double.TryParse(_projectSettingRepository.GetByNameAndVM("稳定尾极耳圈周长-正极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                JRCWParamSettingModel.Ca1 = double.TryParse(_projectSettingRepository.GetByNameAndVM("稳定首极耳圈周长-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                JRCWParamSettingModel.Can = double.TryParse(_projectSettingRepository.GetByNameAndVM("稳定尾极耳圈周长-负极", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
            }

            if (projectSetting == "义乌")
            {
                JRCWParamSettingModel.P_Switch = double.TryParse(_projectSettingRepository.GetByNameAndVM("压花调节阈值", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                JRCWParamSettingModel.P_t = double.TryParse(_projectSettingRepository.GetByNameAndVM("压花调节系数", JRCWCore.BelongToVM)?.Value ?? "", out valueDouble) ? valueDouble : 0;
                JRCWParamSettingModel.P_j_d = int.TryParse(_projectSettingRepository.GetByNameAndVM("压花调节精度", JRCWCore.BelongToVM)?.Value ?? "", out valueInt) ? valueInt : 0;
            }
        }
        #endregion
    }
}
