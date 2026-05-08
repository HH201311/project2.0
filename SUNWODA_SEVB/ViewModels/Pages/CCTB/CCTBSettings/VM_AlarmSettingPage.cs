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
        private readonly OperationUtil _operationUtil;

        /// <summary>
        /// 保存按钮
        /// </summary>
        public ICommand? ParamSaveCommand { get; set; }

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

            _operationUtil = new OperationUtil(projectSettingRepository);
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public override void OnInitialize()
        {
            ParamSaveCommand = new RelayCommand(OnParamSave);
            LoadAlarmSettings();

            base.OnInitialize();
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
        private void OnParamSave()
        {
            var re = _operationUtil.SaveAlarmConfigsProjectParamrers(AlarmConfigs.ToList());
            if (!re)
            {
                MessageBox.Show("保存失败");
                return;
            }
            re = _operationUtil.SaveAlarmConfigParamProjectParamrers(AlarmConfigParam);
            if (re) MessageBox.Show("保存成功");
            else MessageBox.Show("保存失败");
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
            var alarmConfigs = _operationUtil.LoadAlarmConfigsProjectParamrers();
            foreach (var config in AlarmDefaultConfig.Configs)
            {
                if (alarmConfigs.Exists(o => o.Code == config.Key)) continue;
                var newConfig = config.Value.CopyByJson();
                if (newConfig == null) continue;
                alarmConfigs.Add(newConfig);
            }

            RunOnUIThread(() =>
            {
                AlarmConfigs.Clear();
                AlarmConfigs.AddRange(alarmConfigs);
            });

            var alarmConfigParam = _operationUtil.LoadAlarmConfigParamProjectParamrers();
            if (alarmConfigParam == null) return;
            RunOnUIThread(() =>
            {
                AlarmConfigParam = alarmConfigParam;
            });
        }
        #endregion
    }
}
