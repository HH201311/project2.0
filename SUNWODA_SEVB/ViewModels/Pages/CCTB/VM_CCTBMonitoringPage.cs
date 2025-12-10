using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Services;
using SUNWODA_SEVB.ViewModels.Pages.HawkEye;
using SUNWODA_SEVB.Views.Pages.CCTB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB
{
    [Module("VM_CCTBMonitoringPage", "系统记录", Order = 0, Category = "VM_CCTBMonitoringPage")]
    class VM_CCTBMonitoringPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CCTBMonitoringPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        private Page? _navigationPage;
        private ModuleInfo? _selectedModule;
        private bool _isNavigating;
        private string _loadingMessage = "";

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

        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
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
            InitNavigation();
            base.OnInitialize();
        }

        private void InitNavigation()
        {
            // 显示加载提示
            IsNavigating = true;
            LoadingMessage = "正在初始化导航系统...";

            // 加载模块
            _moduleManager.AutoRegisterModulesByName("VM_SummaryPage", "VM_TBDataPage", "VM_CCDDataPage");

            SummaryCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_SummaryPage"));
            TBDataCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_TBDataPage"));
            CCDDataCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_CCDDataPage"));

            LoadingMessage = "";
            IsNavigating = false;
            NavigateToDefaultAsync();
        }

        private async void NavigateToDefaultAsync()
        {
            var defaultModule = _moduleManager.GetModule("VM_SummaryPage");
            if (defaultModule != null)
            {
                defaultModule.IsSelected = true;
                SelectedModule = defaultModule; 
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

                NavigationPage = _moduleManager.GetView(module.Name);
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

        protected override async Task OnCleanupAsync()
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();

            await base.OnCleanupAsync();
        }
    }
}
