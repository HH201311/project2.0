using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Project.HawkEye;
using SUNWODA_SEVB.Core.Models.Web;
using SUNWODA_SEVB.Tool.Helper;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace SUNWODA_SEVB.WEB.Services
{
    public class WebApiClient : IWebApiClient
    {
        private readonly ILoggerService<WebApiClient> _logger;
        private readonly IWebConfiguration _configuration;
        private readonly HttpHelper _httpHelper;

        // 问题1：将 _uuid 声明为可空类型
        private string? _uuid;

        public WebApiClient(
            ILoggerService<WebApiClient> logger,
            IWebConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpHelper = new HttpHelper();
        }

        public void SetUuid(string uuid)
        {
            _uuid = uuid;
        }

        public string? GetUuid()
        {
            return _uuid;
        }

        public async Task<DeviceBindingResponse> BindDeviceAsync(DeviceBindingRequest request)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (string.IsNullOrEmpty(settings.CentralControlWebUrl))
                {
                    return new DeviceBindingResponse
                    {
                        Code = 00,
                        Message = "CentralControlWebUrl未配置"
                    };
                }

                var url = $"{settings.CentralControlWebUrl}machine/insert";

                var json = JsonHelper.Serialize(request);
                var result = Encoding.Default.GetString(await Task.Run(() => _httpHelper.IMEWebPost(url, json)));

                if (!result.Contains("failed"))
                {
                    if (JsonHelper.TryDeserialize<DeviceBindingResponse>(result, out var response))
                    {
                        if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.Data))
                        {
                            _uuid = response.Data;
                        }
                        return response ?? new DeviceBindingResponse
                        {
                            Code = 00,
                            Message = "反序列化结果为空"
                        };
                    }
                }

                return new DeviceBindingResponse
                {
                    Code = 00,
                    Message = result
                };
            }
            catch (Exception ex)
            {
                _logger.Error("设备绑定API调用失败", ex);
                return new DeviceBindingResponse
                {
                    Code = 00,
                    Message = ex.Message
                };
            }
        }

        public async Task<HeartbeatResponse> SendHeartbeatAsync(string deviceSn, string state, string info, string uuid)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (settings == null || string.IsNullOrWhiteSpace(settings.CentralControlWebUrl))
                {
                    return new HeartbeatResponse
                    {
                        Code = 0,
                        Message = "CentralControlWebUrl未配置"
                    };
                }

                var baseUrl = settings.CentralControlWebUrl!;
                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                var url = $"{baseUrl}machineHeart/heart";

                // 参数容错处理，避免 null
                var parameters = new Dictionary<string, string>
                {
                    { "devId", deviceSn ?? string.Empty },
                    { "state", state ?? string.Empty },
                    { "info",  info  ?? string.Empty },
                    { "UUID",  uuid  ?? string.Empty }
                };

                var sw = Stopwatch.StartNew();
                var result = await Task.Run(() => _httpHelper.IMEWebGet(url, parameters));
                sw.Stop();

                HeartbeatResponse? response = null;
                if (!string.IsNullOrWhiteSpace(result))
                {
                    try
                    {
                        response = JsonHelper.Deserialize<HeartbeatResponse>(result);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("心跳API反序列化失败", ex);
                    }
                }
                var statusCode = response?.Code ?? 0;

                var hasFailedKeyword = !string.IsNullOrEmpty(result) && result.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!hasFailedKeyword)
                {
                    // 成功路径：若反序列化失败，给出提示
                    return response ?? new HeartbeatResponse
                    {
                        Code = 0,
                        Message = "反序列化失败"
                    };
                }

                // 失败路径：返回原始返回体或空提示
                return new HeartbeatResponse
                {
                    Code = 00,
                    Message = result
                };
            }
            catch (Exception ex)
            {
                _logger.Error("心跳API调用失败", ex);
                return new HeartbeatResponse
                {
                    Code = 00,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 发送机器错误预警信息到上位机接口
        /// </summary>
        /// <param name="deviceId">设备ID，必填</param>
        /// <param name="info">心跳信息，必填</param>
        /// <param name="state">状态，必填</param>
        /// <param name="type">消息操作类型，必填，默认"WX"</param>
        /// <param name="projectName">项目名称，用于选择企微推送名单，必填</param>
        /// <param name="level">预警等级，必填，1～10，代表不同频率1表示1小时1次，2表示30分钟1次，3表示20分钟1次，4表示15分钟1次，5表示12分钟1次，6表示10分钟1次，10表示每次报警都发送</param>
        /// <returns>返回预警结果的响应对象</returns>
        public async Task<MachineErrorResponse> SendMachineErrorAlertAsync(
            string deviceId,
            string info,
            string state,
            string type = "WX",
            string projectName = "",
            string level = "10")
        {
            try
            {
                var settings = _configuration.GetSettings();
                if(settings==null || string.IsNullOrEmpty(settings.CentralControlWebUrl))
                {
                    return new MachineErrorResponse
                    {
                        Code = 0,
                        Message = "数据库中Web端的Url未配置"
                    };
                }
                if (!settings.EnableMachineErrorAlert) { 
                    return new MachineErrorResponse
                    {
                        Code = 0,
                        Message = "数据库中预警功能未启用"
                    };
                }
                var baseUrl = settings.CentralControlWebUrl!;
                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                var url = $"{baseUrl}machineHeart/machineErrorLog";

                // 参数容错，避免null
                var parameters = new Dictionary<string, string>
                {
                    { "devId", deviceId ?? string.Empty },
                    { "info", info ?? string.Empty },
                    { "state", state ?? string.Empty },
                    { "type", type ?? "WX" },
                    { "projectName", projectName ?? string.Empty },
                    { "level", level ?? "10" }
                };

                var sw = Stopwatch.StartNew();
                var result = await Task.Run(() => _httpHelper.IMEWebGet(url, parameters));
                sw.Stop();

                MachineErrorResponse? response = null;
                if (!string.IsNullOrWhiteSpace(result))
                {
                    try
                    {
                        response = JsonHelper.Deserialize<MachineErrorResponse>(result);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("上位机预警API反序列化失败", ex);
                    }
                }

                if (response != null)
                {
                    return response;
                }
                else
                {
                    return new MachineErrorResponse
                    {
                        Code = 0,
                        Message = "反序列化失败或接口无返回"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error("上位机预警API调用失败", ex);
                return new MachineErrorResponse
                {
                    Code = 0,
                    Message = ex.Message
                };
            }
        }


        public async Task<PcInfoResponse> UploadPcInfoAsync(PcInfoRequest request, string uuid)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (string.IsNullOrEmpty(settings.CentralControlWebUrl))
                {
                    return new PcInfoResponse
                    {
                        Code = 00,
                        Message = "CentralControlWebUrl未配置"
                    };
                }

                var baseUrl = settings.CentralControlWebUrl!;
                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                var url = $"{baseUrl}machineHeart/pcInfo";
                var json = JsonHelper.Serialize(request);

                var headers = new Dictionary<string, string>
                {
                    {"UUID", uuid ?? string.Empty}
                };

                var sw = Stopwatch.StartNew();
                var result = Encoding.Default.GetString(await Task.Run(() => _httpHelper.IMEWebPost(url, json, headers)));
                sw.Stop();

                PcInfoResponse? response = null;
                if (!string.IsNullOrWhiteSpace(result))
                {
                    try
                    {
                        response = JsonHelper.Deserialize<PcInfoResponse>(result!);
                    }
                    catch (Exception ex)
                    {
                        // 反序列化失败也记录，但不中断后续流程
                        _logger.Error("PC信息上传API反序列化失败", ex);
                    }
                }

                var statusCode = response?.Code ?? 0;
                _logger.LogWebInterface(url, "pcinfo", json ?? string.Empty, result ?? string.Empty, statusCode, sw.ElapsedMilliseconds);

                var hasFailedKeyword = !string.IsNullOrEmpty(result) &&
                               result.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!hasFailedKeyword)
                {
                    return response ?? new PcInfoResponse
                    {
                        Code = 00,
                        Message = "反序列化失败"
                    };
                }

                return new PcInfoResponse
                {
                    Code = 0,
                    Message = result ?? "调用返回为空"
                };
            }
            catch (Exception ex)
            {
                _logger.Error("PC信息上传API调用失败", ex);
                return new PcInfoResponse
                {
                    Code = 00,
                    Message = ex.Message
                };
            }
        }

        public async Task<CheckVersionResponse> CheckVersionAsync(string softName, string softVersion, string deviceSn)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (string.IsNullOrEmpty(settings.CentralControlWebUrl))
                {
                    return new CheckVersionResponse
                    {
                        Code = 00,
                        Message = "CentralControlWebUrl未配置"
                    };
                }

                var url = $"{settings.CentralControlWebUrl}machineHeart/checkVerInfo";
                var headers = new Dictionary<string, string>
                {
                    {"softName", softName},
                    {"softVersion", softVersion},
                    {"devId", deviceSn}
                };

                var result = await Task.Run(() => _httpHelper.IMEWebGet(url, headers));

                if (!result.Contains("failed"))
                {
                    var response = JsonHelper.Deserialize<CheckVersionResponse>(result);
                    return response ?? new CheckVersionResponse
                    {
                        Code = 00,
                        Message = "反序列化失败"
                    };
                }

                return new CheckVersionResponse
                {
                    Code = 00,
                    Message = result
                };
            }
            catch (Exception ex)
            {
                _logger.Error("版本检查API调用失败", ex);
                return new CheckVersionResponse
                {
                    Code = 00,
                    Message = ex.Message
                };
            }
        }

        public async Task<DownloadFileResponse> DownloadFileAsync(string softName, string softVersion, string savePath, string fileName, string uuid)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (string.IsNullOrEmpty(settings.CentralControlWebUrl))
                {
                    return new DownloadFileResponse
                    {
                        Code = 00,
                        Message = "CentralControlWebUrl未配置"
                    };
                }

                var url = $"{settings.CentralControlWebUrl}MachineSoftUpdateController/downloadFile";
                var headers = new Dictionary<string, string>
                {
                    {"softName", softName},
                    {"softVersion", softVersion},
                    {"UUID", uuid}
                };

                var result = await Task.Run(() =>
                    _httpHelper.DownloadFileGet(url, savePath, fileName, headers));

                if (!result.Contains("failed"))
                {
                    // 使用 TryDeserialize 安全地反序列化
                    if (JsonHelper.TryDeserialize<DownloadFileResponse>(result, out var response) && response != null)
                    {
                        // 如果下载成功，读取文件内容
                        if (response.IsSuccess)
                        {
                            var filePath = Path.Combine(savePath, fileName);
                            if (File.Exists(filePath))
                            {
                                response.FileData = File.ReadAllBytes(filePath);
                            }
                        }
                        return response;
                    }
                }

                return new DownloadFileResponse
                {
                    Code = 00,
                    Message = result
                };
            }
            catch (Exception ex)
            {
                _logger.Error("文件下载API调用失败", ex);
                return new DownloadFileResponse
                {
                    Code = 00,
                    Message = ex.Message
                };
            }
        }

        public async Task<UploadFileResponse> UploadFileAsync(string filePath, string version, string name, string fileName)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (string.IsNullOrEmpty(settings.CentralControlWebUrl))
                {
                    return new UploadFileResponse
                    {
                        Code = 00,
                        Message = "CentralControlWebUrl未配置"
                    };
                }

                var url = $"{settings.CentralControlWebUrl}MachineSoftUpdateController/uploadFile";
                var body = new Dictionary<string, string>
                {
                    {"softVersion", version},
                    {"softName", name}
                };

                var result = await Task.Run(() =>
                    _httpHelper.FileWebPost(url, filePath, fileName, "file", 60000, null, body));

                if (!result.Contains("failed"))
                {
                    var response = JsonHelper.Deserialize<UploadFileResponse>(result);
                    return response ?? new UploadFileResponse
                    {
                        Code = 00,
                        Message = "反序列化失败"
                    };
                }

                return new UploadFileResponse
                {
                    Code = 00,
                    Message = result
                };
            }
            catch (Exception ex)
            {
                _logger.Error("文件上传API调用失败", ex);
                return new UploadFileResponse
                {
                    Code = 00,
                    Message = ex.Message
                };
            }
        }

        public async Task<SysCenterFileResponse> DownloadSysCenterFileAsync(string appKey, string signature, string pythonCode, string executeType)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (string.IsNullOrEmpty(settings.WebTestUrlSysCenter))
                {
                    return new SysCenterFileResponse
                    {
                        Success = false,
                        Message = "WebTestUrlSysCenter未配置"
                    };
                }

                var url = $"{settings.WebTestUrlSysCenter}sysCenter/interfaceApi/downloadFile";
                var request = new SysCenterFileRequest
                {
                    AppKey = appKey,
                    Signature = signature,
                    PythonCode = pythonCode,
                    ExecuteType = executeType
                };

                var json = JsonHelper.Serialize(request);
                var result = Encoding.Default.GetString(await Task.Run(() => _httpHelper.IMEWebPost(url, json, "application/json")));

                // 尝试反序列化，如果失败则将结果作为 DataStr 返回
                if (JsonHelper.TryDeserialize<SysCenterFileResponse>(result, out var response) && response != null)
                {
                    return response;
                }

                // 如果不能反序列化为 SysCenterFileResponse，则将原始结果作为 DataStr 返回
                return new SysCenterFileResponse
                {
                    Success = true,
                    DataStr = result
                };
            }
            catch (Exception ex)
            {
                _logger.Error("SysCenter文件下载API调用失败", ex);
                return new SysCenterFileResponse
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 库文件.dll下载接口
        /// </summary>
        /// <param name="appKey"></param>
        /// <param name="signature"></param>
        /// <param name="pythonCode"></param>
        /// <param name="executeType"></param>
        /// <returns></returns>
        public async Task<DownloadDllResponse> DownloadDllAsync(string appKey, string signature, string pythonCode, string executeType)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (string.IsNullOrEmpty(settings.WebTestUrlSysCenter))
                {
                    return new DownloadDllResponse
                    {
                        IsSuccess = false,
                        Message = "WebTestUrlSysCenter未配置"
                    };
                }

                var url = $"{settings.WebTestUrlSysCenter}sysCenter/interfaceApi/downloadFile";
                var request = new DownloadDllRequest
                {
                    AppKey = appKey,
                    Signature = signature,
                    PythonCode = pythonCode,
                    ExecuteType = executeType
                };

                var json = JsonHelper.Serialize(request);
                var result = await Task.Run(() => _httpHelper.IMEWebPost(url, json, "application/json"));

                // 接口报错返回字符串包含failed
                if (Encoding.Default.GetString(result).Contains("failed"))
                {
                    return new DownloadDllResponse
                    {
                        IsSuccess = false,
                        Message = Encoding.Default.GetString(result)
                    };
                }

                // 尝试反序列化，如果失败则将结果作为 DataStr 返回
                if (JsonHelper.TryDeserialize<DownloadDllResponse>(Encoding.Default.GetString(result), out var response) && response != null)
                {
                    return response;
                }

                // 如果不能反序列化为 DownloadDllResponse，则将原始结果作为 DataStr 返回
                return new DownloadDllResponse
                {
                    IsSuccess = true,
                    DataStr = result
                };
            }
            catch (Exception ex)
            {
                _logger.Error("SysCenter文件下载API调用失败", ex);
                return new DownloadDllResponse
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<FileVersionResponse> GetFileVersionAsync(string pythonCode)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (string.IsNullOrEmpty(settings.WebTestUrlSysCenter))
                {
                    return new FileVersionResponse
                    {
                        Success = false,
                        Message = "WebTestUrlSysCenter未配置"
                    };
                }

                var url = $"{settings.WebTestUrlSysCenter}sysCenter/interfaceApi/selFileVersion";
                var parameters = new Dictionary<string, string>
                {
                    {"pythonCode", pythonCode}
                };

                var result = await Task.Run(() => _httpHelper.IMEWebGet(url, parameters));

                var response = JsonHelper.Deserialize<FileVersionResponse>(result);
                return response ?? new FileVersionResponse
                {
                    Success = false,
                    Message = "反序列化失败"
                };
            }
            catch (Exception ex)
            {
                _logger.Error("获取文件版本API调用失败", ex);
                return new FileVersionResponse
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// UE端数据接收接口-视觉机损检测
        /// </summary>
        /// <param name="position"></param>
        /// <param name="barCode"></param>
        /// <param name="abnormalTimeStamp"></param>
        /// <param name="score"></param>
        /// <param name="defectWidth"></param>
        /// <param name="defectHeight"></param>
        /// <param name="defectArea"></param>
        /// <param name="detectionResult"></param>
        /// <param name="pictureFile"></param>
        /// <returns></returns>
        public async Task<AiDetectionDataResponse> AiDetectionDataAsync(string position, string barCode, long abnormalTimeStamp, float score, float defectWidth, float defectHeight, float defectArea, string detectionResult, byte[]? pictureFile)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (string.IsNullOrEmpty(settings.WebTestUrlSysCenter))
                {
                    return new AiDetectionDataResponse
                    {
                        IsSuccess = false,
                        Message = "WebTestUrlSysCenter未配置"
                    };
                }

                var url = $"{settings.WebTestUrlSysCenter}eeService/abnormalData/aiDetectionData";
                var workshipParts = settings.Workship?.Split(':') ?? new string[4];
                var request = new AiDetectionDataRequest
                {
                    ProductionBase = workshipParts.ElementAtOrDefault(0) ?? "",
                    ProductionLine = workshipParts.ElementAtOrDefault(2) ?? "",
                    Process = workshipParts.ElementAtOrDefault(3) ?? "",
                    DeviceId = settings.DeviceSn ?? "",
                    Position = position,
                    BarCode = barCode,
                    AbnormalTimeStamp = abnormalTimeStamp,
                    Score = score,
                    DefectWidth = defectWidth,
                    DefectHeight = defectHeight,
                    DefectArea = defectArea,
                    DetectionResult = detectionResult
                };

                var json = JsonHelper.Serialize(request);

                var result = Encoding.Default.GetString(await Task.Run(() => _httpHelper.HttpWebPostBytesAsync(json, pictureFile, url, "image/png", "png")));
                // 接口报错返回字符串包含failed
                if (result.Contains("failed"))
                {
                    return new AiDetectionDataResponse
                    {
                        IsSuccess = false,
                        Message = result
                    };
                }

                // 尝试反序列化，如果失败则将结果作为 DataStr 返回
                if (JsonHelper.TryDeserialize<AiDetectionDataResponse>(result, out var response) && response != null)
                {
                    return response;
                }

                return new AiDetectionDataResponse
                {
                    IsSuccess = false,
                    Message = result
                };
            }
            catch (Exception ex)
            {
                _logger.Error("UE端数据上传API调用失败", ex);
                return new AiDetectionDataResponse
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 摄像头新增/更新接口
        /// </summary>
        /// <param name="cameras"></param>
        /// <returns></returns>
        public async Task<AddCameraResponse> AddCameraAsync(List<CameraModel> cameras)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (string.IsNullOrEmpty(settings.WebTestUrlSysCenter))
                {
                    return new AddCameraResponse
                    {
                        IsSuccess = false,
                        Message = "WebTestUrlSysCenter未配置"
                    };
                }

                var url = $"{settings.WebTestUrlSysCenter}eeService/device/addCamera";
                var workshipParts = settings.Workship?.Split(':') ?? new string[4];
                var request = new List<AddCameraRequest>();
                foreach (var item in cameras)
                {
                    request.Add(new AddCameraRequest
                    {
                        ProductionBase = workshipParts.ElementAtOrDefault(0) ?? "",
                        ProductionLine = workshipParts.ElementAtOrDefault(2) ?? "",
                        Process = workshipParts.ElementAtOrDefault(3) ?? "",
                        DeviceId = settings.DeviceSn ?? "",
                        Position = item.WorkStaion,
                        IpAddress = item.Remake,
                        CameraSn = item.GuidId,
                        Enable = item.Enable,
                    });
                }
                var json = JsonHelper.Serialize(request);
                var result = Encoding.Default.GetString(await Task.Run(() => _httpHelper.IMEWebPost(url, json, "application/json")));

                // 接口报错返回字符串包含failed
                if (result.Contains("failed"))
                {
                    return new AddCameraResponse
                    {
                        IsSuccess = false,
                        Message = result
                    };
                }

                // 尝试反序列化，如果失败则将结果作为 DataStr 返回
                if (JsonHelper.TryDeserialize<AddCameraResponse>(result, out var response) && response != null)
                {
                    return response;
                }

                return new AddCameraResponse
                {
                    IsSuccess = false,
                    Message = result
                };
            }
            catch (Exception ex)
            {
                _logger.Error("摄像头新增API调用失败", ex);
                return new AddCameraResponse
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// UE端数据接收-轨迹检测
        /// </summary>
        /// <param name="position"></param>
        /// <param name="barCode"></param>
        /// <param name="abnormalTimeStamp"></param>
        /// <param name="abnormalTimeStampEnd"></param>
        /// <param name="detectionResult"></param>
        /// <param name="file"></param>
        /// <param name="aiPointListDtoList"></param>
        /// <returns></returns>
        public async Task<TrajectoryDataResponse> TrajectoryDataAsync
        (
            string position, string barCode, long abnormalTimeStamp,
            long abnormalTimeStampEnd, byte[] file,
            List<FrameDtoList> frameDtoList, string abnormaVideoStatus
        )
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (string.IsNullOrEmpty(settings.WebTestUrlSysCenter))
                {
                    return new TrajectoryDataResponse
                    {
                        IsSuccess = false,
                        Message = "WebTestUrlSysCenter未配置"
                    };
                }

                var url = $"{settings.WebTestUrlSysCenter}eeService/abnormalData/trajectoryData";
                var workshipParts = settings.Workship?.Split(':') ?? new string[4];
                var request = new TtrajectoryDataRequest
                {
                    Position = position,
                    Process = "叠片工序",
                    ProductionLine = "PTO",
                    ProductionBase = "惠州",
                    //ProductionBase = "枣庄",
                    //ProductionLine = "C01A线",
                    //Process = "CT工序",                    
                    //Position = "上料工位",
                    DeviceId = "CT01",
                    BarCode = barCode,
                    AbnormalTimeStamp = abnormalTimeStamp,
                    AbnormalTimeStampEnd = abnormalTimeStampEnd,
                    DetectionResult = abnormaVideoStatus,
                    AlertLevel = 1,
                    StatusFlag = 1,
                    FrameDtoList = frameDtoList
                };

                var json = JsonHelper.Serialize(request);
                frameDtoList.Clear();
                abnormaVideoStatus = string.Empty;
                var result = Encoding.Default.GetString(await Task.Run(() => _httpHelper.HttpWebPostBytesAsync(json, file, url, "video/mp4", "mp4")));

                // 接口报错返回字符串包含failed
                if (result.Contains("failed"))
                {
                    return new TrajectoryDataResponse
                    {
                        IsSuccess = false,
                        Message = result
                    };
                }

                // 尝试反序列化，如果失败则将结果作为 DataStr 返回
                if (JsonHelper.TryDeserialize<TrajectoryDataResponse>(result, out var response) && response != null)
                {
                    return response;
                }

                return new TrajectoryDataResponse
                {
                    IsSuccess = false,
                    Message = result
                };
            }
            catch (Exception ex)
            {
                _logger.Error("UE端数据接收API调用失败", ex);
                return new TrajectoryDataResponse
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }


        public async Task<TrajectoryDataResponse> TrajectoryDataAsync(
            string position,
            string barCode,
            long abnormalTimeStamp,
            long abnormalTimeStampEnd,
            List<(byte[] FileBytes, string ContentType, string FileName, string FileExt)> videoFiles, // <= 1 或 2 或 0 都支持
            List<FrameDtoList> frameDtoList,
            string abnormaVideoStatus)
        {
            try
            {
                var settings = _configuration.GetSettings();
                var url = $"{settings.WebTestUrlSysCenter}eeService/abnormalData/trajectoryData";

                var request = new TtrajectoryDataRequest
                {
                    Position = position,
                    Process = "叠片工序",
                    ProductionLine = "PTO",
                    ProductionBase = "惠州",
                    DeviceId = "CT01",
                    BarCode = barCode,
                    AbnormalTimeStamp = abnormalTimeStamp,
                    AbnormalTimeStampEnd = abnormalTimeStampEnd,
                    DetectionResult = abnormaVideoStatus,
                    AlertLevel = 1,
                    StatusFlag = 1,
                    FrameDtoList = frameDtoList
                };

                var json = JsonHelper.Serialize(request);
                frameDtoList.Clear();

                // 上传
                var result = Encoding.Default.GetString(await Task.Run(() => _httpHelper.HttpWebPostMultiFilesAsync(json, videoFiles, url)));

                if (result.Contains("failed"))
                    return new TrajectoryDataResponse { IsSuccess = false, Message = result };

                if (JsonHelper.TryDeserialize<TrajectoryDataResponse>(result, out var response) && response != null)
                    return response;

                return new TrajectoryDataResponse { IsSuccess = false, Message = result };
            }
            catch (Exception ex)
            {
                _logger.Error("UE端数据接收API失败", ex);
                return new TrajectoryDataResponse { IsSuccess = false, Message = ex.Message };
            }
        }

        #region 消息or文件推送
        /// <summary>
        /// 推送文本消息
        /// </summary>
        /// <param name="messageContent">消息内容</param>
        /// <returns>是否推送成功</returns>
        public async Task<WebApiSendResponse> PushMessageAsync(string messageContent)
        {
            try
            {
                var settings = _configuration.GetSettings();
                if (settings == null || string.IsNullOrEmpty(settings.WebPushApiUrl))
                {
                    return new WebApiSendResponse
                    {
                        Code = 0,
                        Message = "数据库中Web端的Url未配置"
                    };
                }
                if (!settings.EnableWebPush)
                {
                    return new WebApiSendResponse
                    {
                        Code = 0,
                        Message = "数据库中信息或文件推送功能未启用"
                    };
                }
                var baseUrl = settings.WebPushApiUrl;
                
                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                var url = $"{baseUrl}sendTextFs";

                var request = new PushMessageRequest
                {
                    SystemTypeId = settings.PushSystemTypeId,
                    Message = messageContent,
                    UserId = settings.PushUserId,
                    AppId = settings.PushAppId,
                    SystemType = settings.PushSystemType
                };

                string postData = JsonHelper.Serialize(request);
                var result = await _httpHelper.PostWithRetryAsync(url,postData,retryCount: settings.PushRetryCount,retryDelayMs: settings.PushRetryDelayMs,timeoutSeconds: 5);

                if (result.StartsWith("failed"))
                {
                    _logger.Error($"消息推送失败: {result}");
                    return new WebApiSendResponse { Code = 0, Message = result };
                }

                if (JsonHelper.TryDeserialize<WebApiSendResponse>(result, out var response) && response != null)
                {
                    return response;
                }

                return new WebApiSendResponse { Code = 200, Message = "推送成功" };
            }
            catch (Exception ex)
            {
                _logger.Error($"推送文本消息失败: {ex.Message}", ex);
                return new WebApiSendResponse
                {
                    Code = 0,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 推送文件到默认用户
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否推送成功</returns>
        public async Task<WebApiSendResponse> PushFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.Error($"推送文件不存在: {filePath}");
                return new WebApiSendResponse
                {
                    Code = 0,
                    Message = $"推送文件不存在: {filePath}"
                };
            }

            try
            {
                var settings = _configuration.GetSettings();

                if (settings == null || string.IsNullOrEmpty(settings.WebPushApiUrl))
                {
                    return new WebApiSendResponse
                    {
                        Code = 0,
                        Message = "数据库中Web端的Url未配置"
                    };
                }
                if (!settings.EnableWebPush)
                {
                    return new WebApiSendResponse
                    {
                        Code = 0,
                        Message = "数据库中信息或文件推送功能未启用"
                    };
                }
                if (string.IsNullOrEmpty(settings.PushUserId))
                {
                    return new WebApiSendResponse
                    {
                        Code = 0,
                        Message = "数据库中userId信息未配置"
                    };
                }

                var baseUrl = settings.WebPushApiUrl;
                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                var url = $"{baseUrl}sendFsTextAndFile";

                var formData = new Dictionary<string, string>
                {
                    { "userId", settings.PushUserId },
                    { "appId", settings.PushAppId ?? string.Empty }
                };

                var result = await _httpHelper.PostFileWithRetryAsync( url, filePath, formData, retryCount: settings.PushRetryCount, retryDelayMs: settings.PushRetryDelayMs, timeoutSeconds: 30);

                if (result.StartsWith("failed"))
                {
                    _logger.Error($"文件推送失败: {result}");
                    return new WebApiSendResponse { Code = 0, Message = result };
                }

                if (JsonHelper.TryDeserialize<WebApiSendResponse>(result, out var response) && response != null)
                {
                    return response;
                }

                return new WebApiSendResponse { Code = 200, Message = "文件推送成功" };
            }
            catch (Exception ex)
            {
                _logger.Error($"推送文件失败: {ex.Message}", ex);
                return new WebApiSendResponse
                {
                    Code = 0,
                    Message = ex.Message
                };
            }
        }
     
        #endregion

    }
}
