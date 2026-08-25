using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer
{
    /// <summary>
    /// 模头分区数据
    /// </summary>
    public class WeightDataRequest
    {
        /// <summary>
        /// 设备编号
        /// 备注：
        ///     A：A面净重数据 B：B面净重数据
        /// </summary>
        public string? DEVICENAME { get; set; }
        /// <summary>
        /// 工艺公差
        /// </summary>
        public double TECHNIQUE { get; set; }
        /// <summary>
        /// 目标值
        /// </summary>
        public double TARGET_VALUE { get; set; }
        /// <summary>
        /// 基材值
        /// </summary>
        public double BASEVALUE { get; set; }
        /// <summary>
        /// 规格上限
        /// </summary>
        public double SPEC_UP_LIM { get; set; }
        /// <summary>
        /// 规格下限
        /// </summary>
        public double SPEC_DW_LIM { get; set; }
        /// <summary>
        /// 预警公差
        /// </summary>
        public double ALARMTOL { get; set; }
        /// <summary>
        /// 分区数
        /// </summary>
        public int ZONENUM { get; set; }
        /// <summary>
        /// K值
        /// </summary>
        public double K_VALUE { get; set; }
        /// <summary>
        /// b值
        /// </summary>
        public double B_VALUE { get; set; }
        /// <summary>
        /// 平均值
        /// </summary>
        public double AVERAGE_VALUE { get; set; }
        /// <summary>
        /// 趟数
        /// </summary>
        public int SCANCOUNT { get; set; }
        /// <summary>
        /// 模头分区重量数据
        /// </summary>
        public double[]? WEIGHTDATAS { get; set; }
    }
}
