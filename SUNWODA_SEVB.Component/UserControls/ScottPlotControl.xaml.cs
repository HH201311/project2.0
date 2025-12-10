using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SUNWODA_SEVB.Component.UserControls
{
    /// <summary>
    /// ScottPlotControl.xaml 的交互逻辑
    /// 支持多组数据的折线图控件（支持1-64条及更多折线）
    /// </summary>
    public partial class ScottPlotControl : UserControl
    {
        // 预定义64种颜色列表，精心挑选以确保视觉区分度
        private static readonly ScottPlot.Color[] DefaultColors = new[]
        {
            // 第一组：经典配色（1-10）
            ScottPlot.Color.FromHex("#1f77b4"),  // 1.  蓝色
            ScottPlot.Color.FromHex("#ff7f0e"),  // 2.  橙色
            ScottPlot.Color.FromHex("#2ca02c"),  // 3.  绿色
            ScottPlot.Color.FromHex("#d62728"),  // 4.  红色
            ScottPlot.Color.FromHex("#9467bd"),  // 5.  紫色
            ScottPlot.Color.FromHex("#8c564b"),  // 6.  棕色
            ScottPlot.Color.FromHex("#e377c2"),  // 7.  粉色
            ScottPlot.Color.FromHex("#7f7f7f"),  // 8.  灰色
            ScottPlot.Color.FromHex("#bcbd22"),  // 9.  黄绿色
            ScottPlot.Color.FromHex("#17becf"),  // 10. 青色
            
            // 第二组：Tableau 配色扩展（11-20）
            ScottPlot.Color.FromHex("#aec7e8"),  // 11. 浅蓝色
            ScottPlot.Color.FromHex("#ffbb78"),  // 12. 浅橙色
            ScottPlot.Color.FromHex("#98df8a"),  // 13. 浅绿色
            ScottPlot.Color.FromHex("#ff9896"),  // 14. 浅红色
            ScottPlot.Color.FromHex("#c5b0d5"),  // 15. 浅紫色
            ScottPlot.Color.FromHex("#c49c94"),  // 16. 浅棕色
            ScottPlot.Color.FromHex("#f7b6d2"),  // 17. 浅粉色
            ScottPlot.Color.FromHex("#c7c7c7"),  // 18. 浅灰色
            ScottPlot.Color.FromHex("#dbdb8d"),  // 19. 浅黄绿色
            ScottPlot.Color.FromHex("#9edae5"),  // 20. 浅青色
            
            // 第三组：深色调（21-30）
            ScottPlot.Color.FromHex("#393b79"),  // 21. 深蓝紫色
            ScottPlot.Color.FromHex("#637939"),  // 22. 橄榄绿
            ScottPlot.Color.FromHex("#8c6d31"),  // 23. 深金色
            ScottPlot.Color.FromHex("#843c39"),  // 24. 深红棕色
            ScottPlot.Color.FromHex("#7b4173"),  // 25. 深紫红色
            ScottPlot.Color.FromHex("#5254a3"),  // 26. 靛蓝色
            ScottPlot.Color.FromHex("#8ca252"),  // 27. 苔绿色
            ScottPlot.Color.FromHex("#bd9e39"),  // 28. 土黄色
            ScottPlot.Color.FromHex("#ad494a"),  // 29. 砖红色
            ScottPlot.Color.FromHex("#a55194"),  // 30. 洋红色
            
            // 第四组：明亮色调（31-40）
            ScottPlot.Color.FromHex("#6b6ecf"),  // 31. 中蓝紫色
            ScottPlot.Color.FromHex("#b5cf6b"),  // 32. 黄绿色
            ScottPlot.Color.FromHex("#e7ba52"),  // 33. 金黄色
            ScottPlot.Color.FromHex("#ce6dbd"),  // 34. 亮紫红色
            ScottPlot.Color.FromHex("#de9ed6"),  // 35. 浅洋红色
            ScottPlot.Color.FromHex("#3182bd"),  // 36. 钴蓝色
            ScottPlot.Color.FromHex("#e6550d"),  // 37. 深橙色
            ScottPlot.Color.FromHex("#31a354"),  // 38. 翠绿色
            ScottPlot.Color.FromHex("#756bb1"),  // 39. 中紫色
            ScottPlot.Color.FromHex("#636363"),  // 40. 深灰色
            
            // 第五组：柔和色调（41-50）
            ScottPlot.Color.FromHex("#6baed6"),  // 41. 天蓝色
            ScottPlot.Color.FromHex("#fd8d3c"),  // 42. 橘红色
            ScottPlot.Color.FromHex("#74c476"),  // 43. 草绿色
            ScottPlot.Color.FromHex("#9e9ac8"),  // 44. 淡紫色
            ScottPlot.Color.FromHex("#969696"),  // 45. 中灰色
            ScottPlot.Color.FromHex("#9ecae1"),  // 46. 粉蓝色
            ScottPlot.Color.FromHex("#fdae6b"),  // 47. 杏色
            ScottPlot.Color.FromHex("#a1d99b"),  // 48. 嫩绿色
            ScottPlot.Color.FromHex("#bcbddc"),  // 49. 薰衣草色
            ScottPlot.Color.FromHex("#bdbdbd"),  // 50. 银灰色
            
            // 第六组：补充色（51-64）
            ScottPlot.Color.FromHex("#e41a1c"),  // 51. 鲜红色
            ScottPlot.Color.FromHex("#377eb8"),  // 52. 皇家蓝
            ScottPlot.Color.FromHex("#4daf4a"),  // 53. 鲜绿色
            ScottPlot.Color.FromHex("#984ea3"),  // 54. 葡萄紫
            ScottPlot.Color.FromHex("#ff7f00"),  // 55. 纯橙色
            ScottPlot.Color.FromHex("#ffff33"),  // 56. 亮黄色
            ScottPlot.Color.FromHex("#a65628"),  // 57. 赭石色
            ScottPlot.Color.FromHex("#f781bf"),  // 58. 亮粉色
            ScottPlot.Color.FromHex("#66c2a5"),  // 59. 薄荷绿
            ScottPlot.Color.FromHex("#fc8d62"),  // 60. 珊瑚色
            ScottPlot.Color.FromHex("#8da0cb"),  // 61. 矢车菊蓝
            ScottPlot.Color.FromHex("#e78ac3"),  // 62. 兰花紫
            ScottPlot.Color.FromHex("#a6d854"),  // 63. 青柠色
            ScottPlot.Color.FromHex("#ffd92f"),  // 64. 向日葵黄
        };

        /// <summary>
        /// 获取指定索引的颜色
        /// 如果索引超出预定义颜色数量，则通过HSL算法动态生成颜色
        /// </summary>
        /// <param name="index">颜色索引</param>
        /// <returns>ScottPlot颜色</returns>
        private static ScottPlot.Color GetColorForIndex(int index)
        {
            if (index < DefaultColors.Length)
            {
                return DefaultColors[index];
            }

            // 超出预定义颜色范围时，使用HSL色彩空间动态生成颜色
            // 这样可以支持任意数量的折线图
            return GenerateDistinctColor(index);
        }

        /// <summary>
        /// 基于HSL色彩空间生成具有良好区分度的颜色
        /// </summary>
        /// <param name="index">颜色索引</param>
        /// <returns>生成的颜色</returns>
        private static ScottPlot.Color GenerateDistinctColor(int index)
        {
            // 使用黄金角度来分散色相，确保相邻索引的颜色差异较大
            const double goldenRatioConjugate = 0.618033988749895;

            // 计算色相（0-360度）
            double hue = (index * goldenRatioConjugate) % 1.0 * 360.0;

            // 饱和度在70%-90%之间变化
            double saturation = 0.7 + (index % 3) * 0.1;

            // 亮度在45%-65%之间变化，确保颜色既不太暗也不太亮
            double lightness = 0.45 + (index % 5) * 0.05;

            // HSL转RGB
            var rgb = HslToRgb(hue, saturation, lightness);

            return ScottPlot.Color.FromHex($"#{rgb.r:X2}{rgb.g:X2}{rgb.b:X2}");
        }

        /// <summary>
        /// HSL颜色空间转换为RGB
        /// </summary>
        private static (byte r, byte g, byte b) HslToRgb(double h, double s, double l)
        {
            double r, g, b;

            if (Math.Abs(s) < 0.001)
            {
                r = g = b = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h / 360.0 + 1.0 / 3.0);
                g = HueToRgb(p, q, h / 360.0);
                b = HueToRgb(p, q, h / 360.0 - 1.0 / 3.0);
            }

            return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }

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

        // 多组数据集合（新增）
        public static readonly DependencyProperty DataSeriesCollectionProperty =
            DependencyProperty.Register(nameof(DataSeriesCollection), typeof(ObservableCollection<PlotDataSeries>),
                typeof(ScottPlotControl), new PropertyMetadata(null, OnDataSeriesCollectionChanged));

        public ObservableCollection<PlotDataSeries> DataSeriesCollection
        {
            get => (ObservableCollection<PlotDataSeries>)GetValue(DataSeriesCollectionProperty);
            set => SetValue(DataSeriesCollectionProperty, value);
        }

        // 单组数据点集合（保持向后兼容）
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

        // 是否显示图例
        public static readonly DependencyProperty ShowLegendProperty =
            DependencyProperty.Register(nameof(ShowLegend), typeof(bool),
                typeof(ScottPlotControl), new PropertyMetadata(true, OnShowLegendChanged));

        public bool ShowLegend
        {
            get => (bool)GetValue(ShowLegendProperty);
            set => SetValue(ShowLegendProperty, value);
        }

        #endregion

        #region 属性变化处理

        private static void OnDataSeriesCollectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ScottPlotControl)d;

            // 取消订阅旧集合的事件
            if (e.OldValue is ObservableCollection<PlotDataSeries> oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnDataSeriesCollectionChanged;
                foreach (var series in oldCollection)
                {
                    series.PropertyChanged -= control.OnSeriesPropertyChanged;
                    if (series.DataPoints != null)
                    {
                        series.DataPoints.CollectionChanged -= control.OnSeriesDataPointsChanged;
                    }
                }
            }

            // 订阅新集合的事件
            if (e.NewValue is ObservableCollection<PlotDataSeries> newCollection)
            {
                newCollection.CollectionChanged += control.OnDataSeriesCollectionChanged;
                foreach (var series in newCollection)
                {
                    series.PropertyChanged += control.OnSeriesPropertyChanged;
                    if (series.DataPoints != null)
                    {
                        series.DataPoints.CollectionChanged += control.OnSeriesDataPointsChanged;
                    }
                }
            }

            control.UpdatePlot();
        }

        private void OnDataSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 处理移除的项
            if (e.OldItems != null)
            {
                foreach (PlotDataSeries series in e.OldItems)
                {
                    series.PropertyChanged -= OnSeriesPropertyChanged;
                    if (series.DataPoints != null)
                    {
                        series.DataPoints.CollectionChanged -= OnSeriesDataPointsChanged;
                    }
                }
            }

            // 处理添加的项
            if (e.NewItems != null)
            {
                foreach (PlotDataSeries series in e.NewItems)
                {
                    series.PropertyChanged += OnSeriesPropertyChanged;
                    if (series.DataPoints != null)
                    {
                        series.DataPoints.CollectionChanged += OnSeriesDataPointsChanged;
                    }
                }
            }

            UpdatePlot();
        }

        private void OnSeriesPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlotDataSeries.DataPoints))
            {
                // 重新订阅 DataPoints 集合的事件
                if (sender is PlotDataSeries series && series.DataPoints != null)
                {
                    series.DataPoints.CollectionChanged -= OnSeriesDataPointsChanged;
                    series.DataPoints.CollectionChanged += OnSeriesDataPointsChanged;
                }
            }
            UpdatePlot();
        }

        private void OnSeriesDataPointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdatePlot();
        }

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

        private void OnDataPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

        private static void OnShowLegendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ScottPlotControl)d;
            control.UpdatePlot();
        }

        #endregion

        #region 更新方法

        private void UpdatePlot()
        {
            if (PlotControl?.Plot == null) return;

            PlotControl.Plot.Clear();

            // 优先使用多组数据集合
            if (DataSeriesCollection != null && DataSeriesCollection.Count > 0)
            {
                UpdateMultiSeriesPlot();
            }
            // 兼容单组数据
            else if (DataPoints != null && DataPoints.Count > 0)
            {
                UpdateSingleSeriesPlot();
            }

            // 配置图例
            ConfigureLegend();

            // 应用配置（标题、标签、网格）
            ApplyPlotConfiguration();

            // 设置坐标轴范围
            SetAxisLimits();

            PlotControl.Refresh();
        }

        private void UpdateMultiSeriesPlot()
        {
            if (PlotControl?.Plot == null || DataSeriesCollection == null) return;

            int colorIndex = 0;
            foreach (var series in DataSeriesCollection)
            {
                if (series.DataPoints == null || series.DataPoints.Count == 0)
                    continue;

                var xData = new double[series.DataPoints.Count];
                var yData = new double[series.DataPoints.Count];

                for (int i = 0; i < series.DataPoints.Count; i++)
                {
                    xData[i] = series.DataPoints[i].X;
                    yData[i] = series.DataPoints[i].Y;
                }

                // 获取颜色：优先使用用户指定的颜色，否则使用自动分配的颜色
                var color = series.Color.HasValue
                    ? ScottPlot.Color.FromHex(series.Color.Value.ToString())
                    : GetColorForIndex(colorIndex);  // 修改：使用新的颜色获取方法

                // 根据系列自己的图表类型或全局图表类型绘制
                var plotType = series.SeriesPlotType ?? PlotType;

                switch (plotType)
                {
                    case PlotType.Line:
                        var linePlot = PlotControl.Plot.Add.SignalXY(xData, yData);
                        linePlot.LegendText = series.Name ?? $"Series {colorIndex + 1}";
                        linePlot.Color = color;
                        if (series.LineWidth.HasValue)
                            linePlot.LineWidth = series.LineWidth.Value;
                        break;

                    case PlotType.Scatter:
                        var scatterPlot = PlotControl.Plot.Add.Scatter(xData, yData);
                        scatterPlot.LegendText = series.Name ?? $"Series {colorIndex + 1}";
                        scatterPlot.Color = color;
                        if (series.LineWidth.HasValue)
                            scatterPlot.LineWidth = series.LineWidth.Value;
                        if (series.MarkerSize.HasValue)
                            scatterPlot.MarkerSize = series.MarkerSize.Value;
                        break;

                    case PlotType.Bar:
                        var barPlot = PlotControl.Plot.Add.Bars(xData, yData);
                        barPlot.LegendText = series.Name ?? $"Series {colorIndex + 1}";
                        barPlot.Color = color;
                        break;
                }

                colorIndex++;
            }
        }

        private void UpdateSingleSeriesPlot()
        {
            if (PlotControl?.Plot == null || DataPoints == null || DataPoints.Count == 0) return;

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

        private void ConfigureLegend()
        {
            if (PlotControl?.Plot == null) return;

            if (ShowLegend && DataSeriesCollection != null && DataSeriesCollection.Count > 0)
            {
                var legend = PlotControl.Plot.ShowLegend();
                legend.Alignment = ScottPlot.Alignment.LowerRight;
            }
            else
            {
                PlotControl.Plot.Legend.IsVisible = false;
            }
        }

        private void ApplyPlotConfiguration()
        {
            if (PlotControl?.Plot == null) return;

            if (!string.IsNullOrEmpty(Title))
                PlotControl.Plot.Title(Title);

            PlotControl.Plot.Axes.Bottom.Label.Text = XAxisLabel ?? string.Empty;
            PlotControl.Plot.Axes.Left.Label.Text = YAxisLabel ?? string.Empty;
            PlotControl.Plot.Grid.IsVisible = ShowGrid;
            PlotControl.Plot.Font.Automatic();
        }

        private void SetAxisLimits()
        {
            if (PlotControl?.Plot == null) return;

            var limits = PlotControl.Plot.Axes.GetLimits();

            // X轴从0开始
            if (limits.Left < 0)
            {
                PlotControl.Plot.Axes.SetLimitsX(left: 0,right:0);
            }
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

        /// <summary>
        /// 强制刷新图表
        /// </summary>
        public void RefreshPlot()
        {
            UpdatePlot();
        }

        /// <summary>
        /// 添加一组数据系列
        /// </summary>
        public void AddSeries(PlotDataSeries series)
        {
            if (DataSeriesCollection == null)
            {
                DataSeriesCollection = new ObservableCollection<PlotDataSeries>();
            }
            DataSeriesCollection.Add(series);
        }

        /// <summary>
        /// 移除指定名称的数据系列
        /// </summary>
        public bool RemoveSeries(string seriesName)
        {
            if (DataSeriesCollection == null) return false;

            var series = DataSeriesCollection.FirstOrDefault(s => s.Name == seriesName);
            if (series != null)
            {
                return DataSeriesCollection.Remove(series);
            }
            return false;
        }

        /// <summary>
        /// 清空所有数据系列
        /// </summary>
        public void ClearAllSeries()
        {
            DataSeriesCollection?.Clear();
            DataPoints?.Clear();
            ClearPlot();
        }

        /// <summary>
        /// 获取指定索引的预定义颜色（供外部使用）
        /// </summary>
        /// <param name="index">颜色索引</param>
        /// <returns>System.Drawing.Color</returns>
        public static System.Drawing.Color GetPredefinedColor(int index)
        {
            var scottColor = GetColorForIndex(index);
            return System.Drawing.Color.FromArgb(scottColor.A, scottColor.R, scottColor.G, scottColor.B);
        }

        /// <summary>
        /// 获取预定义颜色的总数量
        /// </summary>
        public static int PredefinedColorCount => DefaultColors.Length;

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 数据点类
    /// </summary>
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

    /// <summary>
    /// 数据系列类（支持多组数据）
    /// </summary>
    public class PlotDataSeries : INotifyPropertyChanged
    {
        private string? _name;
        private ObservableCollection<PlotDataPoint>? _dataPoints;
        private System.Drawing.Color? _color;
        private float? _lineWidth;
        private float? _markerSize;
        private PlotType? _seriesPlotType;

        /// <summary>
        /// 系列名称（用于图例显示）
        /// </summary>
        public string? Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        /// <summary>
        /// 数据点集合
        /// </summary>
        public ObservableCollection<PlotDataPoint>? DataPoints
        {
            get => _dataPoints;
            set
            {
                _dataPoints = value;
                OnPropertyChanged(nameof(DataPoints));
            }
        }

        /// <summary>
        /// 线条/点颜色
        /// </summary>
        public System.Drawing.Color? Color
        {
            get => _color;
            set
            {
                _color = value;
                OnPropertyChanged(nameof(Color));
            }
        }

        /// <summary>
        /// 线条宽度
        /// </summary>
        public float? LineWidth
        {
            get => _lineWidth;
            set
            {
                _lineWidth = value;
                OnPropertyChanged(nameof(LineWidth));
            }
        }

        /// <summary>
        /// 标记点大小
        /// </summary>
        public float? MarkerSize
        {
            get => _markerSize;
            set
            {
                _markerSize = value;
                OnPropertyChanged(nameof(MarkerSize));
            }
        }

        /// <summary>
        /// 系列特定的图表类型（可选，不设置则使用全局PlotType）
        /// </summary>
        public PlotType? SeriesPlotType
        {
            get => _seriesPlotType;
            set
            {
                _seriesPlotType = value;
                OnPropertyChanged(nameof(SeriesPlotType));
            }
        }

        public PlotDataSeries()
        {
            DataPoints = new ObservableCollection<PlotDataPoint>();
        }

        public PlotDataSeries(string name) : this()
        {
            Name = name;
        }

        public PlotDataSeries(string name, IEnumerable<PlotDataPoint> points) : this(name)
        {
            if (points != null)
            {
                foreach (var point in points)
                {
                    DataPoints!.Add(point);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 图表类型枚举
    /// </summary>
    public enum PlotType
    {
        Line,
        Scatter,
        Bar
    }

    #endregion
}