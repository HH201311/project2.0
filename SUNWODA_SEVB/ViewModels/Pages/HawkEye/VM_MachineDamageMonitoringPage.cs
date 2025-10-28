using Dm.filter;
using Dm.util;
using Fleck;
using HalconDotNet;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HelixToolkit.Wpf;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.IdentityModel.Tokens;
using Mysqlx.Notice;
using MySqlX.XDevAPI.Common;
using NetTaste;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using OpenCvSharp;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Component;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.HawkEye;
using SUNWODA_SEVB.Core.Models.Web;
using SUNWODA_SEVB.Data.Models;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.SocketHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.Common;
using SUNWODA_SEVB.ViewModels.Dialogs.HawkEye;
using SUNWODA_SEVB.ViewModels.Pages.Demo;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Serialization;
using TrajectoryDiagnosisLib;
using Yolo8Core;
using static Mysqlx.Notice.Warning.Types;
using static SUNWODA_SEVB.ViewModels.Dialogs.HawkEye.VM_AddCameraDialog;
using static TrajectoryDiagnosisLib.TrajectoryDiagnosis;
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
        private byte[] dllBytes = new byte[0];
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
            WebSocketInit(8181);
            //SearchDevices_Thread();

            // 从web获取AI算法模型文件onnx  —— TODO
            //modelPath = await DownloadOnnxAsync("ZZ_YY_C01_DP_DLL", "ZZ_YY", "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCQiNCeYgNIL35V99cJ0giEOtOCd6tf9fVEVCuFmFu0zuskPJYB0D1pUcYjLtJfadGa1HflWYINl/zk9bq0fDipuz5vPkswsp0N4efiVzwoBbzUTpLPePNkpb6rDyd+3RUNAC8UtZU2ptKGCNVvvgMQGZp+2mnIKPamuEXzK5qkkwIDAQAB");
            //modelPath1 = await DownloadOnnxAsync("ZZ_YY_C01_DP_DLL", "ZZ_YY", "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCQiNCeYgNIL35V99cJ0giEOtOCd6tf9fVEVCuFmFu0zuskPJYB0D1pUcYjLtJfadGa1HflWYINl/zk9bq0fDipuz5vPkswsp0N4efiVzwoBbzUTpLPePNkpb6rDyd+3RUNAC8UtZU2ptKGCNVvvgMQGZp+2mnIKPamuEXzK5qkkwIDAQAB");
            modelPath = "../Configs/best.onnx";
            modelPath1 = "../Configs/best[托盘].onnx";

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
            var mPath = workstation == "CT托盘位" ? modelPath1 : modelPath;
            var objInit = Execute_ModelInit(mPath, workstation, objInfo.ModelInit, objInfo.YingYanAI);
            if (!objInit.IsValid || objInit.H246modle == null)
            {
                throw new Exception($"[{camera.Name}]-{workstation}]视觉算法模型初始化异常：[{objInit.IsValid}]-[{objInit.ExInfo}]");
            }
            _logger.Info($"[{camera.Name}-{workstation}]模型预加载成功！");
            #endregion

            #region 初始化相机
            // 创建缓存队列
            var frameCache = new ConcurrentQueue<(DateTime dateTime, string barCode, Mat frame)>();
            // 执行视频读取
            camera.ExecuteFrame += new VideoCaptureHelper.OnReadingFrame((frame) =>
            {
                // frameCache.Enqueue((DateTime.Now, barCode, frame));
                // 缓存过多则清理
                // if (frameCache.Count > 100) frameCache.TryDequeue(out _);
            });
            camera.StartReading();
            _logger.Info($"相机[{camera.Name}-{workstation}]加载完成！");
            #endregion

            //// test
            //var fileDirectory = Directory.GetFiles("D:\\LocalFile\\LocalCode\\SunwodaV2\\SUNWODA_SEVB\\bin\\Debug\\Configs\\托盘关键帧");
            //for (int i = 0; i < fileDirectory.Length; i++)
            //{
            //    var f = Cv2.ImRead(fileDirectory[i], ImreadModes.Color);
            //    frameCache.Enqueue((DateTime.Now, $"{i}", f));
            //}

            var t = new Thread(() =>
            {
                var aiModels = new List<IPredictor>() { objInit.H246modle };
                var flag = "0";// 0:开始；1:结束
                var temp_resultFlag = 0;// 偶数则强制设置大数据组算法输出位NG，——临时测试使用
                var frameCacheLis = new List<(DateTime dateTime, string barCode, Mat frame, DiagnosisResult dataResult)>();
                // 循环执行算法
                var index = 200;
                var diagnosis = new TrajectoryDiagnosis();
                diagnosis.Initialize();
                while (true)
                {
                    try
                    {
                        // 优雅退出
                        if (token.IsCancellationRequested) return;

                        // test
                        if (frameCache.Count == 0)
                        {
                            //if (index == 610) index = 200;
                            temp_resultFlag = 0;
                            var path = workstation == "CT托盘位" ? "../Configs/CT托盘位测试帧" : "../Configs/放料模拟数据";
                            var fileDirectory = Directory.GetFiles(path);
                            //var f = Cv2.ImRead(fileDirectory[index], ImreadModes.Color);
                            //frameCache.Enqueue((DateTime.Now, $"{index}", f));
                            //index++;
                            for (int i = 200; i < 610; i++)
                            {
                                var f = Cv2.ImRead(fileDirectory[i], ImreadModes.Color);
                                frameCache.Enqueue((DateTime.Now, $"{i}", f));
                            }
                        }

                        if (frameCache.Count <= 0)
                        {
                            // 无数据，则等待一下下
                            Thread.Sleep(1);
                            continue;
                        }
                        if (!frameCache.TryDequeue(out (DateTime dateTime, string barCode, Mat frame) frameInfo))
                        {
                            // 获取视频帧失败，则等待一下下
                            Thread.Sleep(1);
                            _logger?.Error($"[{camera.Name}-{workstation}]视觉从队列获取视频帧失败！");
                            continue;
                        }

                        // 执行视觉推理算法
                        var aiResult = Execute_ModelAction(workstation, flag, new List<Mat>() { frameInfo.frame }, aiModels, objInfo.ModelAction, objInfo.YingYanAI);
                        try
                        {
                            switch (workstation)
                            {
                                case "CT托盘位":
                                    // 执行CT托盘位
                                    ExcuteWorkstation_CTTray(workstation, camera, frameInfo, aiResult);
                                    break;
                                case "CT放料位":
                                    // 执行CT放料位
                                    ExcuteWorkstation_CTPut(workstation, camera, frameInfo, aiResult, ref diagnosis, ref flag, ref frameCacheLis, ref temp_resultFlag);
                                    break;
                                default:
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
        private void ExcuteWorkstation_CTTray(string workstation, VideoCaptureHelper camera, (DateTime dateTime, string barCode, Mat frame) frameInfo, (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult)
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
        private void ExcuteWorkstation_CTPut(string workstation, VideoCaptureHelper camera, (DateTime dateTime, string barCode, Mat frame) frameInfo, (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult, ref TrajectoryDiagnosis diagnosis, ref string? flag, ref List<(DateTime dateTime, string barCode, Mat frame, DiagnosisResult dataResult)> frameCacheLis, ref int temp_resultFlag)
        {
            if (aiResult.AIResult?.Count <= 0)
            {
                flag = aiResult.flag;
                _logger?.Error($"[{camera.Name}-{workstation}]视觉算法推理失败：[{aiResult.IsValid}]-[{aiResult.ExInfo}]");
                // AI判定放料开始：0
                if (flag == "0")
                {
                    frameCacheLis.ForEach(o => o.frame?.Dispose());
                    frameCacheLis.Clear();
                }
                return;
            }

            // 处理AI结果数据
            var imageName = (flag == "0" && aiResult.flag == "1") ? "1" : "0";
            // 大数据算法固定检测“下左角电芯”数据
            var positionName = "下左角电芯";
            if (aiResult.AIResult != null && !aiResult.AIResult[0].Exists(o => o.Name == positionName))
            {
                aiResult.AIResult[0].Add(new AIResultModel() { Name = positionName });
            }
            var x = (double)(aiResult.AIResult?[0]?.First(o => o.Name == positionName)?.Point3D?.X ?? 0);
            var y = (double)(aiResult.AIResult?[0]?.First(o => o.Name == positionName)?.Point3D?.Y ?? 0);
            // test
            var result = temp_resultFlag % 2 == 0 ? "NG" : "OK";
            // 执行大数据诊断逻辑算法
            var dataResult = diagnosis.CheckSinglePosition(imageName, positionName, x, y);
            // test
            dataResult.Status = result;

            // 异步发送实时数据：AI结果数据&大数据结果数据
            _ = FramesUpLoadWebAsync(aiResult.AIResult, positionName, dataResult);

            // AI判定放料结束：1=》0
            if (flag == "1" && aiResult.flag == "0")
            {
                temp_resultFlag++;
                // var _frameCache = frameCacheLis.ToList();
                frameCacheLis.ForEach(o => o.frame?.Dispose());
                frameCacheLis.Clear();

                // -TODO    上传web
                if (result == "NG")
                {
                    //  _ = NGFramesUpLoadWebAsync1(workstation, frameInfo.dateTime, frameInfo.barCode, frameInfo.frame.Clone(), aiResult.AIResult?[0]);
                }
            }
            flag = aiResult.flag;
            // AI判定放料进行中：1
            if (flag == "1")
            {
                frameCacheLis.Add((frameInfo.dateTime, frameInfo.barCode, frameInfo.frame.Clone(), dataResult));
            }
            // AI判定放料开始：0
            if (flag == "0")
            {
                if (aiResult.AIResult?.Count == 0)
                {
                    _logger?.Error($"[{camera.Name}-{workstation}]视觉算法推理失败：[{aiResult.IsValid}]-[{aiResult.ExInfo}]");
                    return;
                }
                frameCacheLis.ForEach(o => o.frame?.Dispose());
                frameCacheLis.Clear();
            }
        }

        /// <summary>
        /// 轨迹算法试试双穿web
        /// </summary>
        /// <param name="aiResult"></param>
        /// <param name="dataResult"></param>
        /// <returns></returns>
        private async Task FramesUpLoadWebAsync(List<List<AIResultModel>>? aiResult, string positionName, DiagnosisResult dataResult)
        {
            var categoryMap = new Dictionary<string, (string key, List<DataList> list)>();
            categoryMap.Add("电芯", ("电芯", new List<DataList>()));
            categoryMap.Add("夹爪", ("夹爪", new List<DataList>()));
            categoryMap.Add("载具", ("载具", new List<DataList>()));

            foreach (var resultModel in aiResult ?? new List<List<AIResultModel>>())
            {
                foreach (var item in resultModel)
                {
                    // 释放
                    item.Region?.Dispose();

                    var Name = item.Name;
                    var X = item.Point3D?.X ?? 0;
                    var Y = item.Point3D?.Y ?? 0;
                    var Z = item.Point3D?.Z ?? 0;

                    foreach (var kv in categoryMap)
                    {
                        if (Name.Contains(kv.Key))
                        {
                            if (Name == positionName)
                                kv.Value.list.Add(CreateDataList(Name, X, Y, Z, dataResult.Status == "OK" ? 1 : 0, (float)dataResult.Dx, (float)dataResult.Dy));
                            else
                                kv.Value.list.Add(CreateDataList(Name, X, Y, Z));
                        }
                    }

                    item.Img?.Dispose();
                    item.Region?.Dispose();
                }

                var categoryDtoList = categoryMap.Select(kv => new CategoryDtoList
                {
                    Category = kv.Value.key,
                    DataList = kv.Value.list
                }).ToList();

                var frameDtoList = new List<FrameDtoList>
                    {
                        new FrameDtoList
                        {
                            FrameTimeStamp = GetCurrentTimestampSeconds(),
                            CategoryDtoList = categoryDtoList
                        }
                    };

                var data1 = new HawkEyeSocketRequest()
                {
                    Position = "CT摄像头1",
                    Process = "CT",
                    ProductionLine = "C06",
                    ProductionBase = "枣庄",
                    DeviceId = "CT01",
                    BarCode = "xxxxx",
                    DetectionTimeStamp = GetCurrentTimestampSeconds(),
                    DetectionResult = "NG",
                    AlertLevel = 1,
                    FrameDtoList = frameDtoList
                };

                var message = System.Text.Json.JsonSerializer.Serialize(data1, new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = false
                });
                // 发送测试数据
                await WebSocketSendAsync(message);
                resultModel.Clear();
            }
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
        private DataList CreateDataList(string name, float x, float y, float z, int abnormal = 1, float xDiff = 0f, float yDiff = 0, float zDiff = 0)
        {
            return new DataList
            {
                PointName = name,
                AiPointXYZDtoList = new List<AIPointXYZDto>
                {
                    new AIPointXYZDto
                    {
                        PictureTimeStamp = GetCurrentTimestampSeconds(),
                        StatusFlag = 1,
                        MoveDirection = "x",
                        X = x,
                        Y = y,
                        Z = z,
                        Speed = 0f,
                        XDiff = xDiff,
                        YDiff = yDiff,
                        ZDiff = zDiff,
                        Abnormal = abnormal,
                        XStandard = 0f,
                        YStandard = 0f,
                        ZStandard = 0f
                    }
                }
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
        /// 视频流上传web
        /// </summary>
        /// <param name="position"></param>
        /// <param name="dateTime"></param>
        /// <param name="barCode"></param>
        /// <param name="frame"></param>
        /// <param name="AIResults"></param>
        private async Task NGFramesUpLoadWebAsync(string? position, DateTime dateTime, string barCode, Mat frame, List<AIResultModel>? AIResults)
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
                            var path = Path.Combine(Path.GetRandomFileName(), "temp.png");
                            Cv2.Rectangle(graphicalResult, AIResultModel.Rectangle, new Scalar(0, 255, 0), 2);
                            Cv2.ImWrite(path, graphicalResult);
                            file = File.ReadAllBytes(path);
                            File.Delete(path);
                        }
                        finally
                        {
                            graphicalResult?.Dispose();
                        }

                        // 时间类型转换
                        var localDateTimeOffset = new DateTimeOffset(dateTime);
                        var abnormalTimeStamp = localDateTimeOffset.ToUnixTimeSeconds();

                        var re = await _webApiClient.AiDetectionDataAsync($"{position}", $"{barCode}", abnormalTimeStamp,
                               AIResultModel.Score, AIResultModel.Width, AIResultModel.Height, AIResultModel.Area, "NG", file);
                        if (re.IsSuccess || re.Code == 200)
                            _logger.Info($"[{position}-{barCode}]AI算法结果上传web成功！");
                        else
                            _logger.Error($"[{position}-{barCode}]AI算法结果上传web失败：[{re.Message}]");
                    }
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
        private async Task NGFramesUpLoadWebAsync1(string? position, DateTime dateTime, string barCode, Mat frame, List<AIResultModel>? AIResults)
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
                            var path = Path.Combine(Path.GetRandomFileName(), "temp.png");
                            Cv2.Rectangle(graphicalResult, AIResultModel.Rectangle, new Scalar(0, 255, 0), 2);
                            Cv2.ImWrite(path, graphicalResult);
                            file = File.ReadAllBytes(path);
                            File.Delete(path);
                        }
                        finally
                        {
                            graphicalResult?.Dispose();
                        }

                        // 时间类型转换
                        var localDateTimeOffset = new DateTimeOffset(dateTime);
                        var abnormalTimeStamp = localDateTimeOffset.ToUnixTimeSeconds();

                        var re = await _webApiClient.AiDetectionDataAsync($"{position}", $"{barCode}", abnormalTimeStamp,
                               AIResultModel.Score, AIResultModel.Width, AIResultModel.Height, AIResultModel.Area, "NG", file);
                        if (re.IsSuccess || re.Code == 200)
                            _logger.Info($"[{position}-{barCode}]AI算法结果上传web成功！");
                        else
                            _logger.Error($"[{position}-{barCode}]AI算法结果上传web失败：[{re.Message}]");
                    }
                }
                finally
                {
                    frame?.Dispose();
                }
            });
            await t;
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
        private List<IWebSocketConnection> webClients = new List<IWebSocketConnection>();
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
            foreach (var webClient in webClients)
            {
                await webClient.Send(message);
            }
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
