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
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBSettings.FunctionSetting
{
    [Module("VM_BasicFunctionsPage", "基础功能", Order = 3, Category = "VM_CCTBSettingsPage/VM_FunctionSettingPage/VM_BasicFunctionsPage")]
    class VM_BasicFunctionsPage : ViewModelBase
    {
        private readonly ILoggerService<VM_BasicFunctionsPage> _logger;
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
        public ICommand? ControlPageNavigateCommand => new RelayCommand(OnControlPageNavigate);

        /// <summary>
        /// 保存按钮
        /// </summary>
        public ICommand? ParamSaveCommand => new RelayCommand(OnParamSaveAsync);

        /// <summary>
        /// 取消按钮
        /// </summary>
        public ICommand? ParamCancleCommand => new RelayCommand(OnParamCancle);

        /// <summary>
        /// FTP手动上传
        /// </summary>
        public ICommand? FTPUpLoadCommand => new RelayCommand(OnFTPUpLoad);

        /// <summary>
        /// 膜卷号读取地址验证
        /// </summary>
        public ICommand? RollNumPathVerifyCommand => new RelayCommand(OnRollNumPathVerifyAsync);

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_BasicFunctionsPage(
            ILoggerService<VM_BasicFunctionsPage> logger,
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
            ControlPageName = "WMFunctionControl";
        }

        /// <summary>
        /// 功能参数界面切换时触发
        /// </summary>
        private void OnControlPageNavigate(object? arg)
        {
            if (!(arg is string pageName)) return;

            _logger.UserInfo($"用户导航到了 基础功能/{pageName} 界面！");

            LoadFunctionSettingModel();
        }

        /// <summary>
        /// 当参数保存时触发
        /// </summary>
        private async void OnParamSaveAsync()
        {
            var re = _projectSettingRepository.SaveBasicFunctionSettingProjectParameters(FunctionModel);
            if (re)
            {
                // 参数更新通知
                CCTBCore.SettingMessageNotificationEvent?.Invoke(this, new MessageEventArgs<object>()
                {
                    Tag = "基础功能参数保存成功！",
                    Message = FunctionModel
                });

                _logger.UserInfo($"用户保存了 基础功能/{ControlPageName} 参数修改！");
                await VM_CCTBMessageBoxDialog.InfoAsync("保存成功！", "保存提示");
            }
            else await VM_CCTBMessageBoxDialog.ErrorAsync("保存失败！", "保存提示");
        }

        /// <summary>
        /// 当参数保存取消时触发
        /// </summary>
        private void OnParamCancle()
        {

        }

        #region 基础功能参数
        private BasicFunctionSettingModel functionModel = new();
        private DateTime ftpUpLoadData = DateTime.Now;

        /// <summary>
        /// 基础功能参数模型
        /// </summary>
        public BasicFunctionSettingModel FunctionModel { get => functionModel; set => SetProperty(ref functionModel, value); }

        /// <summary>
        /// FTP手动上传日期
        /// </summary>
        public DateTime FTPUpLoadData { get => ftpUpLoadData; set => SetProperty(ref ftpUpLoadData, value); }

        /// <summary>
        /// 加载基础功能参数
        /// </summary>
        /// <returns></returns>
        private void LoadFunctionSettingModel()
        {
            var settingModel = _projectSettingRepository.LoadBasicFunctionSettingProjectParameters();
            if (settingModel == null) return;

            FunctionModel = settingModel;
        }

        /// <summary>
        /// 手动上传FTP时触发
        /// </summary>
        /// <param name="arg"></param>
        private void OnFTPUpLoad(object? arg)
        {

        }

        /// <summary>
        /// 点击验证膜卷号获取地址时触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnRollNumPathVerifyAsync(object? arg)
        {
            if (!(arg is string filePath)) return;

            try
            {
                var productSn = CCTBCore.GetProductSn(filePath);
                if (string.IsNullOrEmpty(productSn)) await VM_CCTBMessageBoxDialog.WarningAsync("获取的膜卷号为空值！！", "获取膜卷号失败");
                else await VM_CCTBMessageBoxDialog.InfoAsync($"膜卷号:[{productSn}]！", "获取膜卷号成功");
            }
            catch (Exception ex)
            {
                await VM_CCTBMessageBoxDialog.ErrorAsync(ex.Message, "获取膜卷号异常");
            }
        }
        #endregion
    }
}
