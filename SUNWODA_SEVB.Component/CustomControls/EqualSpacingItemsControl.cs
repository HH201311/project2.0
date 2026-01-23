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
    /// 自定义等间距ItemsControl
    /// </summary>
    public class EqualSpacingItemsControl : ItemsControl
    {
        // 布局方向属性
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("Orientation", typeof(Orientation), typeof(EqualSpacingItemsControl),
                new PropertyMetadata(Orientation.Horizontal));

        public Orientation Orientation
        {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        // 间距属性
        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register("Spacing", typeof(double), typeof(EqualSpacingItemsControl),
                new PropertyMetadata(10.0));

        public double Spacing
        {
            get { return (double)GetValue(SpacingProperty); }
            set { SetValue(SpacingProperty, value); }
        }

        static EqualSpacingItemsControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(EqualSpacingItemsControl),
                new FrameworkPropertyMetadata(typeof(EqualSpacingItemsControl)));
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ContentPresenter();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is ContentPresenter;
        }
    }
}
