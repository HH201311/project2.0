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

        private static readonly ECommunicationMode[] CommunicationModes = new[] { ECommunicationMode.服务端Socket, ECommunicationMode.客户端Mqtt };

        /// <summary>
        /// 通讯模式数据源
        /// </summary>
        public ObservableCollection<ComboboxItemModel<ECommunicationMode>> CommunicationModeSource { get; } =
               new ObservableCollection<ComboboxItemModel<ECommunicationMode>>(CommunicationModes.Select(v =>
               new ComboboxItemModel<ECommunicationMode> { ItemValue = v, DisplayName = v.ToString() }));

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

        #region SocketClientConfigCommand
        public ICommand SocketClientConfigCommand
        {
            get { return (ICommand)GetValue(SocketClientConfigCommandProperty); }
            set { SetValue(SocketClientConfigCommandProperty, value); }
        }

        public static readonly DependencyProperty SocketClientConfigCommandProperty = DependencyProperty.Register(
            "SocketClientConfigCommand",
            typeof(ICommand),
            typeof(DHCommunicationControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnSocketClientConfigCommandChanged))
        );

        private static void OnSocketClientConfigCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as DHCommunicationControl;
        }
        #endregion

        #region SocketServerConfigCommand
        public ICommand SocketServerConfigCommand
        {
            get { return (ICommand)GetValue(SocketServerConfigCommandProperty); }
            set { SetValue(SocketServerConfigCommandProperty, value); }
        }

        public static readonly DependencyProperty SocketServerConfigCommandProperty = DependencyProperty.Register(
            "SocketServerConfigCommand",
            typeof(ICommand),
            typeof(DHCommunicationControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnSocketServerConfigCommandChanged))
        );

        private static void OnSocketServerConfigCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as DHCommunicationControl;
        }
        #endregion

        #region MqttClientConfigCommand
        public ICommand MqttClientConfigCommand
        {
            get { return (ICommand)GetValue(MqttClientConfigCommandProperty); }
            set { SetValue(MqttClientConfigCommandProperty, value); }
        }

        public static readonly DependencyProperty MqttClientConfigCommandProperty = DependencyProperty.Register(
            "MqttClientConfigCommand",
            typeof(ICommand),
            typeof(DHCommunicationControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnMqttClientConfigCommandChanged))
        );

        private static void OnMqttClientConfigCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
