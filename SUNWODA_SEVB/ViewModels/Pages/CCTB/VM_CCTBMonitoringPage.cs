using ControlzEx.Standard;
using Dm;
using Dm.filter;
using DnsClient.Protocol;
using HalconDotNet;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools.Extension;
using Newtonsoft.Json;
using Python.Runtime;
using ScottPlot.ArrowShapes;
using SharpCompress.Common;
using SqlSugar;
using SUNWODA_SEVB.Component.UserControls.Project.CCTB;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AssistanceModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.BusinessModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Data.Repositories;
using SUNWODA_SEVB.PLC;
using SUNWODA_SEVB.Services;
using SUNWODA_SEVB.Services.Project.CCTB;
using SUNWODA_SEVB.Services.Project.CCTB.Business;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.Tool.Services.Socket;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB.RecodDialogs;
using SUNWODA_SEVB.ViewModels.Dialogs.JRCW;
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
using System.Runtime.Intrinsics.X86;
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
    [Module("VM_CCTBMonitoringPage", "生产监控", Order = 0, Category = "VM_CCTBMonitoringPage")]
    partial class VM_CCTBMonitoringPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CCTBMonitoringPage> _logger;
        private readonly IAppLogRepository _appLogRepository;
        private readonly IPLCService _plcService;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;
        private readonly ISocketServerManager _socketServerManager;
        private readonly IAlgorithmService _algorithmService;
        private readonly BusinessServiceManager _businessServiceManager;
        private readonly BusinessServiceFactory _businessServiceFactory;

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

        public ICommand? SummaryCommand => new RelayCommand(() => NavigateToSpecialModule("VM_SummaryPage"));
        public ICommand? TBDataCommand => new RelayCommand(() => NavigateToSpecialModule("VM_TBDataPage"));
        public ICommand? CCDDataCommand => new RelayCommand(() => NavigateToSpecialModule("VM_CCDDataPage"));
        public ICommand? AlarmRecordingsCommand => new RelayCommand(AlarmRecordingsOpenAsync);
        public ICommand? ControlRecordingsCommand => new RelayCommand(ControlRecordingsOpenAsync);

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_CCTBMonitoringPage(
            ILoggerService<VM_CCTBMonitoringPage> logger,
            IAppLogRepository appLogRepository,
            IPLCService plcService,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager,
            ISocketServerManager socketServerManager,
            IAlgorithmService algorithmService,
            BusinessServiceManager businessServiceManager,
            BusinessServiceFactory businessServiceFactory
        )
        {
            _logger = logger;
            _appLogRepository = appLogRepository;
            _plcService = plcService;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
            _socketServerManager = socketServerManager;
            _algorithmService = algorithmService;
            _businessServiceManager = businessServiceManager;
            _businessServiceFactory = businessServiceFactory;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <returns></returns>
        public override void OnInitialize()
        {
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
            // 闭环业务相关初始化
            await InitializeClosedLoopAsync();
            await base.OnInitializeAsync();
        }

        /// <summary>
        /// 初始化导航
        /// </summary>
        private void InitNavigation()
        {
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

                // 处理之前的界面的导航离开
                if (NavigationPage?.DataContext is ViewModelBase oldVM) oldVM.OnNavigatedFrom();

                NavigationPage = _moduleManager.GetView(module.Name);
                var viewModel = NavigationPage?.DataContext as ViewModelBase;
                viewModel?.OnNavigatedTo(null);

                _logger.UserInfo($"用户导航到了 {module.DisplayName} 界面！");
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
            base.OnNavigatedTo(parameter);
        }

        /// <summary>
        /// 清理
        /// </summary>
        /// <returns></returns>
        protected override async Task OnCleanupAsync()
        {
            // 清理后台业务逻辑
            await _businessServiceManager.StopBusinessAsync(EBusinessType.BackBusiness);
            await _businessServiceManager.StopBusinessAsync(EBusinessType.MDBusiness);
            await _businessServiceManager.StopBusinessAsync(EBusinessType.TDBusiness);
            await _businessServiceManager.StopBusinessAsync(EBusinessType.MDTDBusiness);

            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
            await base.OnCleanupAsync();
        }

        /// <summary>
        /// 报警记录
        /// </summary>
        private async void AlarmRecordingsOpenAsync()
        {
            _logger.UserInfo("用户打开了 报警记录 窗口！");
            var dialog = new VM_AlarmRecordDialog(_appLogRepository);
            await Dialog.Show(dialog.UIElement)
                .Initialize<VM_AlarmRecordDialog>(vm =>
                {
                    // 通过附加属性设置
                    var element = vm.UIElement;
                    if (element != null)
                    {
                        element.SetValue(System.Windows.Window.WindowStateProperty, WindowState.Normal);
                        element.SetCurrentValue(FrameworkElement.MarginProperty, new Thickness(0, -240, 0, 0));
                    }
                }).GetResultAsync<bool>();
        }

        /// <summary>
        /// 调机记录
        /// </summary>
        private async void ControlRecordingsOpenAsync()
        {
            test();

            _logger.UserInfo("用户打开了 控制记录 窗口！");
            var dialog = new VM_AdjustmentRecordDialog(_appLogRepository);
            await Dialog.Show(dialog.UIElement)
                .Initialize<VM_AdjustmentRecordDialog>(vm =>
                {
                    // 通过附加属性设置
                    var element = vm.UIElement;
                    if (element != null)
                    {
                        element.SetValue(System.Windows.Window.WindowStateProperty, WindowState.Normal);
                        element.SetCurrentValue(FrameworkElement.MarginProperty, new Thickness(0, -240, 0, 0));
                    }
                }).GetResultAsync<bool>();
        }

        private async void test()
        {
            var t = Task.Run(async () =>
            {
                var mdtdBusiness = _businessServiceFactory.GetService(EBusinessType.MDTDBusiness);
                var modelName = ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.WMSocketClientDeviceName];
                var rm = new Random();
                while (true)
                {
                    try
                    {
                        var cdA = new CoatingDataModel();
                        cdA.SideType = ESideType.A;
                        cdA.TargetValueA = 376;
                        cdA.ToleranceValueA = 4;
                        cdA.TargetValueB = 752;
                        cdA.ToleranceValueB = 4;
                        for (int i = 0; i < 25; i++)
                        {
                            cdA.Weights.Add((float)(rm.NextDouble() - 0.5) * 10 + 376);
                        }
                        CCTBCore.CoatingMessageNotificationEvent?.Invoke(mdtdBusiness, new MessageEventArgs<CoatingDataModel>() { Tag = modelName, Message = cdA });

                        var cdB = new CoatingDataModel();
                        cdB.SideType = ESideType.B;
                        cdB.TargetValueA = 376;
                        cdB.ToleranceValueA = 4;
                        cdB.TargetValueB = 752;
                        cdB.ToleranceValueB = 4;
                        for (int i = 0; i < 25; i++)
                        {
                            cdB.Weights.Add((float)(rm.NextDouble() - 0.5) * 10 + 752);
                        }
                        CCTBCore.CoatingMessageNotificationEvent?.Invoke(mdtdBusiness, new MessageEventArgs<CoatingDataModel>() { Tag = modelName, Message = cdB });

                        await Task.Delay(5000);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            });
        }
    }
    #endregion

    #region 业务代码
    #region 公共模块
    /// <summary>
    /// 公共模块
    /// </summary>
    partial class VM_CCTBMonitoringPage : ViewModelBase
    {
        /// <summary>
        /// 闭环初始化
        /// </summary>
        private async Task InitializeClosedLoopAsync()
        {
            try
            {
                // 初始化PLC配置
                await InitializePLCConfigAsync();

                // 初始化算法校验
                var (verityResult, verityMessage) = await _algorithmService.InitializeAlgorithmVerityAsync();
                if (!verityResult) RunOnUIThread(async () => { await VM_CCTBMessageBoxDialog.ErrorAsync(verityMessage, "算法校验提示"); });

                // 初始化业务逻辑代码
                await _businessServiceManager.StartBusinessAsync(EBusinessType.BackBusiness);
                await _businessServiceManager.StartBusinessAsync(EBusinessType.MDBusiness);
                await _businessServiceManager.StartBusinessAsync(EBusinessType.TDBusiness);
                await _businessServiceManager.StartBusinessAsync(EBusinessType.MDTDBusiness);
            }
            catch (Exception ex)
            {
                RunOnUIThread(async () =>
               {
                   await VM_CCTBMessageBoxDialog.FatalAsync(ex.Message, "初始化异常");
                   Process.GetCurrentProcess().Kill();
                   Environment.Exit(0);
               });
            }
        }

        #region 初始化
        /// <summary>
        /// 初始化PLC字符串颠倒（欧姆龙PLC需配置IsStringReverseByteWord）
        /// </summary>
        /// <returns></returns>
        private async Task InitializePLCConfigAsync()
        {
            var enabledConfigs = await _plcConfigRepository.GetEnabledConfigsAsync();
            var isStringReverseByteWord = _projectSettingRepository.LoadIsStringReverseByteWordProjectParameters();
            if (!isStringReverseByteWord) return;
            if (!enabledConfigs.Exists(o => o.Name == CCTBCore.PLCName)) return;

            var isCompleted = false;
            do
            {
                await Task.Delay(1000);
                var plc = _plcService.GetPLCByName(CCTBCore.PLCName);
                if (!(plc != null && plc.Device != null)) continue;

                plc.Device.ByteTransform.IsStringReverseByteWord = isStringReverseByteWord;
                isCompleted = true;
                _logger.Info($"[{CCTBCore.PLCName} {plc.IP} {plc.Port}]配置[IsStringReverseByteWord:{isStringReverseByteWord}]完成！");
            }
            while (!isCompleted);
        }
        #endregion
    }
    #endregion
    #endregion
}
