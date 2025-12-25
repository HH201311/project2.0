using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SUNWODA_SEVB.Component.CustomControls
{
    /// <summary>
    /// 自定义等间距面板
    /// </summary>
    public class EqualSpacingPanel : Panel
    {
        // 布局方向属性
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("Orientation", typeof(Orientation), typeof(EqualSpacingPanel),
                new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public Orientation Orientation
        {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        // 间距属性
        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register("Spacing", typeof(double), typeof(EqualSpacingPanel),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double Spacing
        {
            get { return (double)GetValue(SpacingProperty); }
            set { SetValue(SpacingProperty, value); }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Size childConstraint = availableSize;
            double maxChildDesiredWidth = 0.0;
            double maxChildDesiredHeight = 0.0;

            // 测量所有子元素
            for (int i = 0; i < Children.Count; i++)
            {
                UIElement child = Children[i];
                if (child != null)
                {
                    child.Measure(childConstraint);
                    Size childDesiredSize = child.DesiredSize;
                    if (maxChildDesiredWidth < childDesiredSize.Width)
                        maxChildDesiredWidth = childDesiredSize.Width;
                    if (maxChildDesiredHeight < childDesiredSize.Height)
                        maxChildDesiredHeight = childDesiredSize.Height;
                }
            }

            // 计算所需大小
            if (Orientation == Orientation.Horizontal)
            {
                double totalWidth = maxChildDesiredWidth * Children.Count;
                if (Children.Count > 1)
                    totalWidth += Spacing * (Children.Count - 1);
                return new Size(totalWidth, maxChildDesiredHeight);
            }
            else
            {
                double totalHeight = maxChildDesiredHeight * Children.Count;
                if (Children.Count > 1)
                    totalHeight += Spacing * (Children.Count - 1);
                return new Size(maxChildDesiredWidth, totalHeight);
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Children.Count == 0)
                return finalSize;

            if (Orientation == Orientation.Horizontal)
            {
                // 计算每个子元素的宽度和间距
                double totalSpacing = (Children.Count - 1) * Spacing;
                double availableWidth = finalSize.Width - totalSpacing;
                double childWidth = availableWidth / Children.Count;

                double x = 0;
                for (int i = 0; i < Children.Count; i++)
                {
                    UIElement child = Children[i];
                    if (child != null)
                    {
                        child.Arrange(new Rect(x, 0, childWidth, finalSize.Height));
                        x += childWidth + Spacing;
                    }
                }
            }
            else
            {
                // 计算每个子元素的高度和间距
                double totalSpacing = (Children.Count - 1) * Spacing;
                double availableHeight = finalSize.Height - totalSpacing;
                double childHeight = availableHeight / Children.Count;

                double y = 0;
                for (int i = 0; i < Children.Count; i++)
                {
                    UIElement child = Children[i];
                    if (child != null)
                    {
                        child.Arrange(new Rect(0, y, finalSize.Width, childHeight));
                        y += childHeight + Spacing;
                    }
                }
            }

            return finalSize;
        }
    }
}
