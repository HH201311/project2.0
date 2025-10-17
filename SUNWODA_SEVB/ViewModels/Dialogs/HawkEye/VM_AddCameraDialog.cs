using HandyControl.Tools.Extension;
using MySqlX.XDevAPI.Common;
using NetTaste;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Project.HawkEye;
using SUNWODA_SEVB.ViewModels.Pages.HawkEye;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static SUNWODA_SEVB.ViewModels.Dialogs.HawkEye.VM_AddCameraDialog;

namespace SUNWODA_SEVB.ViewModels.Dialogs.HawkEye
{
    public class VM_AddCameraDialog : DialogViewModelBase, IDialogResultable<AddCameraResult?>
    {
        /// <summary>
        /// 设备信息
        /// </summary>
        public DeviceInfo? DeviceInfo
        {
            set
            {
                if (value == null) return;

                // 掉 "0x" 前缀并解析十六进制数处理 HostIP 转换
                var hexValue = Convert.ToUInt32(value.HostIP, 16);
                var deviceIp = string.Join(".", BitConverter.GetBytes(hexValue));
                var c = new CameraModel();
                c.HostIP = deviceIp;
                c.Protocol = "RTSP";
                CameraModel = c;
            }
        }

        private CameraModel? cameraModel;
        /// <summary>
        /// 相机信息
        /// </summary>
        public CameraModel? CameraModel
        {
            get => cameraModel;
            set => SetProperty(ref cameraModel, value);
        }

        private AddCameraResult? _result = new AddCameraResult();
        public AddCameraResult? Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }

        public Action? CloseAction { get; set; }

        public ICommand CloseCommand =>
            new RelayCommand(() =>
            {
                Result = null;
                CloseAction?.Invoke();
            });

        public ICommand SaveCommand =>
            new RelayCommand((obj) =>
            {
                if (obj == null) return;
                if (!(obj is CameraModel cm)) return;
                if (string.IsNullOrEmpty(cm.Name) || string.IsNullOrEmpty(cm.HostIP) || string.IsNullOrEmpty(cm.Workstaion))
                {
                    HandyControl.Controls.MessageBox.Show(
                            $"[名称、相机IP、工位名称]不可为空，请确认！",
                            "信息确认",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    return;
                }
                if (!IPAddress.TryParse(cm.HostIP, out IPAddress? ip))
                {
                    HandyControl.Controls.MessageBox.Show(
                            $"[相机IP]格式不正确，请确认！",
                            "信息确认",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    return;
                }
                cm.HostIP = ip.ToString();
                VM_MachineDamageMonitoringPage.SetCameras(cm);
                HandyControl.Controls.MessageBox.Show(
                        $"[{cm.Name} ip:{cm.HostIP}]已保存！",
                        "信息确认",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                Result = new AddCameraResult();
                Result.Result = MessageBoxResult.OK;
                Result.CameraModel = CameraModel;
                CloseAction?.Invoke();
            });

        public ICommand CancelCommand =>
            new RelayCommand(() =>
            {
                Result = new AddCameraResult();
                Result.Result = MessageBoxResult.Cancel;
                Result.CameraModel = CameraModel;
                CloseAction?.Invoke();
            });

        public class AddCameraResult
        {
            public MessageBoxResult Result { get; set; }
            public CameraModel? CameraModel { get; set; }
        }
    }
}
