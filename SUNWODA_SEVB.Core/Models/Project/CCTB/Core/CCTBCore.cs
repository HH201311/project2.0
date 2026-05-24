using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SUNWODA_SEVB.Core.Models.Project.CCTB.Core.CCTBCore;

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
