using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.MisBusinessModels
{
    /// <summary>
    /// 极耳错位单路通道采集数据类（模板骨架）
    /// </summary>
    /// <remarks>
    /// <para>结构参照 <c>MCGatherData</c>：一条通道由「时间 / 检测值 / 目标值」采样序列 + 设定值组成，可直接用于曲线绑定与 CSV 落盘。</para>
    /// <para>⏸ 极耳错位真实的通道划分（正负极各极耳的远/近边距、极耳角度序列等）待现场资料确认后调整，本类只提供承载容器。</para>
    /// </remarks>
    public class MisGatherData : ModelBase
    {
        private List<(DateTime time, float detValue, float tarValue)> values = [];
        private float setValue;

        /// <summary>
        /// 极耳错位数据：采集时间 / 检测值 / 目标值
        /// </summary>
        public List<(DateTime time, float detValue, float tarValue)> Values { get => values; set => SetProperty(ref values, value); }

        /// <summary>
        /// 极耳错位设定值
        /// </summary>
        public float SetValue { get => setValue; set => SetProperty(ref setValue, value); }
    }
}
