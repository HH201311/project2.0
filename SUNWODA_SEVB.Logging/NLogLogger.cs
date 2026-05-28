using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using System;
using System.Runtime.CompilerServices;
using ILogger = NLog.ILogger;

namespace SUNWODA_SEVB.Logging
{
    /// <summary>
    /// 使用 NLog 实现的泛型日志记录器
    /// </summary>
    /// <typeparam name="T">日志上下文类型</typeparam>
    public class NLogLogger<T> : ILoggerService<T>
    {
        // 从 NLog 获取与泛型类型 T 关联的 logger 实例
        private static readonly ILogger Logger = NLog.LogManager.GetLogger(typeof(T).FullName!);
        private readonly IServiceProvider _serviceProvider;
        #region ILoggerService<T> 实现

        /// <summary>
        /// 获取日志上下文类型
        /// </summary>
        public Type ContextType => typeof(T);

        // 通过构造函数注入仓储
        public NLogLogger(IServiceProvider serviceProvider, IMesInterfaceLogRepository? mesLogRepository = null)
        {
            _serviceProvider = serviceProvider;
            _mesLogRepository = mesLogRepository;
        }

        #endregion

        #region 委托事件
        public event ILoggerService.OnLoggerReportEventHandle? OnLoggerReportEvent;
        #endregion

        #region 基础日志方法（仅消息）

        public void Trace(string message, bool isToDatabase = false,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Trace, message, null, isToDatabase, memberName, filePath, lineNumber);
        }

