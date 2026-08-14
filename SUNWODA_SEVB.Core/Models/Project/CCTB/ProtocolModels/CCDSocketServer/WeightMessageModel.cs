using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Models.Project.CoatingOPMM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.CCDSocketServer
{
    /// <summary>
    /// CCD发送的重量数据
    /// </summary>
    public class WeightMessageModel
    {
        /// <summary>
        /// 极耳宽度(极耳宽度列表)
        /// </summary>
        [JsonProperty("tab_width")]
        public List<ParaData>? TabWidth { get; set; }
        /// <summary>
        /// 陶瓷宽度(陶瓷宽度列表，负极没有传空列表)
        /// </summary>
        [JsonProperty("cil_width")]
        public List<ParaData>? CilWidth { get; set; }
        /// <summary>
        /// 涂膜宽度(涂膜宽度列表)
        /// </summary>
        [JsonProperty("coating_width")]
        public List<ParaData>? CoatingWidth { get; set; }
        /// <summary>
        /// 错位数据(错位数据列表)
        /// </summary>
        [JsonProperty("misalign")]
        public List<ParaData>? Misalign { get; set; }
        /// <summary>
        /// 总膜区宽度
        /// </summary>
        [JsonProperty("coating_total_width")]
        public double CoatingTotalWidth { get; set; }
        /// <summary>
        /// 膜区判定结果(膜区判定结果列表)
        /// </summary>
        [JsonProperty("judge_res")]
        public List<JudgeResult>? JudgeRes { get; set; }
        /// <summary>
        /// 极耳规格(极耳规格列表)
        /// </summary>
        [JsonProperty("tab_specification")]
        public List<ParaData>? TabSpecification { get; set; }
        /// <summary>
        /// 陶瓷规格(陶瓷规格列表，负极没有传空列表)
        /// </summary>
        [JsonProperty("cil_specification")]
        public List<ParaData>? CilSpecification { get; set; }
        /// <summary>
        /// 涂膜规格(涂膜规格列表)
        /// </summary>
        [JsonProperty("coating_specification")]
        public List<ParaData>? CoatingSpecification { get; set; }
        /// <summary>
        /// 错位规格(错位规格列表)
        /// </summary>
        [JsonProperty("misalign_specification")]
        public List<ParaData>? MisalignSpecification { get; set; }
    }
}
