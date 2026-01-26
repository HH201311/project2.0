using SUNWODA_SEVB.Core.Common;
using System.Windows.Controls;
using System.Xml.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.PTO
{

    //当 OverlapAuthority.NoEditableByAdministrator = true 时：DataGrid 全局只读，所有单元格（包括文本列、复选框列）都无法进入编辑状态；
    //当 OverlapAuthority.NoEditableByAdministrator = false 时：DataGrid 可编辑（但 “启用” 列的复选框因自身IsEnabled= False仍无法操作）。
    public class OverlapAuthority
    {
        /// <summary>
        /// 标准按钮权限（适配项目原有逻辑）
        /// </summary>
        public bool StandardAuthorityButton => true;

        /// <summary>
        /// 是否禁止管理员编辑（适配项目原有逻辑）
        /// </summary>
        public bool NoEditableByAdministrator => false;
    }

    /// <summary>
    /// 缺陷参数模型（防栈溢出+双向绑定）
    /// </summary>
    public class DefectParameter : ModelBase
    {
        private string? _defectName;
        private double _length;
        private double _width;
        private double _height;
        private double _area;
        private double _credibility;
        private bool _isEnable;

        public string? DefectName
        {
            get => _defectName;
            set => SetProperty(ref _defectName, value);
        }
        public double Length
        {
            get => _length;
            set
            {
                if (SetProperty(ref _length, Math.Max(0, value)))
                {
                    CalculateArea(); // 仅值变化时计算面积
                }
            }
        }

        public double Width
        {
            get => _width;
            set
            {
                if (SetProperty(ref _width, Math.Max(0, value)))
                {
                    CalculateArea();
                }
            }
        }

        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public double Area
        {
            get => _area;
            set => SetProperty(ref _area, value);
        }

        public double Credibility
        {
            get => _credibility;
            set => SetProperty(ref _credibility, value);
        }

        public bool IsEnable
        {
            get => _isEnable;
            set => SetProperty(ref _isEnable, value);
        }

        private void CalculateArea()
        {
            Area = Length * Width;
        }
    }
}
