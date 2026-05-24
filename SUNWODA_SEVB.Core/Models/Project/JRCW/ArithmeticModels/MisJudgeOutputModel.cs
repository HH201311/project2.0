using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.ArithmeticModels
{
    /// <summary>
    /// 闭环算法出参模型
    /// </summary>
    public class MisJudgeOutputModel
    {
        /// <summary>
        /// 
        /// </summary>
        public float J1 { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float J2 { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float Press_J { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string? Dialog { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int J_Signal { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int Over_J_Num { get; set; }
    }
}
