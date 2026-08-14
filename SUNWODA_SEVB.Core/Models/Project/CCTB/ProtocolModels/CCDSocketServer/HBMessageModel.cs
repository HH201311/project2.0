using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.CCDSocketServer
{
    /// <summary>
    /// CCD发送的心跳数据
    /// </summary>
    public class HBMessageModel
    {
        /// <summary>
        /// 心跳信息
        /// </summary>
        [JsonProperty("status")]
        public int Status { get; set; }
    }
}
