using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.ArithmeticModels
{
    /// <summary>
    /// 闭环算法MisJudge入参模型
    /// </summary>
    public class MisJudgeInputModel
    {
        /// <summary>
        /// 
        /// </summary>
        public string? Data { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public double P_Switch { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int Change { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public double T { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public double P_t { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int U_Limit { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int J_d { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int P_j_d { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int J_Single { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int Over_J_Num { get; set; }
    }
}
