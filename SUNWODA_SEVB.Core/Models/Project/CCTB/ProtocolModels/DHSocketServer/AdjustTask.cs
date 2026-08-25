using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer
{
    /// <summary>
    /// T块调机信息
    /// </summary>
    public class AdjustTask
    {
        /// <summary>
        /// T块编号
        /// </summary>
        public int TId { get; set; }

        /// <summary>
        /// T块实时位置
        /// </summary>
        public int TPosition { get; set; }

        /// <summary>
        /// T块调机量
        /// </summary>
        public int TAdjustValue { get; set; }

        /// <summary>
        /// 趟号
        /// </summary>
        public int ScanNum { get; set; }

        /// <summary>
        /// 触发调节的面密度值
        /// </summary>
        public double TriggerEpkValue { get; set; }
    }
}
