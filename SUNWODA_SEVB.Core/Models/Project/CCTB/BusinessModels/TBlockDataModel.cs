using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.BusinessModels
{
    /// <summary>
    /// T块采集数据
    /// </summary>
    public class TBlockDataModel
    {
        /// <summary>
        /// 采集时间
        /// </summary>
        public DateTime GatherTime { get; set; }

        /// <summary>
        /// AB面
        /// </summary>
        public ESideType SideType { get; set; }

        /// <summary>
        /// 设备编号
        /// 备注：
        ///     A：A面模头 B：B面模头
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// 调机模式
        /// 备注：0：手动；1：上川闭环；2：欣旺达TD闭环；3：欣旺达MD闭环；4：欣旺达MDTD闭环
        /// </summary>
        public int TAdjustMode { get; set; }

        /// <summary>
        /// T块总数
        /// </summary>
        public int TNumber { get; set; }

        /// <summary>
        /// T块信息
        /// </summary>
        public List<TBlockInfo> TBlockInfoLis { get; set; } = [];

        /// <summary>
        /// 响应时间
        /// </summary>
        public DateTime ReponseTime { get; set; }
    }
}
