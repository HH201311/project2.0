using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer
{
    /// <summary>
    /// MD调机洗发信息
    /// </summary>
    public class MAdjustRequestModel
    {  /// <summary>
       /// 泵速调机信息
       /// </summary>
        public List<AdjustPump>? AdjustPumpLis { get; set; }

        /// <summary>
        /// 调机时间
        /// </summary>
        public DateTime AdjustTime { get; set; }
    }
}
