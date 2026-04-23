using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
    /// TBlockChartControl.xaml 的交互逻辑
    /// </summary>
    public partial class TBlockChartControl : UserControl
    {
        private Subject<Unit> _dataUpdateRequests = new Subject<Unit>();
        private IDisposable _subscription;

        /// <summary>
        /// 
        /// </summary>
        public TBlockChartControl()
        {
            InitializeComponent(); 

            _subscription = _dataUpdateRequests
                            .Throttle(TimeSpan.FromMilliseconds(200)) // 200ms内合并请求
                            .Subscribe(_ => Dispatcher.Invoke(() => DrawChart()));
        }

        private const double yStep = 200;
        private const double centerValue = 0;

        #region 事件
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeAxisAndSubLine();
        }

        private void ChartGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _dataUpdateRequests.OnNext(Unit.Default);
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
            typeof(TBlockChartControl),
            new PropertyMetadata(-300d, new PropertyChangedCallback(OnYMinChanged))
        );

        private static void OnYMinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as TBlockChartControl;
            if (uc != null)
            {
                uc._dataUpdateRequests.OnNext(Unit.Default);
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
            typeof(TBlockChartControl),
            new PropertyMetadata(1100d, new PropertyChangedCallback(OnYMaxChanged))
        );

        private static void OnYMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as TBlockChartControl;
            if (uc != null)
            {
                uc._dataUpdateRequests.OnNext(Unit.Default);
            }
        }
        #endregion

        #region ItemSource
        public IEnumerable<TBlockModel> ItemSource
        {
            get { return (IEnumerable<TBlockModel>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(
            "ItemSource",
            typeof(IEnumerable<TBlockModel>),
            typeof(TBlockChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemSourceChanged))
        );

        private static void OnItemSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as TBlockChartControl;
            if (uc != null && uc.ItemSource != null)
            {
                // A. 防止内存泄漏：取消订阅旧集合的事件
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= uc.OnItemSourcePropertyChanged;
                }

                // B. 订阅新集合的事件
                if (e.NewValue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += uc.OnItemSourcePropertyChanged;
                }

                // C. 数据源变了，立即重绘一次
                uc._dataUpdateRequests.OnNext(Unit.Default);
            }
        }

        private void OnItemSourcePropertyChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 当数据增加、删除、重置时，重新绘制
            _dataUpdateRequests.OnNext(Unit.Default);
        }
        #endregion

        Line subLine = new();
        List<Path> barGraphs = [];

        /// <summary>
        /// 初始化坐标轴及虚线
        /// </summary>
        private void InitializeAxisAndSubLine()
        {
            XAxisPainter.RotationAngle = 0;
            XAxisPainter.Update(0, 26, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25);
            YAxisPainter.RotationAngle = 0;
            YAxisPainter.IsReverse = true;
            YAxisPainter.Update(-300, 1100, -300, -100, 100, 300, 500, 700, 900, 1100);

            (double yMin, double yMax) = RefreshYAxis();
            var yRange = yMax - yMin;

            RefreshSubLine(ref subLine, 0, yMin, yRange, subLineStroke);
        }

        private void DrawChart()
        {
            if (ItemSource == null || ItemSource.Count() <= 0)
            {
                InitializeAxisAndSubLine();
                return;
            }

            var datas = ItemSource.ToList();
            var xLabels = ItemSource.Select((v, i) => (i + 1).ToString()).ToList();
            RefreshXAxis(xLabels);
            (double yMin, double yMax) = RefreshYAxis(datas.Select(o => o.TBlockPosition).ToArray());
            var yRange = yMax - yMin;

            RefreshSubLine(ref subLine, 0, yMin, yRange, subLineStroke);
            RefreshBarGraph(datas, yMin, yRange);
        }

        private void RefreshXAxis(List<string> labels)
        {
            labels.Insert(0, "");
            labels.Add("");
            XAxisPainter.RotationAngle = 0;
            XAxisPainter.AvoidAxisLabel = false;
            XAxisPainter.Update(labels.ToArray());
        }

        private (double yMin, double yMax) RefreshYAxis(params double[] values)
        {
            var datas = values.Where(o => o != 0).ToArray();
            var vMin = datas?.Length > 0 ? datas.Min() : 0;
            var vMax = datas?.Length > 0 ? datas.Max() : 0;
            var range = vMax - vMin;
            double yMin = vMin - range / 5, yMax = vMax + range / 5;
            if (YMax > YMin)
            {
                yMin = YMin; yMax = YMax;
                if (datas?.Length > 0)
                {
                    while (yMin >= vMin) yMin -= yStep;
                    while (yMax <= vMax) yMax += yStep;
                }
            }

            var yValues = new List<double>();
            for (var i = yMin; i <= yMax; i += yStep) yValues.Add(i);

            YAxisPainter.RotationAngle = 0;
            YAxisPainter.Update(yMin, yMax, yValues.ToArray());
            return (yMin, yMax);
        }

        #region 辅助线
        private readonly Brush subLineStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3DCC84"));

        private Style? subLineStyle;
        private void RefreshSubLine(ref Line subLine, double yValue, double yMin, double yRange, Brush stroke, double opacity = 0.5)
        {
            if (subLineStyle == null) subLineStyle = this.Resources["SubLineStyle"] as Style;
            subLine.Style = subLineStyle;
            subLine.Stroke = stroke;
            subLine.Opacity = opacity;
            Panel.SetZIndex(subLine, 1);

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1;
            var y = GraphicHelper.Normalize(yValue, yMin, yRange, h, true);
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
        // 数据柱状图&数据表格颜色
        private readonly Brush autoBarStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3DCC84"));
        private readonly Brush semiAutoBarStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF527ACC"));
        private readonly Brush adjustBarStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCFFFFFF"));

        private Style? barGraphStyle;
        private Style? chartToolTipStyle;
        private void RefreshBarGraph(List<TBlockModel> datas, double yMin, double yRange)
        {
            if (barGraphStyle == null) barGraphStyle = this.Resources["BarGraphPathStyle"] as Style;
            if (chartToolTipStyle == null) chartToolTipStyle = this.Resources["ChartToolTipStyle"] as Style;
            while (barGraphs.Count > datas.Count)
            {
                var p = barGraphs[barGraphs.Count - 1];
                if (ChartGrid.Children.Contains(p)) ChartGrid.Children.Remove(p);
                barGraphs.Remove(p);
            }
            while (barGraphs.Count < datas.Count())
            {
                var p = new Path() { Style = barGraphStyle };
                p.ToolTip = new ToolTip { Style = chartToolTipStyle };
                Panel.SetZIndex(p, 10);
                barGraphs.Add(p);
                ChartGrid.Children.Add(p);
            }
            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1; // 防止除零
            var stepX = w / (datas.Count + 1); // X轴步长
            for (int i = 0; i < datas.Count; i++)
            {
                if (datas[i].TBlockPosition == centerValue)
                {
                    barGraphs[i].Visibility = Visibility.Hidden;
                    continue;
                }
                var x = (i + 1) * stepX;
                var y = GraphicHelper.Normalize(datas[i].TBlockPosition, yMin, yRange, h, true);
                var center = GraphicHelper.Normalize(0, yMin, yRange, h, true);
                var pointLB = datas[i].TBlockPosition > centerValue ? new Point(x - 10, center) : new Point(x - 10, y);
                var pointLT = datas[i].TBlockPosition > centerValue ? new Point(x - 10, y) : new Point(x - 10, center);
                var pointRT = datas[i].TBlockPosition > centerValue ? new Point(x + 10, y) : new Point(x + 10, center);
                var pointRB = datas[i].TBlockPosition > centerValue ? new Point(x + 10, center) : new Point(x + 10, y);
                var geometry = datas[i].TBlockPosition > centerValue
                    ? GraphicHelper.CreatUpStreamGeometryWithCorner(pointLB, pointLT, pointRT, pointRB, true)
                    : GraphicHelper.CreatDownStreamGeometryWithCorner(pointLB, pointLT, pointRT, pointRB, true);
                barGraphs[i].Data = geometry;
                barGraphs[i].Fill = datas[i].IsAuto ? autoBarStroke : semiAutoBarStroke;
                barGraphs[i].Visibility = Visibility.Visible;

                var toolTip = barGraphs[i].ToolTip as ToolTip;
                toolTip!.Content = datas[i].TBlockPosition.ToString();
            }
        }
        #endregion
    }
}
