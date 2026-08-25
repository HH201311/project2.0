using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.CCDSocketServer
{
    /// <summary>
    /// 判定结果
    /// </summary>
    public class JudgeResult
    {
        /// <summary>
        /// 膜区(哪个膜区)
        /// </summary>
        [JsonProperty("Zone")]
        public string? Zone { get; set; }
        /// <summary>
        /// 判定结果(OK或NG)
        /// </summary>
        [JsonProperty("Result")]
        public string? Result { get; set; }
    }
}
