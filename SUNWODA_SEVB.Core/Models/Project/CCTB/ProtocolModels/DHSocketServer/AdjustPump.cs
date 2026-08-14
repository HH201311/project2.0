using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer
{
    /// <summary>
    /// 调机数据
    /// </summary>
    public class AdjustPump
    {
        /// <summary>
        /// 泵标识
        /// </summary>
        public string? PumpTag { get; set; }

        /// <summary>
        /// 泵速偏移量
        /// </summary>
        public float AdjustValue { get; set; }
    }
}
