using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.CCDSocketServer
{
    /// <summary>
    /// 数据对象
    /// </summary>
    public class ParaData
    {
        /// <summary>
        /// 参数名
        /// </summary>
        [JsonProperty("PName")]
        public string? PName { get; set; }
        /// <summary>
        /// 参数值
        /// </summary>
        [JsonProperty("PValue")]
        public string? PValue { get; set; }
    }
}
