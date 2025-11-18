using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class HttpHelper
    {
        private readonly HttpClient _httpClient;
        private bool _disposed = false;


        public HttpHelper()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
        }

        public byte[] IMEWebPost(string url, string jsonData, Dictionary<string, string>? headers = null)
        {
            return IMEWebPost(url, jsonData, "application/json", headers);
        }

        public byte[] IMEWebPost(string url, string jsonData, string contentType, Dictionary<string, string>? headers = null)
        {
            try
            {
                var content = new StringContent(jsonData, Encoding.UTF8, contentType);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;

                // 添加自定义头部
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                var response = _httpClient.SendAsync(request).Result;
                return response.Content.ReadAsByteArrayAsync().Result;
            }
            catch (Exception ex)
            {
                return Encoding.Default.GetBytes($"failed: {ex.Message}");
            }
        }

        public async Task<byte[]> IMEWebPostAsync(string url, string jsonData, string contentType, Dictionary<string, string>? headers = null)
        {
            try
            {
                var content = new StringContent(jsonData, Encoding.UTF8, contentType);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                var response = await _httpClient.SendAsync(request);
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                return Encoding.Default.GetBytes($"failed: {ex.Message}");
            }
        }

        public string IMEWebGet(string url, Dictionary<string, string>? parameters = null, Dictionary<string, string>? headers = null)
        {
            try
            {
                var queryString = new StringBuilder();
                if (parameters != null && parameters.Count > 0)
                {
                    queryString.Append('?');
                    foreach (var param in parameters)
                    {
                        queryString.Append($"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}&");
                    }
                    queryString.Length--; // 移除最后一个 &
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, url + queryString.ToString());
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                var response = _httpClient.SendAsync(request).Result;
                return response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ex)
            {
                return $"failed: {ex.Message}";
            }
        }

        public string DownloadFileGet(string url, string savePath, string fileName, Dictionary<string, string>? headers = null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                var response = _httpClient.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    var fileBytes = response.Content.ReadAsByteArrayAsync().Result;
                    var fullPath = Path.Combine(savePath, fileName);

                    // 确保目录存在
                    if (!Directory.Exists(savePath))
                    {
                        Directory.CreateDirectory(savePath);
                    }

                    File.WriteAllBytes(fullPath, fileBytes);

                    return "{\"code\":\"200\",\"message\":\"下载成功\"}";
                }

                return $"{{\"code\":\"{(int)response.StatusCode}\",\"message\":\"下载失败: {response.ReasonPhrase}\"}}";
            }
            catch (Exception ex)
            {
                return $"{{\"code\":\"500\",\"message\":\"下载失败: {ex.Message}\"}}";
            }
        }

        public string FileWebPost(string url, string filePath, string fileName, string fileParamName,
            int timeout = 30, Dictionary<string, string>? headers = null, Dictionary<string, string>? formData = null)
        {
            try
            {
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    return $"{{\"code\":\"404\",\"message\":\"文件不存在: {filePath}\"}}";
                }

                // 临时设置超时时间
                var originalTimeout = _httpClient.Timeout;
                _httpClient.Timeout = TimeSpan.FromSeconds(timeout);

                try
                {
                    using var form = new MultipartFormDataContent();
                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    // 添加文件
                    var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    form.Add(fileContent, fileParamName, fileName);

                    // 添加表单数据
                    if (formData != null)
                    {
                        foreach (var item in formData)
                        {
                            form.Add(new StringContent(item.Value), item.Key);
                        }
                    }

                    request.Content = form;

                    // 添加头部
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }

                    var response = _httpClient.SendAsync(request).Result;
                    return response.Content.ReadAsStringAsync().Result;
                }
                finally
                {
                    // 恢复原始超时时间
                    _httpClient.Timeout = originalTimeout;
                }
            }
            catch (Exception ex)
            {
                return $"{{\"code\":\"500\",\"message\":\"上传失败: {ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 视频与图片上传接口
        /// </summary>
        /// <param name="json"></param>
        /// <param name="filePath"></param>
        /// <param name="url"></param>
        /// <param name="fileType"></param>
        /// <returns></returns>
        public async Task<byte[]> HttpWebPostAsync(JObject json, string filePath, string url, string fileType)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // 创建 multipart/form-data 请求体
            using var form = new MultipartFormDataContent();

            // 1️、 添加 JSON 字段
            foreach (var prop in json.Properties())
            {
                // 直接将 JToken 转成字符串写入表单字段
                form.Add(new StringContent(prop.Value.ToString(), Encoding.UTF8), prop.Name);
            }

            // 2️、 添加文件部分（如果存在）
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                var fileContent = new StreamContent(File.OpenRead(filePath));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(fileType);
                form.Add(fileContent, "file", Path.GetFileName(filePath));
            }

            // 3️、 发送 POST 请求
            var response = await _httpClient.PostAsync(url, form);
            response.EnsureSuccessStatusCode();

            // 4️、 读取响应字节
            return await response.Content.ReadAsByteArrayAsync();
        }


        public async Task<byte[]> HttpWebPostBytesAsync(JObject json, byte[] fileBytes, string fileName, string url, string fileType)
        {            
            using var form = new MultipartFormDataContent();

            foreach (var prop in json.Properties())
            {
                form.Add(new StringContent(prop.Value.ToString(), Encoding.UTF8), prop.Name);
            }

            if (fileBytes != null && fileBytes.Length > 0)
            {
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(fileType);
                form.Add(fileContent, "file", fileName);
            }

            var response = await _httpClient.PostAsync(url, form);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<byte[]> HttpWebPostBytesAsync(string jsonString, byte[]? fileBytes, string url, string fileType, string format)
        {
            using var form = new MultipartFormDataContent();

            // 将 JSON 字符串反序列化为字典
            if (!string.IsNullOrWhiteSpace(jsonString))
            {
                var jsonDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                if (jsonDict != null)
                {
                    foreach (var kv in jsonDict)
                    {
                        form.Add(new StringContent(kv.Value?.ToString() ?? string.Empty, Encoding.UTF8), kv.Key);
                    }
                }
            }

            // 如果有文件数据，则添加文件部分
            if (fileBytes != null && fileBytes.Length > 0)
            {                
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(fileType);
                // 自动生成文件名（保证 multipart 格式完整）
                string autoFileName = $"upload_{DateTime.Now:yyyyMMdd_HHmmssfff}.{format}";
                form.Add(fileContent, "file", autoFileName);
            }
            
            var response = await _httpClient.PostAsync(url, form);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
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
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
