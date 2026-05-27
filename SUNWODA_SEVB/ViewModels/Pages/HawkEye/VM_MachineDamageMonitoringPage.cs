using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Fleck;
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
using TrajectoryDiagnosisLib1;
using Yolo8Core;
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
        public VM_MachineDamageMonitoringPage
        (
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
                var psm = new ProjectSettingModel("BarCodeWatchPath", "VM_MachineDamageMonitoringPage", "D:\\Users\\2210120078\\Desktop\\新建文件夹", "string", 0)
                {
                    Remark = "电芯码监控路径（修改后需重启软件后加载！）"
                };
                _projectSettingRepository.Add(psm);
            }
            // websocket通信端口
            if (!_projectSettingRepository.Exists(m => m.BelongToVM == "VM_MachineDamageMonitoringPage" && m.Name == "ServerPort"))
            {
                var psm = new ProjectSettingModel("ServerPort", "VM_MachineDamageMonitoringPage", "8181", "int", 0)
                {
                    Remark = "监控服务监听端口（修改后需重启软件后加载！）"
                };
                _projectSettingRepository.Add(psm);
            }
            base.OnInitialize();
        }

        #region 主流程

        #region 公共参数

        private string barCode = "";
        private string modelPath1 = "";
        private string modelPath2 = "";
        private string modelPath3 = "";
        private string modelPath4 = "";
        private string modelPath5 = string.Empty;
        private string modelPath6 = string.Empty;
        private string modelPath7 = string.Empty;
        private byte[] dllBytes = new byte[0];
        private string? webBrowserControlSource;
        private WriteableBitmap? bitmap;


        private readonly JsonSerializerOptions _jsonOpt = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly string[] categories = { "电芯", "夹爪", "载具", "金属" };

        /// <summary>
        /// UE端界面源
        /// </summary>
        public string? WebBrowserControlSource
        {
            get => webBrowserControlSource;
            set => SetProperty(ref webBrowserControlSource, value);
        }

        #endregion

        /// <summary>
        /// 初始化进程
        /// </summary>
        private async void Init_Process()
        {
            var barCodeWatchPath = _projectSettingRepository.GetByNameAndVM("BarCodeWatchPath", "VM_MachineDamageMonitoringPage")?.Value ?? ""; ;
            InitBarCodeWatcher(barCodeWatchPath);
            var serverPort = _projectSettingRepository.GetByNameAndVM("ServerPort", "VM_MachineDamageMonitoringPage")?.Value ?? "8181"; ;
            WebBrowserControlSource = _globalSettingRepository.GetByName("WebTestUrlSysCenter")?.Value.Split(":7035").FirstOrDefault() + "/hsfdev/";

            StartServer(int.Parse(serverPort));

            // 加载配置上传相机配置信息到web端
            var cameras = GetCameras();
            var re = await _webApiClient.AddCameraAsync(cameras);

            if (re.IsSuccess || re.Code == 200)
                _logger.Info($"相机配置上传成功！");
            else
                _logger.Error($"相机配置上传失败：[{re.Message}]");

            // 从web获取AI算法模型文件onnx  —— TODO
            //modelPath = await DownloadOnnxAsync("ZZ_YY_C01_DP_DLL", "ZZ_YY", "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCQiNCeYgNIL35V99cJ0giEOtOCd6tf9fVEVCuFmFu0zuskPJYB0D1pUcYjLtJfadGa1HflWYINl/zk9bq0fDipuz5vPkswsp0N4efiVzwoBbzUTpLPePNkpb6rDyd+3RUNAC8UtZU2ptKGCNVvvgMQGZp+2mnIKPamuEXzK5qkkwIDAQAB");
            //modelPath1 = await DownloadOnnxAsync("ZZ_YY_C01_DP_DLL", "ZZ_YY", "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCQiNCeYgNIL35V99cJ0giEOtOCd6tf9fVEVCuFmFu0zuskPJYB0D1pUcYjLtJfadGa1HflWYINl/zk9bq0fDipuz5vPkswsp0N4efiVzwoBbzUTpLPePNkpb6rDyd+3RUNAC8UtZU2ptKGCNVvvgMQGZp+2mnIKPamuEXzK5qkkwIDAQAB");
            modelPath1 = "../Configs/best.onnx";
            modelPath2 = "../Configs/best[托盘].onnx";
            modelPath3 = "../Configs/best[PTO叠片粘电芯].onnx";
            modelPath4 = "../Configs/best_qufangliao.onnx";
            modelPath5 = "../Configs/枣庄热压.onnx";
            modelPath6 = "../Configs/枣庄连接片上料.onnx";
            modelPath7 = "../Configs/枣庄连接片下料.onnx";

            cameras.ForEach(o => VM_CameraConfigurationManagementPage.CameraConfig.Add(o));

            // 从web获取AI视频流算法库文件dll  —— TODO
            //dllBytes = await DownloadDllAsync("ZZ_YY_C01_DP_DLL", "ZZ_YY", "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCQiNCeYgNIL35V99cJ0giEOtOCd6tf9fVEVCuFmFu0zuskPJYB0D1pUcYjLtJfadGa1HflWYINl/zk9bq0fDipuz5vPkswsp0N4efiVzwoBbzUTpLPePNkpb6rDyd+3RUNAC8UtZU2ptKGCNVvvgMQGZp+2mnIKPamuEXzK5qkkwIDAQAB");
            CamerasMonitor_Thread();
        }

        /// <summary>
        /// 相机检测：检测流程自动开启关闭
        /// </summary>
        private void CamerasMonitor_Thread()
        {
            var videoCaptureCache = new ConcurrentDictionary<string, VideoCaptureHelper>();
            var machineDamageMonitoringCache = new ConcurrentDictionary<string, CancellationTokenSource>();

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        foreach (var camera in VM_CameraConfigurationManagementPage.CameraConfig ?? [])
                        {
                            // ===== 停用 =====
                            if (!camera.Enable)
                            {
                                if (machineDamageMonitoringCache.TryRemove(camera.GuidId, out var cts))
                                {
                                    _logger.Info($"[{camera.Name}] 停止机损检测线程");
                                    cts.Cancel();
                                    cts.Dispose();
                                }

                                if (videoCaptureCache.TryRemove(camera.GuidId, out var vc))
                                {
                                    _logger.Info($"[{camera.Name}] 断开相机");
                                    vc.DisConnect();
                                }
                                continue;
                            }

                            // ===== 连接相机 =====
                            var videoCapture = videoCaptureCache.GetOrAdd(camera.GuidId, _ =>
                            {
                                _logger.Info($"[{camera.Name}] 相机连接中...");
                                var vc = new VideoCaptureHelper(_logger, camera.Name, $"{camera.Remake}");
                                vc.Connect();
                                _logger.Info($"[{camera.Name}] 相机已连接");
                                return vc;
                            });

                            // ===== 启动机损线程 =====
                            if (!machineDamageMonitoringCache.ContainsKey(camera.GuidId))
                            {
                                var cts = new CancellationTokenSource();
                                if (machineDamageMonitoringCache.TryAdd(camera.GuidId, cts))
                                {
                                    MachineDamageMonitoring_Thread(camera, videoCapture, cts.Token);
                                    _logger.Info($"[{camera.Name}] 工位:{camera.WorkStation} 机损检测启动");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("相机&机损监控异常", ex);
                    }
                    finally
                    {
                        await Task.Delay(2000);
                    }
                }
            });
        }

        private void MachineDamageMonitoring_Thread(CameraModel cameraConfig, VideoCaptureHelper camera, CancellationToken token)
        {
            #region 初始化AI算法
            // 初始化算法实例
            var (YingYanAI, ModelInit, ModelAction, Undistorted, CropImgMat) = Init_YingYanAI(dllBytes);
            _logger.Info($"[{camera.Name}-{cameraConfig.WorkStation}]初始化算法实例成功！");

            var modelPath = cameraConfig.WorkStation switch
            {
                "CT托盘位" => modelPath2,
                "CT放料位" => modelPath1,
                "PTO叠片粘电芯" => modelPath3,
                "PTO叠片取放料" => modelPath4,
                "ZZ热压" => modelPath5,
                "ZZ连接片上料" => modelPath6,
                "ZZ连接片下料" => modelPath7,
                "测试" => "",
                _ => throw new Exception($"[{camera.Name}]-{cameraConfig.WorkStation}]工位未实现相关功能!!!")
            };

            var modelInit = Execute_ModelInit(modelPath, cameraConfig.WorkStation, ModelInit, YingYanAI);

            if (!modelInit.IsValid || modelInit.H246modle == null)
            {
                _logger.Error($"[{camera.Name}]-{cameraConfig.WorkStation}]视觉算法模型初始化异常：[{modelInit.IsValid}]-[{modelInit.ExInfo}]");
                throw new Exception($"[{camera.Name}]-{cameraConfig.WorkStation}]工位未实现相关功能!!!");
            }
            _logger.Info($"[{camera.Name}-{cameraConfig.WorkStation}]模型预加载成功！，模型为{modelInit.H246modle}");

            var aiModels = new List<IPredictor> { modelInit.H246modle };

            #endregion

            // ================= 帧缓存 =================
            var frameCache = new BlockingCollection<(long ts, string barCode, Mat frame)>(50);

            // ================= 相机帧生产者 =================
            void OnFrame(Mat frame)
            {
                //if (token.IsCancellationRequested || frameCache.IsAddingCompleted)
                //{
                //    frame?.Dispose();
                //    return;
                //}

                //try
                //{
                //    frameCache.Add((GetCurrentTimestampSeconds(), string.Empty, frame), token);
                //    _logger.Info($"[{camera.Name}-{cameraConfig.WorkStation}]-获取流当前时间为{DateTime.Now: HH:mm:ss.fff}");
                //}
                //catch
                //{
                //    frame?.Dispose();
                //}
            }

            camera.ExecuteFrame += OnFrame;
            camera.StartReading();
            _logger.Info($"相机[{camera.Name}-{cameraConfig.WorkStation}]加载完成！");

            #region test
            Task.Run(() =>
            {
                var path = cameraConfig.WorkStation switch
                {
                    "CT托盘位" => "../Configs/放料模拟数据",
                    "CT放料位" => "../Configs/放料模拟数据",
                    "PTO叠片粘电芯" => "../Configs/PTO叠片粘电芯数据/后角边粘连",
                    "PTO叠片取放料" => "../Configs/PTO叠片取放料数据/5mm",
                    "ZZ热压" => "../Configs/枣庄热压NG数据\\20260331171150839-右侧轻微\\523",
                    "ZZ连接片上料" => "../Configs/枣庄连接片上料OK数据-20帧\\11013",
                    "ZZ连接片下料" => "../Configs/枣庄下料数据\\下料OK数据\\20260110105954421_use2test",
                    _ => throw new NotImplementedException()
                };

                var fileDirectory = Directory.GetFiles(path)
                .OrderBy(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    var num = int.Parse(Regex.Match(name, @"\d+").Value);
                    return num;
                })
                .ToArray();

                for (int i = 0; i < fileDirectory.Length; i++)
                {
                    var f = Cv2.ImRead(fileDirectory[i], ImreadModes.Color);
                    frameCache.Add((GetCurrentTimestampSeconds(), $"{Path.GetFileName(fileDirectory[i])}", f));
                }
            });
            #endregion

            // ================= 帧处理线程 =================
            Task.Run(() =>
            {
                var flag = "0";
                var lastFrameIsValid = false;
                var diagnosis = new UnifiedTrajectoryDiagnosis();

                List<Mat> abnormalFrames = [];
                List<FrameDtoList> frameResultDataList = [];
                List<string> resultStatus = [];
                List<Mat> abnormalKeyFrames = [];
                string abnormaVideoStatus = string.Empty;
                string? currentProcessFolder = null;
                int frameIndex = 0;

                try
                {
                    foreach (var item in frameCache.GetConsumingEnumerable(token))
                    {
                        var frameInfo = item;
                        try
                        {
                            // ====== 去畸变 ======
                            if (cameraConfig.RemoveDistortion && File.Exists(cameraConfig.CalibrationFilePath))
                            {
                                try
                                {
                                    var json = File.ReadAllText(cameraConfig.CalibrationFilePath);
                                    var cal = JsonConvert.DeserializeObject<CalibrationResult>(json);

                                    if (cal != null)
                                    {
                                        var newFrame = Execute_Undistorted(cal, Undistorted, YingYanAI, frameInfo.frame);

                                        if (!ReferenceEquals(newFrame, frameInfo.frame))
                                        {
                                            // 释放旧帧
                                            frameInfo.frame.Dispose();
                                            frameInfo = (frameInfo.ts, frameInfo.barCode, newFrame);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.Warn($"[{camera.Name}] 去畸变失败：{ex.Message}");
                                }
                            }

                            // ====== 实时显示 ======
                            if (cameraConfig.RealTimeDisplay)
                            {
                                RunOnUIThread(() =>
                                {
                                    if (!frameInfo.frame.Empty())
                                        RenderFrame(frameInfo.frame, cameraConfig);
                                });
                            }

                            // ====== AI 推理 ======
                            var aiResult = Execute_ModelAction
                            (
                                cameraConfig.WorkStation,
                                flag,
                                [frameInfo.frame],
                                aiModels,
                                ModelAction,
                                YingYanAI
                            );

                            // ====== 工位分发 ======
                            switch (cameraConfig.WorkStation)
                            {
                                case "CT托盘位":
                                    ExcuteWorkstation_CTTray(cameraConfig.WorkStation, camera, frameInfo, aiResult);
                                    break;
                                case "CT放料位":
                                    ExcuteWorkstation_CTPut
                                    (
                                        cameraConfig.WorkStation, cameraConfig.Resolution,
                                        camera, frameInfo, aiResult, CropImgMat,
                                        YingYanAI, ref diagnosis, ref flag,
                                        ref lastFrameIsValid, ref abnormalFrames, ref frameResultDataList,
                                        ref resultStatus, ref abnormaVideoStatus, ref abnormalKeyFrames
                                    );
                                    break;
                                case "PTO叠片粘电芯":
                                    ExcuteWorkstation_PTOStackingBondingBatteryCell
                                    (
                                        cameraConfig.WorkStation, cameraConfig.Resolution,
                                        camera, frameInfo, aiResult, CropImgMat,
                                        YingYanAI, ref diagnosis, ref flag,
                                        ref lastFrameIsValid, ref abnormalFrames, ref frameResultDataList,
                                        ref resultStatus, ref abnormaVideoStatus, ref abnormalKeyFrames
                                    );
                                    break;
                                case "ZZ热压":
                                    ExcuteWorkstation_ZZHotPress
                                    (
                                        cameraConfig.WorkStation, cameraConfig.Resolution, cameraConfig.VideoSaving,
                                        camera, frameInfo, aiResult, CropImgMat,
                                        YingYanAI, ref diagnosis, ref flag,
                                        ref lastFrameIsValid, ref abnormalFrames, ref frameResultDataList,
                                        ref resultStatus, ref abnormaVideoStatus, ref abnormalKeyFrames,
                                        ref currentProcessFolder, ref frameIndex
                                    );
                                    break;
                                case "PTO叠片取放料":
                                    ExcuteWorkstation_PTOStackingPut
                                    (
                                        cameraConfig.WorkStation, cameraConfig.Resolution,
                                        camera, frameInfo, aiResult, CropImgMat,
                                        YingYanAI, ref diagnosis, ref flag,
                                        ref lastFrameIsValid, ref abnormalFrames, ref frameResultDataList,
                                        ref resultStatus, ref abnormaVideoStatus, ref abnormalKeyFrames
                                    );
                                    break;
                                case "ZZ连接片上料":
                                    ExcuteWorkstation_ZZConnection
                                    (
                                        cameraConfig.WorkStation, cameraConfig.Resolution, cameraConfig.VideoSaving,
                                        camera, frameInfo, aiResult, CropImgMat,
                                        YingYanAI, ref diagnosis, ref flag,
                                        ref lastFrameIsValid, ref abnormalFrames, ref frameResultDataList,
                                        ref resultStatus, ref abnormaVideoStatus, ref abnormalKeyFrames,
                                        ref currentProcessFolder, ref frameIndex
                                    );
                                    break;
                                case "ZZ连接片下料":
                                    ExcuteWorkstation_ZZConnection_Release
                                    (
                                        cameraConfig.WorkStation, cameraConfig.Resolution, cameraConfig.VideoSaving,
                                        camera, frameInfo, aiResult, CropImgMat,
                                        YingYanAI, ref diagnosis, ref flag,
                                        ref lastFrameIsValid, ref abnormalFrames, ref frameResultDataList,
                                        ref resultStatus, ref abnormaVideoStatus, ref abnormalKeyFrames,
                                        ref currentProcessFolder, ref frameIndex
                                    );
                                    break;
                                default:
                                    flag = aiResult.flag;
                                    _logger?.Warn($"未实现[{camera.Name}-{cameraConfig.WorkStation}]逻辑！");
                                    break;
                            }
                        }
                        finally
                        {
                            frameInfo.frame?.Dispose();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.Info($"[{camera.Name}-{cameraConfig.WorkStation}] 机损线程取消");
                }
                finally
                {
                    frameCache.CompleteAdding();
                    camera.ExecuteFrame -= OnFrame;
                    camera.StopReading();
                    _logger.Info($"[{camera.Name}-{cameraConfig.WorkStation}] 机损线程已退出");
                }
            }, token);
        }            

        /// <summary>
        /// 渲染帧
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="cameraConfig"></param>
        private void RenderFrame(Mat frame, CameraModel cameraConfig)
        {
            if (bitmap == null || bitmap.PixelWidth != frame.Width || bitmap.PixelHeight != frame.Height)
            {
                InitBitmap(frame, cameraConfig);
            }

            bitmap!.Lock();

            unsafe
            {
                Buffer.MemoryCopy
                (
                    frame.DataPointer,
                    (void*)bitmap.BackBuffer,
                    bitmap.BackBufferStride * bitmap.PixelHeight,
                    frame.Step() * frame.Rows
                );
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, frame.Width, frame.Height));

            bitmap.Unlock();
        }

        /// <summary>
        /// 初始化位图
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="cameraConfig"></param>
        private void InitBitmap(Mat frame, CameraModel cameraConfig)
        {
            // OpenCV 默认 BGR
            bitmap = new WriteableBitmap
            (
                frame.Width,
                frame.Height,
                96,
                96,
                PixelFormats.Bgr24,
                null
            );

            cameraConfig.ImageSource = bitmap;
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
            for (int i = 0; i < aiResult.AIResult?.Count; i++)
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
            double resolution,
            VideoCaptureHelper camera,
            (long dateTime, string barCode, Mat frame) frameInfo,
            (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult,
            MethodInfo? CropImgMat,
            object? YingYanAI,
            ref UnifiedTrajectoryDiagnosis diagnosis,
            ref string? flag,
            ref bool lastFrameIsValid,
            ref List<Mat> abnormalFrames,
            ref List<FrameDtoList> frameResultDataList,
            ref List<string> resultStatus,
            ref string abnormaVideoStatus,
            ref List<Mat> abnormalKeyFrames
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

            // === 获取异常视频的类型 ===
            if (!string.IsNullOrEmpty(dataResultLis.FirstOrDefault(o => o.PositionName == "下左角电芯")?.AbnormalType))
            {
                abnormaVideoStatus = dataResultLis.FirstOrDefault(o => o.PositionName == "下左角电芯")?.AbnormalType ?? "NG";
            }

            // === 图像切割获取异常关键帧 === 
            if (dataResultLis.FirstOrDefault(o => o.PositionName == "下左角电芯")?.AbnormalDetected == 1 || dataResultLis.FirstOrDefault(o => o.PositionName == "下左角电芯")?.AbnormalDetected == 2)
            {
                Execute_ImageCutting(frameInfo.frame, CropImgMat, YingYanAI, abnormalKeyFrames);
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
                        _ = NGVideoUpLoadWebAsync("上料工位", frameInfo.dateTime, frameInfo.barCode, abnormalFrames, abnormalKeyFrames, frameResultDataList, abnormaVideoStatus, resolution);
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
            string workstation,
            double resolution,
            VideoCaptureHelper camera,
            (long dateTime, string barCode, Mat frame) frameInfo,
            (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult,
            MethodInfo? CropImgMat,
            object? YingYanAI,
            ref UnifiedTrajectoryDiagnosis diagnosis,
            ref string? flag,
            ref bool lastFrameIsValid,
            ref List<Mat> abnormalFrames,
            ref List<FrameDtoList> frameResultDataList,
            ref List<string> resultStatus,
            ref string abnormaVideoStatus,
            ref List<Mat> abnormalKeyFrames
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

                // === 执行诊断逻辑 ===
                _logger.Info($"[{camera.Name}-{workstation}]-准备执行大数据轨迹算法");
                Stopwatch stopwatch = new();
                stopwatch.Restart();
                diagnosis.SelectProgram("PTO叠片粘电芯");

                UnifiedDiagnosisResult diagnosisResult = diagnosis.ExecuteDiagnosis
                (
                    frameStatus,
                    left.Point3D.X, left.Point3D.Y,
                    mid.Point3D.X, mid.Point3D.Y,
                    right.Point3D.X, right.Point3D.Y
                );
                stopwatch.Stop();
                _logger.Info($"[{camera.Name} - {workstation}]-大数据轨迹算法执行完毕,耗时为{stopwatch.ElapsedMilliseconds}");

                // === 获取异常视频的类型 ===
                if (!string.IsNullOrEmpty(diagnosisResult.AbnormalType))
                    abnormaVideoStatus = diagnosisResult.AbnormalType;

                // === 图像切割获取异常关键帧 ===                
                if (diagnosisResult.AbnormalDetected == 1 || diagnosisResult.AbnormalDetected == 2)
                {
                    _logger.Info($"[{camera.Name}-{workstation}]-准备执行视觉图像分割算法");
                    Execute_ImageCutting(frameInfo.frame, CropImgMat, YingYanAI, abnormalKeyFrames);
                }

                // === 日志 ===
                _logger?.Info($"[{camera.Name}-{workstation}] 流程执行完成：" + $"[Valid={aiResult.IsValid}] [当前时间为：{DateTime.Now:HH:mm:ss:fff}]");

                // === 根据状态执行不同动作 ===
                switch (frameStatus)
                {
                    case "1": // 起始帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到起始帧");
                        abnormalFrames.ForEach(frameInfo => frameInfo?.Dispose());
                        abnormalFrames.Clear();

                        abnormalKeyFrames.ForEach(f => f.Dispose());
                        abnormalKeyFrames.Clear();

                        abnormalFrames.Add(frameInfo.frame.Clone());

                        resultStatus.Clear();
                        resultStatus.Add(diagnosisResult.Status);

                        frameResultDataList.Clear();
                        break;

                    case "0": // 过程帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到过程帧");
                        abnormalFrames.Add(frameInfo.frame.Clone());
                        resultStatus.Add(diagnosisResult.Status);
                        break;

                    case "2": // 结束帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到结束帧");
                        // 异步发送实时数据：AI结果数据&大数据结果数据
                        _ = FramesUpLoadWebAsync("热压点位", resultList, [diagnosisResult], frameResultDataList, aiResult.IsValid, int.Parse(frameStatus));

                        if (resultStatus.Contains("NG"))
                        {
                            _logger?.Info($"[{camera.Name}-{workstation}]-识别到NG状态,准备开始执行生成异常视频流程");
                            _ = NGVideoUpLoadWebAsync("热压点位", frameInfo.dateTime, frameInfo.barCode, abnormalFrames, abnormalKeyFrames, frameResultDataList, abnormaVideoStatus, resolution);
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


        private void ExcuteWorkstation_ZZHotPress
        (
           string workstation,
           double resolution,
           bool videoSaving,
           VideoCaptureHelper camera,
           (long dateTime, string barCode, Mat frame) frameInfo,
           (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult,
           MethodInfo? CropImgMat,
           object? YingYanAI,
           ref UnifiedTrajectoryDiagnosis diagnosis,
           ref string? flag,
           ref bool lastFrameIsValid,
           ref List<Mat> abnormalFrames,
           ref List<FrameDtoList> frameResultDataList,
           ref List<string> resultStatus,
           ref string abnormaVideoStatus,
           ref List<Mat> abnormalKeyFrames,
           ref string? currentProcessFolder,
           ref int frameIndex
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
                string[] requiredNames = { "中心点", "左点", "右点", "金属左下点" };
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
                var MetalPlate = GetPoint("金属左下点");

                SaveTXTFile($"../结果数据", "枣庄热压结果数据", $"{frameInfo.barCode}\t左点电芯X\t{left.Point3D.X}\t左点电芯Y\t{left.Point3D.Y}\t中心点电芯X\t{mid.Point3D.X}\t中心点电芯Y\t{mid.Point3D.Y}\t右点电芯X\t{right.Point3D.X}\t右点电芯Y\t{right.Point3D.Y}\t金属左下点\t{MetalPlate.Point3D.X}\t金属左下点\t{MetalPlate.Point3D.Y}");

                // === 执行诊断逻辑 ===
                _logger.Info($"[{camera.Name}-{workstation}]-准备执行大数据轨迹算法");
                Stopwatch stopwatch = new();
                stopwatch.Restart();
                diagnosis.SelectProgram("枣庄W04叠片热压");

                var diagnosisResult = diagnosis.ExecuteDiagnosis
                (
                    frameStatus,
                    left.Point3D.X, left.Point3D.Y,
                    mid.Point3D.X, mid.Point3D.Y,
                    right.Point3D.X, right.Point3D.Y,
                    MetalPlate.Point3D.X, MetalPlate.Point3D.Y
                );

                stopwatch.Stop();
                _logger.Info($"[{camera.Name} - {workstation}]-大数据轨迹算法执行完毕,耗时为{stopwatch.ElapsedMilliseconds}");

                // === 获取异常视频的类型 ===
                if (!string.IsNullOrEmpty(diagnosisResult.AbnormalType))
                    abnormaVideoStatus = diagnosisResult.AbnormalType;

                // === 图像切割获取异常关键帧 ===                
                //if (diagnosisResult.AbnormalDetected == 1 || diagnosisResult.AbnormalDetected == 2)
                //{
                //    logger.Info($"[{camera.Name}-{workstation}]-准备执行视觉图像分割算法");
                //    Execute_ImageCutting(frameInfo.frame, CropImgMat, YingYanAI, abnormalKeyFrames);
                //}

                // === 日志 ===
                _logger?.Info($"[{camera.Name}-{workstation}] 流程执行完成：" + $"[Valid={aiResult.IsValid}] [当前时间为：{DateTime.Now:HH:mm:ss:fff}]");

                // === 根据状态执行不同动作 ===
                switch (frameStatus)
                {
                    case "1": // 起始帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到起始帧");
                        abnormalFrames.ForEach(frameInfo => frameInfo?.Dispose());
                        abnormalFrames.Clear();

                        abnormalKeyFrames.ForEach(f => f.Dispose());
                        abnormalKeyFrames.Clear();

                        resultStatus.Clear();
                        frameResultDataList.Clear();

                        // 保存过程帧图片
                        if (videoSaving)
                        {
                            string root = $"../{workstation}过程图片/{DateTime.Now:yyyy-MM}/{DateTime.Now:dd}";
                            if (!Directory.Exists(root))
                                Directory.CreateDirectory(root);

                            string folderName = $"{DateTime.Now:HH-mm-ss.fff}_{frameInfo.barCode}";
                            currentProcessFolder = Path.Combine(root, folderName);

                            Directory.CreateDirectory(currentProcessFolder);

                            frameIndex = 0;

                            SaveFrameToFolder(frameInfo.frame, currentProcessFolder, ref frameIndex);
                        }

                        abnormalFrames.Add(frameInfo.frame.Clone());
                        resultStatus.Add(diagnosisResult.Status);

                        break;

                    case "0": // 过程帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到过程帧");

                        if (videoSaving)
                            SaveFrameToFolder(frameInfo.frame, currentProcessFolder, ref frameIndex);

                        abnormalFrames.Add(frameInfo.frame.Clone());
                        resultStatus.Add(diagnosisResult.Status);
                        break;

                    case "2": // 结束帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到结束帧");

                        if (videoSaving)
                            SaveFrameToFolder(frameInfo.frame, currentProcessFolder, ref frameIndex);

                        if (resultStatus.Contains("NG"))
                        {
                            _logger?.Info($"[{camera.Name}-{workstation}]-识别到NG状态,准备开始执行生成异常视频流程");
                            _ = NGVideoUpLoadWebAsync("热压点位", frameInfo.dateTime, frameInfo.barCode, abnormalFrames, abnormalKeyFrames, frameResultDataList, abnormaVideoStatus, resolution);
                        }

                        abnormalFrames.ForEach(f => f.Dispose());
                        abnormalFrames.Clear();
                        resultStatus.Clear();

                        currentProcessFolder = null;
                        frameIndex = 0;
                        break;
                }

                // 异步发送实时数据：AI结果数据&大数据结果数据
                _ = FramesUpLoadWebAsync("热压点位", resultList, [diagnosisResult], frameResultDataList, aiResult.IsValid, int.Parse(frameStatus));

                // === 更新帧状态 ===
                lastFrameIsValid = aiResult.IsValid;
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
            string workstation,
            double resolution,
            VideoCaptureHelper camera,
            (long dateTime, string barCode, Mat frame) frameInfo,
            (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult,
            MethodInfo? CropImgMat,
            object? YingYanAI,
            ref UnifiedTrajectoryDiagnosis diagnosis,
            ref string? flag,
            ref bool lastFrameIsValid,
            ref List<Mat> abnormalFrames,
            ref List<FrameDtoList> frameResultDataList,
            ref List<string> resultStatus,
            ref string abnormaVideoStatus,
            ref List<Mat> abnormalKeyFrames
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

                //SaveTXTFile($"../结果数据", "取放料结果数据", $"{frameInfo.barCode}\t左点电芯X\t{left.Point3D.X}\t左点电芯Y\t{left.Point3D.Y}\t中心点电芯X\t{mid.Point3D.X}\t中心点电芯Y\t{mid.Point3D.Y}\t右点电芯X\t{right.Point3D.X}\t右点电芯Y\t{right.Point3D.Y}");

                // === 执行轨迹诊断逻辑 ===
                _logger.Info($"[{camera.Name}-{workstation}]-准备执行大数据轨迹算法");
                Stopwatch stopwatch = new();
                stopwatch.Restart();
                diagnosis.SelectProgram("PTO叠片取放料");

                UnifiedDiagnosisResult diagnosisResult = diagnosis.ExecuteDiagnosis
                (
                    frameStatus,
                    left.Point3D.X, left.Point3D.Y,
                    mid.Point3D.X, mid.Point3D.Y,
                    right.Point3D.X, right.Point3D.Y
                );
                stopwatch.Stop();
                _logger.Info($"[{camera.Name} - {workstation}]-大数据轨迹算法执行完毕,耗时为{stopwatch.ElapsedMilliseconds}");

                // === 获取异常视频的类型 ===
                if (!string.IsNullOrEmpty(diagnosisResult.AbnormalType))
                    abnormaVideoStatus = diagnosisResult.AbnormalType;

                // === 图像切割获取异常关键帧 === 
                if (diagnosisResult.AbnormalDetected == 1 || diagnosisResult.AbnormalDetected == 2)
                {
                    _logger.Info($"[{camera.Name}-{workstation}]-准备执行视觉图像分割算法");
                    Execute_ImageCutting(frameInfo.frame, CropImgMat, YingYanAI, abnormalKeyFrames);
                }

                // === 日志 ===
                _logger?.Info($"[{camera.Name}-{workstation}] 流程执行完成：" + $"[Valid={aiResult.IsValid}] [当前时间为：{DateTime.Now:HH:mm:ss:fff}]");

                // === 根据状态执行不同动作 ===
                switch (frameStatus)
                {
                    case "1": // 起始帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到起始帧");
                        abnormalFrames.ForEach(frameInfo => frameInfo?.Dispose());
                        abnormalFrames.Clear();

                        abnormalKeyFrames.ForEach(f => f.Dispose());
                        abnormalKeyFrames.Clear();

                        abnormalFrames.Add(frameInfo.frame.Clone());

                        resultStatus.Clear();
                        resultStatus.Add(diagnosisResult.Status);

                        frameResultDataList.Clear();
                        break;

                    case "0": // 过程帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到过程帧");
                        abnormalFrames.Add(frameInfo.frame.Clone());
                        resultStatus.Add(diagnosisResult.Status);
                        break;

                    case "2": // 结束帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到结束帧");
                        // 异步发送实时数据：AI结果数据&大数据结果数据
                        _ = FramesUpLoadWebAsync("电芯转运点位", resultList, [diagnosisResult], frameResultDataList, aiResult.IsValid, int.Parse(frameStatus));

                        if (resultStatus.Contains("NG"))
                        {
                            _logger?.Info($"[{camera.Name}-{workstation}]-识别到NG状态,准备开始执行生成异常视频流程");
                            _ = NGVideoUpLoadWebAsync("电芯转运点位", frameInfo.dateTime, frameInfo.barCode, abnormalFrames, abnormalKeyFrames, frameResultDataList, abnormaVideoStatus, resolution);
                        }

                        abnormalFrames.ForEach(f => f.Dispose());
                        abnormalFrames.Clear();
                        abnormalKeyFrames.ForEach(f => f.Dispose());
                        abnormalKeyFrames.Clear();
                        resultStatus.Clear();
                        break;
                }

                if (frameStatus != "2")
                    // 异步发送实时数据：AI结果数据&大数据结果数据
                    _ = FramesUpLoadWebAsync("电芯转运点位", resultList, [diagnosisResult], frameResultDataList, aiResult.IsValid, int.Parse(frameStatus));

                // === 更新帧状态 ===
                lastFrameIsValid = aiResult.IsValid;
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

        private void ExcuteWorkstation_ZZConnection
        (
           string workstation,
           double resolution,
           bool videoSaving,
           VideoCaptureHelper camera,
           (long dateTime, string barCode, Mat frame) frameInfo,
           (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult,
           MethodInfo? CropImgMat,
           object? YingYanAI,
           ref UnifiedTrajectoryDiagnosis diagnosis,
           ref string? flag,
           ref bool lastFrameIsValid,
           ref List<Mat> abnormalFrames,
           ref List<FrameDtoList> frameResultDataList,
           ref List<string> resultStatus,
           ref string abnormaVideoStatus,
           ref List<Mat> abnormalKeyFrames,
           ref string? currentProcessFolder,
           ref int frameIndex
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

                // === 取出关键点，防止空引用 ===
                AIResultModel GetPoint(string keyword) => resultList.FirstOrDefault(o => o.Name.Contains(keyword)) ?? new AIResultModel { Point3D = new AIpointXYZ() };

                var leftClaw1 = GetPoint("左1夹爪");
                var leftCell1 = GetPoint("左1电芯");
                var leftClaw2 = GetPoint("左2夹爪");
                var leftCell2 = GetPoint("左2电芯");
                var rightClaw1 = GetPoint("右1夹爪");
                var rightCell1 = GetPoint("右1电芯");
                var rightClaw2 = GetPoint("右2夹爪");
                var rightCell2 = GetPoint("右2电芯");

                SaveTXTFile($"../结果数据", "枣庄连接片结果数据",
                    $"{frameInfo.barCode}\t左1夹爪X\t{leftClaw1.Point3D.X}\t左1夹爪Y\t{leftClaw1.Point3D.Y}\t" +
                    $"左1电芯X\t{leftCell1.Point3D.X}\t左1电芯Y\t{leftCell1.Point3D.Y}\t" +
                    $"左2夹爪X\t{leftClaw2.Point3D.X}\t左2夹爪Y\t{leftClaw2.Point3D.Y}\t" +
                    $"左2电芯X\t{leftCell2.Point3D.X}\t左2电芯Y\t{leftCell2.Point3D.Y}\t" +
                    $"右1夹爪X\t{rightClaw1.Point3D.X}\t右1夹爪Y\t{rightClaw1.Point3D.Y}\t" +
                    $"右1电芯X\t{rightCell1.Point3D.X}\t右1电芯Y\t{rightCell1.Point3D.Y}\t" +
                    $"右2夹爪X\t{rightClaw2.Point3D.X}\t右2夹爪Y\t{rightClaw2.Point3D.Y}\t" +
                    $"右2电芯X\t{rightCell2.Point3D.X}\t右2电芯Y\t{rightCell2.Point3D.Y}\t"
                    );

                // === 执行诊断逻辑 ===
                _logger.Info($"[{camera.Name}-{workstation}]-准备执行大数据轨迹算法");
                Stopwatch stopwatch = new();
                stopwatch.Restart();
                diagnosis.SelectProgram("枣庄W04连接片上料");

                var diagnosisResult = diagnosis.ExecuteDiagnosis
                (
                    frameStatus,
                    leftClaw1.Point3D.X, leftClaw1.Point3D.Y,
                    leftCell1.Point3D.X, leftCell1.Point3D.Y,
                    leftClaw2.Point3D.X, leftClaw2.Point3D.Y,
                    leftCell2.Point3D.X, leftCell2.Point3D.Y,
                    rightClaw1.Point3D.X, rightClaw1.Point3D.Y,
                    rightCell1.Point3D.X, rightCell1.Point3D.Y,
                    rightClaw2.Point3D.X, rightClaw2.Point3D.Y,
                    rightCell2.Point3D.X, rightCell2.Point3D.Y
                );
                stopwatch.Stop();
                _logger.Info($"[{camera.Name} - {workstation}]-大数据轨迹算法执行完毕,耗时为{stopwatch.ElapsedMilliseconds}");

                // === 获取异常视频的类型 ===
                if (!string.IsNullOrEmpty(diagnosisResult.AbnormalType))
                    abnormaVideoStatus = diagnosisResult.AbnormalType;

                // === 日志 ===
                _logger?.Info($"[{camera.Name}-{workstation}] 流程执行完成：" + $"[Valid={aiResult.IsValid}] [当前时间为：{DateTime.Now:HH:mm:ss:fff}]");

                // === 根据状态执行不同动作 ===
                switch (frameStatus)
                {
                    case "1": // 起始帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到起始帧");
                        abnormalFrames.ForEach(frameInfo => frameInfo?.Dispose());
                        abnormalFrames.Clear();

                        abnormalKeyFrames.ForEach(f => f.Dispose());
                        abnormalKeyFrames.Clear();

                        resultStatus.Clear();
                        frameResultDataList.Clear();

                        // 保存过程帧图片
                        if (videoSaving)
                        {
                            string root = $"../{workstation}过程图片/{DateTime.Now:yyyy-MM}/{DateTime.Now:dd}";
                            if (!Directory.Exists(root))
                                Directory.CreateDirectory(root);

                            string folderName = $"{DateTime.Now:HH-mm-ss.fff}_{frameInfo.barCode}";
                            currentProcessFolder = Path.Combine(root, folderName);

                            Directory.CreateDirectory(currentProcessFolder);

                            frameIndex = 0;

                            SaveFrameToFolder(frameInfo.frame, currentProcessFolder, ref frameIndex);
                        }
                       
                        abnormalFrames.Add(frameInfo.frame.Clone());
                        resultStatus.Add(diagnosisResult.Status);

                        break;

                    case "0": // 过程帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到过程帧");

                        if (videoSaving)
                            SaveFrameToFolder(frameInfo.frame, currentProcessFolder, ref frameIndex);

                        abnormalFrames.Add(frameInfo.frame.Clone());
                        resultStatus.Add(diagnosisResult.Status);
                        break;

                    case "2": // 结束帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到结束帧");

                        if (resultStatus.Contains("NG"))
                        {
                            _logger?.Info($"[{camera.Name}-{workstation}]-识别到NG状态,准备开始执行生成异常视频流程");
                            var abnormalTimeStamp = GetCurrentTimestampSeconds();
                            _ = NGVideoUpLoadWebAsync("上料点位", abnormalTimeStamp, frameInfo.barCode, abnormalFrames, frameResultDataList, abnormaVideoStatus, resolution);
                        }

                        abnormalFrames.ForEach(f => f.Dispose());
                        abnormalFrames.Clear();
                        resultStatus.Clear();

                        currentProcessFolder = null;
                        frameIndex = 0;
                        break;
                }

                if (frameStatus != "2")
                    // —— 构造Frame
                    frameResultDataList.Add(BuildFrameDtoFast(resultList, [diagnosisResult], diagnosisResult));

                // === 更新帧状态 ===
                lastFrameIsValid = aiResult.IsValid;
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

        private void ExcuteWorkstation_ZZConnection_Release
        (
           string workstation,
           double resolution,
           bool videoSaving,
           VideoCaptureHelper camera,
           (long dateTime, string barCode, Mat frame) frameInfo,
           (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult, string? flag) aiResult,
           MethodInfo? CropImgMat,
           object? YingYanAI,
           ref UnifiedTrajectoryDiagnosis diagnosis,
           ref string? flag,
           ref bool lastFrameIsValid,
           ref List<Mat> abnormalFrames,
           ref List<FrameDtoList> frameResultDataList,
           ref List<string> resultStatus,
           ref string abnormaVideoStatus,
           ref List<Mat> abnormalKeyFrames,
           ref string? currentProcessFolder,
           ref int frameIndex
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

                // === 取出关键点，防止空引用 ===
                AIResultModel GetPoint(string keyword) => resultList.FirstOrDefault(o => o.Name.Contains(keyword)) ?? new AIResultModel { Point3D = new AIpointXYZ() };

                var Cell = GetPoint("电芯");
                var Tray = GetPoint("载具");                

                // === 执行诊断逻辑 ===
                _logger.Info($"[{camera.Name}-{workstation}]-准备执行大数据轨迹算法");
                Stopwatch stopwatch = new();
                stopwatch.Restart();
                diagnosis.SelectProgram("枣庄W04连接片放料");

                var diagnosisResult = diagnosis.ExecuteDiagnosis
                (
                    frameStatus,
                    Cell.Point3D.X, Cell.Point3D.Y,
                    Tray.Point3D.X, Tray.Point3D.Y
                );
                stopwatch.Stop();
                _logger.Info($"[{camera.Name} - {workstation}]-大数据轨迹算法执行完毕,耗时为{stopwatch.ElapsedMilliseconds}");

                // === 获取异常视频的类型 ===
                if (!string.IsNullOrEmpty(diagnosisResult.AbnormalType))
                    abnormaVideoStatus = diagnosisResult.AbnormalType;

                // === 日志 ===
                _logger?.Info($"[{camera.Name}-{workstation}] 流程执行完成：" + $"[Valid={aiResult.IsValid}] [当前时间为：{DateTime.Now:HH:mm:ss:fff}]");

                // === 根据状态执行不同动作 ===
                switch (frameStatus)
                {
                    case "1": // 起始帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到起始帧");
                        abnormalFrames.ForEach(frameInfo => frameInfo?.Dispose());
                        abnormalFrames.Clear();

                        abnormalKeyFrames.ForEach(f => f.Dispose());
                        abnormalKeyFrames.Clear();

                        resultStatus.Clear();
                        frameResultDataList.Clear();

                        // 保存过程帧图片
                        if (videoSaving)
                        {
                            string root = $"../{workstation}过程图片/{DateTime.Now:yyyy-MM}/{DateTime.Now:dd}";
                            if (!Directory.Exists(root))
                                Directory.CreateDirectory(root);

                            string folderName = $"{DateTime.Now:HH-mm-ss.fff}_{frameInfo.barCode}";
                            currentProcessFolder = Path.Combine(root, folderName);

                            Directory.CreateDirectory(currentProcessFolder);

                            frameIndex = 0;

                            SaveFrameToFolder(frameInfo.frame, currentProcessFolder, ref frameIndex);
                        }

                        // —— 构造Frame
                        frameResultDataList = [BuildFrameDtoFast(resultList, [diagnosisResult], diagnosisResult)];

                        abnormalFrames.Add(frameInfo.frame.Clone());
                        resultStatus.Add(diagnosisResult.Status);

                        break;

                    case "0": // 过程帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到过程帧");

                        if (videoSaving)
                            SaveFrameToFolder(frameInfo.frame, currentProcessFolder, ref frameIndex);

                        abnormalFrames.Add(frameInfo.frame.Clone());
                        resultStatus.Add(diagnosisResult.Status);
                        break;

                    case "2": // 结束帧
                        _logger?.Info($"[{camera.Name}-{workstation}]-识别到结束帧");

                        if (resultStatus.Contains("NG"))
                        {
                            _logger?.Info($"[{camera.Name}-{workstation}]-识别到NG状态,准备开始执行生成异常视频流程");
                            var abnormalTimeStamp = GetCurrentTimestampSeconds();
                            _ = NGVideoUpLoadWebAsync("放料点位", abnormalTimeStamp, frameInfo.barCode, abnormalFrames, frameResultDataList, abnormaVideoStatus, resolution);
                        }

                        abnormalFrames.ForEach(f => f.Dispose());
                        abnormalFrames.Clear();
                        resultStatus.Clear();

                        currentProcessFolder = null;
                        frameIndex = 0;
                        break;
                }

                if (frameStatus != "2")
                    // —— 构造Frame
                    frameResultDataList.Add(BuildFrameDtoFast(resultList, [diagnosisResult], diagnosisResult));

                // === 更新帧状态 ===
                lastFrameIsValid = aiResult.IsValid;
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

        private void SaveFrameToFolder(Mat frame, string? folder, ref int index)
        {
            if (frame == null || string.IsNullOrEmpty(folder))
                return;

            try
            {
                index++;
                string filePath = Path.Combine(folder, $"{index:D4}.jpg");
                Cv2.ImWrite(filePath, frame);
            }
            catch (Exception ex)
            {
                _logger?.Error($"保存过程帧失败: {ex.Message}");
            }
        }

        public void SaveTXTFile(string FilePath, string FileName, string content)
        {
            try
            {
                // 1. 规范路径拼接（自动处理 / 和 \）
                string fullPath = Path.Combine(FilePath, $"{FileName}.txt");

                // 2. 确保目录存在（Directory.CreateDirectory 已内置空值/存在性检查）
                string? dirPath = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                // 3. 统一使用追加模式写入（File.AppendAllText 自动处理文件创建）
                File.AppendAllText(fullPath, content + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.Error($"保存TXT文件出错: {ex.Message}, 路径: {FilePath}/{FileName}.txt");
                throw;
            }
        }
       
        /// 轨迹数据实时上传web
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
                _logger?.Info($"[{position}]-[FramesUpLoadWebAsync]-准备处理实时轨迹数据进行上传");

                if (aiResult == null || aiResult.Count == 0)
                    return;

                // —— 诊断结果缓存
                var diagnosisList = diagnosisResults as List<UnifiedDiagnosisResult> ?? [.. diagnosisResults];
                if (diagnosisList.Count == 0)
                    return;

                var firstDiagnosis = diagnosisList[0];

                // —— 构造Frame
                var frameDto = BuildFrameDtoFast(aiResult, diagnosisList, firstDiagnosis);

                if (isValid)
                    frameResultDataList.Add(frameDto);

                var request = CreateSocketRequestFast(position, frameDto, firstDiagnosis, frameStatus);

                var jsonMsg = System.Text.Json.JsonSerializer.Serialize(request, _jsonOpt);

                await BroadcastAsync(jsonMsg);

                aiResult.Clear();

                _logger?.Info($"[{position}] 轨迹数据上传完成");
            }
            catch (Exception ex)
            {
                _logger?.Error($"轨迹上传异常: {ex.Message}");
            }
        }

        private FrameDtoList BuildFrameDtoFast
        (
            List<AIResultModel> aiResult,
            List<UnifiedDiagnosisResult> diagnosisList,
            UnifiedDiagnosisResult firstDiagnosis
        )
        {
            // —— 分类容器
            var categoryMap = new Dictionary<string, List<DataList>>(categories.Length);

            for (int i = 0; i < categories.Length; i++)
                categoryMap[categories[i]] = new List<DataList>(10);

            long timestamp = GetCurrentTimestampSeconds();

            foreach (var item in aiResult)
            {
                item.Img?.Dispose();
                item.Region?.Dispose();

                UnifiedDiagnosisResult dataResult = firstDiagnosis;

                for (int j = 0; j < diagnosisList.Count; j++)
                {
                    if (diagnosisList[j].PositionName == item.Name)
                    {
                        dataResult = diagnosisList[j];
                        break;
                    }
                }

                var category = GetCategory(item.Name);
                if (category == null)
                    continue;

                var p = GetPointValues(item.Name, dataResult);

                categoryMap[category].Add
                (
                    CreateDataList
                    (
                        item.Name,
                        timestamp,
                        p.x, p.y, p.z,
                        p.dx, p.dy, p.dz,
                        p.xStd, p.yStd, p.zStd,
                        item.Point3D.X,
                        item.Point3D.Y,
                        item.Point3D.Z,
                        p.abnormal,
                        p.averageSlope
                    )
                );
            }

            var categoryList = new List<CategoryDtoList>(categoryMap.Count);

            foreach (var kv in categoryMap)
            {
                categoryList.Add(new CategoryDtoList
                {
                    Category = kv.Key,
                    DataList = kv.Value
                });
            }

            return new FrameDtoList
            {
                FrameTimeStamp = GetCurrentTimestampSeconds(),
                CategoryDtoList = categoryList
            };
        }

        private HawkEyeSocketRequest CreateSocketRequestFast
        (
            string position,
            FrameDtoList frameDto,
            UnifiedDiagnosisResult diagnosis,
            int frameStatus
        )
        {
            var workshipStr = _globalSettingRepository.GetSettingValue("workship");
            var deviceSn = _globalSettingRepository.GetSettingValue("DeviceSn");

            string[] workship = workshipStr?.Split(':') ?? new string[4];

            return new HawkEyeSocketRequest
            {
                Position = position,
                Process = workship.Length > 3 ? workship[3] : "",
                ProductionLine = workship.Length > 2 ? workship[2] : "",
                ProductionBase = workship.Length > 0 ? workship[0] : "",
                DeviceId = deviceSn,
                BarCode = "xxxxx",
                DetectionTimeStamp = GetCurrentTimestampSeconds(),
                DetectionResult = diagnosis.AbnormalType ?? "NG",
                AlertLevel = 1,
                FrameDtoList = [frameDto],
                StatusFlag = frameStatus
            };
        }

        private (float x, float y, float z, float xStd, float yStd, float zStd, float dx, float dy, float dz, int abnormal, float averageSlope) GetPointValues(string name, UnifiedDiagnosisResult d)
        {
            return name switch
            {
                "左点电芯" =>
                (d.LeftReverseProjectedPoint?.X ?? 0.0f, d.LeftReverseProjectedPoint?.Y ?? 0.0f, (d.LeftReverseProjectedPoint?.Z ?? 0.0f),
                  (d.ExpectedLeft3DPoint?.X ?? 0.0f), (d.ExpectedLeft3DPoint?.Y ?? 0.0f), (d.ExpectedLeft3DPoint?.Z ?? 0.0f),
                  (d.LeftDx3D), (d.LeftDy3D), (d.LeftDz3D),
                 d.Status == "OK" ? 1 : 0, d.AverageSlope),

                "中心点电芯" =>
                ((d.MiddleReverseProjectedPoint?.X ?? 0.0f), (d.MiddleReverseProjectedPoint?.Y ?? 0.0f), (d.MiddleReverseProjectedPoint?.Z ?? 0.0f),
                  (d.ExpectedMiddle3DPoint?.X ?? 0.0f), (d.ExpectedMiddle3DPoint?.Y ?? 0.0f), (d.ExpectedMiddle3DPoint?.Z ?? 0.0f),
                  (d.MiddleDx3D), (d.MiddleDy3D), (d.MiddleDz3D),
                 d.Status == "OK" ? 1 : 0, d.AverageSlope),

                "右点电芯" =>
                ((d.RightReverseProjectedPoint?.X ?? 0.0f), (d.RightReverseProjectedPoint?.Y ?? 0.0f), (d.RightReverseProjectedPoint?.Z ?? 0.0f),
                  (d.ExpectedRight3DPoint?.X ?? 0.0f), (d.ExpectedRight3DPoint?.Y ?? 0.0f), (d.ExpectedRight3DPoint?.Z ?? 0.0f),
                  (d.RightDx3D), (d.RightDy3D), (d.RightDz3D),
                 d.Status == "OK" ? 1 : 0, d.AverageSlope),

                "金属左下点" =>
               ((d.Bottom_leftReverseProjectedPoint?.X ?? 0.0f), (d.Bottom_leftReverseProjectedPoint?.Y ?? 0.0f), (d.Bottom_leftReverseProjectedPoint?.Z ?? 0.0f),
                 (d.ExpectedBottom_left3DPoint?.X ?? 0.0f), (d.ExpectedBottom_left3DPoint?.Y ?? 0.0f), (d.ExpectedBottom_left3DPoint?.Z ?? 0.0f),
                 (d.Bottom_leftDx3D), (d.Bottom_leftDy3D), (d.Bottom_leftDz3D),
                d.Status == "OK" ? 1 : 0, d.AverageSlope),

                _ =>
                ((d.ReverseProjectedPoint?.X ?? 0.0f), (d.ReverseProjectedPoint?.Y ?? 0.0f), (d.ReverseProjectedPoint?.Z ?? 0.0f),
                  (d.Expected3DPoint?.X ?? 0.0f), (d.Expected3DPoint?.Y ?? 0.0f), (d.Expected3DPoint?.Z ?? 0.0f),
                  (d.Dx3D), (d.Dy3D), (d.Dz3D),
                 d.Status == "OK" ? 1 : 0, d.AverageSlope)
            };
        }


        private string? GetCategory(string name)
        {
            if (name.Contains("电芯")) return "电芯";
            if (name.Contains("夹爪")) return "夹爪";
            if (name.Contains("载具")) return "载具";
            if (name.Contains("金属")) return "金属";
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
        private DataList CreateDataList
        (
            string name,
            long timestamp,
            float x, float y, float z,
            float xDiff = 0f,
            float yDiff = 0f,
            float zDiff = 0f,
            float xStandard = 0f,
            float yStandard = 0f,
            float zStandard = 0f,
            float pixelX = 0f,
            float pixelY = 0f,
            float pixelZ = 0f,
            int abnormal = 1,
            float averageSlope = 0f
        )
        {
            var point = new AIPointXYZDto
            {
                PictureTimeStamp = timestamp,
                MoveDirection = "x",

                X = x,
                Y = y,
                Z = z,

                Speed = 0f,

                XDiff = xDiff,
                YDiff = yDiff,
                ZDiff = zDiff,

                Abnormal = abnormal,

                XStandard = xStandard,
                YStandard = yStandard,
                ZStandard = zStandard,

                PixelCoordinateX = pixelX,
                PixelCoordinateY = pixelY,
                PixelCoordinateZ = pixelZ,

                AverageSlope = averageSlope
            };

            return new DataList
            {
                PointName = name,
                AiPointXYZDtoList = [point]
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
            var t = Task.Run(async () =>
            {
                try
                {
                    foreach (var AIResultModel in AIResults ?? [])
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

                        var re = await _webApiClient.AiDetectionDataAsync
                        (
                            $"{position}", $"{barCode}", abnormalTimeStamp, "NG", file
                        );

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


        private async Task NGVideoUpLoadWebAsync
        (
            string position,
            long dateTime,
            string barCode,
            List<Mat> abnormalFrames,
            List<Mat>? abnormalKeyFrames,
            List<FrameDtoList> frameResultDataList,
            string abnormaVideoStatus,
            double resolution
        )
        {
            try
            {
                var videoFiles = new List<(byte[] FileBytes, string ContentType, string FileName, string FileExt)>();
                Stopwatch stopwatch = new();
                byte[] file = [];

                // 生成异常视频
                if (abnormalFrames?.Count > 0)
                {
                    _logger?.Info($"[{position}]-当前异常视频帧数为{abnormalFrames.Count},准备开始生成异常视频");

                    stopwatch.Restart();
                    file = SaveToMP4Bytes(abnormalFrames, abnormalFrames[0].Cols, abnormalFrames[0].Rows, resolution);
                    stopwatch.Stop();

                    _logger?.Info($"[{position}]-异常视频生成完毕,耗时为{stopwatch.ElapsedMilliseconds}ms");
                    videoFiles.Add((file, "video/mp4", "abnormal.mp4", "file"));
                }

                // 生成异常关键帧视频
                if (abnormalKeyFrames != null && abnormalKeyFrames.Count > 0)
                {
                    _logger?.Info($"[{position}]-当前异常关键帧视频帧数为{abnormalKeyFrames.Count},准备开始生成异常关键帧视频");

                    stopwatch.Restart();
                    file = SaveToMP4Bytes(abnormalKeyFrames, abnormalKeyFrames[0].Cols, abnormalKeyFrames[0].Rows, resolution);
                    stopwatch.Stop();

                    _logger?.Info($"[{position}]-异常关键帧视频生成完毕,耗时为{stopwatch.ElapsedMilliseconds}ms");

                    videoFiles.Add((file, "video/mp4", "abnormalKey.mp4", "featureFile"));
                }
                else
                {
                    videoFiles.Add((file, "video/mp4", "abnormalKey.mp4", "featureFile"));
                }

                // 控制 FrameDto 数量
                if (frameResultDataList.Count > 300)
                    frameResultDataList.RemoveRange(0, 200);

                // 上传（自动适配 1 或 2 个视频）
                _logger?.Info($"[{position}]-准备上传数据！");
                var re = await _webApiClient.TrajectoryDataAsync
                (
                    position, barCode, dateTime, dateTime,
                    videoFiles, frameResultDataList, abnormaVideoStatus
                );

                if (re.IsSuccess || re.Code == 200)
                    _logger?.Info($"[{position}-{barCode}] 数据上传成功！");
                else
                    _logger?.Error($"[{position}-{barCode}] 数据上传失败：[{re.Message}]");
            }
            catch (Exception ex)
            {
                _logger.Error($"[NGVideoUpLoadWebAsync] 上传异常: {ex.Message}");
            }
        }


        private async Task NGVideoUpLoadWebAsync
        (
            string position,
            long abnormalTimeStamp,
            string barCode,
            List<Mat> abnormalFrames,
            List<FrameDtoList> frameResultDataList,
            string abnormaVideoStatus,
            double resolution
        )
        {
            try
            {

                Stopwatch stopwatch = new();
                byte[] file = [];

                // 生成异常视频
                if (abnormalFrames?.Count > 0)
                {
                    _logger?.Info($"[{position}]-当前异常视频帧数为{abnormalFrames.Count},准备开始生成异常视频");

                    stopwatch.Restart();
                    file = SaveToMP4Bytes(abnormalFrames, abnormalFrames[0].Cols, abnormalFrames[0].Rows, resolution);
                    stopwatch.Stop();

                    _logger?.Info($"[{position}]-异常视频生成完毕,耗时为{stopwatch.ElapsedMilliseconds}ms");
                }

                // 上传（自动适配 1 或 2 个视频）
                _logger?.Info($"[{position}]-准备上传数据！");

                var re = await _webApiClient.AiDetectionDataAsync
                (
                    $"{position}", $"{barCode}", abnormalTimeStamp, abnormaVideoStatus, file, frameResultDataList, "video/mp4", "mp4", 3
                );

                if (re.IsSuccess || re.Code == 200)
                    _logger?.Info($"[{position}-{barCode}] 数据上传成功！");
                else
                    _logger?.Error($"[{position}-{barCode}] 数据上传失败：[{re.Message}]");
            }
            catch (Exception ex)
            {
                _logger.Error($"[NGVideoUpLoadWebAsync] 上传异常: {ex.Message}");
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
                _logger.Error($"[SaveToMP4Bytes] 未能合成视频: {ex.Message}");
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

        #region websocket服务端       
        private readonly ConcurrentBag<IWebSocketConnection> webClients = [];
        private DateTime _lastSend = DateTime.MinValue;
        private readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(10); // 节流间隔，防止CPU占用高          

        public void StartServer(int port = 8181)
        {
            var server = new WebSocketServer($"ws://0.0.0.0:{port}");
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    _logger.Info($"[{socket.ConnectionInfo.ClientIpAddress}]客户端已连接！");
                    webClients.Add(socket);
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
            var clients = webClients.ToList();
            clients.Remove(client);
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
            var clientsSnapshot = webClients.ToList();
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
        private (object? YingYanAI, MethodInfo? ModelInit, MethodInfo? ModelAction, MethodInfo? Undistorted, MethodInfo? CropImgMat) Init_YingYanAI(byte[] bytes)
        {
            try
            {
                #region 后续删掉，使用下面的从web获取  -TODO
                object? yingYanAI;
                MethodInfo? modelInit, modelAction, undistorted, cropImgMat;
                //var DLLPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "YingYanAlgoCore.dll");
                if (File.Exists($"YingYanAlgoCore.dll"))
                {
                    var ass = Assembly.LoadFrom($"YingYanAlgoCore.dll");
                    var tp = ass.GetType("ZZYingYanAICore.YingYanAI") ??
                        throw new NullReferenceException("[YingYanAlgoCore.YingYanAI]type获取失败！");
                    yingYanAI = Activator.CreateInstance(tp);
                    modelInit = tp.GetMethod("ModelInit");
                    modelAction = tp.GetMethod("ModelAction");
                    undistorted = tp.GetMethod("Undistorted");
                    cropImgMat = tp.GetMethod("CropImgMat");
                    return (yingYanAI, modelInit, modelAction, undistorted, cropImgMat);
                }
                #endregion

                var assembly = Assembly.Load(bytes);
                //获取类名，必须 命名空间+类名
                var type = assembly.GetType("YingYanAlgoCore.YingYanAI") ??
                    throw new NullReferenceException("[YingYanAlgoCore.YingYanAI]type获取失败！");
                //建立实例
                yingYanAI = Activator.CreateInstance(type);
                // 获取方法
                modelInit = type.GetMethod("ModelInit");
                modelAction = type.GetMethod("ModelAction");
                undistorted = type.GetMethod("Undistorted");
                cropImgMat = type.GetMethod("CropImgMat");

                return (yingYanAI, modelInit, modelAction, undistorted, cropImgMat);
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
                _logger.Info($"[{workstation}]准备执行视觉推理算法");
                Stopwatch stopwatch = new();
                stopwatch.Restart();
                var AIParamData = new object?[]
                {
                    AIModels,     // 模型列表
                    FrameList,    // 帧图片
                    workstation,  // 工位名称
                    flag,         // 默认传"0"
                    null,         // 推理结果列表
                    null,         // 调用是否有效
                    null,         // 回传的标志位: "1"表示开始，"0"表示结束
                    null          // 接口异常返回信息
                };
                // 执行推理算法
                var re = ModelAction?.Invoke(YingYanAI, AIParamData);
                stopwatch.Stop();
                _logger.Info($"[{workstation}]-视觉推理算法执行完毕,耗时为{stopwatch.ElapsedMilliseconds}");

                var AIResult = AIParamData[4] as List<List<AIResultModel>>;
                var IsValid = AIParamData[5] as bool?;
                var reFlag = AIParamData[6] as string;
                var ExInfo = AIParamData[7] as string;

                _logger.Info($"[{workstation}]视觉推理算法执行完毕,[IsValid]={IsValid},[reFlag]={reFlag},结果数量为{AIResult?.Count},模型为{AIModels[0]}");

                return (IsValid ?? false, ExInfo, AIResult, reFlag);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null, "0");
            }
        }


        private Mat Execute_Undistorted(CalibrationResult CalibrationResult, MethodInfo? Undistorted, object? YingYanAI, Mat Frame)
        {
            try
            {
                var AIParamData = new object?[]
                {
                    Frame,              // 帧图片
                    CalibrationResult,  // 相机标定参数
                    null                // 算法返回的结果图片
                };
                // 执行去畸变算法
                Undistorted?.Invoke(YingYanAI, AIParamData);

                if (AIParamData[2] is Mat AIResultImg && !AIResultImg.Empty())
                {
                    _logger.Info($"执行去畸变算法成功!");
                    return AIResultImg;
                }

                _logger.Warn($"执行去畸变算法返回异常结果：{AIParamData[2]}，采用原图");
            }
            catch (Exception ex)
            {
                _logger.Error($"执行去畸变算法异常：{ex.Message}，采用原图");
            }
            // 回退原图
            return Frame;
        }

        private void Execute_ImageCutting(Mat Frame, MethodInfo? CropImgMat, object? YingYanAI, List<Mat> AbnormalKeyFrames)
        {
            try
            {
                var AIParamData = new object?[]
                {
                    Frame,      // 原图片
                    800,        // 裁图左上角行坐标
                    1130,       // 裁图左上角列坐标
                    1600,       // 裁图右下角行坐标
                    2300,       // 裁图右下角列坐标                        
                    null,       // 裁切后图片
                    null        // 接口异常返回信息                        
                };

                Stopwatch stopwatch = new();
                stopwatch.Restart();
                // 执行图片裁切算法
                CropImgMat?.Invoke(YingYanAI, AIParamData);
                stopwatch.Stop();

                if (AIParamData[5] is Mat AIResultImg && !AIResultImg.Empty())
                {
                    AbnormalKeyFrames.Add(AIResultImg);
                    _logger.Info($"执行图像裁切算法成功!,耗时为{stopwatch.ElapsedMilliseconds}");
                    return;
                }
                _logger.Error($"执行图像裁切算法异常：返回为{AIParamData[5]}");
            }
            catch (Exception ex)
            {
                _logger.Error($"执行图像裁切算法异常：{ex.Message}");
            }
        }
        #endregion

        #region Camera相关
        /// <summary>
        /// 相机读写锁
        /// </summary>
        private static readonly object _locker = new();

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
                        _cameras = XmlHelper.XmlToModel<List<CameraModel>>(path) ?? [];
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
    }
}
