using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketServer
{
    /// <summary>
    /// 重量数据
    /// </summary>
    public class CCTBWEIGHTQUEST
    {
        /// <summary>
        /// 数据类型
        /// </summary>
        public string? DEVICENAME { get; set; }
        /// <summary>
        /// 设备编号
        /// </summary>
        public string? DEVICESN { get; set; }
        /// <summary>
        /// 工艺公差
        /// </summary>
        public string? TECHNIQUE { get; set; }
        /// <summary>
        /// 净重目标值
        /// </summary>
        public string? TARGET_VALUE { get; set; }
        /// <summary>
        /// 基材值
        /// </summary>
        public string? BASEVALUE { get; set; }
        /// <summary>
        /// 净重规格上限
        /// </summary>
        public string? SPEC_UP_LIM { get; set; }
        /// <summary>
        /// 净重规格下限
        /// </summary>
        public string? SPEC_DW_LIM { get; set; }
        /// <summary>
        /// 净重目标预警公差
        /// </summary>
        public double NET_TARGETWARN_TOLERANCE { get; set; }
        /// <summary>
        /// 净重目标预警上限
        /// </summary>
        public double NET_TARGETWARN_UPPER { get; set; }
        /// <summary>
        /// 净重目标预警下限
        /// </summary>
        public double NET_TARGETWARN_LOWER { get; set; }
        /// <summary>
        /// CPK
        /// </summary>
        public double CPK { get; set; }
        /// <summary>
        /// COV
        /// </summary>
        public double COV { get; set; }
        /// <summary>
        /// 单趟标准差
        /// </summary>
        public double STDDEV { get; set; }
        /// <summary>
        /// K值
        /// </summary>
        public string? K_VALUE { get; set; }
        /// <summary>
        /// b值
        /// </summary>
        public string? B_VALUE { get; set; }
        /// <summary>
        /// 卷长
        /// </summary>
        public double VOL_LENGTH { get; set; }
        /// <summary>
        /// 平均值
        /// </summary>
        public string? AVERAGE_VALUE { get; set; }
        /// <summary>
        /// 趟数
        /// </summary>
        public string? SCANCOUNT { get; set; }
        /// <summary>
        /// 有效膜区分区
        /// </summary>
        public List<TBAreaInfo>? TBAREAINFO { get; set; }
        /// <summary>
        /// 分区数
        /// </summary>
        public string? WEIGHT_COUNT { get; set; }
        /// <summary>
        /// 重量数据(数组)
        /// </summary>
        public string[]? WEIGHT { get; set; }
        /// <summary>
        /// 报警信息(同时可能有多条报警信息，没有报警为空列表)
        /// </summary>
        public List<string>? ALARMS { get; set; }
    }
}
