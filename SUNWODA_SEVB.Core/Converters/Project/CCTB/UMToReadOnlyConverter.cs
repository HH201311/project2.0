using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RoleModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters.Project.CCTB
{
    /// <summary>
    /// 用户权限转只读状态转换器
    /// 根据用户角色和控件编号，判断控件是否为只读状态
    /// 绑定值：UserRole（当前用户角色）
    /// 参数：控件编号（如"RM001"）
    /// 返回值：true=只读，false=可编辑
    /// </summary>
    public class UMToReadOnlyConverter : IValueConverter
    {
        /// <summary>
        /// 将用户角色转换为只读状态
        /// </summary>
        /// <param name="value">绑定的值（UserRole类型）</param>
        /// <param name="targetType">目标类型（bool）</param>
        /// <param name="parameter">控件编号（如"RM001"）</param>
        /// <param name="culture">区域信息</param>
        /// <returns>true=只读，false=可编辑</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 参数验证：默认只读，安全优先
            if (value is not UserRole role) return true;

            if (parameter == null) return false;

            var codeStr = parameter.ToString();
            if (string.IsNullOrWhiteSpace(codeStr)) return false;

            // 解析控件编号列表
            var codes = codeStr.Split(',').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();

            if (codes.Count == 0) return false;

            // 获取全局配置
            var rms = CCTBCore.RoleManagementConfigs;
            if (rms == null || rms.Count == 0) return false;

            // 遍历所有控件编号，任一满足权限即返回启用
            foreach (var code in codes)
            {
                var roleModel = rms.FirstOrDefault(o => o.Code == code);

                if (roleModel == null) continue;

                // 根据角色获取对应权限
                var permission = GetPermissionByRole(roleModel, role);

                // 如果有编辑权限，则返回启用
                if (IsEditPermission(permission)) return false;
            }

            // 所有控件都不满足权限，返回只读
            return true;
        }

        /// <summary>
        /// 根据用户角色获取对应的权限
        /// </summary>
        private EUserPermission GetPermissionByRole(RoleManagementModel model, UserRole role)
        {
            return role switch
            {
                UserRole.Guest => model.GuestPermission,
                UserRole.Engineer => model.EngineerPermission,
                UserRole.Admin => model.AdminPermission,
                UserRole.SuperAdmin => model.SadminPermission,
                _ => EUserPermission.NoPermission
            };
        }

        /// <summary>
        /// 判断是否有编辑权限
        /// </summary>
        private bool IsEditPermission(EUserPermission permission)
        {
            return permission == EUserPermission.ReadAndEdit;
        }

        /// <summary>
        /// 将 bool 转换回值（通常不实现，因为不可逆）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
