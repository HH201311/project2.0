using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using HandyControl.Controls;

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
        private readonly OperationUtil _operationUtil;

        private string? controlPageName;
        public string? ControlPageName { get => controlPageName; set => SetProperty(ref controlPageName, value); }

        /// <summary>
        /// 界面导航按钮
        /// </summary>
        public ICommand? ControlPageNavigateCommand { get; set; }

        /// <summary>
        /// 保存按钮
        /// </summary>
        public ICommand? ParamSaveCommand { get; set; }

        /// <summary>
        /// 取消按钮
        /// </summary>
        public ICommand? ParamCancleCommand { get; set; }

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

            _operationUtil = new OperationUtil(projectSettingRepository);
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public override void OnInitialize()
        {
            ControlPageNavigateCommand = new RelayCommand(OnControlPageNavigateCommand);
            ParamSaveCommand = new RelayCommand(OnParamSave);
            ParamCancleCommand = new RelayCommand(OnParamCancle);

            NavigateToDefault();
            base.OnInitialize();
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
        private void OnParamSave()
        {
            var re = _operationUtil.SaveSpecialFunctionSettingProjectParamrers(FunctionModel);
            if (re) MessageBox.Show("保存成功");
            else MessageBox.Show("保存失败");
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
            var settingModel = _operationUtil.LoadSpecialFunctionSettingProjectParamrers();
            if (settingModel == null) return;

            FunctionModel = settingModel;
        }
        #endregion
    }
}
