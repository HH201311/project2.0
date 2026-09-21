using System.Windows;
using System.Windows.Threading;

namespace SUNWODA_SEVB.Views.Pages.ZZSIX
{
    /// <summary>非模态 NG 警示窗：窗未关则由页面追加产品码；点「确定」关闭即可。</summary>
    public partial class Pack6NgAlarmWindow : Window
    {
        public Pack6NgAlarmWindow()
        {
            InitializeComponent();
        }

        public void UpdateNgList(IReadOnlyList<string> productCodes)
        {
            void Apply()
            {
                NgListBox.Text = productCodes.Count == 0
                    ? string.Empty
                    : string.Join(Environment.NewLine, productCodes);
                NgListBox.CaretIndex = NgListBox.Text.Length;
                NgListBox.ScrollToEnd();
            }

            if (Dispatcher.CheckAccess())
                Apply();
            else
                Dispatcher.Invoke(Apply, DispatcherPriority.Normal);
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e) => Close();
    }
}
