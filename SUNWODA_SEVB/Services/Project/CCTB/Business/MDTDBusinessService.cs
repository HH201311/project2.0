using Dm.util;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Wordprocessing;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools.Extension;
using HslCommunication;
using HslCommunication.MQTT;
using Masuit.Tools;
using Newtonsoft.Json;
using OpenCvSharp.Dnn;
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
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Data.Models;
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
using TouchSocket.Sockets;

namespace SUNWODA_SEVB.Services.Project.CCTB.Business
{
    /// <summary>
    /// MDTD业务逻辑服务
    /// </summary>
    public class MDTDBusinessService : BusinessServiceBase<MDTDBusinessService>
    {
        private readonly ILoggerService<MDTDBusinessService> _logger;
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
        public override EBusinessType BusinessType => EBusinessType.MDTDBusiness;

        /// <summary>
        /// 闭环类型
        /// </summary>
        public override EClosedLoopType ClosedLoopType => EClosedLoopType.MDTD;

        /// <summary>
        /// 构造函数
        /// </summary>
        public MDTDBusinessService(
            ILoggerService<MDTDBusinessService> logger,
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

            CCTBCore.TBlockMessageNotificationEvent -= OnTBlockMessageNotificationAsync;
            CCTBCore.TBlockMessageNotificationEvent += OnTBlockMessageNotificationAsync;

            _logger.Info($"业务 {BusinessType} 初始化完成");
        }

        /// <summary>
        /// 执行业务
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _logger.Info($"业务 {BusinessType} 开始执行");

            // 闭环计算
            var calculateTask = CalculateLoopAsync(ct);

            await Task.WhenAny(calculateTask);
        }

        /// <summary>
        /// 清理
        /// </summary>
        /// <returns></returns>
        protected override async Task CleanupAsync()
        {
            _logger.Info($"清理业务 {BusinessType} 资源");

            CCTBCore.CoatingMessageNotificationEvent -= OnCoatingMessageNotificationAsync;
            CCTBCore.TBlockMessageNotificationEvent -= OnTBlockMessageNotificationAsync;

            // 清理资源
            gatherCoatingDataModels?.Dispose();
            coatingDataCache?.Clear();
            _cts?.Cancel();
            _cts?.Dispose();

            await Task.CompletedTask;
        }

        #region 闭环计算
        #region 闭环全局参数
        /// <summary>
        /// 重量数据缓存
        /// </summary>
        private BlockingCollection<CoatingDataModel> gatherCoatingDataModels = [];

        /// <summary>
        /// T块数据缓存
        /// </summary>
        private TBlockDataModel? tBlockDataCache;

        /// <summary>
        /// 数据缓存
        /// </summary>
        private List<(CoatingDataModel cd, MDTDCalculateModel cm)> coatingDataCache = [];
        #endregion

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
        /// 异步接收数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnTBlockMessageNotificationAsync(object? sender, MessageEventArgs<TBlockDataModel> e)
        {
            if (e.Message == null) return;
            if (e.Message == null || e.Message.TBlockInfoLis == null || e.Message.TBlockInfoLis.Count == 0) return;

            if (!IsNeedTBLockData(e.Tag)) return;
            tBlockDataCache = e.Message.CopyByJson();
        }

        /// <summary>
        /// 卷绕极耳错位闭环计算逻辑
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

