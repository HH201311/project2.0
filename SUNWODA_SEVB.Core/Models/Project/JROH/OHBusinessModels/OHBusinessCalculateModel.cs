using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.OHBusinessModels
{
    /// <summary>
    /// 包覆OH闭环计算过程类
    /// </summary>
    public class OHBusinessCalculateModel : ModelBase
    {
        private DateTime calculateTime;
        private int a;
        private (int, int, int) es;
        private (int, int, int) fs;
        private int issueValueOfNegFront;
        private int issueValueOfPosFront;
        private int issueValueOfNegMiddle;
        private int issueValueOfPosMiddle;
        private int issueValueOfNegBack;
        private int issueValueOfPosBack;
        private string endMessage = string.Empty;

        /// <summary>
        /// 保存时间
        /// </summary>
        public DateTime CalculateTime { get => calculateTime; set => SetProperty(ref calculateTime, value); }

        /// <summary>
        /// A=检测面-非检测面
        /// A≥0:A=0;A＜0:发送A/2
        /// </summary>
        public int A { get => a; set => SetProperty(ref a, value); }

        /// <summary>
        /// 正极E值（前中后）
        /// </summary>
        public (int eOfSAFront, int eOfSAMiddle, int eOfSABack) Es { get => es; set => SetProperty(ref es, value); }

        /// <summary>
        /// 负极F值（前中后）
        /// </summary>
        public (int feOfSAFront, int fOfSAMiddle, int fOfSABack) Fs { get => fs; set => SetProperty(ref fs, value); }

        /// <summary>
        /// 负极纠偏发送值-前段
        /// </summary>
        public int IssueValueOfNegFront { get => issueValueOfNegFront; set => SetProperty(ref issueValueOfNegFront, value); }

        /// <summary>
        /// 正极纠偏发送值-前段
        /// </summary>
        public int IssueValueOfPosFront { get => issueValueOfPosFront; set => SetProperty(ref issueValueOfPosFront, value); }

        /// <summary>
        /// 负极纠偏发送值-中段
        /// </summary>
        public int IssueValueOfNegMiddle { get => issueValueOfNegMiddle; set => SetProperty(ref issueValueOfNegMiddle, value); }

        /// <summary>
        /// 正极纠偏发送值-中段
        /// </summary>
        public int IssueValueOfPosMiddle { get => issueValueOfPosMiddle; set => SetProperty(ref issueValueOfPosMiddle, value); }

        /// <summary>
        /// 负极纠偏发送值-后段
        /// </summary>
        public int IssueValueOfNegBack { get => issueValueOfNegBack; set => SetProperty(ref issueValueOfNegBack, value); }

        /// <summary>
        /// 正极纠偏发送值-后段
        /// </summary>
        public int IssueValueOfPosBack { get => issueValueOfPosBack; set => SetProperty(ref issueValueOfPosBack, value); }

        /// <summary>
        /// 结束消息
        /// </summary>
        public string EndMessage { get => endMessage; set => SetProperty(ref endMessage, value); }
    }
}
