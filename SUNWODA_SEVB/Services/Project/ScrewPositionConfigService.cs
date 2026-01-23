using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Project.STC;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SUNWODA_SEVB.Services.Project
{
    /// <summary>
    /// 界面图片螺丝位置配置服务
    /// </summary>
    public interface IScrewPositionConfigService
    {
        Task<ScrewPositionConfigFile?> LoadConfigAsync(string processCode);
        Task<bool> SaveConfigAsync(ScrewPositionConfigFile config);
        Task<string> GetConfigFilePathAsync(string processCode);
        Task<string> GetConfigBasePathAsync();
        Task InitializeAsync(); 
    }

    public class ScrewPositionConfigService : IScrewPositionConfigService
    {
        private string? _configBasePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILoggerService<ScrewPositionConfigService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly SemaphoreSlim _initSemaphore = new(1, 1);
        private bool _isInitialized = false;

        public ScrewPositionConfigService(
            ILoggerService<ScrewPositionConfigService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        /// <summary>
        /// 显式初始化方法（可在应用启动时调用）
        /// </summary>
        public async Task InitializeAsync()
        {
            await InitializeConfigPathAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 初始化配置路径（从数据库获取或使用默认路径）
        /// </summary>
        private async Task InitializeConfigPathAsync()
        {
            if (_isInitialized) return;

            await _initSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isInitialized) return;

                string? dbConfigPath = null;

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var projectSettingRepo = scope.ServiceProvider
                        .GetRequiredService<IProjectSettingRepository>();

                    var configPath = await projectSettingRepo
                        .GetByNameAndVMAsync("ConfigBasePath", "ConConectorInstaller")
                        .ConfigureAwait(false);
                    dbConfigPath = configPath?.Value;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"从数据库获取配置路径失败，将使用默认路径: {ex.Message}", true);
                }

                if (string.IsNullOrWhiteSpace(dbConfigPath))
                {
                    _configBasePath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Config",
                        "ScrewPositions"
                    );
                    _logger.Info($"使用默认配置路径: {_configBasePath}");
                }
                else
                {
                    _configBasePath = dbConfigPath;
                    _logger.Info($"使用数据库配置路径: {_configBasePath}");
                }

                if (!Directory.Exists(_configBasePath))
                {
                    try
                    {
                        Directory.CreateDirectory(_configBasePath);
                        _logger.Info($"创建配置目录: {_configBasePath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"创建配置目录失败: {ex.Message}");
                        throw;
                    }
                }

                _isInitialized = true;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        /// <summary>
        /// 获取配置基础路径（异步）
        /// </summary>
        public async Task<string> GetConfigBasePathAsync()
        {
            await InitializeConfigPathAsync().ConfigureAwait(false);
            return _configBasePath!;
        }

        /// <summary>
        /// 获取配置文件路径（异步版本）
        /// </summary>
        public async Task<string> GetConfigFilePathAsync(string processCode)
        {
            if (string.IsNullOrWhiteSpace(processCode))
                throw new ArgumentException("工序编码不能为空", nameof(processCode));

            await InitializeConfigPathAsync().ConfigureAwait(false);

            if (_configBasePath == null)
                throw new InvalidOperationException("配置路径未初始化");

            if (!Path.IsPathRooted(_configBasePath))
                throw new InvalidOperationException("配置路径必须为绝对路径");

            var safeProcessCode = Path.GetFileName(processCode);

            // 额外安全校验：只允许字母、数字、下划线、短横线和点，避免特殊符号
            if (string.IsNullOrEmpty(safeProcessCode) || safeProcessCode.Contains(".."))
                throw new ArgumentException("无效的工序编码，包含非法字符或目录跳转。", nameof(processCode));

            // 正则校验： 允许 a-zA-Z0-9._-
            if (!Regex.IsMatch(safeProcessCode, @"^[\w\-.]+$"))
                throw new ArgumentException("工序编码包含不允许的字符，只允许字母、数字、下划线、短横线和点。", nameof(processCode));

            // 确保_basePath不以目录分隔符结尾
            string basePath = _configBasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // 使用 Path.ChangeExtension 添加或替换成 .json
            string fileNameWithJson = Path.ChangeExtension(safeProcessCode, ".json");

            string fullPath = Path.Combine(basePath, fileNameWithJson);
            return fullPath;
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        public async Task<ScrewPositionConfigFile?> LoadConfigAsync(string processCode)
        {
            if (string.IsNullOrWhiteSpace(processCode))
                return null;

            var filePath = await GetConfigFilePathAsync(processCode).ConfigureAwait(false);

            if (!File.Exists(filePath))
            {
                _logger.Warn($"配置文件不存在: {filePath}", true);
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                var config = JsonSerializer.Deserialize<ScrewPositionConfigFile>(json, _jsonOptions);

                if (config != null && config.ProcessCode != processCode)
                {
                    _logger.Warn($"配置文件中的工序编码({config.ProcessCode})与请求的({processCode})不匹配");
                }

                return config;
            }
            catch (JsonException ex)
            {
                _logger.Error($"JSON解析失败 {filePath}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error($"读取配置文件失败 {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存配置文件
        /// </summary>
        public async Task<bool> SaveConfigAsync(ScrewPositionConfigFile config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(config.ProcessCode))
            {
                _logger.Error("保存配置失败: 工序编码为空");
                return false;
            }

            var filePath = await GetConfigFilePathAsync(config.ProcessCode).ConfigureAwait(false);

            try
            {
                if (File.Exists(filePath))
                {
                    var backupPath = $"{filePath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
                    File.Copy(filePath, backupPath, true);
                    _logger.Info($"创建配置文件备份: {backupPath}");
                }

                var json = JsonSerializer.Serialize(config, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);

                _logger.Info($"配置文件保存成功: {filePath}");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Error($"保存配置失败，权限不足: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                _logger.Error($"保存配置失败，IO错误: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"保存配置失败 {filePath}: {ex.Message}");
                return false;
            }
        }
    }
}