using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Component.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.SettingControls.CommunicationSettingControls
{
    /// <summary>
    /// DHCommunicationControl.xaml 的交互逻辑
    /// </summary>
    public partial class DHCommunicationControl : UserControl
    {
        public DHCommunicationControl()
        {
            InitializeComponent();
        }

        #region SideType
        public ESideType SideType
        {
            get { return (ESideType)GetValue(SideTypeProperty); }
            set { SetValue(SideTypeProperty, value); }
        }

        public static readonly DependencyProperty SideTypeProperty = DependencyProperty.Register(
            "SideType",
            typeof(ESideType),
            typeof(DHCommunicationControl),
            new PropertyMetadata(ESideType.A, new PropertyChangedCallback(OnSideTypeChanged))
        );

        private static void OnSideTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as DHCommunicationControl;
        }
        #endregion

        #region ItemSource
        public CommunicationSettingModel ItemSource
        {
            get { return (CommunicationSettingModel)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(
            "ItemSource",
            typeof(CommunicationSettingModel),
            typeof(DHCommunicationControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemSourceChanged))
        );

        private static void OnItemSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as DHCommunicationControl;
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
            typeof(DHCommunicationControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnSaveCommandChanged))
        );

        private static void OnSaveCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as DHCommunicationControl;
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
            typeof(DHCommunicationControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnCancleCommandChanged))
        );

        private static void OnCancleCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as DHCommunicationControl;
        }
        #endregion
    }
}
