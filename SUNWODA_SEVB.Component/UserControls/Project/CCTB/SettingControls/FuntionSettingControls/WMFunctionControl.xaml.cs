using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.SettingControls.FunctionSettingControls
{
    /// <summary>
    /// WMFunctionControl.xaml 的交互逻辑
    /// </summary>
    public partial class WMFunctionControl : UserControl
    {
        public WMFunctionControl()
        {
            InitializeComponent();
        }

        #region ItemSource
        public BasicFunctionSettingModel ItemSource
        {
            get { return (BasicFunctionSettingModel)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(
            "ItemSource",
            typeof(BasicFunctionSettingModel),
            typeof(WMFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemSourceChanged))
        );

        private static void OnItemSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as WMFunctionControl;
        }
        #endregion

        #region SaveCommand
        public ICommand SaveCommand
        {
            get { return (ICommand)GetValue(SaveCommandProperty); }
            set { SetValue(SaveCommandProperty, value); }
        }

        public static readonly DependencyProperty SaveCommandProperty = DependencyProperty.Register(
            "SaveCommand",
            typeof(ICommand),
            typeof(WMFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnSaveCommandChanged))
        );

        private static void OnSaveCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as WMFunctionControl;
        }
        #endregion

        #region CancleCommand
        public ICommand CancleCommand
        {
            get { return (ICommand)GetValue(CancleCommandProperty); }
            set { SetValue(CancleCommandProperty, value); }
        }

        public static readonly DependencyProperty CancleCommandProperty = DependencyProperty.Register(
            "CancleCommand",
            typeof(ICommand),
            typeof(WMFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnCancleCommandChanged))
        );

        private static void OnCancleCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as WMFunctionControl;
        }
        #endregion
    }
}
