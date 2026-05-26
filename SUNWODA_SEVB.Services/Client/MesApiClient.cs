using System.Diagnostics;
using System.Net.Http;
using System.Text;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.Core.Models.MES;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.MES.Client
{
    public class MesApiClient : IMesApiClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly MesApiConfiguration _config;
        private readonly ILoggerService<MesApiClient> _logger;
        private bool _disposed;

        public MesApiClient(HttpClient httpClient, MesApiConfiguration config, ILoggerService<MesApiClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); 
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(
            string endpoint, TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IMesRequest
            where TResponse : IMesResponse
        {
            ThrowIfDisposed();

            var requestJson = JsonHelper.Serialize(request);
            var url = _config.GetEndpointUrl(endpoint);

            return await ExecuteWithTimeout<TResponse>(async (ct) =>
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                };
                

                var response = await _httpClient.SendAsync(httpRequest, ct);
                return await ProcessResponse<TResponse>(response, endpoint);
            }, requestJson, cancellationToken);
        }

        public Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint, TRequest request, MesContentType contentType, CancellationToken cancellationToken = default)
        where TRequest : IMesRequest
        where TResponse : IMesResponse
        {
            ThrowIfDisposed();

            var requestJson = JsonHelper.Serialize(request);
            var url = _config.GetEndpointUrl(endpoint);

            return ExecuteWithTimeout<TResponse>(async (ct) =>
            {
                HttpContent content;
                switch (contentType)
                {
                    case MesContentType.JsonBody:
                        content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                        break;
                    case MesContentType.FormUrlEncoded:
                    default:
                        var formData = new Dictionary<string, string>
                        {
                            { "jsonData", requestJson }
                        };
                        content = new FormUrlEncodedContent(formData);
                        // 确保 charset 正确
                        content.Headers.ContentType!.CharSet = Encoding.UTF8.WebName;
                        break;
                }

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };

                var response = await _httpClient.SendAsync(httpRequest, ct);
                return await ProcessResponse<TResponse>(response, endpoint);
            }, requestJson, cancellationToken);
        }

        public async Task<TResponse> GetAsync<TResponse>(
            string endpoint, object? parameters = null, CancellationToken cancellationToken = default)
            where TResponse : IMesResponse
        {
            ThrowIfDisposed();

            var url = _config.GetEndpointUrl(endpoint);
            if (parameters != null)
            {
                var query = BuildQueryString(parameters);
                url = $"{url}?{query}";
            }

            return await ExecuteWithTimeout<TResponse>(async (ct) =>
            {
                var response = await _httpClient.GetAsync(url, ct);
                return await ProcessResponse<TResponse>(response, endpoint);
            }, $"GET {url}", cancellationToken);
        }

        private async Task<TResponse> ExecuteWithTimeout<TResponse>(
            Func<CancellationToken, Task<TResponse>> action,
            string requestInfo,
            CancellationToken externalToken) where TResponse : IMesResponse
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken, timeoutCts.Token);

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await action(linkedCts.Token);
                sw.Stop();
                return result;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                sw.Stop();
                _logger.Error($"[{requestInfo}] 请求超时 ({_config.TimeoutSeconds}s)");
                return CreateErrorResponse<TResponse>("TIMEOUT", "请求超时");
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                _logger.Error($"[{requestInfo}] 网络错误: {ex.Message}");
                return CreateErrorResponse<TResponse>("NETWORK_ERROR", ex.Message);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.Error($"[{requestInfo}] 未知错误: {ex.Message}");
                return CreateErrorResponse<TResponse>("UNKNOWN_ERROR", ex.Message);
            }
        }

        private async Task<TResponse> ProcessResponse<TResponse>(HttpResponseMessage response, string endpoint)
    where TResponse : IMesResponse
        {
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warn($"[{endpoint}] HTTP {(int)response.StatusCode}: {body}");
                return CreateErrorResponse<TResponse>(response.StatusCode.ToString(), body);
            }

            var result = JsonHelper.Deserialize<TResponse>(body);
            if (result == null)
            {
                _logger.Error($"[{endpoint}] 响应反序列化失败");
                return CreateErrorResponse<TResponse>("DESERIALIZE_ERROR", "响应解析失败");
            }

            return result;
        }

        private TResponse CreateErrorResponse<TResponse>(string code, string message) where TResponse : IMesResponse
        {
            var resp = Activator.CreateInstance<TResponse>();
            resp.Success = false;
            resp.Code = code;
            resp.Message = message;
            return resp;
        }

        private string BuildQueryString(object parameters)
        {
            var props = parameters.GetType().GetProperties();
            var segments = from p in props
                           let val = p.GetValue(parameters)
                           where val != null && !string.IsNullOrWhiteSpace(val.ToString())
                           select $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(val.ToString()!)}";
            return string.Join("&", segments);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MesApiClient));
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}