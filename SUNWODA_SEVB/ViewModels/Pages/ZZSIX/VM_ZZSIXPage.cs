using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;

namespace SUNWODA_SEVB.ViewModels.Pages.ZZSIX
{
    /// <summary>
    /// ZZSIX 项目主页面（基础结构 demo）。
    /// </summary>
    [Module("VM_ZZSIXPage", "ZZSIX")]
    public class VM_ZZSIXPage : ViewModelBase
    {
        private readonly ILoggerService<VM_ZZSIXPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;

        private string? _bindingString;

        public string? BindingString
        {
            get => _bindingString;
            set => SetProperty(ref _bindingString, value);
        }

        public VM_ZZSIXPage(
            ILoggerService<VM_ZZSIXPage> logger,
            IGlobalSettingRepository globalSettingRepository)
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
        }

        public override void OnInitialize()
        {
            BindingString = "ZZSIX 项目页面（demo）";
            _logger.Info("ZZSIX 页面初始化完成");
            base.OnInitialize();
        }
    }
}
