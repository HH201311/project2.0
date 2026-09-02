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
    /// CMFunctionControl.xaml 的交互逻辑
    /// </summary>
    public partial class CMFunctionControl : UserControl
    {
        public CMFunctionControl()
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
            typeof(CMFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemSourceChanged))
        );

        private static void OnItemSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CMFunctionControl;
        }
        #endregion

        #region PLCGatherConfigCommand
        public ICommand PLCGatherConfigCommand
        {
            get { return (ICommand)GetValue(PLCGatherConfigCommandProperty); }
            set { SetValue(PLCGatherConfigCommandProperty, value); }
        }

        public static readonly DependencyProperty PLCGatherConfigCommandProperty = DependencyProperty.Register(
            "PLCGatherConfigCommand",
            typeof(ICommand),
            typeof(CMFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnPLCGatherConfigCommandChanged))
        );

        private static void OnPLCGatherConfigCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CMFunctionControl;
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
            typeof(CMFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnSaveCommandChanged))
        );

        private static void OnSaveCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CMFunctionControl;
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
            typeof(CMFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnCancleCommandChanged))
        );

        private static void OnCancleCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CMFunctionControl;
        }
        #endregion
    }
}
