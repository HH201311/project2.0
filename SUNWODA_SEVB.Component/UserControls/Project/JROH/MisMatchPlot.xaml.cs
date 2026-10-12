using ScottPlot;
using ScottPlot.WPF;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SUNWODA_SEVB.Component.UserControls.Project.JROH
{
    /// <summary>
    /// 极耳错位闭环曲线控件（检测值 / 设定值双曲线）
    /// </summary>
    /// <remarks>
    /// 结构对齐 <c>MultiCorrectPlot</c>；⏸ 通道划分与曲线条数待极耳错位资料确认后调整。
    /// </remarks>
    public partial class MisMatchPlot : UserControl
    {
        public MisMatchPlot()
        {
            InitializeComponent();
            InitializePlot();
        }

        private void InitializePlot()
        {
            if (PlotControl?.Plot == null) return;
            PlotControl.Plot.Grid.IsVisible = ShowGrid;
            PlotControl.Plot.Legend.IsVisible = ShowLegend;
            PlotControl.Plot.Axes.DateTimeTicksBottom();

            // 订阅鼠标事件，用于检测用户手动缩放/平移
            PlotControl.AddHandler(MouseWheelEvent, new MouseWheelEventHandler(OnUserInteraction), true);
            PlotControl.AddHandler(MouseDownEvent, new MouseButtonEventHandler(OnUserInteraction), true);
            PlotControl.AddHandler(MouseUpEvent, new MouseButtonEventHandler(OnUserInteraction), true);
        }

        #region 依赖属性

        /// <summary>
        /// 检测值数据集合
        /// </summary>
        public static readonly DependencyProperty LineSeries1CollectionProperty =
            DependencyProperty.Register(
                nameof(LineSeries1Collection),
                typeof(ObservableCollection<(DateTime, double)>),
                typeof(MisMatchPlot),
                new PropertyMetadata(null, OnLineSeriesCollectionChanged));

        public ObservableCollection<(DateTime x, double y)> LineSeries1Collection
        {
            get => (ObservableCollection<(DateTime, double)>)GetValue(LineSeries1CollectionProperty);
            set => SetValue(LineSeries1CollectionProperty, value);
        }

        /// <summary>
        /// 设定值数据集合
        /// </summary>
        public static readonly DependencyProperty LineSeries2CollectionProperty =
            DependencyProperty.Register(
                nameof(LineSeries2Collection),
                typeof(ObservableCollection<(DateTime, double)>),
                typeof(MisMatchPlot),
                new PropertyMetadata(null, OnLineSeriesCollectionChanged));

        public ObservableCollection<(DateTime x, double y)> LineSeries2Collection
        {
            get => (ObservableCollection<(DateTime, double)>)GetValue(LineSeries2CollectionProperty);
            set => SetValue(LineSeries2CollectionProperty, value);
        }

        // 图表标题
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string),
                typeof(MisMatchPlot), new PropertyMetadata(string.Empty, OnAppearanceChanged));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        // X轴标签
        public static readonly DependencyProperty XAxisLabelProperty =
            DependencyProperty.Register(nameof(XAxisLabel), typeof(string),
                typeof(MisMatchPlot), new PropertyMetadata("时间", OnAppearanceChanged));

        public string XAxisLabel
        {
            get => (string)GetValue(XAxisLabelProperty);
            set => SetValue(XAxisLabelProperty, value);
        }

        // Y轴标签
        public static readonly DependencyProperty YAxisLabelProperty =
            DependencyProperty.Register(nameof(YAxisLabel), typeof(string),
                typeof(MisMatchPlot), new PropertyMetadata("数值", OnAppearanceChanged));

        public string YAxisLabel
        {
            get => (string)GetValue(YAxisLabelProperty);
            set => SetValue(YAxisLabelProperty, value);
        }

        // 是否显示网格
        public static readonly DependencyProperty ShowGridProperty =
            DependencyProperty.Register(nameof(ShowGrid), typeof(bool),
                typeof(MisMatchPlot), new PropertyMetadata(true, OnAppearanceChanged));

        public bool ShowGrid
        {
            get => (bool)GetValue(ShowGridProperty);
            set => SetValue(ShowGridProperty, value);
        }

        // 是否显示图例
        public static readonly DependencyProperty ShowLegendProperty =
            DependencyProperty.Register(nameof(ShowLegend), typeof(bool),
                typeof(MisMatchPlot), new PropertyMetadata(true, OnAppearanceChanged));

        public bool ShowLegend
        {
            get => (bool)GetValue(ShowLegendProperty);
            set => SetValue(ShowLegendProperty, value);
        }

        #endregion

        #region 属性变化处理

        private static void OnLineSeriesCollectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not MisMatchPlot control) return;

            if (e.OldValue is ObservableCollection<(DateTime, double)> oldCollection)
                oldCollection.CollectionChanged -= control.OnDataCollectionChanged;

            if (e.NewValue is ObservableCollection<(DateTime, double)> newCollection)
            {
                newCollection.CollectionChanged += control.OnDataCollectionChanged;
                control.UpdatePlot(); // 新集合赋值时立即更新
            }
        }

        private void OnDataCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 集合内容变化时更新绘图（可考虑节流，这里简单处理）
            UpdatePlot();
        }

        private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not MisMatchPlot control) return;
            control.UpdatePlot();
        }

        #endregion

        private bool _isUserInteracting = false;
        private void OnUserInteraction(object sender, MouseEventArgs e)
        {
            if (e is not MouseButtonEventArgs args) return;
            _isUserInteracting = args.ChangedButton == MouseButton.Middle ? false : true;
        }

        #region 绘图更新

        private void UpdatePlot()
        {
            if (PlotControl?.Plot == null) return;

            PlotControl.Plot.Clear();

            // 绘制数据曲线
            DrawDataCurve(LineSeries1Collection, "检测值", System.Drawing.Color.DarkGreen);
            DrawDataCurve(LineSeries2Collection, "设定值", System.Drawing.Color.DarkRed);

            // 应用外观配置
            ApplyPlotConfiguration();

            PlotControl.Refresh();
        }

        private void DrawDataCurve(ObservableCollection<(DateTime x, double y)> line, string legendText, System.Drawing.Color color)
        {
            if (line == null || line.Count == 0) return;

            var pointCount = line.Count;
            var xDates = new DateTime[pointCount];
            var yDatas = new double[pointCount];

            for (int i = 0; i < pointCount; i++)
            {
                xDates[i] = line[i].x;
                yDatas[i] = line[i].y;
            }

            var scatter = PlotControl.Plot.Add.Scatter(xDates, yDatas);
            scatter.Color = ScottPlot.Color.FromSDColor(color);
            scatter.LineWidth = 2.0f;
            scatter.MarkerSize = 4;
            scatter.LegendText = legendText;
        }

        private void ApplyPlotConfiguration()
        {
            var plot = PlotControl.Plot;

            if (!string.IsNullOrEmpty(Title))
                plot.Title(Title);

            plot.Axes.Bottom.Label.Text = XAxisLabel ?? "时间";
            plot.Axes.Left.Label.Text = YAxisLabel ?? "数值";

            plot.Grid.IsVisible = ShowGrid;
            plot.Legend.IsVisible = ShowLegend;

            if (!_isUserInteracting)
            {
                PlotControl.Plot.Axes.AutoScale();
            }
            plot.Font.Automatic();
        }

        #endregion
    }
}
