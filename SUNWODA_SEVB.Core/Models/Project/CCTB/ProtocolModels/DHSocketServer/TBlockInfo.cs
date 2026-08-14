using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.DHSocketServer
{
    /// <summary>
    /// T块信息
    /// </summary>
    public class TBlockInfo
    {
        /// <summary>
        /// T块编号
        /// </summary>
        public int TId { get; set; }

        /// <summary>
        /// T块调机模式
        /// True:自动；False:半自动
        /// </summary>
        public bool TAdjustMode { get; set; }

        /// <summary>
        /// T块标识
        /// 备注：
        ///     默认值(不用此变量时)：0；
        ///     (上川:1：非边缘T块；2：边缘T块)
        ///     (曼恩斯特:-1：垫片完全压着的；1：消薄T块；2：正常T块)
        /// </summary>
        public int TTag { get; set; }

        /// <summary>
        /// T块使能状态（0：未使能；1：使能）
        /// </summary>
        [Description("目前仅有 曼恩斯特Mqtt 协议有使能字段，其他都默认为1")]
        public int TEnable { get; set; } = 1;

        /// <summary>
        /// T块报警Code
        /// </summary>
        public List<int>? AlarmCode { get; set; }

        /// <summary>
        /// T块实时位置
        /// </summary>
        public int TPosition { get; set; }

        /// <summary>
        /// T块调机信息
        /// </summary>
        public TAdjustInfo? TAdjustInfo { get; set; }

        /// <summary>
        /// T块上限位置
        /// </summary>
        public int TUpperLimit { get; set; }

        /// <summary>
        /// T块下限位置
        /// </summary>
        public int TLowerLimit { get; set; }

        /// <summary>
        /// T块报警信息
        /// </summary>
        [Obsolete("弃用，依据AlarmCode查表获取报警信息")]
        public string? TErrorInfo { get; set; }
    }
}
