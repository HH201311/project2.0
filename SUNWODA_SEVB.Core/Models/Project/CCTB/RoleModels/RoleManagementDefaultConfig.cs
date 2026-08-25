using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.RoleModels
{
    /// <summary>
    /// 角色权限管理默认配置类，包含系统中所有角色权限管理的默认信息配置
    /// </summary>
    public class RoleManagementDefaultConfig
    {
        public const string RM001 = "RM001";
        public const string RM002 = "RM002";
        public const string RM003 = "RM003";
        public const string RM004 = "RM004";
        public const string RM005 = "RM005";
        public const string RM006 = "RM006";
        public const string RM007 = "RM007";
        public const string RM008 = "RM008";

        /// <summary>
        /// 默认的角色权限管理配置
        /// </summary>
        public static readonly Dictionary<string, RoleManagementModel> Configs = new()
        {
            [RM001] = new RoleManagementModel(RM001, "涂布控制系统-状态栏"),
            [RM002] = new RoleManagementModel(RM002, "生产监控-面密度-图表数据配置"),
            [RM003] = new RoleManagementModel(RM003, "系统设置-闭环设置-A/B面"),
            [RM004] = new RoleManagementModel(RM004, "系统设置-功能设置-基础功能"),
            [RM005] = new RoleManagementModel(RM005, "系统设置-功能设置-特殊功能"),
            [RM006] = new RoleManagementModel(RM006, "系统设置-报警设置"),
            [RM007] = new RoleManagementModel(RM007, "系统设置-通讯设置"),
            [RM008] = new RoleManagementModel(RM008, "系统记录-调机记录"),
        };
    }
}
