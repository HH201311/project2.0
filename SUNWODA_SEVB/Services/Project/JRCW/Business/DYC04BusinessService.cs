using Dm.util;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HslCommunication;
using Newtonsoft.Json;
using Python.Runtime;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.JRCW;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.JRCW.ArithmeticModels;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Business2Models;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Core;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.JRCWHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.JRCW;
using SUNWODA_SEVB.ViewModels.Dialogs.JRCW.BusinessDialogs;
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
using System.Xml.Linq;
using TouchSocket.Core;

namespace SUNWODA_SEVB.Services.Project.JRCW.Business
{
    public class DYC04BusinessService : BusinessServiceBase<DYC04BusinessService>
    {
        private readonly ILoggerService<DYC04BusinessService> _logger;
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
        public override EBusinessType BusinessType => EBusinessType.BusinessType2;

        /// <summary>
        /// 构造函数
        /// </summary>
        public DYC04BusinessService(
            ILoggerService<DYC04BusinessService> logger,
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

            // 用户角色更新时触发
            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
            UserStateChangedEvent.OnUserStateChanged += OnUserStateChanged;
        }

        /// <summary>
        /// 初始化业务
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        protected override async Task InitializeAsync(CancellationToken ct)
        {
            _logger.Info($"初始化业务 {BusinessType} - {BusinessName}");

            // 初始化python代码
            await InitializePythonCodeAsync();

            // 初始化项目设置
            var rootPath = _projectSettingRepository.LoadFileSaveRootPathProjectParameter();
            fileSaveRootPath = Path.Combine(rootPath, BusinessType.ToString());
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

            // 心跳/数采触发极耳层数 监控
            var heartBeatTask = PLCHeartMonitorLoopAsync(ct);
            var tabCountTask = TabCountMonitorLoppAsync(ct);

            // 信号监听
            var signal1Task = PLCSigalListenerMonitorLoopAsync(plcDataListener, ct);
            var signal2Task = PLCSigalListenerMonitorLoopAsync(ccdDataListener, ct);

            // 闭环计算
            var calculateTask = CalculateLoopAsync(ct);

            await Task.WhenAny(heartBeatTask, tabCountTask, signal1Task, signal2Task, calculateTask);
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
            await ShowBusinessConfigDialogAsync<Business2ParamModel>(ShowBusinessConfigDialog);
        }

        #region 参数配置
        private UserRole userRole;

        /// <summary>
        /// 当前的锅炉账号权限
        /// </summary>
        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        /// <summary>
        /// 登录用户
        /// </summary>
        /// <param name="userAccount"></param>
        private void OnUserStateChanged(string userAccount)
        {
            var roleId = _usersRepository.GetByUserAccount(userAccount)?.RoleId ?? 0;

            UserRole = (UserRole)roleId;
        }

        /// <summary>
        /// 显示业务类型2的配置对话框
        /// </summary>
        /// <typeparam name="TConfig"></typeparam>
        /// <param name="config"></param>
        /// <returns></returns>
        private async Task<bool> ShowBusinessConfigDialog<TConfig>(TConfig config) where TConfig : class
        {
            if (!(config is Business2ParamModel model)) return false;
            var result = await Dialog.Show(new VM_Business2ParamDialog().UIElement)
                    .Initialize<VM_Business2ParamDialog>(vm =>
                    {
                        vm.UserRole = UserRole;
                        vm.ItemSource = model;
                        vm.VerifyBatteryCodeCommand = new RelayCommand(async (arg) =>
                        {
                            var batteryCode = vm.VerifyBatteryCode;
                            if (string.IsNullOrEmpty(batteryCode))
                            {
                                MessageBox.Warning($"请输入电芯条码！", "提示");
                                return;
                            }
                            var validResult = GetPerfectAngleValues(batteryCode, model.S_Nb1, model.S_Nbn, model.S_Na1, model.S_Nan);
                            if (!validResult.isSuccess)
                            {
                                MessageBox.Error($"电芯条码[{batteryCode}]获取完美电芯角度失败！请更换完美电芯条码重新尝试！", "错误");
                                return;
                            }
                            if (!int.TryParse(validResult.jzh, out var jzh)) throw new NotImplementedException($"不支持的电芯卷针号: {validResult.jzh}");
                            var perfectBattery = jzh switch
                            {
                                1 => model.PerfectBattery1,
                                2 => model.PerfectBattery2,
                                3 => model.PerfectBattery3,
                                _ => throw new NotImplementedException($"不支持的电芯卷针号: {validResult.jzh}")
                            };
                            vm.SelectedTabIndex = jzh - 1;
                            perfectBattery.BatteryCode = batteryCode;
                            perfectBattery.P_θb1 = validResult.angleOfPF;
                            perfectBattery.P_θbn = validResult.angleOfPL;
                            perfectBattery.P_θa1 = validResult.angleOfNF;
                            perfectBattery.P_θan = validResult.angleOfNL;
                            perfectBattery.B120 = 0;
                            perfectBattery.B121 = 0;
                            MessageBox.Success($"电芯条码[{batteryCode}]获取完美电芯角度成功！", "提示");
                        });
                    }).GetResultAsync<bool>();
            return result;
        }

