using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RecordModels;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Extension
{
    public static class LoggerExtension
    {
        #region 用户/系统操作日志记录扩展
        public const string UserLog = "UserLog";

        public static void UserTrace<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var record = new SystemRecordModel();
            record.Name = CCTBCore.UserName;
            record.Account = CCTBCore.UserAccount;
            record.Role = OperationUtil.RoleIDToString(CCTBCore.UserRoleID);
            record.Message = message;
            var msg = JsonConvert.SerializeObject(record);
            logger.LogToSpecialFile(UserLog, msg, CoreLogLevel.Trace, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void UserDebug<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var record = new SystemRecordModel();
            record.Name = CCTBCore.UserName;
            record.Account = CCTBCore.UserAccount;
            record.Role = OperationUtil.RoleIDToString(CCTBCore.UserRoleID);
            record.Message = message;
            var msg = JsonConvert.SerializeObject(record);
            logger.LogToSpecialFile(UserLog, msg, CoreLogLevel.Debug, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void UserInfo<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var record = new SystemRecordModel();
            record.Name = CCTBCore.UserName;
            record.Account = CCTBCore.UserAccount;
            record.Role = OperationUtil.RoleIDToString(CCTBCore.UserRoleID);
            record.Message = message;
            var msg = JsonConvert.SerializeObject(record);
            logger.LogToSpecialFile(UserLog, msg, CoreLogLevel.Info, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void UserWarn<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var record = new SystemRecordModel();
            record.Name = CCTBCore.UserName;
            record.Account = CCTBCore.UserAccount;
            record.Role = OperationUtil.RoleIDToString(CCTBCore.UserRoleID);
            record.Message = message;
            var msg = JsonConvert.SerializeObject(record);
            logger.LogToSpecialFile(UserLog, msg, CoreLogLevel.Warn, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void UserError<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var record = new SystemRecordModel();
            record.Name = CCTBCore.UserName;
            record.Account = CCTBCore.UserAccount;
            record.Role = OperationUtil.RoleIDToString(CCTBCore.UserRoleID);
            record.Message = message;
            var msg = JsonConvert.SerializeObject(record);
            logger.LogToSpecialFile(UserLog, msg, CoreLogLevel.Error, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void UserFatal<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var record = new SystemRecordModel();
            record.Name = CCTBCore.UserName;
            record.Account = CCTBCore.UserAccount;
            record.Role = OperationUtil.RoleIDToString(CCTBCore.UserRoleID);
            record.Message = message;
            var msg = JsonConvert.SerializeObject(record);
            logger.LogToSpecialFile(UserLog, msg, CoreLogLevel.Fatal, isToDatabase, memberName, filePath, lineNumber);
        }
        #endregion

        #region 调机动作日志记录扩展
        public const string AdjustmentLog = "AdjustmentLog";

        public static void AdjustmentTrace<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AdjustmentLog, message, CoreLogLevel.Trace, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void AdjustmentDebug<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AdjustmentLog, message, CoreLogLevel.Debug, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void AdjustmentInfo<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AdjustmentLog, message, CoreLogLevel.Info, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void AdjustmentWarn<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AdjustmentLog, message, CoreLogLevel.Warn, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void AdjustmentError<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AdjustmentLog, message, CoreLogLevel.Error, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void AdjustmentFatal<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AdjustmentLog, message, CoreLogLevel.Fatal, isToDatabase, memberName, filePath, lineNumber);
        }
        #endregion

        #region 报警日志记录扩展
        public const string AlarmLog = "AlarmLog";

        public static void AlarmTrace<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AlarmLog, message, CoreLogLevel.Trace, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void AlarmDebug<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AlarmLog, message, CoreLogLevel.Debug, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void AlarmInfo<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AlarmLog, message, CoreLogLevel.Info, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void AlarmWarn<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AlarmLog, message, CoreLogLevel.Warn, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void AlarmError<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AlarmLog, message, CoreLogLevel.Error, isToDatabase, memberName, filePath, lineNumber);
        }

        public static void AlarmFatal<T>(this ILoggerService<T> logger, string message, bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.LogToSpecialFile(AlarmLog, message, CoreLogLevel.Fatal, isToDatabase, memberName, filePath, lineNumber);
        }
        #endregion
    }
}
