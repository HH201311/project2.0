using HandyControl.Controls;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces.Data;
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
    /// TBDataChartControl.xaml 的交互逻辑
    /// </summary>
    public partial class TBDataChartControl : UserControl
    {
        private Subject<Unit> _dataUpdateRequests = new Subject<Unit>();
        private IDisposable _subscription;

        /// <summary>
        /// 
        /// </summary>
        public TBDataChartControl()
        {
            InitializeComponent();

            _subscription = _dataUpdateRequests
                            .Throttle(TimeSpan.FromMilliseconds(200)) // 200ms内合并请求
                            .Subscribe(_ => Dispatcher.Invoke(() => DrawChart()));
        }

        private const double yStep = 2;

        #region 事件
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeAxisAndDashLine();
        }

        private void ChartGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _dataUpdateRequests.OnNext(Unit.Default);
        }
        #endregion

        #region ParamOptions
        public ObservableCollection<ParameterModel<string>> ParamOptions
        {
            get { return (ObservableCollection<ParameterModel<string>>)GetValue(ParamOptionsProperty); }
            set { SetValue(ParamOptionsProperty, value); }
        }

        public static readonly DependencyProperty ParamOptionsProperty = DependencyProperty.Register(
            "ParamOptions",
            typeof(ObservableCollection<ParameterModel<string>>),
            typeof(TBDataChartControl),
            new PropertyMetadata(new ObservableCollection<ParameterModel<string>>(), new PropertyChangedCallback(OnParamOptionsChanged))
        );

        private static void OnParamOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as TBDataChartControl;
            if (uc != null)
            {
                uc.ParamOption_IC.ItemsSource = uc.ParamOptions;
            }
        }
        #endregion

        #region ParamOptionSelectedCommand
        public ICommand ParamOptionSelectedCommand
        {
            get { return (ICommand)GetValue(ParamOptionSelectedCommandProperty); }
            set { SetValue(ParamOptionSelectedCommandProperty, value); }
        }

        public static readonly DependencyProperty ParamOptionSelectedCommandProperty = DependencyProperty.Register(
            "ParamOptionSelectedCommand",
            typeof(ICommand),
            typeof(TBDataChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnParamOptionSelectedCommandChanged))
        );

        private static void OnParamOptionSelectedCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as TBDataChartControl;
            if (uc != null)
            {
                uc.ParamOptionSelected_Btn.Command = uc.ParamOptionSelectedCommand;
            }
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
            typeof(TBDataChartControl),
            new PropertyMetadata(370d, new PropertyChangedCallback(OnYMinChanged))
        );

        private static void OnYMinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as TBDataChartControl;
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
            typeof(TBDataChartControl),
            new PropertyMetadata(382d, new PropertyChangedCallback(OnYMaxChanged))
        );

        private static void OnYMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as TBDataChartControl;
            if (uc != null)
            {
                uc._dataUpdateRequests.OnNext(Unit.Default);
            }
        }
        #endregion

        #region DataLimitParam
        public DataLimitParamModel DataLimitParam
        {
            get { return (DataLimitParamModel)GetValue(DataLimitParamProperty); }
            set { SetValue(DataLimitParamProperty, value); }
        }

        public static readonly DependencyProperty DataLimitParamProperty = DependencyProperty.Register(
            "DataLimitParam",
            typeof(DataLimitParamModel),
            typeof(TBDataChartControl),
            new PropertyMetadata(new DataLimitParamModel(372, 374, 376, 378, 380), new PropertyChangedCallback(OnDataLimitParamChanged))
        );

        private static void OnDataLimitParamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as TBDataChartControl;
            if (uc != null)
            {
                // 取消旧对象的事件订阅
                if (e.OldValue is DataLimitParamModel oldModel)
                {
                    oldModel.PropertyChanged -= uc.OnDataLimitParamPropertyChanged;
                }

                // 订阅新对象的事件
                if (e.NewValue is DataLimitParamModel newModel)
                {
                    newModel.PropertyChanged += uc.OnDataLimitParamPropertyChanged;
                }

                uc._dataUpdateRequests.OnNext(Unit.Default);
            }
        }

        private void OnDataLimitParamPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _dataUpdateRequests.OnNext(Unit.Default);
        }
        #endregion

        #region ItemSource
        public IEnumerable<double> ItemSource
        {
            get { return (IEnumerable<double>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(
            "ItemSource",
            typeof(IEnumerable<double>),
            typeof(TBDataChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemSourceChanged))
        );

        private static void OnItemSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as TBDataChartControl;
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

        Line dashLine0 = new(), dashLine1 = new(), dashLine2 = new(), dashLine3 = new(), dashLine4 = new();
        List<Path> barGraphs = [];
        List<Path> lines = [];

        /// <summary>
        /// 初始化坐标轴及虚线
        /// </summary>
        private void InitializeAxisAndDashLine()
        {
            XAxisPainter.RotationAngle = 0;
            XAxisPainter.Update(0, 26, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25);
            YAxisPainter.RotationAngle = 0;
            YAxisPainter.Update(370, 382, 372, 374, 376, 378, 380);

            (double yMin, double yMax) = RefreshYAxis();
            var yRange = yMax - yMin;

            RefreshDashLine(ref dashLine0, DataLimitParam.UpDangerLimit, yMin, yRange, dangerLimitLineStroke);
            RefreshDashLine(ref dashLine1, DataLimitParam.UpWarningLimit, yMin, yRange, warningLimitLineStroke);
            RefreshDashLine(ref dashLine2, DataLimitParam.CenterLimit, yMin, yRange, centerLimitLineStroke);
            RefreshDashLine(ref dashLine3, DataLimitParam.DownWarningLimit, yMin, yRange, warningLimitLineStroke);
            RefreshDashLine(ref dashLine4, DataLimitParam.DownDangerLimit, yMin, yRange, dangerLimitLineStroke);
        }

        private void DrawChart()
        {
            if (ItemSource == null || ItemSource.Count() <= 0)
            {
                InitializeAxisAndDashLine();
                return;
            }

            var datas = ItemSource.ToList();
            var xLabels = ItemSource.Select((v, i) => (i + 1).ToString()).ToList();
            RefreshXAxis(xLabels);
            (double yMin, double yMax) = RefreshYAxis(datas.ToArray());
            var yRange = yMax - yMin;

            RefreshDashLine(ref dashLine0, DataLimitParam.UpDangerLimit, yMin, yRange, dangerLimitLineStroke);
            RefreshDashLine(ref dashLine1, DataLimitParam.UpWarningLimit, yMin, yRange, warningLimitLineStroke);
            RefreshDashLine(ref dashLine2, DataLimitParam.CenterLimit, yMin, yRange, centerLimitLineStroke);
            RefreshDashLine(ref dashLine3, DataLimitParam.DownWarningLimit, yMin, yRange, warningLimitLineStroke);
            RefreshDashLine(ref dashLine4, DataLimitParam.DownDangerLimit, yMin, yRange, dangerLimitLineStroke);
            RefreshBarGraph(datas, yMin, yRange);
            RefreshLine(datas, yMin, yRange);
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

            YAxisPainter.RotationAngle = 0;
            YAxisPainter.Update(yMin, yMax, DataLimitParam.UpDangerLimit, DataLimitParam.UpWarningLimit, DataLimitParam.CenterLimit, DataLimitParam.DownWarningLimit, DataLimitParam.DownDangerLimit);
            return (yMin, yMax);
        }

        #region 虚线
        // 警告线颜色
        private readonly Brush dangerLimitLineStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC5252"));
        private readonly Brush warningLimitLineStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC9629"));
        private readonly Brush centerLimitLineStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3DCC84"));

        private Style? subLineStyle;
        private void RefreshDashLine(ref Line subLine, double yValue, double yMin, double yRange, Brush stroke, double opacity = 0.5)
        {
            if (subLineStyle == null) subLineStyle = this.Resources["SubLineStyle"] as Style;
            subLine.Style = subLineStyle;
            subLine.Stroke = stroke;
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
        // 数据柱状图&数据表格颜色
        private readonly Brush highValueBarStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC5252"));
        private readonly Brush normalValueBarStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3DCC84"));
        private readonly Brush lowValueBarStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF527ACC"));

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
                p.ToolTip = new ToolTip { Style = chartToolTipStyle };
                Panel.SetZIndex(p, 9);
                barGraphs.Add(p);
                ChartGrid.Children.Add(p);
            }
            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1; // 防止除零
            var stepX = w / (datas.Count + 1); // X轴步长
            for (int i = 0; i < datas.Count; i++)
            {
                if (datas[i] == 0 || datas[i] == DataLimitParam.CenterLimit)
                {
                    barGraphs[i].Visibility = Visibility.Hidden;
                    continue;
                }

                var x = (i + 1) * stepX;
                var y = GraphicHelper.Normalize(datas[i], yMin, yRange, h);
                var center = GraphicHelper.Normalize(DataLimitParam.CenterLimit, yMin, yRange, h);
                var pointLB = datas[i] > DataLimitParam.CenterLimit ? new Point(x - 10, center) : new Point(x - 10, y);
                var pointLT = datas[i] > DataLimitParam.CenterLimit ? new Point(x - 10, y) : new Point(x - 10, center);
                var pointRT = datas[i] > DataLimitParam.CenterLimit ? new Point(x + 10, y) : new Point(x + 10, center);
                var pointRB = datas[i] > DataLimitParam.CenterLimit ? new Point(x + 10, center) : new Point(x + 10, y);
                var geometry = datas[i] > DataLimitParam.CenterLimit
                    ? GraphicHelper.CreatUpStreamGeometryWithCorner(pointLB, pointLT, pointRT, pointRB)
                    : GraphicHelper.CreatDownStreamGeometryWithCorner(pointLB, pointLT, pointRT, pointRB);
                barGraphs[i].Data = geometry;
                barGraphs[i].Fill = datas[i] > DataLimitParam.UpWarningLimit ? highValueBarStroke :
                    (datas[i] < DataLimitParam.DownWarningLimit ? lowValueBarStroke : normalValueBarStroke);
                barGraphs[i].Visibility = Visibility.Visible;

                var toolTip = barGraphs[i].ToolTip as ToolTip;
                toolTip!.Content = datas[i].ToString();
            }
        }
        #endregion

        #region 折线
        // 数据折线颜色
        private Style? linePathStyle;
        private void RefreshLine(List<double> datas, double yMin, double yRange)
        {
            if (linePathStyle == null) linePathStyle = this.Resources["LinePathStyle"] as Style;

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            #region 按照0数据分组
            var dataPairs = datas.Select((v, i) => (v, i)).ToList();
            var dataPairGroups = new List<List<(double v, int i)>>();
            var currentDataPairGroup = new List<(double v, int i)>();
            for (int i = 0; i < dataPairs.Count; i++)
            {
                if (dataPairs[i].v == 0 || i == dataPairs.Count - 1)
                {
                    if (currentDataPairGroup.Count > 0)
                    {
                        dataPairGroups.Add(currentDataPairGroup);
                        currentDataPairGroup = new List<(double, int)>();
                    }
                    continue;
                }
                currentDataPairGroup.Add(dataPairs[i]);
            }
            #endregion

            if (yRange == 0) yRange = 1;
            var stepX = w / (datas.Count + 1); // X轴步长
            var partLinePoints = new List<Point>();
            var lineIndex = 0;
            foreach (var dataPairGroup in dataPairGroups)
            {
                if (dataPairGroup.Count <= 1) continue;
                foreach (var dataPair in dataPairGroup)
                {
                    var x = (dataPair.i + 1) * stepX;
                    var y = GraphicHelper.Normalize(dataPair.v, yMin, yRange, h);
                    var point = new Point(x, y);
                    partLinePoints.Add(point);
                }

                var geometry = GraphicHelper.CreatStreamGeometry(partLinePoints);
                partLinePoints.Clear();
                Path line;
                if (lines.Count <= lineIndex)
                {
                    line = new Path() { Style = linePathStyle };
                    Panel.SetZIndex(line, 10);
                    lines.Add(line);
                    ChartGrid.Children.Add(line);
                }
                line = lines[lineIndex++];
                line.Data = geometry;
            }

            while (lines.Count > lineIndex)
            {
                var line = lines[lines.Count - 1];
                if (ChartGrid.Children.Contains(line)) ChartGrid.Children.Remove(line);
                lines.Remove(line);
            }
        }
        #endregion
    }
}
