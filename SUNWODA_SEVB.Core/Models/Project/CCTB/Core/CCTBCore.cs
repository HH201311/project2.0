using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using System;
using System.Collections.Generic;
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
        public static string? UserAccount;

        /// <summary>
        /// 当前登录用户名称
        /// </summary>
        public static string? UserName;

        /// <summary>
        /// 当前登录用户角色
        /// </summary>
        public static int UserRoleID;

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

        #region 涂布项目全局消息通知
        public delegate void MessageEventHandler<T>(object? sender, MessageEventArgs<T> e) where T : class;

        /// <summary>
        /// 涂布消息通知
        /// </summary>
        public static MessageEventHandler<CoatingMessage>? CoatingMessageNotificationEvent;

        /// <summary>
        /// 涂布消息通知
        /// </summary>
        public static MessageEventHandler<TBlockMessage>? TBlockMessageNotificationEvent;

        /// <summary>
        /// CCD消息通知
        /// </summary>
        public static MessageEventHandler<CCDMessage>? CCDMessageNotificationEvent;

        /// <summary>
        /// 设置消息通知
        /// </summary>
        public static MessageEventHandler<string>? SettingMessageNotificationEvent;

        /// <summary>
        /// CPK文件监控配置更新通知
        /// </summary>
        public static MessageEventHandler<CPKWatcherConfigMessage>? CPKWatcherConfigMessageNotificationEvent;
        #endregion
    }
}
