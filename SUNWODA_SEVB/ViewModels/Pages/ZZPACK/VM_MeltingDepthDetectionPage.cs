using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
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
using SUNWODA_SEVB.Core.Models.Project.ZZPACK;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.ViewModels.Pages.ZZPACK
{
    [Module("VM_MeltingDepthDetectionPage", "焊接过程预警系统V1.0", Order = 0)]
    internal class VM_MeltingDepthDetectionPage : ViewModelBase
    {
        private readonly ILoggerService<VM_MeltingDepthDetectionPage> logger;
        private readonly IGlobalSettingRepository globalSettingRepository;
        private readonly IMESSettingRepository mESSettingRepository;
        private readonly IPLCService plcService;
        private readonly IPLCConfigRepository plcConfigRepository;
        private readonly IPLCRWConfigRepository plcRWConfigRepository;
        private readonly IPLCAddressConfigRepository plcAddressConfigRepository;
        private readonly ITestDataService testDataService;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_MeltingDepthDetectionPage
        (
            ILoggerService<VM_MeltingDepthDetectionPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IMESSettingRepository mESSettingRepository,
            IPLCService pLCService,
            IPLCConfigRepository plcConfigRepository,
            IPLCRWConfigRepository plcRWConfigRepository,
            IPLCAddressConfigRepository plcAddressConfigRepository,
            ITestDataService testDataService
        )
        {
            this.logger = logger;
            this.globalSettingRepository = globalSettingRepository;
            this.mESSettingRepository = mESSettingRepository;
            this.plcService = pLCService;
            this.plcConfigRepository = plcConfigRepository;
            this.plcRWConfigRepository = plcRWConfigRepository;
            this.plcAddressConfigRepository = plcAddressConfigRepository;
            this.testDataService = testDataService;

            logger.OnLoggerReportEvent += OnLoggerReport;
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
            //stringBuilder = ("离焦量,保护气流量,焊接能量最大值,焊接速度\n");
            //stringBuilder = stringBuilder + "-1,20.1208,4809,80";
            //double[] predictedResult = new double[1];
            //ManualResetEvent mr = new(false);
            //object[] ALGParam = [stringBuilder, mr, predictedResult];
            //PythonConfigHelper.AddAction((TestALG, ALGParam));
            //mr.WaitOne();
        }

        /// <summary>
        /// 页面初始化时，确保PLC配置、读写配置和地址配置存在，并启动PLC信号监听循环
        /// </summary>
        public override void OnInitialize()
        {
            InitPLCConfig();
            InitPLCRWConfig();
            InitPLCAddressConfig();
            InitMESSettings();
            InitMESServer();
            InitializePLCSigalListenerLoopAsync();
            HandlePACKCode();

            base.OnInitialize();
        }

        /// <summary>
        /// 初始化PLC配置，如果不存在则添加默认配置
        /// </summary>
        private void InitPLCConfig()
        {
            if (!plcConfigRepository.Exists(model => model.Name == "设备PLC"))
                plcConfigRepository.Add(new PLCConfigModel("设备PLC", 1, "127.0.0.1", 102, "Siemens_S7NetS1500", true));
        }

        /// <summary>
        /// 初始化PLC读写配置，如果不存在则添加默认配置
        /// </summary>
        private void InitPLCRWConfig()
        {
            if (!plcRWConfigRepository.Exists(model => model.Name == "AI采集"))
            {
                var plcConfig = plcConfigRepository.Get(model => model.Name == "设备PLC");
                plcRWConfigRepository.Add(new PLCRWConfigModel("AI采集", plcConfig?.ID ?? 1, "DB191.", "DBX0", 200, "R", 1, true) { Cycle = 100 });
            }
        }

        /// <summary>
        /// 初始化PLC地址配置，如果不存在则添加默认配置
        /// </summary>
        private void InitPLCAddressConfig()
        {
            var plcConfig = plcConfigRepository.Get(model => model.Name == "设备PLC");
            var plcRWConfig = plcRWConfigRepository.Get(model => model.Name == "AI采集");

            if (!plcAddressConfigRepository.Exists(model => model.ParameterName == "左焊房AI采集信号"))
                plcAddressConfigRepository.Add(new PLCAddressConfigModel(plcConfig?.ID ?? 1, plcRWConfig?.ID ?? 1, 1, "左焊房AI采集信号", "int", 1, "DB191.DBX0.0", true));

            if (!plcAddressConfigRepository.Exists(model => model.ParameterName == "右焊房AI采集信号"))
                plcAddressConfigRepository.Add(new PLCAddressConfigModel(plcConfig?.ID ?? 1, plcRWConfig?.ID ?? 1, 1, "右焊房AI采集信号", "int", 1, "DB191.DBX2.0", true));

            if (!plcAddressConfigRepository.Exists(model => model.ParameterName == "左焊房PACK码"))
                plcAddressConfigRepository.Add(new PLCAddressConfigModel(plcConfig?.ID ?? 1, plcRWConfig?.ID ?? 1, 1, "左焊房PACK码", "string", 17, "DB191.DBX6.0", true));

            if (!plcAddressConfigRepository.Exists(model => model.ParameterName == "右焊房PACK码"))
                plcAddressConfigRepository.Add(new PLCAddressConfigModel(plcConfig?.ID ?? 1, plcRWConfig?.ID ?? 1, 1, "右焊房PACK码", "string", 24, "DB191.DBX58.0", true));
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
                    logger.Error("MES服务初始化异常", ex);
                }
            });
        }

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

        /// <summary>
        /// 清除日志命令
        /// </summary>
        public ICommand ClearLogsCommand => new RelayCommand(async () =>
        {
            AppLogs.Clear();
        });

        #endregion 

        /// <summary>
        /// 信号监听/数据采集
        /// </summary>
        private ValueListener? LeftWeldingRoomListener, RightWeldingRoomListener;

        /// <summary>
        /// 初始化PLC信号监听与循环读取
        /// </summary>
        /// <returns></returns>
        private void InitializePLCSigalListenerLoopAsync()
        {
            LeftWeldingRoomListener = new ValueListener() { Name = "左焊房AI采集信号", Value = "0" };
            LeftWeldingRoomListener.OnValueChangedEven += OnPLCDataListenerValueChanged;

            RightWeldingRoomListener = new ValueListener() { Name = "右焊房AI采集信号", Value = "0" };
            RightWeldingRoomListener.OnValueChangedEven += OnPLCDataListenerValueChanged;

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        LeftWeldingRoomListener.Value = plcService.RWAddresses?.Values?.FirstOrDefault(o => o.ParameterName == LeftWeldingRoomListener.Name)?.MonitorValue?.ToString() ?? "0";
                        RightWeldingRoomListener.Value = plcService.RWAddresses?.Values?.FirstOrDefault(o => o.ParameterName == RightWeldingRoomListener.Name)?.MonitorValue?.ToString() ?? "0";

                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("PLC信号监听线程异常", ex);
                    }
                }
            });
        }

        /// <summary>
        /// PACK码生产-消费队列
        /// </summary>
        BlockingCollection<string> PACKCodeBC { get; set; } = [];

        /// <summary>
        /// PLC数据采集---AI信号触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnPLCDataListenerValueChanged(object? sender, EventArgs<string> args)
        {
            if (sender is not ValueListener listener) return;

            string listenerName = listener.Name ?? string.Empty;

            if (args.OldValue == args.NewValue) return;

            logger.Info($"[{listenerName}]读取到数据信号：[{args.NewValue}]");

            if (args.NewValue == "0") return;

            if (args.NewValue == "1")
            {
                var addressMap = plcService.RWAddresses?.Values;

                string? targetParam = listenerName.Contains('右') ? "右焊房PACK码" : "左焊房PACK码";
                var packValue = addressMap?.FirstOrDefault(o => o.ParameterName == targetParam)?.MonitorValue;

                string packCode = packValue?.ToString() ?? "未知PACK码";

                logger.Info($"[{listenerName}] 触发数据读取，PACK码: {packCode}");

                PACKCodeBC.Add(packCode);
            }
        }

        readonly string CsvFileHeader = "PACK条码,序号,时间,PACK码,离焦量,保护气流量,焊接能量平均值,焊接能量最大值," +
                           "焊接能量最小值,焊接速度,焊接位压力值,第一摆动幅度,第一摆动频率," +
                           "第二摆动幅度,熔深阈值下限,熔深阈值上限,预测熔深,判定结果";
        private int nextId = 1;

        /// <summary>
        /// 对PACK码进行处理，获取MES数据，生成CSV文件
        /// </summary>
        /// <returns></returns>
        void HandlePACKCode()
        {
            Task.Run(async () =>
            {
                try
                {
                    foreach (var item in PACKCodeBC.GetConsumingEnumerable())
                    {
                        logger.Info($"开始处理PACK码: {item}");
                        var PACKCodeData = await GetMESData(item);

                        if (PACKCodeData?.TestResultDetails == null || PACKCodeData.TestResultDetails.Count == 0)
                            continue;

                        // 1. 构建无尾部逗号的 CSV 字符串
                        var headers = new List<string>();
                        var values = new List<string>();
                        foreach (var temp in PACKCodeData.TestResultDetails)
                        {
                            if (temp?.ParamName != null)
                            {
                                headers.Add(temp.ParamName);
                                values.Add(temp.ParamValue ?? "");
                            }
                        }

                        string csvData = string.Join(",", headers) + "\n" + string.Join(",", values);

                        // 用于接收算法结果的容器                   
                        double[] PYResult = [double.NaN];

                        using (var mr = new ManualResetEvent(false))
                        {
                            object[] ALGParam = [csvData, mr, PYResult];
                            PythonConfigHelper.AddAction((TestALG, ALGParam));
                            await Task.Run(() => mr.WaitOne());
                        }

                        if (double.IsNaN(PYResult[0]))
                        {
                            logger.Error($"PACK码 [{item}] 算法执行失败，跳过该记录");
                            continue;
                        }

                        double predictedDepth = PYResult[0];

                        var record = new MeltingDepthDetectionModel
                        {
                            ID = nextId++,
                            TimeDate = DateTime.Now,
                            PackCode = item,

                            // 从 MES 参数中取值（以下名称需与 MES 实际参数名完全一致）
                            DefocusAmount = GetParamValue(PACKCodeData, "离焦量"),
                            ProtectiveGasFlow = GetParamValue(PACKCodeData, "保护气流量"),
                            WeldingEnergyAverage = GetParamValue(PACKCodeData, "焊接能量平均值"),
                            WeldingEnergyMax = GetParamValue(PACKCodeData, "焊接能量最大值"),
                            WeldingEnergyMin = GetParamValue(PACKCodeData, "焊接能量最小值"),
                            WeldingSpeed = GetParamValue(PACKCodeData, "焊接速度"),
                            WeldingTensionPressure = GetParamValue(PACKCodeData, "焊接位压力值"),
                            FirstOscillationAmplitude = GetParamValue(PACKCodeData, "第一摆动幅度"),
                            FirstOscillationFrequency = GetParamValue(PACKCodeData, "第一摆动频率"),
                            SecondOscillationAmplitude = GetParamValue(PACKCodeData, "第二摆动幅度"),
                            DepthValue = predictedDepth,
                            IsOk = Math.Abs(predictedDepth - TargetValue) <= Tolerance
                        };

                        await RunOnUIThreadAsync(() =>
                        {
                            Records.Add(record);
                            while (Records.Count > 100)
                                Records.RemoveAt(0);
                        });

                        StringBuilder content = new();
                        content.Append($"{record.PackCode},");
                        content.Append($"{record.ID},");
                        content.Append($"{record.TimeDate:yyyy-MM-dd HH:mm:ss},");
                        content.Append($"{record.PackCode},");
                        content.Append($"{record.DefocusAmount},");
                        content.Append($"{record.ProtectiveGasFlow},");
                        content.Append($"{record.WeldingEnergyAverage},");
                        content.Append($"{record.WeldingEnergyMax},");
                        content.Append($"{record.WeldingEnergyMin},");
                        content.Append($"{record.WeldingSpeed},");
                        content.Append($"{record.WeldingTensionPressure},");
                        content.Append($"{record.FirstOscillationAmplitude},");
                        content.Append($"{record.FirstOscillationFrequency},");
                        content.Append($"{record.SecondOscillationAmplitude},");
                        content.Append($"{TargetValue - Tolerance},");
                        content.Append($"{TargetValue + Tolerance},");
                        content.Append($"{record.DepthValue},");
                        content.Append($"{(record.IsOk ? "OK" : "NG")}");

                        lock (CsvSaveHelper.FileObj)
                        {
                            CsvSaveHelper.SaveCSVFile
                            (
                                $"../熔深预测结果/{DateTime.Now.Year}年/{DateTime.Now.Month}月/{DateTime.Now.Day}号",
                                "熔深预测结果",
                                CsvFileHeader,
                                content.ToString()
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("[HandlePACKCode] 方法异常", ex);
                }
            });
        }

        /// <summary>
        /// 根据PACK码获取MES数据
        /// </summary>
        /// <param name="packCode"></param>
        /// <returns></returns>
        private async Task<GetTestDataResponse> GetMESData(string packCode)
        {
            try
            {
                // 检查服务是否已启用
                if (!testDataService.IsEnabled)
                {
                    logger.Error($"[{testDataService.ServiceName}] 服务未启用");

                    logger.Info($"尝试重新初始化[{testDataService.ServiceName}]服务...");
                    var initResult = await testDataService.InitializeAsync();
                    if (!initResult)
                    {
                        logger.Error($"[{testDataService.ServiceName}] 服务初始化失败!");
                    }
                    else
                    {
                        logger.Info($"[{testDataService.ServiceName}] 服务初始化成功!");
                    }
                }

                var response = await testDataService.GetTestDataAsync(packCode)
                               ?? new GetTestDataResponse() { Message = "接口无响应", Success = false };

                if (response.Success)
                    logger.Info($"测试数据获取成功，数据条数: {response.TestResultDetails?.Count ?? 0}");
                else
                    logger.Error($"测试数据获取失败：[{response.Message}]");

                return response;
            }
            catch (Exception ex)
            {
                logger.Error($"[GetMESData] 方法异常获取MES数据失败!", ex);
                return new GetTestDataResponse() { Message = "接口调用错误" };
            }
        }

        private void TestALG(object[] obj)
        {
            logger.Info("开始执行预测算法");
            string? paramStr = obj?.Length > 0 ? obj[0] as string : null;
            ManualResetEvent? mr = obj?.Length > 1 ? obj[1] as ManualResetEvent : null;
            double[]? resultArray = obj?.Length > 2 ? obj[2] as double[] : null;

            try
            {
                using (Py.GIL())
                {
                    if (!Directory.Exists("./PythonScripts"))
                        Directory.CreateDirectory("./PythonScripts");

                    if (!File.Exists(".\\PythonScripts\\枣庄PACK激光焊熔深预测.py"))
                    {
                        logger.Warn("算法文件不存在");
                        return;
                    }
                    dynamic demo = Py.Import("枣庄PACK激光焊熔深预测");
                    dynamic stander = demo.Stander();
                    dynamic predictor = demo.Predictor(paramStr, stander);
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
                logger.Error("Python算法调用异常", ex);
            }
            finally
            {
                mr?.Set();
            }
        }

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
