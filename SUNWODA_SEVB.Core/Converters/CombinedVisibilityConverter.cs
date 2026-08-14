using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters
{
    /// <summary>
    /// 组合多个可见性转换结果
    /// 默认逻辑：所有子条件都返回 Visible 时，最终才返回 Visible
    /// </summary>
    public class CombinedVisibilityConverter : IMultiValueConverter
    {
        /// <summary>
        /// 从 true 到 false，只要有一个不可见就整体不可见
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // 如果没有绑定值，默认隐藏
            if (values == null || values.Length == 0) return Visibility.Collapsed;

            // 参数控制逻辑类型：不传或传 "AND" 表示所有条件都要满足
            // 传 "OR" 表示任一条件满足即可
            var paramStr = parameter?.ToString()??string.Empty;
            var isOrLogic = !string.IsNullOrEmpty(paramStr) && paramStr.Equals("OR", StringComparison.OrdinalIgnoreCase);

            foreach (var value in values)
            {
                // 处理可能返回的 Visibility 类型
                Visibility visibility;
                if (value is Visibility vis)
                {
                    visibility = vis;
                }
                else if (value is bool boolValue)
                {
                    visibility = boolValue ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    // 非预期类型，按隐藏处理（根据逻辑决定）
                    visibility = Visibility.Collapsed;
                }

                if (isOrLogic)
                {
                    // OR 逻辑：任一可见就整体可见
                    if (visibility == Visibility.Visible) return Visibility.Visible;
                }
                else
                {
                    // AND 逻辑：任一不可见就整体不可见
                    if (visibility != Visibility.Visible) return Visibility.Collapsed;
                }
            }

            // AND 逻辑走到这里说明都可见；OR 逻辑走到这里说明都不可见
            return isOrLogic ? Visibility.Collapsed : Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("CombinedVisibilityConverter 不支持反向转换");
        }
    }
}