using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketServer
{
    /// <summary>
    /// 心跳数据
    /// </summary>
    public class CCTBHEARTQUEST
    {
        /// <summary>
        /// 设备编号
        /// </summary>
        public string? DEVICESN { get; set; }
        /// <summary>
        /// 心跳信息(默认写值1)
        /// </summary>
        public string? HEARTMESSAGE { get; set; }
    }
}
