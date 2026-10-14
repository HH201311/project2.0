using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.MCBusinessModels
{
    /// <summary>
    /// 多级联动纠偏闭环采集数据类
    /// </summary>
    public class MCBusinessGatherModel : ModelBase
    {
        private DateTime gatherTime = DateTime.Now;
        private string virtualCode = string.Empty;
        private MCGatherData infeedDataOfPos = new();
        private MCGatherData snake4DataOfPos = new();
        private MCGatherData snake3DataOfPos = new();
        private MCGatherData snake2DataOfPos = new();
        private MCGatherData snake1DataOfPos = new();
        private MCGatherData infeedDataOfNeg = new();
        private MCGatherData snake3DataOfNeg = new();
        private MCGatherData snake2DataOfNeg = new();
        private MCGatherData snake1DataOfNeg = new();
        private MCGatherData upperDiaphragmData = new();
        private MCGatherData lowerDiaphragmData = new();

        /// <summary>
        /// 采集数据时间
        /// </summary>
        public DateTime GatherTime { get => gatherTime; set => SetProperty(ref gatherTime, value); }

        /// <summary>
        /// 虚拟码
        /// </summary>
        public string VirtualCode { get => virtualCode; set => SetProperty(ref virtualCode, value); }

        #region 正极多级纠偏联动采集数据
        /// <summary>
        /// 正极入料纠偏数据
        /// </summary>
        public MCGatherData InfeedDataOfPos { get => infeedDataOfPos; set => SetProperty(ref infeedDataOfPos, value); }

        /// <summary>
        /// 正极蛇形4纠偏数据
        /// </summary>
        public MCGatherData Snake4DataOfPos { get => snake4DataOfPos; set => SetProperty(ref snake4DataOfPos, value); }

        /// <summary>
        /// 正极蛇形3纠偏数据
        /// </summary>
        public MCGatherData Snake3DataOfPos { get => snake3DataOfPos; set => SetProperty(ref snake3DataOfPos, value); }

        /// <summary>
        /// 正极蛇形2纠偏数据
        /// </summary>
        public MCGatherData Snake2DataOfPos { get => snake2DataOfPos; set => SetProperty(ref snake2DataOfPos, value); }

        /// <summary>
        /// 正极蛇形1纠偏数据
        /// </summary>
        public MCGatherData Snake1DataOfPos { get => snake1DataOfPos; set => SetProperty(ref snake1DataOfPos, value); }
        #endregion

        #region 负极多级纠偏联动采集数据
        /// <summary>
        /// 负极入料纠偏数据
        /// </summary>
        public MCGatherData InfeedDataOfNeg { get => infeedDataOfNeg; set => SetProperty(ref infeedDataOfNeg, value); }

        /// <summary>
        /// 负极蛇形3纠偏数据
        /// </summary>
        public MCGatherData Snake3DataOfNeg { get => snake3DataOfNeg; set => SetProperty(ref snake3DataOfNeg, value); }

        /// <summary>
        /// 负极蛇形2纠偏数据
        /// </summary>
        public MCGatherData Snake2DataOfNeg { get => snake2DataOfNeg; set => SetProperty(ref snake2DataOfNeg, value); }

        /// <summary>
        /// 负极蛇形1纠偏数据
        /// </summary>
        public MCGatherData Snake1DataOfNeg { get => snake1DataOfNeg; set => SetProperty(ref snake1DataOfNeg, value); }
        #endregion

        #region 隔膜多级纠偏联动采集数据
        /// <summary>
        /// 上隔膜破损纠偏数据
        /// </summary>
        public MCGatherData UpperDiaphragmData { get => upperDiaphragmData; set => SetProperty(ref upperDiaphragmData, value); }

        /// <summary>
        /// 下隔膜破损纠偏数据
        /// </summary>
        public MCGatherData LowerDiaphragmData { get => lowerDiaphragmData; set => SetProperty(ref lowerDiaphragmData, value); }
        #endregion
    }
}
