using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.Core.Models.MES;
using SUNWODA_SEVB.Tool.Helper;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;

namespace SUNWODA_SEVB.MES.Client
{
    /// <summary>
    /// MES API客户端实现
    /// </summary>
    public class MesApiClient : IMesApiClient, IDisposable
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly MesApiConfiguration _configuration;
        private readonly ILoggerService<MesApiClient> _logger;
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        // 静态构造函数 - 正确配置HttpClient
        static MesApiClient()
        {
            var handler = new HttpClientHandler
            {
                // 禁用自动重定向
                AllowAutoRedirect = false,
                // 使用默认代理
                UseProxy = false,
                // 禁用自动解压
                AutomaticDecompression = System.Net.DecompressionMethods.None
            };

            _httpClient = new HttpClient(handler);

            // 设置默认请求头 - 模拟RestSharp的行为
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("*/*"));
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "MES-Client/1.0.0");
            _httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");

            // 设置默认超时
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public MesApiClient(HttpClient httpClient, MesApiConfiguration configuration, ILoggerService<MesApiClient> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _semaphore = new SemaphoreSlim(10, 10);

            // 设置配置的超时时间
            if (_configuration.TimeoutSeconds > 0)
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
            }
        }

        //public async Task<TResponse> PostAsync<TRequest, TResponse>(
        //    string endpoint,
        //    TRequest request,
        //    CancellationToken cancellationToken = default)
        //    where TRequest : IMesRequest
        //    where TResponse : IMesResponse, new()
        //{
        //    ThrowIfDisposed();
        //    await _semaphore.WaitAsync(cancellationToken);

        //    var stopwatch = Stopwatch.StartNew();
        //    var interfaceName = endpoint;
        //    string requestData = string.Empty; // 存储序列化后的JSON
        //    string responseData = string.Empty;
        //    bool isSuccess = false;
        //    string errorMessage = string.Empty;
        //    HttpResponseMessage? httpResponse = null;

        //    try
        //    {
        //        if (!request.Validate(out var validationError))
        //        {
        //            errorMessage = $"请求验证失败: {validationError}";
        //            _logger.Error($"[{interfaceName}] {errorMessage}");
        //            return CreateErrorResponse<TResponse>("400", errorMessage);
        //        }              

        //        var fullUrl = _configuration.GetEndpointUrl(endpoint);
        //        //var requestUrl = "http://10.178.5.6:9002/mes/OfflineDataUpload";
        //        var requestUrl = "http://10.178.5.6:9002";

        //        requestData = JsonHelper.Serialize(request);

        //        _logger.Debug($"[{interfaceName}] 发送请求到 {requestUrl}");
        //        _logger.Debug($"[{interfaceName}] BaseAddress: {_httpClient.BaseAddress}");
        //        _logger.Debug($"[{interfaceName}] 请求的JSON数据: {requestData}");

        //        // 测试
        //        string httpresponse = await TestDirectConnection( requestData);

        //        Console.WriteLine("服务器响应:");
        //        Console.WriteLine(httpresponse);

        //        var formContent = new FormUrlEncodedContent(new[]
        //        {
        //            new KeyValuePair<string, string>("jsonData", requestData)
        //        });

        //        var jsonContent = new StringContent(
        //             requestData,
        //             Encoding.UTF8,
        //             "application/json");

        //        httpResponse = await _retryPolicy.ExecuteHttpAsync(
        //            async () => await _httpClient.PostAsync(requestUrl, jsonContent, cancellationToken),
        //            cancellationToken);


        //        //httpResponse = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cancellationToken);




        //        responseData = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        //        _logger.Info($"[{interfaceName}] 响应数据: {responseData}");

        //        if (!httpResponse.IsSuccessStatusCode)
        //        {
        //            errorMessage = $"HTTP {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}";
        //            _logger.Warn($"[{interfaceName}] {errorMessage}. Response: {responseData}");
        //            return CreateErrorResponse<TResponse>(
        //                httpResponse.StatusCode.ToString(),
        //                errorMessage);
        //        }

        //        var result = DeserializeResponse<TResponse>(responseData);
        //        if (result == null)
        //        {
        //            errorMessage = "响应反序列化失败";
        //            return CreateErrorResponse<TResponse>("DESERIALIZE_ERROR", errorMessage);
        //        }

        //        isSuccess = result.Success;
        //        if (!isSuccess)
        //        {
        //            errorMessage = result.Message ?? "MES返回未知错误";
        //            _logger.Warn($"[{interfaceName}] MES返回错误: {errorMessage}");
        //        }

        //        return result;
        //    }
        //    catch (TaskCanceledException ex)
        //    {
        //        errorMessage = "请求超时";
        //        _logger.Error($"[{interfaceName}] {errorMessage}", ex);
        //        return CreateErrorResponse<TResponse>("TIMEOUT", errorMessage);
        //    }
        //    catch (HttpRequestException ex)
        //    {
        //        errorMessage = $"网络错误: {ex.Message}";
        //        _logger.Error($"[{interfaceName}] {errorMessage}", ex);
        //        return CreateErrorResponse<TResponse>("NETWORK_ERROR", errorMessage);
        //    }
        //    catch (Exception ex)
        //    {
        //        errorMessage = $"未知错误: {ex.Message}";
        //        _logger.Error($"[{interfaceName}] {errorMessage}", ex);
        //        return CreateErrorResponse<TResponse>("UNKNOWN_ERROR", errorMessage);
        //    }
        //    finally
        //    {
        //        _semaphore.Release();
        //        httpResponse?.Dispose();
        //        stopwatch.Stop();

        //        _logger.LogMesInterface(
        //            interfaceName: interfaceName,
        //            requestData: $"jsonData={requestData}", // 记录实际发送的Form数据
        //            responseData: responseData,
        //            isSuccess: isSuccess,
        //            executionTime: (int)stopwatch.ElapsedMilliseconds,
        //            errorMessage: errorMessage
        //        );
        //    }
        //}

        //public async Task<string> TestDirectConnection()
        //{
        //    try
        //    {
        //        // 创建新的HttpClient，完全模拟RestSharp的行为
        //        using var client = new HttpClient();

        //        // 设置与RestSharp相同的headers
        //        client.DefaultRequestHeaders.Accept.Clear();
        //        client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        //        client.DefaultRequestHeaders.UserAgent.Clear();
        //        client.DefaultRequestHeaders.UserAgent.ParseAdd("Apifox/1.0.0 (https://apifox.com)");
        //        client.DefaultRequestHeaders.Host = "10.178.5.6:9002";
        //        client.DefaultRequestHeaders.Connection.Clear();
        //        client.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");

        //        // 准备数据
        //        var jsonData = @"{
        //            ""operatorId"": ""53084205"",
        //            ""productSn"": ""190124C16971700"",
        //            ""groupCode"": ""CDXPD1"",
        //            ""deviceSn"": ""L3ZZABPDJ001"",
        //            ""moNumber"": ""569-MO2407260702-01"",
        //            ""timeStamp"": """ + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + @""",
        //            ""testResult"": ""1"",
        //            ""testData"": [{
        //                ""paramCode"": ""1026"",
        //                ""paramName"": ""AI检测翻折"",
        //                ""paramValue"": ""正极翻折"",
        //                ""paramResult"": ""1"",
        //                ""paramUnit"": """"
        //            }],
        //            ""environment"": [],
        //            ""stepData"": []
        //        }";

        //        var content = new FormUrlEncodedContent(new[]
        //        {
        //            new KeyValuePair<string, string>("jsonData", jsonData)
        //        });

        //        // 发送请求
        //        var response = await client.PostAsync(
        //            "http://10.178.5.6:9002/mes/OfflineDataUpload",
        //            content);

        //        return await response.Content.ReadAsStringAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.Error($"测试连接失败: {ex.Message}", ex);
        //        throw;
        //    }
        //}


        public async Task<TResponse> PostAsync<TRequest, TResponse>(
           string endpoint,
           TRequest request,
           CancellationToken cancellationToken = default)
           where TRequest : IMesRequest
           where TResponse : IMesResponse, new()
        {
            ThrowIfDisposed();
            await _semaphore.WaitAsync(cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            var interfaceName = endpoint;
            string requestData = string.Empty;
            string responseData = string.Empty;
            bool isSuccess = false;
            string errorMessage = string.Empty;
            HttpResponseMessage? httpResponse = null;

            try
            {
                // 1. 验证请求
                if (!request.Validate(out var validationError))
                {
                    errorMessage = $"请求验证失败: {validationError}";
                    _logger.Error($"[{interfaceName}] {errorMessage}");
                    return CreateErrorResponse<TResponse>("VALIDATION_ERROR", errorMessage);
                }

                // 2. 构建请求URL
                var requestUrl = _configuration.GetEndpointUrl(endpoint);
                // 3. 序列化请求数据
                requestData = JsonHelper.Serialize(request);

                _logger.Debug($"[{interfaceName}] 发送请求到: {requestUrl}");
                _logger.Debug($"[{interfaceName}] 请求数据: {requestData}");

                // 4. 创建Form表单内容
                var formData = new Dictionary<string, string>
                {
                    { "jsonData", requestData }
                };
                var formContent = new FormUrlEncodedContent(formData);
                // 5. 设置Content-Type 
                formContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded")
                {
                    CharSet = "utf-8" 
                };
                //var currentTimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                //var jsonparams = $@"{{
                //  ""operatorId"": ""53084205"",
                //  ""productSn"": ""190124C16971700"",
                //  ""groupCode"": ""CDXPD1"",
                //  ""deviceSn"": ""L3ZZABPDJ001"",
                //  ""moNumber"": ""569-MO2407260702-01"",
                //  ""timeStamp"": ""{currentTimeStamp}"",
                //  ""testResult"": ""NG"",
                //  ""testData"": [
                //    {{
                //      ""paramCode"": ""1026"",
                //      ""paramName"": ""AI检测翻折"",
                //      ""paramValue"": ""正极翻折"",
                //      ""paramResult"": ""1"",
                //      ""paramUnit"": """"
                //    }}
                //  ],
                //  ""environment"": [],
                //  ""stepData"": []
                //}}";
                //string jsonHeader = "jsonData";
                //var jsonData = jsonparams.Replace("\n", "").Replace("\t", "");
                //if (!string.IsNullOrEmpty(jsonHeader))
                //{
                //    jsonData = $"{jsonHeader}={Uri.EscapeDataString(jsonparams)}";
                //}

                //var formContent = new StringContent(jsonData, Encoding.UTF8, "application/x-www-form-urlencoded");

                // 6. 创建请求消息 - 可以更精细地控制请求
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = formContent
                };

                // 7. 添加特定的请求头（如果MES服务器需要）
                // requestMessage.Headers.Add("Custom-Header", "value");

                _logger.Debug($"[{interfaceName}] Content-Type: {formContent.Headers.ContentType}");

                // 6. 发送请求
                httpResponse = await _httpClient.PostAsync(requestUrl, formContent);

                // 7. 读取响应
                responseData = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.Debug($"[{interfaceName}] 响应数据: {responseData}");

                // 8. 检查HTTP状态
                if (!httpResponse.IsSuccessStatusCode)
                {
                    errorMessage = $"HTTP {(int)httpResponse.StatusCode}: {httpResponse.ReasonPhrase}";
                    _logger.Warn($"[{interfaceName}] {errorMessage}");
                    return CreateErrorResponse<TResponse>(
                        httpResponse.StatusCode.ToString(),
                        errorMessage);
                }

                // 9. 反序列化响应
                var result = DeserializeResponse<TResponse>(responseData);
                if (result == null)
                {
                    errorMessage = "响应反序列化失败";
                    _logger.Error($"[{interfaceName}] {errorMessage}");
                    return CreateErrorResponse<TResponse>("DESERIALIZE_ERROR", errorMessage);
                }

                // 10. 检查业务状态
                isSuccess = result.Success;
                if (!isSuccess)
                {
                    errorMessage = result.Message ?? "MES返回失败";
                    _logger.Warn($"[{interfaceName}] {errorMessage}");
                }
                else
                {
                    _logger.Info($"[{interfaceName}] 请求成功");
                }

                return result;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                errorMessage = $"请求超时 (>{_configuration.TimeoutSeconds}s)";
                _logger.Error($"[{interfaceName}] {errorMessage}", ex);
                return CreateErrorResponse<TResponse>("TIMEOUT", errorMessage);
            }
            catch (HttpRequestException ex)
            {
                errorMessage = $"网络错误: {ex.Message}";
                _logger.Error($"[{interfaceName}] {errorMessage}", ex);
                return CreateErrorResponse<TResponse>("NETWORK_ERROR", errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = $"未知错误: {ex.Message}";
                _logger.Error($"[{interfaceName}] {errorMessage}", ex);
                return CreateErrorResponse<TResponse>("UNKNOWN_ERROR", errorMessage);
            }
            finally
            {
                _semaphore.Release();
                httpResponse?.Dispose();
                stopwatch.Stop();

                // 记录接口日志
                _logger.LogMesInterface(
                    interfaceName: interfaceName,
                    requestData: $"jsonData={requestData}",
                    responseData: responseData,
                    isSuccess: isSuccess,
                    executionTime: (int)stopwatch.ElapsedMilliseconds,
                    errorMessage: errorMessage
                );
            }
        }

        public async Task<string> TestDirectConnection( string jsonParams)
        {
            try
            {
                using var client = new HttpClient();
                // 设置超时
                client.Timeout = TimeSpan.FromSeconds(30);

                var formData = new Dictionary<string, string>
                {
                    { "jsonData", jsonParams }
                };
                var content = new FormUrlEncodedContent(formData);

                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");


                _logger.Debug($"测试连接 - 请求数据: {content}");

                var response = await client.PostAsync(
                  "http://10.178.5.6:9002/mes/OfflineDataUpload",
                  content);
                var responseString = await response.Content.ReadAsStringAsync();

                _logger.Debug($"测试连接 - 响应状态: {response.StatusCode}");
                _logger.Debug($"测试连接 - 响应内容: {responseString}");

                return responseString;
            }
            catch (Exception ex)
            {
                _logger.Error($"测试连接失败: {ex.Message}", ex);
                return $"Error: {ex.Message}";
            }
        }

        #region Other Methods
        public async Task<TResponse> GetAsync<TResponse>(
            string endpoint,
            object? parameters = null,
            CancellationToken cancellationToken = default)
            where TResponse : IMesResponse, new()
        {
            ThrowIfDisposed();

            await _semaphore.WaitAsync(cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            var interfaceName = endpoint;
            string requestData = string.Empty;
            string responseData = string.Empty;
            bool isSuccess = false;
            string errorMessage = string.Empty;
            HttpResponseMessage? httpResponse = null;

            try
            {
                // 构建URL
                var fullUrl = _configuration.GetEndpointUrl(endpoint);
                if (parameters != null)
                {
                    var queryString = BuildQueryString(parameters);
                    fullUrl = $"{fullUrl}?{queryString}";
                    requestData = JsonHelper.Serialize(parameters);
                }
                else
                {
                    requestData = $"GET {endpoint}";
                }

                _logger.Debug($"[{interfaceName}] 发送GET请求到 {fullUrl}");

                // 发送请求
                httpResponse =  await _httpClient.GetAsync(fullUrl, cancellationToken);

                // 读取响应
                responseData = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                // 检查HTTP状态码
                if (!httpResponse.IsSuccessStatusCode)
                {
                    errorMessage = $"HTTP {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}";
                    _logger.Warn($"[{interfaceName}] {errorMessage}. Response: {responseData}");
                    return CreateErrorResponse<TResponse>(
                        httpResponse.StatusCode.ToString(),
                        errorMessage);
                }

                // 反序列化响应
                var result = DeserializeResponse<TResponse>(responseData);
                if (result == null)
                {
                    errorMessage = "响应反序列化失败";
                    return CreateErrorResponse<TResponse>("DESERIALIZE_ERROR", errorMessage);
                }

                isSuccess = result.Success;
                if (!isSuccess)
                {
                    errorMessage = result.Message ?? "MES返回未知错误";
                    _logger.Warn($"[{interfaceName}] MES返回错误: {errorMessage}");
                }

                return result;
            }
            catch (Exception ex)
            {
                errorMessage = $"请求失败: {ex.Message}";
                _logger.Error($"[{interfaceName}] {errorMessage}", ex);
                return CreateErrorResponse<TResponse>("ERROR", errorMessage);
            }
            finally
            {
                _semaphore.Release();
                httpResponse?.Dispose();
                stopwatch.Stop();

                // 记录MES接口日志
                _logger.LogMesInterface(
                    interfaceName: interfaceName,
                    requestData: requestData,
                    responseData: responseData,
                    isSuccess: isSuccess,
                    executionTime: (int)stopwatch.ElapsedMilliseconds,
                    errorMessage: errorMessage
                );
            }
        }

        public void SetTimeout(TimeSpan timeout)
        {
            _httpClient.Timeout = timeout;
            _logger.Info($"HttpClient超时时间设置为: {timeout.TotalSeconds}秒");
        }

        private TResponse CreateErrorResponse<TResponse>(string code, string message)
            where TResponse : IMesResponse, new()
        {
            var response = new TResponse
            {
                Success = false,
                Code = code,
                Message = message
            };
            return response;
        }

        private TResponse? DeserializeResponse<TResponse>(string responseData)
            where TResponse : IMesResponse
        {
            try
            {
                return JsonHelper.Deserialize<TResponse>(responseData);
            }
            catch (Exception ex)
            {
                _logger.Error($"反序列化响应失败: {ex.Message}. 响应数据: {responseData}");
                return default;
            }
        }

        private string BuildQueryString(object parameters)
        {
            var properties = parameters.GetType().GetProperties();
            var queryParams = new List<string>();

            foreach (var prop in properties)
            {
                var value = prop.GetValue(parameters);
                if (value != null)
                {
                    var stringValue = value.ToString();
                    if (!string.IsNullOrEmpty(stringValue))
                    {
                        queryParams.Add($"{prop.Name}={Uri.EscapeDataString(stringValue)}");
                    }
                }
            }

            return string.Join("&", queryParams);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MesApiClient));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _semaphore?.Dispose();
                }
                _disposed = true;
            }
        }
        #endregion
    }
}