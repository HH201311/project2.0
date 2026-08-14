using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHMqttClient
{
    /// <summary>
    /// 数据上报
    /// </summary>
    public class ExportDataPayload
    {
        /// <summary>
        /// 闭环是否开启
        /// </summary>
        [JsonProperty("controlEnable")]
        public int ControlEnable { get; set; }

        /// <summary>
        /// 表示泵速控制是否开启
        /// </summary>
        [JsonProperty("pumpSpeedControlEnable")]
        public int PumpSpeedControlEnable { get; set; }

        /// <summary>
        /// 本次闭环是否调节
        /// </summary>
        [JsonProperty("isThisAdjustEnabled")]
        public int IsThisAdjustEnabled { get; set; }

        /// <summary>
        /// 涂布机走带计米
        /// </summary>
        [JsonProperty("beltPos")]
        public double BeltPos { get; set; }

        /// <summary>
        /// 目标面密度
        /// </summary>
        [JsonProperty("targetDensity")]
        public double TargetDensity { get; set; }

        /// <summary>
        /// 设定泵速
        /// </summary>
        [JsonProperty("pumpSpeed")]
        public double PumpSpeed { get; set; }

        /// <summary>
        /// A泵设定泵速
        /// </summary>
        [JsonProperty("pumpSpeed_A")]
        public double PumpSpeed_A { get; set; }

        /// <summary>
        /// B泵设定泵速
        /// </summary>
        [JsonProperty("pumpSpeed_B")]
        public double PumpSpeed_B { get; set; }

        /// <summary>
        /// 垫片尺寸：2N+1，数组
        /// </summary>
        [JsonProperty("gasket")]
        public string Gasket { get; set; } = string.Empty;

        /// <summary>
        /// 面密度毫米原始数据:数组
        /// </summary>
        [JsonProperty("density")]
        public string Density { get; set; } = string.Empty;

        /// <summary>
        /// T块对应的面密度重量：数组
        /// </summary>
        [JsonProperty("axisDensityAverage")]
        public string AxisDensityAverage { get; set; } = string.Empty;

        /// <summary>
        /// T块对应的面密度调节值：数组
        /// </summary>
        [JsonProperty("adjust")]
        public string Adjust { get; set; } = string.Empty;

        /// <summary>
        /// T块对应的位置：数组
        /// </summary>
        [JsonProperty("axisPos")]
        public string AxisPos { get; set; } = string.Empty;

        /// <summary>
        /// 目标上限  
        /// </summary>
        [JsonProperty("densityUp")]
        public double DensityUp { get; set; }

        /// <summary>
        /// 目标下限
        /// </summary>
        [JsonProperty("densityDown")]
        public double DensityDown { get; set; }

        /// <summary>
        /// T块分区总个数
        /// </summary>
        [JsonProperty("axisNum")]
        public int AxisNum { get; set; }

        /// <summary>
        /// 实时均重
        /// </summary>
        [JsonProperty("average")]
        public double Average { get; set; }

        /// <summary>
        /// 毫米数据趟数
        /// </summary>
        [JsonProperty("seq")]
        public int Seq { get; set; }

        /// <summary>
        /// 设备编号：A面、B面
        /// </summary>
        [JsonProperty("deviceName")]
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// 调机模式：区分不同的闭环或正在手动调节
        /// </summary>
        [JsonProperty("adjustMode")]
        public int AdjustMode { get; set; }

        /// <summary>
        /// 轴类型:-1垫片完全压着的,1消薄T块，2正常T块
        /// </summary>
        [JsonProperty("axisTap")]
        public string AxisTap { get; set; } = string.Empty;

        /// <summary>
        /// T块上限：数组
        /// </summary>
        [JsonProperty("posUpLimit")]
        public string PosUpLimit { get; set; } = string.Empty;

        /// <summary>
        /// T块下限：一个值
        /// </summary>
        [JsonProperty("posLowLimit")]
        public string PosLowLimit { get; set; } = string.Empty;

        /// <summary>
        /// T块使能：数组(从1开始，代表指定编号的T块未使能)
        /// </summary>
        [JsonProperty("disabledTblock")]
        public string DisabledTblock { get; set; } = string.Empty;
    }
}
