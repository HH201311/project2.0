using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.CCDSocketServer
{
    /// <summary>
    /// CCD发送的设备信息
    /// </summary>
    public class CCTBCCDQuestModel
    {
        /// <summary>
        /// 消息类型(心跳/A湿数据/A干B湿数据/B干数据/报警：hb/data_a_wet/data_b_wet/data_b_dry/alarm)
        /// </summary>
        [JsonProperty("msgType")]
        public string? MsgType { get; set; }
        /// <summary>
        /// 设备ID(设备定义的标识)
        /// </summary>
        [JsonProperty("sid")]
        public string? Sid { get; set; }
        /// <summary>
        /// 设备侧UTC时间戳
        /// </summary>
        [JsonProperty("ts")]
        public long Ts { get; set; }
        /// <summary>
        /// 压缩格式（默认为空则不压缩，gzip）
        /// </summary>
        [JsonProperty("z")]
        public string? Z { get; set; }
        /// <summary>
        /// 数据(心跳内容为空)
        /// </summary>
        [JsonProperty("data")]
        public dynamic? Data { get; set; }
        /// <summary>
        /// 校验码(**可选校验**：对 data 的 JSON(去除空格)做 CRC32，16进制大写8位)
        /// </summary>
        [JsonProperty("sig")]
        public string? Sig { get; set; }
    }
}
