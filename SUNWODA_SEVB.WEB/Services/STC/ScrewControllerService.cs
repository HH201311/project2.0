using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.STC;
using SUNWODA_SEVB.Core.Models.Project.STC.Results;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace SUNWODA_SEVB.WEB.Services.STC
{
    public class ScrewControllerService : IScrewControllerService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService<ScrewControllerService> _logger;
        private readonly HttpClient _httpClient;
        private bool _disposed;
        private string vmProjectName = "";

        // Cookie缓存（按控制器IP）
        private readonly ConcurrentDictionary<string, CookieCacheRecord> _cookieCache = new();

        // TR值缓存（按控制器IP）
        private readonly ConcurrentDictionary<string, TrValueCacheRecord> _trValueCache = new();

        // 配置缓存（按VM名称）
        private readonly ConcurrentDictionary<string, ScrewControllerSettings> _settingsCache = new();

        // 扭矩/电流转换常量
        private const double TorqueConversionConst = 1.0 / 16384.0;
        private const double CurrentConversionConst = 1.0 / 256.0;

        // 数据库缓存Key前缀
        private const string TR_CACHE_PREFIX = "TrCache_";

        // Cookie过期时间（小时），默认24小时
        private const int DEFAULT_COOKIE_EXPIRATION_HOURS = 24;

        public ScrewControllerService(
            IServiceProvider serviceProvider,
            ILoggerService<ScrewControllerService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClientFactory.CreateClient("ScrewController");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        #region 配置加载

        /// <summary>
        /// 从ProjectSetting加载指定工序的控制器配置
        /// </summary>
        public async Task<ScrewControllerSettings> LoadSettingsAsync(string vmName)
        {
            ArgumentException.ThrowIfNullOrEmpty(vmName);
            vmProjectName = vmName;
            // 检查缓存
            if (_settingsCache.TryGetValue(vmName, out var cached))
            {
                return cached;
            }

            var settings = new ScrewControllerSettings();

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                // 加载基础配置
                await LoadBasicSettingsAsync(projectSettingRepo, vmName, settings);

                // 加载控制器配置
                await LoadControllerConfigsAsync(projectSettingRepo, vmName, settings);

                // 加载网络配置
                await LoadNetworkSettingsAsync(projectSettingRepo, vmName, settings);

                // 缓存配置
                _settingsCache[vmName] = settings;

                // 如果启用了数据库缓存，加载已缓存的TR值
                if (settings.EnableTrValueDbCache)
                {
                    await LoadTrValuesFromDbToMemoryAsync(vmName, settings);
                }

                LogSettingsInfo(vmName, settings);
            }
            catch (Exception ex)
            {
                _logger.Error($"加载螺丝控制器配置失败 - VM: {vmName}", ex);
            }

            return settings;
        }

        private async Task LoadBasicSettingsAsync(
          IProjectSettingRepository repo, string vmName, ScrewControllerSettings settings)
        {
            // 曲线数据获取开关
            var enableFetch = await repo.GetByNameAndVMAsync("Screw_EnableCurveDataFetch", vmName);
            if (bool.TryParse(enableFetch?.Value, out bool enable))
            {
                settings.EnableCurveDataFetch = enable;
            }

            // TR值数据库缓存开关
            var enableTrDbCache = await repo.GetByNameAndVMAsync("Screw_EnableTrValueDbCache", vmName);
            if (bool.TryParse(enableTrDbCache?.Value, out bool enableDbCache))
            {
                settings.EnableTrValueDbCache = enableDbCache;
            }

            // TR值缓存过期时间
            var trCacheExpiration = await repo.GetByNameAndVMAsync("Screw_TrValueCacheExpirationHours", vmName);
            if (int.TryParse(trCacheExpiration?.Value, out int hours) && hours >= 0)
            {
                settings.TrValueCacheExpirationHours = hours;
            }

            // 枪型配置（有线/无线）
            var isWiredConfig = await repo.GetByNameAndVMAsync("Screw_IsWired", vmName);
            if (bool.TryParse(isWiredConfig?.Value, out bool isWired))
            {
                settings.IsWired = isWired;
            }

            // 用户名密码从配置中获取
            var username = await repo.GetByNameAndVMAsync("Screw_LoginUsername", vmName);
            if (!string.IsNullOrEmpty(username?.Value))
            {
                settings.LoginUsername = username.Value;
            }

            var password = await repo.GetByNameAndVMAsync("Screw_LoginPassword", vmName);
            if (!string.IsNullOrEmpty(password?.Value))
            {
                settings.LoginPassword = password.Value;
            }

            //请求体
            var requestBody = await repo.GetByNameAndVMAsync("Screw_LoginRequestBody", vmName);
            if (!string.IsNullOrEmpty(requestBody?.Value))
            {
                settings.base64RequestBody = requestBody.Value;
            }
        }

        private async Task LoadControllerConfigsAsync(
            IProjectSettingRepository repo, string vmName, ScrewControllerSettings settings)
        {
            // 优先级1：JSON格式的多控制器配置
            var controllersJson = await repo.GetByNameAndVMAsync("Screw_Controllers", vmName);
            if (!string.IsNullOrEmpty(controllersJson?.Value))
            {
                try
                {
                    var controllers = JsonSerializer.Deserialize<List<ControllerEndpoint>>(
                        controllersJson.Value,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (controllers?.Count > 0)
                    {
                        settings.Controllers = controllers;
                        BuildTightenGunMapping(settings, controllers);
                        _logger.Info($"加载JSON控制器配置成功 - VM: {vmName}, 控制器数: {controllers.Count}",true);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.Warn($"解析多控制器配置失败: {ex.Message}");
                }
            }

            // 优先级2：螺丝枪映射配置
            var gunMappingConfig = await repo.GetByNameAndVMAsync("Screw_TightenGunMapping", vmName);
            if (!string.IsNullOrEmpty(gunMappingConfig?.Value))
            {
                ParseGunMappingConfig(settings, gunMappingConfig.Value);
            }

            // 优先级3：逗号分隔的多个IP
            var controllerIPs = await repo.GetByNameAndVMAsync("Screw_ControllerIPs", vmName);
            if (!string.IsNullOrEmpty(controllerIPs?.Value))
            {
                ParseControllerIPs(settings, controllerIPs.Value);
            }

            // 优先级4：单个IP（向后兼容）
            var controllerIP = await repo.GetByNameAndVMAsync("Screw_ControllerIP", vmName);
            if (!string.IsNullOrEmpty(controllerIP?.Value) && IsValidIP(controllerIP.Value.Trim()))
            {
                settings.ControllerIP = controllerIP.Value.Trim();
            }
        }

        private async Task LoadNetworkSettingsAsync(
            IProjectSettingRepository repo, string vmName, ScrewControllerSettings settings)
        {
            var apiBasePath = await repo.GetByNameAndVMAsync("Screw_ApiBasePath", vmName);
            if (!string.IsNullOrEmpty(apiBasePath?.Value))
            {
                settings.ApiBasePath = NormalizeApiPath(apiBasePath.Value);
            }

            var timeout = await repo.GetByNameAndVMAsync("Screw_RequestTimeoutSeconds", vmName);
            if (int.TryParse(timeout?.Value, out int timeoutSec) && timeoutSec > 0)
            {
                settings.RequestTimeoutSeconds = timeoutSec;
            }

            var retryCount = await repo.GetByNameAndVMAsync("Screw_RetryCount", vmName);
            if (int.TryParse(retryCount?.Value, out int retry) && retry >= 0)
            {
                settings.RetryCount = retry;
            }

            var retryInterval = await repo.GetByNameAndVMAsync("Screw_RetryIntervalMs", vmName);
            if (int.TryParse(retryInterval?.Value, out int interval) && interval > 0)
            {
                settings.RetryIntervalMs = interval;
            }
        }

        private void BuildTightenGunMapping(ScrewControllerSettings settings, List<ControllerEndpoint> controllers)
        {
            foreach (var controller in controllers)
            {
                var gun = controller.TightenGuns?.Trim();
                if (!string.IsNullOrEmpty(gun) && !settings.TightenGunMapping.ContainsKey(gun))
                {
                    settings.TightenGunMapping[gun] = controller.IP;
                }
            }
        }

        private void ParseGunMappingConfig(ScrewControllerSettings settings, string config)
        {
            var mappings = config.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var mapping in mappings)
            {
                var parts = mapping.Split('=');
                if (parts.Length == 2)
                {
                    var gun = parts[0].Trim();
                    var ip = parts[1].Trim();
                    if (!string.IsNullOrEmpty(gun) && IsValidIP(ip))
                    {
                        settings.TightenGunMapping[gun] = ip;
                    }
                }
            }
        }

        private void ParseControllerIPs(ScrewControllerSettings settings, string ipList)
        {
            var ips = ipList.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var ip in ips)
            {
                var trimmedIp = ip.Trim();
                if (IsValidIP(trimmedIp) && !settings.ControllerIPs.Contains(trimmedIp))
                {
                    settings.ControllerIPs.Add(trimmedIp);
                }
            }
        }

        private static string NormalizeApiPath(string path)
        {
            var normalized = path.Trim();
            if (!normalized.StartsWith('/'))
                normalized = "/" + normalized;
            if (!normalized.EndsWith('/'))
                normalized += "/";
            return normalized;
        }

        private void LogSettingsInfo(string vmName, ScrewControllerSettings settings)
        {
            var ipList = GetAllControllerIPs(settings);
            var gunMappingInfo = string.Join(", ", settings.TightenGunMapping.Select(kv => $"{kv.Key}={kv.Value}"));

            _logger.Info($"螺丝控制器配置加载完成 - VM: {vmName}");
            _logger.Info($"  - 总IP数: {ipList.Count}, IP列表: {string.Join(", ", ipList)}");
            _logger.Info($"  - 螺丝枪映射: {(string.IsNullOrEmpty(gunMappingInfo) ? "无" : gunMappingInfo)}");
            _logger.Info($"  - TR值数据库缓存: {(settings.EnableTrValueDbCache ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 根据螺丝枪编号获取对应控制器IP
        /// </summary>
        public string? GetControllerIPByTightenGun(ScrewControllerSettings settings, string tightenGun)
        {
            if (settings == null) return null;
            if (string.IsNullOrEmpty(tightenGun))
                return settings.ControllerIP;

            // 优先从映射中获取
            var key = settings.TightenGunMapping.Keys
                .FirstOrDefault(k => string.Equals(k, tightenGun, StringComparison.OrdinalIgnoreCase));
            if (key != null && settings.TightenGunMapping.TryGetValue(key, out var mappedIP))
            {
                return mappedIP;
            }

            // 从Controllers配置中查找
            var controller = settings.Controllers.FirstOrDefault(c =>
                 string.Equals(c.TightenGuns?.Trim(), tightenGun, StringComparison.OrdinalIgnoreCase));
            if (controller != null)
            {
                return controller.IP;
            }

            // 返回默认IP
            return settings.ControllerIP ?? settings.ControllerIPs.FirstOrDefault();
        }

        /// <summary>
        /// 获取所有配置的控制器IP地址
        /// </summary>
        public List<string> GetAllControllerIPs(ScrewControllerSettings settings)
        {
            if (settings == null) return new List<string>();

            var ipList = new List<string>();

            // 优先级1：JSON配置的控制器
            if (settings.Controllers.Count > 0)
            {
                foreach (var controller in settings.Controllers)
                {
                    if (!string.IsNullOrEmpty(controller.IP) && IsValidIP(controller.IP))
                    {
                        ipList.Add(controller.IP);
                    }
                }
            }

            // 优先级2：逗号分隔的IP列表
            if (settings.ControllerIPs.Count > 0)
            {
                foreach (var ip in settings.ControllerIPs)
                {
                    if (IsValidIP(ip) && !ipList.Contains(ip)) // 避免重复
                    {
                        ipList.Add(ip);
                    }
                }
            }

            // 优先级3：单个IP配置（向后兼容）
            if (!string.IsNullOrEmpty(settings.ControllerIP) &&
                IsValidIP(settings.ControllerIP) &&
                !ipList.Contains(settings.ControllerIP))
            {
                ipList.Add(settings.ControllerIP);
            }

            return ipList.Distinct().ToList();
        }

        /// <summary>
        /// 验证IP地址格式
        /// </summary>
        private static bool IsValidIP(string ip)
        {
            //return !string.IsNullOrWhiteSpace(ip) && System.Net.IPAddress.TryParse(ip, out _);
            if (string.IsNullOrWhiteSpace(ip))
                return false;

            // 简单的IP地址验证
            var parts = ip.Split('.');
            if (parts.Length != 4)
                return false;

            foreach (var part in parts)
            {
                if (!int.TryParse(part, out int num) || num < 0 || num > 255)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 获取控制器配置详情（包含名称）
        /// </summary>
        public List<ScrewControllerDetail> GetControllerDetails(ScrewControllerSettings settings)
        {
            if (settings == null)
            {
                _logger.Warn("GetControllerDetails: 输入参数settings为null，返回空列表。");
                return new List<ScrewControllerDetail>();
            }

            _logger.Info("GetControllerDetails: 开始获取控制器详情。");

            var details = new List<ScrewControllerDetail>();
            var addedIPs = new HashSet<string>();

            // 从JSON配置获取
            if (settings.Controllers == null)
            {
                _logger.Warn("GetControllerDetails: settings.Controllers为null，跳过JSON配置部分。");
            }
            else
            {
                _logger.Info($"GetControllerDetails: 从JSON配置中加载 {settings.Controllers.Count} 个控制器。");
                foreach (var controller in settings.Controllers)
                {
                    if (controller == null)
                    {
                        _logger.Warn("遇到JSON配置中为null的控制器对象，跳过。");
                        continue;
                    }

                    if (string.IsNullOrEmpty(controller.IP))
                    {
                        _logger.Warn($"跳过IP为空的控制器：Name={controller.Name}");
                        continue;
                    }

                    if (addedIPs.Add(controller.IP))
                    {
                        details.Add(new ScrewControllerDetail
                        {
                            Name = controller.Name,
                            IP = controller.IP,
                            Source = "JSON配置",
                            TightenGuns = controller.TightenGuns
                        });
                        _logger.Info($"添加控制器: Name={controller.Name}, IP={controller.IP}, Source=JSON配置");
                    }
                    else
                    {
                        _logger.Warn($"重复IP地址，已跳过: IP={controller.IP}");
                    }
                }
            }

            // 从IP列表获取
            if (settings.ControllerIPs == null)
            {
                _logger.Warn("GetControllerDetails: settings.ControllerIPs为null，跳过IP列表部分。");
            }
            else
            {
                _logger.Info($"GetControllerDetails: 从IP列表中加载 {settings.ControllerIPs.Count} 个控制器。");
                for (int i = 0; i < settings.ControllerIPs.Count; i++)
                {
                    var ip = settings.ControllerIPs[i];
                    if (string.IsNullOrEmpty(ip))
                    {
                        _logger.Warn($"跳过IP列表中空IP，索引={i}");
                        continue;
                    }
                    if (addedIPs.Add(ip))
                    {
                        var detail = new ScrewControllerDetail
                        {
                            Name = $"控制器{i + 1}",
                            IP = ip,
                            Source = "IP列表"
                        };
                        details.Add(detail);
                        _logger.Info($"添加控制器: Name={detail.Name}, IP={ip}, Source=IP列表");
                    }
                    else
                    {
                        _logger.Warn($"重复IP地址，已跳过: IP={ip}");
                    }
                }
            }

            // 从单个IP获取
            if (!string.IsNullOrEmpty(settings.ControllerIP))
            {
                if (addedIPs.Add(settings.ControllerIP))
                {
                    var detail = new ScrewControllerDetail
                    {
                        Name = "主控制器",
                        IP = settings.ControllerIP,
                        Source = "单个IP"
                    };
                    details.Add(detail);
                    _logger.Info($"添加控制器: Name=主控制器, IP={settings.ControllerIP}, Source=单个IP");
                }
                else
                {
                    _logger.Warn($"重复IP地址，已跳过单个IP: IP={settings.ControllerIP}");
                }
            }
            else
            {
                _logger.Info("单个控制器IP为空，未添加主控制器。");
            }

            _logger.Info($"GetControllerDetails: 控制器详情加载完成，共添加 {details.Count} 条记录。");

            return details;
        }


        /// <summary>
        /// 清除配置缓存（配置变更时调用）
        /// </summary>
        public void ClearSettingsCache(string? vmName = null)
        {
            if (string.IsNullOrEmpty(vmName))
            {
                _settingsCache.Clear();
            }
            else
            {
                _settingsCache.TryRemove(vmName, out _);
            }
        }

        #endregion

        #region Cookie认证管理

        /// <summary>
        /// 登录并获取Cookie
        /// </summary>
        /// <param name="controllerIP">控制器IP</param>
        /// <returns>Cookie字符串</returns>
        public async Task<string?> LoginAndGetCookieAsync(string controllerIP)
        {
            if (string.IsNullOrEmpty(controllerIP))
            {
                _logger.Warn("登录失败：控制器IP不能为空");
                return null;
            }
            // 从缓存获取配置
            ScrewControllerSettings? settings = null;
            foreach (var kvp in _settingsCache)
            {
                if (GetAllControllerIPs(kvp.Value).Contains(controllerIP))
                {
                    settings = kvp.Value;
                    break;
                }
            }

            if (settings == null)
            {
                _logger.Warn($"登录失败：未找到控制器IP {controllerIP} 的配置");
                return null;
            }

            if (string.IsNullOrEmpty(settings.LoginUsername) || string.IsNullOrEmpty(settings.LoginPassword))
            {
                _logger.Warn($"登录失败：用户名或密码未配置 - IP: {controllerIP}");
                return null;
            }

            try
            {
                // 构建请求体
                var requestBody = BuildLoginRequestBody(settings);
                var url = $"http://{controllerIP}/rest/login/";

                // 创建请求
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // 添加必要的请求头


                // 发送请求
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Warn($"登录失败 - IP: {controllerIP}, 状态码: {response.StatusCode}");
                    return null;
                }

                // 从响应头中提取Cookie
                var cookie = ExtractCookiesFromResponse(response);

                if (string.IsNullOrEmpty(cookie))
                {
                    // 尝试从响应体中解析
                    var responseBody = await response.Content.ReadAsStringAsync();
                    cookie = ExtractCookieFromResponseBody(responseBody);
                }

                if (!string.IsNullOrEmpty(cookie))
                {
                    // 缓存Cookie
                    var cacheRecord = new CookieCacheRecord
                    {
                        ControllerIP = controllerIP,
                        Cookie = cookie,
                        CachedTime = DateTime.Now,
                        ExpirationTime = DateTime.Now.AddHours(DEFAULT_COOKIE_EXPIRATION_HOURS)
                    };
                    _cookieCache[controllerIP] = cacheRecord;

                    _logger.Info($"登录成功 - IP: {controllerIP}, Cookie已缓存");
                }
                else
                {
                    _logger.Warn($"登录响应中未找到有效Cookie - IP: {controllerIP}");
                }

                return cookie;
            }
            catch (Exception ex)
            {
                _logger.Error($"登录过程中发生错误 - IP: {controllerIP}", ex);
                return null;
            }
        }

        /// <summary>
        /// 构建登录请求体（Base64编码的JSON）
        /// </summary>
        private string BuildLoginRequestBody(ScrewControllerSettings settings)
        {
            try
            {
                var base64RequestBody = settings.base64RequestBody ?? throw new ArgumentException("base64RequestBody cannot be null or empty");

                if (string.IsNullOrEmpty(base64RequestBody))
                {
                    _logger.Error("Base64请求体为空或未设置");                
                }

                // 解码原始请求体
                var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(base64RequestBody));

                var jsonNode = JsonNode.Parse(decodedJson) ?? throw new InvalidOperationException("无法解析JSON数据");

                // 获取原始登录数据
                var loginDataNode = jsonNode["loginData"]?? throw new InvalidOperationException("JSON中缺少loginData字段");

                // 解码原始用户名密码（双重Base64）
                var originalUserName = loginDataNode["userName"]?.GetValue<string>() ?? throw new InvalidOperationException("userName字段为空");
                var originalPassword = loginDataNode["passWord"]?.GetValue<string>() ?? throw new InvalidOperationException("passWord字段为空");

                // 双重解码
                var decodedUserName = DecodeDoubleBase64(originalUserName);
                var decodedPassword = DecodeDoubleBase64(originalPassword);

                _logger.Info($"原始用户名: {decodedUserName}");

                // 检查是否需要更新
                if (decodedUserName == settings.LoginUsername && decodedPassword == settings.LoginPassword)
                {
                    _logger.Info("配置与原始请求体匹配，使用原始请求体");
                    return base64RequestBody;
                }

                _logger.Warn($"用户名密码不匹配，使用配置值覆盖。原始用户: {decodedUserName}，新用户: {settings.LoginUsername}");

                // 更新用户名密码（双重Base64编码）
                var newUserName = EncodeDoubleBase64(settings.LoginUsername);
                var newPassword = EncodeDoubleBase64(settings.LoginPassword);

                var loginDataObj = loginDataNode.AsObject();
                loginDataObj["userName"] = newUserName;
                loginDataObj["passWord"] = newPassword;

                // 更新 auditData 中的 auditUserName（如果存在）
                var auditDataNode = jsonNode["auditData"];
                if (auditDataNode != null)
                {
                    var paramsArray = auditDataNode["params"] as JsonArray;
                    if (paramsArray?.Count > 0)
                    {
                        var firstParam = paramsArray[0];
                        if (firstParam != null)
                        {
                            var paramObj = firstParam.AsObject();
                            paramObj["auditUserName"] = settings.LoginUsername;
                        }
                    }
                }

                // 转换为Base64
                var updatedJson = jsonNode.ToJsonString();
                var finalBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedJson));

                return finalBase64;
            }
            catch (FormatException ex)
            {
                _logger.Error($"Base64格式错误: {ex.Message}");
                throw new InvalidOperationException("请求体Base64格式无效", ex);
            }
            catch (JsonException ex)
            {
                _logger.Error($"JSON解析失败: {ex.Message}");
                throw new InvalidOperationException("无法解析请求体JSON结构", ex);
            }
            catch (Exception ex)
            {
                _logger.Error($"构建登录请求体失败: {ex.Message}", ex);
                throw;
            }
        }

        // 辅助方法：双重Base64解码
        private string DecodeDoubleBase64(string doubleEncodedString)
        {
            try
            {
                // 第一次解码
                var firstDecoded = Encoding.UTF8.GetString(Convert.FromBase64String(doubleEncodedString));

                // 第二次解码
                return firstDecoded;
            }
            catch (FormatException ex)
            {
                _logger.Error($"双重Base64解码失败: {doubleEncodedString} - {ex.Message}");
                throw;
            }
        }

        // 辅助方法：双重Base64编码
        private string EncodeDoubleBase64(string plainText)
        {
            // 第一次编码
            var firstEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
            return firstEncoded;
            // 第二次编码
            //return Convert.ToBase64String(Encoding.UTF8.GetBytes(firstEncoded));
        }

        /// <summary>
        /// 从响应头中提取Cookie
        /// </summary>
        private string? ExtractCookiesFromResponse(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Set-Cookie", out var cookieValues))
            {
                foreach (var cookieHeader in cookieValues)
                {
                    // 查找session相关的Cookie
                    if (cookieHeader.Contains("session="))
                    {
                        var sessionMatch = System.Text.RegularExpressions.Regex.Match(
                            cookieHeader,
                            @"session=([^;]+)"
                        );

                        if (sessionMatch.Success)
                        {
                            return $"session={sessionMatch.Groups[1].Value}";
                        }
                    }
                }

                // 如果没有找到session，返回所有Cookie
                var allCookies = cookieValues
                    .Select(c => c.Split(';').FirstOrDefault())
                    .Where(c => !string.IsNullOrEmpty(c));

                return string.Join("; ", allCookies);
            }

            return null;
        }

        /// <summary>
        /// 从响应体中提取Cookie
        /// </summary>
        private string? ExtractCookieFromResponseBody(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // 检查常见的认证响应字段
                if (root.TryGetProperty("session", out var sessionElement))
                {
                    return $"session={sessionElement.GetString()}";
                }

                if (root.TryGetProperty("token", out var tokenElement))
                {
                    return $"token={tokenElement.GetString()}";
                }

                if (root.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("session", out var dataSessionElement))
                {
                    return $"session={dataSessionElement.GetString()}";
                }
            }
            catch
            {
                // 尝试正则匹配
                if (responseBody.Contains("session="))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        responseBody,
                        @"session=([^;""'\s]+)"
                    );
                    if (match.Success)
                    {
                        return $"session={match.Groups[1].Value}";
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取有效的Cookie（自动登录刷新）
        /// </summary>
        /// <param name="controllerIP">控制器IP</param>
        /// <param name="forceRefresh">是否强制刷新</param>
        /// <returns>Cookie字符串</returns>
        public async Task<string?> GetValidCookieAsync(string controllerIP, bool forceRefresh = false)
        {
            if (string.IsNullOrEmpty(controllerIP))
            {
                return null;
            }

            // 检查缓存
            if (!forceRefresh && _cookieCache.TryGetValue(controllerIP, out var cached))
            {
                // 检查是否过期
                if (!cached.ExpirationTime.HasValue || cached.ExpirationTime > DateTime.Now)
                {
                    return cached.Cookie;
                }

                // Cookie已过期，移除缓存
                _cookieCache.TryRemove(controllerIP, out _);
                _logger.Info($"Cookie已过期，需要重新登录 - IP: {controllerIP}");
            }

            // 重新登录获取Cookie
            return await LoginAndGetCookieAsync(controllerIP);
        }

        /// <summary>
        /// 清除Cookie缓存
        /// </summary>
        /// <param name="controllerIP">指定控制器IP，为空则清除所有</param>
        public void ClearCookieCache(string? controllerIP = null)
        {
            if (string.IsNullOrEmpty(controllerIP))
            {
                _cookieCache.Clear();
                _logger.Info("所有Cookie缓存已清除");
            }
            else
            {
                _cookieCache.TryRemove(controllerIP, out _);
                _logger.Info($"Cookie缓存已清除 - IP: {controllerIP}");
            }
        }

        #endregion



        #region TR值获取

        /// <summary>
        /// 获取TR值（优先从缓存获取）
        /// </summary>
        public async Task<ScrewTrValueResult> GetTrValueAsync(string controllerIP, string? vmName = null)
        {
            var result = new ScrewTrValueResult { ControllerIP = controllerIP };

            if (string.IsNullOrEmpty(controllerIP))
            {
                result.Message = "控制器IP不能为空";
                return result;
            }

            try
            {
                // 检查内存缓存
                if (_trValueCache.TryGetValue(controllerIP, out var cached))
                {
                    if (!cached.ExpirationTime.HasValue || cached.ExpirationTime > DateTime.Now)
                    {
                        result.IsSuccess = true;
                        result.TrValue = cached.TrValue;
                        result.FromCache = true;
                        result.CachedTime = cached.CachedTime;
                        return result;
                    }
                    // 缓存过期，移除
                    _trValueCache.TryRemove(controllerIP, out _);
                }

                // 获取配置设置
                ScrewControllerSettings? settings = null;

                // 如果提供了vmName，直接使用
                if (!string.IsNullOrEmpty(vmName) && _settingsCache.TryGetValue(vmName, out settings))
                {
                    // 使用指定的vmName配置
                }
                else
                {
                    // 根据IP查找对应的配置
                    foreach (var kvp in _settingsCache)
                    {
                        if (GetAllControllerIPs(kvp.Value).Contains(controllerIP))
                        {
                            settings = kvp.Value;
                            break;
                        }
                    }
                }

                if (settings == null)
                {
                    result.Message = $"未找到控制器IP {controllerIP} 的配置";
                    return result;
                }

                // 根据有线枪和无线枪选择不同的请求方式
                if (settings.IsWired)
                {
                    // 有线枪请求
                    result = await CommonGetTrValueAsync(controllerIP);
                    if (!result.IsSuccess)
                    {
                        result = await SpecGetTrValueForWiredGunAsync(controllerIP);
                    }
                    
                }
                else
                {
                    // 无线枪请求
                    result = await CommonGetTrValueAsync(controllerIP);
                }

                // 如果获取成功，更新缓存
                if (result.IsSuccess)
                {
                    var cacheRecord = new TrValueCacheRecord
                    {
                        ControllerIP = controllerIP,
                        TrValue = result.TrValue,
                        CachedTime = DateTime.Now,
                        ExpirationTime = DateTime.Now.AddHours(settings.TrValueCacheExpirationHours)
                    };
                    _trValueCache[controllerIP] = cacheRecord;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Message = $"异常: {ex.Message}";
                _logger.Error($"获取TR值失败 - IP: {controllerIP}", ex);
                return result;
            }

        }

        /// <summary>
        /// 系统校准方式获取TR值
        /// </summary>
        private async Task<ScrewTrValueResult> SpecGetTrValueForWiredGunAsync(
            string controllerIP)
        {
            var result = new ScrewTrValueResult { ControllerIP = controllerIP };

            try
            {
                // 有线枪请求需要时间戳参数
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var url = $"http://{controllerIP}/rest/toolCalibration/";
                var body = $"?a={timestamp}";

                var response = await PostWithAuthAsync(controllerIP, url, body);
                if (response == null)
                {
                    result.Message = "请求控制器失败（可能是认证问题）";
                    return result;
                }

                _logger.Info($"有线枪TR值响应 - IP: {controllerIP}, Response: {response.Substring(0, Math.Min(500, response.Length))}");

                using var jsonDoc = JsonDocument.Parse(response);
                if (jsonDoc.RootElement.TryGetProperty("transducerRangeCurrValue", out var trElement))
                {
                    float trValue = 0f;
                    bool parsed = false;

                    switch (trElement.ValueKind)
                    {
                        case JsonValueKind.Number:
                            trValue = trElement.GetSingle();
                            parsed = true;
                            break;

                        case JsonValueKind.String:
                            var trString = trElement.GetString();
                            if (!string.IsNullOrEmpty(trString))
                            {
                                trString = trString.Replace(',', '.');
                                parsed = float.TryParse(trString,
                                    System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out trValue);
                            }
                            break;
                    }

                    if (parsed)
                    {
                        result.IsSuccess = true;
                        result.TrValue = trValue;
                        result.FromCache = false;
                        _logger.Info($"获取有线枪TR值成功 - IP: {controllerIP}, TR: {result.TrValue}", true);
                    }
                    else
                    {
                        result.Message = $"无法解析TR值: {trElement}";
                    }
                }
                else
                {
                    result.Message = "响应中未找到transducerRangeCurrValue字段";
                }
            }
            catch (Exception ex)
            {
                result.Message = $"获取有线枪TR值异常: {ex.Message}";
                _logger.Error($"获取有线枪TR值失败 - IP: {controllerIP}", ex);
            }

            return result;
        }

        /// <summary>
        /// 通用方法获取无线枪TR值
        /// </summary>
        private async Task<ScrewTrValueResult> CommonGetTrValueAsync(
            string controllerIP)
        {
            var result = new ScrewTrValueResult { ControllerIP = controllerIP };

            try
            {
                var url = $"http://{controllerIP}/rest/db/";
                var body = "/tool_details/toolData";

                var response = await PostWithAuthAsync(controllerIP, url, body);
                if (response == null)
                {
                    result.Message = "请求控制器失败";
                    return result;
                }

                using var jsonDoc = JsonDocument.Parse(response);
                if (jsonDoc.RootElement.TryGetProperty("toolUserTr", out var trElement))
                {
                    float trValue = 0f;
                    bool parsed = false;

                    switch (trElement.ValueKind)
                    {
                        case JsonValueKind.Number:
                            trValue = trElement.GetSingle();
                            parsed = true;
                            break;

                        case JsonValueKind.String:
                            var trString = trElement.GetString();
                            if (!string.IsNullOrEmpty(trString))
                            {
                                // 处理可能的本地化数字格式（如 "1,5" vs "1.5"）
                                trString = trString.Replace(',', '.');
                                parsed = float.TryParse(trString,
                                    System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out trValue);
                            }
                            break;

                        default:
                            result.Message = $"toolUserTr字段类型不支持: {trElement.ValueKind}";
                            return result;
                    }

                    if (parsed)
                    {
                        result.IsSuccess = true;
                        result.TrValue = trValue;
                        result.FromCache = false;
                        _logger.Info($"获取无线枪TR值成功 - IP: {controllerIP}, TR: {result.TrValue}", true);
                    }
                    else
                    {
                        _logger.Warn($"响应中未找到toolUserTr字段 - IP: {controllerIP}, Response: {response.Substring(0, Math.Min(500, response.Length))}");
                        result.Message = $"无法解析TR值: {trElement}";
                    }
                }
                else
                {
                    result.Message = "响应中未找到toolUserTr字段";
                }
            }
            catch (JsonException ex)
            {
                result.Message = $"JSON解析失败: {ex.Message}";
                _logger.Error($"获取无线枪TR值JSON解析失败 - IP: {controllerIP}", ex);
            }
            catch (Exception ex)
            {
                result.Message = $"获取螺丝枪TR值异常: {ex.Message}";
                _logger.Error($"获取螺丝枪TR值失败 - IP: {controllerIP}", ex);
            }

            return result;
        }


        public async Task<BatchTrValueResult> GetBatchTrValuesAsync(
           ScrewControllerSettings settings,
           IEnumerable<string> tightenGuns)
        {
            var result = new BatchTrValueResult();

            if (settings == null)
            {
                result.Message = "控制器配置不能为空";
                return result;
            }

            if (tightenGuns == null || !tightenGuns.Any())
            {
                result.Message = "螺丝枪列表不能为空";
                return result;
            }

            try
            {
                // 按控制器IP分组螺丝枪，每个控制器只请求一次
                var gunsByController = tightenGuns
                    .Where(g => !string.IsNullOrEmpty(g))
                    .GroupBy(g => GetControllerIPByTightenGun(settings, g) ?? string.Empty)
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .ToDictionary(g => g.Key, g => g.ToList());

                if (gunsByController.Count == 0)
                {
                    result.Message = "无法找到有效的控制器IP映射";
                    return result;
                }

                // 并行获取每个控制器的TR值
                var tasks = gunsByController.Select(async kvp =>
                {
                    var controllerIP = kvp.Key;
                    var guns = kvp.Value;
                    var trResult = await GetTrValueAsync(controllerIP);
                    return (ControllerIP: controllerIP, Guns: guns, TrResult: trResult);
                });

                var results = await Task.WhenAll(tasks);

                // 构建结果映射
                int successCount = 0;
                foreach (var (controllerIP, guns, trResult) in results)
                {
                    if (trResult.IsSuccess) successCount++;

                    foreach (var gun in guns)
                    {
                        result.TrValues[gun] = new ScrewTrValueResult
                        {
                            ControllerIP = controllerIP,
                            TightenGun = gun,
                            IsSuccess = trResult.IsSuccess,
                            Message = trResult.Message,
                            TrValue = trResult.TrValue,
                            FromCache = trResult.FromCache,
                            CachedTime = trResult.CachedTime
                        };
                    }
                }

                result.IsSuccess = successCount > 0;
                result.Message = $"成功获取 {successCount}/{gunsByController.Count} 个控制器的TR值";

                _logger.Info($"批量获取TR值完成 - 螺丝枪数: {tightenGuns.Count()}, " +
                            $"控制器数: {gunsByController.Count}, 成功: {successCount}");
            }
            catch (Exception ex)
            {
                result.Message = $"批量获取TR值异常: {ex.Message}";
                _logger.Error("批量获取TR值失败", ex);
            }

            return result;
        }

        /// <summary>
        /// 批量获取所有控制器的TR值
        /// </summary>
        public async Task<List<ScrewTrValueResult>> GetAllTrValuesAsync(ScrewControllerSettings settings)
        {
            var ipList = GetAllControllerIPs(settings);

            // 并行获取
            var tasks = ipList.Select(async ip =>
            {
                var result = await GetTrValueAsync(ip);
                result.ControllerIP = ip;
                return result;
            });

            return (await Task.WhenAll(tasks)).ToList();
        }

        public void ClearTrCache()
        {
            _trValueCache.Clear();
            _logger.Info("TR值内存缓存已清除");
        }

        #endregion

        #region TR值数据库缓存

        /// <inheritdoc/>
        public async Task<bool> SaveTrValueToDbAsync(
            string vmName, string controllerIP, string tightenGun, float trValue)
        {
            if (string.IsNullOrEmpty(vmName) || string.IsNullOrEmpty(controllerIP))
            {
                _logger.Warn("保存TR值到数据库失败：参数无效");
                return false;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                var cacheKey = BuildTrCacheKey(vmName, controllerIP, tightenGun);
                var cacheRecord = new TrValueCacheRecord
                {
                    ControllerIP = controllerIP,
                    TightenGun = tightenGun ?? string.Empty,
                    TrValue = trValue,
                    CachedTime = DateTime.Now
                };

                var cacheData = JsonSerializer.Serialize(cacheRecord);

                // 保存到数据库
                var existing = await projectSettingRepo.GetByNameAndVMAsync(cacheKey, vmName);

                if (existing != null)
                {
                    // 更新现有记录
                    existing.Value = cacheData;
                    existing.Type = "String";
                    await projectSettingRepo.UpdateAsync(existing);
                }
                else
                {
                    // 添加新记录
                    var newRecord = new ProjectSettingModel
                    {
                        Name = cacheKey,
                        Value = cacheData,
                        Type = "String",
                        BelongToVM = vmName,
                        Remark = $"TR值缓存 - {controllerIP}"
                    };
                    await projectSettingRepo.AddAsync(newRecord);
                }


                _logger.Info($"TR值已保存到数据库 - VM: {vmName}, IP: {controllerIP}, " +
                     $"Gun: {tightenGun ?? "N/A"}, TR: {trValue}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"保存TR值到数据库失败 - VM: {vmName}, IP: {controllerIP}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, TrValueCacheRecord>> LoadTrValuesFromDbAsync(string vmName)
        {
            var result = new Dictionary<string, TrValueCacheRecord>();

            if (string.IsNullOrEmpty(vmName))
            {
                return result;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                // 获取该VM的所有配置，然后在内存中筛选
                var allSettings = await projectSettingRepo.GetByVMNameAsync(vmName);

                if (allSettings == null || allSettings.Count == 0)
                {
                    return result;
                }

                // 筛选TR缓存记录（以前缀开头）
                var cachePrefix = $"{TR_CACHE_PREFIX}{vmName}_";
                var cacheRecords = allSettings
                    .Where(s => s.Name?.StartsWith(cachePrefix) == true)
                    .ToList();

                foreach (var record in cacheRecords)
                {
                    if (string.IsNullOrEmpty(record?.Value)) continue;

                    try
                    {
                        var cacheData = JsonSerializer.Deserialize<TrValueCacheRecord>(record.Value);
                        if (cacheData != null && !string.IsNullOrEmpty(cacheData.ControllerIP))
                        {
                            result[cacheData.ControllerIP] = cacheData;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.Warn($"解析TR值缓存记录失败: {ex.Message}, Key: {record.Name}");
                    }
                }

                _logger.Info($"从数据库加载TR值缓存 - VM: {vmName}, 记录数: {result.Count}");
            }
            catch (Exception ex)
            {
                _logger.Error($"从数据库加载TR值缓存失败 - VM: {vmName}", ex);
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task<bool> ClearTrValueDbCacheAsync(string vmName, string? controllerIP = null)
        {
            if (string.IsNullOrEmpty(vmName))
            {
                return false;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                // 获取该VM的所有配置
                var allSettings = await projectSettingRepo.GetByVMNameAsync(vmName);

                if (allSettings == null || allSettings.Count == 0)
                {
                    _logger.Info($"无需清除TR值缓存 - VM: {vmName}（无缓存记录）");
                    return true;
                }

                // 确定要删除的记录
                IEnumerable<ProjectSettingModel> recordsToDelete;

                if (string.IsNullOrEmpty(controllerIP))
                {
                    // 清除该VM的所有TR值缓存
                    var cachePrefix = $"{TR_CACHE_PREFIX}{vmName}_";
                    recordsToDelete = allSettings.Where(s => s.Name?.StartsWith(cachePrefix) == true);

                    // 同时清除内存缓存
                    _trValueCache.Clear();
                }
                else
                {
                    // 清除指定控制器的缓存
                    var specificPrefix = $"{TR_CACHE_PREFIX}{vmName}_{controllerIP}_";
                    recordsToDelete = allSettings.Where(s => s.Name?.StartsWith(specificPrefix) == true);

                    // 同时清除内存缓存
                    _trValueCache.TryRemove(controllerIP, out _);
                }
                // 批量删除
                var deleteList = recordsToDelete.ToList();
                if (deleteList.Count > 0)
                {
                    foreach (var record in deleteList)
                    {
                        await projectSettingRepo.DeleteAsync(record);
                    }

                    _logger.Info($"已清除数据库TR值缓存 - VM: {vmName}, " +
                                $"IP: {controllerIP ?? "全部"}, 删除数量: {deleteList.Count}");
                }
                else
                {
                    _logger.Info($"无需清除TR值缓存 - VM: {vmName}, IP: {controllerIP ?? "全部"}（无匹配记录）");
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"清除数据库TR值缓存失败 - VM: {vmName}", ex);
                return false;
            }
        }

        /// <summary>
        /// 从数据库加载TR值到内存缓存
        /// </summary>
        private async Task LoadTrValuesFromDbToMemoryAsync(string vmName, ScrewControllerSettings settings)
        {
            try
            {
                var dbCache = await LoadTrValuesFromDbAsync(vmName);
                var expirationHours = settings.TrValueCacheExpirationHours;
                int loadedCount = 0;

                foreach (var (ip, record) in dbCache)
                {
                    // 检查是否过期
                    if (expirationHours > 0)
                    {
                        var expirationTime = record.CachedTime.AddHours(expirationHours);
                        if (DateTime.Now > expirationTime)
                        {
                            _logger.Info($"TR值缓存已过期，跳过 - IP: {ip}, 缓存时间: {record.CachedTime}");
                            continue;
                        }
                        record.ExpirationTime = expirationTime;
                    }

                    _trValueCache[ip] = record;
                    loadedCount++;
                }

                if (loadedCount > 0)
                {
                    _logger.Info($"已加载 {loadedCount} 条TR值缓存到内存 - VM: {vmName}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"加载TR值到内存失败 - VM: {vmName}", ex);
            }
        }

        private static string BuildTrCacheKey(string vmName, string controllerIP, string? tightenGun)
        {
            var gun = string.IsNullOrEmpty(tightenGun) ? "default" : tightenGun;
            return $"{TR_CACHE_PREFIX}{vmName}_{controllerIP}_{gun}";
        }

        #endregion

        #region 周期匹配
        /// <summary>
        /// 根据EorCycleCount获取EorProcessCycleNumber（作为TraceLogId）
        /// </summary>
        /// <param name="controllerIP">控制器IP</param>
        /// <param name="eorCycleCount">周期计数（对应workCycleId）</param>
        /// <returns>包含eorProcessCycleNumber的结果</returns>
        public async Task<ScrewProcessCycleResult> GetProcessCycleNumberByCycleCountAsync(
            string controllerIP, int eorCycleCount)
        {
            var result = new ScrewProcessCycleResult();

            if (string.IsNullOrEmpty(controllerIP))
            {
                result.Message = "控制器IP不能为空";
                return result;
            }

            if (eorCycleCount <= 0)
            {
                result.Message = "eorCycleCount必须大于0";
                return result;
            }

            try
            {
                // 使用ID排序视图查询
                var url = $"http://{controllerIP}/rest/db/";
                var body = $"/eor_logs/_design/eorDesignIdSorted/_view/eorViewIdSorted?" +
                           $"skip=0&limit=100&descending=true&include_docs=true";

                var response = await PostWithAuthAsync(controllerIP, url, body);
                if (response == null)
                {
                    result.Message = "请求控制器失败";
                    return result;
                }

                using var jsonDoc = JsonDocument.Parse(response);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("rows", out var rows) ||
                    rows.ValueKind != JsonValueKind.Array)
                {
                    result.Message = "响应格式错误：未找到rows数组";
                    return result;
                }

                // 遍历查找匹配的eorCycleCount
                foreach (var row in rows.EnumerateArray())
                {
                    if (!row.TryGetProperty("doc", out var doc))
                        continue;

                    // 获取eorCycleCount
                    if (!doc.TryGetProperty("eorCycleCount", out var cycleCountElement))
                        continue;

                    var docCycleCount = cycleCountElement.GetInt32();

                    if (docCycleCount == eorCycleCount)
                    {
                        // 找到匹配记录，获取eorProcessCycleNumber
                        if (doc.TryGetProperty("eorProcessCycleNumber", out var processCycleElement))
                        {
                            result.ProcessCycleNumber = processCycleElement.GetInt32();
                            result.Id = result.ProcessCycleNumber; // 使用ProcessCycleNumber作为TraceLogId
                        }

                        // 获取其他信息
                        if (doc.TryGetProperty("eorDocId", out var docIdElement))
                        {
                            result.DocId = docIdElement.GetInt32();
                        }

                        if (doc.TryGetProperty("eorMeasuredTorque", out var torqueElement))
                        {
                            result.TorqueResult = torqueElement.GetDouble();
                        }

                        if (doc.TryGetProperty("eorMeasuredAngle", out var angleElement))
                        {
                            result.AngleResult = angleElement.GetDouble();
                        }

                        if (doc.TryGetProperty("eorTimeStamp", out var timestampElement))
                        {
                            var timestamp = (long)timestampElement.GetDouble();
                            result.TimeStamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
                        }

                        if (doc.TryGetProperty("eorCyclePassFail", out var passFailElement))
                        {
                            result.IsPass = passFailElement.GetInt32() == 1;
                        }

                        result.IsSuccess = true;
                        _logger.Info($"成功获取ProcessCycleNumber - CycleCount: {eorCycleCount}, " +
                                    $"ProcessCycleNumber: {result.ProcessCycleNumber}");
                        return result;
                    }
                }

                // 如果在最近100条记录中未找到，尝试使用全文搜索
                result = await SearchProcessCycleByFullTextAsync(controllerIP, eorCycleCount);
            }
            catch (JsonException ex)
            {
                result.Message = $"JSON解析失败: {ex.Message}";
                _logger.Error($"获取ProcessCycleNumber JSON解析失败", ex);
            }
            catch (Exception ex)
            {
                result.Message = $"异常: {ex.Message}";
                _logger.Error($"获取ProcessCycleNumber失败 - CycleCount: {eorCycleCount}", ex);
            }

            return result;
        }

        /// <summary>
        /// 使用全文搜索查找ProcessCycleNumber
        /// </summary>
        private async Task<ScrewProcessCycleResult> SearchProcessCycleByFullTextAsync(
            string controllerIP, int eorCycleCount)
        {
            var result = new ScrewProcessCycleResult();

            try
            {
                var url = $"http://{controllerIP}/rest/db/";
                var body = $"/eor_logs/_fti/_design/eorDesignLuceneIndex/eorFullTextLuceneIndex?" +
                           $"sort=\\\\eorDocId<int>&q=eorCycleCount<int>:{eorCycleCount}&limit=10&skip=0";

                var response = await PostWithAuthAsync(controllerIP, url, body);
                if (response == null)
                {
                    result.Message = "全文搜索请求失败";
                    return result;
                }

                using var jsonDoc = JsonDocument.Parse(response);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("total_rows", out var totalRowsElement) ||
                    totalRowsElement.GetInt32() <= 0)
                {
                    result.Message = $"未找到CycleCount为{eorCycleCount}的记录";
                    return result;
                }

                if (!root.TryGetProperty("rows", out var rows) ||
                    rows.ValueKind != JsonValueKind.Array ||
                    rows.GetArrayLength() == 0)
                {
                    result.Message = "响应中无有效数据";
                    return result;
                }

                var firstRow = rows[0];

                if (firstRow.TryGetProperty("fields", out var fields))
                {
                    if (fields.TryGetProperty("eorProcessCycleNumber", out var processCycleElement))
                    {
                        result.ProcessCycleNumber = processCycleElement.GetInt32();
                        result.Id = result.ProcessCycleNumber;
                    }

                    if (fields.TryGetProperty("eorTorqueResult", out var torqueElement))
                    {
                        result.TorqueResult = torqueElement.GetDouble();
                    }

                    if (fields.TryGetProperty("eorAngleResult", out var angleElement))
                    {
                        result.AngleResult = angleElement.GetDouble();
                    }

                    if (fields.TryGetProperty("eorTimeStamp", out var timestampElement))
                    {
                        var timestamp = timestampElement.GetInt64();
                        result.TimeStamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
                    }
                }

                if (firstRow.TryGetProperty("id", out var idElement))
                {
                    if (int.TryParse(idElement.GetString(), out var docId))
                    {
                        result.DocId = docId;
                    }
                }

                if (result.ProcessCycleNumber > 0)
                {
                    result.IsSuccess = true;
                    _logger.Info($"全文搜索成功 - CycleCount: {eorCycleCount}, " +
                                $"ProcessCycleNumber: {result.ProcessCycleNumber}");
                }
                else
                {
                    result.Message = "未能解析ProcessCycleNumber";
                }
            }
            catch (Exception ex)
            {
                result.Message = $"全文搜索异常: {ex.Message}";
                _logger.Error($"全文搜索ProcessCycleNumber失败", ex);
            }

            return result;
        }

        #endregion


        #region 曲线数据获取

        /// <summary>
        /// 获取曲线数据
        /// </summary>
        public async Task<ScrewCurveDataResult> GetCurveDataAsync(string controllerIP, int traceLogId, float trValue, string? vmName = null)
        {
            var result = new ScrewCurveDataResult { TraceLogId = traceLogId };

            if (string.IsNullOrEmpty(controllerIP))
            {
                result.Message = "控制器IP不能为空";
                return result;
            }

            if (traceLogId <= 0)
            {
                result.Message = "TraceLogId必须大于0";
                return result;
            }

            // 获取配置设置
            ScrewControllerSettings? settings = null;

            if (!string.IsNullOrEmpty(vmName))
            {
                _settingsCache.TryGetValue(vmName, out settings);
            }

            if (settings == null)
            {
                // 根据IP查找对应的配置
                foreach (var kvp in _settingsCache)
                {
                    if (GetAllControllerIPs(kvp.Value).Contains(controllerIP))
                    {
                        settings = kvp.Value;
                        break;
                    }
                }
            }

            if (settings == null)
            {
                result.Message = $"未找到控制器IP {controllerIP} 的配置";
                return result;
            }

            try
            {
                if (settings.IsWired)
                {
                    // 有线枪曲线数据获取
                    result = await GetCurveDataForWiredGunAsync(controllerIP, traceLogId, trValue, settings);
                }
                else
                {
                    // 无线枪曲线数据获取
                    result = await GetCurveDataForWirelessGunAsync(controllerIP, traceLogId, trValue, settings);
                }
            }
            catch (JsonException ex)
            {
                result.Message = $"JSON解析失败: {ex.Message}";
                _logger.Error($"获取曲线数据JSON解析失败 - ID: {traceLogId}", ex);
            }
            catch (Exception ex)
            {
                result.Message = $"异常: {ex.Message}";
                _logger.Error($"获取曲线数据失败 - ID: {traceLogId}", ex);
            }

            return result;
        }

        /// <summary>
        /// 获取有线枪曲线数据
        /// </summary>
        private async Task<ScrewCurveDataResult> GetCurveDataForWiredGunAsync(
            string controllerIP, int traceLogId, float trValue, ScrewControllerSettings settings)
        {
            var result = new ScrewCurveDataResult { TraceLogId = traceLogId };

            var url = $"http://{controllerIP}{settings.WiredCurveRequestPath}";  // 有线枪http://192.168.250.93/rest/db/
            var body = $"/trace_logs/{traceLogId}/"; // /trace_logs/883/

            //_logger.Info($"请求有线枪曲线数据 - URL: {url}, Body: {body}",true);

            var response = await PostWithAuthAsync(controllerIP, url, body);
            if (response == null)
            {
                result.Message = "请求曲线数据失败";
                return result;
            }

            // 解析有线枪曲线数据格式
            return ParseCurveDataFromResponse(response, trValue);
        }

        /// <summary>
        /// 获取无线枪曲线数据
        /// </summary>
        private async Task<ScrewCurveDataResult> GetCurveDataForWirelessGunAsync(
            string controllerIP, int traceLogId, float trValue, ScrewControllerSettings settings)
        {
            var result = new ScrewCurveDataResult { TraceLogId = traceLogId };

            var url = $"http://{controllerIP}{settings.WirelessCurveRequestPath}";
            var body = $"/trace_logs/{traceLogId}/";

            _logger.Info($"请求无线枪曲线数据 - URL: {url}, Body: {body}");

            var response = await PostWithAuthAsync(controllerIP, url, body);
            if (response == null)
            {
                result.Message = "请求曲线数据失败";
                return result;
            }

            // 解析无线枪曲线数据格式
            return ParseCurveDataFromResponse(response, trValue);
        }

        /// <summary>
        /// 从响应中解析曲线数据
        /// </summary>
        private ScrewCurveDataResult ParseCurveDataFromResponse(string response, float trValue)
        {
            var result = new ScrewCurveDataResult();

            if (string.IsNullOrWhiteSpace(response))
            {
                result.Message = "响应内容为空";
                return result;
            }

            try
            {
                // 记录原始响应（截取前500字符用于调试）
                var logResponse = response.Length > 500 ? response.Substring(0, 500) + "..." : response;
                _logger.Info($"解析曲线响应: {logResponse}");

                using var jsonDoc = JsonDocument.Parse(response);
                var root = jsonDoc.RootElement;

                // 尝试多种路径查找 traceDataPt
                JsonElement traceDataPt;

                // 路径1: 直接在根节点
                if (root.TryGetProperty("traceDataPt", out traceDataPt))
                {
                    _logger.Info("在根节点找到 traceDataPt");
                }
                // 路径2: 在 data 字段下
                else if (root.TryGetProperty("data", out var dataElement) &&
                         dataElement.TryGetProperty("traceDataPt", out traceDataPt))
                {
                    _logger.Info("在 data 节点找到 traceDataPt");
                }
                // 路径3: 在 result 字段下
                else if (root.TryGetProperty("result", out var resultElement) &&
                         resultElement.TryGetProperty("traceDataPt", out traceDataPt))
                {
                    _logger.Info("在 result 节点找到 traceDataPt");
                }
                // 路径4: 在 rows 数组的第一个元素中
                else if (root.TryGetProperty("rows", out var rowsElement) &&
                         rowsElement.ValueKind == JsonValueKind.Array &&
                         rowsElement.GetArrayLength() > 0)
                {
                    var firstRow = rowsElement[0];
                    if (firstRow.TryGetProperty("doc", out var docElement) &&
                        docElement.TryGetProperty("traceDataPt", out traceDataPt))
                    {
                        _logger.Info("在 rows[0].doc 节点找到 traceDataPt");
                    }
                    else if (firstRow.TryGetProperty("traceDataPt", out traceDataPt))
                    {
                        _logger.Info("在 rows[0] 节点找到 traceDataPt");
                    }
                    else
                    {
                        // 记录实际的JSON结构用于调试
                        LogJsonStructure(root);
                        result.Message = "响应中未找到 traceDataPt 字段（已检查多种路径）";
                        return result;
                    }
                }
                else
                {
                    // 记录实际的JSON结构用于调试
                    LogJsonStructure(root);
                    result.Message = "响应中未找到 traceDataPt 字段";
                    return result;
                }

                // 验证 traceDataPt 是数组
                if (traceDataPt.ValueKind != JsonValueKind.Array)
                {
                    result.Message = $"traceDataPt 不是数组类型，实际类型: {traceDataPt.ValueKind}";
                    return result;
                }

                // 解析曲线数据点
                int validPoints = 0;
                int invalidPoints = 0;

                foreach (var point in traceDataPt.EnumerateArray())
                {
                    try
                    {
                        // 获取角度值
                        double angle = 0;
                        if (point.TryGetProperty("angle", out var angleElement))
                        {
                            angle = GetDoubleValue(angleElement);
                        }
                        else
                        {
                            invalidPoints++;
                            continue;
                        }

                        // 获取 currentAndTorque 值
                        double currentAndTorque = 0;
                        if (point.TryGetProperty("currentAndTorque", out var torqueElement))
                        {
                            currentAndTorque = GetDoubleValue(torqueElement);
                        }
                        else
                        {
                            invalidPoints++;
                            continue;
                        }

                        // 计算扭矩和电流
                        var (torque, current) = CalculateTorqueAndCurrent(currentAndTorque, trValue);

                        result.DataPoints.Add(new CurveDataPoint
                        {
                            Angle = angle,
                            Torque = torque,
                            Current = current,
                            RawValue = currentAndTorque
                        });

                        validPoints++;
                    }
                    catch (Exception ex)
                    {
                        invalidPoints++;
                        _logger.Warn($"解析数据点异常: {ex.Message}");
                    }
                }

                if (validPoints > 0)
                {
                    result.IsSuccess = true;
                    _logger.Info($"曲线数据解析成功 - 有效点: {validPoints}, 无效点: {invalidPoints}");
                }
                else
                {
                    result.Message = $"未能解析任何有效数据点（总点数: {traceDataPt.GetArrayLength()}, 无效: {invalidPoints}）";
                }
            }
            catch (JsonException ex)
            {
                result.Message = $"JSON解析失败: {ex.Message}";
                _logger.Error($"JSON解析异常: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                result.Message = $"解析异常: {ex.Message}";
                _logger.Error($"解析曲线数据异常", ex);
            }

            return result;
        }

        /// <summary>
        /// 记录JSON结构
        /// </summary>
        private void LogJsonStructure(JsonElement element, string prefix = "")
        {
            try
            {
                var properties = new List<string>();

                if (element.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in element.EnumerateObject())
                    {
                        var valueType = prop.Value.ValueKind.ToString();
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            valueType = $"Array[{prop.Value.GetArrayLength()}]";
                        }
                        properties.Add($"{prop.Name}({valueType})");
                    }
                }

                _logger.Info($"{prefix}JSON结构: {{ {string.Join(", ", properties)} }}",true);
            }
            catch (Exception ex)
            {
                _logger.Warn($"记录JSON结构失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全获取Double值（兼容多种类型）
        /// </summary>
        private double GetDoubleValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.String => double.TryParse(element.GetString(), out var val) ? val : 0,
                _ => 0
            };
        }

        ///
        /// 获取无线枪曲线数据
        /// 请求URL： http://{controllerIP}/rest/getCycleTraceData/
        /// 请求方法：POST
        /// 请求负载：$"fld_controller_cycle_number:{traceLogId}"

        /// <summary>
        /// 根据EorCycleCount获取作业信息
        /// </summary>
        public async Task<ScrewProcessCycleResult> GetProcessCycleInfoAsync(
            string controllerIP, int eorCycleCount)
        {

            var result = new ScrewProcessCycleResult();

            if (string.IsNullOrEmpty(controllerIP))
            {
                result.Message = "控制器IP不能为空";
                return result;
            }

            try
            {
                var url = $"http://{controllerIP}/rest/db/";

                // 尝试不同高位值（处理eorCycleCount高位缺失问题）
                for (int highBit = 0; highBit <= 9; highBit++)
                {
                    var fullCycleCount = eorCycleCount + highBit * 1000000;
                    var body = $"/eor_logs/_fti/_design/eorDesignLuceneIndex/eorFullTextLuceneIndex?" +
                              $"sort=\\eorDocId<int>&q=eorCycleCount<int>:{fullCycleCount}&limit=10&skip=0";

                    // 使用带认证的POST请求
                    var response = await PostWithAuthAsync(controllerIP, url, body);
                    if (response == null) continue;

                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(response);

                        if (!jsonDoc.RootElement.TryGetProperty("total_rows", out var totalRowsElement))
                            continue;

                        var totalRows = totalRowsElement.GetInt32();
                        if (totalRows <= 0) continue;

                        if (!jsonDoc.RootElement.TryGetProperty("rows", out var rows))
                            continue;

                        var firstRow = rows.EnumerateArray().FirstOrDefault();
                        if (firstRow.ValueKind == JsonValueKind.Undefined)
                            continue;

                        result.Id = firstRow.GetProperty("id").GetInt32();
                        var fields = firstRow.GetProperty("fields");

                        result.ProcessCycleNumber = fields.GetProperty("eorProcessCycleNumber").GetInt32();
                        result.TorqueResult = fields.GetProperty("eorTorqueResult").GetDouble();
                        result.AngleResult = fields.GetProperty("eorAngleResult").GetDouble();

                        var timestamp = fields.GetProperty("eorTimeStamp").GetInt64();
                        result.TimeStamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;

                        result.IsSuccess = true;
                        _logger.Info($"获取作业信息成功 - CycleCount: {fullCycleCount}, ID: {result.Id}");
                        return result;
                    }
                    catch (JsonException)
                    {
                        // 继续尝试下一个高位值
                        continue;
                    }
                }

                result.Message = $"未找到CycleCount为{eorCycleCount}的作业信息";
            }
            catch (Exception ex)
            {
                result.Message = $"异常: {ex.Message}";
                _logger.Error($"获取作业信息失败 - CycleCount: {eorCycleCount}", ex);
            }

            return result;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// POST请求
        /// </summary>
        private async Task<string?> PostAsync(string url, string body)
        {
            try
            {
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }

                _logger.Warn($"POST请求失败 - URL: {url}, Status: {response.StatusCode}");
                return null;
            }
            catch (TaskCanceledException)
            {
                _logger.Warn($"POST请求超时 - URL: {url}");
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.Warn($"POST请求网络错误 - URL: {url}, Error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error($"POST请求异常 - URL: {url}", ex);
                return null;
            }
        }

        /// <summary>
        /// POST请求（带Cookie认证）
        /// </summary>
        private async Task<string?> PostWithAuthAsync(string controllerIP, string url, string body)
        {
            try
            {
                // 获取有效Cookie
                var cookie = await GetValidCookieAsync(controllerIP);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                // 添加Cookie
                if (!string.IsNullOrEmpty(cookie))
                {
                    request.Headers.Add("Cookie", cookie);
                }

                // 添加其他必要的请求头
                request.Headers.Add("Accept", "application/json, text/plain, */*");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                request.Headers.Add("Referer", $"http://{controllerIP}/");

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }

                // 如果返回401或403，尝试重新登录
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.Warn($"认证失败，尝试重新登录 - IP: {controllerIP}");

                    // 清除旧Cookie并重新登录
                    _cookieCache.TryRemove(controllerIP, out _);
                    cookie = await LoginAndGetCookieAsync(controllerIP);

                    if (!string.IsNullOrEmpty(cookie))
                    {
                        // 重试请求
                        using var retryRequest = new HttpRequestMessage(HttpMethod.Post, url);
                        retryRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
                        retryRequest.Headers.Add("Cookie", cookie);
                        retryRequest.Headers.Add("Accept", "application/json, text/plain, */*");
                        retryRequest.Headers.Add("Referer", $"http://{controllerIP}/");

                        var retryResponse = await _httpClient.SendAsync(retryRequest);
                        if (retryResponse.IsSuccessStatusCode)
                        {
                            return await retryResponse.Content.ReadAsStringAsync();
                        }
                    }
                }

                _logger.Warn($"POST请求失败 - URL: {url}, Status: {response.StatusCode}");
                return null;
            }
            catch (TaskCanceledException)
            {
                _logger.Warn($"POST请求超时 - URL: {url}");
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.Warn($"POST请求网络错误 - URL: {url}, Error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error($"POST请求异常 - URL: {url}", ex);
                return null;
            }
        }

        /// <summary>
        /// 计算扭矩和电流值
        /// 公式来源：VM_PageScrewTraceClosed.GetTraceResult
        /// </summary>
        private (double torque, double current) CalculateTorqueAndCurrent(
            double currentAndTorqueData, float trValue)
        {
            // 位与运算分离扭矩和电流数据
            var torqData = (int)((long)currentAndTorqueData & 0x0000ffff);
            var currentData = (int)((long)currentAndTorqueData & 0xffff0000);

            // 扭矩符号位处理
            if ((torqData & 0x8000) != 0)
            {
                torqData = (int)(torqData | 0xffff0000);
            }

            // 扭力值 = torqData × 扭矩转换常量 × tr值
            var torque = torqData * TorqueConversionConst * trValue;

            // 电流数据右移16位
            currentData >>= 16;
            if ((currentData & 0x8000) != 0)
            {
                currentData = (int)(currentData | 0xffff0000);
            }

            // 电流值
            var current = currentData * CurrentConversionConst;

            return (torque, current);
        }

        #endregion

        #region IDisposable

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
                    _trValueCache.Clear();
                    _settingsCache.Clear();
                    _cookieCache.Clear();
                }
                _disposed = true;
            }
        }

        #endregion
    }


}
