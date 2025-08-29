using System.Collections.ObjectModel;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Data.Models;
using SUNWODA_SEVB.PLC;

namespace SUNWODA_SEVB.ViewModels.Pages.Demo
{
    [Module("VM_DevelopProjectDemoPage", "业务项目开发Demo")]
    class VM_DevelopProjectDemoPage : ViewModelBase
    {
        private readonly ILoggerService<VM_DevelopProjectDemoPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;

        private string? bindingString;

        public string? BindingString
        {
            get => bindingString;
            set => SetProperty(ref bindingString, value);
        }

        public VM_DevelopProjectDemoPage(
            ILoggerService<VM_DevelopProjectDemoPage> logger,
            IGlobalSettingRepository globalSettingRepository
        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
        }

        public override void OnInitialize()
        {
            // ViewModel初始化完成后调用
            BindingString = "业务开发演示";
            // 重写OnInitialize方法，这里可以初始化一些变量数据，还有一个异步方法OnInitializeAsync
            base.OnInitialize();
        }

        public override void OnNavigatedFrom()
        {
            // 导航离开前调用
            // 清理一些数据，以节省内存资源，还有一个异步方法OnNavigatedFromAsync
            base.OnNavigatedFrom();
        }

        public override void OnNavigatedTo(object? parameter)
        {
            // 导航完成后调用，初始加载一些需要绑定的数据，还有一个异步方法OnNavigatedToAsync
            base.OnNavigatedTo(parameter);
        }

        public override void OnCleanup()
        {
            // ViewModel缓存回收时调用，释放资源使用，还有一个异步方法OnCleanupAsync
            base.OnCleanup();
        }

        public override bool CanNavigateFrom()
        {
            // 可自定义设置当前页面是否可以导航离开，还有一个异步方法CanNavigateFromAsync
            return base.CanNavigateFrom();
        }
    }
}
