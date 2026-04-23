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
    /// 统一的配置服务，整合加密配置生成和加载功能
    /// </summary>
    public static class ConfigurationService
    {
        private static readonly string DefaultConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        /// <summary>
        /// 检查是否需要生成默认配置文件
        /// </summary>
        public static bool ShouldGenerateDefaultConfig()
        {
            if (!File.Exists(DefaultConfigPath))
                return true;

            try
            {
                // 尝试读取文件内容检查是否有效
                var content = File.ReadAllText(DefaultConfigPath);

                // 如果是加密文件，尝试解密验证
                AppSettings? existingSettings = null;
                if (JsonHelper.IsEncrypted(content))
                {
                    existingSettings = JsonHelper.ReadJsonFile<AppSettings>(DefaultConfigPath);
                    if (existingSettings == null || string.IsNullOrEmpty(existingSettings.ConnectionStrings?.DefaultConnection))
                    {
                        return true;
                    }
                }
                else
                {
                    // 如果是未加密文件，尝试直接解析验证
                    try
                    {
                        existingSettings = JsonSerializer.Deserialize<AppSettings>(content);
                        if (existingSettings == null || string.IsNullOrEmpty(existingSettings.ConnectionStrings?.DefaultConnection))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        return true; // 解析失败，需要重新生成
                    }
                }

                // 创建默认配置对象用于比较
                var defaultSettings = CreateDefaultAppSettings();

                // 比较现有配置与默认配置是否一致
                return !AreSettingsEqual(existingSettings, defaultSettings);
            }
            catch
            {
                return true; // 读取失败，需要重新生成
            }
        }

        /// <summary>
        /// 创建默认的AppSettings对象
        /// </summary>
        private static AppSettings CreateDefaultAppSettings()
        {
            return new AppSettings
            {
                ConnectionStrings = new ConnectionStrings
                {
                    DefaultConnection = "server=127.0.0.1;database=sunwoda_DEMO;uid=root;pwd=root;Port=3306;Min Pool Size=5;Max Pool Size=100;Persist Security Info=True;SslMode=None;AllowPublicKeyRetrieval=True;"
                },

                // 数据库日志清理默认设置
                LogCleanup = new LogCleanupSettings
                {
                    AppLogRetentionDays = 7,
                    InterfaceLogRetentionDays = 31,
                    MaxDatabaseSizeMB = 100
                }             
            };
        }

        /// <summary>
        /// 比较两个AppSettings对象是否相等
        /// </summary>
        private static bool AreSettingsEqual(AppSettings a, AppSettings b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            // 比较连接字符串
            if (!AreConnectionStringsEqual(a.ConnectionStrings, b.ConnectionStrings))
                return false;

            // 比较日志清理设置
            if (!AreLogCleanupSettingsEqual(a.LogCleanup, b.LogCleanup))
                return false;

            return true;
        }

        /// <summary>
        /// 比较两个ConnectionStrings对象是否相等
        /// </summary>
        private static bool AreConnectionStringsEqual(ConnectionStrings a, ConnectionStrings b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            return a.DefaultConnection == b.DefaultConnection;
        }

        /// <summary>
        /// 比较两个LogCleanupSettings对象是否相等
        /// </summary>
        private static bool AreLogCleanupSettingsEqual(LogCleanupSettings a, LogCleanupSettings b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            return a.AppLogRetentionDays == b.AppLogRetentionDays &&
                   a.InterfaceLogRetentionDays == b.InterfaceLogRetentionDays &&
                   a.MaxDatabaseSizeMB == b.MaxDatabaseSizeMB;
        }

        /// <summary>
        /// 生成默认的加密配置文件
        /// </summary>
        public static void GenerateDefaultEncryptedConfig()
        {
            // 使用CreateDefaultAppSettings方法确保一致性
            var appSettings = CreateDefaultAppSettings();

            // 生成加密文件
            JsonHelper.WriteJsonFile(DefaultConfigPath, appSettings, encrypt: true);
            Console.WriteLine($"加密配置文件已生成: {DefaultConfigPath}");
        }

        /// <summary>
        /// 确保配置文件存在且有效，如果不一致则采用默认配置
        /// </summary>
        public static void EnsureValidConfig()
        {
            if (ShouldGenerateDefaultConfig())
            {
                Console.WriteLine("检测到配置不完整或与默认配置不一致，重新生成配置文件...");
                GenerateDefaultEncryptedConfig();
            }
            else
            {
                // 即使配置存在且有效，也检查是否需要更新（如果默认配置已更改）
                try
                {
                    var existingSettings = JsonHelper.ReadJsonFile<AppSettings>(DefaultConfigPath);
                    if (existingSettings == null)
                    {
                        Console.WriteLine("配置文件内容为空，重新生成配置文件...");
                        GenerateDefaultEncryptedConfig();
                        return;
                    }
                    var defaultSettings = CreateDefaultAppSettings();

                    if (!AreSettingsEqual(existingSettings, defaultSettings))
                    {
                        Console.WriteLine("检测到默认配置已更新，重新生成配置文件...");
                        GenerateDefaultEncryptedConfig();
                    }
                    else
                    {
                        // 检查配置文件是否已加密，如果未加密则进行加密
                        var content = File.ReadAllText(DefaultConfigPath);
                        if (!JsonHelper.IsEncrypted(content))
                        {
                            Console.WriteLine("检测到未加密的配置文件，正在进行加密...");
                            ConvertToEncrypted(DefaultConfigPath, DefaultConfigPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 如果读取失败，重新生成配置文件
                    Console.WriteLine($"读取配置文件时出错: {ex.Message}");
                    Console.WriteLine("重新生成配置文件...");
                    GenerateDefaultEncryptedConfig();
                }
            }
        }

        /// <summary>
        /// 将现有的未加密配置文件转换为加密格式
        /// </summary>
        public static void ConvertToEncrypted(string sourcePath, string? outputPath = null)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"源文件不存在: {sourcePath}");
            }

            // 如果没有指定输出路径，使用默认路径
            outputPath ??= Path.Combine(
                Path.GetDirectoryName(sourcePath) ?? "",
                Path.GetFileNameWithoutExtension(sourcePath) + ".encrypted" + Path.GetExtension(sourcePath)
            );

            // 读取原始配置
            var content = File.ReadAllText(sourcePath);

            // 检查是否已加密
            if (JsonHelper.IsEncrypted(content))
            {
                Console.WriteLine("文件已经是加密状态");
                return;
            }

            // 反序列化为对象
            var appSettings = JsonHelper.Deserialize<AppSettings>(content);

            if (appSettings == null)
            {
                throw new InvalidOperationException("无法解析配置文件");
            }

            // 写入加密文件
            JsonHelper.WriteJsonFile(outputPath, appSettings, encrypt: true);
            Console.WriteLine($"加密配置文件已生成: {outputPath}");
        }

        /// <summary>
        /// 解密配置文件（用于调试）
        /// </summary>
        public static void DecryptConfig(string encryptedPath, string? outputPath = null)
        {
            if (!File.Exists(encryptedPath))
            {
                throw new FileNotFoundException($"加密文件不存在: {encryptedPath}");
            }

            // 如果没有指定输出路径，使用默认路径
            outputPath ??= Path.Combine(
                Path.GetDirectoryName(encryptedPath) ?? "",
                Path.GetFileNameWithoutExtension(encryptedPath) + ".decrypted" + Path.GetExtension(encryptedPath)
            );

            // 读取加密的配置
            var appSettings = JsonHelper.ReadJsonFile<AppSettings>(encryptedPath);

            if (appSettings == null)
            {
                throw new InvalidOperationException("无法解密配置文件");
            }

            // 写入未加密的文件
            JsonHelper.WriteJsonFile(outputPath, appSettings, encrypt: false);
            Console.WriteLine($"解密配置文件已生成: {outputPath}");
        }

        /// <summary>
        /// 从代码生成配置文件（避免手动编辑）
        /// </summary>
        public static void GenerateConfigFromCode(
            string connectionString,
            bool enableMES,
            string outputPath,
            bool encrypt = true)
        {
            var appSettings = new AppSettings
            {
                ConnectionStrings = new ConnectionStrings
                {
                    DefaultConnection = connectionString
                }
            };

            // 写入文件
            JsonHelper.WriteJsonFile(outputPath, appSettings, encrypt);
            Console.WriteLine($"{(encrypt ? "加密" : "未加密")}配置文件已生成: {outputPath}");
        }

        /// <summary>
        /// 验证配置文件是否可以正确读取
        /// </summary>
        public static bool ValidateConfigFile(string configPath)
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"配置文件不存在: {configPath}");
                    return false;
                }

                // 尝试读取配置
                var appSettings = JsonHelper.ReadJsonFile<AppSettings>(configPath);

                if (appSettings == null)
                {
                    Console.WriteLine("无法读取配置文件");
                    return false;
                }

                // 验证必要的配置项
                if (string.IsNullOrEmpty(appSettings.ConnectionStrings?.DefaultConnection))
                {
                    Console.WriteLine("缺少数据库连接字符串");
                    return false;
                }

                var isEncrypted = JsonHelper.IsFileEncrypted(configPath);
                Console.WriteLine($"配置文件验证成功 - 加密状态: {(isEncrypted ? "已加密" : "未加密")}");
                Console.WriteLine($"数据库连接: {appSettings.ConnectionStrings.DefaultConnection}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"验证配置文件时出错: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 配置管理器 - 用于在运行时更新配置
    /// </summary>
    public class ConfigurationManager
    {
        private readonly string _configPath;
        private readonly bool _alwaysEncrypt;

        public ConfigurationManager(string configPath, bool alwaysEncrypt = true)
        {
            _configPath = configPath;
            _alwaysEncrypt = alwaysEncrypt;
        }

        /// <summary>
        /// 更新连接字符串
        /// </summary>
        public void UpdateConnectionString(string newConnectionString)
        {
            // 读取当前配置
            var appSettings = JsonHelper.ReadJsonFile<AppSettings>(_configPath) ?? new AppSettings();

            // 更新连接字符串
            appSettings.ConnectionStrings.DefaultConnection = newConnectionString;

            // 保存配置（保持加密状态）
            var shouldEncrypt = _alwaysEncrypt || JsonHelper.IsFileEncrypted(_configPath);
            JsonHelper.WriteJsonFile(_configPath, appSettings, shouldEncrypt);
        }

        /// <summary>
        /// 更新MES启用状态
        /// </summary>
        public void UpdateMesEnabled(bool enabled)
        {
            // 读取当前配置
            var appSettings = JsonHelper.ReadJsonFile<AppSettings>(_configPath) ?? new AppSettings();

            // 保存配置（保持加密状态）
            var shouldEncrypt = _alwaysEncrypt || JsonHelper.IsFileEncrypted(_configPath);
            JsonHelper.WriteJsonFile(_configPath, appSettings, shouldEncrypt);
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public AppSettings? GetCurrentSettings()
        {
            return JsonHelper.ReadJsonFile<AppSettings>(_configPath);
        }
    }
}