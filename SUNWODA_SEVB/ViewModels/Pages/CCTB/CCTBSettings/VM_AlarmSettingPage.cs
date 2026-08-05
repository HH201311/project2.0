using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Wordprocessing;
using HandyControl.Controls;
using Masuit.Tools;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Component.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels;
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

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBSettings
{
    [Module("VM_AlarmSettingPage", "报警设置", Order = 2, Category = "VM_CCTBSettingsPage/VM_AlarmSettingPage")]
    class VM_AlarmSettingPage : ViewModelBase
    {
        private readonly ILoggerService<VM_AlarmSettingPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        /// <summary>
        /// 保存按钮
        /// </summary>
        public ICommand? ParamSaveCommand => new RelayCommand(OnParamSaveAsync);

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_AlarmSettingPage(
            ILoggerService<VM_AlarmSettingPage> logger,
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
            LoadAlarmSettings();
            base.OnNavigatedTo(parameter);
        }

        /// <summary>
        /// 当参数保存时触发
        /// </summary>
        private async void OnParamSaveAsync()
        {
            var re = _projectSettingRepository.SaveAlarmConfigProjectParameters(AlarmConfigs.ToList());
            if (!re)
            {
                await VM_CCTBMessageBoxDialog.ErrorAsync("保存失败！", "保存提示");
                return;
            }
            re = _projectSettingRepository.SaveAlarmConfigParamProjectParameters(AlarmConfigParam);
            if (re) await VM_CCTBMessageBoxDialog.InfoAsync("保存成功！", "保存提示");
            else await VM_CCTBMessageBoxDialog.ErrorAsync("保存失败！", "保存提示");
        }

        #region 报警配置表格
        /// <summary>
        /// 报警等级
        /// </summary>
        public ObservableCollection<ComboboxItemModel<EAlarmLevel>> AlarmLevelSource { get; } =
                 new ObservableCollection<ComboboxItemModel<EAlarmLevel>>(
                 Enum.GetValues(typeof(EAlarmLevel)).Cast<EAlarmLevel>()
                 .Select(v => new ComboboxItemModel<EAlarmLevel>() { ItemValue = v, DisplayName = v.ToString() }));


        private AlarmConfigParamModel alarmConfigParam = new();

        /// <summary>
        /// 报警信息
        /// </summary>
        public ObservableCollection<AlarmConfigModel> AlarmConfigs { get; set; } = [];

        /// <summary>
        /// 报警参数
        /// </summary>
        public AlarmConfigParamModel AlarmConfigParam { get => alarmConfigParam; set => SetProperty(ref alarmConfigParam, value); }

        /// <summary>
        /// 加载报警信息机参数配置
        /// </summary>
        private void LoadAlarmSettings()
        {
            var configs = _projectSettingRepository.LoadAlarmConfigWithDefault();

            RunOnUIThread(() =>
            {
                AlarmConfigs.Clear();
                AlarmConfigs.AddRange(configs);
            });

            var alarmConfigParam = _projectSettingRepository.LoadAlarmConfigParamProjectParameters();
            if (alarmConfigParam == null) return;
            RunOnUIThread(() =>
            {
                AlarmConfigParam = alarmConfigParam;
            });
        }
        #endregion
    }
}
