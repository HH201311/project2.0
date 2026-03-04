using SUNWODA_SEVB.Component.CustomControls;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB
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

        #region Header
        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            "Header",
            typeof(string),
            typeof(ProductionInfoControl),
            new PropertyMetadata("", new PropertyChangedCallback(OnHeaderChanged))
        );

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ProductionInfoControl;
            if (uc != null)
            {
                uc.PPairsGroupBox.Header = uc.Header;
            }
        }
        #endregion

        #region ParameterSource
        public ProductionInfoModel ParameterSource
        {
            get { return (ProductionInfoModel)GetValue(ParameterSourceProperty); }
            set { SetValue(ParameterSourceProperty, value); }
        }

        public static readonly DependencyProperty ParameterSourceProperty = DependencyProperty.Register(
            "ParameterSource",
            typeof(ProductionInfoModel),
            typeof(ProductionInfoControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnParameterSourceChanged))
        );

        private static void OnParameterSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ProductionInfoControl;
            if (uc != null && uc.ParameterSource != null)
            {
                uc.ProductionInfoContentControl.SetBinding(ContentControl.ContentProperty,
                  new Binding("ParameterSource")
                  {
                      Source = uc,
                      Mode = BindingMode.OneWay
                  });
            }
        }
        #endregion
    }
}
