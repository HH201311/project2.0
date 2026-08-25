using HandyControl.Controls;
using HandyControl.Data;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Services;
using SUNWODA_SEVB.ViewModels.Windows.Project;
using SUNWODA_SEVB.Views.Pages.CCTB;
using System;
using System.Collections.Concurrent;
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
    [Module("VM_CCTBRecordingsPage", "系统记录", Order = 1, Category = "VM_CCTBRecordingsPage")]
    class VM_CCTBRecordingsPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CCTBRecordingsPage> _logger;
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
        public ICommand? SystemRecordCommand => new RelayCommand(() => NavigateToSpecialModule("VM_SystemRecordPage"));
        public ICommand? AdjustmentRecordCommand => new RelayCommand(() => NavigateToSpecialModule("VM_AdjustmentRecordPage"));
        public ICommand? AlarmRecordCommand => new RelayCommand(() => NavigateToSpecialModule("VM_AlarmRecordPage"));

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_CCTBRecordingsPage(
            ILoggerService<VM_CCTBRecordingsPage> logger,
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

        /// <summary>
        /// 初始化
        /// </summary>
        public override void OnInitialize()
        {
            // 初始化导航
            InitNavigation();

            base.OnInitialize();
        }

        /// <summary>
        /// 初始化导航
        /// </summary>
        private void InitNavigation()
        {
            NavigateToDefault();
        }

        /// <summary>
        /// 导航到默认界面
        /// </summary>
        /// <returns></returns>
        private void NavigateToDefault()
        {
            var defaultModule = _moduleManager.GetModule("VM_SystemRecordPage");
            if (defaultModule == null) return;

            defaultModule.IsSelected = true;
            SelectedModule = defaultModule;
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
        /// 异步导航到指定界面
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
        /// 导航完成时触发
        /// </summary>
        /// <param name="parameter"></param>
        public override void OnNavigatedTo(object? parameter)
        {
            NavigateToDefault();
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

