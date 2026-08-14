using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Services;

namespace SUNWODA_SEVB.ViewModels.Windows.Common
{
    public class VM_MainWindow : ViewModelBase
    {
        private readonly ILoggerService<VM_MainWindow> _logger;
        private readonly INavigationService _navigationService;
        private readonly IModuleManager _moduleManager;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private ModuleInfo? _selectedModule;
        private bool _isNavigating;
        private string _loadingMessage = "";
        private bool _isNavigationExpanded = false;
        private GridLength _navigationColumnDefinitionWidth;
        private bool _isExiting = false;

        // 添加导航防抖
        private CancellationTokenSource? _navigationCts;
        private DateTime _lastNavigationTime = DateTime.MinValue;
        private const int NAVIGATION_DEBOUNCE_MS = 100;

        public Frame? NavigationFrame { get; set; }

        public ObservableCollection<ModuleInfo>? MenuItems { get; set; }

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

        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        public bool IsNavigationExpanded
        {
            get => _isNavigationExpanded;
            set => SetProperty(ref _isNavigationExpanded, value);
        }
        /// <summary>
        /// 是否正在退出
        /// </summary>
        public bool IsExiting
        {
            get => _isExiting;
            set => SetProperty(ref _isExiting, value);
        }


        public ICommand? NavigateCommand { get; set; }
        public ICommand? SettingsCommand { get; set; }
        public ICommand? UserCommand { get; set; }
        public ICommand? ToggleNavigationStateCommand { get; set; }

        public VM_MainWindow(
            ILoggerService<VM_MainWindow> logger,
            INavigationService navigationService,
            IModuleManager moduleManager,
            IGlobalSettingRepository globalSettingRepository
        )
        {
            _logger = logger;
            _navigationService = navigationService;
            _moduleManager = moduleManager;
            _globalSettingRepository = globalSettingRepository;
        }

        public void InitNavigation()
        {
            // 显示加载提示
            IsNavigating = true;
            LoadingMessage = "正在初始化导航系统...";

            // 加载模块
            _moduleManager.AutoRegisterModules();

            //PreLoadModule();

            // 构建菜单 - 只显示普通模块
            MenuItems = new ObservableCollection<ModuleInfo>(
                _moduleManager.GetModulesByType(ModuleType.Normal)
            );

            NavigateCommand = new RelayCommand(ExecuteNavigate);
            SettingsCommand = new RelayCommand(
                () => NavigateToSpecialModuleAsync(ModuleType.Settings)
            );
            UserCommand = new RelayCommand(
                () => NavigateToSpecialModuleAsync(ModuleType.UserCenter)
            );
            ToggleNavigationStateCommand = new RelayCommand(ToggleNavigationState);

            // 订阅导航事件
            _navigationService.Navigated += OnNavigated;
            _navigationService.Navigating += OnNavigating;

            // 订阅模块注册事件
            _moduleManager.ModuleRegistered += OnModuleRegistered;

            // 初始化导航服务
            ((NavigationService)_navigationService).Initialize(
                NavigationFrame ?? throw new ArgumentNullException(nameof(NavigationFrame))
            );

            NavigateToDefaultAsync();

            LoadingMessage = "";
            IsNavigating = false;
        }

        private void PreLoadModule()
        {
            foreach (var module in _moduleManager.Modules)
            {
                if (
                    module.Type == ModuleType.Dashboard
                    || module.Type == ModuleType.Settings
                    || module.Type == ModuleType.UserCenter
                    || module.Order < 2
                )
                {
                    _moduleManager.GetView(module);
                }
            }
            _logger.Info("预加载业务模块完成");
        }

