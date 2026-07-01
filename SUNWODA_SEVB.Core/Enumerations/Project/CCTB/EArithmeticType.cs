using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Enumerations.Project.CCTB
{
    /// <summary>
    /// 算法模型类型
    /// </summary>
    public enum EAlgorithmType
    {
        Unknown,
        专家PID,
        流量PID,
        MDControl,
        TDControl,
        MDTDControl,
        MULTIMDTDControl,
    }
}
