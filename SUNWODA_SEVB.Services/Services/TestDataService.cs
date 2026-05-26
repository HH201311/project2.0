using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.Core.Models.MES;

namespace SUNWODA_SEVB.MES.Services
{
    /// <summary>
    /// 获取测试数据服务
    /// </summary>
    public class TestDataService : ITestDataService
    {
        private readonly IMesApiClient _apiClient;
        private readonly IMesConfigurationProvider _configProvider;
        private readonly ILoggerService<TestDataService> _logger;

        public string ServiceName => "GetTestData";
        public bool IsEnabled { get; private set; }

        public TestDataService(
            IMesApiClient apiClient,
            IMesConfigurationProvider configProvider,
            ILoggerService<TestDataService> logger)
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

        public async Task<GetTestDataResponse> GetTestDataAsync(string sn, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
                return new GetTestDataResponse { Success = false, Code = "SERVICE_DISABLED", Message = "服务未启用" };

            var config = await _configProvider.GetConfigurationAsync();
            if (config == null)
                return new GetTestDataResponse { Success = false, Code = "CONFIG_MISSING", Message = "MES配置不可用" };


            var request = new GetTestDataRequest
            {
                GroupCode = config.GroupCode,
                OperatorId = config.OperatorId,
                Sn = sn,
                SnType = config.SnType
            };

            request.Validate(out var validationErrors);
            if (validationErrors.Length != 0)
            {
                return new GetTestDataResponse { Success = false, Code = "VALIDATION_FAILED", Message = string.Join(", ", validationErrors) };
            }

            var response = await _apiClient.PostAsync<GetTestDataRequest, GetTestDataResponse>
            (
                "GetTestData",
                request,
                MesContentType.FormUrlEncoded,
                cancellationToken
            );

            if (response.Success)
                _logger.Info($"PACK码 {sn} 的测试数据获取成功");
            else
                _logger.Warn($"PACK码 {sn} 的测试数据获取失败: {response.Message}");

            return response;
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