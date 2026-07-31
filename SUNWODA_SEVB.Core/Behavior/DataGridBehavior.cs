using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SUNWODA_SEVB.Core.Behavior
{
    public static class DataGridBehavior
    {
        [Flags]
        private enum AutoScrollMode { None = 0, ToFirst = 1, ToLast = 2 }

        /// <summary>
        /// 保存每个 DataGrid 当前启用的自动滚动模式组合
        /// </summary>
        private static readonly Dictionary<DataGrid, AutoScrollMode> _modes = new();

        /// <summary>
        /// 保存每个 DataGrid 当前的事件处理器（用于取消订阅）
        /// </summary>
        private static readonly Dictionary<DataGrid, ItemsChangedEventHandler> _handlers = new();

        // ========== AutoScrollToFirst ==========
        public static bool GetAutoScrollToFirst(DependencyObject obj) =>
            (bool)obj.GetValue(AutoScrollToFirstProperty);
        public static void SetAutoScrollToFirst(DependencyObject obj, bool value) =>
            obj.SetValue(AutoScrollToFirstProperty, value);

        public static readonly DependencyProperty AutoScrollToFirstProperty =
            DependencyProperty.RegisterAttached("AutoScrollToFirst", typeof(bool),
            typeof(DataGridBehavior), new PropertyMetadata(false, OnAutoScrollToFirstChanged));

        private static void OnAutoScrollToFirstChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid && e.NewValue is bool value)
                UpdateMode(dataGrid, AutoScrollMode.ToFirst, value);
        }

        // ========== AutoScrollToLast ==========
        public static bool GetAutoScrollToLast(DependencyObject obj) =>
            (bool)obj.GetValue(AutoScrollToLastProperty);
        public static void SetAutoScrollToLast(DependencyObject obj, bool value) =>
            obj.SetValue(AutoScrollToLastProperty, value);

        public static readonly DependencyProperty AutoScrollToLastProperty =
            DependencyProperty.RegisterAttached("AutoScrollToLast", typeof(bool),
            typeof(DataGridBehavior), new PropertyMetadata(false, OnAutoScrollToLastChanged));

        private static void OnAutoScrollToLastChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid && e.NewValue is bool value)
                UpdateMode(dataGrid, AutoScrollMode.ToLast, value);
        }

        // ========== 核心管理方法 ==========
        private static void UpdateMode(DataGrid dataGrid, AutoScrollMode flag, bool enable)
        {
            // 获取当前模式
            var current = _modes.TryGetValue(dataGrid, out var mode) ? mode : AutoScrollMode.None;
            var newMode = enable ? (current | flag) : (current & ~flag);

            if (newMode == AutoScrollMode.None)
            {
                // 不再需要任何行为，取消订阅并清理
                Unsubscribe(dataGrid);
                _modes.Remove(dataGrid);
            }
            else
            {
                _modes[dataGrid] = newMode;

                // 确保 Loaded 事件只绑定一次
                if (current == AutoScrollMode.None)
                {
                    dataGrid.Loaded += DataGrid_Loaded;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void DataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                // 避免重复订阅
                Unsubscribe(dataGrid);

                var generator = dataGrid.ItemContainerGenerator;
                if (generator == null) return;

                ItemsChangedEventHandler? handler = null;
                handler = (s, args) =>
                {
                    if (args.Action != NotifyCollectionChangedAction.Add || dataGrid.Items.Count == 0)
                        return;

                    // 根据当前启用的模式决定执行哪些滚动
                    if (_modes.TryGetValue(dataGrid, out var mode))
                    {
                        if (mode.HasFlag(AutoScrollMode.ToFirst))
                            ScrollToFirst(dataGrid);
                        if (mode.HasFlag(AutoScrollMode.ToLast))
                            ScrollToLast(dataGrid);
                    }
                };

                _handlers[dataGrid] = handler;
                generator.ItemsChanged += handler;
            }
        }

        /// <summary>
        /// 取消订阅 DataGrid 的 ItemContainerGenerator.ItemsChanged 事件，并从字典中移除相关记录
        /// </summary>
        /// <param name="dataGrid"></param>
        private static void Unsubscribe(DataGrid dataGrid)
        {
            if (_handlers.TryGetValue(dataGrid, out var handler))
            {
                dataGrid.ItemContainerGenerator.ItemsChanged -= handler;
                _handlers.Remove(dataGrid);
            }
        }

        /// <summary>
        /// 滚动到第一个项
        /// </summary>
        /// <param name="dataGrid"></param>
        private static void ScrollToFirst(DataGrid dataGrid)
        {
            dataGrid.Dispatcher.BeginInvoke(new Action(() =>
            {
                dataGrid.SelectedIndex = 0;
                if (dataGrid.Items.Count > 0)
                    dataGrid.ScrollIntoView(dataGrid.Items[0]);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 滚动到最后一项
        /// </summary>
        /// <param name="dataGrid"></param>
        private static void ScrollToLast(DataGrid dataGrid)
        {
            dataGrid.Dispatcher.BeginInvoke(new Action(() =>
            {
                dataGrid.SelectedIndex = dataGrid.Items.Count - 1;
                if (dataGrid.Items.Count > 0)
                    dataGrid.ScrollIntoView(dataGrid.Items[dataGrid.Items.Count - 1]);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}
