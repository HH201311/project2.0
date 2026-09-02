using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.PWBusinessModels
{
    /// <summary>
    /// 极片宽度闭环参数类
    /// </summary>
    public class PWBusinessParamModel : ModelBase
    {
        #region 配置参数
        private bool _isUse;

        private int _totalWidthOfMold_Center = 196000;
        private int _totalWidthOfMold_LowerLimit = 195500;
        private int _totalWidthOfMold_UpperLimit = 196500;
        private int _poleWidthOfNeg_Center = 191000;
        private int _poleWidthOfNeg_LowerLimit = 190500;
        private int _poleWidthOfNeg_UpperLimit = 191500;
        private int _poleWidthOfPos_Center = 190800;
        private int _poleWidthOfPos_LowerLimit = 190300;
        private int _poleWidthOfPos_UpperLimit = 191300;
        private int _coatedWidthOfPos_Center = 188300;
        private int _coatedWidthOfPos_LowerLimit = 187800;
        private int _coatedWidthOfPos_UpperLimit = 188800;
        private int _cilWidthOfPos_Center = 2500;
        private int _cilWidthOfPos_LowerLimit = 2000;
        private int _cilWidthOfPos_UpperLimit = 3000;
        private int _negToPosACTop_Center = 1600;
        private int _negToPosACTop_LowerLimit = 600;
        private int _negToPosACTop_UpperLimit = 3500;
        private int _negToPosACBottom_Center = 1100;
        private int _negToPosACBottom_LowerLimit = 400;
        private int _negToPosACBottom_UpperLimit = 3200;
        private int _moldToNegSABottom_Center = 2200;
        private int _moldToNegSABottom_LowerLimit = 1000;
        private int _moldToNegSABottom_UpperLimit = 4500;
        private int _moldToNegSATop_Center = 2800;
        private int _moldToNegSATop_LowerLimit = 500;
        private int _moldToNegSATop_UpperLimit = 4000;
        private int _moldToCILTop_Center = 1900;
        private int _moldToCILTop_LowerLimit = 500;
        private int _moldToCILTop_UpperLimit = 5000;
        private int _cilOverNegTop_Center = 900;
        private int _cilOverNegTop_LowerLimit = 90;
        private int _cilOverNegTop_UpperLimit = 33500;

        private float _adjustCorrectionOfPos = 1;
        private float _adjustCorrectionOfNeg = -1;
        private int _maxAdjustmentLimitPos = 400;
        private int _maxAdjustmentLimitNeg = 400;
        private int _minAdjustmentLimitPos = 100;
        private int _minAdjustmentLimitNeg = 100;
        private bool _cpControlEnablePos = true;
        private bool _cpControlEnableNeg = true;
        private int _ohWidthCheckValuePos = 1000;
        private int _ohWidthCheckValueNeg = 1000;
        private bool _closedLoopEnablePos = true;
        private bool _closedLoopEnableNeg = true;
        private byte _adjustFrequency = 2;
        private byte _adjustInterval = 1;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsUse { get => _isUse; set => SetProperty(ref _isUse, value); }

        #region CP管控参数 - 基础尺寸
        /// <summary>
        /// 隔膜总宽度基准值
        /// </summary>
        public int TotalWidthOfMold_Center { get => _totalWidthOfMold_Center; set => SetProperty(ref _totalWidthOfMold_Center, value); }

        /// <summary>
        /// 隔膜总宽度范围下限值
        /// </summary>
        public int TotalWidthOfMold_LowerLimit { get => _totalWidthOfMold_LowerLimit; set => SetProperty(ref _totalWidthOfMold_LowerLimit, value); }

        /// <summary>
        /// 隔膜总宽度范围上限值
        /// </summary>
        public int TotalWidthOfMold_UpperLimit { get => _totalWidthOfMold_UpperLimit; set => SetProperty(ref _totalWidthOfMold_UpperLimit, value); }

        /// <summary>
        /// 负极极片总宽基准值
        /// </summary>
        public int PoleWidthOfNeg_Center { get => _poleWidthOfNeg_Center; set => SetProperty(ref _poleWidthOfNeg_Center, value); }

        /// <summary>
        /// 负极极片总宽范围下限值
        /// </summary>
        public int PoleWidthOfNeg_LowerLimit { get => _poleWidthOfNeg_LowerLimit; set => SetProperty(ref _poleWidthOfNeg_LowerLimit, value); }

        /// <summary>
        /// 负极极片总宽范围上限值
        /// </summary>
        public int PoleWidthOfNeg_UpperLimit { get => _poleWidthOfNeg_UpperLimit; set => SetProperty(ref _poleWidthOfNeg_UpperLimit, value); }

        /// <summary>
        /// 正极极片总宽基准值
        /// </summary>
        public int PoleWidthOfPos_Center { get => _poleWidthOfPos_Center; set => SetProperty(ref _poleWidthOfPos_Center, value); }

        /// <summary>
        /// 正极极片总宽范围下限值
        /// </summary>
        public int PoleWidthOfPos_LowerLimit { get => _poleWidthOfPos_LowerLimit; set => SetProperty(ref _poleWidthOfPos_LowerLimit, value); }

        /// <summary>
        /// 正极极片总宽范围上限值
        /// </summary>
        public int PoleWidthOfPos_UpperLimit { get => _poleWidthOfPos_UpperLimit; set => SetProperty(ref _poleWidthOfPos_UpperLimit, value); }

        /// <summary>
        /// 正极涂膜宽度基准值
        /// </summary>
        public int CoatedWidthOfPos_Center { get => _coatedWidthOfPos_Center; set => SetProperty(ref _coatedWidthOfPos_Center, value); }

        /// <summary>
        /// 正极涂膜宽度范围下限值
        /// </summary>
        public int CoatedWidthOfPos_LowerLimit { get => _coatedWidthOfPos_LowerLimit; set => SetProperty(ref _coatedWidthOfPos_LowerLimit, value); }

        /// <summary>
        /// 正极涂膜宽度范围上限值
        /// </summary>
        public int CoatedWidthOfPos_UpperLimit { get => _coatedWidthOfPos_UpperLimit; set => SetProperty(ref _coatedWidthOfPos_UpperLimit, value); }

        /// <summary>
        /// 正极CIL宽度基准值
        /// </summary>
        public int CilWidthOfPos_Center { get => _cilWidthOfPos_Center; set => SetProperty(ref _cilWidthOfPos_Center, value); }

        /// <summary>
        /// 正极CIL宽度范围下限值
        /// </summary>
        public int CilWidthOfPos_LowerLimit { get => _cilWidthOfPos_LowerLimit; set => SetProperty(ref _cilWidthOfPos_LowerLimit, value); }

        /// <summary>
        /// 正极CIL宽度范围上限值
        /// </summary>
        public int CilWidthOfPos_UpperLimit { get => _cilWidthOfPos_UpperLimit; set => SetProperty(ref _cilWidthOfPos_UpperLimit, value); }

        /// <summary>
        /// 负极到正极AC顶部基准值
        /// </summary>
        public int NegToPosACTop_Center { get => _negToPosACTop_Center; set => SetProperty(ref _negToPosACTop_Center, value); }

        /// <summary>
        /// 负极到正极AC顶部范围下限值
        /// </summary>
        public int NegToPosACTop_LowerLimit { get => _negToPosACTop_LowerLimit; set => SetProperty(ref _negToPosACTop_LowerLimit, value); }

        /// <summary>
        /// 负极到正极AC顶部范围上限值
        /// </summary>
        public int NegToPosACTop_UpperLimit { get => _negToPosACTop_UpperLimit; set => SetProperty(ref _negToPosACTop_UpperLimit, value); }

        /// <summary>
        /// 负极到正极AC底部基准值
        /// </summary>
        public int NegToPosACBottom_Center { get => _negToPosACBottom_Center; set => SetProperty(ref _negToPosACBottom_Center, value); }

        /// <summary>
        /// 负极到正极AC底部范围下限值
        /// </summary>
        public int NegToPosACBottom_LowerLimit { get => _negToPosACBottom_LowerLimit; set => SetProperty(ref _negToPosACBottom_LowerLimit, value); }

        /// <summary>
        /// 负极到正极AC底部范围上限值
        /// </summary>
        public int NegToPosACBottom_UpperLimit { get => _negToPosACBottom_UpperLimit; set => SetProperty(ref _negToPosACBottom_UpperLimit, value); }

        /// <summary>
        /// 隔膜到负极SA底部基准值
        /// </summary>
        public int MoldToNegSABottom_Center { get => _moldToNegSABottom_Center; set => SetProperty(ref _moldToNegSABottom_Center, value); }

        /// <summary>
        /// 隔膜到负极SA底部范围下限值
        /// </summary>
        public int MoldToNegSABottom_LowerLimit { get => _moldToNegSABottom_LowerLimit; set => SetProperty(ref _moldToNegSABottom_LowerLimit, value); }

        /// <summary>
        /// 隔膜到负极SA底部范围上限值
        /// </summary>
        public int MoldToNegSABottom_UpperLimit { get => _moldToNegSABottom_UpperLimit; set => SetProperty(ref _moldToNegSABottom_UpperLimit, value); }

        /// <summary>
        /// 隔膜到负极SA顶部基准值
        /// </summary>
        public int MoldToNegSATop_Center { get => _moldToNegSATop_Center; set => SetProperty(ref _moldToNegSATop_Center, value); }

        /// <summary>
        /// 隔膜到负极SA顶部范围下限值
        /// </summary>
        public int MoldToNegSATop_LowerLimit { get => _moldToNegSATop_LowerLimit; set => SetProperty(ref _moldToNegSATop_LowerLimit, value); }

        /// <summary>
        /// 隔膜到负极SA顶部范围上限值
        /// </summary>
        public int MoldToNegSATop_UpperLimit { get => _moldToNegSATop_UpperLimit; set => SetProperty(ref _moldToNegSATop_UpperLimit, value); }

        /// <summary>
        /// 隔膜到CIL顶部基准值
        /// </summary>
        public int MoldToCILTop_Center { get => _moldToCILTop_Center; set => SetProperty(ref _moldToCILTop_Center, value); }

        /// <summary>
        /// 隔膜到CIL顶部范围下限值
        /// </summary>
        public int MoldToCILTop_LowerLimit { get => _moldToCILTop_LowerLimit; set => SetProperty(ref _moldToCILTop_LowerLimit, value); }

        /// <summary>
        /// 隔膜到CIL顶部范围上限值
        /// </summary>
        public int MoldToCILTop_UpperLimit { get => _moldToCILTop_UpperLimit; set => SetProperty(ref _moldToCILTop_UpperLimit, value); }

        /// <summary>
        /// CIL超负极顶部基准值
        /// </summary>
        public int CilOverNegTop_Center { get => _cilOverNegTop_Center; set => SetProperty(ref _cilOverNegTop_Center, value); }

        /// <summary>
        /// CIL超负极顶部范围下限值
        /// </summary>
        public int CilOverNegTop_LowerLimit { get => _cilOverNegTop_LowerLimit; set => SetProperty(ref _cilOverNegTop_LowerLimit, value); }

        /// <summary>
        /// CIL超负极顶部范围上限值
        /// </summary>
        public int CilOverNegTop_UpperLimit { get => _cilOverNegTop_UpperLimit; set => SetProperty(ref _cilOverNegTop_UpperLimit, value); }
        #endregion

        #region 闭环控制参数 - 控制逻辑
        /// <summary>
        /// 正极方向修正系数
        /// </summary>
        public float AdjustCorrectionOfPos { get => _adjustCorrectionOfPos; set => SetProperty(ref _adjustCorrectionOfPos, value); }

        /// <summary>
        /// 负极方向修正系数
        /// </summary>
        public float AdjustCorrectionOfNeg { get => _adjustCorrectionOfNeg; set => SetProperty(ref _adjustCorrectionOfNeg, value); }

        /// <summary>
        /// 正极限幅调整最大值
        /// </summary>
        public int MaxAdjustmentLimitPos { get => _maxAdjustmentLimitPos; set => SetProperty(ref _maxAdjustmentLimitPos, value); }

        /// <summary>
        /// 负极限幅调整最大值
        /// </summary>
        public int MaxAdjustmentLimitNeg { get => _maxAdjustmentLimitNeg; set => SetProperty(ref _maxAdjustmentLimitNeg, value); }

        /// <summary>
        /// 正极限幅调整最小值
        /// </summary>
        public int MinAdjustmentLimitPos { get => _minAdjustmentLimitPos; set => SetProperty(ref _minAdjustmentLimitPos, value); }

        /// <summary>
        /// 负极限幅调整最小值
        /// </summary>
        public int MinAdjustmentLimitNeg { get => _minAdjustmentLimitNeg; set => SetProperty(ref _minAdjustmentLimitNeg, value); }

        /// <summary>
        /// 正极极片总宽CP管控
        /// </summary>
        public bool CpControlEnablePos { get => _cpControlEnablePos; set => SetProperty(ref _cpControlEnablePos, value); }

        /// <summary>
        /// 负极极片总宽CP管控
        /// </summary>
        public bool CpControlEnableNeg { get => _cpControlEnableNeg; set => SetProperty(ref _cpControlEnableNeg, value); }

        /// <summary>
        /// 正极OH总宽防呆尺寸
        /// </summary>
        public int OhWidthCheckValuePos { get => _ohWidthCheckValuePos; set => SetProperty(ref _ohWidthCheckValuePos, value); }

        /// <summary>
        /// 负极OH总宽防呆尺寸
        /// </summary>
        public int OhWidthCheckValueNeg { get => _ohWidthCheckValueNeg; set => SetProperty(ref _ohWidthCheckValueNeg, value); }

        /// <summary>
        /// 正极闭环功能开启按钮
        /// </summary>
        public bool ClosedLoopEnablePos { get => _closedLoopEnablePos; set => SetProperty(ref _closedLoopEnablePos, value); }

        /// <summary>
        /// 负极闭环功能开启按钮
        /// </summary>
        public bool ClosedLoopEnableNeg { get => _closedLoopEnableNeg; set => SetProperty(ref _closedLoopEnableNeg, value); }

        /// <summary>
        /// 正负极调节频次（电芯数）
        /// </summary>
        public byte AdjustFrequency { get => _adjustFrequency; set => SetProperty(ref _adjustFrequency, value); }

        /// <summary>
        /// 正负极调节间隔（n）
        /// </summary>
        public byte AdjustInterval { get => _adjustInterval; set => SetProperty(ref _adjustInterval, value); }
        #endregion
        #endregion
    }
}