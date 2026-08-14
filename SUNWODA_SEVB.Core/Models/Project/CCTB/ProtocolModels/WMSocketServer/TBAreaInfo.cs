using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketServer
{
    /// <summary>
    /// 膜区分区信息
    /// </summary>
    public class TBAreaInfo
    {
        /// <summary>
        /// 膜区编号
        /// 备注：
        ///     保持和T块编号顺序一样
        /// </summary>
        public int AreaIndex { get; set; }
        /// <summary>
        /// 小分区个数
        /// </summary>
        public int AreaCount { get; set; }
        /// <summary>
        /// 膜区起点
        /// 备注：
        ///     界面配置的参数
        /// </summary>
        public int AreaHead { get; set; }
        /// <summary>
        /// 膜区终点
        /// 备注：
        ///     界面配置的参数
        /// </summary>
        public int AreaEnd { get; set; }
    }
}
