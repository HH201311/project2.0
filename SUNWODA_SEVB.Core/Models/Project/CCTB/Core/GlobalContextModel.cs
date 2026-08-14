using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Services;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RoleModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.Core
{
    /// <summary>
    /// CCTB的全局绑定单例模型
    /// </summary>
    public class GlobalContextModel : ModelBase
    {
        private static readonly Lazy<GlobalContextModel> instance = new(() => new GlobalContextModel());

        /// <summary>
        /// GlobalContextModel的单例对象
        /// </summary>
        public static GlobalContextModel Instance => instance.Value;

        /// <summary>
        /// 构造函数
        /// </summary>
        private GlobalContextModel() { }

        /// <summary>
        /// 构造函数
        /// </summary>
        static GlobalContextModel()
        {
            // 通过服务定位器获取初始化服务
            try
            {
                var _globalContextInitializer = ServiceLocator.GetRequiredService<IGlobalContextInitializer>();

                // 自动初始化
                _ = _globalContextInitializer?.InitializeAsync();
            }
            catch (Exception ex)
            {
                // 如果服务未注册，忽略异常（某些情况下可能不需要初始化）
                System.Diagnostics.Debug.WriteLine($"GlobalContextModel 初始化失败: {ex.Message}");
            }
        }

        #region 用户相关
        private string userAccount = "guest";
        private UserRole? userRole = null;
        private string userName = "***";
        private string userIcon = "*";
        private int userRoleID;

        /// <summary>
        /// 用户角色
        /// </summary>
        public string UserAccount { get => userAccount; set => SetProperty(ref userAccount, value); }

        /// <summary>
        /// 用户角色
        /// </summary>
        public UserRole? UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        /// <summary>
        /// 用户姓名
        /// </summary>
        public string UserName { get => userName; set => SetProperty(ref userName, value); }

        /// <summary>
        /// 用户图标
        /// </summary>
        public string UserIcon { get => userIcon; set => SetProperty(ref userIcon, value); }

        /// <summary>
        /// 角色枚举值
        /// </summary>
        public int UserRoleID { get => userRoleID; set => SetProperty(ref userRoleID, value); }
        #endregion

        #region 项目设定相关
        #region 闭环开关
        private bool closedLoopSwitchMDA, closedLoopSwitchTDA, closedLoopSwitchCCDA, closedLoopSwitchMDB, closedLoopSwitchTDB, closedLoopSwitchCCDB;

        /// <summary>
        /// A面MD闭环开关
        /// </summary>
        public bool ClosedLoopSwitchMDA { get => closedLoopSwitchMDA; set => SetProperty(ref closedLoopSwitchMDA, value); }

        /// <summary>
        /// A面TD闭环开关
        /// </summary>
        public bool ClosedLoopSwitchTDA { get => closedLoopSwitchTDA; set => SetProperty(ref closedLoopSwitchTDA, value); }

        /// <summary>
        /// A面MDTD闭环开关
        /// </summary>
        public bool ClosedLoopSwitchCCDA { get => closedLoopSwitchCCDA; set => SetProperty(ref closedLoopSwitchCCDA, value); }

        /// <summary>
        /// B面MD闭环开关
        /// </summary>
        public bool ClosedLoopSwitchMDB { get => closedLoopSwitchMDB; set => SetProperty(ref closedLoopSwitchMDB, value); }

        /// <summary>
        /// B面tD闭环开关
        /// </summary>
        public bool ClosedLoopSwitchTDB { get => closedLoopSwitchTDB; set => SetProperty(ref closedLoopSwitchTDB, value); }

        /// <summary>
        /// B面MDtd闭环开关
        /// </summary>
        public bool ClosedLoopSwitchCCDB { get => closedLoopSwitchCCDB; set => SetProperty(ref closedLoopSwitchCCDB, value); }
        #endregion

        #region 生产监控-总况-生产信息
        private ProductionInfoModel productionInfo = new();
        /// <summary>
        /// 生产信息
        /// </summary>
        public ProductionInfoModel ProductionInfo { get => productionInfo; set => SetProperty(ref productionInfo, value); }
        #endregion
        #endregion
    }
}
