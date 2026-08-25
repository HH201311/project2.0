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
using System.Windows;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters.Project.CCTB
{
    /// <summary>
    /// 用户角色权限到可见性的转换器
    /// 根据用户角色和控件编号，判断控件是否可见
    /// 绑定值：UserRole（当前用户角色）
    /// 参数：控件编号（多个用逗号分隔，任一满足权限即返回可见）
    /// </summary>
    public class UMToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 将用户角色转换为控件可见性
        /// </summary>
        /// <param name="value">绑定的值（UserRole类型）</param>
        /// <param name="targetType">目标类型（Visibility）</param>
        /// <param name="parameter">控件编号，多个用逗号分隔（如"RM001,RM002"）</param>
        /// <param name="culture">区域信息</param>
        /// <returns>Visibility.Visible 或 Visibility.Collapsed</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 参数验证
            if (value is not UserRole role) return Visibility.Collapsed;

            if (parameter == null) return Visibility.Collapsed;

            var codeStr = parameter.ToString();
            if (string.IsNullOrWhiteSpace(codeStr)) return Visibility.Collapsed;

            // 解析控件编号列表
            var codes = codeStr.Split(',').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();

            if (codes.Count == 0) return Visibility.Collapsed;

            // 获取全局配置
            var rms = CCTBCore.RoleManagementConfigs;
            if (rms == null || rms.Count == 0) return Visibility.Collapsed;

            // 遍历所有控件编号，任一满足权限即返回可见
            foreach (var code in codes)
            {
                var roleModel = rms.FirstOrDefault(o => o.Code == code);

                if (roleModel == null) continue;

                // 根据角色获取对应权限
                var permission = GetPermissionByRole(roleModel, role);

                // 如果权限满足要求（有查看或编辑权限），则返回可见
                if (HasAccessPermission(permission)) return Visibility.Visible;
            }

            // 所有控件都不满足权限，返回隐藏
            return Visibility.Collapsed;
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
        /// 判断是否有访问权限（读取或编辑）
        /// </summary>
        private bool HasAccessPermission(EUserPermission permission)
        {
            return permission == EUserPermission.ReadOnly ||
                   permission == EUserPermission.ReadAndEdit;
        }

        /// <summary>
        /// 将 Visibility 转换回值（通常不实现，因为不可逆）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
