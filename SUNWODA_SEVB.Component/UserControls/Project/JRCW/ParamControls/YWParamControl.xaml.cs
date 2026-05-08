using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Models.Project.JRCW;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.JRCW.ParamControls
{
    /// <summary>
    /// YWParamControl.xaml 的交互逻辑
    /// </summary>
    public partial class YWParamControl : UserControl
    {
        public YWParamControl()
        {
            InitializeComponent();
        }

        #region ParamModel
        public JRCWParamSettingModel ParamModel
        {
            get { return (JRCWParamSettingModel)GetValue(ParamModelProperty); }
            set { SetValue(ParamModelProperty, value); }
        }

        public static readonly DependencyProperty ParamModelProperty = DependencyProperty.Register(
            "ParamModel",
            typeof(JRCWParamSettingModel),
            typeof(YWParamControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnParamModelChanged))
        );

        private static void OnParamModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as YWParamControl;
            if (uc != null)
            {

            }
        }
        #endregion

        #region UserRole
        public UserRole UserRole
        {
            get { return (UserRole)GetValue(UserRoleProperty); }
            set { SetValue(UserRoleProperty, value); }
        }

        public static readonly DependencyProperty UserRoleProperty = DependencyProperty.Register(
            "UserRole",
            typeof(UserRole),
            typeof(YWParamControl),
            new FrameworkPropertyMetadata(UserRole.Guest, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnUserRoleChanged))
        );

        private static void OnUserRoleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as YWParamControl;
            if (uc != null)
            {

            }
        }
        #endregion
    }
}
