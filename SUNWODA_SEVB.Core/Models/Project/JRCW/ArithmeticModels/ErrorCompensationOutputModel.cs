using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.ArithmeticModels
{
    /// <summary>
    /// 错位分解算法出参模型
    /// </summary>
    public class ErrorCompensationOutputModel
    {
        /// <summary>
        /// 
        /// </summary>
        public float DeltaCOfP { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float DeltaTCOfP { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float DeltaCOfN { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float DeltaTCOfN { get; set; }
    }
}
