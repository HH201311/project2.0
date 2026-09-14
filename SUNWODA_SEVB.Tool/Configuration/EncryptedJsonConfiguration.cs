using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using SUNWODA_SEVB.Tool.Helper;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SUNWODA_SEVB.Tool.Configuration
{
    /// <summary>
    /// 支持加密的 JSON 配置源（不依赖 JsonConfigurationSource，避免 Configuration 版本混用时基类 Load 异常）。
    /// </summary>
    public class EncryptedJsonConfigurationSource : FileConfigurationSource
    {
        public byte[]? EncryptionKey { get; set; }
        public byte[]? EncryptionIV { get; set; }

        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);
            return new EncryptedJsonConfigurationProvider(this);
        }
    }

    /// <summary>
    /// 支持加密/明文的 JSON 配置提供程序。
    /// 自行展平 JSON，不调用 Microsoft.Extensions.Configuration.Json 的解析器。
    /// </summary>
    public class EncryptedJsonConfigurationProvider : FileConfigurationProvider
    {
        private readonly byte[]? _encryptionKey;
        private readonly byte[]? _encryptionIV;

        public EncryptedJsonConfigurationProvider(EncryptedJsonConfigurationSource source) : base(source)
        {
            _encryptionKey = source.EncryptionKey;
            _encryptionIV = source.EncryptionIV;
        }

        public override void Load(Stream stream)
        {
            try
            {
                // leaveOpen: true，避免释放 FileConfigurationProvider 打开的文件流
                string content;
                using (var reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true))
                {
                    content = reader.ReadToEnd();
                }

                // 去掉 UTF-8 BOM / 零宽字符，防止现场编辑器另存后解析失败
                content = content.TrimStart('\uFEFF', '\u200B', '\u0000').Trim();

                if (string.IsNullOrWhiteSpace(content))
                {
                    Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    return;
                }

                if (JsonHelper.IsEncrypted(content))
                    content = JsonHelper.DecryptJson(content, _encryptionKey, _encryptionIV);

                Data = FlattenJson(content);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException("无法解密配置文件，请检查加密密钥是否正确", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载配置文件失败: {ex.Message}", ex);
            }
        }

        private static Dictionary<string, string?> FlattenJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            FlattenElement(doc.RootElement, parentPath: null, data);
            return data;
        }

        private static void FlattenElement(JsonElement element, string? parentPath, IDictionary<string, string?> data)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        var path = parentPath == null ? prop.Name : $"{parentPath}:{prop.Name}";
                        FlattenElement(prop.Value, path, data);
                    }
                    break;
                case JsonValueKind.Array:
                    var index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        FlattenElement(item, $"{parentPath}:{index}", data);
                        index++;
                    }
                    break;
                case JsonValueKind.String:
                    data[parentPath!] = element.GetString();
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    data[parentPath!] = element.ToString();
                    break;
                case JsonValueKind.Null:
                    data[parentPath!] = null;
                    break;
            }
        }
    }

    public static class EncryptedJsonConfigurationExtensions
    {
        public static IConfigurationBuilder AddEncryptedJsonFile(
            this IConfigurationBuilder builder,
            string path,
            bool optional = false,
            bool reloadOnChange = false,
            byte[]? encryptionKey = null,
            byte[]? encryptionIV = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("路径不能为空", nameof(path));

            return builder.Add(new EncryptedJsonConfigurationSource
            {
                Path = path,
                Optional = optional,
                ReloadOnChange = reloadOnChange,
                EncryptionKey = encryptionKey,
                EncryptionIV = encryptionIV,
                FileProvider = null
            });
        }

        public static IConfigurationBuilder AddEncryptedJsonFile(
            this IConfigurationBuilder builder,
            Action<EncryptedJsonConfigurationSource> configureSource)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configureSource);

            var source = new EncryptedJsonConfigurationSource();
            configureSource(source);
            return builder.Add(source);
        }
    }
}
