using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.CCDSocketServer
{
    /// <summary>
    /// CCD发送的报警信息
    /// </summary>
    public class AlarmMessageModel
    {
        /// <summary>
        /// 膜区(哪个膜区(A湿右/A干右/A湿左/A干左/B湿右/B干右/B湿左/B干左))
        /// </summary>
        [JsonProperty("zone")]
        public string? Zone { get; set; }
        /// <summary>
        /// 报警信息
        /// </summary>
        [JsonProperty("info")]
        public string? Info { get; set; }
    }
}
