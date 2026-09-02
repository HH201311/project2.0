using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.MCBusinessModels
{
    /// <summary>
    /// 多级联动纠偏闭环参数类
    /// </summary>
    public class MCBusinessParamModel : ModelBase
    {
        #region 配置参数
        private float _adjustCorrectionOfPos = 1;
        private float _adjustCorrectionOfNeg = -1;
        private float _k = 0.5f;
        private float _d = 0.05f;
        private bool _closedLoopEnablePos = true;
        private bool _closedLoopEnableNeg = true;
        #endregion

        #region 闭环控制参数 - 控制逻辑
        /// <summary>
        /// 方向修正系数-正极
        /// </summary>
        public float AdjustCorrectionOfPos { get => _adjustCorrectionOfPos; set => SetProperty(ref _adjustCorrectionOfPos, value); }

        /// <summary>
        /// 方向修正系数-负极
        /// </summary>
        public float AdjustCorrectionOfNeg { get => _adjustCorrectionOfNeg; set => SetProperty(ref _adjustCorrectionOfNeg, value); }

        /// <summary>
        /// 触发调节阈值 K(50%~80%,范围防呆)
        /// </summary>
        public float K { get => _k; set => SetProperty(ref _k, value); }

        /// <summary>
        /// 调整反馈值 D偏差值（ 0.05~0.3,范围防呆)
        /// </summary>
        public float D { get => _d; set => SetProperty(ref _d, value); }

        /// <summary>
        /// 闭环功能开启按钮-正极
        /// </summary>
        public bool ClosedLoopEnablePos { get => _closedLoopEnablePos; set => SetProperty(ref _closedLoopEnablePos, value); }

        /// <summary>
        /// 闭环功能开启按钮-负极
        /// </summary>
        public bool ClosedLoopEnableNeg { get => _closedLoopEnableNeg; set => SetProperty(ref _closedLoopEnableNeg, value); }
        #endregion
    }
}