using NLog;
using NLog.Config;
using NLog.Targets;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using NLog.Common;
using NLog.Targets.Wrappers;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Logging.Targets
{
    /// <summary>
    /// 数据库日志目标类型
    /// </summary>
    public enum DatabaseLogType
    {
        AppLog,
        MesInterfaceLog,
        WebInterfaceLog
    }

    /// <summary>
    /// NLog数据库日志目标
    /// </summary>
    [Target("DatabaseLog")]
    public sealed class DatabaseLogTarget : TargetWithLayout
    {
        private static IServiceProvider? _serviceProvider;
        private static readonly object _initLock = new object();
        private readonly ConcurrentQueue<object> _logQueue = new();
        private Timer? _flushTimer;
        private readonly object _flushLock = new();
        private const int BatchSize = 10;
        private const int FlushIntervalSeconds = 2;
        private bool _isInitialized = false;

        /// <summary>
        /// 连接字符串属性 - 修复NLog配置错误
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// 日志类型
        /// </summary>
        [DefaultValue("AppLog")]
        public string LogType { get; set; } = "AppLog";

        public DatabaseLogTarget()
        {
            // 设置默认布局
            Layout = "${longdate} | ${level:uppercase=true} | ${logger} | ${Message} ${exception:format=tostring}";
        }

        /// <summary>
        /// 初始化服务提供者（静态方法，应用启动时调用一次）
        /// </summary>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            lock (_initLock)
            {
                _serviceProvider = serviceProvider;
                InternalLogger.Info($"DatabaseLogTarget: ServiceProvider initialized at {DateTime.Now:HH:mm:ss.fff}");

                // 找到所有已注册的 DatabaseLogTarget 实例并重新初始化
                var config = LogManager.Configuration;
                if (config != null)
                {
                    foreach (var target in config.AllTargets)
                    {
                        if (target is DatabaseLogTarget dbTarget)
                        {
                            dbTarget.ReinitializeIfNeeded();
                        }
                        else if (target is WrapperTargetBase wrapper)
                        {
                            // 处理 AsyncWrapper 等包装器
                            var innerTarget = GetInnermostTarget(wrapper);
                            if (innerTarget is DatabaseLogTarget dbInnerTarget)
                            {
                                dbInnerTarget.ReinitializeIfNeeded();
                            }
                        }
                    }
                }
            }
        }

        private static Target? GetInnermostTarget(WrapperTargetBase wrapper)
        {
            var current = wrapper.WrappedTarget;
            while (current is WrapperTargetBase innerWrapper)
            {
                current = innerWrapper.WrappedTarget;
            }
            return current;
        }

        private void ReinitializeIfNeeded()
        {
            if (_serviceProvider != null && !_isInitialized)
            {
                InitializeInstance();
            }
        }

        private void InitializeInstance()
        {
            lock (_flushLock)
            {
                if (_isInitialized) return;

                _isInitialized = true;

                // 确保名称正确
                if (string.IsNullOrEmpty(Name))
                {
                    Name = $"DatabaseLog_{LogType}";
                }

                // 停止旧的定时器（如果存在）
                _flushTimer?.Dispose();

                // 创建新的定时器
                _flushTimer = new Timer(
                    callback: _ =>
                    {
                        try
                        {
                            Task.Run(async () => await FlushLogsAsync().ConfigureAwait(false));
                        }
                        catch (Exception ex)
                        {
                            InternalLogger.Error(ex, "Timer flush error");
                        }
                    },
                    state: null,
                    dueTime: TimeSpan.FromSeconds(1),
                    period: TimeSpan.FromSeconds(FlushIntervalSeconds));

                InternalLogger.Info($"DatabaseLogTarget '{Name}' instance initialized for LogType: {LogType}, ConnectionString: {(!string.IsNullOrEmpty(ConnectionString) ? "Configured" : "Not Set")}");
            }
        }

        /// <summary>
        /// 初始化目标（NLog调用）
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            // 验证参数
            if (string.IsNullOrEmpty(LogType))
            {
                LogType = "AppLog";
            }

            if (!Enum.TryParse<DatabaseLogType>(LogType, out _))
            {
                InternalLogger.Warn($"Invalid LogType '{LogType}', defaulting to 'AppLog'");
                LogType = "AppLog";
            }

            // 记录连接字符串状态（不记录实际值以保护安全）
            InternalLogger.Info($"DatabaseLogTarget InitializeTarget - ConnectionString: {(!string.IsNullOrEmpty(ConnectionString) ? "Set" : "Empty")}");

            // 如果服务提供者已经可用，立即初始化
            if (_serviceProvider != null && !_isInitialized)
            {
                InitializeInstance();
            }

            InternalLogger.Info($"DatabaseLogTarget InitializeTarget completed for '{Name}' with LogType: {LogType}");
        }

        protected override void Write(LogEventInfo logEvent)
        {
            try
            {
                // 检查服务提供者
                if (_serviceProvider == null)
                {
                    if (DateTime.Now.Second % 10 == 0)
                    {
                        InternalLogger.Warn($"DatabaseLogTarget '{Name}': ServiceProvider is null");
                    }
                    return;
                }

                // 确保已初始化
                if (!_isInitialized)
                {
                    InitializeInstance();
                }

                var logType = Enum.TryParse<DatabaseLogType>(LogType, out var type) ? type : DatabaseLogType.AppLog;

                switch (logType)
                {
                    case DatabaseLogType.AppLog:
                        // 是否写入数据库
                        bool writeToDatabase = GetPropertyValue(logEvent, "IsToDatabase", false);
                        if (!writeToDatabase)
                        {
                            return;
                        }

                        var appLog = new AppLogModel
                        {
                            LogTime = logEvent.TimeStamp,
                            LogLevel = logEvent.Level.Name,
                            Logger = string.IsNullOrEmpty(logEvent.CallerMemberName)
                                ? (logEvent.LoggerName ?? "Unknown")
                                : $"{logEvent.LoggerName ?? "Unknown"}.{logEvent.CallerMemberName}",
                            Message = logEvent.FormattedMessage ?? string.Empty,
                            Exception = logEvent.Exception?.ToString(),
                        };
                        _logQueue.Enqueue(appLog);
                        break;

                    case DatabaseLogType.MesInterfaceLog:
                        // MES日志总是写入数据库，不需要检查IsToDatabase
                        var mesLog = CreateMesInterfaceLog(logEvent);
                        if (mesLog != null)
                        {
                            _logQueue.Enqueue(mesLog);
                        }
                        break;

                    case DatabaseLogType.WebInterfaceLog:
                        var webLog = CreateWebInterfaceLog(logEvent);
                        if (webLog != null)
                        {
                            _logQueue.Enqueue(webLog);
                        }
                        break;
                }

                // 如果队列过大，立即刷新
                if (_logQueue.Count >= BatchSize)
                {
                    Task.Run(async () => await FlushLogsAsync().ConfigureAwait(false));
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, $"DatabaseLogTarget Write error");
            }
        }

        /// <summary>
        /// 异步刷新日志到数据库
        /// </summary>
        private async Task FlushLogsAsync()
        {
            if (_serviceProvider == null) return;

            try
            {
                List<AppLogModel> appLogs;
                List<MesInterfaceLogModel> mesLogs;
                List<WebInterfaceLogModel> webLogs;

                lock (_flushLock)
                {
                    if (_logQueue.IsEmpty) return;

                    appLogs = new List<AppLogModel>();
                    mesLogs = new List<MesInterfaceLogModel>();
                    webLogs = new List<WebInterfaceLogModel>();

                    int count = 0;
                    while (_logQueue.TryDequeue(out var log) && count < BatchSize * 2)
                    {
                        switch (log)
                        {
                            case AppLogModel appLog:
                                appLogs.Add(appLog);
                                break;
                            case MesInterfaceLogModel mesLog:
                                mesLogs.Add(mesLog);
                                break;
                            case WebInterfaceLogModel webLog:
                                webLogs.Add(webLog);
                                break;
                        }
                        count++;
                    }
                }

                if (appLogs.Count > 0 || mesLogs.Count > 0 || webLogs.Count > 0)
                {
                    await SaveLogsToDatabase(appLogs, mesLogs, webLogs).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "FlushLogsAsync error");
            }
        }

      

        /// <summary>
        /// 保存日志到数据库
        /// </summary>
        private async Task SaveLogsToDatabase(
            List<AppLogModel> appLogs,
            List<MesInterfaceLogModel> mesLogs,
            List<WebInterfaceLogModel> webLogs)
        {
            try
            {
                using var scope = _serviceProvider!.CreateScope();

                if (appLogs.Count > 0)
                {
                    var appLogRepo = scope.ServiceProvider.GetRequiredService<IAppLogRepository>();
                    var success = await appLogRepo.BulkInsertAsync(appLogs).ConfigureAwait(false);
                    if (success)
                    {
                        InternalLogger.Info($"Successfully saved {appLogs.Count} app logs to database");
                    }
                }

                if (mesLogs.Count > 0)
                {
                    var mesLogRepo = scope.ServiceProvider.GetRequiredService<IMesInterfaceLogRepository>();
                    var success = await mesLogRepo.BulkInsertAsync(mesLogs).ConfigureAwait(false);
                    if (success)
                    {
                        InternalLogger.Info($"Successfully saved {mesLogs.Count} MES logs to database");
                    }
                }

                if (webLogs.Count > 0)
                {
                    var webLogRepo = scope.ServiceProvider.GetRequiredService<IWebInterfaceLogRepository>();
                    var success = await webLogRepo.BulkInsertAsync(webLogs).ConfigureAwait(false);
                    if (success)
                    {
                        InternalLogger.Info($"Successfully saved {webLogs.Count} Web logs to database");
                    }
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, $"Failed to save logs to database: {ex.Message}");

                // 将失败的日志重新加入队列（限制重试次数）
                if (_logQueue.Count < BatchSize * 3)
                {
                    foreach (var log in appLogs) _logQueue.Enqueue(log);
                    foreach (var log in mesLogs) _logQueue.Enqueue(log);
                    foreach (var log in webLogs) _logQueue.Enqueue(log);
                }
            }
        }

        /// <summary>
        /// 异步刷新（NLog调用）
        /// </summary>
        protected override void FlushAsync(AsyncContinuation asyncContinuation)
        {
            Task.Run(async () =>
            {
                try
                {
                    await FlushLogsAsync().ConfigureAwait(false);
                    asyncContinuation(null);
                }
                catch (Exception ex)
                {
                    asyncContinuation(ex);
                }
            });
        }

        /// <summary>
        /// 关闭目标
        /// </summary>
        protected override void CloseTarget()
        {
            try
            {
                InternalLogger.Info($"Closing DatabaseLogTarget '{Name}'");

                // 停止定时器
                _flushTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _flushTimer?.Dispose();
                _flushTimer = null;

                // 最后一次刷新
                var flushTask = FlushLogsAsync();
                if (!flushTask.Wait(TimeSpan.FromSeconds(10)))
                {
                    InternalLogger.Warn("Final flush timeout during close");
                }

                _isInitialized = false;
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Error closing DatabaseLogTarget");
            }
            finally
            {
                base.CloseTarget();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _flushTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 辅助方法
        /// <summary>
        /// 创建MES接口日志
        /// </summary>
        private MesInterfaceLogModel? CreateMesInterfaceLog(LogEventInfo logEvent)
        {
            try
            {
                // 从事件属性中获取数据
                var interfaceName = GetPropertyValue<string?>(logEvent, "InterfaceName", null);
                if (string.IsNullOrEmpty(interfaceName))
                {
                    // 如果没有InterfaceName，尝试从Method获取
                    interfaceName = GetPropertyValue<string?>(logEvent, "Method", null);
                }

                if (string.IsNullOrEmpty(interfaceName))
                {
                    InternalLogger.Warn("MES日志缺少接口名称");
                    return null;
                }

                var startTime = GetPropertyValue<DateTime>(logEvent, "StartTime", DateTime.Now);
                var endTime = GetPropertyValue<DateTime>(logEvent, "EndTime", DateTime.Now);
                var executionTime = GetPropertyValue<int>(logEvent, "ExecutionTime", 0);

                // 如果没有明确的开始/结束时间，根据执行时间计算
                if (executionTime > 0 && startTime == endTime)
                {
                    startTime = endTime.AddMilliseconds(-executionTime);
                }

                var mesLog = new MesInterfaceLogModel
                {
                    Method = interfaceName,
                    InputJson = GetPropertyValue<string?>(logEvent, "RequestData", "{}")
                               ?? GetPropertyValue<string?>(logEvent, "InputJson", "{}")
                               ?? "{}",
                    OutputJson = GetPropertyValue<string?>(logEvent, "ResponseData", "{}")
                                ?? GetPropertyValue<string?>(logEvent, "OutputJson", "{}")
                                ?? "{}",
                    SuccessFlag = GetPropertyValue<bool>(logEvent, "IsSuccess", false)
                                 || GetPropertyValue<bool>(logEvent, "SuccessFlag", false),
                    StartTime = startTime,
                    EndTime = endTime,
                    ConsumingTime = executionTime,
                    LogDate = endTime,

                    // 扩展字段
                    ApiType = GetPropertyValue<string?>(logEvent, "ApiType", null),
                    Endpoint = GetPropertyValue<string?>(logEvent, "Endpoint", null),
                    HttpStatusCode = GetPropertyValue<int?>(logEvent, "HttpStatusCode", null),
                    ErrorCode = GetPropertyValue<string?>(logEvent, "ErrorCode", null),
                    OperatorId = GetPropertyValue<string?>(logEvent, "OperatorId", null),
                    DeviceNumber = GetPropertyValue<string?>(logEvent, "DeviceNumber", null)
                };

                // 如果有异常，提取错误信息
                if (logEvent.Exception != null && string.IsNullOrEmpty(mesLog.ErrorCode))
                {
                    mesLog.ErrorCode = logEvent.Exception.GetType().Name;
                }

                // 如果有错误消息，也保存到OutputJson中
                var errorMessage = GetPropertyValue<string?>(logEvent, "ErrorMessage", null);
                if (!string.IsNullOrEmpty(errorMessage) && !mesLog.SuccessFlag)
                {
                    // 使用 JsonHelper 进行序列化和反序列化
                    if (JsonHelper.TryDeserialize<Dictionary<string, object>>(mesLog.OutputJson ?? "{}", out var outputObj))
                    {
                        outputObj ??= new Dictionary<string, object>();
                        outputObj["errorMessage"] = errorMessage;
                        mesLog.OutputJson = JsonHelper.Serialize(outputObj);
                    }
                    else
                    {
                        // 如果解析失败，直接创建错误信息
                        mesLog.OutputJson = JsonHelper.Serialize(new { error = errorMessage });
                    }
                }

                return mesLog;
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "创建MES日志失败");
                return null;
            }
        }

        /// <summary>
        /// 创建Web接口日志
        /// </summary>
        private WebInterfaceLogModel? CreateWebInterfaceLog(LogEventInfo logEvent)
        {
            try
            {
                // 获取API路径
                var apiPath = GetPropertyValue<string?>(logEvent, "ApiPath", null);
                if (string.IsNullOrEmpty(apiPath))
                {
                    // 尝试从其他可能的属性名获取
                    apiPath = GetPropertyValue<string?>(logEvent, "Path", null)
                             ?? GetPropertyValue<string?>(logEvent, "RequestPath", null)
                             ?? GetPropertyValue<string?>(logEvent, "Method", null);
                }

                if (string.IsNullOrEmpty(apiPath))
                {
                    InternalLogger.Warn("Web日志缺少API路径");
                    return null;
                }

                // 获取时间相关信息
                var startTime = GetPropertyValue<DateTime>(logEvent, "StartTime", logEvent.TimeStamp);
                var endTime = GetPropertyValue<DateTime>(logEvent, "EndTime", DateTime.Now);
                var executionTime = GetPropertyValue<long>(logEvent, "ExecutionTime", 0L);

                // 如果没有明确的开始/结束时间，根据执行时间计算
                if (executionTime > 0 && startTime == endTime)
                {
                    startTime = endTime.AddMilliseconds(-executionTime);
                }

                // 获取状态码
                var statusCode = GetPropertyValue<int>(logEvent, "StatusCode", 0);
                if (statusCode == 0)
                {
                    // 尝试从其他可能的属性名获取
                    statusCode = GetPropertyValue<int>(logEvent, "HttpStatusCode", 0);
                }

                // 获取输入输出数据
                var inputJson = GetPropertyValue<string?>(logEvent, "RequestBody", "{}")
                               ?? GetPropertyValue<string?>(logEvent, "Request", "{}")
                               ?? GetPropertyValue<string?>(logEvent, "InputJson", "{}")
                               ?? "{}";

                var outputJson = GetPropertyValue<string?>(logEvent, "ResponseBody", "{}")
                                ?? GetPropertyValue<string?>(logEvent, "Response", "{}")
                                ?? GetPropertyValue<string?>(logEvent, "OutputJson", "{}")
                                ?? "{}";

                // 判断成功标志
                var successFlag = statusCode >= 200 && statusCode < 300;

                // 如果有明确的成功标志，使用它
                var explicitSuccess = GetPropertyValue<bool?>(logEvent, "SuccessFlag", null);
                if (explicitSuccess.HasValue)
                {
                    successFlag = explicitSuccess.Value;
                }

                // 如果有异常，设置为失败
                if (logEvent.Exception != null)
                {
                    successFlag = false;

                    // 将异常信息添加到输出JSON中
                    if (JsonHelper.TryDeserialize<Dictionary<string, object>>(outputJson, out var outputObj))
                    {
                        outputObj ??= new Dictionary<string, object>();
                        outputObj["exception"] = new
                        {
                            type = logEvent.Exception.GetType().Name,
                            message = logEvent.Exception.Message,
                            stackTrace = logEvent.Exception.StackTrace
                        };
                        outputJson = JsonHelper.Serialize(outputObj);
                    }
                    else
                    {
                        outputJson = JsonHelper.Serialize(new
                        {
                            error = "Exception occurred",
                            exception = new
                            {
                                type = logEvent.Exception.GetType().Name,
                                message = logEvent.Exception.Message,
                                stackTrace = logEvent.Exception.StackTrace
                            }
                        });
                    }
                }

                // 处理错误消息
                var errorMessage = GetPropertyValue<string?>(logEvent, "ErrorMessage", null);
                if (!string.IsNullOrEmpty(errorMessage) && !successFlag)
                {
                    if (JsonHelper.TryDeserialize<Dictionary<string, object>>(outputJson, out var outputObj))
                    {
                        outputObj ??= new Dictionary<string, object>();
                        outputObj["errorMessage"] = errorMessage;
                        outputJson = JsonHelper.Serialize(outputObj);
                    }
                    else
                    {
                        outputJson = JsonHelper.Serialize(new { error = errorMessage });
                    }
                }

                // 创建 WebInterfaceLogModel
                var webLog = new WebInterfaceLogModel
                {
                    Method = apiPath,
                    InputJson = inputJson,
                    OutputJson = outputJson,                  
                    ConsumingTime = executionTime > 0 ? executionTime : (long)(endTime - startTime).TotalMilliseconds,
                    SuccessFlag = successFlag,
                    LogDate = endTime
                };

                return webLog;
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "创建Web日志失败");
                return null;
            }
        }

        /// <summary>
        /// 从LogEventInfo中获取属性值
        /// </summary>
        private T? GetPropertyValue<T>(LogEventInfo logEvent, string propertyName, T? defaultValue = default)
        {
            if (logEvent.Properties.TryGetValue(propertyName, out var value))
            {
                if (value is T typedValue)
                    return typedValue;

                try
                {
                    if (typeof(T) == typeof(bool))
                    {
                        // 特殊处理布尔值
                        if (value is string strValue)
                        {
                            return (T)(object)bool.Parse(strValue);
                        }
                    }

                    if (value != null)
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                }
                catch
                {
                    // 转换失败，返回默认值
                }
            }

            return defaultValue;
        }

        #endregion
    }
}