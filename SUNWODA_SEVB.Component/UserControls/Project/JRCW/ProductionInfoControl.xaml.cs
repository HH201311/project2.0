using SUNWODA_SEVB.Core.Models.Project.JRCW.FormModels;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.JRCW
{
    /// <summary>
    /// ProductionInfoControl.xaml 的交互逻辑
    /// </summary>
    public partial class ProductionInfoControl : UserControl
    {
        public ProductionInfoControl()
        {
            InitializeComponent();
        }

        #region ItemSource
        public ProductInfoModel ItemSource
        {
            get { return (ProductInfoModel)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(
            "ItemSource",
            typeof(ProductInfoModel),
            typeof(ProductionInfoControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemSourceChanged))
        );

        private static void OnItemSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ProductionInfoControl;
        }
        #endregion
    }
}
