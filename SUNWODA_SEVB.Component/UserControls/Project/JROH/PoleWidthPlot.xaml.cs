using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SUNWODA_SEVB.Component.UserControls.Project.JROH
{
    public partial class PoleWidthPlot : UserControl
    {
        public PoleWidthPlot()
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

        // Y轴数据集合（单条曲线）
        public static readonly DependencyProperty LineSeriesCollectionProperty =
            DependencyProperty.Register(
                nameof(LineSeriesCollection),
                typeof(ObservableCollection<(DateTime, double)>),
                typeof(PoleWidthPlot),
                new PropertyMetadata(null, OnLineSeriesCollectionChanged));

        public ObservableCollection<(DateTime x, double y)> LineSeriesCollection
        {
            get => (ObservableCollection<(DateTime, double)>)GetValue(LineSeriesCollectionProperty);
            set => SetValue(LineSeriesCollectionProperty, value);
        }

        // 上限水平线（NaN表示不显示）
        public static readonly DependencyProperty UpperLineProperty =
            DependencyProperty.Register(nameof(UpperLine), typeof(double),
                typeof(PoleWidthPlot), new PropertyMetadata(double.NaN, OnLinePropertiesChanged));

        public double UpperLine
        {
            get => (double)GetValue(UpperLineProperty);
            set => SetValue(UpperLineProperty, value);
        }

        // 下限水平线（NaN表示不显示）
        public static readonly DependencyProperty LowerLineProperty =
            DependencyProperty.Register(nameof(LowerLine), typeof(double),
                typeof(PoleWidthPlot), new PropertyMetadata(double.NaN, OnLinePropertiesChanged));

        public double LowerLine
        {
            get => (double)GetValue(LowerLineProperty);
            set => SetValue(LowerLineProperty, value);
        }

        // 图表标题
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string),
                typeof(PoleWidthPlot), new PropertyMetadata(string.Empty, OnAppearanceChanged));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        // X轴标签
        public static readonly DependencyProperty XAxisLabelProperty =
            DependencyProperty.Register(nameof(XAxisLabel), typeof(string),
                typeof(PoleWidthPlot), new PropertyMetadata("时间", OnAppearanceChanged));

        public string XAxisLabel
        {
            get => (string)GetValue(XAxisLabelProperty);
            set => SetValue(XAxisLabelProperty, value);
        }

        // Y轴标签
        public static readonly DependencyProperty YAxisLabelProperty =
            DependencyProperty.Register(nameof(YAxisLabel), typeof(string),
                typeof(PoleWidthPlot), new PropertyMetadata("数值", OnAppearanceChanged));

        public string YAxisLabel
        {
            get => (string)GetValue(YAxisLabelProperty);
            set => SetValue(YAxisLabelProperty, value);
        }

        // 是否显示网格
        public static readonly DependencyProperty ShowGridProperty =
            DependencyProperty.Register(nameof(ShowGrid), typeof(bool),
                typeof(PoleWidthPlot), new PropertyMetadata(true, OnAppearanceChanged));

        public bool ShowGrid
        {
            get => (bool)GetValue(ShowGridProperty);
            set => SetValue(ShowGridProperty, value);
        }

        // 是否显示图例
        public static readonly DependencyProperty ShowLegendProperty =
            DependencyProperty.Register(nameof(ShowLegend), typeof(bool),
                typeof(PoleWidthPlot), new PropertyMetadata(true, OnAppearanceChanged));

        public bool ShowLegend
        {
            get => (bool)GetValue(ShowLegendProperty);
            set => SetValue(ShowLegendProperty, value);
        }

        #endregion

        #region 属性变化处理

        private static void OnLineSeriesCollectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PoleWidthPlot control) return;

            if (e.OldValue is ObservableCollection<(DateTime, double)> oldCollection)
                oldCollection.CollectionChanged -= control.OnDataCollectionChanged;

            if (e.NewValue is ObservableCollection<(DateTime, double)> newCollection)
            {
                newCollection.CollectionChanged += control.OnDataCollectionChanged;
                control.UpdatePlot(); // 新集合赋值时立即更新
            }
        }

        private static void OnXTimeValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PoleWidthPlot control) return;

            if (e.OldValue is ObservableCollection<DateTime> oldCollection)
                oldCollection.CollectionChanged -= control.OnDataCollectionChanged;

            if (e.NewValue is ObservableCollection<DateTime> newCollection)
            {
                newCollection.CollectionChanged += control.OnDataCollectionChanged;
                control.UpdatePlot();
            }
        }

        private void OnDataCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 集合内容变化时更新绘图（可考虑节流，这里简单处理）
            UpdatePlot();
        }

        private static void OnLinePropertiesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PoleWidthPlot control) return;
            control.UpdatePlot();
        }

        private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PoleWidthPlot control) return;
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

            // 添加上下限水平线
            AddHorizontalLine(UpperLine, System.Drawing.Color.Red, "上限");
            AddHorizontalLine(LowerLine, System.Drawing.Color.Blue, "下限");

            // 绘制数据曲线
            DrawDataCurve();

            // 应用外观配置
            ApplyPlotConfiguration();

            PlotControl.Refresh();
        }

        private void AddHorizontalLine(double value, System.Drawing.Color color, string legendText)
        {
            if (double.IsNaN(value)) return;

            var line = PlotControl.Plot.Add.HorizontalLine(value);
            line.Color = ScottPlot.Color.FromSDColor(color);
            line.LineWidth = 2.5f;
            line.LinePattern = LinePattern.Dashed;
            line.LegendText = legendText;
        }

        private void DrawDataCurve()
        {
            if (LineSeriesCollection == null || LineSeriesCollection.Count == 0) return;

            var pointCount = LineSeriesCollection.Count;
            var xDates = new DateTime[pointCount];
            var yDatas = new double[pointCount];

            for (int i = 0; i < pointCount; i++)
            {
                xDates[i] = LineSeriesCollection[i].x;
                yDatas[i] = LineSeriesCollection[i].y;
            }

            var scatter = PlotControl.Plot.Add.Scatter(xDates, yDatas);
            scatter.Color = ScottPlot.Color.FromSDColor(System.Drawing.Color.DarkGreen);
            scatter.LineWidth = 2.0f;
            scatter.MarkerSize = 4;
            scatter.LegendText = "数据";
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