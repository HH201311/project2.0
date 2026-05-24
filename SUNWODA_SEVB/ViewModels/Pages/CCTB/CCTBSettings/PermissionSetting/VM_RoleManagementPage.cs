using HandyControl.Controls;
using Masuit.Tools;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Component.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RoleModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBSettings.PermissionSetting
{
    [Module("VM_RoleManagementPage", "角色管理", Order = 3, Category = "VM_CCTBSettingsPage/VM_PermissionSettingPage/VM_RoleManagementPage")]
    class VM_RoleManagementPage : ViewModelBase
    {
        private readonly ILoggerService<VM_RoleManagementPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;
        private readonly OperationUtil _operationUtil;

        /// <summary>
        /// 保存按钮
        /// </summary>
        public ICommand? ParamSaveCommand => new RelayCommand(OnParamSaveAsync);

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_RoleManagementPage(
            ILoggerService<VM_RoleManagementPage> logger,
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
        /// 导航结束时触发
        /// </summary>
        /// <param name="parameter"></param>
        public override void OnNavigatedTo(object? parameter)
        {
            LoadRoleManagementSettings();
            base.OnNavigatedTo(parameter);
        }

        /// <summary>
        /// 当参数保存时触发
        /// </summary>
        private async void OnParamSaveAsync()
        {
            var re = _operationUtil.SaveRoleManagementConfigProjectParamrers(RoleManagementConfigs.ToList());
            if (re) await VM_CCTBMessageBoxDialog.InfoAsync("保存成功！", "保存提示");
            else await VM_CCTBMessageBoxDialog.ErrorAsync("保存失败！", "保存提示");
        }

        #region 角色管理表格
        /// <summary>
        /// 用户权限枚举数据源
        /// </summary>
        public ObservableCollection<ComboboxItemModel<EUserPermission>> UserPermissionSource { get; } =
                 new ObservableCollection<ComboboxItemModel<EUserPermission>>(
                 Enum.GetValues(typeof(EUserPermission)).Cast<EUserPermission>()
                 .Select(v => new ComboboxItemModel<EUserPermission>() { ItemValue = v, DisplayName = v.ToString() }));


        /// <summary>
        /// 角色管理信息
        /// </summary>
        public ObservableCollection<RoleManagementModel> RoleManagementConfigs { get; set; } = [];

        /// <summary>
        /// 加载报警信息机参数配置
        /// </summary>
        private void LoadRoleManagementSettings()
        {
            var configs = _operationUtil.LoadRoleManagementConfigWithDefault();

            RunOnUIThread(() =>
            {
                RoleManagementConfigs.Clear();
                RoleManagementConfigs.AddRange(configs);
            });
        }
        #endregion
    }
}
