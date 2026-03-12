using Newtonsoft.Json.Linq;
using NLog.Time;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
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
            InitializeAxisAndSubLine();
        }

        public const double CPK1_33 = 1.33;
        public const double CPK1_67 = 1.67;
        public const double CPK2_00 = 2.00;

        private const double yStep = 0.5;

        #region 事件 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChartGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawChart();
        }
        #endregion

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

        #region ItemSource
        public IEnumerable<CPKModel> ItemsSource
        {
            get { return (IEnumerable<CPKModel>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<CPKModel>),
            typeof(CPKChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemsSourceChanged))
        );

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CPKChartControl;
            if (uc != null && uc.ItemsSource != null)
            {
                // A. 防止内存泄漏：取消订阅旧集合的事件
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= uc.OnItemsSourcePropertyChanged;
                }

                // B. 订阅新集合的事件
                if (e.NewValue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += uc.OnItemsSourcePropertyChanged;
                }

                // C. 数据源变了，立即重绘一次
                uc.DrawChart();
            }
        }
        #endregion

        private void OnItemsSourcePropertyChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 当数据增加、删除、重置时，重新绘制
            DrawChart();
        }

        Line subLine0 = new(), subLine1 = new(), subLine2 = new();
        List<Path> lines = [];
        List<Ellipse> points = [];

        private void InitializeAxisAndSubLine()
        {
            XAxisPainter.RotationAngle = 315;
            XAxisPainter.Update("1", "2", "3", "4", "5", "6", "7", "8", "9", "趟号");
            YAxisPainter.RotationAngle = 0;
            YAxisPainter.Update(0, 4, 0.5);

            (double yMin, double yMax) = RefreshYAxis();
            var yRange = yMax - yMin;

            RefreshSubLine(ref subLine0, CPK2_00, yMin, yRange, 0.9);
            RefreshSubLine(ref subLine1, CPK1_67, yMin, yRange, 0.6);
            RefreshSubLine(ref subLine2, CPK1_33, yMin, yRange, 0.3);
        }

        private void DrawChart()
        {
            if (ItemsSource == null || ItemsSource.Count() <= 0)
            {
                InitializeAxisAndSubLine();
                return;
            }

            var points = ItemsSource.Select(o => (o.CPKValue, o.IsManual)).ToList();
            var min = points.Select(o => o.Item1).Min();
            var max = points.Select(o => o.Item1).Max();

            var xLabels = ItemsSource.Select(o => o.DisplayName ?? "").ToList();
            RefreshXAxis(xLabels);
            (double yMin, double yMax) = RefreshYAxis(min, max);
            var yRange = yMax - yMin;

            RefreshSubLine(ref subLine0, CPK2_00, yMin, yRange, 0.9);
            RefreshSubLine(ref subLine1, CPK1_67, yMin, yRange, 0.6);
            RefreshSubLine(ref subLine2, CPK1_33, yMin, yRange, 0.3);
            RefreshLine(points, yMin, yRange);
            RefreshPoints(points, yMin, yRange);
        }

        private void RefreshXAxis(List<string> labels)
        {
            XAxisPainter.RotationAngle = 315;
            XAxisPainter.Update(labels.ToArray());
        }

        private (double yMin, double yMax) RefreshYAxis(params double[] values)
        {
            var vMin = values?.Count() > 0 ? values.Min() : 0;
            var vMax = values?.Count() > 0 ? values.Max() : 0;
            var range = vMax - vMin;
            double yMin = vMin - range / 5, yMax = vMax + range / 5;
            if (YMax > YMin)
            {
                yMin = YMin; yMax = YMax;
                if (values?.Length > 0)
                {
                    while (yMin >= vMin) yMin -= yStep;
                    while (yMax <= vMax) yMax += yStep;
                }
            }

            var yRange = yMax - yMin;
            var yLabels = new List<string>();
            for (var i = yMin; i < yMax; i += yStep)
            {
                yLabels.Add(i.ToString("F1"));
            }
            YAxisPainter.RotationAngle = 0;
            YAxisPainter.Update(yLabels.ToArray());
            return (yMin, yMax);
        }

        #region 虚线
        private Style? subLineStyle;

        private void RefreshSubLine(ref Line subLine, double yValue, double yMin, double yRange, double opacity)
        {
            if (subLineStyle == null) subLineStyle = this.Resources["SubLineStyle"] as Style;
            subLine.Style = subLineStyle;
            subLine.Opacity = opacity;
            Panel.SetZIndex(subLine, 1);

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1;
            var y = GraphicHelper.Normalize(yValue, yMin, yRange, h);
            subLine.X1 = 0;
            subLine.Y1 = y;
            subLine.X2 = w;
            subLine.Y2 = y;

            if (!ChartGrid.Children.Contains(subLine) && subLine != null)
                ChartGrid.Children.Add(subLine);
        }
        #endregion

        #region 折线
        private readonly Brush manualCPKLineStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCFFFFFF"));
        private readonly Brush normalCPKLineStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF29CC7A"));
        private Style? linePathStyle;
        private void RefreshLine(List<(double cpkValue, bool isManual)> points, double yMin, double yRange)
        {
            if (linePathStyle == null) linePathStyle = this.Resources["LinePathStyle"] as Style;

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1;
            var stepX = w / (points.Count - 1); // X轴步长
            var partLinePoints = new List<Point>();
            var lineIndex = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var x = i * stepX;
                var y = GraphicHelper.Normalize(points[i].cpkValue, yMin, yRange, h);
                var point = new Point(x, y);
                partLinePoints.Add(point);
                if (i == 0 || partLinePoints.Count <= 1) continue;
                // 人工/闭环 切换，或最后一个点，则开始绘制一条折线
                if ((points[i - 1].isManual ^ points[i].isManual) || i == points.Count - 1)
                {
                    var geometry = GraphicHelper.CreatStreamGeometry(partLinePoints);
                    partLinePoints.Clear();
                    Path line;
                    if (lines.Count <= lineIndex)
                    {
                        line = new Path { Style = linePathStyle };
                        Panel.SetZIndex(line, 9);
                        lines.Add(line);
                        ChartGrid.Children.Add(line);
                    }
                    line = lines[lineIndex++];
                    line.Data = geometry;
                    line.Stroke = points[i - 1].isManual ? manualCPKLineStroke : normalCPKLineStroke;
                    line.Opacity = points[i - 1].isManual ? 1 : 0.8;
                    i--;
                }
            }

            while (lines.Count > lineIndex)
            {
                var line = lines[lines.Count - 1];
                if (ChartGrid.Children.Contains(line)) ChartGrid.Children.Remove(line);
                lines.Remove(line);
            }
        }
        #endregion

        #region 点线
        private readonly Brush manualCPKPointStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DEFFFFFF"));
        private readonly Brush normalCPKPointStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC29CC7A"));
        private Style? pointStyle;
        private Style? chartToolTipStyle;
        private void RefreshPoints(List<(double cpkValue, bool isManual)> datas, double yMin, double yRange)
        {
            if (pointStyle == null) pointStyle = this.Resources["PointsEllipseStyle"] as Style;
            if (chartToolTipStyle == null) chartToolTipStyle = this.Resources["ChartToolTipStyle"] as Style;

            while (points.Count > datas.Count)
            {
                var point = points[points.Count - 1];
                if (ChartGrid.Children.Contains(point)) ChartGrid.Children.Remove(point);
                points.Remove(point);
            }
            while (points.Count < datas.Count)
            {
                var e = new Ellipse { Style = pointStyle };
                e.ToolTip = new ToolTip { Style = chartToolTipStyle };
                Panel.SetZIndex(e, 10);
                points.Add(e);
                ChartGrid.Children.Add(e);
            }

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1;
            var stepX = w / (datas.Count - 1); // X轴步长
            for (int i = 0; i < datas.Count; i++)
            {
                var x = i * stepX;
                var y = GraphicHelper.Normalize(datas[i].cpkValue, yMin, yRange, h);
                // 创建变换组，先设置位置再平移
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(new TranslateTransform(x, y));
                points[i].RenderTransform = transformGroup;
                points[i].Stroke = datas[i].isManual ? manualCPKPointStroke : normalCPKPointStroke;

                var toolTip = points[i].ToolTip as ToolTip;
                toolTip!.Content = datas[i].cpkValue.ToString();
            }
        }
        #endregion
    }
}
