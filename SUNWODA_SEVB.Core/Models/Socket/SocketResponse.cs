using System.Text.Json;

namespace SUNWODA_SEVB.Core.Models.Socket
{
    /// <summary>
    /// Socket响应模型
    /// </summary>
    public class SocketResponse
    {
        public bool Result { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime? Timestamp { get; set; }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonOptions);
        }

        public static SocketResponse? FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<SocketResponse>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public static SocketResponse Success(string message = "成功")
        {
            return new SocketResponse
            {
                Result = true,
                Code = "200",
                Message = message,
                Timestamp = DateTime.Now
            };
        }

        public static SocketResponse Error(string code, string message)
        {
            return new SocketResponse
            {
                Result = false,
                Code = code,
                Message = message,
                Timestamp = DateTime.Now
            };
        }

        public static SocketResponse Error(string message)
        {
            return Error("500", message);
        }
    }
}
