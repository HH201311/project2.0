using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels
{
    /// <summary>
    /// 流量PID输出模型类
    /// </summary>
    public class FlowPIDOutputModel
    {
        /// <summary>
        /// 泵速调整描述
        /// </summary>
        public string? BAdjustDescription { get; set; }

        /// <summary>
        /// 泵速偏移值1
        /// </summary>
        public float BOffset1 { get; set; }

        /// <summary>
        /// 泵速偏移值2
        /// </summary>
        public float BOffset2 { get; set; }
    }
}
