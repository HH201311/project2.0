using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft;
using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.RoleModels
{
    /// <summary>
    /// 角色权限管理模型
    /// </summary>
    public class RoleManagementModel : ModelBase
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public RoleManagementModel() { }

        /// <summary>
        /// 构造函数
        /// </summary>
        public RoleManagementModel(string? code, string? name,
            EUserPermission guestPermission = EUserPermission.NoPermission,
            EUserPermission engineerPermission = EUserPermission.ReadOnly,
            EUserPermission adminPermission = EUserPermission.ReadAndEdit)
        {
            this.code = code;
            this.name = name;
            this.guestPermission = guestPermission;
            this.engineerPermission = engineerPermission;
            this.adminPermission = adminPermission;
        }

        private string? code;
        private string? name;
        private EUserPermission guestPermission = EUserPermission.NoPermission;
        private EUserPermission engineerPermission = EUserPermission.ReadOnly;
        private EUserPermission adminPermission = EUserPermission.ReadAndEdit;
        private EUserPermission sadminPermission = EUserPermission.ReadAndEdit;

        /// <summary>
        /// 管理模块标识符
        /// </summary>
        public string? Code { get => code; set => SetProperty(ref code, value); }

        /// <summary>
        /// 管理模块名称
        /// </summary>
        [JsonIgnore]
        public string? Name { get => name; set => SetProperty(ref name, value); }

        /// <summary>
        /// 访客权限
        /// </summary>
        public EUserPermission GuestPermission { get => guestPermission; set => SetProperty(ref guestPermission, value); }

        /// <summary>
        /// 工程师权限
        /// </summary>
        public EUserPermission EngineerPermission { get => engineerPermission; set => SetProperty(ref engineerPermission, value); }

        /// <summary>
        /// 管理员权限
        /// </summary>
        public EUserPermission AdminPermission { get => adminPermission; set => SetProperty(ref adminPermission, value); }

        /// <summary>
        /// 超级管理员权限
        /// </summary>
        [JsonIgnore]
        public EUserPermission SadminPermission { get => sadminPermission; private set => SetProperty(ref sadminPermission, value); }
    }
}
