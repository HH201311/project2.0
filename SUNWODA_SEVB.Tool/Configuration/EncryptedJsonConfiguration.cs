using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Tool.Helper;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SUNWODA_SEVB.Tool.Configuration
{
    /// <summary>
    /// 支持加密的JSON配置源
    /// </summary>
    public class EncryptedJsonConfigurationSource : JsonConfigurationSource
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
    /// 支持加密的JSON配置提供程序
    /// </summary>
    public class EncryptedJsonConfigurationProvider : JsonConfigurationProvider
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
                // 读取流内容
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();

                // 检查是否加密
                if (JsonHelper.IsEncrypted(content))
                {
                    // 解密内容
                    content = JsonHelper.DecryptJson(content, _encryptionKey, _encryptionIV);

                    // 将解密后的内容转换为流并调用基类的Load方法
                    var decryptedBytes = Encoding.UTF8.GetBytes(content);
                    using var decryptedStream = new MemoryStream(decryptedBytes);
                    base.Load(decryptedStream);
                }
                else
                {
                    // 如果未加密，重置流位置并调用基类方法
                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                        base.Load(stream);
                    }
                    else
                    {
                        // 如果流不支持定位，创建新流
                        var bytes = Encoding.UTF8.GetBytes(content);
                        using var newStream = new MemoryStream(bytes);
                        base.Load(newStream);
                    }
                }
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
    }

    /// <summary>
    /// IConfigurationBuilder的扩展方法
    /// </summary>
    public static class EncryptedJsonConfigurationExtensions
    {
        /// <summary>
        /// 添加支持加密的JSON配置文件
        /// </summary>
        public static IConfigurationBuilder AddEncryptedJsonFile(
            this IConfigurationBuilder builder,
            string path,
            bool optional = false,
            bool reloadOnChange = false,
            byte[]? encryptionKey = null,
            byte[]? encryptionIV = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("路径不能为空", nameof(path));
            }

            return builder.Add(new EncryptedJsonConfigurationSource
            {
                Path = path,
                Optional = optional,
                ReloadOnChange = reloadOnChange,
                EncryptionKey = encryptionKey,
                EncryptionIV = encryptionIV
            });
        }

        /// <summary>
        /// 添加支持加密的JSON配置文件（使用Action配置）
        /// </summary>
        public static IConfigurationBuilder AddEncryptedJsonFile(
            this IConfigurationBuilder builder,
            Action<EncryptedJsonConfigurationSource> configureSource)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configureSource == null)
            {
                throw new ArgumentNullException(nameof(configureSource));
            }

            var source = new EncryptedJsonConfigurationSource();
            configureSource(source);
            return builder.Add(source);
        }
    }

   
}
