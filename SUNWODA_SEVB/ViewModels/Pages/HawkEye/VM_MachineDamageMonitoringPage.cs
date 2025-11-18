using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Joins;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Dm.util;
using Fleck;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HelixToolkit.Wpf;
using Newtonsoft.Json;
using OpenCvSharp;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.HawkEye;
using SUNWODA_SEVB.Core.Models.Web;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.SocketHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.HawkEye;
using TrajectoryDiagnosisLib;
using Yolo8Core;
using static SUNWODA_SEVB.ViewModels.Dialogs.HawkEye.VM_AddCameraDialog;
using static Yolo8Core.BaseClass;
using Path = System.IO.Path;

namespace SUNWODA_SEVB.ViewModels.Pages.HawkEye
{
    [Module("VM_MachineDamageMonitoringPage", "鹰眼机损监测", Order = 0)]
    class VM_MachineDamageMonitoringPage : ViewModelBase
    {
        private readonly ILoggerService<VM_MachineDamageMonitoringPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_MachineDamageMonitoringPage(
            ILoggerService<VM_MachineDamageMonitoringPage> logger,
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

            Init_Process();
        }

        /// <summary>
        /// 初始化项目字段
        /// </summary>
        public override void OnInitialize()
        {
            // 电芯码监控路径
            if (!_projectSettingRepository.Exists(m => m.BelongToVM == "VM_MachineDamageMonitoringPage" && m.Name == "BarCodeWatchPath"))
            {
                var psm = new ProjectSettingModel("BarCodeWatchPath", "VM_MachineDamageMonitoringPage", "D:\\Users\\2210120078\\Desktop\\新建文件夹", "string", 0);
                psm.Remark = "电芯码监控路径（修改后需重启软件后加载！）";
                _projectSettingRepository.Add(psm);
            }
            // 监控服务监听端口
            if (!_projectSettingRepository.Exists(m => m.BelongToVM == "VM_MachineDamageMonitoringPage" && m.Name == "ServerPort"))
            {
                var psm = new ProjectSettingModel("ServerPort", "VM_MachineDamageMonitoringPage", "8000", "int", 0);
                psm.Remark = "监控服务监听端口（修改后需重启软件后加载！）";
                _projectSettingRepository.Add(psm);
            }
            base.OnInitialize();
        }

        #region 主流程
        #region 公共参数
        private string barCode = "";
        private string modelPath = "";
        private string modelPath1 = "";
        private string modelPath2 = "";
        private string modelPath3 = "";
        private byte[] dllBytes = new byte[0];

        private readonly JsonSerializerOptions _jsonOpt = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly string[] categories = { "电芯", "夹爪", "载具" };


        #endregion

        /// <summary>
        /// 初始化进程
        /// </summary>
        private async void Init_Process()
        {
            var barCodeWatchPath = _projectSettingRepository.GetByNameAndVM("BarCodeWatchPath", "VM_MachineDamageMonitoringPage")?.Value ?? ""; ;
            InitBarCodeWatcher(barCodeWatchPath);
            var serverPort = _projectSettingRepository.GetByNameAndVM("ServerPort", "VM_MachineDamageMonitoringPage")?.Value ?? "8000"; ;
            _ = InitServerAsync(int.Parse(serverPort));
            //WebSocketInit(8181);
            StartServer(8181);
            //SearchDevices_Thread();

            // 从web获取AI算法模型文件onnx  —— TODO
            //modelPath = await DownloadOnnxAsync("ZZ_YY_C01_DP_DLL", "ZZ_YY", "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCQiNCeYgNIL35V99cJ0giEOtOCd6tf9fVEVCuFmFu0zuskPJYB0D1pUcYjLtJfadGa1HflWYINl/zk9bq0fDipuz5vPkswsp0N4efiVzwoBbzUTpLPePNkpb6rDyd+3RUNAC8UtZU2ptKGCNVvvgMQGZp+2mnIKPamuEXzK5qkkwIDAQAB");
            //modelPath1 = await DownloadOnnxAsync("ZZ_YY_C01_DP_DLL", "ZZ_YY", "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCQiNCeYgNIL35V99cJ0giEOtOCd6tf9fVEVCuFmFu0zuskPJYB0D1pUcYjLtJfadGa1HflWYINl/zk9bq0fDipuz5vPkswsp0N4efiVzwoBbzUTpLPePNkpb6rDyd+3RUNAC8UtZU2ptKGCNVvvgMQGZp+2mnIKPamuEXzK5qkkwIDAQAB");
            modelPath = "../Configs/best.onnx";
            modelPath1 = "../Configs/best[托盘].onnx";
            modelPath2 = "../Configs/best[PTO叠片粘电芯].onnx";
            modelPath3 = "../Configs/best_qufangliao.onnx";

            // 从web获取AI视频流算法库文件dll  —— TODO
            //dllBytes = await DownloadDllAsync("ZZ_YY_C01_DP_DLL", "ZZ_YY", "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCQiNCeYgNIL35V99cJ0giEOtOCd6tf9fVEVCuFmFu0zuskPJYB0D1pUcYjLtJfadGa1HflWYINl/zk9bq0fDipuz5vPkswsp0N4efiVzwoBbzUTpLPePNkpb6rDyd+3RUNAC8UtZU2ptKGCNVvvgMQGZp+2mnIKPamuEXzK5qkkwIDAQAB");
            CamerasMonitor_Thread();
        }

