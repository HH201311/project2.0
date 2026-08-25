using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer
{
    /// <summary>
    /// 调机下发相应信息
    /// </summary>
    public class AdjustReponseModel
    {
        /// <summary>
        /// 下发结果
        /// 备注：A：A面模头 B：B面模头
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// 下发结果
        /// 备注：0：下发成功；1：下发失败
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// 响应信息
        /// </summary>
        public string? Message { get; set; }
    }
}
