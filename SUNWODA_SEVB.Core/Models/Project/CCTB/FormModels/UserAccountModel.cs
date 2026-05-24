using SUNWODA_SEVB.Core.Enumerations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels
{
    /// <summary>
    /// 用户账户模型
    /// </summary>
    public class UserAccountModel : UserManagementModel
    {
        private bool isChecked;
        private UserRole userRole;

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsChecked
        {
            get => isChecked;
            set => SetProperty(ref isChecked, value);
        }

        /// <summary>
        /// 用户角色
        /// </summary>
        public UserRole UserRole
        {
            get => userRole;
            set => SetProperty(ref userRole, value);
        }
    }
}
