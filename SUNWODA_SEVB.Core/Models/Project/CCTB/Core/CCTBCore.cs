using HandyControl.Controls;
using HandyControl.Data;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.BusinessModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.CCDSocketServer;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RecordModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RoleModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.Core
{
    /// <summary>
    /// CCTB项目静态资源
    /// </summary>
    public static class CCTBCore
    {
        /// <summary>
        /// 自定义配置表ProjectSetting中BelongToVM字段配置：CCTB项目
        /// </summary>
        public const string BelongToVM = "VM_CCTBMainWindow";

        /// <summary>
        /// PLC名称
        /// </summary>
        public const string PLCName = "涂布PLC";

        #region 登录用户信息
        /// <summary>
        /// 当前登录用户账号
        /// </summary>
        public static string? UserAccount { get; set; }

        /// <summary>
        /// 当前登录用户名称
        /// </summary>
        public static string? UserName { get; set; }

        /// <summary>
        /// 当前登录用户角色
        /// </summary>
        public static int UserRoleID { get; set; }

        #region 用户/角色相关
        /// <summary>
        /// 用户角色枚举转换字符串
        /// </summary>
        /// <param name="userRole"></param>
        /// <returns></returns>
        public static string RoleIDToString(UserRole role)
        {
            return RoleIDToString((int)role);
        }

        /// <summary>
        /// 用户角色枚举转换字符串
        /// </summary>
        /// <param name="userRole"></param>
        /// <returns></returns>
        public static string RoleIDToString(int id)
        {
            return id switch
            {
                (int)UserRole.Guest => "操作员",
                (int)UserRole.Engineer => "工程师",
                (int)UserRole.Admin => "管理员",
                (int)UserRole.SuperAdmin => "超级管理员",
                _ => "操作员",
            };
        }

        /// <summary>
        /// 用户角色字符串转换枚举
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public static int StringToRoleID(string role)
        {
            return role switch
            {
                "操作员" => (int)UserRole.Guest,
                "工程师" => (int)UserRole.Engineer,
                "管理员" => (int)UserRole.Admin,
                "超级管理员" => (int)UserRole.SuperAdmin,
                _ => (int)UserRole.Guest,
            };
        }

        /// <summary>
        /// 账号校验(仅包含数字&字母)
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        public static bool IsValidAccount(string? account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return false;

            // ^[a-zA-Z0-9]+$ 表示：从头到尾只能是字母和数字
            return Regex.IsMatch(account, @"^[a-zA-Z0-9]+$");
        }
        #endregion
        #endregion

        #region 项目配置参数
        public static ClosedLoopSettingModel? ClosedLoopSettingMDA { get; set; } = null;
        public static ClosedLoopSettingModel? ClosedLoopSettingMDB { get; set; } = null;
        public static ClosedLoopSettingModel? ClosedLoopSettingTDA { get; set; } = null;
        public static ClosedLoopSettingModel? ClosedLoopSettingTDB { get; set; } = null;
        public static ClosedLoopSettingModel? ClosedLoopSettingMDTDA { get; set; } = null;
        public static ClosedLoopSettingModel? ClosedLoopSettingMDTDB { get; set; } = null;
        public static GeneralSettingsModel? GeneralSettings { get; set; } = null;
        public static BasicFunctionSettingModel? BasicFunctionSetting { get; set; } = null;
        public static SpecialFunctionSettingModel? SpecialFunctionSetting { get; set; } = null;

        public static List<RoleManagementModel> RoleManagementConfigs { get; set; } = [];
        public static List<AlarmConfigModel> AlarmConfigs { get; set; } = [];
        public static AlarmConfigParamModel? AlarmParamConfig { get; set; }

        /// <summary>
        /// PLC采集参数配置
        /// </summary>
        public static List<int> PLCGatherConfigOfA = [], PLCGatherConfigOfB = [], PLCGatherConfigOfPLC = [];
        #endregion

        #region 业务相关
        /// <summary>
        /// 膜卷号
        /// </summary>
        public static string? ProductSn { get; set; } = null;
        #endregion

        #region 涂布项目全局消息通知
        public delegate void MessageEventHandler<T>(object? sender, MessageEventArgs<T> e) where T : class;

        /// <summary>
        /// 涂布消息通知
        /// </summary>
        public static MessageEventHandler<CoatingDataModel>? CoatingMessageNotificationEvent;

        /// <summary>
        /// 涂布消息通知
        /// </summary>
        public static MessageEventHandler<TBlockDataModel>? TBlockMessageNotificationEvent;

        /// <summary>
        /// CCD消息通知
        /// </summary>
        public static MessageEventHandler<Tuple<string, WeightMessageModel>>? CCDMessageNotificationEvent;

        /// <summary>
        /// 设置消息通知
        /// </summary>
        public static MessageEventHandler<object>? SettingMessageNotificationEvent;
        #endregion

        #region 辅助方法
        #region 膜卷号
        /// <summary>
        /// 获取膜卷号
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>

        public static string GetProductSn(string filePath)
        {
            var content = ReadFileSafely(filePath, Encoding.UTF8);
            var match = Regex.Match(content,
                @"MachineID：*(.+)\r\nMoNumber：*(.+)\r\nPreProductSn：*(.+)\r\nOperatorID：*(.+)\r\nGroupCode：*(.+)\r\nProductSn：*(.+)\r\nModelNum：*(.+)\r\nOperatorNumber：*(.+)\r\n*(.+)");

            return match.Success ? match.Groups[6].Value.Trim() : string.Empty;
        }

        /// <summary>
        /// 安全地读取文件内容
        /// </summary>
        /// <param name="path"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        private static string ReadFileSafely(string path, Encoding encoding)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs, encoding, detectEncodingFromByteOrderMarks: true);
            return sr.ReadToEnd();
        }
        #endregion

        #region Grow主界面弹窗
        public static void ShowGrowSuccess(string message, string caption)
        {
            Growl.Success(new GrowlInfo
            {
                Message = $"{caption}：{message}",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
        }
        public static void ShowGrowInfo(string message, string caption)
        {
            Growl.Info(new GrowlInfo
            {
                Message = $"{caption}：{message}",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
        }
        public static void ShowGrowWarning(string message, string caption)
        {
            Growl.Warning(new GrowlInfo
            {
                Message = $"{caption}：{message}",
                Token = "ProductionMonitor",
                ShowDateTime = true,
                StaysOpen = false,
                WaitTime = 5,
            });
        }
        public static void ShowGrowError(string message, string caption)
        {
            Growl.Error(new GrowlInfo
            {
                Message = $"{caption}：{message}",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
        }
        public static void ShowGrowFatal(string message, string caption)
        {
            Growl.Fatal(new GrowlInfo
            {
                Message = $"{caption}：{message}",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
        }
        public static void ShowGrowAsk(string message, string caption)
        {
            Growl.Ask(new GrowlInfo
            {
                Message = $"{caption}：{message}",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
        }

        /// <summary>
        /// 报警弹窗Grow
        /// </summary>
        /// <param name="record"></param>
        public static void ShowGrowAlarm(AlarmRecordModel record)
        {
            var ac = CCTBCore.AlarmConfigs.FirstOrDefault(o => o.Code == record.Code);
            if (ac == null || !ac.IsEnabled) return;
            if (string.IsNullOrEmpty(ac.Name) || string.IsNullOrEmpty(record.AlarmMessage)) return;

            switch (ac.Level)
            {
                case EAlarmLevel.一般:
                    ShowGrowWarning(record.AlarmMessage, ac.Name);
                    break;
                case EAlarmLevel.中等:
                    ShowGrowError(record.AlarmMessage, ac.Name);
                    break;
                case EAlarmLevel.严重:
                    ShowGrowFatal(record.AlarmMessage, ac.Name);
                    break;
                default: return;
            }
        }
        #endregion
        #endregion
    }
}
