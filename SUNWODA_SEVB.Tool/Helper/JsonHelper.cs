using SUNWODA_SEVB.Core.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// JSON序列化和反序列化帮助类
    /// </summary>
    public static class JsonHelper
    {
        // 加密标识符，用于识别文件是否已加密
        private const string ENCRYPTION_MARKER = "ENCRYPTED_JSON_V1:";
        // 加密密钥和IV
        private static readonly byte[] DEFAULT_KEY = Encoding.UTF8.GetBytes("SWD2024@SecureKey!@#$%^&*()12345"); // 32 bytes for AES-256
        private static readonly byte[] DEFAULT_IV = Encoding.UTF8.GetBytes("SW2024InitVector"); // 16 bytes


        private static readonly JsonSerializerOptions DefaultOptions = new()
        {
            // 关键配置：允许所有 Unicode 字符，不进行转义
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        #region 基础序列化方法（不加密）
        /// <summary>
        /// 序列化对象为JSON字符串
        /// </summary>
        public static string Serialize<T>(T obj, JsonSerializerOptions? options = null)
        {
            return JsonSerializer.Serialize(obj, options ?? DefaultOptions);
        }

        /// <summary>
        /// 反序列化JSON字符串为对象
        /// </summary>
        public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
        }

        /// <summary>
        /// 尝试反序列化JSON字符串
        /// </summary>
        public static bool TryDeserialize<T>(string json, out T? result, JsonSerializerOptions? options = null)
        {
            result = default;
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return false;

                result = JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
                return result != null;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region 加密相关方法

        /// <summary>
        /// 检查内容是否已加密
        /// </summary>
        public static bool IsEncrypted(string content)
        {
            return !string.IsNullOrEmpty(content) && content.StartsWith(ENCRYPTION_MARKER);
        }

        /// <summary>
        /// 检查文件是否已加密
        /// </summary>
        public static bool IsFileEncrypted(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            var content = File.ReadAllText(filePath);
            return IsEncrypted(content);
        }

        /// <summary>
        /// 加密JSON字符串
        /// </summary>
        public static string EncryptJson(string jsonContent, byte[]? key = null, byte[]? iv = null)
        {
            if (string.IsNullOrEmpty(jsonContent))
                return jsonContent;

            using var aes = Aes.Create();
            aes.Key = key ?? DEFAULT_KEY;
            aes.IV = iv ?? DEFAULT_IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var jsonBytes = Encoding.UTF8.GetBytes(jsonContent);
            var encryptedBytes = encryptor.TransformFinalBlock(jsonBytes, 0, jsonBytes.Length);

            // 添加加密标识符
            return ENCRYPTION_MARKER + Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// 解密JSON字符串
        /// </summary>
        public static string DecryptJson(string encryptedContent, byte[]? key = null, byte[]? iv = null)
        {
            if (string.IsNullOrEmpty(encryptedContent))
                return encryptedContent;

            // 检查是否有加密标识符
            if (!IsEncrypted(encryptedContent))
                return encryptedContent; // 如果没有加密，直接返回原内容

            // 移除加密标识符
            var base64Content = encryptedContent.Substring(ENCRYPTION_MARKER.Length);
            var encryptedBytes = Convert.FromBase64String(base64Content);

            using var aes = Aes.Create();
            aes.Key = key ?? DEFAULT_KEY;
            aes.IV = iv ?? DEFAULT_IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        /// <summary>
        /// 读取JSON文件（自动处理加密）
        /// </summary>
        public static T? ReadJsonFile<T>(string filePath, byte[]? key = null, byte[]? iv = null)
        {
            if (!File.Exists(filePath))
                return default;

            var content = File.ReadAllText(filePath);

            // 自动检测并解密
            if (IsEncrypted(content))
            {
                content = DecryptJson(content, key, iv);
            }

            return Deserialize<T>(content);
        }

        /// <summary>
        /// 写入JSON文件（可选加密）
        /// </summary>
        public static void WriteJsonFile<T>(string filePath, T obj, bool encrypt = false, byte[]? key = null, byte[]? iv = null)
        {
            var jsonContent = Serialize(obj);

            if (encrypt)
            {
                jsonContent = EncryptJson(jsonContent, key, iv);
            }

            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, jsonContent);
        }

        /// <summary>
        /// 生成加密的配置文件
        /// </summary>
        public static void GenerateEncryptedConfig(object configObject, string outputPath, byte[]? key = null, byte[]? iv = null)
        {
            WriteJsonFile(outputPath, configObject, true, key, iv);
        }

        /// <summary>
        /// 从代码生成配置对象
        /// </summary>
        public static AppSettings GenerateDefaultAppSettings()
        {
            return new AppSettings
            {
                ConnectionStrings = new ConnectionStrings
                {
                    DefaultConnection = "server=127.0.0.1;database=sunwoda_demo;uid=root;pwd=root;Port=3306;Persist Security Info=True;SslMode=None;AllowPublicKeyRetrieval=True;"
                }
            };
        }

        #endregion

    }
}
