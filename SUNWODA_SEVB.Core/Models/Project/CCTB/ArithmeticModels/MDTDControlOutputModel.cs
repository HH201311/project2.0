using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels
{
    /// <summary>
    /// MDTD算法输出模型类
    /// </summary>
    public class MDTDControlOutputModel
    {
        /// <summary>
        /// 调整模式
        /// </summary>
        public int AdjustMode { get; set; }

        /// <summary>
        /// 调机描述
        /// </summary>
        public string? AdjustDescription { get; set; }

        /// <summary>
        /// 泵速偏移值
        /// </summary>
        public float BOffset { get; set; }

        /// <summary>
        /// T块偏移值
        /// </summary>
        public Dictionary<string, int>? TOffset { get; set; }
    }
}
