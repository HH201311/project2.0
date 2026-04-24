using HandyControl.Controls;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBSettings.FunctionSetting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBSettings
{
    [Module("VM_CommunicationSettingPage", "通讯设置", Order = 2, Category = "VM_CCTBSettingsPage/VM_CommunicationSettingPage")]
    class VM_CommunicationSettingPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CommunicationSettingPage> _logger;
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
        /// PLC参数保存按钮
        /// </summary>
        public ICommand? ParamPLCSaveCommand { get; set; }

        /// <summary>
        /// 取消按钮
        /// </summary>
        public ICommand? ParamCancleCommand { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_CommunicationSettingPage(
            ILoggerService<VM_CommunicationSettingPage> logger,
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
            ParamPLCSaveCommand = new RelayCommand(OnParamPLCSave);
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
            LoadCommunicationSettingModel();
            NavigateToDefault();
            base.OnNavigatedTo(parameter);
        }

        /// <summary>
        /// 导航到默认界面
        /// </summary>
        /// <returns></returns>
        private void NavigateToDefault()
        {
            ControlPageName = "WMCommunicationControl";
        }

        /// <summary>
        /// 通讯设置界面切换时触发
        /// </summary>
        private void OnControlPageNavigateCommand()
        {
            LoadCommunicationSettingModel();
        }

        /// <summary>
        /// 当参数保存时触发
        /// </summary>
        private void OnParamSave()
        {
            var re = _operationUtil.SaveCommunicationSettingProjectParamrers(CommunicationModel);
            if (re) MessageBox.Show("保存成功！请重启软件以更新配置！");
            else MessageBox.Show("保存失败");
        }

        /// <summary>
        /// 当PLC参数保存时触发
        /// </summary>
        private void OnParamPLCSave()
        {
            var model = _plcConfigRepository.GetList(o => o.Name == CCTBCore.PLCName).FirstOrDefault();
            var isExists = model != null;
            if (!isExists)
            {
                model = new PLCConfigModel();
                model.Name = CCTBCore.PLCName;
                model.BrandSpecificationProtocal = "Omron_Fins_TCP";
                model.CycleReadTime = 500;
                model.CycleWriteTime = 500;
                model.IsEnable = true;
            }

            model!.IP = CommunicationModel.CMIP ?? "192.168.0.1";
            model!.Port = CommunicationModel.CMPort;
            var re = isExists ? _plcConfigRepository.Update(model) : _plcConfigRepository.Add(model);
            if (re) MessageBox.Show("保存成功！请重启软件以更新配置！");
            else MessageBox.Show("保存失败");
        }

        /// <summary>
        /// 当参数保存取消时触发
        /// </summary>
        private void OnParamCancle()
        {

        }

        #region 通讯设置参数
        private CommunicationSettingModel communicationModel = new();

        /// <summary>
        /// 通讯设置参数模型
        /// </summary>
        public CommunicationSettingModel CommunicationModel { get => communicationModel; set => SetProperty(ref communicationModel, value); }

        /// <summary>
        /// 加载通讯设置参数
        /// </summary>
        /// <returns></returns>
        private void LoadCommunicationSettingModel()
        {
            var settingModel = _operationUtil.LoadCommunicationSettingProjectParamrers();
            if (settingModel == null) return;

            CommunicationModel = settingModel;
            var model = _plcConfigRepository.GetList(o => o.Name == CCTBCore.PLCName).FirstOrDefault();
            if (model == null)
            {
                CommunicationModel.CMIP = "192.168.0.1";
                CommunicationModel.CMPort = 9600;
            }
            else
            {
                CommunicationModel.CMIP = model.IP;
                CommunicationModel.CMPort = model.Port;
            }
        }
        #endregion
    }
}
