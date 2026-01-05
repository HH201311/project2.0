using SUNWODA_SEVB.ViewModels.Pages.PTO;
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

namespace SUNWODA_SEVB.Views.Pages.PTO
{
    /// <summary>
    /// PTODamageDetectionPage.xaml 的交互逻辑
    /// </summary>
    public partial class PTODamageDetectionPage : Page
    {
        private VM_PTODamageDetectionPage? _viewModel;
        public PTODamageDetectionPage()
        {

            InitializeComponent();
            this.Loaded += (s, e) =>
            {
                _viewModel = this.Resources["DamageDetectionVM"] as VM_PTODamageDetectionPage;
            };
        }
        /// <summary>
        /// InkCanvas加载完成后，将引用传给ViewModel
        /// </summary>
        private void NegativeRoiInkCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is InkCanvas inkCanvas && _viewModel != null)
            {
                _viewModel.SetInkCanvas(inkCanvas); 
            }
        }


    }
}
