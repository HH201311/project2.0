//using Google.Protobuf.WellKnownTypes;
//using Org.BouncyCastle.Asn1.Crmf;
//using SUNWODA_SEVB.Core.Interfaces;
//using SUNWODA_SEVB.Core.Interfaces.MES;
//using SUNWODA_SEVB.Core.Models.MES;
//using SUNWODA_SEVB.MES.Models;
//using System.Net.Http;
//using System.Text;
//using RestSharp;

//namespace SUNWODA_SEVB.MES.Services
//{
//    /// <summary>
//    /// 离线数据上传服务接口
//    /// </summary>
//    public interface IOfflineDataUploadService : IMesService
//    {     
//        Task<OfflineDataUploadResponse> UploadAsync(
//            string productSn,
//            string testResult,
//            List<OfflineTestData> testDatas,
//            List<EnvironmentData>? environments = null,
//            List<StepData>? stepDatas = null,
//            CancellationToken cancellationToken = default);
//        Task<string> HttpWebPostAsync(string url,string jsonData);
//    }

//    /// <summary>
//    /// 离线数据上传服务实现
//    /// </summary>
//    public class OfflineDataUploadService : IOfflineDataUploadService
//    {
//        private readonly IMesApiClient _apiClient;
//        private readonly IMesConfigurationProvider _configProvider;
//        private readonly ILoggerService<OfflineDataUploadService> _logger;

//        public string ServiceName => "OfflineDataUpload";
//        public bool IsEnabled { get; private set; }

//        public OfflineDataUploadService(
//            IMesApiClient apiClient,
//            IMesConfigurationProvider configProvider,
//            ILoggerService<OfflineDataUploadService> logger)
//        {
//            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
//            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public async Task<bool> InitializeAsync()
//        {
//            try
//            {
//                var config = await _configProvider.GetConfigurationAsync();
//                IsEnabled = config != null;

//                if (IsEnabled)
//                {
//                    _logger.Info($"{ServiceName} 服务初始化成功");
//                }
//                else
//                {
//                    _logger.Info($"{ServiceName} 服务未启用");
//                }

//                return IsEnabled;
//            }
//            catch (Exception ex)
//            {
//                _logger.Error($"{ServiceName} 服务初始化失败", ex);
//                IsEnabled = false;
//                return false;
//            }
//        }

//        public async Task<OfflineDataUploadResponse> UploadAsync(
//            string productSn,
//            string testResult,
//            List<OfflineTestData> testDatas,
//            List<EnvironmentData>? _environments = null,
//            List<StepData>? _stepDatas = null,
//            CancellationToken cancellationToken = default)
//        {
//            if (!IsEnabled)
//            {
//                _logger.Warn("离线数据上传服务未启用");
//                return new OfflineDataUploadResponse
//                {
//                    Success = false,
//                    Code = "SERVICE_DISABLED",
//                    Message = "离线数据上传服务未启用"
//                };
//            }

//            try
//            {
//                var config = await _configProvider.GetConfigurationAsync();
//                if (config == null)
//                {
//                    throw new InvalidOperationException("MES配置不可用");
//                }

//                _logger.Info($"开始上传产品 {productSn} 的离线数据");

//                var request = new OfflineDataUploadRequest
//                {
//                    operatorId = config.OperatorId,
//                    productSn = productSn,
//                    groupCode = config.GroupCode,
//                    deviceSn = config.DeviceSn,
//                    moNumber = config.MoNumber,
//                    timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
//                    testResult = testResult,
//                    testData = testDatas,
//                    environment = [],
//                    stepData = []
//                };

//                var response = await _apiClient.PostAsync<OfflineDataUploadRequest, OfflineDataUploadResponse>(
//                    "OfflineDataUpload",
//                    request,
//                    cancellationToken);

//                if (response.Success)
//                {
//                    _logger.Info($"产品 {productSn} 的离线数据上传成功");
//                }
//                else
//                {
//                    _logger.Warn($"产品 {productSn} 的离线数据上传失败: {response.Message}");
//                }

//                return response;
//            }
//            catch (Exception ex)
//            {
//                _logger.Error($"上传产品 {productSn} 的离线数据时发生错误", ex);

//                return new OfflineDataUploadResponse
//                {
//                    Success = false,
//                    Code = "EXCEPTION",
//                    Message = ex.Message
//                };
//            }
//        }



//        #region 方法测试

//        private static readonly HttpClient httpClient = new HttpClient();


//        /// <summary>
//        /// 测试方法
//        /// </summary>
//        /// <param name="url"></param>
//        /// <param name="jsonparams"></param>
//        /// <param name="jsonHeader"></param>
//        /// <returns></returns>
//        public async Task<string> HttpWebPostAsync(string url, string jsonData)
//        {
//            try
//            {
//                _logger.Info("开始使用方式一代码进行测试上传...");

//                // 完全复制控制台程序的HttpClient配置
//                using var httpClient = new HttpClient();

//                // 准备要发送的数据 - 完全与控制台程序相同
//                var jsonDataEncoded = $"jsonData={Uri.EscapeDataString(jsonData)}";

