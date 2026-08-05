using Dm.util;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HslCommunication;
using Newtonsoft.Json;
using Python.Runtime;
using SUNWODA_SEVB.Core.Attributes.Project.JRCW;
using SUNWODA_SEVB.Core.Enumerations.Project.JRCW;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.JECW;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.JRCW.ArithmeticModels;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Business1Models;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Core;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.JRCWHelper;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.ViewModels.Dialogs.JRCW;
using SUNWODA_SEVB.ViewModels.Pages.JRCW;
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

namespace SUNWODA_SEVB.Services.Project.JRCW.Business
{
    public class YWC05BusinessService : BusinessServiceBase<YWC05BusinessService>
    {
        private readonly ILoggerService<YWC05BusinessService> _logger;
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
        public override EBusinessType BusinessType => EBusinessType.BusinessType1;

        /// <summary>
        /// 构造函数
        /// </summary>
        public YWC05BusinessService(
            ILoggerService<YWC05BusinessService> logger,
            IPLCService plcService,
            IGlobalSettingRepository globalSettingRepository,
            IUsersRepository usersRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        ) : base(logger, plcService)
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
            paramSettingModel = _projectSettingRepository.LoadBusiness1ParamProjectParameters();
            // 初始化项目设置
            fileSaveRootPath = _projectSettingRepository.LoadFileSaveRootPathProjectParameter();
            // 初始化各个任务
            InitializePLCSigalListenerMonitor();

            // 开机时开关检测
            var checkSwitchTask = CheckOfClosedLoopSwitchAsync(ct);

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

            // 心跳监控
            var heartBeatTask = PLCHeartMonitorLoopAsync(ct);

            // 信号监听
            var signal1Task = PLCSigalListenerMonitorLoopAsync(plcDataListener, ct);
            var signal2Task = PLCSigalListenerMonitorLoopAsync(ccdDataListener, ct);
            var signal3Task = PLCSigalListenerMonitorLoopAsync(yieldListener, ct);

            // 闭环计算
            var calculateTask = CalculateLoopAsync(ct);

            await Task.WhenAny(heartBeatTask, signal1Task, signal2Task, signal3Task, calculateTask);
        }

        /// <summary>
        /// 清理
        /// </summary>
        /// <returns></returns>
        protected override async Task CleanupAsync()
        {
            _logger.Info($"清理业务 {BusinessType} 资源");

            // 清理资源
            gatherPLCDataModels?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();

            await Task.CompletedTask;
        }

        #region 闭环逻辑开发
        #region 闭环全局参数
        /// <summary>
        /// 文件保存根目录
        /// </summary>
        private string fileSaveRootPath = string.Empty;

        /// <summary>
        /// 配置参数
        /// </summary>
        private Business1ParamModel paramSettingModel = new();

        /// <summary>
        /// PLC采集数据缓存
        /// </summary>
        private BlockingCollection<Business1PLCDataModel> gatherPLCDataModels = new();

        /// <summary>
        /// CCD采集数据缓存
        /// </summary>
        private List<Business1CCDDataModel> gatherCCDDataModels = new();
        #endregion

        #region 开机检测闭环开关
        /// <summary>
        /// 开机检测闭环开关
        /// </summary>
        private async Task CheckOfClosedLoopSwitchAsync(CancellationToken ct)
        {
            var task = Task.Run(async () =>
            {
                await Task.Delay(50000);
                var value = GetPLCAddressValue("PLC闭环开启信号");
                if (value == "1")
                {
                    _logger.Info("开机检测:卷绕极耳错位闭环PLC开关已开启！");
                    return;
                }

                var messageText = $"开机检测:卷绕极耳错位闭环PLC开关[{value}]未开启，请人工确认！";
                await MessageBoxShowDialog(messageText, "警告");
            });
            await task.WaitAsync(ct);
        }
        #endregion

