using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Services.Project.CCTB
{
    /// <summary>
    /// 设备状态事件参数
    /// </summary>
    public class DeviceStatusEventArgs : EventArgs
    {
        /// <summary>
        /// 设备状态
        /// </summary>
        public DeviceStatusModel? DeviceStatus { get; set; }
    }
}