                        var cm = new MDTDCalculateModel();
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
                            // 公差值赋值
                            cm.Tolerance = GetToleranceValue(cd.SideType, cd.ToleranceValueA, cd.ToleranceValueB);
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
        private void Data_Init(CoatingDataModel cd, GeneralSettingsModel gm, ClosedLoopSettingModel pm, ref MDTDCalculateModel cm)
        {
            var data = cm.Weights.Where(x => x != 0).ToList() ?? new List<float>();
            cm.WeightAvg = data.Average();
            cm.Velocity = ReadPLCValueFloat("系统运行实时速度");
            cm.VelocitySetting = ReadPLCValueFloat("系统正转设定速度");
            cm.IsUseJudgeOfKnife = LoadBasicFunctionSetting().IsUseJudgeOfKnife;
            cm.KnifeSwitch = GetKnifeSwitch(cd.SideType);
            cm.Lag_DelayTime = gm.DistanceBetweenAB / cm.VelocitySetting * 60;
            cm.Dearance_Left = ReadPLCValueFloat($"机头{cd.SideType} DIE头到钢辊间隙设定值-左");
            cm.Dearance_Right = ReadPLCValueFloat($"机头{cd.SideType} DIE头到钢辊间隙设定值-右");
            cm.PumpSetValues = GetPumpSetValues(cd.SideType, gm.PumpCount, gm.SetB_IsRound, gm.SetB_Digits);
            cm.TBlockData = tBlockDataCache.CopyByJson();
            cm.CLSwitchSignal = GetCLSwitchSignal(cd.SideType);
            cm.CLSwitchSignal_Supplier = GetCLSwitchSignal_Supplier(cd.SideType);
        }

        /// <summary>
        /// 初始化闭环逻辑参数
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="gm"></param>
        /// <param name="pm"></param>
        /// <param name="cm"></param>
        private void ExcuteCalculte(ESideType sideType, GeneralSettingsModel gm, ClosedLoopSettingModel pm, ref MDTDCalculateModel cm)
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
                // 滞后数据之后的数据，滞后起点：(1、下发点；2、刀距变化点；3、闭环重启点)
                var wetightCache_RemoveLag = GetCacheAfterLagging(gm, pm, wetightCache_Filtrate);
                // 是否满足滞后条件
                if (wetightCache_RemoveLag.Count <= 0) return;