        #region 心跳
        /// <summary>
        /// 初始化PLC心跳监控
        /// 备注：每秒写入1，PLC清零，5秒内无变化PLC端报警
        /// </summary>
        private async Task PLCHeartMonitorLoopAsync(CancellationToken ct)
        {
            var task = Task.Run(async () =>
            {
                var parameterName = "闭环心跳信号";
                var plcAddress = GetPLCAddressModel(parameterName);
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        if (plcAddress == null) plcAddress = GetPLCAddressModel(parameterName);
                        if (plcAddress == null) throw new Exception($"未查询到[{parameterName}]PLC地址配置！");

                        var value = GetPLCAddressValue("闭环心跳信号");
                        var re = await WritePLCAsync(plcAddress.Address ?? string.Empty, plcAddress.Type ?? string.Empty, value == "0" ? (short)1 : (short)0);
                        if (!re.IsSuccess) throw new Exception($"PLC心跳信号[{parameterName}]写入异常！原因：{re.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"PLC心跳监听线程异常[60s后重试]", ex);
                        await Task.Delay(60000);
                    }
                }
            });
            await task.WaitAsync(ct);
        }
        #endregion

        #region 信号监听/数据采集
        private ValueListener? plcDataListener, ccdDataListener, yieldListener;

        /// <summary>
        /// 初始化PLC信号监听委托
        /// </summary>
        private void InitializePLCSigalListenerMonitor()
        {
            plcDataListener = new ValueListener() { Name = "客户端采集电芯数据信号", Value = "0" };
            plcDataListener.OnValueChangedEven -= OnPLCDataListenerValueChangedAsync;
            plcDataListener.OnValueChangedEven += OnPLCDataListenerValueChangedAsync;
            ccdDataListener = new ValueListener() { Name = "电芯扫码/ccd扫描信号", Value = "0" };
            ccdDataListener.OnValueChangedEven -= OnCCDDataListenerValueChangedAsync;
            ccdDataListener.OnValueChangedEven += OnCCDDataListenerValueChangedAsync;
            yieldListener = new ValueListener() { Name = "6h良率80警告", Value = "0" };
            yieldListener.OnValueChangedEven -= OnYieldListenerValueChangedAsync;
            yieldListener.OnValueChangedEven += OnYieldListenerValueChangedAsync;
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
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        listener.Value = GetPLCAddressValue(listener.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("PLC信号监听线程异常", ex);
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
        private async void OnPLCDataListenerValueChangedAsync(object? sender, EventArgs<string> args)
        {
            if (!(sender is ValueListener listener)) return;
            if (listener.Name != "客户端采集电芯数据信号") return;
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
                short valueShort;
                float valueFloat;
                var plcDataModel = new Business1PLCDataModel();
                plcDataModel.ZJData_XNM = GetPLCAddressValue("卷针处虚拟码");
                plcDataModel.ZJData_JZH = short.TryParse(GetPLCAddressValue("卷针号"), out valueShort) ? valueShort : (short)0;
                plcDataModel.MalpositionValueOfPositive = float.TryParse(GetPLCAddressValue("正极错位量"), out valueFloat) ? valueFloat : 0f;
                plcDataModel.MalpositionValueOfNegative = float.TryParse(GetPLCAddressValue("负极错位量"), out valueFloat) ? valueFloat : 0f;
                for (int i = 0; i < 35; i++)
                {
                    var value = float.TryParse(GetPLCAddressValue($"正极第{i + 1}个极耳错位偏差量"), out valueFloat) ? valueFloat : 0f;
                    plcDataModel.MalpositionDeviationValueOfPositiveBatteryTab.Add(value);
                }
                for (int i = 0; i < 36; i++)
                {
                    var value = float.TryParse(GetPLCAddressValue($"负极第{i + 1}个极耳错位偏差量"), out valueFloat) ? valueFloat : 0f;
                    plcDataModel.MalpositionDeviationValueOfNegativeBatteryTab.Add(value);
                }
                plcDataModel.SV_Pos1 = float.TryParse(GetPLCAddressValue("卷针1变径位置"), out valueFloat) ? valueFloat : 0f;
                plcDataModel.SV_Pos2 = float.TryParse(GetPLCAddressValue("卷针2变径位置"), out valueFloat) ? valueFloat : 0f;
                plcDataModel.StampingAirPressure = float.TryParse(GetPLCAddressValue("实时压花气压"), out valueFloat) ? valueFloat : 0f;

                plcDataModel.T1 = paramSettingModel.T1;
                plcDataModel.T2 = paramSettingModel.T2;
                plcDataModel.T3 = paramSettingModel.T3;
                plcDataModel.T4 = paramSettingModel.T4;
                plcDataModel.K = paramSettingModel.K;

                // 缓存
                gatherPLCDataModels.Add(plcDataModel);

                // 保存为CSV
                var savePath = Path.Combine(fileSaveRootPath, $"卷绕错位PLC数据\\{DateTime.Now:yyyyMMdd}");
                var dic = new Dictionary<string, string>();
                ToDictionalByDataColumn(plcDataModel, ref dic);
                var dt = JRCWCsvHelper.DicToTable(dic);
                lock (JRCWCsvHelper.WriteLocker) JRCWCsvHelper.WriteCSV(savePath, dt);
                return;
            }

            _logger.Error($"{listener.Name}读取数据信号为异常值[{args.NewValue}]");
        }

        /// <summary>
        /// CCD数据采集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnCCDDataListenerValueChangedAsync(object? sender, EventArgs<string> args)
        {
            if (!(sender is ValueListener listener)) return;
            if (listener.Name != "电芯扫码/ccd扫描信号") return;
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
                float valueFloat;
                var ccdDataModel = new Business1CCDDataModel();
                ccdDataModel.CCD_ScanCode = GetPLCAddressValue("扫码处虚拟码");
                ccdDataModel.CCD_Barcode = GetPLCAddressValue("Barcode码");
                ccdDataModel.FeedInletPositionOfPositive = float.TryParse(GetPLCAddressValue("入料位置-正极"), out valueFloat) ? valueFloat : 0f;
                ccdDataModel.FeedInletPositionOfNegative = float.TryParse(GetPLCAddressValue("入料位置-负极"), out valueFloat) ? valueFloat : 0f;
                ccdDataModel.FarDistanceOfPositiveBatteryTab = float.TryParse(GetPLCAddressValue("正极极耳远边距"), out valueFloat) ? valueFloat : 0f;
                ccdDataModel.NearDistanceOfPositiveBatteryTab = float.TryParse(GetPLCAddressValue("正极极耳近边距"), out valueFloat) ? valueFloat : 0f;
                ccdDataModel.FarDistanceOfNegativeBatteryTab = float.TryParse(GetPLCAddressValue("负极极耳远边距"), out valueFloat) ? valueFloat : 0f;
                ccdDataModel.NearDistanceOfNegativeBatteryTab = float.TryParse(GetPLCAddressValue("负极极耳近边距"), out valueFloat) ? valueFloat : 0f;

                // 缓存
                lock (gatherCCDDataModels)
                {
                    gatherCCDDataModels.Add(ccdDataModel);
                    if (gatherCCDDataModels.Count > 40) gatherCCDDataModels.RemoveAt(0);
                }

                // 保存为CSV
                var savePath = Path.Combine(fileSaveRootPath, $"卷绕错位CCD数据\\{DateTime.Now:yyyyMMdd}");
                var dic = new Dictionary<string, string>();
                ToDictionalByDataColumn(ccdDataModel, ref dic);
                var dt = JRCWCsvHelper.DicToTable(dic);
                lock (JRCWCsvHelper.WriteLocker) JRCWCsvHelper.WriteCSV(savePath, dt);
                return;
            }

            _logger.Error($"{listener.Name}读取数据信号为异常值[{args.NewValue}]");
        }

        /// <summary>
        /// 6h良率80警告 监控
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnYieldListenerValueChangedAsync(object? sender, EventArgs<string> args)
        {
            if (!(sender is ValueListener listener)) return;
            if (listener.Name != "6h良率80警告") return;
            if (args.OldValue == args.NewValue) return;

            if (args.NewValue == "0")
            {
                _logger.Info($"[{listener.Name}]读取数据信号复位0");
                return;
            }

            if (args.NewValue == "1")
            {
                _logger.Info($"[{listener.Name}]读取数据信号值为1");
                // -TODO    飞书推送
                await Task.Delay(200);
                return;
            }

            _logger.Error($"{listener.Name}读取数据信号为异常值[{args.NewValue}]");
        }
        #endregion

        #region 闭环计算
        private VM_JRCWMessageBoxDialog mb = new VM_JRCWMessageBoxDialog();
        /// <summary>
        /// 卷绕极耳错位闭环计算逻辑
        /// </summary>
        /// <returns></returns>
        private async Task CalculateLoopAsync(CancellationToken ct)
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    // 缓存
                    int jSingleCache = 0, overJNumCache = 0;
                    float j1Cache = 0, j2Cache = 0;
                    var calculateDatasCache = new List<(Business1CalculateModel, Business1PLCDataModel)>();
                    var ccdData = new Business1CCDDataModel();
                    foreach (var plcData in gatherPLCDataModels.GetConsumingEnumerable())
                    {
                        var calculateModel = new Business1CalculateModel();
                        calculateModel.JSingleCache = jSingleCache;
                        calculateModel.OverJNumCache = overJNumCache;
                        calculateModel.J1Cache = j1Cache;
                        calculateModel.J2Cache = j2Cache;
                        try
                        {
                            #region 1.接帯/换卷信号处理
                            calculateModel.Host_TapeSplicing = await CheckOfChangeOrRewindSignalAsync("接带信号");
                            calculateModel.Host_ReelAlternatioan = await CheckOfChangeOrRewindSignalAsync("换卷信号");
                            calculateModel.ChangeOfArithmetic = calculateModel.Host_ReelAlternatioan == 1 ? 2 : (calculateModel.Host_TapeSplicing == 1 ? 1 : 0);
                            if (calculateModel.ChangeOfArithmetic != 0) calculateDatasCache.Clear();
                            #endregion

                            #region 2.CCD数据匹配
                            ccdData = gatherCCDDataModels.FirstOrDefault(o => o.CCD_ScanCode == plcData.ZJData_XNM);
                            if (ccdData == null)
                            {
                                _logger.Error($"未查询到[{plcData.ZJData_XNM}]对应的CCD采集数据！");
                                ccdData = new Business1CCDDataModel();
                            }
                            else lock (gatherCCDDataModels) gatherCCDDataModels.Remove(ccdData);
                            calculateDatasCache.add((calculateModel, plcData));
                            // 闭环参数最多12条
                            if (calculateDatasCache.Count > 12) calculateDatasCache.RemoveAt(0);
                            calculateModel.PLCBorcode = plcData.ZJData_XNM;
                            calculateModel.JZH = plcData.ZJData_JZH;
                            #endregion

                            #region 3.参数加载:每次重新加载一下参数
                            LoadJRCWParamSettingModel();
                            #endregion

                            #region 4.正/负极错位量处理
                            var reOfMV = await CheckOfMalpositionValueAsync(plcData.MalpositionValueOfPositive, plcData.MalpositionValueOfNegative, paramSettingModel);
                            if (!reOfMV.result)
                            {
                                calculateModel.M = reOfMV.m;
                                calculateModel.EndMessage = reOfMV.message;
                                continue;
                            }
                            #endregion

                            #region 5.算法入参处理(错位分类算法&错位分解算法)
                            var mdValuesOfPLis = plcData.MalpositionDeviationValueOfPositiveBatteryTab;
                            var mdValuesOfPArr = mdValuesOfPLis.Take(35).Skip(2).Take(mdValuesOfPLis.Count - 4).ToArray();
                            var mdValuesOfNLis = plcData.MalpositionDeviationValueOfNegativeBatteryTab;
                            var mdValuesOfNArr = mdValuesOfNLis.Take(36).Skip(2).Take(mdValuesOfNLis.Count - 4).ToArray();
                            #endregion

                            #region 6.错位分类算法
                            var cOfM = ExcuteDisclass(mdValuesOfPArr, mdValuesOfNArr);
                            calculateModel.CategoryOfPositiveMalposition = cOfM.COfPM;
                            calculateModel.CategoryOfNegativeMalposition = cOfM.COfNM;
                            #endregion

                            #region 7.错位分解算法
                            // 两个编码是否存在非0值：若存在，则执行错位分解算法
                            if (!(cOfM.COfPM == 0 && cOfM.COfNM == 0))
                            {
                                var dOfEC = ExcuteErrorCompensation(mdValuesOfPArr, mdValuesOfNArr);
                                calculateModel.DeltaCOfPositive = dOfEC.DeltaCOfP;
                                calculateModel.DeltaTCOfPositive = dOfEC.DeltaTCOfP;
                                calculateModel.DeltaCOfNegative = dOfEC.DeltaCOfN;
                                calculateModel.DeltaTCOfNegative = dOfEC.DeltaTCOfN;
                            }
                            #endregion

                            #region 8.闭环算法
                            var data = GetDataOfMisJudge(calculateDatasCache.ToArray());
                            var t = paramSettingModel.K * (paramSettingModel.Na + paramSettingModel.Nb) / 2;
                            var reOfMJ = ExcuteMisJudge(data, paramSettingModel, calculateModel.ChangeOfArithmetic, t, jSingleCache, overJNumCache);
                            jSingleCache = reOfMJ.J_Signal;
                            overJNumCache = reOfMJ.Over_J_Num;
                            calculateModel.J1 = reOfMJ.J1;
                            calculateModel.J2 = reOfMJ.J2;
                            calculateModel.PressJ = reOfMJ.Press_J;
                            calculateModel.Notes = reOfMJ.Notes;
                            calculateModel.Dialog = reOfMJ.Dialog;
                            calculateModel.JSingle = reOfMJ.J_Signal;
                            calculateModel.OverJNum = reOfMJ.Over_J_Num;
                            // 清空数据表data
                            if (reOfMJ.J1 != 0 || reOfMJ.J2 != 0 || reOfMJ.Press_J != 0) calculateDatasCache.clear();
                            #endregion
                        }
                        catch (Exception ex)
                        {
                            calculateModel.EndMessage += ex.Message;
                            _logger.Error("卷绕极耳错位闭环计算异常！", ex);
                        }
                        finally
                        {
                            try
                            {
                                #region 9.调机下发  闭环执行异常，但有缓存值则依旧下发
                                var jOfAdjust = await JJPAdjustAsync(plcData.ZJData_JZH, calculateModel.J1, calculateModel.J2, calculateModel.PressJ, calculateModel.Dialog, j1Cache, j2Cache);
                                // 缓存
                                j1Cache = jOfAdjust.j1Cache;
                                j2Cache = jOfAdjust.j2Cache;
                                calculateModel.EndMessage = jOfAdjust.message;
                                #endregion
                            }
                            catch (Exception ex)
                            {
                                calculateModel.EndMessage += ex.Message;
                                _logger.Error("卷绕极耳错位闭环下发异常！", ex);
                            }

                            // 计算过程模型异步保存CSV
                            _ = Task.Run(() =>
                            {
                                var savePath = Path.Combine(fileSaveRootPath, $"卷绕错位CCD及闭环过程数据\\{DateTime.Now:yyyyMMdd}");
                                var dic = new Dictionary<string, string>();
                                ToDictionalByDataColumn(ccdData, ref dic);
                                ToDictionalByDataColumn(calculateModel, ref dic);
                                //var dics = ToDictionalsByDataColumn(ccdData, calculateModel);
                                var dt = JRCWCsvHelper.DicToTable(dic);
                                lock (JRCWCsvHelper.WriteLocker) JRCWCsvHelper.WriteCSV(savePath, dt);
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("卷绕极耳错位闭环异常！", ex);
                    throw;
                }
            });
            await task.WaitAsync(ct);
        }

        /// <summary>
        /// 接帯/换卷信号处理
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        private async Task<short> CheckOfChangeOrRewindSignalAsync(string parameterName)
        {
            var plcAddress = GetPLCAddressModel(parameterName);
            if (plcAddress == null)
            {
                _logger.Error($"未查询到[{parameterName}]PLC地址配置！");
                return 0;
            }
            var monitorValue = plcAddress.MonitorValue?.ToString();
            if (!short.TryParse(monitorValue, out short value))
            {
                _logger.Warn($"[{parameterName}]PLC地址数据读取解析异常[{monitorValue}]！");
                return 0;
            }
            if (value == 0) return 0;
            if (value == 1)
            {
                var re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, (short)0);
                if (re.IsSuccess) _logger.Info($"[{parameterName}]PLC地址复位完成！");
                else _logger.Error($"[{parameterName}]PLC地址复位失败:[{re.Message}]");
                return 1;
            }

            _logger.Warn($"[{parameterName}]PLC地址数据[{monitorValue}]不为0或1！");
            return value;
        }

        /// <summary>
        /// 正/负极错位量处理
        /// </summary>
        /// <param name="mvOfP"></param>
        /// <param name="mvOfN"></param>
        /// <param name="pm"></param>
        /// <returns></returns>
        private async Task<(bool result, double m, string message)> CheckOfMalpositionValueAsync(float mvOfP, float mvOfN, Business1ParamModel pm)
        {
            var msg = "Success";

            // 正/负极错位量绝对值>t4
            var mAbsOfP = Math.Abs(mvOfP);
            var mAbsOfN = Math.Abs(mvOfN);
            if (!(mAbsOfP > pm.T4 || mAbsOfN > pm.T4)) return (true, 0, msg);

            // 正/负极错位量绝对值>=200
            if (!(mAbsOfP < 200 || mAbsOfN < 200))
            {
                msg = $"正/负极错位量[{mvOfP},{mvOfN}]绝对值大于等于[200]且大于[{pm.T4}]!";
                return (false, 0, msg);
            }

            // 错位异常警告信号下发&弹窗
            var parameterName = "错位异常偏大警告信号";
            var plcAddress = GetPLCAddressModel(parameterName);
            var re = await WritePLCAsync(parameterName, plcAddress?.Type ?? string.Empty, (short)1);
            if (!re.IsSuccess) _logger.Error($"[{parameterName}]PLC地址写入失败:[{re.Message}]");

            // 推荐“变径调节量”，保留一位小数
            var m = Math.Round((mvOfP + mvOfN) / ((pm.Nb + pm.Na - 2) * pm.K * 10), 1);

            // 弹窗
            msg = $"错位异常偏大，推荐变径调整量：{m}，请结合实际情况进行调整！";
            _ = MessageBoxShowDialog(msg, "警告");

            return (false, m, msg);
        }

        /// <summary>
        /// 加载业务参数配置
        /// </summary>
        private void LoadJRCWParamSettingModel()
        {
            var value = _projectSettingRepository.LoadBusiness1ParamProjectParameters();
            if (value != null) paramSettingModel = value;
        }

        #region Python算法相关
        #region 错位分类算法
        /// <summary>
        /// 执行错位分类算法
        /// </summary>
        /// <param name="mdValuesOfP"></param>
        /// <param name="mdValuesOfN"></param>
        /// <returns></returns>
        private DisclassOutputModel ExcuteDisclass(float[] mdValuesOfP, float[] mdValuesOfN)
        {
            var pyResoult = new PyResultModel<DisclassInputModel, DisclassOutputModel>();
            pyResoult.PyInPut = new DisclassInputModel();
            pyResoult.PyInPut.MDValuesOfP = mdValuesOfP;
            pyResoult.PyInPut.MDValuesOfN = mdValuesOfN;
            var sw = new Stopwatch();
            sw.Start();
            using var mr = new ManualResetEvent(false);
            var args = new object[] { mr, pyResoult };
            PythonConfigHelper.AddAction((ExcutePython_Disclass, args));
            mr.WaitOne();
            sw.Stop();

            if (pyResoult.IsSuccess)
            {
                _logger.Info($"'错位分类算法'执行完毕[耗时:{sw.ElapsedMilliseconds}ms]");
                return pyResoult.Result ?? new DisclassOutputModel();
            }

            throw new Exception($"'错位分类算法'执行异常[耗时:{sw.ElapsedMilliseconds}ms]:[{pyResoult.Message}]\r\n,入参[{JsonConvert.SerializeObject(pyResoult.PyInPut)}]\r\n,出参[{pyResoult.PyOutPut}]");
        }

        /// <summary>
        /// 错位分类算法
        /// </summary>
        /// <param name="args"></param>
        private void ExcutePython_Disclass(object[] args)
        {
            if (args.Length != 2 ||
                !(args[0] is ManualResetEvent mr) ||
                !(args[1] is PyResultModel<DisclassInputModel, DisclassOutputModel> pyResoult))
            {
                _logger.Error($"'错位分类算法'参数解析异常[{JsonConvert.SerializeObject(args)}]");
                throw new ArgumentException($"参数异常[{JsonConvert.SerializeObject(args)}]", nameof(args));
            }
            try
            {
                var inputModel = pyResoult.PyInPut;
                if (inputModel == null) throw new ArgumentException($"参数异常[{JsonConvert.SerializeObject(inputModel)}]", nameof(inputModel));

                // 算法导入&执行
                using var gs = Py.GIL();
                dynamic pm = PyModule.Import("Disclass");
                dynamic po = pm.Disclass(inputModel.MDValuesOfP, inputModel.MDValuesOfN);
                dynamic re = po.disclass();

                // 算法结果解析
                var reTp = PyTuple.AsTuple(re);
                var pyRes = new DisclassOutputModel();
                pyRes.COfPM = (int)reTp[0];
                pyRes.COfNM = (int)reTp[1];

                // 返回算法结果
                pyResoult.IsSuccess = true;
                pyResoult.PyOutPut = re;
                pyResoult.Result = pyRes;
                pyResoult.Message = "Success";
            }
            catch (Exception ex)
            {
                pyResoult.IsSuccess = false;
                pyResoult.Message = ex.Message;
            }
            finally { mr.Set(); }
        }
        #endregion

        #region 错位分解算法
        /// <summary>
        /// 执行错位分解算法
        /// </summary>
        /// <param name="mdValuesOfP"></param>
        /// <param name="mdValuesOfN"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ErrorCompensationOutputModel ExcuteErrorCompensation(float[] mdValuesOfP, float[] mdValuesOfN)
        {
            var pyResoult = new PyResultModel<ErrorCompensationInputModel, ErrorCompensationOutputModel>();
            pyResoult.PyInPut = new ErrorCompensationInputModel();
            pyResoult.PyInPut.MDValuesOfP = mdValuesOfP;
            pyResoult.PyInPut.MDValuesOfN = mdValuesOfN;
            var sw = new Stopwatch();
            sw.Start();
            using var mr = new ManualResetEvent(false);
            var args = new object[] { mr, pyResoult };
            PythonConfigHelper.AddAction((ExcutePython_ErrorCompensation, args));
            mr.WaitOne();
            sw.Stop();

            if (pyResoult.IsSuccess)
            {
                _logger.Info($"'错位分解算法'执行完毕[耗时:{sw.ElapsedMilliseconds}ms]");
                return pyResoult.Result ?? new ErrorCompensationOutputModel();
            }

            throw new Exception($"'错位分解算法'执行异常[耗时:{sw.ElapsedMilliseconds}ms]:[{pyResoult.Message}]\r\n,入参[{JsonConvert.SerializeObject(pyResoult.PyInPut)}]\r\n,出参[{pyResoult.PyOutPut}]");
        }

        /// <summary>
        /// 错位分解算法
        /// </summary>
        /// <param name="args"></param>
        private void ExcutePython_ErrorCompensation(object[] args)
        {
            if (args.Length != 2 ||
              !(args[0] is ManualResetEvent mr) ||
              !(args[1] is PyResultModel<ErrorCompensationInputModel, ErrorCompensationOutputModel> pyResoult))
            {
                _logger.Error($"'错位分解算法'参数解析异常[{JsonConvert.SerializeObject(args)}]");
                throw new ArgumentException($"参数异常[{JsonConvert.SerializeObject(args)}]", nameof(args));
            }

            try
            {
                var inputModel = pyResoult.PyInPut;
                if (inputModel == null) throw new ArgumentException($"参数异常[{JsonConvert.SerializeObject(inputModel)}]", nameof(inputModel));

                // 算法导入&执行
                using var gs = Py.GIL();
                dynamic pm = PyModule.Import("Error_Compensation");
                dynamic po = pm.DiamagneticAnalyzer(inputModel.MDValuesOfP, inputModel.MDValuesOfN);
                dynamic re = po.analyze();

                // 算法结果解析
                var reTp = PyTuple.AsTuple(re);
                var pyRes = new ErrorCompensationOutputModel();
                pyRes.DeltaCOfP = (float)reTp[0];
                pyRes.DeltaTCOfP = (float)reTp[1];
                pyRes.DeltaCOfN = (float)reTp[2];
                pyRes.DeltaTCOfN = (float)reTp[3];

                // 返回算法结果
                pyResoult.IsSuccess = true;
                pyResoult.PyOutPut = re;
                pyResoult.Result = pyRes;
                pyResoult.Message = "Success";
            }
            catch (Exception ex)
            {
                pyResoult.IsSuccess = false;
                pyResoult.Message = ex.Message;
            }
            finally { mr.Set(); }
        }
        #endregion

        #region 闭环算法
        /// <summary>
        /// 执行闭环算法
        /// </summary>
        /// <param name="data"></param>
        /// <param name="pm"></param>
        /// <param name="change"></param>
        /// <param name="t"></param>
        /// <param name="j_single"></param>
        /// <param name="over_j_num"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private MisJudgeOutputModel ExcuteMisJudge(string data, Business1ParamModel pm, int change, double t, int j_single, int over_j_num)
        {
            var pyResoult = new PyResultModel<MisJudgeInputModel, MisJudgeOutputModel>();
            pyResoult.PyInPut = new MisJudgeInputModel();
            pyResoult.PyInPut.Data = data;
            pyResoult.PyInPut.P_Switch = pm.P_Switch;
            pyResoult.PyInPut.Change = change;
            pyResoult.PyInPut.T = t;
            pyResoult.PyInPut.P_t = pm.P_t;
            pyResoult.PyInPut.U_Limit = pm.U_Limit;
            pyResoult.PyInPut.J_d = pm.J_d;
            pyResoult.PyInPut.P_j_d = pm.P_j_d;
            pyResoult.PyInPut.J_Single = j_single;
            pyResoult.PyInPut.Over_J_Num = over_j_num;
            var sw = new Stopwatch();
            sw.Start();
            using var mr = new ManualResetEvent(false);
            var args = new object[] { mr, pyResoult };
            PythonConfigHelper.AddAction((ExcutePython_MisJudge, args));
            mr.WaitOne();
            sw.Stop();

            if (pyResoult.IsSuccess)
            {
                _logger.Info($"'闭环算法'执行完毕[耗时:{sw.ElapsedMilliseconds}ms]");
                return pyResoult.Result ?? new MisJudgeOutputModel();
            }

            throw new Exception($"'闭环算法'执行异常[耗时:{sw.ElapsedMilliseconds}ms]:[{pyResoult.Message}]\r\n,入参[{JsonConvert.SerializeObject(pyResoult.PyInPut)}]\r\n,出参[{pyResoult.PyOutPut}]");
        }

        /// <summary>
        /// 闭环算法
        /// </summary>
        /// <param name="args"></param>
        private void ExcutePython_MisJudge(object[] args)
        {
            if (args.Length != 2 ||
              !(args[0] is ManualResetEvent mr) ||
              !(args[1] is PyResultModel<MisJudgeInputModel, MisJudgeOutputModel> pyResoult))
            {
                _logger.Error($"'闭环算法'参数解析异常[{JsonConvert.SerializeObject(args)}]");
                throw new ArgumentException($"参数异常[{JsonConvert.SerializeObject(args)}]", nameof(args));
            }

            try
            {
                var inputModel = pyResoult.PyInPut;
                if (inputModel == null) throw new ArgumentException($"参数异常[{JsonConvert.SerializeObject(inputModel)}]", nameof(inputModel));

                // 算法导入&执行
                using var gs = Py.GIL();
                dynamic pm = PyModule.Import("mis_judge");
                dynamic po = pm.Judge(inputModel.Data, inputModel.P_Switch, inputModel.Change, inputModel.T, inputModel.P_t, inputModel.U_Limit, inputModel.J_d, inputModel.P_j_d, inputModel.J_Single, inputModel.Over_J_Num);
                dynamic re = po.mis_judge();

                // 算法结果解析
                var reTp = PyTuple.AsTuple(re);
                var pyRes = new MisJudgeOutputModel();
                pyRes.J1 = (float)reTp[0];
                pyRes.J2 = (float)reTp[1];
                pyRes.Press_J = (float)reTp[2];
                pyRes.Notes = (string)reTp[3];
                pyRes.Dialog = (string)reTp[4];
                pyRes.J_Signal = (int)reTp[5];
                pyRes.Over_J_Num = (int)reTp[6];

                // 返回算法结果
                pyResoult.IsSuccess = true;
                pyResoult.PyOutPut = re;
                pyResoult.Result = pyRes;
                pyResoult.Message = "Success";
            }
            catch (Exception ex)
            {
                pyResoult.IsSuccess = false;
                pyResoult.Message = ex.Message;
            }
            finally { mr.Set(); }
        }

        /// <summary>
        /// 获取闭环算法的参数data
        /// </summary>
        /// <param name="objs"></param>
        /// <returns></returns>
        public static string GetDataOfMisJudge(params (Business1CalculateModel cm, Business1PLCDataModel pd)[] objs)
        {
            if (objs == null || objs.Length <= 0) return string.Empty;

            var lis = new List<Dictionary<string, string>>();
            foreach (var obj in objs)
            {
                var dic = new Dictionary<string, string>();
                dic["正极错位分类类型"] = obj.cm.CategoryOfPositiveMalposition.ToString();
                dic["负极错位分类类型"] = obj.cm.CategoryOfNegativeMalposition.ToString();
                dic["正极卷针周长波动"] = obj.cm.DeltaCOfPositive.ToString();
                dic["正极极片厚度波动"] = obj.cm.DeltaTCOfPositive.ToString();
                dic["负极卷针周长波动"] = obj.cm.DeltaCOfNegative.ToString();
                dic["负极极片厚度波动"] = obj.cm.DeltaTCOfNegative.ToString();
                ToDictionalByDataColumn(obj.pd, ref dic);
                lis.Add(dic);
            }

            var headers = lis.SelectMany(d => d.Keys).Distinct().ToList();
            var sb = new StringBuilder();
            sb.Append(string.Join(',', headers));

            foreach (var dic in lis)
            {
                sb.Append(Environment.NewLine);
                foreach (var header in headers)
                {
                    var value = dic.ContainsKey(header) ? dic[header] : string.Empty;
                    sb.Append(value + ",");
                }
                sb.Remove(sb.Length - 1, 1);// 去除最后一个逗号
            }
            return sb.ToString();
        }
        #endregion
        #endregion

        /// <summary>
        /// 闭环算法调机值下发
        /// </summary>
        /// <param name="jzh"></param>
        /// <param name="j1"></param>
        /// <param name="j2"></param>
        /// <param name="press_j"></param>
        /// <param name="dialog"></param>
        /// <param name="j1Cache"></param>
        /// <param name="j2Cache"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private async Task<(float j1Cache, float j2Cache, string message)> JJPAdjustAsync(short jzh, float j1, float j2, float press_j, string? dialog, float j1Cache, float j2Cache)
        {
            if (jzh != 1 && jzh != 2) throw new ArgumentException($"PLC卷针号[{jzh}]信息异常:不为1或2!", nameof(jzh));

            string parameterName, message = "Success";
            var messageSB = new StringBuilder();
            PLCRWAddress? plcAddress;
            OperateResult re;
            // 算法输出下发值全为0
            if (!(j1 != 0 || j2 != 0 || press_j != 0))
            {
                if (jzh == 1 && j2Cache != 0)
                {
                    // 下发J2Cache
                    parameterName = "卷针2变径位置调整量";
                    plcAddress = GetPLCAddressModel(parameterName);
                    re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, j2Cache);

                    messageSB.AppendLine(re.IsSuccess ? $"Success(J2下发[{j2Cache}]成功)" : $"[{parameterName}]PLC地址写入[{j2Cache}]失败:[{re.Message}]");
                    message = messageSB.ToString();
                    if (!re.IsSuccess) _logger.Error(message);
                    return (j1Cache, 0, message);
                }
                if (jzh == 2 && j1Cache != 0)
                {
                    // 下发J1Cache
                    parameterName = "卷针1变径位置调整量";
                    plcAddress = GetPLCAddressModel(parameterName);
                    re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, j1Cache);

                    messageSB.AppendLine(re.IsSuccess ? $"Success(J1下发[{j1Cache}]成功)" : $"[{parameterName}]PLC地址写入[{j1Cache}]失败:[{re.Message}]");
                    message = messageSB.ToString();
                    if (!re.IsSuccess) _logger.Error(message);
                    return (0, j2Cache, message);
                }
                message = "Success(不执行下发操作)";
                return (j1Cache, j2Cache, message);
            }

            switch (jzh)
            {
                case 1: // 下发j2
                    {
                        j2 += j2Cache;
                        parameterName = "卷针2变径位置调整量";
                        plcAddress = GetPLCAddressModel(parameterName);
                        re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, j2);

                        messageSB.AppendLine(re.IsSuccess ? $"Success(J2下发[{j2}]成功)" : $"[{parameterName}]PLC地址写入[{j2}]失败:[{re.Message}]");
                        message = messageSB.ToString();
                        if (!re.IsSuccess) _logger.Error(message);
                        j1Cache += j1;
                        j2Cache = 0;
                        break;
                    }
                case 2: // 下发j1
                    {
                        j1 += j1Cache;
                        parameterName = "卷针1变径位置调整量";
                        plcAddress = GetPLCAddressModel(parameterName);
                        re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, j1);

                        messageSB.AppendLine(re.IsSuccess ? $"Success(J1下发[{j1}]成功)" : $"[{parameterName}]PLC地址写入[{j1}]失败:[{re.Message}]");
                        message = messageSB.ToString();
                        if (!re.IsSuccess) _logger.Error(message);
                        j1Cache = 0;
                        j2Cache += j2;
                        break;
                    }
                default:
                    {
                        messageSB.AppendLine($"false(卷针号[{jzh}]不为1或2，j1、j2不执行下发)");
                        break;
                    }
            }

            // 下发press_j
            if (press_j != 0)
            {
                parameterName = "压花气压调整量";
                plcAddress = GetPLCAddressModel(parameterName);
                re = await WritePLCAsync(plcAddress?.Address ?? string.Empty, plcAddress?.Type ?? string.Empty, press_j);
                if (!re.IsSuccess) _logger.Error($"[{parameterName}]PLC地址写入[{press_j}]失败:[{re.Message}]");

                messageSB.AppendLine(re.IsSuccess ? $"Success(PressJ下发[{press_j}]成功)" : $"[{parameterName}]PLC地址写入[{press_j}]失败:[{re.Message}]");
                message = messageSB.ToString();
                if (!re.IsSuccess) _logger.Error(message);
            }

            // 弹窗提示
            _logger.Info(dialog ?? string.Empty);
            var msg = dialog?.replace(";", ";\r\n");
            //_ = MessageBoxShowDialog(msg, "调节提示");
            Growl.InfoGlobal(msg);
            //MessageBox.Show(msg, "调节提示");

            return (j1Cache, j2Cache, message);
        }
        #endregion
        #endregion
    }
}
