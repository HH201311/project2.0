using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels
{
    /// <summary>
    /// 流量PID输入模型类
    /// </summary>
    public class FlowPIDInputModel
    {
        /// <summary>
        /// 泵速1
        /// </summary>
        public double PumpSpeed1 { get; set; }

        /// <summary>
        /// 泵速2
        /// </summary>
        public double PumpSpeed2 { get; set; }

        /// <summary>
        /// 流量数据
        /// </summary>
        public string? FlowData { get; set; }

        /// <summary>
        /// MA值
        /// </summary>
        public int MA { get; set; }

        /// <summary>
        /// R值
        /// </summary>
        public float R { get; set; }

        /// <summary>
        /// 流量趋势值
        /// </summary>
        public double AllTrend { get; set; }

        /// <summary>
        /// 流量趋势规则值
        /// </summary>
        public double Trend { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int TrendRule { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float Single { get; set; }

        /// <summary>
        /// 备用参数1
        /// </summary>
        public object? InputData1 { get; set; }

        /// <summary>
        /// 备用参数2
        /// </summary>
        public object? InputData2 { get; set; }
    }
}
