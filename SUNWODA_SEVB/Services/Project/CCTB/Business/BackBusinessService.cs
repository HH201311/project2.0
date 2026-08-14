using ControlzEx.Standard;
using Dm.util;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HslCommunication;
using HslCommunication.MQTT;
using HslCommunication.Profinet.OpenProtocol;
using LibVLCSharp.Shared;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Ocsp;
using Python.Runtime;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Mqtt;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.CCTB.BusinessModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.CCDSocketServer;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHMqttClient;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketClient;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketServer;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DataReceivedEventArgs = SUNWODA_SEVB.Core.Models.Socket.DataReceivedEventArgs;

namespace SUNWODA_SEVB.Services.Project.CCTB.Business
{
    /// <summary>
    /// 其他业务逻辑服务
    /// </summary>
    public class BackBusinessService : BusinessServiceBase<BackBusinessService>
    {
        private readonly ILoggerService<BackBusinessService> _logger;
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
        public override EBusinessType BusinessType => EBusinessType.BackBusiness;

        /// <summary>
        /// 闭环类型
        /// </summary>
        public override EClosedLoopType ClosedLoopType => EClosedLoopType.Other;

        /// <summary>
        /// 构造函数
        /// </summary>
        public BackBusinessService(
            ILoggerService<BackBusinessService> logger,
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

            // 初始化各个任务
            InitializeknifeSwitchListenerMonitor();

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

            // PLC心跳任务
            var plcHeartLoopATask = PLCHeartMonitorLoopAsync("1#心跳", ct);
            var plcHeartLoopBTask = PLCHeartMonitorLoopAsync("2#心跳", ct);

            // 全局监控任务
            var globalMonitorTask = GlobalValueMonitorLoopAsync(ct);

            // 信号监听
            var signal1Task = KnifeSwitchListenerMonitorLoopAsync(knifeSwitchListener_A, ct);
            var signal2Task = KnifeSwitchListenerMonitorLoopAsync(knifeSwitchListener_B, ct);

            // OPMM采集PLC地址数据
            var opmmGatherPLCTask = OpmmGatherPLCDataMonitorLoopAsync(ct);

            // 趟号监听任务
            var passCountMonitorTask = WMSocketClient_PassCountMonitorLoopAsync(ct);

            // T块数据采集任务
            var tBlockDataGatherTaskA = DHSocketClient_TBloockDataMonitorLoopAsync(ESideType.A, ct);
            var tBlockDataGatherTaskB = DHSocketClient_TBloockDataMonitorLoopAsync(ESideType.B, ct);

            await Task.WhenAny(plcHeartLoopATask, plcHeartLoopBTask, globalMonitorTask, signal1Task, signal2Task, opmmGatherPLCTask, passCountMonitorTask, tBlockDataGatherTaskA, tBlockDataGatherTaskB);
        }

        /// <summary>
        /// 清理
        /// </summary>
        /// <returns></returns>
        protected override async Task CleanupAsync()
        {
            _logger.Info($"清理业务 {BusinessType} 资源");

            _communicationService.WMSocketClientDataReceived -= WMSocketClient_OnDataReceived;
            _communicationService.WMSocketSererDataReceived -= WMSocketServer_OnDataReceived;
            _communicationService.DHSocketSererDataReceived -= DHSocketServer_OnDataReceived_OPMM;
            _communicationService.DHSocketSererDataReceived -= DHSocketServer_OnDataReceived;
            _communicationService.DHASocketClientDataReceived -= DHSocketClient_OnDataReceived;
            _communicationService.DHBSocketClientDataReceived -= DHSocketClient_OnDataReceived;
            _communicationService.CCDSocketSererDataReceived -= CCDSocketServer_OnDataReceived_OPMM;
            _communicationService.CCDSocketSererDataReceived -= CCDSocketServer_OnDataReceived;

            if (knifeSwitchListener_A != null) knifeSwitchListener_A.OnValueChangedEven -= OnKnifeSwitchListenerValueChangedAsync;
            if (knifeSwitchListener_B != null) knifeSwitchListener_B.OnValueChangedEven -= OnKnifeSwitchListenerValueChangedAsync;

            // 清理资源
            _cts?.Cancel();
            _cts?.Dispose();

            await Task.CompletedTask;
        }

