using HandyControl.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace SUNWODA_SEVB.Component.UserControls.Project.OPMMRemote
{
    /// <summary>
    /// VncViewerWithParameterBar.xaml 的交互逻辑
    /// </summary>
    public partial class VncViewerWithParameterBar : UserControl
    {
        private bool isPanelVisible = false;
        private double panelHeight = 100; // 设置栏的高度

        /// <summary>
        /// IP
        /// </summary>
        public string HostIP
        {
            get { return (string)GetValue(HostIPProperty); }
            set { SetValue(HostIPProperty, value); }
        }

        public static readonly DependencyProperty HostIPProperty =
            DependencyProperty.Register("HostIP", typeof(string), typeof(VncViewerWithParameterBar), new PropertyMetadata("127.0.0.1"));

        /// <summary>
        /// 端口号
        /// </summary>
        public int HostPort
        {
            get { return (int)GetValue(HostPortProperty); }
            set { SetValue(HostPortProperty, value); }
        }

        public static readonly DependencyProperty HostPortProperty =
            DependencyProperty.Register("HostPort", typeof(int), typeof(VncViewerWithParameterBar), new PropertyMetadata(5900));

        /// <summary>
        /// 密码
        /// </summary>
        public string HostPassword
        {
            get { return (string)GetValue(HostPasswordProperty); }
            set { SetValue(HostPasswordProperty, value); }
        }

        public static readonly DependencyProperty HostPasswordProperty =
            DependencyProperty.Register("HostPassword", typeof(string), typeof(VncViewerWithParameterBar), new PropertyMetadata(""));


        public VncViewerWithParameterBar()
        {
            InitializeComponent();

            // 监听主区域鼠标离开事件
            MainArea.MouseLeave += MainArea_MouseLeave;

            // 鼠标在窗口内移动
            this.MouseMove += MainWindow_MouseMove;
        }

        private void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            // 鼠标相对于主区域的位置
            Point pos = e.GetPosition(MainArea);

            bool isInsideMainArea = pos.X >= 0 && pos.X <= MainArea.ActualWidth &&
                                    pos.Y >= 0 && pos.Y <= MainArea.ActualHeight;

            if (isInsideMainArea)
            {
                // 鼠标靠近顶部 5px 弹出
                if (pos.Y <= 5 && !isPanelVisible)
                    ShowPanel();
                else if (pos.Y > panelHeight && isPanelVisible)
                    HidePanel();
            }
            else
            {
                // 鼠标离开主区域
                if (isPanelVisible)
                    HidePanel();
            }
        }

        private void MainArea_MouseLeave(object sender, MouseEventArgs e)
        {
            // 鼠标离开主区域立即隐藏
            if (isPanelVisible)
                HidePanel();
        }

        private void ShowPanel()
        {
            isPanelVisible = true;
            DoubleAnimation animation = new DoubleAnimation(2, panelHeight, TimeSpan.FromMilliseconds(200));
            SettingsPanel.BeginAnimation(HeightProperty, animation);
        }

        private void HidePanel()
        {
            isPanelVisible = false;
            DoubleAnimation animation = new DoubleAnimation(panelHeight, 2, TimeSpan.FromMilliseconds(200));
            SettingsPanel.BeginAnimation(HeightProperty, animation);
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            MainArea.Host = HostIP;
            MainArea.Port = HostPort;
            MainArea.Password = HostPassword;
            try
            {
                MainArea.Connect();
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal($"连接错误：{ex}");
                ConnectButton.IsEnabled = true;
                DisconnectButton.IsEnabled = false;
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            MainArea.Disconnect();
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
        }
    }
}
