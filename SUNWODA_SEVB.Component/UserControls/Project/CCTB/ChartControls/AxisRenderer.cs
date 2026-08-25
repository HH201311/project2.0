using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static SUNWODA_SEVB.Component.UserControls.Project.CCTB.ChartControls.AxisRenderer;

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.ChartControls
{
    /// <summary>
    /// 坐标轴绘制
    /// </summary>
    public class AxisRenderer : FrameworkElement
    {
        /// <summary>
        /// 属性：决定是画X轴还是Y轴
        /// </summary>
        public EAxisType AxisType { get; set; } = EAxisType.X;
        public double RotationAngle { get; set; } = 0;
        public bool IsReverse { get; set; } = false;
        public bool AvoidAxisLabel { get; set; } = true;
        public string AxisLabel { get; set; } = string.Empty;

        // 内部数据 --- 坐标轴用的标签集合
        private List<string> _axisLabels = [];
        private List<double> _axisValues = [];
        private double _min, _max;
        private EAxisDrawType _axisDrawType = EAxisDrawType.Label;

        // 绘图资源缓存
        private Typeface _typeface;
        private Brush _textBrush;

        public AxisRenderer()
        {
            var globalFont = Application.Current.Resources["CCTB.SourceHanSansCN-Medium"] as FontFamily;
            _typeface = new Typeface(globalFont, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
            _textBrush = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)); // 半透明白
            _textBrush.Freeze();
        }

        /// <summary>
        /// 更新坐标轴轴数据
        /// </summary>
        public void Update(params string[] labels)
        {
            _axisLabels.Clear();
            _axisLabels.AddRange(labels);
            _axisDrawType = EAxisDrawType.Label;
            InvalidateVisual(); // 触发 OnRender
        }

        /// <summary>
        /// 更新坐标轴轴数据
        /// </summary>
        public void Update(double min, double max, params double[] values)
        {
            _axisValues.Clear();
            _axisValues.AddRange(values);
            _min = min;
            _max = max;
            _axisDrawType = EAxisDrawType.Value;
            InvalidateVisual(); // 触发 OnRender
        }

        /// <summary>
        /// 更新坐标轴轴数据
        /// </summary>
        public void Update(params double[] labels)
        {
            _axisLabels.Clear();
            var ls = labels.Select(o => o.ToString()).ToList();
            _axisLabels.AddRange(ls);
            _axisDrawType = EAxisDrawType.Label;
            InvalidateVisual(); // 触发 OnRender
        }

        /// <summary>
        /// 更新坐标轴轴数据
        /// </summary>
        public void Update(double min, double max, double step = 0.1, byte digital = 2)
        {
            if (step <= 0) step = 0.1;
            _axisLabels = new List<string>();
            for (double i = min; i <= max; i += step)
                _axisLabels.Add(i.ToString($"F{digital}"));
            _axisDrawType = EAxisDrawType.Label;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (ActualWidth == 0 || ActualHeight == 0) return;

            switch (AxisType, _axisDrawType)
            {
                case (EAxisType.X, EAxisDrawType.Label):
                    {
                        DrawXAxis(drawingContext, _axisLabels);
                        break;
                    }
                case (EAxisType.X, EAxisDrawType.Value):
                    {
                        DrawXAxis(drawingContext, _min, _max, _axisValues);
                        break;
                    }
                case (EAxisType.Y, EAxisDrawType.Label):
                    {
                        DrawYAxis(drawingContext, _axisLabels);
                        break;
                    }
                case (EAxisType.Y, EAxisDrawType.Value):
                    {
                        DrawYAxis(drawingContext, _min, _max, _axisValues);
                        break;
                    }
            }
        }

        /// <summary>
        /// 绘制 X 轴标签
        /// </summary>
        /// <param name="ctx"></param>
        private void DrawXAxis(DrawingContext ctx, List<string> axisLabels)
        {
            if (axisLabels == null || axisLabels.Count == 0) return;

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
            var count = axisLabels.Count;
            var step = w / (count - 1);
            for (int i = 0; i < count; i++)
            {
                var ft = CreateText(axisLabels[i]);
                var x = i * step;
                var drawX = x;
                if (IsReverse) drawX = w - drawX;
                drawX = drawX - (ft.Width / 2);

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
        /// 绘制 X 轴标签
        /// </summary>
        /// <param name="ctx"></param>
        private void DrawXAxis(DrawingContext ctx, double min, double max, List<double> axisValues)
        {
            if (axisValues == null || axisValues.Count == 0) return;

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

            #region 绘制X轴刻度标签
            if (min >= max) throw new ArgumentException("最大最小值异常！");
            var range = max - min;
            foreach (var axisValue in axisValues)
            {
                if (axisValue < min || axisValue > max) continue;

                var ft = CreateText(axisValue.ToString());
                var drawX = GraphicHelper.Normalize(axisValue, min, range, w, true);
                if (IsReverse) drawX = w - drawX;
                drawX = drawX - (ft.Width / 2);

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
        private void DrawYAxis(DrawingContext ctx, List<string> axisLabels)
        {
            if (axisLabels == null || axisLabels.Count == 0) return;

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
            var count = axisLabels.Count;
            var step = h / (count - 1);
            for (int i = 0; i < count; i++)
            {
                var ft = CreateText(axisLabels[count - i - 1]);
                var drawX = ActualWidth - ft.Width;
                var y = i * step;
                var drawY = y;
                if (IsReverse) drawY = h - drawY;
                drawY = drawY - (ft.Height / 2);

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
        /// 绘制 Y 轴标签
        /// </summary>
        /// <param name="ctx"></param>
        private void DrawYAxis(DrawingContext ctx, double min, double max, List<double> axisValues)
        {
            if (axisValues == null || axisValues.Count == 0) return;

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
            if (min >= max) throw new ArgumentException("最大最小值异常！");
            var range = max - min;
            foreach (var axisValue in axisValues)
            {
                if (axisValue < min || axisValue > max) continue;

                var ft = CreateText(axisValue.ToString());
                var drawX = ActualWidth - ft.Width;
                var drawY = GraphicHelper.Normalize(axisValue, min, range, h, false);
                if (IsReverse) drawY = h - drawY;
                drawY = drawY - (ft.Height / 2);

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

        #region 内部类
        public enum EAxisType
        {
            X, Y
        }

        private enum EAxisDrawType
        {
            Label, Value
        }
        #endregion
    }
}
