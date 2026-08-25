using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Enumerations.Project.CCTB
{
    /// <summary>
    /// 闭环计算所需的数据来源（4套协议）：大成、锐奇、上川/宇世嘉、曼恩斯特
    /// </summary>
    public enum EDataSourceOfClosedLoop
    {
        WMSocketClient,
        WMSocketServer,
        DHSocketServer,
        DHMqttClient,
    }
}
