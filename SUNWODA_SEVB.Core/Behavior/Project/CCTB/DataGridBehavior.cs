using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SUNWODA_SEVB.Core.Behavior.Project.CCTB
{
    public static class DataGridBehavior
    {
        public static bool GetAutoScrollToFirst(DependencyObject obj) =>
            (bool)obj.GetValue(AutoScrollToFirstProperty);
        public static void SetAutoScrollToFirst(DependencyObject obj, bool value) =>
            obj.SetValue(AutoScrollToFirstProperty, value);

        public static readonly DependencyProperty AutoScrollToFirstProperty =
            DependencyProperty.RegisterAttached("AutoScrollToFirst", typeof(bool),
            typeof(DataGridBehavior), new PropertyMetadata(false, OnAutoScrollToFirstChanged));

        private static void OnAutoScrollToFirstChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid && e.NewValue is bool enabled)
            {
                if (enabled)
                    dataGrid.Loaded += DataGrid_Loaded;
                else
                    dataGrid.Loaded -= DataGrid_Loaded;
            }
        }

        private static void DataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                if (dataGrid.ItemsSource is INotifyCollectionChanged incc)
                {
                    // 避免重复订阅
                    incc.CollectionChanged -= OnCollectionChanged;
                    incc.CollectionChanged += OnCollectionChanged;

                    void OnCollectionChanged(object? s, NotifyCollectionChangedEventArgs args)
                    {
                        // 如果是“重置”或“先清再增”导致的多次通知，只响应最终添加完成的情况
                        if (args.Action == NotifyCollectionChangedAction.Add && dataGrid.Items.Count > 0)
                        {
                            dataGrid.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                dataGrid.SelectedIndex = 0;
                                if (dataGrid.Items.Count > 0)
                                    dataGrid.ScrollIntoView(dataGrid.Items[0]);
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                    }
                }
            }
        }
    }
}