        public void Debug(string message, bool isToDatabase = false,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Debug, message, null, isToDatabase, memberName, filePath, lineNumber);
        }

        public void Info(string message, bool isToDatabase = false,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Info, message, null, isToDatabase, memberName, filePath, lineNumber);
        }

        public void Warn(string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Warn, message, null, isToDatabase, memberName, filePath, lineNumber);
        }

        public void Error(string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Error, message, null, isToDatabase, memberName, filePath, lineNumber);
        }

        public void Fatal(string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Fatal, message, null, isToDatabase, memberName, filePath, lineNumber);
        }

        #endregion

        #region 带异常的日志方法

        public void Trace(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Trace, message, exception, isToDatabase, memberName, filePath, lineNumber);
        }

        public void Debug(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Debug, message, exception, isToDatabase, memberName, filePath, lineNumber);
        }

        public void Info(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Info, message, exception, isToDatabase, memberName, filePath, lineNumber);
        }

        public void Warn(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Warn, message, exception, isToDatabase, memberName, filePath, lineNumber);
        }

        public void Error(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Error, message, exception, isToDatabase, memberName, filePath, lineNumber);
        }

        public void Fatal(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(NLog.LogLevel.Fatal, message, exception, isToDatabase, memberName, filePath, lineNumber);
        }

        #endregion

        #region 通用日志方法

        public void Log(CoreLogLevel level, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(ConvertToNLogLevel(level), message, null, isToDatabase, memberName, filePath, lineNumber);
        }

        public void Log(CoreLogLevel level, string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogWithCallSite(ConvertToNLogLevel(level), message, exception, isToDatabase, memberName, filePath, lineNumber);
        }

        #endregion

        #region 特殊日志方法
        /// <summary>
        /// 记录到特殊日志文件
        /// </summary>
        public void LogToSpecialFile(string fileName, string message, CoreLogLevel level = CoreLogLevel.Info, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            // 使用文件名创建特定的logger
            var specialLogger = NLog.LogManager.GetLogger($"SpecialFile.{fileName}");
            var nlogLevel = ConvertToNLogLevel(level);

            var logEvent = new NLog.LogEventInfo(nlogLevel, specialLogger.Name, message);

            // 设置特殊文件标记
            logEvent.Properties["SpecialFileName"] = fileName;

            // 设置是否记录到数据库
            logEvent.Properties["IsToDatabase"] = isToDatabase;

            // 设置调用位置信息
            if (!string.IsNullOrEmpty(filePath))
            {
                string className = typeof(T).Name;
                logEvent.SetCallerInfo(className, memberName, filePath, lineNumber);
            }

            specialLogger.Log(typeof(NLogLogger<T>), logEvent);
        }

        public void LogSpecial(CoreLogLevel level, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var logEvent = CreateLogEventWithCallSite(ConvertToNLogLevel(level), message, null, isToDatabase, memberName, filePath, lineNumber);
            logEvent.Properties["LogToSpecialFile"] = true;
            Logger.Log(typeof(NLogLogger<T>), logEvent);
        }

        public void LogSpecial(CoreLogLevel level, string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var logEvent = CreateLogEventWithCallSite(ConvertToNLogLevel(level), message, exception, isToDatabase, memberName, filePath, lineNumber);
            logEvent.Properties["LogToSpecialFile"] = true;
            Logger.Log(typeof(NLogLogger<T>), logEvent);
        }

        #endregion

        #region 接口日志方法
        private readonly IMesInterfaceLogRepository? _mesLogRepository;

        /// <summary>
        /// 记录MES接口日志
        /// </summary>
        public void LogMesInterface(string interfaceName, string requestData, string responseData,
            bool isSuccess, int executionTime = 0, string? errorMessage = null)
        {
            try
            {
                var logger = NLog.LogManager.GetLogger($"MesInterface.{interfaceName}");
                var logLevel = isSuccess ? NLog.LogLevel.Info : NLog.LogLevel.Error;

                var logEvent = NLog.LogEventInfo.Create(
                    logLevel,
                    logger.Name,
                    $"MES接口调用: {interfaceName} | 成功: {isSuccess} | 耗时: {executionTime}ms");

                // 设置所有属性
                logEvent.Properties["InterfaceName"] = interfaceName;
                logEvent.Properties["RequestData"] = requestData;
                logEvent.Properties["ResponseData"] = responseData;
                logEvent.Properties["IsSuccess"] = isSuccess;
                logEvent.Properties["ExecutionTime"] = executionTime;

                // 计算开始和结束时间
                var endTime = DateTime.Now;
                var startTime = endTime.AddMilliseconds(-executionTime);
                logEvent.Properties["StartTime"] = startTime;
                logEvent.Properties["EndTime"] = endTime;

                // 设置扩展属性
                logEvent.Properties["ApiType"] = GetApiTypeFromInterface(interfaceName);
                logEvent.Properties["DeviceNumber"] = GetCurrentDeviceNumber();
                logEvent.Properties["OperatorId"] = GetCurrentOperatorId();
                logEvent.Properties["Endpoint"] = GetEndpointFromInterface(interfaceName);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    logEvent.Exception = new Exception(errorMessage);
                    logEvent.Properties["ErrorMessage"] = errorMessage;
                    logEvent.Properties["ErrorCode"] = ExtractErrorCode(errorMessage);
                }

                // 记录日志
                logger.Log(logEvent);

                // 同时记录到特殊文件（可选）
                if (!isSuccess || executionTime > 1000) // 失败或慢响应记录到特殊文件
                {
                    var message = $"[{interfaceName}] Success:{isSuccess} Time:{executionTime}ms";
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        message += $" Error:{errorMessage}";
                    }
                    LogToSpecialFile("MES_SlowOrError.log", message,
                        isSuccess ? CoreLogLevel.Warn : CoreLogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                // 日志记录失败不应影响主流程
                Error($"记录MES接口日志失败: {ex.Message}", ex, false);
            }
        }

        /// <summary>
        /// 记录Web接口日志
        /// </summary>
        public void LogWebInterface(string apiPath, string httpMethod, string requestBody,
            string responseBody, int statusCode, long executionTime)
        {
            var logger = NLog.LogManager.GetLogger("WebInterface." + apiPath.Replace("/", "."));
            var logEvent = NLog.LogEventInfo.Create(
                statusCode >= 200 && statusCode < 300 ? NLog.LogLevel.Info : NLog.LogLevel.Error,
                logger.Name,
                $"Web API调用: {httpMethod} {apiPath}");

            logEvent.Properties["ApiPath"] = apiPath;
            logEvent.Properties["HttpMethod"] = httpMethod;
            logEvent.Properties["RequestBody"] = requestBody;
            logEvent.Properties["ResponseBody"] = responseBody;
            logEvent.Properties["StatusCode"] = statusCode;
            logEvent.Properties["ExecutionTime"] = executionTime;
            logEvent.Properties["IsToDatabase"] = true;
            logger.Log(logEvent);
        }
        #endregion



        #region 私有辅助方法

        /// <summary>
        /// 核心日志记录方法，包含调用位置信息
        /// </summary>
        private void LogWithCallSite(NLog.LogLevel level, string message, Exception? exception, bool isToDatabase,
            string memberName, string filePath, int lineNumber)
        {
            var logEvent = CreateLogEventWithCallSite(level, message, exception, isToDatabase, memberName, filePath, lineNumber);
            Logger.Log(typeof(NLogLogger<T>), logEvent);

            var logModel = new AppLogModel();
            logModel.LogTime = logEvent.TimeStamp;
            logModel.LogLevel = logEvent.Level.Name;
            logModel.Message = logEvent.Message;
            logModel.Exception = logEvent.Exception?.ToString();
            Task.Run(() => OnLoggerReportEvent?.Invoke(logModel));
            Task.Run(() => ILoggerService.RaiseOnLoggerReportGlobalEvent(logModel));
        }

        /// <summary>
        /// 创建包含调用位置信息的 LogEventInfo
        /// </summary>
        private NLog.LogEventInfo CreateLogEventWithCallSite(NLog.LogLevel level, string message, Exception? exception, bool isToDatabase,
            string memberName, string filePath, int lineNumber)
        {
            var logEvent = new NLog.LogEventInfo(level, Logger.Name, message);
            logEvent.Exception = exception;

            // 设置是否记录到数据库
            logEvent.Properties["IsToDatabase"] = isToDatabase;

            // 设置调用位置信息
            if (!string.IsNullOrEmpty(filePath))
            {
                // 使用泛型类型 T 的名称作为类名
                string className = typeof(T).Name;

                // 设置 CallSite 信息
                logEvent.SetCallerInfo(className, memberName, filePath, lineNumber);
            }

            return logEvent;
        }

        /// <summary>
        /// 将自定义的 LogLevel 转换为 NLog 的 LogLevel
        /// </summary>
        private static NLog.LogLevel ConvertToNLogLevel(CoreLogLevel level)
        {
            return level switch
            {
                CoreLogLevel.Trace => NLog.LogLevel.Trace,
                CoreLogLevel.Debug => NLog.LogLevel.Debug,
                CoreLogLevel.Info => NLog.LogLevel.Info,
                CoreLogLevel.Warn => NLog.LogLevel.Warn,
                CoreLogLevel.Error => NLog.LogLevel.Error,
                CoreLogLevel.Fatal => NLog.LogLevel.Fatal,
                _ => NLog.LogLevel.Debug, // 默认级别
            };
        }

        #region MES日志辅助方法

        /// <summary>
        /// 根据接口名称获取API类型
        /// </summary>
        private string GetApiTypeFromInterface(string interfaceName)
        {
            if (interfaceName.Contains("Client", StringComparison.OrdinalIgnoreCase))
                return "Client";
            if (interfaceName.Contains("EVCEVB", StringComparison.OrdinalIgnoreCase))
                return "EVCEVB";
            if (interfaceName.Contains("IME", StringComparison.OrdinalIgnoreCase))
                return "IME";
            if (interfaceName.Contains("Web", StringComparison.OrdinalIgnoreCase))
                return "Web";
            if (interfaceName.Contains("DIPS", StringComparison.OrdinalIgnoreCase))
                return "DIPS";

            return "Unknown";
        }

        /// <summary>
        /// 获取当前设备编号
        /// </summary>
        private string? GetCurrentDeviceNumber()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();

                var deviceNumber = globalSettingRepo.GetSettingValue("DeviceNumber");
                return deviceNumber?.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// 获取当前操作员ID
        /// </summary>
        private string? GetCurrentOperatorId()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();

                var currentUser = globalSettingRepo.GetSettingValue("CurrentUserAccount");
                return currentUser?.ToString();
            }
            catch
            {
                return "System";
            }
        }

        /// <summary>
        /// 根据接口名称获取端点
        /// </summary>
        private string? GetEndpointFromInterface(string interfaceName)
        {
            try
            {
                var endpoints = ConfigurationHelper.GetSection<Dictionary<string, string>>("MesApi:Endpoints");
                if (endpoints != null && endpoints.TryGetValue(interfaceName, out var endpoint))
                {
                    return endpoint;
                }

                // 根据接口名称推断端点
                return interfaceName.Replace(".", "/").ToLower();
            }
            catch
            {
                return interfaceName;
            }
        }

        /// <summary>
        /// 从错误消息中提取错误代码
        /// </summary>
        private string? ExtractErrorCode(string? errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
                return null;

            // 尝试从错误消息中提取错误代码
            // 假设格式为 "[ERROR_CODE] Message" 或 "Error Code: ERROR_CODE"
            var patterns = new[]
            {
                @"\[([A-Z0-9_]+)\]",
                @"Error Code:\s*([A-Z0-9_]+)",
                @"错误代码:\s*([A-Z0-9_]+)"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(errorMessage, pattern);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        #endregion

        #endregion
    }
}
