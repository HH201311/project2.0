using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.ViewModels.Pages.HawkEye;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB
{
    [Module("VM_CCTBSettingsPage", "系统设置", Order = 0, Category = "VM_CCTBSettingsPage")]
    class VM_CCTBSettingsPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CCTBSettingsPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        private ConcurrentDictionary<string, Page?> _navigationPages = [];
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
        public ICommand? ClosedLoopSettingCommand { get; set; }
        public ICommand? FunctionSettingCommand { get; set; }
        public ICommand? AlarmSettingCommand { get; set; }
        public ICommand? CommunicationSettingCommand { get; set; }
        public ICommand? PermissionSettingCommand { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_CCTBSettingsPage(
            ILoggerService<VM_CCTBSettingsPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
        }

        public override void OnInitialize()
        {
            // 初始化导航
            _ = InitNavigationAsync();

            base.OnInitialize();
        }

        private async Task InitNavigationAsync()
        {
            // 显示加载提示
            IsNavigating = true;

            // 加载模块
            _moduleManager.AutoRegisterModulesByName("VM_ClosedLoopSettingPage", "VM_FunctionSettingPage", "VM_AlarmSettingPage", "VM_CommunicationSettingPage", "VM_PermissionSettingPage");

            InitializeNavigationPages("VM_ClosedLoopSettingPage", "VM_FunctionSettingPage", "VM_AlarmSettingPage", "VM_CommunicationSettingPage", "VM_PermissionSettingPage");

            ClosedLoopSettingCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_ClosedLoopSettingPage"));
            FunctionSettingCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_FunctionSettingPage"));
            AlarmSettingCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_AlarmSettingPage"));
            CommunicationSettingCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_CommunicationSettingPage"));
            PermissionSettingCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_PermissionSettingPage"));

            IsNavigating = false;

            await NavigateToDefaultAsync();
        }

        private Task NavigateToDefaultAsync()
        {
            var defaultModule = _moduleManager.GetModule("VM_ClosedLoopSettingPage");
            if (defaultModule != null)
            {
                defaultModule.IsSelected = true;
                SelectedModule = defaultModule;
            }
            return Task.CompletedTask;
        }

        private void InitializeNavigationPages(params string[] moduleNames)
        {
            foreach (var moduleName in moduleNames)
            {
                var module = _moduleManager.GetModule(moduleName);
                if (module == null) continue;
                if (string.IsNullOrEmpty(module.Name)) continue;
                if (_navigationPages.ContainsKey(module.Name)) continue;

                NavigationPage = _moduleManager.GetView(module.Name);
                _navigationPages[module.Name] = NavigationPage;
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
                if (_navigationPages.TryGetValue(module.Name, out Page? navigationPage) && navigationPage != null)
                {
                    NavigationPage = navigationPage;
                    var viewModel = navigationPage.DataContext as ViewModelBase;
                    viewModel?.OnNavigatedTo(null);
                    return;
                }
                NavigationPage = _moduleManager.GetView(module.Name);
                _navigationPages[module.Name] = NavigationPage;
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

        private async void NavigateToSpecialModuleAsync(string moduleName)
        {
            if (IsNavigating)
                return;

            var module = _moduleManager.GetModule(moduleName);

            if (module != null)
            {
                if (SelectedModule != null)
                    SelectedModule.IsSelected = false;
                SelectedModule = module;
            }
        }

        public override void OnNavigatedTo(object? parameter)
        {
            base.OnNavigatedTo(parameter);
        }

        protected override async Task OnCleanupAsync()
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();

            await base.OnCleanupAsync();
        }
    }
}
