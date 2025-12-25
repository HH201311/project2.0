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
    /// 等间距自动换行ItemsControl，支持数据绑定和模板
    /// </summary>
    public class UniformWrapItemsControl : ItemsControl
    {
        #region 静态构造函数
        static UniformWrapItemsControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UniformWrapItemsControl),
                new FrameworkPropertyMetadata(typeof(UniformWrapItemsControl)));
        }
        #endregion

        #region 依赖属性

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register("Columns", typeof(int), typeof(UniformWrapItemsControl),
                new FrameworkPropertyMetadata(0, OnLayoutPropertyChanged));

        public static readonly DependencyProperty HorizontalSpacingProperty =
            DependencyProperty.Register("HorizontalSpacing", typeof(double), typeof(UniformWrapItemsControl),
                new FrameworkPropertyMetadata(10.0, OnLayoutPropertyChanged));

        public static readonly DependencyProperty VerticalSpacingProperty =
            DependencyProperty.Register("VerticalSpacing", typeof(double), typeof(UniformWrapItemsControl),
                new FrameworkPropertyMetadata(10.0, OnLayoutPropertyChanged));

        public static readonly DependencyProperty AutoWrapReferenceWidthProperty =
            DependencyProperty.Register("AutoWrapReferenceWidth", typeof(double), typeof(UniformWrapItemsControl),
                new FrameworkPropertyMetadata(400.0));

        #endregion

        #region 属性

        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        public double AutoWrapReferenceWidth
        {
            get => (double)GetValue(AutoWrapReferenceWidthProperty);
            set => SetValue(AutoWrapReferenceWidthProperty, value);
        }

        #endregion

        #region 重写方法

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ContentPresenter();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is ContentPresenter;
        }

        #endregion

        #region 私有方法

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UniformWrapItemsControl control)
            {
                control.InvalidateVisual();
                control.UpdateLayout();
            }
        }

        #endregion
    }
}
