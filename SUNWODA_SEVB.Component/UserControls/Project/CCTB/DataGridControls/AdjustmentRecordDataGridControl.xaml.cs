using ScottPlot.MultiplotLayouts;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RecordModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
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
    /// AdjustmentRecordDataGridControl.xaml 的交互逻辑
    /// </summary>
    public partial class AdjustmentRecordDataGridControl : UserControl
    {
        public AdjustmentRecordDataGridControl()
        {
            InitializeComponent();

            DG.ItemsSource = Items;
            InitializeDefaultColumns();
        }

        #region ItemsSource
        public ObservableCollection<AdjustmentRecordModel> ItemsSource
        {
            get { return (ObservableCollection<AdjustmentRecordModel>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<AdjustmentRecordModel>),
            typeof(AdjustmentRecordDataGridControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemsSourceChanged))
        );

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as AdjustmentRecordDataGridControl;
            if (uc == null) return;

            if (e.OldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= uc.OnCollectionChanged;
            }
            if (e.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += uc.OnCollectionChanged;
            }

            // 完全重新加载数据
            uc.ReloadAllData();
        }

        /// <summary>
        /// 集合变化处理
        /// </summary>
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    HandleAddItems(e.NewItems, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    HandleRemoveItems(e.OldItems, e.OldStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    HandleReplaceItems(e.NewItems, e.OldItems, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    HandleMoveItems(e.NewItems, e.NewStartingIndex, e.OldStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    ReloadAllData();
                    break;
            }
        }

        #endregion
        /// <summary>
        /// 初始化表格
        /// </summary>
        private void InitializeDefaultColumns()
        {
            var columnNames = new HashSet<string>();
            columnNames.Add("趟号");
            for (int i = 0; i < 25; i++) columnNames.Add($"P{i + 1}");
            for (int i = 0; i < 2; i++) columnNames.Add($"泵速{i + 1}");
            columnNames.Add("刀距-左");
            columnNames.Add("刀距-右");
            columnNames.Add("时间");
            columnNames.Add("调节面");
            UpdateColumns(columnNames);

            DG.UpdateLayout();
        }

        #region 核心方法
        private HashSet<string> _currentColumns = new HashSet<string>();
        private readonly object _lockObject = new object();

        public ObservableCollection<Dictionary<string, object>> Items { get; private set; } = new();

        /// <summary>
        /// 完全重新加载所有数据
        /// </summary>
        private void ReloadAllData()
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
            {
                lock (_lockObject)
                {
                    Items.Clear();
                }
                return;
            }

            lock (_lockObject)
            {
                // 转换所有数据
                var newItems = ItemsSource.Select(record => ConvertToDictionary(record)).ToList();

                // 收集所有键
                var allKeys = new HashSet<string>();
                foreach (var item in newItems)
                {
                    foreach (var key in item.Keys)
                    {
                        allKeys.Add(key);
                    }
                }

                // 更新列（只在需要时）
                if (!_currentColumns.SetEquals(allKeys))
                {
                    UpdateColumns(allKeys);
                }

                // 更新数据
                Items.Clear();
                foreach (var item in newItems)
                {
                    Items.Add(item);
                }
            }
        }

        /// <summary>
        /// 更新列定义
        /// </summary>
        private void UpdateColumns(HashSet<string> newKeys)
        {
            _currentColumns.Clear();
            DG.Columns.Clear();

            // 按照预定义顺序添加列
            var orderedKeys = GetOrderedColumnKeys(newKeys);

            foreach (var columnName in orderedKeys)
            {
                var column = CreateDataGridTextColumn(columnName);
                _currentColumns.Add(columnName);
                DG.Columns.Add(column);
            }
        }

        /// <summary>
        /// 获取有序的列键
        /// </summary>
        private List<string> GetOrderedColumnKeys(HashSet<string> keys)
        {
            var orderedKeys = new List<string>();

            // 1. 趟号
            var passCountKey = keys.FirstOrDefault(k => k.StartsWith("趟号"));
            if (passCountKey != null) orderedKeys.Add(passCountKey);

            // 2. P0-Pn (按数字排序)
            var pKeys = keys.Where(k => k.StartsWith("P") && k.Length > 1 && char.IsDigit(k[1]))
                           .OrderBy(k => int.Parse(k.Substring(1)))
                           .ToList();
            orderedKeys.AddRange(pKeys);

            // 3. 泵速1-泵速n (按数字排序)
            var pumpKeys = keys.Where(k => k.StartsWith("泵速"))
                              .OrderBy(k => int.Parse(k.Replace("泵速", "")))
                              .ToList();
            orderedKeys.AddRange(pumpKeys);

            // 4. 刀距-左, 刀距-右
            if (keys.Contains("刀距-左")) orderedKeys.Add("刀距-左");
            if (keys.Contains("刀距-右")) orderedKeys.Add("刀距-右");

            // 5. 时间
            if (keys.Contains("时间")) orderedKeys.Add("时间");

            // 6. 调节面
            if (keys.Contains("调节面")) orderedKeys.Add("调节面");

            // 7. 其他未归类的键
            var remainingKeys = keys.Except(orderedKeys).ToList();
            orderedKeys.AddRange(remainingKeys);

            return orderedKeys;
        }

        /// <summary>
        /// 创建列
        /// </summary>
        private DataGridTextColumn CreateDataGridTextColumn(string columnName)
        {
            var style = FindResource("RecordTextBlockStyle") as Style;
            var column = new DataGridTextColumn
            {
                Header = columnName,
                Binding = new Binding($"[{columnName}]")
                {
                    Mode = BindingMode.OneWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                CanUserSort = false
            };

            if (style != null)
            {
                column.ElementStyle = style;
                column.EditingElementStyle = style;
            }

            // 设置列宽
            SetColumnWidth(column, columnName);

            return column;
        }

        /// <summary>
        /// 设置列宽和样式
        /// </summary>
        private void SetColumnWidth(DataGridTextColumn column, string columnName)
        {
            if (columnName.StartsWith("趟号"))
            {
                column.MinWidth = 80;
                column.Width = new DataGridLength(80);
            }
            else if (columnName.StartsWith("P"))
            {
                column.MinWidth = 80;
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            }
            else if (columnName.StartsWith("泵速"))
            {
                column.MinWidth = 85;
                column.Width = new DataGridLength(85);
            }
            else if (columnName.StartsWith("刀距"))
            {
                column.MinWidth = 100;
                column.Width = new DataGridLength(100);
                column.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3DCC84"));
            }
            else if (columnName.StartsWith("时间"))
            {
                column.MinWidth = 180;
                column.Width = new DataGridLength(180);
            }
            else if (columnName.StartsWith("调节面"))
            {
                column.MinWidth = 90;
                column.Width = new DataGridLength(90);
            }
            else
            {
                column.MinWidth = 60;
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
        }

        /// <summary>
        /// 处理新增项
        /// </summary>
        private void HandleAddItems(System.Collections.IList? newItems, int startingIndex)
        {
            if (newItems == null || newItems.Count == 0) return;

            var newDictItems = new List<Dictionary<string, object>>();
            var hasNewColumns = false;
            foreach (var item in newItems)
            {
                if (!(item is AdjustmentRecordModel record)) continue;
                var dict = ConvertToDictionary(record);
                newDictItems.Add(dict);

                // 检查是否有新列
                hasNewColumns = dict.Keys.Except(_currentColumns).Count() > 0;
            }

            lock (_lockObject)
            {
                if (hasNewColumns)
                {
                    // 有新列，需要重建整个表格
                    ReloadAllData();
                }
                else
                {
                    // 没有新列，按索引位置插入
                    int insertIndex = startingIndex >= 0 && startingIndex <= Items.Count
                        ? startingIndex
                        : Items.Count;

                    for (int i = 0; i < newDictItems.Count; i++)
                    {
                        Items.Insert(insertIndex + i, newDictItems[i]);
                    }
                }
            }
        }

        /// <summary>
        /// 处理移除项
        /// </summary>
        private void HandleRemoveItems(System.Collections.IList? oldItems, int startingIndex)
        {
            if (oldItems == null || oldItems.Count == 0) return;

            lock (_lockObject)
            {
                // 从指定索引开始移除
                if (startingIndex >= 0 && startingIndex < Items.Count)
                {
                    int removeCount = Math.Min(oldItems.Count, Items.Count - startingIndex);
                    for (int i = 0; i < removeCount; i++)
                    {
                        Items.RemoveAt(startingIndex);
                    }
                }
                else
                {
                    // 索引无效，通过匹配移除
                    var recordsToRemove = oldItems.Cast<AdjustmentRecordModel>().ToList();
                    var itemsToRemove = Items.Where(item =>
                        recordsToRemove.Any(record =>
                            item.ContainsKey("时间") &&
                            item["时间"]?.ToString() == record.RecordTime?.ToString("yyyy-MM-dd HH:mm:ss fff") &&
                            item.ContainsKey("趟号") &&
                            item["趟号"]?.ToString() == record.PassCount.ToString()
                        )
                    ).ToList();

                    foreach (var item in itemsToRemove)
                    {
                        Items.Remove(item);
                    }
                }
            }
        }

        /// <summary>
        /// 处理替换项
        /// </summary>
        private void HandleReplaceItems(System.Collections.IList? newItems, System.Collections.IList? oldItems, int startingIndex)
        {
            if (newItems == null || oldItems == null) return;

            lock (_lockObject)
            {
                // 先移除旧项
                HandleRemoveItems(oldItems, startingIndex);
                // 再添加新项
                HandleAddItems(newItems, startingIndex);
            }
        }

        /// <summary>
        /// 处理移动项
        /// </summary>
        private void HandleMoveItems(System.Collections.IList? movedItems, int newStartingIndex, int oldStartingIndex)
        {
            if (movedItems == null || movedItems.Count == 0) return;

            lock (_lockObject)
            {
                if (oldStartingIndex >= 0 && oldStartingIndex < Items.Count &&
                    newStartingIndex >= 0 && newStartingIndex <= Items.Count)
                {
                    Items.Move(oldStartingIndex, newStartingIndex);
                }
                else
                {
                    // 索引无效，重建数据
                    ReloadAllData();
                }
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 转换为字典
        /// </summary>
        private Dictionary<string, object> ConvertToDictionary(AdjustmentRecordModel record)
        {
            var dic = new Dictionary<string, object>();

            // 趟号
            dic["趟号"] = record.PassCount;

            // TAdjustOffsets -> P0, P1, P2...
            if (record.TAdjustOffsets != null)
            {
                for (int i = 0; i < record.TAdjustOffsets.Count; i++)
                {
                    var columnName = $"P{i}";
                    dic[columnName] = FormatValue(record.TAdjustOffsets[i]);
                }
            }

            // BAdjustOffsets -> 泵速1, 泵速2...
            if (record.BAdjustOffsets != null)
            {
                for (int i = 0; i < record.BAdjustOffsets.Count; i++)
                {
                    var columnName = $"泵速{i + 1}";
                    dic[columnName] = FormatValue(record.BAdjustOffsets[i]);
                }
            }

            // 刀距
            dic["刀距-左"] = FormatValue(record.KnifeGapLeftOffset);
            dic["刀距-右"] = FormatValue(record.KnifeGapRightOffset);

            // 时间
            dic["时间"] = record.RecordTime?.ToString("yyyy-MM-dd HH:mm:ss fff") ?? "";

            // 调节面
            dic["调节面"] = record.SideType.ToString();

            return dic;
        }

        /// <summary>
        /// 格式化偏移量
        /// </summary>
        private string FormatValue(float value)
        {
            return (value > 0 ? "+" : string.Empty) + value.ToString("F2");
        }

        #endregion
    }
}