using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels
{
    /// <summary>
    /// 设备状态类
    /// </summary>
    public class DeviceStatusModel : ModelBase
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public DeviceStatusModel() { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="deviceStatus"></param>
        /// <param name="acceptDeviceCount"></param>
        public DeviceStatusModel(string deviceName, EDeviceStatus deviceStatus = EDeviceStatus.未启用, int acceptDeviceCount = 0)
        {
            DeviceName = deviceName;
            DeviceStatus = deviceStatus;
            AcceptDeviceCount = acceptDeviceCount;
        }

        private string deviceName = string.Empty;
        private EDeviceStatus deviceStatus = EDeviceStatus.未启用;
        private int acceptDeviceCount = 0;

        /// <summary>
        /// 设备名称
        /// </summary>
        public string DeviceName { get => deviceName; set => SetProperty(ref deviceName, value); }

        /// <summary>
        /// 设备状态
        /// </summary>
        public EDeviceStatus DeviceStatus { get => deviceStatus; set => SetProperty(ref deviceStatus, value); }

        /// <summary>
        /// 接入的设备数量
        /// </summary>
        public int AcceptDeviceCount { get => acceptDeviceCount; set => SetProperty(ref acceptDeviceCount, value); }
    }
}
