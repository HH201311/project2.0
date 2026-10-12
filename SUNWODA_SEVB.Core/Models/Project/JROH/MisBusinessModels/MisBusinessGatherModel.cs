using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.MisBusinessModels
{
    /// <summary>
    /// 极耳错位闭环采集数据类（模板骨架）
    /// </summary>
    /// <remarks>
    /// <para>一次触发（一个电芯 / 一个卷芯）对应一条本模型实例，经 <c>MisBusinessService.EnqueueGatherData</c> 入队后由闭环消费循环串行处理。</para>
    /// <para>⏸ 当前只保留正极 / 负极两路占位通道；真实采集字段（PLC 点位、CCD 边距、极耳角度序列、极耳层数等）<b>待现场资料确认</b>后补充，禁止臆造（规范 B-09）。</para>
    /// </remarks>
    public class MisBusinessGatherModel : ModelBase
    {
        private DateTime gatherTime = DateTime.Now;
        private string virtualCode = string.Empty;
        private MisGatherData positiveTabData = new();
        private MisGatherData negativeTabData = new();

        /// <summary>
        /// 采集数据时间
        /// </summary>
        public DateTime GatherTime { get => gatherTime; set => SetProperty(ref gatherTime, value); }

        /// <summary>
        /// 虚拟码
        /// </summary>
        public string VirtualCode { get => virtualCode; set => SetProperty(ref virtualCode, value); }

        #region 极耳错位采集数据
        /// <summary>
        /// 正极极耳错位数据（占位通道）
        /// </summary>
        public MisGatherData PositiveTabData { get => positiveTabData; set => SetProperty(ref positiveTabData, value); }

        /// <summary>
        /// 负极极耳错位数据（占位通道）
        /// </summary>
        public MisGatherData NegativeTabData { get => negativeTabData; set => SetProperty(ref negativeTabData, value); }
        #endregion
    }
}