        /// <summary>
        /// 相机检测：检测流程自动开启关闭
        /// </summary>
        private void CamerasMonitor_Thread()
        {
            // 相机缓存字典
            var videoCaptureCache = new Dictionary<string, VideoCaptureHelper>();
            // 机损检测线程缓存字典
            var machineDamageMonitoringCache = new Dictionary<string, CancellationTokenSource>();
            var t = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        var cameras = GetCameras();
                        foreach (var camera in cameras ?? new List<CameraModel>())
                        {
                            // 未开启则断开相机、取消机损检测
                            if (!camera.Enable)
                            {
                                // 关闭相机
                                if (videoCaptureCache.ContainsKey(camera.GuidId))
                                {
                                    _logger.Info($"[{camera.Name} ip:{camera.HostIP}]正在断开相机连接...");
                                    videoCaptureCache[camera.GuidId].DisConnect();
                                    videoCaptureCache.Remove(camera.GuidId);
                                    _logger.Info($"[{camera.Name} ip:{camera.HostIP}]已断开相机连接！");
                                }
                                // 关闭机损检测任务
                                if (machineDamageMonitoringCache.ContainsKey(camera.GuidId))
                                {
                                    _logger.Info($"[{camera.Name} ip:{camera.HostIP}]正在停止机损检测任务...");
                                    machineDamageMonitoringCache[camera.GuidId]?.Cancel();
                                    machineDamageMonitoringCache.Remove(camera.GuidId);
                                    _logger.Info($"[{camera.Name} ip:{camera.HostIP}]已停止机损检测任务！");
                                }
                                continue;
                            }
                            // 连接相机
                            if (!videoCaptureCache.ContainsKey(camera.GuidId))
                            {
                                var videoCapture = new VideoCaptureHelper(_logger, camera.Name, $"{camera.ConnectionString}");
                                videoCapture.Connect();
                                videoCaptureCache[camera.GuidId] = videoCapture;
                            }
                            // 启动流程
                            if (!machineDamageMonitoringCache.ContainsKey(camera.GuidId))
                            {
                                var cts = new CancellationTokenSource();
                                MachineDamageMonitoring_Thread(camera.Workstaion, videoCaptureCache[camera.GuidId], cts.Token);
                                machineDamageMonitoringCache[camera.GuidId] = cts;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"相机&机损任务监控线程异常！", ex);
                    }
                    finally
                    {
                        Thread.Sleep(5000);
                    }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        /// <summary>
        /// 异步开启线程
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="frameCount"></param>
        /// <param name="camera"></param>
        private void MachineDamageMonitoring_Thread(string? workstation, VideoCaptureHelper camera, CancellationToken token)
        {
            #region 初始化AI算法
            // 初始化算法实例
            var objInfo = Init_YingYanAI(dllBytes);
            // 初始化算法模型(先用一次，以预加载)            
            var mPath = workstation switch
            {
                "CT托盘位" => modelPath1,
                "CT放料位" => modelPath,
                "PTO叠片粘电芯" => modelPath2,
                "PTO叠片取放料" => modelPath3,
                _ => throw new Exception($"[{camera.Name}]-{workstation}]工位未实现相关功能!!!"),
            };
            var objInit = Execute_ModelInit(mPath, workstation, objInfo.ModelInit, objInfo.YingYanAI);
            if (!objInit.IsValid || objInit.H246modle == null)
            {
                throw new Exception($"[{camera.Name}]-{workstation}]视觉算法模型初始化异常：[{objInit.IsValid}]-[{objInit.ExInfo}]");
            }
            _logger.Info($"[{camera.Name}-{workstation}]模型预加载成功！");
            #endregion

            #region 初始化相机
            // 创建缓存队列
            var frameCache = new ConcurrentQueue<(long dateTime, string barCode, Mat frame)>();
            // 执行视频读取
            camera.ExecuteFrame += ((frame) =>
            {
                //frameCache.Enqueue((GetCurrentTimestampSeconds(), barCode, frame));
                //// 缓存过多则清理
                //if (frameCache.Count > 100) frameCache.TryDequeue(out _);
            });
            camera.StartReading();
            _logger.Info($"相机[{camera.Name}-{workstation}]加载完成！");
            #endregion            

            var t = new Thread(() =>
            {
                var aiModels = new List<IPredictor>() { objInit.H246modle };
                var flag = "0";// 0->1:开始；1->0:结束                
                var lastFrameIsValid = false;// false->true:开始；true->false:结束                
                var frameCacheLis = new List<(DateTime dateTime, string barCode, Mat frame)>();
                // 存放每帧结果数据的缓存 用于生成异常视频               
                List<FrameDtoList> frameResultDataList = [];
                // 存放过程帧的缓存 用于生成异常视频
                List<Mat> abnormalFrames = [];
                // 存放一个过程，所有帧的结果状态
                List<string> resultStatus = [];
                // 存放视频的异常状态
                string abnormaVideoStatus = string.Empty;

                var diagnosis = new UnifiedTrajectoryDiagnosis();

                while (true)
                {
                    try
                    {
                        // 优雅退出
                        if (token.IsCancellationRequested) return;

                        #region test
                        if (frameCache.Count == 0)
                        {
                            var path = workstation switch
                            {
                                "CT托盘位" => "../Configs/放料模拟数据",
                                "CT放料位" => "../Configs/放料模拟数据",
                                "PTO叠片粘电芯" => "../Configs/PTO叠片粘电芯数据/全粘连4",
                                "PTO叠片取放料" => "../Configs/PTO叠片取放料数据/放料倾斜1",
                                _ => throw new NotImplementedException(),
                            };
                            var fileDirectory = Directory.GetFiles(path);

                            for (int i = 0; i < fileDirectory.Length; i++)
                            {
                                var f = Cv2.ImRead(fileDirectory[i], ImreadModes.Color);
                                frameCache.Enqueue((GetCurrentTimestampSeconds(), $"{Path.GetFileName(fileDirectory[i])}", f));
                                if (i == fileDirectory.Length - 1)
                                {
                                    HandyControl.Controls.MessageBox.Show($"图片测试完成", "信息", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                        }
                        #endregion                        

                        if (frameCache.Count <= 0)
                        {
                            // 无数据，则等待一下下
                            Thread.Sleep(5);
                            continue;
                        }
                        if (!frameCache.TryDequeue(out (long dateTime, string barCode, Mat frame) frameInfo))
                        {
                            // 获取视频帧失败，则等待一下下
                            Thread.Sleep(1);
                            _logger?.Error($"[{camera.Name}-{workstation}]视觉从队列获取视频帧失败！");
                            continue;
                        }
                        // 执行视觉推理算法
                        var aiResult = Execute_ModelAction(workstation, flag, [frameInfo.frame], aiModels, objInfo.ModelAction, objInfo.YingYanAI);
                        try
                        {
                            switch (workstation)
                            {
                                case "CT托盘位":
                                    ExcuteWorkstation_CTTray(workstation, camera, frameInfo, aiResult);
                                    break;
                                case "CT放料位":
                                    ExcuteWorkstation_CTPut(workstation, camera, frameInfo, aiResult, ref diagnosis, ref flag, ref lastFrameIsValid, ref abnormalFrames, ref frameResultDataList, ref resultStatus, ref abnormaVideoStatus);
                                    break;
                                case "PTO叠片粘电芯":
                                    ExcuteWorkstation_PTOStackingBondingBatteryCell(workstation, camera, frameInfo, aiResult, ref diagnosis, ref flag, ref lastFrameIsValid, ref abnormalFrames, ref frameResultDataList, ref resultStatus, ref abnormaVideoStatus);
                                    break;
                                case "PTO叠片取放料":
                                    ExcuteWorkstation_PTOStackingPut(workstation, camera, frameInfo, aiResult, ref diagnosis, ref flag, ref lastFrameIsValid, ref abnormalFrames, ref frameResultDataList, ref resultStatus, ref abnormaVideoStatus);
                                    break;
                                default:
                                    flag = aiResult.flag;
                                    _logger?.Warn($"未实现[{camera.Name}-{workstation}]逻辑！");
                                    break;
                            }
                        }
                        finally
                        {
                            frameInfo.frame?.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"[{camera.Name}-{workstation}]机损检测异常！", ex);
                    }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        /// <summary>
        /// 执行CT托盘位数据处理
        /// </summary>
        /// <param name="workstation"></param>
        /// <param name="camera"></param>
        /// <param name="frameInfo"></param>
        /// <param name="aiResult"></param>
        private void ExcuteWorkstation_CTTray(string workstation, VideoCaptureHelper camera, (long dateTime, string barCode, Mat frame) frameInfo, (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult)
        {
            // 垃圾帧，跳过
            if (!aiResult.IsValid) return;
            if (aiResult.AIResult?.Count <= 0)
            {
                _logger?.Error($"[{camera.Name}-{workstation}]视觉算法推理失败：[{aiResult.IsValid}]-[{aiResult.ExInfo}]");
                return;
            }
            // 如果存在"无胶套"，则有异常，执行异常视频流上传web
            for (int i = 0; i < aiResult.AIResult?.Count(); i++)
            {
                if (!aiResult.AIResult[i].Exists(o => o.Name.Contains("无胶套"))) continue;
                // 如果包含“无胶套”，则存在NG，需上传web
                _ = NGFramesUpLoadWebAsync(workstation, frameInfo.dateTime, frameInfo.barCode, frameInfo.frame.Clone(), aiResult.AIResult?[0]);
            }
            _logger?.Info($"[{camera.Name}-{workstation}]视觉算法推理完成：[{aiResult.IsValid}]-[{aiResult.ExInfo}]");
        }

        /// <summary>
        /// 执行CT放料位数据处理
        /// </summary>
        /// <param name="workstation"></param>
        /// <param name="camera"></param>
        /// <param name="frameInfo"></param>
        /// <param name="aiResult"></param>
        /// <param name="diagnosis"></param>
        /// <param name="flag"></param>
        /// <param name="frameCacheLis"></param>
        private void ExcuteWorkstation_CTPut
        (
            string workstation,
            VideoCaptureHelper camera,
            (long dateTime, string barCode, Mat frame) frameInfo,
            (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult,
            ref UnifiedTrajectoryDiagnosis diagnosis,
            ref string? flag,
            ref bool lastFrameIsValid,
            ref List<Mat> abnormalFrames,
            ref List<FrameDtoList> frameResultDataList,
            ref List<string> resultStatus,
            ref string abnormaVideoStatus
        )
        {
            // 更新标志位
            flag = aiResult.flag;

            // === 无效帧判定逻辑 ===
            // 若当前帧无效且上一帧也无效 → 直接跳出
            if (!aiResult.IsValid && !lastFrameIsValid)
            {
                frameInfo.frame?.Dispose();
                return;
            }

            // 获取当前帧检测结果列表
            var resultList = aiResult.AIResult?.FirstOrDefault() ?? [];

            // === 帧状态标识 ===
            string frameStatus = (aiResult.IsValid, lastFrameIsValid) switch
            {
                (true, false) => "1",   // 起始帧：从无效 → 有效
                (true, true) => "2",    // 连续有效帧
                (false, true) => "0",   // 结束帧：从有效 → 无效
                _ => "3"                // 其它情况（不会触发）
            };

            // 定义需要检测的点名称
            string[] requiredNames = { "上右角电芯", "上左角电芯", "上左角夹爪", "上右角夹爪", "下右角电芯", "下左角电芯", "下左角夹爪", "下右角夹爪", "载具圆心" };

            List<UnifiedDiagnosisResult> dataResultLis = [];

            // 确保每个所需点位存在
            foreach (var name in requiredNames)
            {
                if (!resultList.Any(o => o.Name == name))
                    resultList.Add(new AIResultModel { Name = name, Point3D = new AIpointXYZ() });
            }

            // 移除名称包含“载具拐点”的对象
            resultList.RemoveAll(o => o.Name != null && o.Name.Contains("载具拐点"));

            // 提前拿出圆心坐标   
            var centerX = resultList.FirstOrDefault(o => o.Name == "载具圆心")?.Point3D?.X ?? 0;
            var centerY = resultList.FirstOrDefault(o => o.Name == "载具圆心")?.Point3D?.Y ?? 0;

            diagnosis.SelectProgram("CT放料位");

            UnifiedDiagnosisResult dataResult = new();
            if (frameStatus != "2")
            {
                // 执行轨迹计算
                foreach (var name in requiredNames)
                {
                    var x = (double)(resultList.FirstOrDefault(o => o.Name.Contains(name))?.Point3D?.X ?? 0);
                    var y = (double)(resultList.FirstOrDefault(o => o.Name.Contains(name))?.Point3D?.Y ?? 0);

                    // 理想圆心坐标和阈值 为固定值 后续需开放至数据库中可设置 SetCTParameters(double positionThreshold, double angleThreshold, double idealCenterX, double idealCenterY)      
                    // 执行大数据轨迹算法
                    dataResult = diagnosis.ExecuteDiagnosis(frameStatus, name, x, y, centerX, centerY);
                    dataResultLis.Add(dataResult);
                }
            }

            // === 获取异常视频的类型 ===
            if (!string.IsNullOrEmpty(dataResultLis.FirstOrDefault(o => o.PositionName == "下左角电芯")?.AbnormalType))
            {
                abnormaVideoStatus = dataResultLis.FirstOrDefault(o => o.PositionName == "下左角电芯")?.AbnormalType ?? "NG";
            }

            // === 根据状态执行不同动作 ===
            switch (frameStatus)
            {
                case "1": // 起始帧
                    abnormalFrames.ForEach(frameInfo => frameInfo?.Dispose());
                    abnormalFrames.Clear();
                    abnormalFrames.Add(frameInfo.frame.Clone());
                    resultStatus.Add(dataResultLis.FirstOrDefault(o => o.PositionName == "下左角电芯")?.Status ?? "NG");
                    break;

                case "2": // 过程帧
                    abnormalFrames.Add(frameInfo.frame.Clone());
                    resultStatus.Add(dataResultLis.FirstOrDefault(o => o.PositionName == "下左角电芯")?.Status ?? "NG");
                    break;

                case "0": // 结束帧                    
                    // 异步发送实时数据：AI结果数据&大数据结果数据
                    _ = FramesUpLoadWebAsync("上料工位", resultList, dataResultLis, frameResultDataList, aiResult.IsValid, int.Parse(frameStatus));

                    if (resultStatus.Contains("NG"))
                    {
                        _ = NGVideoUpLoadWebAsync("上料工位", frameInfo.dateTime, frameInfo.barCode, abnormalFrames, frameResultDataList, abnormaVideoStatus);
                    }
                    abnormalFrames.Clear();
                    resultStatus.Clear();
                    break;
            }

            if (frameStatus != "2")
            {
                // 异步发送实时数据：AI结果数据&大数据结果数据
                _ = FramesUpLoadWebAsync("上料工位", resultList, dataResultLis, frameResultDataList, aiResult.IsValid, int.Parse(frameStatus));
            }

            // === 更新帧状态 ===
            lastFrameIsValid = aiResult.IsValid;

            // === 日志 ===
            _logger?.Info($"[{camera.Name}-{workstation}] 视觉算法推理完成：" + $"[Valid={aiResult.IsValid}] [Info={aiResult.ExInfo}]");
        }


        private void ExcuteWorkstation_PTOStackingBondingBatteryCell
        (
            string workstation, VideoCaptureHelper camera,
            (long dateTime, string barCode, Mat frame) frameInfo,
            (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult,
            ref UnifiedTrajectoryDiagnosis diagnosis,
            ref string? flag,
            ref bool lastFrameIsValid,
            ref List<Mat> abnormalFrames,
            ref List<FrameDtoList> frameResultDataList,
            ref List<string> resultStatus,
            ref string abnormaVideoStatus
        )
        {
            try
            {
                // 更新标志位
                flag = aiResult.flag;

                // === 无效帧判定逻辑 ===
                // 若当前帧无效且上一帧也无效 → 直接跳出
                if (!aiResult.IsValid && !lastFrameIsValid)
                {
                    frameInfo.frame?.Dispose();
                    return;
                }

                // 获取当前帧检测结果列表（防空）
                var resultList = aiResult.AIResult?.FirstOrDefault() ?? [];

                // === 帧状态标识 ===
                string frameStatus = (aiResult.IsValid, lastFrameIsValid) switch
                {
                    (true, false) => "1",   // 起始帧：从无效 → 有效
                    (true, true) => "0",    // 连续有效帧
                    (false, true) => "2",   // 结束帧：从有效 → 无效
                    _ => "3"                // 其它情况（不会触发）
                };

                // === 确保关键点存在 ===
                string[] requiredNames = { "中心点", "左点", "右点" };
                foreach (var name in requiredNames)
                {
                    if (!resultList.Any(o => o.Name == name))
                        resultList.Add(new AIResultModel { Name = name, Point3D = new AIpointXYZ() });
                }

                // === 重命名关键点 ===
                foreach (var item in resultList)
                {
                    if (item.Name is "中心点" or "左点" or "右点")
                        item.Name += "电芯";
                }

                // === 取出关键点，防止空引用 ===
                AIResultModel GetPoint(string keyword) => resultList.FirstOrDefault(o => o.Name.Contains(keyword)) ?? new AIResultModel { Point3D = new AIpointXYZ() };

                var left = GetPoint("左点电芯");
                var mid = GetPoint("中心点电芯");
                var right = GetPoint("右点电芯");

                SaveTXTFile($"../结果数据", "粘电芯结果数据", $"{frameInfo.barCode}\t左点电芯X\t{left.Point3D.X}\t左点电芯Y\t{left.Point3D.Y}\t中心点电芯X\t{mid.Point3D.X}\t中心点电芯Y\t{mid.Point3D.Y}\t右点电芯X\t{right.Point3D.X}\t右点电芯Y\t{right.Point3D.Y}");

                // === 执行诊断逻辑 ===
                diagnosis.SelectProgram("PTO叠片粘电芯");

                UnifiedDiagnosisResult diagnosisResult = frameStatus == "2" ? new UnifiedDiagnosisResult()   // 结束帧不诊断
                : diagnosis.ExecuteDiagnosis
                (
                    frameStatus,
                    left.Point3D.X, left.Point3D.Y,
                    mid.Point3D.X, mid.Point3D.Y,
                    right.Point3D.X, right.Point3D.Y
                );

                // === 获取异常视频的类型 ===
                if (!string.IsNullOrEmpty(diagnosisResult.AbnormalType))
                    abnormaVideoStatus = diagnosisResult.AbnormalType;

                // === 根据状态执行不同动作 ===
                switch (frameStatus)
                {
                    case "1": // 起始帧
                        //_logger?.Info($"[{workstation}]-获得起始帧");
                        abnormalFrames.ForEach(frameInfo => frameInfo?.Dispose());
                        abnormalFrames.Clear();
                        abnormalFrames.Add(frameInfo.frame.Clone());

                        resultStatus.Clear();
                        resultStatus.Add(diagnosisResult.Status);
                        break;

                    case "0": // 过程帧
                        abnormalFrames.Add(frameInfo.frame.Clone());
                        resultStatus.Add(diagnosisResult.Status);
                        break;

                    case "2": // 结束帧                        
                        // 异步发送实时数据：AI结果数据&大数据结果数据
                        _ = FramesUpLoadWebAsync("热压点位", resultList, [diagnosisResult], frameResultDataList, aiResult.IsValid, int.Parse(frameStatus));

                        if (resultStatus.Contains("NG"))
                        {
                            //_logger?.Info($"[{workstation}]-开始生成异常视频");
                            _ = NGVideoUpLoadWebAsync("热压点位", frameInfo.dateTime, frameInfo.barCode, abnormalFrames, frameResultDataList, abnormaVideoStatus);
                        }

                        abnormalFrames.ForEach(f => f.Dispose());
                        abnormalFrames.Clear();
                        resultStatus.Clear();
                        break;
                }

                if (frameStatus != "2")
                    // 异步发送实时数据：AI结果数据&大数据结果数据
                    _ = FramesUpLoadWebAsync("热压点位", resultList, [diagnosisResult], frameResultDataList, aiResult.IsValid, int.Parse(frameStatus));

                // === 更新帧状态 ===
                lastFrameIsValid = aiResult.IsValid;

                // === 日志 ===
                _logger?.Info($"[{camera.Name}-{workstation}] 视觉算法推理完成：" + $"[Valid={aiResult.IsValid}] [Info={aiResult.ExInfo}]");
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{camera.Name}-{workstation}] 调用出错: {ex.Message}");
            }
            finally
            {
                frameInfo.frame?.Dispose();
            }
        }

        private void ExcuteWorkstation_PTOStackingPut
        (
            string workstation, VideoCaptureHelper camera,
            (long dateTime, string barCode, Mat frame) frameInfo,
            (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult,
            ref UnifiedTrajectoryDiagnosis diagnosis,
            ref string? flag,
            ref bool lastFrameIsValid,
            ref List<Mat> abnormalFrames,
            ref List<FrameDtoList> frameResultDataList,
            ref List<string> resultStatus,
            ref string abnormaVideoStatus
        )
        {
            try
            {
                // 更新标志位
                flag = aiResult.flag;

                // === 无效帧判定逻辑 ===
                // 若当前帧无效且上一帧也无效 → 直接跳出
                if (!aiResult.IsValid && !lastFrameIsValid)
                {
                    frameInfo.frame?.Dispose();
                    return;
                }

                // 获取当前帧检测结果列表（防空）
                var resultList = aiResult.AIResult?.FirstOrDefault() ?? [];

                // === 帧状态标识 ===
                string frameStatus = (aiResult.IsValid, lastFrameIsValid) switch
                {
                    (true, false) => "1",   // 起始帧：从无效 → 有效
                    (true, true) => "0",    // 连续有效帧
                    (false, true) => "2",   // 结束帧：从有效 → 无效
                    _ => "3"                // 其它情况（不会触发）
                };


                // === 确保关键点存在 ===
                string[] requiredNames = { "右夹爪关键点", "左夹爪关键点", "中心点", "左点", "右点" };
                foreach (var name in requiredNames)
                {
                    if (!resultList.Any(o => o.Name == name))
                        resultList.Add(new AIResultModel { Name = name, Point3D = new AIpointXYZ() });
                }

                // 移除不需要的关键点
                string[] removeNames = { "右夹爪关键点", "左夹爪关键点" };
                resultList.RemoveAll(o => removeNames.Contains(o.Name));

                // === 重命名关键点 ===
                foreach (var item in resultList)
                {
                    if (item.Name is "中心点" or "左点" or "右点")
                        item.Name += "电芯";
                }

                // === 取出关键点，防止空引用 ===
                AIResultModel GetPoint(string keyword) => resultList.FirstOrDefault(o => o.Name.Contains(keyword)) ?? new AIResultModel { Point3D = new AIpointXYZ() };

                var left = GetPoint("左点电芯");
                var mid = GetPoint("中心点电芯");
                var right = GetPoint("右点电芯");

                SaveTXTFile($"../结果数据", "取放料结果数据", $"{frameInfo.barCode}\t左点电芯X\t{left.Point3D.X}\t左点电芯Y\t{left.Point3D.Y}\t中心点电芯X\t{mid.Point3D.X}\t中心点电芯Y\t{mid.Point3D.Y}\t右点电芯X\t{right.Point3D.X}\t右点电芯Y\t{right.Point3D.Y}");

                // === 执行诊断逻辑 ===
                diagnosis.SelectProgram("PTO叠片取放料");

                UnifiedDiagnosisResult diagnosisResult = frameStatus == "2" ? new UnifiedDiagnosisResult()   // 结束帧不诊断
                : diagnosis.ExecuteDiagnosis
                (
                    frameStatus,
                    left.Point3D.X, left.Point3D.Y,
                    mid.Point3D.X, mid.Point3D.Y,
                    right.Point3D.X, right.Point3D.Y
                );

                // === 获取异常视频的类型 ===
                if (!string.IsNullOrEmpty(diagnosisResult.AbnormalType))
                    abnormaVideoStatus = diagnosisResult.AbnormalType;

                // === 根据状态执行不同动作 ===
                switch (frameStatus)
                {
                    case "1": // 起始帧
                        abnormalFrames.ForEach(frameInfo => frameInfo?.Dispose());
                        abnormalFrames.Clear();
                        abnormalFrames.Add(frameInfo.frame.Clone());

                        resultStatus.Clear();
                        resultStatus.Add(diagnosisResult.Status);
                        break;

                    case "0": // 过程帧
                        abnormalFrames.Add(frameInfo.frame.Clone());
                        resultStatus.Add(diagnosisResult.Status);
                        break;

                    case "2": // 结束帧
                        // 异步发送实时数据：AI结果数据&大数据结果数据
                        _ = FramesUpLoadWebAsync("电芯转运点位", resultList, [diagnosisResult], frameResultDataList, aiResult.IsValid, int.Parse(frameStatus));

                        if (resultStatus.Contains("NG"))
                        {
                            _ = NGVideoUpLoadWebAsync("电芯转运点位", frameInfo.dateTime, frameInfo.barCode, abnormalFrames, frameResultDataList, abnormaVideoStatus);
                        }

                        abnormalFrames.ForEach(f => f.Dispose());
                        abnormalFrames.Clear();
                        resultStatus.Clear();
                        break;
                }

                if (frameStatus != "2")
                    // 异步发送实时数据：AI结果数据&大数据结果数据
                    _ = FramesUpLoadWebAsync("电芯转运点位", resultList, [diagnosisResult], frameResultDataList, aiResult.IsValid, int.Parse(frameStatus));

                // === 更新帧状态 ===
                lastFrameIsValid = aiResult.IsValid;

                // === 日志 ===
                _logger?.Info($"[{camera.Name}-{workstation}] 视觉算法推理完成：" + $"[Valid={aiResult.IsValid}] [Info={aiResult.ExInfo}]");
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{camera.Name}-{workstation}] 调用出错: {ex.Message}");
            }
            finally
            {
                frameInfo.frame?.Dispose();
            }
        }

        public static void SaveTXTFile(string FilePath, string FileName, string content)
        {
            try
            {
                FilePath = FilePath + "//" + FileName + ".txt";
                FileInfo FI = new FileInfo(FilePath);
                if (!FI.Directory.Exists)
                {
                    FI.Directory.Create();
                }
                if (File.Exists(FilePath) == false)
                {
                    using (StreamWriter sw = new StreamWriter(FilePath, false, Encoding.Default))
                    {
                        sw.WriteLine(content);
                    }
                }
                else
                {
                    using (StreamWriter sw = new StreamWriter(FilePath, true, Encoding.Default))
                    {
                        sw.WriteLine(content);
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// 轨迹算法实时上传web
        /// </summary>
        /// <param name="position"></param>
        /// <param name="aiResult"></param>
        /// <param name="diagnosisResults"></param>
        /// <param name="frameResultDataList"></param>
        /// <param name="isValid"></param>
        /// <param name="frameStatus"></param>
        /// <returns></returns>
        private async Task FramesUpLoadWebAsync
        (
            string position,
            List<AIResultModel> aiResult,
            IEnumerable<UnifiedDiagnosisResult> diagnosisResults,
            List<FrameDtoList> frameResultDataList,
            bool isValid,
            int frameStatus
        )
        {
            try
            {
                // —— 诊断结果缓存
                var diagnosisList = diagnosisResults?.ToList() ?? [];
                var firstDiagnosis = diagnosisList.FirstOrDefault();

                if (firstDiagnosis == null)
                    return;

                // —— 分类容器
                var categoryMap = categories.ToDictionary(c => c, c => new List<DataList>());

                // —— 遍历所有检测点
                foreach (var item in aiResult)
                {
                    item.Img?.Dispose();
                    item.Region?.Dispose();

                    var dataResult = diagnosisList.FirstOrDefault(o => o.PositionName == item.Name) ?? firstDiagnosis;

                    var category = GetCategory(item.Name);

                    if (category != null)
                    {
                        categoryMap[category].Add
                        (
                            CreateDataList
                            (
                                item.Name,
                                GetPointValues(item.Name, dataResult).x, GetPointValues(item.Name, dataResult).y, GetPointValues(item.Name, dataResult).z,
                                GetPointValues(item.Name, dataResult).dx, GetPointValues(item.Name, dataResult).dy, GetPointValues(item.Name, dataResult).dz,
                                GetPointValues(item.Name, dataResult).xStd, GetPointValues(item.Name, dataResult).yStd, GetPointValues(item.Name, dataResult).zStd,
                                GetPointValues(item.Name, dataResult).abnormal
                            )
                        );
                    }
                }

                // —— 构造 frame DTO
                var frameDto = new FrameDtoList
                {
                    FrameTimeStamp = GetCurrentTimestampSeconds(),
                    CategoryDtoList = [.. categoryMap.Select(kv => new CategoryDtoList
                    {
                        Category = kv.Key,
                        DataList = kv.Value
                    })]
                };

                if (isValid) frameResultDataList.Add(frameDto);

                var detectionResult = firstDiagnosis.AbnormalType ?? "NG";

                var sendObj = new HawkEyeSocketRequest
                {
                    Position = position,
                    Process = "叠片工序",
                    ProductionLine = "PTO",
                    ProductionBase = "惠州",
                    DeviceId = "CT01",
                    BarCode = "xxxxx",
                    DetectionTimeStamp = GetCurrentTimestampSeconds(),
                    DetectionResult = detectionResult,
                    AlertLevel = 1,
                    FrameDtoList = [frameDto],
                    StatusFlag = frameStatus
                };

                var jsonMsg = System.Text.Json.JsonSerializer.Serialize(sendObj, _jsonOpt);

                await BroadcastAsync(jsonMsg);

                aiResult.Clear();
            }
            catch (Exception ex)
            {
                _logger?.Error($"上传异常: {ex.Message}");
            }
        }


        private (float x, float y, float z, float xStd, float yStd, float zStd, float dx, float dy, float dz, int abnormal) GetPointValues(string name, UnifiedDiagnosisResult d)
        {
            float F(double? v) => (float)(v ?? 0);

            return name switch
            {
                "左点电芯" =>
                (F(d.LeftReverseProjectedPoint?.X), F(d.LeftReverseProjectedPoint?.Y), F(d.LeftReverseProjectedPoint?.Z),
                 F(d.ExpectedLeft3DPoint?.X), F(d.ExpectedLeft3DPoint?.Y), F(d.ExpectedLeft3DPoint?.Z),
                 F(d.LeftDx3D), F(d.LeftDy3D), F(d.LeftDz3D),
                 d.Status == "OK" ? 1 : 0),

                "中心点电芯" =>
                (F(d.MiddleReverseProjectedPoint?.X), F(d.MiddleReverseProjectedPoint?.Y), F(d.MiddleReverseProjectedPoint?.Z),
                 F(d.ExpectedMiddle3DPoint?.X), F(d.ExpectedMiddle3DPoint?.Y), F(d.ExpectedMiddle3DPoint?.Z),
                 F(d.MiddleDx3D), F(d.MiddleDy3D), F(d.MiddleDz3D),
                 d.Status == "OK" ? 1 : 0),

                "右点电芯" =>
                (F(d.RightReverseProjectedPoint?.X), F(d.RightReverseProjectedPoint?.Y), F(d.RightReverseProjectedPoint?.Z),
                 F(d.ExpectedRight3DPoint?.X), F(d.ExpectedRight3DPoint?.Y), F(d.ExpectedRight3DPoint?.Z),
                 F(d.RightDx3D), F(d.RightDy3D), F(d.RightDz3D),
                 d.Status == "OK" ? 1 : 0),

                _ =>
                (F(d.ReverseProjectedPoint?.X), F(d.ReverseProjectedPoint?.Y), F(d.ReverseProjectedPoint?.Z),
                 F(d.Expected3DPoint?.X), F(d.Expected3DPoint?.Y), F(d.Expected3DPoint?.Z),
                 F(d.Dx3D), F(d.Dy3D), F(d.Dz3D),
                 d.Status == "OK" ? 1 : 0)
            };
        }


        private string? GetCategory(string name)
        {
            if (name.Contains("电芯")) return "电芯";
            if (name.Contains("夹爪")) return "夹爪";
            if (name.Contains("载具")) return "载具";
            return null;
        }


        /// <summary>
        /// 创建对象
        /// </summary>
        /// <param name="name"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="abnormal"></param>
        /// <param name="xDiff"></param>
        /// <param name="yDiff"></param>
        /// <param name="zDiff"></param>
        /// <returns></returns>
        private DataList CreateDataList(string name, float x, float y, float z, float xDiff = 0f, float yDiff = 0f, float zDiff = 0f, float XStandard = 0f, float YStandard = 0f, float ZStandard = 0f, int abnormal = 1)
        {
            return new DataList
            {
                PointName = name,
                AiPointXYZDtoList =
                [
                    new AIPointXYZDto
                    {
                        PictureTimeStamp = GetCurrentTimestampSeconds(),
                        MoveDirection = "x",
                        X = x,
                        Y = y,
                        Z = z,
                        Speed = 0f,
                        XDiff = xDiff,
                        YDiff = yDiff,
                        ZDiff = zDiff,
                        Abnormal = abnormal,
                        XStandard = XStandard,
                        YStandard = YStandard,
                        ZStandard = ZStandard
                    }
                ]
            };
        }

        /// <summary>
        /// 获取当前时间戳
        /// </summary>
        /// <returns></returns>
        public long GetCurrentTimestampSeconds()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            long millisecondsSinceEpoch = now.ToUnixTimeMilliseconds();
            return millisecondsSinceEpoch;
        }

        /// <summary>
        /// 图片流上传web
        /// </summary>
        /// <param name="position"></param>
        /// <param name="dateTime"></param>
        /// <param name="barCode"></param>
        /// <param name="frame"></param>
        /// <param name="AIResults"></param>
        private async Task NGFramesUpLoadWebAsync(string? position, long dateTime, string barCode, Mat frame, List<AIResultModel>? AIResults)
        {
            var t = Task.Factory.StartNew(async () =>
            {
                try
                {
                    foreach (var AIResultModel in AIResults ?? new List<AIResultModel>())
                    {
                        // 释放
                        AIResultModel.Region?.Dispose();
                        // 目前只上传NG的
                        if (!AIResultModel.Name.Contains("无胶套")) continue;

                        // 原图处理为结果图（原图中绘制AI检测结果的框框-opencv实现）
                        var graphicalResult = frame.Clone();
                        byte[] file;
                        try
                        {
                            // 直接在内存中绘制并编码，无需写入磁盘
                            Cv2.Rectangle(graphicalResult, AIResultModel.Rectangle, new Scalar(0, 255, 0), 2);
                            // 编码成PNG字节数组
                            Cv2.ImEncode(".png", graphicalResult, out file);
                        }
                        finally
                        {
                            graphicalResult?.Dispose();
                        }
                        var abnormalTimeStamp = GetCurrentTimestampSeconds();

                        var re = await _webApiClient.AiDetectionDataAsync($"{position}", $"{barCode}", abnormalTimeStamp,
                               AIResultModel.Score, AIResultModel.Width, AIResultModel.Height, AIResultModel.Area, "NG", file);
                        if (re.IsSuccess || re.Code == 200)
                            _logger.Info($"[{position}-{barCode}]AI算法结果上传web成功！");
                        else
                            _logger.Error($"[{position}-{barCode}]AI算法结果上传web失败：[{re.Message}]");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[{position}-{barCode}]AI算法结果上传web异常：[{ex.Message}]");
                }
                finally
                {
                    frame?.Dispose();
                }
            });
            await t;
        }

        /// <summary>
        /// 视频流上传web
        /// </summary>
        /// <param name="position"></param>
        /// <param name="dateTime"></param>
        /// <param name="barCode"></param>
        /// <param name="frame"></param>
        /// <param name="AIResults"></param>
        private async Task NGVideoUpLoadWebAsync(string? position, long dateTime, string barCode, List<Mat> abnormalFrames, List<FrameDtoList> frameResultDataList, string abnormaVideoStatus)
        {
            try
            {
                var file = SaveToMP4Bytes(abnormalFrames, abnormalFrames[0].Cols, abnormalFrames[0].Rows);

                if (frameResultDataList.Count > 300)
                {
                    frameResultDataList.RemoveRange(0, Math.Min(200, frameResultDataList.Count));
                }

                var re = await _webApiClient.TrajectoryDataAsync($"{position}", $"{barCode}", dateTime, dateTime, file, frameResultDataList, abnormaVideoStatus);
                if (re.IsSuccess || re.Code == 200)
                {
                    _logger.Info($"[{position}-{barCode}]AI算法结果上传web成功！");
                    frameResultDataList.Clear();
                    abnormaVideoStatus = string.Empty;
                }
                else
                    _logger.Error($"[{position}-{barCode}]AI算法结果上传web失败：[{re.Message}]");
            }
            catch (Exception ex)
            {
                _logger.Error($"[NGVideoUpLoadWebAsync]视频流上传错误: {ex.Message}");
            }
        }

        private void SaveToMP4(List<Mat> frames, double actualFps, double actualWidth, double actualHeight)
        {
            try
            {
                FourCC codec = FourCC.H264;

                using var writer = new VideoWriter($"..\\output.mp4", codec, actualFps, new OpenCvSharp.Size(actualWidth, actualHeight), true);
                foreach (var frame in frames)
                {
                    writer.Write(frame);
                    frame.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[ERROR] Failed to save video: {ex.Message}");
            }
        }

        private byte[] SaveToMP4Bytes(List<Mat> frames, double actualWidth, double actualHeight, double actualFps = 25)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"output_{Guid.NewGuid()}.mp4");
            byte[] fileBytes = [];

            try
            {
                FourCC codec = FourCC.H264;

                if (frames.Count > 300)
                {
                    frames.Take(200).ToList().ForEach(f => f?.Dispose());
                    frames.RemoveRange(0, Math.Min(200, frames.Count));
                }

                using (var writer = new VideoWriter(tempFile, codec, actualFps, new OpenCvSharp.Size(actualWidth, actualHeight), true))
                {
                    foreach (var frame in frames)
                    {
                        writer.Write(frame);
                        frame.Dispose();
                    }
                }

                // 读取文件为字节数组
                if (File.Exists(tempFile))
                {
                    fileBytes = File.ReadAllBytes(tempFile);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[ERROR] Failed to save video to memory: {ex.Message}");
            }
            finally
            {
                // 清理临时文件
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch { /* 忽略清理错误 */ }
            }

            return fileBytes;
        }

        #endregion

        #region 监控电芯码
        private FileSystemWatcher? watcher;

        /// <summary>
        /// 初始化电芯码文件监控
        /// </summary>
        private void InitBarCodeWatcher(string path, string filter = "*.xlsx")
        {
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                watcher = new FileSystemWatcher(path, "*.xlsx");
                watcher.Created += new FileSystemEventHandler((s, e) =>
                {
                    try
                    {
                        barCode = Path.GetFileNameWithoutExtension(e.Name) ?? "";
                        _logger.Info($"电芯码:{barCode}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"获取电芯码异常:{ex.Message}");
                    }
                });
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _logger.Error($"初始化电芯码检测任务发生异常！", ex);
            }
        }
        #endregion

        #region 服务端
        private SocketServer? server;

        /// <summary>
        /// 初始化服务端
        /// </summary>
        /// <returns></returns>
        private async Task InitServerAsync(int port)
        {
            try
            {
                server = new SocketServer("127.0.0.1", port, _logger);
                server.OnClientConnected += new EventHandler<ClientEventArgs<string>>((s, e) =>
                {
                    _logger.Info($"[{e.ClientSocket.RemoteEndPoint}]客户端已连接！");
                });
                server.OnClientDisconnected += new EventHandler<ClientEventArgs<string>>((s, e) =>
                {
                    _logger.Info($"[{e.ClientSocket.RemoteEndPoint}]客户端已断开！");
                });
                server.OnMessageReceived += new EventHandler<ClientEventArgs<byte[]>>((s, e) =>
                {
                    _logger.Info($"接收[{e.ClientSocket.RemoteEndPoint}]客户端消息：[{Encoding.UTF8.GetString(e.Message ?? new byte[0])}]！");
                    //((JavaSocketServer?)s)?.SendToClientAsync(e.ClientSocket, "123123123");
                });
                await server.StartAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"初始化服务器发生异常！", ex);
            }
        }
        #endregion

        #region web服务器
        private WebSocketServer? webServer;
        private List<IWebSocketConnection> webClients = [];
        private readonly ConcurrentBag<IWebSocketConnection> _clients = [];
        private readonly object _sendLock = new();
        private DateTime _lastSend = DateTime.MinValue;
        private readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(10); // 节流间隔，防止CPU占用高
        private void WebSocketInit(int port = 8181)
        {
            webServer = new WebSocketServer($"ws://0.0.0.0:{port}");

            webServer.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    _logger.Info($"[{socket.ConnectionInfo.ClientIpAddress}]客户端已连接！");
                    webClients.Add(socket);
                };

                socket.OnClose = () =>
                {
                    _logger.Info($"[{socket.ConnectionInfo.ClientIpAddress}]客户端已断开！");
                    webClients.Remove(socket);
                };

                socket.OnMessage = message =>
                {
                    _logger.Info($"接收[{socket.ConnectionInfo.ClientIpAddress}]客户端消息：[{message}]！");
                };
            });
        }

        private async Task WebSocketSendAsync(string message)
        {
            try
            {
                foreach (var webClient in webClients)
                {
                    if (webClient.IsAvailable)
                    {
                        await webClient.Send(message);
                    }
                    else
                    {
                        // 从集合移除无效连接
                        webClients.Remove(webClient);
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        public void StartServer(int port = 8181)
        {
            var server = new WebSocketServer($"ws://0.0.0.0:{port}");
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    _logger.Info($"[{socket.ConnectionInfo.ClientIpAddress}]客户端已连接！");
                    _clients.Add(socket);
                };

                socket.OnClose = () =>
                {
                    _logger.Info($"[{socket.ConnectionInfo.ClientIpAddress}]客户端已断开！");
                    RemoveClient(socket);
                };

                socket.OnMessage = message =>
                {
                    _logger.Info($"接收[{socket.ConnectionInfo.ClientIpAddress}]客户端消息：[{message}]！");
                };

                socket.OnError = (ex) =>
                {
                    RemoveClient(socket);
                    _logger.Info($"异常：[{socket.ConnectionInfo.ClientIpAddress}]：[{ex}]！");
                };
            });
        }

        private void RemoveClient(IWebSocketConnection client)
        {
            var clients = _clients.ToList();
            if (clients.Contains(client))
            {
                clients.Remove(client);
            }
        }

        // 高频安全广播
        public async Task BroadcastAsync(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // 节流控制（例如每50ms发送一次）
            if (DateTime.Now - _lastSend < _minInterval)
                return;

            _lastSend = DateTime.Now;

            // 拍快照避免枚举冲突
            var clientsSnapshot = _clients.ToList();
            if (clientsSnapshot.Count == 0)
                return;

            var tasks = clientsSnapshot.Select(async client =>
            {
                try
                {
                    if (client.IsAvailable)
                        await Task.Run(() => client.Send(message));
                    else
                        RemoveClient(client);
                }
                catch
                {
                    RemoveClient(client);
                }
            });

            await Task.WhenAll(tasks);
        }

        static string GetLocalIPAddress()
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            return "127.0.0.1";
        }
        #endregion

        #region 视频流算法
        /// <summary>
        /// 获取模型文件onnx
        /// </summary>
        /// <param name="pythonCode"></param>
        /// <param name="appKey"></param>
        /// <param name="publicKey"></param>
        /// <param name="executeType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<string> DownloadOnnxAsync(string pythonCode, string appKey, string publicKey, string executeType = "PRED")
        {
            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{pythonCode}.onnx");
            var xmlPublicKey = RasHelper.ToXmlPublicKey(publicKey);
            var signature = RasHelper.RSAEncrypt(xmlPublicKey, appKey);
            // 获取PID算法文件
            var reFile = await _webApiClient.DownloadDllAsync(appKey, signature, pythonCode, executeType);
            if (!(reFile != null && reFile.DataStr != null))
            {
                var message = $"获取AI算法onnx模型文件失败：pythonCode:[{pythonCode}]-message[{reFile?.Message}]";
                _logger.Error(message);
                throw new Exception(message);
            }
            File.WriteAllBytes(modelPath, reFile.DataStr);
            return modelPath;
        }

        /// <summary>
        /// 从web获取视频流算法
        /// </summary>
        /// <param name="pythonCode"></param>
        /// <param name="appKey"></param>
        /// <param name="publicKey"></param>
        /// <param name="executeType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<byte[]> DownloadDllAsync(string pythonCode, string appKey, string publicKey, string executeType = "PRED")
        {
            var xmlPublicKey = RasHelper.ToXmlPublicKey(publicKey);
            var signature = RasHelper.RSAEncrypt(xmlPublicKey, appKey);
            // 获取PID算法文件
            var reFile = await _webApiClient.DownloadDllAsync(appKey, signature, pythonCode, executeType);
            if (!(reFile != null && reFile.DataStr != null))
            {
                var message = $"获取AI算法dll文件信息失败：pythonCode:[{pythonCode}]-message[{reFile?.Message}]";
                _logger.Error(message);
                throw new Exception(message);
            }

            return reFile.DataStr;
        }

        /// <summary>
        /// 初始化视频流模型
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>(算法实例、初始化方法、执行方法)</returns>
        /// <exception cref="Exception"></exception>
        private (object? YingYanAI, MethodInfo? ModelInit, MethodInfo? ModelAction) Init_YingYanAI(byte[] bytes)
        {
            try
            {
                #region 后续删掉，使用下面的从web获取  -TODO
                object? yingYanAI;
                MethodInfo? modelInit, modelAction;
                var DLLPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "YingYanAlgoCore.dll");
                if (File.Exists($"{DLLPath}"))
                {
                    var ass = Assembly.LoadFrom($"{DLLPath}");
                    var tp = ass.GetType("ZZYingYanAICore.YingYanAI");
                    if (tp == null) throw new NullReferenceException("[YingYanAlgoCore.YingYanAI]type获取失败！");
                    yingYanAI = Activator.CreateInstance(tp);
                    modelInit = tp.GetMethod("ModelInit");
                    modelAction = tp.GetMethod("ModelAction");
                    return (yingYanAI, modelInit, modelAction);
                }
                #endregion

                var assembly = Assembly.Load(bytes);
                //获取类名，必须 命名空间+类名
                var type = assembly.GetType("YingYanAlgoCore.YingYanAI");
                if (type == null) throw new NullReferenceException("[YingYanAlgoCore.YingYanAI]type获取失败！");
                //建立实例
                yingYanAI = Activator.CreateInstance(type);
                // 获取方法
                modelInit = type.GetMethod("ModelInit");
                modelAction = type.GetMethod("ModelAction");

                return (yingYanAI, modelInit, modelAction);
            }
            catch (Exception ex)
            {
                throw new Exception($"创建AI算法dll实例异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 初始化算法，获取算法模型
        /// </summary>
        /// <param name="modelPath"></param>
        /// <param name="ModelInit"></param>
        /// <param name="YingYanAI"></param>
        /// <returns></returns>
        private (bool IsValid, string? ExInfo, IPredictor? H246modle) Execute_ModelInit(string? modelPath, string? workstation, MethodInfo? ModelInit, object? YingYanAI)
        {
            try
            {
                var AIModelData = new object?[]
                {
                    modelPath,
                    null,           // 不需要直接置null
                    workstation,    // 工位名称
                    null,           // 返回的算法模型
                    null,           // 接口异常返回信息
                    true            // 是否启用GPU
                };
                // 执行初始化算法
                var re = ModelInit?.Invoke(YingYanAI, AIModelData);

                var H246modle = AIModelData[3] as IPredictor;
                var ExInfo = AIModelData[4] as string;
                return (H246modle != null, ExInfo, H246modle);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        /// <summary>
        /// 执行推理算法：推理多帧图片输出结果
        /// </summary>
        /// <param name="FrameList"></param>
        /// <param name="AIModels"></param>
        /// <param name="ModelAction"></param>
        /// <param name="YingYanAI"></param>
        /// <returns></returns>
        private (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) Execute_ModelAction(string? workstation, string? flag, List<Mat> FrameList, List<IPredictor> AIModels, MethodInfo? ModelAction, object? YingYanAI)
        {
            try
            {
                var AIParamData = new object?[]
                {
                    AIModels,     // 模型列表
                    FrameList,    // 多帧图片数据流
                    workstation,  // 工位名称
                    flag,         // 默认传"0"
                    null,         // 推理结果列表
                    null,         // 调用是否有效
                    null,         // 回传的标志位: "1"表示开始，"0"表示结束
                    null          // 接口异常返回信息
                };
                // 执行推理算法
                var re = ModelAction?.Invoke(YingYanAI, AIParamData);
                var AIResult = AIParamData[4] as List<List<AIResultModel>>;
                var IsValid = AIParamData[5] as bool?;
                var reFlag = AIParamData[6] as string;
                var ExInfo = AIParamData[7] as string;
                return (IsValid ?? false, ExInfo, AIResult, reFlag);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null, "0");
            }
        }
        #endregion

        #region 自动检测RTSP摄像头
        /// <summary>
        /// 检索摄像头
        /// </summary>
        /// <returns></returns>
        private void SearchDevices_Thread()
        {
            InitSDK();
            var t = new Thread(async () =>
            {
                while (true)
                {
                    try
                    {
                        // 检测设备接入
                        XCloudSDK.XCloudSDK_Device_SearchDevices(_hUser, 1000, 1);

                        #region 弹窗新增相机配置
                        if (deviceInfoCache.Count <= 0) continue;
                        if (!deviceInfoCache.TryTake(out DeviceInfo? deviceInfo) || deviceInfo == null) continue;
                        // 自定义弹窗新增相机，保存在xml文档中
                        AddCameraResult? re = null;
                        await Dispatcher.Invoke(async () =>
                        {
                            await Dialog
                               .Show(new VM_AddCameraDialog().UIElement)
                               .Initialize<VM_AddCameraDialog>(vm => vm.DeviceInfo = deviceInfo)
                               .GetResultAsync<AddCameraResult?>()
                               .ContinueWith(um =>
                               {
                                   re = um.Result;
                               });
                        });
                        if (re?.Result != MessageBoxResult.OK) continue;

                        //var vm = _moduleManager.GetViewModel("VM_CameraConfigurationManagementPage") as VM_CameraConfigurationManagementPage;
                        RunOnUIThread(() =>
                        {
                            var view = _moduleManager.GetView("VM_CameraConfigurationManagementPage");
                            var vm = view?.DataContext as VM_CameraConfigurationManagementPage;
                            vm?.AddCamera(re?.CameraModel);
                        });

                        // 掉 "0x" 前缀并解析十六进制数处理 HostIP 转换
                        var hexValue = Convert.ToUInt32(deviceInfo.HostIP, 16);
                        var deviceIp = string.Join(".", BitConverter.GetBytes(hexValue));
                        // 同步系统设置时间
                        var re1 = XCloudSDK.XCloudSDK_Device_DevSynTime(_hUser, deviceIp + ":34567", $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}", 1);
                        _logger.Info($"新设备{deviceIp}同步系统设置时间[{re1}]");
                        #endregion
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"检索相机任务发生异常！", ex);
                    }
                    finally
                    {
                        deviceInfoCache.Clear();
                        Thread.Sleep(5000);
                    }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        /// <summary>
        /// 用户句柄
        /// </summary>
        private int _hUser = 0;
        public XCloudSDK.PXSDK_MessageCallBack? _mainCallback;
        private ConcurrentBag<DeviceInfo> deviceInfoCache = new ConcurrentBag<DeviceInfo>();

        /// <summary>
        /// SDK初始化
        /// </summary>
        public void InitSDK()
        {
            try
            {
                //DeviceConfig.ini里填写初始化相关的参数：开发者鉴权信息、配置文件目录等
                var sInit = "{\"LogLevel\":8,\"TempPath\":\"\",\"ConfigPath\":\"\",\"PlatUUID\":\"\",\"PlatAppKey\":\"\",\"PlatAppSecret\":\"\",\"PlatMovedCard\":0,\"ServerIP\":\"\",\"ServerPort\":34567,\"InitType\":0}";
                if (File.Exists("../Configs/DeviceConfig.ini"))
                {
                    //如果是合法的Json，就读取配置文件，否则使用默认参数
                    var sFileContext = File.ReadAllText("../Configs/DeviceConfig.ini");
                    if (IsValidJson(sFileContext)) sInit = sFileContext;
                }
                File.WriteAllText("../Configs/DeviceConfig.ini", sInit);
                var re = XCloudSDK.XCloudSDK_Init(sInit);
                _mainCallback = new XCloudSDK.PXSDK_MessageCallBack(XCloudSDKCallBackAsync);
                _hUser = XCloudSDK.XCloudSDK_RegisterCallback(_mainCallback, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                _logger.Error($"SDK初始化发生异常！", ex);
            }
        }

        /// <summary>
        /// 回调函数
        /// </summary>
        /// <param name="hObject"></param>
        /// <param name="nMsgId"></param>
        /// <param name="nParam1"></param>
        /// <param name="nParam2"></param>
        /// <param name="nParam3"></param>
        /// <param name="szString"></param>
        /// <param name="pObject"></param>
        /// <param name="lParam"></param>
        /// <param name="nSeq"></param>
        /// <param name="pUserData"></param>
        /// <param name="pMsg"></param>
        /// <returns></returns>
        private int XCloudSDKCallBackAsync(int hObject, int nMsgId, int nParam1, int nParam2, int nParam3, string szString, IntPtr pObject, int lParam, int nSeq, IntPtr pUserData, IntPtr pMsg)
        {
            try
            {
                //if (nMsgId != 13001) return 0;
                szString = "[{\n\t\t\"AuxFunctionMask\":\t0,\n\t\t\"BuildDate\":\t\"2025- 5- 9  9:23:21\",\n\t\t\"ChannelNum\":\t1,\n\t\t\"DeviceType\":\t0,\n\t\t\"GateWay\":\t\"0x0101A8C0\",\n\t\t\"HostIP\":\t\"0x0E01A8C0\",\n\t\t\"HostName\":\t\"admin\",\n\t\t\"HttpPort\":\t80,\n\t\t\"MAC\":\t\"00:12:34:5d:14:a2\",\n\t\t\"MonMode\":\t\"TCP\",\n\t\t\"NetConnectState\":\t1,\n\t\t\"OtherFunction\":\t\"D=2025-10-15 17:59:42 V=b9539b657115662\",\n\t\t\"SN\":\t\"95e8088c44506364esnh\",\n\t\t\"SSLPort\":\t8443,\n\t\t\"Submask\":\t\"0x00FFFFFF\",\n\t\t\"TCPMaxConn\":\t10,\n\t\t\"TCPPort\":\t34567,\n\t\t\"UDPPort\":\t34568,\n\t\t\"UseHSDownLoad\":\tfalse,\n\t\t\"Version\":\t\"V1.00.R02.000749N8.10010.140300.0020000\"\n\t}]";

                // 反序列化 JSON 字符串                
                var deviceInfos = JsonConvert.DeserializeObject<List<DeviceInfo>>(szString);
                if (deviceInfos == null) return 0;
                var cameras = GetCameras();
                foreach (var deviceInfo in deviceInfos)
                {
                    // 掉 "0x" 前缀并解析十六进制数处理 HostIP 转换
                    var hexValue = Convert.ToUInt32(deviceInfos[0].HostIP, 16);
                    var deviceIp = string.Join(".", BitConverter.GetBytes(hexValue));
                    // 已存在，则跳过
                    if (cameras.Exists(o => o.HostIP == deviceIp))
                    {
                        var deviceInfoStr = JsonConvert.SerializeObject(deviceInfo);
                        // 更新设备信息
                        if (cameras.Exists(o => o.HostIP == deviceIp && o.DeviceInfoStr != deviceInfoStr))
                        {
                            cameras.ForEach(o =>
                            {
                                if (o.HostIP == deviceIp && o.DeviceInfoStr != deviceInfoStr)
                                {
                                    o.DeviceInfoStr = deviceInfoStr;
                                    SetCameras(o);
                                }
                            });
                        }
                        continue;
                    }
                    // 已缓存，则跳过
                    if (deviceInfoCache.ToArray().ToList().Exists(o => o.HostIP == deviceInfo.HostIP)) continue;
                    // 新增设备添加缓存中
                    deviceInfoCache.Add(deviceInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"检索相机任务发生异常！", ex);
            }
            return 0;
        }

        /// <summary>
        /// 校验json字符串
        /// </summary>
        /// <param name="jsonString"></param>
        /// <returns></returns>
        private bool IsValidJson(string jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString)) return false;

            try
            {
                using (var j = JsonDocument.Parse(jsonString)) return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region Methods
        #region Camera相关
        /// <summary>
        /// 相机读写锁
        /// </summary>
        private static readonly object _locker = new object();

        /// <summary>
        /// 获取
        /// </summary>
        /// <exception cref="Exception"></exception>
        public static List<CameraModel> GetCameras()
        {
            try
            {
                lock (_locker)
                {
                    var path = "../Configs/CameraConfig.xml";
                    var _cameras = new List<CameraModel>();
                    if (File.Exists(path))
                    {
                        _cameras = XmlHelper.XmlToModel<List<CameraModel>>(path) ?? new List<CameraModel>();
                    }
                    return _cameras;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("获取相机配置信息异常！", ex);
            }
        }

        /// <summary>
        /// 新增或更新
        /// </summary>
        /// <param name="cameras"></param>
        public static void SetCameras(params CameraModel[] cameras)
        {
            try
            {
                lock (_locker)
                {
                    if (cameras == null || cameras.Length <= 0) return;
                    var path = "../Configs/CameraConfig.xml";
                    var _cameras = GetCameras();
                    foreach (var camera in cameras)
                    {
                        if (_cameras.Exists(o => o.GuidId == camera.GuidId))
                        {
                            var index = _cameras.FindIndex(o => o.GuidId == camera.GuidId);
                            _cameras[index] = camera;
                            continue;
                        }
                        _cameras.Add(camera);
                    }
                    XmlHelper.ModelToXml(_cameras, path);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("新增或更新相机配置信息异常！", ex);
            }
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="cameras"></param>
        public static void DeleteCameras(params CameraModel[] cameras)
        {
            if (cameras == null || cameras.Length <= 0) return;
            var guidIds = cameras.Select(o => o.GuidId).ToArray();
            DeleteCameras(guidIds);
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="guidIds"></param>
        public static void DeleteCameras(params string[] guidIds)
        {
            try
            {
                lock (_locker)
                {
                    if (guidIds == null || guidIds.Length <= 0) return;
                    var path = "../Configs/CameraConfig.xml";
                    var _cameras = GetCameras();
                    foreach (var guidId in guidIds)
                    {
                        if (!_cameras.Exists(o => o.GuidId == guidId)) continue;

                        var index = _cameras.FindIndex(o => o.GuidId == guidId);
                        _cameras.RemoveAt(index);
                    }
                    XmlHelper.ModelToXml(_cameras, path);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("删除相机配置信息异常！", ex);
            }
        }
        #endregion
        #endregion
    }
}
