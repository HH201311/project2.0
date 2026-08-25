using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer
{
    /// <summary>
    /// T块调机信息
    /// </summary>
    public class TAdjustInfo
    {
        /// <summary>
        /// T块调机量
        /// </summary>
        public int TAdjustValue { get; set; }

        /// <summary>
        /// T块调机时的重量数据趟数
        /// </summary>
        public int TAdjustScanCount { get; set; }

        /// <summary>
        /// T块调机时间
        /// </summary>
        public DateTime TAdjustTime { get; set; }
    }
}