        /// <summary>
        /// 获取完美电芯角度值
        /// </summary>
        /// <param name="batteryCode"></param>
        /// <param name="indexOfPF"></param>
        /// <param name="indexOfPL"></param>
        /// <param name="indexOfNF"></param>
        /// <param name="indexOfNL"></param>
        /// <returns></returns>
        private (bool isSuccess, string jzh, float angleOfPF, float angleOfPL, float angleOfNF, float angleOfNL) GetPerfectAngleValues(string batteryCode, int indexOfPF, int indexOfPL, int indexOfNF, int indexOfNL)
        {
            try
            {
                var savePath = Path.Combine(fileSaveRootPath, "过程数据", DateTime.Now.ToString("yyyyMMdd"));
                var key1 = $"卷针号";
                var key2 = $"正极第{indexOfPF}个极耳角度";
                var key3 = $"正极第{indexOfPL}个极耳角度";
                var key4 = $"负极第{indexOfNF}个极耳角度";
                var key5 = $"负极第{indexOfNL}个极耳角度";

                List<Dictionary<string, string>> dics;
                lock (JRCWCsvHelper.WriteLocker)
                {
                    dics = JRCWCsvHelper.SearchCsv(savePath, "Barcode", batteryCode, key1, key2, key3, key4, key5);
                }

                // 安全解析，转换失败时使用默认值
                var dic = dics.LastOrDefault();
                var jzh = dic?.ContainsKey(key1) ?? false ? dic[key1] : string.Empty;
                var anglePF = TryParseAngle(dic, key2);
                var anglePL = TryParseAngle(dic, key3);
                var angleNF = TryParseAngle(dic, key4);
                var angleNL = TryParseAngle(dic, key5);

                return (true, jzh, anglePF, anglePL, angleNF, angleNL);
            }
            catch (Exception ex)
            {
                _logger.Error($"获取完美电芯角度值失败！电芯条码[{batteryCode}]，异常信息：{ex.Message}");
                return (false, string.Empty, 0, 0, 0, 0);
            }
        }

