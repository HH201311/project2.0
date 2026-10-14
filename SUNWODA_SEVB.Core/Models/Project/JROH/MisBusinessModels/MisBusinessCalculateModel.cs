using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.MisBusinessModels
{
    /// <summary>
    /// 极耳错位闭环计算过程类（模板骨架）
    /// </summary>
    /// <remarks>
    /// <para>字段风格对齐 <c>PWBusinessCalculateModel</c>：正 / 负极各记录「是否调机 + 下发值」，外加结束消息。</para>
    /// <para>⏸ 极耳错位真实的中间量（稳定极耳错位、首尾极耳角度、补偿量等，可参考 JRCW 的 <c>Business2CalculateModel</c>）待资料确认后补充。</para>
    /// </remarks>
    public class MisBusinessCalculateModel : ModelBase
    {
        private DateTime calculateTime;
        private bool isAdjustOfPos;
        private float issuedValueOfPos;
        private bool isAdjustOfNeg;
        private float issuedValueOfNeg;
        private string endMessage = string.Empty;

        /// <summary>
        /// 计算时间
        /// </summary>
        public DateTime CalculateTime { get => calculateTime; set => SetProperty(ref calculateTime, value); }

        #region 正极
        /// <summary>
        /// 是否调机正极
        /// </summary>
        public bool IsAdjustOfPos { get => isAdjustOfPos; set => SetProperty(ref isAdjustOfPos, value); }

        /// <summary>
        /// 正极调机下发值
        /// </summary>
        public float IssuedValueOfPos { get => issuedValueOfPos; set => SetProperty(ref issuedValueOfPos, value); }
        #endregion

        #region 负极
        /// <summary>
        /// 是否调机负极
        /// </summary>
        public bool IsAdjustOfNeg { get => isAdjustOfNeg; set => SetProperty(ref isAdjustOfNeg, value); }

        /// <summary>
        /// 负极调机下发值
        /// </summary>
        public float IssuedValueOfNeg { get => issuedValueOfNeg; set => SetProperty(ref issuedValueOfNeg, value); }
        #endregion

        /// <summary>
        /// 结束消息
        /// </summary>
        public string EndMessage { get => endMessage; set => SetProperty(ref endMessage, value); }
    }
}
