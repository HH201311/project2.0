using System.Windows;
using SUNWODA_SEVB.ViewModels.Pages.ZZSIX;

namespace SUNWODA_SEVB.Views.Pages.ZZSIX
{
    public partial class Pack6SettingsWindow : Window
    {
        public Pack6SettingsWindow()
        {
            InitializeComponent();
        }

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is VM_ZZSIXPage vm)
                await vm.SaveConfigFromSettingsAsync();
            DialogResult = true;
            Close();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
