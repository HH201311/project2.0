using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class WebHelper1
    {
        public static Response<byte[]> downloadFile1(string appKey, string signature, string pythonCode, string executeType)
        {
            string DeciceInfoAndonUrl = "http://10.201.88.151:7097/" + "sysCenter/interfaceApi/downloadFile";
            Request request = new Request();
            request.appKey = appKey;
            request.signature = signature;
            request.pythonCode = pythonCode;
            request.executeType = executeType;

            var js = new JsonSerializerSettings()
            {
                DateFormatString = "yyyy-MM-dd HH:mm:ss",
            };

            string json = JsonConvert.SerializeObject(request, js);

            var recvData = HttpWebPost1(DeciceInfoAndonUrl, json);

            Response<byte[]> response;
            try
            {
                response = JsonConvert.DeserializeObject<Response<byte[]>>(Encoding.UTF8.GetString(recvData));
            }
            catch
            {
                response = new Response<byte[]>() { success = true, dataStr = recvData };
            }
            return response;
        }
        public static byte[] HttpWebPost1(string url, string jsonparams)
        {
            byte[] buffer = null;
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 3000;
                request.KeepAlive = false;
                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(jsonparams);
                }
                using (var httpResponse = (HttpWebResponse)request.GetResponse())
                {
                    var st = httpResponse.GetResponseStream();
                    buffer = new byte[1024];
                    int actual = 0;
                    //先保存到内存流中MemoryStream
                    var ms = new MemoryStream();
                    while ((actual = st.Read(buffer, 0, 1024)) > 0)
                    {
                        ms.Write(buffer, 0, actual);
                    }
                    ms.Position = 0;
                    buffer = new byte[ms.Length];
                    buffer = ms.ToArray();
                    ms.Close();
                    st.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HttpWebPost异常：{ex}");
            }
            return buffer;
        }
    }

    public class Request
    {
        /// <summary>
        /// 请求调用的接口用户Key，由Web组提供
        /// </summary>
        public string appKey { get; set; }

        /// <summary>
        /// 加密串
        /// </summary>
        public string signature { get; set; }

        /// <summary>
        /// Python脚本配置的编码，前期由Web组提供
        /// </summary>
        public string pythonCode { get; set; }

        /// <summary>
        /// 运行模型类型，PRED或TRAIN
        /// </summary>
        public string executeType { get; set; }
    }
    public class Response<T>
    {
        /// <summary>
        /// 是否成功编码，200表示成功，其余表示失败
        /// </summary>
        public string code { get; set; }
        /// <summary>
        /// 文本信息
        /// </summary>
        public string message { get; set; }
        /// <summary>
        /// 返回python脚本执行的数据
        /// </summary>
        public string data { get; set; }
        /// <summary>
        /// 成功为true，失败为false
        /// </summary>
        public bool success { get; set; }
        /// <summary>
        /// 接收到的字节流文件数据
        /// </summary>
        [JsonIgnore]
        public T dataStr { get; set; }
    }
}
