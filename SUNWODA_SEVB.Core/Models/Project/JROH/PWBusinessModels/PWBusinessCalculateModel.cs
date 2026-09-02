using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.PWBusinessModels
{
    /// <summary>
    /// 闭环计算过程类
    /// </summary>
    public class PWBusinessCalculateModel : ModelBase
    {
        private DateTime calculateTime;

        private bool isAdjustOfPos;
        private int targetTotalWidthOfPos;
        private int deltaW;
        private int issuedValueOfPos;

        private bool isAdjustOfNeg;
        private int targetTotalWidthOfNeg;
        private int deltaL;
        private int issuedValueOfNeg;
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
        /// 正极调机下发值
        /// </summary>
        public int IssuedValueOfPos { get => issuedValueOfPos; set => SetProperty(ref issuedValueOfPos, value); }
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
        /// 负极调机下发值
        /// </summary>
        public int IssuedValueOfNeg { get => issuedValueOfNeg; set => SetProperty(ref issuedValueOfNeg, value); }
        #endregion

        /// <summary>
        /// 结束消息
        /// </summary>
        public string EndMessage { get => endMessage; set => SetProperty(ref endMessage, value); }
    }
}
