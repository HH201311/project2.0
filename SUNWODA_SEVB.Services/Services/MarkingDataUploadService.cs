using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.Core.Models.MES;

namespace SUNWODA_SEVB.MES.Services
{
    /// <summary>
    /// Marking数据上传服务实现
    /// </summary>
    public class MarkingDataUploadService : IMarkingDataUploadService
    {
        private readonly IMesApiClient _apiClient;
        private readonly IMesConfigurationProvider _configProvider;
        private readonly ILoggerService<MarkingDataUploadService> _logger;

        public string ServiceName => "IncreaseMarking";
        public bool IsEnabled { get; private set; }

        public MarkingDataUploadService(
            IMesApiClient apiClient,
            IMesConfigurationProvider configProvider,
            ILoggerService<MarkingDataUploadService> logger)
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

        public async Task<IncreaseMarkingResponse> UploadAsync(
            string productSn, List<string>? defectList = null, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
                return new IncreaseMarkingResponse { Success = false, Code = "SERVICE_DISABLED", Message = "服务未启用" };

            var config = await _configProvider.GetConfigurationAsync();
            if (config == null)
                return new IncreaseMarkingResponse { Success = false, Code = "CONFIG_MISSING", Message = "配置不可用" };

            var request = new IncreaseMarkingRequest
            {
                DeviceSn = config.DeviceSn,
                SerialNumber = productSn,
                GroupCode = config.GroupCode,
                ControlGroup = config.ControlGroup,
                TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Remark = defectList != null && defectList.Count > 0 ? string.Join(",", defectList) : string.Empty
            };

            return await _apiClient.PostAsync<IncreaseMarkingRequest, IncreaseMarkingResponse>(
                "IncreaseMarking", request, cancellationToken);
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