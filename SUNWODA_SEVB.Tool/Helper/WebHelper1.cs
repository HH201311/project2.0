using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class WebHelper1
    {
        private static readonly HttpClient httpClient = new();

        /// <summary>
        /// 同步方法：下载文件（内部调用异步实现）
        /// </summary>
        public static Response<byte[]> DownloadFile1(string appKey, string signature, string pythonCode, string executeType)
        {
            return DownloadFile1Async(appKey, signature, pythonCode, executeType).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步实现，使用 HttpClient
        /// </summary>
        public static async Task<Response<byte[]>> DownloadFile1Async(string appKey, string signature, string pythonCode, string executeType)
        {
            string deviceInfoAndonUrl = "http://10.201.88.151:7097/" + "sysCenter/interfaceApi/downloadFile";

            Request request = new()
            {
                AppKey = appKey,
                Signature = signature,
                PythonCode = pythonCode,
                ExecuteType = executeType
            };

            var jsonSettings = new JsonSerializerSettings
            {
                DateFormatString = "yyyy-MM-dd HH:mm:ss"
            };

            string json = JsonConvert.SerializeObject(request, jsonSettings);

            // 发送 POST 请求，获取响应字节数组
            byte[] recvData = await HttpPostAsync(deviceInfoAndonUrl, json);

            string responseString = Encoding.UTF8.GetString(recvData);
            Response<byte[]>? response;
            try
            {                
                response = JsonConvert.DeserializeObject<Response<byte[]>>(responseString);
            }
            catch
            {
                response = null;
            }

            // 如果反序列化失败或返回 null，则构造默认成功响应，将原始数据放入 DataStr
            response ??= new Response<byte[]> { Success = true, DataStr = recvData };
            return response;                                    
        }

        /// <summary>
        /// 使用 HttpClient 发送 JSON 并返回响应字节数组
        /// </summary>
        private static async Task<byte[]> HttpPostAsync(string url, string jsonParams)
        {
            // 设置超时
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var content = new StringContent(jsonParams, Encoding.UTF8, "application/json");            
            HttpResponseMessage response = await httpClient.PostAsync(url, content, cts.Token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
    }

    public class Request
    {
        /// <summary>
        /// 请求调用的接口用户Key，由Web组提供
        /// </summary>
        [JsonPropertyName("appKey")]
        public string? AppKey { get; set; }

        /// <summary>
        /// 加密串
        /// </summary>
        [JsonPropertyName("signature")]
        public string? Signature { get; set; }

        /// <summary>
        /// Python脚本配置的编码，前期由Web组提供
        /// </summary>
        [JsonPropertyName("pythonCode")]
        public string? PythonCode { get; set; }

        /// <summary>
        /// 运行模型类型，PRED或TRAIN
        /// </summary>
        [JsonPropertyName("executeType")]
        public string? ExecuteType { get; set; }
    }
    public class Response<T>
    {
        /// <summary>
        /// 是否成功编码，200表示成功，其余表示失败
        /// </summary>
        [JsonPropertyName("code")]
        public string? Code { get; set; }
        /// <summary>
        /// 文本信息
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        /// <summary>
        /// 返回python脚本执行的数据
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; }
        /// <summary>
        /// 成功为true，失败为false
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// 接收到的字节流文件数据
        /// </summary>
        [JsonPropertyName("dataStr")]
        public T? DataStr { get; set; }
    }
}
