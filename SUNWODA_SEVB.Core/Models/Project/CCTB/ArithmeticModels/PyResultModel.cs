using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels
{
    /// <summary>
    /// Python算法输出结果类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PyResultModel<TInPut, TResult>
    {
        /// <summary>
        /// python执行结果
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// python算法入参
        /// </summary>
        public TInPut? PyInPut { get; set; }

        /// <summary>
        /// python算法出参
        /// </summary>
        public dynamic? PyOutPut { get; set; }

        /// <summary>
        /// python解析后的算法结果
        /// </summary>
        public TResult? Result { get; set; }

        /// <summary>
        /// python算法结果信息
        /// </summary>
        public string? Message { get; set; }
    }
}
