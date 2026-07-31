using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels
{
    /// <summary>
    /// MULTIMDTD算法输出模型类
    /// </summary>
    public class MULTIMDTDControlOutputModel
    {
        /// <summary>
        /// 泵速调整模式
        /// </summary>
        public int BAdjustMode { get; set; }

        /// <summary>
        /// 泵速调整描述
        /// </summary>
        public Dictionary<string, string>? BAdjustDescription { get; set; }

        /// <summary>
        /// 泵速调整偏移值
        /// </summary>
        public Dictionary<string, float>? BAdjustOffset { get; set; }

        /// <summary>
        /// T块调整模式
        /// </summary>
        public int TAdjustMode { get; set; }

        /// <summary>
        /// T块调整描述
        /// </summary>
        public Dictionary<string, string>? TAdjustDescription { get; set; }

        /// <summary>
        /// T块调整偏移值
        /// </summary>
        public Dictionary<string, int>? TAdjustOffset { get; set; }
    }
}
