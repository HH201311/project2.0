using Dm.filter;
using NLog;
using OpenCvSharp;
using OpenNetV8;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Component;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.ViewModels.Pages.Demo;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Mysqlx.Notice.Warning.Types;

namespace SUNWODA_SEVB.ViewModels.Pages.HawkEye
{
    [Module("VM_MachineDamageMonitoringPage", "鹰眼机损监测")]
    class VM_MachineDamageMonitoringPage : ViewModelBase
    {
        private readonly ILoggerService<VM_MachineDamageMonitoringPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_MachineDamageMonitoringPage(
            ILoggerService<VM_MachineDamageMonitoringPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient

        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;

            Init_Process();
        }

        #region 视频流算法
        /// <summary>
        /// 从web获取视频流算法
        /// </summary>
        /// <param name="pythonCode"></param>
        /// <param name="appKey"></param>
        /// <param name="publicKey"></param>
        /// <param name="executeType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<byte[]> DownloadDll(string pythonCode, string appKey, string publicKey, string executeType = "PRED")
        {
            var xmlPublicKey = RasHelper.ToXmlPublicKey(publicKey);
            var signature = RasHelper.RSAEncrypt(xmlPublicKey, appKey);
            // 获取PID算法文件
            var reFile = await _webApiClient.DownloadDllAsync(appKey, signature, pythonCode, executeType);
            if (!(reFile != null && reFile.DataStr != null))
            {
                var message = $"获取算法文件信息失败：pythonCode:[{pythonCode}]-message[{reFile?.Message}]";
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
                object? yingYanAI;
                MethodInfo? modelInit, modelAction;
                var DLLPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "YingYanAI.dll");
                if (File.Exists($"{DLLPath}"))
                {
                    var ass = Assembly.LoadFrom($"{DLLPath}");
                    var tp = ass.GetType("ZZYingYanAI.YingYanAI");
                    if (tp == null) throw new NullReferenceException("[ZZYingYanAI.YingYanAI]type获取失败！");
                    yingYanAI = Activator.CreateInstance(tp);
                    modelInit = tp.GetMethod("ModelInit");
                    modelAction = tp.GetMethod("ModelAction");
                    return (yingYanAI, modelInit, modelAction);
                }

                var assembly = Assembly.Load(bytes);
                //获取类名，必须 命名空间+类名
                var type = assembly.GetType("ZZYingYanAI.YingYanAI");
                if (type == null) throw new NullReferenceException("[ZZYingYanAI.YingYanAI]type获取失败！");
                //建立实例
                yingYanAI = Activator.CreateInstance(type);
                // 获取方法
                modelInit = type.GetMethod("ModelInit");
                modelAction = type.GetMethod("ModelAction");

                return (yingYanAI, modelInit, modelAction);
            }
            catch (Exception ex)
            {
                throw new Exception($"创建dll实例异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 初始化算法，获取算法模型
        /// </summary>
        /// <param name="modelPath"></param>
        /// <param name="ModelInit"></param>
        /// <param name="YingYanAI"></param>
        /// <returns></returns>
        private (bool IsValid, string? ExInfo, IPredictor? H246modle) Execute_ModelInit(string? modelPath, MethodInfo? ModelInit, object? YingYanAI)
        {
            try
            {
                //var modelPath = $".\\tuopan_det.onnx";
                var AIModelData = new object?[]
                {
                    modelPath,
                    null,           // 不需要直接置null
                    null,           // 工位名称
                    null,           // 返回的算法模型
                    null,           // 接口异常返回信息
                    false           // 是否启用GPU
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
        /// <param name="AIModels"></param>
        /// <param name="FrameList"></param>
        /// <param name="ModelAction"></param>
        /// <param name="YingYanAI"></param>
        /// <returns></returns>
        private (bool IsValid, string? ExInfo, List<List<AIResultModel>>? AIResult) Execute_ModelAction(List<Mat> FrameList, List<IPredictor> AIModels, MethodInfo? ModelAction, object? YingYanAI)
        {
            try
            {
                var AIParamData = new object?[]
                {
                    AIModels,     // 模型列表
                    FrameList,    // 多帧图片数据流
                    null,         // 工位名称
                    null,         // 推理结果列表
                    null,         // 调用是否有效
                    null          // 接口异常返回信息
                };
                // 执行推理算法
                var re = ModelAction?.Invoke(YingYanAI, AIParamData);
                var AIResult = AIParamData[4] as List<List<AIResultModel>>;
                var IsValid = AIParamData[4] as bool?;
                var ExInfo = AIParamData[5] as string;
                return (IsValid ?? false, ExInfo, AIResult);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }
        #endregion

        /// <summary>
        /// 初始化设备连接
        /// </summary>
        /// <exception cref="Exception"></exception>
        private List<CameraModel> Init_Camera()
        {
            var DeviceList = new List<CameraModel>();
            try
            {
                // 从配置文件中获取设备   —— TODO

                //var plc_configeModel = new Model.plc_config();
                //var plc_configBll = new BLL.plc_config();
                //// 初始化摄像头 enabled = 2
                //var cameras = plc_configBll.GetModelList("enabled = 2") ?? new List<Model.plc_config>();
                //_plcConfigRepository.GetList(model => model.e)
                //foreach (var item in cameras)
                //{
                //    var dm = new DeviceModel();
                //    dm.Name = item.plc_name;
                //    dm.IP = item.address;
                //    dm.Port = int.Parse(item.address_para);
                //    dm.Remake = item.remark;
                //    DeviceList.Add(dm);
                //    _logger?.Info("添加：" + item.plc_name + "连接!");
                //}

                return DeviceList;
            }
            catch (Exception ex)
            {
                throw new Exception("初始化设备连接异常！", ex);
            }
        }

        /// <summary>
        /// 初始化进程
        /// </summary>
        private async void Init_Process()
        {
            // 从web获取AI视频流算法  —— TODO
            var bytes = await DownloadDll("ZZ_YY_C01_DP_DLL", "ZZ_YY", "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCQiNCeYgNIL35V99cJ0giEOtOCd6tf9fVEVCuFmFu0zuskPJYB0D1pUcYjLtJfadGa1HflWYINl/zk9bq0fDipuz5vPkswsp0N4efiVzwoBbzUTpLPePNkpb6rDyd+3RUNAC8UtZU2ptKGCNVvvgMQGZp+2mnIKPamuEXzK5qkkwIDAQAB");
            var cameras = Init_Camera();
            foreach (var cameraInfo in cameras)
            {
                var camera = new VideoCaptureHelper(_logger, cameraInfo.Name, $"rtsp://{cameraInfo.IP}:{cameraInfo.Port}/{cameraInfo.Remake}");
                camera.Connect();

                // 启动流程
                StartProcess_Thread(bytes, 2, camera);
            }
        }

        /// <summary>
        /// 异步开启线程
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="frameCount"></param>
        /// <param name="camera"></param>
        private void StartProcess_Thread(byte[] bytes, int frameCount, VideoCaptureHelper camera)
        {
            // 初始化算法实例
            var objInfo = Init_YingYanAI(bytes);
            // 初始化算法模型
            var objInit = Execute_ModelInit($".\\tuopan_det.onnx", objInfo.ModelInit, objInfo.YingYanAI);
            if (!objInit.IsValid || objInit.H246modle == null)
            {
                throw new Exception($"[{camera.Name}]视觉算法模型初始化异常：[{objInit.IsValid}]-[{objInit.ExInfo}]");
            }

            // 创建缓存队列
            var frameCache = new ConcurrentQueue<Mat>();

            // 执行视频读取
            camera.ExecuteFreame += new VideoCaptureHelper.OnReadingFrame((frame) =>
            {
                frameCache.Enqueue(frame);
            });
            camera.StartReading();

            var t = new Thread(() =>
            {
                var frameLis = new List<Mat>();
                var aiModels = new List<IPredictor>() { objInit.H246modle };
                // 循环执行算法
                while (true)
                {
                    if (frameCache.Count < frameCount) continue;
                    frameLis.ForEach(o => o?.Dispose());
                    frameLis.Clear();
                    for (int i = 0; i < frameCount; i++)
                    {
                        if (!frameCache.TryDequeue(out Mat? frame))
                        {
                            _logger?.Error($"[{camera.Name}]视觉从队列获取视频帧失败！");
                            continue;
                        }
                        frameLis.Add(frame);
                    }

                    // 执行视觉推理算法
                    var re = Execute_ModelAction(frameLis, aiModels, objInfo.ModelAction, objInfo.YingYanAI);
                    if (!re.IsValid || re.AIResult == null)
                    {
                        // -TODO   执行异常视频流上传WEB
                        NGFramesUpLoadWebAsync(frameLis);
                        _logger?.Error($"[{camera.Name}]视觉算法推理失败：[{re.IsValid}]-[{re.ExInfo}]");
                        continue;
                    }

                    // -TODO
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        /// <summary>
        /// 视频流上传web
        /// </summary>
        /// <param name="frameLis"></param>
        /// <returns></returns>
        private async void NGFramesUpLoadWebAsync(List<Mat> frameLis)
        {
            var t = Task.Factory.StartNew(() =>
            {
                // -TOD   上传WEB
            });
            await t;
        }

        /// <summary>
        /// 设备类
        /// </summary>
        public class CameraModel
        {
            /// <summary>
            /// 名称
            /// </summary>
            public string? Name { get; set; }

            /// <summary>
            /// IP
            /// </summary>
            public string? IP { get; set; }

            /// <summary>
            /// 端口
            /// </summary>
            public int Port { get; set; }

            /// <summary>
            /// 通讯协议
            /// </summary>
            public string? Protocol { get; set; }

            private int state;
            /// <summary>
            /// 连接状态
            /// </summary>
            public int State { get; set; }

            /// <summary>
            /// 备注
            /// </summary>
            public string? Remake { get; set; }
        }
    }
}
