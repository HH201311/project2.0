using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.ChartControls
{
    public class AxisRenderer : FrameworkElement
    {
        /// <summary>
        /// 属性：决定是画X轴还是Y轴
        /// </summary>
        public EAxisType AxisType { get; set; } = EAxisType.X;
        public double RotationAngle { get; set; } = 0;
        public bool AvoidAxisLabel { get; set; } = true;
        public string? AxisLabel { get; set; }

        // 内部数据 --- 坐标轴用的标签集合
        private List<string>? _labels;

        // 绘图资源缓存
        private Typeface _typeface;
        private Brush _textBrush;

        public AxisRenderer()
        {
            var globalFont = Application.Current.Resources["CCTB.SourceHanSansCN"] as FontFamily;
            _typeface = new Typeface(globalFont, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
            _textBrush = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)); // 半透明白
            _textBrush.Freeze();
        }

        /// <summary>
        /// 更新坐标轴轴数据
        /// </summary>
        public void Update(params string[] labels)
        {
            if (_labels == null) _labels = new List<string>();
            _labels.Clear();
            _labels.AddRange(labels);
            InvalidateVisual(); // 触发 OnRender
        }

        /// <summary>
        /// 更新坐标轴轴数据
        /// </summary>
        public void Update(params double[] labels)
        {
            if (_labels == null) _labels = new List<string>();
            _labels.Clear();
            var l = labels.Select(o => o.ToString()).ToList();
            _labels.AddRange(l);
            InvalidateVisual(); // 触发 OnRender
        }

        /// <summary>
        /// 更新坐标轴轴数据
        /// </summary>
        public void Update(double min, double max, double step = 0.1, byte digital = 2)
        {
            if (step <= 0) step = 0.1;
            _labels = new List<string>();
            for (double i = min; i <= max; i += step) _labels.Add(i.ToString($"F{digital}"));
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (ActualWidth == 0 || ActualHeight == 0) return;

            if (AxisType == EAxisType.X)
            {
                DrawXAxis(drawingContext);
            }
            else
            {
                DrawYAxis(drawingContext);
            }
        }

        /// <summary>
        /// 绘制 X 轴标签
        /// </summary>
        /// <param name="ctx"></param>
        private void DrawXAxis(DrawingContext ctx)
        {
            if (_labels == null || _labels.Count == 0) return;

            var w = ActualWidth;

            #region 绘制X轴标签
            var axisLabeldrawX = 0d;
            if (!string.IsNullOrEmpty(AxisLabel))
            {
                var ft = CreateText(AxisLabel);
                var drawX = w - ft.Width;
                ctx.DrawText(ft, new Point(drawX, 0));
                axisLabeldrawX = drawX;
            }
            #endregion

            #region 回执X轴刻度标签
            var count = _labels.Count;
            var step = w / (count - 1);
            for (int i = 0; i < count; i++)
            {
                var ft = CreateText(_labels[i]);
                var x = i * step;
                var drawX = x - (ft.Width / 2);
                //if (drawX < 0) drawX = 0;
                //if (drawX + ft.Width > w) drawX = w - ft.Width;

                if (!string.IsNullOrEmpty(AxisLabel) && AvoidAxisLabel)
                {
                    // 如果会重叠就取消
                    if ((drawX + ft.Width) > axisLabeldrawX)
                        continue;
                }

                // 应用旋转转换
                ctx.PushTransform(new RotateTransform(RotationAngle, drawX, 0));
                ctx.DrawText(ft, new Point(drawX, 0));
                ctx.Pop();
            }
            #endregion
        }

        /// <summary>
        /// 绘制 Y 轴标签
        /// </summary>
        /// <param name="ctx"></param>
        private void DrawYAxis(DrawingContext ctx)
        {
            if (_labels == null || _labels.Count == 0) return;

            var h = ActualHeight;

            #region 绘制Y轴标签
            var axisLabeldrawY_End = 0d;
            if (!string.IsNullOrEmpty(AxisLabel))
            {
                var ft = CreateText(AxisLabel);
                var drawX = ActualWidth - ft.Width;
                var drawY = 0 - ft.Height / 2;
                ctx.DrawText(ft, new Point(drawX, drawY));
                axisLabeldrawY_End = drawY + ft.Height;
            }
            #endregion

            #region 绘制Y轴刻度标签
            var count = _labels.Count;
            var step = h / (count - 1);
            for (int i = 0; i < count; i++)
            {
                var ft = CreateText(_labels[count - i - 1]);
                var drawX = ActualWidth - ft.Width;
                var y = i * step;
                var drawY = y - (ft.Height / 2);
                //if (drawY < 0) drawY = 0;
                //if (drawY + ft.Height > h) drawY = h - ft.Height;

                if (!string.IsNullOrEmpty(AxisLabel) && AvoidAxisLabel)
                {
                    if (drawY < axisLabeldrawY_End)
                        continue;
                }
                // 应用旋转转换
                ctx.PushTransform(new RotateTransform(RotationAngle, drawX, 0));
                ctx.DrawText(ft, new Point(drawX, drawY));
                ctx.Pop();
            }
            #endregion
        }

        /// <summary>
        /// 辅助方法：创建 FormattedText
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private FormattedText CreateText(string text)
        {
            return new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                12, // 字体大小
                _textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }
    }
}
