using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Models.Data;
using System.Runtime.CompilerServices;


namespace SUNWODA_SEVB.Core.Interfaces
{
    /// <summary>
    /// 日志服务接口
    /// </summary>
    public interface ILoggerService
    {
        #region 委托实现自定义处理
        public delegate void OnLoggerReportEventHandle(AppLogModel logModel);
        event OnLoggerReportEventHandle? OnLoggerReportEvent;
        static event OnLoggerReportEventHandle? OnLoggerReportGlobalEvent;
        protected static void RaiseOnLoggerReportGlobalEvent(AppLogModel logModel) => OnLoggerReportGlobalEvent?.Invoke(logModel);
        #endregion

        #region 基础日志方法

        void Trace(string message, bool isToDatabase = false,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        void Debug(string message, bool isToDatabase = false,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        void Info(string message, bool isToDatabase = false,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        void Warn(string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        void Error(string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        void Fatal(string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        #endregion

        #region 带异常的日志方法

        void Trace(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        void Debug(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        void Info(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        void Warn(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        void Error(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        void Fatal(string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        #endregion

        #region 通用日志方法

        void Log(CoreLogLevel level, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        void Log(CoreLogLevel level, string message, Exception exception, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        #endregion

        #region 特殊日志方法

        /// <summary>
        /// 记录到特殊日志文件
        /// </summary>
        void LogToSpecialFile(string fileName, string message, CoreLogLevel level = CoreLogLevel.Info, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0);

        /// <summary>
        /// 记录MES接口日志
        /// </summary>
        void LogMesInterface(string interfaceName, string requestData, string responseData,
            bool isSuccess, int executionTime = 0, string errorMessage = null!);

        /// <summary>
        /// 记录Web接口日志
        /// </summary>
        void LogWebInterface(string apiPath, string httpMethod, string requestBody,
            string responseBody, int statusCode, long executionTime);

        #endregion
    }

    /// <summary>
    /// 泛型日志服务接口，用于依赖注入
    /// </summary>
    /// <typeparam name="T">日志上下文类型（通常是使用日志的类）</typeparam>
    public interface ILoggerService<T> : ILoggerService
    {
        /// <summary>
        /// 获取日志上下文类型
        /// </summary>
        Type ContextType { get; }
    }

}