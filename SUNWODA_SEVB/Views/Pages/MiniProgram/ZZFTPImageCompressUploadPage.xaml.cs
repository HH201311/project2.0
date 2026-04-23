using System;
using System.Collections.Generic;
using System.Linq;
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
using iText.Kernel.XMP.Impl;
using SUNWODA_SEVB.ViewModels.Pages.MiniProgram;
using static SUNWODA_SEVB.ViewModels.Pages.MiniProgram.VM_ZZFTPImageCompressUploadPage;

namespace SUNWODA_SEVB.Views.Pages.MiniProgram
{
    /// <summary>
    /// ZZFTPImageCompressUploadPage.xaml 的交互逻辑
    /// </summary>
    public partial class ZZFTPImageCompressUploadPage : Page
    {
        public ZZFTPImageCompressUploadPage()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is VM_ZZFTPImageCompressUploadPage vm && e.NewValue is FtpNode node)
            {
                vm.OnNodeSelected(node);
            }
        }

        private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item)
            {
                if (item.DataContext is FtpNode node)
                {
                    await node.LoadChildrenAsync();
                }
            }
        }
    }
}
