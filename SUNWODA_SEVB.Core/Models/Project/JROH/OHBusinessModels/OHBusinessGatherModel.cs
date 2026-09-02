using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.OHBusinessModels
{
    /// <summary>
    /// 包覆OH闭环采集数据类
    /// </summary>
    public class OHBusinessGatherModel
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
        /// 顶部AC：顶部负极到正极
        /// </summary>
        public int[] ACOfTop { get; set; } = [];

        /// <summary>
        /// 底部AC：底部负极到正极
        /// </summary>
        public int[] ACOfBottom { get; set; } = [];

        /// <summary>
        /// 底部SA：底部隔膜到负极
        /// </summary>
        public int[] SAOfBottom { get; set; } = [];

        /// <summary>
        /// 顶部SA：顶部隔膜到负极
        /// </summary>
        public int[] SAOfTop { get; set; } = [];

        /// <summary>
        /// 负极纠偏值-前段
        /// </summary>
        public int AdjustmentValueOfNegFront { get; set; }

        /// <summary>
        /// 正极纠偏值-前段
        /// </summary>
        public int AdjustmentValueOfPosFront { get; set; }

        /// <summary>
        /// 负极纠偏值-中段
        /// </summary>
        public int AdjustmentValueOfNegMiddle { get; set; }

        /// <summary>
        /// 正极纠偏值-中段
        /// </summary>
        public int AdjustmentValueOfPosMiddle { get; set; }

        /// <summary>
        /// 负极纠偏值-后段
        /// </summary>
        public int AdjustmentValueOfNegBack { get; set; }

        /// <summary>
        /// 正极纠偏值-后段
        /// </summary>
        public int AdjustmentValueOfPosBack { get; set; }

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
