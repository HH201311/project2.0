using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer
{
    /// <summary>
    /// T块调机下发信息
    /// </summary>
    public class TAdjustRequestModel
    {
        /// <summary>
        /// T块调机信息
        /// </summary>
        public List<AdjustTask>? AdjustTaskLis { get; set; }

        /// <summary>
        /// 调机时间
        /// </summary>
        public DateTime AdjustTime { get; set; }
    }
}
