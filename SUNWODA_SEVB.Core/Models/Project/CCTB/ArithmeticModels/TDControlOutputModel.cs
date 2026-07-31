using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels
{
    /// <summary>
    /// TD算法输出模型类
    /// </summary>
    public class TDControlOutputModel
    {
        /// <summary>
        /// 调整模式
        /// </summary>
        public int TAdjustMode { get; set; }

        /// <summary>
        /// 调机描述
        /// </summary>
        public string? TAdjustDescription { get; set; }

        /// <summary>
        /// T块偏移值
        /// </summary>
        public Dictionary<string, int>? TOffset { get; set; }
    }
}
