using SUNWODA_SEVB.Component.UserControls.Project.CCTB.DataGridControls;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection.Metadata;
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
    /// CCDDataChartControl.xaml 的交互逻辑
    /// </summary>
    public partial class CCDDataChartControl : UserControl
    {
        private Subject<Unit> _dataUpdateRequests = new Subject<Unit>();
        private IDisposable _subscription;
        public CCDDataChartControl()
        {
            InitializeComponent();
            UpdateChart(1);
            _subscription = _dataUpdateRequests
                            .Throttle(TimeSpan.FromMilliseconds(200)) // 200ms内合并请求
                            .Subscribe(_ => Dispatcher.Invoke(() => DrawChart()));
        }

        private void ChartGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _dataUpdateRequests.OnNext(Unit.Default);
        }

        #region BaseMaterialValue
        public double BaseMaterialValue
        {
            get { return (double)GetValue(BaseMaterialValueProperty); }
            set { SetValue(BaseMaterialValueProperty, value); }
        }

        public static readonly DependencyProperty BaseMaterialValueProperty = DependencyProperty.Register(
            nameof(BaseMaterialValue),
            typeof(double),
            typeof(CCDDataChartControl),
            new PropertyMetadata(0.000d, new PropertyChangedCallback(OnBaseMaterialValueChanged))
        );

        private static void OnBaseMaterialValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CCDDataChartControl;
            if (uc != null)
            {
                uc.BaseMaterialValueTB.Text = uc.BaseMaterialValue.ToString();
            }
        }
        #endregion

        #region TitleHeaderA
        public string TitleHeaderA
        {
            get { return (string)GetValue(TitleHeaderAProperty); }
            set { SetValue(TitleHeaderAProperty, value); }
        }

        public static readonly DependencyProperty TitleHeaderAProperty = DependencyProperty.Register(
            nameof(TitleHeaderA),
            typeof(string),
            typeof(CCDDataChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnTitleHeaderAChanged))
        );

        private static void OnTitleHeaderAChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CCDDataChartControl;
            if (uc != null && uc.TitleHeaderA != null)
            {
                uc.HeaderA.Text = uc.TitleHeaderA.ToString();
            }
        }
        #endregion

        #region TitleHeaderB
        public string TitleHeaderB
        {
            get { return (string)GetValue(TitleHeaderBProperty); }
            set { SetValue(TitleHeaderBProperty, value); }
        }

        public static readonly DependencyProperty TitleHeaderBProperty = DependencyProperty.Register(
            nameof(TitleHeaderB),
            typeof(string),
            typeof(CCDDataChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnTitleHeaderBChanged))
        );

        private static void OnTitleHeaderBChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CCDDataChartControl;
            if (uc != null && uc.TitleHeaderB != null)
            {
                uc.HeaderB.Text = uc.TitleHeaderB.ToString();
            }
        }
        #endregion

        #region ItemASource
        public CCDModel ItemASource
        {
            get { return (CCDModel)GetValue(ItemASourceProperty); }
            set { SetValue(ItemASourceProperty, value); }
        }

        public static readonly DependencyProperty ItemASourceProperty = DependencyProperty.Register(
            nameof(ItemASource),
            typeof(CCDModel),
            typeof(CCDDataChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemASourceChanged))
        );

        private static void OnItemASourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CCDDataChartControl;
            if (uc != null && uc.ItemASource != null)
            {
                uc._dataUpdateRequests.OnNext(Unit.Default);
            }
        }
        #endregion

        #region ItemBSource
        public CCDModel ItemBSource
        {
            get { return (CCDModel)GetValue(ItemBSourceProperty); }
            set { SetValue(ItemBSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemBSourceProperty = DependencyProperty.Register(
            nameof(ItemBSource),
            typeof(CCDModel),
            typeof(CCDDataChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemBSourceChanged))
        );

        private static void OnItemBSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CCDDataChartControl;
            if (uc != null && uc.ItemBSource != null)
            {
                uc._dataUpdateRequests.OnNext(Unit.Default);
            }
        }
        #endregion

        private void DrawChart()
        {
            // 获取最大膜区数
            var maxCountA = GetFilmCount(ItemASource);
            var maxCountB = GetFilmCount(ItemBSource);
            var maxCount = Math.Max(maxCountA, maxCountB);
            if (maxCount == 0)
            {
                UpdateChart(1);
                return;
            }
            UpdateChart(maxCount);

            if (ItemASource != null)
                UpdateData(ref DataGridA, ref tbsA, ItemASource, true, maxCount);
            if (ItemBSource != null)
                UpdateData(ref DataGridB, ref tbsB, ItemBSource, false, maxCount);
        }

        /// <summary>
        /// 获取最大膜区数
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        private int GetFilmCount(CCDModel? model)
        {
            if (model == null) return 0;
            var filmCount1 = model.BatteryTabDatas?.Length - 1 ?? 0;
            var filmCount2 = model.CeramicDatas?.Length / 2 ?? 0;
            var filmCount3 = model.FilmDatas?.Length ?? 0;
            var filmCount4 = model.MalpositionDatas?.Length / 2 ?? 0;
            var maxFilmCount = MathHelper.Max(filmCount1, filmCount2, filmCount3, filmCount4);
            return maxFilmCount;
        }

        private Style? subLineStyle, barGraphStyle;
        private Line subLine = new();
        private List<Path> flimBarGraphs = new(), malpositionBarGraphs = new();
        private void UpdateChart(int maxFilmCount = 1)
        {
            RefreshFlimBarGraphs(maxFilmCount);
            RefreshMalpositionBarGraphs(maxFilmCount);
            RefershSubline();
        }

        private void RefreshFlimBarGraphs(int maxFilmCount = 1)
        {
            if (barGraphStyle == null) barGraphStyle = this.Resources["BarGraphPathStyle"] as Style;
            while (flimBarGraphs.Count > maxFilmCount)
            {
                var p = flimBarGraphs[flimBarGraphs.Count - 1];
                if (ChartGrid.Children.Contains(p)) ChartGrid.Children.Remove(p);
                flimBarGraphs.Remove(p);
                p.Data = null;
            }
            while (flimBarGraphs.Count < maxFilmCount)
            {
                var p = new Path { Style = barGraphStyle };
                flimBarGraphs.Add(p);
                ChartGrid.Children.Add(p);
            }

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            var length = (w - 80 - 80 - 50 * (maxFilmCount - 1)) / maxFilmCount;
            for (int i = 0; i < maxFilmCount; i++)
            {
                var pointLT = new Point(80 + (50 + length) * i, 0);
                var pointRT = new Point(80 + (50 + length) * i + length, 0);
                var pointRB = new Point(80 + (50 + length) * i + length, w);
                var pointLB = new Point(80 + (50 + length) * i, w);
                var geometry = GraphicHelper.CreatStreamGeometry(pointLT, pointRT, pointRB, pointLB);
                flimBarGraphs[i].Data = geometry;
            }
        }

        private void RefreshMalpositionBarGraphs(int maxFilmCount = 1)
        {
            if (barGraphStyle == null) barGraphStyle = this.Resources["BarGraphPathStyle"] as Style;
            while (malpositionBarGraphs.Count > 2 * maxFilmCount)
            {
                var p = malpositionBarGraphs[malpositionBarGraphs.Count - 1];
                if (ChartGrid.Children.Contains(p)) ChartGrid.Children.Remove(p);
                malpositionBarGraphs.Remove(p);
                p.Data = null;
            }
            while (malpositionBarGraphs.Count < 2 * maxFilmCount)
            {
                var p = new Path { Style = barGraphStyle };
                malpositionBarGraphs.Add(p);
                ChartGrid.Children.Add(p);
            }

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            var length = (w - 80 - 80 - 50 * (maxFilmCount - 1)) / maxFilmCount;
            for (int i = 0; i < maxFilmCount; i++)
            {
                var pointLT = new Point(80 + (50 + length) * i, 0);
                var pointRT = new Point(80 + (50 + length) * i + 50, 0);
                var pointRB = new Point(80 + (50 + length) * i + 50, w);
                var pointLB = new Point(80 + (50 + length) * i, w);
                var geometry = GraphicHelper.CreatStreamGeometry(pointLT, pointRT, pointRB, pointLB);
                malpositionBarGraphs[2 * i].Data = geometry;

                pointLT = new Point(80 + (50 + length) * i + length - 50, 0);
                pointRT = new Point(80 + (50 + length) * i + length, 0);
                pointRB = new Point(80 + (50 + length) * i + length, w);
                pointLB = new Point(80 + (50 + length) * i + length - 50, w);
                geometry = GraphicHelper.CreatStreamGeometry(pointLT, pointRT, pointRB, pointLB);
                malpositionBarGraphs[2 * i + 1].Data = geometry;
            }
        }

        private void RefershSubline()
        {
            if (subLineStyle == null) subLineStyle = this.Resources["SubLineStyle"] as Style;
            subLine.Style = subLineStyle;

            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            subLine.X1 = 0;
            subLine.X2 = w;
            subLine.Y1 = h / 2;
            subLine.Y2 = h / 2;

            if (!ChartGrid.Children.Contains(subLine))
            {
                ChartGrid.Children.Add(subLine);
            }
        }

        private Style? tbStyle;
        private List<TextBlock> tbsA = new(), tbsB = new();

        private void UpdateData(ref Grid dataGrid, ref List<TextBlock> tbs, CCDModel itemSource, bool isDataA = true, int maxFilmCount = 1)
        {
            if (maxFilmCount < 1) return;
            var columnCount = 1 + 6 * maxFilmCount;
            if (columnCount <= 0) return;
            // 初始化Grid
            if (dataGrid.ColumnDefinitions.Count != columnCount)
            {
                dataGrid.ColumnDefinitions.Clear();
                for (int i = 0; i < maxFilmCount; i++)
                {
                    // 极耳1
                    if (i == 0) dataGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50, GridUnitType.Pixel) });
                    // 陶瓷-错位-涂膜-错位-陶瓷-极耳
                    dataGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50, GridUnitType.Pixel) });
                    dataGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                    dataGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50, GridUnitType.Pixel) });
                    dataGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                    dataGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50, GridUnitType.Pixel) });
                    dataGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50, GridUnitType.Pixel) });
                }
            }
            if (tbStyle == null) tbStyle = this.Resources["ValueTextBlockStyle"] as Style;
            while (tbs.Count > columnCount)
            {
                var tb = tbs[tbs.Count - 1];
                if (dataGrid.Children.Contains(tb)) dataGrid.Children.Remove(tb);
                tbs.Remove(tb);
            }
            while (tbs.Count < columnCount)
            {
                var tb = new TextBlock();
                tb.Style = tbStyle;
                tbs.Add(tb);
                dataGrid.Children.Add(tb);
                Grid.SetRow(tb, 0);
                Grid.SetColumn(tb, tbs.Count - 1);
            }
            var value = 0d;
            for (int i = 0; i < maxFilmCount; i++)
            {
                if (i == 0)// 极耳1
                {
                    value = itemSource.BatteryTabDatas?.Length > 0 ? itemSource.BatteryTabDatas[0] : 0;
                    tbs[0].Text = $"{value:F2}";
                    tbs[0].Foreground = ConvertToForeground(value, itemSource.BatteryTabLimitDown, itemSource.BatteryTabLimitUp);
                }
                // 陶瓷
                value = itemSource.CeramicDatas?.Length > 2 * i ? itemSource.CeramicDatas[2 * i] : 0;
                tbs[i * 6 + 1].Text = $"{value:F2}";
                tbs[i * 6 + 1].Foreground = ConvertToForeground(value, itemSource.CeramicLimitDown, itemSource.CeramicLimitUp);
                // 错位
                value = itemSource.MalpositionDatas?.Length > 2 * i ? itemSource.MalpositionDatas[2 * i] : 0;
                tbs[i * 6 + 2].Text = $"({value:F2})";
                tbs[i * 6 + 2].Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCFFFFFF"));
                if (!isDataA) tbs[i * 6 + 2].Visibility = Visibility.Collapsed;
                // 涂膜
                value = itemSource.FilmDatas?.Length > i ? itemSource.FilmDatas[i] : 0;
                tbs[i * 6 + 3].Text = $"{value:F2}";
                tbs[i * 6 + 3].Foreground = ConvertToForeground(value, itemSource.FilmLimitDown, itemSource.FilmLimitUp);
                // 错位
                value = itemSource.MalpositionDatas?.Length > 2 * i + 1 ? itemSource.MalpositionDatas[2 * i + 1] : 0;
                tbs[i * 6 + 4].Text = $"({value:F2})";
                tbs[i * 6 + 4].Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCFFFFFF"));
                if (!isDataA) tbs[i * 6 + 4].Visibility = Visibility.Collapsed;
                // 陶瓷
                value = itemSource.CeramicDatas?.Length > 2 * i + 1 ? itemSource.CeramicDatas[2 * i + 1] : 0;
                tbs[i * 6 + 5].Text = $"{value:F2}";
                tbs[i * 6 + 5].Foreground = ConvertToForeground(value, itemSource.CeramicLimitDown, itemSource.CeramicLimitUp);
                // 极耳
                value = itemSource.BatteryTabDatas?.Length > i + 1 ? itemSource.BatteryTabDatas[i + 1] : 0;
                tbs[i * 6 + 6].Text = $"{value:F2}";
                tbs[i * 6 + 6].Foreground = ConvertToForeground(value, itemSource.BatteryTabLimitDown, itemSource.BatteryTabLimitUp);
            }
        }

        private static readonly SolidColorBrush highValueForeStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC5252"));
        private static readonly SolidColorBrush defaultValueForeStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3DCC84"));
        private static readonly SolidColorBrush lowValueForeStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF527ACC"));

        private Brush? ConvertToForeground(object? value, double limitDown, double limitUp)
        {
            if (value == null) return Brushes.Transparent;
            var isSuccess = double.TryParse(value.ToString(), out double cellValue);
            if (!isSuccess) return defaultValueForeStroke;
            if (limitDown > limitUp) return defaultValueForeStroke;

            if (cellValue >= limitUp) return highValueForeStroke;
            else if (cellValue <= limitDown) return lowValueForeStroke;
            else return defaultValueForeStroke;
        }
    }
}