        /// <summary>
        /// 安全从字典中获取并解析角度值
        /// </summary>
        private float TryParseAngle(Dictionary<string, string>? dic, string key)
        {
            if (dic != null && dic.TryGetValue(key, out var value) && float.TryParse(value, out var result))
            {
                return result;
            }

            return 0f;
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
        private BlockingCollection<Business2PLCDataModel> gatherPLCDataModels = new();

        /// <summary>
        /// 计算过程数据缓存
        /// </summary>
        private List<(Business2CalculateModel calculateData, Business2PLCDataModel plcData)> calculateDatasCache = new();
        #endregion

        #region 开机检测闭环开关
        /// <summary>
        /// 开机检测闭环开关
        /// </summary>
        private async Task CheckOfClosedLoopSwitchAsync(CancellationToken ct)
        {
            var task = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(50000, ct);
                        var isSuccess = _plcService.ConnectionStatus.TryGetValue(JRCWCore.PLCName, out var plcStatus);
                        if (!isSuccess || plcStatus == null || !plcStatus.Status) throw new Exception($"PLC[{JRCWCore.PLCName}]未开启或连接异常！");

                        var value = GetPLCAddressValue("PLC闭环开启信号");
                        if (value == "1")
                        {
                            _logger.Info("开机检测:卷绕极耳错位闭环PLC开关已开启！");
                            return;
                        }
                        var messageText = $"开机检测:卷绕极耳错位闭环PLC开关[{value}]未开启，请人工确认！";
                        _logger.Warn(messageText);
                        await MessageBoxShowDialog(messageText, "警告");
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Info("开机检测:卷绕极耳错位闭环PLC开关检测任务已取消！");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"开机检测:卷绕极耳错位闭环PLC开关异常，60s后重试！", ex);
                        await Task.Delay(60000, ct);
                    }
                }
            });
            await task.WaitAsync(ct);
        }
        #endregion

        #region 心跳/数采触发极耳层数 监控
        /// <summary>
        /// PLC心跳监控任务
        /// 备注：每秒写入1，PLC清零，5秒内无变化PLC端报警
        /// </summary>
        private async Task PLCHeartMonitorLoopAsync(CancellationToken ct)
        {
            var task = Task.Run(async () =>
            {
                var parameterName = "闭环心跳信号";
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        var value = GetPLCAddressValue(parameterName);
                        var re = await WritePLCAsync(parameterName, value == "0" ? 1f : 0f);
                        if (!re.IsSuccess) throw new Exception($"PLC心跳信号[{parameterName}]写入异常！原因：{re.Message}");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Info("PLC心跳监听线程任务已取消！");
                        break;
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

        /// <summary>
        /// 数采触发极耳层数 监控任务
        /// 备注：人为输入的参数，后台每秒检测
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task TabCountMonitorLoppAsync(CancellationToken ct)
        {
            var task = Task.Run(async () =>
            {
                var parameterName = "数采触发极耳层数";
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        var valueStr = GetPLCAddressValue(parameterName);
                        if (!float.TryParse(valueStr, out float value)) throw new Exception($"数据[{valueStr}]转换为float失败！");

                        var paramModel = LoadParamSettingModel();
                        if (paramModel.TabCount != value)
                        {
                            var re = await WritePLCAsync(parameterName, (float)paramModel.TabCount);
                            if (!re.IsSuccess) throw new Exception($"数采触发极耳层数[{parameterName}]写入异常！原因：{re.Message}");
                            _logger.Info($"数采触发极耳层数 配置已更新[{value} -> {paramModel.TabCount}]");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Info("数采触发极耳层数 监听线程任务已取消！");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"数采触发极耳层数 监听线程异常[60s后重试]", ex);
                        await Task.Delay(60000);
                    }
                }
            });
            await task.WaitAsync(ct);
        }
        #endregion

        #region 信号监听/数据采集
        private ValueListener? plcDataListener, ccdDataListener;

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
                    catch (OperationCanceledException)
                    {
                        _logger.Info("PLC信号监听线程任务已取消！");
                        break;
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
                var paramModel = LoadParamSettingModel();
                float valueFloat;
                var plcDataModel = new Business2PLCDataModel();
                plcDataModel.GatherTime = DateTime.Now;
                plcDataModel.ZJData_XNM = GetPLCAddressValue("卷针处虚拟码");
                plcDataModel.ZJData_JZH = float.TryParse(GetPLCAddressValue("卷针号"), out valueFloat) ? valueFloat : 0f;
                plcDataModel.MVOfP = float.TryParse(GetPLCAddressValue("正极错位量"), out valueFloat) ? valueFloat : 0f;
                plcDataModel.MVOfN = float.TryParse(GetPLCAddressValue("负极错位量"), out valueFloat) ? valueFloat : 0f;
                plcDataModel.TabCount = float.TryParse(GetPLCAddressValue("数采触发极耳层数"), out valueFloat) ? valueFloat : 0f;
                for (int i = 0; i < 37; i++)
                {
                    var value = float.TryParse(GetPLCAddressValue($"正极第{i + 1}个极耳角度"), out valueFloat) ? valueFloat : 0f;
                    plcDataModel.AngleOfPBTab.Add(value);
                }
                for (int i = 0; i < 38; i++)
                {
                    var value = float.TryParse(GetPLCAddressValue($"负极第{i + 1}个极耳角度"), out valueFloat) ? valueFloat : 0f;
                    plcDataModel.AngleOfNBTab.Add(value);
                }
                plcDataModel.InPOfP = float.TryParse(GetPLCAddressValue("入料位置-正极"), out valueFloat) ? valueFloat : 0f;
                plcDataModel.InPOfN = float.TryParse(GetPLCAddressValue("入料位置-负极"), out valueFloat) ? valueFloat : 0f;
                plcDataModel.SV_Pos1 = float.TryParse(GetPLCAddressValue("卷针1变径位置"), out valueFloat) ? valueFloat : 0f;
                plcDataModel.SV_Pos2 = float.TryParse(GetPLCAddressValue("卷针2变径位置"), out valueFloat) ? valueFloat : 0f;
                plcDataModel.SV_Pos3 = float.TryParse(GetPLCAddressValue("卷针3变径位置"), out valueFloat) ? valueFloat : 0f;
                plcDataModel.StampingAirPressure = float.TryParse(GetPLCAddressValue("实时压花气压"), out valueFloat) ? valueFloat : 0f;

                // 缓存
                gatherPLCDataModels.Add(plcDataModel);
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
                var ccdData = new Business2CCDDataModel();
                ccdData.GatherTime = DateTime.Now;
                ccdData.CCD_ScanCode = GetPLCAddressValue("扫码处虚拟码");
                ccdData.CCD_Barcode = GetPLCAddressValue("Barcode码");
                ccdData.FarDistanceOfPositiveBatteryTab = float.TryParse(GetPLCAddressValue("正极极耳远边距"), out valueFloat) ? valueFloat : 0f;
                ccdData.NearDistanceOfPositiveBatteryTab = float.TryParse(GetPLCAddressValue("正极极耳近边距"), out valueFloat) ? valueFloat : 0f;
                ccdData.FarDistanceOfNegativeBatteryTab = float.TryParse(GetPLCAddressValue("负极极耳远边距"), out valueFloat) ? valueFloat : 0f;
                ccdData.NearDistanceOfNegativeBatteryTab = float.TryParse(GetPLCAddressValue("负极极耳近边距"), out valueFloat) ? valueFloat : 0f;

                #region CCD数据匹配
                var plcData = new Business2PLCDataModel();
                var calculateData = new Business2CalculateModel();
                lock (calculateDatasCache)
                {
                    var index = calculateDatasCache.FindIndex(o => o.plcData.ZJData_XNM == ccdData.CCD_ScanCode);
                    if (index == -1)
                    {
                        _logger.Warn($"未查询到CCD虚拟码[{ccdData.CCD_ScanCode}]对应的 过程数据！");
                    }
                    else
                    {
                        var existingItem = calculateDatasCache[index];
                        plcData = existingItem.plcData;
                        calculateData = existingItem.calculateData;
                        calculateDatasCache.RemoveAt(index);
                    }
                }
                #endregion

                var paramModel = LoadParamSettingModel();

                // 保存为CSV
                var savePath1 = Path.Combine(fileSaveRootPath, $"过程数据\\{DateTime.Now:yyyyMMdd}");
                var dic1 = ConvertToDictionary(plcData, ccdData, calculateData);
                var dt1 = JRCWCsvHelper.DicToTable(dic1);
                lock (JRCWCsvHelper.WriteLocker) JRCWCsvHelper.WriteCSV(savePath1, dt1);

                var savePath2 = Path.Combine(fileSaveRootPath, $"闭环纠偏数据\\{DateTime.Now:yyyyMMdd}");
                var dic2 = ConvertToDictionary(plcData, ccdData, calculateData, paramModel);
                var dt2 = JRCWCsvHelper.DicToTable(dic2);
                lock (JRCWCsvHelper.WriteLocker) JRCWCsvHelper.WriteCSV(savePath2, dt2);
                return;
            }

            _logger.Error($"{listener.Name}读取数据信号为异常值[{args.NewValue}]");
        }
        #endregion

        #region 闭环计算
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
                    Queue<float> queue_p = new(), queue_n = new(), queue1_p = new(), queue1_n = new(), queue2_p = new(), queue2_n = new(), queue3_p = new(), queue3_n = new();
                    foreach (var plcData in gatherPLCDataModels.GetConsumingEnumerable())
                    {
                        var pm = LoadParamSettingModel();
                        var cm = new Business2CalculateModel();
                        cm.CalculateTime = DateTime.Now;
                        cm.JSingleCache = jSingleCache;
                        cm.OverJNumCache = overJNumCache;
                        try
                        {
                            #region 1.接帯/换卷信号处理
                            cm.CLEnableSigle = float.TryParse(GetPLCAddressValue("PLC闭环开启信号"), out float valueFloat) ? valueFloat : 0f;
                            cm.Host_TapeSplicing = await CheckOfChangeOrRewindSignalAsync("接带信号");
                            cm.Host_ReelAlternatioan = await CheckOfChangeOrRewindSignalAsync("换卷信号");
                            if (cm.Host_TapeSplicing == 1) cm.ChangeOfArithmetic = 1;
                            if (cm.Host_ReelAlternatioan == 1) cm.ChangeOfArithmetic = 2;
                            if (cm.ChangeOfArithmetic != 0)
                            {
                                queue_p.Clear();
                                queue_n.Clear();
                                queue1_p.Clear();
                                queue1_n.Clear();
                                queue2_p.Clear();
                                queue2_n.Clear();
                                queue3_p.Clear();
                                queue3_n.Clear();
                            }
                            #endregion

                            #region 2.过程数据做缓存
                            calculateDatasCache.Add((cm, plcData));
                            // 闭环参数最多1条
                            lock (calculateDatasCache)
                            {
                                while (calculateDatasCache.Count > 200)
                                {
                                    var removeItem = calculateDatasCache.FirstOrDefault();
                                    calculateDatasCache.Remove(removeItem);
                                    _logger.Warn($"移除过期的 缓存数据[{JsonConvert.SerializeObject(removeItem)}]");
                                }
                            }
                            #endregion

                            #region 3.错位量计算参数筛选
                            var pb = plcData.ZJData_JZH switch
                            {
                                1 => pm.PerfectBattery1,
                                2 => pm.PerfectBattery2,
                                3 => pm.PerfectBattery3,
                                _ => throw new Exception($"卷针号为异常值 [{plcData.ZJData_JZH}]！")
                            };
                            cm.Θb1 = FindClosest(plcData.AngleOfPBTab, pb.P_θb1, pm.S_Nb1);
                            cm.Θbn = FindClosest(plcData.AngleOfPBTab, pb.P_θbn, pm.S_Nbn);
                            cm.Θa1 = FindClosest(plcData.AngleOfNBTab, pb.P_θa1, pm.S_Na1);
                            cm.Θan = FindClosest(plcData.AngleOfNBTab, pb.P_θan, pm.S_Nan);
                            #endregion

                            #region 4.正/负极错位量计算
                            cm.S_ΔDb = (cm.Θbn - pb.P_θbn) / 360 * pm.Cbn - (cm.Θb1 - pb.P_θb1) / 360 * pm.Cb1;
                            cm.S_ΔDa = (cm.Θan - pb.P_θan) / 360 * pm.Can - (cm.Θa1 - pb.P_θa1) / 360 * pm.Ca1;
                            cm.Bb = (pm.Nb - pm.S_Nbn + pm.S_Nb1 - 1) * cm.S_ΔDb / (pm.S_Nbn - pm.S_Nb1);
                            cm.Ba = (pm.Na - pm.S_Nan + pm.S_Na1 - 1) * cm.S_ΔDa / (pm.S_Nan - pm.S_Na1);
                            cm.ΔDb = cm.S_ΔDb + cm.Bb + pb.B120;
                            cm.ΔDa = cm.S_ΔDa + cm.Ba + pb.B121;
                            #endregion

                            #region 5.正/负极错位量处理
                            var reOfMV = await CheckOfMalpositionValueAsync(cm.ΔDb, cm.ΔDa, pm);
                            if (!reOfMV.result)
                            {
                                cm.M = reOfMV.m;
                                cm.EndMessage = reOfMV.message;
                                continue;
                            }
                            #endregion

                            #region 6.错位量缓存(最大长度为3及6，先进先出)
                            queue_p.Enqueue(cm.ΔDb);
                            while (queue_p.Count > 3) queue_p.Dequeue();
                            queue_n.Enqueue(cm.ΔDa);
                            while (queue_n.Count > 3) queue_n.Dequeue();
                            switch (plcData.ZJData_JZH)
                            {
                                case 1:
                                    queue1_p.Enqueue(cm.ΔDb);
                                    while (queue1_p.Count > 6) queue1_p.Dequeue();
                                    queue1_n.Enqueue(cm.ΔDa);
                                    while (queue1_n.Count > 6) queue1_n.Dequeue();
                                    break;
                                case 2:
                                    queue2_p.Enqueue(cm.ΔDb);
                                    while (queue2_p.Count > 6) queue2_p.Dequeue();
                                    queue2_n.Enqueue(cm.ΔDa);
                                    while (queue2_n.Count > 6) queue2_n.Dequeue();
                                    break;
                                case 3:
                                    queue3_p.Enqueue(cm.ΔDb);
                                    while (queue3_p.Count > 6) queue3_p.Dequeue();
                                    queue3_n.Enqueue(cm.ΔDa);
                                    while (queue3_n.Count > 6) queue3_n.Dequeue();
                                    break;
                                default: throw new Exception($"未实现卷针号[{plcData.ZJData_JZH}]逻辑！");
                            }
                            #endregion

                            #region 7.其他算法参数预处理&闭环算法
                            var t = pm.K * (pm.Na + pm.Nb) / 2;
                            var result = ExcuteJudge(queue_p.ToArray(), queue_n.ToArray(), queue1_p.ToArray(), queue1_n.ToArray(),
                                queue2_p.ToArray(), queue2_n.ToArray(), queue3_p.ToArray(), queue3_n.ToArray(),
                                pm, plcData.ZJData_JZH.ToString(), cm.ChangeOfArithmetic, t, jSingleCache, overJNumCache);
                            cm.J1 = result.J1;
                            cm.J2 = result.J2;
                            cm.J3 = result.J3;
                            cm.Notes = result.Notes;
                            cm.Dialog = result.Dialog;
                            cm.JSingle = result.J_Signal;
                            cm.OverJNum = result.Over_J_Num;
                            jSingleCache = result.J_Signal;
                            overJNumCache = result.Over_J_Num;
                            // 清空错位量缓存
                            if (result.J1 != 0 || result.J2 != 0 || result.J3 != 0)
                            {
                                queue_p.Clear();
                                queue_n.Clear();
                                queue1_p.Clear();
                                queue1_n.Clear();
                                queue2_p.Clear();
                                queue2_n.Clear();
                                queue3_p.Clear();
                                queue3_n.Clear();
                            }
                            #endregion

                            #region 8.调机下发  闭环执行异常，但有缓存值则依旧下发
                            var message = await JJJAdjustAsync(cm.J1, cm.J2, cm.J3, cm.Dialog);
                            cm.EndMessage = message;
                            #endregion
                        }
                        catch (Exception ex)
                        {
                            cm.EndMessage += ex.Message;
                            _logger.Error("卷绕极耳错位闭环计算异常！", ex);
                        }
                        finally
                        {
                            // 计算过程模型异步保存CSV
                            _ = Task.Run(() =>
                            {
                                var savePath = Path.Combine(fileSaveRootPath, $"缓存数据\\{DateTime.Now:yyyyMMdd}");
                                var dic = ConvertToDictionary(plcData, cm, pm);
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
        private async Task<float> CheckOfChangeOrRewindSignalAsync(string parameterName)
        {
            var plcAddress = GetPLCAddressModel(parameterName);
            if (plcAddress == null)
            {
                _logger.Error($"未查询到[{parameterName}]PLC地址配置！");
                return 0;
            }
            var monitorValue = plcAddress.MonitorValue?.ToString();
            if (!float.TryParse(monitorValue, out float value))
            {
                _logger.Warn($"[{parameterName}]PLC地址数据读取解析异常[{monitorValue}]！");
                return 0;
            }
            if (value == 0) return 0;
            if (value == 1)
            {
                var re = await WritePLCAsync(plcAddress?.Address, plcAddress?.Type, (float)0);
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
        private async Task<(bool result, float m, string message)> CheckOfMalpositionValueAsync(float mvOfP, float mvOfN, Business2ParamModel pm)
        {
            var msg = "Success";

            // 正极错位量下发
            var parameterName = "正极错位量";
            var re = await WritePLCAsync(parameterName, mvOfP);
            if (!re.IsSuccess) _logger.Error($"[{parameterName}:{mvOfP}] PLC地址写入失败:[{re.Message}]");

            // 负极错位量下发
            parameterName = "负极错位量";
            re = await WritePLCAsync(parameterName, mvOfN);
            if (!re.IsSuccess) _logger.Error($"[{parameterName}:{mvOfN}] PLC地址写入失败:[{re.Message}]");

            // 正/负极错位量绝对值>t4
            var mAbsOfP = Math.Abs(mvOfP);
            var mAbsOfN = Math.Abs(mvOfN);
            if (!(mAbsOfP > pm.T4 || mAbsOfN > pm.T4)) return (true, 0, msg);

            // 正极错位量绝对值>200 且 负极错位量绝对值<t4
            if (mAbsOfP > 200 && mAbsOfN < pm.T4) return (true, 0, msg);

            // 正/负极错位量绝对值>=200
            if (mAbsOfP > 200 && mAbsOfN > 200)
            {
                msg = $"正/负极错位量[{mvOfP},{mvOfN}]绝对值大于[200]!";
                return (false, 0, msg);
            }

            // 错位异常警告信号下发&弹窗
            parameterName = "错位异常偏大警告信号";
            re = await WritePLCAsync(parameterName, 1f);
            if (!re.IsSuccess) _logger.Error($"[{parameterName}]PLC地址写入失败:[{re.Message}]");

            // 推荐“变径调节量”，保留一位小数
            var m = Math.Round((mvOfP + mvOfN) / ((pm.Nb + pm.Na - 2) * pm.K * 10), 1);

            // 弹窗
            msg = $"错位异常偏大，推荐变径调整量：{m}，请结合实际情况进行调整！";
            _ = MessageBoxShowDialog(msg, "警告");

            return (false, (float)m, msg);
        }

        /// <summary>
        /// 加载业务参数配置
        /// </summary>
        private Business2ParamModel LoadParamSettingModel()
        {
            lock (this) return _projectSettingRepository.LoadBusiness2ParamProjectParameters();
        }

        /// <summary>
        /// CSV文件：过程数据
        /// </summary>
        /// <param name="plcData"></param>
        /// <param name="ccdData"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(Business2PLCDataModel plcData, Business2CCDDataModel ccdData, Business2CalculateModel calculateData)
        {
            var dic = new Dictionary<string, string>();
            dic.Add("Barcode ", ccdData.CCD_Barcode ?? string.Empty);
            dic.Add("PLC采集时间", plcData.GatherTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            dic.Add("数采触发极耳层数", plcData.TabCount.ToString());
            dic.Add("卷针号", plcData.ZJData_JZH.ToString());
            dic.Add("正极错位量", calculateData.ΔDb.ToString());
            dic.Add("负极错位量", calculateData.ΔDa.ToString());
            for (int i = 0; i < plcData.AngleOfPBTab.Count; i++)
            {
                dic.Add($"正极第{i + 1}个极耳角度", plcData.AngleOfPBTab[i].ToString());
            }
            for (int i = 0; i < plcData.AngleOfNBTab.Count; i++)
            {
                dic.Add($"负极第{i + 1}个极耳角度", plcData.AngleOfNBTab[i].ToString());
            }

            return dic;
        }

        /// <summary>
        /// CSV文件：闭环纠偏数据
        /// </summary>
        /// <param name="plcData"></param>
        /// <param name="ccdData"></param>
        /// <param name="calculateData"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(Business2PLCDataModel plcData, Business2CCDDataModel ccdData, Business2CalculateModel calculateData, Business2ParamModel paramModel)
        {
            var dic = new Dictionary<string, string>();
            dic.Add("CCD采集时间", ccdData.GatherTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            dic.Add("Barcode ", ccdData.CCD_Barcode ?? string.Empty);
            dic.Add("正极极耳远边距", ccdData.FarDistanceOfPositiveBatteryTab.ToString());
            dic.Add("正极极耳近边距", ccdData.NearDistanceOfPositiveBatteryTab.ToString());
            dic.Add("负极极耳远边距", ccdData.FarDistanceOfNegativeBatteryTab.ToString());
            dic.Add("负极极耳近边距", ccdData.NearDistanceOfNegativeBatteryTab.ToString());
            dic.Add("卷针1变径位置", plcData.SV_Pos1.ToString());
            dic.Add("卷针2变径位置", plcData.SV_Pos2.ToString());
            dic.Add("卷针3变径位置", plcData.SV_Pos3.ToString());
            dic.Add("实时压花气压", plcData.StampingAirPressure.ToString());
            dic.Add("闭环计算时间", calculateData.CalculateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            dic.Add("卷针号", plcData.ZJData_JZH.ToString());
            dic.Add("闭环开启信号", calculateData.CLEnableSigle.ToString());
            dic.Add("接带信号", calculateData.Host_TapeSplicing.ToString());
            dic.Add("换卷信号", calculateData.Host_ReelAlternatioan.ToString());
            dic.Add("算法参数change", calculateData.ChangeOfArithmetic.ToString());
            dic.Add("推荐变径调整量", calculateData.M.ToString());

            var perfectBattery = plcData.ZJData_JZH switch
            {
                1 => paramModel?.PerfectBattery1,
                2 => paramModel?.PerfectBattery2,
                3 => paramModel?.PerfectBattery3,
                _ => null
            };
            if (perfectBattery != null)
            {
                dic.Add("完美电芯错位量 - 正极", perfectBattery.B120.ToString());
                dic.Add("完美电芯错位量 - 负极", perfectBattery.B121.ToString());
                dic.Add("完美电芯正极极耳远边距", perfectBattery.P_θb1.ToString());
                dic.Add("完美电芯正极极耳近边距", perfectBattery.P_θbn.ToString());
                dic.Add("完美电芯负极极耳远边距", perfectBattery.P_θa1.ToString());
                dic.Add("完美电芯负极极耳近边距", perfectBattery.P_θan.ToString());
            }

            dic.Add("实时电芯的首极耳角度 - 正极", calculateData.Θb1.ToString());
            dic.Add("实时电芯的尾极耳角度 - 正极", calculateData.Θbn.ToString());
            dic.Add("实时电芯的首极耳角度 - 负极", calculateData.Θa1.ToString());
            dic.Add("实时电芯的尾极耳角度 - 负极", calculateData.Θan.ToString());
            dic.Add("稳定极耳错位 - 正极", calculateData.S_ΔDb.ToString());
            dic.Add("稳定极耳错位 - 负极", calculateData.S_ΔDa.ToString());
            dic.Add("缺失错位补偿 - 正极", calculateData.Bb.ToString());
            dic.Add("缺失错位补偿 - 负极", calculateData.Ba.ToString());
            dic.Add("正极错位量", calculateData.ΔDb.ToString());
            dic.Add("负极错位量", calculateData.ΔDa.ToString());
            dic.Add("JSingle缓存值", calculateData.JSingleCache.ToString());
            dic.Add("OverJNum缓存值", calculateData.OverJNumCache.ToString());
            dic.Add("J1", calculateData.J1.ToString());
            dic.Add("J2", calculateData.J2.ToString());
            dic.Add("J3", calculateData.J3.ToString());
            dic.Add("Notes", calculateData.Notes ?? string.Empty);
            dic.Add("Dialog", calculateData.Dialog ?? string.Empty);
            dic.Add("JSingle", calculateData.JSingle.ToString());
            dic.Add("OverJNum", calculateData.OverJNum.ToString());
            dic.Add("闭环结果信息", calculateData.EndMessage ?? string.Empty);

            return dic;
        }

        /// <summary>
        /// CSV文件：缓存数据
        /// </summary>
        /// <param name="plcData"></param>
        /// <param name="calculateData"></param>
        /// <param name="paramModel"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(Business2PLCDataModel plcData, Business2CalculateModel calculateData, Business2ParamModel paramModel)
        {
            var dic = new Dictionary<string, string>();
            dic.Add("PLC采集时间", plcData.GatherTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            dic.Add("数采触发极耳层数", plcData.TabCount.ToString());
            dic.Add("卷针处虚拟码", plcData.ZJData_XNM ?? string.Empty);
            dic.Add("卷针号", plcData.ZJData_JZH.ToString());
            dic.Add("接带信号", calculateData.Host_TapeSplicing.ToString());
            dic.Add("换卷信号", calculateData.Host_ReelAlternatioan.ToString());
            dic.Add("闭环开启信号", calculateData.CLEnableSigle.ToString());
            for (int i = 0; i < plcData.AngleOfPBTab.Count; i++)
            {
                dic.Add($"正极第{i + 1}个极耳角度", plcData.AngleOfPBTab[i].ToString());
            }
            for (int i = 0; i < plcData.AngleOfNBTab.Count; i++)
            {
                dic.Add($"负极第{i + 1}个极耳角度", plcData.AngleOfNBTab[i].ToString());
            }
            dic.Add("入料位置-正极", plcData.InPOfP.ToString());
            dic.Add("入料位置-负极", plcData.InPOfN.ToString());
            dic.Add("卷针1变径位置", plcData.SV_Pos1.ToString());
            dic.Add("卷针2变径位置", plcData.SV_Pos2.ToString());
            dic.Add("卷针3变径位置", plcData.SV_Pos3.ToString());
            dic.Add("实时压花气压", plcData.StampingAirPressure.ToString());
            dic.Add("t1", paramModel.T1.ToString());
            dic.Add("t2", paramModel.T2.ToString());
            dic.Add("t3", paramModel.T3.ToString());
            dic.Add("t4", paramModel.T4.ToString());
            dic.Add("k", paramModel.K.ToString());
            dic.Add("闭环计算时间", calculateData.CalculateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            dic.Add("算法参数change", calculateData.ChangeOfArithmetic.ToString());
            dic.Add("推荐变径调整量", calculateData.M.ToString());

            var perfectBattery = plcData.ZJData_JZH switch
            {
                1 => paramModel?.PerfectBattery1,
                2 => paramModel?.PerfectBattery2,
                3 => paramModel?.PerfectBattery3,
                _ => null
            };
            if (perfectBattery != null)
            {
                dic.Add("完美电芯错位量 - 正极", perfectBattery.B120.ToString());
                dic.Add("完美电芯错位量 - 负极", perfectBattery.B121.ToString());
                dic.Add("完美电芯正极极耳远边距", perfectBattery.P_θb1.ToString());
                dic.Add("完美电芯正极极耳近边距", perfectBattery.P_θbn.ToString());
                dic.Add("完美电芯负极极耳远边距", perfectBattery.P_θa1.ToString());
                dic.Add("完美电芯负极极耳近边距", perfectBattery.P_θan.ToString());
            }

            dic.Add("实时电芯的首极耳角度 - 正极", calculateData.Θb1.ToString());
            dic.Add("实时电芯的尾极耳角度 - 正极", calculateData.Θbn.ToString());
            dic.Add("实时电芯的首极耳角度 - 负极", calculateData.Θa1.ToString());
            dic.Add("实时电芯的尾极耳角度 - 负极", calculateData.Θan.ToString());
            dic.Add("稳定极耳错位 - 正极", calculateData.S_ΔDb.ToString());
            dic.Add("稳定极耳错位 - 负极", calculateData.S_ΔDa.ToString());
            dic.Add("缺失错位补偿 - 正极", calculateData.Bb.ToString());
            dic.Add("缺失错位补偿 - 负极", calculateData.Ba.ToString());
            dic.Add("正极错位量", calculateData.ΔDb.ToString());
            dic.Add("负极错位量", calculateData.ΔDa.ToString());
            dic.Add("JSingle缓存值", calculateData.JSingleCache.ToString());
            dic.Add("OverJNum缓存值", calculateData.OverJNumCache.ToString());
            dic.Add("J1", calculateData.J1.ToString());
            dic.Add("J2", calculateData.J2.ToString());
            dic.Add("J3", calculateData.J3.ToString());
            dic.Add("Notes", calculateData.Notes ?? string.Empty);
            dic.Add("Dialog", calculateData.Dialog ?? string.Empty);
            dic.Add("JSingle", calculateData.JSingle.ToString());
            dic.Add("OverJNum", calculateData.OverJNum.ToString());
            dic.Add("闭环结果信息", calculateData.EndMessage ?? string.Empty);

            return dic;
        }

        #region Python算法相关
        /// <summary>
        /// 初始化python代码
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task InitializePythonCodeAsync()
        {
            var pyCodes = _projectSettingRepository.LoadPyCodesProjectParamter();
            if (pyCodes == null || pyCodes.Count < 1) throw new Exception("未配置算法字段[PyCodes]！");

            var xmlPublicKey = RasHelper.ToXmlPublicKey(PublicKey);
            var signature = RasHelper.RSAEncrypt(xmlPublicKey, AppKey);
            var fileResp = await _webApiClient.DownloadDllAsync(AppKey, signature, pyCodes[0], "PRED");

            if (!fileResp.IsSuccess) throw new Exception($"算法[{pyCodes[0]}]文件加载失败[{fileResp.Code}:{fileResp.Message}]");

            if (fileResp.DataStr == null || fileResp.DataStr.Length == 0)
                throw new Exception($"算法[{pyCodes[0]}]文件加载失败！");

            pyCode_Judg = Encoding.UTF8.GetString(fileResp.DataStr);
            _logger.Info($"算法[{pyCodes[0]}]加载完成！");
        }

        #region 闭环算法
        private string pyCode_Judg = string.Empty;
        /// <summary>
        /// 执行闭环算法
        /// </summary>
        /// <param name="data_P"></param>
        /// <param name="data_N"></param>
        /// <param name="data1_P"></param>
        /// <param name="data1_N"></param>
        /// <param name="data2_P"></param>
        /// <param name="data2_N"></param>
        /// <param name="data3_P"></param>
        /// <param name="data3_N"></param>
        /// <param name="pm"></param>
        /// <param name="n"></param>
        /// <param name="change"></param>
        /// <param name="t"></param>
        /// <param name="j_signal"></param>
        /// <param name="over_j_num"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private JudgeOutputModel ExcuteJudge(float[] data_P, float[] data_N, float[] data1_P, float[] data1_N, float[] data2_P, float[] data2_N, float[] data3_P, float[] data3_N,
            Business2ParamModel pm, string n, int change, double t, int j_signal, int over_j_num)
        {
            var pyResoult = new PyResultModel<JudgeInputModel, JudgeOutputModel>();
            pyResoult.PyInPut = new JudgeInputModel();
            pyResoult.PyInPut.T1 = pm.T1;
            pyResoult.PyInPut.T2 = pm.T2;
            pyResoult.PyInPut.T3 = pm.T3;
            pyResoult.PyInPut.N = n;
            pyResoult.PyInPut.Queue_P = data_P;
            pyResoult.PyInPut.Queue_N = data_N;
            pyResoult.PyInPut.Queue1_P = data1_P;
            pyResoult.PyInPut.Queue1_N = data1_N;
            pyResoult.PyInPut.Queue2_P = data2_P;
            pyResoult.PyInPut.Queue2_N = data2_N;
            pyResoult.PyInPut.Queue3_P = data3_P;
            pyResoult.PyInPut.Queue3_N = data3_N;
            pyResoult.PyInPut.Change = change;
            pyResoult.PyInPut.T = t;
            pyResoult.PyInPut.U_Limit = pm.U_Limit;
            pyResoult.PyInPut.J_d = pm.J_d;
            pyResoult.PyInPut.J_Signal = j_signal;
            pyResoult.PyInPut.Over_J_Num = over_j_num;

            var sw = new Stopwatch();
            sw.Start();
            using var mr = new ManualResetEvent(false);
            var args = new object[] { mr, pyCode_Judg, pyResoult };
            PythonConfigHelper.AddAction((ExcutePython_Judge, args));
            mr.WaitOne();
            sw.Stop();

            // 调试使用，后续根据要求删除
            _logger.Info($"算法执行完毕(测试&调试阶段使用):[耗时:{sw.ElapsedMilliseconds}ms]:[{pyResoult.Message}]\r\n,入参[{JsonConvert.SerializeObject(pyResoult.PyInPut)}]\r\n,出参[{pyResoult.PyOutPut}]");

            if (pyResoult.IsSuccess)
            {
                _logger.Info($"'闭环算法'执行完毕[耗时:{sw.ElapsedMilliseconds}ms]");
                return pyResoult.Result ?? new JudgeOutputModel();
            }

            throw new Exception($"'闭环算法'执行异常[耗时:{sw.ElapsedMilliseconds}ms]:[{pyResoult.Message}]\r\n,入参[{JsonConvert.SerializeObject(pyResoult.PyInPut)}]\r\n,出参[{pyResoult.PyOutPut}]");
        }

        /// <summary>
        /// 闭环算法
        /// </summary>
        /// <param name="args"></param>
        private void ExcutePython_Judge(object[] args)
        {
            if (args.Length != 3 ||
              !(args[0] is ManualResetEvent mr) ||
              !(args[1] is string pyCode) ||
              !(args[2] is PyResultModel<JudgeInputModel, JudgeOutputModel> pyResoult))
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
                //dynamic pm = PyModule.Import("DY_C04_325AH_0");
                dynamic pm = PyModule.FromString("Judge", pyCode);
                dynamic po = pm.Judge(inputModel.T1, inputModel.T2, inputModel.T3, inputModel.N,
                    inputModel.Queue_P, inputModel.Queue_N, inputModel.Queue1_P, inputModel.Queue1_N,
                    inputModel.Queue2_P, inputModel.Queue2_N, inputModel.Queue3_P, inputModel.Queue3_N,
                    inputModel.Change, inputModel.T, inputModel.U_Limit, inputModel.J_d, inputModel.J_Signal, inputModel.Over_J_Num);
                dynamic re = po.mis_judge();

                // 算法结果解析
                var reTp = PyTuple.AsTuple(re);
                var pyRes = new JudgeOutputModel();
                pyRes.J1 = (float)reTp[0];
                pyRes.J2 = (float)reTp[1];
                pyRes.J3 = (float)reTp[2];
                pyRes.Notes = (string)reTp[3];
                pyRes.Dialog = (string)reTp[4];
                pyRes.J_Signal = (int)reTp[5];
                pyRes.Over_J_Num = (int)reTp[6];

                // 返回算法结果
                pyResoult.IsSuccess = true;
                pyResoult.PyOutPut = re.ToString();
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
        #endregion

        /// <summary>
        /// 闭环算法调机值下发
        /// </summary>
        /// <param name="j1"></param>
        /// <param name="j2"></param>
        /// <param name="j3"></param>
        /// <param name="dialog"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private async Task<string> JJJAdjustAsync(float j1, float j2, float j3, string? dialog)
        {
            var messageSB = new StringBuilder();
            // 下发J1
            if (j1 != 0)
            {
                var parameterName = "卷针1变径位置调整量";
                var re = await WritePLCAsync(parameterName, j1);

                var message = re.IsSuccess ? $"Success(J1下发[{j1}]成功)" : $"[{parameterName}]PLC地址写入[{j1}]失败:[{re.Message}]";
                messageSB.AppendLine(message);
                if (!re.IsSuccess) _logger.Error(message);
            }

            // 下发J2
            if (j2 != 0)
            {
                var parameterName = "卷针2变径位置调整量";
                var re = await WritePLCAsync(parameterName, j2);

                var message = re.IsSuccess ? $"Success(J2下发[{j2}]成功)" : $"[{parameterName}]PLC地址写入[{j2}]失败:[{re.Message}]";
                messageSB.AppendLine(message);
                if (!re.IsSuccess) _logger.Error(message);
            }

            // 下发J3
            if (j3 != 0)
            {
                var parameterName = "卷针3变径位置调整量";
                var re = await WritePLCAsync(parameterName, j3);

                var message = re.IsSuccess ? $"Success(J3下发[{j3}]成功)" : $"[{parameterName}]PLC地址写入[{j3}]失败:[{re.Message}]";
                messageSB.AppendLine(message);
                if (!re.IsSuccess) _logger.Error(message);
            }

            // 下发调节量信号
            if (j1 != 0 || j2 != 0 || j3 != 0)
            {
                var parameterName = "下发调节量信号";
                var re = await WritePLCAsync(parameterName, 1f);

                var message = re.IsSuccess ? $"Success(调节量信号下发[{1}]成功)" : $"[{parameterName}]PLC地址写入[{1}]失败:[{re.Message}]";
                messageSB.AppendLine(message);
                if (!re.IsSuccess) _logger.Error(message);
            }

            // 弹窗提示
            if (!string.IsNullOrEmpty(dialog))
            {
                _logger.Info(dialog ?? string.Empty);
                var msg = dialog?.replace(";", ";\r\n");
                //_ = MessageBoxShowDialog(msg, "调节提示");
                Growl.InfoGlobal(msg);
                //MessageBox.Show(msg, "调节提示");
            }

            return messageSB.ToString();
        }
        #endregion
        #endregion
    }
}
