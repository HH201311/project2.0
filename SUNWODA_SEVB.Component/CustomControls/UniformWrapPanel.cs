using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SUNWODA_SEVB.Component.CustomControls
{
    /// <summary>
    /// 等间距自动换行Panel，支持设置列数
    /// </summary>
    public class UniformWrapPanel : Panel
    {
        #region 依赖属性

        /// <summary>
        /// 列数属性
        /// </summary>
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register("Columns", typeof(int), typeof(UniformWrapPanel),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        /// <summary>
        /// 水平间距属性
        /// </summary>
        public static readonly DependencyProperty HorizontalSpacingProperty =
            DependencyProperty.Register("HorizontalSpacing", typeof(double), typeof(UniformWrapPanel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        /// <summary>
        /// 垂直间距属性
        /// </summary>
        public static readonly DependencyProperty VerticalSpacingProperty =
            DependencyProperty.Register("VerticalSpacing", typeof(double), typeof(UniformWrapPanel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        /// <summary>
        /// 自动换行的参考宽度（当父容器宽度无限时使用）
        /// </summary>
        public static readonly DependencyProperty AutoWrapReferenceWidthProperty =
            DependencyProperty.Register("AutoWrapReferenceWidth", typeof(double), typeof(UniformWrapPanel),
                new FrameworkPropertyMetadata(400.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        #endregion

        #region 属性

        /// <summary>
        /// 列数，0表示自动计算
        /// </summary>
        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        /// <summary>
        /// 水平间距
        /// </summary>
        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        /// <summary>
        /// 垂直间距
        /// </summary>
        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        /// <summary>
        /// 自动换行的参考宽度
        /// </summary>
        public double AutoWrapReferenceWidth
        {
            get => (double)GetValue(AutoWrapReferenceWidthProperty);
            set => SetValue(AutoWrapReferenceWidthProperty, value);
        }

        #endregion

        #region 重写方法

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Children.Count == 0)
                return new Size(0, 0);

            // 获取有效的约束宽度
            double constraintWidth = GetEffectiveConstraintWidth(availableSize);

            // 计算实际列数
            int actualColumns = CalculateActualColumns(constraintWidth);
            if (actualColumns <= 0) actualColumns = 1;

            // 计算每个子元素的可用大小
            double itemWidth = Math.Max(0, (constraintWidth - (actualColumns - 1) * HorizontalSpacing) / actualColumns);
            Size itemSize = new Size(itemWidth, double.PositiveInfinity);

            // 测量所有子元素
            double maxItemHeight = 0;
            foreach (UIElement child in Children)
            {
                child.Measure(itemSize);
                maxItemHeight = Math.Max(maxItemHeight, child.DesiredSize.Height);
            }

            // 计算总行数
            int rows = (int)Math.Ceiling((double)Children.Count / actualColumns);

            // 计算总大小
            double totalWidth = actualColumns * itemWidth + (actualColumns - 1) * HorizontalSpacing;
            double totalHeight = rows * maxItemHeight + (rows - 1) * VerticalSpacing;

            // 如果原始宽度是无穷大，返回计算出的实际宽度
            if (double.IsInfinity(availableSize.Width))
            {
                return new Size(totalWidth, totalHeight);
            }

            return new Size(Math.Min(totalWidth, availableSize.Width), totalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Children.Count == 0)
                return finalSize;

            // 获取有效的约束宽度
            double constraintWidth = GetEffectiveConstraintWidth(finalSize);

            // 计算实际列数
            int actualColumns = CalculateActualColumns(constraintWidth);
            if (actualColumns <= 0) actualColumns = 1;

            // 计算每个子元素的大小
            double itemWidth = Math.Max(0, (constraintWidth - (actualColumns - 1) * HorizontalSpacing) / actualColumns);

            // 重新测量所有子元素以获取准确的高度
            Size itemSize = new Size(itemWidth, double.PositiveInfinity);
            double maxItemHeight = 0;
            foreach (UIElement child in Children)
            {
                child.Measure(itemSize);
                maxItemHeight = Math.Max(maxItemHeight, child.DesiredSize.Height);
            }

            // 排列子元素
            for (int i = 0; i < Children.Count; i++)
            {
                int row = i / actualColumns;
                int col = i % actualColumns;

                double x = col * (itemWidth + HorizontalSpacing);
                double y = row * (maxItemHeight + VerticalSpacing);

                Rect childRect = new Rect(x, y, itemWidth, maxItemHeight);
                Children[i].Arrange(childRect);
            }

            return finalSize;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 获取有效的约束宽度
        /// </summary>
        private double GetEffectiveConstraintWidth(Size availableSize)
        {
            if (!double.IsInfinity(availableSize.Width))
            {
                return availableSize.Width;
            }

            // 当宽度无限时，尝试从父元素或祖先元素获取实际宽度
            double referenceWidth = GetReferenceWidthFromAncestors();

            if (referenceWidth > 0)
            {
                return referenceWidth;
            }

            // 如果无法获取参考宽度，使用设置的参考宽度
            return AutoWrapReferenceWidth;
        }

        /// <summary>
        /// 从祖先元素获取参考宽度
        /// </summary>
        private double GetReferenceWidthFromAncestors()
        {
            DependencyObject current = this;

            while (current != null)
            {
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);

                if (current is FrameworkElement element)
                {
                    // 跳过ScrollViewer，因为它的宽度可能也是无穷大
                    if (current is ScrollViewer)
                        continue;

                    // 获取实际宽度
                    if (element.ActualWidth > 0)
                    {
                        // 减去可能的边距和填充
                        double effectiveWidth = element.ActualWidth;

                        if (element.Margin.Left + element.Margin.Right > 0)
                            effectiveWidth -= (element.Margin.Left + element.Margin.Right);

                        if (element is Control control && control.Padding.Left + control.Padding.Right > 0)
                            effectiveWidth -= (control.Padding.Left + control.Padding.Right);

                        if (effectiveWidth > 100) // 确保宽度合理
                            return effectiveWidth;
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// 计算实际列数
        /// </summary>
        private int CalculateActualColumns(double constraintWidth)
        {
            if (Columns > 0)
                return Columns;

            if (Children.Count == 0)
                return 1;

            // 测量第一个子元素来估算宽度
            Children[0].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double itemWidth = Children[0].DesiredSize.Width;

            if (itemWidth <= 0)
                return Math.Min(Children.Count, 3); // 默认最多3列

            // 计算可以容纳的列数
            int maxColumns = Math.Max(1, (int)((constraintWidth + HorizontalSpacing) / (itemWidth + HorizontalSpacing)));

            // 限制最大列数，避免过多列导致元素太小
            int reasonableMaxColumns = Math.Min(Children.Count, Math.Max(1, Children.Count / 2 + 1));

            return Math.Min(maxColumns, reasonableMaxColumns);
        }

        #endregion
    }
}
