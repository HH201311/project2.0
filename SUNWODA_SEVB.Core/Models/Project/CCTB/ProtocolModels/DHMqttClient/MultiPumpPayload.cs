using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHMqttClient
{
    /// <summary>
    /// 多幅泵速调节
    /// </summary>
    public class MultiPumpPayload
    {
        /// <summary>
        /// 通道1
        /// </summary>
        [JsonProperty("ch1")]
        public double Ch1 { get; set; }

        /// <summary>
        /// 通道2
        /// </summary>
        [JsonProperty("ch2")]
        public double Ch2 { get; set; }

        /// <summary>
        /// 通道3
        /// </summary>
        [JsonProperty("ch3")]
        public double Ch3 { get; set; }

        /// <summary>
        /// 通道4
        /// </summary>
        [JsonProperty("ch4")]
        public double Ch4 { get; set; }

        /// <summary>
        /// 目标面密度
        /// </summary>
        [JsonProperty("target")]
        public double Target { get; set; }

        /// <summary>
        /// 每幅的均重列表
        /// </summary>
        [JsonProperty("average")]
        public string Average { get; set; } = string.Empty;
    }
}
