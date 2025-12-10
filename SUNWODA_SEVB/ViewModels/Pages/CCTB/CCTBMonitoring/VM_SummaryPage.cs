using Mapster.Models;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.ViewModels.Pages.HawkEye;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBMonitoring
{
    [Module("VM_SummaryPage", "系统设置", Order = 0, Category = "VM_CCTBMonitoringPage/VM_SummaryPage")]
    class VM_SummaryPage : ViewModelBase
    {
        private readonly ILoggerService<VM_SummaryPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_SummaryPage(
            ILoggerService<VM_SummaryPage> logger,
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

        public override void OnInitialize()
        {
            InitializeLineInfos();

            base.OnInitialize();
        }

        public ObservableCollection<(string, string)>? LineInfos { get; set; }

        private void InitializeLineInfos()
        {
            LineInfos = new ObservableCollection<(string, string)>();
            var workship = _globalSettingRepository.GetByName("workship")?.Value;
            var electrode = _projectSettingRepository.GetByNameAndVM("electrode","VM_CCTBMainWindow")?.Value;
            if (workship != null)
            {
                LineInfos.Add(("基地", workship.Split(':')[0] + "基地"));
                //LineInfos.Add(("电极",))
            }
        }
    }
}
