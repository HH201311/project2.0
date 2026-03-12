using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Tool.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.DataGridControls
{
    /// <summary>
    /// CCDDataGridControl.xaml 的交互逻辑
    /// </summary>
    public partial class CCDDataGridControl : UserControl
    {
        private Subject<Unit> _dataUpdateRequests = new Subject<Unit>();
        private IDisposable _subscription;
        public CCDDataGridControl()
        {
            InitializeComponent();
            // 初始化列-默认1膜区
            UpdateColumns(1);

            DC.ItemsSource = Columns;
            DR.ItemsSource = Rows; 
            _subscription = _dataUpdateRequests
                            .Throttle(TimeSpan.FromMilliseconds(200)) // 200ms内合并请求
                            .Subscribe(_ => Dispatcher.Invoke(() => DrawChart()));
        }

        public ObservableCollection<string> Columns { get; set; } = new();
        public ObservableCollection<List<CellItem>> Rows { get; set; } = new();

        #region ItemsSource
        public ObservableCollection<CCDModel> ItemsSource
        {
            get { return (ObservableCollection<CCDModel>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<CCDModel>),
            typeof(CCDDataGridControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemsSourceChanged))
        );

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CCDDataGridControl;
            if (uc != null && uc.ItemsSource != null)
            {
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= uc.OnCollectionChanged;
                }
                if (e.NewValue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += uc.OnCollectionChanged;
                }
                var arg = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                uc.OnCollectionChanged(null, arg);
            }
        }

        /// <summary>
        /// 更新表头、数据行等
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (ItemsSource == null || ItemsSource.Count == 0) return;

            _dataUpdateRequests.OnNext(Unit.Default);

            //如果是新增，则拉倒最新行
            if (e.Action == NotifyCollectionChangedAction.Add ||
                e.Action == NotifyCollectionChangedAction.Reset ||
                e.Action == NotifyCollectionChangedAction.Replace)
                Sv?.ScrollToEnd();
        }
        #endregion

        private void DrawChart()
        {
            var sw = new Stopwatch();
            sw.Start();
            // 获取最大列数
            var maxCount = GetMaxFilmCount();
            if (maxCount == 0) return;

            // 更新表头
            UpdateColumns(maxCount);
            // 更新数据行
            UpdateRows(maxCount);
            sw.Stop();
            var t = sw.ElapsedMilliseconds;
        }

        /// <summary>
        /// 获取最大膜区数
        /// </summary>
        /// <returns></returns>
        private int GetMaxFilmCount()
        {
            if (ItemsSource == null || ItemsSource.Count == 0) return 0;
            var maxFilmCount = ItemsSource.Max(o => GetFilmCount(o));
            return maxFilmCount;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="maxCount"></param>
        private void UpdateColumns(int maxFilmCount = 1)
        {
            if (maxFilmCount < 1) return;
            var columnCount = 1 + 6 * maxFilmCount;
            if (Columns.Count == columnCount || columnCount <= 0) return;

            Columns.Clear();
            // 第一列是“时间”
            Columns.Add($"时间");
            // 以“极耳-陶瓷-涂膜-陶瓷-极耳”一个膜区的顺序
            for (int i = 0; i < maxFilmCount; i++)
            {
                if (i == 0) Columns.Add($"极耳1");
                Columns.Add($"陶瓷{2 * i + 1}");
                Columns.Add($"涂膜{i + 1}");
                Columns.Add($"陶瓷{2 * i + 2}");
                Columns.Add($"极耳{i + 2}");
            }
            // 错位放在最右边
            for (int i = 0; i < maxFilmCount; i++)
            {
                Columns.Add($"错位{2 * i + 1}");
                Columns.Add($"错位{2 * i + 2}");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void UpdateRows(int maxFilmCount = 1)
        {
            if (ItemsSource == null || ItemsSource.Count == 0 || maxFilmCount <= 0) return;
            var maxColumnCount = 2 + 6 * maxFilmCount;
            if (maxColumnCount <= 0) return;
            // 多个删，少了加
            while (Rows.Count > ItemsSource.Count) Rows.RemoveAt(0);
            while (Rows.Count < ItemsSource.Count) Rows.Add(new List<CellItem>(maxColumnCount));

            for (int i = 0; i < ItemsSource.Count; i++)
            {
                var item = ItemsSource[i];
                if (Rows.Count < i + 1) Rows.Add(new List<CellItem>(maxColumnCount));

                var row = Rows[i];
                // 多个删，少了加
                while (row.Count > maxColumnCount) row.RemoveAt(0);
                while (row.Count < maxColumnCount) row.Add(new CellItem());

                var cellIndex = 0;
                // 更新时间
                row[cellIndex++].Refresh(item.GatherTime.ToString("HH:mm:ss"));
                // 更新极耳/陶瓷/涂膜数据
                var content = 0d;
                for (int j = 0; j < maxFilmCount; j++)
                {
                    if (j == 0)// 极耳1
                    {
                        content = item.BatteryTabDatas?.Length > 0 ? item.BatteryTabDatas[0] : 0;
                        row[cellIndex++].Refresh(content, item.BatteryTabLimitDown, item.BatteryTabLimitUp);
                    }
                    // 陶瓷
                    content = item.CeramicDatas?.Length > (2 * j) ? item.CeramicDatas[2 * j] : 0;
                    row[cellIndex++].Refresh(content, item.CeramicLimitDown, item.CeramicLimitUp);
                    // 涂膜
                    content = item.FilmDatas?.Length > j ? item.FilmDatas[j] : 0;
                    row[cellIndex++].Refresh(content, item.FilmLimitDown, item.FilmLimitUp);
                    // 陶瓷
                    content = item.CeramicDatas?.Length > (2 * j + 1) ? item.CeramicDatas[2 * j + 1] : 0;
                    row[cellIndex++].Refresh(content, item.CeramicLimitDown, item.CeramicLimitUp);
                    // 极耳
                    content = item.BatteryTabDatas?.Length > j + 1 ? item.BatteryTabDatas[j + 1] : 0;
                    row[cellIndex++].Refresh(content, item.BatteryTabLimitDown, item.BatteryTabLimitUp);
                }
                // 更新错位数据
                for (int j = 0; j < maxFilmCount; j++)
                {
                    content = item.MalpositionDatas?.Length > 2 * j ? item.MalpositionDatas[2 * j] : 0;
                    row[cellIndex++].Refresh(content, item.MalpositionLimitDown, item.MalpositionLimitUp);
                    content = item.MalpositionDatas?.Length > 2 * j + 1 ? item.MalpositionDatas[2 * j + 1] : 0;
                    row[cellIndex++].Refresh(content, item.MalpositionLimitDown, item.MalpositionLimitUp);
                }
            }
        }

        /// <summary>
        /// 单元格对象
        /// </summary>
        public class CellItem : ModelBase
        {
            private static readonly Brush highValueForeStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC5252"));
            private static readonly Brush defaultValueForeStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#61FFFFFF"));
            private static readonly Brush lowValueForeStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF527ACC"));

            private static readonly Brush highValueBackStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC5252"));
            private static readonly Brush defaultValueBackStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("Transparent"));
            private static readonly Brush lowValueBackStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF527ACC"));

            private object? content;
            private Brush? background;
            private double backOpacity;
            private Brush? foreground;
            private double foreOpacity;

            public object? Content
            {
                get => content;
                set => SetProperty(ref content, value);
            }

            public Brush? Background
            {
                get => background;
                set => SetProperty(ref background, value);
            }

            public double BackOpacity
            {
                get => backOpacity;
                set => SetProperty(ref backOpacity, value);
            }

            public Brush? Foreground
            {
                get => foreground;
                set => SetProperty(ref foreground, value);
            }

            public double ForeOpacity
            {
                get => foreOpacity;
                set => SetProperty(ref foreOpacity, value);
            }

            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="content"></param>
            /// <param name="limitDown"></param>
            /// <param name="limitUp"></param>
            public void Refresh(object? content, double limitDown = 0, double limitUp = 0)
            {
                Content = content;
                Background = ConvertToBackground(content, limitDown, limitUp);
                BackOpacity = ConvertToBackOpacity(content, limitDown, limitUp);
                Foreground = ConvertToForeground(content, limitDown, limitUp);
                ForeOpacity = ConvertToForeOpacity(content, limitDown, limitUp);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="value"></param>
            /// <param name="dataLimitParam"></param>
            /// <returns></returns>
            private static Brush? ConvertToBackground(object? value, double limitDown, double limitUp)
            {
                if (value == null) return defaultValueBackStroke;
                var isSuccess = double.TryParse(value.ToString(), out double cellValue);
                if (!isSuccess) return defaultValueBackStroke;
                if (limitDown > limitUp) return defaultValueBackStroke;

                if (cellValue >= limitUp) return highValueBackStroke;
                else if (cellValue <= limitDown) return lowValueBackStroke;
                else return defaultValueBackStroke;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="value"></param>
            /// <param name="dataLimitParam"></param>
            /// <returns></returns>
            private static double ConvertToBackOpacity(object? value, double limitDown, double limitUp)
            {
                if (value == null) return 1;
                var isSuccess = double.TryParse(value.ToString(), out double cellValue);
                if (!isSuccess) return 1;
                if (limitDown > limitUp) return 1;

                if (cellValue >= limitUp) return 0.2;
                else if (cellValue <= limitDown) return 0.2;
                else return 1;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="value"></param>
            /// <param name="dataLimitParam"></param>
            /// <returns></returns>
            private static Brush? ConvertToForeground(object? value, double limitDown, double limitUp)
            {
                if (value == null) return Brushes.Transparent;
                var isSuccess = double.TryParse(value.ToString(), out double cellValue);
                if (!isSuccess) return defaultValueForeStroke;
                if (limitDown > limitUp) return defaultValueForeStroke;

                if (cellValue >= limitUp) return highValueForeStroke;
                else if (cellValue <= limitDown) return lowValueForeStroke;
                else return defaultValueForeStroke;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="value"></param>
            /// <param name="dataLimitParam"></param>
            /// <returns></returns>
            private static double ConvertToForeOpacity(object? value, double limitDown, double limitUp)
            {
                return 1;
            }
        }
    }
}
