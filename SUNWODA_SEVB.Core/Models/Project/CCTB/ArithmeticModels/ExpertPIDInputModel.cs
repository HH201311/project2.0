using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels
{
    /// <summary>
    /// 专家PID输入模型类
    /// </summary>
    public class ExpertPIDInputModel
    {
        /// <summary>
        /// PID模型参数
        /// </summary>
        public PIDModel PID { get; set; }

        /// <summary>
        /// 目标值
        /// </summary>
        public double TargetValue { get; set; }

        /// <summary>
        /// 当前值1
        /// </summary>
        public double CurrentValue1 { get; set; }

        /// <summary>
        /// 当前值2
        /// </summary>
        public double CurrentValue2 { get; set; }

        /// <summary>
        /// 当前值3
        /// </summary>
        public double CurrentValue3 { get; set; }

        /// <summary>
        /// 泵速1
        /// </summary>
        public double PumpSpeed1 { get; set; }

        /// <summary>
        /// 泵速2
        /// </summary>
        public double PumpSpeed2 { get; set; }

        /// <summary>
        /// 流量1
        /// </summary>
        public double FlowValue1 { get; set; }

        /// <summary>
        /// 流量2
        /// </summary>
        public double FlowValue2 { get; set; }

        /// <summary>
        /// 流量趋势值1
        /// </summary>
        public double FluDataDelta1 { get; set; }

        /// <summary>
        /// 流量趋势值2
        /// </summary>
        public double FluDataDelta2 { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public float JJA { get; set; }

        /// <summary>
        /// 备用参数
        /// </summary>
        public object? InputData { get; set; }
    }
}
