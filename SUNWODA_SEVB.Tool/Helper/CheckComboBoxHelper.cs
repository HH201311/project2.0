using System.Collections;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Controls;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class CheckComboBoxHelper : DependencyObject
    {
        public static IList GetSelectedItems(DependencyObject obj)
        {
            return (IList)obj.GetValue(SelectedItemsProperty);
        }

        public static void SetSelectedItems(DependencyObject obj, int value)
        {
            obj.SetValue(SelectedItemsProperty, value);
        }

        public string SelectedItems
        {
            get { return (string)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems",
                typeof(IEnumerable),
                typeof(CheckComboBoxHelper),
                new PropertyMetadata(null, OnSelectedItemsChanged)
            );

        private static void OnSelectedItemsChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        )
        {
            if (d is CheckComboBox cbox)
            {
                if (e.OldValue != null)
                {
                    cbox.SelectionChanged -= OnSelectionChanged;
                }
                if (e.NewValue is IList list)
                {
                    cbox.SelectedItems.Clear();
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            cbox.SelectedItems.Add(item);
                        }
                        cbox.OnApplyTemplate();
                        cbox.SelectionChanged += OnSelectionChanged;
                    }
                }
            }
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dataSource = GetSelectedItems((sender as DependencyObject)!);
            // 添加用户选中的项
            foreach (var item in e.AddedItems)
            {
                if (dataSource.IndexOf(item) == -1)
                    dataSource.Add(item);
            }
            // 删除用户取消选中的项
            foreach (var item in e.RemovedItems)
            {
                if (dataSource.IndexOf(item) >= 0)
                    dataSource.Remove(item);
            }
        }
    }
}
