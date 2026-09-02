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
using SUNWODA_SEVB.Core.Models.Project.JROH.PWBusinessModels;
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
    public class PWBusinessService : BusinessServiceBase<PWBusinessService>
    {
        private readonly ILoggerService<PWBusinessService> _logger;
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
        public override EBusinessType BusinessType => EBusinessType.PoleWidth;

        /// <summary>
        /// 构造函数
        /// </summary>
        public PWBusinessService(
            ILoggerService<PWBusinessService> logger,
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
            paramModel = LoadBusinessParamConfig<PWBusinessParamModel>();

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
            var signa1Task = PLCSigalListenerMonitorLoopAsync(plcSigalListener, ct);

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
            await ShowBusinessConfigDialogAsync<PWBusinessParamModel>(ShowBusinessConfigDialog);
            paramModel = LoadBusinessParamConfig<PWBusinessParamModel>();
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
            if (!(config is PWBusinessParamModel model)) return false;
            var result = await Dialog.Show(new VM_PWBusinessParamDialog().UIElement)
                    .Initialize<VM_PWBusinessParamDialog>(vm =>
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
        private BlockingCollection<PWBusinessGatherModel> gatherDataModels = new();

        /// <summary>
        /// 计算过程数据缓存
        /// </summary>
        private List<(PWBusinessCalculateModel calculateData, PWBusinessGatherModel gatherData)> calculateDatasCache = new();
        #endregion

        #region 信号监听/数据采集
        private ValueListener? plcSigalListener;

        /// <summary>
        /// 初始化PLC信号监听委托
        /// </summary>
        private void InitializePLCSigalListenerMonitor()
        {
            plcSigalListener = new ValueListener() { Name = "极片数据触发信号", Value = "0" };
            plcSigalListener.OnValueChangedEven -= OnPLCListenerValueChangedAsync;
            plcSigalListener.OnValueChangedEven += OnPLCListenerValueChangedAsync;
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
                        //var d = new PWBusinessGatherModel();
                        //d.ModuleWidthOfPos = rm.Next(190000, 193000);
                        //d.ModuleWidthOfNeg = rm.Next(190000, 193000);
                        //OnDataReceived(d);

                        //var dic = new Dictionary<string, string>();
                        //dic["1231231"] = "asdfafas";
                        //dic["1231sad231"] = "asdgadsfafas";
                        //dic["123s1231"] = "asdfafas";
                        //dic["123vc1231"] = "asdfasdafas";
                        //dic["1231b231"] = "asdfsadafas";
                        //dic["12g31231"] = "asdfsadafas";
                        //dic["12331231"] = "asdfczafas";
                        //dic["12312j31"] = "asdfxZCafas";
                        //OnAdjustmentRecordReceived(dic);

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
        private async void OnPLCListenerValueChangedAsync(object? sender, EventArgs<string> args)
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
                int valueInt;
                var gatherModel = new PWBusinessGatherModel();
                gatherModel.GatherTime = DateTime.Now;
                gatherModel.VirtualCode = GetPLCAddressValue("虚拟码");
                gatherModel.ModuleWidthOfPosA = int.TryParse(GetPLCAddressValue("正极A面CCD模宽数据"), out valueInt) ? valueInt : 0;
                gatherModel.ModuleWidthOfPosB = int.TryParse(GetPLCAddressValue("正极B面CCD模宽数据"), out valueInt) ? valueInt : 0;
                gatherModel.ModuleWidthOfNeg = int.TryParse(GetPLCAddressValue("负极CCD模宽数据"), out valueInt) ? valueInt : 0;
                gatherModel.ModuleWidthOfPos = int.TryParse(GetPLCAddressValue("正极CCD模宽数据"), out valueInt) ? valueInt : 0;
                gatherModel.OriginYOfPos = int.TryParse(GetPLCAddressValue("正极飞行原点Y"), out valueInt) ? valueInt : 0;
                gatherModel.OriginYOfNeg = int.TryParse(GetPLCAddressValue("负极飞行原点Y"), out valueInt) ? valueInt : 0;

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
        private void OnDataReceived(PWBusinessGatherModel gatherData)
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
        private PWBusinessParamModel? paramModel;

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
                        var cm = new PWBusinessCalculateModel();
                        cm.CalculateTime = DateTime.Now;
                        try
                        {
                            // 选取模宽较大的mean值作为参考基准（正极模宽）
                            var maxModuleWidthOfPos = Math.Max(gatherData.ModuleWidthOfPosA, gatherData.ModuleWidthOfPosB);
                            // 负极总宽尺寸 = 正极模宽 + 负极到正极AC(顶部)基准 + 负极到正极AC(底部)基准
                            cm.TargetTotalWidthOfNeg = maxModuleWidthOfPos + pm.NegToPosACTop_Center + pm.NegToPosACBottom_Center;
                            // 顶部SA值 = 隔膜宽度196mm - 负极极片宽度191mm - 底部SA值2.2mm = 2.8mm
                            var valueTopOfSA = pm.TotalWidthOfMold_Center - pm.PoleWidthOfNeg_Center - pm.MoldToNegSABottom_Center;
                            // 正极模宽 ≤ 负极总宽 ≤ (隔膜下限值 - 隔膜到负极SA(底部) - 隔膜到负极SA(顶部) - 防呆尺寸)
                            if (!(maxModuleWidthOfPos <= cm.TargetTotalWidthOfNeg && cm.TargetTotalWidthOfNeg >= (pm.TotalWidthOfMold_LowerLimit - pm.MoldToNegSABottom_Center - pm.MoldToNegSATop_Center - pm.OhWidthCheckValueNeg)))
                            {
                                var msg = "正极模宽不符合工艺要求!";
                                _logger.Warn(msg);
                                MessageBox.Show(msg, $"设备报警:{BusinessName}");
                                continue;
                            }

                            // 正极总宽尺寸 = 负极总宽尺寸 + CIL超负极 - 负极到正极AC(底部)基准
                            cm.TargetTotalWidthOfPos = cm.TargetTotalWidthOfNeg + pm.CilOverNegTop_Center - pm.NegToPosACBottom_Center;

                            #region 正极下发流程
                            var w1 = cm.TargetTotalWidthOfPos;
                            // 是否开启正极总宽尺寸管控
                            if (pm.CpControlEnablePos)
                            {
                                // 1,超上限发送正极总宽上限值
                                // 2,超下限发送正极总宽下限值
                                // 3,工艺范围内直接发送正极总宽尺寸
                                if (cm.TargetTotalWidthOfPos > pm.PoleWidthOfPos_UpperLimit)
                                {
                                    w1 = pm.PoleWidthOfPos_UpperLimit;
                                }
                                else if (cm.TargetTotalWidthOfPos < pm.PoleWidthOfPos_LowerLimit)
                                {
                                    w1 = pm.PoleWidthOfPos_LowerLimit;
                                }
                                else
                                {
                                    w1 = cm.TargetTotalWidthOfPos;
                                }
                            }

                            // 比对选定的尺寸W1&CCD数据W2总宽度，偏差值△W=W1-W2
                            cm.DeltaW = w1 - gatherData.ModuleWidthOfPos;
                            // 参数限幅
                            var adjustOffsetOfPos = cm.DeltaW;
                            if (Math.Abs(cm.DeltaW) < pm.MinAdjustmentLimitPos)
                            {
                                adjustOffsetOfPos = 0;
                            }
                            if (Math.Abs(cm.DeltaW) > pm.MaxAdjustmentLimitPos)
                            {
                                adjustOffsetOfPos = Math.Sign(cm.DeltaW) * pm.MaxAdjustmentLimitPos;
                            }

                            // 计算正极Y+K*（△W\0.4\-0.4）
                            var adjustValueOfPos = (int)(gatherData.OriginYOfPos + pm.AdjustCorrectionOfPos * adjustOffsetOfPos);
                            adjustValueOfPosCache.Add(adjustValueOfPos);
                            while (adjustValueOfPosCache.Count > pm.AdjustFrequency) adjustValueOfPosCache.RemoveAt(0);
                            if (adjustValueOfPosCache.Count == pm.AdjustFrequency)
                            {
                                adjustValueOfPos = (int)adjustValueOfPosCache.Average();
                                adjustValueOfPosCache.Clear();
                                // 是否发送偏差值
                                if (pm.ClosedLoopEnablePos)
                                {
                                    cm.IsAdjustOfPos = true;
                                    cm.IssuedValueOfPos = adjustValueOfPos;
                                    await WritePLCAsync("正极纠偏值下发", cm.IssuedValueOfPos);
                                    var message = $"[{BusinessName}]正极纠偏下发值:[{adjustValueOfPos}]";
                                    _logger.Info(message);
                                    cm.EndMessage += message;
                                }
                                else
                                {
                                    adjustValueOfPosCache.Clear();
                                    var message = $"[{BusinessName}]正极推荐下发值:[{adjustValueOfPos}]";
                                    _logger.Warn(message);
                                    cm.EndMessage += message;
                                }
                            }
                            #endregion

                            #region 负极下发流程
                            var l1 = cm.TargetTotalWidthOfNeg;
                            // 是否开启负极总宽尺寸管控
                            if (pm.CpControlEnableNeg)
                            {
                                // 1,超上限发送负极总宽上限值
                                // 2,超下限发送负极总宽下限值
                                // 3,工艺范围内直接发送负极总宽尺寸
                                if (cm.TargetTotalWidthOfNeg > pm.PoleWidthOfNeg_UpperLimit)
                                {
                                    l1 = pm.PoleWidthOfNeg_UpperLimit;
                                }
                                else if (cm.TargetTotalWidthOfNeg < pm.PoleWidthOfNeg_LowerLimit)
                                {
                                    l1 = pm.PoleWidthOfNeg_LowerLimit;
                                }
                                else
                                {
                                    l1 = cm.TargetTotalWidthOfNeg;
                                }
                            }

                            // 比对选定的尺寸L1&CCD数据L2总宽度，偏差值△L=L1-L2
                            cm.DeltaL = l1 - gatherData.ModuleWidthOfNeg;
                            // 参数限幅
                            var adjustOffsetOfNeg = cm.DeltaL;
                            if (Math.Abs(cm.DeltaL) < pm.MinAdjustmentLimitNeg)
                            {
                                adjustOffsetOfNeg = 0;
                            }
                            if (Math.Abs(cm.DeltaL) > pm.MaxAdjustmentLimitNeg)
                            {
                                adjustOffsetOfNeg = cm.DeltaL > 0 ? pm.MaxAdjustmentLimitNeg : -pm.MaxAdjustmentLimitNeg;
                            }

                            // 计算负极Y+K*（△L\0.4\-0.4）
                            var adjustValueOfNeg = (int)(gatherData.OriginYOfNeg + pm.AdjustCorrectionOfNeg * adjustOffsetOfNeg);
                            adjustValueOfNegCache.Add(adjustValueOfNeg);
                            // 正极发送值后经过n个电芯
                            var intervalCount = pm.AdjustFrequency;
                            while (adjustValueOfNegCache.Count > intervalCount) adjustValueOfNegCache.RemoveAt(0);
                            if (adjustValueOfNegCache.Count == intervalCount)
                            {
                                adjustValueOfNeg = (int)adjustValueOfNegCache.Take(pm.AdjustFrequency).Average();
                                adjustValueOfNegCache.RemoveRange(0, pm.AdjustFrequency);
                                // 是否发送偏差值
                                if (pm.ClosedLoopEnableNeg)
                                {
                                    // 若正极闭环未启用，且负极闭环启用，则当负极达到下发条件时，才发送负极调整值
                                    cm.IsAdjustOfNeg = true;
                                    cm.IssuedValueOfNeg = adjustValueOfNeg;
                                    await WritePLCAsync("负极纠偏值下发", cm.IssuedValueOfNeg);
                                    var message = $"[{BusinessName}]负极纠偏下发值:[{adjustValueOfNeg}]";
                                    _logger.Info(message);
                                    cm.EndMessage += message;
                                }
                                else
                                {
                                    adjustValueOfNegCache.Clear();
                                    var message = $"[{BusinessName}]负极推荐下发值:[{adjustValueOfNeg}]";
                                    _logger.Warn(message);
                                    cm.EndMessage += message;
                                }
                            }
                            #endregion
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

        /// <summary>
        /// CSV文件：闭环结果保存
        /// </summary>
        /// <param name="plcData"></param>
        /// <param name="ccdData"></param>
        /// <param name="calculateData"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(PWBusinessGatherModel gatherData, PWBusinessCalculateModel calculateData, PWBusinessParamModel paramModel)
        {
            var dic = new Dictionary<string, string>();
            dic.Add("采集时间", gatherData.GatherTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            dic.Add("虚拟码 ", gatherData.VirtualCode ?? string.Empty);
            dic.Add("正极A面CCD模宽数据", gatherData.ModuleWidthOfPosA.ToString());
            dic.Add("正极B面CCD模宽数据", gatherData.ModuleWidthOfPosB.ToString());
            dic.Add("正极CCD模宽数据", gatherData.ModuleWidthOfPos.ToString());
            dic.Add("负极CCD模宽数据", gatherData.ModuleWidthOfNeg.ToString());
            dic.Add("正极飞行原点Y", gatherData.OriginYOfPos.ToString());
            dic.Add("负极飞行原点Y", gatherData.OriginYOfNeg.ToString());

            dic.Add("闭环计算时间", calculateData.CalculateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            dic.Add("正极是否调机", calculateData.IsAdjustOfPos.ToString());
            dic.Add("正极总宽目标值", calculateData.TargetTotalWidthOfPos.ToString());
            dic.Add("正极宽度偏差值", calculateData.DeltaW.ToString());
            dic.Add("正极调机下发值", calculateData.IssuedValueOfPos.ToString());
            dic.Add("负极是否调机", calculateData.IsAdjustOfNeg.ToString());
            dic.Add("负极总宽目标值", calculateData.TargetTotalWidthOfNeg.ToString());
            dic.Add("负极宽度偏差值", calculateData.DeltaL.ToString());
            dic.Add("负极调机下发值", calculateData.IssuedValueOfNeg.ToString());
            dic.Add("闭环结果信息", calculateData.EndMessage ?? string.Empty);

            return dic;
        }
        #endregion
        #endregion
    }
}
