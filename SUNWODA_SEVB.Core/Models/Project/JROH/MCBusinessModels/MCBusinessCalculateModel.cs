using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.MCBusinessModels
{
    /// <summary>
    /// 多级联动纠偏闭环计算过程类
    /// </summary>
    public class MCBusinessCalculateModel : ModelBase
    {
        private DateTime calculateTime;

        private bool isAdjustOfPos;
        private int targetTotalWidthOfPos;
        private int deltaW;
        private int adjustValueOfPos;

        private bool isAdjustOfNeg;
        private int targetTotalWidthOfNeg;
        private int deltaL;
        private int adjustValueOfNeg;
        private string endMessage = string.Empty;

        /// <summary>
        /// 保存时间
        /// </summary>
        public DateTime CalculateTime { get => calculateTime; set => SetProperty(ref calculateTime, value); }

        #region 正极
        /// <summary>
        /// 是否调机正极
        /// </summary>
        public bool IsAdjustOfPos { get => isAdjustOfPos; set => SetProperty(ref isAdjustOfPos, value); }

        /// <summary>
        /// 正极总宽目标值
        /// </summary>
        public int TargetTotalWidthOfPos { get => targetTotalWidthOfPos; set => SetProperty(ref targetTotalWidthOfPos, value); }

        /// <summary>
        /// 正极宽度偏差值△W
        /// </summary>
        public int DeltaW { get => deltaW; set => SetProperty(ref deltaW, value); }

        /// <summary>
        /// 调机量正极
        /// </summary>
        public int AdjustValueOfPos { get => adjustValueOfPos; set => SetProperty(ref adjustValueOfPos, value); }
        #endregion

        #region 负极
        /// <summary>
        /// 是否调机负极
        /// </summary>
        public bool IsAdjustOfNeg { get => isAdjustOfNeg; set => SetProperty(ref isAdjustOfNeg, value); }

        /// <summary>
        /// 负极总宽目标值
        /// </summary>
        public int TargetTotalWidthOfNeg { get => targetTotalWidthOfNeg; set => SetProperty(ref targetTotalWidthOfNeg, value); }

        /// <summary>
        /// 负极宽度偏差值△L
        /// </summary>
        public int DeltaL { get => deltaL; set => SetProperty(ref deltaL, value); }

        /// <summary>
        /// 调机量负极
        /// </summary>
        public int AdjustValueOfNeg { get => adjustValueOfNeg; set => SetProperty(ref adjustValueOfNeg, value); }
        #endregion

        /// <summary>
        /// 结束消息
        /// </summary>
        public string EndMessage { get => endMessage; set => SetProperty(ref endMessage, value); }
    }
}
