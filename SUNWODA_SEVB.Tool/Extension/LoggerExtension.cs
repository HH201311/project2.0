using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
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
using System.Windows.Interop;

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
            record.Role = CCTBCore.RoleIDToString(CCTBCore.UserRoleID);
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
            record.Role = CCTBCore.RoleIDToString(CCTBCore.UserRoleID);
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
            record.Role = CCTBCore.RoleIDToString(CCTBCore.UserRoleID);
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
            record.Role = CCTBCore.RoleIDToString(CCTBCore.UserRoleID);
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
            record.Role = CCTBCore.RoleIDToString(CCTBCore.UserRoleID);
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
            record.Role = CCTBCore.RoleIDToString(CCTBCore.UserRoleID);
            record.Message = message;
            var msg = JsonConvert.SerializeObject(record);
            logger.LogToSpecialFile(UserLog, msg, CoreLogLevel.Fatal, isToDatabase, memberName, filePath, lineNumber);
        }
        #endregion

        #region 调机动作日志记录扩展
        public const string AdjustmentLog = "AdjustmentLog";

        /// <summary>
        /// 泵速调机推荐动作日志记录
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="sideType"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="passCount"></param>
        /// <param name="positions"></param>
        /// <param name="adjustOffsets"></param>
        /// <param name="isToDatabase"></param>
        /// <param name="memberName"></param>
        /// <param name="filePath"></param>
        /// <param name="lineNumber"></param>
        public static void AdjustmentBRecommend<T>(this ILoggerService<T> logger,
            ESideType sideType,
            EClosedLoopType closedLoopType,
            float passCount,
            List<float> positions,
            List<float> adjustOffsets,
            bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.AdjustmentInfo(sideType, closedLoopType, 0, passCount, positions, adjustOffsets, new List<int>(), new List<int>(), isToDatabase, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// 泵速调机成功动作日志记录
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="sideType"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="passCount"></param>
        /// <param name="positions"></param>
        /// <param name="adjustOffsets"></param>
        /// <param name="isToDatabase"></param>
        /// <param name="memberName"></param>
        /// <param name="filePath"></param>
        /// <param name="lineNumber"></param>
        public static void AdjustmentBSuccess<T>(this ILoggerService<T> logger,
            ESideType sideType,
            EClosedLoopType closedLoopType,
            float passCount,
            List<float> positions,
            List<float> adjustOffsets,
            bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.AdjustmentInfo(sideType, closedLoopType, 1, passCount, positions, adjustOffsets, new List<int>(), new List<int>(), isToDatabase, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// 泵速调机失败动作日志记录
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="sideType"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="passCount"></param>
        /// <param name="positions"></param>
        /// <param name="adjustOffsets"></param>
        /// <param name="isToDatabase"></param>
        /// <param name="memberName"></param>
        /// <param name="filePath"></param>
        /// <param name="lineNumber"></param>
        public static void AdjustmentBFailed<T>(this ILoggerService<T> logger,
            ESideType sideType,
            EClosedLoopType closedLoopType,
            float passCount,
            List<float> positions,
            List<float> adjustOffsets,
            bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.AdjustmentInfo(sideType, closedLoopType, 2, passCount, positions, adjustOffsets, new List<int>(), new List<int>(), isToDatabase, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// T块调机推荐动作日志记录
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="sideType"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="passCount"></param>
        /// <param name="positions"></param>
        /// <param name="adjustOffsets"></param>
        /// <param name="isToDatabase"></param>
        /// <param name="memberName"></param>
        /// <param name="filePath"></param>
        /// <param name="lineNumber"></param>
        public static void AdjustmentTRecommend<T>(this ILoggerService<T> logger,
            ESideType sideType,
            EClosedLoopType closedLoopType,
            float passCount,
            List<int> positions,
            List<int> adjustOffsets,
            bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.AdjustmentInfo(sideType, closedLoopType, 0, passCount, new List<float>(), new List<float>(), positions, adjustOffsets, isToDatabase, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// T块调机成功动作日志记录
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="sideType"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="passCount"></param>
        /// <param name="positions"></param>
        /// <param name="adjustOffsets"></param>
        /// <param name="isToDatabase"></param>
        /// <param name="memberName"></param>
        /// <param name="filePath"></param>
        /// <param name="lineNumber"></param>
        public static void AdjustmentTSuccess<T>(this ILoggerService<T> logger,
            ESideType sideType,
            EClosedLoopType closedLoopType,
            float passCount,
            List<int> positions,
            List<int> adjustOffsets,
            bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.AdjustmentInfo(sideType, closedLoopType, 1, passCount, new List<float>(), new List<float>(), positions, adjustOffsets, isToDatabase, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// T块调机失败动作日志记录
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="sideType"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="passCount"></param>
        /// <param name="positions"></param>
        /// <param name="adjustOffsets"></param>
        /// <param name="isToDatabase"></param>
        /// <param name="memberName"></param>
        /// <param name="filePath"></param>
        /// <param name="lineNumber"></param>
        public static void AdjustmentTFailed<T>(this ILoggerService<T> logger,
            ESideType sideType,
            EClosedLoopType closedLoopType,
            float passCount,
            List<int> positions,
            List<int> adjustOffsets,
            bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger.AdjustmentInfo(sideType, closedLoopType, 2, passCount, new List<float>(), new List<float>(), positions, adjustOffsets, isToDatabase, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// 调机动作日志记录
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="sideType"></param>
        /// <param name="closedLoopType"></param>
        /// <param name="adjustModel"></param>
        /// <param name="passCount"></param>
        /// <param name="bPositions"></param>
        /// <param name="bAdjustOffsets"></param>
        /// <param name="tPositions"></param>
        /// <param name="tAdjustOffsets"></param>
        /// <param name="isToDatabase"></param>
        /// <param name="memberName"></param>
        /// <param name="filePath"></param>
        /// <param name="lineNumber"></param>
        private static void AdjustmentInfo<T>(this ILoggerService<T> logger,
            ESideType sideType,
            EClosedLoopType closedLoopType,
            int adjustModel,
            float passCount,
            List<float> bPositions,
            List<float> bAdjustOffsets,
            List<int> tPositions,
            List<int> tAdjustOffsets,
            bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var record = new AdjustmentRecordModel();
            record.SideType = sideType;
            record.ClosedLoopType = closedLoopType;
            record.AdjustModel = adjustModel;
            record.PassCount = passCount;
            record.BPositions = bPositions;
            record.BAdjustOffsets = bAdjustOffsets;
            record.TPositions = tPositions;
            record.TAdjustOffsets = tAdjustOffsets;
            var msg = JsonConvert.SerializeObject(record);
            logger.LogToSpecialFile(AdjustmentLog, msg, CoreLogLevel.Info, isToDatabase, memberName, filePath, lineNumber);
        }
        #endregion

        #region 报警日志记录扩展
        public const string AlarmLog = "AlarmLog";

        /// <summary>
        /// 报警记录
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="code"></param>
        /// <param name="message"></param>
        /// <param name="isToDatabase"></param>
        /// <param name="memberName"></param>
        /// <param name="filePath"></param>
        /// <param name="lineNumber"></param>
        public static void AlarmRecord<T>(this ILoggerService<T> logger,
            string code,
            string message,
            bool isToDatabase = true,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var ac = CCTBCore.AlarmConfigs.FirstOrDefault(o => o.Code == code);
            var level = ac?.Level switch
            {
                EAlarmLevel.一般 => CoreLogLevel.Warn,
                EAlarmLevel.中等 => CoreLogLevel.Error,
                EAlarmLevel.严重 => CoreLogLevel.Fatal,
                _ => CoreLogLevel.Info
            };

            var record = new AlarmRecordModel();
            record.Code = code;
            record.AlarmMessage = message;
            var msg = JsonConvert.SerializeObject(record);
            logger.LogToSpecialFile(AlarmLog, msg, level, isToDatabase, memberName, filePath, lineNumber);

            // 弹窗
            CCTBCore.ShowGrowAlarm(record);
        }
        #endregion
    }
}