                Judge_Finally(pm, wetightCache_RemoveLag, ref cm);
            }
            catch (Exception ex)
            {
                _logger.Error($"计算{sideType}面{cm.MessageTag}架数据异常!", ex);
            }
        }

        /// <summary>
        /// 滞后数据之后的数据，滞后起点：(1、最后下发点；2、刀距变化点；3、闭环重启点)
        /// </summary>
        /// <param name="gm"></param>
        /// <param name="pm"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        private List<(CoatingDataModel cd, MDTDCalculateModel cm)> GetCacheAfterLagging(GeneralSettingsModel gm, ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDTDCalculateModel cm)> datas)
        {
            var currentData = datas.Last();
            // 找到滞后起点(已触发调节)
            var laggingStartIndex = datas.FindLastIndex(o =>
            {
                // 已触发调节
                if (!o.cm.JudgTrigger) return true;
                // 刀距发生变化
                if (Math.Abs(currentData.cm.Dearance_Left - o.cm.Dearance_Left) >= pm.Lag_DearanceValue ||
                    Math.Abs(currentData.cm.Dearance_Right - o.cm.Dearance_Right) >= pm.Lag_DearanceValue)
                    return true;
                // 如果闭环重新开启(即：null/0/1 => !(null/0/1))
                if ((o.cm.TBlockData?.TAdjustMode == 0 || o.cm.TBlockData?.TAdjustMode == 1) &&
                    (currentData.cm.TBlockData?.TAdjustMode != 0 && o.cm.TBlockData?.TAdjustMode != 1))
                    return true;
                return false;
            });
            if (laggingStartIndex < 0) return datas;

            // 去掉滞后起点及固定时间之前的数据
            var laggingEndIndex = datas.FindIndex(o =>
            {
                return (o.cd.GatherTime - datas[laggingStartIndex].cd.GatherTime).TotalSeconds > currentData.cm.Lag_DelayTime;
            });
            var index = laggingEndIndex >= 0 ? laggingEndIndex : datas.Count - 1;
            return datas.Skip(index).ToList();
        }

        /// <summary>
        /// 拦截判断
        /// </summary>
        /// <param name="pm"></param>
        /// <param name="datas"></param>
        /// <param name="cm"></param>
        /// <exception cref="Exception"></exception>
        private void Judge_Finally(ClosedLoopSettingModel pm, List<(CoatingDataModel cd, MDTDCalculateModel cm)> datas, ref MDTDCalculateModel cm)
        {
            // 剔除异常数据
            var dl = datas.FindAll(o => !o.cm.Rid_JudgeResult).ToList();
            var inputDataCount = pm.Arithmetic_InputDataCount;
            if (dl.Count < inputDataCount)
            {
                cm.JudgTrigger = false;
                cm.Judg_HandelResult = $"拦截判定:缓存数据不足[{inputDataCount}]";
                return;
            }

            var currentData = datas.Last();
            var algorithmModel = _projectSettingRepository.LoadAlgorithmProjectParameters(ClosedLoopType, currentData.cd.SideType);
            if (!algorithmModel.IsEnable) throw new Exception($"{algorithmModel.AlgorithmName} 算法未启用！");
            var algorithmType = AlgorithmModel.GetEAlgorithmType(ClosedLoopType, algorithmModel.AlgorithmName);
            switch (algorithmType)
            {
                case EAlgorithmType.MDTDControl:
                    {
                        var dls = dl.TakeLast(inputDataCount).ToList();
                        var newBT = GetNewBTByMDTD(algorithmModel, dls);
                        cm.NewBT = newBT;
                        // 0:异常；1:仅调泵速；2:仅调T块；3:泵速、T块都调；4:限位调节
                        if (newBT.AdjustMode == 0)
                        {
                            cm.JudgTrigger = false;
                            cm.Judg_HandelResult = $"拦截判定:{newBT.AdjustMode}-{newBT.AdjustDescription}";
                            return;
                        }
                        cm.JudgTrigger = true;
                        cm.Judg_HandelResult = $"{newBT.AdjustMode}-{newBT.AdjustDescription}";
                        SetNewB(algorithmType, pm, currentData.cd, cm);
                        SetNewT(algorithmType, pm, currentData.cd, cm);
                        break;
                    }
                case EAlgorithmType.MULTIMDTDControl:
                    {
                        var dls = dl.TakeLast(inputDataCount).ToList();
                        var newBT = GetNewBTByMULTIMDTD(algorithmModel, dls);
                        cm.NewMultiBT = newBT;
                        // 0:异常；1:OK
                        if (newBT.BAdjustMode == 0 && newBT.TAdjustMode == 0)
                        {
                            cm.JudgTrigger = false;
                            cm.Judg_HandelResult = $"拦截判定:[泵速拦截:{newBT.BAdjustMode}-{JsonConvert.SerializeObject(newBT.BAdjustDescription)}][T块拦截:{newBT.TAdjustMode}-{JsonConvert.SerializeObject(newBT.TAdjustDescription)}]";
                            return;
                        }
                        cm.JudgTrigger = true;
                        cm.Judg_HandelResult = $"[泵速:{newBT.BAdjustMode}-{JsonConvert.SerializeObject(newBT.BAdjustDescription)}][T块:{newBT.TAdjustMode}-{JsonConvert.SerializeObject(newBT.TAdjustDescription)}]";
                        SetNewB(algorithmType, pm, currentData.cd, cm);
                        SetNewT(algorithmType, pm, currentData.cd, cm);
                        break;
                    }
                default: throw new Exception($"[{ClosedLoopType}] 业务不实现算法 [{algorithmType}:{algorithmModel.AlgorithmName}]！");
            }
        }

        /// <summary>
        /// 执行算法
        /// </summary>
        /// <param name="algorithmModel"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private MDTDControlOutputModel GetNewBTByMDTD(AlgorithmModel algorithmModel, List<(CoatingDataModel cd, MDTDCalculateModel cm)> datas)
        {
            var currentData = datas.Last();
            var dataStrLis = new List<List<string>>();
            for (int i = 0; i < datas.Count; i++)
            {
                if (i == 0) dataStrLis.Add(new List<string>());
                dataStrLis.Add(new List<string>());

                var index = datas.Count - i - 1;
                if (i == 0) dataStrLis[0].Add("目标值");
                dataStrLis[i + 1].Add(datas[index].cm.TargetValue.ToString());

                if (i == 0) dataStrLis[0].Add("公差");
                dataStrLis[i + 1].Add(datas[index].cm.Tolerance.ToString());

                if (i == 0) dataStrLis[0].Add("泵速");
                dataStrLis[i + 1].Add(datas[index].cm.PumpSetValues.ContainsKey(0) ? datas[index].cm.PumpSetValues[0].speed.ToString() : "");

                var tBlockData = datas[index].cm.TBlockData;
                if (tBlockData == null) throw new Exception($"T块数据采集值不可为空！");
                if (i == 0) dataStrLis[0].Add($"T块调机模式");
                dataStrLis[i + 1].Add(tBlockData.TAdjustMode.ToString());

                for (int j = 0; j < datas[index].cm.Weights.Count; j++)
                {
                    if (i == 0) dataStrLis[0].Add($"重量{j + 1}");
                    dataStrLis[i + 1].Add(datas[index].cm.Weights[j].ToString());
                }
                var tBlockInfoLis = tBlockData.TBlockInfoLis.OrderBy(o => o.TId).ToList();
                for (int j = 0; j < tBlockInfoLis.Count(); j++)
                {
                    if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-实时位置");
                    dataStrLis[i + 1].Add(tBlockInfoLis[j].TPosition.ToString());
                }
                for (int j = 0; j < tBlockInfoLis.Count(); j++)
                {
                    if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-上限值");
                    dataStrLis[i + 1].Add(tBlockInfoLis[j].TUpperLimit.ToString());
                }
                for (int j = 0; j < tBlockInfoLis.Count(); j++)
                {
                    if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-下限值");
                    dataStrLis[i + 1].Add(tBlockInfoLis[j].TLowerLimit.ToString());
                }
                for (int j = 0; j < tBlockInfoLis.Count(); j++)
                {
                    if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-使能");
                    dataStrLis[i + 1].Add(tBlockInfoLis[j].TEnable.ToString());
                }
                for (int j = 0; j < tBlockInfoLis.Count(); j++)
                {
                    if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-报警标识");
                    dataStrLis[i + 1].Add($"[{string.Join(",", tBlockInfoLis[j].AlarmCode ?? new List<int>())}]");
                }
            }

            var data = string.Join("\r\n", dataStrLis.ConvertAll(o => string.Join(",", o)));
            var result = _algorithmService.ExcuteMDTDControl(currentData.cd.SideType, data);
            _logger.Info($"{currentData.cd.SideType}-{EAlgorithmType.MDTDControl}算法输出[{result.AdjustMode}、{result.AdjustDescription}、{result.BOffset}、{JsonConvert.SerializeObject(result.TOffset)}]");
            return result;
        }

        /// <summary>
        /// 下发泵速
        /// </summary>
        /// <param name="algorithmType"></param>
        /// <param name="pm"></param>
        /// <param name="cd"></param>
        /// <param name="cm"></param>
        /// <exception cref="Exception"></exception>
        private void SetNewB(EAlgorithmType algorithmType, ClosedLoopSettingModel pm, CoatingDataModel cd, MDTDCalculateModel cm)
        {
            var bPairs = new Dictionary<int, (float setB, float offsetB)>();
            var isAdjust = false;
            if (algorithmType == EAlgorithmType.MDTDControl)
            {
                if (cm.NewBT == null) throw new Exception("泵速下发值不可为空！");
                var setB = cm.PumpSetValues.FirstOrDefault().Value.speed;
                bPairs[1] = (setB, cm.NewBT.BOffset * pm.SetB_Coefficient);
                isAdjust = cm.NewBT.AdjustMode == 1 || cm.NewBT.AdjustMode == 3 || cm.NewBT.AdjustMode == 4;
            }
            if (algorithmType == EAlgorithmType.MULTIMDTDControl)
            {
                if (cm.NewMultiBT == null || cm.NewMultiBT.BAdjustOffset == null) throw new Exception("泵速下发值不可为空！");
                foreach (var kv in cm.NewMultiBT.BAdjustOffset)
                {
                    var index = byte.Parse(Regex.Match(kv.Key, @"\d+").Value);
                    var setB = cm.PumpSetValues.ContainsKey(index) ? cm.PumpSetValues[index].speed : 0;
                    bPairs[index] = (setB, (float)(kv.Value * pm.SetB_Coefficient));
                }
                isAdjust = cm.NewMultiBT.BAdjustMode == 1;
            }

            if (isAdjust) SetNewB(cd, cm.TBlockData!, bPairs);
        }

        /// <summary>
        /// 下发泵速
        /// </summary>
        /// <param name="cd"></param>
        /// <param name="tBlockData"></param>
        /// <param name="newB"></param>
        /// <exception cref="Exception"></exception>
        protected async void SetNewB(CoatingDataModel cd, TBlockDataModel tBlockData, Dictionary<int, (float setB, float offsetB)> bPairs)
        {
            var bf = LoadBasicFunctionSetting();
            var isAdjust = false;
            if (bf.ModeOfPumpAdjust == EModeOfPumpAdjust.PLC)
            {
                // 游津、李果修改上川的PLC泵速下发逻辑(PLC开关地址为1、同时SC上位机标识为2/4）  --By 2210120078  20251209
                var isEnable = ReadPLCValueFloat($"机头{cd.SideType}泵速闭环有效") == 1;
                isAdjust = isEnable & (tBlockData.TAdjustMode == 2 || tBlockData.TAdjustMode == 4);
            }
            else if (bf.ModeOfPumpAdjust == EModeOfPumpAdjust.Network)
            {
                // 游津、李果修改上川的泵速下发逻辑(PLC供应商开关地址为1(若存在此地址)、同时SC上位机标识为3/4）  --By 2210120078  20251202
                var isEnable = ReadPLCValueFloat($"机头{cd.SideType}泵速闭环有效_供应商") == 1;
                isAdjust = isEnable & (tBlockData.TAdjustMode == 3 || tBlockData.TAdjustMode == 4);
            }
            else throw new Exception($"不支持的数据来源及下发方式[{bf.TBlockDataSource}]");

            SetNewBCommon(bf.ModeOfPumpAdjust, bf.CoatingDataSource, cd, bPairs, isAdjust);
        }

        /// <summary>
        /// T块调机下发
        /// </summary>
        /// <param name="algorithmType"></param>
        /// <param name="pm"></param>
        /// <param name="cd"></param>
        /// <param name="cm"></param>
        /// <exception cref="Exception"></exception>
        private void SetNewT(EAlgorithmType algorithmType, ClosedLoopSettingModel pm, CoatingDataModel cd, MDTDCalculateModel cm)
        {
            if (cm.TBlockData == null) throw new Exception("采集的T块数据不可为空！");
            var newT = new Dictionary<string, int>();
            var isAdjust = false;
            if (algorithmType == EAlgorithmType.MDTDControl)
            {
                if (cm.NewBT == null || cm.NewBT.TOffset == null) throw new Exception("T块下发值不可为空！");
                foreach (var kv in cm.NewBT.TOffset) newT[kv.Key] = (int)(kv.Value * pm.SetT_Coefficient);
                isAdjust = cm.NewBT.AdjustMode == 2 || cm.NewBT.AdjustMode == 3 || cm.NewBT.AdjustMode == 4;
            }
            if (algorithmType == EAlgorithmType.MULTIMDTDControl)
            {
                if (cm.NewMultiBT == null || cm.NewMultiBT.TAdjustOffset == null) throw new Exception("T块下发值不可为空！");
                foreach (var kv in cm.NewMultiBT.TAdjustOffset) newT[kv.Key] = (int)(kv.Value * pm.SetT_Coefficient);
                isAdjust = cm.NewMultiBT.TAdjustMode == 1;
            }

            if (isAdjust) SetNewTCommon(cd, cm.WeightAvg, cm.TBlockData, newT);
        }

        /// <summary>
        /// 执行算法
        /// </summary>
        /// <param name="algorithmModel"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        private MULTIMDTDControlOutputModel GetNewBTByMULTIMDTD(AlgorithmModel algorithmModel, List<(CoatingDataModel cd, MDTDCalculateModel cm)> datas)
        {
            var currentData = datas.Last();
            var dataStrLis = new List<List<string>>();
            for (int i = 0; i < datas.Count; i++)
            {
                if (i == 0) dataStrLis.Add(new List<string>());
                dataStrLis.Add(new List<string>());

                var index = datas.Count - i - 1;
                if (i == 0) dataStrLis[0].Add("时间");
                dataStrLis[i + 1].Add(datas[index].cd.GatherTime.ToString());

                if (i == 0) dataStrLis[0].Add("趟数");
                dataStrLis[i + 1].Add(datas[index].cd.PassCount.ToString());

                if (i == 0) dataStrLis[0].Add("目标值");
                dataStrLis[i + 1].Add(datas[index].cm.TargetValue.ToString());

                if (i == 0) dataStrLis[0].Add("公差");
                dataStrLis[i + 1].Add(datas[index].cm.Tolerance.ToString());

                foreach (var setB in datas[index].cm.PumpSetValues)
                {
                    if (i == 0) dataStrLis[0].Add($"泵速{setB.Key}");
                    dataStrLis[i + 1].Add(setB.Value.speed.ToString());
                    if (i == 0) dataStrLis[0].Add($"浆料泵启动标识{setB.Key}");
                    dataStrLis[i + 1].Add(setB.Value.flag.ToString());
                }

                var tBlockData = datas[index].cm.TBlockData;
                if (tBlockData == null) throw new Exception($"T块数据采集值不可为空！");
                if (i == 0) dataStrLis[0].Add($"T块调机模式");
                dataStrLis[i + 1].Add(tBlockData.TAdjustMode.ToString());

                for (int j = 0; j < datas[index].cm.Weights.Count; j++)
                {
                    if (i == 0) dataStrLis[0].Add($"重量{j + 1}");
                    dataStrLis[i + 1].Add(datas[index].cm.Weights[j].ToString());
                }
                var tBlockInfoLis = tBlockData.TBlockInfoLis.OrderBy(o => o.TId).ToList();
                for (int j = 0; j < tBlockInfoLis.Count(); j++)
                {
                    if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-实时位置");
                    dataStrLis[i + 1].Add(tBlockInfoLis[j].TPosition.ToString());
                }
                for (int j = 0; j < tBlockInfoLis.Count(); j++)
                {
                    if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-上限值");
                    dataStrLis[i + 1].Add(tBlockInfoLis[j].TUpperLimit.ToString());
                }
                for (int j = 0; j < tBlockInfoLis.Count(); j++)
                {
                    if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-下限值");
                    dataStrLis[i + 1].Add(tBlockInfoLis[j].TLowerLimit.ToString());
                }
                for (int j = 0; j < tBlockInfoLis.Count(); j++)
                {
                    if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-使能");
                    dataStrLis[i + 1].Add(tBlockInfoLis[j].TEnable.ToString());
                }
                for (int j = 0; j < tBlockInfoLis.Count(); j++)
                {
                    if (i == 0) dataStrLis[0].Add($"T块[{tBlockInfoLis[j].TId}]-报警标识");
                    dataStrLis[i + 1].Add($"[{string.Join(",", tBlockInfoLis[j].AlarmCode ?? new List<int>())}]");
                }
            }

            var data = string.Join("\r\n", dataStrLis.ConvertAll(o => string.Join(",", o)));
            var result = _algorithmService.ExcuteMULTIMDTDControl(currentData.cd.SideType, data);
            _logger.Info($"{currentData.cd.SideType}-{EAlgorithmType.MULTIMDTDControl}算法输出[{JsonConvert.SerializeObject(result)}]");
            return result;
        }

        /// <summary>
        /// 转换为字典数据
        /// </summary>
        /// <param name="gm"></param>
        /// <param name="pm"></param>
        /// <param name="cd"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(GeneralSettingsModel gm, ClosedLoopSettingModel pm, CoatingDataModel cd, MDTDCalculateModel cm)
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

                result.Add("密度横向均值", cm.WeightAvg.ToString());
                result.Add("AB面走带间隔距离(m)", gm.DistanceBetweenAB.ToString());
                result.Add("PLC走带实时速度", cm.Velocity.ToString());
                result.Add("PLC走带设定速度", cm.VelocitySetting.ToString());
                result.Add("刀距设定值左", cm.Dearance_Left.ToString());
                result.Add("刀距设定值右", cm.Dearance_Right.ToString());

                var bf = _projectSettingRepository.LoadBasicFunctionSettingProjectParameters();
                result.Add("进退刀检测开关", bf.IsUseJudgeOfKnife ? "开启" : "关闭");
                result.Add("进退刀信号", cm.KnifeSwitch == 1 ? "进刀" : cm.KnifeSwitch == 0 ? "退刀" : $"{cm.KnifeSwitch}未知状态");

                result.Add($"{cd.SideType}闭环开关信号", cm.CLSwitchSignal.ToString());
                result.Add($"{cd.SideType}闭环开关信号_供应商", cm.CLSwitchSignal_Supplier.ToString());

                result.Add("T块调节系数", pm.SetT_Coefficient.ToString());
                result.Add("算法入参趟数", pm.Arithmetic_InputDataCount.ToString());

                result.Add("判异-走带速度", pm.Rid_VelocityCondition.ToString());
                result.Add("判异-阈值", pm.Rid_Threshold.ToString());
                result.Add("判异-判定个数", pm.Rid_Count.ToString());
                result.Add("是否异常参数", cm.Rid_JudgeResult.ToString());
                result.Add("异常描述", cm.Rid_JudgeDescription ?? string.Empty);
                result.Add("触发调节", cm.JudgTrigger.ToString());
                result.Add("判定结果", cm.Judg_HandelResult);
                result.Add("闭环数据源类型", cd.WeightDataType.ToString());
                result.Add("膜区处理", gm.MembraneZoneType.ToString());
                result.Add("滞后时间(s)", cm.Lag_DelayTime.ToString());

                result.Add("单泵算法输出", JsonConvert.SerializeObject(cm.NewBT));
                result.Add("多泵算法输出", JsonConvert.SerializeObject(cm.NewMultiBT));
                if (cm.TBlockData != null)
                {
                    result.Add("T块信息-设备编号", cm.TBlockData?.DeviceName ?? string.Empty);
                    result.Add("T块信息-调机模式", cm.TBlockData?.TAdjustMode.ToString() ?? string.Empty);
                    result.Add("T块信息-T块总数", cm.TBlockData?.TNumber.ToString() ?? string.Empty);
                    result.Add("T块信息-响应时间", cm.TBlockData?.ReponseTime.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                    if (cm.TBlockData?.TBlockInfoLis != null)
                    {
                        var tBlockInfoLis = cm.TBlockData.TBlockInfoLis.OrderBy(o => o.TId);
                        foreach (var tBlockInfo in tBlockInfoLis)
                            result.Add($"T块[{tBlockInfo.TId}]使能", tBlockInfo.TEnable.ToString());
                        foreach (var tBlockInfo in tBlockInfoLis)
                            result.Add($"T块[{tBlockInfo.TId}]调机模式", tBlockInfo.TAdjustMode ? "自动" : "半自动");
                        foreach (var tBlockInfo in tBlockInfoLis)
                            result.Add($"T块[{tBlockInfo.TId}]实时位置", tBlockInfo.TPosition.ToString());
                        foreach (var tBlockInfo in tBlockInfoLis)
                            result.Add($"T块[{tBlockInfo.TId}]调机量", tBlockInfo.TAdjustInfo?.TAdjustValue.ToString() ?? string.Empty);
                        foreach (var tBlockInfo in tBlockInfoLis)
                            result.Add($"T块[{tBlockInfo.TId}]调机时间", tBlockInfo.TAdjustInfo?.TAdjustTime.ToString() ?? string.Empty);
                        foreach (var tBlockInfo in tBlockInfoLis)
                            result.Add($"T块[{tBlockInfo.TId}]调机时趟数", tBlockInfo.TAdjustInfo?.TAdjustScanCount.ToString() ?? string.Empty);
                        foreach (var tBlockInfo in tBlockInfoLis)
                            result.Add($"T块[{tBlockInfo.TId}]上限位置", tBlockInfo.TUpperLimit.ToString());
                        foreach (var tBlockInfo in tBlockInfoLis)
                            result.Add($"T块[{tBlockInfo.TId}]下限位置", tBlockInfo.TLowerLimit.ToString());
                        foreach (var tBlockInfo in tBlockInfoLis)
                            result.Add($"T块[{tBlockInfo.TId}]报警编码", $"[{string.Join(",", tBlockInfo.AlarmCode ?? new List<int>())}]");
                    }
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
    }
}
