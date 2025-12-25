using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SUNWODA_SEVB.Core.Models.Component
{
    /// <summary>
    /// 数据单元格模型
    /// </summary>
    public class CellData : ModelBase
    {
        private string _value = "";
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        private Brush _brush = Brushes.White;
        public Brush Background
        {
            get => _brush;
            set => SetProperty(ref _brush, value);
        }

        private Brush _foreground = Brushes.Black;
        public Brush Foreground
        {
            get => _foreground;
            set => SetProperty(ref _foreground, value);
        }
    }
}
