using DocumentFormat.OpenXml.Drawing;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Microsoft.VisualBasic.ApplicationServices;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RoleModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Core.Models.Services.Project.CCTB;
using SUNWODA_SEVB.Services;
using SUNWODA_SEVB.Services.Project.CCTB;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB.UserOperation;
using SUNWODA_SEVB.ViewModels.Dialogs.Common;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace SUNWODA_SEVB.ViewModels.Windows.Project
{
    public class VM_CCTBMainWindow : ViewModelBase
    {
        private readonly ILoggerService<VM_CCTBMainWindow> _logger;
        private readonly INavigationService _navigationService;
        private readonly IModuleManager _moduleManager;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly ICommunicationService _communicationService1;

        private Page? _navigationPage;
        private ModuleInfo? _selectedModule;
        private bool _isNavigating;
        private bool _isExiting = false;

        // 添加导航防抖
        private CancellationTokenSource? _navigationCts;
        private DateTime _lastNavigationTime = DateTime.MinValue;
        private const int NAVIGATION_DEBOUNCE_MS = 100;

        public Frame? NavigationFrame { get; set; }
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

        /// <summary>
        /// 是否正在退出
        /// </summary>
        public bool IsExiting
        {
            get => _isExiting;
            set => SetProperty(ref _isExiting, value);
        }

        public ICommand? ChangeAccountCommand => new RelayCommand(OnChangeAccountAsync);
        public ICommand? ChangePasswordCommand => new RelayCommand(OnChangePasswordCommandAsync);
        public ICommand? LogoutCommand => new RelayCommand(OnLogoutAsync);

        public ICommand? ControlSwitchoverCommand => new RelayCommand(OnClosedLoopSwitchChanged);
        public ICommand? MonitoringCommand => new RelayCommand(() => NavigateToSpecialModuleAsync("VM_CCTBMonitoringPage"));
        public ICommand? SettingsCommand => new RelayCommand(() => NavigateToSpecialModuleAsync("VM_CCTBSettingsPage"));
        public ICommand? RecordingsCommand => new RelayCommand(() => NavigateToSpecialModuleAsync("VM_CCTBRecordingsPage"));

        public VM_CCTBMainWindow(
            ILoggerService<VM_CCTBMainWindow> logger,
            INavigationService navigationService,
            IModuleManager moduleManager,
            IGlobalSettingRepository globalSettingRepository,
            IProjectSettingRepository projectSettingRepository,
            IUsersRepository usersRepository,
            ICommunicationService communicationService
        )
        {
            _logger = logger;
            _navigationService = navigationService;
            _moduleManager = moduleManager;
            _globalSettingRepository = globalSettingRepository;
            _projectSettingRepository = projectSettingRepository;
            _usersRepository = usersRepository;
            _communicationService1 = communicationService;

            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
            UserStateChangedEvent.OnUserStateChanged += OnUserStateChanged;

            _communicationService1.DeviceStatusChanged -= OnDeviceStatusChanged;
            _communicationService1.DeviceStatusChanged += OnDeviceStatusChanged;
        }

        /// <summary>
        /// 清理
        /// </summary>
        public override void OnCleanup()
        {
            CCTBCore.SettingMessageNotificationEvent -= OnSettingMessageChanged;
            base.OnCleanup();
        }

        /// <summary>
        /// 初始化导航系统
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public void InitNavigation()
        {
            // 初始化涂布闭环全局参数
            InitializeCCTBCore();

            // 初始化用户
            UserStateChangedEvent.Raise("Guest");

            // 显示加载提示
            IsNavigating = true;

            // 加载图不闭环所有模块
            _moduleManager.AutoRegisterModulesByName("VM_SummaryPage", "VM_TBDataPage", "VM_CCDDataPage");
            _moduleManager.AutoRegisterModulesByName("VM_CCTBMonitoringPage", "VM_CCTBSettingsPage", "VM_CCTBRecordingsPage");
            _moduleManager.AutoRegisterModulesByName("VM_ClosedLoopSettingPage", "VM_FunctionSettingPage", "VM_AlarmSettingPage", "VM_CommunicationSettingPage", "VM_PermissionSettingPage");
            _moduleManager.AutoRegisterModulesByName("VM_BasicFunctionsPage", "VM_SpecialFunctionsPage");
            _moduleManager.AutoRegisterModulesByName("VM_RoleManagementPage", "VM_UserManagementPage");
            _moduleManager.AutoRegisterModulesByName("VM_SystemRecordPage", "VM_AdjustmentRecordPage", "VM_AlarmRecordPage");

            // 创建实例（下列界面后台数据需要更新）
            _moduleManager.GetViewModel("VM_SummaryPage");
            _moduleManager.GetViewModel("VM_TBDataPage");
            _moduleManager.GetViewModel("VM_CCDDataPage");

            // 导航到默认界面
            NavigateToDefaultAsync();

            IsNavigating = false;
        }

        /// <summary>
        /// 导航到默认界面
        /// </summary>
        private async void NavigateToDefaultAsync()
        {
            var defaultModule = _moduleManager.GetModule("VM_CCTBMonitoringPage");
            if (defaultModule == null) return;

            await Application.Current.Dispatcher.InvokeAsync(
                () =>
                {
                    defaultModule.IsSelected = true;
                    SelectedModule = defaultModule;
                },
                DispatcherPriority.Normal
            );
        }

        /// <summary>
        /// 导航到指定界面-防抖
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
        /// 异步导航到指定界面
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
        /// 异步导航到指定界面
        /// </summary>
        /// <param name="moduleName"></param>
        private async void NavigateToSpecialModuleAsync(string moduleName)
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
        /// 当退出清理时触发
        /// </summary>
        /// <returns></returns>
        protected override async Task OnCleanupAsync()
        {
            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;

            _navigationCts?.Cancel();
            _navigationCts?.Dispose();

            await base.OnCleanupAsync();
        }

        /// <summary>
        /// 请求退出应用程序
        /// </summary>
        /// <returns>是否允许退出</returns>
        public async Task<bool> RequestExitAsync()
        {
            if (IsExiting)
                return true; // 避免重复执行

            IsExiting = true;

            try
            {
                // 1. 检查是否有运行中的模块需要确认
                bool hasRunningTasks = await CheckRunningTasksAsync();

                // 2. 弹窗确认
                string message = hasRunningTasks
                    ? "当前有任务正在运行，确定要退出吗？\n退出后当前进度将丢失！"
                    : "确定要退出软件吗？";

                var result = await Application.Current.Dispatcher.InvokeAsync(() =>
                    System.Windows.MessageBox.Show(
                        message,
                        "确认退出",
                        MessageBoxButton.YesNo,
                        hasRunningTasks ? MessageBoxImage.Warning : MessageBoxImage.Question
                    )
                );

                if (result == MessageBoxResult.No)
                {
                    IsExiting = false;
                    return false; // 用户取消退出
                }

                // 3. 执行清理工作
                await PerformCleanupAsync();

                _logger.Info("应用程序正常退出");
                return true; // 允许退出
            }
            catch (Exception ex)
            {
                _logger.Error("退出时发生错误", ex);
                IsExiting = false;

                // 即使发生错误也询问用户是否强制退出
                var forceExit = await Application.Current.Dispatcher.InvokeAsync(() =>
                    System.Windows.MessageBox.Show(
                        $"退出时发生错误：{ex.Message}\n\n是否强制退出？",
                        "退出错误",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Error
                    )
                );

                return forceExit == MessageBoxResult.Yes;
            }
        }

        /// <summary>
        /// 检查是否有运行中的任务
        /// </summary>
        private async Task<bool> CheckRunningTasksAsync()
        {
            try
            {
                // 检查所有已加载的模块是否有运行中的任务
                foreach (var module in _moduleManager.Modules)
                {
                    var viewModel = _moduleManager.GetViewModel(module);

                    // 如果 ViewModel 实现了特定接口，检查其状态
                    if (viewModel is ITaskRunningChecker checker)
                    {
                        if (await checker.HasRunningTasksAsync())
                        {
                            _logger.Info($"模块 {module.DisplayName} 有运行中的任务");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("检查运行任务时发生错误", ex);
                return false; // 检查失败时保守处理
            }
        }

        /// <summary>
        /// 执行退出前的清理工作
        /// </summary>
        private async Task PerformCleanupAsync()
        {
            try
            {
                // 1. 通知所有模块准备退出
                foreach (var module in _moduleManager.Modules)
                {
                    try
                    {
                        var viewModel = _moduleManager.GetViewModel(module);
                        if (viewModel is ViewModelBase vm)
                        {
                            vm.OnCleanup();
                            _logger.Info($"模块 {module.DisplayName} 清理完成");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"清理模块 {module.DisplayName} 时发生错误", ex);
                    }
                }

                // 2. 保存全局设置
                //_globalSettingRepository.SaveSettings();

                // 3. 取消所有导航任务
                _navigationCts?.Cancel();

                // 4. 执行 ViewModel 自身的清理
                await OnCleanupAsync();
            }
            catch (Exception ex)
            {
                _logger.Error("执行清理工作时发生错误", ex);
            }
        }

        /// <summary>
        /// 当用户角色变化时触发
        /// </summary>
        /// <param name="userAccount"></param>
        private void OnUserStateChanged(string userAccount)
        {
            var role = _usersRepository.GetByUserAccount(userAccount);
            if (role == null) return;

            NavigateToDefaultAsync();
        }

        #region 闭环开关
        /// <summary>
        /// 当业务闭环开关字段变化时触发
        /// </summary>
        /// <param name="arg"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void OnClosedLoopSwitchChanged(object? arg)
        {
            if (!(arg is string flag)) return;
            var globalContext = GlobalContextModel.Instance;
            (ESideType sideType, EClosedLoopType clType, bool clSwitch) = flag switch
            {
                "TDA" => (ESideType.A, EClosedLoopType.TD, globalContext.ClosedLoopSwitchTDA),
                "MDA" => (ESideType.A, EClosedLoopType.MD, globalContext.ClosedLoopSwitchMDA),
                "CCDA" => (ESideType.A, EClosedLoopType.MDTD, globalContext.ClosedLoopSwitchCCDA),
                "TDB" => (ESideType.B, EClosedLoopType.TD, globalContext.ClosedLoopSwitchTDB),
                "MDB" => (ESideType.B, EClosedLoopType.MD, globalContext.ClosedLoopSwitchMDB),
                "CCDB" => (ESideType.B, EClosedLoopType.MDTD, globalContext.ClosedLoopSwitchCCDB),
                _ => throw new NotImplementedException($"未实现此标识[{flag}]!")
            };
            var oldCLSwitch = !clSwitch;
            var result = _projectSettingRepository.SaveClosedLoopSwitchProjectParameters(clType, sideType, clSwitch);
            var message = $"{clType} 业务{sideType}面闭环开关切换{(result ? "成功" : "失败")}([{oldCLSwitch}=>{clSwitch}])";
            if (result)
            {
                _logger.Info(message);
                _logger.UserInfo($"用户切换了业务闭环开关状态：{message}");
            }
            else _logger.Error(message);
        }
        #endregion

        #region 设备状态
        private DeviceStatusModel wmDeviceStatus = new("涂布机"), cmDeviceStatus = new("测重仪"), dhADeviceStatus = new("模头A"), dhBDeviceStatus = new("模头B"), ccdDeviceStatus = new("CCD");
        public DeviceStatusModel WMDeviceStatus { get => wmDeviceStatus; set => SetProperty(ref wmDeviceStatus, value); }
        public DeviceStatusModel CMDeviceStatus { get => cmDeviceStatus; set => SetProperty(ref cmDeviceStatus, value); }
        public DeviceStatusModel DHADeviceStatus { get => dhADeviceStatus; set => SetProperty(ref dhADeviceStatus, value); }
        public DeviceStatusModel DHBDeviceStatus { get => dhBDeviceStatus; set => SetProperty(ref dhBDeviceStatus, value); }
        public DeviceStatusModel CCDDeviceStatus { get => ccdDeviceStatus; set => SetProperty(ref ccdDeviceStatus, value); }

        /// <summary>
        /// 更新设备状态
        /// </summary>
        private void OnDeviceStatusChanged(object? sender, DeviceStatusEventArgs args)
        {
            if (args == null || args.DeviceStatus == null) return;

            var settings = _communicationService1.Settings;
            var deviceStatus = args.DeviceStatus;
            switch (args.DeviceStatus.DeviceName)
            {
                case ICommunicationService.WMSocketServerDeviceName:
                    {
                        if (settings == null || settings.WMCommunicationMode != ECommunicationMode.服务端Socket) break;

                        wmDeviceStatus.DeviceName = "涂布机";
                        wmDeviceStatus.DeviceStatus = deviceStatus.DeviceStatus;
                        wmDeviceStatus.AcceptDeviceCount = deviceStatus.AcceptDeviceCount;
                        break;
                    }
                case ICommunicationService.WMSocketClientDeviceName:
                    {
                        if (settings == null || settings.WMCommunicationMode != ECommunicationMode.客户端Socket) break;

                        wmDeviceStatus.DeviceName = "涂布机";
                        wmDeviceStatus.DeviceStatus = deviceStatus.DeviceStatus;
                        wmDeviceStatus.AcceptDeviceCount = deviceStatus.AcceptDeviceCount;
                        break;
                    }
                case ICommunicationService.DHASocketClientDeviceName:
                    {
                        if (settings == null || settings.DHCommunicationMode != ECommunicationMode.服务端Socket) break;

                        dhADeviceStatus.DeviceName = "模头A";
                        dhADeviceStatus.DeviceStatus = deviceStatus.DeviceStatus;
                        dhADeviceStatus.AcceptDeviceCount = deviceStatus.AcceptDeviceCount;
                        break;
                    }
                case ICommunicationService.DHBSocketClientDeviceName:
                    {
                        if (settings == null || settings.DHCommunicationMode != ECommunicationMode.服务端Socket) break;

                        dhBDeviceStatus.DeviceName = "模头B";
                        dhBDeviceStatus.DeviceStatus = deviceStatus.DeviceStatus;
                        dhBDeviceStatus.AcceptDeviceCount = deviceStatus.AcceptDeviceCount;
                        break;
                    }
                case ICommunicationService.DHAMqttClientDeviceName:
                    {
                        if (settings == null || settings.DHCommunicationMode != ECommunicationMode.客户端Mqtt) break;

                        dhADeviceStatus.DeviceName = "模头A";
                        dhADeviceStatus.DeviceStatus = deviceStatus.DeviceStatus;
                        dhADeviceStatus.AcceptDeviceCount = deviceStatus.AcceptDeviceCount;
                        break;
                    }
                case ICommunicationService.DHBMqttClientDeviceName:
                    {
                        if (settings == null || settings.DHCommunicationMode != ECommunicationMode.客户端Mqtt) break;

                        dhBDeviceStatus.DeviceName = "模头B";
                        dhBDeviceStatus.DeviceStatus = deviceStatus.DeviceStatus;
                        dhBDeviceStatus.AcceptDeviceCount = deviceStatus.AcceptDeviceCount;
                        break;
                    }
                case ICommunicationService.CCDSocketServerDeviceName:
                    {
                        ccdDeviceStatus.DeviceName = "CCD";
                        ccdDeviceStatus.DeviceStatus = deviceStatus.DeviceStatus;
                        ccdDeviceStatus.AcceptDeviceCount = deviceStatus.AcceptDeviceCount;
                        break;
                    }
                case ICommunicationService.CMPLCDeviceName:
                    {
                        cmDeviceStatus.DeviceName = "测厚仪";
                        cmDeviceStatus.DeviceStatus = deviceStatus.DeviceStatus;
                        cmDeviceStatus.AcceptDeviceCount = deviceStatus.AcceptDeviceCount;
                        break;
                    }
            }
        }
        #endregion

        #region 用户操作
        /// <summary>
        /// 档切换用户登录时触发
        /// </summary>
        private async void OnChangeAccountAsync()
        {
            _logger.UserInfo("用户点击了 切换用户 按钮！");
            var userModel = new UserManagementModel();
            var password = string.Empty;
            await Dialog
                  .Show(new VM_UserLoginDialog().UIElement)
                  .Initialize<VM_UserLoginDialog>(vm =>
                  {
                      vm.UserModel = userModel;
                  })
                  .GetResultAsync<string?>()
                  .ContinueWith(um =>
                  {
                      password = um.Result;
                  });
            if (string.IsNullOrEmpty(password)) return;

            if (!string.IsNullOrEmpty(userModel.Account) && userModel.Account == CCTBCore.UserAccount)
            {
                Growl.WarningGlobal("当前账号已登录");
                return;
            }

            (bool isLoggedIn, UsersModel? user) = await LoginAsync(userModel?.Account, password);
            if (isLoggedIn)
            {
                await _usersRepository.UpdateAsync(user!);
                await _globalSettingRepository.UpdateSettingValueAsync(
                    "CurrentUserAccount",
                    user!.UserAccount
                );
                UserStateChangedEvent.Raise(user.UserAccount);
                _logger.UserInfo($"用户 {user.UserName}:{user.UserAccount} 在 {user.LastLoginTime:yyyy-MM-dd HH:mm:ss} 登录成功!");
                Growl.SuccessGlobal("登录成功");
            }
            else
            {
                _logger.UserWarn($"用户 {userModel?.Name}:{userModel?.Account} 登录失败！", true);
                Growl.WarningGlobal("账号或密码错误");
            }
        }

        /// <summary>
        /// 登录验证
        /// </summary>
        /// <param name="userAccount"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private async Task<(bool, UsersModel?)> LoginAsync(string? userAccount, string? password)
        {
            if (userAccount == null)
                return (false, null);

            var user = await _usersRepository.GetByUserAccountAsync(userAccount);
            if (user == null) return (false, user);

            var result = BCrypt.Net.BCrypt.Verify(password, user.Password);
            return (result, user);
        }

        /// <summary>
        /// 当退出登录时触发
        /// </summary>
        private async void OnLogoutAsync()
        {
            _logger.UserInfo("用户点击了 退出登录 按钮！");
            // 获取当前用户角色
            var userAccount = _globalSettingRepository.GetByName("CurrentUserAccount")?.Value ?? string.Empty;
            var role = _usersRepository.GetByUserAccount(userAccount);
            if (role == null) return;

            var result = await VM_CCTBMessageBoxDialog.ShowAsync($"注销用户 '{role.UserName}({role.UserAccount})' ?", "是否注销", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            _logger.UserInfo($"{userAccount} 在 {DateTime.Now:yyyy-MM-dd HH:mm:ss} 退出登录");
            await _globalSettingRepository.UpdateSettingValueAsync("CurrentUserAccount", "guest");
            UserStateChangedEvent.Raise("guest");
            Growl.SuccessGlobal("退出登录");
        }

        /// <summary>
        /// 当修改密码时触发
        /// </summary>
        private async void OnChangePasswordCommandAsync()
        {
            _logger.UserInfo("用户点击了 修改密码 按钮！");
            // 获取当前用户角色
            var userAccount = _globalSettingRepository.GetByName("CurrentUserAccount")?.Value ?? string.Empty;
            var user = await _usersRepository.GetByUserAccountAsync(userAccount);
            if (user != null)
            {
                string? newPassword = null;
                await Dialog
                    .Show(new VM_UserChangePasswordDialog().UIElement)
                    .Initialize<VM_UserChangePasswordDialog>(vm =>
                    {
                        vm.OldPassword = user.Password;
                    })
                    .GetResultAsync<string?>()
                    .ContinueWith(um => newPassword = um.Result);
                if (newPassword != null)
                {
                    user.Password = newPassword;
                    if (await _usersRepository.UpdateAsync(user))
                    {
                        _logger.UserInfo($"用户 {user.UserName}:{user.UserAccount} 修改密码成功！");
                        Growl.SuccessGlobal($"用户 {user.UserName}:{user.UserAccount} 修改密码成功！");
                        return;
                    }
                    else
                    {
                        _logger.Warn($"用户 {user.UserName}:{user.UserAccount} 修改密码失败！", true);
                        Growl.WarningGlobal($"用户 {user.UserName}:{user.UserAccount} 修改密码失败！");
                    }
                }
            }
            else
            {
                _logger.Warn($"{userAccount} 账号未找到！", true);
                Growl.WarningGlobal($"{userAccount} 账号未找到！");
            }
        }
        #endregion

        #region 全局&业务相关
        /// <summary>
        /// 初始化全局参数
        /// </summary>
        private void InitializeCCTBCore()
        {
            CCTBCore.RoleManagementConfigs = _projectSettingRepository.LoadRoleManagementConfigWithDefault();
            CCTBCore.AlarmConfigs = _projectSettingRepository.LoadAlarmConfigWithDefault();
            CCTBCore.AlarmParamConfig = _projectSettingRepository.LoadAlarmConfigParamProjectParameters();

            CCTBCore.ClosedLoopSettingMDA = _projectSettingRepository.LoadClosedLoopSettingProjectParameters(EClosedLoopType.MD, ESideType.A);
            CCTBCore.ClosedLoopSettingTDA = _projectSettingRepository.LoadClosedLoopSettingProjectParameters(EClosedLoopType.TD, ESideType.A);
            CCTBCore.ClosedLoopSettingMDTDA = _projectSettingRepository.LoadClosedLoopSettingProjectParameters(EClosedLoopType.MDTD, ESideType.A);
            CCTBCore.ClosedLoopSettingMDB = _projectSettingRepository.LoadClosedLoopSettingProjectParameters(EClosedLoopType.MD, ESideType.B);
            CCTBCore.ClosedLoopSettingTDB = _projectSettingRepository.LoadClosedLoopSettingProjectParameters(EClosedLoopType.TD, ESideType.B);
            CCTBCore.ClosedLoopSettingMDTDB = _projectSettingRepository.LoadClosedLoopSettingProjectParameters(EClosedLoopType.MDTD, ESideType.B);

            CCTBCore.GeneralSettings = _projectSettingRepository.LoadGeneralSettingsProjectParameters();
            CCTBCore.BasicFunctionSetting = _projectSettingRepository.LoadBasicFunctionSettingProjectParameters();
            CCTBCore.SpecialFunctionSetting = _projectSettingRepository.LoadSpecialFunctionSettingProjectParameters();
            CCTBCore.FtpConfig = _projectSettingRepository.LoadFtpConfigProjectParameters();

            CCTBCore.PLCGatherConfigOfA = _projectSettingRepository.LoadAPLCGatherConfig();
            CCTBCore.PLCGatherConfigOfB = _projectSettingRepository.LoadBPLCGatherConfig();
            CCTBCore.PLCGatherConfigOfPLC = _projectSettingRepository.LoadPLCGatherConfig();

            CCTBCore.SettingMessageNotificationEvent -= OnSettingMessageChanged;
            CCTBCore.SettingMessageNotificationEvent += OnSettingMessageChanged;
        }

        /// <summary>
        /// 当参数变化时更新全局缓存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSettingMessageChanged(object? sender, MessageEventArgs<object> e)
        {
            if (e.Message is List<RoleManagementModel> rms)
            {
                CCTBCore.RoleManagementConfigs = rms;
                return;
            }

            if (e.Message is List<AlarmConfigModel> acs)
            {
                CCTBCore.AlarmConfigs = acs;
                return;
            }

            if (e.Message is AlarmConfigParamModel acp)
            {
                CCTBCore.AlarmParamConfig = acp;
                return;
            }

            if (e.Message is ClosedLoopSettingModel pm)
            {
                switch (e.Tag)
                {
                    case "MDA": CCTBCore.ClosedLoopSettingMDA = pm; break;
                    case "TDA": CCTBCore.ClosedLoopSettingTDA = pm; break;
                    case "MDTDA": CCTBCore.ClosedLoopSettingMDTDA = pm; break;
                    case "MDB": CCTBCore.ClosedLoopSettingMDB = pm; break;
                    case "TDB": CCTBCore.ClosedLoopSettingTDB = pm; break;
                    case "MDTDB": CCTBCore.ClosedLoopSettingMDTDB = pm; break;
                    default: return;
                }
                return;
            }

            if (e.Message is GeneralSettingsModel gm)
            {
                CCTBCore.GeneralSettings = gm;
                return;
            }

            if (e.Message is BasicFunctionSettingModel bf)
            {
                CCTBCore.BasicFunctionSetting = bf;
                return;
            }

            if (e.Message is SpecialFunctionSettingModel sf)
            {
                CCTBCore.SpecialFunctionSetting = sf;
                return;
            }

            if (e.Message is List<int> configs)
            {
                switch (e.Tag)
                {
                    case "A": CCTBCore.PLCGatherConfigOfA = configs; break;
                    case "B": CCTBCore.PLCGatherConfigOfB = configs; break;
                    case "PLC": CCTBCore.PLCGatherConfigOfPLC = configs; break;
                    default: return;
                }
                return;
            }
        }
        #endregion
    }
}