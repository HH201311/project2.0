using Dm.util;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Mapster.Models;
using OpenCvSharp;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AssistanceModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.BusinessModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
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

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBMonitoring
{
    [Module("VM_TBDataPage", "面密度", Order = 1, Category = "VM_CCTBMonitoringPage/VM_TBDataPage")]
    class VM_TBDataPage : ViewModelBase
    {
        private readonly ILoggerService<VM_TBDataPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        private bool _isDisplaying = false;

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

        /// <summary>
        /// 初始化
        /// </summary>
        public override void OnInitialize()
        {
            IniatailizeParameters();

            CCTBCore.CoatingMessageNotificationEvent -= OnCoatingMessageNotificationAsync;
            CCTBCore.CoatingMessageNotificationEvent += OnCoatingMessageNotificationAsync;

            CCTBCore.TBlockMessageNotificationEvent -= OnTBlockMessageNotificationAsync;
            CCTBCore.TBlockMessageNotificationEvent += OnTBlockMessageNotificationAsync;

            // 订阅配置参数变更事件
            CCTBCore.SettingMessageNotificationEvent -= OnSettingMessageChanged;
            CCTBCore.SettingMessageNotificationEvent += OnSettingMessageChanged;

            base.OnInitialize();
        }

        /// <summary>
        /// 异步初始化
        /// </summary>
        /// <returns></returns>
        protected override async Task OnInitializeAsync()
        {
            // 初始化面密度实时数据控件
            await InializeParamOptionsCalculateAsync();

            await base.OnInitializeAsync();
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
        /// 清理
        /// </summary>
        public override void OnCleanup()
        {
            CCTBCore.CoatingMessageNotificationEvent -= OnCoatingMessageNotificationAsync;
            CCTBCore.TBlockMessageNotificationEvent -= OnTBlockMessageNotificationAsync;
            CCTBCore.SettingMessageNotificationEvent -= OnSettingMessageChanged;

            base.OnCleanup();
        }

        #region 双缓存机制：界面刷新应用
        /// <summary>
        /// 双缓存机制缓存变量
        /// </summary>
        private List<CoatingDataModel> _pendingCoatingData = new();

        /// <summary>
        /// 双缓存机制缓存变量
        /// </summary>
        private List<TBlockDataModel> _pendingTBlockData = new();

        /// <summary>
        /// 刷新涂布数据界面
        /// </summary>
        private async Task RefreshPendingCoatingDatAsync()
        {
            if (_pendingCoatingData.Count() == 0) return;

            var datas = new List<CoatingDataModel>();
            lock (_pendingCoatingData)
            {
                datas.AddRange(_pendingCoatingData);
                _pendingCoatingData.Clear();
            }

            foreach (var data in datas)
            {
                if (data.Weights == null || data.Weights.Count == 0) return;

                // 更新面密度数据柱状图
                await RefreshCoatingDataAsync(data.SideType, data.Weights);
                // 更新面密度数据表格
                await RefreshCoatingDataHistoryAsync(data.SideType, data.Weights);
            }
        }

        /// <summary>
        /// 刷新T快数据界面
        /// </summary>
        private async Task RefreshPendingTBlockDataAsync()
        {
            if (_pendingTBlockData.Count() == 0) return;

            var datas = new List<TBlockDataModel>();
            lock (_pendingTBlockData)
            {
                datas.AddRange(_pendingTBlockData);
                _pendingTBlockData.Clear();
            }
            var lastedata = datas.LastOrDefault();
            if (lastedata == null) return;

            var tDatas = lastedata.TBlockInfoLis.ConvertAll(o => new TBlockModel() { IsAuto = o.TAdjustMode, TBlockPosition = o.TPosition }).ToList();

            // 更新T块数据
            await RefreshTBlockDatasAsync(lastedata.SideType, tDatas);
        }

        /// <summary>
        /// 涂布数据更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnCoatingMessageNotificationAsync(object? sender, MessageEventArgs<CoatingDataModel> e)
        {
            _ = Task.Run(async () =>
            {
                if (!(sender is IBusinessService businessService)) return;
                // 面密度实时数据源判断
                if (!businessService.IsNeedCoatingData(e.Tag)) return;
                if (e.Message == null) return;
                if (e.Message.Weights == null || e.Message.Weights.Count == 0) return;

                if (!_isDisplaying)
                {
                    lock (_pendingCoatingData)
                    {
                        _pendingCoatingData.Add(e.Message);
                        while (_pendingCoatingData.Count > 20) _pendingCoatingData.RemoveAt(0);
                    }
                }
                else
                {
                    // 更新缓存数据
                    await RefreshPendingCoatingDatAsync();
                    // 更新面密度数据柱状图
                    await RefreshCoatingDataAsync(e.Message.SideType, e.Message.Weights);
                    // 更新面密度数据表格
                    await RefreshCoatingDataHistoryAsync(e.Message.SideType, e.Message.Weights);
                }
            });
        }

        /// <summary>
        /// T块数据更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnTBlockMessageNotificationAsync(object? sender, MessageEventArgs<TBlockDataModel> e)
        {
            _ = Task.Run(async () =>
            {
                if (!(sender is IBusinessService businessService)) return;
                // T块实时数据源判断
                if (!businessService.IsNeedTBLockData(e.Tag)) return;
                if (e.Message == null) return;
                if (e.Message.TBlockInfoLis == null || e.Message.TBlockInfoLis.Count == 0) return;

                if (!_isDisplaying)
                {
                    lock (_pendingTBlockData)
                    {
                        _pendingTBlockData.Add(e.Message);
                        while (_pendingTBlockData.Count > 20) _pendingTBlockData.RemoveAt(0);
                    }
                }
                else
                {
                    await RefreshPendingTBlockDataAsync();

                    var datas = e.Message.TBlockInfoLis.ConvertAll(o => new TBlockModel() { IsAuto = o.TAdjustMode, TBlockPosition = o.TPosition }).ToList();
                    // 更新T块数据
                    await RefreshTBlockDatasAsync(e.Message.SideType, datas);
                }
            });
        }

        /// <summary>
        /// 当参数变更时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSettingMessageChanged(object? sender, MessageEventArgs<object> e)
        {
            if (e.Message == null || e.Message is not SpecialFunctionSettingModel sf) return;

            RefreshTBlockParameters(sf);
            RefreshCoatingLimitParameters(sf);
        }
        #endregion

        /// <summary>
        /// 初始化T块图表参数
        /// </summary>
        private void IniatailizeParameters()
        {
            var specialFunction = _projectSettingRepository.LoadSpecialFunctionSettingProjectParameters();
            if (specialFunction == null) return;
            RefreshCoatingLimitParameters(specialFunction);
            RefreshTBlockParameters(specialFunction);
        }

        #region 面密度相关
        private DataLimitParamModel tbDataLimitParamA = new(372, 374, 376, 378, 380), tbDataLimitParamB = new(748, 750, 752, 754, 756);
        public DataLimitParamModel TBDataLimitParamA { get => tbDataLimitParamA; set => SetProperty(ref tbDataLimitParamA, value); }
        public DataLimitParamModel TBDataLimitParamB { get => tbDataLimitParamB; set => SetProperty(ref tbDataLimitParamB, value); }

        /// <summary>
        /// 更新面密度表格参数
        /// </summary>
        /// <param name="sf"></param>
        private void RefreshCoatingLimitParameters(SpecialFunctionSettingModel sf)
        {
            if (sf.UpperLimitOfCoatingA == sf.LowerLimitOfCoatingA)
            {
                MinOfCoatingA = sf.UpperLimitOfCoatingA - 10;
                MaxOfCoatingA = sf.LowerLimitOfCoatingA + 10;
            }
            else
            {
                MinOfCoatingA = Math.Min(sf.LowerLimitOfCoatingA, sf.UpperLimitOfCoatingA);
                MaxOfCoatingA = Math.Max(sf.LowerLimitOfCoatingA, sf.UpperLimitOfCoatingA);
            }

            if (sf.UpperLimitOfCoatingB == sf.LowerLimitOfCoatingB)
            {
                MinOfCoatingB = sf.UpperLimitOfCoatingB - 10;
                MaxOfCoatingB = sf.LowerLimitOfCoatingB + 10;
            }
            else
            {
                MinOfCoatingB = Math.Min(sf.LowerLimitOfCoatingB, sf.UpperLimitOfCoatingB);
                MaxOfCoatingB = Math.Max(sf.LowerLimitOfCoatingB, sf.UpperLimitOfCoatingB);
            }

            TBDataLimitParamA.CenterLimit = sf.TargetValueOfCoatingA;
            TBDataLimitParamA.UpWarningLimit = sf.TargetValueOfCoatingA + Math.Abs(sf.WarningOffsetOfCoatingA);
            TBDataLimitParamA.DownWarningLimit = sf.TargetValueOfCoatingA - Math.Abs(sf.WarningOffsetOfCoatingA);
            TBDataLimitParamA.UpDangerLimit = sf.TargetValueOfCoatingA + Math.Abs(sf.DangerOffsetOfCoatingA);
            TBDataLimitParamA.DownDangerLimit = sf.TargetValueOfCoatingA - Math.Abs(sf.DangerOffsetOfCoatingA);

            TBDataLimitParamB.CenterLimit = sf.TargetValueOfCoatingB;
            TBDataLimitParamB.UpWarningLimit = sf.TargetValueOfCoatingB + Math.Abs(sf.WarningOffsetOfCoatingB);
            TBDataLimitParamB.DownWarningLimit = sf.TargetValueOfCoatingB - Math.Abs(sf.WarningOffsetOfCoatingB);
            TBDataLimitParamB.UpDangerLimit = sf.TargetValueOfCoatingB + Math.Abs(sf.DangerOffsetOfCoatingB);
            TBDataLimitParamB.DownDangerLimit = sf.TargetValueOfCoatingB - Math.Abs(sf.DangerOffsetOfCoatingB);
        }
        #region 面密度实时数据
        private List<EParamOption> tbParamOptionsACache = [], tbParamOptionsBCache = [];

        private float minOfCoatingA = 370, maxOfCoatingA = 382, minOfCoatingB = 746, maxOfCoatingB = 758;
        private List<float> coatingDatasA = [], coatingDatasB = [];

        public ObservableCollection<ParameterModel<string>> ParamOptionsA { get; set; } = [];
        public ObservableCollection<ParameterModel<string>> ParamOptionsB { get; set; } = [];

        public float MinOfCoatingA { get => minOfCoatingA; set => SetProperty(ref minOfCoatingA, value); }
        public float MaxOfCoatingA { get => maxOfCoatingA; set => SetProperty(ref maxOfCoatingA, value); }
        public float MinOfCoatingB { get => minOfCoatingB; set => SetProperty(ref minOfCoatingB, value); }
        public float MaxOfCoatingB { get => maxOfCoatingB; set => SetProperty(ref maxOfCoatingB, value); }

        public List<float> CoatingDatasA { get => coatingDatasA; set => SetProperty(ref coatingDatasA, value); }
        public List<float> CoatingDatasB { get => coatingDatasB; set => SetProperty(ref coatingDatasB, value); }

        public ICommand ParamOptionSelectedCommand => new RelayCommand(OnParamOptionSelected);

        /// <summary>
        /// 面密度参数修改时触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnParamOptionSelected(object? arg)
        {
            if (!(arg is ESideType sideType)) return;

            // 加载配置
            _logger.UserInfo($"用户打开了{sideType}面 数据展示配置 窗口！");
            var paramOptions = _projectSettingRepository.LoadParamOptionsProjectParameters(sideType);
            var result = await Dialog
                .Show(new VM_TBDataConfigDialog(paramOptions).UIElement)
                .GetResultAsync<bool>();

            if (!result)
            {
                _logger.UserInfo($"用户关闭了{sideType}面 数据展示配置 窗口！");
                return;
            }

            _projectSettingRepository.SaveParamOptionsProjectParameters(sideType, paramOptions);
            _logger.UserInfo($"用户修改了{sideType}面 数据展示配置 参数['{string.Join(',', tbParamOptionsACache)}' => '{string.Join(',', paramOptions)}']");
            if (sideType == ESideType.A)
            {
                tbParamOptionsACache.Clear();
                tbParamOptionsACache.AddRange(paramOptions);
                await RefreshCoatingDataAsync(ESideType.A, CoatingDatasA.ToList());
            }
            if (sideType == ESideType.B)
            {
                tbParamOptionsBCache.Clear();
                tbParamOptionsBCache.AddRange(paramOptions);
                await RefreshCoatingDataAsync(ESideType.B, CoatingDatasB.ToList());
            }
        }

        /// <summary>
        /// 计算面密度显示参数
        /// </summary>
        /// <param name="paramOptions"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private List<ParameterModel<string>> ParamOptionCalculate(List<EParamOption> paramOptions, List<float> datas)
        {
            var result = new List<ParameterModel<string>>();
            if (paramOptions == null || paramOptions.Count == 0) return result;
            // 数据清洗：不为0
            var cleanedDatas = datas.Where(o => o != 0).ToList();
            foreach (var paramOption in paramOptions)
            {
                var paramPair = new ParameterModel<string>();
                paramPair.PName = paramOption.ToString();
                paramPair.PValue = paramOption switch
                {
                    EParamOption.Cov => $"{(cleanedDatas.Count > 0 ? cleanedDatas.COV() * 100 : 0):F2}%",
                    EParamOption.Mean => $"{(cleanedDatas.Count > 0 ? cleanedDatas.Average() : 0):F2}",
                    EParamOption.Sigma => $"{(cleanedDatas.Count > 0 ? cleanedDatas.Sigma() : 0):F2}",
                    EParamOption.Min => $"{(cleanedDatas.Count > 0 ? cleanedDatas.Min() : 0):F2}",
                    EParamOption.Max => $"{(cleanedDatas.Count > 0 ? cleanedDatas.Max() : 0):F2}",
                    EParamOption.Range => $"{(cleanedDatas.Count > 0 ? (cleanedDatas.Max() - cleanedDatas.Min()) : 0):F2}",
                    _ => "--",
                };
                result.Add(paramPair);
            }
            return result;
        }

        /// <summary>
        /// 更新面密度参数
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        private async Task RefreshCoatingDataAsync(ESideType sideType, List<float> datas)
        {
            var paramOptions = sideType switch
            {
                ESideType.A => ParamOptionCalculate(tbParamOptionsACache, datas),
                ESideType.B => ParamOptionCalculate(tbParamOptionsBCache, datas),
                _ => throw new Exception($"未实现[{sideType}]!")
            };

            await RunOnUIThreadAsync(() =>
            {
                if (sideType == ESideType.A)
                {
                    ParamOptionsA.Clear();
                    paramOptions.ForEach(o => ParamOptionsA.Add(o));
                    CoatingDatasA.Clear();
                    CoatingDatasA = new List<float>(datas);
                    return;
                }
                if (sideType == ESideType.B)
                {
                    ParamOptionsB.Clear();
                    paramOptions.ForEach(o => ParamOptionsB.Add(o));
                    CoatingDatasB.Clear();
                    CoatingDatasB = new List<float>(datas);
                    return;
                }
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
                await RefreshCoatingDataAsync(sideType, new List<float>());
            }
        }
        #endregion

        #region 面密度实时数据列表
        public ObservableCollection<float[]> TBDataHistoryA { get; set; } = [];
        public ObservableCollection<float[]> TBDataHistoryB { get; set; } = [];

        /// <summary>
        /// 更新面密度历史数据表格
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        private async Task RefreshCoatingDataHistoryAsync(ESideType sideType, List<float> datas)
        {
            await RunOnUIThreadAsync(() =>
            {
                if (sideType == ESideType.A)
                {
                    TBDataHistoryA.Add(datas.ToArray());
                    while (TBDataHistoryA.Count > 20) TBDataHistoryA.RemoveAt(0);
                    return;
                }
                if (sideType == ESideType.B)
                {
                    TBDataHistoryB.Add(datas.ToArray());
                    while (TBDataHistoryB.Count > 20) TBDataHistoryB.RemoveAt(0);
                    return;
                }
            });
        }
        #endregion
        #endregion

        #region T块实时位置        
        private uint yStep = 200;
        private float minOfTBlock = -300, maxOfTBlock = 1100, centerValue = 0;

        public uint YStep { get => yStep; set => SetProperty(ref yStep, value); }
        public float MinOfTBlock { get => minOfTBlock; set => SetProperty(ref minOfTBlock, value); }
        public float MaxOfTBlock { get => maxOfTBlock; set => SetProperty(ref maxOfTBlock, value); }
        public float CenterValue { get => centerValue; set => SetProperty(ref centerValue, value); }
        public ObservableCollection<TBlockModel> TBlockDatasA { get; set; } = [];
        public ObservableCollection<TBlockModel> TBlockDatasB { get; set; } = [];

        /// <summary>
        /// 更新T块图表参数
        /// </summary>
        /// <param name="sf"></param>
        private void RefreshTBlockParameters(SpecialFunctionSettingModel sf)
        {
            YStep = sf.IntervalOfTBlock == 0 ? 200 : sf.IntervalOfTBlock;
            if (sf.UpperLimitOfTBlock == sf.LowerLimitOfTBlock)
            {
                MinOfTBlock = sf.UpperLimitOfTBlock - YStep;
                MaxOfTBlock = sf.LowerLimitOfTBlock + YStep;
            }
            else
            {
                MinOfTBlock = Math.Min(sf.LowerLimitOfTBlock, sf.UpperLimitOfTBlock);
                MaxOfTBlock = Math.Max(sf.LowerLimitOfTBlock, sf.UpperLimitOfTBlock);
            }

            CenterValue = sf.CenterValueOfTBlock;
        }

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
