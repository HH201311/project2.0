using Dm;
using Dm.filter;
using HalconDotNet;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools.Extension;
using Newtonsoft.Json;
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
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CoatingOPMM;
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

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB
{
    #region 导航&按钮等界面功能
    /// <summary>
    /// 导航&按钮等界面功能
    /// </summary>
    [Module("VM_CCTBMonitoringPage", "系统记录", Order = 0, Category = "VM_CCTBMonitoringPage")]
    partial class VM_CCTBMonitoringPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CCTBMonitoringPage> _logger;
        private readonly IPLCService _pLCService;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        private Page? _navigationPage;
        private ModuleInfo? _selectedModule;
        private bool _isNavigating;

        // 添加导航防抖
        private CancellationTokenSource? _navigationCts;
        private DateTime _lastNavigationTime = DateTime.MinValue;
        private const int NAVIGATION_DEBOUNCE_MS = 100;

        public Page? NavigationPage
        {
            get => _navigationPage;
            set => SetProperty(ref _navigationPage, value);
        }

        public ModuleInfo? SelectedModule
        {
            get => _selectedModule;
            set
            {
                SetProperty(ref _selectedModule, value);
                if (value != null && !IsNavigating)
                {
                    // 使用防抖机制避免频繁导航
                    NavigateToModuleDebounced(value);
                }
            }
        }

        public bool IsNavigating
        {
            get => _isNavigating;
            set => SetProperty(ref _isNavigating, value);
        }

        public ICommand? SummaryCommand { get; set; }
        public ICommand? TBDataCommand { get; set; }
        public ICommand? CCDDataCommand { get; set; }
        public ICommand? AlarmRecordingsCommand { get; set; }
        public ICommand? ControlRecordingsCommand { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_CCTBMonitoringPage(
            ILoggerService<VM_CCTBMonitoringPage> logger,
            IPLCService pLCService,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        )
        {
            _logger = logger;
            _pLCService = pLCService;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <returns></returns>
        public override void OnInitialize()
        {
            AlarmRecordingsCommand = new RelayCommand(AlarmRecordingsOpen);
            ControlRecordingsCommand = new RelayCommand(ControlRecordingsOpen);

            // 初始化导航
            InitNavigation();

            base.OnInitialize();
        }

        /// <summary>
        /// 异步初始化
        /// </summary>
        /// <returns></returns>
        protected override async Task OnInitializeAsync()
        {
            await base.OnInitializeAsync();
        }

        /// <summary>
        /// 初始化导航
        /// </summary>
        private void InitNavigation()
        {
            // 显示加载提示
            IsNavigating = true;

            // 加载模块
            _moduleManager.AutoRegisterModulesByName("VM_SummaryPage", "VM_TBDataPage", "VM_CCDDataPage");

            SummaryCommand = new RelayCommand(() => NavigateToSpecialModule("VM_SummaryPage"));
            TBDataCommand = new RelayCommand(() => NavigateToSpecialModule("VM_TBDataPage"));
            CCDDataCommand = new RelayCommand(() => NavigateToSpecialModule("VM_CCDDataPage"));

            IsNavigating = false;

            NavigateToDefault();
        }

        /// <summary>
        /// 到喊道默认界面
        /// </summary>
        /// <returns></returns>
        private void NavigateToDefault()
        {
            if (SelectedModule?.Name == "VM_SummaryPage") return;

            var defaultModule = _moduleManager.GetModule("VM_SummaryPage");
            if (defaultModule == null) return;

            defaultModule.IsSelected = true;
            SelectedModule = defaultModule;
        }

        /// <summary>
        /// 异步导航到指定界面：防抖
        /// </summary>
        /// <param name="module"></param>
        private async void NavigateToModuleDebounced(ModuleInfo module)
        {
            // 取消之前的导航
            _navigationCts?.Cancel();
            _navigationCts = new CancellationTokenSource();

            var now = DateTime.Now;
            var timeSinceLastNav = (now - _lastNavigationTime).TotalMilliseconds;

            // 如果距离上次导航时间太短，等待一下
            if (timeSinceLastNav < NAVIGATION_DEBOUNCE_MS)
            {
                await Task.Delay(
                    NAVIGATION_DEBOUNCE_MS - (int)timeSinceLastNav,
                    _navigationCts.Token
                );
            }

            if (!_navigationCts.Token.IsCancellationRequested)
            {
                _lastNavigationTime = DateTime.Now;
                NavigateToModuleAsync(module);
            }
        }

        /// <summary>
        /// 导航到指定界面
        /// </summary>
        /// <param name="module"></param>
        private async void NavigateToModuleAsync(ModuleInfo module)
        {
            if (string.IsNullOrEmpty(module.Name))
                return;

            try
            {
                IsNavigating = true;
                NavigationPage = _moduleManager.GetView(module.Name);
                var viewModel = NavigationPage?.DataContext as ViewModelBase;
                viewModel?.OnNavigatedTo(null);
            }
            catch (Exception ex)
            {
                _logger?.Error($"导航到 {module.Name} 时发生错误", ex, true);
            }
            finally
            {
                // 延迟一点清除加载状态，避免闪烁
                await Task.Delay(50);
                IsNavigating = false;
            }
        }

        /// <summary>
        /// 导航到指定界面
        /// </summary>
        /// <param name="moduleName"></param>
        private void NavigateToSpecialModule(string moduleName)
        {
            if (IsNavigating)
                return;

            var module = _moduleManager.GetModule(moduleName);

            if (module == null) return;

            if (SelectedModule != null)
                SelectedModule.IsSelected = false;

            module.IsSelected = true;
            SelectedModule = module;
        }

        /// <summary>
        /// 导航完成时触达
        /// </summary>
        /// <param name="parameter"></param>
        public override void OnNavigatedTo(object? parameter)
        {
            Growl.Success(new GrowlInfo
            {
                Message = "Success：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
            Growl.Info(new GrowlInfo
            {
                Message = "Info：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
            Growl.Warning(new GrowlInfo
            {
                Message = "Warning：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = true,
                StaysOpen = false,
                WaitTime = 5,
            });
            Growl.Error(new GrowlInfo
            {
                Message = "Error：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
            Growl.Fatal(new GrowlInfo
            {
                Message = "Fatal：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
            Growl.Ask(new GrowlInfo
            {
                Message = "Ask：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
            base.OnNavigatedTo(parameter);
        }

        protected override async Task OnCleanupAsync()
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();

            await base.OnCleanupAsync();
        }

        bool isStart = false;
        private void AlarmRecordingsOpen()
        {
            if (!isStart)
            {
                TestTBDataFiles();
                TestCoatingMessage();
                TestTBlockMessage();
                TestCCDMessage();
                isStart = true;
            }
        }

        private void ControlRecordingsOpen()
        {

        }

        private async void TestTBDataFiles()
        {
            await RunInBackgroundAsync(async () =>
            {
                int count = 0, passCount = 0;
                var rm = new Random();
                var barCode = "AASDJNALK123J";
                var header = "采集时间,报文标识,AB面,数据类型,基材值,A面目标值,B面目标值,A面公差值,B面公差值,趟号,扫描类型,重量1,重量2,重量3,重量4,重量5,重量6,重量7,重量8,重量9,重量10,重量11,重量12,重量13,重量14,重量15,重量16,重量17,重量18,重量19,重量20,重量21,重量22,重量23,重量24,重量25";

                while (true)
                {
                    try
                    {
                        var path = $"D:\\通用上位机\\闭环数据\\涂布重量数据\\TCPClient\\{DateTime.Today:yyyy-MM-dd}\\" + barCode + count + "_A面.csv";
                        var dir = Path.GetDirectoryName(path);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

                        var data = $"2025-12-26 08:58,BT,B,干膜净重分区数据,53,324,630,14,17,{passCount++},-1";
                        for (int i = 0; i < 25; i++)
                        {
                            var weightData = rm.NextDouble() * 40 + 300;
                            data += $",{weightData}";
                        }
                        var fe = File.Exists(path);
                        using var sw = new StreamWriter(path, true, Encoding.UTF8);
                        if (!fe) sw.WriteLine(header);
                        for (int i = 0; i < 1; i++) sw.WriteLine(data);

                        var data1 = $"2025-12-26 08:58,BT,B,干膜净重分区数据,53,324,630,14,17,{passCount++},-1";
                        for (int i = 0; i < 25; i++)
                        {
                            var weightData = rm.NextDouble() * 30 + 615;
                            data1 += $",{weightData}";
                        }
                        var path1 = $"D:\\通用上位机\\闭环数据\\涂布重量数据\\TCPClient\\{DateTime.Today:yyyy-MM-dd}\\" + barCode + count + "_B面.csv";
                        var fe1 = File.Exists(path1);
                        using var sw1 = new StreamWriter(path1, true, Encoding.UTF8);
                        if (!fe1) sw1.WriteLine(header);
                        for (int i = 0; i < 1; i++) sw1.WriteLine(data1);

                        await Task.Delay(1000);
                        if (passCount > 100)
                        {
                            passCount = 0;
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
            });
        }

        private async void TestCoatingMessage()
        {
            await RunInBackgroundAsync(async () =>
            {
                var rd = new Random();
                var datas = new List<double>();
                while (true)
                {
                    try
                    {
                        datas.Clear();
                        for (int i = 0; i < 25; i++)
                        {
                            if (i == 0 || (i >= 12 && i <= 14) || i == 24) datas.Add(0);
                            else datas.Add(rd.NextDouble() * 12 + 370);
                        }

                        var argsA = new MessageEventArgs<CoatingMessage>();
                        argsA.Message = new CoatingMessage();
                        argsA.Message.SideType = ESideType.A;
                        argsA.Message.Datas = datas;
                        CCTBCore.CoatingMessageNotificationEvent?.Invoke(this, argsA);

                        var argsB = new MessageEventArgs<CoatingMessage>();
                        argsB.Message = new CoatingMessage();
                        argsB.Message.SideType = ESideType.B;
                        argsB.Message.Datas = datas.Select(o => o * 2).ToList();
                        CCTBCore.CoatingMessageNotificationEvent?.Invoke(this, argsB);
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {

                    }

                }
            });
        }

        private async void TestTBlockMessage()
        {
            await RunInBackgroundAsync(async () =>
            {
                var rd = new Random();
                var datas = new List<double>();
                while (true)
                {
                    try
                    {
                        datas.Clear();
                        for (int i = 0; i < 25; i++)
                        {
                            if (i == 0 || (i >= 12 && i <= 14) || i == 24) datas.Add(0);
                            else datas.Add(rd.NextDouble() * 400 - 200);
                        }

                        var argsA = new MessageEventArgs<TBlockMessage>();
                        argsA.Message = new TBlockMessage();
                        argsA.Message.SideType = ESideType.A;
                        argsA.Message.Datas = datas.ConvertAll(o => new TBlockModel() { TBlockPosition = o, IsAuto = rd.Next(0, 2) > 0 });
                        CCTBCore.TBlockMessageNotificationEvent?.Invoke(this, argsA);

                        var argsB = new MessageEventArgs<TBlockMessage>();
                        argsB.Message = new TBlockMessage();
                        argsB.Message.SideType = ESideType.B;
                        argsB.Message.Datas = datas.ConvertAll(o => new TBlockModel() { TBlockPosition = o, IsAuto = rd.Next(0, 2) > 0 });
                        CCTBCore.TBlockMessageNotificationEvent?.Invoke(this, argsB);
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {

                    }

                }
            });
        }

        private async void TestCCDMessage()
        {
            await RunInBackgroundAsync(async () =>
            {
                var rd = new Random();
                var func = new Func<ESideType, bool, MessageEventArgs<CCDMessage>>((side, isDry) =>
                {
                    var data = new CCDModel();
                    data.GatherTime = DateTime.Now;
                    data.BatteryTabDatas = new double[5];
                    data.CeramicDatas = new double[8];
                    data.FilmDatas = new double[4];
                    data.MalpositionDatas = new double[8];
                    for (int i = 0; i < 4; i++)
                    {
                        if (i == 0) data.BatteryTabDatas[0] = Math.Round(rd.NextDouble() * 10 + 100, 3);
                        data.CeramicDatas[2 * i] = Math.Round(rd.NextDouble() * 10 + 100, 3);
                        data.FilmDatas[i] = Math.Round(rd.NextDouble() * 10 + 100, 3);
                        data.CeramicDatas[2 * i + 1] = Math.Round(rd.NextDouble() * 10 + 100, 3);
                        data.BatteryTabDatas[i + 1] = Math.Round(rd.NextDouble() * 10 + 100, 3);
                        data.MalpositionDatas[2 * i] = Math.Round(rd.NextDouble() * 10 + 100, 3);
                        data.MalpositionDatas[2 * i + 1] = Math.Round(rd.NextDouble() * 10 + 100, 3);
                    }
                    data.BatteryTabLimitDown = 102;
                    data.BatteryTabLimitUp = 108;
                    data.CeramicLimitDown = 102;
                    data.CeramicLimitUp = 108;
                    data.FilmLimitDown = 102;
                    data.FilmLimitUp = 108;
                    data.MalpositionLimitDown = 102;
                    data.MalpositionLimitUp = 108;

                    var message = new MessageEventArgs<CCDMessage>();
                    message.Message = new CCDMessage();
                    message.Message.Data = data;
                    message.Message.SideType = side;
                    message.Message.IsDryFilmData = isDry;

                    return message;
                });
                while (true)
                {
                    try
                    {
                        var message1 = func(ESideType.A, true);
                        message1.Message!.Data!.BatteryTabDatas![0] = 0;
                        CCTBCore.CCDMessageNotificationEvent?.Invoke(this, message1);

                        var message2 = func(ESideType.A, false);
                        message2.Message!.Data!.BatteryTabDatas![0] = 1;
                        CCTBCore.CCDMessageNotificationEvent?.Invoke(this, message2);

                        var message3 = func(ESideType.B, true);
                        message3.Message!.Data!.BatteryTabDatas![0] = 2;
                        CCTBCore.CCDMessageNotificationEvent?.Invoke(this, message3);

                        var message4 = func(ESideType.B, false);
                        message4.Message!.Data!.BatteryTabDatas![0] = 3;
                        CCTBCore.CCDMessageNotificationEvent?.Invoke(this, message4);
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            });
        }

        private T? ObjCopy<T>(T obj)
        {
            if (obj is string) return obj;
            var json = JsonConvert.SerializeObject(obj);
            var reObj = JsonConvert.DeserializeObject<T>(json);
            return reObj;
        }
    }
    #endregion

    //    #region 公共模块
    //    /// <summary>
    //    /// 公共模块
    //    /// </summary>
    //    partial class VM_CCTBMonitoringPage : ViewModelBase
    //    {
    //        public async Task InitializeProjectSettingsCommonAsync()
    //        {
    //            // 初始化Python配置字段
    //            await RunInBackgroundAsync(InitializePythonConfigAsync);
    //            // 初始化Python环境
    //            await RunInBackgroundAsync(InitializePythonAsync);
    //            // 验证PID算法文件
    //            await RunInBackgroundAsync(InitializePythonAsync);
    //            // 初始化PLC字符串颠倒
    //            await RunInBackgroundAsync(InitializePLCConfigAsync);
    //            // 初始化算法模型配置字段
    //            await RunInBackgroundAsync(InitializeArithmeticModelConfigAsync);

    //            // 异步线程初始化
    //            InitActions();
    //        }
    //        #region 私有操作
    //        #region 配置参数

    //        /// <summary>
    //        /// Pid信息缓存字典
    //        /// </summary>
    //        private Dictionary<string, (string url, string version, string pythonStr)> PidFileInfoDic = new Dictionary<string, (string url, string version, string pythonStr)>();
    //        #endregion

    //        #region 初始化
    //        /// <summary>
    //        /// 初始化PLC字符串颠倒（欧姆龙PLC需配置IsStringReverseByteWord）
    //        /// </summary>
    //        /// <returns></returns>
    //        private async Task InitializePLCConfigAsync()
    //        {
    //            if (!_projectSettingRepository.Exists(model => model.Name == "IsStringReverseByteWord" && model.BelongToVM == CCTBCore.BelongToVM))
    //            {
    //                _projectSettingRepository.Add(
    //                    new ProjectSettingModel("IsStringReverseByteWord", CCTBCore.BelongToVM, false.ToString(), DataTypeConverter.TypeToString(typeof(bool)), 2)
    //                    {
    //                        Remark = "涂布PLC字符串颠倒配置：欧姆龙PLC"
    //                    }
    //                );
    //            }

    //            var enabledConfigs = await _plcConfigRepository.GetEnabledConfigsAsync();
    //            var isStringReverseByteWord = _projectSettingRepository.GetByNameAndVM("IsStringReverseByteWord", CCTBCore.BelongToVM)?.Value.ToBool() ?? false;
    //            if (!isStringReverseByteWord) return;
    //            if (!enabledConfigs.Exists(o => o.Name == CCTBCore.CCTBPLCName)) return;

    //            var isCompleted = false;
    //            do
    //            {
    //                await Task.Delay(100);
    //                var plc = _pLCService.GetPLCByName(CCTBCore.CCTBPLCName) as PLC.PLC;
    //                if (!(plc != null && plc.Device != null)) continue;

    //                plc.Device.ByteTransform.IsStringReverseByteWord = isStringReverseByteWord;
    //                isCompleted = true;
    //                _logger.Info($"[{CCTBCore.CCTBPLCName} {plc.IP} {plc.Port}]配置[IsStringReverseByteWord:{isStringReverseByteWord}]完成！");
    //            }
    //            while (!isCompleted);
    //        }

    //        /// <summary>
    //        /// 初始化算法信息配置
    //        /// </summary>
    //        /// <returns></returns>
    //        private Task InitializeArithmeticModelConfigAsync()
    //        {
    //            var sideTypes = Enum.GetValues(typeof(ESideType));
    //            var closedLoopTypes = Enum.GetValues(typeof(EClosedLoopType));
    //            foreach (ESideType sideType in sideTypes)
    //            {
    //                foreach (EClosedLoopType closedLoopType in closedLoopTypes)
    //                {
    //                    var arithmeticEnableName = $"ArithmeticEnable{sideType}_{closedLoopType}";
    //                    var arithmeticNameName = $"ArithmeticName{sideType}_{closedLoopType}";

    //                    if (!_projectSettingRepository.Exists(model => model.Name == arithmeticEnableName && model.BelongToVM == CCTBCore.BelongToVM))
    //                    {
    //                        _projectSettingRepository.Add(
    //                            new ProjectSettingModel(arithmeticNameName, CCTBCore.BelongToVM, false.ToString(), DataTypeConverter.TypeToString(typeof(bool)), 2)
    //                            {
    //                                Remark = $"涂布算法使能-{sideType}面{closedLoopType}"
    //                            }
    //                        );
    //                    }

    //                    if (!_projectSettingRepository.Exists(model => model.Name == arithmeticNameName && model.BelongToVM == CCTBCore.BelongToVM))
    //                    {
    //                        _projectSettingRepository.Add(
    //                            new ProjectSettingModel(arithmeticNameName, CCTBCore.BelongToVM, "---", DataTypeConverter.TypeToString(typeof(string)), 2)
    //                            {
    //                                Remark = $"涂布算法名称-{sideType}面{closedLoopType}"
    //                            }
    //                        );
    //                    }
    //                }
    //            }
    //            return Task.CompletedTask;
    //        }

    //        /// <summary>
    //        /// 初始化Python配置字段（Python）
    //        /// </summary>
    //        /// <returns></returns>
    //        private Task InitializePythonConfigAsync()
    //        {
    //            var PythonEnableName = "PythonEnable";
    //            var PythonEnvironmentPathName = "PythonEnvironmentPath";
    //            if (!_projectSettingRepository.Exists(model => model.Name == PythonEnableName && model.BelongToVM == CCTBCore.BelongToVM))
    //            {
    //                _projectSettingRepository.Add(
    //                    new ProjectSettingModel(PythonEnableName, CCTBCore.BelongToVM, false.ToString(), DataTypeConverter.TypeToString(typeof(bool)), 2)
    //                    {
    //                        Remark = "涂布闭环Python初始化开关"
    //                    }
    //                );
    //            }
    //            if (!_projectSettingRepository.Exists(model => model.Name == PythonEnvironmentPathName && model.BelongToVM == CCTBCore.BelongToVM))
    //            {
    //                _projectSettingRepository.Add(
    //                    new ProjectSettingModel(PythonEnvironmentPathName, CCTBCore.BelongToVM, "D:\\通用上位机\\闭环程序文件\\Python\\Python38", DataTypeConverter.TypeToString(typeof(string)), 2)
    //                    {
    //                        Remark = "涂布闭环Python环境部署路径，例：D:\\通用上位机\\闭环程序文件\\Python\\Python38"
    //                    }
    //                );
    //            }

    //            PythonEnable = _projectSettingRepository.GetByNameAndVM(PythonEnableName, CCTBCore.BelongToVM)?.Value.ToBool() ?? false;
    //            PythonEnvironmentPath = _projectSettingRepository.GetByNameAndVM(PythonEnvironmentPathName, CCTBCore.BelongToVM)?.Value ?? string.Empty;
    //            PythonPydPath = $"{PythonEnvironmentPath}\\..\\Dlls\\TBPyds";

    //            return Task.CompletedTask;
    //        }

    //        /// <summary>
    //        /// 初始化python环境（使能、python环境路径）
    //        /// </summary>
    //        private Task InitializePythonAsync()
    //        {
    //            try
    //            {
    //                if (!PythonEnable)
    //                {
    //                    _logger.Warn($"未开启初始化Python环境初始化使能");
    //                    return Task.CompletedTask;
    //                }
    //                if (string.IsNullOrEmpty(PythonEnvironmentPath) || !Path.Exists(PythonEnvironmentPath))
    //                {
    //                    _logger.Error($"Python环境路径配置异常或不存在！");
    //                    return Task.CompletedTask;
    //                }

    //                var pyInstallPath = $"{PythonEnvironmentPath}".Replace("\\", "/");
    //                var pySearchPath1 = $"{PythonEnvironmentPath}\\..\\Dlls".Replace("\\", "/");
    //                if (!Directory.Exists(pySearchPath1)) Directory.CreateDirectory(pySearchPath1);
    //                var pySearchPath2 = $"{PythonPydPath}".Replace("\\", "/");
    //                if (!Directory.Exists(pySearchPath2)) Directory.CreateDirectory(pySearchPath2);
    //                // 启动PythonThread线程
    //                PythonHelper.Instance.PyhtonThreadStart();
    //                // 初始化Python环境配置
    //                PythonHelper.Instance.InitPythonConfig(pyInstallPath, pySearchPath1, pySearchPath2);
    //                return Task.CompletedTask;
    //            }
    //            catch (Exception ex)
    //            {
    //                throw new Exception($"初始化Python环境异常！", ex);
    //            }
    //        }

    //        #region PID算法文件校验
    //        private List<string> WebUrls = new List<string>();

    //        private bool PythonEnable = false;
    //        private string PythonEnvironmentPath = string.Empty;
    //        private string PythonPydPath = string.Empty;

    //        /// <summary>
    //        /// PID算法校验
    //        /// </summary>
    //        /// <exception cref="Exception"></exception>
    //        private Task InitArithmeticVerityAsync()
    //        {
    //            // 删除所有缓存pyd文件
    //            if (Directory.Exists(PythonPydPath))
    //            {
    //                // 获取 TBPyds 及子目录下的所有文件（若需过滤后缀可在这里改 filter）
    //                var files = Directory.EnumerateFiles(PythonPydPath, "*.pyd", SearchOption.AllDirectories).ToList();
    //                files.ForEach(o => File.Delete(o));
    //            }
    //            var sideTypes = Enum.GetValues(typeof(ESideType));
    //            var closedLoopTypes = Enum.GetValues(typeof(EClosedLoopType));
    //            foreach (ESideType sideType in sideTypes)
    //            {
    //                foreach (EClosedLoopType closedLoopType in closedLoopTypes)
    //                {
    //                    var arithmeticModel = GetArithmeticModelConfig(sideType, closedLoopType);
    //                    if (!arithmeticModel.IsEnable)
    //                    {
    //                        _logger.Warn($"{sideType}面{closedLoopType}算法未开启，已跳过验证！");
    //                        continue;
    //                    }
    //                    if (string.IsNullOrEmpty(arithmeticModel.ArithmeticName))
    //                    {
    //                        throw new ArgumentException($"{sideType}面{closedLoopType}算法名称为空!", nameof(arithmeticModel.ArithmeticName));
    //                    }
    //                    foreach (var webUrl in WebUrls)
    //                    {
    //                        // 执行算法验证
    //                        var re = VarietySelectedVerity(varietyName.Replace('.', 'd'));
    //                        if (!re.isSuccess)
    //                        {
    //                            _logger.Error($"初始化PID：[{re.message}", type: eType, projectName: eProjectName, level: eLevel);
    //                            throw new Exception($"{re.message}");
    //                        }
    //                    }
    //                }
    //            }

    //            _logger.Info($"初始化PID：[{re.message}", false);
    //            return Task.CompletedTask;
    //        }

    //        /// <summary>
    //        /// 获取算法配置
    //        /// </summary>
    //        /// <param name="sideType"></param>
    //        /// <param name="closedLoopType"></param>
    //        /// <returns></returns>
    //        private ArithmeticModel GetArithmeticModelConfig(ESideType sideType, EClosedLoopType closedLoopType)
    //        {
    //            var arithmeticEnableName = $"ArithmeticEnable{sideType}_{closedLoopType}";
    //            var arithmeticNameName = $"ArithmeticName{sideType}_{closedLoopType}";

    //            var arithmeticEnable = _projectSettingRepository.GetByNameAndVM(arithmeticEnableName, CCTBCore.BelongToVM)?.Value.ToBool() ?? false;
    //            var arithmeticName = _projectSettingRepository.GetByNameAndVM(arithmeticNameName, CCTBCore.BelongToVM)?.Value ?? string.Empty;

    //            var arithmeticModel = new ArithmeticModel();
    //            arithmeticModel.IsEnable = arithmeticEnable;
    //            arithmeticModel.ArithmeticName = arithmeticName;
    //            arithmeticModel.ArithmeticType = ArithmeticModel.GetEArithmeticType(closedLoopType, arithmeticName);
    //            return arithmeticModel;
    //        }

    //        private async Task<bool> RefreshAndCheckTBPydConfig(ESideType sideType, ArithmeticModel arithmeticModel, string url, out string message)
    //        {
    //            var arithmeticType = arithmeticModel.ArithmeticType;
    //            if (!arithmeticModel.IsEnable)
    //            {
    //                message = $"[{sideType}面未开启{arithmeticType}算法功能！";
    //                return true;
    //            }

    //            var arithmeticName = arithmeticModel.ArithmeticName;
    //            var reVersion = WebInterfaceClient.WebClient.selFileVersion(url, arithmeticName);
    //            // 获取版本信息失败
    //            if (!(reVersion != null && (reVersion.success || reVersion.code == "200")))
    //            {
    //                message = $"获取{arithmeticType}算法版本信息失败：url:[{url}] pythonCode:[{arithmeticName}]-code[{reVersion?.code}]-message[{reVersion?.message}]";
    //                return false;
    //            }

    //            // url、版本一致，则不需要更新
    //            if (PidFileInfoDic.ContainsKey(arithmeticName) && PidFileInfoDic[arithmeticName].url == url && PidFileInfoDic[arithmeticName].version == reVersion.data)
    //            {
    //                message = $"{arithmeticType}算法文件无需更新！";
    //                return true;
    //            }

    //            // 获取PID算法文件
    //            var xmlPublicKey = RasHelper.ToXmlPublicKey(ArithmeticModel.PublicKey);
    //            var signature = RasHelper.RSAEncrypt(xmlPublicKey, ArithmeticModel.AppKey);
    //            var reFile = await _webApiClient.DownloadSysCenterFileAsync(ArithmeticModel.AppKey, signature, arithmeticName, ArithmeticModel.ExecuteType);
    //            if (!(reFile.DataStr is byte[] fileBuffer && fileBuffer.Length > 0))
    //            {
    //                message = $"获取{arithmeticType}算法文件信息失败：url:[{url}] pythonCode:[{arithmeticName}]-message[{reFile?.message}]";
    //                return false;
    //            }

    //            var path = $"{PythonPydPath}\\{reVersion.data}";
    //            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    //            var fullPath = Path.Combine(path, $"{arithmeticName}.pyd");
    //            if (!File.Exists(fullPath)) File.WriteAllBytes(fullPath, reFile.dataStr);

    //            var arithmeticCode = GetArithmeticCode(arithmeticType, $"{reVersion.data}.{arithmeticName}");
    //            // PID算法文件验证
    //            var isSuccess = CheckTBPythonAction(arithmeticModel, arithmeticCode, out string msg);
    //            if (!isSuccess)
    //            {
    //                message = $"[{sideType}面{arithmeticType}算法文件验证失败:url:[{url}]-pythonCode[{arithmeticName}]-版本[{reVersion.data}]-描述[{reFile?.message}]！异常:{msg}";
    //                return false;
    //            }

    //            // 更新算法信息
    //            lock (PidFileInfoDic) PidFileInfoDic[arithmeticName] = (url, reVersion.data, arithmeticCode);
    //            message = msg;
    //            _logger.Info($"获取[{sideType}面{arithmeticType}算法文件信息完成 url:[{url}]-[{reVersion.data}]-[{reFile?.message}]", false);
    //            return true;
    //        }

    //        /// <summary>
    //        /// 获取加密算法公开python程序
    //        /// </summary>
    //        /// <param name="arithmeticType"></param>
    //        /// <param name="arithmeticName"></param>
    //        /// <returns></returns>
    //        private string GetArithmeticCode(EArithmeticType arithmeticType, string arithmeticName)
    //        {
    //            switch (arithmeticType)
    //            {
    //                case EArithmeticType.流量PID:
    //                    {
    //                        return $@"# -*- coding: utf-8 -*-
    //import io
    //import pandas as pd
    //import re
    //from {arithmeticName} import FluMDControl

    //def algorithm_run(target, cur_val, cur_val_1, cur_val_2, dt, p, i, d, pump_top, pump_bottom, flu_top, flu_bottom, single, flu_top_delta, flu_bottom_delta, input_1):
    //    dff = FluMDControl(target, cur_val, cur_val_1, cur_val_2, dt, p, i, d, pump_top, pump_bottom, flu_top, flu_bottom, single, flu_top_delta, flu_bottom_delta, input_1)
    //    pump_top, pump_bottom = dff.final_output()
    //    return pump_top, pump_bottom";
    //                    }
    //                case EArithmeticType.专家PID:
    //                    {
    //                        return $@"# -*- coding: utf-8 -*-
    //import io
    //import pandas as pd
    //import re
    //from {arithmeticName} import MDControl

    //def algorithm_run(target, cur_val, cur_val_1, cur_val_2, dt, p, i, d, pump_top, pump_bottom, flu_top, flu_bottom, single, flu_top_delta, flu_bottom_delta, input_1):
    //    dff = MDControl(target, cur_val, cur_val_1, cur_val_2, dt, p, i, d, pump_top, pump_bottom, flu_top, flu_bottom, single, flu_top_delta, flu_bottom_delta, input_1)
    //    pump_top, pump_bottom = dff.final_output()
    //    return pump_top, pump_bottom";
    //                    }
    //                case EArithmeticType.TDControl:
    //                    {
    //                        return $@"# -*- coding: utf-8 -*-
    //import io
    //import pandas as pd
    //import re
    //from {arithmeticName} import TDControl

    //def algorithm_run(data_str):
    //    data = re.sub(r'\[(.*?)\]', lambda x: x.group(0).replace(',', ' '), data_str)
    //    data = io.StringIO(data)
    //    data = pd.read_csv(data)

    //    dff = TDControl(data)
    //    td_result = dff.final_output()
    //    return td_result";
    //                    }
    //                case EArithmeticType.MDTDControl:
    //                    {
    //                        return $@"# -*- coding: utf-8 -*-
    //import io
    //import pandas as pd
    //import re
    //from {arithmeticName} import MDTDControl

    //def algorithm_run(data_str):
    //    data = re.sub(r'\[(.*?)\]', lambda x: x.group(0).replace(',', ' '), data_str)
    //    data = io.StringIO(data)
    //    data = pd.read_csv(data)

    //    dff = MDTDControl(data)
    //    mdtd_result = dff.final_output()
    //    return mdtd_result";
    //                    }
    //                case EArithmeticType.MULTIMDTDControl:
    //                    {
    //                        return $@"# -*- coding: utf-8 -*-
    //import io
    //import pandas as pd
    //import re
    //from {arithmeticName} import MultiMDTDControl

    //def algorithm_run(data_str):  # 多腔调用代码
    //    data = re.sub(r'\[(.*?)\]', lambda x: x.group(0).replace(',', ' '), data_str)
    //    data = io.StringIO(data)
    //    data = pd.read_csv(data)

    //    dff = MultiMDTDControl(data)
    //    md_result, td_result = dff.final_output()
    //    return md_result, td_result";
    //                    }
    //                default: return "";
    //            }
    //        }

    //        /// <summary>
    //        /// 涂布Python算法校验
    //        /// </summary>
    //        /// <param name="arithmeticType"></param>
    //        /// <param name="arithmeticName"></param>
    //        /// <param name="arithmeticCode"></param>
    //        /// <param name="message"></param>
    //        /// <returns></returns>
    //        private bool CheckTBPythonAction(ArithmeticModel arithmeticModel, string arithmeticCode, out string message)
    //        {
    //            try
    //            {
    //                var arithmeticType = arithmeticModel.ArithmeticType;
    //                var arithmeticName = arithmeticModel.ArithmeticName;
    //                if (arithmeticType == EArithmeticType.流量PID)
    //                {
    //                    var data = "flu_top,flu_bottom,range,target,weight,pump_top,pump_bottom" +
    //                        "\r\n134.6527778,127.5,1.2075,189.8,189.907,44.74,43.56" +
    //                        "\r\n134.5972222,127.3888889,1.8302,189.8,189.952,44.74,43.56" +
    //                        "\r\n134.5125,127.25,1.5172,189.8,189.945,44.74,43.56" +
    //                        "\r\n134.65,127.3375,1.8941,189.8,189.946,44.74,43.56" +
    //                        "\r\n134.6111111,127.3888889,1.5686,189.8,189.945,44.74,43.56" +
    //                        "\r\n134.625,127.6388889,1.5039,189.8,189.96,44.74,43.56" +
    //                        "\r\n134.6944444,127.6111111,1.5243,189.8,190,44.74,43.56" +
    //                        "\r\n134.7638889,127.4444444,1.6989,189.8,190.012,44.74,43.56" +
    //                        "\r\n134.7361111,127.3333333,1.5889,189.8,189.993,44.74,43.56";
    //                    var reJJ = AddExcuteFluPIDpyAction(arithmeticName, arithmeticCode, (20, 21), data, 3, 3f, 0.25, 0.14, 5, 0, null, null);
    //                    message = "success";
    //                    return true;
    //                }
    //                else if (arithmeticType == EArithmeticType.专家PID)
    //                {
    //                    var pidTest = new PIDModel() { T = 0.05f, P = 0.005f, I = 0.001f, D = 0.001f, };
    //                    var reJJ = AddExcuteExpertPIDpyAction(arithmeticName, arithmeticCode, pidTest, 301, 302, 303, 304, (20, 21), (0, 0), (0, 0), 0.1, null);
    //                    message = "success";
    //                    return true;
    //                }
    //                else if (arithmeticType == EArithmeticType.TDControl)
    //                {
    //                    var data = "时间,重量1,重量2,重量3,重量4,重量5,重量6,重量7,重量8,重量9,重量10,重量11,重量12,重量13,重量14,重量15,重量16,重量17,重量18,重量19,重量20,重量21,重量22,重量23,重量24,重量25," +
    //                        "T块调机模式,T块[1]-实时位置,T块[2]-实时位置,T块[3]-实时位置,T块[4]-实时位置,T块[5]-实时位置,T块[6]-实时位置,T块[7]-实时位置,T块[8]-实时位置,T块[9]-实时位置,T块[10]-实时位置,T块[11]-实时位置,T块[12]-实时位置,T块[13]-实时位置,T块[14]-实时位置,T块[15]-实时位置,T块[16]-实时位置,T块[17]-实时位置,T块[18]-实时位置,T块[19]-实时位置,T块[20]-实时位置,T块[21]-实时位置,T块[22]-实时位置,T块[23]-实时位置,T块[24]-实时位置,T块[25]-实时位置," +
    //                        "T块[1]-报警标识,T块[2]-报警标识,T块[3]-报警标识,T块[4]-报警标识,T块[5]-报警标识,T块[6]-报警标识,T块[7]-报警标识,T块[8]-报警标识,T块[9]-报警标识,T块[10]-报警标识,T块[11]-报警标识,T块[12]-报警标识,T块[13]-报警标识,T块[14]-报警标识,T块[15]-报警标识,T块[16]-报警标识,T块[17]-报警标识,T块[18]-报警标识,T块[19]-报警标识,T块[20]-报警标识,T块[21]-报警标识,T块[22]-报警标识,T块[23]-报警标识,T块[24]-报警标识,T块[25]-报警标识," +
    //                        "T块[1]-上限值,T块[2]-上限值,T块[3]-上限值,T块[4]-上限值,T块[5]-上限值,T块[6]-上限值,T块[7]-上限值,T块[8]-上限值,T块[9]-上限值,T块[10]-上限值,T块[11]-上限值,T块[12]-上限值,T块[13]-上限值,T块[14]-上限值,T块[15]-上限值,T块[16]-上限值,T块[17]-上限值,T块[18]-上限值,T块[19]-上限值,T块[20]-上限值,T块[21]-上限值,T块[22]-上限值,T块[23]-上限值,T块[24]-上限值,T块[25]-上限值," +
    //                        "T块[1]-下限值,T块[2]-下限值,T块[3]-下限值,T块[4]-下限值,T块[5]-下限值,T块[6]-下限值,T块[7]-下限值,T块[8]-下限值,T块[9]-下限值,T块[10]-下限值,T块[11]-下限值,T块[12]-下限值,T块[13]-下限值,T块[14]-下限值,T块[15]-下限值,T块[16]-下限值,T块[17]-下限值,T块[18]-下限值,T块[19]-下限值,T块[20]-下限值,T块[21]-下限值,T块[22]-下限值,T块[23]-下限值,T块[24]-下限值,T块[25]-下限值" +
    //                        "\r\n2024/8/30 11:42:45,0,698.21,696.532,699.996,698.877,700.078,700.268,700.713,701.509,702.343,703.169,0,0,0,698.128,700.343,701.539,701.2,702.276,700.519,702.51,701.585,700.009,699.294,0,1,0,1043,410,1317,1375,1028,1050,1197,857,620,980,0,0,10,1305,766,894,635,731,647,1154,955,1006,663,0,[],[],[20321,20323],[20423],[],[],[],[],[],[],[],[],[],[],[21523],[],[],[],[],[],[],[],[],[],[],0,476,637,455,427,440,394,434,431,319,347,0,17,0,315,0,323,67,167,178,288,213,351,183,0,395,1949,2000,1939,1952,1970,1920,1722,1961,1575,1652,285,270,353,1720,1425,1724,1572,1689,1595,1808,1729,1704,1539,360" +
    //                        "\r\n2024/8/30 11:42:39,0,699.175,698.676,699.841,700.053,699.338,700.084,699.493,699.942,700.469,702.459,0,0,0,700.396,701.892,701.807,701.425,701.602,700.231,701.254,699.73,699.429,700.291,0,1,0,1043,411,1317,1376,1028,1051,1197,857,621,980,0,0,10,1306,766,894,635,731,646,1154,954,1005,663,0,[],[],[20321,20323],[20423],[],[],[],[],[],[],[],[],[],[],[21523],[],[],[],[],[],[],[],[],[],[],0,476,637,455,427,440,394,434,431,319,347,0,17,0,315,0,323,67,167,178,288,213,351,183,0,395,1949,2000,1939,1952,1970,1920,1722,1961,1575,1652,285,270,353,1720,1425,1724,1572,1689,1595,1808,1729,1704,1539,360" +
    //                        "\r\n2024/8/30 11:42:33,0,699.916,699.621,701.308,700.997,699.481,700.283,699.291,698.261,698.997,702.221,0,0,0,701.887,703.129,703.393,702.023,701.251,699.295,700.013,699.236,699.38,700.257,0,1,0,1044,410,1317,1375,1028,1050,1198,857,620,979,0,0,10,1305,766,895,635,730,647,1155,954,1005,663,0,[],[],[20321,20323],[20423],[],[],[],[],[],[],[],[],[],[],[21523],[],[],[],[],[],[],[],[],[],[],0,476,637,455,427,440,394,434,431,319,347,0,17,0,315,0,323,67,167,178,288,213,351,183,0,395,1949,2000,1939,1952,1970,1920,1722,1961,1575,1652,285,270,353,1720,1425,1724,1572,1689,1595,1808,1729,1704,1539,360" +
    //                        "\r\n2024/8/30 11:42:27,0,698.986,698.259,701.678,701.498,701.218,700.962,700.641,699.055,699.687,701.491,0,0,0,700.329,702.149,704.384,703.922,703.133,700.749,700.195,699.521,699.23,699.51,0,1,0,1044,411,1317,1375,1028,1050,1197,857,620,979,0,0,9,1305,766,895,635,730,646,1155,954,1006,663,0,[],[],[20321,20323],[20423],[],[],[],[],[],[],[],[],[],[],[21523],[],[],[],[],[],[],[],[],[],[],0,476,637,455,427,440,394,434,431,319,347,0,17,0,315,0,323,67,167,178,288,213,351,183,0,395,1949,2000,1939,1952,1970,1920,1722,1961,1575,1652,285,270,353,1720,1425,1724,1572,1689,1595,1808,1729,1704,1539,360";
    //                    var reJJ = AddExcuteTDControlpyAction(arithmeticName, arithmeticCode, data);
    //                    message = "success";
    //                    return true;
    //                }
    //                else if (arithmeticType == EArithmeticType.MDTDControl)
    //                {
    //                    var data = "目标值,公差,泵速,T块调机模式,重量1,重量2,重量3,重量4,重量5,重量6,重量7,重量8,重量9,重量10,重量11,重量12,重量13,重量14,重量15,重量16,重量17,重量18,重量19,重量20,重量21,重量22,重量23,重量24,重量25," +
    //                        "T块[1]-实时位置,T块[2]-实时位置,T块[3]-实时位置,T块[4]-实时位置,T块[5]-实时位置,T块[6]-实时位置,T块[7]-实时位置,T块[8]-实时位置,T块[9]-实时位置,T块[10]-实时位置,T块[11]-实时位置,T块[12]-实时位置,T块[13]-实时位置,T块[14]-实时位置,T块[15]-实时位置,T块[16]-实时位置,T块[17]-实时位置,T块[18]-实时位置,T块[19]-实时位置,T块[20]-实时位置,T块[21]-实时位置,T块[22]-实时位置,T块[23]-实时位置,T块[24]-实时位置,T块[25]-实时位置," +
    //                        "T块[1]-上限值,T块[2]-上限值,T块[3]-上限值,T块[4]-上限值,T块[5]-上限值,T块[6]-上限值,T块[7]-上限值,T块[8]-上限值,T块[9]-上限值,T块[10]-上限值,T块[11]-上限值,T块[12]-上限值,T块[13]-上限值,T块[14]-上限值,T块[15]-上限值,T块[16]-上限值,T块[17]-上限值,T块[18]-上限值,T块[19]-上限值,T块[20]-上限值,T块[21]-上限值,T块[22]-上限值,T块[23]-上限值,T块[24]-上限值,T块[25]-上限值," +
    //                        "T块[1]-下限值,T块[2]-下限值,T块[3]-下限值,T块[4]-下限值,T块[5]-下限值,T块[6]-下限值,T块[7]-下限值,T块[8]-下限值,T块[9]-下限值,T块[10]-下限值,T块[11]-下限值,T块[12]-下限值,T块[13]-下限值,T块[14]-下限值,T块[15]-下限值,T块[16]-下限值,T块[17]-下限值,T块[18]-下限值,T块[19]-下限值,T块[20]-下限值,T块[21]-下限值,T块[22]-下限值,T块[23]-下限值,T块[24]-下限值,T块[25]-下限值," +
    //                        "T块[1]-报警标识,T块[2]-报警标识,T块[3]-报警标识,T块[4]-报警标识,T块[5]-报警标识,T块[6]-报警标识,T块[7]-报警标识,T块[8]-报警标识,T块[9]-报警标识,T块[10]-报警标识,T块[11]-报警标识,T块[12]-报警标识,T块[13]-报警标识,T块[14]-报警标识,T块[15]-报警标识,T块[16]-报警标识,T块[17]-报警标识,T块[18]-报警标识,T块[19]-报警标识,T块[20]-报警标识,T块[21]-报警标识,T块[22]-报警标识,T块[23]-报警标识,T块[24]-报警标识,T块[25]-报警标识" +
    //                        "\r\n376,4,20,1,0,372.931,375.365,376.064,375.772,375.416,375.935,377.227,376.711,375.169,375.272,0,0,0,376.119,375.559,375.275,376.282,376.695,376.563,376.057,376.645,376.553,376.916,0,0,101,573,1020,420,454,609,651,650,512,646,36,0,166,877,551,850,676,775,867,739,605,731,355,7,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n376,4,20,1,0,373.62,375.78,375.642,375.357,375.676,376.181,376.869,375.974,375.918,374.743,0,0,0,375.286,376.079,375.497,375.793,376.076,376.502,376.333,376.267,375.871,376.985,0,0,101,572,1020,420,455,609,651,649,511,646,37,0,166,877,551,849,676,775,867,739,605,731,355,6,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n376,4,20,1,0,373.595,376.059,376.213,376.075,376.251,376.684,376.441,375.91,376.124,374.494,0,0,0,374.945,376.55,376.141,376.35,376.07,376.843,376.387,376.409,376.366,376.411,0,0,101,572,1021,420,454,610,650,649,512,647,37,0,166,876,551,850,675,774,867,739,605,732,355,6,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n376,4,20,1,0,373.536,376.394,376.349,376.873,376.368,375.928,376.049,376.751,376.276,374.462,0,0,0,375.898,376.607,375.686,376.299,375.776,375.785,376.377,376.593,376.971,376.488,0,0,101,572,1020,420,455,610,650,649,511,647,37,0,166,877,551,849,676,774,867,739,605,732,356,6,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n376,4,20,1,0,373.5,376.837,376.484,376.809,375.256,375.005,375.557,376.414,376.352,374.017,0,0,0,376.253,376.72,375.539,375.767,375.304,376.049,376.739,377.103,375.962,376.091,0,0,101,573,1020,421,455,609,650,649,512,646,37,0,166,876,551,849,675,775,867,740,605,731,355,7,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n376,4,20,1,0,372.691,376.577,377.5,376.134,375.958,375.508,375.66,376.352,376.051,373.897,0,0,0,376.002,377.36,376.363,376.073,376.087,376.747,376.371,376.631,376.817,376.768,0,0,101,572,1021,420,455,609,650,650,512,646,36,0,166,876,550,850,676,774,867,739,606,731,355,7,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n376,4,20,1,0,373.371,376.66,376.119,375.512,376.293,375.612,375.551,376.816,375.345,374.293,0,0,0,375.976,377.538,376.571,376.607,375.978,376.182,376.44,375.637,377.549,376.862,0,0,101,573,1019,419,451,609,650,649,511,646,35,0,166,875,550,849,677,775,869,743,608,734,359,7,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,[],[20221],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]";
    //                    var reJJ = AddExcuteMDTDControlpyAction(arithmeticName, arithmeticCode, data);
    //                    message = "success";
    //                    return true;
    //                }
    //                else if (arithmeticType == EArithmeticType.MULTIMDTDControl)
    //                {
    //                    var data = "时间,趟数,目标值,公差,泵速0,浆料泵启动标识0,泵速1,浆料泵启动标识1,泵速2,浆料泵启动标识2,泵速3,浆料泵启动标识3,T块调机模式,重量1,重量2,重量3,重量4,重量5,重量6,重量7,重量8,重量9,重量10,重量11,重量12,重量13,重量14,重量15,重量16,重量17,重量18,重量19,重量20,重量21,重量22,重量23,重量24,重量25,重量26,重量27,重量28,重量29,重量30,重量31,重量32,重量33,重量34,重量35,重量36,重量37,重量38,重量39,重量40,重量41,重量42," +
    //                        "T块[1]-实时位置,T块[2]-实时位置,T块[3]-实时位置,T块[4]-实时位置,T块[5]-实时位置,T块[6]-实时位置,T块[7]-实时位置,T块[8]-实时位置,T块[9]-实时位置,T块[10]-实时位置,T块[11]-实时位置,T块[12]-实时位置,T块[13]-实时位置,T块[14]-实时位置,T块[15]-实时位置,T块[16]-实时位置,T块[17]-实时位置,T块[18]-实时位置,T块[19]-实时位置,T块[20]-实时位置,T块[21]-实时位置,T块[22]-实时位置,T块[23]-实时位置,T块[24]-实时位置,T块[25]-实时位置,T块[26]-实时位置,T块[27]-实时位置,T块[28]-实时位置,T块[29]-实时位置,T块[30]-实时位置,T块[31]-实时位置,T块[32]-实时位置,T块[33]-实时位置,T块[34]-实时位置,T块[35]-实时位置,T块[36]-实时位置,T块[37]-实时位置,T块[38]-实时位置,T块[39]-实时位置,T块[40]-实时位置,T块[41]-实时位置,T块[42]-实时位置,T块[43]-实时位置,T块[44]-实时位置,T块[45]-实时位置,T块[46]-实时位置,T块[47]-实时位置,T块[48]-实时位置,T块[49]-实时位置,T块[50]-实时位置,T块[51]-实时位置,T块[52]-实时位置,T块[53]-实时位置,T块[54]-实时位置," +
    //                        "T块[1]-上限值,T块[2]-上限值,T块[3]-上限值,T块[4]-上限值,T块[5]-上限值,T块[6]-上限值,T块[7]-上限值,T块[8]-上限值,T块[9]-上限值,T块[10]-上限值,T块[11]-上限值,T块[12]-上限值,T块[13]-上限值,T块[14]-上限值,T块[15]-上限值,T块[16]-上限值,T块[17]-上限值,T块[18]-上限值,T块[19]-上限值,T块[20]-上限值,T块[21]-上限值,T块[22]-上限值,T块[23]-上限值,T块[24]-上限值,T块[25]-上限值,T块[26]-上限值,T块[27]-上限值,T块[28]-上限值,T块[29]-上限值,T块[30]-上限值,T块[31]-上限值,T块[32]-上限值,T块[33]-上限值,T块[34]-上限值,T块[35]-上限值,T块[36]-上限值,T块[37]-上限值,T块[38]-上限值,T块[39]-上限值,T块[40]-上限值,T块[41]-上限值,T块[42]-上限值,T块[43]-上限值,T块[44]-上限值,T块[45]-上限值,T块[46]-上限值,T块[47]-上限值,T块[48]-上限值,T块[49]-上限值,T块[50]-上限值,T块[51]-上限值,T块[52]-上限值," +
    //                        "T块[1]-下限值,T块[2]-下限值,T块[3]-下限值,T块[4]-下限值,T块[5]-下限值,T块[6]-下限值,T块[7]-下限值,T块[8]-下限值,T块[9]-下限值,T块[10]-下限值,T块[11]-下限值,T块[12]-下限值,T块[13]-下限值,T块[14]-下限值,T块[15]-下限值,T块[16]-下限值,T块[17]-下限值,T块[18]-下限值,T块[19]-下限值,T块[20]-下限值,T块[21]-下限值,T块[22]-下限值,T块[23]-下限值,T块[24]-下限值,T块[25]-下限值,T块[26]-下限值,T块[27]-下限值,T块[28]-下限值,T块[29]-下限值,T块[30]-下限值,T块[31]-下限值,T块[32]-下限值,T块[33]-下限值,T块[34]-下限值,T块[35]-下限值,T块[36]-下限值,T块[37]-下限值,T块[38]-下限值,T块[39]-下限值,T块[40]-下限值,T块[41]-下限值,T块[42]-下限值,T块[43]-下限值,T块[44]-下限值,T块[45]-下限值,T块[46]-下限值,T块[47]-下限值,T块[48]-下限值,T块[49]-下限值,T块[50]-下限值,T块[51]-下限值,T块[52]-下限值," +
    //                        "T块[1]-报警标识,T块[2]-报警标识,T块[3]-报警标识,T块[4]-报警标识,T块[5]-报警标识,T块[6]-报警标识,T块[7]-报警标识,T块[8]-报警标识,T块[9]-报警标识,T块[10]-报警标识,T块[11]-报警标识,T块[12]-报警标识,T块[13]-报警标识,T块[14]-报警标识,T块[15]-报警标识,T块[16]-报警标识,T块[17]-报警标识,T块[18]-报警标识,T块[19]-报警标识,T块[20]-报警标识,T块[21]-报警标识,T块[22]-报警标识,T块[23]-报警标识,T块[24]-报警标识,T块[25]-报警标识,T块[26]-报警标识,T块[27]-报警标识,T块[28]-报警标识,T块[29]-报警标识,T块[30]-报警标识,T块[31]-报警标识,T块[32]-报警标识,T块[33]-报警标识,T块[34]-报警标识,T块[35]-报警标识,T块[36]-报警标识,T块[37]-报警标识,T块[38]-报警标识,T块[39]-报警标识,T块[40]-报警标识,T块[41]-报警标识,T块[42]-报警标识,T块[43]-报警标识,T块[44]-报警标识,T块[45]-报警标识,T块[46]-报警标识,T块[47]-报警标识,T块[48]-报警标识,T块[49]-报警标识,T块[50]-报警标识,T块[51]-报警标识,T块[52]-报警标识" +
    //                        "\r\n2025-06-18 01:31:49,6332,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,350.7343,353.7544,354.5932,354.2084,354.5324,354.4376,0,0,0,0,354.6538,355.6228,354.5168,353.6692,353.79,353.6928,0,0,0,0,354.9013,354.9472,354.848,354.5796,354.0516,353.5324,0,0,0,0,354.96,354.2944,354.3932,354.812,353.9532,354.4596,0,0,0,1,2,0,3,670,124,443,282,406,0,0,0,1,325,405,314,385,337,347,0,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n2025-06-18 01:31:57,6333,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,350.4621,353.736,353.85,354.698,354.2276,354.1116,0,0,0,0,353.579,355.6964,355.7024,355.2364,355.1216,353.6884,0,0,0,0,353.7822,353.6564,353.0104,353.7224,354.0644,355.0432,0,0,0,0,354.355,353.6084,353.8252,354.126,354.0184,353.5716,0,0,0,1,2,0,3,669,124,443,282,406,0,0,0,2,325,405,314,385,337,347,1,-1,1,-38,334,330,347,376,274,152,4,0,0,1,-38,334,330,347,376,274,0,4,0,0,-38,334,330,347,376,274,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n2025-06-18 01:32:06,6334,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,350.8329,353.9324,354.5896,354.7308,354.178,354.142,0,0,0,0,353.5827,355.716,356.0712,356.3712,355.9844,354.4828,0,0,0,0,355.856,353.4808,353.9108,354.2944,355.754,355.1692,0,0,0,0,354.1324,353.4464,354.5084,354.418,353.802,354.1216,0,0,0,1,2,0,3,669,124,443,282,406,0,0,0,1,325,405,313,385,337,347,0,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n2025-06-18 01:32:14,6335,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,351.1487,354.2356,354.0708,353.824,354.8016,354.3,0,0,0,0,354.5391,355.612,354.8264,353.64,354.2436,354.2752,0,0,0,0,355.1367,355.1212,354.898,353.3852,353.7072,353.1448,0,0,0,0,354.7809,354.1616,353.1596,354.5188,353.808,354.3248,0,0,0,1,2,0,3,670,124,443,282,406,0,0,0,1,325,405,313,385,337,347,0,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n2025-06-18 01:32:28,6336,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,350.9093,353.6724,354.302,354.7648,354.4648,354.6548,0,0,0,0,354.8977,356.2012,355.2144,353.6104,353.6292,353.7696,0,0,0,0,355.4005,355.2408,354.2768,353.6236,352.906,353.0524,0,0,0,0,354.215,354.6808,354.1468,354.2208,353.6828,354.0812,0,0,0,1,2,0,3,670,124,443,282,406,0,0,0,1,325,405,314,385,337,347,0,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n2025-06-18 01:32:44,6337,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,350.4521,353.7188,354.298,354.508,354.8096,354.3292,0,0,0,0,353.479,355.7556,355.5136,355.5592,355.4652,354.0384,0,0,0,0,353.9165,353.8592,354.3468,354.5372,354.5076,354.1112,0,0,0,0,354.009,354.406,353.4832,355.092,354.2224,354.3856,0,0,0,1,2,0,6,669,124,444,281,406,0,0,0,1,325,407,314,385,337,348,1,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
    //                        "\r\n2025-06-18 01:32:53,6338,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,349.9886,354.0836,354.3388,354.2464,354.28,353.958,0,0,0,0,352.8443,355.1572,354.8992,355.1584,355.3448,354.0664,0,0,0,0,353.7,352.9328,353.4172,353.1792,354.4396,355.5012,0,0,0,0,353.2936,354.3244,353.5924,354.9792,354.6996,354.9212,0,0,0,1,2,0,7,667,123,444,281,406,0,0,0,1,325,406,314,385,336,348,0,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,[],[20221],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]";
    //                    var reJJ = AddExcuteMULTIMDTDControlpyAction(arithmeticName, arithmeticCode, data);
    //                    message = "success";
    //                    return true;
    //                }
    //                else
    //                {
    //                    message = $"未实现[{arithmeticType}]算法！";
    //                    return false;
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                message = ex.Message;
    //                return false;
    //            }
    //        }

    //        /// <summary>
    //        /// 执行py文件算法
    //        /// </summary>
    //        /// <param name="obj"></param>
    //        private void ExcuteTBPythonAction(object[] obj)
    //        {
    //            var pyKey = obj[0] as string;
    //            try
    //            {
    //                var sw = new Stopwatch();
    //                sw.Start();
    //                var pythonCode = obj[1] as string;
    //                var pyCodeStr = obj[2] as string;
    //                var dataStr = obj[3] is string ? obj[3] as string : JsonConvert.SerializeObject(obj[3]);
    //                var func = obj[4] as Func<dynamic, object, object>;
    //                _logger.Info($"算法执行开始，入参[算法模型:{pythonCode}-" +
    //                    $"数据:{dataStr}]", false);
    //                object pyResult;
    //                using (var gs = Py.GIL())
    //                {
    //                    dynamic pm = PyModule.FromString($"pythonCode-{pythonCode}", pyCodeStr);
    //                    pyResult = func(pm, obj[3]);
    //                    lock (PyResultDic) PyResultDic[pyKey] = pyResult;
    //                }
    //                sw.Stop();
    //                _logger.Info($"算法执行完毕，耗时[{sw.ElapsedMilliseconds}]！" +
    //                    $"出参:[{JsonConvert.SerializeObject(pyResult)}]", false);
    //            }
    //            catch (Exception ex)
    //            {
    //                lock (PyResultDic) PyResultDic[pyKey] = null;
    //                _logger.Error("算法文件执行异常！", ex);
    //            }
    //            finally
    //            {
    //                lock (PyManualResetEventDic)
    //                    if (PyManualResetEventDic.ContainsKey(pyKey))
    //                        PyManualResetEventDic[pyKey].Set();
    //            }
    //        }

    //        /// <summary>
    //        /// 更新pyd并执行程序
    //        /// </summary>
    //        /// <typeparam name="TOut"></typeparam>
    //        /// <param name="sideType"></param>
    //        /// <param name="arithmeticModel"></param>
    //        /// <param name="varietyName"></param>
    //        /// <param name="func"></param>
    //        /// <returns></returns>
    //        private TOut? RefreshAndExcuteTBPydAction<TOut>(ESideType sideType, ArithmeticModel arithmeticModel, Func<string, TOut> func)
    //        {
    //            try
    //            {
    //                if (!arithmeticModel.IsEnable) return default;

    //                var arithmeticName = arithmeticModel.ArithmeticName;
    //                var message = string.Empty;
    //                if (PidFileInfoDic.ContainsKey(arithmeticName) && WebUrls.Exists(o => o == PidFileInfoDic[arithmeticName].url))
    //                {
    //                    WebUrls.Remove(PidFileInfoDic[arithmeticName].url);
    //                    WebUrls.Insert(0, PidFileInfoDic[arithmeticName].url);
    //                }

    //                foreach (var webUrl in WebUrls)
    //                {
    //                    // a.不需更新；b.更新成功
    //                    if (RefreshAndCheckTBPydConfig(sideType, arithmeticModel, webUrl, out string msg))
    //                    {
    //                        message = msg;
    //                        return func.Invoke(arithmeticName);
    //                    }
    //                    message += msg;
    //                }

    //                // 更新失败
    //                _logger.Error($"{arithmeticModel.ArithmeticType}算法文件[{arithmeticName}]更新/检验算法文件异常，将使用版本[{PidFileInfoDic[arithmeticName].version}(url:{PidFileInfoDic[arithmeticName].url})]！Error:{message}"l);
    //                return func.Invoke(arithmeticName);
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"涂布[{sideType}-{arithmeticModel.ArithmeticType}]闭环算法异常", ex);
    //                var msg = $"涂布[{sideType}]闭环{arithmeticModel.ArithmeticType}算法文件执行异常！\r\n建议关闭闭环，人工调节闭环！\r\n[{ex.InnerException?.Message}]";
    //                ShowGrowl(InfoType.Error, msg);
    //                return default(TOut);
    //            }
    //        }
    //        #endregion

    //        #region 初始化异步
    //        private void InitActions()
    //        {
    //            // 一步初始化FTP上传
    //            _ = RunInBackgroundAsync(InitializeFtpSaveTimerAsync);
    //            InitializeExcelDataSink();
    //            InitializeWeightDataMessageSink();
    //            InitializeCCDDataMessageSink();

    //            // 客户端设备连接
    //            InitSocketClientMonitor();
    //            // 开启客户端
    //            ListenWight_Thread();
    //            // 开启CCD服务端
    //            InitTBCCDServer();
    //            // 开启TcpServer服务端
    //            InitTBWeightTcpServer();
    //            // 开启TcpServerDieHead服务端
    //            InitTBWeightTcpServerDieHead();

    //            OPMMGatherPLCData_Thread();
    //            GatherPlcData_Thread();
    //            GatherFlueData_Thread();
    //            TBSwitchMonitor_Thread();
    //            TBKnifeSwitchMonitor_Thread();
    //            PLCHeartLoop_Thread();
    //        }

    //        #region Socket客户端连接
    //        /// <summary>
    //        /// 客户端列表
    //        /// </summary>
    //        public ObservableCollection<CCTBDeviceModel> ClientDevices = new ObservableCollection<CCTBDeviceModel>();
    //        /// <summary>
    //        /// 初始化设备连接
    //        /// </summary>
    //        /// <exception cref="Exception"></exception>
    //        private void InitSocketClient(params string[] clientNames)
    //        {
    //            try
    //            {
    //                var plc_configeModel = new Model.plc_config();
    //                var plc_configBll = new BLL.plc_config();
    //                // enable=2 为socket通讯
    //                var plc_configlist = plc_configBll.GetModelList("enabled = 2") ?? new List<Model.plc_config>();

    //                foreach (var clientName in clientNames)
    //                {
    //                    if (!plc_configlist.Exists(o => o.plc_name == clientName))
    //                    {
    //                        _logger.Error($"未查询到[{clientName}]客户端，请检查客户端配置信息！");
    //                        continue;
    //                    }
    //                    var clientConfig = plc_configlist.First(o => o.plc_name == clientName);
    //                    if (clientConfig == null)
    //                    {
    //                        _logger.Error($"未查询到[{clientName}]客户端，请检查客户端配置信息！");
    //                        continue;
    //                    }
    //                    var dm = new CCTBDeviceModel();
    //                    dm.Name = clientConfig.plc_name;
    //                    dm.IP = clientConfig.address;
    //                    dm.Port = int.Parse(clientConfig.address_para);
    //                    ClientDevices.Add(dm);
    //                    _logger.Info($"已添加[{clientConfig.plc_name}]连接!");
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                throw new Exception("初始化设备连接异常！", ex);
    //            }
    //        }
    //        /// <summary>
    //        /// 异步监听设备连接
    //        /// </summary>
    //        /// <exception cref="Exception"></exception>
    //        private void InitSocketClientMonitor()
    //        {
    //            var clientNames = new List<string>();
    //            var tBOPMMGatherPlcDataSwitch = UserDefineVariableInfo.DicVariables["TBOPMMGatherPlcDataSwitch"].ToString();
    //            if (tBOPMMGatherPlcDataSwitch == "1") clientNames.Add("一人多机PLC");
    //            var tBSUMMTSwitch = UserDefineVariableInfo.DicVariables["TBSingleUserMultipleMachineTerminalSwitch"].ToString();
    //            if (tBSUMMTSwitch == "1")
    //            {
    //                clientNames.Add("一人多机CCD");
    //                clientNames.Add("一人多机测厚仪");
    //            }
    //            if (CCTBConfig.CommunicationMode == "TcpClient") clientNames.Add("涂布测重");
    //            if (CCTBConfig.CommunicationMode == "TcpServer_DieHead")
    //            {
    //                clientNames.Add("涂布模头A");
    //                clientNames.Add("涂布模头B");
    //            }
    //            if (clientNames?.Count <= 0) return;

    //            InitSocketClient(clientNames.ToArray());

    //            _ = RunInBackgroundAsync(async () =>
    //            {
    //                _logger.Info("初始化异步监听设备连接！");
    //                while (true)
    //                {
    //                    try
    //                    {
    //                        foreach (var clientDevice in ClientDevices)
    //                        {
    //                            // 已连接则循环
    //                            if (clientDevice.SocketClient?.IsAlive() ?? false) continue;

    //                            var client = new CCTBSocketClient();
    //                            var isOpen = client.OpenClient(clientDevice.IP, clientDevice.Port, Encoding.UTF8.GetBytes("\u0003"));
    //                            if (isOpen)
    //                            {
    //                                _logger.Info($"设备[{clientDevice.Name} {clientDevice.IPEndPoint}]连接成功！");
    //                                clientDevice.SocketClient = client;
    //                                continue;
    //                            }
    //                            _logger.Error($"设备[{clientDevice.Name} {clientDevice.IPEndPoint}]重连失败！", type: eType, projectName: eProjectName, level: ELevel.Level12);
    //                        }
    //                        await Task.Delay(5000);
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error($"[{MethodInfo.GetCurrentMethod().Name}]监听设备连接异常", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                        await Task.Delay(5000);
    //                    }
    //                }
    //            });
    //        }

    //        private CCTBSocketClient GetSocketClient(string clientName)
    //        {
    //            var exist = ClientDevices.Any(o => o.Name == clientName);
    //            if (!exist) return null;
    //            return ClientDevices.First(o => o.Name == clientName).SocketClient;
    //        }
    //        #endregion

    //        #region excel处理方法
    //        /// <summary>
    //        /// 异步保存excel文件
    //        /// 备注：
    //        ///     文件路径、文件名称、sheet名称、行数据（列名、数据）
    //        /// </summary>
    //        private BlockingCollection<(string, string, Dictionary<string, string>)> excelDataQue;
    //        private void ExcelDataEnqueue(Tuple<string, string, Dictionary<string, string>> data)
    //        {
    //            try
    //            {
    //                var excelData = (data.Item1, data.Item2, data.Item3);
    //                excelDataQue.Add(excelData);
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"表格数据缓存异常(当前缓存{excelDataQue?.Count}条)，已丢弃：[{data.Item1}]-[{data.Item2}]-表格数据[{JsonConvert.SerializeObject(data.Item3)}]", ex);
    //            }
    //        }
    //        private void InitializeExcelDataSink()
    //        {
    //            var que = new ConcurrentQueue<(string, string, Dictionary<string, string>)>();
    //            excelDataQue = new BlockingCollection<(string, string, Dictionary<string, string>)>(que, 1000);
    //            _ = RunInBackgroundAsync(ExcelDataSinkAsync);
    //        }
    //        private Task ExcelDataSinkAsync()
    //        {
    //            try
    //            {
    //                _logger.Info("初始化异步保存Excel数据文件");
    //                var sw = new Stopwatch();
    //                // 键：文件完整路径、sheet名称、多行数据 创建时间
    //                var ddLis = new List<(string FilePath, string SheetName, List<Dictionary<string, string>> Rows, DateTime CreateTime)>();
    //                foreach ((string filePath, string sheetName, Dictionary<string, string> row) data in excelDataQue.GetConsumingEnumerable())
    //                {
    //                    try
    //                    {
    //                        #region 缓存数据
    //                        if (!ddLis.Exists(o => o.FilePath == data.Item1))
    //                            ddLis.Add((data.filePath, data.sheetName, new List<Dictionary<string, string>>(), DateTime.Now));

    //                        ddLis.Find(o => o.FilePath == data.filePath).Rows.Add(data.row);
    //                        #endregion

    //                        var now = DateTime.Now;
    //                        var isNeedWrite = ddLis.Exists(dd => dd.Rows.Count > 100 || (now - dd.CreateTime).TotalSeconds > 10);
    //                        if (!isNeedWrite) continue;

    //                        var needWrites = ddLis.Where(dd => dd.Rows.Count > 100 || (now - dd.CreateTime).TotalSeconds > 10).ToList();
    //                        // 循环文件路径、sheet ---- 串行写入（IO密集型任务不适合并行）
    //                        foreach (var dd in needWrites)
    //                        {
    //                            try
    //                            {
    //                                if (dd.Rows == null || dd.Rows.Count == 0) continue;
    //                                sw.Restart();
    //                                var dt = CCTBCsvHelper.DicToTable(dd.Rows);
    //                                var finallyFilePath = CCTBCsvHelper.WriteCSV(dd.FilePath, dt);
    //                                sw.Stop();
    //                                _logger.Info($"--------------[{dd.Rows.Count}]条数据写入excel[{finallyFilePath}]成功,耗时[{sw.ElapsedMilliseconds}ms]------------------", false);
    //                                dd.Rows.Clear();
    //                            }
    //                            catch (Exception ex)
    //                            {
    //                                dd.Rows?.Clear();
    //                                _logger.Error($"[{dd.FilePath} {dd.SheetName}]文件写入异常，已清理[{dd.Rows?.Count}]条数据！", ex);
    //                                var fileChars = string.Empty;
    //                                foreach (var c in dd.FilePath) fileChars += ((int)c + "-");
    //                                _logger.Error($"文件完整路径拆分为字符[{fileChars}]");
    //                            }
    //                        }

    //                        // 清理缓存
    //                        ddLis.RemoveAll(o => o.Rows.Count == 0);
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error($"excel文件异步写入异常！", ex);
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error(ex);
    //            }
    //            return Task.CompletedTask;
    //        }
    //        #endregion

    //        #region 闭环通用方法
    //        #region 毫米数据处理
    //        /// <summary>
    //        /// 毫米数据进行分区处理
    //        /// </summary>
    //        /// <param name="eCCTBClosedType"></param>
    //        /// <param name="data"></param>
    //        /// <returns></returns>
    //        public static float[] PartitionedData(EClosedLoopType eCCTBClosedType, float[] datas)
    //        {
    //            var tBPartitionedConfig = UserDefineVariableInfo.DicVariables["TBPartitionedConfig"].ToString().Split(';');
    //            var partitionedConfig = (tBPartitionedConfig?.Length > 0 && PartitionedDic.ContainsKey(tBPartitionedConfig[0]))
    //                ? PartitionedDic[tBPartitionedConfig[0]] : 1;
    //            var partitionedModelLis = PartitionedModel.GetPartitionedModelLis(partitionedConfig);
    //            var newDatas = new List<float>();
    //            switch (eCCTBClosedType)
    //            {
    //                case EClosedLoopType.MD:
    //                case EClosedLoopType.TD:
    //                case EClosedLoopType.MDTD:
    //                default:
    //                    foreach (var pm in partitionedModelLis)
    //                    {
    //                        // 先复制
    //                        var _datas = datas.Copy().ToList();
    //                        // 数据不足则补0
    //                        if (_datas.Count < pm.EndIndex)
    //                            for (int i = 0; i < (pm.EndIndex - _datas.Count); i++)
    //                                _datas.Add(0);

    //                        // 截取当前膜区起止位置的所有数据
    //                        var pmDatas = _datas.Skip(pm.StartIndex - 1).Take(pm.EndIndex - (pm.StartIndex - 1)).ToList();
    //                        // 计算商和余数 (有效涂膜区/分区个数)
    //                        var quotient = Math.DivRem(pmDatas.Count, pm.PartitionedCount, out int remainder);
    //                        // 有效涂膜区%分区个数 > (1/2)*分区个数
    //                        var coefficient1 = 2 * remainder > pm.PartitionedCount;
    //                        int skipCount = 0, takeCount = 0;
    //                        for (int i = 0; i < pm.PartitionedCount; i++)
    //                        {
    //                            skipCount += takeCount;
    //                            if (remainder == 0)// 如果分配均匀
    //                            {
    //                                takeCount = quotient;
    //                            }
    //                            else
    //                            {
    //                                if (coefficient1)// 如果：有效涂膜区%分区个数 > (1/2)*分区个数
    //                                {
    //                                    var quotient2 = Math.DivRem((pm.PartitionedCount - remainder), 2, out int remainder2);
    //                                    // 是否是余数边缘部分
    //                                    var coefficient2 = i < quotient2 || i >= (quotient2 + remainder);
    //                                    takeCount = quotient + (coefficient2 ? 0 : 1);
    //                                }
    //                                else
    //                                {
    //                                    var quotient2 = Math.DivRem(remainder, 2, out int remainder2);
    //                                    // 是否是余数边缘部分
    //                                    var coefficient2 = i < quotient2 || i >= (quotient2 + (pm.PartitionedCount - remainder));
    //                                    takeCount = quotient + (coefficient2 ? 1 : 0);
    //                                }
    //                            }
    //                            // 获取分区数据均值
    //                            var newData = pmDatas.Skip(skipCount).Take(takeCount).Average();
    //                            newDatas.Add(newData);
    //                        }
    //                        // 膜区后加0用作分隔
    //                        newDatas.Add(0);
    //                    }
    //                    return newDatas.ToArray();
    //            }
    //        }
    //        #endregion

    //        #region 数据判异
    //        /// <summary>
    //        /// 剔除规则 
    //        /// 备注：
    //        ///     1、0数据判异规则；
    //        ///     2、阈值判异规则
    //        /// </summary>
    //        /// <param name="sideConfig"></param>
    //        /// <param name="cCTBModel"></param>
    //        private void RemoveCCTBModelItem<TSideConfig, TCCTB>(TSideConfig sideConfig, ref TCCTB cCTBModel)
    //            where TSideConfig : SideConfig
    //            where TCCTB : ICCTBModel, new()
    //        {
    //            var data = cCTBModel.DataArr.Where(x => x != 0).ToArray();
    //            #region 0数据判异规则
    //            if (data.Count() == 0)// 超规格剔除 去除空值
    //            {
    //                _logger.Info($"{sideConfig.EClosedLoopType}剔除{cCTBModel.MessageTag}架[超规格:0]数据" + string.Join(",", cCTBModel.DataArr));
    //                cCTBModel.Rid_sJudge = "Y";
    //                cCTBModel.Rid_sJudgeDescription = "触发0数据判异规则";
    //                return;
    //            }
    //            #endregion
    //            #region 速度判异规则
    //            if (cCTBModel.Velocity < cCTBModel.Rid_TBVelocityCondition)// 超规格剔除
    //            {
    //                _logger.Info($"{sideConfig.EClosedLoopType}剔除{cCTBModel.MessageTag}架[超规格:走带速度{cCTBModel.Velocity} < {cCTBModel.Rid_TBVelocityCondition}]数据" + string.Join(",", cCTBModel.DataArr));
    //                cCTBModel.Rid_sJudge = "Y";
    //                cCTBModel.Rid_sJudgeDescription = "触发速度判异规则";
    //                return;
    //            }
    //            #endregion
    //            #region 阈值判异规则
    //            var updata = cCTBModel.TValue + Math.Abs(cCTBModel.Rid_aThreshold);
    //            var lowdata = cCTBModel.TValue - Math.Abs(cCTBModel.Rid_aThreshold);
    //            var count = data.Where(x => (x >= updata) || (x <= lowdata)).ToArray().Count();
    //            if (count > cCTBModel.Rid_bCount)//超规格剔除
    //            {
    //                _logger.Info($"{sideConfig.EClosedLoopType}剔除{sideConfig.SideType}面-{cCTBModel.MessageTag}架[超规格:{cCTBModel.TValue}±{Math.Abs(cCTBModel.Rid_aThreshold)}]数据" + string.Join(",", cCTBModel.DataArr));
    //                cCTBModel.Rid_sJudge = "Y";
    //                cCTBModel.Rid_sJudgeDescription = $"触发阈值判异规则[{count} > {cCTBModel.Rid_bCount}]";
    //                return;
    //            }
    //            #endregion
    //            cCTBModel.Rid_sJudge = "N";
    //            cCTBModel.Rid_sJudgeDescription = "未触发判异规则";
    //        }
    //        #endregion

    //        #region 数据预处理
    //        /// <summary>
    //        /// 数据源更新判断
    //        /// 备注：
    //        ///     返回数据源更新之后的数据列表
    //        /// </summary>
    //        /// <returns></returns>
    //        private List<TCCTB> GetCacheAfterReboot<TCCTB>(List<TCCTB> dataLis, TCCTB cCTBModel) where TCCTB : ICCTBModel, new()
    //        {
    //            if (dataLis?.Count <= 0) return new List<TCCTB>();
    //            var lastIndex = dataLis.FindLastIndex(o => o.WeightSource != cCTBModel.WeightSource);
    //            var reCache = dataLis.Skip(lastIndex + 1).ToList();
    //            return GetCacheAfterRebootByZero(reCache, cCTBModel);
    //        }

    //        /// <summary>
    //        /// 开机/重启判断
    //        /// 备注：
    //        ///     返回开机/重启/进刀(若开启)之后的数据列表：剔除开头0数据/固定趟数数据
    //        /// </summary>
    //        /// <returns></returns>
    //        private List<TCCTB> GetCacheAfterRebootByZero<TCCTB>(List<TCCTB> dataLis, TCCTB cCTBModel) where TCCTB : ICCTBModel, new()
    //        {
    //            // 初始化返回数据
    //            var reCache = new List<TCCTB>();
    //            var dt = new List<TCCTB>(dataLis);
    //            dt.Add(cCTBModel);
    //            switch (cCTBModel.BootUp_EBootUpType)
    //            {
    //                case EBootUpType.DataNumber:
    //                    var count = 0;// 0数据连续计数
    //                    for (int i = dt.Count - 1; i >= 0; i--)
    //                    {
    //                        reCache.Insert(0, dt[i]);
    //                        // 0数据判断
    //                        count = (dt[i].DataAvg == 0f) ? (count + 1) : 0;
    //                        if (count >= cCTBModel.BootUp_ZeroCount)
    //                        {
    //                            // 剔除0数据
    //                            reCache.RemoveRange(0, count);
    //                            break;
    //                        }
    //                    }
    //                    break;
    //                case EBootUpType.Time:
    //                    for (int i = dt.Count - 1; i >= 0; i--)
    //                    {
    //                        reCache.Insert(0, dt[i]);
    //                        // 开机时间数据判断
    //                        var lastGatherTime = i - 1 > 0 ? dt[i - 1].GatherTime : DateTime.MinValue;
    //                        var bootTime = (reCache[0].GatherTime - lastGatherTime).TotalSeconds;
    //                        if (bootTime >= cCTBModel.BootUp_BootTime)
    //                            break;
    //                    }
    //                    break;
    //            }

    //            // 进刀(1)闭环开启，退刀(0)闭环关闭
    //            var tBKnifeIndex = reCache.FindLastIndex(o => o.TBKnifeSwitch == 0);
    //            if (cCTBModel.IsTBKnifeSwitchMonitor)
    //                reCache.RemoveRange(0, tBKnifeIndex + 1);

    //            // 开机剔除固定数据量
    //            reCache.RemoveRange(0, Math.Min(reCache.Count, cCTBModel.BootUp_RidCount));
    //            return reCache;
    //        }

    //        /// <summary>
    //        /// 异常数据间隔判断判断
    //        /// 备注：
    //        ///     返回异常数据间隔之后的列表：已剔除开头异常数据
    //        /// </summary>
    //        /// <param name="dataLis"></param>
    //        /// <param name="cCTBModel"></param>
    //        /// <returns></returns>
    //        private List<TCCTB> GetCacheAfterFiltrate<TCCTB>(List<TCCTB> dataLis, TCCTB cCTBModel) where TCCTB : ICCTBModel, new()
    //        {
    //            var reCache = new List<TCCTB>();
    //            var count = 0; // 异常数据连续计数
    //            for (int i = dataLis.Count - 1; i >= 0; i--)
    //            {
    //                reCache.Insert(0, dataLis[i]);
    //                // 异常数据判断
    //                count = (dataLis[i].Rid_sJudge == "Y") ? (count + 1) : 0;
    //                if (count >= cCTBModel.Rid_Interval)
    //                {
    //                    reCache.RemoveRange(0, count);
    //                    break;
    //                }
    //            }
    //            return reCache;
    //        }
    //        #endregion

    //        #region 调机值下发
    //        /// <summary>
    //        /// 单个泵下发
    //        /// 备注:
    //        ///     逻辑：清除完成信号->写入微调值->写入使能信号->写入完成信号
    //        /// </summary>
    //        /// <param name="sideConfig"></param>
    //        /// <param name="adjustValueBase"></param>
    //        /// <param name="switchAddress"></param>
    //        /// <param name="plcAddressConfigMD"></param>
    //        /// <returns></returns>
    //        private float SetNewBCommon<TSideConfig>(TSideConfig sideConfig, float adjustValueBase, PLCAddressConfigMDPump plcAddressConfigMD, bool isAdjust = true) where TSideConfig : SideConfig
    //        {
    //            if (adjustValueBase == 0) return 0;
    //            _logger.Info($"{sideConfig.EClosedLoopType}[{plcAddressConfigMD.PumpTag}]新泵速下发PLC！", false);

    //            var adjust = plcAddressConfigMD?.Adjust;
    //            var adjusst = plcAddressConfigMD?.Adjust;
    //            var signalReady = plcAddressConfigMD?.SignalReady;
    //            var signalConfirm = plcAddressConfigMD?.SignalConfirm;
    //            var speedValue = plcAddressConfigMD?.SetPumpSpeedValue;

    //            // 清除完成信号
    //            if (signalConfirm?.AddresObj != null)
    //            {
    //                var signalConfirmResult = signalConfirm.WriteValue(0);
    //                _logger.Info($"[{signalConfirm.AddressName}]-[{signalConfirm.AddressDescribe}]写入完成清零:{signalConfirmResult.IsSuccess}");
    //                Thread.Sleep(10);
    //            }

    //            // 记载弹窗配置
    //            ushort WidthPopup = 400, HeightPopup = 200;
    //            bool GreenSwitch = true, YellowSwitch = true, RedSwitch = true;
    //            var tBPopupConfig = UserDefineVariableInfo.DicVariables["TBPopupConfig"].ToString().Split(',');
    //            if (tBPopupConfig.Length == 5 && ushort.TryParse(tBPopupConfig[0], out ushort width) && ushort.TryParse(tBPopupConfig[1], out ushort height)
    //                && bool.TryParse(tBPopupConfig[2], out bool greenSwitch) && bool.TryParse(tBPopupConfig[3], out bool yellowSwitch) && bool.TryParse(tBPopupConfig[4], out bool redSwitch))
    //            {
    //                WidthPopup = width;
    //                HeightPopup = height;
    //                GreenSwitch = greenSwitch;
    //                YellowSwitch = yellowSwitch;
    //                RedSwitch = redSwitch;
    //            }

    //            OperateResult adjustResult = null;
    //            OperateResult signalReadyResult = null;

    //            var adjustValue = 0f;
    //            // PLC开关开启则写入PLC地址数据
    //            if (isAdjust && adjust?.AddresObj != null && signalReady?.AddresObj != null)
    //            {
    //                adjustValue = adjustValueBase;
    //                var signalConfirmValue = 1;

    //                adjustResult = adjust.WriteValue(adjustValue.ToString("0.00"));
    //                _logger.Info($"[{adjust.AddressName}]-[{adjust.AddressDescribe}]写入值{adjustValue:0.00},下发结果:{adjustResult.IsSuccess}");
    //                Thread.Sleep(10);
    //                signalReadyResult = signalReady.WriteValue(signalConfirmValue);
    //                _logger.Info($"[{signalReady.AddressName}]-[{signalReady.AddressDescribe}]更新标识[{signalConfirmValue}]:{signalReadyResult.IsSuccess}");

    //                if ((adjustResult?.IsSuccess ?? false) && (signalReadyResult?.IsSuccess ?? false))
    //                {
    //                    var msg = $"[{sideConfig.SideType}]面[{plcAddressConfigMD.PumpTag}]泵速微调写入值[{adjustValueBase.ToString("0.00")}]成功";
    //                    _logger.Info(msg);
    //                    if (GreenSwitch) ShowGrowl(InfoType.Info, msg);
    //                }
    //                else
    //                {
    //                    var msg = $"[{sideConfig.SideType}]面[{plcAddressConfigMD.PumpTag}]泵速微调写入值[{adjustValueBase.ToString("0.00")}]失败";
    //                    _logger.Error(msg);
    //                    adjustValue = 0;
    //                    if (RedSwitch) ShowGrowl(InfoType.Error, msg);
    //                }
    //            }
    //            else// PLC开关未开启则清除PLC地址数据
    //            {
    //                adjustValue = 0f;
    //                var msg = $"推荐[{sideConfig.SideType}]面[{plcAddressConfigMD.PumpTag}]泵速微调写入值[{adjustValueBase.ToString("0.00")}]";
    //                _logger.Info(msg);
    //                if (YellowSwitch) ShowGrowl(InfoType.Warning, msg);
    //            }
    //            return adjustValue;
    //        }

    //        /// <summary>
    //        /// 多个泵下发
    //        /// 备注:
    //        ///     下发至上位机
    //        /// </summary>
    //        /// <param name="sideConfig"></param>
    //        /// <param name="newB"></param>
    //        /// <returns></returns>
    //        private bool SetNewBCommon<TSideConfig>(TSideConfig sideConfig, Dictionary<string, float> newB, bool isAdjust = true) where TSideConfig : SideConfig
    //        {
    //            _logger.Info($"{sideConfig.EClosedLoopType}[{sideConfig.SideType}]新泵速下发！", false);

    //            // 记载弹窗配置
    //            ushort WidthPopup = 400, HeightPopup = 200;
    //            bool GreenSwitch = true, YellowSwitch = true, RedSwitch = true;
    //            var tBPopupConfig = UserDefineVariableInfo.DicVariables["TBPopupConfig"].ToString().Split(',');
    //            if (tBPopupConfig.Length == 5 && ushort.TryParse(tBPopupConfig[0], out ushort width) && ushort.TryParse(tBPopupConfig[1], out ushort height)
    //                && bool.TryParse(tBPopupConfig[2], out bool greenSwitch) && bool.TryParse(tBPopupConfig[3], out bool yellowSwitch) && bool.TryParse(tBPopupConfig[4], out bool redSwitch))
    //            {
    //                WidthPopup = width;
    //                HeightPopup = height;
    //                GreenSwitch = greenSwitch;
    //                YellowSwitch = yellowSwitch;
    //                RedSwitch = redSwitch;
    //            }

    //            var mAdjustRequestData = new MAdjustRequestData()
    //            {
    //                AdjustTime = DateTime.Now,
    //                AdjustPumpLis = newB.ToList().ConvertAll<AdjustPump>(o => new AdjustPump() { PumpTag = o.Key, AdjustValue = o.Value }),
    //            };
    //            #region 
    //            if (!isAdjust)
    //            {
    //                var msg = $"推荐{sideConfig.EClosedLoopType}[{sideConfig.SideType}]面下发MD调节值[{string.Join(",", newB.Select(o => $"{o.Key}:{o.Value}"))}])";
    //                _logger.Info(msg);
    //                if (YellowSwitch) ShowGrowl(InfoType.Warning, msg);
    //                return false;
    //            }

    //            // MD调机下发
    //            var reLis = ClientSendAndRecieveData<MAdjustRequestData>($"涂布模头{sideConfig.SideType}", 1, "MADJUSTREQUEST", mAdjustRequestData, 2000);
    //            _logger.Info($"[{sideConfig.SideType}]下发MD调机值[{JsonConvert.SerializeObject(reLis)}]");
    //            if (!(reLis != null && reLis.Count > 0 && reLis[0] is MAdjustReponseData))
    //            {
    //                var msg = $"{sideConfig.EClosedLoopType}[{sideConfig.SideType}]面下发MD调节值异常(接收返回值超时)";
    //                _logger.Error(msg);
    //                if (RedSwitch) ShowGrowl(InfoType.Error, msg);
    //                return false;
    //            }

    //            var re = (MAdjustReponseData)reLis[0];
    //            if (re.Code != 0)
    //            {
    //                var msg = $"{sideConfig.EClosedLoopType}[{sideConfig.SideType}]面下发MD调节值失败({re.Code}-{re.Message})";
    //                _logger.Error(msg);
    //                if (RedSwitch) ShowGrowl(InfoType.Error, msg);
    //                return false;
    //            }

    //            var msg1 = $"{sideConfig.EClosedLoopType}[{sideConfig.SideType}]面下发MD调节值成功({re.Code}-{re.Message})";
    //            _logger.Info(msg1);
    //            if (GreenSwitch) ShowGrowl(InfoType.Info, msg1);
    //            return true;
    //            #endregion
    //        }

    //        /// <summary>
    //        /// 下发T块调机值
    //        /// </summary>
    //        /// <param name="sideConfig">机架号</param>
    //        /// <param name="cCTBModel"></param>
    //        private void SetNewTCommon<TSideConfig>(TSideConfig sideConfig, int dataNumber, double dataAvg, TInfoReponseData tInfo, Dictionary<string, int> newT, bool isAdjust = true) where TSideConfig : SideConfig
    //        {
    //            _logger.Info($"{sideConfig.EClosedLoopType}[{sideConfig.SideType}]下发T块调机值！", false);

    //            // 记载弹窗配置
    //            ushort WidthPopup = 400, HeightPopup = 200;
    //            bool GreenSwitch = true, YellowSwitch = true, RedSwitch = true;
    //            var tBPopupConfig = UserDefineVariableInfo.DicVariables["TBPopupConfig"].ToString().Split(',');
    //            if (tBPopupConfig.Length == 5 && ushort.TryParse(tBPopupConfig[0], out ushort width) && ushort.TryParse(tBPopupConfig[1], out ushort height)
    //                && bool.TryParse(tBPopupConfig[2], out bool greenSwitch) && bool.TryParse(tBPopupConfig[3], out bool yellowSwitch) && bool.TryParse(tBPopupConfig[4], out bool redSwitch))
    //            {
    //                WidthPopup = width;
    //                HeightPopup = height;
    //                GreenSwitch = greenSwitch;
    //                YellowSwitch = yellowSwitch;
    //                RedSwitch = redSwitch;
    //            }

    //            var tAdjustRequestData = new TAdjustRequestData()
    //            {
    //                AdjustTime = DateTime.Now,
    //                AdjustTaskLis = new List<AdjustTask>(),
    //            };
    //            foreach (var TBlockInfo in tInfo.TBlockInfoLis)
    //            {
    //                var tAdjustValue = 0;
    //                if (newT.ContainsKey($"T块{TBlockInfo.TId}调节量"))
    //                    tAdjustValue = newT[$"T块{TBlockInfo.TId}调节量"];
    //                var adjustTask = new AdjustTask()
    //                {
    //                    TId = TBlockInfo.TId,
    //                    TPosition = TBlockInfo.TPosition,
    //                    TAdjustValue = tAdjustValue,
    //                    ScanNum = dataNumber,
    //                    TriggerEpkValue = dataAvg
    //                };
    //                tAdjustRequestData.AdjustTaskLis.Add(adjustTask);
    //            }

    //            #region 欣旺达闭环
    //            // T块下发仅需满足：SC 上位机开关标识为 2/4    --By 2210120078  20251202
    //            if (!((tInfo?.TAdjustMode == 2 || tInfo?.TAdjustMode == 4) && isAdjust))
    //            {
    //                var msg = $"推荐{sideConfig.EClosedLoopType}[{sideConfig.SideType}]面T块调节值";
    //                _logger.Info(msg);
    //                if (YellowSwitch) ShowGrowl(InfoType.Warning, msg);
    //                return;
    //            }

    //            // T块调机下发
    //            var reLis = ClientSendAndRecieveData<TAdjustRequestData>($"涂布模头{sideConfig.SideType}", 1, "TADJUSTREQUEST", tAdjustRequestData, 2000);
    //            _logger.Info($"[{sideConfig.SideType}]下发T块调机值[{JsonConvert.SerializeObject(reLis)}]");
    //            if (!(reLis != null && reLis.Count > 0 && reLis[0] is TAdjustReponseData))
    //            {
    //                var msg = $"{sideConfig.EClosedLoopType}[{sideConfig.SideType}]面下发T块调节值异常(接收返回值超时)";
    //                _logger.Error(msg);
    //                if (RedSwitch) ShowGrowl(InfoType.Error, msg);
    //                return;
    //            }

    //            var re = (TAdjustReponseData)reLis[0];
    //            if (re.Code != 0)
    //            {
    //                var msg = $"{sideConfig.EClosedLoopType}[{sideConfig.SideType}]面下发T块调节值失败({re.Code}-{re.Message})";
    //                _logger.Info(msg);
    //                if (RedSwitch) ShowGrowl(InfoType.Error, msg);
    //                return;
    //            }

    //            var msg1 = $"{sideConfig.EClosedLoopType}[{sideConfig.SideType}]面下发T块调节值成功({re.Code}-{re.Message})";
    //            _logger.Info(msg1);
    //            if (GreenSwitch) ShowGrowl(InfoType.Info, msg1);
    //            #endregion
    //        }
    //        #endregion

    //        /// <summary>
    //        /// 输出表格文件
    //        /// </summary>
    //        private void SaveToExcel<TSide, TCCTB>(TSide sideConfig, TCCTB cCTBModel)
    //            where TSide : SideConfig
    //            where TCCTB : ICCTBModel, new()
    //        {
    //            _logger.Info($"{sideConfig.EClosedLoopType}数据保存excel缓存预处理！", false);
    //            try
    //            {
    //                var dicDep = new Dictionary<string, string>();
    //                if (sideConfig is SideConfigMD scMD && cCTBModel is CCTBMDModel ccMD)
    //                    dicDep = UpdateMD(scMD, ccMD) ?? new Dictionary<string, string>();
    //                else if (sideConfig is SideConfigTD scTD && cCTBModel is CCTBTDModel ccTD)
    //                    dicDep = UpdateTD(scTD, ccTD) ?? new Dictionary<string, string>();
    //                else if (sideConfig is SideConfigMDTD scMDTD && cCTBModel is CCTBMDTDModel ccMDTD)
    //                    dicDep = UpdateMDTD(scMDTD, ccMDTD) ?? new Dictionary<string, string>();

    //                var filePath = UserDefineVariableInfo.DicVariables["productionDataSavePath1"].ToString() + $"\\涂布{sideConfig.EClosedLoopType}数据\\" + DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    //                GetFilmSn(out string filmSn);
    //                var fullPath = SanitizeFullPath(filePath, filmSn + $"_{sideConfig.EClosedLoopType}-{sideConfig.SideType}面.csv");

    //                var excelData = Tuple.Create(fullPath, $"{sideConfig.EClosedLoopType}数据", dicDep);
    //                ExcelDataEnqueue(excelData);
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"没有生成{sideConfig.EClosedLoopType}-{sideConfig.SideType}:{ex}", type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //        }

    //        #region PythonNet
    //        /// <summary>
    //        /// ExcutePythonAction的ManualResetEvent
    //        /// </summary>
    //        private Dictionary<string, ManualResetEvent> PyManualResetEventDic = new Dictionary<string, ManualResetEvent>();
    //        /// <summary>
    //        /// ExcutePythonAction算法结果全局变量
    //        /// </summary>
    //        private Dictionary<string, object> PyResultDic = new Dictionary<string, object>();
    //        #endregion
    //        #endregion

    //        #region 一人多机PLC采集
    //        private void OPMMGatherPLCData_Thread()
    //        {
    //            var t = new Thread(() =>
    //            {
    //                _logger.Info("初始化一人多机PLC数据采集任务！");
    //                var tBOPMMGatherPlcDataSwitch = false;
    //                var tBOPMMGatherPlcDataCycleTime = 1000;
    //                var clientName = "一人多机PLC";
    //                while (true)
    //                {
    //                    try
    //                    {
    //                        tBOPMMGatherPlcDataSwitch = UserDefineVariableInfo.DicVariables["TBOPMMGatherPlcDataSwitch"].ToString() == "1";
    //                        tBOPMMGatherPlcDataCycleTime = int.TryParse(UserDefineVariableInfo.DicVariables["TBOPMMGatherPlcDataCycleTime"].ToString(), out int cycleTime) ? cycleTime : 1000;
    //                        if (!tBOPMMGatherPlcDataSwitch) continue;

    //                        var plc_configeModel = new Model.plc_config();
    //                        var plc_configBll = new BLL.plc_config();
    //                        // enable=1 为plc通讯
    //                        var plc_configlist = plc_configBll.GetModelList("enabled = 1") ?? new List<Model.plc_config>();
    //                        var plcAddresses = UserDefineVariableInfo.lstUserDefineVariables.Where(o => o.Remark == "OPMM" && o.VariableType == "PLC变量").ToList();
    //                        var cCTBOPMMQUEST = new CCTBOPMMQUEST();
    //                        cCTBOPMMQUEST.MsgType = "data";
    //                        cCTBOPMMQUEST.Sid = UserDefineVariableInfo.DicVariables["workship"].ToString().Split(':')[2];
    //                        cCTBOPMMQUEST.Z = "";
    //                        cCTBOPMMQUEST.Ts = GetTimeStampMilliseconds();
    //                        var opmmPLCData = new OPMMPLCData();
    //                        opmmPLCData.LineName = UserDefineVariableInfo.DicVariables["workship"].ToString().Split(':')[2];
    //                        opmmPLCData.IP = plc_configlist.FirstOrDefault().address;
    //                        opmmPLCData.DeviceSn = UserDefineVariableInfo.DicVariables["DeviceSn"].ToString();
    //                        opmmPLCData.PLCParameterModelList = plcAddresses.Select(o => new PLCParameterModel()
    //                        {
    //                            PName = o.VariableName,
    //                            PType = o.ValueType,
    //                            PValue = UserDefineVariableInfo.DicVariables[o.VariableName].ToString()
    //                        }).ToList();
    //                        cCTBOPMMQUEST.Data = opmmPLCData;
    //                        cCTBOPMMQUEST.Sig = "";

    //                        var client = GetSocketClient(clientName);
    //                        if (client == null || !client.IsAlive())
    //                        {
    //                            throw new Exception($"[{clientName} {client?.ClientIP?.ToString()}]设备未连接！");
    //                        }

    //                        var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(cCTBOPMMQUEST));
    //                        var header = new byte[4];
    //                        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)payload.Length);
    //                        client.Send(header.Concat(payload).ToArray());

    //                        Thread.Sleep(Math.Max(1000, tBOPMMGatherPlcDataCycleTime));
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error("一人多机PLC数据采集发送异常!", ex);
    //                        Thread.Sleep(5000);
    //                    }
    //                }
    //            });
    //            t.IsBackground = true;
    //            t.Start();
    //        }
    //        #endregion

    //        #region PLC数据采集
    //        /// <summary>
    //        /// 初始化异步采集PLC地址数据
    //        /// </summary>
    //        private void GatherPlcData_Thread()
    //        {
    //            var t = new Thread(() =>
    //            {
    //                _logger.Info("初始化PLC数据异步导出！");
    //                var gatherPlcDataSwitch = false;
    //                var filePath = UserDefineVariableInfo.DicVariables["productionDataSavePath1"].ToString() + "\\PLC地址数据\\";
    //                while (true)
    //                {
    //                    try
    //                    {
    //                        gatherPlcDataSwitch = UserDefineVariableInfo.DicVariables["GatherPlcDataSwitch"].ToString() == "1";
    //                        if (!gatherPlcDataSwitch) continue;

    //                        var dateTime = DateTime.Now;
    //                        var _filePath = filePath + dateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    //                        var fileName = dateTime.ToString("HH", System.Globalization.CultureInfo.InvariantCulture) + ".xlsx";
    //                        var depDic = new Dictionary<string, string>();
    //                        depDic.Add("数据分割时间点", dateTime.ToString("yyyy-MM-dd HH:mm:ss:fff", System.Globalization.CultureInfo.InvariantCulture));
    //                        var udcDic = GetUDCParam("plc");
    //                        foreach (var udcKv in udcDic) depDic.Add(udcKv.Key, udcKv.Value);

    //                        var excelData = Tuple.Create($"{_filePath}\\{fileName}", "PLC", depDic);
    //                        ExcelDataEnqueue(excelData);
    //                        Thread.Sleep(1000);
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error("PLC数据异步导出异常!", ex);
    //                        Thread.Sleep(5000);
    //                    }
    //                }
    //            });
    //            t.IsBackground = true;
    //            t.Start();
    //        }
    //        #endregion

    //        #region 进退刀监控
    //        /// <summary>
    //        /// 监控检测触发
    //        /// </summary>
    //        private ValueListener TBKnifeSwitchMonitor_A, TBKnifeSwitchMonitor_B;
    //        /// <summary>
    //        /// 进退刀监控触发
    //        /// </summary>
    //        /// <param name="sender"></param>
    //        /// <param name="e"></param>
    //        private void OnTBKnifeSwitchMonitorChangedEvent(object sender, EventArgs e)
    //        {
    //            try
    //            {
    //                if (sender is ValueListener v)
    //                {
    //                    if (v.Value == "1")// 进刀
    //                    {
    //                        var msg = $"{v.description}进刀状态，涂布MD/TD闭环已开启！";
    //                        _logger.Info(msg);
    //                        ShowGrowl(InfoType.Info, msg);
    //                    }
    //                    else if (v.Value == "0")// 退刀
    //                    {
    //                        var msg = $"{v.description}退刀状态，涂布MD/TD闭环已关闭！";
    //                        _logger.Warn(msg);
    //                        ShowGrowl(InfoType.Warning, msg);
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error(ex);
    //            }
    //        }
    //        /// <summary>
    //        /// 进退刀监控
    //        /// 备注：
    //        ///     进刀为开机，开启闭环
    //        ///     推刀为关机，关闭闭环
    //        /// </summary>
    //        private void TBKnifeSwitchMonitor_Thread()
    //        {
    //            _logger.Info("进退刀监控初始化！");
    //            TBKnifeSwitchMonitor_A = new ValueListener() { Value = null, description = "A面" };
    //            TBKnifeSwitchMonitor_A.OnValueChanged += OnTBKnifeSwitchMonitorChangedEvent;
    //            TBKnifeSwitchMonitor_B = new ValueListener() { Value = null, description = "B面" };
    //            TBKnifeSwitchMonitor_B.OnValueChanged += OnTBKnifeSwitchMonitorChangedEvent;
    //            var t = new Thread(() =>
    //            {
    //                var isTBKnifeSwitchMonitor = false;
    //                while (true)
    //                {
    //                    try
    //                    {
    //                        isTBKnifeSwitchMonitor = UserDefineVariableInfo.DicVariables["IsTBKnifeSwitchMonitor"].ToString() == "1";
    //                        if (isTBKnifeSwitchMonitor)
    //                        {
    //                            if (!(CCTBConfig.TBKnifeSwitchA?.ReadValue(out string tBKnifeSwitchA) ?? false))
    //                                throw new Exception($"[{CCTBConfig.TBKnifeSwitchA?.AddressDescribe}]读取数据异常，请检查！");
    //                            TBKnifeSwitchMonitor_A.Value = tBKnifeSwitchA;

    //                            if (!(CCTBConfig.TBKnifeSwitchB?.ReadValue(out string tBKnifeSwitchB) ?? false))
    //                                throw new Exception($"[{CCTBConfig.TBKnifeSwitchB?.AddressDescribe}]读取数据异常，请检查！");
    //                            TBKnifeSwitchMonitor_B.Value = tBKnifeSwitchB;
    //                        }
    //                        Thread.Sleep(1000);
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error("进退刀信号异步监控异常!", ex);
    //                        Thread.Sleep(30000);
    //                    }
    //                }
    //            });
    //            t.IsBackground = true;
    //            t.Start();
    //        }
    //        #endregion

    //        #region FTP上传文件
    //        private System.Timers.Timer ftpSaveTimer;
    //        /// <summary>
    //        /// 初始化FTP上传：每天中午12点上传，开机上传一次
    //        /// </summary>
    //        private async Task InitializeFtpSaveTimerAsync()
    //        {
    //            // 若开启则执行一次：上传前一天的数据
    //            var isTBFtpUploadMonitor = _projectSettingRepository.GetByNameAndVM("IsTBFtpUploadMonitor", CCTBCore.BelongToVM)?.Value.ToBool() ?? false;
    //            if (isTBFtpUploadMonitor) SaveToFtp(DateTime.Now.AddDays(-1));

    //            // 计算明天的中午 11 点日期
    //            var tomorrow = DateTime.Now.Date.AddDays(1);
    //            var tomorrowNoon = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, 11, 0, 0);
    //            var timeDifference = tomorrowNoon - DateTime.Now;
    //            await Task.Delay(timeDifference);
    //            ftpSaveTimer = new System.Timers.Timer();
    //            ftpSaveTimer.AutoReset = true;                  // 循环执行
    //            ftpSaveTimer.Interval = 24 * 60 * 60 * 1000;    // 24小时触发一次+
    //            ftpSaveTimer.Elapsed += (sender, e) =>
    //            {
    //                var isTBFtpUploadMonitor = _projectSettingRepository.GetByNameAndVM("IsTBFtpUploadMonitor", CCTBCore.BelongToVM)?.Value.ToBool() ?? false;
    //                if (!isTBFtpUploadMonitor) return;
    //                // 上传前一天的数据
    //                SaveToFtp(DateTime.Now.AddDays(-1));
    //            };
    //            ftpSaveTimer.Start(); // 启动定时器
    //        }

    //        /// <summary>
    //        /// 上传ftp
    //        /// </summary>
    //        private void SaveToFtp(DateTime dateTime)
    //        {
    //            try
    //            {
    //                using var burProgress = new BlurProgressWindow();
    //                var tBFtpUploadConfig = UserDefineVariableInfo.DicVariables["TBFtpUploadConfig"].ToString().Split(',');
    //                if (!(tBFtpUploadConfig.Length == 4
    //                    && !string.IsNullOrEmpty(tBFtpUploadConfig[0]) && !string.IsNullOrEmpty(tBFtpUploadConfig[1])
    //                    && !string.IsNullOrEmpty(tBFtpUploadConfig[2]) && !string.IsNullOrEmpty(tBFtpUploadConfig[3])))
    //                {
    //                    _logger.Error($"FTP配置信息异常！", type: eType, projectName: eProjectName, level: eLevel);
    //                    return;
    //                }

    //                var url = tBFtpUploadConfig[0];
    //                var user = tBFtpUploadConfig[1];
    //                var passWord = tBFtpUploadConfig[2];
    //                var isUseSSL = tBFtpUploadConfig[3] == "1";
    //                var ftpHelper = new Sunwoda_FtpHelper(user, passWord, url, isUseSSL);
    //                Sunwoda_FtpHelper.Speed = "16Mbps";

    //                var localFilePath = UserDefineVariableInfo.DicVariables["productionDataSavePath1"].ToString();
    //                var workShip = UserDefineVariableInfo.DicVariables["workship"].ToString().Split(':');
    //                var deviceSn = UserDefineVariableInfo.DicVariables["DeviceSn"].ToString();
    //                var deviceName = UserDefineVariableInfo.DicVariables["DeviceName"].ToString();
    //                var ftpDirLis = new List<string>();
    //                ftpDirLis.Add("涂布");
    //                ftpDirLis.AddRange(workShip);
    //                ftpDirLis.Add($"{deviceName}({deviceSn})");
    //                var ftpFilePathHead = Path.Combine(ftpDirLis.ToArray());

    //                // 获取本地所有文件
    //                if (!Directory.Exists(localFilePath)) return;
    //                var dirArr = Directory.GetDirectories(localFilePath, "*", SearchOption.TopDirectoryOnly);
    //                dirArr = dirArr.ToList().ConvertAll(o => Path.Combine(o, dateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))).ToArray();
    //                var fileKvLis = new List<KeyValuePair<string, List<string>>>();
    //                foreach (var dir in dirArr)
    //                {
    //                    if (!Directory.Exists(dir)) continue;
    //                    var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
    //                    fileKvLis.AddRange(files.GroupBy(o => Path.GetDirectoryName(o)).ToDictionary(group => group.Key, group => group.ToList()).ToList());
    //                }

    //                var sw = new Stopwatch();
    //                var startTime = DateTime.Now;
    //                sw.Start();
    //                int okCount = 0, ngCount = 0, skipCount = 0;
    //                burProgress.Show();
    //                foreach (var fileKV in fileKvLis)
    //                {
    //                    // 生成ftp路径
    //                    var secondBackslashIndex = fileKV.Key.IndexOf('\\') + 1;
    //                    var relativePath = fileKV.Key.Substring(secondBackslashIndex);
    //                    var ftpFilePath = GetOkPath(Path.Combine(ftpFilePathHead, relativePath), ' ', '#');
    //                    if (ftpHelper.MakeDir(ftpFilePath))
    //                    {
    //                        // 获取FTP指定路径内所有文件名称
    //                        var ftpFilelis = ftpHelper.GetAllList(ftpFilePath, isUseSSL).ToList();
    //                        foreach (var file in fileKV.Value)
    //                        {
    //                            try
    //                            {
    //                                // 存在则跳过
    //                                if (ftpFilelis?.Exists(o => o.EndsWith(Path.GetFileName(file))) ?? false)
    //                                {
    //                                    skipCount++;
    //                                    continue;
    //                                }
    //                                var re = ftpHelper.FtpFileUpload(file, ftpFilePath, isUseSSL);
    //                                okCount++;
    //                            }
    //                            catch (Exception ex)
    //                            {
    //                                ngCount++;
    //                            }
    //                            finally
    //                            {
    //                                var finishCount = okCount + ngCount + skipCount;
    //                                var totalCount = fileKvLis.SelectMany(o => o.Value).Count();
    //                                var progress = (double)finishCount / (double)totalCount * 100;
    //                                burProgress.UpdateProgress(progress, $"{finishCount}/{totalCount}个文件正在上传FTP...", $"成功:{okCount} 跳过:{skipCount} 失败:{ngCount}");
    //                            }
    //                        }
    //                    }
    //                }
    //                sw.Stop();
    //                _logger.Info($"上传ftp成功[OK:{okCount} NG:{ngCount} Skip:{skipCount}]-开始时间[{startTime.ToString("HH:mm:ss fff")}] 耗时[{sw.ElapsedMilliseconds}]ms");
    //                // 延迟退出
    //                Thread.Sleep(1000);
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"涂布文件上传FTP异常！", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //        }

    //        /// <summary>//From:www.13le.com
    //        /// C#快速剔除路径字符串中不合法的字符
    //        /// </summary>
    //        /// <param name="filePath"></param>
    //        /// <returns></returns>
    //        public string GetOkPath(string filePath, char replaceChar, params char[] chars)
    //        {
    //            var rBuilder = new StringBuilder(filePath);
    //            var rInvalidChars = Path.GetInvalidPathChars().ToList();
    //            rInvalidChars.AddRange(chars);
    //            foreach (var rInvalidChar in rInvalidChars)
    //                rBuilder.Replace(rInvalidChar.ToString(), replaceChar.ToString());
    //            return rBuilder.ToString().Trim();
    //        }
    //        #endregion

    //        #region 品种信息验证
    //        /// <summary>
    //        /// 产品品种验证
    //        /// </summary>
    //        private (bool isSuccess, string message) VarietySelectedVerity(string varietyName)
    //        {
    //            var message = "";
    //            try
    //            {
    //                // A面专家PID算法验证
    //                if (!CheckTBPydConfig(EArithmeticType.专家PID, CCTBConfig.BasicConfiguration_MD.SideConfigA, varietyName, out message))
    //                {
    //                    message = $"品种[{varietyName}]失败：[{message}";
    //                    return (false, message);
    //                }
    //                // B面专家PID算法验证
    //                if (!CheckTBPydConfig(EArithmeticType.专家PID, CCTBConfig.BasicConfiguration_MD.SideConfigB, varietyName, out message))
    //                {
    //                    message = $"品种[{varietyName}]失败：[{message}";
    //                    return (false, message);
    //                }
    //                // A面流量PID算法验证
    //                if (!CheckTBPydConfig(EArithmeticType.流量PID, CCTBConfig.BasicConfiguration_MD.SideConfigA, varietyName, out message))
    //                {
    //                    message = $"品种[{varietyName}]失败：[{message}";
    //                    return (false, message);
    //                }
    //                // B面流量PID算法验证
    //                if (!CheckTBPydConfig(EArithmeticType.流量PID, CCTBConfig.BasicConfiguration_MD.SideConfigB, varietyName, out message))
    //                {
    //                    message = $"品种[{varietyName}]失败：[{message}";
    //                    return (false, message);
    //                }
    //                // A面TDControl算法验证
    //                if (!CheckTBPydConfig(EArithmeticType.TDControl, CCTBConfig.BasicConfiguration_TD.SideConfigA, varietyName, out message))
    //                {
    //                    message = $"品种[{varietyName}]失败：[{message}";
    //                    return (false, message);
    //                }
    //                // B面TDControl算法验证
    //                if (!CheckTBPydConfig(EArithmeticType.TDControl, CCTBConfig.BasicConfiguration_TD.SideConfigB, varietyName, out message))
    //                {
    //                    message = $"品种[{varietyName}]失败：[{message}";
    //                    return (false, message);
    //                }
    //                var pIDObtainA = CCTBConfig.BasicConfiguration_MDTD.SideConfigA.PIDObtain;
    //                if (pIDObtainA?.IsEnable_PID ?? false)
    //                {
    //                    try
    //                    {
    //                        var controlType = pIDObtainA?.PythonCode?.Split('_')[1];
    //                        switch (controlType)
    //                        {
    //                            case "MDTD":
    //                                // A面MDTDControl算法验证
    //                                if (!CheckTBPydConfig(EArithmeticType.MDTDControl, CCTBConfig.BasicConfiguration_MDTD.SideConfigA, varietyName, out message))
    //                                {
    //                                    message = $"品种[{varietyName}]失败：[{message}";
    //                                    return (false, message);
    //                                }
    //                                break;
    //                            case "MULTIMDTD":
    //                                // A面MULTIMDTDControl算法验证
    //                                if (!CheckTBPydConfig(EArithmeticType.MULTIMDTDControl, CCTBConfig.BasicConfiguration_MDTD.SideConfigA, varietyName, out message))
    //                                {
    //                                    message = $"品种[{varietyName}]失败：[{message}";
    //                                    return (false, message);
    //                                }
    //                                break;
    //                            default:
    //                                message = $"未实现[{controlType}]类别算法！";
    //                                return (false, message);
    //                        }
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        throw new Exception($"算法名称[{pIDObtainA?.PythonCode}]配置异常:[{ex.Message}]");
    //                    }
    //                }
    //                var pIDObtainB = CCTBConfig.BasicConfiguration_MDTD.SideConfigB.PIDObtain;
    //                if (pIDObtainB?.IsEnable_PID ?? false)
    //                {
    //                    try
    //                    {
    //                        var controlType = pIDObtainB?.PythonCode?.Split('_')[1];
    //                        switch (controlType)
    //                        {
    //                            case "MDTD":
    //                                // B面MDTDControl算法验证
    //                                if (!CheckTBPydConfig(EArithmeticType.MDTDControl, CCTBConfig.BasicConfiguration_MDTD.SideConfigB, varietyName, out message))
    //                                {
    //                                    message = $"品种[{varietyName}]失败：[{message}";
    //                                    return (false, message);
    //                                }
    //                                break;
    //                            case "MULTIMDTD":
    //                                // B面MULTIMDTDControl算法验证
    //                                if (!CheckTBPydConfig(EArithmeticType.MULTIMDTDControl, CCTBConfig.BasicConfiguration_MDTD.SideConfigB, varietyName, out message))
    //                                {
    //                                    message = $"品种[{varietyName}]失败：[{message}";
    //                                    return (false, message);
    //                                }
    //                                break;
    //                            default:
    //                                message = $"未实现[{controlType}]类别算法！";
    //                                return (false, message);
    //                        }
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        throw new Exception($"算法名称[{pIDObtainB?.PythonCode}]配置异常:[{ex.Message}]");
    //                    }
    //                }
    //                message = $"品种[{varietyName}]验证成功：[{message}]";
    //                return (true, message);
    //            }
    //            catch (Exception ex)
    //            {
    //                message = $"产品品种[{varietyName}]验证异常[{ex}]！";
    //                return (false, message);
    //            }
    //        }
    //        #endregion

    //        /// <summary>
    //        /// 流量数据缓存
    //        /// </summary>
    //        private Dictionary<ESideType, Dictionary<EPump, Queue<Tuple<DateTime, double>>>> FluCache = new Dictionary<ESideType, Dictionary<EPump, Queue<Tuple<DateTime, double>>>>();
    //        /// <summary>
    //        /// 流量数据获取
    //        /// </summary>
    //        private void GatherFlueData_Thread()
    //        {
    //            // 流量数据地址字典
    //            var addressDic = new Dictionary<ESideType, Dictionary<EPump, string>>()
    //            {
    //                { ESideType.A,new Dictionary<EPump, string>()
    //                {
    //                    { EPump.泵0,"机头A流量上层"},
    //                    { EPump.泵1,"机头A流量下层"}
    //                } },
    //                { ESideType.B,new Dictionary<EPump, string>()
    //                {
    //                    { EPump.泵0,"机头B流量上层"},
    //                    { EPump.泵1,"机头B流量下层"}
    //                } },
    //            };
    //            // 异步采集
    //            var t = new Thread(() =>
    //            {
    //                while (true)
    //                {
    //                    try
    //                    {
    //                        // 遍历AB面
    //                        foreach (var sideTyprKv in addressDic)
    //                        {
    //                            // 遍历上下泵
    //                            foreach (var pumpKv in sideTyprKv.Value)
    //                            {
    //                                var address = UserDefineVariableInfo.lstUserDefineVariables.Where(x => x.VariableName == pumpKv.Value).FirstOrDefault();
    //                                if (address != null)
    //                                {
    //                                    var fluStr = address.DictValue;
    //                                    if (double.TryParse(fluStr, out double flu))
    //                                    {
    //                                        lock (FluCache)
    //                                        {
    //                                            if (!FluCache.ContainsKey(sideTyprKv.Key))
    //                                                FluCache[sideTyprKv.Key] = new Dictionary<EPump, Queue<Tuple<DateTime, double>>>();

    //                                            if (!FluCache[sideTyprKv.Key].ContainsKey(pumpKv.Key))
    //                                                FluCache[sideTyprKv.Key][pumpKv.Key] = new Queue<Tuple<DateTime, double>>();

    //                                            FluCache[sideTyprKv.Key][pumpKv.Key].Enqueue(Tuple.Create(DateTime.Now, flu));

    //                                            // 限制缓存
    //                                            if (FluCache[sideTyprKv.Key][pumpKv.Key].Count() > 200)
    //                                                FluCache[sideTyprKv.Key][pumpKv.Key].Dequeue();
    //                                        }
    //                                    }
    //                                }
    //                            }
    //                        }
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error("采集流量数据异常!", ex);
    //                    }
    //                    finally
    //                    {
    //                        Thread.Sleep(1000);
    //                    }
    //                }
    //            });
    //            t.IsBackground = true;
    //            t.Start();
    //        }

    //        /// <summary>
    //        /// 涂布闭环开关检测
    //        /// </summary>
    //        private void TBSwitchMonitor_Thread()
    //        {
    //            var t = new Thread(async () =>
    //            {
    //                _logger.Info("涂布闭环开关检测异步开启！");
    //                var cycleTime = 1000;
    //                var filmSn = string.Empty;
    //                var filmReplceTimeDic = new Dictionary<(ESideType, EPump), DateTime>();
    //                var needPopupDic = new Dictionary<(ESideType, EPump), bool>();
    //                var toPopupDic = new Dictionary<(ESideType, EPump), bool>();
    //                while (true)
    //                {
    //                    try
    //                    {
    //                        // 涂布闭环开关检测使能字段
    //                        var isTBSwitchMonitor = UserDefineVariableInfo.DicVariables["IsTBSwitchMonitor"].ToString();
    //                        // 未开启则重新循环
    //                        if (isTBSwitchMonitor != "1") continue;

    //                        // 读取PLC膜卷号-未正确读取则重新循环
    //                        if (!GetFilmSn(out string newFilmSn) || string.IsNullOrEmpty(newFilmSn)) continue;

    //                        // 时间中间缓存
    //                        var filmReplceTimeTemp = DateTime.Now;
    //                        // AB面分别判断
    //                        foreach (var sideConfigMD in CCTBConfig.BasicConfiguration_MD.SideConfigLis)
    //                        {
    //                            // 若闭环开关未开启
    //                            var switchObj = sideConfigMD?.PLCAddressConfig?.SwitchAddress?.AddresObj;
    //                            if (!(switchObj != null && switchObj?.DictValue != "1")) continue;

    //                            // 多泵分别判断
    //                            foreach (var plcPupmp in sideConfigMD.PLCAddressConfig.PLCAddressLis)
    //                            {
    //                                // 若此泵未开则跳过
    //                                if (!plcPupmp.IsEnable) continue;
    //                                // 若模组码已更新
    //                                if (newFilmSn != filmSn)
    //                                {
    //                                    filmReplceTimeDic[(sideConfigMD.SideType, plcPupmp.Pump)] = filmReplceTimeTemp;
    //                                    needPopupDic[(sideConfigMD.SideType, plcPupmp.Pump)] = true;
    //                                }

    //                                if (newFilmSn == filmSn && needPopupDic.TryGetValue((sideConfigMD.SideType, plcPupmp.Pump), out bool needPopup) && needPopup)
    //                                {
    //                                    // 若模组码更新时间已超过十分钟
    //                                    if (filmReplceTimeDic.TryGetValue((sideConfigMD.SideType, plcPupmp.Pump), out DateTime filmReplceTime) &&
    //                                        (filmReplceTimeTemp - filmReplceTime).TotalMilliseconds > 10 * 60 * 1000)
    //                                    {
    //                                        // 弹窗提示
    //                                        needPopupDic[(sideConfigMD.SideType, plcPupmp.Pump)] = false;
    //                                        toPopupDic[(sideConfigMD.SideType, plcPupmp.Pump)] = true;
    //                                    }
    //                                }
    //                            }
    //                        }

    //                        // 更新模组码
    //                        filmSn = newFilmSn;

    //                        // 弹窗提示
    //                        var toPopupLis = toPopupDic.Where(o => o.Value).ToList();
    //                        if (toPopupLis?.Count > 0)
    //                        {
    //                            var msg = "(" + string.Join("、", toPopupLis.Select(o => o.Key.Item1 + ":" + o.Key.Item2)) + ")";
    //                            msg += Environment.NewLine;
    //                            msg += $"更换膜卷[{filmSn}]已超过10分钟，请及时开启闭环！";
    //                            ShowGrowl(InfoType.Warning, msg);
    //                            toPopupDic?.Clear();
    //                        }
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error("涂布闭环开关检测异步异常!", ex);
    //                        cycleTime = 60000;
    //                    }
    //                    finally
    //                    {
    //                        Thread.Sleep(cycleTime);
    //                        cycleTime = 1000;
    //                    }
    //                }
    //            });
    //            t.IsBackground = true;
    //            t.Start();
    //        }

    //        /// <summary>
    //        /// 初始化涂布防呆逻辑
    //        /// </summary>
    //        private void CCTBLogicInterceptInit_Thread()
    //        {
    //            var t = new Thread(() =>
    //            {
    //                CCTBLogic_InterceptTask();
    //            });
    //            t.IsBackground = true;
    //            t.Start();
    //        }
    //        #endregion
    //        #endregion
    //        #endregion

    //        /// <summary>
    //        /// 获取数据库参数
    //        /// </summary>
    //        /// <param name="num"></param>
    //        private Dictionary<string, string> GetUDCParam(string state)
    //        {
    //            var reDic = new Dictionary<string, string>();
    //            try
    //            {
    //                //获取自定义配置参数实时采集 
    //                var user_define_config_bll = new BLL.user_define_config();
    //                var user_define_config_list = user_define_config_bll.GetModelList($"state='{state}'");
    //                // 数据库没有，则新增
    //                if (user_define_config_list?.Count() <= 0)
    //                {
    //                    // 步骤1：查询最大ID（单独执行，避免批量SQL执行的问题）
    //                    var queryMaxIdSql = "SELECT IFNULL(MAX(id), 0) FROM user_define_config;"; // 无数据时返回0，避免null
    //                    var maxIdObj = Maticsoft.DBUtility.DbHelperMySQL.GetSingle(queryMaxIdSql);
    //                    var maxId = maxIdObj != DBNull.Value ? Convert.ToInt32(maxIdObj) : 0;

    //                    // 步骤2：计算自增起始值（最大ID + 1，核心防冲突逻辑）
    //                    var autoIncrementStart = maxId + 1;
    //                    user_define_config_bll.Add(new Model.user_define_config() { id = autoIncrementStart, config_list = "", state = state, remark = $"{state}采集数据到表格", datatime = DateTime.Now });
    //                    return reDic;
    //                }

    //                var udcItem = user_define_config_list[0];
    //                var userConfig = udcItem.config_list.Split(',').ToList();
    //                if (userConfig?.Count() > 0)
    //                {
    //                    foreach (var udvItem in UserDefineVariableInfo.lstUserDefineVariables.Where(x => userConfig.Exists(t => x.id.ToString().Equals(t))))
    //                    {
    //                        var key = udvItem.VariableName; ;
    //                        var value = string.Empty;
    //                        if (UserDefineVariableInfo.DicVariables._dict.ContainsKey(udvItem.VariableName))
    //                            value = UserDefineVariableInfo.DicVariables[udvItem.VariableName].ToString();

    //                        reDic.Add(key, value);
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error("获取打印配置字段异常", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //            return reDic;
    //        }

    //        /// <summary>
    //        /// 获取膜卷号
    //        /// </summary>
    //        /// <returns></returns>
    //        public static bool GetFilmSn(out string filmSn)
    //        {
    //            filmSn = "";
    //            var tBFileSnConfigs = UserDefineVariableInfo.DicVariables["TBFilmSnConfig"].ToString().Split(';');
    //            if (tBFileSnConfigs[0] == "PLC")
    //            {
    //                filmSn = UserDefineVariableInfo.DicVariables["膜卷号"].ToString();
    //                return true;
    //            }

    //            if (tBFileSnConfigs.Length < 2)
    //            {
    //                _logger.Error($"未配置膜卷号文件地址！");
    //                return false;
    //            }
    //            try
    //            {
    //                var mesClientData = File.ReadAllText(tBFileSnConfigs[1]);
    //                var mesClientDatas = Regex.Split(mesClientData, Environment.NewLine).ToList();
    //                if (!mesClientDatas.Exists(o => o.startswith("ProductSn：")))
    //                {
    //                    _logger.Error($"未查询到膜卷号信息！");
    //                    return false;
    //                }

    //                var productSnKV = mesClientDatas.Find(o => o.startswith("ProductSn：")).Split('：');
    //                if (productSnKV.Length < 2)
    //                {
    //                    _logger.Error($"未查询到膜卷号信息！");
    //                    return false;
    //                }
    //                filmSn = productSnKV[1];
    //                return true;
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"获取膜卷号异常[{ex}]");
    //                return false;
    //            }
    //        }

    //        /// <summary>
    //        /// 过滤字符串中的非法字符
    //        /// </summary>
    //        /// <param name="input">原始字符串</param>
    //        /// <returns>过滤后的安全字符串</returns>
    //        public static string SanitizeFullPath(string filePath, string fileName)
    //        {
    //            try
    //            {
    //                var newFilePath = RemoveInvalidPathChars(filePath);
    //                var newFileName = RemoveInvalidFileNameChars(fileName);
    //                var fullPath = Path.Combine(newFilePath, newFileName);
    //                // 移除路径中的不可见控制字符（如 ASCII 0）
    //                fullPath = new string(fullPath.Where(ch => !char.IsControl(ch)).ToArray());
    //                return fullPath;
    //            }
    //            catch (Exception ex)
    //            {
    //                throw new Exception($"[{filePath} {fileName}]生成路径异常！", ex);
    //            }
    //        }

    //        private static string RemoveInvalidPathChars(string input)
    //        {
    //            var invalidChars = Path.GetInvalidPathChars();
    //            foreach (char c in invalidChars)
    //            {
    //                input = input.Replace(c.ToString(), "");
    //            }
    //            return input;
    //        }

    //        private static string RemoveInvalidFileNameChars(string input)
    //        {
    //            var invalidChars = Path.GetInvalidFileNameChars();
    //            foreach (char c in invalidChars)
    //            {
    //                input = input.Replace(c.ToString(), "");
    //            }
    //            return input;
    //        }

    //        public static DateTime ToLocalDateTime(long ts)
    //        {
    //            var dto = ts >= 1_000_000_000_000L   // 13 位多为毫秒
    //                ? DateTimeOffset.FromUnixTimeMilliseconds(ts)
    //                : DateTimeOffset.FromUnixTimeSeconds(ts);

    //            return dto.ToLocalTime().DateTime;
    //        }

    //        /// <summary>
    //        /// 获取当前时间的毫秒级 Unix 时间戳（long 类型）
    //        /// </summary>
    //        public static long GetTimeStampMilliseconds()
    //        {
    //            // 基准时间：1970-01-01 00:00:00 UTC
    //            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    //            // 当前 UTC 时间 - 基准时间 = 时间差
    //            var timeSpan = DateTime.UtcNow - unixEpoch;
    //            // 转换为毫秒（long 类型）
    //            return (long)timeSpan.TotalMilliseconds;
    //        }

    //        private static void ShowGrowl(InfoType type, string message)
    //        {
    //            switch (type)
    //            {
    //                case InfoType.Ask:
    //                    Growl.AskGlobal(new GrowlInfo() { Message = message, WaitTime = 60, IsCustom = true });
    //                    break;
    //                case InfoType.Info:
    //                    Growl.InfoGlobal(new GrowlInfo() { Message = message, WaitTime = 5, IsCustom = true });
    //                    break;
    //                case InfoType.Warning:
    //                    Growl.WarningGlobal(new GrowlInfo() { Message = message, WaitTime = 60, IsCustom = true });
    //                    break;
    //                case InfoType.Error:
    //                    Growl.ErrorGlobal(new GrowlInfo() { Message = message, WaitTime = 60, IsCustom = true });
    //                    break;
    //                case InfoType.Fatal:
    //                    Growl.FatalGlobal(new GrowlInfo() { Message = message, WaitTime = 60, IsCustom = true });
    //                    break;
    //                case InfoType.Success:
    //                    Growl.SuccessGlobal(new GrowlInfo() { Message = message, WaitTime = 5, IsCustom = true });
    //                    break;
    //            }
    //        }
    //    }
    //    #endregion

    //    #region 数据通讯模块
    //    /// <summary>
    //    /// 数据通讯模块
    //    /// </summary>
    //    partial class VM_CCTBMonitoringPage : ViewModelBase
    //    {
    //        #region 获取CCD数据
    //        /// <summary>
    //        /// CCD检测数据报文队列
    //        /// </summary>
    //        private BlockingCollection<(DateTime, string, string, byte[])> ccdDataQue;
    //        private void CCDDataEnqueue(Tuple<DateTime, string, string, byte[]> data)
    //        {
    //            try
    //            {
    //                var ccdData = (data.Item1, data.Item2, data.Item3, data.Item4);
    //                ccdDataQue.Add(ccdData);
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"CCD报文缓存异常(当前缓存{ccdDataQue?.Count}条)，已丢弃：[{data.Item1.ToString("yyyy-MM-dd HH:mm:ss")}]-[{data.Item2}]-[{data.Item3}]-报文字节[{BitConverter.ToString(data.Item4)}]", ex);
    //            }
    //        }
    //        private void InitializeCCDDataMessageSink()
    //        {
    //            var que = new ConcurrentQueue<(DateTime, string, string, byte[])>();
    //            ccdDataQue = new BlockingCollection<(DateTime, string, string, byte[])>(que, 200);
    //            _ = RunInBackgroundAsync(CCDDataMessageSinkAsync);
    //        }
    //        private Task CCDDataMessageSinkAsync()
    //        {
    //            try
    //            {
    //                var isEnable = UserDefineVariableInfo.DicVariables["TBTcpServerCCDSwitch"].ToString();
    //                if (isEnable != "1") return Task.CompletedTask;
    //                foreach ((DateTime gatherTime, string communicationMode, string ipEndPointStr, byte[] bufRec) data in ccdDataQue.GetConsumingEnumerable())
    //                {
    //                    try
    //                    {
    //                        switch (data.communicationMode)
    //                        {
    //                            case "TcpServer_CCD":
    //                                CCDMessageSink_CCDServer(data.gatherTime, data.ipEndPointStr, data.bufRec);
    //                                break;
    //                            default:
    //                                _logger.Warn($"未实现[{data.communicationMode}]协议：请重新配置！");
    //                                break;
    //                        }
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error($"CCD报文解析异常！丢弃当前条数据：[{data.Item1.ToString("yyyy-MM-dd HH:mm:ss")}]-[{data.Item2}]-[{data.Item3}]-报文字节[{BitConverter.ToString(data.Item4)}]", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error(ex);
    //            }
    //            return Task.CompletedTask;
    //        }
    //        #region 

    //        #endregion 上位机：服务器
    //        CCTBSocketServer ccdSocketServer;
    //        /// <summary>
    //        /// 初始化涂布服务器
    //        /// </summary>
    //        private void InitTBCCDServer()
    //        {
    //            var isEnable = UserDefineVariableInfo.DicVariables["TBTcpServerCCDSwitch"].ToString();
    //            if (isEnable != "1") return;
    //            if (!int.TryParse(UserDefineVariableInfo.DicVariables["TBTcpServerCCDPort"].ToString(), out int port)) port = 9001;
    //            _logger.Info($"初始化[TcpServerCCD]监听服务[端口{port}]!");
    //            ccdSocketServer = new CCTBSocketServer();
    //            ccdSocketServer.Separator = Encoding.UTF8.GetBytes(TBCCDProtocolData.Separator);
    //            ccdSocketServer.OnReceiveDataEven += new CCTBSocketServer.ReceiveDataDelegate(CCDMessageSendToOPMM_CCDServer);
    //            ccdSocketServer.OnReceiveDataEven += new CCTBSocketServer.ReceiveDataDelegate(CCDMessageSinkEnqueue_CCDServer);
    //            var isOpen = ccdSocketServer.OpenServer(port);
    //            if (!isOpen)
    //            {
    //                var msg = $"[TcpServerCCD]开启端口[{port}]失败！";
    //                _logger.Error(msg);
    //                throw new Exception(msg);
    //            }
    //        }
    //        /// <summary>
    //        /// 接受消息发送到一人多机
    //        /// </summary>
    //        /// <param name="name"></param>
    //        /// <param name="str"></param>
    //        /// <param name="buf"></param>
    //        private void CCDMessageSendToOPMM_CCDServer(CCTBPublicSession session, byte[] buf)
    //        {
    //            var tBSUMMTSwitch = UserDefineVariableInfo.DicVariables["TBSingleUserMultipleMachineTerminalSwitch"].ToString();
    //            if (tBSUMMTSwitch != "1") return;
    //            var clientName = "一人多机CCD";
    //            _ = RunInBackgroundAsync(() =>
    //            {
    //                try
    //                {
    //                    var client = GetSocketClient(clientName);
    //                    if (client == null || !client.IsAlive())
    //                    {
    //                        throw new Exception($"[{clientName}]设备未连接！");
    //                    }

    //                    var bufStr = Encoding.UTF8.GetString(buf);
    //                    var regexSeparator = Regex.Escape(TBCCDProtocolData.Separator);
    //                    var datas = Regex.Split(bufStr, regexSeparator);
    //                    foreach (var data in datas)
    //                    {
    //                        if (string.IsNullOrEmpty(data)) continue;
    //                        var payload = Encoding.UTF8.GetBytes(data);
    //                        var header = new byte[4];
    //                        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)payload.Length);
    //                        client.Send(header.Concat(payload).ToArray());
    //                    }
    //                    return Task.CompletedTask;
    //                }
    //                catch (Exception ex)
    //                {
    //                    _logger.Warn($"发送异常的报文[{Encoding.UTF8.GetString(buf)}]");
    //                    _logger.Error($"向[{clientName}]设备发送数据异常!", ex);
    //                    return Task.CompletedTask;
    //                }
    //            });
    //        }

    //        private void CCDMessageSinkEnqueue_CCDServer(CCTBPublicSession session, byte[] buf)
    //        {
    //            var ccdData = Tuple.Create(DateTime.Now, "TcpServer_CCD", session.IPEndPointStr, buf);
    //            CCDDataEnqueue(ccdData);
    //        }
    //        #region CCD服务器报文协议
    //        /// <summary>
    //        /// 服务器接收客户端重量数据信息
    //        /// </summary>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="name"></param>
    //        /// <param name="buf"></param>
    //        private void CCDMessageSink_CCDServer(DateTime gatherTime, string ipEndPointStr, byte[] buf)
    //        {
    //            var sw = new Stopwatch();
    //            sw.Start();
    //            _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]开始处理接收报文[{sw.ElapsedMilliseconds}ms]：设备[{ipEndPointStr}] —— 报文字节长度[{buf.Length}]", false);
    //            CCDMessageParse(gatherTime, Encoding.UTF8.GetString(buf));
    //            sw.Stop();
    //            _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]结束处理接收报文[{sw.ElapsedMilliseconds}ms]：设备[{ipEndPointStr}]", false);
    //        }
    //        private void CCDMessageParse(DateTime gatherTime, string str)
    //        {
    //            try
    //            {
    //                var tbLis = TBCCDProtocolData.ProtocolAnalysis(str, false);
    //                foreach (var tb in tbLis)
    //                {
    //                    if (tb == null || !tb.AnalysisFlag)
    //                    {
    //                        _logger.Error($"报文解析失败");
    //                        return;
    //                    }
    //                    if ((tb.NeedCrcCheck && !tb.DataCrcFlag))
    //                    {
    //                        _logger.Error($"[{tb.CommandName}]CRC[{tb.DataCrc}]校验失败");
    //                        return;
    //                    }
    //                    switch (tb.CommandName)
    //                    {
    //                        case "hb":
    //                            var hBMessageData = JsonConvert.DeserializeObject<HBMessageData>(tb.DataJson);
    //                            CCDMessageParse_HBMessageData(gatherTime, hBMessageData, tb.CCTBCCDQUEST);
    //                            break;
    //                        case "data_a_wet":
    //                        case "data_b_wet":
    //                        case "data_b_dry":
    //                            var weightMessageData = JsonConvert.DeserializeObject<WeightMessageData>(tb.DataJson);
    //                            CCDMessageParse_WeightMessageData(gatherTime, weightMessageData, tb.CCTBCCDQUEST);
    //                            break;
    //                        case "alarm":
    //                            var alarmMessageData = JsonConvert.DeserializeObject<AlarmMessageData>(tb.DataJson);
    //                            CCDMessageParse_AlarmMessageData(gatherTime, alarmMessageData, tb.CCTBCCDQUEST);
    //                            break;
    //                        default:
    //                            _logger.Warn($"未实现报文指令[{tb.CommandName}]");
    //                            break;
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Warn($"解析异常的报文[{str}]", false);
    //                _logger.Error("CCD报文解析异常：", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //        }

    //        #region 指令
    //        /// <summary>
    //        /// CCD数据处理
    //        /// </summary>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="messageTagStr"></param>
    //        /// <param name="weight"></param>
    //        /// <exception cref="Exception"></exception>
    //        private void CCDMessageParse_HBMessageData(DateTime gatherTime, HBMessageData message, CCTBCCDQUEST quest)
    //        {
    //            var dateTime = ToLocalDateTime(quest?.Ts ?? 0).ToString("yyyy-MM-dd HH:mm:ss.fff");
    //            _logger.Info($"正在处理CCD心跳数据[设备ID{quest?.Sid}]心跳 消息类型:{quest?.MsgType} 设备侧时间[{dateTime}]", false);
    //        }

    //        /// <summary>
    //        /// CCD数据处理
    //        /// </summary>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="messageTagStr"></param>
    //        /// <param name="weight"></param>
    //        /// <exception cref="Exception"></exception>
    //        private void CCDMessageParse_WeightMessageData(DateTime gatherTime, WeightMessageData message, CCTBCCDQUEST quest)
    //        {
    //            var dateTime = ToLocalDateTime(quest?.Ts ?? 0).ToString("yyyy-MM-dd HH:mm:ss.fff");
    //            _logger.Info($"正在处理CCD重量数据[设备ID:{quest?.Sid} 消息类型:{quest?.MsgType} 设备侧时间:{dateTime}]！", false);
    //            var dicDep = new Dictionary<string, string>();
    //            dicDep[$"采集时间"] = gatherTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
    //            dicDep[$"消息类型"] = quest?.MsgType;
    //            dicDep[$"设备ID"] = quest?.Sid;
    //            dicDep[$"设备侧时间"] = dateTime;
    //            dicDep[$"压缩格式"] = quest?.Z;
    //            dicDep[$"检测码"] = quest?.Sig;

    //            dicDep.Add("总膜区宽度", message.CoatingTotalWidth.ToString());
    //            foreach (var data in message?.TabWidth ?? new List<ParaData>()) dicDep[$"{data?.PName}"] = data?.PValue;
    //            foreach (var data in message?.CilWidth ?? new List<ParaData>()) dicDep[$"{data?.PName}"] = data?.PValue;
    //            foreach (var data in message?.CoatingWidth ?? new List<ParaData>()) dicDep[$"{data?.PName}"] = data?.PValue;
    //            foreach (var data in message?.Misalign ?? new List<ParaData>()) dicDep[$"{data?.PName}"] = data?.PValue;
    //            foreach (var data in message?.JudgeRes ?? new List<JudgeResult>()) dicDep[$"{data?.Zone}"] = data?.Result;
    //            foreach (var data in message?.TabSpecification ?? new List<ParaData>()) dicDep[$"{data?.PName}"] = data?.PValue;
    //            foreach (var data in message?.CilSpecification ?? new List<ParaData>()) dicDep[$"{data?.PName}"] = data?.PValue;
    //            foreach (var data in message?.CoatingSpecification ?? new List<ParaData>()) dicDep[$"{data?.PName}"] = data?.PValue;
    //            foreach (var data in message?.MisalignSpecification ?? new List<ParaData>()) dicDep[$"{data?.PName}"] = data?.PValue;

    //            var filePath = UserDefineVariableInfo.DicVariables["productionDataSavePath1"].ToString() + $"\\涂布CCD数据\\" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    //            GetFilmSn(out string filmSn);
    //            // 获取PLC地址数据
    //            var dic = GetUDCParam("ccd") ?? new Dictionary<string, string>();
    //            dic.ToList().ForEach(o => { dicDep[o.Key] = o.Value; });

    //            var fullPath = SanitizeFullPath(filePath, filmSn + $"_{quest?.Sid}_{quest?.MsgType}.csv");
    //            var excelData = Tuple.Create(fullPath, quest?.MsgType, dicDep);
    //            ExcelDataEnqueue(excelData);
    //        }
    //        /// <summary>
    //        /// CCD数据处理
    //        /// </summary>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="messageTagStr"></param>
    //        /// <param name="weight"></param>
    //        /// <exception cref="Exception"></exception>
    //        private void CCDMessageParse_AlarmMessageData(DateTime gatherTime, AlarmMessageData message, CCTBCCDQUEST quest)
    //        {
    //            var dateTime = ToLocalDateTime(quest?.Ts ?? 0).ToString("yyyy-MM-dd HH:mm:ss.fff");
    //            _logger.Info($"正在处理CCD报警数据[设备ID:{quest?.Sid} 消息类型:{quest?.MsgType} 设备侧时间:{dateTime}]", false);
    //            var dicDep = new Dictionary<string, string>();
    //            dicDep[$"采集时间"] = gatherTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
    //            dicDep[$"消息类型"] = quest?.MsgType;
    //            dicDep[$"设备ID"] = quest?.Sid;
    //            dicDep[$"设备侧时间"] = dateTime;
    //            dicDep[$"压缩格式"] = quest?.Z;
    //            dicDep[$"检测码"] = quest?.Sig;
    //            dicDep[$"报警膜区"] = message?.Zone;
    //            dicDep[$"报警信息"] = message?.Info;
    //            var filePath = UserDefineVariableInfo.DicVariables["productionDataSavePath1"].ToString() + $"\\涂布CCD数据\\" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    //            GetFilmSn(out string filmSn);

    //            var fullPath = SanitizeFullPath(filePath, filmSn + $"_{quest?.Sid}_{quest?.MsgType}.csv");
    //            var excelData = Tuple.Create(fullPath, quest?.MsgType, dicDep);
    //            ExcelDataEnqueue(excelData);
    //        }
    //        #endregion
    //        #endregion
    //        #endregion

    //        #region 测重获取重量数据      
    //        /// <summary>
    //        /// 重量数据报文队列
    //        /// </summary>
    //        private BlockingCollection<(DateTime, string, string, byte[])> weightDataQue;
    //        private void WeightDataEnqueue(Tuple<DateTime, string, string, byte[]> data)
    //        {
    //            try
    //            {
    //                var weightData = (data.Item1, data.Item2, data.Item3, data.Item4);
    //                weightDataQue.Add(weightData);
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"涂布报文缓存异常(当前缓存{weightDataQue?.Count}条)，已丢弃：[{data.Item1.ToString("yyyy-MM-dd HH:mm:ss")}]-[{data.Item2}]-[{data.Item3}]-报文字节[{BitConverter.ToString(data.Item4)}]", ex);
    //            }
    //        }
    //        private void InitializeWeightDataMessageSink()
    //        {
    //            var que = new ConcurrentQueue<(DateTime, string, string, byte[])>();
    //            weightDataQue = new BlockingCollection<(DateTime, string, string, byte[])>(que, 200);
    //            _ = RunInBackgroundAsync(WeightMessageSinkAsync);
    //        }
    //        private Task WeightMessageSinkAsync()
    //        {
    //            try
    //            {
    //                foreach ((DateTime gatherTime, string communicationMode, string ipEndPointStr, byte[] bufRec) data in weightDataQue.GetConsumingEnumerable())
    //                {
    //                    try
    //                    {
    //                        // 非设置的闭环选择数据则丢弃
    //                        if (data.communicationMode != CCTBConfig.CommunicationMode) continue;
    //                        switch (data.communicationMode)
    //                        {
    //                            case "TcpClient":
    //                                WeightMessageSink_Client(data.gatherTime, data.ipEndPointStr, data.bufRec);
    //                                break;
    //                            case "TcpServer":
    //                                WeightMessageSink_Server(data.gatherTime, data.ipEndPointStr, data.bufRec);
    //                                break;
    //                            case "TcpServer_DieHead":
    //                                WeightMessageSink_Server_DieHead(data.gatherTime, data.ipEndPointStr, data.bufRec);
    //                                break;
    //                            default:
    //                                _logger.Warn($"未实现[{data.Item2}]协议：请重新配置！");
    //                                break;
    //                        }
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error($"涂布报文解析异常！丢弃当前条数据：[{data.Item1.ToString("yyyy-MM-dd HH:mm:ss")}]-[{data.Item2}]-[{data.Item3}]-报文字节[{BitConverter.ToString(data.Item4)}]", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error(ex);
    //            }
    //            return Task.CompletedTask;
    //        }

    //        #region 上位机：服务器
    //        #region TcpServer
    //        CCTBSocketServer socketServer_TcpServer;
    //        /// <summary>
    //        /// 初始化涂布服务器
    //        /// </summary>
    //        private void InitTBWeightTcpServer()
    //        {
    //            var isEnable = UserDefineVariableInfo.DicVariables["TBTcpServerSwitch"].ToString();
    //            if (!(isEnable == "1" || CCTBConfig.CommunicationMode == "TcpServer")) return;
    //            if (!int.TryParse(UserDefineVariableInfo.DicVariables["TBTcpServerPort"].ToString(), out int port)) port = 6666;
    //            _logger.Info($"初始化[TcpServer]监听服务[端口{port}]!");
    //            socketServer_TcpServer = new CCTBSocketServer();
    //            socketServer_TcpServer.Separator = Encoding.UTF8.GetBytes(TBProtocolData.Tail);
    //            socketServer_TcpServer.OnReceiveDataEven += new CCTBSocketServer.ReceiveDataDelegate(WeightMessageSendToOPMM_TcpServer);
    //            socketServer_TcpServer.OnReceiveDataEven += new CCTBSocketServer.ReceiveDataDelegate(WeightMessageSinkEnqueue_TcpServer);
    //            var isOpen = socketServer_TcpServer.OpenServer(port);
    //            if (!isOpen)
    //            {
    //                var msg = $"[TcpServer]开启端口[{port}]失败！";
    //                _logger.Error(msg);
    //                throw new Exception(msg);
    //            }
    //        }
    //        /// <summary>
    //        /// 接受消息发送到一人多机
    //        /// </summary>
    //        /// <param name="name"></param>
    //        /// <param name="str"></param>
    //        /// <param name="buf"></param>
    //        private void WeightMessageSendToOPMM_TcpServer(CCTBPublicSession session, byte[] buf)
    //        {
    //            var isEnable = UserDefineVariableInfo.DicVariables["TBSingleUserMultipleMachineTerminalSwitch"].ToString();
    //            if (isEnable != "1") return;
    //            var clientName = "一人多机测厚仪";
    //            _ = RunInBackgroundAsync(() =>
    //            {
    //                try
    //                {
    //                    var client = GetSocketClient(clientName);
    //                    if (client == null || !client.IsAlive())
    //                    {
    //                        throw new Exception($"[{clientName}]设备未连接！");
    //                    }

    //                    var header = new byte[4];
    //                    BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)buf.Length);
    //                    client.Send(header.Concat(buf).ToArray());
    //                    return Task.CompletedTask;
    //                }
    //                catch (Exception ex)
    //                {
    //                    _logger.Warn($"发送异常的报文[{Encoding.UTF8.GetString(buf)}]");
    //                    _logger.Error($"向[{clientName}]设备发送数据异常!", ex);
    //                    return Task.CompletedTask;
    //                }
    //            });
    //        }
    //        private void WeightMessageSinkEnqueue_TcpServer(CCTBPublicSession session, byte[] buf)
    //        {
    //            var weightData = Tuple.Create(DateTime.Now, "TcpServer", session.IPEndPointStr, buf);
    //            WeightDataEnqueue(weightData);
    //        }
    //        #endregion

    //        #region TcpServer_DieHead
    //        CCTBSocketServer socketServer_TcpServerDieHead;
    //        /// <summary>
    //        /// 初始化涂布服务器
    //        /// </summary>
    //        private void InitTBWeightTcpServerDieHead()
    //        {
    //            var isEnable = UserDefineVariableInfo.DicVariables["TBTcpServerDieHeadSwitch"].ToString();
    //            if (!(isEnable == "1" || CCTBConfig.CommunicationMode == "TcpServer_DieHead")) return;
    //            if (!int.TryParse(UserDefineVariableInfo.DicVariables["TBTcpServerDieHeadPort"].ToString(), out int port)) port = 5555;
    //            _logger.Info($"初始化[TcpServerDieHead]监听服务[端口{port}]!");
    //            socketServer_TcpServerDieHead = new CCTBSocketServer();
    //            socketServer_TcpServerDieHead.Separator = Encoding.UTF8.GetBytes(TBDHProtocolData.Tail);
    //            socketServer_TcpServerDieHead.OnReceiveDataEven += new CCTBSocketServer.ReceiveDataDelegate(WeightMessageSinkEnqueue_TcpServerDieHead);
    //            var isOpen = socketServer_TcpServerDieHead.OpenServer(port);
    //            if (!isOpen)
    //            {
    //                var msg = $"[TcpServerDieHead]开启端口[{port}]失败！";
    //                _logger.Error(msg);
    //                throw new Exception(msg);
    //            }
    //        }
    //        private void WeightMessageSinkEnqueue_TcpServerDieHead(CCTBPublicSession session, byte[] buf)
    //        {
    //            var weightData = Tuple.Create(DateTime.Now, "TcpServer_DieHead", session.IPEndPointStr, buf);
    //            WeightDataEnqueue(weightData);
    //        }
    //        #endregion

    //        #region  测重服务器报文协议——锐奇
    //        /// <summary>
    //        /// 服务器接收客户端重量数据信息
    //        /// </summary>
    //        /// <param name="name"></param>
    //        /// <param name="str"></param>
    //        /// <param name="buf"></param>
    //        /// <exception cref="Exception"></exception>
    //        private void WeightMessageSink_Server(DateTime gatherTime, string ipEndPointStr, byte[] buf)
    //        {
    //            var sw = new Stopwatch();
    //            sw.Start();
    //            _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]开始处理接收报文[{sw.ElapsedMilliseconds}ms]：设备[{ipEndPointStr}] —— 报文字节长度[{buf.Length}]", false);
    //            WeightMessageParseS(gatherTime, Encoding.UTF8.GetString(buf));
    //            sw.Stop();
    //            _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]结束处理接收报文[{sw.ElapsedMilliseconds}ms]：设备[{ipEndPointStr}]", false);
    //        }

    //        /// <summary>
    //        /// 解析报文
    //        /// </summary>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="str"></param>
    //        private void WeightMessageParseS(DateTime gatherTime, string str)
    //        {
    //            try
    //            {
    //                var tbLis = TBProtocolData.ProtocolAnalysis(str, false);
    //                foreach (var tb in tbLis)
    //                {
    //                    if (tb == null || !tb.AnalysisFlag)
    //                    {
    //                        _logger.Error($"报文解析失败");
    //                        continue;
    //                    }
    //                    if ((tb.NeedCrcCheck && !tb.DataCrcFlag))
    //                    {
    //                        _logger.Error($"[{tb.CommandName}]CRC[{tb.DataCrc}]校验失败");
    //                        continue;
    //                    }
    //                    switch (tb.CommandName)
    //                    {
    //                        case "CCTBHEARTQUEST":
    //                            var cCTBHEARTQUEST = JsonConvert.DeserializeObject<CCTBHEARTQUEST>(tb.DataJson);
    //                            WeightMessageParseS_CCTBHEARTQUEST(gatherTime, cCTBHEARTQUEST);
    //                            break;
    //                        case "CCTBWEIGHTQUEST":
    //                            var cCTBWEIGHTQUEST = JsonConvert.DeserializeObject<CCTBWEIGHTQUEST>(tb.DataJson);
    //                            WeightMessageParseS_CCTBWEIGHTQUEST(gatherTime, cCTBWEIGHTQUEST);
    //                            break;
    //                        default:
    //                            _logger.Warn($"未实现报文指令[{tb.CommandName}]");
    //                            break;
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Warn($"解析异常的报文[{str}]", false);
    //                _logger.Error("涂布报文解析异常：", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //        }

    //        #region 指令
    //        /// <summary>
    //        /// 心跳处理
    //        /// </summary>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="messageTagStr"></param>
    //        /// <param name="weight"></param>
    //        /// <exception cref="Exception"></exception>
    //        private void WeightMessageParseS_CCTBHEARTQUEST(DateTime gatherTime, CCTBHEARTQUEST weight)
    //        {
    //            try
    //            {
    //                _logger.Info("正在处理Server心跳数据！", false);
    //                // -TODO    心跳处理
    //            }
    //            catch (Exception ex)
    //            {
    //                throw new Exception($"设备[{weight?.DEVICESN}]心跳报文处理异常！", ex);
    //            }
    //        }
    //        /// <summary>
    //        /// 毫米数据处理
    //        /// </summary>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="messageTagStr"></param>
    //        /// <param name="weight"></param>
    //        /// <exception cref="Exception"></exception>
    //        private void WeightMessageParseS_CCTBWEIGHTQUEST(DateTime gatherTime, CCTBWEIGHTQUEST weight)
    //        {
    //            _logger.Info("正在处理Server重量数据！", false);
    //            var dataArr = new float[0];
    //            var dataNumber = 0;
    //            var scanType = -1f;
    //            var baseMaterialWeight = 0f;
    //            var _targetValueA = 0f;
    //            var _targetValueB = 0f;
    //            var _toleranceA = 0f;
    //            var _toleranceB = 0f;

    //            dataArr = weight.WEIGHT.ToList().ConvertAll(float.Parse).ToArray();
    //            dataNumber = int.Parse(weight.SCANCOUNT);
    //            baseMaterialWeight = float.Parse(weight.BASEVALUE);
    //            try
    //            {
    //                // 一人多机专用协议字段
    //                if (weight.DEVICENAME == "BV") return;
    //                var wmc = CCTBConfig.WeightMessage.WeightMessagConfigLis.Single((WeightMessageConfig o) => o.IsEnable && o.MessageTag2 == weight.DEVICENAME);
    //                lock (this)
    //                {
    //                    if (wmc.SideType == ESideType.A)
    //                    {
    //                        targetValue_A = float.Parse(weight.TARGET_VALUE);
    //                        toleranceA = float.Parse(weight.TECHNIQUE);
    //                    }
    //                    else if (wmc.SideType == ESideType.B)
    //                    {
    //                        targetValue_B = float.Parse(weight.TARGET_VALUE) - targetValue_A;
    //                        toleranceB = float.Parse(weight.TECHNIQUE);
    //                    }
    //                    _targetValueA = targetValue_A;
    //                    _targetValueB = targetValue_B;
    //                    _toleranceA = toleranceA;
    //                    _toleranceB = toleranceB;
    //                    _logger.Info("报文标识[" + weight.DEVICENAME + "] -- 数据长度[" + weight.WEIGHT_COUNT + "]", isAddCore: false);
    //                }
    //                GetWeightDataCmd(gatherTime, weight.DEVICENAME, dataArr, wmc.SideType, wmc.WeightSourceType, baseMaterialWeight, _targetValueA, _targetValueB, _toleranceA, _toleranceB, dataNumber, scanType);
    //            }
    //            catch (Exception ex)
    //            {
    //                throw new Exception($"未查询到与报文标识[{weight?.DEVICENAME}]唯一匹配的报文配置！", ex);
    //            }
    //        }
    //        #endregion
    //        #endregion

    //        #region  模头服务器报文协议——上川、YSJ
    //        /// <summary>
    //        /// 服务器接收客户端重量数据信息
    //        /// </summary>
    //        /// <param name="name"></param>
    //        /// <param name="str"></param>
    //        /// <param name="buf"></param>
    //        /// <exception cref="Exception"></exception>
    //        private void WeightMessageSink_Server_DieHead(DateTime gatherTime, string ipEndPointStr, byte[] buf)
    //        {
    //            var sw = new Stopwatch();
    //            sw.Start();
    //            _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]开始处理接收报文[{sw.ElapsedMilliseconds}ms]：设备[{ipEndPointStr}] —— 报文字节长度[{buf.Length}]", false);
    //            // 上川供应商报文使用utf8编码
    //            WeightMessageParseSDH(gatherTime, Encoding.UTF8.GetString(buf));
    //            sw.Stop();
    //            _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]结束处理接收报文[{sw.ElapsedMilliseconds}ms]：设备[{ipEndPointStr}]", false);
    //        }

    //        /// <summary>
    //        /// 解析报文
    //        /// </summary>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="str"></param>
    //        private void WeightMessageParseSDH(DateTime gatherTime, string str)
    //        {
    //            try
    //            {
    //                var tbLis = TBDHProtocolData.ProtocolAnalysis(str, false);
    //                foreach (var tb in tbLis)
    //                {
    //                    if (tb == null || !tb.AnalysisFlag)
    //                    {
    //                        _logger.Error($"报文解析失败");
    //                        continue;
    //                    }
    //                    if ((tb.NeedCrcCheck && !tb.DataCrcFlag))
    //                    {
    //                        _logger.Error($"[{tb.CommandName}]CRC[{tb.DataCrc}]校验失败");
    //                        continue;
    //                    }
    //                    switch (tb.CommandName)
    //                    {
    //                        case "WEIGHTDATAREQUEST":
    //                            var weightDataRequest = JsonConvert.DeserializeObject<WeightDataRequest>(tb.DataJson);
    //                            WeightMessageParseSDH_WEIGHTDATAREQUEST(gatherTime, weightDataRequest);
    //                            break;
    //                        default:
    //                            _logger.Warn($"未实现报文指令[{tb.CommandName}]");
    //                            break;
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Warn($"解析异常的报文[{str}]", false);
    //                _logger.Error("涂布报文解析异常：", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //        }

    //        #region 指令
    //        /// <summary>
    //        /// 模头分区数据处理
    //        /// </summary>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="weight"></param>
    //        /// <exception cref="Exception"></exception>
    //        private void WeightMessageParseSDH_WEIGHTDATAREQUEST(DateTime gatherTime, WeightDataRequest weight)
    //        {
    //            _logger.Info("正在处理模头重量数据！", false);
    //            var dataArr = new float[0];
    //            var dataNumber = 0;
    //            var scanType = -1f;
    //            var baseMaterialWeight = 0f;
    //            var _targetValueA = 0f;
    //            var _targetValueB = 0f;
    //            var _toleranceA = 0f;
    //            var _toleranceB = 0f;

    //            dataArr = weight.WEIGHTDATAS.ToList().ConvertAll(o => (float)o).ToArray();
    //            dataNumber = (int)weight.SCANCOUNT;
    //            baseMaterialWeight = (float)weight.BASEVALUE;
    //            try
    //            {
    //                // 一人多机专用协议字段
    //                if (weight.DEVICENAME == "BV") return;
    //                var wmc = CCTBConfig.WeightMessage.WeightMessagConfigLis.Single((WeightMessageConfig o) => o.IsEnable && o.MessageTag2 == weight.DEVICENAME);
    //                lock (this)
    //                {
    //                    if (wmc.SideType == ESideType.A)
    //                    {
    //                        targetValue_A = (float)weight.TARGET_VALUE;
    //                        toleranceA = (float)weight.TECHNIQUE;
    //                    }
    //                    else if (wmc.SideType == ESideType.B)
    //                    {
    //                        targetValue_B = (float)weight.TARGET_VALUE - targetValue_A;
    //                        toleranceB = (float)weight.TECHNIQUE;
    //                    }
    //                    _targetValueA = targetValue_A;
    //                    _targetValueB = targetValue_B;
    //                    _toleranceA = toleranceA;
    //                    _toleranceB = toleranceB;
    //                    _logger.Info(string.Format("报文标识[{0}] -- 数据长度[{1}]", weight.DEVICENAME, weight.ZONENUM), isAddCore: false);
    //                }
    //                GetWeightDataCmd(gatherTime, weight.DEVICENAME, dataArr, wmc.SideType, wmc.WeightSourceType, baseMaterialWeight, _targetValueA, _targetValueB, _toleranceA, _toleranceB, dataNumber, scanType);
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Warn($"解析异常的报文[{JsonConvert.SerializeObject(weight)}]", false);
    //                throw new Exception($"未查询到与报文标识[{weight.DEVICENAME}]唯一匹配的报文配置！", ex);
    //            }
    //        }

    //        /// <summary>
    //        /// 发送并等待接收
    //        /// </summary>
    //        /// <typeparam name="TIn"></typeparam>
    //        /// <param name="deviceName"></param>
    //        /// <param name="messageSequence"></param>
    //        /// <param name="commandName"></param>
    //        /// <param name="data"></param>
    //        /// <returns></returns>
    //        private List<object> ClientSendAndRecieveData<TIn>(string clientName, int messageSequence, string commandName, TIn data, int timeOut = 5000)
    //        {
    //            var reLis = new List<object>();
    //            var strRec = string.Empty;
    //            try
    //            {
    //                var client = GetSocketClient(clientName);
    //                if (client == null || !client.IsAlive())
    //                {
    //                    throw new Exception($"[{clientName}]设备未连接！");
    //                }

    //                var pdStr = TBDHProtocolData.ProtocolSplicing(messageSequence, commandName, JsonConvert.SerializeObject(data));
    //                var sw = new Stopwatch();
    //                sw.Start();
    //                var bufSend = Encoding.UTF8.GetBytes(pdStr);
    //                client.RecieveTimeOut = timeOut;
    //                var bufRec = client.SendAndRecieve(bufSend);
    //                strRec = Encoding.UTF8.GetString(bufRec);
    //                sw.Stop();
    //                _logger.Info($"TCP发送并接收耗时[{sw.ElapsedMilliseconds}]ms,发送报文[{pdStr}]", false);
    //                var pdLis = TBDHProtocolData.ProtocolAnalysis(strRec);
    //                foreach (var pd in pdLis)
    //                {
    //                    if (!pd.AnalysisFlag)
    //                    {
    //                        _logger.Error(clientName + "报文解析失败");
    //                        continue;
    //                    }
    //                    if (!(!pd.NeedCrcCheck || pd.DataCrcFlag))
    //                    {
    //                        _logger.Error($"{clientName}[{pd.CommandName}]CRC[{pd.DataCrc}]校验失败");
    //                        continue;
    //                    }
    //                    object obj = null;
    //                    switch (commandName)
    //                    {
    //                        case "TINFOREUQEST":
    //                            if (pd.CommandName != "TINFOREPONSE")
    //                            {
    //                                _logger.Error($"{clientName}[{commandName}]接受指令[{pd.CommandName}]非[{commandName}]!");
    //                                continue;
    //                            }
    //                            obj = JsonConvert.DeserializeObject<TInfoReponseData>(pd.DataJson);
    //                            break;
    //                        case "TADJUSTREQUEST":
    //                            if (pd.CommandName != "TADJUSTREPONSE")
    //                            {
    //                                _logger.Error($"{clientName}[{commandName}]接受指令[{pd.CommandName}]非[{commandName}]!");
    //                                continue;
    //                            }
    //                            obj = JsonConvert.DeserializeObject<TAdjustReponseData>(pd.DataJson);
    //                            break;
    //                        case "MADJUSTREQUEST":
    //                            if (pd.CommandName != "MADJUSTREPONSE")
    //                            {
    //                                _logger.Error($"{clientName}[{commandName}]接受指令[{pd.CommandName}]非[{commandName}]!");
    //                                continue;
    //                            }
    //                            obj = JsonConvert.DeserializeObject<MAdjustReponseData>(pd.DataJson);
    //                            break;
    //                    }
    //                    if (obj != null) reLis.Add(obj);
    //                }

    //                if (reLis.Count <= 0) throw new Exception();
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Warn($"解析异常的报文[{strRec}]!");
    //                _logger.Error($"[{clientName}]报文解析异常!", ex);
    //            }
    //            return reLis;
    //        }
    //        #endregion
    //        #endregion
    //        #endregion

    //        #region 上位机：客户端
    //        #region 测重报文处理
    //        #region 报文发送接收方法
    //        /// <summary>
    //        /// 涂布重量指令
    //        /// </summary>
    //        /// <param name="buffer"></param>
    //        private void GetWeightCmd(MessagConfig weightMC)
    //        {
    //            if (!weightMC.IsEnable) return;
    //            var bufSend = BRProtocolData.ProtocolSplicing(weightMC.BrCmd, null);
    //            var description = weightMC.MessageDescribe;
    //            if (bufSend?.Length <= 0) return;

    //            var clientName = "涂布测重";
    //            var client = GetSocketClient(clientName);
    //            if (client == null || !client.IsAlive())
    //            {
    //                throw new Exception($"[{clientName} {client?.ClientIP?.ToString()}]设备未连接！");
    //            }

    //            var sw = new Stopwatch();
    //            sw.Start();
    //            _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]开始发送报文[{sw.ElapsedMilliseconds}ms]：描述[{description}] —— IP[{client.ClientIP.ToString()}] —— 报文[{BitConverter.ToString(bufSend)}]", false);
    //            var bufRec = client.SendAndRecieve(bufSend);
    //            WeightMessageSinkEnqueue_TcpClient(client.ClientIP.ToString(), bufRec);
    //            sw.Stop();
    //            _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]结束发送报文[{sw.ElapsedMilliseconds}ms]：描述[{description}] —— IP[{client.ClientIP.ToString()}]", false);
    //        }

    //        private void WeightMessageSinkEnqueue_TcpClient(string ipEndPointStr, byte[] buf)
    //        {
    //            var weightData = Tuple.Create(DateTime.Now, "TcpClient", ipEndPointStr, buf);
    //            WeightDataEnqueue(weightData);
    //        }

    //        /// <summary>
    //        /// 心跳请求
    //        /// </summary>
    //        private void PLCHeartLoop_Thread()
    //        {
    //            var t = new Thread(() =>
    //            {
    //                while (true)
    //                {
    //                    try
    //                    {
    //                        byte tBVelocityCondition = 0;
    //                        if (!byte.TryParse(UserDefineVariableInfo.DicVariables["TBVelocityCondition"].ToString(), out tBVelocityCondition))
    //                            tBVelocityCondition = 20;
    //                        //系统运行实时速度条件限制
    //                        if (!float.TryParse(CCTBConfig.Velocity.ReadValue(), out float velocity) || velocity < tBVelocityCondition) return;

    //                        // A/B心跳地址写1
    //                        CCTBConfig.BasicConfiguration_MD.SideConfigLis.ForEach(o =>
    //                        {
    //                            var eartLoop = o.PLCAddressConfig.HeartLoopSwitch;
    //                            if (eartLoop?.AddresObj == null) return;
    //                            try
    //                            {
    //                                var re = eartLoop.WriteValue(1);
    //                                if (!re.IsSuccess)
    //                                    _logger.Error($"[{eartLoop.AddressName}]-[{eartLoop.AddressDescribe}]下发结果:[{re.IsSuccess}]-[{re.Message}]", true);
    //                                else
    //                                    _logger.Info($"[{eartLoop.AddressName}]-[{eartLoop.AddressDescribe}]下发结果:[{re.IsSuccess}]-[{re.Message}]", false);
    //                            }
    //                            catch (Exception ex)
    //                            {
    //                                _logger.Error($"[{eartLoop.AddressName}]-[{eartLoop.AddressDescribe}]下发异常:[{ex}]", true);
    //                            }
    //                        });
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error($"[{MethodInfo.GetCurrentMethod().Name}]监听异常", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                    }
    //                    finally
    //                    {
    //                        Thread.Sleep(1000);
    //                    }
    //                }
    //            });
    //            t.IsBackground = true;
    //            t.Start();
    //        }

    //        /// <summary>
    //        /// 监听重量请求
    //        /// </summary>
    //        private void ListenWight_Thread()
    //        {
    //            if (CCTBConfig.CommunicationMode != "TcpClient") return;

    //            _logger.Info($"初始化[TcpClient]监听！");
    //            var t = new Thread(() =>
    //            {
    //                while (true)
    //                {
    //                    try
    //                    {
    //                        _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]系统运行实时速度为[{CCTBConfig.Velocity.AddresObj.DictValue}]", false);
    //                        byte tBVelocityCondition = 0;
    //                        if (!byte.TryParse(UserDefineVariableInfo.DicVariables["TBVelocityCondition"].ToString(), out tBVelocityCondition))
    //                            tBVelocityCondition = 20;
    //                        //系统运行实时速度条件限制
    //                        if (float.Parse(CCTBConfig.Velocity.AddresObj.DictValue) >= tBVelocityCondition)
    //                        {
    //                            // GetData请求-趟数
    //                            GetWeightCmd(CCTBConfig.GetData_Cmd);
    //                            // 操作单请求-基材
    //                            GetWeightCmd(CCTBConfig.OperationSheet_G_Cmd);
    //                        }
    //                        Thread.Sleep(4000);
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error($"[{MethodInfo.GetCurrentMethod().Name}]监听异常！", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                        Thread.Sleep(5000);
    //                    }
    //                }
    //            });
    //            t.IsBackground = true;
    //            t.Start();
    //        }
    //        #endregion
    //        #endregion

    //        #region 报文接收处理
    //        /// <summary>
    //        /// 获取Socket数据
    //        /// </summary>
    //        /// <param name="name"></param>
    //        /// <param name="str"></param>
    //        public void WeightMessageSink_Client(DateTime gatherTime, string ipEndPointStr, byte[] buf)
    //        {
    //            try
    //            {
    //                var sw = new Stopwatch();
    //                sw.Start();
    //                _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]开始处理接收报文[{sw.ElapsedMilliseconds}ms]：设备[{ipEndPointStr}] —— 报文字节长度[{buf.Length}]", false);
    //                if (ipEndPointStr == "涂布测重") WeightMessageParse(gatherTime, buf);
    //                sw.Stop();
    //                _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]结束处理接收报文[{sw.ElapsedMilliseconds}ms]：设备[{ipEndPointStr}]", false);
    //            }
    //            catch (Exception ex)
    //            {
    //                throw new Exception($"[{MethodInfo.GetCurrentMethod().Name}]报文接收异常！", ex);
    //            }
    //        }

    //        #region 测重报文解析逻辑
    //        /// <summary>
    //        /// 重量报文请求头
    //        /// </summary>
    //        float[] ADValue;
    //        /// <summary>
    //        /// 趟数
    //        /// </summary>
    //        int[] DataNumber;
    //        /// <summary>
    //        /// 扫描类型
    //        /// </summary>
    //        float[] ScanType;
    //        float[] direction;
    //        float[] alarm;
    //        float[] start;
    //        float[] RadioactiveSource;
    //        float[] areaNumber;
    //        /// <summary>
    //        /// 基材值
    //        /// </summary>
    //        float baseMaterialWeight;
    //        float targetValue_A;
    //        float targetValue_B;
    //        float toleranceA;
    //        float toleranceB;

    //        /// <summary>
    //        /// 获取涂布接口报文
    //        /// </summary>
    //        /// <param name="buffer"></param>
    //        private void WeightMessageParse(DateTime gatherTime, byte[] buffer)
    //        {
    //            try
    //            {
    //                _logger.Info("正在处理Client重量数据！", false);
    //                var pdLis = BRProtocolData.ProtocolAnalysis(buffer, false);
    //                foreach (var pd in pdLis)
    //                {
    //                    if (!pd.AnalysisFlag)
    //                    {
    //                        _logger.Error($"报文解析失败");
    //                        continue;
    //                    }
    //                    if (pd.NeedCrcCheck && !pd.DataCrcFlag)
    //                    {
    //                        _logger.Error($"[{pd.BrAttachment}]CRC[{pd.BrCRC}]校验失败");
    //                        continue;
    //                    }

    //                    //获取数据Float
    //                    var dataArr = Utils.TransFloat(pd.BrData, 0, pd.BrDLen / 4);
    //                    // 报文解析后打log
    //                    _logger.Info($"报文标识[{pd.BrAttachment}] -- 数据长度[{pd.BrDLen}]", false);
    //                    // “G”为惠州、南京的趟数标识；“N”为南昌的趟数标识；“O”为基材标识
    //                    if (Nullable.Equals(pd.BrAttachment, CCTBConfig.GetData_Cmd.MessageTag.ToUpper()))  // 趟数报文处理
    //                    {
    //                        GetDataNumberCmd(dataArr, CCTBConfig.DataNumberCount);
    //                    }
    //                    else if (Nullable.Equals(pd.BrAttachment, CCTBConfig.OperationSheet_G_Cmd.MessageTag.ToUpper()))   // 基材报文处理
    //                    {
    //                        GetBaseMaterialCmd(dataArr);
    //                    }
    //                    else// 箔材、重量数据报文处理
    //                    {
    //                        try
    //                        {
    //                            // 获取与报文头标识符合的重量报文数据配置
    //                            var wmc = CCTBConfig.WeightMessage.WeightMessagConfigLis.Single(o => o.IsEnable && o.MessageTag == pd.BrAttachment);

    //                            // 用于测试
    //                            if (wmc?.IsMMData ?? false)
    //                                _logger.Info($"毫米趟数对比(GetData趟数[{ScanType[wmc.DataNumberIndex]}])毫米趟数[{dataArr[0]}]", false);

    //                            // 毫米重量数据的第一位为趟数
    //                            dataArr = wmc?.IsMMData ?? false ? dataArr.Skip(1).Take(dataArr.Length - 1).ToArray() : dataArr;

    //                            // -TODO 毫米重量数据处理方式等待提供方案
    //                            GetWeightDataCmd(gatherTime, pd.BrAttachment, dataArr, wmc.SideType, wmc.WeightSourceType,
    //                                    baseMaterialWeight, targetValue_A, targetValue_B, toleranceA, toleranceB,
    //                                    DataNumber[wmc.DataNumberIndex], ScanType[wmc.DataNumberIndex]);
    //                        }
    //                        catch (Exception ex)
    //                        {
    //                            throw new Exception($"依据解析的报文标识[{pd.BrAttachment}]查询唯一匹配的报文配置发生异常:", ex);
    //                        }
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Warn($"解析异常的报文[{string.Join(",", buffer)}]", false);
    //                _logger.Error("涂布报文解析异常：", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //        }

    //        /// <summary>
    //        /// 根据GetData返回的报文
    //        /// </summary>
    //        /// <param name="data"></param>
    //        private void GetBaseMaterialCmd(float[] data)
    //        {
    //            try
    //            {
    //                _logger.Info("正在处理ClientGetData(基材)数据！", false);
    //                baseMaterialWeight = data[8];
    //                targetValue_A = data[9];
    //                targetValue_B = data[10];
    //                toleranceA = data[12];
    //                toleranceB = data[13];
    //                _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]报文:基材值[{baseMaterialWeight}]-A面设定值[{targetValue_A}]-B面设定值[{targetValue_B}]-A面公差[{toleranceA}]-B面公差[{toleranceB}] —— 数据[{string.Join(",", data)}]", false);
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"[{MethodInfo.GetCurrentMethod().Name}]异常:", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //        }

    //        /// <summary>
    //        /// 操作单报文解析
    //        /// </summary>
    //        /// <param name="data"></param>
    //        private void GetDataNumberCmd(float[] data, byte rackCount)
    //        {
    //            try
    //            {
    //                _logger.Info("正在处理Client操作单(趟号)数据！", false);
    //                var newDataNumber = new int[rackCount];
    //                newDataNumber = data.Skip(4 + 2 * rackCount).Take(rackCount).ToList().ConvertAll(o => (int)o).ToArray();
    //                ScanType = data.Skip(4 + 3 * rackCount).Take(rackCount).ToArray();

    //                areaNumber = data.Skip(3).Take(1).ToArray();
    //                ADValue = data.Skip(4).Take(rackCount).ToArray();
    //                direction = data.Skip(5 + 7 * rackCount).Take(rackCount).ToArray();
    //                alarm = data.Skip(5 + 8 * rackCount).Take(rackCount).ToArray();
    //                start = data.Skip(5 + 5 * rackCount).Take(2 * rackCount).ToArray();
    //                RadioactiveSource = data.Skip(7 + 9 * rackCount).Take(rackCount).ToArray();

    //                _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]报文:上次趟数[{(DataNumber?.Length > 0 ? string.Join(",", DataNumber) : "NULL")}]" +
    //                    $"新趟数[{(newDataNumber?.Length > 0 ? string.Join(",", newDataNumber) : "NULL")}] —— 数据[{string.Join(",", data)}]", false);

    //                // 循环重量数据报文配置
    //                CCTBConfig.WeightMessage.WeightMessagConfigLis.ForEach(o =>
    //                {
    //                    if (DataNumber == null)
    //                    {
    //                        GetWeightCmd(o);
    //                        return;
    //                    }

    //                    // 若未启用
    //                    if (!o.IsEnable) return;
    //                    // 确认配置索引在趟数长度配置范围内
    //                    if (o.DataNumberIndex >= CCTBConfig.DataNumberCount)
    //                        throw new Exception($"[{o.MessageDescribe}]-[{o.MessageTag}]设置索引[{o.DataNumberIndex}]大于等于趟数长度设置[{CCTBConfig.DataNumberCount}]");

    //                    // 如果对应趟数发生变化则发送请求---获取箔材/涂布重量
    //                    if (DataNumber[o.DataNumberIndex] != newDataNumber[o.DataNumberIndex]) GetWeightCmd(o);
    //                });
    //                DataNumber = newDataNumber;
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"[{MethodInfo.GetCurrentMethod().Name}]处理异常：", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //            #region GetData解析
    //            //1、测量数据报警状态（0表示无报警，1表示有报警）（30 - 34是这个信号的细分，此信号仍然要提供）
    //            //2、其余报警状态（0表示无报警，1表示有报警）
    //            //3、软件可工作状态（0表示没有正常测量，1表示在正常测量（停机自动停止扫描的情况下，一走带即可继续测量的情况，也算是正常测量））
    //            //4、当前分区数
    //            //5、1#空气AD值
    //            //6、2#空气AD值（不能提供时给0）
    //            //7、3#空气AD值（不能提供时给0）
    //            //8、4#空气AD值（不能提供时给0）
    //            //9、5#空气AD值（不能提供时给0）
    //            //10、1#样本AD值
    //            //11、2#样本AD值（不能提供时给0）
    //            //12、3#样本AD值（不能提供时给0）
    //            //13、4#样本AD值（不能提供时给0）
    //            //14、5#样本AD值（不能提供时给0）
    //            //15、1#当前扫描趟数
    //            //16、2#当前扫描趟数（不能提供时给0）
    //            //17、3#当前扫描趟数（不能提供时给0）
    //            //18、4#当前扫描趟数（不能提供时给0）
    //            //19、5#当前扫描趟数（不能提供时给0）
    //            //20、1#当前扫描类型（0为未扫描，1为同步扫描，2为自由扫描）
    //            //21、2#当前扫描类型（不能提供时给0）
    //            //22、3#当前扫描类型（不能提供时给0）
    //            //23、4#当前扫描类型（不能提供时给0）
    //            //24、5#当前扫描类型（不能提供时给0）
    //            //25、1#当前机器状态（此状态码是测量软件给我的，每个状态码表示一种状态）
    //            //26、2#当前机器状态（不能提供时给0）
    //            //27、3#当前机器状态（不能提供时给0）
    //            //28、4#当前机器状态（不能提供时给0）
    //            //29、5#当前机器状态（不能提供时给0）
    //            //30、扫描速度（系统设置中的）（不能提供时给0）
    //            //31、1#行程起点（系统设置中的）（不能提供时给0）
    //            //32、1#行程终点（系统设置中的）（不能提供时给0）
    //            //33、2#行程起点（系统设置中的）（不能提供时给0）
    //            //34、2#行程终点（系统设置中的）（不能提供时给0）
    //            //35、3#行程起点（系统设置中的）（不能提供时给0）
    //            //36、3#行程终点（系统设置中的）（不能提供时给0）
    //            //37、4#行程起点（系统设置中的）（不能提供时给0）
    //            //38、4#行程终点（系统设置中的）（不能提供时给0）
    //            //39、5#行程起点（系统设置中的）（不能提供时给0）
    //            //40、5#行程终点（系统设置中的）（不能提供时给0）
    //            //41、1#扫描方向（扫描趟数对应的扫描方向，0为正向，1为反向）（不能提供时给0）
    //            //42、2#扫描方向（扫描趟数对应的扫描方向，0为正向，1为反向）（不能提供时给0）
    //            //43、3#扫描方向（扫描趟数对应的扫描方向，0为正向，1为反向）（不能提供时给0）
    //            //44、4#扫描方向（扫描趟数对应的扫描方向，0为正向，1为反向）（不能提供时给0）
    //            //45、5#扫描方向（扫描趟数对应的扫描方向，0为正向，1为反向）（不能提供时给0）
    //            //46、1#测量结果异常报警（0表示无报警，1表示有报警）（不能提供时给0）
    //            //47、2#测量结果异常报警（0表示无报警，1表示有报警）（不能提供时给0）
    //            //48、3#测量结果异常报警（0表示无报警，1表示有报警）（不能提供时给0）
    //            //49、4#测量结果异常报警（0表示无报警，1表示有报警）（不能提供时给0）
    //            //50、5#测量结果异常报警（0表示无报警，1表示有报警）（不能提供时给0）
    //            //51、系统设置中，其他设置下的“涂布速度”（不能提供时给0）
    //            //52、系统设置中，“膜片宽度”（不能提供时给0）
    //            //53、1#放射源开启状态（0为关闭，1为开启）（不能提供时给0）
    //            //54、2#放射源开启状态（0为关闭，1为开启）（不能提供时给0）
    //            //55、3#放射源开启状态（0为关闭，1为开启）（不能提供时给0）
    //            //56、4#放射源开启状态（0为关闭，1为开启）（不能提供时给0）
    //            //57、5#放射源开启状态（0为关闭，1为开启）（不能提供时给0）
    //            #endregion
    //        }

    //        /// <summary>
    //        /// 获取重量float[]
    //        /// </summary>
    //        /// <param name="rackNumber">机架号</param>
    //        /// <param name="data">数据</param>
    //        private void GetWeightDataCmd(DateTime gatherTime, string messageTag, float[] data, ESideType eSideType, EWeightDataType eWeightSource,
    //            float baseMaterialWeight, float targetValue_A, float targetValue_B, float toleranceA, float toleranceB,
    //            int dataNumber, float scanType)
    //        {
    //            try
    //            {
    //                var dataStr = string.Join(",", data).Replace("1000000", "0");
    //                _logger.Info($"[{MethodInfo.GetCurrentMethod().Name}]报文：机架号[{messageTag}] —— 数据：[{dataStr}]", false);
    //                var dataArray = dataStr.Split(',');
    //                var dataFloat = dataArray.Select(x => Convert.ToSingle(x)).ToArray();
    //                var re = GetFilmSn(out string filmSn);

    //                #region 重量数据单独保存
    //                var dicDep = new Dictionary<string, string>();
    //                dicDep.Add("时间", gatherTime.ToString("yyyy-MM-dd HH:mm:ss"));
    //                dicDep.Add("报文标识", messageTag);
    //                dicDep.Add("AB面", eSideType.ToString());
    //                dicDep.Add("数据源类型", eWeightSource.ToString());
    //                dicDep.Add("基材值", baseMaterialWeight.ToString());
    //                dicDep.Add("A面目标值", targetValue_A.ToString());
    //                dicDep.Add("B面目标值", targetValue_B.ToString());
    //                dicDep.Add("A面公差值", toleranceA.ToString());
    //                dicDep.Add("B面公差值", toleranceB.ToString());
    //                dicDep.Add("趟数", dataNumber.ToString());
    //                dicDep.Add("扫描类型", scanType.ToString());
    //                for (int i = 0; i < data?.Count(); i++) dicDep.Add($"重量{i + 1}", data[i].ToString());
    //                var filePath = UserDefineVariableInfo.DicVariables["productionDataSavePath1"].ToString() + $"\\涂布重量数据\\" + DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    //                var fileName = filmSn + $"_{eSideType}.xlsx";

    //                var excelData = Tuple.Create($"{filePath}\\{fileName}", $"{eSideType}-{eWeightSource}", dicDep);
    //                ExcelDataEnqueue(excelData);
    //                #endregion

    //                // 加载闭环开关配置
    //                bool MDSwitch = false, TDSwitch = false, MDTDSwitch = false, ICSwitch = false;
    //                var tBSwitchControlTyoe = UserDefineVariableInfo.DicVariables["TBSwitchControlTyoe"].ToString().Split(',');
    //                if (tBSwitchControlTyoe.Length == 4 && bool.TryParse(tBSwitchControlTyoe[0], out bool mDSwitch) &&
    //                    bool.TryParse(tBSwitchControlTyoe[1], out bool tDSwitch) && bool.TryParse(tBSwitchControlTyoe[2], out bool mDTDSwitch)
    //                     && bool.TryParse(tBSwitchControlTyoe[3], out bool iCSwitch))
    //                {
    //                    MDSwitch = mDSwitch;
    //                    TDSwitch = tDSwitch;
    //                    MDTDSwitch = mDTDSwitch;
    //                    ICSwitch = iCSwitch;
    //                }

    //                var swT = new Stopwatch();
    //                swT.Start();
    //                var tLis = new List<Task>();

    //                if (MDSwitch)// MD涂布闭环
    //                {
    //                    var t1 = Task.Run(() =>
    //                    {
    //                        try
    //                        {
    //                            var sw = new Stopwatch();
    //                            sw.Start();
    //                            _logger.Info($"MD开始异步处理！", false);
    //                            CCTBLogic_MD(gatherTime, messageTag, dataFloat, eSideType, eWeightSource,
    //                                targetValue_A, targetValue_B, baseMaterialWeight, dataNumber, scanType);
    //                            sw.Stop();
    //                            _logger.Info($"MD异步处理完成,耗时[{sw.ElapsedMilliseconds}]！", false);
    //                        }
    //                        catch (Exception ex)
    //                        {
    //                            _logger.Error($"MD异常：", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                        }
    //                    }); tLis.Add(t1);
    //                }

    //                if (TDSwitch)// TD涂布闭环
    //                {
    //                    var t2 = Task.Run(() =>
    //                    {
    //                        try
    //                        {
    //                            var sw = new Stopwatch();
    //                            sw.Start();
    //                            _logger.Info($"TD开始异步处理！", false);
    //                            CCTBLogic_TD(gatherTime, messageTag, dataFloat, eSideType, eWeightSource,
    //                                targetValue_A, targetValue_B, baseMaterialWeight, dataNumber, scanType);
    //                            sw.Stop();
    //                            _logger.Info($"TD异步处理完成,耗时[{sw.ElapsedMilliseconds}]！", false);
    //                        }
    //                        catch (Exception ex)
    //                        {
    //                            _logger.Error($"TD异常：", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                        }
    //                    });
    //                    tLis.Add(t2);
    //                }

    //                if (MDTDSwitch)// MDTD涂布闭环
    //                {
    //                    var t3 = Task.Run(() =>
    //                    {
    //                        try
    //                        {
    //                            var sw = new Stopwatch();
    //                            sw.Start();
    //                            _logger.Info($"MDTD开始异步处理！", false);
    //                            CCTBLogic_MDTD(gatherTime, messageTag, dataFloat, eSideType, eWeightSource,
    //                                targetValue_A, targetValue_B, toleranceA, toleranceB, baseMaterialWeight, dataNumber, scanType);
    //                            sw.Stop();
    //                            _logger.Info($"MDTD异步处理完成,耗时[{sw.ElapsedMilliseconds}]！", false);
    //                        }
    //                        catch (Exception ex)
    //                        {
    //                            _logger.Error($"MDTD异常：", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                        }
    //                    });
    //                    tLis.Add(t3);
    //                }

    //                if (ICSwitch)// 涂布闭环防呆
    //                {
    //                    //var t4 = Task.Run(() =>
    //                    //{
    //                    //    try
    //                    //    {
    //                    //        var sw = new Stopwatch();
    //                    //        sw.Start();
    //                    //        _logger.Info($"防呆开始异步处理！", false);
    //                    //        CCTBLogic_InterceptCache(gatherTime, messageTag, dataFloat, eSideType, eWeightSource,
    //                    //            targetValue_A, targetValue_B, baseMaterialWeight, dataNumber, scanType);
    //                    //        sw.Stop();
    //                    //        _logger.Info($"防呆异步处理完成,耗时[{sw.ElapsedMilliseconds}]！", false);
    //                    //    }
    //                    //    catch (Exception ex)
    //                    //    {
    //                    //        _logger.Error($"涂布防呆异常：", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                    //    }
    //                    //});
    //                    //tLis.Add(t4);
    //                }

    //                // 异步等待
    //                if (tLis?.Count > 0)
    //                    Task.WaitAll(tLis.ToArray());

    //                tLis.ForEach(t => { t?.Dispose(); t = null; });
    //                swT.Stop();
    //                _logger.Info($"报文异步处理完成，耗时[{swT.ElapsedMilliseconds}]", false);
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"[{MethodInfo.GetCurrentMethod().Name}]处理异常：", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //        }
    //        #endregion
    //        #endregion
    //        #endregion
    //        #endregion
    //    }
    //    #endregion

    //    #region MD闭环模块
    //    /// <summary>
    //    /// MD闭环模块
    //    /// </summary>
    //    partial class VM_CCTBMonitoringPage : ViewModelBase
    //    {
    //        /// <summary>
    //        /// MD逻辑方法
    //        /// </summary>
    //        /// <param name="lastGatherTime"></param>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="messageTag"></param>
    //        /// <param name="data"></param>
    //        /// <param name="eSideType"></param>
    //        /// <param name="eWeightSource"></param>
    //        /// <param name="targetValue_A"></param>
    //        /// <param name="targetValue_B"></param>
    //        /// <param name="baseMaterialWeight"></param>
    //        /// <param name="dataNumber"></param>
    //        /// <param name="scanType"></param>
    //        /// <exception cref="Exception"></exception>
    //        private void CCTBLogic_MD(DateTime gatherTime, string messageTag, float[] data, ESideType eSideType, EWeightDataType eWeightSource,
    //            float targetValue_A, float targetValue_B, float baseMaterialWeight, int dataNumber, float scanType)
    //        {
    //            try
    //            {
    //                // 获取A/B面基础配置
    //                var sc = CCTBConfig.BasicConfiguration_MD.SideConfigLis.Find(o => o.SideType == eSideType);
    //                // 加载涂布闭环配置xml文件路径
    //                var path = UserDefineVariableInfo.DicVariables["CCTBConfigPath"].ToString();
    //                // 加载A/B面配置文件
    //                var CCTBModelItem = Sunwoda_XmlHelper.XmlToModel<CCTBMDModel>(path, sc.ConfigFileName);
    //                var cctbModels = CCTBMDModel.GetCache(sc.SideType);
    //                // 上次采集时间
    //                var lastGatherTime = cctbModels?.Count() > 0 ? cctbModels.Last().GatherTime : DateTime.MinValue;
    //                CCTBModelItem.LastGatherTime = lastGatherTime;
    //                // 本次采集时间
    //                CCTBModelItem.GatherTime = gatherTime;
    //                // 报文标识
    //                CCTBModelItem.MessageTag = messageTag;

    //                // 如果获取的重量数据与配置使用数据源一致则进行运算
    //                if (eWeightSource != CCTBModelItem.WeightSource)
    //                    return;
    //                // 报文内目标设定值
    //                switch (eSideType)
    //                {
    //                    case ESideType.A:
    //                        CCTBModelItem.DynamicTargetValue = targetValue_A;
    //                        break;
    //                    case ESideType.B:
    //                        CCTBModelItem.DynamicTargetValue = targetValue_A + targetValue_B;
    //                        break;
    //                    default:
    //                        _logger.Warn($"未实现[{eSideType}]的动态目标设定值配置！");
    //                        break;
    //                }
    //                // 基材值
    //                CCTBModelItem.BaseMaterialValue = baseMaterialWeight;
    //                // 目标值配置
    //                switch (CCTBModelItem.WeightSource)
    //                {
    //                    case EWeightDataType.干膜净重分区数据:
    //                    case EWeightDataType.湿膜净重分区数据:
    //                        {
    //                            CCTBModelItem.DataArr = data;
    //                            if (CCTBModelItem.IsUseDynamicTargetValue)
    //                                CCTBModelItem.TValue = CCTBModelItem.DynamicTargetValue + CCTBModelItem.TOffset;
    //                            else
    //                                CCTBModelItem.TValue = CCTBModelItem.TChange + CCTBModelItem.TOffset;
    //                            break;
    //                        }
    //                    case EWeightDataType.干膜净重毫米数据:
    //                    case EWeightDataType.湿膜净重毫米数据:
    //                        {
    //                            CCTBModelItem.DataArr = PartitionedData(EClosedLoopType.MD, data);
    //                            if (CCTBModelItem.IsUseDynamicTargetValue)
    //                                CCTBModelItem.TValue = CCTBModelItem.DynamicTargetValue + CCTBModelItem.TOffset;
    //                            else
    //                                CCTBModelItem.TValue = CCTBModelItem.TChange + CCTBModelItem.TOffset;
    //                            break;
    //                        }
    //                    case EWeightDataType.干膜总重分区数据:
    //                    case EWeightDataType.湿膜总重分区数据:
    //                        {
    //                            CCTBModelItem.DataArr = data;
    //                            if (CCTBModelItem.IsUseDynamicTargetValue)
    //                                CCTBModelItem.TValue = CCTBModelItem.BaseMaterialValue + CCTBModelItem.DynamicTargetValue + CCTBModelItem.TOffset;
    //                            else
    //                                CCTBModelItem.TValue = CCTBModelItem.BaseMaterialValue + CCTBModelItem.TChange + CCTBModelItem.TOffset;
    //                            break;
    //                        }
    //                    case EWeightDataType.干膜总重毫米数据:
    //                    case EWeightDataType.湿膜总重毫米数据:
    //                        {
    //                            CCTBModelItem.DataArr = PartitionedData(EClosedLoopType.MD, data);
    //                            if (CCTBModelItem.IsUseDynamicTargetValue)
    //                                CCTBModelItem.TValue = CCTBModelItem.BaseMaterialValue + CCTBModelItem.DynamicTargetValue + CCTBModelItem.TOffset;
    //                            else
    //                                CCTBModelItem.TValue = CCTBModelItem.BaseMaterialValue + CCTBModelItem.TChange + CCTBModelItem.TOffset;
    //                            break;
    //                        }
    //                }
    //                // 趟数
    //                CCTBModelItem.DataNumber = dataNumber;
    //                // 扫描类型
    //                CCTBModelItem.ScanType = scanType == 0 ? "0未扫描" :
    //                    scanType == 1 ? "1同步扫描" :
    //                    scanType == 2 ? "2自由扫描" : scanType.ToString();

    //                // 初始化数据,采集泵速设定值、流量数据
    //                Data_Init(sc, ref CCTBModelItem);
    //                // 剔除异常数据：1、0数据判异规则；2、阈值判异规则
    //                RemoveCCTBModelItem(sc, ref CCTBModelItem);

    //                // 闭环计算
    //                var cCTBMDModel = CCTBCalculte_MD(sc, CCTBModelItem);

    //                // 保存文档
    //                SaveToExcel(sc, cCTBMDModel);

    //                // 做缓存：主键：AB面
    //                CCTBMDModel.SetCache(sc.SideType, cCTBMDModel);
    //            }
    //            catch (Exception ex)
    //            {
    //                throw new Exception($"[MDLogic]涂布MD逻辑异常！", ex);
    //            }
    //        }

    //        /// <summary>
    //        /// 初始化闭环逻辑参数
    //        /// </summary>
    //        /// <param name="sideConfig">AB面配置</param>
    //        /// <param name="cCTBModel">数据</param>
    //        private CCTBMDModel CCTBCalculte_MD(SideConfigMD sideConfig, CCTBMDModel cCTBModel)
    //        {
    //            var reCCTBModel = cCTBModel;
    //            try
    //            {
    //                var weightList = CCTBMDModel.GetCache(sideConfig.SideType);
    //                #region 缓存数据处理
    //                // 获取开机/重启/进刀(若开启)后的数据，同时剔除开头0数据，同时剔除固定开机数据 —— 具有相同数据源配置
    //                var weightLis_RebootByZero = GetCacheAfterReboot(weightList, reCCTBModel);
    //                // 异常数据间隔之后的数据，且剔除开头异常数据
    //                var weightLis_Filtrate = GetCacheAfterFiltrate(weightLis_RebootByZero, reCCTBModel);
    //                #endregion

    //                if (reCCTBModel.Rid_sJudge == "Y") return reCCTBModel;
    //                if (weightLis_Filtrate.Count <= 0) return reCCTBModel;

    //                // 非剔除数据则继续进行规则计算
    //                // 计算均值及EWMA均值
    //                var weightLis_Caculatation = EWMA(weightLis_Filtrate, ref reCCTBModel);
    //                // 滞后数据之后的数据，滞后起点：(1、下发点；)
    //                var weightLis_RemoveLag = GetCacheAfterLagging(weightLis_Caculatation, reCCTBModel);
    //                // 是否满足滞后条件
    //                if (weightLis_RemoveLag.Count <= 0) return reCCTBModel;

    //                var reLis = new List<Tuple<bool, string>>();
    //                reLis.Add(Judge_Threshold(ref weightLis_RemoveLag));
    //                reLis.Add(Judge_Trend(ref weightLis_RemoveLag));
    //                reLis.Add(Judge_Position(ref weightLis_RemoveLag));
    //                reLis.Add(Judge_Ratio(ref weightLis_RemoveLag));
    //                reLis.Add(Judge_Dynamic(ref weightLis_RemoveLag));
    //                Judge_Finally(reLis.FirstOrDefault(o => o.Item1), sideConfig, weightLis_RemoveLag, ref reCCTBModel);
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"计算{sideConfig.SideType}面{cCTBModel.MessageTag}架[{cCTBModel?.WeightSource}]数据异常：", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //            return reCCTBModel;
    //        }

    //        /// <summary>
    //        /// 滞后数据之后的数据，滞后起点：(1、最后下发点)
    //        /// </summary>
    //        /// <param name="dataLis"></param>
    //        /// <param name="cCTBModel"></param>
    //        /// <returns></returns>
    //        private List<CCTBMDModel> GetCacheAfterLagging(List<CCTBMDModel> dataLis, CCTBMDModel cCTBModel)
    //        {
    //            // 找到滞后起点(已触发调节)
    //            var laggingStartIndex = dataLis.FindLastIndex(o => o.JudgTrigger == "Y");
    //            if (laggingStartIndex < 0) return dataLis;

    //            // 去掉滞后起点之前的数据
    //            var laggingEndIndex = laggingStartIndex + cCTBModel.Lag_gConditiont;
    //            var index = laggingEndIndex < dataLis.Count ? laggingEndIndex : dataLis.Count - 1;
    //            return dataLis.Skip(index).ToList();
    //        }

    //        /// <summary>
    //        /// 滞后判断
    //        /// 备注：
    //        ///     数据滞后包含异常数据
    //        /// </summary>
    //        /// <param name="dataLis"></param>
    //        /// <returns></returns>
    //        private bool Judge_Lagging(ref List<CCTBMDModel> dataLis)
    //        {
    //            //滞后判断
    //            if (dataLis == null || dataLis.Count <= 0) return false;

    //            var cCTBModel = dataLis.Last();
    //            var index = 0;
    //            for (int i = 0; i < dataLis.Count; i++)
    //            {
    //                index = dataLis.Count - 1 - i;
    //                if (i < cCTBModel.Lag_gConditiont + 1 && //滞后数据范围内
    //                    dataLis[index].JudgTrigger == "Y")// 已触发调节
    //                {
    //                    return true;
    //                }
    //            }
    //            return false;
    //        }

    //        /// <summary>
    //        /// 获取新泵速
    //        /// </summary>
    //        private bool GetNewB(bool isFluPid, SideConfigMD sideConfig, List<CCTBMDModel> dataLis, ref CCTBMDModel cCTBModel)
    //        {
    //            _logger.Info($"{sideConfig.SideType}面触发规则：" + cCTBModel.Judg_HandelType, false);

    //            // 剔除异常数据
    //            var dl = dataLis?.FindAll(o => o?.Rid_sJudge == "N")?.ToList();

    //            var cCTBModelCount = isFluPid ? cCTBModel.Lag_gConditiont + 9 : 3;
    //            // 流量PID计算时，实际滞后数据包含滞后趟数个数+采样个数 --By 2210120078 20250331
    //            var cCTBModelCacheCount = dl.Exists(o => o.JudgTrigger == "Y") ? (dl?.Count - (dl?.LastIndexOf(dl?.Where(o => o?.JudgTrigger == "Y")?.Last()) + 1)) : dl?.Count;
    //            if (cCTBModelCacheCount < cCTBModelCount)
    //            {
    //                cCTBModel.JudgTrigger = "N";
    //                cCTBModel.Judg_HandelResults = $"泵速判定拦截:缓存数据不足[{cCTBModelCount}]";
    //                return false;
    //            }
    //            if (isFluPid)// 流量PID
    //            {
    //                var newBTuple = GetFluPidNewB(sideConfig, dl, ref cCTBModel);
    //                if (newBTuple.Item1 == "N")
    //                {
    //                    cCTBModel.JudgTrigger = "N";
    //                    cCTBModel.Judg_HandelResults = $"流量趋势判定拦截";
    //                    return false;
    //                }
    //                cCTBModel.JudgFluTrend_Result = newBTuple.Item1;
    //                cCTBModel.NewB[EPump.泵0] = newBTuple.Item2.ToString("0.00");
    //                cCTBModel.NewB[EPump.泵1] = newBTuple.Item3.ToString("0.00");
    //            }
    //            else// 专家PID
    //            {
    //                var newBTuple = GetExpertPidNewB(sideConfig, dl, ref cCTBModel);
    //                cCTBModel.NewB[EPump.泵0] = newBTuple.Item1.ToString("0.00");
    //                cCTBModel.NewB[EPump.泵1] = newBTuple.Item2.ToString("0.00");
    //            }

    //            var _cCTBModel = cCTBModel;
    //            foreach (var pumbInfo in sideConfig.PLCAddressConfig.PLCAddressLis)
    //            {
    //                if (pumbInfo == null || !pumbInfo.IsEnable || !_cCTBModel.NewB.ContainsKey(pumbInfo.Pump)
    //                    || !_cCTBModel.SetB.ContainsKey(pumbInfo.Pump)) continue;
    //                if (!decimal.TryParse(_cCTBModel.NewB[pumbInfo.Pump], out decimal newB)) continue;
    //                if (!decimal.TryParse(_cCTBModel.SetB[pumbInfo.Pump], out decimal setB)) continue;

    //                var f = newB - setB;
    //                var a = setB * (decimal)_cCTBModel.JudgSpeed_Range;
    //                cCTBModel.AdjustValue[pumbInfo.Pump] = float.Parse((f * (decimal)_cCTBModel.SetB_Coefficient).ToString("0.00"));
    //                // 泵速上下限限制
    //                if (Math.Abs(f) > Math.Abs(a))
    //                {
    //                    var _a = f > 0 ? (0 + Math.Abs(a)) : (0 - Math.Abs(a));
    //                    cCTBModel.AdjustValue[pumbInfo.Pump] = float.Parse((_a * (decimal)_cCTBModel.SetB_Coefficient).ToString("0.00"));
    //                }
    //            }

    //            // 如果都为0，则不调节
    //            if (cCTBModel.AdjustValue.ToList().All(o => o.Value == 0))
    //            {
    //                _cCTBModel.JudgTrigger = "N";
    //                _cCTBModel.Judg_HandelResults = "泵速判定拦截";
    //                return false;
    //            }
    //            return true;
    //        }

    //        /// <summary>
    //        /// A面调机结果缓存队列
    //        /// </summary>
    //        private Queue<Tuple<DateTime, List<float>>> jjAQue = new Queue<Tuple<DateTime, List<float>>>();

    //        /// <summary>
    //        /// 流量PID算法获取新泵速
    //        /// </summary>
    //        private (float, float) GetExpertPidNewB(SideConfigMD sideConfig, List<CCTBMDModel> dataLis, ref CCTBMDModel cCTBModel)
    //        {
    //            var eTBPython = EArithmeticType.专家PID;
    //            var sw = new Stopwatch();
    //            sw.Start();
    //            var rePumpSpeed1 = float.TryParse(cCTBModel.SetB[EPump.泵0], out float pumpSpeed1); //当前时刻浆料泵速-上泵
    //            var rePumpSpeed2 = float.TryParse(cCTBModel.SetB[EPump.泵1], out float pumpSpeed2); //当前时刻浆料泵速-下泵
    //            var _cCTBModel = cCTBModel;
    //            var jj = (pumpSpeed1, pumpSpeed2);
    //            // 算法执行
    //            var func = new Func<string, (float, float)?>((pythonCode) =>
    //            {
    //                try
    //                {
    //                    var tarVal = (double)_cCTBModel.TValue;   //涂布重量目标值
    //                    var curVal1 = dataLis[dataLis.Count - 1].GetEWMA_Avg(_cCTBModel.IsUseFluAndDataValue); //当前时刻涂布重量均值
    //                    var curVal2 = dataLis[dataLis.Count - 2].GetEWMA_Avg(_cCTBModel.IsUseFluAndDataValue); //上一时刻涂布重量均值
    //                    var curVal3 = dataLis[dataLis.Count - 3].GetEWMA_Avg(_cCTBModel.IsUseFluAndDataValue); //上上一时刻涂布重量均值
    //                    var pyCode = "";
    //                    lock (PidFileInfoDic) pyCode = PidFileInfoDic[pythonCode].pythonStr;
    //                    float jjA = 0f;
    //                    // B面闭环需要传入A面下发泵速数据   ——By 202210120078
    //                    if (sideConfig.SideType == ESideType.B)
    //                    {
    //                        if (_cCTBModel.VelocitySetting > 0)
    //                        {
    //                            // AB间隔时间
    //                            var timeBetweenAB = _cCTBModel.DistanceBetweenAB / _cCTBModel.VelocitySetting;
    //                            lock (jjAQue)
    //                            {
    //                                if (jjAQue.Count > 0)
    //                                {
    //                                    while ((_cCTBModel.GatherTime - jjAQue.Peek().Item1).TotalSeconds > timeBetweenAB)
    //                                    {
    //                                        var jjaObj = jjAQue.Dequeue();
    //                                        // 若A面调机数据在允许的误差时间内
    //                                        if ((_cCTBModel.GatherTime - jjaObj.Item1).TotalSeconds < _cCTBModel.LimitTimeB)
    //                                            jjA = (float)jjaObj.Item2.Sum();

    //                                        // 继续判断下一个，保证获取到最新的
    //                                        if (jjAQue.Count <= 0) break;
    //                                    }
    //                                }
    //                            }
    //                        }
    //                    }

    //                    _cCTBModel.AdjustValue_A = jjA;
    //                    var jjR = AddExcuteExpertPIDpyAction(pythonCode, pyCode, _cCTBModel.PID_PIDModel, tarVal, curVal1, curVal2, curVal3,
    //                        (pumpSpeed1, pumpSpeed2), (_cCTBModel.FluDataAvgPump1, _cCTBModel.FluDataAvgPump2), (_cCTBModel.FluDataDeltaPump1, _cCTBModel.FluDataDeltaPump2), jjA, null);
    //                    _logger.Info($"{sideConfig.SideType}-{eTBPython}新泵速[{jjR.Item1}、{jjR.Item2}]");
    //                    return (pumpSpeed1 + jjR.Item1, pumpSpeed2 + jjR.Item2);
    //                }
    //                catch (Exception ex)
    //                {
    //                    _logger.Error($"{sideConfig.SideType}-{eTBPython}算法异常", ex);
    //                    return null;
    //                }
    //            });
    //            // 算法获取
    //            var re = RefreshAndExcuteTBPydAction<(float, float)?>(eTBPython, sideConfig, cCTBModel.VarietyName, func);
    //            if (re != null) jj = ((float pumpSpeed1, float pumpSpeed2))re;
    //            sw.Stop();
    //            _logger.Info($"{sideConfig.SideType}-{eTBPython}算法获取新泵速耗时[{sw.ElapsedMilliseconds}]", false);
    //            return jj;
    //        }

    //        /// <summary>
    //        /// 流量PID算法获取新泵速
    //        /// </summary>
    //        private (string, float, float) GetFluPidNewB(SideConfigMD sideConfig, List<CCTBMDModel> dataLis, ref CCTBMDModel cCTBModel)
    //        {
    //            var eTBPython = EArithmeticType.流量PID;
    //            var sw = new Stopwatch();
    //            sw.Start();
    //            var rePumpSpeed1 = float.TryParse(cCTBModel.SetB[EPump.泵0], out float pumpSpeed1); //当前时刻浆料泵速-上泵
    //            var rePumpSpeed2 = float.TryParse(cCTBModel.SetB[EPump.泵1], out float pumpSpeed2); //当前时刻浆料泵速-下泵
    //            var _cCTBModel = cCTBModel;
    //            var jj = ("N", pumpSpeed1, pumpSpeed2);
    //            // 算法执行
    //            var func = new Func<string, (string, float, float)?>((pythonCode) =>
    //            {
    //                try
    //                {
    //                    // 历史重量数据
    //                    // 非0流量数据均值-上泵、非0流量数据均值-下泵、极差、目标值T、数据源EWMA均值、设定泵速处理值-上泵、设定泵速处理值-下泵
    //                    var fluData = "flu_top,flu_bottom,range,target,weight,pump_top,pump_bottom";
    //                    for (int i = 9; i > 0; i--)
    //                    {
    //                        var data = dataLis[dataLis.Count - i];
    //                        fluData += Environment.NewLine;
    //                        fluData += $"{data.FluDataAvgPump1},{data.FluDataAvgPump2},{data.DataRange},{data.TValue}," +
    //                            $"{data.GetEWMA_Avg(_cCTBModel.IsUseFluAndDataValue)},{data.SetB[EPump.泵0]},{data.SetB[EPump.泵1]}";
    //                    }

    //                    var ma = _cCTBModel.EWMA_Count;// EWMA计算的数据个数
    //                    var r = _cCTBModel.DataRange;// 极差
    //                    var allTrend = _cCTBModel.JudgFluTrend_allThreshold;// 流量判定规则-流量和趋势变化累计值的阈值
    //                    var trend = _cCTBModel.JudgFluTrend_singleThreshold;// 流量判定规则-上、下流量趋势变化累计值的阈值
    //                    var trendRule = _cCTBModel.JudgFluTrend_lCount;// 流量判定规则-连续个数
    //                    var pyCode = "";
    //                    lock (PidFileInfoDic) pyCode = PidFileInfoDic[pythonCode].pythonStr;
    //                    float jjA = 0f;
    //                    if (sideConfig.SideType == ESideType.B)
    //                    {
    //                        if (_cCTBModel.VelocitySetting > 0)
    //                        {
    //                            // AB间隔时间
    //                            var timeBetweenAB = _cCTBModel.DistanceBetweenAB / _cCTBModel.VelocitySetting;
    //                            lock (jjAQue)
    //                            {
    //                                if (jjAQue.Count > 0)
    //                                {
    //                                    // 若A面调机数据大于AB面间隔时间
    //                                    while ((_cCTBModel.GatherTime - jjAQue.Peek().Item1).TotalSeconds > timeBetweenAB)
    //                                    {
    //                                        // 取出
    //                                        var jjaObj = jjAQue.Dequeue();
    //                                        // 若A面调机数据在允许的误差时间内
    //                                        if ((_cCTBModel.GatherTime - jjaObj.Item1).TotalSeconds < _cCTBModel.LimitTimeB)
    //                                            jjA = (float)jjaObj.Item2.Sum();

    //                                        // 继续判断下一个，保证获取到最新的
    //                                        if (jjAQue.Count <= 0) break;
    //                                    }
    //                                }
    //                            }
    //                        }
    //                    }

    //                    _cCTBModel.AdjustValue_A = jjA;
    //                    var jjR = AddExcuteFluPIDpyAction(pythonCode, pyCode, (pumpSpeed1, pumpSpeed2),
    //                        fluData, ma, r, allTrend, trend, trendRule, jjA, null, null);
    //                    _logger.Info($"{sideConfig.SideType}-{eTBPython}新泵速[{jjR.Item1}、{jjR.Item2}、{jjR.Item3}]");
    //                    return (jjR.Item1, pumpSpeed1 + jjR.Item2, pumpSpeed2 + jjR.Item3);
    //                }
    //                catch (Exception ex)
    //                {
    //                    _logger.Error($"{sideConfig.SideType}-{eTBPython}算法异常", ex);
    //                    return null;
    //                }
    //            });
    //            // 算法获取
    //            var re = RefreshAndExcuteTBPydAction<(string, float, float)?>(eTBPython, sideConfig, cCTBModel.VarietyName, func);
    //            if (re != null) jj = ((string, float pumpSpeed1, float pumpSpeed2))re;
    //            sw.Stop();
    //            _logger.Info($"{sideConfig.SideType}-{eTBPython}算法获取新泵速耗时[{sw.ElapsedMilliseconds}]", false);
    //            return jj;
    //        }

    //        #region PythonNet
    //        #region 专家PID
    //        /// <summary>
    //        /// 执行专家PID的py文件算法
    //        /// </summary>
    //        /// <param name="pythonCode"></param>
    //        /// <param name="pyCodeStr"></param>
    //        /// <param name="pid">PID配置</param>
    //        /// <param name="tarVal">涂布重量目标值</param>
    //        /// <param name="curVal1">当前时刻涂布重量均值</param>
    //        /// <param name="curVal2">上一时刻涂布重量均值</param>
    //        /// <param name="curVal3">上上一时刻涂布重量均值</param>
    //        /// <param name="pumpSpeed">当前时刻浆料泵速</param>
    //        /// <param name="flu">当前时刻流量均值</param>
    //        /// <param name="jjA">A面上次调机量</param>
    //        /// <returns></returns>
    //        private (float, float) AddExcuteExpertPIDpyAction(string pythonCode, string pyCodeStr, PIDModel pid, double tarVal,
    //            double curVal1, double curVal2, double curVal3, (double, double) pumpSpeed, (double, double) flu, (double, double) FluDataDelta, float jjA = 0, object input_1 = null)
    //        {
    //            var eTBPython = EArithmeticType.专家PID;
    //            var pyResult = (0f, 0f);
    //            var pyKey = Guid.NewGuid().ToString();
    //            try
    //            {
    //                using (var mr = new ManualResetEvent(false))
    //                {
    //                    lock (PyManualResetEventDic)
    //                        PyManualResetEventDic[pyKey] = mr;

    //                    var data = new object[] { pid, tarVal, curVal1, curVal2, curVal3, pumpSpeed, flu, jjA, FluDataDelta, input_1 };
    //                    var func = new Func<dynamic, object, object>((pm, da) =>
    //                    {
    //                        var das = da as object[];
    //                        var _pid = das[0] as PIDModel?;
    //                        var _tarVal = das[1] as double?;
    //                        var _curVal1 = das[2] as double?;
    //                        var _curVal2 = das[3] as double?;
    //                        var _curVal3 = das[4] as double?;
    //                        var _pumpSpeed = das[5] as (double, double)?;
    //                        var _flu = das[6] as (double, double)?;
    //                        var _jjA = das[7] as float?;
    //                        var _FluDataDelta = das[8] as (double, double)?;
    //                        var _input_1 = das[9] as object;
    //                        // python更改为pyd
    //                        //dynamic po = pm.DeltaPID(_tarVal, _curVal1, _curVal2, _curVal3, _pid?.T, _pid?.P, _pid?.I, _pid?.D,
    //                        //_pumpSpeed?.Item1, _pumpSpeed?.Item2, _flu?.Item1, _flu?.Item2, _jjA, _FluDataDelta?.Item1, _FluDataDelta?.Item2, _input_1);
    //                        //dynamic re = po.final_output();
    //                        dynamic re = pm.algorithm_run(_tarVal, _curVal1, _curVal2, _curVal3, _pid?.T, _pid?.P, _pid?.I, _pid?.D,
    //                        _pumpSpeed?.Item1, _pumpSpeed?.Item2, _flu?.Item1, _flu?.Item2, _jjA, _FluDataDelta?.Item1, _FluDataDelta?.Item2, _input_1);
    //                        var reTp = PyTuple.AsTuple(re);
    //                        var pyRes = ((float)reTp[0], (float)reTp[1]);
    //                        return pyRes;
    //                    });
    //                    var obj = new object[] { pyKey, pythonCode, pyCodeStr, data, func };
    //                    PythonHelper.Instance.AddPyhtonAction((ExcuteTBPythonAction, obj));
    //                    mr.WaitOne();
    //                    lock (PyResultDic)
    //                    {
    //                        if (!PyResultDic.ContainsKey(pyKey) || PyResultDic[pyKey] == null)
    //                        {
    //                            if (PyResultDic.ContainsKey(pyKey))
    //                                PyResultDic.Remove(pyKey);

    //                            throw new Exception("算法输出结果异常！");
    //                        }

    //                        pyResult = ((float, float))PyResultDic[pyKey];
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"{eTBPython}算法文件[{pythonCode}]执行异常！", ex);
    //                throw new Exception($"{eTBPython}算法文件[{pythonCode}]执行异常！", ex);
    //            }
    //            finally
    //            {
    //                lock (PyManualResetEventDic)
    //                    if (PyManualResetEventDic.ContainsKey(pyKey))
    //                        PyManualResetEventDic.Remove(pyKey);
    //            }
    //            return pyResult;
    //        }
    //        #endregion
    //        #region 流量PID
    //        /// <summary>
    //        /// 执行流量PID的py文件算法
    //        /// </summary>
    //        /// <param name="pythonCode"></param>
    //        /// <param name="pyCodeStr"></param>
    //        /// <param name="pid">PID配置</param>
    //        /// <param name="tarVal">涂布重量目标值</param>
    //        /// <param name="curVal1">当前时刻涂布重量均值</param>
    //        /// <param name="curVal2">上一时刻涂布重量均值</param>
    //        /// <param name="curVal3">上上一时刻涂布重量均值</param>
    //        /// <param name="pumpSpeed">当前时刻浆料泵速</param>
    //        /// <param name="flu">当前时刻流量均值</param>
    //        /// <param name="jjA">A面上次调机量</param>
    //        /// <returns></returns>
    //        private (string, float, float) AddExcuteFluPIDpyAction(string pythonCode, string pyCodeStr, (double, double) pumpSpeed,
    //            object fluData, int ma, float r, double allTrend, double trend, int trendRule, float single, object input1, object input2)
    //        {
    //            var eTBPython = EArithmeticType.流量PID;
    //            var pyResult = ("N", 0f, 0f);
    //            var pyKey = Guid.NewGuid().ToString();
    //            try
    //            {
    //                using (var mr = new ManualResetEvent(false))
    //                {
    //                    lock (PyManualResetEventDic)
    //                        PyManualResetEventDic[pyKey] = mr;

    //                    var data = new object[] { pumpSpeed, fluData, ma, r, allTrend, trend, trendRule, single, input1, input2 };
    //                    var func = new Func<dynamic, object, object>((pm, da) =>
    //                    {
    //                        var das = da as object[];
    //                        var _pumpSpeed = das[0] as (double, double)?;
    //                        var _fluData = das[1] as object;
    //                        var _ma = das[2] as int?;
    //                        var _r = das[3] as float?;
    //                        var _allTrend = das[4] as double?;
    //                        var _trend = das[5] as double?;
    //                        var _trendRule = das[6] as int?;
    //                        var _single = das[7] as float?;
    //                        var _input1 = das[8] as object;
    //                        var _input2 = das[9] as object;
    //                        // python更改为pyd
    //                        //dynamic po = pm.FluControl(_fluData, _ma, _r, _allTrend, _trend, _trendRule, _single, _input1, _input2);
    //                        //dynamic re = po.determine();
    //                        dynamic re = pm.algorithm_run(_fluData, _ma, _r, _allTrend, _trend, _trendRule, _single, _input1, _input2);
    //                        var reTp = PyTuple.AsTuple(re);
    //                        var pyRes = ((string)reTp[0], (float)reTp[1], (float)reTp[2]);
    //                        return pyRes;
    //                    });
    //                    var obj = new object[] { pyKey, pythonCode, pyCodeStr, data, func };
    //                    PythonHelper.Instance.AddPyhtonAction((ExcuteTBPythonAction, obj));
    //                    mr.WaitOne();
    //                    lock (PyResultDic)
    //                    {
    //                        if (!PyResultDic.ContainsKey(pyKey) || PyResultDic[pyKey] == null)
    //                        {
    //                            if (PyResultDic.ContainsKey(pyKey))
    //                                PyResultDic.Remove(pyKey);

    //                            throw new Exception("算法输出结果异常！");
    //                        }

    //                        pyResult = ((string, float, float))PyResultDic[pyKey];
    //                        PyResultDic.Remove(pyKey);
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"{eTBPython}算法文件[{pythonCode}]执行异常！", ex);
    //                throw new Exception($"{eTBPython}算法文件[{pythonCode}]执行异常！", ex);
    //            }
    //            finally
    //            {
    //                lock (PyManualResetEventDic)
    //                    if (PyManualResetEventDic.ContainsKey(pyKey))
    //                        PyManualResetEventDic.Remove(pyKey);
    //            }
    //            return pyResult;
    //        }
    //        #endregion
    //        #endregion

    //        /// <summary>
    //        /// 新泵速下发PLC
    //        /// </summary>
    //        /// <param name="sideConfig">机架号</param>
    //        /// <param name="cCTBModel"></param>
    //        private void SetNewB(SideConfigMD sideConfig, ref CCTBMDModel cCTBModel)
    //        {
    //            var _cCTBModel = cCTBModel;
    //            var jjDic = new Dictionary<EPump, float>();
    //            var TBPumoAdjustType = UserDefineVariableInfo.DicVariables["TBPumoAdjustType"].ToString();
    //            if (TBPumoAdjustType == "PLC")
    //            {
    //                var switchAddress = sideConfig.PLCAddressConfig.SwitchAddress;
    //                var isAdjust = switchAddress?.AddresObj != null && switchAddress.ReadValue() == "1";
    //                sideConfig.PLCAddressConfig.PLCAddressLis.ForEach(o =>
    //                {
    //                    try
    //                    {
    //                        if (o == null) return;

    //                        var adjustValue = _cCTBModel.AdjustValue[o.Pump];
    //                        var jjDicValue = SetNewBCommon(sideConfig, adjustValue, o, isAdjust);
    //                        jjDic[o.Pump] = jjDicValue;
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error($"{sideConfig.SideType}[{o.PumpTag}]新泵速下发PLC异常！", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                    }
    //                });
    //            }
    //            else if (TBPumoAdjustType == "Socket")
    //            {
    //                var jjStrDic = new Dictionary<string, float>();
    //                sideConfig.PLCAddressConfig.PLCAddressLis.ForEach(o =>
    //                {
    //                    if (o == null) return;
    //                    var adjustValue = _cCTBModel.AdjustValue[o.Pump];
    //                    jjStrDic[o.PumpTag] = adjustValue;
    //                    jjDic[o.Pump] = adjustValue;
    //                });
    //                if (!SetNewBCommon(sideConfig, jjStrDic, true)) jjDic.Clear();
    //            }
    //            else
    //            {
    //                _logger.Error($"{sideConfig.EClosedLoopType}未实现[{TBPumoAdjustType}]通讯方式下发泵速！", type: eType, projectName: eProjectName, level: eLevel);
    //            }

    //            // 缓存A面调机量
    //            if (sideConfig.SideType != ESideType.A) return;
    //            jjAQue.Enqueue(Tuple.Create(cCTBModel.GatherTime, jjDic.Values.ToList()));
    //            if (jjAQue.Count > 200) jjAQue.Dequeue();
    //        }
    //        #region 判定方法
    //        /// <summary>
    //        /// 初始化数据
    //        /// </summary>
    //        /// <param name="sideConfig"></param>
    //        /// <param name="cCTBModel"></param>
    //        private void Data_Init(SideConfigMD sideConfig, ref CCTBMDModel cCTBModel)
    //        {
    //            var data = cCTBModel.DataArr.Where(x => x != 0).ToArray();

    //            // EWMA均值
    //            cCTBModel.EWMA_Avg_WeightData = 0;
    //            cCTBModel.EWMA_Avg_FluAndDataValue = 0;
    //            cCTBModel.LimitTimeB = 10;
    //            cCTBModel.Velocity = float.Parse(CCTBConfig.Velocity.ReadValue() ?? "0");
    //            cCTBModel.VelocitySetting = float.Parse(CCTBConfig.VelocitySetting.ReadValue() ?? "0");
    //            // AB走带间距
    //            cCTBModel.DistanceBetweenAB = byte.TryParse(UserDefineVariableInfo.DicVariables["DistanceBetweenAB"].ToString(), out byte distanceBetweenAB) ? distanceBetweenAB : (byte)0;
    //            // 进退刀检测开关
    //            cCTBModel.IsTBKnifeSwitchMonitor = UserDefineVariableInfo.DicVariables["IsTBKnifeSwitchMonitor"].ToString() == "1";
    //            switch (sideConfig.SideType) // 进退刀数据
    //            {
    //                case ESideType.A:
    //                    cCTBModel.TBKnifeSwitch = double.TryParse(CCTBConfig.TBKnifeSwitchA?.ReadValue(), out double tBKnifeSwitchA) ? tBKnifeSwitchA : 0;
    //                    break;
    //                case ESideType.B:
    //                    cCTBModel.TBKnifeSwitch = double.TryParse(CCTBConfig.TBKnifeSwitchB?.ReadValue(), out double tBKnifeSwitchB) ? tBKnifeSwitchB : 0;
    //                    break;
    //            }
    //            // 均值
    //            cCTBModel.DataAvg = data.Length == 0 ? 0 : float.Parse(data.Average().ToString("0.000"));
    //            // 最大值
    //            cCTBModel.DataMax = data.Length == 0 ? 0 : data.Max();
    //            // 最小值
    //            cCTBModel.DataMin = data.Length == 0 ? 0 : data.Min();
    //            // 极差
    //            cCTBModel.DataRange = data.Length == 0 ? 0 : data.Max() - data.Min();
    //            // 判断结果
    //            cCTBModel.JudgTrigger = "N";

    //            var path = UserDefineVariableInfo.DicVariables["CCTBConfigPath"].ToString();
    //            var fileName = UserDefineVariableInfo.DicVariables["CCTBIniFileName"].ToString();
    //            var fullPath = System.IO.Path.ChangeExtension(System.IO.Path.Combine(path, fileName), ".ini");
    //            var iniSampleHelper = new Sunwoda_IniSampleHelper(fullPath);
    //            var varietyName = iniSampleHelper.IniReadValue("品种信息", "VarietyName");
    //            cCTBModel.VarietyName = varietyName;

    //            #region 从缓存中获取流量数据
    //            var fluDic = new Dictionary<EPump, List<Tuple<DateTime, double>>>();
    //            lock (FluCache)
    //            {
    //                if (FluCache?.ContainsKey(sideConfig.SideType) ?? false)
    //                {
    //                    foreach (var pumpKv in FluCache[sideConfig.SideType])
    //                    {
    //                        fluDic[pumpKv.Key] = pumpKv.Value.ToList();
    //                    }
    //                }
    //            }
    //            var gatherTime = cCTBModel.GatherTime;
    //            var lastGatherTime = cCTBModel.LastGatherTime;
    //            // 获取本次重量数据至上次重量数据（最大时间间隔10秒）之间的流量数据
    //            var limitTime = (gatherTime - lastGatherTime).TotalSeconds > 10 ? 10 : (gatherTime - lastGatherTime).TotalSeconds;
    //            foreach (var fluKv in fluDic)
    //            {
    //                var flus = fluKv.Value.Where(o => (gatherTime - o.Item1).TotalSeconds <= limitTime).Select(o => o.Item2).ToList();
    //                if (cCTBModel.FluDataDic.ContainsKey(fluKv.Key))
    //                    cCTBModel.FluDataDic[fluKv.Key].AddRange(flus);
    //            }

    //            //_logger.Info($"采集时间：{gatherTime}\r\n范围时间{limitTime}\r\n采集数据{JsonConvert.SerializeObject(fluDic)}\r\n采集数据2{JsonConvert.SerializeObject(cCTBModel.FluDataDic)}");
    //            #endregion

    //            #region 采集泵速设定值
    //            var _cCTBModel = cCTBModel;
    //            var setBDic = new Dictionary<EPump, string>();
    //            sideConfig.PLCAddressConfig.PLCAddressLis.ForEach(o =>
    //            {
    //                try
    //                {
    //                    var speedValue = o?.SetPumpSpeedValue;
    //                    if (speedValue?.AddresObj == null) return;
    //                    // 转换为decimal类型
    //                    var setBDouble = decimal.Parse(speedValue.AddresObj.DictValue);
    //                    // 不四舍五入
    //                    if (!_cCTBModel.SetB_Round)
    //                    {
    //                        var pow = (decimal)Math.Pow(10, _cCTBModel.SetB_Digits);
    //                        // 截取小数位长度
    //                        setBDouble = Math.Truncate(setBDouble * pow) / pow;
    //                    }
    //                    setBDic[o.Pump] = Math.Round(setBDouble, _cCTBModel.SetB_Digits).ToString();
    //                }
    //                catch (Exception ex)
    //                {
    //                    _logger.Error($"[{o.PumpTag}]泵速设定值处理异常！", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                }
    //            });

    //            foreach (var setBkv in setBDic)
    //            {
    //                cCTBModel.SetB[setBkv.Key] = setBkv.Value;
    //            }
    //            #endregion
    //            GetRatioThresholdNumber(ref cCTBModel);
    //        }

    //        /// <summary>
    //        /// 计算EWMA及平均值 
    //        /// </summary>
    //        /// <param name="dataLis">开机/重启后首行非零数据之后的数据列表</param>
    //        /// <param name="cCTBModel">当前数据</param>
    //        private List<CCTBMDModel> EWMA(List<CCTBMDModel> dataLis, ref CCTBMDModel cCTBModel)
    //        {
    //            var reLis = new List<CCTBMDModel>(dataLis);

    //            // 剔除异常数据
    //            var dl = dataLis?.FindAll(o => o?.Rid_sJudge == "N")?.ToList();

    //            // 计算流量面密度数据
    //            if (dl?.Count >= 8)
    //            {
    //                dl.Reverse();
    //                // 上泵流量趋势变化量
    //                cCTBModel.FluDataDeltaPump1 = (dl[0].FluDataAvgPump1 + dl[1].FluDataAvgPump1 + dl[2].FluDataAvgPump1 - dl[5].FluDataAvgPump1 - dl[6].FluDataAvgPump1 - dl[7].FluDataAvgPump1) / 3;
    //                // 下泵流量趋势变化量
    //                cCTBModel.FluDataDeltaPump1 = (dl[0].FluDataAvgPump2 + dl[1].FluDataAvgPump2 + dl[2].FluDataAvgPump2 - dl[5].FluDataAvgPump2 - dl[6].FluDataAvgPump2 - dl[7].FluDataAvgPump2) / 3;
    //                // 流量面密度数据
    //                var fluAndDataValue = cCTBModel.DataAvg + 0.4 * cCTBModel.FluDataDeltaPump1 + 0.3 * cCTBModel.FluDataDeltaPump2;
    //                cCTBModel.FluAndDataValue = Math.Round(fluAndDataValue, 4);
    //                dl.Reverse();
    //            }

    //            #region EWMA_Avg_FluAndDataValue
    //            // 剔除FluAndDataValue计算的前7列数据
    //            var dl_FluAndDataValue = new List<CCTBMDModel>(dl.Skip(Math.Min(dl.Count, 7)));
    //            if (dl_FluAndDataValue?.Count() >= cCTBModel.EWMA_Count)
    //            {
    //                var daLis = dl_FluAndDataValue.Skip(dl_FluAndDataValue.Count - cCTBModel.EWMA_Count).Select(o => o.FluAndDataValue).ToList();
    //                cCTBModel.EWMA_Avg_FluAndDataValue = double.Parse((daLis?.Average() ?? 0).ToString("0.000"));
    //            }

    //            // 剔除FluAndDataValue计算的前7列数据
    //            if (cCTBModel.IsUseFluAndDataValue)
    //            {
    //                reLis.RemoveRange(0, Math.Min(reLis.Count, 7));
    //            }
    //            #endregion

    //            #region  EWMA_Avg_WeightData
    //            if (dl?.Count >= cCTBModel.EWMA_Count)
    //            {
    //                var daLis = dl.Skip(dl.Count - cCTBModel.EWMA_Count).Select(o => (double)o.DataAvg).ToList();
    //                cCTBModel.EWMA_Avg_WeightData = double.Parse((daLis?.Average() ?? 0).ToString("0.000"));
    //            }
    //            #endregion

    //            // 剔除EWMA计算的前几行数据
    //            reLis.RemoveRange(0, Math.Min(reLis.Count, cCTBModel.EWMA_Count - 1));

    //            // 返回数据不剔除异常数据是因为滞后数量包含异常数据
    //            return reLis;
    //        }

    //        /// <summary>
    //        /// 获取超过比例阈值或低于比例阈值的重量数据的数量
    //        /// </summary>
    //        /// <param name="cCTBModel"></param>
    //        /// <param name="data"></param>
    //        private void GetRatioThresholdNumber(ref CCTBMDModel cCTBModel)
    //        {
    //            //比例阈值上限
    //            var ratioUp = (float)(cCTBModel.TValue + cCTBModel.JudgRatio_gThreshold);
    //            //比例阈值下限
    //            var ratioDown = (float)(cCTBModel.TValue - cCTBModel.JudgRatio_gThreshold);
    //            cCTBModel.JudgRatio__UpThresholdCount = cCTBModel?.DataArr?.Where(o => o != 0 && o > ratioUp)?.Count() ?? 0;
    //            cCTBModel.JudgRatio__DownThresholdCount = cCTBModel?.DataArr?.Where(o => o != 0 && o < ratioDown)?.Count() ?? 0;
    //        }

    //        /// <summary>
    //        /// 获取平均值
    //        /// 备注：
    //        ///     忽略0数据
    //        /// </summary>
    //        private float GetWeightAverage(float[] data)
    //        {
    //            float[] data1 = data.Where(x => x != 0).ToArray();
    //            int number = data1.Length;
    //            float avgResutl = 0;
    //            if (data1.Length == 0)
    //            {
    //                avgResutl = 0;
    //            }
    //            else
    //            {
    //                avgResutl = data1.Average();
    //            }
    //            return float.Parse(avgResutl.ToString("0.000"));
    //        }

    //        #region 判定规则
    //        /// <summary>
    //        /// 阈值判定
    //        /// </summary>
    //        /// <param name="dataLis"></param>
    //        /// <returns></returns>
    //        private Tuple<bool, string> Judge_Threshold(ref List<CCTBMDModel> dataLis)
    //        {
    //            var reBool = false;

    //            if (dataLis?.Count > 0)
    //            {
    //                var cCTBModel = dataLis.Last();
    //                // 初始化
    //                var EAdata = cCTBModel.TValue - cCTBModel.GetEWMA_Avg(cCTBModel.IsUseFluAndDataValue);
    //                cCTBModel.JudgThreshold_Result = Math.Abs(EAdata) > cCTBModel.JudgThreshold_bThreshold ? "Y" : "N";

    //                // 剔除异常数据
    //                var dl = dataLis?.FindAll(o => o?.Rid_sJudge == "N")?.ToList() ?? new List<CCTBMDModel>();

    //                var index = 0;
    //                var count = 0;
    //                for (int i = 0; i < Math.Min(dl.Count, cCTBModel.JudgThreshold_dCount); i++)
    //                {
    //                    index = dl.Count - 1 - i;
    //                    if (dl[index].JudgThreshold_Result == "Y")
    //                    {
    //                        count++;
    //                        // 累计出现bCount次数
    //                        if (count >= cCTBModel.JudgThreshold_cCount)
    //                        {
    //                            reBool = true;
    //                            break;
    //                        }
    //                    }
    //                }
    //            }

    //            return Tuple.Create(reBool, "阈值判定");
    //        }

    //        /// <summary>
    //        /// 趋势判定
    //        /// </summary>
    //        /// <param name="dataLis"></param>
    //        /// <returns></returns>
    //        private Tuple<bool, string> Judge_Trend(ref List<CCTBMDModel> dataLis)
    //        {
    //            var reBool = false;
    //            if (dataLis?.Count > 0)
    //            {
    //                var cCTBModel = dataLis.Last();
    //                // 剔除异常数据
    //                var dl = dataLis?.FindAll(o => o?.Rid_sJudge == "N")?.ToList() ?? new List<CCTBMDModel>();

    //                if (dl.Count > 1)
    //                {
    //                    var lastEWMAAvg = dl[dl.Count - 2].GetEWMA_Avg(cCTBModel.IsUseFluAndDataValue);
    //                    if (lastEWMAAvg > 0)
    //                    {
    //                        var eWMA_Avg = cCTBModel.GetEWMA_Avg(cCTBModel.IsUseFluAndDataValue);
    //                        if (eWMA_Avg > cCTBModel.TValue && eWMA_Avg >= lastEWMAAvg)
    //                            cCTBModel.JudgTrend_Result = "上增";
    //                        else if (eWMA_Avg < cCTBModel.TValue && eWMA_Avg <= lastEWMAAvg)
    //                            cCTBModel.JudgTrend_Result = "下减";
    //                        else
    //                            cCTBModel.JudgTrend_Result = "--";
    //                    }
    //                }

    //                var count = 0;
    //                var index = 0;
    //                for (int i = 0; i < dl.Count; i++)
    //                {
    //                    index = dl.Count - 1 - i;
    //                    if (dl[index].JudgTrend_Result != cCTBModel.JudgTrend_Result) break;
    //                    if (!(cCTBModel.JudgTrend_Result == "上增" || cCTBModel.JudgTrend_Result == "下减")) break;
    //                    count++;
    //                    // 达到指定条件
    //                    if (count >= cCTBModel.JudgTrend_mCount)
    //                    {
    //                        reBool = true;
    //                        break;
    //                    }
    //                }
    //            }

    //            return Tuple.Create(reBool, "趋势判定");
    //        }

    //        /// <summary>
    //        /// 位置判定
    //        /// </summary>
    //        /// <param name="dataLis"></param>
    //        /// <returns></returns>
    //        private Tuple<bool, string> Judge_Position(ref List<CCTBMDModel> dataLis)
    //        {
    //            var reBool = false;

    //            if (dataLis?.Count > 0)
    //            {
    //                var cCTBModel = dataLis.Last();
    //                // 初始化
    //                var positionUp = cCTBModel.TValue + cCTBModel.JudgPosition_dThreshold;
    //                var positionDown = cCTBModel.TValue - cCTBModel.JudgPosition_dThreshold;
    //                var eWMA_Avg = cCTBModel.GetEWMA_Avg(cCTBModel.IsUseFluAndDataValue);
    //                cCTBModel.JudgPosition_Position = eWMA_Avg > positionUp ? "上" : (eWMA_Avg < positionDown ? "下" : "中");

    //                // 剔除异常数据
    //                var dl = dataLis?.FindAll(o => o?.Rid_sJudge == "N")?.ToList() ?? new List<CCTBMDModel>();
    //                // 当前触发条件
    //                if (cCTBModel.JudgPosition_Position == "上" || cCTBModel.JudgPosition_Position == "下")
    //                {
    //                    var count = 0;
    //                    for (int i = 0; i < dl.Count; i++)
    //                    {
    //                        var index = dl.Count - 1 - i;
    //                        if (!(dl[index].JudgPosition_Position == "上" || dl[index].JudgPosition_Position == "下")) break;
    //                        count++;
    //                        // 达到指定条件
    //                        if (count >= cCTBModel.JudgPosition_fCount)
    //                        {
    //                            reBool = true;
    //                            break;
    //                        }
    //                    }
    //                }
    //            }

    //            return Tuple.Create(reBool, "位置判定");
    //        }

    //        /// <summary>
    //        /// 比例判定
    //        /// </summary>
    //        /// <param name="dataLis"></param>
    //        /// <returns></returns>
    //        private Tuple<bool, string> Judge_Ratio(ref List<CCTBMDModel> dataLis)
    //        {
    //            var reBool = false;

    //            if (dataLis?.Count > 0)
    //            {
    //                var cCTBModel = dataLis.Last();
    //                // 初始化
    //                var dataCount = cCTBModel.DataArr.Where(x => x != 0).ToArray().Count();
    //                //大于比例阈值的比例数th
    //                var ratio1 = (decimal)(cCTBModel.JudgRatio__UpThresholdCount) / (decimal)(dataCount);
    //                //小于比例阈值的比例数
    //                var ratio2 = (decimal)(cCTBModel.JudgRatio__DownThresholdCount) / (decimal)(dataCount);
    //                if (ratio1 >= (decimal)cCTBModel.JudgRatio_hRatio || ratio2 >= (decimal)cCTBModel.JudgRatio_hRatio)
    //                {
    //                    cCTBModel.JudgRatio__Result = "Y";
    //                }
    //                else
    //                {
    //                    cCTBModel.JudgRatio__Result = "N";
    //                }

    //                // 剔除异常数据
    //                var dl = dataLis?.FindAll(o => o?.Rid_sJudge == "N")?.ToList() ?? new List<CCTBMDModel>();

    //                var count = 0;
    //                for (int i = 0; i < Math.Min(dl.Count, cCTBModel.JudgRatio_iCount); i++)
    //                {
    //                    var index = dl.Count - 1 - i;
    //                    if (dl[index].JudgRatio__Result != "Y") continue;
    //                    count++;
    //                    // 达到指定条件
    //                    if (count >= cCTBModel.JudgRatio_jCount)
    //                    {
    //                        reBool = true;
    //                        break;
    //                    }
    //                }
    //            }

    //            return Tuple.Create(reBool, "比例判定");
    //        }

    //        /// <summary>
    //        /// 动态判断数据生成器
    //        /// </summary>
    //        private Random RandomGenerator = new Random();
    //        /// <summary>
    //        /// 动态判定
    //        /// </summary>
    //        /// <param name="dataLis"></param>
    //        /// <returns></returns>
    //        private Tuple<bool, string> Judge_Dynamic(ref List<CCTBMDModel> dataLis)
    //        {
    //            var reBool = false;

    //            if (dataLis?.Count > 0)
    //            {
    //                var cCTBModel = dataLis.Last();
    //                // 初始化
    //                cCTBModel.JudgDynamic_Result = (byte)RandomGenerator.Next(0, 101);
    //                reBool = cCTBModel.JudgDynamic_Result > cCTBModel.JudgDynamic_kValue;
    //            }

    //            return Tuple.Create(reBool, "动态判定");
    //        }
    //        #endregion

    //        /// <summary>
    //        /// 判定之后
    //        /// </summary>
    //        /// <returns></returns>
    //        private void Judge_Finally(Tuple<bool, string> re, SideConfigMD sideConfig, List<CCTBMDModel> dataLis, ref CCTBMDModel cCTBModel)
    //        {
    //            cCTBModel.Judg_HandelType = re?.Item2 ?? "N";
    //            if (re?.Item1 ?? false)
    //            {
    //                // 极差判定
    //                if (!Judge_Range(ref cCTBModel)) return;
    //                if (!GetNewB(false, sideConfig, dataLis, ref cCTBModel)) return;

    //                cCTBModel.JudgTrigger = "Y";
    //                SetNewB(sideConfig, ref cCTBModel);
    //            }
    //            else// 流量趋势判定PID算法
    //            {
    //                if (!cCTBModel.JudgFluTrend_IsEnable) return;
    //                if (!GetNewB(true, sideConfig, dataLis, ref cCTBModel)) return;

    //                cCTBModel.JudgTrigger = "Y";
    //                cCTBModel.Judg_HandelType = "执行流量判定";
    //                SetNewB(sideConfig, ref cCTBModel);
    //            }
    //        }

    //        /// <summary>
    //        /// 极差规则
    //        /// </summary>
    //        /// <param name="cCTBModel"></param>
    //        private bool Judge_Range(ref CCTBMDModel cCTBModel)
    //        {
    //            try
    //            {
    //                var max = cCTBModel.DataMax;
    //                var min = cCTBModel.DataMin;
    //                var range = cCTBModel.DataRange;
    //                if (range > cCTBModel.JudgRange_Threshold)
    //                {
    //                    cCTBModel.JudgTrigger = "N";
    //                    cCTBModel.Judg_HandelResults = "极差判定拦截";
    //                    _logger.Info($"极差判定:最大值:" + max.ToString() + "最小值" + min.ToString() + "极差" + range.ToString());
    //                    return false;
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"极差判定异常", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //            return true;
    //        }
    //        #endregion

    //        /// <summary>
    //        /// MD保存数据整理
    //        /// </summary>
    //        /// <param name="sideConfig"></param>
    //        /// <param name="cCTBModel"></param>
    //        /// <returns></returns>
    //        private Dictionary<string, string> UpdateMD(SideConfigMD sideConfig, CCTBMDModel cCTBModel)
    //        {
    //            var paramDic = new Dictionary<string, string>();
    //            try
    //            {
    //                if (cCTBModel == null) return paramDic;

    //                paramDic.Add("时间", cCTBModel.GatherTime.ToString("yyyy-MM-dd HH:mm:ss"));
    //                paramDic.Add("扫描类型", cCTBModel.ScanType);
    //                paramDic.Add("趟数", cCTBModel.DataNumber.ToString());
    //                paramDic.Add("目标值T", cCTBModel.TValue.ToString());
    //                paramDic.Add("基材值", cCTBModel.BaseMaterialValue.ToString());
    //                paramDic.Add("报文目标值", cCTBModel.DynamicTargetValue.ToString());
    //                paramDic.Add("目标设定值", cCTBModel.TChange.ToString());
    //                paramDic.Add("目标值偏移量", cCTBModel.TOffset.ToString());

    //                paramDic.Add("是否使用流量趋势数据计算MA", cCTBModel.IsUseFluAndDataValue.ToString());
    //                paramDic.Add("EWMA-密度横向均值", cCTBModel.EWMA_Avg_WeightData.ToString());
    //                paramDic.Add("EWMA-流量&密度横向值", cCTBModel.EWMA_Avg_FluAndDataValue.ToString());
    //                paramDic.Add("密度横向均值", cCTBModel.DataAvg.ToString());
    //                paramDic.Add("流量&密度横向值", cCTBModel.FluAndDataValue.ToString());

    //                paramDic.Add("进退刀检测开关", cCTBModel.IsTBKnifeSwitchMonitor ? "开启" : "关闭");
    //                paramDic.Add("进退刀信号", cCTBModel.TBKnifeSwitch == 1 ? "进刀" : cCTBModel.TBKnifeSwitch == 0 ? "退刀" : $"{cCTBModel.TBKnifeSwitch}未知状态");

    //                paramDic.Add($"{sideConfig.SideType}闭环开关信号", sideConfig.PLCAddressConfig?.SwitchAddress?.ReadValue());
    //                paramDic.Add($"{sideConfig.SideType}闭环开关信号_供应商", sideConfig.PLCAddressConfig?.SwitchAddress_Supplier?.ReadValue());

    //                foreach (var pump in sideConfig.PLCAddressConfig.PLCAddressLis)
    //                {
    //                    if (!pump.IsEnable) continue;
    //                    var pumpTag = pump.PumpTag;
    //                    paramDic.Add($"{sideConfig.SideType}泵速下发完成信号 -{pumpTag}", pump?.SignalConfirm?.ReadValue());
    //                    paramDic.Add($"设定泵速实际值-{pumpTag}", pump?.SetPumpSpeedValue?.ReadValue());
    //                    paramDic.Add($"设定泵速处理值-{pumpTag}", cCTBModel.SetB.ContainsKey(pump.Pump) ? cCTBModel.SetB[pump.Pump] : "");
    //                    paramDic.Add($"新泵速B微调偏移值-{pumpTag}", pump?.Adjust?.ReadValue());
    //                    paramDic.Add($"新泵速B调机值-{pumpTag}", cCTBModel.AdjustValue.ContainsKey(pump.Pump) ? cCTBModel.AdjustValue[pump.Pump].ToString() : "");
    //                    var re = decimal.TryParse(pump?.ActualPumpSpeedValue?.ReadValue(), out decimal actualPumpSpeedValue);
    //                    paramDic.Add($"实时泵速实际值-{pumpTag}", actualPumpSpeedValue.ToString());
    //                    // 不四舍五入
    //                    if (!cCTBModel.SetB_Round)
    //                    {
    //                        var pow = (decimal)Math.Pow(10, cCTBModel.SetB_Digits);
    //                        // 截取小数位长度
    //                        actualPumpSpeedValue = Math.Truncate(actualPumpSpeedValue * pow) / pow;
    //                    }
    //                    paramDic.Add($"实时泵速处理值-{pumpTag}", Math.Round(actualPumpSpeedValue, cCTBModel.SetB_Digits).ToString());
    //                }
    //                paramDic.Add("新泵速调节系数", cCTBModel.SetB_Coefficient.ToString());
    //                paramDic.Add("新泵速调节范围", cCTBModel.JudgSpeed_Range.ToString());
    //                var pumpTag1 = sideConfig.PLCAddressConfig.PLCAddressMDPump0.PumpTag;
    //                var pumpTag2 = sideConfig.PLCAddressConfig.PLCAddressMDPump1.PumpTag;
    //                paramDic.Add($"非0流量数据均值-{pumpTag1}", cCTBModel.FluDataAvgPump1.ToString());
    //                //paramDic.Add($"非0流量数据-{pumpTag1}", string.Join(",", cCTBModel.FluDataPump1Lis));
    //                paramDic.Add($"非0流量数据均值-{pumpTag2}", cCTBModel.FluDataAvgPump2.ToString());
    //                //paramDic.Add($"非0流量数据-{pumpTag2}", string.Join(",", cCTBModel.FluDataPump2Lis));
    //                paramDic.Add("判异-走带速度", cCTBModel.Rid_TBVelocityCondition.ToString());
    //                paramDic.Add("判异-阈值", cCTBModel.Rid_aThreshold.ToString());
    //                paramDic.Add("判异-判定个数", cCTBModel.Rid_bCount.ToString());
    //                paramDic.Add("是否异常参数", cCTBModel.Rid_sJudge.ToString());
    //                paramDic.Add("异常描述", cCTBModel.Rid_sJudgeDescription.ToString());
    //                paramDic.Add("是否超出阈值", cCTBModel.JudgThreshold_Result);
    //                paramDic.Add("规格线", cCTBModel.JudgPosition_Position);
    //                paramDic.Add("趋势", cCTBModel.JudgTrend_Result);
    //                paramDic.Add("大于比例阈值数", cCTBModel.JudgRatio__UpThresholdCount.ToString());
    //                paramDic.Add("小于比例阈值数", cCTBModel.JudgRatio__DownThresholdCount.ToString());
    //                paramDic.Add("比例阈值判定", cCTBModel.JudgRatio__Result);
    //                paramDic.Add("流量趋势判定", cCTBModel.JudgFluTrend_Result);
    //                paramDic.Add("触发调节", cCTBModel.JudgTrigger);
    //                paramDic.Add("触发调节规则", cCTBModel.Judg_HandelType);
    //                paramDic.Add("判定结果", cCTBModel.Judg_HandelResults);
    //                paramDic.Add("闭环数据源类型", Enum.GetName(typeof(EWeightDataType), cCTBModel.WeightSource));
    //                paramDic.Add("膜区处理", cCTBModel.AreaType);
    //                paramDic.Add("滞后趟数g", cCTBModel.Lag_gConditiont.ToString());
    //                paramDic.Add("阈值b", cCTBModel.JudgThreshold_bThreshold.ToString());
    //                paramDic.Add("位置判定C", cCTBModel.JudgPosition_dThreshold.ToString());
    //                paramDic.Add("极差判定阈值", cCTBModel.JudgRange_Threshold.ToString());
    //                paramDic.Add("动态判定规则", cCTBModel.JudgDynamic_kValue.ToString());
    //                paramDic.Add("动态判定结果", cCTBModel.JudgDynamic_Result.ToString());

    //                if (sideConfig.SideType == ESideType.B)
    //                {
    //                    paramDic.Add("AB面走带间隔距离_m", cCTBModel?.DistanceBetweenAB.ToString());
    //                    paramDic.Add("B面获取A面数据限制时间_s", cCTBModel?.LimitTimeB.ToString());
    //                    paramDic.Add("PLC走带实时速度", cCTBModel.Velocity.ToString());
    //                    paramDic.Add("PLC走带设定速度", cCTBModel.VelocitySetting.ToString());
    //                    paramDic.Add("A面实际调机值", cCTBModel.AdjustValue_A.ToString());
    //                }

    //                var udcDic = GetUDCParam(sideConfig.ParamState);
    //                foreach (var kv in udcDic) paramDic.Add(kv.Key, kv.Value);
    //                for (int k = 0; k < cCTBModel.DataArr.Count(); k++)
    //                    paramDic.Add("重量" + (k + 1).ToString(), cCTBModel.DataArr[k].ToString());
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"{sideConfig.SideType}:生成异常", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }

    //            return paramDic;
    //        }
    //    }
    //    #endregion

    //    #region TD闭环模块
    //    /// <summary>
    //    /// TD闭环模块
    //    /// </summary>
    //    partial class VM_CCTBMonitoringPage : ViewModelBase
    //    {
    //        /// <summary>
    //        /// TD逻辑方法
    //        /// </summary>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="messageTag"></param>
    //        /// <param name="data"></param>
    //        /// <param name="eSideType"></param>
    //        /// <param name="eWeightSource"></param>
    //        /// <param name="targetValue_A"></param>
    //        /// <param name="targetValue_B"></param>
    //        /// <param name="baseMaterialWeight"></param>
    //        /// <param name="dataNumber"></param>
    //        /// <param name="scanType"></param>
    //        /// <exception cref="Exception"></exception>
    //        private void CCTBLogic_TD(DateTime gatherTime, string messageTag, float[] data, ESideType eSideType, EWeightDataType eWeightSource, float targetValue_A, float targetValue_B, float baseMaterialWeight, int dataNumber, float scanType)
    //        {
    //            try
    //            {
    //                // 获取A/B面基础配置
    //                var sc = CCTBConfig.BasicConfiguration_TD.SideConfigLis.Find(o => o.SideType == eSideType);
    //                // 加载涂布闭环配置xml文件路径
    //                var path = UserDefineVariableInfo.DicVariables["CCTBConfigPath"].ToString();
    //                // 加载A/B面配置文件
    //                var CCTBModelItem = Sunwoda_XmlHelper.XmlToModel<CCTBTDModel>(path, sc.ConfigFileName);
    //                // 本次采集时间
    //                CCTBModelItem.GatherTime = gatherTime;
    //                // 报文标识
    //                CCTBModelItem.MessageTag = messageTag;

    //                // 如果获取的重量数据与配置使用数据源一致则进行运算
    //                if (eWeightSource != CCTBModelItem.WeightSource)
    //                    return;
    //                // 报文内目标设定值
    //                switch (eSideType)
    //                {
    //                    case ESideType.A:
    //                        CCTBModelItem.DynamicTargetValue = targetValue_A;
    //                        break;
    //                    case ESideType.B:
    //                        CCTBModelItem.DynamicTargetValue = targetValue_A + targetValue_B;
    //                        break;
    //                    default:
    //                        _logger.Warn($"未实现[{eSideType}]的动态目标设定值配置！");
    //                        break;
    //                }
    //                // 基材值
    //                CCTBModelItem.BaseMaterialValue = baseMaterialWeight;
    //                // 目标值配置
    //                switch (CCTBModelItem.WeightSource)
    //                {
    //                    case EWeightDataType.干膜净重:
    //                    case EWeightDataType.湿膜净重:
    //                    case EWeightDataType.干膜净重MMData:
    //                    case EWeightDataType.湿膜净重MMData:
    //                        if (CCTBModelItem.IsUseDynamicTargetValue)
    //                            CCTBModelItem.TValue = CCTBModelItem.DynamicTargetValue + CCTBModelItem.TOffset;
    //                        else
    //                            CCTBModelItem.TValue = CCTBModelItem.TChange + CCTBModelItem.TOffset;
    //                        break;
    //                    case EWeightDataType.干膜总重:
    //                    case EWeightDataType.湿膜总重:
    //                    case EWeightDataType.干膜总重MMData:
    //                    case EWeightDataType.湿膜总重MMData:
    //                        if (CCTBModelItem.IsUseDynamicTargetValue)
    //                            CCTBModelItem.TValue = CCTBModelItem.BaseMaterialValue + CCTBModelItem.DynamicTargetValue + CCTBModelItem.TOffset;
    //                        else
    //                            CCTBModelItem.TValue = CCTBModelItem.BaseMaterialValue + CCTBModelItem.TChange + CCTBModelItem.TOffset;
    //                        break;
    //                }
    //                // 膜区数据发送界面
    //                //UpdateTDForm_FilmData(eSideType, data.ToList().ConvertAll(o => (double)o).ToList());
    //                // 赋值数据
    //                CCTBModelItem.DataArr = data;
    //                // 趟数
    //                CCTBModelItem.DataNumber = dataNumber;
    //                // 扫描类型
    //                CCTBModelItem.ScanType = scanType == 0 ? "0未扫描" :
    //                    scanType == 1 ? "1同步扫描" :
    //                    scanType == 2 ? "2自由扫描" : scanType.ToString();
    //                // 初始化数据,采集刀距信息、T块信息
    //                Data_Init(sc, ref CCTBModelItem);
    //                // 剔除异常数据：1、0数据判异规则；2、阈值判异规则；
    //                RemoveCCTBModelItem(sc, ref CCTBModelItem);

    //                // 闭环计算
    //                var cCTBModel = CCTBCalculte_TD(sc, CCTBModelItem);

    //                // 保存文档
    //                SaveToExcel(sc, cCTBModel);

    //                // 做缓存：主键：AB面
    //                CCTBTDModel.SetCache(sc.SideType, cCTBModel);
    //            }
    //            catch (Exception ex)
    //            {
    //                throw new Exception("[CCTBLogic_TD]涂布TD逻辑异常！", ex);
    //            }
    //        }

    //        /// <summary>
    //        /// 初始化数据
    //        /// </summary>
    //        /// <param name="sideConfig"></param>
    //        /// <param name="cCTBModel"></param>
    //        private void Data_Init(SideConfigTD sideConfig, ref CCTBTDModel cCTBModel)
    //        {
    //            var data = cCTBModel.DataArr.Where(x => x != 0f).ToArray();
    //            cCTBModel.DataAvg = (data.Length == 0) ? 0f : float.Parse(data.Average().ToString("0.000"));
    //            cCTBModel.Judg_HandelResults = "N";
    //            cCTBModel.JudgTrigger = "N";
    //            cCTBModel.Velocity = float.Parse(CCTBConfig.Velocity.ReadValue() ?? "0");
    //            cCTBModel.VelocitySetting = float.Parse(CCTBConfig.VelocitySetting.ReadValue() ?? "0");
    //            // AB走带间距
    //            cCTBModel.DistanceBetweenAB = byte.TryParse(UserDefineVariableInfo.DicVariables["DistanceBetweenAB"].ToString(), out byte distanceBetweenAB) ? distanceBetweenAB : (byte)0;
    //            // 进退刀检测开关
    //            cCTBModel.IsTBKnifeSwitchMonitor = UserDefineVariableInfo.DicVariables["IsTBKnifeSwitchMonitor"].ToString() == "1";
    //            switch (sideConfig.SideType) // 进退刀数据
    //            {
    //                case ESideType.A:
    //                    cCTBModel.TBKnifeSwitch = double.TryParse(CCTBConfig.TBKnifeSwitchA?.ReadValue(), out double tBKnifeSwitchA) ? tBKnifeSwitchA : 0;
    //                    break;
    //                case ESideType.B:
    //                    cCTBModel.TBKnifeSwitch = double.TryParse(CCTBConfig.TBKnifeSwitchB?.ReadValue(), out double tBKnifeSwitchB) ? tBKnifeSwitchB : 0;
    //                    break;
    //            }
    //            cCTBModel.Lag_gConditiont = cCTBModel.DistanceBetweenAB / cCTBModel.VelocitySetting * 60;
    //            cCTBModel.Dearance_Left = double.Parse(sideConfig.PLCAddressConfig.Dearance_Left?.ReadValue() ?? "0");
    //            cCTBModel.Dearance_Right = double.Parse(sideConfig.PLCAddressConfig.Dearance_Right?.ReadValue() ?? "0");

    //            var path = UserDefineVariableInfo.DicVariables["CCTBConfigPath"].ToString();
    //            var fileName = UserDefineVariableInfo.DicVariables["CCTBIniFileName"].ToString();
    //            var fullPath = System.IO.Path.ChangeExtension(System.IO.Path.Combine(path, fileName), ".ini");
    //            var iniSampleHelper = new Sunwoda_IniSampleHelper(fullPath);
    //            var varietyName = iniSampleHelper.IniReadValue("品种信息", "VarietyName");
    //            cCTBModel.VarietyName = varietyName;

    //            // T块信息获取
    //            var reLis = ClientSendAndRecieveData<object>($"涂布模头{sideConfig.SideType}", 1, "TINFOREUQEST", null);
    //            if (!(reLis != null && reLis.Count > 0 && reLis[0] is TInfoReponseData tInfoReponseData))
    //            {
    //                _logger.Error($"T块信息获取失败:[{JsonConvert.SerializeObject(cCTBModel.TInfo)}]");
    //                return;
    //            }
    //            cCTBModel.TInfo = tInfoReponseData;
    //        }

    //        /// <summary>
    //        /// 初始化闭环逻辑参数
    //        /// </summary>
    //        /// <param name="sideConfig">AB面配置</param>
    //        /// <param name="cCTBModel">数据</param>
    //        private CCTBTDModel CCTBCalculte_TD(SideConfigTD sideConfig, CCTBTDModel cCTBModel)
    //        {
    //            var reCCTBModel = cCTBModel;
    //            try
    //            {
    //                var weightList = CCTBTDModel.GetCache(sideConfig.SideType);
    //                #region 缓存数据处理
    //                // 获取开机/重启后的数据，同时剔除开头0数据，同时剔除固定开机数据 —— 具有相同数据源配置
    //                var weightLis_RebootByZero = GetCacheAfterReboot(weightList, reCCTBModel);
    //                // 异常数据间隔之后的数据，且剔除开头异常数据
    //                var weightLis_Filtrate = GetCacheAfterFiltrate(weightLis_RebootByZero, reCCTBModel);
    //                // 滞后数据之后的数据，滞后起点：(1、下发点；2、刀距变化点；3、闭环重启点)
    //                var weightLis_RemoveLag = GetCacheAfterLagging(weightLis_Filtrate, reCCTBModel);
    //                #endregion
    //                // 非剔除数据则继续进行规则计算
    //                if (reCCTBModel.Rid_sJudge == "N")
    //                {
    //                    Judge_Finally(sideConfig, weightLis_RemoveLag, ref reCCTBModel);
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"TD计算{sideConfig.SideType}面{cCTBModel.MessageTag}架[{cCTBModel?.WeightSource}]数据异常：", ex, isAddCore: true, eType, eProjectName, eLevel);
    //            }
    //            return reCCTBModel;
    //        }

    //        /// <summary>
    //        /// 滞后数据之后的数据，滞后起点：(1、下发点；2、刀距变化点；3、闭环重启点)
    //        /// </summary>
    //        /// <param name="dataLis"></param>
    //        /// <param name="cCTBModel"></param>
    //        /// <returns></returns>
    //        private List<CCTBTDModel> GetCacheAfterLagging(List<CCTBTDModel> dataLis, CCTBTDModel cCTBModel)
    //        {
    //            var reLis = new List<CCTBTDModel>();
    //            // 找到滞后起点
    //            var laggingStartIndex = dataLis.FindLastIndex(o =>
    //            {
    //                // 已触发调节
    //                if (o.JudgTrigger == "Y")
    //                    return true;
    //                // 刀距发生变化
    //                if (Math.Abs(cCTBModel.Dearance_Left - o.Dearance_Left) >= cCTBModel.Lag_DearanceConditiont ||
    //                    Math.Abs(cCTBModel.Dearance_Right - o.Dearance_Right) >= cCTBModel.Lag_DearanceConditiont)
    //                    return true;
    //                // 如果闭环重新开启(即：null/0/1 => !(null/0/1))
    //                if ((o.TInfo?.TAdjustMode == 0 || o.TInfo?.TAdjustMode == 1) &&
    //                    (cCTBModel.TInfo?.TAdjustMode != 0 && o.TInfo?.TAdjustMode != 1))
    //                    return true;
    //                return false;
    //            });
    //            if (laggingStartIndex >= 0)
    //            {
    //                // 去掉滞后起点之前的数据
    //                var laggingEndIndex = dataLis.FindIndex(o =>
    //                {
    //                    return (o.GatherTime - dataLis[laggingStartIndex].GatherTime).TotalSeconds > cCTBModel.Lag_gConditiont;
    //                });
    //                var index = laggingEndIndex >= 0 ? laggingEndIndex : dataLis.Count - 1;
    //                reLis.AddRange(dataLis.Skip(index));
    //            }

    //            return reLis;
    //        }

    //        /// <summary>
    //        /// 判定之后
    //        /// </summary>
    //        /// <returns></returns>
    //        private void Judge_Finally(SideConfigTD sideConfig, List<CCTBTDModel> dataLis, ref CCTBTDModel cCTBModel)
    //        {
    //            // 极差判定
    //            if (GetNewT(sideConfig, dataLis, ref cCTBModel))
    //            {
    //                SetNewT(sideConfig, ref cCTBModel);
    //            }
    //        }

    //        /// <summary>
    //        /// 获取T块调机值
    //        /// </summary>
    //        private bool GetNewT(SideConfigTD sideConfig, List<CCTBTDModel> dataLis, ref CCTBTDModel cCTBModel)
    //        {
    //            // 剔除异常数据
    //            var dl = dataLis?.FindAll(o => o?.Rid_sJudge == "N")?.ToList();
    //            var cCTBModelCount = cCTBModel.Control_DataCount;
    //            if (dl?.Count < cCTBModelCount)
    //            {
    //                cCTBModel.JudgTrigger = "N";
    //                cCTBModel.Judg_HandelResults = $"拦截判定:缓存数据不足[{cCTBModelCount}]";
    //                return false;
    //            }

    //            var newBTuple = GetTDControlNewB(sideConfig, dl, cCTBModelCount, ref cCTBModel);
    //            // 0:正常 其他:异常
    //            if (newBTuple.Item1 != 0)
    //            {
    //                cCTBModel.JudgTrigger = "N";
    //                cCTBModel.Judg_HandelResults = $"拦截判定:{newBTuple.Item1}-{newBTuple.Item2}";
    //                cCTBModel.NewB = newBTuple.Item3;
    //                return false;
    //            }
    //            cCTBModel.JudgTrigger = "Y";
    //            cCTBModel.Judg_HandelResults = $"{newBTuple.Item1}-{newBTuple.Item2}";
    //            cCTBModel.NewB = newBTuple.Item3;
    //            return true;
    //        }

    //        /// <summary>
    //        /// TDControl算法获取T块调机量
    //        /// </summary>
    //        private (int, string, Dictionary<string, int>) GetTDControlNewB(SideConfigTD sideConfig, List<CCTBTDModel> dataLis, int dataCount, ref CCTBTDModel cCTBModel)
    //        {
    //            var eTBPython = EArithmeticType.TDControl;
    //            var sw = new Stopwatch();
    //            sw.Start();
    //            var jj = (-1, "Python算法执行异常！", new Dictionary<string, int>());
    //            // 算法执行
    //            var func = new Func<string, (int, string, Dictionary<string, int>)?>((pythonCode) =>
    //            {
    //                try
    //                {
    //                    var dataStrLis = new List<List<string>>();
    //                    for (int i = 0; i < dataCount; i++)
    //                    {
    //                        if (i == 0)
    //                            dataStrLis.Add(new List<string>());
    //                        dataStrLis.Add(new List<string>());

    //                        var index = dataLis.Count - i - 1;
    //                        if (i == 0)
    //                            dataStrLis[0].Add("时间");
    //                        dataStrLis[i + 1].Add(dataLis[index].GatherTime.ToString());

    //                        for (int j = 0; j < dataLis[index].DataArr.Length; j++)
    //                        {
    //                            if (i == 0)
    //                                dataStrLis[0].Add($"重量{j + 1}");
    //                            dataStrLis[i + 1].Add(dataLis[index].DataArr[j].ToString());
    //                        }

    //                        if (i == 0)
    //                            dataStrLis[0].Add($"T块调机模式");
    //                        dataStrLis[i + 1].Add(dataLis[index].TInfo.TAdjustMode.ToString());

    //                        var tBlockInfoLis = dataLis[index].TInfo.TBlockInfoLis.OrderBy(o => o.TId).ToList();
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0)
    //                                dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-实时位置");
    //                            dataStrLis[i + 1].Add(tBlockInfoLis[j].TPosition.ToString());
    //                        }
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0)
    //                                dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-报警标识");
    //                            dataStrLis[i + 1].Add($"[{string.Join(",", tBlockInfoLis[j].AlarmCode ?? new List<int>())}]");
    //                        }
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0)
    //                                dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-上限值");
    //                            dataStrLis[i + 1].Add(tBlockInfoLis[j].TUpperLimit.ToString());
    //                        }
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0)
    //                                dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-下限值");
    //                            dataStrLis[i + 1].Add(tBlockInfoLis[j].TLowerLimit.ToString());
    //                        }
    //                    }
    //                    var data = string.Join("\r\n", dataStrLis.ConvertAll(o => string.Join(",", o)));
    //                    var pyCode = "";
    //                    lock (PidFileInfoDic) pyCode = PidFileInfoDic[pythonCode].pythonStr;
    //                    var jjR = AddExcuteTDControlpyAction(pythonCode, pyCode, data);
    //                    _logger.Info($"{sideConfig.SideType}-{eTBPython}算法输出[{jjR.Item1}、{jjR.Item2}、{JsonConvert.SerializeObject(jjR.Item3)}]");
    //                    return jjR;
    //                }
    //                catch (Exception ex)
    //                {
    //                    _logger.Error($"{sideConfig.SideType}-{eTBPython}算法异常", ex);
    //                    return null;
    //                }
    //            });
    //            // 算法获取
    //            var re = RefreshAndExcuteTBPydAction<(int, string, Dictionary<string, int>)?>(eTBPython, sideConfig, cCTBModel.VarietyName, func);
    //            if (re != null) jj = ((int, string, Dictionary<string, int>))re;
    //            sw.Stop();
    //            _logger.Info($"{sideConfig.SideType}-{eTBPython}算法计算耗时[{sw.ElapsedMilliseconds}]", false);
    //            return jj;
    //        }

    //        #region PythonNet
    //        #region TDControl算法
    //        /// <summary>
    //        /// 执行TDControl的py文件算法
    //        /// </summary>
    //        /// <param name="pythonCode"></param>
    //        /// <param name="pyCodeStr"></param>
    //        /// <returns></returns>
    //        private (int, string, Dictionary<string, int>) AddExcuteTDControlpyAction(string pythonCode, string pyCodeStr, string data)
    //        {
    //            var eTBPython = EArithmeticType.TDControl;
    //            var pyResult = (-1, "Python算法执行异常！", new Dictionary<string, int>());
    //            var pyKey = Guid.NewGuid().ToString();
    //            try
    //            {
    //                using (var mr = new ManualResetEvent(false))
    //                {
    //                    lock (PyManualResetEventDic)
    //                        PyManualResetEventDic[pyKey] = mr;

    //                    var func = new Func<dynamic, object, object>((pm, da) =>
    //                    {
    //                        // python更改为pyd
    //                        //dynamic po = pm.TDControl(da as string);
    //                        //dynamic re = po.final_output();
    //                        dynamic re = pm.algorithm_run(da as string);
    //                        var reTp = PyTuple.AsTuple(re);
    //                        var reTp1 = (int)reTp[0];
    //                        var reTp2 = (string)reTp[1];
    //                        var reTp3 = new PyDict(reTp[2]);
    //                        var reDic = reTp3.ToDictionary(o => o.ToString(), o => int.Parse(reTp3[o].ToString().Trim('[', ']')));
    //                        var pyRes = (reTp1, reTp2, reDic);
    //                        return pyRes;
    //                    });
    //                    var obj = new object[] { pyKey, pythonCode, pyCodeStr, data, func };
    //                    PythonHelper.Instance.AddPyhtonAction((ExcuteTBPythonAction, obj));
    //                    mr.WaitOne();
    //                    lock (PyResultDic)
    //                    {
    //                        if (!PyResultDic.ContainsKey(pyKey) || PyResultDic[pyKey] == null)
    //                        {
    //                            if (PyResultDic.ContainsKey(pyKey))
    //                                PyResultDic.Remove(pyKey);

    //                            throw new Exception("算法输出结果异常！");
    //                        }

    //                        pyResult = ((int, string, Dictionary<string, int>))PyResultDic[pyKey];
    //                        PyResultDic.Remove(pyKey);
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"{eTBPython}算法文件[{pythonCode}]执行异常！", ex);
    //                throw new Exception($"{eTBPython}算法文件[{pythonCode}]执行异常！", ex);
    //            }
    //            finally
    //            {
    //                lock (PyManualResetEventDic)
    //                    if (PyManualResetEventDic.ContainsKey(pyKey))
    //                        PyManualResetEventDic.Remove(pyKey);
    //            }
    //            return pyResult;
    //        }
    //        #endregion
    //        #endregion

    //        /// <summary>
    //        /// 下发T块调机值
    //        /// </summary>
    //        /// <param name="sideConfig"></param>
    //        /// <param name="cCTBModel"></param>
    //        private void SetNewT(SideConfigTD sideConfig, ref CCTBTDModel cCTBModel)
    //        {
    //            var coefficient = cCTBModel.SetT_Coefficient;
    //            var newT = new Dictionary<string, int>();
    //            (cCTBModel.NewB ?? new Dictionary<string, int>()).ToList().ForEach(o => newT[o.Key] = (int)(o.Value * coefficient));
    //            SetNewTCommon(sideConfig, cCTBModel.DataNumber, cCTBModel.DataAvg, cCTBModel.TInfo, cCTBModel.NewB);
    //        }

    //        /// <summary>
    //        /// TD保存数据整理
    //        /// </summary>
    //        /// <param name="sideConfig"></param>
    //        /// <param name="cCTBModel"></param>
    //        /// <returns></returns>
    //        private Dictionary<string, string> UpdateTD(SideConfigTD sideConfig, CCTBTDModel cCTBModel)
    //        {
    //            var paramDic = new Dictionary<string, string>();
    //            try
    //            {
    //                if (cCTBModel != null)
    //                {
    //                    paramDic.Add("时间", cCTBModel.GatherTime.ToString("yyyy-MM-dd HH:mm:ss"));
    //                    paramDic.Add("扫描类型", cCTBModel.ScanType);
    //                    paramDic.Add("趟数", cCTBModel.DataNumber.ToString());
    //                    paramDic.Add("目标值T", cCTBModel.TValue.ToString());
    //                    paramDic.Add("基材值", cCTBModel.BaseMaterialValue.ToString());
    //                    paramDic.Add("报文目标值", cCTBModel.DynamicTargetValue.ToString());
    //                    paramDic.Add("目标设定值", cCTBModel.TChange.ToString());
    //                    paramDic.Add("目标值偏移量", cCTBModel.TOffset.ToString());

    //                    paramDic.Add("是否异常参数", cCTBModel.Rid_sJudge.ToString());
    //                    paramDic.Add("异常描述", cCTBModel.Rid_sJudgeDescription.ToString());
    //                    paramDic.Add("判异-走带速度", cCTBModel.Rid_TBVelocityCondition.ToString());
    //                    paramDic.Add("判异-阈值", cCTBModel.Rid_aThreshold.ToString());
    //                    paramDic.Add("判异-判定个数", cCTBModel.Rid_bCount.ToString());
    //                    paramDic.Add("是否触发调节", cCTBModel.JudgTrigger);
    //                    paramDic.Add("触发调节描述", cCTBModel.Judg_HandelResults);
    //                    paramDic.Add("闭环数据源类型", cCTBModel.WeightSource.ToString());
    //                    paramDic.Add("膜区处理", cCTBModel.AreaType);
    //                    paramDic.Add("刀距滞后变化值", cCTBModel.Lag_DearanceConditiont.ToString());
    //                    paramDic.Add("滞后时间(s)", cCTBModel.Lag_gConditiont.ToString());

    //                    paramDic.Add("AB面走带间隔距离_m", cCTBModel?.DistanceBetweenAB.ToString());
    //                    paramDic.Add("PLC走带实时速度", cCTBModel.Velocity.ToString());
    //                    paramDic.Add("PLC走带设定速度", cCTBModel.VelocitySetting.ToString());
    //                    paramDic.Add("刀距设定值左", cCTBModel.Dearance_Left.ToString());
    //                    paramDic.Add("刀距设定值右", cCTBModel.Dearance_Right.ToString());
    //                    paramDic.Add("进退刀检测开关", cCTBModel.IsTBKnifeSwitchMonitor ? "开启" : "关闭");
    //                    paramDic.Add("进退刀信号", cCTBModel.TBKnifeSwitch == 1 ? "进刀" : cCTBModel.TBKnifeSwitch == 0 ? "退刀" : $"{cCTBModel.TBKnifeSwitch}未知状态");

    //                    // -TODO 临时使用
    //                    var scMd = CCTBConfig.BasicConfiguration_MD.SideConfigLis.Find(o => o.SideType == sideConfig.SideType);
    //                    foreach (var pump in scMd.PLCAddressConfig.PLCAddressLis)
    //                    {
    //                        if (!pump.IsEnable) continue;
    //                        var pumpTag = pump.PumpTag;
    //                        paramDic.Add($"设定泵速实际值-{pumpTag}", pump?.SetPumpSpeedValue?.ReadValue());
    //                    }

    //                    paramDic.Add("T块调节系数", cCTBModel.SetT_Coefficient.ToString());
    //                    paramDic.Add("算法入参趟数", cCTBModel.Control_DataCount.ToString());
    //                    var udcDic = GetUDCParam(sideConfig.ParamState);
    //                    foreach (var kv in udcDic) paramDic.Add(kv.Key, kv.Value);

    //                    paramDic.Add("算法输出T块调机量", JsonConvert.SerializeObject(cCTBModel?.NewB));
    //                    if (cCTBModel.TInfo != null)
    //                    {
    //                        paramDic.Add("T块信息-设备编号", cCTBModel?.TInfo.DeviceName);
    //                        paramDic.Add("T块信息-调机模式", cCTBModel?.TInfo.TAdjustMode.ToString());
    //                        paramDic.Add("T块信息-T块总数", cCTBModel?.TInfo.TNumber.ToString());
    //                        paramDic.Add("T块信息-响应时间", cCTBModel?.TInfo.ReponseTime.ToString("yyyy-MM-dd HH:mm:ss"));
    //                        if (cCTBModel.TInfo.TBlockInfoLis != null)
    //                        {
    //                            var tBlockInfoLis = cCTBModel.TInfo.TBlockInfoLis.OrderBy(o => o.TId);
    //                            foreach (var tBlockInfo in tBlockInfoLis)
    //                                paramDic.Add($"T块[{tBlockInfo.TId}]调机模式", tBlockInfo.TAdjustMode ? "自动" : "半自动");
    //                            foreach (var tBlockInfo in tBlockInfoLis)
    //                                paramDic.Add($"T块[{tBlockInfo.TId}]实时位置", tBlockInfo.TPosition.ToString());
    //                            foreach (var tBlockInfo in tBlockInfoLis)
    //                                paramDic.Add($"T块[{tBlockInfo.TId}]调机量", tBlockInfo.TAdjustInfo?.TAdjustValue.ToString());
    //                            foreach (var tBlockInfo in tBlockInfoLis)
    //                                paramDic.Add($"T块[{tBlockInfo.TId}]调机时间", tBlockInfo.TAdjustInfo?.TAdjustTime.ToString());
    //                            foreach (var tBlockInfo in tBlockInfoLis)
    //                                paramDic.Add($"T块[{tBlockInfo.TId}]调机时趟数", tBlockInfo.TAdjustInfo?.TAdjustScanCount.ToString());
    //                            foreach (var tBlockInfo in tBlockInfoLis)
    //                                paramDic.Add($"T块[{tBlockInfo.TId}]上限位置", tBlockInfo.TUpperLimit.ToString());
    //                            foreach (var tBlockInfo in tBlockInfoLis)
    //                                paramDic.Add($"T块[{tBlockInfo.TId}]下限位置", tBlockInfo.TLowerLimit.ToString());
    //                            foreach (var tBlockInfo in tBlockInfoLis)
    //                                paramDic.Add($"T块[{tBlockInfo.TId}]报警编码", $"[{string.Join(",", tBlockInfo.AlarmCode ?? new List<int>())}]");
    //                        }
    //                    }
    //                    for (int k = 0; k < cCTBModel.DataArr?.Count(); k++)
    //                        paramDic.Add("重量" + (k + 1).ToString(), cCTBModel.DataArr[k].ToString());
    //                }

    //                return paramDic;
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"{sideConfig.SideType}:生成异常", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //            return paramDic;
    //        }
    //    }
    //    #endregion

    //    #region MDTD闭环模块
    //    /// <summary>
    //    /// MDTD闭环模块
    //    /// </summary>
    //    partial class VM_CCTBMonitoringPage : ViewModelBase
    //    {
    //        /// <summary>
    //        /// MD&TD联合闭环逻辑方法
    //        /// </summary>
    //        /// <param name="gatherTime"></param>
    //        /// <param name="messageTag"></param>
    //        /// <param name="data"></param>
    //        /// <param name="eSideType"></param>
    //        /// <param name="eWeightSource"></param>
    //        /// <param name="targetValue_A"></param>
    //        /// <param name="targetValue_B"></param>
    //        /// <param name="toleranceA"></param>
    //        /// <param name="toleranceB"></param>
    //        /// <param name="baseMaterialWeight"></param>
    //        /// <param name="dataNumber"></param>
    //        /// <param name="scanType"></param>
    //        /// <exception cref="Exception"></exception>
    //        private void CCTBLogic_MDTD(DateTime gatherTime, string messageTag, float[] data, ESideType eSideType, EWeightDataType eWeightSource, float targetValue_A, float targetValue_B, float toleranceA, float toleranceB, float baseMaterialWeight, int dataNumber, float scanType)
    //        {
    //            try
    //            {
    //                // 获取A/B面基础配置
    //                var sc = CCTBConfig.BasicConfiguration_MDTD.SideConfigLis.Find(o => o.SideType == eSideType);
    //                sc.PLCAddressConfigMD = CCTBConfig.BasicConfiguration_MD.SideConfigLis.Find(o => o.SideType == eSideType).PLCAddressConfig;
    //                sc.PLCAddressConfigTD = CCTBConfig.BasicConfiguration_TD.SideConfigLis.Find(o => o.SideType == eSideType).PLCAddressConfig;
    //                // 加载涂布闭环配置xml文件路径
    //                var path = UserDefineVariableInfo.DicVariables["CCTBConfigPath"].ToString();
    //                // 加载A/B面配置文件
    //                var CCTBModelItem = Sunwoda_XmlHelper.XmlToModel<CCTBMDTDModel>(path, sc.ConfigFileName);
    //                // 本次采集时间
    //                CCTBModelItem.GatherTime = gatherTime;
    //                // 报文标识
    //                CCTBModelItem.MessageTag = messageTag;

    //                // 如果获取的重量数据与配置使用数据源一致则进行运算
    //                if (eWeightSource != CCTBModelItem.WeightSource)
    //                    return;
    //                // 报文内目标设定值
    //                switch (eSideType)
    //                {
    //                    case ESideType.A:
    //                        CCTBModelItem.DynamicTargetValue = targetValue_A;
    //                        CCTBModelItem.Tolerance = toleranceA;
    //                        break;
    //                    case ESideType.B:
    //                        CCTBModelItem.DynamicTargetValue = targetValue_A + targetValue_B;
    //                        CCTBModelItem.Tolerance = toleranceB;
    //                        break;
    //                    default:
    //                        _logger.Warn($"未实现[{eSideType}]的动态目标设定值配置！");
    //                        break;
    //                }
    //                // 基材值
    //                CCTBModelItem.BaseMaterialValue = baseMaterialWeight;
    //                // 目标值配置
    //                switch (CCTBModelItem.WeightSource)
    //                {
    //                    case EWeightDataType.干膜净重:
    //                    case EWeightDataType.湿膜净重:
    //                    case EWeightDataType.干膜净重MMData:
    //                    case EWeightDataType.湿膜净重MMData:
    //                        if (CCTBModelItem.IsUseDynamicTargetValue)
    //                            CCTBModelItem.TValue = CCTBModelItem.DynamicTargetValue + CCTBModelItem.TOffset;
    //                        else
    //                            CCTBModelItem.TValue = CCTBModelItem.TChange + CCTBModelItem.TOffset;
    //                        break;
    //                    case EWeightDataType.干膜总重:
    //                    case EWeightDataType.湿膜总重:
    //                    case EWeightDataType.干膜总重MMData:
    //                    case EWeightDataType.湿膜总重MMData:
    //                        if (CCTBModelItem.IsUseDynamicTargetValue)
    //                            CCTBModelItem.TValue = CCTBModelItem.BaseMaterialValue + CCTBModelItem.DynamicTargetValue + CCTBModelItem.TOffset;
    //                        else
    //                            CCTBModelItem.TValue = CCTBModelItem.BaseMaterialValue + CCTBModelItem.TChange + CCTBModelItem.TOffset;
    //                        break;
    //                }
    //                // 膜区数据发送界面
    //                //UpdateTDForm_FilmData(eSideType, data.ToList().ConvertAll(o => (double)o).ToList());
    //                // 赋值数据
    //                CCTBModelItem.DataArr = data;
    //                // 趟数
    //                CCTBModelItem.DataNumber = dataNumber;
    //                // 扫描类型
    //                CCTBModelItem.ScanType = scanType == 0 ? "0未扫描" :
    //                    scanType == 1 ? "1同步扫描" :
    //                    scanType == 2 ? "2自由扫描" : scanType.ToString();
    //                // 初始化数据,采集刀距信息、T块信息
    //                Data_Init(sc, ref CCTBModelItem);
    //                // 剔除异常数据：1、0数据判异规则；2、阈值判异规则；
    //                RemoveCCTBModelItem(sc, ref CCTBModelItem);

    //                // 闭环计算
    //                var cCTBModel = CCTBCalculte_MDTD(sc, CCTBModelItem);

    //                // 保存文档
    //                SaveToExcel(sc, cCTBModel);

    //                // 做缓存：主键：AB面
    //                CCTBMDTDModel.SetCache(sc.SideType, cCTBModel);
    //            }
    //            catch (Exception ex)
    //            {
    //                throw new Exception("[CCTBLogic_TD]涂布TD逻辑异常！", ex);
    //            }
    //        }

    //        /// <summary>
    //        /// 初始化数据
    //        /// </summary>
    //        /// <param name="sideConfig"></param>
    //        /// <param name="cCTBModel"></param>
    //        private void Data_Init(SideConfigMDTD sideConfig, ref CCTBMDTDModel cCTBModel)
    //        {
    //            var data = cCTBModel.DataArr.Where(x => x != 0f).ToArray();
    //            cCTBModel.DataAvg = (data.Length == 0) ? 0f : float.Parse(data.Average().ToString("0.000"));
    //            cCTBModel.Judg_HandelResults = "N";
    //            cCTBModel.JudgTrigger = "N";
    //            cCTBModel.Velocity = float.Parse(CCTBConfig.Velocity.ReadValue() ?? "0");
    //            cCTBModel.VelocitySetting = float.Parse(CCTBConfig.VelocitySetting.ReadValue() ?? "0");
    //            // AB走带间距
    //            cCTBModel.DistanceBetweenAB = byte.TryParse(UserDefineVariableInfo.DicVariables["DistanceBetweenAB"].ToString(), out byte distanceBetweenAB) ? distanceBetweenAB : (byte)0;
    //            // 进退刀检测开关
    //            cCTBModel.IsTBKnifeSwitchMonitor = UserDefineVariableInfo.DicVariables["IsTBKnifeSwitchMonitor"].ToString() == "1";
    //            switch (sideConfig.SideType) // 进退刀数据
    //            {
    //                case ESideType.A:
    //                    cCTBModel.TBKnifeSwitch = double.TryParse(CCTBConfig.TBKnifeSwitchA?.ReadValue(), out double tBKnifeSwitchA) ? tBKnifeSwitchA : 0;
    //                    break;
    //                case ESideType.B:
    //                    cCTBModel.TBKnifeSwitch = double.TryParse(CCTBConfig.TBKnifeSwitchB?.ReadValue(), out double tBKnifeSwitchB) ? tBKnifeSwitchB : 0;
    //                    break;
    //            }
    //            cCTBModel.Lag_gConditiont = cCTBModel.DistanceBetweenAB / cCTBModel.VelocitySetting * 60;
    //            cCTBModel.Dearance_Left = double.Parse(sideConfig.PLCAddressConfigTD.Dearance_Left?.ReadValue() ?? "0");
    //            cCTBModel.Dearance_Right = double.Parse(sideConfig.PLCAddressConfigTD.Dearance_Right?.ReadValue() ?? "0");

    //            var path = UserDefineVariableInfo.DicVariables["CCTBConfigPath"].ToString();
    //            var fileName = UserDefineVariableInfo.DicVariables["CCTBIniFileName"].ToString();
    //            var fullPath = System.IO.Path.ChangeExtension(System.IO.Path.Combine(path, fileName), ".ini");
    //            var iniSampleHelper = new Sunwoda_IniSampleHelper(fullPath);
    //            var varietyName = iniSampleHelper.IniReadValue("品种信息", "VarietyName");
    //            cCTBModel.VarietyName = varietyName;

    //            #region 采集泵速设定值
    //            foreach (var plcAddress in sideConfig.PLCAddressConfigMD.PLCAddressLis)
    //            {
    //                try
    //                {
    //                    // 赋值初始值
    //                    cCTBModel.SetB[plcAddress.Pump] = (plcAddress.IsEnable, 0, -1);
    //                    #region PLC泵速
    //                    var speedValue = plcAddress?.SetPumpSpeedValue;
    //                    if (speedValue?.AddresObj == null) continue;
    //                    // 转换为decimal类型
    //                    var setBDouble = decimal.Parse(speedValue.AddresObj.DictValue);
    //                    // 不四舍五入
    //                    if (!cCTBModel.SetB_Round)
    //                    {
    //                        var pow = (decimal)Math.Pow(10, cCTBModel.SetB_Digits);
    //                        // 截取小数位长度
    //                        setBDouble = Math.Truncate(setBDouble * pow) / pow;
    //                    }
    //                    #endregion
    //                    #region PLC泵开关标识
    //                    var pumbSwitchTag = plcAddress?.PumbSwitchTag;
    //                    var setBSwitchTag = pumbSwitchTag?.AddresObj == null ? -1f : float.Parse(pumbSwitchTag.AddresObj.DictValue);
    //                    #endregion
    //                    cCTBModel.SetB[plcAddress.Pump] = (plcAddress.IsEnable, (float)Math.Round(setBDouble, cCTBModel.SetB_Digits), setBSwitchTag);
    //                }
    //                catch (Exception ex)
    //                {
    //                    _logger.Error($"[{plcAddress.PumpTag}]泵速设定值处理异常！", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                }
    //            }
    //            #endregion
    //            #region 获取T块信息
    //            var reLis = ClientSendAndRecieveData<object>($"涂布模头{sideConfig.SideType}", 1, "TINFOREUQEST", null);
    //            if (!(reLis != null && reLis.Count > 0 && reLis[0] is TInfoReponseData tInfoReponseData))
    //            {
    //                _logger.Error($"T块信息获取失败:[{JsonConvert.SerializeObject(cCTBModel.TInfo)}]");
    //                return;
    //            }
    //            cCTBModel.TInfo = tInfoReponseData;
    //            #endregion
    //        }

    //        /// <summary>
    //        /// 初始化闭环逻辑参数
    //        /// </summary>
    //        /// <param name="sideConfig">AB面配置</param>
    //        /// <param name="cCTBModel">数据</param>
    //        private CCTBMDTDModel CCTBCalculte_MDTD(SideConfigMDTD sideConfig, CCTBMDTDModel cCTBModel)
    //        {
    //            var reCCTBModel = cCTBModel;
    //            try
    //            {
    //                var weightList = CCTBMDTDModel.GetCache(sideConfig.SideType);
    //                #region 缓存数据处理
    //                // 获取开机/重启后的数据，同时剔除开头0数据，同时剔除固定开机数据 —— 具有相同数据源配置
    //                var weightLis_RebootByZero = GetCacheAfterReboot(weightList, reCCTBModel);
    //                // 异常数据间隔之后的数据，且剔除开头异常数据
    //                var weightLis_Filtrate = GetCacheAfterFiltrate(weightLis_RebootByZero, reCCTBModel);
    //                // 滞后数据之后的数据，滞后起点：(1、下发点；2、刀距变化点；3、闭环重启点)
    //                var weightLis_RemoveLag = GetCacheAfterLagging(sideConfig, weightLis_Filtrate, reCCTBModel);
    //                #endregion
    //                // 非剔除数据则继续进行规则计算
    //                if (reCCTBModel.Rid_sJudge == "N")
    //                {
    //                    Judge_Finally(sideConfig, weightLis_RemoveLag, ref reCCTBModel);
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"计算{sideConfig.SideType}面{cCTBModel.MessageTag}架[{cCTBModel?.WeightSource}]数据异常：", ex, isAddCore: true, eType, eProjectName, eLevel);
    //            }
    //            return reCCTBModel;
    //        }

    //        /// <summary>
    //        /// 滞后数据之后的数据，滞后起点：(1、下发点；2、刀距变化点；3、闭环重启点)
    //        /// </summary>
    //        /// <param name="dataLis"></param>
    //        /// <param name="cCTBModel"></param>
    //        /// <returns></returns>
    //        private List<CCTBMDTDModel> GetCacheAfterLagging(SideConfigMDTD sideConfig, List<CCTBMDTDModel> dataLis, CCTBMDTDModel cCTBModel)
    //        {
    //            if (dataLis?.Count <= 0) return dataLis;

    //            var reLis = new List<CCTBMDTDModel>();
    //            // 找到滞后起点
    //            var laggingStartIndex = 0;
    //            var laggingStartCondition = "滞后起点判断-未触发滞后";

    //            #region 下发点：是否触发调机
    //            var laggingStartIndex1 = dataLis.FindLastIndex(o => { return o.JudgTrigger == "Y"; });
    //            if (laggingStartIndex1 > laggingStartIndex)
    //            {
    //                laggingStartIndex = laggingStartIndex1;
    //                laggingStartCondition = "滞后起点判断-已触发调节";
    //            }
    //            #endregion

    //            #region 刀距变化点：左/右刀距位置发生变化
    //            var laggingStartIndex2 = dataLis.FindLastIndex(o =>
    //            {
    //                var isDearanceChanged = (Math.Abs(cCTBModel.Dearance_Left - o.Dearance_Left) >= cCTBModel.Lag_DearanceConditiont ||
    //                    Math.Abs(cCTBModel.Dearance_Right - o.Dearance_Right) >= cCTBModel.Lag_DearanceConditiont);
    //                return isDearanceChanged;
    //            });
    //            if (laggingStartIndex2 > laggingStartIndex)
    //            {
    //                laggingStartIndex = laggingStartIndex2;
    //                laggingStartCondition = "滞后起点判断-刀距设定值发生变化";
    //            }
    //            #endregion

    //            #region 闭环重启点：如果闭环重新开启(即：null/0/1 => !(null/0/1))
    //            var laggingStartIndex3 = dataLis.FindLastIndex(o =>
    //            {
    //                var isRestart = o.TInfo != null && cCTBModel.TInfo != null &&
    //                    ((o.TInfo?.TAdjustMode == 0 || o.TInfo?.TAdjustMode == 1) &&
    //                    (cCTBModel.TInfo?.TAdjustMode != 0 && o.TInfo?.TAdjustMode != 1));
    //                return isRestart;
    //            });
    //            if (laggingStartIndex3 > laggingStartIndex)
    //            {
    //                laggingStartIndex = laggingStartIndex3;
    //                laggingStartCondition = $"滞后起点判断-闭环重新开启[趟号 {dataLis[laggingStartIndex].DataNumber}]";
    //            }
    //            #endregion

    //            _logger.Info($"[{sideConfig.SideType}-{sideConfig.EClosedLoopType}]滞后起点：数据{laggingStartIndex + 1}/{dataLis?.Count}[趟号 {dataLis[laggingStartIndex].DataNumber}](条件:{laggingStartCondition})！", false);

    //            #region 滞后时间判断： 去掉滞后起点之后某段时间的数据
    //            var laggingEndIndex = dataLis.FindIndex(o =>
    //            {
    //                return (o.GatherTime - dataLis[laggingStartIndex].GatherTime).TotalSeconds > cCTBModel.Lag_gConditiont;
    //            });
    //            var index = laggingEndIndex >= 0 ? laggingEndIndex : dataLis.Count - 1;
    //            #endregion

    //            _logger.Info($"[{sideConfig.SideType}-{sideConfig.EClosedLoopType}]滞后起点：数据{index + 1}/{dataLis?.Count}[趟号 {dataLis[index].DataNumber}](条件:滞后起点判断-滞后时间[{cCTBModel.Lag_gConditiont}s])", false);
    //            reLis.AddRange(dataLis.Skip(index));

    //            return reLis;
    //        }

    //        /// <summary>
    //        /// 判定之后
    //        /// </summary>
    //        /// <returns></returns>
    //        private void Judge_Finally(SideConfigMDTD sideConfig, List<CCTBMDTDModel> dataLis, ref CCTBMDTDModel cCTBModel)
    //        {
    //            // 极差判定
    //            var re = GetNewBT(sideConfig, dataLis, ref cCTBModel);
    //            var controlType = sideConfig?.PIDObtain?.PythonCode?.Split('_')[1];
    //            if (re.re_newB) // 泵速判定
    //            {
    //                var isAdjust = false;
    //                if (controlType == "MDTD") isAdjust = (cCTBModel.NewBT_MDTD.result == 1 || cCTBModel.NewBT_MDTD.result == 3 || cCTBModel.NewBT_MDTD.result == 4);
    //                if (controlType == "MULTIMDTD") isAdjust = cCTBModel.NewBT_MULTIMDTD.newBTuple.result == 1;
    //                if (isAdjust) SetNewB(sideConfig, ref cCTBModel);
    //            }
    //            if (re.re_newT) // T块判定
    //            {
    //                var isAdjust = false;
    //                // 供应商上位机开关
    //                if (controlType == "MDTD") isAdjust = (cCTBModel.NewBT_MDTD.result == 2 || cCTBModel.NewBT_MDTD.result == 3 || cCTBModel.NewBT_MDTD.result == 4);
    //                if (controlType == "MULTIMDTD") isAdjust = cCTBModel.NewBT_MULTIMDTD.newTTuple.result == 1;
    //                if (isAdjust) SetNewT(sideConfig, ref cCTBModel);
    //            }
    //        }

    //        /// <summary>
    //        /// 获取T块调机值
    //        /// </summary>
    //        private (bool re_newB, bool re_newT) GetNewBT(SideConfigMDTD sideConfig, List<CCTBMDTDModel> dataLis, ref CCTBMDTDModel cCTBModel)
    //        {
    //            // 剔除异常数据
    //            var dl = dataLis?.FindAll(o => o?.Rid_sJudge == "N")?.ToList();
    //            var cCTBModelCount = cCTBModel.Control_DataCount;
    //            if (dl?.Count < cCTBModelCount)
    //            {
    //                cCTBModel.JudgTrigger = "N";
    //                cCTBModel.Judg_HandelResults = $"拦截判定:缓存数据[{dl?.Count}]不足[{cCTBModelCount}]";
    //                return (false, false);
    //            }
    //            var controlType = sideConfig?.PIDObtain?.PythonCode?.Split('_')[1];
    //            if (controlType == "MDTD")
    //            {
    //                var newBTTuple = GetMDTDControlNewBT(sideConfig, dl, cCTBModelCount, ref cCTBModel);
    //                // 0:异常；1:仅调泵速；2:仅调T块；3:泵速、T块都调；4:限位调节
    //                if (newBTTuple.result == 0 || newBTTuple.result == -1)
    //                {
    //                    cCTBModel.JudgTrigger = "N";
    //                    cCTBModel.Judg_HandelResults = $"拦截判定:{newBTTuple.result}-{newBTTuple.message}";
    //                    cCTBModel.NewBT_MDTD = newBTTuple;
    //                    return (false, false);
    //                }
    //                cCTBModel.JudgTrigger = "Y";
    //                cCTBModel.Judg_HandelResults = $"{newBTTuple.result}-{newBTTuple.message}";
    //                cCTBModel.NewBT_MDTD = newBTTuple;
    //                return (true, true);
    //            }
    //            else if (controlType == "MULTIMDTD")
    //            {
    //                var newBTTuple = GetMULTIMDTDControlNewBT(sideConfig, dl, cCTBModelCount, ref cCTBModel);
    //                // 0:异常；1:OK
    //                if ((newBTTuple.Item1.Item1 == 0 || newBTTuple.Item1.Item1 == -1)
    //                    && (newBTTuple.Item2.Item1 == 0 || newBTTuple.Item2.Item1 == -1))
    //                {
    //                    cCTBModel.JudgTrigger = "N";
    //                    cCTBModel.Judg_HandelResults = $"拦截判定:[泵速拦截:{newBTTuple.Item1.Item1}-{JsonConvert.SerializeObject(newBTTuple.Item1.Item2)}][T块拦截:{newBTTuple.Item2.Item1}-{JsonConvert.SerializeObject(newBTTuple.Item2.Item2)}]";
    //                    cCTBModel.NewBT_MULTIMDTD = newBTTuple;
    //                    return (false, false);
    //                }
    //                cCTBModel.JudgTrigger = "Y";
    //                cCTBModel.Judg_HandelResults = $"[泵速:{newBTTuple.Item1.Item1}-{JsonConvert.SerializeObject(newBTTuple.Item1.Item2)}][T块:{newBTTuple.Item2.Item1}-{JsonConvert.SerializeObject(newBTTuple.Item2.Item2)}]";
    //                cCTBModel.NewBT_MULTIMDTD = newBTTuple;
    //                return (newBTTuple.Item1.Item1 == 1, newBTTuple.Item2.Item1 == 1);
    //            }
    //            else
    //            {
    //                cCTBModel.JudgTrigger = "N";
    //                cCTBModel.Judg_HandelResults = $"拦截判定:未实现[{controlType}]算法！";
    //                return (false, false);
    //            }
    //        }

    //        /// <summary>
    //        /// MDTDControl算法获取T块调机量
    //        /// </summary>
    //        private (int result, string message, float newB, Dictionary<string, int> newT) GetMDTDControlNewBT(SideConfigMDTD sideConfig, List<CCTBMDTDModel> dataLis, int dataCount, ref CCTBMDTDModel cCTBModel)
    //        {
    //            var eTBPython = EArithmeticType.MDTDControl;
    //            var sw = new Stopwatch();
    //            sw.Start();
    //            var jj = (-1, "Python算法执行异常！", 0f, new Dictionary<string, int>());
    //            // 算法执行
    //            var func = new Func<string, (int result, string message, float newB, Dictionary<string, int> newT)?>((pythonCode) =>
    //            {
    //                try
    //                {
    //                    var dataStrLis = new List<List<string>>();
    //                    for (int i = 0; i < dataCount; i++)
    //                    {
    //                        if (i == 0) dataStrLis.Add(new List<string>());
    //                        dataStrLis.Add(new List<string>());

    //                        var index = dataLis.Count - i - 1;
    //                        if (i == 0) dataStrLis[0].Add("目标值");
    //                        dataStrLis[i + 1].Add(dataLis[index].TValue.ToString());

    //                        if (i == 0) dataStrLis[0].Add("公差");
    //                        dataStrLis[i + 1].Add(dataLis[index].Tolerance.ToString());

    //                        if (i == 0) dataStrLis[0].Add("泵速");
    //                        dataStrLis[i + 1].Add(dataLis[index].SetB.ContainsKey(EPump.泵0) ? dataLis[index].SetB[EPump.泵0].value.ToString() : "");

    //                        if (i == 0) dataStrLis[0].Add($"T块调机模式");
    //                        dataStrLis[i + 1].Add(dataLis[index].TInfo.TAdjustMode.ToString());

    //                        for (int j = 0; j < dataLis[index].DataArr.Length; j++)
    //                        {
    //                            if (i == 0) dataStrLis[0].Add($"重量{j + 1}");
    //                            dataStrLis[i + 1].Add(dataLis[index].DataArr[j].ToString());
    //                        }
    //                        var tBlockInfoLis = dataLis[index].TInfo.TBlockInfoLis.OrderBy(o => o.TId).ToList();
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-实时位置");
    //                            dataStrLis[i + 1].Add(tBlockInfoLis[j].TPosition.ToString());
    //                        }
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-上限值");
    //                            dataStrLis[i + 1].Add(tBlockInfoLis[j].TUpperLimit.ToString());
    //                        }
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-下限值");
    //                            dataStrLis[i + 1].Add(tBlockInfoLis[j].TLowerLimit.ToString());
    //                        }
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-报警标识");
    //                            dataStrLis[i + 1].Add($"[{string.Join(",", tBlockInfoLis[j].AlarmCode ?? new List<int>())}]");
    //                        }
    //                    }
    //                    var data = string.Join("\r\n", dataStrLis.ConvertAll(o => string.Join(",", o)));
    //                    var pyCode = "";
    //                    lock (PidFileInfoDic) pyCode = PidFileInfoDic[pythonCode].pythonStr;
    //                    var jjR = AddExcuteMDTDControlpyAction(pythonCode, pyCode, data);
    //                    _logger.Info($"{sideConfig.SideType}-{eTBPython}算法输出[{jjR.Item1}、{jjR.Item2}、{jjR.Item3}、{JsonConvert.SerializeObject(jjR.Item4)}]");
    //                    return jjR;
    //                }
    //                catch (Exception ex)
    //                {
    //                    _logger.Error($"{sideConfig.SideType}-{eTBPython}算法异常", ex);
    //                    return null;
    //                }
    //            });
    //            // 算法获取
    //            var re = RefreshAndExcuteTBPydAction<(int result, string message, float newB, Dictionary<string, int> newT)?>(eTBPython, sideConfig, cCTBModel.VarietyName, func);
    //            if (re != null) jj = ((int, string, float, Dictionary<string, int>))re;
    //            sw.Stop();
    //            _logger.Info($"{sideConfig.SideType}-{eTBPython}算法计算耗时[{sw.ElapsedMilliseconds}]", false);
    //            return jj;
    //        }

    //        /// <summary>
    //        /// MULTIMDTDControl算法获取T块调机量
    //        /// </summary>
    //        private ((int, Dictionary<string, string>, Dictionary<string, float>), (int, Dictionary<string, string>, Dictionary<string, int>)) GetMULTIMDTDControlNewBT(SideConfigMDTD sideConfig, List<CCTBMDTDModel> dataLis, int dataCount, ref CCTBMDTDModel cCTBModel)
    //        {
    //            var eTBPython = EArithmeticType.MULTIMDTDControl;
    //            var sw = new Stopwatch();
    //            sw.Start();
    //            var jj = ((-1, new Dictionary<string, string>(), new Dictionary<string, float>()), (-1, new Dictionary<string, string>(), new Dictionary<string, int>()));
    //            // 算法执行
    //            var func = new Func<string, ((int, Dictionary<string, string>, Dictionary<string, float>), (int, Dictionary<string, string>, Dictionary<string, int>))?>((pythonCode) =>
    //            {
    //                try
    //                {
    //                    var dataStrLis = new List<List<string>>();
    //                    for (int i = 0; i < dataCount; i++)
    //                    {
    //                        if (i == 0) dataStrLis.Add(new List<string>());
    //                        dataStrLis.Add(new List<string>());

    //                        var index = dataLis.Count - i - 1;
    //                        if (i == 0) dataStrLis[0].Add("时间");
    //                        dataStrLis[i + 1].Add(dataLis[index].GatherTime.ToString());

    //                        if (i == 0) dataStrLis[0].Add("趟数");
    //                        dataStrLis[i + 1].Add(dataLis[index].DataNumber.ToString());

    //                        if (i == 0) dataStrLis[0].Add("目标值");
    //                        dataStrLis[i + 1].Add(dataLis[index].TValue.ToString());

    //                        if (i == 0) dataStrLis[0].Add("公差");
    //                        dataStrLis[i + 1].Add(dataLis[index].Tolerance.ToString());

    //                        foreach (var setB in dataLis[index].SetB)
    //                        {
    //                            if (!setB.Value.isEnable) continue;
    //                            if (i == 0) dataStrLis[0].Add($"泵速{(int)setB.Key}");
    //                            dataStrLis[i + 1].Add(setB.Value.value.ToString());
    //                            if (i == 0) dataStrLis[0].Add($"浆料泵启动标识{(int)setB.Key}");
    //                            dataStrLis[i + 1].Add(setB.Value.pumbSwitchTag.ToString());
    //                        }

    //                        if (i == 0) dataStrLis[0].Add($"T块调机模式");
    //                        dataStrLis[i + 1].Add(dataLis[index].TInfo?.TAdjustMode.ToString());

    //                        for (int j = 0; j < dataLis[index].DataArr.Length; j++)
    //                        {
    //                            if (i == 0) dataStrLis[0].Add($"重量{j + 1}");
    //                            dataStrLis[i + 1].Add(dataLis[index].DataArr[j].ToString());
    //                        }
    //                        var tBlockInfoLis = dataLis[index].TInfo.TBlockInfoLis.OrderBy(o => o.TId).ToList();
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-实时位置");
    //                            dataStrLis[i + 1].Add(tBlockInfoLis[j].TPosition.ToString());
    //                        }
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-上限值");
    //                            dataStrLis[i + 1].Add(tBlockInfoLis[j].TUpperLimit.ToString());
    //                        }
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-下限值");
    //                            dataStrLis[i + 1].Add(tBlockInfoLis[j].TLowerLimit.ToString());
    //                        }
    //                        for (int j = 0; j < tBlockInfoLis.Count(); j++)
    //                        {
    //                            if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-报警标识");
    //                            dataStrLis[i + 1].Add($"[{string.Join(",", tBlockInfoLis[j].AlarmCode ?? new List<int>())}]");
    //                        }
    //                    }
    //                    var data = string.Join("\r\n", dataStrLis.ConvertAll(o => string.Join(",", o)));
    //                    var pyCode = "";
    //                    lock (PidFileInfoDic) pyCode = PidFileInfoDic[pythonCode].pythonStr;
    //                    var jjR = AddExcuteMULTIMDTDControlpyAction(pythonCode, pyCode, data);
    //                    _logger.Info($"{sideConfig.SideType}-{eTBPython}算法输出[{JsonConvert.SerializeObject(jjR)}]");
    //                    return jjR;
    //                }
    //                catch (Exception ex)
    //                {
    //                    _logger.Error($"{sideConfig.SideType}-{eTBPython}算法异常", ex);
    //                    return null;
    //                }
    //            });
    //            // 算法获取
    //            var re = RefreshAndExcuteTBPydAction<((int, Dictionary<string, string>, Dictionary<string, float>), (int, Dictionary<string, string>, Dictionary<string, int>))?>(eTBPython, sideConfig, cCTBModel.VarietyName, func);
    //            if (re != null) jj = (((int, Dictionary<string, string>, Dictionary<string, float>), (int, Dictionary<string, string>, Dictionary<string, int>)))re;
    //            sw.Stop();
    //            _logger.Info($"{sideConfig.SideType}-{eTBPython}算法计算耗时[{sw.ElapsedMilliseconds}]", false);
    //            return jj;
    //        }

    //        #region PythonNet
    //        #region MDTDControl算法
    //        /// <summary>
    //        /// 执行MDTDControl的py文件算法
    //        /// </summary>
    //        /// <param name="pythonCode"></param>
    //        /// <param name="pyCodeStr"></param>
    //        /// <returns></returns>
    //        private (int, string, float, Dictionary<string, int>) AddExcuteMDTDControlpyAction(string pythonCode, string pyCodeStr, string data)
    //        {
    //            var eTBPython = EArithmeticType.MDTDControl;
    //            var pyResult = (-1, "Python算法执行异常！", 0f, new Dictionary<string, int>());
    //            var pyKey = Guid.NewGuid().ToString();
    //            try
    //            {
    //                using (var mr = new ManualResetEvent(false))
    //                {
    //                    lock (PyManualResetEventDic)
    //                        PyManualResetEventDic[pyKey] = mr;

    //                    var func = new Func<dynamic, object, object>((pm, da) =>
    //                    {
    //                        // python更改为pyd
    //                        //dynamic po = pm.TDControl(da as string);
    //                        //var re = po.final_output();
    //                        dynamic re = pm.algorithm_run(da as string);
    //                        var reTp = PyTuple.AsTuple(re);
    //                        var reTp1 = (int)reTp[0];
    //                        var reTp2 = (string)reTp[1];
    //                        var reTp3 = (float)reTp[2];
    //                        var reTp4 = new PyDict(reTp[3]);
    //                        var reDic = reTp4.ToDictionary(o => o.ToString(), o => int.Parse(reTp4[o].ToString().Trim('[', ']')));
    //                        var pyRes = (reTp1, reTp2, reTp3, reDic);
    //                        return pyRes;
    //                    });
    //                    var obj = new object[] { pyKey, pythonCode, pyCodeStr, data, func };
    //                    PythonHelper.Instance.AddPyhtonAction((ExcuteTBPythonAction, obj));
    //                    mr.WaitOne();
    //                    lock (PyResultDic)
    //                    {
    //                        if (!PyResultDic.ContainsKey(pyKey) || PyResultDic[pyKey] == null)
    //                        {
    //                            if (PyResultDic.ContainsKey(pyKey))
    //                                PyResultDic.Remove(pyKey);

    //                            throw new Exception("算法输出结果异常！");
    //                        }

    //                        pyResult = ((int, string, float, Dictionary<string, int>))PyResultDic[pyKey];
    //                        PyResultDic.Remove(pyKey);
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"{eTBPython}算法文件[{pythonCode}]执行异常！", ex);
    //                throw new Exception($"{eTBPython}算法文件[{pythonCode}]执行异常！", ex);
    //            }
    //            finally
    //            {
    //                lock (PyManualResetEventDic)
    //                    if (PyManualResetEventDic.ContainsKey(pyKey))
    //                        PyManualResetEventDic.Remove(pyKey);
    //            }
    //            return pyResult;
    //        }
    //        #endregion
    //        #region MULTIMDTDControl算法
    //        /// <summary>
    //        /// 执行MULTIMDTDControl的py文件算法
    //        /// </summary>
    //        /// <param name="pythonCode"></param>
    //        /// <param name="pyCodeStr"></param>
    //        /// <returns></returns>
    //        private ((int, Dictionary<string, string>, Dictionary<string, float>), (int, Dictionary<string, string>, Dictionary<string, int>)) AddExcuteMULTIMDTDControlpyAction(string pythonCode, string pyCodeStr, string data)
    //        {
    //            var eTBPython = EArithmeticType.MULTIMDTDControl;
    //            var pyResult = ((-1, new Dictionary<string, string>(), new Dictionary<string, float>()), (-1, new Dictionary<string, string>(), new Dictionary<string, int>()));
    //            var pyKey = Guid.NewGuid().ToString();
    //            try
    //            {
    //                using (var mr = new ManualResetEvent(false))
    //                {
    //                    lock (PyManualResetEventDic)
    //                        PyManualResetEventDic[pyKey] = mr;

    //                    var func = new Func<dynamic, object, object>((pm, da) =>
    //                    {
    //                        // python更改为pyd
    //                        //dynamic po = pm.TDControl(da as string);
    //                        //dynamic re = po.final_output();
    //                        dynamic re = pm.algorithm_run(da as string);
    //                        var reTp = PyTuple.AsTuple(re);

    //                        var reTpB = PyTuple.AsTuple(reTp[0]);
    //                        var reTpB1 = (int)reTpB[0];
    //                        var reTpBDic1 = new PyDict(reTpB[1]);
    //                        var reBDic1 = reTpBDic1.ToDictionary(o => o.ToString(), o => reTpBDic1[o].ToString().Trim('[', ']'));
    //                        var reTpBDic2 = new PyDict(reTpB[2]);
    //                        var reBDic2 = reTpBDic2.ToDictionary(o => o.ToString(), o => float.Parse(reTpBDic2[o].ToString().Trim('[', ']')));

    //                        var reTpT = PyTuple.AsTuple(reTp[1]);
    //                        var reTpT1 = (int)reTpT[0];
    //                        var reTpTDic1 = new PyDict(reTpT[1]);
    //                        var reTDic1 = reTpTDic1.ToDictionary(o => o.ToString(), o => reTpTDic1[o].ToString().Trim('[', ']'));
    //                        var reTpTDic2 = new PyDict(reTpT[2]);
    //                        var reTDic2 = reTpTDic2.ToDictionary(o => o.ToString(), o => int.Parse(reTpTDic2[o].ToString().Trim('[', ']')));

    //                        var pyRes = ((reTpB1, reBDic1, reBDic2), (reTpT1, reTDic1, reTDic2));
    //                        return pyRes;
    //                    });
    //                    var obj = new object[] { pyKey, pythonCode, pyCodeStr, data, func };
    //                    PythonHelper.Instance.AddPyhtonAction((ExcuteTBPythonAction, obj));
    //                    mr.WaitOne();
    //                    lock (PyResultDic)
    //                    {
    //                        if (!PyResultDic.ContainsKey(pyKey) || PyResultDic[pyKey] == null)
    //                        {
    //                            if (PyResultDic.ContainsKey(pyKey))
    //                                PyResultDic.Remove(pyKey);

    //                            throw new Exception("算法输出结果异常！");
    //                        }

    //                        pyResult = (((int, Dictionary<string, string>, Dictionary<string, float>), (int, Dictionary<string, string>, Dictionary<string, int>)))PyResultDic[pyKey];
    //                        PyResultDic.Remove(pyKey);
    //                    }
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"{eTBPython}算法文件[{pythonCode}]执行异常！", ex);
    //                throw new Exception($"{eTBPython}算法文件[{pythonCode}]执行异常！", ex);
    //            }
    //            finally
    //            {
    //                lock (PyManualResetEventDic)
    //                    if (PyManualResetEventDic.ContainsKey(pyKey))
    //                        PyManualResetEventDic.Remove(pyKey);
    //            }
    //            return pyResult;
    //        }
    //        #endregion
    //        #endregion
    //        /// <summary>
    //        /// 新泵速下发PLC
    //        /// </summary>
    //        /// <param name="sideConfig">机架号</param>
    //        /// <param name="cCTBModel"></param>
    //        private void SetNewB(SideConfigMDTD sideConfig, ref CCTBMDTDModel cCTBModel)
    //        {
    //            var _cCTBModel = cCTBModel;
    //            var controlType = sideConfig?.PIDObtain?.PythonCode?.Split('_')[1];
    //            var TBPumoAdjustType = UserDefineVariableInfo.DicVariables["TBPumoAdjustType"].ToString();
    //            if (TBPumoAdjustType == "PLC")
    //            {
    //                // 游津、李果修改上川的PLC泵速下发逻辑(PLC开关地址为1、同时SC上位机标识为2/4）  --By 2210120078  20251209
    //                var isAdjust = cCTBModel.TInfo?.TAdjustMode == 2 || cCTBModel.TInfo?.TAdjustMode == 4;
    //                var switchAddress = sideConfig.PLCAddressConfigMD?.SwitchAddress;
    //                isAdjust = isAdjust && switchAddress?.ReadValue() == "1";
    //                sideConfig.PLCAddressConfigMD.PLCAddressLis.ForEach(o =>
    //                {
    //                    try
    //                    {
    //                        // 未配置/未开启则跳过
    //                        if (o == null || !o.IsEnable) return;

    //                        var adjustValue = 0f;
    //                        if (controlType == "MDTD") adjustValue = _cCTBModel.NewBT_MDTD.newB;
    //                        if (controlType == "MULTIMDTD")
    //                        {
    //                            if (_cCTBModel?.NewBT_MULTIMDTD.newBTuple.newB?.ContainsKey($"泵速{(int)o.Pump}") ?? false)
    //                                adjustValue = _cCTBModel.NewBT_MULTIMDTD.newBTuple.newB[$"泵速{(int)o.Pump}"];
    //                        }
    //                        adjustValue = (float)(adjustValue * _cCTBModel.SetB_Coefficient);
    //                        if (adjustValue == 0) return;
    //                        var jjDicValue = SetNewBCommon(sideConfig, adjustValue, o, isAdjust);
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.Error($"{sideConfig.EClosedLoopType}[{o.PumpTag}]新泵速下发PLC异常！", ex, type: eType, projectName: eProjectName, level: eLevel);
    //                    }
    //                });
    //            }
    //            else if (TBPumoAdjustType == "Socket")
    //            {
    //                var jjStrDic = new Dictionary<string, float>();
    //                sideConfig.PLCAddressConfigMD.PLCAddressLis.ForEach(o =>
    //                {
    //                    // 未配置/未开启则跳过
    //                    if (o == null || !o.IsEnable) return;
    //                    var adjustValue = 0f;
    //                    if (controlType == "MDTD") adjustValue = _cCTBModel.NewBT_MDTD.newB;
    //                    if (controlType == "MULTIMDTD")
    //                    {
    //                        if (_cCTBModel?.NewBT_MULTIMDTD.newBTuple.newB?.ContainsKey($"泵速{(int)o.Pump}") ?? false)
    //                            adjustValue = _cCTBModel.NewBT_MULTIMDTD.newBTuple.newB[$"泵速{(int)o.Pump}"];
    //                    }
    //                    jjStrDic[o.PumpTag] = (float)(adjustValue * _cCTBModel.SetB_Coefficient);
    //                });

    //                // 游津、李果修改上川的泵速下发逻辑(PLC供应商开关地址为1(若存在此地址)、同时SC上位机标识为3/4）  --By 2210120078  20251202
    //                var isAdjust = cCTBModel.TInfo?.TAdjustMode == 3 || cCTBModel.TInfo?.TAdjustMode == 4;
    //                var switchAddress_Supplier = sideConfig.PLCAddressConfigMD?.SwitchAddress_Supplier;
    //                isAdjust = isAdjust && (switchAddress_Supplier?.AddresObj == null || switchAddress_Supplier?.ReadValue() == "1");
    //                var re = SetNewBCommon(sideConfig, jjStrDic, isAdjust);
    //            }
    //            else
    //            {
    //                _logger.Error($"{sideConfig.EClosedLoopType}未实现[{TBPumoAdjustType}]通讯方式下发泵速！", type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //        }
    //        /// <summary>
    //        /// 下发T块调机值
    //        /// </summary>
    //        /// <param name="sideConfig">机架号</param>
    //        /// <param name="cCTBModel"></param>
    //        private void SetNewT(SideConfigMDTD sideConfig, ref CCTBMDTDModel cCTBModel)
    //        {
    //            var coefficient = cCTBModel.SetT_Coefficient;
    //            var newT = new Dictionary<string, int>();
    //            var controlType = sideConfig?.PIDObtain?.PythonCode?.Split('_')[1];
    //            if (controlType == "MDTD") (cCTBModel.NewBT_MDTD.newT ?? new Dictionary<string, int>()).ToList().ForEach(o => newT[o.Key] = (int)(o.Value * coefficient));
    //            if (controlType == "MULTIMDTD") (cCTBModel.NewBT_MULTIMDTD.newTTuple.newT ?? new Dictionary<string, int>()).ToList().ForEach(o => newT[o.Key] = (int)(o.Value * coefficient));
    //            SetNewTCommon(sideConfig, cCTBModel.DataNumber, cCTBModel.DataAvg, cCTBModel.TInfo, newT);
    //        }

    //        /// <summary>
    //        /// MDTD保存数据整理
    //        /// </summary>
    //        /// <param name="sideConfig"></param>
    //        /// <param name="cCTBModel"></param>
    //        /// <returns></returns>
    //        private Dictionary<string, string> UpdateMDTD(SideConfigMDTD sideConfig, CCTBMDTDModel cCTBModel)
    //        {
    //            var paramDic = new Dictionary<string, string>();
    //            try
    //            {
    //                if (cCTBModel == null) return paramDic;

    //                paramDic.Add("时间", cCTBModel.GatherTime.ToString("yyyy-MM-dd HH:mm:ss"));
    //                paramDic.Add("扫描类型", cCTBModel.ScanType);
    //                paramDic.Add("趟数", cCTBModel.DataNumber.ToString());
    //                paramDic.Add("目标值T", cCTBModel.TValue.ToString());
    //                paramDic.Add("基材值", cCTBModel.BaseMaterialValue.ToString());
    //                paramDic.Add("工艺公差", cCTBModel.Tolerance.ToString());
    //                paramDic.Add("报文目标值", cCTBModel.DynamicTargetValue.ToString());
    //                paramDic.Add("目标设定值", cCTBModel.TChange.ToString());
    //                paramDic.Add("目标值偏移量", cCTBModel.TOffset.ToString());

    //                paramDic.Add("是否异常参数", cCTBModel.Rid_sJudge.ToString());
    //                paramDic.Add("异常描述", cCTBModel.Rid_sJudgeDescription.ToString());
    //                paramDic.Add("判异-走带速度", cCTBModel.Rid_TBVelocityCondition.ToString());
    //                paramDic.Add("判异-阈值", cCTBModel.Rid_aThreshold.ToString());
    //                paramDic.Add("判异-判定个数", cCTBModel.Rid_bCount.ToString());
    //                paramDic.Add("是否触发调节", cCTBModel.JudgTrigger);
    //                paramDic.Add("触发调节描述", cCTBModel.Judg_HandelResults);
    //                paramDic.Add("闭环数据源类型", cCTBModel.WeightSource.ToString());
    //                paramDic.Add("膜区处理", cCTBModel.AreaType);
    //                paramDic.Add("刀距滞后变化值", cCTBModel.Lag_DearanceConditiont.ToString());
    //                paramDic.Add("滞后时间(s)", cCTBModel.Lag_gConditiont.ToString());

    //                paramDic.Add("AB面走带间隔距离_m", cCTBModel?.DistanceBetweenAB.ToString());
    //                paramDic.Add("PLC走带实时速度", cCTBModel.Velocity.ToString());
    //                paramDic.Add("PLC走带设定速度", cCTBModel.VelocitySetting.ToString());
    //                paramDic.Add("刀距设定值左", cCTBModel.Dearance_Left.ToString());
    //                paramDic.Add("刀距设定值右", cCTBModel.Dearance_Right.ToString());
    //                paramDic.Add("进退刀检测开关", cCTBModel.IsTBKnifeSwitchMonitor ? "开启" : "关闭");
    //                paramDic.Add("进退刀信号", cCTBModel.TBKnifeSwitch == 1 ? "进刀" : cCTBModel.TBKnifeSwitch == 0 ? "退刀" : $"{cCTBModel.TBKnifeSwitch}未知状态");

    //                if (UserDefineVariableInfo.DicVariables.ContainsKey($"机头{sideConfig.SideType}_浆料缓存罐液位"))
    //                    paramDic.Add($"{sideConfig.SideType}浆料液位", (UserDefineVariableInfo.DicVariables[$"机头{sideConfig.SideType}_浆料缓存罐液位"].ToString()));

    //                paramDic.Add($"{sideConfig.SideType}闭环开关信号", sideConfig.PLCAddressConfigMD?.SwitchAddress?.ReadValue());
    //                paramDic.Add($"{sideConfig.SideType}闭环开关信号_供应商", sideConfig.PLCAddressConfigMD?.SwitchAddress_Supplier?.ReadValue());

    //                foreach (var pump in sideConfig.PLCAddressConfigMD.PLCAddressLis)
    //                {
    //                    if (!pump.IsEnable) continue;
    //                    var pumpTag = pump.PumpTag;
    //                    paramDic.Add($"{sideConfig.SideType}泵速下发完成信号-{pumpTag}", pump?.SignalConfirm?.ReadValue());
    //                    paramDic.Add($"设定泵速实际值-{pumpTag}", pump?.SetPumpSpeedValue?.ReadValue());
    //                    paramDic.Add($"设定泵速处理值-{pumpTag}", cCTBModel.SetB.ContainsKey(pump.Pump) ? cCTBModel.SetB[pump.Pump].value.ToString() : "");
    //                    paramDic.Add($"浆料泵启动标识-{pumpTag}", cCTBModel.SetB.ContainsKey(pump.Pump) ? cCTBModel.SetB[pump.Pump].pumbSwitchTag.ToString() : "");
    //                    paramDic.Add($"新泵速B微调偏移值-{pumpTag}", pump?.Adjust?.ReadValue());
    //                    var re = decimal.TryParse(pump?.ActualPumpSpeedValue?.ReadValue(), out decimal actualPumpSpeedValue);
    //                    paramDic.Add($"实时泵速实际值-{pumpTag}", actualPumpSpeedValue.ToString());
    //                    // 不四舍五入
    //                    if (!cCTBModel.SetB_Round)
    //                    {
    //                        var pow = (decimal)Math.Pow(10, cCTBModel.SetB_Digits);
    //                        // 截取小数位长度
    //                        actualPumpSpeedValue = Math.Truncate(actualPumpSpeedValue * pow) / pow;
    //                    }
    //                    paramDic.Add($"实时泵速处理值-{pumpTag}", Math.Round(actualPumpSpeedValue, cCTBModel.SetB_Digits).ToString());
    //                }

    //                paramDic.Add("新泵速调节系数", cCTBModel.SetB_Coefficient.ToString());
    //                paramDic.Add("T块调节系数", cCTBModel.SetT_Coefficient.ToString());
    //                paramDic.Add("算法入参趟数", cCTBModel.Control_DataCount.ToString());
    //                var udcDic = GetUDCParam(sideConfig.ParamState);
    //                foreach (var kv in udcDic) paramDic.Add(kv.Key, kv.Value);

    //                var controlType = sideConfig?.PIDObtain?.PythonCode?.Split('_')[1];
    //                if (controlType == "MDTD") paramDic.Add("算法输出调机量", JsonConvert.SerializeObject(cCTBModel?.NewBT_MDTD));
    //                if (controlType == "MULTIMDTD") paramDic.Add("算法输出调机量", JsonConvert.SerializeObject(cCTBModel?.NewBT_MULTIMDTD));
    //                if (cCTBModel.TInfo != null)
    //                {
    //                    paramDic.Add("T块信息-设备编号", cCTBModel?.TInfo.DeviceName);
    //                    paramDic.Add("T块信息-调机模式", cCTBModel?.TInfo.TAdjustMode.ToString());
    //                    paramDic.Add("T块信息-T块总数", cCTBModel?.TInfo.TNumber.ToString());
    //                    paramDic.Add("T块信息-响应时间", cCTBModel?.TInfo.ReponseTime.ToString("yyyy-MM-dd HH:mm:ss"));
    //                    if (cCTBModel.TInfo.TBlockInfoLis != null)
    //                    {
    //                        var tBlockInfoLis = cCTBModel.TInfo.TBlockInfoLis.OrderBy(o => o.TId);
    //                        foreach (var tBlockInfo in tBlockInfoLis)
    //                            paramDic.Add($"T块[{tBlockInfo.TId}]调机模式", tBlockInfo.TAdjustMode ? "自动" : "半自动");
    //                        foreach (var tBlockInfo in tBlockInfoLis)
    //                            paramDic.Add($"T块[{tBlockInfo.TId}]实时位置", tBlockInfo.TPosition.ToString());
    //                        foreach (var tBlockInfo in tBlockInfoLis)
    //                            paramDic.Add($"T块[{tBlockInfo.TId}]调机量", tBlockInfo.TAdjustInfo?.TAdjustValue.ToString());
    //                        foreach (var tBlockInfo in tBlockInfoLis)
    //                            paramDic.Add($"T块[{tBlockInfo.TId}]调机时间", tBlockInfo.TAdjustInfo?.TAdjustTime.ToString());
    //                        foreach (var tBlockInfo in tBlockInfoLis)
    //                            paramDic.Add($"T块[{tBlockInfo.TId}]调机时趟数", tBlockInfo.TAdjustInfo?.TAdjustScanCount.ToString());
    //                        foreach (var tBlockInfo in tBlockInfoLis)
    //                            paramDic.Add($"T块[{tBlockInfo.TId}]上限位置", tBlockInfo.TUpperLimit.ToString());
    //                        foreach (var tBlockInfo in tBlockInfoLis)
    //                            paramDic.Add($"T块[{tBlockInfo.TId}]下限位置", tBlockInfo.TLowerLimit.ToString());
    //                        foreach (var tBlockInfo in tBlockInfoLis)
    //                            paramDic.Add($"T块[{tBlockInfo.TId}]报警编码", $"[{string.Join(",", tBlockInfo.AlarmCode ?? new List<int>())}]");
    //                    }
    //                }
    //                for (int k = 0; k < cCTBModel.DataArr?.Count(); k++)
    //                    paramDic.Add("重量" + (k + 1).ToString(), cCTBModel.DataArr[k].ToString());
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.Error($"{sideConfig.SideType}:生成异常", ex, type: eType, projectName: eProjectName, level: eLevel);
    //            }
    //            return paramDic;
    //        }
    //    }
    //    #endregion
}
