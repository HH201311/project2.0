using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace SUNWODA_SEVB.Views.Pages.ZZSIX
{
    /// <summary>编辑某一编码段下拉选项（增删）。</summary>
    public partial class Pack6OptionListEditorWindow : Window
    {
        private readonly ObservableCollection<string> _items;

        public IReadOnlyList<string> ResultItems => _items.ToList();

        public Pack6OptionListEditorWindow(string title, IEnumerable<string> initial)
        {
            InitializeComponent();
            Title = $"编辑选项 — {title}";
            _items = new ObservableCollection<string>(
                initial.Select(s => s.Trim().ToUpperInvariant()).Where(s => s.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase));
            ItemList.ItemsSource = _items;
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            var text = (NewItemBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (text.Length == 0)
                return;
            if (_items.Any(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, $"已存在：{text}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _items.Add(text);
            NewItemBox.Clear();
            NewItemBox.Focus();
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (ItemList.SelectedItem is string sel)
                _items.Remove(sel);
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
