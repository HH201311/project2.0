using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Enumerations.Project.JROH
{
    /// <summary>
    /// 卷绕闭环业务类别
    /// </summary>
    public enum EBusinessType
    {
        /// <summary>
        /// 极片宽度闭环
        /// </summary>
        PoleWidth,

        /// <summary>
        /// 包覆OH闭环
        /// </summary>
        CoatedOH,

        /// <summary>
        /// 多级联动闭环
        /// </summary>
        MultiCorrect,

        /// <summary>
        /// 极耳错位闭环
        /// </summary>
        Mismatch,
    }
}
