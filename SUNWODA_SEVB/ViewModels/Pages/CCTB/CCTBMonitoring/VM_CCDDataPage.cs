using Dm;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Services;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.ViewModels.Pages.HawkEye;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBMonitoring
{
    [Module("VM_CCDDataPage", "系统设置", Order = 0, Category = "VM_CCTBMonitoringPage/VM_CCDDataPage")]
    class VM_CCDDataPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CCDDataPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_CCDDataPage(
            ILoggerService<VM_CCDDataPage> logger,
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
            DryFilmChangedACommand = new RelayCommand((obj) => OnFilmDataChanged(obj, ESideType.A, true));
            WetFilmChangedACommand = new RelayCommand((obj) => OnFilmDataChanged(obj, ESideType.A, false));
            DryFilmChangedBCommand = new RelayCommand((obj) => OnFilmDataChanged(obj, ESideType.B, true));
            WetFilmChangedBCommand = new RelayCommand((obj) => OnFilmDataChanged(obj, ESideType.B, false));

            base.OnInitialize();
        }

        protected override async Task OnInitializeAsync()
        {
            CCTBCore.CCDMessageNotificationEvent -= OnCCDMessageNotificationAsync;
            CCTBCore.CCDMessageNotificationEvent += OnCCDMessageNotificationAsync;

            await base.OnInitializeAsync();
        }

        /// <summary>
        /// T块数据更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnCCDMessageNotificationAsync(object? sender, MessageEventArgs<CCDMessage> e)
        {
            if (e.Message == null) return;
            if (e.Message.Data == null) return;

            // 更新CCD尺寸数据基材值
            await RefreshFilmBaseMaterialAsync(e.Message.SideType, new Random().NextDouble() * 100);
            // 更新CCD尺寸数据示意图
            await RefreshFilmChartAsync(e.Message.SideType, e.Message.IsDryFilmData, e.Message.Data.CopyByJson());
            // 更新CCD尺寸数据表格
            await RefreshFilmDatasAsync(e.Message.SideType, e.Message.IsDryFilmData, e.Message.Data.CopyByJson());
            // 更新按钮使能
            await RefreshFilmEnableAsync(e.Message.SideType, e.Message.IsDryFilmData);
        }

        #region A/B面尺寸数据示意图
        private double baseMaterialValueA, baseMaterialValueB;
        public double BaseMaterialValueA { get => baseMaterialValueA; set => SetProperty(ref baseMaterialValueA, value); }
        public double BaseMaterialValueB { get => baseMaterialValueB; set => SetProperty(ref baseMaterialValueB, value); }

        private CCDModel dryFilmDataA = new(), wetFilmDataA = new(), dryFilmDataB = new(), wetFilmDataB = new();
        public CCDModel DryFilmDataA { get => dryFilmDataA; set => SetProperty(ref dryFilmDataA, value); }
        public CCDModel WetFilmDataA { get => wetFilmDataA; set => SetProperty(ref wetFilmDataA, value); }
        public CCDModel DryFilmDataB { get => dryFilmDataB; set => SetProperty(ref dryFilmDataB, value); }
        public CCDModel WetFilmDataB { get => wetFilmDataB; set => SetProperty(ref wetFilmDataB, value); }

        /// <summary>
        /// 更新CCD尺寸数据基材值
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="baseMaterialValue"></param>
        /// <returns></returns>
        private async Task RefreshFilmBaseMaterialAsync(ESideType sideType, double baseMaterialValue)
        {
            if (sideType == ESideType.A)
            {
                await RunOnUIThreadAsync(() => BaseMaterialValueA = baseMaterialValue);
                return;
            }
            if (sideType == ESideType.B)
            {
                await RunOnUIThreadAsync(() => BaseMaterialValueB = baseMaterialValue);
                return;
            }
        }

        /// <summary>
        /// 更新CCD尺寸数据示意图
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="isDry"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task RefreshFilmChartAsync(ESideType sideType, bool isDry, CCDModel? data)
        {
            if (data == null) return;

            if (sideType == ESideType.A && isDry)
            {
                await RunOnUIThreadAsync(() => DryFilmDataA = data);
                return;
            }
            if (sideType == ESideType.A && !isDry)
            {
                await RunOnUIThreadAsync(() => WetFilmDataA = data);
                return;
            }
            if (sideType == ESideType.B && isDry)
            {
                await RunOnUIThreadAsync(() => DryFilmDataB = data);
                return;
            }
            if (sideType == ESideType.B && !isDry)
            {
                await RunOnUIThreadAsync(() => WetFilmDataB = data);
                return;
            }
        }
        #endregion

        #region A/B面尺寸数据表格：干膜数据/湿膜数据
        private bool dryFilmEnableA = false, wetFilmEnableA = false, dryFilmEnableB = false, wetFilmEnableB = false;
        public bool DryFilmEnableA { get => dryFilmEnableA; set => SetProperty(ref dryFilmEnableA, value); }
        public bool WetFilmEnableA { get => wetFilmEnableA; set => SetProperty(ref wetFilmEnableA, value); }
        public bool DryFilmEnableB { get => dryFilmEnableB; set => SetProperty(ref dryFilmEnableB, value); }
        public bool WetFilmEnableB { get => wetFilmEnableB; set => SetProperty(ref wetFilmEnableB, value); }

        public ObservableCollection<CCDModel> DryFilmDatasA { get; set; } = [];
        public ObservableCollection<CCDModel> WetFilmDatasA { get; set; } = [];
        public ObservableCollection<CCDModel> DryFilmDatasB { get; set; } = [];
        public ObservableCollection<CCDModel> WetFilmDatasB { get; set; } = [];

        public ICommand? DryFilmChangedACommand { get; set; }
        public ICommand? WetFilmChangedACommand { get; set; }
        public ICommand? DryFilmChangedBCommand { get; set; }
        public ICommand? WetFilmChangedBCommand { get; set; }

        /// <summary>
        /// 更新按钮开关：有数据则使能
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="isDry"></param>
        /// <returns></returns>
        private async Task RefreshFilmEnableAsync(ESideType sideType, bool isDry)
        {
            if (sideType == ESideType.A && isDry && !DryFilmEnableA)
            {
                await RunOnUIThreadAsync(() => DryFilmEnableA = true);
                return;
            }
            if (sideType == ESideType.A && !isDry && !WetFilmEnableA)
            {
                await RunOnUIThreadAsync(() => WetFilmEnableA = true);
                return;
            }
            if (sideType == ESideType.B && isDry && !DryFilmEnableB)
            {
                await RunOnUIThreadAsync(() => DryFilmEnableB = true);
                return;
            }
            if (sideType == ESideType.B && !isDry && !WetFilmEnableB)
            {
                await RunOnUIThreadAsync(() => WetFilmEnableB = true);
                return;
            }
        }

        /// <summary>
        /// 更新尺寸数据表格
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="isDry"></param>
        /// <returns></returns>
        private async Task RefreshFilmDatasAsync(ESideType sideType, bool isDry, CCDModel? data)
        {
            if (data == null) return;

            var filmDatas = (sideType, isDry) switch
            {
                (ESideType.A, true) => DryFilmDatasA,
                (ESideType.A, false) => WetFilmDatasA,
                (ESideType.B, true) => DryFilmDatasB,
                (ESideType.B, false) => WetFilmDatasB,
                _ => null,
            };
            if (filmDatas == null) return;

            await RunOnUIThreadAsync(() =>
            {
                filmDatas.Add(data); 
                while (filmDatas.Count > 20) filmDatas.RemoveAt(0);
            });
        }

        /// <summary>
        /// 切换数据
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        private async void OnFilmDataChanged(object? obj, ESideType sideType, bool isDry)
        {
            if (obj == null || !(obj is bool isChecked) || !isChecked) return;

            var module = _moduleManager.GetModule("VM_CCDDataPage");
            var view = _moduleManager.GetView(module);
            if (!(view is Views.Pages.CCTB.CCTBMonitoring.CCDDataPage page)) return;

            if (sideType == ESideType.A && isDry)
            {
                await RunOnUIThreadAsync(() => { page.DGA.ItemsSource = DryFilmDatasA; });
                return;
            }

            if (sideType == ESideType.A && !isDry)
            {
                await RunOnUIThreadAsync(() => { page.DGA.ItemsSource = WetFilmDatasA; });
                return;
            }

            if (sideType == ESideType.B && isDry)
            {
                await RunOnUIThreadAsync(() => { page.DGB.ItemsSource = DryFilmDatasB; });
                return;
            }

            if (sideType == ESideType.B && !isDry)
            {
                await RunOnUIThreadAsync(() => { page.DGB.ItemsSource = WetFilmDatasB; });
                return;
            }
        }
        #endregion
    }
}
