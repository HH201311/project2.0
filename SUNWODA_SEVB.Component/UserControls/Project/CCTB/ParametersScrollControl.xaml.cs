using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB
{
    /// <summary>
    /// ParametersScrollControl.xaml 的交互逻辑
    /// </summary>
    public partial class ParametersScrollControl : UserControl
    {
        public ParametersScrollControl()
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
            typeof(ParametersScrollControl),
            new PropertyMetadata("", new PropertyChangedCallback(OnHeaderChanged))
        );
        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ParametersScrollControl;
            if (uc != null)
            {
                uc.PPairsGroupBox.Header = uc.Header;
            }
        }
        #endregion

        #region ParameterSource
        public IEnumerable<CCTBParametersModel<double>> ParameterSource
        {
            get { return (IEnumerable<CCTBParametersModel<double>>)GetValue(ParameterSourceProperty); }
            set { SetValue(ParameterSourceProperty, value); }
        }

        public static readonly DependencyProperty ParameterSourceProperty = DependencyProperty.Register(
            "ParameterSource",
            typeof(IEnumerable<CCTBParametersModel<double>>),
            typeof(ParametersScrollControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnParameterSourceChanged))
        );

        private static void OnParameterSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ParametersScrollControl;
            if (uc != null && uc.ParameterSource != null)
            {
                uc.PPairsItemsControl.ItemsSource = uc.ParameterSource;
            }
        }
        #endregion
    }
}