//                // 设置请求超时时间
//                httpClient.Timeout = TimeSpan.FromSeconds(20);

//                // 创建请求内容 - 完全与控制台程序相同
//                var content = new StringContent(jsonDataEncoded, Encoding.UTF8, "application/x-www-form-urlencoded");

//                // 添加与控制台程序相同的headers
//                content.Headers.Clear();
//                httpClient.DefaultRequestHeaders.Clear();
//                httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
//                httpClient.DefaultRequestHeaders.Add("Host", "10.178.5.6:9002");
//                httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
//                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Apifox/1.0.0 (https://apifox.com)");

//                _logger.Debug($"发送请求到: http://10.178.5.6:9002/mes/OfflineDataUpload");
//                _logger.Debug($"请求体: {jsonDataEncoded}");

//                // 发送 POST 请求
//                HttpResponseMessage response = await httpClient.PostAsync("http://10.178.5.6:9002/mes/OfflineDataUpload", content);

//                // 读取响应内容
//                var resultContent = await response.Content.ReadAsStringAsync();

//                string result;
//                if (response.IsSuccessStatusCode)
//                {
//                    result = $"请求成功! 状态码: {(int)response.StatusCode} {response.StatusCode}, 响应内容: {resultContent}";
//                    _logger.Info($"HttpClient测试上传成功: {resultContent}");
//                }
//                else
//                {
//                    result = $"请求失败! 状态码: {(int)response.StatusCode} {response.StatusCode}, 响应内容: {resultContent}";
//                    _logger.Warn($"HttpClient测试上传失败: {resultContent}");
//                }

//                return result;
//            }
//            catch (Exception ex)
//            {
//                var errorMessage = $"HttpClient请求异常: {ex.Message}";
//                if (ex.InnerException != null)
//                {
//                    errorMessage += $", 内部异常: {ex.InnerException.Message}";
//                }
//                _logger.Error($"HttpClient测试上传异常", ex);
//                return errorMessage;
//            }
//        }





//        #endregion


//        public async Task ResetAsync()
//        {
//            IsEnabled = false;
//            await Task.CompletedTask;
//        }

//        public async Task<bool> UpdateEnabledStatusAsync()
//        {
//            try
//            {
//                var config = await _configProvider.GetConfigurationAsync();
//                var newStatus = config != null;

//                if (newStatus != IsEnabled)
//                {
//                    IsEnabled = newStatus;
//                    _logger.Info($"{ServiceName} 状态已更新: {IsEnabled}");
//                }

//                return IsEnabled;
//            }
//            catch (Exception ex)
//            {
//                _logger.Error($"{ServiceName} 更新状态失败", ex);
//                return IsEnabled;
//            }
//        }
//    }
//}

using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.Core.Models.MES;

namespace SUNWODA_SEVB.MES.Services
{
    public class OfflineDataUploadService : IOfflineDataUploadService
    {
        private readonly IMesApiClient _apiClient;
        private readonly IMesConfigurationProvider _configProvider;
        private readonly ILoggerService<OfflineDataUploadService> _logger;

        public string ServiceName => "OfflineDataUpload";
        public bool IsEnabled { get; private set; }

        public OfflineDataUploadService(
            IMesApiClient apiClient,
            IMesConfigurationProvider configProvider,
            ILoggerService<OfflineDataUploadService> logger)
        {
            _apiClient = apiClient;
            _configProvider = configProvider;
            _logger = logger;
        }

        public async Task<bool> InitializeAsync()
        {
            var config = await _configProvider.GetConfigurationAsync();
            IsEnabled = config != null;
            _logger.Info($"{ServiceName} 初始化完成，状态：{IsEnabled}");
            return IsEnabled;
        }

        public async Task<OfflineDataUploadResponse> UploadAsync(
            string productSn, string testResult,
            List<OfflineTestData> testDatas,
            List<EnvironmentData>? environments = null,
            List<StepData>? stepDatas = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
                return new OfflineDataUploadResponse { Success = false, Code = "SERVICE_DISABLED", Message = "服务未启用" };

            var config = await _configProvider.GetConfigurationAsync();
            if (config == null)
                return new OfflineDataUploadResponse { Success = false, Code = "CONFIG_MISSING", Message = "配置不可用" };

            var request = new OfflineDataUploadRequest
            {
                OperatorId = config.OperatorId,
                ProductSn = productSn,
                GroupCode = config.GroupCode,
                DeviceSn = config.DeviceSn,
                MoNumber = config.MoNumber,
                TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                TestResult = testResult,
                TestData = testDatas,
                Environment = environments ?? new List<EnvironmentData>(),
                StepData = stepDatas ?? new List<StepData>()
            };

            return await _apiClient.PostAsync<OfflineDataUploadRequest, OfflineDataUploadResponse>(
                "OfflineDataUpload", request, cancellationToken);
        }

        public async Task ResetAsync()
        {
            IsEnabled = false;
            await Task.CompletedTask;
        }

        public async Task<bool> UpdateEnabledStatusAsync()
        {
            var config = await _configProvider.GetConfigurationAsync();
            IsEnabled = config != null;
            return IsEnabled;
        }
    }
}