using System.Windows;
using System.Windows.Threading;

namespace SUNWODA_SEVB.Views.Pages.ZZSIX
{
    /// <summary>缺张超时黄色报警窗：展示产品码与缺哪张。</summary>
    public partial class Pack6MissingShotAlarmWindow : Window
    {
        public Pack6MissingShotAlarmWindow()
        {
            InitializeComponent();
        }

        public void ShowAlarm(string productCode, string detail)
        {
            void Apply()
            {
                ProductText.Text = $"产品：{(string.IsNullOrWhiteSpace(productCode) ? "-" : productCode.Trim())}";
                var text = (detail ?? string.Empty).Trim();
                if (text.StartsWith("缺张:", StringComparison.OrdinalIgnoreCase))
                    text = text[3..].TrimStart(':').Trim();
                // 逗号分隔转行，便于阅读
                DetailBox.Text = text
                    .Replace('；', '\n')
                    .Replace(',', '\n')
                    .Replace('，', '\n');
                DetailBox.CaretIndex = 0;
            }

            if (Dispatcher.CheckAccess())
                Apply();
            else
                Dispatcher.Invoke(Apply, DispatcherPriority.Normal);
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e) => Close();
    }
}
