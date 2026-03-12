using Newtonsoft.Json.Linq;
using ScottPlot.Colormaps;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.DataGridControls
{
    /// <summary>
    /// TBDataGridControl.xaml 的交互逻辑
    /// </summary>
    public partial class TBDataGridControl : UserControl
    {
        private Subject<Unit> _dataUpdateRequests = new Subject<Unit>();
        private IDisposable _subscription;

        /// <summary>
        /// 
        /// </summary>
        public TBDataGridControl()
        {
            InitializeComponent();
            for (int i = 0; i < 25; i++) Columns.Add($"P{i}");

            DC.ItemsSource = Columns;
            DR.ItemsSource = Rows;

            _subscription = _dataUpdateRequests
                   .Throttle(TimeSpan.FromMilliseconds(200)) // 200ms内合并请求
                   .Subscribe(_ => Dispatcher.Invoke(() => DrawChart()));
        }

        public ObservableCollection<string> Columns { get; set; } = new();
        public ObservableCollection<List<CellItem>> Rows { get; set; } = new();

        #region DataLimitParam
        /// <summary>
        /// 注册依赖属性，支持WPF数据绑定、样式触发等特性
        /// </summary>
        public static readonly DependencyProperty DataLimitParamProperty = DependencyProperty.Register(
            nameof(DataLimitParam),
            typeof(DataLimitParamModel),
            typeof(TBDataGridControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnDataLimitParamChanged))
        );

        /// <summary>
        /// 阈值参数模型，支持数据绑定
        /// </summary>
        public DataLimitParamModel DataLimitParam
        {
            get => (DataLimitParamModel)GetValue(DataLimitParamProperty);
            set => SetValue(DataLimitParamProperty, value);
        }

        private static void OnDataLimitParamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as TBDataGridControl;
            if (uc != null && uc.DataLimitParam != null)
            {
                // 解绑旧模型的监听
                if (e.OldValue is INotifyPropertyChanged oldModel)
                {
                    oldModel.PropertyChanged -= uc.OnDataLimitParamModelChanged;
                }

                // 绑定新模型的监听
                if (e.NewValue is INotifyPropertyChanged newModel)
                {
                    newModel.PropertyChanged += uc.OnDataLimitParamModelChanged;
                }

                // 更新行
                uc.OnDataLimitParamModelChanged(null, new PropertyChangedEventArgs(nameof(DataLimitParam)));
            }
        }

        private void OnDataLimitParamModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            _dataUpdateRequests.OnNext(Unit.Default);
        }
        #endregion

        #region ItemsSource
        public ObservableCollection<double[]> ItemsSource
        {
            get { return (ObservableCollection<double[]>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<double[]>),
            typeof(TBDataGridControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemsSourceChanged))
        );

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as TBDataGridControl;
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
            }
        }

        /// <summary>
        /// 更新表头、数据行等
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (ItemsSource == null || ItemsSource.Count == 0) return;

            _dataUpdateRequests.OnNext(Unit.Default);

            //如果是新增，则拉倒最新行
            if (e.Action == NotifyCollectionChangedAction.Add)
                Sv?.ScrollToEnd();
        }
        #endregion

        private void DrawChart()
        {
            // 获取最大列数
            var maxCount = GetMaxCount();
            if (maxCount == 0) return;

            // 更新表头
            UpdateColumns(maxCount);
            // 更新数据行
            UpdateRows(maxCount);
        }

        /// <summary>
        /// 获取最大列数
        /// </summary>
        /// <returns></returns>
        private int GetMaxCount()
        {
            if (ItemsSource == null || ItemsSource.Count == 0) return 0;
            var maxCount = ItemsSource.Max(o => o?.Length ?? 0);
            return maxCount;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="maxCount"></param>
        private void UpdateColumns(int maxCount = 25)
        {
            if (Columns.Count == maxCount || maxCount <= 0) return;

            Columns.Clear();
            for (int i = Columns.Count; i < maxCount; i++) Columns.Add($"P{i}");
        }

        /// <summary>
        /// 
        /// </summary>
        private void UpdateRows(int maxCount = 25, bool dataLimitParamChanged = false)
        {
            if (ItemsSource == null || ItemsSource.Count == 0 || maxCount <= 0) return;

            // 多个删，少了加
            while (Rows.Count > ItemsSource.Count) Rows.RemoveAt(0);
            while (Rows.Count < ItemsSource.Count) Rows.Add(new List<CellItem>(maxCount));

            for (int i = 0; i < ItemsSource.Count; i++)
            {
                var item = ItemsSource[i];
                if (Rows.Count < i + 1) Rows.Add(new List<CellItem>(maxCount));

                var row = Rows[i];
                // 多个删，少了加
                while (row.Count > maxCount) row.RemoveAt(0);
                while (row.Count < maxCount) row.Add(new CellItem());

                // 更新所有数据
                for (int j = 0; j < maxCount; j++)
                {
                    var content = item.Length < j + 1 ? 0 : item[j];
                    if (!dataLimitParamChanged && object.Equals(content, row[j].Content)) continue;
                    row[j].Refresh(content, DataLimitParam);
                }
            }
        }

        public class CellItem : ModelBase
        {
            private static readonly Brush zeroValueForeStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCFFFFFF"));
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

            public void Refresh(object? content, DataLimitParamModel dataLimitParam)
            {
                Content = content;
                Background = CellItem.ConvertToBackground(content, dataLimitParam);
                BackOpacity = CellItem.ConvertToBackOpacity(content, dataLimitParam);
                Foreground = CellItem.ConvertToForeground(content, dataLimitParam);
                ForeOpacity = CellItem.ConvertToForeOpacity(content, dataLimitParam);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="value"></param>
            /// <param name="dataLimitParam"></param>
            /// <returns></returns>
            private static Brush? ConvertToBackground(object? value, DataLimitParamModel dataLimitParam)
            {
                if (value == null || dataLimitParam == null) return Brushes.Transparent;
                var isSuccess = double.TryParse(value.ToString(), out double cellValue);
                if (!isSuccess) return defaultValueBackStroke;

                if (cellValue == 0) return defaultValueBackStroke;
                if (cellValue >= dataLimitParam.UpWarningLimit) return highValueBackStroke;
                else if (cellValue <= dataLimitParam.DownWarningLimit) return lowValueBackStroke;
                else return defaultValueBackStroke;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="value"></param>
            /// <param name="dataLimitParam"></param>
            /// <returns></returns>
            private static double ConvertToBackOpacity(object? value, DataLimitParamModel dataLimitParam)
            {
                if (value == null || dataLimitParam == null) return 1;
                var isSuccess = double.TryParse(value.ToString(), out double cellValue);
                if (!isSuccess) return 1;

                if (cellValue == 0) return 1;
                if (cellValue >= dataLimitParam.UpWarningLimit) return 0.1;
                else if (cellValue <= dataLimitParam.DownWarningLimit) return 0.1;
                else return 1;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="value"></param>
            /// <param name="dataLimitParam"></param>
            /// <returns></returns>
            private static Brush? ConvertToForeground(object? value, DataLimitParamModel dataLimitParam)
            {
                if (value == null || dataLimitParam == null) return Brushes.Transparent;

                // 尝试将单元格值转换为double类型
                var isSuccess = double.TryParse(value.ToString(), out double cellValue);
                if (!isSuccess) return defaultValueForeStroke;

                if (cellValue == 0) return zeroValueForeStroke;
                if (cellValue >= dataLimitParam.UpWarningLimit) return highValueForeStroke;
                else if (cellValue <= dataLimitParam.DownWarningLimit) return lowValueForeStroke;
                else return defaultValueForeStroke;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="value"></param>
            /// <param name="dataLimitParam"></param>
            /// <returns></returns>
            private static double ConvertToForeOpacity(object? value, DataLimitParamModel dataLimitParam)
            {
                if (value == null || dataLimitParam == null) return 0.1;
                var isSuccess = double.TryParse(value.ToString(), out double cellValue);
                if (!isSuccess || cellValue == 0) return 0.1;
                else return 1;
            }
        }
    }
}
