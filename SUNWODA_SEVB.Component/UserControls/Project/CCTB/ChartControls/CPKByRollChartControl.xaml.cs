using SUNWODA_SEVB.Core.Models.Project.CCTB;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
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
    /// CPKByRollChartControl.xaml 的交互逻辑
    /// </summary>
    public partial class CPKByRollChartControl : UserControl
    {
        public CPKByRollChartControl()
        {
            InitializeComponent();
        }

        public const double CPK2_00 = 2.00;
        public const double CPK1_67 = 1.67;
        public const double CPK1_33 = 1.33;

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            XAxisPainter.RotationAngle = 350;
            XAxisPainter.Update("1", "2", "3", "4", "5", "6", "7", "8", "9", "膜卷号");
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
            typeof(CPKByRollChartControl),
            new PropertyMetadata(0d, new PropertyChangedCallback(OnYMinChanged))
        );

        private static void OnYMinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CPKByRollChartControl;
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
            typeof(CPKByRollChartControl),
            new PropertyMetadata(4d, new PropertyChangedCallback(OnYMaxChanged))
        );

        private static void OnYMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CPKByRollChartControl;
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
            typeof(CPKByRollChartControl),
            new PropertyMetadata(0.5d, new PropertyChangedCallback(OnYStepChanged))
        );

        private static void OnYStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CPKByRollChartControl;
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
            typeof(CPKByRollChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemSourceChanged))
        );

        private static void OnItemSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CPKByRollChartControl;
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

        Line? cpkDashLine0;
        Line? cpkDashLine1;
        Line? cpkDashLine2;
        List<Path>? cpkBarGraphs;
        private void DrawChart()
        {
            if (ItemSource == null || ItemSource.Count() <= 0) return;

            var datas = ItemSource.Select(o => o.CPKValue ?? 0).ToList();
            var min = datas.Min();
            var max = datas.Max();

            var xLabels = ItemSource.Select(o => o.CPKLabel ?? "").ToList();
            RefreshXAxis(xLabels);
            (double yMin, double yMax) = RefreshYAxis(min, max);
            var yRange = yMax - yMin;

            RefreshCPKDashLine(ref cpkDashLine0, CPK2_00, yMin, yRange, 0.9);
            RefreshCPKDashLine(ref cpkDashLine1, CPK1_67, yMin, yRange, 0.6);
            RefreshCPKDashLine(ref cpkDashLine2, CPK1_33, yMin, yRange, 0.3);
            RefreshCPKBarGraph(datas, yMin, yRange);
        }

        private void RefreshXAxis(List<string> labels)
        {
            labels.Insert(0, "");
            labels.Add("");
            XAxisPainter.RotationAngle = 350;
            XAxisPainter.AvoidAxisLabel = false;
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

        private Style? cpkDashLineStyle;
        private void RefreshCPKDashLine(ref Line? dashLine, double yValue, double yMin, double yRange, double opacity)
        {
            if (cpkDashLineStyle == null) cpkDashLineStyle = this.Resources["CPKDashLineStyle"] as Style;
            if (dashLine == null) dashLine = new Line() { Style = cpkDashLineStyle, Opacity = opacity };

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1;
            var y = Normalize(yValue, yMin, yRange, h);
            dashLine.X1 = 0;
            dashLine.Y1 = y;
            dashLine.X2 = w;
            dashLine.Y2 = y;

            if (!ChartGrid.Children.Contains(dashLine) && dashLine != null)
            {
                ChartGrid.Children.Add(dashLine);
            }
        }

        private Style? cpkBarGraphStyle;
        private Style? cpkToolTipStyle;
        private void RefreshCPKBarGraph(List<double> datas, double yMin, double yRange)
        {
            if (cpkBarGraphs == null) cpkBarGraphs = new List<Path>();
            if (cpkBarGraphStyle == null) cpkBarGraphStyle = this.Resources["CPKBarGraphPathStyle"] as Style;
            if (cpkToolTipStyle == null) cpkToolTipStyle = this.Resources["CPKToolTipStyle"] as Style;
            while (cpkBarGraphs.Count < datas.Count)
            {
                var p = new Path() { Style = cpkBarGraphStyle };
                p.ToolTip = new ToolTip { Style = cpkToolTipStyle };
                cpkBarGraphs.Add(p);
            }
            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1; // 防止除零
            var stepX = w / (datas.Count + 1); // X轴步长
            for (int i = 0; i < datas.Count; i++)
            {
                var x = (i + 1) * stepX;
                var y = Normalize(datas[i], yMin, yRange, h);
                var pointLB = new Point(x - 10, h);
                var pointLT = new Point(x - 10, y);
                var pointRT = new Point(x + 10, y);
                var pointRB = new Point(x + 10, h);
                var geometry = CreatStreamGeometry(pointLB, pointLT, pointRT, pointRB);
                cpkBarGraphs[i].Data = geometry;
                var toolTip = cpkBarGraphs[i].ToolTip as ToolTip;
                toolTip!.Content = datas[i].ToString();
                //toolTip!.HorizontalOffset = x - toolTip!.ActualWidth / 2;
                //toolTip!.VerticalOffset = y - 5;
            }

            for (int i = 0; i < cpkBarGraphs.Count; i++)
            {
                if (i < datas.Count)
                {
                    if (!ChartGrid.Children.Contains(cpkBarGraphs[i]) && cpkBarGraphs[i] != null)
                    {
                        ChartGrid.Children.Add(cpkBarGraphs[i]);
                    }
                    continue;
                }
                if (ChartGrid.Children.Contains(cpkBarGraphs[i]))
                {
                    ChartGrid.Children.Remove(cpkBarGraphs[i]);
                }
            }
        }

        private StreamGeometry CreatStreamGeometry(Point pointLB, Point pointLT, Point pointRT, Point pointRB)
        {
            var r = 5.0; // 圆角半径
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                // 起点 左下角
                ctx.BeginFigure(pointLB, true, true);

                // 左上圆角
                var arcStartPointLT = new Point(pointLT.X, pointLT.Y + r);
                var arcEndPointLT = new Point(pointLT.X + r, pointLT.Y);
                ctx.LineTo(arcStartPointLT, true, true);
                ctx.ArcTo(arcEndPointLT, new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);

                // 右上圆角
                var arcStartPointLRT = new Point(pointRT.X - r, pointRT.Y);
                var arcEndPointRT = new Point(pointRT.X, pointRT.Y + r);
                ctx.LineTo(arcStartPointLRT, true, true);
                ctx.ArcTo(arcEndPointRT, new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);

                // 右下角
                ctx.LineTo(pointRB, true, true);
                // 终点 左下角
                ctx.LineTo(pointLB, true, true);
            }
            geometry.Freeze();
            return geometry;
        }

        /// <summary>
        /// 坐标转换辅助方法：将数值转换为屏幕 Y 坐标
        /// </summary>
        /// <param name="yValue"></param>
        /// <param name="yMin"></param>
        /// <param name="yRange"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        private double Normalize(double yValue, double yMin, double yRange, double height)
        {
            // 数值越大，Y坐标越小（因为屏幕坐标系原点在左上角）
            return height - ((yValue - yMin) / yRange * height);
        }
    }
}
