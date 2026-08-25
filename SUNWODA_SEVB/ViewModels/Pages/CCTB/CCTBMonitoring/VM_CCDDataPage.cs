using Dm;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.CCDSocketServer;
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
    [Module("VM_CCDDataPage", "尺寸", Order = 1, Category = "VM_CCTBMonitoringPage/VM_CCDDataPage")]
    class VM_CCDDataPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CCDDataPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        private bool _isDisplaying = false;

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

        /// <summary>
        /// 初始化
        /// </summary>
        public override void OnInitialize()
        {
            CCTBCore.CCDMessageNotificationEvent -= OnCCDMessageNotificationAsync;
            CCTBCore.CCDMessageNotificationEvent += OnCCDMessageNotificationAsync;

            base.OnInitialize();
        }

        /// <summary>
        /// 当离开界面时触发
        /// </summary>
        /// <returns></returns>
        protected override Task OnNavigatedFromAsync()
        {
            _isDisplaying = false;
            return base.OnNavigatedFromAsync();
        }

        /// <summary>
        /// 当进入界面时触发
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        protected override Task OnNavigatedToAsync(object? parameter)
        {
            _isDisplaying = true;

            return base.OnNavigatedToAsync(parameter);
        }

        /// <summary>
        /// T块数据更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnCCDMessageNotificationAsync(object? sender, MessageEventArgs<Tuple<string, WeightMessageModel>> e)
        {
            if (!(sender is IBusinessService businessService)) return;
            if (e.Message == null || e.Message.Item2 == null) return;

            var data = new CCDMessageModel(DateTime.Now, e.Message.Item1, e.Message.Item2);

            if (_isDisplaying)
            {
                lock (_pendingCCDMessage)
                {
                    _pendingCCDMessage.Add(data);
                    if (_pendingCCDMessage.Count > 20) _pendingCCDMessage.RemoveAt(0);
                }
            }
            else
            {
                if (_pendingCCDMessage.Count > 0)
                {
                    var datas = new List<CCDMessageModel>();
                    lock (_pendingCCDMessage)
                    {
                        datas.AddRange(_pendingCCDMessage);
                        _pendingCCDMessage.Clear();
                    }
                    RefreshCCDMessageData(datas.ToArray());
                }
                RefreshCCDMessageData(data);
            }

        }

        #region 双缓存机制：界面刷新应用
        /// <summary>
        /// 双缓存机制缓存变量
        /// </summary>
        private List<CCDMessageModel> _pendingCCDMessage = new();

        /// <summary>
        /// 刷新最新数据
        /// </summary>
        /// <param name="data"></param>
        private void RefreshCCDMessageData(params CCDMessageModel[] datas)
        {
            if (datas == null || datas.Count() == 0) return;

            var lastedData = datas.LastOrDefault();
            if (lastedData == null) return;

            // 更新CCD尺寸数据示意图
            RefreshFilmChartAsync(lastedData);
            // 更新CCD尺寸数据基材值
            RefreshFilmBaseMaterialAsync(lastedData.SideType, new Random().NextDouble() * 100);

            foreach (var data in datas)
            {
                // 更新CCD尺寸数据表格
                RefreshFilmDatasAsync(data);
                // 更新按钮使能
                RefreshFilmEnableAsync(data.SideType, data.IsDryFilmData);
            }
        }
        #endregion

        #region A/B面尺寸数据示意图
        private double baseMaterialValueA, baseMaterialValueB;
        public double BaseMaterialValueA { get => baseMaterialValueA; set => SetProperty(ref baseMaterialValueA, value); }
        public double BaseMaterialValueB { get => baseMaterialValueB; set => SetProperty(ref baseMaterialValueB, value); }

        private CCDMessageModel? dryFilmDataA, wetFilmDataA, dryFilmDataB, wetFilmDataB;
        public CCDMessageModel? DryFilmDataA { get => dryFilmDataA; set => SetProperty(ref dryFilmDataA, value); }
        public CCDMessageModel? WetFilmDataA { get => wetFilmDataA; set => SetProperty(ref wetFilmDataA, value); }
        public CCDMessageModel? DryFilmDataB { get => dryFilmDataB; set => SetProperty(ref dryFilmDataB, value); }
        public CCDMessageModel? WetFilmDataB { get => wetFilmDataB; set => SetProperty(ref wetFilmDataB, value); }

        /// <summary>
        /// 更新CCD尺寸数据基材值
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="baseMaterialValue"></param>
        /// <returns></returns>
        private async void RefreshFilmBaseMaterialAsync(ESideType sideType, double baseMaterialValue)
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
        public async void RefreshFilmChartAsync(CCDMessageModel? data)
        {
            if (data == null) return;

            if (data.SideType == ESideType.A && data.IsDryFilmData)
            {
                await RunOnUIThreadAsync(() => DryFilmDataA = data);
                return;
            }
            if (data.SideType == ESideType.A && !data.IsDryFilmData)
            {
                await RunOnUIThreadAsync(() => WetFilmDataA = data);
                return;
            }
            if (data.SideType == ESideType.B && data.IsDryFilmData)
            {
                await RunOnUIThreadAsync(() => DryFilmDataB = data);
                return;
            }
            if (data.SideType == ESideType.B && !data.IsDryFilmData)
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

        public ObservableCollection<CCDMessageModel> DryFilmDatasA { get; set; } = [];
        public ObservableCollection<CCDMessageModel> WetFilmDatasA { get; set; } = [];
        public ObservableCollection<CCDMessageModel> DryFilmDatasB { get; set; } = [];
        public ObservableCollection<CCDMessageModel> WetFilmDatasB { get; set; } = [];

        public ICommand? DryFilmChangedACommand => new RelayCommand((obj) => OnFilmDataChanged(obj, ESideType.A, true));
        public ICommand? WetFilmChangedACommand => new RelayCommand((obj) => OnFilmDataChanged(obj, ESideType.A, false));
        public ICommand? DryFilmChangedBCommand => new RelayCommand((obj) => OnFilmDataChanged(obj, ESideType.B, true));
        public ICommand? WetFilmChangedBCommand => new RelayCommand((obj) => OnFilmDataChanged(obj, ESideType.B, false));

        /// <summary>
        /// 更新按钮开关：有数据则使能
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="isDry"></param>
        /// <returns></returns>
        private async void RefreshFilmEnableAsync(ESideType sideType, bool isDry)
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
        private async void RefreshFilmDatasAsync(CCDMessageModel data)
        {
            var filmDatas = (data.SideType, data.IsDryFilmData) switch
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
