using ControlzEx.Standard;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HslCommunication;
using Masuit.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Python.Runtime;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.BusinessModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Services.Project.CCTB.Business
{
    /// <summary>
    /// MD业务逻辑服务
    /// </summary>
    public class MDBusinessService : BusinessServiceBase<MDBusinessService>
    {
        private readonly ILoggerService<MDBusinessService> _logger;
        private readonly IPLCService _plcService;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;
        private readonly ICommunicationService _communicationService;
        private readonly IAlgorithmService _algorithmService;

        /// <summary>
        /// 业务类别
        /// </summary>
        public override EBusinessType BusinessType => EBusinessType.MDBusiness;

        /// <summary>
        /// 闭环类型
        /// </summary>
        public override EClosedLoopType ClosedLoopType => EClosedLoopType.MD;

        /// <summary>
        /// 构造函数
        /// </summary>
        public MDBusinessService(
            ILoggerService<MDBusinessService> logger,
            IPLCService plcService,
            IGlobalSettingRepository globalSettingRepository,
            IUsersRepository usersRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager,
            ICommunicationService communicationService,
            IAlgorithmService algorithmService
        ) : base(logger, plcService, projectSettingRepository, communicationService)
        {
            _logger = logger;
            _plcService = plcService;
            _globalSettingRepository = globalSettingRepository;
            _usersRepository = usersRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
            _communicationService = communicationService;
            _algorithmService = algorithmService;
        }

        /// <summary>
        /// 初始化业务
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        protected override async Task InitializeAsync(CancellationToken ct)
        {
            _logger.Info($"初始化业务 {BusinessType} - {BusinessName}");

            CCTBCore.CoatingMessageNotificationEvent -= OnCoatingMessageNotificationAsync;
            CCTBCore.CoatingMessageNotificationEvent += OnCoatingMessageNotificationAsync;

            _logger.Info($"业务 {BusinessType} 初始化完成");
        }

        /// <summary>
        /// 执行业务
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _logger.Info($"业务 {BusinessType} {ClosedLoopType} 开始执行");

            // 流量采集任务
            var fluGatherTask = FluGatherLoopAsync(ct);

            // 闭环计算
            var calculateTask = CalculateLoopAsync(ct);

            await Task.WhenAny(fluGatherTask, calculateTask);
        }

        /// <summary>
        /// 清理
        /// </summary>
        /// <returns></returns>
        protected override async Task CleanupAsync()
        {
            _logger.Info($"清理业务 {BusinessType} 资源");

            CCTBCore.CoatingMessageNotificationEvent -= OnCoatingMessageNotificationAsync;

            // 清理资源
            gatherCoatingDataModels?.CompleteAdding();
            gatherCoatingDataModels?.Dispose();
            FluCache?.Clear();
            coatingDataCache?.Clear();
            _cts?.Cancel();
            _cts?.Dispose();

            await Task.CompletedTask;
        }

        #region 闭环逻辑开发
        #region 闭环全局参数
        /// <summary>
        /// 流量数据缓存
        /// </summary>
        private Dictionary<ESideType, Dictionary<byte, Queue<(DateTime, float)>>> FluCache = [];

        /// <summary>
        /// 重量数据缓存
        /// </summary>
        private BlockingCollection<CoatingDataModel> gatherCoatingDataModels = [];

        /// <summary>
        /// 数据缓存
        /// </summary>
        private List<(CoatingDataModel cd, MDCalculateModel cm)> coatingDataCache = [];
        #endregion

        #region 流量数据采集
        /// <summary>
        /// 流量数据采集任务
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task FluGatherLoopAsync(CancellationToken ct)
        {
            _logger.Info($"开始执行流量数据采集任务!");
            // 流量数据地址配置
            var pumpInfoLis = new List<(ESideType sideType, byte pump, string parameterName)>();
            var task = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    pumpInfoLis.Clear();
                    var gm = LoadGeneralSettingsModel();
                    for (int i = 0; i < gm.PumpCount; i++)
                    {
                        pumpInfoLis.Add((ESideType.A, (byte)(i + 1), $"机头{ESideType.A}流量{i + 1}"));
                        pumpInfoLis.Add((ESideType.B, (byte)(i + 1), $"机头{ESideType.B}流量{i + 1}"));
                    }

                    foreach ((ESideType sideType, byte pump, string parameterName) pumpInfo in pumpInfoLis)
                    {
                        var parameterName = pumpInfo.parameterName;
                        try
                        {
                            var value = ReadPLCValueFloat(parameterName);
                            lock (FluCache)
                            {
                                if (!FluCache.ContainsKey(pumpInfo.sideType))
                                    FluCache[pumpInfo.sideType] = new Dictionary<byte, Queue<(DateTime, float)>>();

                                if (!FluCache[pumpInfo.sideType].ContainsKey(pumpInfo.pump))
                                    FluCache[pumpInfo.sideType][pumpInfo.pump] = new Queue<(DateTime, float)>();

                                FluCache[pumpInfo.sideType][pumpInfo.pump].Enqueue((DateTime.Now, value));

                                if (FluCache[pumpInfo.sideType][pumpInfo.pump].Count() > 200)
                                    FluCache[pumpInfo.sideType][pumpInfo.pump].Dequeue();
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.Warn($"[{parameterName}] 流量采集任务已取消！");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"流量采集任务线程异常[5s后重试]", ex);
                            await Task.Delay(5000);
                        }
                    }
                }
            });
            await task.WaitAsync(ct);
        }
        #endregion

        #region 闭环计算    
        /// <summary>
        /// 异步接收数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnCoatingMessageNotificationAsync(object? sender, MessageEventArgs<CoatingDataModel> e)
        {
            if (e.Message == null) return;
            if (e.Message.Weights == null || e.Message.Weights.Count == 0) return;

            if (!IsNeedCoatingData(e.Tag)) return;
            await Task.Run(() => gatherCoatingDataModels.Add(e.Message));
        }

        /// <summary>
        /// 闭环计算逻辑
        /// </summary>
        /// <returns></returns>
        private async Task CalculateLoopAsync(CancellationToken ct)
        {
            var task = Task.Run(async () =>
            {
                foreach (var cd in gatherCoatingDataModels.GetConsumingEnumerable())
                {
                    try
                    {
                        var isSwitch = LoadClosedSwitch(cd.SideType);
                        if (!isSwitch) continue;

                        var cm = new MDCalculateModel();
                        cm.CalculateTime = DateTime.Now;
                        var gm = LoadGeneralSettingsModel();
                        var pm = LoadParamSettingModel(cd.SideType);
                        coatingDataCache.Add((cd, cm));
                        while (coatingDataCache.Count > 200) coatingDataCache.RemoveAt(0);
                        try
                        {
                            // 数据源不一致，则弃用
                            if (cd.WeightDataType != gm.WeightDataType) continue;

                            // 报文内目标设定值
                            cm.DynamicTargetValue = GetDynamicTargetValue(cd.SideType, cd.TargetValueA, cd.TargetValueB);

                            // 数据分区处理
                            cm.Weights = GetWeights(cd.WeightDataType, cd.Weights, gm);
                            // 目标值计算
                            cm.TargetValue = GetTargetValue(cd.WeightDataType, cd.SubstrateValue, cm.DynamicTargetValue, pm);
                            // 扫描类型描述
                            cm.ScanType = GetScanType(cd.ScanType);

                            // 过程数据初始化
                            Data_Init(cd, gm, pm, ref cm);

                            // 应用剔除规则：标记异常数据
                            ApplyEliminationRules(ClosedLoopType, cd.SideType, pm, ref cm);

                            // 执行闭环计算
                            ExcuteCalculte(cd.SideType, gm, pm, ref cm);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.Warn($"涂布闭环 {ClosedLoopType} 任务已取消！");
                        }
                        catch (Exception ex)
                        {
                            cm.EndMessage += ex.Message;
                            _logger.Error($"涂布闭环 {ClosedLoopType} 计算异常！", ex);
                        }
                        finally
                        {
                            _ = Task.Run(() =>
                            {
                                try
                                {
                                    var bf = LoadBasicFunctionSetting();
                                    var fullPath = Path.Combine(bf.SavePathOfCL, $"涂布{ClosedLoopType}闭环数据{cd.SideType}", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                                    var filePath = Path.Combine(fullPath, $"{CCTBCore.ProductSn}_{cd.SideType}.csv");
                                    var dic = ConvertToDictionary(gm, pm, cd, cm);
                                    var dt = CCTBCsvHelper.DicToTable(dic);
                                    lock (CCTBCsvHelper.WriteLocker) CCTBCsvHelper.WriteCSV(filePath, dt);
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error($"涂布闭环 {ClosedLoopType} {cd.SideType} 保存CSV文件异常！", ex);
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"涂布闭环 {ClosedLoopType} 计算异常！", ex);
                    }
                }
            });
            await task.WaitAsync(ct);
        }

        /// <summary>
        /// 初始化计算过程数据
        /// </summary>
        /// <param name="cd"></param>
        /// <param name="gm"></param>
        /// <param name="pm"></param>
        /// <param name="cm"></param>
        private void Data_Init(CoatingDataModel cd, GeneralSettingsModel gm, ClosedLoopSettingModel pm, ref MDCalculateModel cm)
        {
            var data = cm.Weights.Where(x => x != 0).ToList() ?? new List<float>();
            cm.WeightAvg = data.Average();
            cm.WeightMax = data.Max();
            cm.WeightMin = data.Min();
            cm.WeightRange = data.Max() - data.Min();
            cm.EWMA_Avg_WeightData = 0;
            cm.EWMA_Avg_FluAndWeightValue = 0;
            cm.LimitTimeB = 10;
            cm.Velocity = ReadPLCValueFloat("系统运行实时速度");
            cm.VelocitySetting = ReadPLCValueFloat("系统正转设定速度");
            cm.IsUseJudgeOfKnife = LoadBasicFunctionSetting().IsUseJudgeOfKnife;
            cm.KnifeSwitch = GetKnifeSwitch(cd.SideType);
            cm.FluDatasCache = GetFluDatas(cd.SideType, cd.GatherTime, cm.LastGatherTime);
            // B面闭环需要传入A面下发泵速数据   ——By 202210120078
            cm.BAdjustAvgValue_A = JJADequeue(cd.SideType, cd.GatherTime, gm, cm);
            cm.PumpSetValues = GetPumpSetValues(cd.SideType, gm.PumpCount, gm.SetB_IsRound, gm.SetB_Digits);
            cm.CLSwitchSignal = GetCLSwitchSignal(cd.SideType);
            cm.CLSwitchSignal_Supplier = GetCLSwitchSignal_Supplier(cd.SideType);

            GetRatioThresholdNumber(pm, ref cm);
        }

        /// <summary>
        /// 初始化闭环逻辑参数
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="gm"></param>
        /// <param name="pm"></param>
        /// <param name="cm"></param>
        private void ExcuteCalculte(ESideType sideType, GeneralSettingsModel gm, ClosedLoopSettingModel pm, ref MDCalculateModel cm)
        {
            try
            {
                // 异常剔除数据则退出
                if (cm.Rid_JudgeResult) return;

                // 获取缓存数据
                var wetightCache = coatingDataCache.Where(o => o.cd.SideType == sideType).ToList();
                if (wetightCache == null || wetightCache.Count <= 0) return;
                #region 缓存数据处理
                // 获取开机/重启/进刀(若开启)后的数据，同时剔除开头0数据，同时剔除固定开机数据 —— 具有相同数据源配置
                var wetightCache_RebootByZero = GetCacheAfterReboot(wetightCache, pm);
                // 异常数据间隔之后的数据，且剔除开头异常数据
                var wetightCache_Filtrate = GetCacheAfterFiltrate(wetightCache_RebootByZero, pm);
                #endregion

                if (wetightCache_Filtrate.Count <= 0) return;

                // 非剔除数据则继续进行规则计算
                // 计算均值及EWMA均值
                var wetightCache_Caculatation = EWMA(gm, pm, wetightCache_Filtrate, ref cm);
                // 滞后数据之后的数据，滞后起点：(1、下发点；)
                var wetightCache_RemoveLag = GetCacheAfterLagging(gm, pm, wetightCache_Caculatation);
                // 是否满足滞后条件
                if (wetightCache_RemoveLag.Count <= 0) return;

                var reLis = new List<Tuple<bool, string>>();
                reLis.Add(Judge_Threshold(pm, wetightCache_RemoveLag, ref cm));
                reLis.Add(Judge_Trend(pm, wetightCache_RemoveLag, ref cm));
                reLis.Add(Judge_Position(pm, wetightCache_RemoveLag, ref cm));
                reLis.Add(Judge_Ratio(pm, wetightCache_RemoveLag, ref cm));
                reLis.Add(Judge_Dynamic(pm, wetightCache_RemoveLag, ref cm));
                var judgResult = reLis.FirstOrDefault(o => o.Item1);
                Judge_Finally(judgResult, gm, pm, wetightCache_RemoveLag, ref cm);
            }
            catch (Exception ex)
            {
                _logger.Error($"计算{sideType}面{cm.MessageTag}架数据异常!", ex);
            }
        }

        #region 判定方法
        /// <summary>
        /// 计算EWMA及平均值 
        /// </summary>
        /// <param name="datas">开机/重启后首行非零数据之后的数据列表</param>
        /// <param name="cm">当前数据</param>
        private List<(CoatingDataModel cd, MDCalculateModel cm)> EWMA(GeneralSettingsModel gm, ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDCalculateModel cm)> datas, ref MDCalculateModel cm)
        {
            var result = new List<(CoatingDataModel cd, MDCalculateModel cm)>(datas);

            // 剔除异常数据
            var dl = datas.FindAll(o => o.cm.Rid_JudgeResult).ToList();

            // 计算流量面密度数据
            if (dl.Count >= 8)
            {
                dl.Reverse();
                var fluDataAvgCache = new List<Dictionary<byte, float>>();
                for (int i = 0; i < 7; i++)
                {
                    var avgCache = new Dictionary<byte, float>();
                    for (int j = 0; i < gm.PumpCount; i++)
                    {
                        var index = (byte)(j + 1);
                        avgCache[index] = dl[i].cm.FluDataAvgsCache.ContainsKey(index) ? dl[i].cm.FluDataAvgsCache[index] : 0;
                    }
                    fluDataAvgCache.Add(avgCache);
                }
                // 流量趋势变化量
                cm.FluDataDeltaPump1 = (fluDataAvgCache[0][1] + fluDataAvgCache[1][1] + fluDataAvgCache[2][1] - fluDataAvgCache[5][1] - fluDataAvgCache[6][1] - fluDataAvgCache[7][1]) / 3;
                cm.FluDataDeltaPump2 = (fluDataAvgCache[0][2] + fluDataAvgCache[1][2] + fluDataAvgCache[2][2] - fluDataAvgCache[5][2] - fluDataAvgCache[6][2] - fluDataAvgCache[7][2]) / 3;
                // 流量面密度数据
                var fluAndWeightValue = cm.WeightAvg + 0.4 * cm.FluDataDeltaPump1 + 0.3 * cm.FluDataDeltaPump2;
                cm.FluAndWeightValue = (float)Math.Round(fluAndWeightValue, 4);
                dl.Reverse();
            }

            #region EWMA_Avg_FluAndDataValue
            // 剔除FluAndDataValue计算的前7列数据
            var dl_FluAndDataValue = new List<(CoatingDataModel cd, MDCalculateModel cm)>(dl.Skip(Math.Min(dl.Count, 7)));
            if (dl_FluAndDataValue.Count() >= pm.EWMA_Count)
            {
                var daLis = dl_FluAndDataValue.Skip(dl_FluAndDataValue.Count - pm.EWMA_Count).Select(o => o.cm.FluAndWeightValue).ToList();
                cm.EWMA_Avg_FluAndWeightValue = daLis.Average();
            }

            // 剔除FluAndDataValue计算的前7列数据
            if (pm.IsEnableFlowTrendMA)
            {
                result.RemoveRange(0, Math.Min(result.Count, 7));
            }
            #endregion

            #region  EWMA_Avg_WeightData
            if (dl?.Count >= pm.EWMA_Count)
            {
                var daLis = dl.Skip(dl.Count - pm.EWMA_Count).Select(o => o.cm.WeightAvg).ToList();
                cm.EWMA_Avg_WeightData = daLis.Average();
            }
            #endregion

            // 剔除EWMA计算的前几行数据
            result.RemoveRange(0, Math.Min(result.Count, pm.EWMA_Count - 1));

            // 返回数据不剔除异常数据是因为滞后数量包含异常数据
            return result;
        }

        /// <summary>
        /// 获取超过比例阈值或低于比例阈值的重量数据的数量
        /// </summary>
        /// <param name="pm"></param>
        /// <param name="cm"></param>
        private void GetRatioThresholdNumber(ClosedLoopSettingModel pm, ref MDCalculateModel cm)
        {
            //比例阈值上限
            var ratioUp = cm.TargetValue + pm.JudgRatio_Threshold;
            //比例阈值下限
            var ratioDown = cm.TargetValue - pm.JudgRatio_Threshold;

            // 超上限个数
            cm.JudgRatio__UpThresholdCount = cm.Weights.Where(o => o != 0 && o > ratioUp).Count();
            // 超下限个数
            cm.JudgRatio__DownThresholdCount = cm.Weights.Where(o => o != 0 && o < ratioDown).Count();
        }

        /// <summary>
        /// 滞后数据之后的数据，滞后起点：(1、最后下发点)
        /// </summary>
        /// <param name="gm"></param>
        /// <param name="pm"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        private List<(CoatingDataModel cd, MDCalculateModel cm)> GetCacheAfterLagging(GeneralSettingsModel gm, ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDCalculateModel cm)> datas)
        {
            // 找到滞后起点(已触发调节)
            var laggingStartIndex = datas.FindLastIndex(o => o.cm.JudgTrigger);
            if (laggingStartIndex < 0) return datas;

            // 去掉滞后起点及固定滞后趟数之前的数据
            var laggingEndIndex = laggingStartIndex + pm.Lag_DataCount;
            var index = laggingEndIndex < datas.Count ? laggingEndIndex : datas.Count - 1;
            return datas.Skip(index).ToList();
        }

        #region 判定规则
        /// <summary>
        /// 阈值判定
        /// </summary>
        /// <param name="pm"></param>
        /// <param name="datas"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        private Tuple<bool, string> Judge_Threshold(ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDCalculateModel cm)> datas, ref MDCalculateModel cm)
        {
            var reBool = false;

            if (datas?.Count > 0)
            {
                // 初始化
                var ewma_Avg = pm.IsEnableFlowTrendMA ? cm.EWMA_Avg_FluAndWeightValue : cm.EWMA_Avg_WeightData;
                var EAdata = cm.TargetValue - ewma_Avg;
                cm.JudgThreshold_Result = Math.Abs(EAdata) > pm.JudgThreshold_Threshold;

                // 剔除异常数据
                var dl = datas.FindAll(o => !o.cm.Rid_JudgeResult).ToList();

                int index = 0, count = 0;
                for (int i = 0; i < Math.Min(dl.Count, pm.JudgThreshold_Count); i++)
                {
                    index = dl.Count - 1 - i;
                    if (dl[index].cm.JudgThreshold_Result)
                    {
                        count++;
                        // 累计出现bCount次数
                        if (count >= pm.JudgThreshold_Count)
                        {
                            reBool = true;
                            break;
                        }
                    }
                }
            }

            return Tuple.Create(reBool, "阈值判定");
        }

        /// <summary>
        /// 趋势判定
        /// </summary>
        /// <param name="pm"></param>
        /// <param name="datas"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        private Tuple<bool, string> Judge_Trend(ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDCalculateModel cm)> datas, ref MDCalculateModel cm)
        {
            var reBool = false;
            if (datas.Count > 0)
            {
                // 剔除异常数据
                var dl = datas.FindAll(o => !o.cm.Rid_JudgeResult).ToList();

                if (dl.Count > 1)
                {
                    var lastCM = dl[dl.Count - 2].cm;
                    var lastEWMAAvg = pm.IsEnableFlowTrendMA ? lastCM.EWMA_Avg_FluAndWeightValue : lastCM.EWMA_Avg_WeightData;
                    if (lastEWMAAvg > 0)
                    {
                        var ewma_Avg = pm.IsEnableFlowTrendMA ? cm.EWMA_Avg_FluAndWeightValue : cm.EWMA_Avg_WeightData;
                        if (ewma_Avg > cm.TargetValue && ewma_Avg >= lastEWMAAvg)
                            cm.JudgTrend_Result = "上增";
                        else if (ewma_Avg < cm.TargetValue && ewma_Avg <= lastEWMAAvg)
                            cm.JudgTrend_Result = "下减";
                        else
                            cm.JudgTrend_Result = "--";
                    }
                }

                int count = 0, index = 0;
                for (int i = 0; i < dl.Count; i++)
                {
                    index = dl.Count - 1 - i;
                    if (dl[index].cm.JudgTrend_Result != cm.JudgTrend_Result) break;
                    if (!(cm.JudgTrend_Result == "上增" || cm.JudgTrend_Result == "下减")) break;
                    count++;
                    // 达到指定条件
                    if (count >= pm.JudgTrend_Count)
                    {
                        reBool = true;
                        break;
                    }
                }
            }

            return Tuple.Create(reBool, "趋势判定");
        }

        /// <summary>
        /// 位置判定
        /// </summary>
        /// <param name="pm"></param>
        /// <param name="datas"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        private Tuple<bool, string> Judge_Position(ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDCalculateModel cm)> datas, ref MDCalculateModel cm)
        {
            var reBool = false;

            if (datas.Count > 0)
            {
                // 初始化
                var positionUp = cm.TargetValue + pm.JudgPosition_Threshold;
                var positionDown = cm.TargetValue - pm.JudgPosition_Threshold;
                var ewma_Avg = pm.IsEnableFlowTrendMA ? cm.EWMA_Avg_FluAndWeightValue : cm.EWMA_Avg_WeightData;
                cm.JudgPosition_Position = ewma_Avg > positionUp ? "上" : (ewma_Avg < positionDown ? "下" : "中");

                // 剔除异常数据
                var dl = datas.FindAll(o => !o.cm.Rid_JudgeResult).ToList();
                // 当前触发条件
                if (cm.JudgPosition_Position == "上" || cm.JudgPosition_Position == "下")
                {
                    var count = 0;
                    for (int i = 0; i < dl.Count; i++)
                    {
                        var index = dl.Count - 1 - i;
                        if (!(dl[index].cm.JudgPosition_Position == "上" || dl[index].cm.JudgPosition_Position == "下")) break;
                        count++;
                        // 达到指定条件
                        if (count >= pm.JudgPosition_Count)
                        {
                            reBool = true;
                            break;
                        }
                    }
                }
            }

            return Tuple.Create(reBool, "位置判定");
        }

        /// <summary>
        /// 比例判定
        /// </summary>
        /// <param name="pm"></param>
        /// <param name="datas"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        private Tuple<bool, string> Judge_Ratio(ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDCalculateModel cm)> datas, ref MDCalculateModel cm)
        {
            var reBool = false;

            if (datas.Count > 0)
            {
                // 初始化
                var dataCount = cm.Weights.Where(x => x != 0).ToArray().Count();
                //大于比例阈值的比例数th
                var ratio1 = cm.JudgRatio__UpThresholdCount / dataCount;
                //小于比例阈值的比例数
                var ratio2 = cm.JudgRatio__DownThresholdCount / dataCount;
                cm.JudgRatio__Result = (ratio1 >= pm.JudgRatio_Ratio || ratio2 >= pm.JudgRatio_Ratio);

                // 剔除异常数据
                var dl = datas.FindAll(o => !o.cm.Rid_JudgeResult).ToList();

                var count = 0;
                for (int i = 0; i < Math.Min(dl.Count, pm.JudgRatio_Count); i++)
                {
                    var index = dl.Count - 1 - i;
                    if (!dl[index].cm.JudgRatio__Result) continue;
                    count++;
                    // 达到指定条件
                    if (count >= pm.JudgRatio_Count)
                    {
                        reBool = true;
                        break;
                    }
                }
            }

            return Tuple.Create(reBool, "比例判定");
        }

        /// <summary>
        /// 动态判断数据生成器
        /// </summary>
        private Random RandomGenerator = new Random();
        /// <summary>
        /// 动态判定
        /// </summary>
        /// <param name="pm"></param>
        /// <param name="datas"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        private Tuple<bool, string> Judge_Dynamic(ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDCalculateModel cm)> datas, ref MDCalculateModel cm)
        {
            var reBool = false;

            if (datas.Count > 0)
            {
                // 初始化
                cm.JudgDynamic_Result = (byte)RandomGenerator.Next(0, 101);
                reBool = cm.JudgDynamic_Result > pm.JudgDynamic_Value;
            }

            return Tuple.Create(reBool, "动态判定");
        }
        #endregion

        /// <summary>
        /// 判定之后
        /// </summary>
        /// <returns></returns>
        private void Judge_Finally(Tuple<bool, string>? judgResult, GeneralSettingsModel gm, ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDCalculateModel cm)> datas, ref MDCalculateModel cm)
        {
            cm.Judg_HandelType = judgResult?.Item2 ?? "N";
            var currentData = datas.Last();
            var algorithmModel = _projectSettingRepository.LoadAlgorithmProjectParameters(ClosedLoopType, currentData.cd.SideType);

            if (judgResult?.Item1 ?? false)
            {
                // 极差判定
                if (!Judge_Range(pm, ref cm)) return;
                var algorithmType = AlgorithmModel.GetEAlgorithmType(ClosedLoopType, algorithmModel.AlgorithmName);
                if (algorithmType != EAlgorithmType.专家PID) throw new Exception($"[{ClosedLoopType}] 业务不实现算法 [{algorithmType}:{algorithmModel.AlgorithmName}]！");
                var dataCount = 3;
                if (!Judge_DataCount(dataCount, datas, ref cm)) return;

                cm.NewBOfExpertPID = GetNewBByExpertPID(gm, pm, datas);
                cm.JudgTrigger = true;
                SetNewB(algorithmType, pm, currentData.cd, ref cm);
            }
            else// 流量趋势判定PID算法
            {
                if (!pm.JudgFluTrend_IsEnable) return;
                var algorithmType = AlgorithmModel.GetEAlgorithmType(ClosedLoopType, algorithmModel.AlgorithmName, 1);
                if (algorithmType != EAlgorithmType.流量PID) throw new Exception($"[{ClosedLoopType}] 业务不实现算法 [{algorithmType}:{algorithmModel.AlgorithmName}]！");
                var dataCount = pm.Lag_DataCount + 9;
                if (!Judge_DataCount(dataCount, datas, ref cm)) return;

                var newB = GetNewBByFlow(gm, pm, datas);
                cm.NewBOfFlow = newB;
                if (newB.BAdjustDescription == "N")
                {
                    cm.JudgTrigger = false;
                    cm.Judg_HandelResult = $"流量趋势判定拦截";
                    return;
                }
                cm.JudgTrigger = true;
                cm.Judg_HandelType = "执行流量判定";
                SetNewB(algorithmType, pm, currentData.cd, ref cm);
            }
        }

        /// <summary>
        /// 极差规则
        /// </summary>
        /// <param name="pm"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        private bool Judge_Range(ClosedLoopSettingModel pm, ref MDCalculateModel cm)
        {
            try
            {
                var max = cm.WeightAvg;
                var min = cm.WeightMin;
                var range = cm.WeightRange;
                if (range > pm.JudgRange_Threshold)
                {
                    cm.JudgTrigger = false;
                    cm.Judg_HandelResult = "极差判定拦截";
                    _logger.Info($"极差判定:最大值:" + max.ToString() + "最小值" + min.ToString() + "极差" + range.ToString());
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"极差判定异常", ex);
            }
            return true;
        }

        private bool Judge_DataCount(int dataCount, List<(CoatingDataModel cd, MDCalculateModel cm)> datas, ref MDCalculateModel cm)
        {
            // 剔除异常数据
            var dl = datas.FindAll(o => !o.cm.Rid_JudgeResult).ToList();
            var dataCacheCount = dl.Exists(o => o.cm.JudgTrigger) ? (dl.Count - (dl.LastIndexOf(dl.Where(o => o.cm.JudgTrigger).Last()) + 1)) : dl.Count;
            if (dataCacheCount < dataCount)
            {
                cm.JudgTrigger = false;
                cm.Judg_HandelResult = $"泵速判定拦截:缓存数据不足[{dataCount}]";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 执行算法
        /// </summary>
        /// <param name="gm"></param>
        /// <param name="pm"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        private ExpertPIDOutputModel GetNewBByExpertPID(GeneralSettingsModel gm, ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDCalculateModel cm)> datas)
        {
            var currentData = datas.Last();
            var tarVal = currentData.cm.TargetValue;   //涂布重量目标值
            var curVal1 = GetEWMA_Avg(datas[datas.Count - 1].cm, pm.IsEnableFlowTrendMA);//当前时刻涂布重量均值
            var curVal2 = GetEWMA_Avg(datas[datas.Count - 1].cm, pm.IsEnableFlowTrendMA);//上一时刻涂布重量均值
            var curVal3 = GetEWMA_Avg(datas[datas.Count - 1].cm, pm.IsEnableFlowTrendMA);//上上一时刻涂布重量均值

            // 获取泵速
            var pumpSetValues = currentData.cm.PumpSetValues;
            var pump1 = pumpSetValues.Count > 0 ? pumpSetValues[0].Item1 : 0;
            var pump2 = pumpSetValues.Count > 1 ? pumpSetValues[1].Item1 : 0;

            // 获取流量均值
            var fluDataAvgsCache = currentData.cm.FluDataAvgsCache;
            var flow1 = fluDataAvgsCache.Count > 0 ? fluDataAvgsCache[0] : 0;
            var flow2 = fluDataAvgsCache.Count > 1 ? fluDataAvgsCache[1] : 0;

            var fluDataDeltaPump1 = currentData.cm.FluDataDeltaPump1;
            var fluDataDeltaPump2 = currentData.cm.FluDataDeltaPump2;
            var jjA = currentData.cm.BAdjustAvgValue_A;// A面调机均值

            return _algorithmService.ExcuteExpertPID(currentData.cd.SideType, pm.PID, tarVal, curVal1, curVal2, curVal3, pump1, pump2, flow1, flow2, fluDataDeltaPump1, fluDataDeltaPump2, jjA);
        }

        /// <summary>
        /// 执行算法
        /// </summary>
        /// <param name="gm"></param>
        /// <param name="pm"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        private FlowPIDOutputModel GetNewBByFlow(GeneralSettingsModel gm, ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDCalculateModel cm)> datas)
        {
            var currentData = datas.Last();
            // 非0流量数据均值-上泵、非0流量数据均值-下泵、极差、目标值T、数据源EWMA均值、设定泵速处理值-上泵、设定泵速处理值-下泵
            var fluData = "flu_top,flu_bottom,range,target,weight,pump_top,pump_bottom";
            for (int i = 9; i > 0; i--)
            {
                var data = datas[datas.Count - i];
                fluData += Environment.NewLine;

                // 获取泵速
                var pump1 = data.cm.PumpSetValues.Count > 0 ? data.cm.PumpSetValues[0].Item1 : 0;
                var pump2 = data.cm.PumpSetValues.Count > 1 ? data.cm.PumpSetValues[1].Item1 : 0;

                // 获取流量均值
                var flow1 = data.cm.FluDataAvgsCache.Count > 0 ? data.cm.FluDataAvgsCache[0] : 0;
                var flow2 = data.cm.FluDataAvgsCache.Count > 1 ? data.cm.FluDataAvgsCache[1] : 0;

                var ewma_Avg = GetEWMA_Avg(data.cm, pm.IsEnableFlowTrendMA);

                fluData += $"{flow1},{flow2},{data.cm.WeightRange},{data.cm.TargetValue},{ewma_Avg},{pump1},{pump2}";
            }

            // 获取泵速
            var pumpSetValues = currentData.cm.PumpSetValues;
            var _pump1 = pumpSetValues.Count > 0 ? pumpSetValues[0].Item1 : 0;
            var _pump2 = pumpSetValues.Count > 1 ? pumpSetValues[1].Item1 : 0;

            var ma = pm.EWMA_Count;// EWMA计算的数据个数
            var r = currentData.cm.WeightRange;// 极差
            var allTrend = pm.JudgFluTrend_VariationThreshold;// 流量判定规则-流量和趋势变化累计值的阈值
            var trend = pm.JudgFluTrend_BoundaryThreshold;// 流量判定规则-上、下流量趋势变化累计值的阈值
            var trendRule = pm.JudgFluTrend_Count;// 流量判定规则-连续个数
            var jjA = currentData.cm.BAdjustAvgValue_A;// A面调机均值

            return _algorithmService.ExcuteFlowPID(currentData.cd.SideType, _pump1, _pump2, fluData, ma, r, allTrend, trend, trendRule, jjA);
        }

        /// <summary>
        /// 下发泵速
        /// </summary>
        /// <param name="algorithmType"></param>
        /// <param name="pm"></param>
        /// <param name="cd"></param>
        /// <param name="cm"></param>
        /// <exception cref="Exception"></exception>
        private void SetNewB(EAlgorithmType algorithmType, ClosedLoopSettingModel pm, CoatingDataModel cd, ref MDCalculateModel cm)
        {
            var bPairs = new Dictionary<int, (float setB, float offsetB)>();
            var pumpOffsetValues = new List<float>();
            if (algorithmType == EAlgorithmType.专家PID)
            {
                if (cm.NewBOfExpertPID == null) throw new Exception("泵速下发值不可为空！");
                pumpOffsetValues.Add(cm.NewBOfExpertPID.BOffset1);
                pumpOffsetValues.Add(cm.NewBOfExpertPID.BOffset2);
            }
            if (algorithmType == EAlgorithmType.流量PID)
            {
                if (cm.NewBOfFlow == null) throw new Exception("泵速下发值不可为空！");
                if (cm.NewBOfFlow.BAdjustDescription == "N") return;
                pumpOffsetValues.Add(cm.NewBOfFlow.BOffset1);
                pumpOffsetValues.Add(cm.NewBOfFlow.BOffset2);
            }

            var isAdjust = true;
            // 下发泵速值处理
            for (byte i = 0; i < cm.PumpSetValues.Count; i++)
            {
                var newB = pumpOffsetValues.Count > i ? pumpOffsetValues[i] : 0;
                var setB = cm.PumpSetValues[i].speed;
                var f = newB - setB;
                var a = setB * pm.JudgSpeed_Range;
                bPairs[i] = (setB, f * pm.SetB_Coefficient);

                // 泵速上下限限制
                if (Math.Abs(f) > Math.Abs(a))
                {
                    var _a = f > 0 ? (0 + Math.Abs(a)) : (0 - Math.Abs(a));
                    bPairs[i] = (setB, _a * pm.SetB_Coefficient);
                }
            }

            // 如果都为0，则不调节
            if (bPairs.Values.All(v => v.offsetB == 0))
            {
                cm.JudgTrigger = false;
                cm.Judg_HandelResult = "泵速判定拦截";
                return;
            }

            if (isAdjust) SetNewB(cd, bPairs);
        }

        /// <summary>
        /// 下发泵速
        /// </summary>
        /// <param name="cd"></param>
        /// <param name="bPairs"></param>
        /// <exception cref="Exception"></exception>
        protected void SetNewB(CoatingDataModel cd, Dictionary<int, (float setB, float offsetB)> bPairs)
        {
            var bf = LoadBasicFunctionSetting();
            var isAdjust = false;
            if (bf.ModeOfPumpAdjust == EModeOfPumpAdjust.PLC)
            {
                isAdjust = ReadPLCValueFloat($"机头{cd.SideType}泵速闭环有效") == 1;
            }
            else if (bf.ModeOfPumpAdjust == EModeOfPumpAdjust.Network)
            {
                isAdjust = true;
            }
            else throw new Exception($"不支持的数据来源及下发方式[{bf.TBlockDataSource}]");

            SetNewBCommon(bf.ModeOfPumpAdjust, bf.CoatingDataSource, cd, bPairs, isAdjust);

            // 缓存A面调机值
            if (isAdjust && cd.SideType == ESideType.A)
            {
                JJAEnqueue(cd.GatherTime, bPairs.Values.Select(v => v.offsetB).ToList());
            }
        }

        /// <summary>
        /// 获取EWMA均值
        /// </summary>
        /// <param name="cm"></param>
        /// <param name="isEnableFlowTrenMA"></param>
        /// <returns></returns>
        private float GetEWMA_Avg(MDCalculateModel cm, bool isEnableFlowTrenMA)
        {
            return isEnableFlowTrenMA ? cm.EWMA_Avg_FluAndWeightValue : cm.EWMA_Avg_WeightData;
        }

        #region A面调机数据缓存
        /// <summary>
        /// A面调机数据缓存
        /// </summary>
        private Queue<Tuple<DateTime, List<float>>> JJACache = new Queue<Tuple<DateTime, List<float>>>();

        /// <summary>
        /// A面调机记录记录
        /// </summary>
        /// <param name="gatherTime"></param>
        /// <param name="values"></param>
        private void JJAEnqueue(DateTime gatherTime, List<float> values)
        {
            lock (JJACache)
            {
                JJACache.Enqueue(Tuple.Create(gatherTime, new List<float>(values)));
                if (JJACache.Count > 200) JJACache.Dequeue();
            }
        }

        /// <summary>
        /// 获取A面调机记录均值
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="gatherTime"></param>
        /// <param name="gm"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        private float JJADequeue(ESideType sideType, DateTime gatherTime, GeneralSettingsModel gm, MDCalculateModel cm)
        {
            if (sideType != ESideType.B) return 0;

            if (cm.VelocitySetting <= 0) return 0;

            // AB间隔时间
            var timeBetweenAB = gm.DistanceBetweenAB / cm.VelocitySetting;
            lock (JJACache)
            {
                if (JJACache.Count <= 0) return 0;

                var result = 0f;
                // 若A面调机数据大于AB面间隔时间
                while ((gatherTime - JJACache.Peek().Item1).TotalSeconds > timeBetweenAB)
                {
                    // 取出
                    var BAddjustValue = JJACache.Dequeue();
                    // 若A面调机数据在允许的误差时间内
                    if ((gatherTime - BAddjustValue.Item1).TotalSeconds < cm.LimitTimeB)
                        result = (float)BAddjustValue.Item2.Sum();

                    // 继续判断下一个，保证获取到最新的
                    if (JJACache.Count <= 0) break;
                }
                return result;
            }
        }
        #endregion
        #endregion

        /// <summary>
        /// 转换为字典数据
        /// </summary>
        /// <param name="gm"></param>
        /// <param name="pm"></param>
        /// <param name="cd"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(GeneralSettingsModel gm, ClosedLoopSettingModel pm, CoatingDataModel cd, MDCalculateModel cm)
        {
            var result = new Dictionary<string, string>();
            try
            {
                result.Add("时间", cd.GatherTime.ToString("yyyy-MM-dd HH:mm:ss"));
                result.Add("扫描类型", cm.ScanType ?? string.Empty);
                result.Add("趟数", cd.PassCount.ToString());
                result.Add("目标值T", cm.TargetValue.ToString());
                result.Add("基材值", cd.SubstrateValue.ToString());
                result.Add("报文目标值", cm.DynamicTargetValue.ToString());
                result.Add("目标设定值", pm.TChange.ToString());
                result.Add("目标值偏移量", pm.TOffset.ToString());

                result.Add("是否使用流量趋势数据计算MA", pm.IsEnableFlowTrendMA.ToString());
                result.Add("EWMA-密度横向均值", cm.EWMA_Avg_WeightData.ToString());
                result.Add("EWMA-流量&密度横向值", cm.EWMA_Avg_FluAndWeightValue.ToString());
                result.Add("密度横向均值", cm.WeightAvg.ToString());
                result.Add("流量&密度横向值", cm.FluAndWeightValue.ToString());

                var bf = _projectSettingRepository.LoadBasicFunctionSettingProjectParameters();
                result.Add("进退刀检测开关", bf.IsUseJudgeOfKnife ? "开启" : "关闭");
                result.Add("进退刀信号", cm.KnifeSwitch == 1 ? "进刀" : cm.KnifeSwitch == 0 ? "退刀" : $"{cm.KnifeSwitch}未知状态");

                result.Add($"{cd.SideType}闭环开关信号", cm.CLSwitchSignal.ToString());
                result.Add($"{cd.SideType}闭环开关信号_供应商", cm.CLSwitchSignal_Supplier.ToString());

                for (int i = 0; i < gm.PumpCount; i++)
                {
                    var index = (byte)(i + 1);
                    result.Add($"设定泵速实际值-{i + 1}", ReadPLCValueString($"机头{cd.SideType}浆料设定泵速{index}"));
                    result.Add($"设定泵速处理值-{i + 1}", cm.PumpSetValues.ContainsKey(index) ? cm.PumpSetValues[index].ToString() : string.Empty);
                    result.Add($"新泵速B微调偏移值-{i + 1}", ReadPLCValueString($"机头{cd.SideType}微调写入值{index}"));
                    result.Add($"实时泵速实际值-{i + 1}", ReadPLCValueString($"机头{cd.SideType}浆料设定泵速{index}"));
                }

                result.Add("专家PID算法输出", JsonConvert.SerializeObject(cm.NewBOfExpertPID));
                result.Add("流量PID算法输出", JsonConvert.SerializeObject(cm.NewBOfFlow));

                result.Add("新泵速调节系数", pm.SetB_Coefficient.ToString());
                result.Add("新泵速调节范围", pm.JudgSpeed_Range.ToString());

                foreach (var kv in cm.FluDataAvgsCache)
                {
                    result.Add($"非0流量数据均值-{kv.Key}", kv.Value.ToString());
                }

                result.Add("判异-走带速度", pm.Rid_VelocityCondition.ToString());
                result.Add("判异-阈值", pm.Rid_Threshold.ToString());
                result.Add("判异-判定个数", pm.Rid_Count.ToString());
                result.Add("是否异常参数", cm.Rid_JudgeResult.ToString());
                result.Add("异常描述", cm.Rid_JudgeDescription ?? string.Empty);
                result.Add("是否超出阈值", cm.JudgThreshold_Result.ToString());
                result.Add("规格线", cm.JudgPosition_Position ?? string.Empty);
                result.Add("趋势", cm.JudgTrend_Result ?? string.Empty);
                result.Add("大于比例阈值数", cm.JudgRatio__UpThresholdCount.ToString());
                result.Add("小于比例阈值数", cm.JudgRatio__DownThresholdCount.ToString());
                result.Add("比例阈值判定", cm.JudgRatio__Result.ToString());
                result.Add("流量趋势判定", cm.JudgFluTrend_Result ?? string.Empty);
                result.Add("触发调节", cm.JudgTrigger.ToString());
                result.Add("触发调节规则", cm.Judg_HandelType ?? string.Empty);
                result.Add("判定结果", cm.Judg_HandelResult);
                result.Add("闭环数据源类型", cd.WeightDataType.ToString());
                result.Add("膜区处理", gm.MembraneZoneType.ToString());
                result.Add("滞后趟数", pm.Lag_DataCount.ToString());
                result.Add("阈值b", pm.JudgThreshold_Threshold.ToString());
                result.Add("位置判定C", pm.JudgPosition_Threshold.ToString());
                result.Add("极差判定阈值", pm.JudgRange_Threshold.ToString());
                result.Add("动态判定规则", pm.JudgDynamic_Value.ToString());
                result.Add("动态判定结果", cm.JudgDynamic_Result.ToString());

                if (cd.SideType == ESideType.B)
                {
                    result.Add("AB面走带间隔距离_m", gm.DistanceBetweenAB.ToString());
                    result.Add("B面获取A面数据限制时间_s", cm.LimitTimeB.ToString());
                    result.Add("PLC走带实时速度", cm.Velocity.ToString());
                    result.Add("PLC走带设定速度", cm.VelocitySetting.ToString());
                }

                for (int k = 0; k < cd.Weights.Count(); k++)
                    result.Add("重量" + (k + 1).ToString(), cd.Weights[k].ToString());

                var ids = cd.SideType switch { ESideType.A => CCTBCore.PLCGatherConfigOfA.ToArray(), ESideType.B => CCTBCore.PLCGatherConfigOfB.ToArray(), _ => [] };
                ReadPLCValuesByIDs(ids, ref result);
            }
            catch (Exception ex)
            {
                _logger.Error($"{cd.SideType}:生成异常", ex);
            }

            return result;
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 获取流量数据
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="gatherTime"></param>
        /// <param name="lastGatherTime"></param>
        /// <returns></returns>
        private Dictionary<byte, List<float>> GetFluDatas(ESideType sideType, DateTime gatherTime, DateTime lastGatherTime)
        {
            var fluDic = new Dictionary<byte, List<(DateTime gatherTime, float flu)>>();
            if (FluCache?.ContainsKey(sideType) ?? false)
            {
                foreach (var pumpKv in FluCache[sideType])
                {
                    fluDic[pumpKv.Key] = pumpKv.Value.ToList();
                }
            }

            var result = new Dictionary<byte, List<float>>();
            // 获取本次重量数据至上次重量数据（最大时间间隔10秒）之间的流量数据
            var limitTime = Math.Min((gatherTime - lastGatherTime).TotalSeconds, 10);
            foreach (var fluKv in fluDic)
            {
                var flus = fluKv.Value.Where(o => (gatherTime - o.gatherTime).TotalSeconds <= limitTime).Select(o => o.flu).ToList();
                result[fluKv.Key] = flus;
            }

            return result;
        }
        #endregion
        #endregion
    }
}
