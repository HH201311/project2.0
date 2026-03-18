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

namespace SUNWODA_SEVB.ViewModels.Windows.Project
{
    public class VM_CCTBMainWindow : ViewModelBase
    {
        private readonly ILoggerService<VM_CCTBMainWindow> _logger;
        private readonly INavigationService _navigationService;
        private readonly IModuleManager _moduleManager;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private ModuleInfo? _selectedModule;
        private bool _isNavigating;
        private string _loadingMessage = "";

        // 添加导航防抖
        private CancellationTokenSource? _navigationCts;
        private DateTime _lastNavigationTime = DateTime.MinValue;
        private const int NAVIGATION_DEBOUNCE_MS = 100;

        public Frame? NavigationFrame { get; set; }

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

        public ICommand? DeviceSwitchoverCommand { get; set; }
        public ICommand? ControlSwitchoverCommand { get; set; }
        public ICommand? MonitoringCommand { get; set; }
        public ICommand? SettingsCommand { get; set; }
        public ICommand? RecordingsCommand { get; set; }

        public VM_CCTBMainWindow(
            ILoggerService<VM_CCTBMainWindow> logger,
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

        protected override async Task OnInitializeAsync()
        {
            DeviceSwitchoverCommand = new RelayCommand(() => { });
            ControlSwitchoverCommand = new RelayCommand(() => { });
            await base.OnInitializeAsync();
        }

        public void InitNavigation()
        {
            // 显示加载提示
            IsNavigating = true;
            LoadingMessage = "正在初始化导航系统...";

            // 加载模块
            _moduleManager.AutoRegisterModulesByName("VM_CCTBMonitoringPage", "VM_CCTBSettingsPage", "VM_CCTBRecordingsPage");

            MonitoringCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_CCTBMonitoringPage"));
            SettingsCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_CCTBSettingsPage"));
            RecordingsCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_CCTBRecordingsPage"));

            // 订阅导航事件
            _navigationService.Navigated += OnNavigated;
            _navigationService.Navigating += OnNavigating;

            // 订阅模块注册事件
            _moduleManager.ModuleRegistered += OnModuleRegistered;

            // 初始化导航服务
            ((NavigationService)_navigationService).Initialize(NavigationFrame ?? throw new ArgumentNullException(nameof(NavigationFrame)));

            NavigateToDefaultAsync();

            LoadingMessage = "";
            IsNavigating = false;
        }

        private async void NavigateToDefaultAsync()
        {
            var defaultModule = _moduleManager.GetModule("VM_CCTBMonitoringPage");
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
        }

        private async void NavigateToSpecialModuleAsync(string moduleName)
        {
            if (IsNavigating)
                return;

            var module = _moduleManager.GetModule(moduleName);

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

        protected override async Task OnCleanupAsync()
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();

            _navigationService.Navigated -= OnNavigated;
            _navigationService.Navigating -= OnNavigating;
            _moduleManager.ModuleRegistered -= OnModuleRegistered;

            await base.OnCleanupAsync();
        }
    }
}