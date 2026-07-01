using HandyControl.Controls;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using SUNWODA_SEVB.Tool.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBSettings.FunctionSetting
{
    [Module("VM_SpecialFunctionsPage", "特殊功能", Order = 3, Category = "VM_CCTBSettingsPage/VM_FunctionSettingPage/VM_SpecialFunctionsPage")]
    class VM_SpecialFunctionsPage : ViewModelBase
    {
        private readonly ILoggerService<VM_SpecialFunctionsPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        private string? controlPageName;
        public string? ControlPageName { get => controlPageName; set => SetProperty(ref controlPageName, value); }

        /// <summary>
        /// 界面导航按钮
        /// </summary>
        public ICommand? ControlPageNavigateCommand => new RelayCommand(OnControlPageNavigateCommand);

        /// <summary>
        /// 保存按钮
        /// </summary>
        public ICommand? ParamSaveCommand => new RelayCommand(OnParamSaveAsync);

        /// <summary>
        /// 取消按钮
        /// </summary>
        public ICommand? ParamCancleCommand => new RelayCommand(OnParamCancle);

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_SpecialFunctionsPage(
            ILoggerService<VM_SpecialFunctionsPage> logger,
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
        /// 导航结束时触发
        /// </summary>
        /// <param name="parameter"></param>
        public override void OnNavigatedTo(object? parameter)
        {
            LoadFunctionSettingModel();
            NavigateToDefault();
            base.OnNavigatedTo(parameter);
        }

        /// <summary>
        /// 导航到默认界面
        /// </summary>
        /// <returns></returns>
        private void NavigateToDefault()
        {
            ControlPageName = "ICFunctionControl";
        }

        /// <summary>
        /// 功能参数界面切换时触发
        /// </summary>
        private void OnControlPageNavigateCommand()
        {
            LoadFunctionSettingModel();
        }

        /// <summary>
        /// 当参数保存时触发
        /// </summary>
        private async void OnParamSaveAsync()
        {
            var re = _projectSettingRepository.SaveSpecialFunctionSettingProjectParameters(FunctionModel);
            if (re) await VM_CCTBMessageBoxDialog.InfoAsync("保存成功！", "保存提示");
            else await VM_CCTBMessageBoxDialog.ErrorAsync("保存失败！", "保存提示");
        }

        /// <summary>
        /// 当参数保存取消时触发
        /// </summary>
        private void OnParamCancle()
        {

        }

        #region 特殊功能参数
        private SpecialFunctionSettingModel functionModel = new();

        /// <summary>
        /// 特殊功能参数模型
        /// </summary>
        public SpecialFunctionSettingModel FunctionModel { get => functionModel; set => SetProperty(ref functionModel, value); }

        /// <summary>
        /// 加载特殊功能参数
        /// </summary>
        /// <returns></returns>
        private void LoadFunctionSettingModel()
        {
            var settingModel = _projectSettingRepository.LoadSpecialFunctionSettingProjectParameters();
            if (settingModel == null) return;

            FunctionModel = settingModel;
        }
        #endregion
    }
}
