using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Python.Runtime;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.MES;
using SUNWODA_SEVB.Core.Models.Project.MeltingDepthDetection;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.ViewModels.Pages.MeltingDepthDetection
{
    /// <summary>
    /// 构造函数
    /// </summary>
    [Module("VM_MeltingDepthDetectionPage", "焊接过程预警系统V1.0", Order = 0)]
    internal class VM_MeltingDepthDetectionPage
    (
        ILoggerService<VM_MeltingDepthDetectionPage> logger,
        IMESSettingRepository mESSettingRepository,
        IPLCService pLCService,
        IPLCConfigRepository plcConfigRepository,
        IPLCRWConfigRepository plcRWConfigRepository,
        IPLCAddressConfigRepository plcAddressConfigRepository,
        ITestDataService testDataService,
        IProjectSettingRepository projectSettingRepository
    ) : ViewModelBase
    {

        /// <summary>
        /// PACK码生产-消费队列，用于处理PLC触发的PACK码数据
        /// </summary>
        private BlockingCollection<string> PACKCodeBC { get; } = [];
        private CancellationTokenSource cts = new();
        private int nextId = 1;

        // 惠州算法输入参数名
        private readonly string[] hzParamNames =
        [
            "中心光输出功率", "铜嘴压力", "保护气流量", "除尘风速",
            "焊接速度", "环形光输出功率", "离焦量", "功率"
        ];

        readonly string modelPath = "./PythonScripts/model1.pth";

        #region 界面数据绑定属性

        /// <summary>
        /// 获取或设置熔化深度检测记录的集合
        /// </summary>
        public ObservableCollection<MeltingDepthDetectionModel> Records { get; set; } = [];

        private double targetValue;
        /// <summary>
        /// 规格标准值
        /// </summary>
        public double TargetValue
        {
            get => targetValue;
            set { SetProperty(ref targetValue, value); UpdateAllStatuses(); }
        }

        private double tolerance;
        /// <summary>
        /// 公差范围
        /// </summary>
        public double Tolerance
        {
            get => tolerance;
            set { SetProperty(ref tolerance, value); UpdateAllStatuses(); }
        }

        private Visibility huizhouColumnVisibility = Visibility.Visible;
        /// <summary>
        /// 获取或设置惠州基地的列可见性
        /// </summary>
        public Visibility HuizhouColumnVisibility
        {
            get => huizhouColumnVisibility;
            set => SetProperty(ref huizhouColumnVisibility, value);
        }

        private Visibility zaozhuangColumnVisibility = Visibility.Collapsed;
        /// <summary>
        /// 获取或设置枣庄基地的列可见性
        /// </summary>
        public Visibility ZaozhuangColumnVisibility
        {
            get => zaozhuangColumnVisibility;
            set => SetProperty(ref zaozhuangColumnVisibility, value);
        }

        private string baseLocation = string.Empty;
        /// <summary>
        /// 获取或设置基地位置信息，用于动态显示不同基地的列
        /// </summary>
        public string BaseLocation
        {
            get => baseLocation;
            set
            {
                if (SetProperty(ref baseLocation, value))
                {
                    HuizhouColumnVisibility = value == "惠州" ? Visibility.Visible : Visibility.Collapsed;
                    ZaozhuangColumnVisibility = value == "枣庄" ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// 清除日志命令
        /// </summary>
        public ICommand ClearLogsCommand => new RelayCommand(AppLogs.Clear);

        #endregion

        /// <summary>
        /// 页面初始化时，确保PLC配置、读写配置和地址配置存在，并启动PLC信号监听循环
        /// </summary>
        public override void OnInitialize()
        {
            logger.OnLoggerReportEvent += OnLoggerReport;
            InitProjectSettingConfig();
            var workShip = projectSettingRepository.GetByNameAndVM("WorkShip", "VM_MeltingDepthDetectionPage")?.Value ?? "";
            BaseLocation = GetBaseLocation(workShip);
            InitPLCConfig(BaseLocation);
            InitPLCRWConfig(BaseLocation);
            InitPLCAddressConfig(BaseLocation);
            InitializePLCSigalListenerLoopAsync(BaseLocation);
            switch (BaseLocation)
            {
                case "惠州":
                    break;
                case "枣庄":
                    InitMESSettings();
                    InitMESServer();
                    HandlePACKCode(cts.Token);
                    break;
                default:
                    break;
            }

            // 初始化模拟数据，包含所有新列
            //Records =
            //[
            //    new() { ID = 1, TimeDate = DateTime.Now, PackCode = "12TPBH3BXA001HG39000001XX", DefocusAmount = 0.5, ProtectiveGasFlow = 15.2, WeldingEnergyAverage = 120.5, WeldingEnergyMax = 125.0, WeldingEnergyMin = 115.0, WeldingSpeed = 10.5, WeldingTensionPressure = 50.2, FirstOscillationAmplitude = 1.2, FirstOscillationFrequency = 50.0, SecondOscillationAmplitude = 1.0, DepthValue = 24.5 },
            //    new() { ID = 2, TimeDate = DateTime.Now, PackCode = "12TPBH3BXA001HG39000001XX", DefocusAmount = 0.6, ProtectiveGasFlow = 15.1, WeldingEnergyAverage = 121.2, WeldingEnergyMax = 126.1, WeldingEnergyMin = 116.0, WeldingSpeed = 10.6, WeldingTensionPressure = 50.5, FirstOscillationAmplitude = 1.3, FirstOscillationFrequency = 51.0, SecondOscillationAmplitude = 1.1, DepthValue = 26.1 },
            //    new() { ID = 3, TimeDate = DateTime.Now, PackCode = "12TPBH3BXA001HG39000001XX", DefocusAmount = 0.4, ProtectiveGasFlow = 15.3, WeldingEnergyAverage = 119.5, WeldingEnergyMax = 124.0, WeldingEnergyMin = 114.0, WeldingSpeed = 10.4, WeldingTensionPressure = 49.8, FirstOscillationAmplitude = 1.1, FirstOscillationFrequency = 49.0, SecondOscillationAmplitude = 0.9, DepthValue = 21.0 }, // 可能会NG
            //    new() { ID = 4, TimeDate = DateTime.Now, PackCode = "12TPBH3BXA001HG39000001XX", DefocusAmount = 0.5, ProtectiveGasFlow = 15.2, WeldingEnergyAverage = 120.5, WeldingEnergyMax = 125.0, WeldingEnergyMin = 115.0, WeldingSpeed = 10.5, WeldingTensionPressure = 50.2, FirstOscillationAmplitude = 1.2, FirstOscillationFrequency = 50.0, SecondOscillationAmplitude = 1.0, DepthValue = 25.0 },
            //    new() { ID = 5, TimeDate = DateTime.Now, PackCode = "12TPBH3BXA001HG39000001XX", DefocusAmount = 0.7, ProtectiveGasFlow = 15.0, WeldingEnergyAverage = 122.1, WeldingEnergyMax = 127.5, WeldingEnergyMin = 117.0, WeldingSpeed = 10.7, WeldingTensionPressure = 51.0, FirstOscillationAmplitude = 1.4, FirstOscillationFrequency = 52.0, SecondOscillationAmplitude = 1.2, DepthValue = 28.5 }, // 可能会NG                
            //];
            //UpdateAllStatuses();
            //logger.Info($"读取数据信号复位0");
            //logger.Info($"读取数据信号复位0");
            //logger.Info($"读取数据信号复位0");
            //logger.Info($"读取数据信号复位0");
            //logger.Info($"读取数据信号复位0");
            //logger.Info($"读取数据信号复位0");
            //logger.Info($"读取数据信号复位0");
            //logger.Info($"读取数据信号复位0");
            //logger.Info($"读取数据信号复位0");
            //logger.Info($"读取数据信号复位0");
            //logger.Info($"读取数据信号复位0");
            //logger.Info($"读取数据信号复位0");

            //string stringBuilder;
            ////stringBuilder = ("离焦量,保护气流量,焊接能量最大值,焊接速度\n");
            //stringBuilder = ("中心光输出功率,铜嘴压力,保护气流量,除尘风速,焊接速度,环形光输出功率,离焦量,功率\n");
            //stringBuilder += "11,20.1208,4809,80,10.5,120.5,0.5,131.5";

            //double[] predictedResult = new double[1];
            //ManualResetEvent mr = new(false);
            //object[] ALGParam = [BaseLocation, stringBuilder, mr, predictedResult];
            //PythonConfigHelper.AddAction((TestALG, ALGParam));
            //mr.WaitOne();

            base.OnInitialize();
        }

        /// <summary>
        /// 获取基地位置信息，格式为"基地:拉线:工序"，返回基地部分
        /// </summary>
        /// <param name="workShip"></param>
        /// <returns></returns>
        private string GetBaseLocation(string workShip)
        {
            if (string.IsNullOrEmpty(workShip)) return "Default";
            var parts = workShip.Split(':');
            return parts.Length > 0 ? parts[0] : "Default";
        }

        /// <summary>
        /// 初始化项目设置，如果不存在则添加默认设置
        /// </summary>
        private void InitProjectSettingConfig()
        {
            // 初始化项目设置
            if (!projectSettingRepository.Exists(m => m.BelongToVM == "VM_MeltingDepthDetectionPage" && m.Name == "WorkShip"))
            {
                var psm = new ProjectSettingModel("WorkShip", "VM_MeltingDepthDetectionPage", "惠州:PTO:PACK激光焊", "string", 0)
                {
                    Remark = "设备信息(基地:拉线:工序)"
                };
                projectSettingRepository.Add(psm);
            }
        }

        /// <summary>
        /// 初始化PLC配置，如果不存在则添加默认配置
        /// </summary>
        private void InitPLCConfig(string baseLocation)
        {
            var plcName = "设备PLC";
            if (plcConfigRepository.Exists(m => m.Name == plcName))
                return;

            var remark = string.Empty;
            var plcAgreement = string.Empty;
            switch (baseLocation)
            {
                case "惠州":
                    remark = "192.168.1.1";
                    plcAgreement = "Siemens_S7NetS1200";
                    break;

                case "枣庄":
                    plcAgreement = "Siemens_S7NetS1500";
                    break;

                default:
                    remark = "127.0.0.1";
                    break;
            }
            plcConfigRepository.Add(new PLCConfigModel(plcName, 1, "127.0.0.1", 102, plcAgreement, true)
            {
                Remark = remark
            });
        }

        /// <summary>
        /// 初始化PLC读写配置，如果不存在则添加默认配置
        /// </summary>
        private void InitPLCRWConfig(string baseLocation)
        {
            var plcConfig = plcConfigRepository.Get(m => m.Name == "设备PLC");
            if (plcConfig == null) return;

            string dbName;
            string startAddress;
            ushort length = 200;
            switch (baseLocation)
            {
                case "惠州":
                    dbName = "DB904.";
                    startAddress = "DBD0";
                    length = 16072;
                    break;

                case "枣庄":
                    dbName = "DB191.";
                    startAddress = "DBD0";
                    length = 200;
                    break;

                default:
                    dbName = "DB904.";
                    startAddress = "DBD0";
                    break;
            }

            if (!plcRWConfigRepository.Exists(m => m.Name == "AI采集"))
            {
                plcRWConfigRepository.Add(new PLCRWConfigModel("AI采集", plcConfig.ID, dbName, startAddress, length, "R", 1, true)
                {
                    Cycle = 100
                });
            }
        }

        /// <summary>
        /// 初始化PLC地址配置，如果不存在则添加默认配置
        /// </summary>
        /// <param name="baseLocation"></param>
        private void InitPLCAddressConfig(string baseLocation)
        {
            // 定义配置：参数名, 地址, 数据量, 数据长度, 数据类型, 地址前缀, 起始值
            var configs = new List<(string BaseName, string Address, int Count, int Step, string DataType, string AddressPrefix, int Start)>();

            switch (baseLocation)
            {
                case "惠州":
                    // 信号与 PACK 码
                    configs.Add(("数据采集信号", "DB904.DBD16034", 1, 1, "short", "DB904.DBD", 16034));
                    configs.Add(("数采完成信号", "DB904.DBD16036", 1, 1, "int", "DB904.DBD", 16036));
                    configs.Add(("PACK码", "DB904.DBD16040", 1, 24, "string", "DB904.DBD", 16040));
                    configs.Add(("离焦量", "DB904.DBD16022", 1, 1, "float", "DB904.DBD", 16022));
                    configs.Add(("焊接速度", "DB904.DBD12816", 1, 1, "short", "DB904.DBD", 12816));
                    configs.Add(("焊接点位", "DB904.DBD16038", 1, 1, "short", "DB904.DBD", 16038));
                    // 大量参数
                    configs.Add(("中心光输出功率", "DB904.DBD0", 400, 4, "float", "DB904.DBD", 0));
                    configs.Add(("铜嘴压力", "DB904.DBD3204", 400, 4, "float", "DB904.DBD", 3204));
                    configs.Add(("保护气流量", "DB904.DBD6408", 400, 4, "float", "DB904.DBD", 6408));
                    configs.Add(("除尘风速", "DB904.DBD9612", 400, 4, "float", "DB904.DBD", 9612));
                    configs.Add(("环形光输出功率", "DB904.DBD12818", 400, 4, "float", "DB904.DBD", 12818));
                    break;

                case "枣庄":
                    configs.Add(("左焊房AI采集信号", "DB191.DBX0.0", 1, 1, "int", "DBD191.DBD", 0));
                    configs.Add(("右焊房AI采集信号", "DB191.DBX2.0", 1, 1, "int", "DBD191.DBD", 2));
                    configs.Add(("左焊房PACK码", "DB191.DBX6.0", 1, 24, "string", "DBD191.DBD", 6));
                    configs.Add(("右焊房PACK码", "DB191.DBX58.0", 1, 24, "string", "DBD191.DBD", 58));
                    break;

                default:
                    break;
            }

            // 统一注册
            foreach (var (baseName, address, count, step, dataType, prefix, start) in configs)
            {
                if (count == 1)
                {
                    EnsureAddressConfig(baseName, dataType, (ushort)(dataType == "string" ? 24 : 1), address);
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        int offset = start + i * step;
                        string paramName = $"{baseName}_{i + 1}";
                        EnsureAddressConfig(paramName, dataType, 1, $"{prefix}{offset}");
                    }
                }
            }
        }

        /// <summary>
        /// 确保PLC地址配置存在，如果不存在则添加默认配置
        /// </summary>
        /// <param name="paramName"></param>
        /// <param name="dataType"></param>
        /// <param name="length"></param>
        /// <param name="address"></param>
        void EnsureAddressConfig(string paramName, string dataType, ushort length, string address)
        {
            var plcConfig = plcConfigRepository.Get(model => model.Name == "设备PLC");
            var plcRWConfig = plcRWConfigRepository.Get(model => model.Name == "AI采集");

            if (!plcAddressConfigRepository.Exists(m => m.ParameterName == paramName))
                plcAddressConfigRepository.Add
                (
                    new PLCAddressConfigModel(plcConfig?.ID ?? 1, plcRWConfig?.ID ?? 1, 1, paramName, dataType, length, address, true)
                );
        }

        /// <summary>
        /// 初始化项目设置，如果不存在则添加默认设置
        /// </summary>
        private void InitMESSettings()
        {
            EnsureSetting("GroupCode", "PHHJC1", "string", "工序代码");
            EnsureSetting("OperatorId", "61239793", "string", "SessionID");
            EnsureSetting("SnType", "cell", "string", "条码类型 cell:产品条码；tray:托盘号；group:产品组");
        }

        /// <summary>
        /// 确保项目设置存在，如果不存在则添加默认设置
        /// </summary>
        /// <param name="settingName"></param>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <param name="remark"></param>
        /// <param name="roleRank"></param>
        private void EnsureSetting(string settingName, string value, string type, string remark)
        {
            if (!mESSettingRepository.Exists(model => model.Key == settingName && model.ProfileName == "test"))
            {
                mESSettingRepository.Add
                (
                    new MESSettingModel()
                    {
                        Key = settingName,
                        Value = value,
                        Type = type,
                        Remark = remark,
                        ProfileName = "test"
                    }
                );
            }
        }

        /// <summary>
        /// 初始化MES服务
        /// </summary>
        private void InitMESServer()
        {
            Task.Run(async () =>
            {
                try
                {
                    logger.Info("开始初始化MES服务");
                    var testDataInitResult = await testDataService.InitializeAsync();
                    logger.Info($"[{testDataService.ServiceName}]获取测试数据服务初始化: {(testDataInitResult ? "成功" : "失败")}");
                }
                catch (Exception ex)
                {
                    logger.Error("MES服务初始化异常" + ex);
                }
            });
        }

        /// <summary>
        /// 信号监听/数据采集
        /// </summary>
        private ValueListener? LeftWeldingRoomListener, RightWeldingRoomListener, DataCollectionListener;

        /// <summary>
        /// 初始化PLC信号监听与循环读取
        /// </summary>
        /// <returns></returns>
        private void InitializePLCSigalListenerLoopAsync(string baseLocation)
        {
            switch (baseLocation)
            {
                case "惠州":
                    DataCollectionListener = new ValueListener() { Name = "数据采集信号", Value = "0" };
                    DataCollectionListener.OnValueChangedEven += OnPLCDataListenerValueChanged_HZ;

                    Task.Run(async () =>
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                DataCollectionListener.Value = GetAddressValue(DataCollectionListener.Name);
                                await Task.Delay(100, cts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (Exception ex)
                            {
                                logger.Error("PLC信号监听线程异常" + ex);
                            }
                        }
                    });
                    break;

                case "枣庄":
                    LeftWeldingRoomListener = new ValueListener() { Name = "左焊房AI采集信号", Value = "0" };
                    LeftWeldingRoomListener.OnValueChangedEven += OnPLCDataListenerValueChanged_ZZ;

                    RightWeldingRoomListener = new ValueListener() { Name = "右焊房AI采集信号", Value = "0" };
                    RightWeldingRoomListener.OnValueChangedEven += OnPLCDataListenerValueChanged_ZZ;

                    Task.Run(async () =>
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                LeftWeldingRoomListener.Value = GetAddressValue(LeftWeldingRoomListener.Name);
                                RightWeldingRoomListener.Value = GetAddressValue(RightWeldingRoomListener.Name);
                                await Task.Delay(100, cts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (Exception ex)
                            {
                                logger.Error("PLC信号监听线程异常" + ex);
                            }
                        }
                    });
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 枣庄PLC数据采集---AI信号触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnPLCDataListenerValueChanged_ZZ(object? sender, EventArgs<string> args)
        {
            if (sender is not ValueListener listener) return;
            if (args.OldValue == args.NewValue) return;

            var listenerName = listener.Name ?? string.Empty;
            if (args.NewValue != "1") return;

            logger.Info($"[{listenerName}]读取到数据信号：[{args.NewValue}]");

            var addressMap = pLCService.RWAddresses?.Values;
            if (addressMap == null) return;

            string targetParam = listenerName.Contains('右') ? "右焊房PACK码" : "左焊房PACK码";
            var packValue = addressMap?.FirstOrDefault(o => o.ParameterName == targetParam)?.MonitorValue;

            string packCode = packValue?.ToString() ?? "未知PACK码";

            logger.Info($"[{listenerName}] 触发数据读取，PACK码: {packCode}");

            if (!PACKCodeBC.IsAddingCompleted)
                PACKCodeBC.Add(packCode);
        }

        /// <summary>
        /// 惠州PLC数据采集---AI信号触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnPLCDataListenerValueChanged_HZ(object? sender, EventArgs<string> args)
        {
            if (sender is not ValueListener listener) return;
            if (args.OldValue == args.NewValue) return;
            if (args.NewValue != "1") return;

            logger.Info($"[{listener.Name}]读取到数据信号：[{args.NewValue}]");

            var addressMap = pLCService.RWAddresses?.Values;
            if (addressMap == null) return;

            string packCode = GetAddressValue("PACK码");
            int weldingPoint = int.TryParse(addressMap?.FirstOrDefault(o => o.ParameterName == "焊接点位")?.MonitorValue?.ToString(), out int wp) ? wp : 0;
            logger.Info($"获取到PACK码: {packCode}, 焊接点位: {weldingPoint}");

            double centerPower = GetDoubleAddressValue($"中心光输出功率_{weldingPoint}");
            double copperPressure = GetDoubleAddressValue($"铜嘴压力_{weldingPoint}");
            double gasFlow = GetDoubleAddressValue($"保护气流量_{weldingPoint}");
            double dustVelocity = GetDoubleAddressValue($"除尘风速_{weldingPoint}");
            double ringPower = GetDoubleAddressValue($"环形光输出功率_{weldingPoint}");
            double weldingSpeed = GetDoubleAddressValue("焊接速度");
            double defocusAmount = GetDoubleAddressValue("离焦量");

            try
            {
                var completeSignal = addressMap?.FirstOrDefault(o => o.ParameterName == "数采完成信号");
                if (completeSignal == null) { logger.Warn("未在PLC配置中找到参数名为[数采完成信号]的地址"); return; }

                int? plcId = pLCService.GetPLCIdForAddress(completeSignal.ID);
                if (!plcId.HasValue || !pLCService.PLCs.TryGetValue(plcId.Value, out var plc)) return;
                if (!await pLCService.EnsurePLCConnectedAsync(plc)) return;

                completeSignal.MonitorValue = 1;
                await pLCService.WriteAddressAsync(plc, completeSignal);

                logger.Info($"[数采完成信号下发成功] 数采完成信号已发送至 {plc.Name} 的 {completeSignal.Address} ");
            }
            catch (Exception ex)
            {
                logger.Error("数采完成信号下发失败\r\n" + ex);
            }

            double totalPower = centerPower + ringPower;
            string csvInput = $"{string.Join(",", hzParamNames)}\n{centerPower},{copperPressure},{gasFlow},{dustVelocity},{ringPower},{weldingSpeed},{defocusAmount},{totalPower}";
            logger.Info($"[模型输入数据]：{csvInput} ");

            // 算法调用
            double[] PYResult = new double[1];
            using (var mr = new ManualResetEvent(false))
            {
                object[] ALGParam = [BaseLocation, csvInput, mr, PYResult];
                PythonConfigHelper.AddAction((TestALG, ALGParam));
                await Task.Run(mr.WaitOne);
            }

            if (double.IsNaN(PYResult[0]))
            {
                logger.Error($"PACK码: [{packCode}] 算法执行失败，跳过该记录");
                return;
            }

            var record = new MeltingDepthDetectionModel
            {
                ID = Interlocked.Increment(ref nextId),
                TimeDate = DateTime.Now,
                PackCode = packCode?.ToString() ?? "",
                CenterOpticalPower = centerPower,
                CopperNozzlePressure = GetDoubleAddressValue($"铜嘴压力_{weldingPoint}"),
                ProtectiveGasFlow = GetDoubleAddressValue($"保护气流量_{weldingPoint}"),
                DustRemovalAirVelocity = GetDoubleAddressValue($"除尘风速_{weldingPoint}"),
                WeldingSpeed = GetDoubleAddressValue("焊接速度"),
                RingOpticalPower = ringPower,
                DefocusAmount = GetDoubleAddressValue("离焦量"),
                TotalPower = totalPower,
                DepthValue = PYResult[0],
                IsOk = Math.Abs(PYResult[0] - TargetValue) <= Tolerance
            };

            await RunOnUIThreadAsync(() =>
            {
                Records.Add(record);
                while (Records.Count > 100) Records.RemoveAt(0);
            });

            SaveRecordToCSV(record);
        }

        #region PACK码消费处理

        /// <summary>
        /// 对PACK码进行处理，获取MES数据，生成CSV文件
        /// </summary>
        /// <returns></returns>
        void HandlePACKCode(CancellationToken token)
        {
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        string packCode = PACKCodeBC.Take(token);
                        await ProcessSinglePackCodeAsync(packCode);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (InvalidOperationException) when (PACKCodeBC.IsAddingCompleted)
                    {
                        // 队列已标记完成，正常退出
                        break;
                    }
                }
            }, token);
        }

        private async Task ProcessSinglePackCodeAsync(string packCode)
        {
            try
            {
                logger.Info($"开始处理PACK码: {packCode}");

                var mesData = await GetMESDataWithRetryAsync(packCode);
                if (mesData?.TestResultDetails == null || mesData.TestResultDetails.Count == 0)
                {
                    logger.Warn($"PACK码 [{packCode}] 未获取到有效MES数据，跳过");
                    return;
                }

                // 构建 CSV 格式的输入数据
                string csvInput = BuildCsvInput(mesData);

                // 用于接收算法结果的容器                   
                double[] PYResult = [double.NaN];

                using (var mr = new ManualResetEvent(false))
                {
                    object[] ALGParam = [BaseLocation, csvInput, mr, PYResult];
                    PythonConfigHelper.AddAction((TestALG, ALGParam));
                    await Task.Run(() => mr.WaitOne());
                }

                if (double.IsNaN(PYResult[0]))
                {
                    logger.Error($"PACK码: [{packCode}] 算法执行失败，跳过该记录");
                    return;
                }

                double predictedDepth = PYResult[0];

                var record = CreateMeltingRecord(packCode, predictedDepth, mesData);
                await RunOnUIThreadAsync(() =>
                {
                    Records.Add(record);
                    while (Records.Count > 100) Records.RemoveAt(0);
                });

                SaveRecordToCSV(record);
                logger.Info($"PACK码 [{packCode}] 处理完成，预测熔深: {predictedDepth:F4}");
            }
            catch (Exception ex)
            {
                logger.Error($"处理PACK码 [{packCode}] 时发生异常" + ex);
            }
        }

        /// <summary>
        /// 构建 CSV 输入字符串，用于传递给 Python 算法
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private string BuildCsvInput(GetTestDataResponse data)
        {
            if (data?.TestResultDetails == null)
                throw new ArgumentNullException(nameof(data), "TestResultDetails 不能为 null");

            var details = data.TestResultDetails
                .Where(d => !string.IsNullOrEmpty(d.ParamName))
                .ToList();

            string headerLine = string.Join(",", details.Select(d => d.ParamName));
            string valueLine = string.Join(",", details.Select(d => d.ParamValue ?? ""));
            return headerLine + "\n" + valueLine;
        }

        /// <summary>
        /// 从PLC地址配置中获取指定参数的数值，并尝试解析为 double 类型
        /// </summary>
        /// <param name="paramName"></param>
        /// <returns></returns>
        private double GetDoubleAddressValue(string paramName)
        {
            var valueStr = GetAddressValue(paramName);
            return double.TryParse(valueStr, out double val) ? val : 0.0;
        }

        /// <summary>
        /// 从PLC地址配置中获取指定参数的数值，返回字符串形式；若未找到则返回 "0"
        /// </summary>
        /// <param name="paramName"></param>
        /// <returns></returns>
        private string GetAddressValue(string paramName)
        {
            var item = pLCService.RWAddresses?.Values?
                .FirstOrDefault(o => o.ParameterName == paramName);
            return item?.MonitorValue?.ToString() ?? "0";
        }

        /// <summary>
        /// 根据 PACK 码、预测熔深和 MES 数据创建熔化深度检测记录
        /// </summary>
        /// <param name="packCode"></param>
        /// <param name="predictedDepth"></param>
        /// <param name="mesData"></param>
        /// <returns></returns>
        private MeltingDepthDetectionModel CreateMeltingRecord(string packCode, double predictedDepth, GetTestDataResponse mesData)
        {
            return new MeltingDepthDetectionModel
            {
                ID = Interlocked.Increment(ref nextId), // 线程安全的自增
                TimeDate = DateTime.Now,
                PackCode = packCode,
                DefocusAmount = GetParamValue(mesData, "离焦量"),
                ProtectiveGasFlow = GetParamValue(mesData, "保护气流量"),
                WeldingEnergyAverage = GetParamValue(mesData, "焊接能量平均值"),
                WeldingEnergyMax = GetParamValue(mesData, "焊接能量最大值"),
                WeldingEnergyMin = GetParamValue(mesData, "焊接能量最小值"),
                WeldingSpeed = GetParamValue(mesData, "焊接速度"),
                WeldingTensionPressure = GetParamValue(mesData, "焊接张力压力"),
                FirstOscillationAmplitude = GetParamValue(mesData, "首次振幅"),
                FirstOscillationFrequency = GetParamValue(mesData, "首次频率"),
                SecondOscillationAmplitude = GetParamValue(mesData, "二次振幅"),
                DepthValue = predictedDepth,
                IsOk = Math.Abs(predictedDepth - TargetValue) <= Tolerance
            };
        }

        /// <summary>
        /// 从 MES 响应中提取指定参数的数值
        /// </summary>
        /// <remarks>对找到的参数使用 double.TryParse 解析为 double；若未找到或解析失败则返回默认值 0.0。</remarks>
        /// <param name="data">包含测试结果明细的 MES 响应，预期通过 TestResultDetails 查找参数。</param>
        /// <param name="paramName">要提取的参数名。</param>
        /// <returns>参数的数值；若不存在或无法解析，则返回 0.0。</returns>
        private double GetParamValue(GetTestDataResponse data, string paramName)
        {
            var detail = data.TestResultDetails?.FirstOrDefault(d => d.ParamName == paramName);
            if (detail != null && double.TryParse(detail.ParamValue?.ToString(), out double val))
                return val;
            return 0.0;
        }

        /// <summary>
        /// 将熔深检测记录保存为 CSV 文件
        /// </summary>
        /// <param name="record"></param>
        private void SaveRecordToCSV(MeltingDepthDetectionModel record)
        {
            try
            {
                string directory = $"../熔深预测结果/{DateTime.Now.Year}年/{DateTime.Now.Month}月/{DateTime.Now.Day}号";
                string header;
                string line;

                if (BaseLocation == "惠州")
                {
                    header = "序号,时间,PACK码,中心光输出功率,铜嘴压力,保护气流量,除尘风速,焊接速度,环形光输出功率,离焦量,功率,预测熔深,判定结果";

                    line = $"{record.ID},{record.TimeDate:yyyy-MM-dd HH:mm:ss},{record.PackCode}," +
                           $"{record.CenterOpticalPower},{record.CopperNozzlePressure}," +
                           $"{record.ProtectiveGasFlow},{record.DustRemovalAirVelocity}," +
                           $"{record.WeldingSpeed},{record.RingOpticalPower},{record.DefocusAmount},{record.TotalPower}," +
                           $"{record.DepthValue},{(record.IsOk ? "OK" : "NG")}";
                }
                else // 枣庄模式
                {
                    header = "PACK条码,序号,时间,离焦量,保护气流量,焊接能量平均值,焊接能量最大值," +
                           "焊接能量最小值,焊接速度,焊接位压力值,第一摆动幅度,第一摆动频率," +
                           "第二摆动幅度,熔深阈值下限,熔深阈值上限,预测熔深,判定结果";

                    line = $"{record.PackCode},{record.ID},{record.TimeDate:yyyy-MM-dd HH:mm:ss}," +
                           $"{record.DefocusAmount},{record.ProtectiveGasFlow},{record.WeldingEnergyAverage},{record.WeldingEnergyMax}," +
                           $"{record.WeldingEnergyMin},{record.WeldingSpeed},{record.WeldingTensionPressure}," +
                           $"{record.FirstOscillationAmplitude},{record.FirstOscillationFrequency},{record.SecondOscillationAmplitude}," +
                           $"{TargetValue - Tolerance},{TargetValue + Tolerance},{record.DepthValue},{(record.IsOk ? "OK" : "NG")}";
                }

                lock (CsvSaveHelper.FileObj)
                    CsvSaveHelper.SaveCSVFile(directory, $"{record.PackCode}_熔深预测结果", header, line);
            }
            catch (Exception ex)
            {
                logger.Error("CSV文件保存失败" + ex);
            }
        }

        #endregion

        #region MES 数据获取        

        /// <summary>
        /// 带简单重试逻辑的MES数据获取
        /// </summary>
        private async Task<GetTestDataResponse> GetMESDataWithRetryAsync(string packCode)
        {
            try
            {
                var response = await testDataService.GetTestDataAsync(packCode)
                               ?? new GetTestDataResponse { Message = "接口无响应", Success = false };

                if (response.Success)
                {
                    logger.Info($"测试数据获取成功，数据条数: {response.TestResultDetails?.Count ?? 0}");
                    return response;
                }

                logger.Error($"测试数据获取失败：[{response.Message}]，尝试重新初始化服务...");

                // 仅尝试一次重新初始化
                if (!testDataService.IsEnabled)
                {
                    var initResult = await testDataService.InitializeAsync();
                    if (initResult)
                    {
                        logger.Info("MES服务重新初始化成功，再次尝试获取数据");
                        response = await testDataService.GetTestDataAsync(packCode)
                                   ?? new GetTestDataResponse { Message = "二次请求无响应", Success = false };
                        if (response.Success) return response;
                    }
                }

                logger.Error($"最终获取PACK码 [{packCode}] 数据失败");
                return new GetTestDataResponse { Message = "获取失败", Success = false };
            }
            catch (Exception ex)
            {
                logger.Error($"获取MES数据异常: {packCode}", ex);
                return new GetTestDataResponse { Message = ex.Message, Success = false };
            }
        }


        #endregion

        #region Python 算法调用

        private void TestALG(object[] obj)
        {
            logger.Info("开始执行预测算法");
            string? baseLocation = obj?.Length > 0 ? obj[0] as string : null;
            string? paramStr = obj?.Length > 1 ? obj[1] as string : null;
            ManualResetEvent? mr = obj?.Length > 2 ? obj[2] as ManualResetEvent : null;
            double[]? resultArray = obj?.Length > 3 ? obj[3] as double[] : null;

            try
            {
                using (Py.GIL())
                {
                    if (!Directory.Exists("./PythonScripts"))
                        Directory.CreateDirectory("./PythonScripts");

                    if (!File.Exists($".\\PythonScripts\\{baseLocation}PACK激光焊熔深预测.py"))
                    {
                        logger.Warn("算法文件不存在");
                        return;
                    }
                    dynamic demo = Py.Import($"{baseLocation}PACK激光焊熔深预测");
                    dynamic stander = demo.Stander();
                    dynamic predictor = demo.Predictor(paramStr, stander, modelPath);
                    dynamic pre_y = predictor.pre_by_pack();
                    double result = (double)pre_y[0];

                    // 将结果写入外部容器
                    if (resultArray != null && resultArray.Length > 0)
                        resultArray[0] = result;
                    logger.Info($"算法执行完成 [结果: {result}]");
                }
            }
            catch (Exception ex)
            {
                logger.Error("Python算法调用异常" + ex);
            }
            finally
            {
                mr?.Set();
            }
        }

        #endregion

        #region 判定与UI更新

        /// <summary>
        /// 判定逻辑：|实际 - 规格| <= 公差
        /// </summary>
        private void UpdateAllStatuses()
        {
            if (Records == null) return;
            foreach (var record in Records)
            {
                record.IsOk = Math.Abs(record.DepthValue - TargetValue) <= Tolerance;
            }
        }

        #endregion

        #region 日志

        public ObservableCollection<AppLogModel> AppLogs { get; set; } = [];

        /// <summary>
        /// 当log更新时触发
        /// </summary>
        /// <param name="logModel"></param>
        private async void OnLoggerReport(AppLogModel logModel)
        {
            await RunOnUIThreadAsync(() =>
            {
                AppLogs.Insert(0, logModel);
                if (AppLogs.Count > 300) AppLogs.RemoveAt(AppLogs.Count - 1);
            });
        }

        #endregion
    }
}
