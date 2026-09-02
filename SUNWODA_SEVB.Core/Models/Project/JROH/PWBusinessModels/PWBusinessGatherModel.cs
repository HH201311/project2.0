using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.PWBusinessModels
{
    /// <summary>
    /// 闭环采集数据类
    /// </summary>
    public class PWBusinessGatherModel
    {
        /// <summary>
        /// 正极A面CCD模宽数据
        /// </summary>
        public int ModuleWidthOfPosA { get; set; }

        /// <summary>
        /// 正极B面CCD模宽数据
        /// </summary>
        public int ModuleWidthOfPosB { get; set; }

        /// <summary>
        /// 负极CCD模宽数据
        /// </summary>
        public int ModuleWidthOfNeg { get; set; }

        /// <summary>
        /// 正极CCD模宽数据
        /// </summary>
        public int ModuleWidthOfPos { get; set; }

        /// <summary>
        /// 正极飞行原点Y
        /// </summary>
        public int OriginYOfPos { get; set; }

        /// <summary>
        /// 负极飞行原点Y
        /// </summary>
        public int OriginYOfNeg { get; set; }

        /// <summary>
        /// 采集数据时间
        /// </summary>
        public DateTime GatherTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 虚拟码
        /// </summary>
        public string VirtualCode { get; set; } = string.Empty;
    }
}
