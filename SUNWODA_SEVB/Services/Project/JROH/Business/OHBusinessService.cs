using Dm.util;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HslCommunication;
using Newtonsoft.Json;
using Python.Runtime;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.JROH;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.JROH.OHBusinessModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.JROHHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.JROH.BusinessDialogs;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SUNWODA_SEVB.Services.Project.JROH.Business
{
    public class OHBusinessService : BusinessServiceBase<OHBusinessService>
    {
        private readonly ILoggerService<OHBusinessService> _logger;
        private readonly IPLCService _plcService;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        /// <summary>
        /// 业务类别
        /// </summary>
        public override EBusinessType BusinessType => EBusinessType.CoatedOH;

        /// <summary>
        /// 构造函数
        /// </summary>
        public OHBusinessService(
            ILoggerService<OHBusinessService> logger,
            IPLCService plcService,
            IGlobalSettingRepository globalSettingRepository,
            IUsersRepository usersRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        ) : base(logger, plcService, projectSettingRepository)
        {
            _logger = logger;
            _plcService = plcService;
            _globalSettingRepository = globalSettingRepository;
            _usersRepository = usersRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
        }

        /// <summary>
        /// 初始化业务
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        protected override async Task InitializeAsync(CancellationToken ct)
        {
            _logger.Info($"初始化业务 {BusinessType} - {BusinessName}");

            // 初始化参数配置
            paramModel = LoadBusinessParamConfig<OHBusinessParamModel>();

            // 初始化项目设置
            var rootPath = _projectSettingRepository.LoadJROHFileSaveRootPathProjectParameter();
            fileSaveRootPath = Path.Combine(rootPath, BusinessType.ToString());
            // 初始化各个任务
            InitializePLCSigalListenerMonitor();

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

            // 信号监听
            var signa1Task = PLCSigalListenerMonitorLoopAsync(poleSigalListener, ct);

            // 闭环计算
            var calculateTask = CalculateLoopAsync(ct);

            await Task.WhenAny(signa1Task, calculateTask);
        }

        /// <summary>
        /// 清理
        /// </summary>
        /// <returns></returns>
        protected override async Task CleanupAsync()
        {
            _logger.Info($"清理业务 {BusinessType} 资源");

            // 清理资源
            gatherDataModels?.Dispose();
            calculateDatasCache?.Clear();
            _cts?.Cancel();
            _cts?.Dispose();

            await Task.CompletedTask;
        }

        /// <summary>
        /// 显示业务配置对话框
        /// </summary>
        /// <returns></returns>
        public override async Task ShowBusinessParamConfigDialogAsync()
        {
            await ShowBusinessConfigDialogAsync<OHBusinessParamModel>(ShowBusinessConfigDialog);
            paramModel = LoadBusinessParamConfig<OHBusinessParamModel>();
        }

        #region 参数配置
        /// <summary>
        /// 显示业务类型2的配置对话框
        /// </summary>
        /// <typeparam name="TConfig"></typeparam>
        /// <param name="config"></param>
        /// <returns></returns>
        private async Task<bool> ShowBusinessConfigDialog<TConfig>(TConfig config) where TConfig : class
        {
            if (!(config is OHBusinessParamModel model)) return false;
            var result = await Dialog.Show(new VM_OHBusinessParamDialog().UIElement)
                    .Initialize<VM_OHBusinessParamDialog>(vm =>
                    {
                        vm.UserRole = UserRole;
                        vm.ItemSource = model;
                    }).GetResultAsync<bool>();
            return result;
        }
        #endregion

        #region 闭环逻辑开发
        #region 闭环全局参数
        /// <summary>
        /// 文件保存根目录
        /// </summary>
        private string fileSaveRootPath = string.Empty;

        /// <summary>
        /// PLC采集数据缓存
        /// </summary>
        private BlockingCollection<OHBusinessGatherModel> gatherDataModels = new();

        /// <summary>
        /// 计算过程数据缓存
        /// </summary>
        private List<(OHBusinessCalculateModel calculateData, OHBusinessGatherModel gatherData)> calculateDatasCache = new();
        #endregion

        #region 信号监听/数据采集
        private int moduleWidthOfPosA, moduleWidthOfPosB;

        private ValueListener? poleSigalListener, coatedSigalListener;

        /// <summary>
        /// 初始化PLC信号监听委托
        /// </summary>
        private void InitializePLCSigalListenerMonitor()
        {
            poleSigalListener = new ValueListener() { Name = "极片数据触发信号", Value = "0" };
            poleSigalListener.OnValueChangedEven -= OnPoleListenerValueChangedAsync;
            poleSigalListener.OnValueChangedEven += OnPoleListenerValueChangedAsync;
            coatedSigalListener = new ValueListener() { Name = "包覆数据触发信号", Value = "0" };
            coatedSigalListener.OnValueChangedEven -= OnCoatedListenerValueChangedAsync;
            coatedSigalListener.OnValueChangedEven += OnCoatedListenerValueChangedAsync;
        }

        /// <summary>
        /// 初始化PLC信号监听循环
        /// </summary>
        /// <returns></returns>
        private async Task PLCSigalListenerMonitorLoopAsync(ValueListener? listener, CancellationToken ct)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            var task = Task.Run(async () =>
            {
                var rm = new Random();
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        listener.Value = GetPLCAddressValue(listener.Name);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Info($"PLC信号[{listener.Name}]监听线程任务已取消！");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"PLC信号[{listener.Name}]监听线程异常", ex);
                    }
                }
            });
            await task.WaitAsync(ct);
        }

        /// <summary>
        /// PLC数据采集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnPoleListenerValueChangedAsync(object? sender, EventArgs<string> args)
        {
            if (!(sender is ValueListener listener)) return;
            if (listener.Name != "极片数据触发信号") return;
            if (args.OldValue == args.NewValue) return;

            if (args.NewValue == "0")
            {
                _logger.Info($"[{listener.Name}]读取数据信号复位0");
                return;
            }

            if (args.NewValue == "1")
            {
                _logger.Debug($"[{listener.Name}]读取数据信号置位1");

                await Task.Delay(200);
                moduleWidthOfPosA = GetPLCAddressValue<int>("正极A面CCD模宽数据");
                moduleWidthOfPosB = GetPLCAddressValue<int>("正极B面CCD模宽数据");
                return;
            }

            _logger.Error($"{listener.Name}读取数据信号为异常值[{args.NewValue}]");
        }

        private async void OnCoatedListenerValueChangedAsync(object? sender, EventArgs<string> args)
        {
            if (!(sender is ValueListener listener)) return;
            if (listener.Name != "包覆数据触发信号") return;
            if (args.OldValue == args.NewValue) return;

            if (args.NewValue == "0")
            {
                _logger.Info($"[{listener.Name}]读取数据信号复位0");
                return;
            }

            if (args.NewValue == "1")
            {
                _logger.Debug($"[{listener.Name}]读取数据信号置位1");

                await Task.Delay(200);
                var gatherModel = new OHBusinessGatherModel();
                gatherModel.GatherTime = DateTime.Now;
                gatherModel.VirtualCode = GetPLCAddressValue("虚拟码");
                gatherModel.ModuleWidthOfPosA = moduleWidthOfPosA;
                gatherModel.ModuleWidthOfPosB = moduleWidthOfPosB;
                gatherModel.ACOfTop = GetPLCAddressValue<int[]>("顶部AC") ?? [];
                gatherModel.ACOfBottom = GetPLCAddressValue<int[]>("底部AC") ?? [];
                gatherModel.SAOfBottom = GetPLCAddressValue<int[]>("底部SA") ?? [];
                gatherModel.SAOfTop = GetPLCAddressValue<int[]>("顶部SA") ?? [];
                gatherModel.AdjustmentValueOfPosFront = GetPLCAddressValue<int>("正极前段纠偏值");
                gatherModel.AdjustmentValueOfNegFront = GetPLCAddressValue<int>("负极前段纠偏值");
                gatherModel.AdjustmentValueOfPosMiddle = GetPLCAddressValue<int>("正极中段纠偏值");
                gatherModel.AdjustmentValueOfNegMiddle = GetPLCAddressValue<int>("负极中段纠偏值");
                gatherModel.AdjustmentValueOfPosBack = GetPLCAddressValue<int>("正极后段纠偏值");
                gatherModel.AdjustmentValueOfNegBack = GetPLCAddressValue<int>("负极后段纠偏值");

                // 缓存
                gatherDataModels.Add(gatherModel);
                OnDataReceived(gatherModel);
                return;
            }

            _logger.Error($"{listener.Name}读取数据信号为异常值[{args.NewValue}]");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vOfPos"></param>
        /// <param name="vOfNeg"></param>
        /// <param name="upperOfPos"></param>
        /// <param name="lowerOfPos"></param>
        /// <param name="upperOfNeg"></param>
        /// <param name="lowerOfNeg"></param>
        private void OnDataReceived(OHBusinessGatherModel gatherData)
        {
            var param = paramModel.CopyByJson();
            var message = (gatherData, param);
            RaiseMessageNotifation(this, message);
        }
        #endregion

        #region 闭环计算
        /// <summary>
        /// 闭环参数
        /// </summary>
        private OHBusinessParamModel? paramModel;

        /// <summary>
        /// 闭环计算逻辑
        /// </summary>
        /// <returns></returns>
        private async Task CalculateLoopAsync(CancellationToken ct)
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    // 正极下发缓存（依据AdjustInterval求平均值下发）
                    var adjustValueOfPosCache = new List<int>();
                    // 负极下发缓存（依据AdjustInterval求平均值下发）
                    var adjustValueOfNegCache = new List<int>();
                    foreach (var gatherData in gatherDataModels.GetConsumingEnumerable())
                    {
                        var pm = paramModel.CopyByJson() ?? new();
                        var cm = new OHBusinessCalculateModel();
                        cm.CalculateTime = DateTime.Now;
                        try
                        {
                            if (pm.IsSigleClosedLoop) await CalculateSigalSide(pm, gatherData, cm);
                            else await CalculateDoubleSide(pm, gatherData, cm);
                        }
                        catch (Exception ex)
                        {
                            cm.EndMessage += ex.Message;
                            _logger.Error($"[{BusinessName}]计算异常！", ex);
                        }
                        finally
                        {
                            // 计算过程模型异步保存CSV
                            _ = Task.Run(() =>
                            {
                                var savePath = Path.Combine(fileSaveRootPath, $"缓存数据_{BusinessName}\\{DateTime.Now:yyyyMMdd}");
                                var dic = ConvertToDictionary(gatherData, cm, pm);
                                var dt = JROHCsvHelper.DicToTable(dic);
                                lock (JROHCsvHelper.WriteLocker) JROHCsvHelper.WriteCSV(savePath, dt);
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[{BusinessName}]异常！", ex);
                    throw;
                }
            });
            await task.WaitAsync(ct);
        }

        #region 闭环逻辑
        #region 单边逻辑
        private List<OHBusinessCalculateModel> cmSigalCache = [];

        /// <summary>
        /// 单边逻辑闭环
        /// </summary>
        /// <param name="pm"></param>
        /// <param name="gatherData"></param>
        /// <param name="cm"></param>
        private async Task CalculateSigalSide(OHBusinessParamModel pm, OHBusinessGatherModel gatherData, OHBusinessCalculateModel cm)
        {
            // 1.计算A值
            var a = gatherData.ModuleWidthOfPosA - gatherData.ModuleWidthOfPosB;
            cm.A = a >= 0 ? 0 : a / 2;

            // 2.计算EF值
            (int eOfSAFront, int fOfSAFront) = GetEFOfSigal(ESections.Front, pm, gatherData, ref cm);
            (int eOfSAMiddle, int fOfSAMiddle) = GetEFOfSigal(ESections.Middle, pm, gatherData, ref cm);
            (int eOfSABack, int fOfSABack) = GetEFOfSigal(ESections.Back, pm, gatherData, ref cm);
            cm.Es = (eOfSAFront, eOfSAMiddle, eOfSABack);
            cm.Fs = (fOfSAFront, fOfSAMiddle, fOfSABack);

            // 3.缓存/获取
            cmSigalCache.Add(cm);
            if (cmSigalCache.Count < pm.AdjustFrequency) return;
            var cms = cmSigalCache.TakeLast(pm.AdjustFrequency).ToList();
            cmSigalCache.Clear();

            // 4.计算偏移量
            (int eOffsetOfMiddle, int fOffsetOfMiddle) = CalculateOffset(ESections.Middle, pm, cms);
            (int eOffsetOfFront, int fOffsetOfFront) = CalculateOffset(ESections.Front, pm, cms, eOffsetOfMiddle, fOffsetOfMiddle, cm.A);
            (int eOffsetOfBack, int fOffsetOfBack) = CalculateOffset(ESections.Back, pm, cms, eOffsetOfMiddle, fOffsetOfMiddle, cm.A);
            cm.IssueValueOfPosFront = gatherData.AdjustmentValueOfPosFront + eOffsetOfFront;
            cm.IssueValueOfNegFront = gatherData.AdjustmentValueOfNegFront + fOffsetOfFront;
            cm.IssueValueOfPosMiddle = gatherData.AdjustmentValueOfPosMiddle + eOffsetOfMiddle;
            cm.IssueValueOfNegMiddle = gatherData.AdjustmentValueOfNegMiddle + fOffsetOfMiddle;
            cm.IssueValueOfPosBack = gatherData.AdjustmentValueOfPosBack + eOffsetOfBack;
            cm.IssueValueOfNegBack = gatherData.AdjustmentValueOfNegBack + fOffsetOfBack;

            // 5.下发调机值
            await PLCIssueActionAsync(ESections.Front, pm, cm);
            await PLCIssueActionAsync(ESections.Middle, pm, cm);
            await PLCIssueActionAsync(ESections.Back, pm, cm);
        }

        /// <summary>
        /// 获取分段数据EF值
        /// </summary>
        /// <param name="p"></param>
        /// <param name="pm"></param>
        /// <param name="gatherData"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private (int e, int f) GetEFOfSigal(ESections sections, OHBusinessParamModel pm, OHBusinessGatherModel gatherData, ref OHBusinessCalculateModel cm)
        {
            // 获取分段数据信息
            (int start, int count) = sections switch
            {
                ESections.Front => (0, (int)pm.CountOfFront),
                ESections.Middle => (pm.CountOfFront, 90 - pm.CountOfFront - pm.CountOfBack),
                ESections.Back => (90 - pm.CountOfBack, pm.CountOfBack),
                _ => throw new NotImplementedException()
            };

            // 获取AC分段数据计算值
            var lowerOfAC = pm.NegToPosACBottom_LowerLimit;
            var upperOfAC = pm.NegToPosACBottom_UpperLimit;
            var upperLimitOfAC = pm.PoleWidthOfNeg_Center - pm.CoatedWidthOfPos_Center - pm.NegToPosACBottom_LowerLimit;
            (int minOfAC, int maxOfAC) = GetExtremum(gatherData.ACOfBottom, start, count, lowerOfAC, upperOfAC);
            var e = CalculateEFOfSigal(minOfAC, maxOfAC, lowerOfAC, upperOfAC, upperLimitOfAC);

            // 获取SA分段数据计算值
            var lowerOfSA = pm.MoldToNegSABottom_LowerLimit;
            var upperOfSA = pm.MoldToNegSABottom_UpperLimit;
            var upperLimitOfSA = pm.TotalWidthOfMold_LowerLimit - pm.PoleWidthOfPos_LowerLimit - pm.MoldToNegSABottom_LowerLimit;
            (int minOfSA, int maxOfSA) = GetExtremum(gatherData.SAOfBottom, start, count, lowerOfSA, upperOfSA);
            var f = CalculateEFOfSigal(minOfSA, maxOfSA, lowerOfSA, upperOfSA, upperLimitOfSA);

            return (e, f);
        }

        /// <summary>
        /// 计算EF值
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="lower"></param>
        /// <param name="upper"></param>
        /// <param name="upperLimit"></param>
        /// <returns></returns>
        private int CalculateEFOfSigal(int min, int max, int lower, int upper, int upperLimit)
        {
            var upperMax = Math.Max(upper, upperLimit);
            var ef1 = upperMax - max;
            var ef2 = min - lower;
            var ef = (ef1 - ef2) / 2;
            return ef;
        }
        #endregion

        #region 双边逻辑
        private List<OHBusinessCalculateModel> cmDoubleCache = [];

        /// <summary>
        /// 双边逻辑闭环
        /// </summary>
        /// <param name="pm"></param>
        /// <param name="gatherData"></param>
        /// <param name="cm"></param>
        private async Task CalculateDoubleSide(OHBusinessParamModel pm, OHBusinessGatherModel gatherData, OHBusinessCalculateModel cm)
        {
            // 1.计算A值
            var a = gatherData.ModuleWidthOfPosA - gatherData.ModuleWidthOfPosB;
            cm.A = a >= 0 ? 0 : a / 2;

            // 2.计算EF值
            (int eOfSAFront, int fOfSAFront) = GetEFOfDouble(ESections.Front, pm, gatherData, ref cm);
            (int eOfSAMiddle, int fOfSAMiddle) = GetEFOfDouble(ESections.Middle, pm, gatherData, ref cm);
            (int eOfSABack, int fOfSABack) = GetEFOfDouble(ESections.Back, pm, gatherData, ref cm);
            cm.Es = (eOfSAFront, eOfSAMiddle, eOfSABack);
            cm.Fs = (fOfSAFront, fOfSAMiddle, fOfSABack);

            // 3.缓存/获取
            cmDoubleCache.Add(cm);
            if (cmDoubleCache.Count < pm.AdjustFrequency) return;
            var cms = cmDoubleCache.TakeLast(pm.AdjustFrequency).ToList();
            cmDoubleCache.Clear();

            // 4.计算偏移量
            (int eOffsetOfMiddle, int fOffsetOfMiddle) = CalculateOffset(ESections.Middle, pm, cms);
            (int eOffsetOfFront, int fOffsetOfFront) = CalculateOffset(ESections.Front, pm, cms, eOffsetOfMiddle, fOffsetOfMiddle, cm.A);
            (int eOffsetOfBack, int fOffsetOfBack) = CalculateOffset(ESections.Back, pm, cms, eOffsetOfMiddle, fOffsetOfMiddle, cm.A);
            cm.IssueValueOfPosFront = gatherData.AdjustmentValueOfPosFront + eOffsetOfFront;
            cm.IssueValueOfNegFront = gatherData.AdjustmentValueOfNegFront + fOffsetOfFront;
            cm.IssueValueOfPosMiddle = gatherData.AdjustmentValueOfPosMiddle + eOffsetOfMiddle;
            cm.IssueValueOfNegMiddle = gatherData.AdjustmentValueOfNegMiddle + fOffsetOfMiddle;
            cm.IssueValueOfPosBack = gatherData.AdjustmentValueOfPosBack + eOffsetOfBack;
            cm.IssueValueOfNegBack = gatherData.AdjustmentValueOfNegBack + fOffsetOfBack;

            // 5.下发调机值
            await PLCIssueActionAsync(ESections.Front, pm, cm);
            await PLCIssueActionAsync(ESections.Middle, pm, cm);
            await PLCIssueActionAsync(ESections.Back, pm, cm);

        }

        /// <summary>
        /// 获取EF值
        /// </summary>
        /// <param name="p"></param>
        /// <param name="pm"></param>
        /// <param name="gatherData"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private (int min, int max) GetEFOfDouble(ESections sections, OHBusinessParamModel pm, OHBusinessGatherModel gatherData, ref OHBusinessCalculateModel cm)
        {
            // 获取分段数据信息
            (int start, int count) = sections switch
            {
                ESections.Front => (0, (int)pm.CountOfFront),
                ESections.Middle => (pm.CountOfFront, 90 - pm.CountOfFront - pm.CountOfBack),
                ESections.Back => (90 - pm.CountOfBack, pm.CountOfBack),
                _ => throw new NotImplementedException()
            };

            // 获取AC分段数据计算值
            var lowerOfACTop = pm.NegToPosACTop_LowerLimit;
            var upperOfACTop = pm.NegToPosACTop_UpperLimit;
            (int minOfACTop, int maxOfACTop) = GetExtremum(gatherData.ACOfTop, start, count, lowerOfACTop, upperOfACTop);
            var lowerOfACBtm = pm.NegToPosACBottom_LowerLimit;
            var upperOfACBtm = pm.NegToPosACBottom_UpperLimit;
            (int minOfACBtm, int maxOfACBtm) = GetExtremum(gatherData.ACOfBottom, start, count, lowerOfACBtm, upperOfACBtm);
            var e = CalculateEFOfDouble(minOfACTop, maxOfACTop, lowerOfACTop, upperOfACTop, minOfACBtm, maxOfACBtm, lowerOfACBtm, upperOfACBtm, pm.K);

            // 获取SA分段数据计算值
            var lowerOfSATop = pm.MoldToNegSATop_LowerLimit;
            var upperOfSATop = pm.MoldToNegSATop_UpperLimit;
            (int minOfSATop, int maxOfSATop) = GetExtremum(gatherData.SAOfTop, start, count, lowerOfSATop, upperOfSATop);
            var lowerOfSABtm = pm.MoldToNegSABottom_LowerLimit;
            var upperOfSABtm = pm.MoldToNegSABottom_UpperLimit;
            (int minOfSABtm, int maxOfSABtm) = GetExtremum(gatherData.SAOfBottom, start, count, lowerOfSABtm, upperOfSABtm);
            var f = CalculateEFOfDouble(minOfSATop, maxOfSATop, lowerOfSATop, upperOfSATop, minOfSABtm, maxOfSABtm, lowerOfSABtm, upperOfSABtm, pm.K);

            return (e, f);
        }

        /// <summary>
        /// 计算EF值
        /// </summary>
        /// <param name="min1"></param>
        /// <param name="max1"></param>
        /// <param name="lower1"></param>
        /// <param name="upper1"></param>
        /// <param name="min2"></param>
        /// <param name="max2"></param>
        /// <param name="lower2"></param>
        /// <param name="upper2"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        private int CalculateEFOfDouble(int min1, int max1, int lower1, int upper1, int min2, int max2, int lower2, int upper2, int k)
        {
            var upperDelta1 = upper1 - max1;
            var lowerDelta1 = min1 - lower1;
            var upperDelta2 = upper2 - max2;
            var lowerDelta2 = min2 - lower2;

            var firstIsUpperDominant = upperDelta1 > lowerDelta1;
            var secondIsUpperDominant = upperDelta2 > lowerDelta2;

            return (firstIsUpperDominant, secondIsUpperDominant) switch
            {
                (true, true) => (upperDelta2 - upperDelta1) / 2,
                (true, false) => k,
                (false, true) => -k,
                (false, false) => (lowerDelta1 - lowerDelta2) / 2,
            };
        }
        #endregion

        /// <summary>
        /// 获取最大最小值
        /// </summary>
        /// <param name="datas"></param>
        /// <param name="start"></param>
        /// <param name="count"></param>
        /// <param name="lower"></param>
        /// <param name="upper"></param>
        /// <returns></returns>
        private (int min, int max) GetExtremum(int[] datas, int start, int count, int lower, int upper)
        {
            var slice = datas.Skip(start).Take(count).Where(o => o > lower && o < upper);
            return (slice.Min(), slice.Max());
        }

        /// <summary>
        /// 计算偏移量
        /// </summary>
        /// <param name="sections"></param>
        /// <param name="cms"></param>
        /// <param name="eAvgOfMiddle"></param>
        /// <param name="fAvgOfMiddle"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private (int offsetOfPos, int offsetOfNeg) CalculateOffset(ESections sections, OHBusinessParamModel pm, List<OHBusinessCalculateModel> cms, int eAvgOfMiddle = 0, int fAvgOfMiddle = 0, int a = 0)
        {
            List<(int e, int f)> efs = sections switch
            {
                ESections.Front => cms.Select(o => (o.Es.eOfSAFront, o.Fs.feOfSAFront)).ToList(),
                ESections.Middle => cms.Select(o => (o.Es.eOfSAFront, o.Fs.feOfSAFront)).ToList(),
                ESections.Back => cms.Select(o => (o.Es.eOfSAFront, o.Fs.feOfSAFront)).ToList(),
                _ => throw new NotImplementedException()
            };

            var eAvg = (int)efs.Select(o => o.e).Average();
            var fAvg = (int)efs.Select(o => o.f).Average();

            (int ofsOfPos, int ofsOfNeg) = sections switch
            {
                ESections.Front => (eAvg - eAvgOfMiddle, a / 2 + fAvg - fAvgOfMiddle),
                ESections.Middle => (eAvg, fAvg),
                ESections.Back => (eAvg - eAvgOfMiddle, a / 2 + fAvg - fAvgOfMiddle),
                _ => throw new NotImplementedException()
            };

            var offsetOfPos = ApplyDeadzoneAndClamp(ofsOfPos, pm.MinLimitOfPos, pm.MaxLimitOfPos);
            var offsetOfNeg = ApplyDeadzoneAndClamp(ofsOfNeg, pm.MinLimitOfNeg, pm.MaxLimitOfNeg);
            return (offsetOfPos, offsetOfNeg);
        }

        /// <summary>
        /// 根据最大最小限幅计算偏移量
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="minLimit"></param>
        /// <param name="maxLimit"></param>
        /// <returns></returns>
        private int ApplyDeadzoneAndClamp(int value, int deadzone, int maxMagnitude)
        {
            var dz = Math.Abs(deadzone);
            var max = Math.Abs(maxMagnitude);

            if (Math.Abs(value) < dz) return 0;

            if (value > max) return max;
            if (value < -max) return -max;
            return value;
        }

        /// <summary>
        /// PLC纠偏值下发
        /// </summary>
        /// <param name="sections"></param>
        /// <param name="pm"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task PLCIssueActionAsync(ESections sections, OHBusinessParamModel pm, OHBusinessCalculateModel cm)
        {
            var sectionsName = sections switch
            {
                ESections.Front => "前段",
                ESections.Middle => "中断",
                ESections.Back => "后段",
                _ => throw new NotImplementedException()
            };

            (int issueOfPos, int issueOfNeg) = sections switch
            {
                ESections.Front => (cm.IssueValueOfPosFront, cm.IssueValueOfNegFront),
                ESections.Middle => (cm.IssueValueOfPosMiddle, cm.IssueValueOfNegMiddle),
                ESections.Back => (cm.IssueValueOfPosBack, cm.IssueValueOfNegBack),
                _ => throw new NotImplementedException()
            };

            // 正极纠偏值下发
            var paramNameOfPos = $"正极{sectionsName}纠偏下发值";
            if (!pm.ClosedLoopEnablePos) _logger.Warn($"[{paramNameOfPos}]推荐下发值[{issueOfPos}]!");
            else
            {
                var re = await WritePLCAsync(paramNameOfPos, issueOfPos);
                if (re.IsSuccess) _logger.Info($"[{paramNameOfPos}]下发值[{issueOfPos}]成功！");
                else _logger.Error($"[{paramNameOfPos}]下发值[{issueOfPos}]异常：[{re.Message}]!");
            }

            // 负极纠偏值下发
            var paramNameOfNeg = $"正极{sectionsName}纠偏下发值";
            if (!pm.ClosedLoopEnablePos) _logger.Warn($"[{paramNameOfNeg}]推荐下发值[{issueOfPos}]!");
            else
            {
                var re = await WritePLCAsync(paramNameOfNeg, issueOfPos);
                if (re.IsSuccess) _logger.Info($"[{paramNameOfNeg}]下发值[{issueOfPos}]成功！");
                else _logger.Error($"[{paramNameOfNeg}]下发值[{issueOfPos}]异常：[{re.Message}]!");
            }
        }

        /// <summary>
        /// 前中后段
        /// </summary>
        private enum ESections
        {
            Front, Middle, Back
        }
        #endregion

        /// <summary>
        /// CSV文件：闭环结果保存
        /// </summary>
        /// <param name="plcData"></param>
        /// <param name="ccdData"></param>
        /// <param name="calculateData"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(OHBusinessGatherModel gatherData, OHBusinessCalculateModel calculateData, OHBusinessParamModel paramModel)
        {
            var dic = new Dictionary<string, string>();
            dic.Add("采集时间", gatherData.GatherTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            dic.Add("虚拟码 ", gatherData.VirtualCode ?? string.Empty);

            dic.Add("负极入料基准纠偏设定值", gatherData.AdjustmentValueOfNegMiddle.ToString());
            dic.Add("负极前段补偿采集值", gatherData.AdjustmentValueOfNegFront.ToString());
            dic.Add("负极后段补偿采集值", gatherData.AdjustmentValueOfNegBack.ToString());

            dic.Add("负极入料纠偏发送值", calculateData.IssueValueOfNegMiddle.ToString());
            dic.Add("负极前段补偿发送值", calculateData.IssueValueOfNegFront.ToString());
            dic.Add("负极后段补偿发送值", calculateData.IssueValueOfNegBack.ToString());

            dic.Add("正极入料基准纠偏设定值", gatherData.AdjustmentValueOfPosMiddle.ToString());
            dic.Add("正极前段补偿采集值", gatherData.AdjustmentValueOfPosFront.ToString());
            dic.Add("正极后段补偿采集值", gatherData.AdjustmentValueOfPosBack.ToString());
            dic.Add("正极正反面模宽补偿值A/2", (calculateData.A / 2).ToString());

            dic.Add("正极入料纠偏发送值", calculateData.IssueValueOfPosMiddle.ToString());
            dic.Add("正极前段补偿发送值", calculateData.IssueValueOfPosFront.ToString());
            dic.Add("正极后段补偿发送值", calculateData.IssueValueOfPosBack.ToString());

            dic.Add("闭环结果信息", calculateData.EndMessage ?? string.Empty);

            return dic;
        }
        #endregion
        #endregion
    }
}
