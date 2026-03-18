using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
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

            InitializeAxisAndSubLine();
        }

        public const double CPK2_00 = 2.00;
        public const double CPK1_67 = 1.67;
        public const double CPK1_33 = 1.33;

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

        #region ItemsSource
        public IEnumerable<CPKByRollModel> ItemsSource
        {
            get { return (IEnumerable<CPKByRollModel>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<CPKByRollModel>),
            typeof(CPKByRollChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemsSourceChanged))
        );

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CPKByRollChartControl;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnItemsSourcePropertyChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 当数据增加、删除、重置时，重新绘制
            DrawChart();
        }
        #endregion

        Line subLine0 = new(), subLine1 = new(), subLine2 = new();
        List<Path> barGraphs = [];

        /// <summary>
        /// 
        /// </summary>
        private void InitializeAxisAndSubLine()
        {
            XAxisPainter.RotationAngle = 350;
            XAxisPainter.Update("1", "2", "3", "4", "5", "6", "7", "8", "9", "膜卷号");
            YAxisPainter.RotationAngle = 0;
            YAxisPainter.Update(0, 4, yStep);

            (double yMin, double yMax) = RefreshYAxis();
            var yRange = yMax - yMin;

            RefreshSubLine(ref subLine0, CPK2_00, yMin, yRange, 0.9);
            RefreshSubLine(ref subLine1, CPK1_67, yMin, yRange, 0.6);
            RefreshSubLine(ref subLine2, CPK1_33, yMin, yRange, 0.3);
        }

        /// <summary>
        /// 
        /// </summary>
        private void DrawChart()
        {
            if (ItemsSource == null || ItemsSource.Count() <= 0)
            {
                InitializeAxisAndSubLine();
                return;
            }

            var datas = ItemsSource.Select(o => o.CPKValue).ToList();

            var xLabels = ItemsSource.Select(o => o.DisplayName ?? "").ToList();
            RefreshXAxis(xLabels);
            (double yMin, double yMax) = RefreshYAxis(datas.ToArray());
            var yRange = yMax - yMin;

            RefreshSubLine(ref subLine0, CPK2_00, yMin, yRange, 0.9);
            RefreshSubLine(ref subLine1, CPK1_67, yMin, yRange, 0.6);
            RefreshSubLine(ref subLine2, CPK1_33, yMin, yRange, 0.3);
            RefreshBarGraph(datas, yMin, yRange);
        }

        private void RefreshXAxis(List<string> labels)
        {
            labels.Insert(0, "");
            labels.Add("");
            XAxisPainter.RotationAngle = 350;
            XAxisPainter.AvoidAxisLabel = false;
            XAxisPainter.Update(labels.ToArray());
        }

        private (double yMin, double yMax) RefreshYAxis(params double[] values)
        {
            var vMin = values?.Length > 0 ? values.Min() : 0;
            var vMax = values?.Length > 0 ? values.Max() : 0;
            var range = vMax - vMin;
            double yMin = vMin - range / 5, yMax = vMax + range / 5;
            if (YMax > YMin)
            {
                yMin = YMin; yMax = YMax;
                if (values?.Length > 0)
                {
                    while (yMin > vMin) yMin -= yStep;
                    while (yMax <= vMax) yMax += yStep;
                }
            }

            var yRange = yMax - yMin;
            var yLabels = new List<string>();
            for (var i = yMin; i <= yMax; i += yStep)
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
            {
                ChartGrid.Children.Add(subLine);
            }
        }
        #endregion

        #region 柱状图
        private Style? barGraphStyle;
        private Style? chartToolTipStyle;
        private void RefreshBarGraph(List<double> datas, double yMin, double yRange)
        {
            if (barGraphStyle == null) barGraphStyle = this.Resources["BarGraphPathStyle"] as Style;
            if (chartToolTipStyle == null) chartToolTipStyle = this.Resources["ChartToolTipStyle"] as Style;

            while (barGraphs.Count > datas.Count)
            {
                var p = barGraphs[barGraphs.Count - 1];
                if (ChartGrid.Children.Contains(p)) ChartGrid.Children.Remove(p);
                barGraphs.Remove(p);
            }
            while (barGraphs.Count < datas.Count)
            {
                var p = new Path { Style = barGraphStyle };
                Panel.SetZIndex(p, 10);
                p.ToolTip = new ToolTip { Style = chartToolTipStyle };
                barGraphs.Add(p);
                ChartGrid.Children.Add(p);
            }

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1; // 防止除零
            var stepX = w / (datas.Count + 1); // X轴步长
            for (int i = 0; i < datas.Count; i++)
            {
                var x = (i + 1) * stepX;
                var y = GraphicHelper.Normalize(datas[i], yMin, yRange, h);
                var pointLB = new Point(x - 10, h);
                var pointLT = new Point(x - 10, y);
                var pointRT = new Point(x + 10, y);
                var pointRB = new Point(x + 10, h);
                var geometry = GraphicHelper.CreatStreamGeometryWithCorner(pointLB, pointLT, pointRT, pointRB);
                barGraphs[i].Data = geometry;
                var toolTip = barGraphs[i].ToolTip as ToolTip;
                toolTip!.Content = datas[i].ToString();
            }
        }
        #endregion
    }
}