        #region PLC心跳
        /// <summary>
        /// PLC心跳监控
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task PLCHeartMonitorLoopAsync(string parameterName, CancellationToken ct)
        {
            _logger.Info($"开支执行PLC心跳任务 [{parameterName}]!");
            var task = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        var value = ReadPLCValueString(parameterName);
                        var re = await WritePLCAsync(parameterName, 1);
                        if (!re.IsSuccess) throw new Exception($"PLC心跳信号 [{parameterName}] 写入异常！原因：{re.Message}");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warn($"[{parameterName}] PLC心跳循环已取消！");
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

        #region 进退刀监控
        private ValueListener? knifeSwitchListener_A, knifeSwitchListener_B;

        /// <summary>
        /// 初始化进退刀监控
        /// </summary>
        private void InitializeknifeSwitchListenerMonitor()
        {
            knifeSwitchListener_A = new ValueListener() { Name = "机头A模头位置状态", Value = "0", Description = "A面" };
            knifeSwitchListener_A.OnValueChangedEven -= OnKnifeSwitchListenerValueChangedAsync;
            knifeSwitchListener_A.OnValueChangedEven += OnKnifeSwitchListenerValueChangedAsync;
            knifeSwitchListener_B = new ValueListener() { Name = "机头B模头位置状态", Value = "0", Description = "B面" };
            knifeSwitchListener_B.OnValueChangedEven -= OnKnifeSwitchListenerValueChangedAsync;
            knifeSwitchListener_B.OnValueChangedEven += OnKnifeSwitchListenerValueChangedAsync;
        }

        /// <summary>
        /// 初始化进退刀监控监听循环
        /// </summary>
        /// <returns></returns>
        private async Task KnifeSwitchListenerMonitorLoopAsync(ValueListener? listener, CancellationToken ct)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            var task = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        var setting = _projectSettingRepository.LoadBasicFunctionSettingProjectParameters();
                        if (!setting.IsUseJudgeOfKnife) continue;

                        listener.Value = ReadPLCValueString(listener.Name);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warn($"[{listener.Name}] 进退刀监控监听循环已取消");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[{listener.Name}] 进退刀监听线程异常", ex);
                    }
                }
            });
            await task.WaitAsync(ct);
        }

        /// <summary>
        /// 进退刀监控值变化事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnKnifeSwitchListenerValueChangedAsync(object? sender, EventArgs<string> args)
        {
            if (!(sender is ValueListener listener)) return;
            if (args.OldValue == args.NewValue) return;

            if (args.NewValue == "0")
            {
                var message = $"{listener.Description}进刀状态，涂布MD/TD闭环已开启！";
                _logger.Info(message);
                Growl.InfoGlobal(message);
                return;
            }

            if (args.NewValue == "1")
            {
                var message = $"{listener.Description}进刀状态，涂布MD/TD闭环已关闭！";
                _logger.Info(message);
                Growl.InfoGlobal(message);
                return;
            }

            _logger.Error($"{listener.Name} 读取数据信号为异常值[{args.NewValue}]");
        }
        #endregion

        #region 项目全局变量更新
        /// <summary>
        /// 初始化项目全局变量监听循环
        /// </summary>
        /// <returns></returns>
        private async Task GlobalValueMonitorLoopAsync(CancellationToken ct)
        {
            var task = Task.Run(async () =>
            {
                var pi = GlobalContextModel.Instance.ProductionInfo;
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        CCTBCore.ProductSn = GetProductSN();
                        pi.FilmRollNumber = CCTBCore.ProductSn;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warn($"全局监控数据任务已取消");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"全局监控数据任务异常", ex);
                    }
                }
            });
            await task.WaitAsync(ct);
        }
        #endregion

        #region 一人多机PLC采集
        /// <summary>
        /// 初始化一人多机PLC采集监控
        /// </summary>
        private async Task OpmmGatherPLCDataMonitorLoopAsync(CancellationToken ct)
        {
            _logger.Info("初始化一人多机PLC数采任务！");
            var task = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        // 功能未开启
                        var setting = LoadBasicFunctionSetting();
                        if (!setting.DataTransferOfOpmmPLCDataEnable) continue;

                        // 设备未启用
                        var cSettings = _communicationService.Settings;
                        if (cSettings == null || !cSettings.OPMMPLCSocketClientEnabled) continue;

                        var workship = _globalSettingRepository.GetByName("workship")?.Value;
                        var deviceSn = _globalSettingRepository.GetByName("DeviceSn")?.Value;
                        var ip = _plcConfigRepository.GetList(o => o.Name == CCTBCore.PLCName).FirstOrDefault()?.IP;
                        var plcAddresses = _plcService.RWAddresses.Where(o => o.Value.Remark == "OPMM").Select(o => o.Value).ToList();

                        var quest = new OpmmQuest();
                        quest.MsgType = "data";
                        quest.Sid = workship?.Split(':')[2];
                        quest.Ts = GetTimeStampMilliseconds();

                        var opmmPLCData = new OpmmPLCData();
                        opmmPLCData.LineName = workship?.Split(':')[2];
                        opmmPLCData.IP = ip;
                        opmmPLCData.DeviceSn = deviceSn;
                        opmmPLCData.PLCParameterModelList = plcAddresses.Select(o => new PLCParameterModel()
                        {
                            PName = o.ParameterName,
                            PType = o.Type,
                            PValue = o.MonitorValue?.ToString()
                        }).ToList();
                        quest.Data = opmmPLCData;

                        var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(quest));
                        var header = new byte[4];
                        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)payload.Length);
                        var sendBuffer = header.Concat(payload).ToArray();

                        if (_communicationService.OPMMPLCSocketClient == null || !_communicationService.OPMMPLCSocketClient.IsConnected)
                        {
                            throw new Exception($"[一人多机PLC {cSettings.OPMMSocketClientIP} {cSettings.OPMMPLCSocketClientPort}]设备未连接！");
                        }

                        await _communicationService.OPMMPLCSocketClient.SendAsync(sendBuffer, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warn($"一人多机PLC数采任务已取消");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"一人多机PLC数采任务线程异常[60s后重试]", ex);
                        await Task.Delay(60000);
                    }
                }
            });
            await task.WaitAsync(ct);
        }
        #endregion

        #region 协议解析（数据接收）
        /// <summary>
        /// 初始化协议监听委托注册
        /// </summary>
        private void InitializeCommunicationRegister()
        {
            _communicationService.WMSocketClientDataReceived -= WMSocketClient_OnDataReceived;
            _communicationService.WMSocketClientDataReceived += WMSocketClient_OnDataReceived;

            _communicationService.WMSocketSererDataReceived -= WMSocketServer_OnDataReceived;
            _communicationService.WMSocketSererDataReceived += WMSocketServer_OnDataReceived;

            _communicationService.DHSocketSererDataReceived -= DHSocketServer_OnDataReceived_OPMM;
            _communicationService.DHSocketSererDataReceived += DHSocketServer_OnDataReceived_OPMM;

            _communicationService.DHSocketSererDataReceived -= DHSocketServer_OnDataReceived;
            _communicationService.DHSocketSererDataReceived += DHSocketServer_OnDataReceived;

            _communicationService.DHASocketClientDataReceived -= DHSocketClient_OnDataReceived;
            _communicationService.DHASocketClientDataReceived -= DHSocketClient_OnDataReceived;

            _communicationService.DHBSocketClientDataReceived -= DHSocketClient_OnDataReceived;
            _communicationService.DHBSocketClientDataReceived -= DHSocketClient_OnDataReceived;

            _communicationService.DHAMqttMessageReceived -= DHMqttClient_OnMessageReceived;
            _communicationService.DHAMqttMessageReceived += DHMqttClient_OnMessageReceived;

            _communicationService.DHBMqttMessageReceived -= DHMqttClient_OnMessageReceived;
            _communicationService.DHBMqttMessageReceived += DHMqttClient_OnMessageReceived;

            _communicationService.CCDSocketSererDataReceived -= CCDSocketServer_OnDataReceived_OPMM;
            _communicationService.CCDSocketSererDataReceived += CCDSocketServer_OnDataReceived_OPMM;

            _communicationService.CCDSocketSererDataReceived -= CCDSocketServer_OnDataReceived;
            _communicationService.CCDSocketSererDataReceived += CCDSocketServer_OnDataReceived;
        }

        #region SocketClient：大成
        /// <summary>
        /// 
        /// </summary>
        private readonly object wmSocketClientLocker = new();
        /// <summary>
        /// 趟号缓存、扫描类型缓存
        /// </summary>
        private float[] passCountCache = [], scanTypeCache = [];
        /// <summary>
        /// 基材值、目标值A、目标值B、公差A、公差B
        /// </summary>
        private float substrateValue, targetValueA, targetValueB, toleranceA, toleranceB;

        /// <summary>
        /// 报文解析
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WMSocketClient_OnDataReceived(object? sender, ClientDataReceivedEventArgs e)
        {
            if (!(sender is ISocketClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";

            try
            {
                var gm = LoadGeneralSettingsModel();
                var bf = LoadBasicFunctionSetting();
                // 是否采集测厚仪数据
                if (!bf.IsUseCollectFunctionOfWMWeightData) return;

                // 解析响应帧
                if (!MeasurementFrame.TryParse(e.Data, out var response, out string error) || response == null) throw new Exception(error);
                var brAttachment = response.Description;
                var databuffers = response.Data;

                if (brAttachment == "GETDATA") ParseGetData(databuffers, gm);
                else if (brAttachment == "OPERATIONSHETT_G") ParseOperationSheet_G(databuffers, gm);
                else if (int.TryParse(brAttachment, out int brAttachmentNumber))
                {
                    var data = ParseATLWeight(databuffers, brAttachmentNumber, gm);

                    // 毫米数据，是否采集
                    var isCollect = (bf.IsUseMillimeterData &&
                        (data.WeightDataType == EWeightDataType.干膜净重毫米数据 ||
                        data.WeightDataType == EWeightDataType.干膜总重毫米数据 ||
                        data.WeightDataType == EWeightDataType.湿膜净重毫米数据 ||
                        data.WeightDataType == EWeightDataType.湿膜总重毫米数据));
                    // 干膜分区净重，是否采集
                    isCollect |= (bf.IsUseDryFilmNetDataOfPartitioned && data.WeightDataType == EWeightDataType.干膜净重分区数据);
                    // 干膜分区总重，是否采集
                    isCollect |= (bf.IsUseDryFilmTotalDataOfPartitioned && data.WeightDataType == EWeightDataType.干膜总重分区数据);

                    if (!isCollect) return;

                    // 执行委托，下发数据
                    var args = new MessageEventArgs<CoatingDataModel>()
                    {
                        Tag = deviceName,
                        Message = data
                    };
                    CCTBCore.CoatingMessageNotificationEvent?.Invoke(this, args);

                    // 数据保存
                    var folderPath = bf.GetFullPathOfCoatingData();
                    var fullPath = Path.Combine(folderPath, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    var filePath = Path.Combine(fullPath, $"{CCTBCore.ProductSn}_{data.SideType}.csv");
                    var dic = ConvertToDictionary(data);
                    WriteToCsvAsync(filePath, dic);
                }
                else throw new Exception($"未实现的报文命令：{brAttachment}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{deviceName}] 接收报文解析异常", ex);
            }
        }

        /// <summary>
        /// 解析GETDATA报文
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="gm"></param>
        private void ParseGetData(byte[] buffer, GeneralSettingsModel gm)
        {
            var getData = GetDataModel.Parse(buffer, (int)gm.RackType);
            if (getData.PassCounts == null) return;

            lock (wmSocketClientLocker)
            {
                scanTypeCache = getData.ScanTypes ?? new float[(int)gm.RackType];
                if (passCountCache == null || passCountCache.Count() != (int)gm.RackType)
                {
                    passCountCache = getData.PassCounts;
                    return;
                }

                for (int i = 0; i < (int)gm.RackType; i++)
                {
                    if (getData.PassCounts[i] != passCountCache[i])
                    {
                        _logger.Info($"[{i + 1}架]趟号发生变化：{passCountCache[i]} => {getData.PassCounts[i]}");
                        passCountCache[i] = getData.PassCounts[i];

                        if (gm.RackType == ERackType.三架 && i == 0) continue;
                        if (gm.RackType == ERackType.五架 && i == 0) continue;

                        var sendByffer2 = BuildBrRequestPacket($"ATLWEIGHT_{i + 1}");
                        _communicationService.WMSocketClient?.SendAsync(sendByffer2);
                    }
                }
            }
        }

        /// <summary>
        /// 解析OPERATIONSHETT_G报文
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="gm"></param>
        private void ParseOperationSheet_G(byte[] buffer, GeneralSettingsModel gm)
        {
            switch (gm.RackType)
            {
                case ERackType.两架:
                    {
                        var operationSheetG = FromBytes<S_OperationSheetCC_2>(buffer);
                        lock (wmSocketClientLocker)
                        {
                            substrateValue = operationSheetG.BaseWeight;
                            targetValueA = operationSheetG.NetWeightA;
                            targetValueB = operationSheetG.NetWeightB;
                            toleranceA = operationSheetG.SingleTol;
                            toleranceB = operationSheetG.DoubleTol;
                        }
                        break;
                    }
                case ERackType.三架:
                    {
                        var operationSheetG = FromBytes<S_OperationSheetCC_3>(buffer);
                        lock (wmSocketClientLocker)
                        {
                            substrateValue = operationSheetG.BaseWeight;
                            targetValueA = operationSheetG.NetWeightA;
                            targetValueB = operationSheetG.NetWeightB;
                            toleranceA = operationSheetG.SingleTol;
                            toleranceB = operationSheetG.DoubleTol;
                        }
                        break;
                    }
                case ERackType.五架:
                    {
                        var operationSheetG = FromBytes<S_OperationSheetCC_5>(buffer);
                        lock (wmSocketClientLocker)
                        {
                            substrateValue = operationSheetG.BaseWeight;
                            targetValueA = operationSheetG.NetWeightA;
                            targetValueB = operationSheetG.NetWeightB;
                            toleranceA = operationSheetG.SingleTol;
                            toleranceB = operationSheetG.DoubleTol;
                        }
                        break;
                    }
                default: throw new NotSupportedException($"不支持的架数类型：{gm.RackType}");
            }
        }

        /// <summary>
        /// 将字节数组转换为指定结构体（结构体必须由 StructLayout 标记）
        /// </summary>
        private T FromBytes<T>(byte[] data) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            if (data == null || data.Length != size)
                throw new ArgumentException($"数据长度应为 {size} 字节", nameof(data));

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }

        /// <summary>
        /// 解析重量数据报文
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="attachmentNumber"></param>
        /// <param name="gm"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        private CoatingDataModel ParseATLWeight(byte[] buffer, int attachmentNumber, GeneralSettingsModel gm)
        {
            var data = new CoatingDataModel();
            data.GatherTime = DateTime.Now;
            data.PassCount = passCountCache.Length >= attachmentNumber ? passCountCache[attachmentNumber - 1] : -1;
            data.ScanType = scanTypeCache.Length >= attachmentNumber ? scanTypeCache[attachmentNumber - 1] : -1;
            data.SubstrateValue = substrateValue;
            data.TargetValueA = targetValueA;
            data.TargetValueB = targetValueB;
            data.ToleranceValueA = toleranceA;
            data.ToleranceValueB = toleranceB;
            switch ((gm.RackType, attachmentNumber))
            {
                case (ERackType.两架, 1):
                case (ERackType.三架, 2):
                case (ERackType.五架, 3):
                    {
                        data.SideType = ESideType.A;
                        data.WeightDataType = EWeightDataType.干膜净重分区数据;
                        var weights = new float[buffer.Length / 4];
                        Buffer.BlockCopy(buffer, 0, weights, 0, buffer.Length);
                        data.Weights = weights.ToList();
                    }
                    break;
                case (ERackType.两架, 2):
                case (ERackType.三架, 3):
                case (ERackType.五架, 5):
                    {
                        data.SideType = ESideType.B;
                        data.WeightDataType = EWeightDataType.干膜净重分区数据;
                        var weights = new float[buffer.Length / 4];
                        Buffer.BlockCopy(buffer, 0, weights, 0, buffer.Length);
                        data.Weights = weights.ToList();
                    }
                    break;
                // -TODO  毫米数据的前4个字节是趟号
                default: throw new NotSupportedException($"不支持的架数类型或附件号：{gm.RackType}, {attachmentNumber}");
            }

            return data;
        }

        /// <summary>
        /// 趟号监控任务
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task WMSocketClient_PassCountMonitorLoopAsync(CancellationToken ct)
        {
            _logger.Info("初始化趟号监控任务[WMSocketClient]！");
            var task = Task.Run(async () =>
            {
                var sw = new Stopwatch();
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        sw.Restart();
                        // 设备未启用
                        var cSettings = _communicationService.Settings;
                        if (cSettings == null || cSettings.WMCommunicationMode != ECommunicationMode.客户端Socket || !cSettings.WMSocketClientEnabled) continue;

                        // 数采功能未开启
                        var bfSettings = LoadBasicFunctionSetting();
                        if (!bfSettings.IsUseCollectFunctionOfWMWeightData) continue;

                        // 设备连接异常
                        if (_communicationService.WMSocketClient == null || !_communicationService.WMSocketClient.IsConnected)
                        {
                            throw new Exception($"[涂布客户端 {cSettings?.WMSocketClientIP} {cSettings?.WMSocketClientPort}]设备未连接！");
                        }

                        // 发送GETDATA命令：获取趟号等信息
                        var sendBuffer1 = BuildBrRequestPacket("GETDATA");
                        await _communicationService.WMSocketClient.SendAsync(sendBuffer1, ct);

                        // 发送OPERATIONSHETT_G明亮，请求操作单数据
                        var sendBuffer2 = BuildBrRequestPacket("OPERATIONSHETT_G");
                        _communicationService.WMSocketClient?.SendAsync(sendBuffer2, ct);
                        sw.Stop();

                        var sleepTime = bfSettings.CollectFrequencyOfWeightData * 1000 - sw.ElapsedMilliseconds;
                        if (!(bfSettings.CollectFrequencyOfWeightData > 1 && sleepTime > 0)) continue;
                        await Task.Delay((int)sleepTime, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warn($"趟号监控任务[WMSocketClient]已取消");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"趟号监控任务[WMSocketClient]线程异常[60s后重试]", ex);
                        await Task.Delay(60000);
                    }
                }
            });
            await task.WaitAsync(ct);
        }
        #endregion

        #region SocketServer：锐奇
        private float targetValueA1, targetValueB1, toleranceA1, toleranceB1;

        /// <summary>
        /// 报文解析
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WMSocketServer_OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            if (!(sender is ISocketServer server)) return;
            var deviceName = server.ActiveOptions?.ModuleName ?? "Unknow";

            try
            {
                // 是否采集测厚仪数据
                var bf = LoadBasicFunctionSetting();
                if (!bf.IsUseCollectFunctionOfWMWeightData) return;

                var dataStr = Encoding.UTF8.GetString(e.Data);
                dataStr = dataStr.Remove(0, "\u0002".length());
                dataStr = dataStr.RemoveLast("\u0003".length());
                var dataArr = Regex.Split(dataStr, "\u0009");
                if (dataArr.Length != 4) return;
                var messageSequence = int.Parse(dataArr[0]);
                var commandName = dataArr[1];
                var dataJson = dataArr[2];
                var dataCrc = uint.Parse(dataArr[3]);

                if (commandName == "CCTBHEARTQUEST") ParseCCTBHeartQuest(dataJson);
                else if (commandName == "CCTBWEIGHTQUEST")
                {
                    var quest = JsonConvert.DeserializeObject<CCTBWEIGHTQUEST>(dataJson);
                    if (quest == null || quest.DEVICENAME == "BV") return;

                    var data = ParseCCTBWeightQuest(quest);

                    // 毫米数据，是否采集
                    if (!bf.IsUseMillimeterData &&
                        (data.WeightDataType == EWeightDataType.干膜净重毫米数据 ||
                        data.WeightDataType == EWeightDataType.干膜总重毫米数据 ||
                        data.WeightDataType == EWeightDataType.湿膜净重毫米数据 ||
                        data.WeightDataType == EWeightDataType.湿膜总重毫米数据)) return;
                    // 干膜分区净重，是否采集
                    if (!bf.IsUseDryFilmNetDataOfPartitioned && data.WeightDataType == EWeightDataType.干膜净重分区数据) return;
                    // 干膜分区总重，是否采集
                    if (!bf.IsUseDryFilmTotalDataOfPartitioned && data.WeightDataType == EWeightDataType.干膜总重分区数据) return;

                    // 执行委托，下发数据
                    var args = new MessageEventArgs<CoatingDataModel>()
                    {
                        Tag = deviceName,
                        Message = data
                    };
                    CCTBCore.CoatingMessageNotificationEvent?.Invoke(this, args);

                    // 数据保存
                    var folderPath = bf.GetFullPathOfCoatingData();
                    var fullPath = Path.Combine(folderPath, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    var filePath = Path.Combine(fullPath, $"{CCTBCore.ProductSn}_{data.SideType}.csv");
                    var dic = ConvertToDictionary(data);
                    WriteToCsvAsync(filePath, dic);
                }
                else throw new Exception($"未实现的报文命令：{commandName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{deviceName}] 接收报文解析异常", ex);
            }
        }

        /// <summary>
        /// 解析心跳报文
        /// </summary>
        /// <param name="data"></param>
        private void ParseCCTBHeartQuest(string data)
        {
            var quest = JsonConvert.DeserializeObject<CCTBHEARTQUEST>(data);

            _logger.Info("正在处理Server心跳数据！");
            // -TODO    心跳处理
        }

        /// <summary>
        /// 解析重量数据报文
        /// </summary>
        /// <param name="quest"></param>
        private CoatingDataModel ParseCCTBWeightQuest(CCTBWEIGHTQUEST quest)
        {
            var result = new CoatingDataModel();
            result.GatherTime = DateTime.Now;
            result.SideType = ConvertToESideType(quest.DEVICENAME);
            result.WeightDataType = ConvertToEWeightDataType(quest.DEVICENAME);
            result.PassCount = int.Parse(quest.SCANCOUNT ?? string.Empty);
            result.ScanType = -1;
            result.SubstrateValue = float.Parse(quest.BASEVALUE ?? string.Empty);
            if (result.SideType == ESideType.A)
            {
                targetValueA1 = float.Parse(quest.TARGET_VALUE ?? string.Empty);
                toleranceA1 = float.Parse(quest.TECHNIQUE ?? string.Empty);
            }
            if (result.SideType == ESideType.B)
            {
                targetValueB1 = float.Parse(quest.TARGET_VALUE ?? string.Empty) - targetValueA1;
                toleranceB1 = float.Parse(quest.TECHNIQUE ?? string.Empty);
            }
            result.TargetValueA = targetValueA1;
            result.TargetValueB = targetValueB1;
            result.ToleranceValueA = toleranceA1;
            result.ToleranceValueB = toleranceB1;
            result.Weights = quest.WEIGHT?.Select(o => float.Parse(o))?.ToList() ?? new List<float>();

            return result;
        }

        /// <summary>
        /// 转换为ESideType类型
        /// </summary>
        /// <param name="deviceName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private ESideType ConvertToESideType(string? deviceName)
        {
            return deviceName switch
            {
                "A" or "AT" or "AMM" or "ATMM" or "AW" or "AWT" or "AWMM" or "AWTMM" => ESideType.A,
                "B" or "BT" or "BMM" or "BTMM" or "BW" or "BWT" or "BWMM" or "BWTMM" => ESideType.B,
                _ => throw new NotImplementedException($"不支持的数据类型[{deviceName}]")
            };
        }

        /// <summary>
        /// 转换为EWeightDataType类型
        /// </summary>
        /// <param name="deviceName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private EWeightDataType ConvertToEWeightDataType(string? deviceName)
        {
            return deviceName switch
            {
                "A" or "B" => EWeightDataType.干膜净重分区数据,
                "AT" or "BT" => EWeightDataType.干膜总重分区数据,
                "AMM" or "BMM" => EWeightDataType.干膜净重毫米数据,
                "ATMM" or "BTMM" => EWeightDataType.干膜总重毫米数据,
                "AW" or "BW" => EWeightDataType.湿膜净重分区数据,
                "AWT" or "BWT" => EWeightDataType.湿膜总重分区数据,
                "AWMM" or "BWMM" => EWeightDataType.湿膜净重毫米数据,
                "AWTMM" or "BWTMM" => EWeightDataType.湿膜总重毫米数据,
                _ => throw new NotImplementedException($"不支持的数据类型[{deviceName}]")
            };
        }
        #endregion

        #region SocketServer：上川、宇世嘉
        private float targetValueA2, targetValueB2, toleranceA2, toleranceB2;

        /// <summary>
        /// 报文解析
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DHSocketServer_OnDataReceived_OPMM(object? sender, DataReceivedEventArgs e)
        {
            if (!(sender is ISocketServer server)) return;
            var deviceName = server.ActiveOptions?.ModuleName ?? "Unknow";

            try
            {
                // 是否采集模头面密度数据发送至一人多机
                var bf = LoadBasicFunctionSetting();
                if (!bf.DataTransferOfOpmmWeightDataEnable) return;

                var cs = _communicationService.Settings;
                if (cs == null || !cs.OPMMWeightSocketClientEnabled) return;

                var client = _communicationService.OPMMWeightSocketClient;
                if (client == null || !client.IsConnected) return;

                var header = new byte[4];
                var payload = e.Data;
                BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)payload.Length);
                await client.SendAsync(header.Concat(payload).ToArray(), _cts?.Token ?? default);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{deviceName}] 接收报文解析异常", ex);
            }
        }

        /// <summary>
        /// 报文解析
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DHSocketServer_OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            if (!(sender is ISocketServer server)) return;
            var deviceName = server.ActiveOptions?.ModuleName ?? "Unknow";

            try
            {
                // 是否采集模头面密度数据
                var bf = LoadBasicFunctionSetting();
                if (!bf.IsUseCollectFunctionOfDHWeightData) return;

                var dataStr = Encoding.UTF8.GetString(e.Data);
                dataStr = dataStr.Remove(0, "\u0002".length());
                dataStr = dataStr.RemoveLast("\u0003".length());
                var dataArr = Regex.Split(dataStr, "\u0009");
                if (dataArr.Length != 4) return;
                var messageSequence = int.Parse(dataArr[0]);
                var commandName = dataArr[1];
                var dataJson = dataArr[2];
                var dataCrc = uint.Parse(dataArr[3]);

                if (commandName == "WEIGHTDATAREQUEST")
                {
                    var quest = JsonConvert.DeserializeObject<WeightDataRequest>(dataJson);
                    if (quest == null || quest.DEVICENAME == "BV") return;

                    var data = ParseWeightDataRequest(quest);

                    // 执行委托，下发数据
                    var args = new MessageEventArgs<CoatingDataModel>()
                    {
                        Tag = deviceName,
                        Message = data
                    };
                    CCTBCore.CoatingMessageNotificationEvent?.Invoke(this, args);

                    // 数据保存
                    var folderPath = bf.GetFullPathOfCoatingData();
                    var fullPath = Path.Combine(folderPath, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    var filePath = Path.Combine(fullPath, $"{CCTBCore.ProductSn}_{data.SideType}.csv");
                    var dic = ConvertToDictionary(data);
                    WriteToCsvAsync(filePath, dic);
                }
                else throw new Exception($"未实现的报文命令：{commandName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{deviceName}] 接收报文解析异常", ex);
            }
        }

        /// <summary>
        /// 报文解析
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DHSocketClient_OnDataReceived(object? sender, ClientDataReceivedEventArgs e)
        {
            if (!(sender is ISocketClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";

            try
            {
                // 是否采集模头T块数据
                var bf = LoadBasicFunctionSetting();
                if (!bf.IsUseCollectFunctionOfDHTBlockData) return;

                var deviceNameA = ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.DHASocketClientDeviceName];
                var deviceNameB = ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.DHBSocketClientDeviceName];
                var sideType = deviceName switch
                {
                    var d when d == deviceNameA => ESideType.A,
                    var d when d == deviceNameB => ESideType.B,
                    _ => throw new Exception($"不支持的设备类型 {deviceName}")
                };

                var dataStr = Encoding.UTF8.GetString(e.Data);
                dataStr = dataStr.Remove(0, "\u0002".length());
                dataStr = dataStr.RemoveLast("\u0003".length());
                var dataArr = Regex.Split(dataStr, "\u0009");
                if (dataArr.Length != 4) return;
                var messageSequence = int.Parse(dataArr[0]);
                var commandName = dataArr[1];
                var dataJson = dataArr[2];
                var dataCrc = uint.Parse(dataArr[3]);

                if (commandName == "TINFOREPONSE")
                {
                    var reponse = JsonConvert.DeserializeObject<TInfoReponseModel>(dataJson);
                    if (reponse == null) return;

                    var data = ParseTInfoReponse(sideType, reponse);

                    var args = new MessageEventArgs<TBlockDataModel>()
                    {
                        Tag = deviceName,
                        Message = data
                    };
                    CCTBCore.TBlockMessageNotificationEvent?.Invoke(this, args);

                    // 数据保存
                    var folderPath = bf.GetFullPathOfTBlockData();
                    var fullPath = Path.Combine(folderPath, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    var filePath = Path.Combine(fullPath, $"{CCTBCore.ProductSn}_{sideType}.csv");
                    var dic = ConvertToDictionary(data);
                    WriteToCsvAsync(filePath, dic);
                }
                else if (commandName == "TADJUSTREQUEST")
                {
                    _logger.Info($"[DHSocketClient]T块调机下发完成：{dataJson}");
                }
                else if (commandName == "MADJUSTREQUEST")
                {
                    _logger.Info($"[DHSocketClient]MD调机下发完成：{dataJson}");
                }
                else throw new Exception($"未实现的报文命令：{commandName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{deviceName}] 接收报文解析异常", ex);
            }
        }

        /// <summary>
        /// 解析重量数据报文
        /// </summary>
        /// <param name="quest"></param>
        /// <returns></returns>
        private CoatingDataModel ParseWeightDataRequest(WeightDataRequest quest)
        {
            var result = new CoatingDataModel();
            result.GatherTime = DateTime.Now;
            result.SideType = ConvertToESideType(quest.DEVICENAME);
            result.WeightDataType = ConvertToEWeightDataType(quest.DEVICENAME);
            result.PassCount = quest.SCANCOUNT;
            result.ScanType = -1;
            result.SubstrateValue = (float)quest.BASEVALUE;
            if (result.SideType == ESideType.A)
            {
                targetValueA2 = (float)quest.TARGET_VALUE;
                toleranceA2 = (float)quest.TECHNIQUE;
            }
            if (result.SideType == ESideType.B)
            {
                targetValueB2 = (float)quest.TARGET_VALUE - targetValueA2;
                toleranceB2 = (float)quest.TECHNIQUE;
            }
            result.TargetValueA = targetValueA2;
            result.TargetValueB = targetValueB2;
            result.ToleranceValueA = toleranceA2;
            result.ToleranceValueB = toleranceB2;
            result.Weights = quest.WEIGHTDATAS?.Select(o => (float)o)?.ToList() ?? new List<float>();

            return result;
        }

        /// <summary>
        /// 解析T块数据报文
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="reponse"></param>
        /// <returns></returns>
        private TBlockDataModel ParseTInfoReponse(ESideType sideType, TInfoReponseModel reponse)
        {
            var result = new TBlockDataModel();
            result.GatherTime = DateTime.Now;
            result.SideType = sideType;
            result.DeviceName = reponse.DeviceName;
            result.TAdjustMode = reponse.TAdjustMode;
            result.TNumber = reponse.TNumber;
            result.TBlockInfoLis = reponse.TBlockInfoLis.CopyByJson() ?? new List<TBlockInfo>();
            // 目前除了 曼恩斯特Mqtt 外，其他协议都没有使能字段，所以默认设置为1
            if (result.TBlockInfoLis != null) result.TBlockInfoLis.ForEach(o => o.TEnable = 1);
            result.ReponseTime = reponse.ReponseTime;

            return result;
        }

        /// <summary>
        /// 后台任务：异步获取T块数据
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task DHSocketClient_TBloockDataMonitorLoopAsync(ESideType sideType, CancellationToken ct)
        {
            _logger.Info($"初始化T块数采任务[DH{sideType}SocketClient]！");
            var task = Task.Run(async () =>
            {
                var sw = new Stopwatch();
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        sw.Restart();
                        // 设备未启用
                        var cSettings = _communicationService.Settings;
                        if (cSettings == null || cSettings.DHCommunicationMode != ECommunicationMode.服务端Socket) continue;
                        (bool enabled, ISocketClient? client) = sideType switch
                        {
                            ESideType.A => (cSettings.DHASocketClientEnabled, _communicationService.DHASocketClient),
                            ESideType.B => (cSettings.DHBSocketClientEnabled, _communicationService.DHBSocketClient),
                            _ => throw new Exception($"不支持的枚举类型 {sideType}")
                        };
                        if (!enabled) continue;

                        // 数采功能未开启
                        var bfSettings = LoadBasicFunctionSetting();
                        if (!bfSettings.IsUseCollectFunctionOfWMWeightData) continue;

                        // 设备连接异常
                        if (client == null || !client.IsConnected)
                        {
                            throw new Exception($"[模头客户端 {client?.RemoteEndPoint}]设备未连接！");
                        }

                        // 发送TINFOREUQEST命令：获取T块数据等信息
                        var sendBuffer = BuildDHRequestPacket(1, "TINFOREUQEST");
                        await client.SendAsync(sendBuffer, ct);

                        var sleepTime = bfSettings.CollectFrequencyOfDHTBlockData * 1000 - sw.ElapsedMilliseconds;
                        if (!(bfSettings.CollectFrequencyOfDHTBlockData > 1 && sleepTime > 0)) continue;
                        await Task.Delay((int)sleepTime, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warn($"T块数采任务[DH{sideType}SocketClient]已取消");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"T块数采任务[DH{sideType}SocketClient]线程异常[60s后重试]", ex);
                        await Task.Delay(60000);
                    }
                }
            });
            await task.WaitAsync(ct);
        }
        #endregion

        #region MqttClient：曼恩斯特
        private ExportDataPayload? exportDataPayload;
        private float targetValueA3, targetValueB3, toleranceA3, toleranceB3;

        /// <summary>
        /// 解析报文
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        private void DHMqttClient_OnMessageReceived(object? sender, MqttApplicationMessage message)
        {
            if (!(sender is ICustomMqttClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";

            try
            {
                var bf = LoadBasicFunctionSetting();

                var deviceNameA = ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.DHAMqttClientDeviceName];
                var deviceNameB = ICommunicationService.ModuleToConfigGroupMap[ICommunicationService.DHBMqttClientDeviceName];
                var sideType = deviceName switch
                {
                    var d when d == deviceNameA => ESideType.A,
                    var d when d == deviceNameB => ESideType.B,
                    _ => throw new Exception($"不支持的设备类型 {deviceName}")
                };

                var exportDataTopic = "v2.0/{0}@manst/mteng_A@manst/0/manst/manst/mteng_A/RPT/SET/0/exportData";
                var axisPosTopic = "v2.0/{0}@manst/mteng_A@manst/0/manst/group/mteng_A/RPT/SET/0/axisPos";
                var multiPumpTopic = "v2.0/multiPump@manst/mteng_A@manst/0/group/manst/mteng_A/RPT/SET/0/multiPump";

                var topic = message.Topic;
                var payload = Encoding.UTF8.GetString(message.Payload);
                var options = client.ActiveOptions?.GetMqttConnectionOptions();
                if (options == null) return;

                if (topic == string.Format(exportDataTopic, options.ClientId))
                {
                    var reponse = JsonConvert.DeserializeObject<ExportDataPayload>(payload);
                    if (reponse == null) return;

                    // T块数据采集时，做缓存
                    if (bf.IsUseCollectFunctionOfDHTBlockData) exportDataPayload = reponse.CopyByJson();

                    // 是否采集面密度数据
                    if (!bf.IsUseCollectFunctionOfDHWeightData) return;
                    var data = ParseExportDataPayload(sideType, reponse);

                    // 执行委托，下发数据
                    var args = new MessageEventArgs<CoatingDataModel>()
                    {
                        Tag = deviceName,
                        Message = data
                    };
                    CCTBCore.CoatingMessageNotificationEvent?.Invoke(this, args);

                    // 数据保存.
                    var folderPath = bf.GetFullPathOfCoatingData();
                    var fullPath = Path.Combine(folderPath, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    var filePath = Path.Combine(fullPath, $"{CCTBCore.ProductSn}_{sideType}.csv");
                    var dic = ConvertToDictionary(data);
                    WriteToCsvAsync(filePath, dic);
                }
                else if (topic == string.Format(axisPosTopic, options.ClientId))
                {
                    // 是否采集T块数据
                    if (!bf.IsUseCollectFunctionOfDHTBlockData) return;

                    // 按逗号分隔解析
                    var parts = payload.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var reponse = parts.Select(p => float.TryParse(p, out var d) ? d : 0).ToArray();
                    if (reponse == null || reponse.Length <= 0 || exportDataPayload == null) return;

                    var data = ParseAxisPos(sideType, reponse, exportDataPayload);

                    // 执行委托，下发数据
                    var args = new MessageEventArgs<TBlockDataModel>()
                    {
                        Tag = deviceName,
                        Message = data
                    };
                    CCTBCore.TBlockMessageNotificationEvent?.Invoke(this, args);

                    // 数据保存
                    var folderPath = bf.GetFullPathOfTBlockData();
                    var fullPath = Path.Combine(folderPath, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    var filePath = Path.Combine(fullPath, $"{CCTBCore.ProductSn}_{sideType}.csv");
                    var dic = ConvertToDictionary(data);
                    WriteToCsvAsync(filePath, dic);
                }
                else if (topic == string.Format(multiPumpTopic, options.ClientId))
                {
                    var reponse = JsonConvert.DeserializeObject<MultiPumpPayload>(payload);
                    if (reponse == null) return;

                    _logger.Info($"设备 {deviceName} 采集泵速数据 [{payload}]");
                }
                else throw new NotImplementedException($"Mqtt未实现的主题[{topic}]!");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{deviceName}] 接收报文解析异常", ex);
            }
        }

        /// <summary>
        /// 解析报文
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="reponse"></param>
        /// <returns></returns>
        private CoatingDataModel ParseExportDataPayload(ESideType sideType, ExportDataPayload reponse)
        {
            var result = new CoatingDataModel();
            result.GatherTime = DateTime.Now;
            result.SideType = sideType;
            result.WeightDataType = ConvertToEWeightDataType(reponse.DeviceName);
            result.PassCount = reponse.Seq;
            result.ScanType = -1;
            result.SubstrateValue = 0;
            if (sideType == ESideType.A)
            {
                targetValueA3 = (float)reponse.TargetDensity;
                var toleranceA_Up = (float)Math.Abs(reponse.DensityUp - reponse.TargetDensity);
                var toleranceA_Down = (float)Math.Abs(reponse.DensityDown - reponse.TargetDensity);
                toleranceA3 = Math.Min(toleranceA_Up, toleranceA_Down);
            }
            if (sideType == ESideType.B)
            {
                targetValueB3 = (float)reponse.TargetDensity - targetValueA3;
                var toleranceB_Up = (float)Math.Abs(reponse.DensityUp - reponse.TargetDensity);
                var toleranceB_Down = (float)Math.Abs(reponse.DensityDown - reponse.TargetDensity);
                toleranceB3 = Math.Min(toleranceB_Up, toleranceB_Down);
            }
            result.TargetValueA = targetValueA3;
            result.TargetValueB = targetValueB3;
            result.ToleranceValueA = toleranceA3;
            result.ToleranceValueB = toleranceB3;
            result.Weights = reponse.AxisDensityAverage.Split(',').Select(o => float.Parse(o)).ToList() ?? new List<float>();

            return result;
        }

        /// <summary>
        /// 解析报文
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="reponse"></param>
        /// <returns></returns>
        private TBlockDataModel ParseAxisPos(ESideType sideType, float[] reponse, ExportDataPayload exportData)
        {
            var result = new TBlockDataModel();
            result.GatherTime = DateTime.Now;
            result.SideType = sideType;
            result.DeviceName = exportData.DeviceName;
            result.TAdjustMode = exportData.AdjustMode;
            result.TNumber = exportData.AxisNum;
            result.ReponseTime = DateTime.Now;
            var tBInfos = new List<TBlockInfo>();

            var axisEnables = exportData.DisabledTblock.Split(',').ToList().ConvertAll(o => int.TryParse(o, out int v) ? v : 0);
            var axisTaps = exportData.AxisTap.Split(',').ToList().ConvertAll(o => int.TryParse(o, out int v) ? v : 0);
            var posUpLimits = exportData.PosUpLimit.Split(',').ToList().ConvertAll(o => int.TryParse(o, out int v) ? v : 0);
            var posLowLimit = float.TryParse(exportData.PosLowLimit, out float low) ? (int)low : 0;
            var axisPoss = exportData.AxisPos.Split(',').ToList().ConvertAll(o => float.TryParse(o, out float v) ? v : 0);
            var count = MathHelper.Max(result.TNumber, axisTaps.Count, posUpLimits.Count, reponse.Length);

            for (int i = 0; i < axisTaps.Count; i++)
            {
                if (tBInfos.Count <= i)
                {
                    tBInfos.Add(new TBlockInfo());
                    tBInfos[i].TId = i + 1;
                }
                tBInfos[i].TEnable = axisEnables.Contains(i + 1) ? 0 : 1;
                tBInfos[i].TTag = axisTaps.Count > i ? axisTaps[i] : 0;
                tBInfos[i].TUpperLimit = posUpLimits.Count > i ? posUpLimits[i] : 0;
                tBInfos[i].TLowerLimit = posLowLimit;
                //tBInfos[i].TPosition = tDatas.Length > i ? tDatas[i] : 0;
                tBInfos[i].TPosition = (int)(axisPoss.Count > i ? axisPoss[i] : 0);
                tBInfos[i].AlarmCode = new List<int>();
            }
            result.TBlockInfoLis = tBInfos;

            return result;
        }
        #endregion

        #region SocketServer：CCD(报文使用 ||| 分割)
        /// <summary>
        /// 报文解析
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CCDSocketServer_OnDataReceived_OPMM(object? sender, DataReceivedEventArgs e)
        {
            if (!(sender is ISocketServer server)) return;
            var deviceName = server.ActiveOptions?.ModuleName ?? "Unknow";

            try
            {
                // 是否采集CCD数据发送至一人多机
                var bf = LoadBasicFunctionSetting();
                if (!bf.DataTransferOfOpmmCCDDataEnable) return;

                var cs = _communicationService.Settings;
                if (cs == null || !cs.OPMMCCDSocketClientEnabled) return;

                var client = _communicationService.OPMMCCDSocketClient;
                if (client == null || !client.IsConnected) return;

                var header = new byte[4];
                var payload = e.Data;
                BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)payload.Length);
                await client.SendAsync(header.Concat(payload).ToArray(), _cts?.Token ?? default);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{deviceName}] 接收报文解析异常", ex);
            }
        }

        /// <summary>
        /// 报文解析
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CCDSocketServer_OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            if (!(sender is ISocketServer server)) return;
            var deviceName = server.ActiveOptions?.ModuleName ?? "Unknow";

            try
            {
                // 是否采集CCD数据
                var bf = LoadBasicFunctionSetting();
                if (!bf.IsUseCollectFunctionOfCCDData) return;

                var quest = JsonConvert.DeserializeObject<CCTBCCDQuestModel>(Encoding.UTF8.GetString(e.Data));
                if (quest == null) return;
                var commandName = quest.MsgType;
                var dataJson = (string?)quest.Data?.ToString();
                if (dataJson == null || string.IsNullOrEmpty(dataJson)) return;
                if (commandName == "hb")
                {
                    var questData = JsonConvert.DeserializeObject<HBMessageModel>(dataJson);
                    if (questData == null) return;

                    var dateTime = ToLocalDateTime(quest?.Ts ?? 0).ToString("yyyy-MM-dd HH:mm:ss.fff");
                    _logger.Info($"正在处理CCD心跳数据[设备ID{quest?.Sid}]心跳 消息类型:{quest?.MsgType} 设备侧时间:{dateTime}]", false);
                }
                else if (commandName == "data_a_wet" || commandName == "data_b_wet" || commandName == "data_b_dry")
                {
                    var questData = JsonConvert.DeserializeObject<WeightMessageModel>(dataJson);
                    if (questData == null) return;

                    // 执行委托，下发数据
                    var args = new MessageEventArgs<Tuple<string, WeightMessageModel>>()
                    {
                        Tag = deviceName,
                        Message = new Tuple<string, WeightMessageModel>(commandName, questData)
                    };
                    CCTBCore.CCDMessageNotificationEvent?.Invoke(this, args);

                    // 数据保存
                    var fullPath = Path.Combine(bf.SavePathOfCCD, $"CCD服务端设备面密度采集数据", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    var filePath = Path.Combine(fullPath, $"{CCTBCore.ProductSn}_{commandName}.csv");
                    var dic = ConvertToDictionary(quest, questData);
                    WriteToCsvAsync(filePath, dic);
                }
                else if (commandName == "alarm")
                {
                    var questData = JsonConvert.DeserializeObject<AlarmMessageModel>(dataJson);
                    if (questData == null) return;

                    // 数据保存
                    var fullPath = Path.Combine(bf.SavePathOfDH, $"CCD服务端设备报警采集数据", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    var filePath = Path.Combine(fullPath, $"{commandName}.csv");
                    var dic = ConvertToDictionary(quest, questData);
                    WriteToCsvAsync(filePath, dic);
                }
                else throw new Exception($"未实现的报文命令：{commandName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{deviceName}] 接收报文解析异常", ex);
            }
        }

        /// <summary>
        /// 转换为字典数据
        /// </summary>
        /// <param name="quest"></param>
        /// <param name="questData"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(CCTBCCDQuestModel quest, WeightMessageModel questData)
        {
            var result = new Dictionary<string, string>();

            result["采集时间"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            result["消息类型"] = quest?.MsgType ?? string.Empty;
            result["设备ID"] = quest?.Sid ?? string.Empty;
            result["设备侧时间"] = ToLocalDateTime(quest?.Ts ?? 0).ToString("yyyy-MM-dd HH:mm:ss.fff");
            result["压缩格式"] = quest?.Z ?? string.Empty;
            result["检测码"] = quest?.Sig ?? string.Empty;
            result["总膜区宽度"] = questData.CoatingTotalWidth.ToString();
            foreach (var data in questData?.TabWidth ?? new List<ParaData>()) result[$"{data?.PName}"] = data?.PValue ?? string.Empty;
            foreach (var data in questData?.CilWidth ?? new List<ParaData>()) result[$"{data?.PName}"] = data?.PValue ?? string.Empty;
            foreach (var data in questData?.CoatingWidth ?? new List<ParaData>()) result[$"{data?.PName}"] = data?.PValue ?? string.Empty;
            foreach (var data in questData?.Misalign ?? new List<ParaData>()) result[$"{data?.PName}"] = data?.PValue ?? string.Empty;
            foreach (var data in questData?.JudgeRes ?? new List<JudgeResult>()) result[$"{data?.Zone}"] = data?.Result ?? string.Empty;
            foreach (var data in questData?.TabSpecification ?? new List<ParaData>()) result[$"{data?.PName}"] = data?.PValue ?? string.Empty;
            foreach (var data in questData?.CilSpecification ?? new List<ParaData>()) result[$"{data?.PName}"] = data?.PValue ?? string.Empty;
            foreach (var data in questData?.CoatingSpecification ?? new List<ParaData>()) result[$"{data?.PName}"] = data?.PValue ?? string.Empty;
            foreach (var data in questData?.MisalignSpecification ?? new List<ParaData>()) result[$"{data?.PName}"] = data?.PValue ?? string.Empty;

            return result;
        }

        /// <summary>
        /// 转换为字典数据
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(CCTBCCDQuestModel quest, AlarmMessageModel questData)
        {
            var result = new Dictionary<string, string>();

            result["采集时间"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            result["消息类型"] = quest?.MsgType ?? string.Empty;
            result["设备ID"] = quest?.Sid ?? string.Empty;
            result["设备侧时间"] = ToLocalDateTime(quest?.Ts ?? 0).ToString("yyyy-MM-dd HH:mm:ss.fff");
            result["压缩格式"] = quest?.Z ?? string.Empty;
            result["检测码"] = quest?.Sig ?? string.Empty;
            result["报警膜区"] = questData?.Zone ?? string.Empty;
            result["报警信息"] = questData?.Info ?? string.Empty;

            return result;
        }
        #endregion

        /// <summary>
        /// 转换为字典数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(CoatingDataModel data)
        {
            var result = new Dictionary<string, string>();

            result["采集时间"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            result["AB面"] = data.SideType.ToString();
            result["数据类型"] = data.WeightDataType.ToString();
            result["基材值"] = data.SubstrateValue.ToString();
            result["A面目标值"] = data.TargetValueA.ToString();
            result["B面目标值"] = data.TargetValueB.ToString();
            result["A面公差值"] = data.ToleranceValueA.ToString();
            result["B面公差值"] = data.ToleranceValueB.ToString();
            result["趟号"] = data.PassCount.ToString();
            result["扫描类型"] = data.ScanType.ToString();
            for (int i = 0; i < data.Weights.Count; i++)
                result[$"重量{i + 1}"] = data.Weights[i].ToString();

            return result;
        }

        /// <summary>
        /// 转换为字典数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(TBlockDataModel data)
        {
            var result = new Dictionary<string, string>();

            result["采集时间"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            result["AB面"] = data.SideType.ToString();
            result["设备名称"] = data.DeviceName ?? string.Empty;
            result["调机模式"] = data.TAdjustMode.ToString();
            result["T块总数"] = data.TNumber.ToString();
            for (int i = 0; i < data.TBlockInfoLis.Count; i++)
                result[$"调机模式{i + 1}"] = data.TBlockInfoLis[i].TAdjustMode.ToString();
            for (int i = 0; i < data.TBlockInfoLis.Count; i++)
                result[$"T块实时位置{i + 1}"] = data.TBlockInfoLis[i].TPosition.ToString();
            for (int i = 0; i < data.TBlockInfoLis.Count; i++)
                result[$"T块上限位置{i + 1}"] = data.TBlockInfoLis[i].TUpperLimit.ToString();
            for (int i = 0; i < data.TBlockInfoLis.Count; i++)
                result[$"T块下限位置{i + 1}"] = data.TBlockInfoLis[i].TLowerLimit.ToString();
            for (int i = 0; i < data.TBlockInfoLis.Count; i++)
                result[$"T块标识{i + 1}"] = data.TBlockInfoLis[i].TLowerLimit.ToString();

            return result;
        }
        #endregion
    }
}
