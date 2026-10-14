using System.Windows;
using SUNWODA_SEVB.ViewModels.Pages.YWSIX;

namespace SUNWODA_SEVB.Views.Pages.YWSIX
{
    public partial class Pack6PinSettingsWindow : Window
    {
        public Pack6PinSettingsWindow()
        {
            InitializeComponent();
        }

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is VM_YWSIXPage vm)
            {
                if (!vm.CanEditPinSettings)
                {
                    MessageBox.Show(
                        "当前账号无权限修改参数，请在侧边栏以管理员登录。",
                        "参数设置",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await vm.SavePinSettingsAsync();
            }

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
