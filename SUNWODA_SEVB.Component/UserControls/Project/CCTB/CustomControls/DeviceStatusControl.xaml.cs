using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.CustomControls
{
    /// <summary>
    /// DeviceStatusControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceStatusControl : UserControl
    {
        public DeviceStatusControl()
        {
            InitializeComponent();
        }

        #region 依赖属性
        /// <summary>
        /// 设备名称
        /// </summary>
        public static readonly DependencyProperty DeviceNameProperty =
            DependencyProperty.Register("DeviceName", typeof(string), typeof(DeviceStatusControl),
                new FrameworkPropertyMetadata("设备名", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDeviceNameChanged));

        /// <summary>
        /// 设备状态
        /// </summary>
        public static readonly DependencyProperty DeviceStatusProperty =
            DependencyProperty.Register("DeviceStatus", typeof(EDeviceStatus), typeof(DeviceStatusControl),
                new FrameworkPropertyMetadata(EDeviceStatus.未启用, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDeviceStatusChanged));

        /// <summary>
        /// 接入设备个数
        /// </summary>
        public static readonly DependencyProperty AcceptDeviceCountProperty =
            DependencyProperty.Register("AcceptDeviceCount", typeof(int), typeof(DeviceStatusControl),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnAcceptDeviceCountChanged));
        #endregion

        #region 属性
        /// <summary>
        /// 设备名称
        /// </summary>
        public string DeviceName { get => (string)GetValue(DeviceNameProperty); set => SetValue(DeviceNameProperty, value); }

        /// <summary>
        /// 设备状态
        /// </summary>
        public EDeviceStatus DeviceStatus { get => (EDeviceStatus)GetValue(DeviceStatusProperty); set => SetValue(DeviceStatusProperty, value); }

        /// <summary>
        /// 接入设备个数
        /// </summary>
        public int AcceptDeviceCount { get => (int)GetValue(AcceptDeviceCountProperty); set => SetValue(AcceptDeviceCountProperty, value); }
        #endregion

        #region 属性变更回调
        /// <summary>
        /// 当设备名称发生变化时触发
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnDeviceNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        /// <summary>
        /// 当设备状态发生变化时触发
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnDeviceStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        /// <summary>
        /// 当接入设备个数发生变化时触发
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnAcceptDeviceCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is DeviceStatusControl uc)) return;
            if (!(e.NewValue is int value)) return;
            uc.AcceptDeviceCountTextBlock.Text = value < 0 ? "0" : (value > 99 ? "99+" : value.ToString());
        }
        #endregion
    }
}
