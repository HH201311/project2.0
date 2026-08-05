using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Mapster.Models;
using OpenCvSharp;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AssistanceModels;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using SUNWODA_SEVB.ViewModels.Pages.HawkEye;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBMonitoring
{
    [Module("VM_TBDataPage", "系统设置", Order = 1, Category = "VM_CCTBMonitoringPage/VM_TBDataPage")]
    class VM_TBDataPage : ViewModelBase
    {
        private readonly ILoggerService<VM_TBDataPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_TBDataPage(
            ILoggerService<VM_TBDataPage> logger,
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

        protected override async Task OnInitializeAsync()
        {
            // 初始化面密度实时数据控件
            await InializeParamOptionsCalculateAsync();

            CCTBCore.CoatingMessageNotificationEvent -= OnCoatingMessageNotificationAsync;
            CCTBCore.CoatingMessageNotificationEvent += OnCoatingMessageNotificationAsync;

            CCTBCore.TBlockMessageNotificationEvent -= OnTBlockMessageNotificationAsync;
            CCTBCore.TBlockMessageNotificationEvent += OnTBlockMessageNotificationAsync;

            await base.OnInitializeAsync();
        }

        /// <summary>
        /// 涂布数据更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnCoatingMessageNotificationAsync(object? sender, MessageEventArgs<CoatingMessage> e)
        {
            if (e.Message == null) return;
            if (e.Message.Datas == null || e.Message.Datas.Count == 0) return;

            // 更新面密度数据柱状图
            await RefreshTBDatasAsync(e.Message.SideType, e.Message.Datas);
            // 更新面密度参数显示
            await RefreshParamOptionCalculateAsync(e.Message.SideType);
            // 更新面密度数据表格
            await RefreshTBDataHistoryAsync(e.Message.SideType, e.Message.Datas);
        }

        /// <summary>
        /// T块数据更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnTBlockMessageNotificationAsync(object? sender, MessageEventArgs<TBlockMessage> e)
        {
            if (e.Message == null) return;
            if (e.Message.Datas == null || e.Message.Datas.Count == 0) return;

            // 更新T块数据
            await RefreshTBlockDatasAsync(e.Message.SideType, e.Message.Datas);
        }

        #region 面密度实时数据
        private List<EParamOption> tbParamOptionsACache = [], tbParamOptionsBCache = [];

        private ObservableCollection<ParameterModel<string>> paramOptionsA = [], paramOptionsB = [];
        public ObservableCollection<ParameterModel<string>> ParamOptionsA { get => paramOptionsA; set => SetProperty(ref paramOptionsA, value); }
        public ObservableCollection<ParameterModel<string>> ParamOptionsB { get => paramOptionsB; set => SetProperty(ref paramOptionsB, value); }

        private double tbDataAMin = 370, tbDataAMax = 382, tbDataBMin = 746, tbDataBMax = 758;
        public double TBDataAMin { get => tbDataAMin; set => SetProperty(ref tbDataAMin, value); }
        public double TBDataAMax { get => tbDataAMax; set => SetProperty(ref tbDataAMax, value); }
        public double TBDataBMin { get => tbDataBMin; set => SetProperty(ref tbDataBMin, value); }
        public double TBDataBMax { get => tbDataBMax; set => SetProperty(ref tbDataBMax, value); }

        private DataLimitParamModel tbDataLimitParamA = new(372, 374, 376, 378, 380), tbDataLimitParamB = new(748, 750, 752, 754, 756);
        public DataLimitParamModel TBDataLimitParamA { get => tbDataLimitParamA; set => SetProperty(ref tbDataLimitParamA, value); }
        public DataLimitParamModel TBDataLimitParamB { get => tbDataLimitParamB; set => SetProperty(ref tbDataLimitParamB, value); }

        private ObservableCollection<double> tbDatasA = [], tbDatasB = [];
        public ObservableCollection<double> TBDatasA { get => tbDatasA; set => SetProperty(ref tbDatasA, value); }
        public ObservableCollection<double> TBDatasB { get => tbDatasB; set => SetProperty(ref tbDatasB, value); }

        public ICommand ParamOptionSelectedCommand => new RelayCommand(OnParamOptionSelected);

        /// <summary>
        /// 面密度参数修改时触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnParamOptionSelected(object? arg)
        {
            if (!(arg is ESideType sideType)) return;

            // 加载配置
            var paramOptions = _projectSettingRepository.LoadParamOptionsProjectParameters(sideType);
            var result = await Dialog
                .Show(new VM_TBDataConfigDialog(paramOptions).UIElement)
                .GetResultAsync<bool>();

            if (!result) return;

            _projectSettingRepository.SaveParamOptionsProjectParameters(sideType, paramOptions);
            if (sideType == ESideType.A)
            {
                tbParamOptionsACache.Clear();
                tbParamOptionsACache.AddRange(paramOptions);
            }
            if (sideType == ESideType.B)
            {
                tbParamOptionsBCache.Clear();
                tbParamOptionsBCache.AddRange(paramOptions);
            }
            await RefreshParamOptionCalculateAsync(sideType);
        }

        /// <summary>
        /// 更新面密度参数显示
        /// </summary>
        /// <param name="sideType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task RefreshParamOptionCalculateAsync(ESideType sideType)
        {
            var paramPairs = sideType switch
            {
                ESideType.A => ParamOptionsA,
                ESideType.B => ParamOptionsB,
                _ => throw new Exception($"未实现[{sideType}]!")
            };
            var datas = sideType switch
            {
                ESideType.A => TBDatasA,
                ESideType.B => TBDatasB,
                _ => throw new Exception($"未实现[{sideType}]!")
            };
            var paramOptionsCache = sideType switch
            {
                ESideType.A => tbParamOptionsACache,
                ESideType.B => tbParamOptionsBCache,
                _ => throw new Exception($"未实现[{sideType}]!")
            };
            await RunOnUIThreadAsync(() => { paramPairs.Clear(); });

            // 数据清洗：不为0
            var cleanedDatas = datas == null ? new List<double>(0) : datas.Where(o => o != 0).ToList();
            if (paramOptionsCache == null || paramOptionsCache.Count == 0) return;

            foreach (var paramOption in paramOptionsCache)
            {
                var paramPair = new ParameterModel<string>();
                paramPair.PName = paramOption.ToString();
                var pValue = paramOption switch
                {
                    EParamOption.Cov => $"{(cleanedDatas.Count > 0 ? cleanedDatas.COV() * 100 : 0):F2}%",
                    EParamOption.Mean => $"{(cleanedDatas.Count > 0 ? cleanedDatas.Average() : 0):F2}",
                    EParamOption.Sigma => $"{(cleanedDatas.Count > 0 ? cleanedDatas.Sigma() : 0):F2}",
                    EParamOption.Min => $"{(cleanedDatas.Count > 0 ? cleanedDatas.Min() : 0):F2}",
                    EParamOption.Max => $"{(cleanedDatas.Count > 0 ? cleanedDatas.Max() : 0):F2}",
                    EParamOption.Range => $"{(cleanedDatas.Count > 0 ? (cleanedDatas.Max() - cleanedDatas.Min()) : 0):F2}",
                    _ => "--",
                };
                paramPair.PValue = pValue;
                await RunOnUIThreadAsync(() => { paramPairs.Add(paramPair); });
            }
        }

        /// <summary>
        /// 更新面密度参数
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        private async Task RefreshTBDatasAsync(ESideType sideType, List<double> datas)
        {
            if (datas == null || datas.Count == 0) return;
            var tbDatas = sideType switch
            {
                ESideType.A => TBDatasA,
                ESideType.B => TBDatasB,
                _ => throw new Exception($"未实现[{sideType}]!")
            };

            if (tbDatas == null) return;
            await RunOnUIThreadAsync(() =>
            {
                tbDatas.Clear();
                datas.ForEach(o => tbDatas.Add(o));
            });
        }

        /// <summary>
        /// 初始化面密度参数显示
        /// </summary>
        /// <returns></returns>
        private async Task InializeParamOptionsCalculateAsync()
        {
            var sideTypes = Enum.GetValues(typeof(ESideType));
            foreach (ESideType sideType in sideTypes)
            {
                var paramOptions = _projectSettingRepository.LoadParamOptionsProjectParameters(sideType);
                if (paramOptions == null) continue;

                if (sideType == ESideType.A) tbParamOptionsACache = paramOptions;
                if (sideType == ESideType.B) tbParamOptionsBCache = paramOptions;
                await RefreshParamOptionCalculateAsync(sideType);
            }
        }
        #endregion

        #region 面密度实时数据列表

        private DataLimitParamModel tbDataLimitParamA1 = new(372, 374, 376, 378, 380), tbDataLimitParamB1 = new(748, 750, 752, 754, 756);
        public DataLimitParamModel TBDataLimitParamA1 { get => tbDataLimitParamA1; set => SetProperty(ref tbDataLimitParamA1, value); }
        public DataLimitParamModel TBDataLimitParamB1 { get => tbDataLimitParamB1; set => SetProperty(ref tbDataLimitParamB1, value); }

        private ObservableCollection<double[]> tbDataHistoryA = new(), tbDataHistoryB = new();
        public ObservableCollection<double[]> TBDataHistoryA { get => tbDataHistoryA; set => SetProperty(ref tbDataHistoryA, value); }
        public ObservableCollection<double[]> TBDataHistoryB { get => tbDataHistoryB; set => SetProperty(ref tbDataHistoryB, value); }

        /// <summary>
        /// 更新面密度历史数据表格
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        private async Task RefreshTBDataHistoryAsync(ESideType sideType, List<double> datas)
        {
            var tbDataHistory = sideType switch
            {
                ESideType.A => TBDataHistoryA,
                ESideType.B => TBDataHistoryB,
                _ => throw new Exception($"未实现[{sideType}]!")
            };

            if (tbDataHistory == null) return;
            await RunOnUIThreadAsync(() => tbDataHistory.Add(datas.ToArray()));

            // 删除一行
            if (tbDataHistory.Count <= 20) return;
            await RunOnUIThreadAsync(() => tbDataHistory.RemoveAt(0));
        }
        #endregion

        #region T块实时位置        
        private double tBlockAMin = -300, tBlockAMax = 1100, tBlockBMin = -300, tBlockBMax = 1100;
        public double TBlockAMin { get => tBlockAMin; set => SetProperty(ref tBlockAMin, value); }
        public double TBlockAMax { get => tBlockAMax; set => SetProperty(ref tBlockAMax, value); }
        public double TBlockBMin { get => tBlockBMin; set => SetProperty(ref tBlockBMin, value); }
        public double TBlockBMax { get => tBlockBMax; set => SetProperty(ref tBlockBMax, value); }
        private ObservableCollection<TBlockModel> tBlockDatasA = [], tBlockDatasB = [];
        public ObservableCollection<TBlockModel> TBlockDatasA { get => tBlockDatasA; set => SetProperty(ref tBlockDatasA, value); }
        public ObservableCollection<TBlockModel> TBlockDatasB { get => tBlockDatasB; set => SetProperty(ref tBlockDatasB, value); }

        /// <summary>
        /// 更新T块数据
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task RefreshTBlockDatasAsync(ESideType sideType, List<TBlockModel> datas)
        {
            if (datas == null || datas.Count == 0) return;
            var tbDatas = sideType switch
            {
                ESideType.A => TBlockDatasA,
                ESideType.B => TBlockDatasB,
                _ => throw new Exception($"未实现[{sideType}]!")
            };

            if (tbDatas == null) return;
            await RunOnUIThreadAsync(() =>
            {
                tbDatas.Clear();
                datas.ForEach(o => tbDatas.Add(o));
            });
        }
        #endregion
    }
}
