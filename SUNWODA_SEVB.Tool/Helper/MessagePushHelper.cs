using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class MessagePushHelper
    {
        // 配置字段
        private static IConfiguration? _configuration;
        private static string _pushTextApiUrl;
        private static string _pushFileApiUrl;
        private static int _defaultSystemTypeId;
        private static string _defaultUserId;
        private static string _defaultAppId;
        private static string _defaultSystemType;
        private static int _maxRetryCount;
        private static int _retryDelayMs;

        //静态构造函数
        static MessagePushHelper()
        {
            try
            {
                string exeDirectory = AppContext.BaseDirectory;
                var builder = new ConfigurationBuilder()
                    .SetBasePath(exeDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

                _configuration = builder.Build();

                // 初始化配置（文本/文件URL分开配置）
                _pushTextApiUrl = _configuration?["PushConfig:PushTextApiUrl"] ?? "http://10.201.88.151:10100/userCenter/message/sendTextFs";//极耳消息推送
                _pushFileApiUrl = _configuration?["PushConfig:PushFileApiUrl"] ?? "http://10.201.88.151:10100/userCenter/message/sendFsTextAndFile";//PTO PDF推送
                _defaultSystemTypeId = int.TryParse(_configuration?["PushConfig:SystemTypeId"], out int stId) ? stId : 31;//
                _defaultUserId = _configuration?["PushConfig:UserId"] ?? "oc_713fbc8ebd0af75235e06c93346c4752";//极耳推送群编号
                _defaultAppId = _configuration?["PushConfig:AppId"] ?? "cli_a68c3b89643bd00c";
                _defaultSystemType = _configuration?["PushConfig:SystemType"] ?? "12";
                _maxRetryCount = int.TryParse(_configuration?["PushConfig:PushRetryCount"], out int retry) ? retry : 3;
                _retryDelayMs = int.TryParse(_configuration?["PushConfig:PushRetryDelayMs"], out int delay) ? delay : 1000;
            }
            catch (Exception ex)
            {
                // 配置读取失败，使用默认值兜底
                _pushTextApiUrl = "http://10.201.88.151:10100/userCenter/message/sendTextFs";
                _pushFileApiUrl = "http://10.201.88.151:10100/userCenter/message/sendFsTextAndFile";
                _defaultSystemTypeId = 31;
                _defaultUserId = "oc_713fbc8ebd0af75235e06c93346c4752";
                _defaultAppId = "cli_a68c3b89643bd00c";
                _defaultSystemType = "12";
                _maxRetryCount = 3;
                _retryDelayMs = 1000;
                string message  = ex.Message;
            }
        }
        public static async Task<bool> PushMessageAsync(string messageContent, string userId)
        {
            var pushModel = new PushMessageModel
            {
                SystemTypeId = _defaultSystemTypeId,
                Message = messageContent,
                UserId = userId,
                AppId = _defaultAppId,
                SystemType = _defaultSystemType
            };

            string postData = JsonConvert.SerializeObject(pushModel);
            return await SendJsonPostAsync(_pushTextApiUrl, postData);
        }
        #region 文件指定ID推送
        public static async Task<bool> PushFileAsync(string filePath, string userId)
        {
            if (!File.Exists(filePath)) return false;

            return await SendMultipartPostAsync1(_pushFileApiUrl, filePath, userId);
        }
        private static async Task<bool> SendMultipartPostAsync1(string url, string filePath, string userId)
        {
            if (!File.Exists(filePath)) return false;

            int retryCount = 0;

            while (retryCount < _maxRetryCount)
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        using (var content = new MultipartFormDataContent())
                        using (var fileStream = File.OpenRead(filePath))
                        {
                            var fileContent = new StreamContent(fileStream);
                            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");

                            content.Add(fileContent, "file", Path.GetFileName(filePath));
                            content.Add(new StringContent(userId), "userId");
                            content.Add(new StringContent(_defaultAppId), "appId");

                            HttpResponseMessage response = await client.PostAsync(url, content);

                            if (response.IsSuccessStatusCode)
                                return true;
                        }
                    }
                }
                catch { }

                retryCount++;
                if (retryCount < _maxRetryCount)
                    await Task.Delay(_retryDelayMs);
            }
            return false;
        }
        #endregion

        #region 文本默认推送方法
        /// 文本消息推送
        public static async Task<bool> PushMessageAsync(string messageContent, CancellationToken cancellationToken = default)
        {
            var pushModel = new PushMessageModel
            {
                SystemTypeId = _defaultSystemTypeId,
                Message = messageContent,
                UserId = _defaultUserId,
                AppId = _defaultAppId,
                SystemType = _defaultSystemType
            };

            string postData = JsonConvert.SerializeObject(pushModel);
            if (string.IsNullOrEmpty(postData)) return false;

            // 调用通用JSON Post方法
            return await SendJsonPostAsync(_pushTextApiUrl, postData);
        }
        /// 文本推送
        private static async Task<bool> SendJsonPostAsync(string url, string jsonData)
        {
            int retryCount = 0;
            while (retryCount < _maxRetryCount)
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(5);
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                        HttpResponseMessage response = await client.PostAsync(url, content);

                        if (response.IsSuccessStatusCode)
                        {
                            return true;
                        }
                        else
                        {
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    string message = ex.Message;
                }

                retryCount++;
                if (retryCount < _maxRetryCount)
                {
                    await Task.Delay(_retryDelayMs);
                }
            }
            return false;
        }
        #endregion

        #region 文件默认推送
        /// 文件推送
        public static async Task<bool> PushFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            // 调用文件推送
            return await SendMultipartPostAsync(_pushFileApiUrl, filePath);
        }

        /// <summary>
        /// 文件推送用，适配file接口
        /// </summary>
        private static async Task<bool> SendMultipartPostAsync(string url, string filePath)
        {
            // 前置校验
            if (!File.Exists(filePath))
            {
                return false;
            }
            if (Path.GetExtension(filePath).ToLower() != ".pdf")
            {
                return false;
            }

            int retryCount = 0;
            while (retryCount < _maxRetryCount)
            {
                try
                {

                    using (HttpClient client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(10); // PDF推送超时时间加长

                        // 构造multipart/form-data请求体
                        using (var content = new MultipartFormDataContent())
                        {
                            //PDF文件流
                            using (var fileStream = File.OpenRead(filePath))
                            {
                                var fileContent = new StreamContent(fileStream);
                                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
                                content.Add(fileContent, "file", Path.GetFileName(filePath));
                                content.Add(new StringContent(_defaultUserId ?? ""), "userId");
                                content.Add(new StringContent(_defaultAppId ?? ""), "appId");
                                HttpResponseMessage response = await client.PostAsync(url, content);
                                string responseContent = await response.Content.ReadAsStringAsync();

                                if (response.IsSuccessStatusCode)
                                {
                                    return true;
                                }
                                else
                                {
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    string message = ex.Message;
                }

                // 重试逻辑
                retryCount++;
                if (retryCount < _maxRetryCount)
                {
                    await Task.Delay(_retryDelayMs);
                }
            }
            // 所有重试失败
            return false;
        }
        #endregion
    }


    public class PushMessageModel
    {
        [JsonProperty("systemTypeId")]
        public int SystemTypeId { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("userId")]
        public string? UserId { get; set; }

        [JsonProperty("appId")]
        public string? AppId { get; set; }

        [JsonProperty("systemType")]
        public string? SystemType { get; set; }
    }
}