        private async void NavigateToDefaultAsync()
        {
            var normalModules = _moduleManager.GetModulesByType(ModuleType.Normal);
            var dashboardModules = _moduleManager.GetModulesByType(ModuleType.Dashboard);
            var defaultProject = _globalSettingRepository.GetSettingValue("DefaultProject");
            var defaultModule =
                normalModules.FirstOrDefault(module => module.ViewModelType?.Name == defaultProject)
                ?? dashboardModules.FirstOrDefault()
                ?? MenuItems?.FirstOrDefault();

            if (defaultModule != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(
                    () =>
                    {
                        defaultModule.IsSelected = true;
                        _navigationService.NavigateTo(defaultModule.Name ?? "");
                    },
                    DispatcherPriority.Normal
                );
            }
        }

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

        private async void NavigateToModuleAsync(ModuleInfo module)
        {
            if (string.IsNullOrEmpty(module.Name))
                return;

            try
            {
                IsNavigating = true;
                LoadingMessage = $"正在加载 {module.DisplayName}...";

                _navigationService.NavigateTo(module.Name);
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
                LoadingMessage = "";
            }
        }

        private void OnNavigating(object? sender, NavigatingEventArgs e)
        {
            // 导航开始时的处理
            IsNavigating = true;
            LoadingMessage = $"正在加载...";
        }

        private void OnNavigated(object? sender, NavigationEventArgs e)
        {
            _logger.Info($"导航完成: {e.ModuleName}");

            // 更新选中状态
            _selectedModule = _moduleManager.GetModule(e.ModuleName ?? "");
            OnPropertyChanged(nameof(SelectedModule));

            // 清除加载状态
            IsNavigating = false;
            LoadingMessage = "";
        }

        private void OnModuleRegistered(object? sender, ModuleRegisteredEventArgs e)
        {
            if (e.Module?.Type == ModuleType.Normal)
            {
                App.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(() =>
                    {
                        MenuItems?.Add(e.Module);
                    })
                );
            }
        }

        private void ExecuteNavigate(object? parameter)
        {
            if (parameter is ModuleInfo module && !IsNavigating)
            {
                // 更新选中状态
                if (SelectedModule != null)
                    SelectedModule.IsSelected = false;
                module.IsSelected = true;

                // 异步导航
                NavigateToModuleAsync(module);
            }
        }

        private async void NavigateToSpecialModuleAsync(ModuleType moduleType)
        {
            if (IsNavigating)
                return;

            var modules = _moduleManager.GetModulesByType(moduleType);
            var module = modules.FirstOrDefault();

            if (module != null)
            {
                if (SelectedModule != null)
                    SelectedModule.IsSelected = false;

                await Task.Run(() =>
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            _navigationService.NavigateTo(module.Name ?? "");
                        })
                    );
                });
            }
        }

        private void ToggleNavigationState(object? sender)
        {
            var navigationColumnDefinition = sender as ColumnDefinition;
            if (navigationColumnDefinition != null)
            {
                IsNavigationExpanded = IsNavigationExpanded ? false : true;

                if (!IsNavigationExpanded)
                {
                    double targetValue = -navigationColumnDefinition.MaxWidth;
                    _navigationColumnDefinitionWidth = navigationColumnDefinition.Width;

                    navigationColumnDefinition.MinWidth = 0;
                    navigationColumnDefinition.Width = new GridLength();
                }
                else
                {
                    double targetValue = navigationColumnDefinition.Width.Value;

                    navigationColumnDefinition.MinWidth = 200;
                    navigationColumnDefinition.Width = _navigationColumnDefinitionWidth;
                }
            }
        }

        private void NavigateToSettings() => NavigateToSpecialModuleAsync(ModuleType.Settings);

        private void NavigateToUser() => NavigateToSpecialModuleAsync(ModuleType.UserCenter);

        protected override async Task OnCleanupAsync()
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();

            _navigationService.Navigated -= OnNavigated;
            _navigationService.Navigating -= OnNavigating;
            _moduleManager.ModuleRegistered -= OnModuleRegistered;

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
                    MessageBox.Show(
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
                    MessageBox.Show(
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
            LoadingMessage = "正在清理资源...";

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

                LoadingMessage = "";
            }
            catch (Exception ex)
            {
                _logger.Error("执行清理工作时发生错误", ex);
            }
        }



    }
}