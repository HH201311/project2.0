using Newtonsoft.Json.Linq;
using NLog.Time;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.ChartControls
{
    /// <summary>
    /// CPKChartControl.xaml 的交互逻辑
    /// </summary>
    public partial class CPKChartControl : UserControl
    {
        public CPKChartControl()
        {
            InitializeComponent();
        }

        public const double CPK1_33 = 1.33;
        public const double CPK1_67 = 1.67;
        public const double CPK2_00 = 2.00;

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            XAxisPainter.RotationAngle = 315;
            XAxisPainter.Update("1", "2", "3", "4", "5", "6", "7", "8", "9", "趟号");
            YAxisPainter.RotationAngle = 0;
            YAxisPainter.Update(0, 4, 0.5);

            (double yMin, double yMax) = RefreshYAxis(0, 0);
            var yRange = yMax - yMin;

            RefreshCPKDashLine(ref cpkDashLine0, CPK2_00, yMin, yRange, 0.9);
            RefreshCPKDashLine(ref cpkDashLine1, CPK1_67, yMin, yRange, 0.6);
            RefreshCPKDashLine(ref cpkDashLine2, CPK1_33, yMin, yRange, 0.3);
        }

        private void ChartGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawChart();
        }

        #region YMin
        public double YMin
        {
            get { return (double)GetValue(YMinProperty); }
            set { SetValue(YMinProperty, value); }
        }

        public static readonly DependencyProperty YMinProperty = DependencyProperty.Register(
            "YMin",
            typeof(double),
            typeof(CPKChartControl),
            new PropertyMetadata(0d, new PropertyChangedCallback(OnYMinChanged))
        );

        private static void OnYMinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CPKChartControl;
            if (uc != null)
            {
                uc.DrawChart();
            }
        }
        #endregion

        #region YMax
        public double YMax
        {
            get { return (double)GetValue(YMaxProperty); }
            set { SetValue(YMaxProperty, value); }
        }

        public static readonly DependencyProperty YMaxProperty = DependencyProperty.Register(
            "YMax",
            typeof(double),
            typeof(CPKChartControl),
            new PropertyMetadata(4d, new PropertyChangedCallback(OnYMaxChanged))
        );

        private static void OnYMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CPKChartControl;
            if (uc != null)
            {
                uc.DrawChart();
            }
        }
        #endregion

        #region YStep
        public double YStep
        {
            get { return (double)GetValue(YStepProperty); }
            set { SetValue(YStepProperty, value); }
        }

        public static readonly DependencyProperty YStepProperty = DependencyProperty.Register(
            "YStep",
            typeof(double),
            typeof(CPKChartControl),
            new PropertyMetadata(0.5d, new PropertyChangedCallback(OnYStepChanged))
        );

        private static void OnYStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CPKChartControl;
            if (uc != null)
            {
                uc.DrawChart();
            }
        }
        #endregion

        #region ItemSource
        public IEnumerable<CCTBCPKModel> ItemSource
        {
            get { return (IEnumerable<CCTBCPKModel>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(
            "ItemSource",
            typeof(IEnumerable<CCTBCPKModel>),
            typeof(CPKChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemSourceChanged))
        );

        private static void OnItemSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CPKChartControl;
            if (uc != null && uc.ItemSource != null)
            {
                // A. 防止内存泄漏：取消订阅旧集合的事件
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= uc.OnCollectionChanged;
                }

                // B. 订阅新集合的事件
                if (e.NewValue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += uc.OnCollectionChanged;
                }

                // C. 数据源变了，立即重绘一次
                uc.DrawChart();
            }
        }
        #endregion

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 当数据增加、删除、重置时，重新绘制
            DrawChart();
        }

        List<Path>? cpkLines;
        List<Ellipse>? cpkPoints;
        Line? cpkDashLine0;
        Line? cpkDashLine1;
        Line? cpkDashLine2;
        private void DrawChart()
        {
            if (ItemSource == null || ItemSource.Count() <= 0) return;

            var points = ItemSource.Select(o => (o.CPKValue ?? 0, o.IsManual ?? false)).ToList();
            var min = points.Select(o => o.Item1).Min();
            var max = points.Select(o => o.Item1).Max();

            var xLabels = ItemSource.Select(o => o.CPKLabel ?? "").ToList();
            RefreshXAxis(xLabels);
            (double yMin, double yMax) = RefreshYAxis(min, max);
            var yRange = yMax - yMin;

            RefreshCPKDashLine(ref cpkDashLine0, CPK2_00, yMin, yRange, 0.9);
            RefreshCPKDashLine(ref cpkDashLine1, CPK1_67, yMin, yRange, 0.6);
            RefreshCPKDashLine(ref cpkDashLine2, CPK1_33, yMin, yRange, 0.3);
            RefreshCPKLine(points, yMin, yRange);
            RefreshCPKPoints(points, yMin, yRange);
        }

        private void RefreshXAxis(List<string> labels)
        {
            XAxisPainter.RotationAngle = 315;
            XAxisPainter.Update(labels.ToArray());
        }

        private (double yMin, double yMax) RefreshYAxis(double vMin, double vMax)
        {
            var range = vMax - vMin;
            double yMin = vMin - range / 5, yMax = vMax + range / 5;
            if (YMax > YMin)
            {
                // 找中线
                var yMiddle = (YMax - YMin) / 2;
                // 中线上范围
                var halfRangeTop = Math.Max(YMax, yMax) - yMiddle;
                // 中线下范围
                var halfRangeButtom = yMiddle - Math.Min(YMin, yMin);
                // 最大半范围
                var halfRangeMax = Math.Max(halfRangeTop, halfRangeButtom);
                yMin = yMiddle - halfRangeMax * 1.2;
                yMax = yMiddle + halfRangeMax * 1.2;
            }

            var yRange = yMax - yMin;
            var yLabelCount = (int)(yRange / YStep);
            var yLabels = new List<string>();
            var yStep = (YStep > 0 && yLabelCount > 2 && yLabelCount < 9) ? YStep : yRange / 8;
            for (var i = yMin; i < yMax; i += yStep)
            {
                yLabels.Add(i.ToString("F2"));
            }
            YAxisPainter.RotationAngle = 0;
            YAxisPainter.Update(yLabels.ToArray());
            return (yMin, yMax);
        }

        private void RefreshCPKDashLine(ref Line? dashLine, double yValue, double yMin, double yRange, double opacity)
        {
            if (dashLine == null)
            {
                var style = this.Resources["CPKDashLineStyle"] as Style;
                dashLine = new Line() { Style = style, Opacity = opacity };
            }

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1;
            var y = Normalize(yValue, yMin, yRange, h);
            dashLine.X1 = 0;
            dashLine.Y1 = y;
            dashLine.X2 = w;
            dashLine.Y2 = y;

            if (!ChartGrid.Children.Contains(dashLine) && dashLine != null)
                ChartGrid.Children.Add(dashLine);
        }

        private readonly Brush manualCPKLineStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCFFFFFF"));
        private readonly Brush normalCPKLineStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF29CC7A"));
        private Style? cpkLinePathStyle;
        private void RefreshCPKLine(List<(double cpkValue, bool isManual)> points, double yMin, double yRange)
        {
            if (cpkLines == null) cpkLines = new List<Path>();
            if (cpkLinePathStyle == null) cpkLinePathStyle = this.Resources["CPKLinePathStyle"] as Style;

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1;
            var stepX = w / (points.Count - 1); // X轴步长
            var partLinePoints = new List<Point>();
            var cpkLineIndex = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var x = i * stepX;
                var y = Normalize(points[i].cpkValue, yMin, yRange, h);
                var point = new Point(x, y);
                partLinePoints.Add(point);
                if (i == 0 || partLinePoints.Count <= 1) continue;
                // 人工/闭环 切换，或最后一个点，则开始绘制一条折线
                if ((points[i - 1].isManual ^ points[i].isManual) || i == points.Count - 1)
                {
                    var geometry = CreatStreamGeometry(partLinePoints);
                    partLinePoints.Clear();
                    Path cpkLine;
                    if (cpkLines.Count <= cpkLineIndex)
                    {
                        cpkLine = new Path() { Style = cpkLinePathStyle };
                        cpkLines.Add(cpkLine);
                    }
                    cpkLine = cpkLines[cpkLineIndex++];
                    cpkLine.Data = geometry;
                    cpkLine.Stroke = points[i - 1].isManual ? manualCPKLineStroke : normalCPKLineStroke;
                    cpkLine.Opacity = points[i - 1].isManual ? 1 : 0.8;
                    i--;
                }
            }

            for (int i = 0; i < cpkLines.Count; i++)
            {
                if (i < cpkLineIndex)
                {
                    if (!ChartGrid.Children.Contains(cpkLines[i]) && cpkLines[i] != null)
                    {
                        ChartGrid.Children.Add(cpkLines[i]);
                    }
                    continue;
                }

                if (ChartGrid.Children.Contains(cpkLines[i]))
                {
                    ChartGrid.Children.Remove(cpkLines[i]);
                }
            }
        }

        private StreamGeometry CreatStreamGeometry(List<Point> points)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                // 移动到起点
                ctx.BeginFigure(points[0], true, false);
                for (var i = 1; i < points.Count; i++)
                {
                    ctx.LineTo(points[i], true, true);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private readonly Brush manualCPKPointStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DEFFFFFF"));
        private readonly Brush normalCPKPointStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC29CC7A"));
        private Style? cpkPointStyle;
        private Style? cpkToolTipStyle;
        private void RefreshCPKPoints(List<(double cpkValue, bool isManual)> points, double yMin, double yRange)
        {
            if (cpkPoints == null) cpkPoints = new List<Ellipse>();
            if (cpkPointStyle == null) cpkPointStyle = this.Resources["CPKPointsEllipseStyle"] as Style;
            if (cpkToolTipStyle == null) cpkToolTipStyle = this.Resources["CPKToolTipStyle"] as Style;
            while (cpkPoints.Count < points.Count)
            {
                var e = new Ellipse() { Style = cpkPointStyle };
                e.ToolTip = new ToolTip { Style = cpkToolTipStyle };
                cpkPoints.Add(e);
            }

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1;
            var stepX = w / (points.Count - 1); // X轴步长
            for (int i = 0; i < points.Count; i++)
            {
                var x = i * stepX;
                var y = Normalize(points[i].cpkValue, yMin, yRange, h);
                // 创建变换组，先设置位置再平移
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(new TranslateTransform(x, y));
                cpkPoints[i].RenderTransform = transformGroup;
                cpkPoints[i].Stroke = points[i].isManual ? manualCPKPointStroke : normalCPKPointStroke;
                var toolTip = cpkPoints[i].ToolTip as ToolTip;
                toolTip!.Content = points[i].cpkValue.ToString();
                //toolTip!.HorizontalOffset = x - toolTip!.ActualWidth / 2;
                //toolTip!.VerticalOffset = y - 5;
            }

            for (int i = 0; i < cpkPoints.Count; i++)
            {
                if (i < points.Count)
                {
                    if (!ChartGrid.Children.Contains(cpkPoints[i]) && cpkPoints[i] != null)
                    {
                        ChartGrid.Children.Add(cpkPoints[i]);
                    }
                    continue;
                }
                if (ChartGrid.Children.Contains(cpkPoints[i]))
                {
                    ChartGrid.Children.Remove(cpkPoints[i]);
                }
            }
        }

        /// <summary>
        /// 坐标转换辅助方法：将数值转换为屏幕 Y 坐标
        /// </summary>
        /// <param name="val"></param>
        /// <param name="min"></param>
        /// <param name="range"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        private double Normalize(double val, double min, double range, double height)
        {
            // 数值越大，Y坐标越小（因为屏幕坐标系原点在左上角）
            return height - ((val - min) / range * height);
        }
    }
}
