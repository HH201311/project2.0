using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace SUNWODA_SEVB.Component.UserControls.Project.JRCW.CustomControls
{
    /// <summary>
    /// 支持剔除Visibility不为Visible的内容的宽高布局
    /// </summary>
    public class CustomUniformGrid : UniformGrid
    {
        private (int cols, int rows) GetGridSize(int count)
        {
            int c = Columns, r = Rows;
            if (c == 0 && r == 0) { c = (int)Math.Ceiling(Math.Sqrt(count)); r = (int)Math.Ceiling((double)count / c); }
            else if (c == 0) c = (int)Math.Ceiling((double)count / r);
            else if (r == 0) r = (int)Math.Ceiling((double)count / c);
            return (c, r);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var visible = Children.Cast<UIElement>().Where(x => x.Visibility == Visibility.Visible).ToList();
            if (visible.Count == 0) return new Size(0, 0);

            var (cols, rows) = GetGridSize(visible.Count);
            var cell = new Size(constraint.Width / cols, constraint.Height / rows);

            double w = 0, h = 0;
            foreach (var child in visible) { child.Measure(cell); w = Math.Max(w, child.DesiredSize.Width); h = Math.Max(h, child.DesiredSize.Height); }

            return new Size(w * cols, h * rows);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var visible = Children.Cast<UIElement>().Where(x => x.Visibility == Visibility.Visible).ToList();
            if (visible.Count == 0) return arrangeSize;

            var (cols, rows) = GetGridSize(visible.Count);
            double cw = arrangeSize.Width / cols, ch = arrangeSize.Height / rows;

            for (int i = 0; i < visible.Count; i++)
            {
                int pos = i + FirstColumn;
                visible[i].Arrange(new Rect((pos % cols) * cw, (pos / cols) * ch, cw, ch));
            }

            return arrangeSize;
        }
    }
}
