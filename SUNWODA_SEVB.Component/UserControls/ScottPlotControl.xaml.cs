using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace SUNWODA_SEVB.Component.UserControls
{
    /// <summary>
    /// ScottPlotControl.xaml 的交互逻辑
    /// </summary>
    public partial class ScottPlotControl : UserControl
    {
        public ScottPlotControl()
        {
            InitializeComponent();
            InitializePlot();
        }
        private void InitializePlot()
        {
            if (PlotControl?.Plot != null)
            {
                PlotControl.Plot.Grid.IsVisible = ShowGrid;
            }
        }

        #region 依赖属性

        // 数据点集合
        public static readonly DependencyProperty DataPointsProperty =
            DependencyProperty.Register(nameof(DataPoints), typeof(ObservableCollection<PlotDataPoint>),
                typeof(ScottPlotControl), new PropertyMetadata(null, OnDataPointsChanged));

        public ObservableCollection<PlotDataPoint> DataPoints
        {
            get => (ObservableCollection<PlotDataPoint>)GetValue(DataPointsProperty);
            set => SetValue(DataPointsProperty, value);
        }

        // 图表标题
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string),
                typeof(ScottPlotControl), new PropertyMetadata(string.Empty, OnTitleChanged));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        // X轴标签
        public static readonly DependencyProperty XAxisLabelProperty =
            DependencyProperty.Register(nameof(XAxisLabel), typeof(string),
                typeof(ScottPlotControl), new PropertyMetadata(string.Empty, OnXAxisLabelChanged));

        public string XAxisLabel
        {
            get => (string)GetValue(XAxisLabelProperty);
            set => SetValue(XAxisLabelProperty, value);
        }

        // Y轴标签
        public static readonly DependencyProperty YAxisLabelProperty =
            DependencyProperty.Register(nameof(YAxisLabel), typeof(string),
                typeof(ScottPlotControl), new PropertyMetadata(string.Empty, OnYAxisLabelChanged));

        public string YAxisLabel
        {
            get => (string)GetValue(YAxisLabelProperty);
            set => SetValue(YAxisLabelProperty, value);
        }

        // 图表类型
        public static readonly DependencyProperty PlotTypeProperty =
            DependencyProperty.Register(nameof(PlotType), typeof(PlotType),
                typeof(ScottPlotControl), new PropertyMetadata(PlotType.Line, OnPlotTypeChanged));

        public PlotType PlotType
        {
            get => (PlotType)GetValue(PlotTypeProperty);
            set => SetValue(PlotTypeProperty, value);
        }

        // 是否启用网格
        public static readonly DependencyProperty ShowGridProperty =
            DependencyProperty.Register(nameof(ShowGrid), typeof(bool),
                typeof(ScottPlotControl), new PropertyMetadata(true, OnShowGridChanged));

        public bool ShowGrid
        {
            get => (bool)GetValue(ShowGridProperty);
            set => SetValue(ShowGridProperty, value);
        }

        #endregion

        #region 属性变化处理

        private static void OnDataPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ScottPlotControl)d;
            if (e.OldValue is ObservableCollection<PlotDataPoint> oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnDataPointsCollectionChanged;
            }

            if (e.NewValue is ObservableCollection<PlotDataPoint> newCollection)
            {
                newCollection.CollectionChanged += control.OnDataPointsCollectionChanged;
            }

            control.UpdatePlot();
        }

        private void OnDataPointsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdatePlot();
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ScottPlotControl)d;
            control.UpdateTitle();
        }

        private static void OnXAxisLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ScottPlotControl)d;
            control.UpdateAxisLabels();
        }

        private static void OnYAxisLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ScottPlotControl)d;
            control.UpdateAxisLabels();
        }

        private static void OnPlotTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ScottPlotControl)d;
            control.UpdatePlot();
        }

        private static void OnShowGridChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ScottPlotControl)d;
            control.UpdateGrid();
        }

        #endregion

        #region 更新方法

        private void UpdatePlot()
        {
            if (PlotControl?.Plot == null || DataPoints == null) return;

            PlotControl.Plot.Clear();

            if (DataPoints.Count == 0)
            {
                PlotControl.Refresh();
                return;
            }

            var xData = new double[DataPoints.Count];
            var yData = new double[DataPoints.Count];

            for (int i = 0; i < DataPoints.Count; i++)
            {
                xData[i] = DataPoints[i].X;
                yData[i] = DataPoints[i].Y;
            }

            switch (PlotType)
            {
                case PlotType.Line:
                    PlotControl.Plot.Add.SignalXY(xData, yData);
                    break;
                case PlotType.Scatter:
                    PlotControl.Plot.Add.Scatter(xData, yData);
                    break;
                case PlotType.Bar:
                    PlotControl.Plot.Add.Bars(xData, yData);
                    break;
            }

            PlotControl.Refresh();
        }

        private void UpdateTitle()
        {
            if (PlotControl?.Plot == null) return;

            PlotControl.Plot.Title(Title);
            PlotControl.Refresh();
        }

        private void UpdateAxisLabels()
        {
            if (PlotControl?.Plot == null) return;

            PlotControl.Plot.Axes.Bottom.Label.Text = XAxisLabel;
            PlotControl.Plot.Axes.Left.Label.Text = YAxisLabel;
            PlotControl.Refresh();
        }

        private void UpdateGrid()
        {
            if (PlotControl?.Plot == null) return;

            PlotControl.Plot.Grid.IsVisible = ShowGrid;
            PlotControl.Refresh();
        }

        #endregion
        #region 公共方法

        /// <summary>
        /// 获取底层的 WpfPlot 控件，用于更高级的自定义
        /// </summary>
        public WpfPlot GetWpfPlot()
        {
            return PlotControl;
        }

        /// <summary>
        /// 清空图表数据
        /// </summary>
        public void ClearPlot()
        {
            PlotControl?.Plot?.Clear();
            PlotControl?.Refresh();
        }

        /// <summary>
        /// 自动缩放图表
        /// </summary>
        public void AutoScale()
        {
            PlotControl?.Plot?.Axes?.AutoScale();
            PlotControl?.Refresh();
        }

        #endregion
    }

    // 数据点类
    public class PlotDataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }

        public PlotDataPoint() { }

        public PlotDataPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    // 图表类型枚举
    public enum PlotType
    {
        Line,
        Scatter,
        Bar
    }
}

