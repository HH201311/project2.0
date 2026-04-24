using SUNWODA_SEVB.Component.CustomControls;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
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
    /// LineInfoControl.xaml 的交互逻辑
    /// </summary>
    public partial class LineInfoControl : UserControl
    {
        public LineInfoControl()
        {
            InitializeComponent();
        }

        #region ParameterSource
        public LineInfoModel ParameterSource
        {
            get { return (LineInfoModel)GetValue(ParameterSourceProperty); }
            set { SetValue(ParameterSourceProperty, value); }
        }

        public static readonly DependencyProperty ParameterSourceProperty = DependencyProperty.Register(
            "ParameterSource",
            typeof(LineInfoModel),
            typeof(LineInfoControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnParameterSourceChanged))
        );

        private static void OnParameterSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as LineInfoControl;
        }
        #endregion
    }
}
