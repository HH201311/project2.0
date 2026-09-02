using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.OHBusinessModels
{
    /// <summary>
    /// 包覆OH闭环参数类
    /// </summary>
    public class OHBusinessParamModel : ModelBase
    {
        #region 配置参数
        private bool _isUse;

        private int _totalWidthOfMold_Center = 78900;
        private int _totalWidthOfMold_LowerLimit = 78500;
        private int _totalWidthOfMold_UpperLimit = 79300;
        private int _poleWidthOfNeg_Center = 72700;
        private int _poleWidthOfNeg_LowerLimit = 72200;
        private int _poleWidthOfNeg_UpperLimit = 73200;
        private int _poleWidthOfPos_Center = 72900;
        private int _poleWidthOfPos_LowerLimit = 72400;
        private int _poleWidthOfPos_UpperLimit = 73400;
        private int _coatedWidthOfPos_Center = 70500;
        private int _coatedWidthOfPos_LowerLimit = 70000;
        private int _coatedWidthOfPos_UpperLimit = 71000;
        private int _cilWidthOfPos_Center = 3100;
        private int _cilWidthOfPos_LowerLimit = 2200;
        private int _cilWidthOfPos_UpperLimit = 3700;
        private int _negToPosACTop_Center = 1700;
        private int _negToPosACTop_LowerLimit = 300;
        private int _negToPosACTop_UpperLimit = 3700;
        private int _negToPosACBottom_Center = 1000;
        private int _negToPosACBottom_LowerLimit = 300;
        private int _negToPosACBottom_UpperLimit = 3700;
        private int _moldToNegSABottom_Center = 3300;
        private int _moldToNegSABottom_LowerLimit = 500;
        private int _moldToNegSABottom_UpperLimit = 5600;
        private int _moldToNegSATop_Center = 2500;
        private int _moldToNegSATop_LowerLimit = 100;
        private int _moldToNegSATop_UpperLimit = 5600;
        private int _moldToCILTop_Center = 1800;
        private int _moldToCILTop_LowerLimit = 500;
        private int _moldToCILTop_UpperLimit = 5600;
        private int _cilOverNegTop_Center = 1400;
        private int _cilOverNegTop_LowerLimit = 100;
        private int _cilOverNegTop_UpperLimit = 3900;

        private bool _isSigleClosedLoop = false;
        private float _adjustCorrectionOfPos = 1;
        private float _adjustCorrectionOfNeg = -1;
        private byte _countOfFront = 5;
        private byte _countOfBack = 5;
        private bool _isFrontOfPosDetextion = true;
        private bool _doubleSideEnableOfPos = true;
        private int _k = 1000;
        private int _maxLimitOfPos = 300;
        private int _maxLimitOfNeg = 300;
        private int _minLimitOfPos = 50;
        private int _minLimitOfNeg = 50;
        private bool _closedLoopEnablePos = true;
        private bool _closedLoopEnableNeg = true;
        private byte _adjustFrequency = 2;
        #endregion

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
        /// 是否是单边闭环
        /// </summary>
        public bool IsSigleClosedLoop { get => _isSigleClosedLoop; set => SetProperty(ref _isSigleClosedLoop, value); }

        /// <summary>
        /// 方向修正系数-正极
        /// </summary>
        public float AdjustCorrectionOfPos { get => _adjustCorrectionOfPos; set => SetProperty(ref _adjustCorrectionOfPos, value); }

        /// <summary>
        /// 方向修正系数-负极
        /// </summary>
        public float AdjustCorrectionOfNeg { get => _adjustCorrectionOfNeg; set => SetProperty(ref _adjustCorrectionOfNeg, value); }

        /// <summary>
        /// OH数据前分段个数
        /// </summary>
        public byte CountOfFront { get => _countOfFront; set => SetProperty(ref _countOfFront, value); }

        /// <summary>
        /// OH数据后分段个数
        /// </summary>
        public byte CountOfBack { get => _countOfBack; set => SetProperty(ref _countOfBack, value); }

        /// <summary>
        /// 正极卷绕CCD检测面
        /// </summary>
        public bool IsFrontOfPosDetextion { get => _isFrontOfPosDetextion; set => SetProperty(ref _isFrontOfPosDetextion, value); }

        /// <summary>
        /// 正极双面OH控制开启
        /// </summary>
        public bool DoubleSideEnableOfPos { get => _doubleSideEnableOfPos; set => SetProperty(ref _doubleSideEnableOfPos, value); }

        /// <summary>
        /// 料线向大板移动0.15所需要的纠偏值
        /// </summary>
        public int K { get => _k; set => SetProperty(ref _k, value); }

        /// <summary>
        /// 限幅调整最大值-正极
        /// </summary>
        public int MaxLimitOfPos { get => _maxLimitOfPos; set => SetProperty(ref _maxLimitOfPos, value); }

        /// <summary>
        /// 限幅调整最大值-负极
        /// </summary>
        public int MaxLimitOfNeg { get => _maxLimitOfNeg; set => SetProperty(ref _maxLimitOfNeg, value); }

        /// <summary>
        /// 限幅调整最小值-正极
        /// </summary>
        public int MinLimitOfPos { get => _minLimitOfPos; set => SetProperty(ref _minLimitOfPos, value); }

        /// <summary>
        /// 限幅调整最小值-负极
        /// </summary>
        public int MinLimitOfNeg { get => _minLimitOfNeg; set => SetProperty(ref _minLimitOfNeg, value); }

        /// <summary>
        /// 闭环功能开启按钮-正极
        /// </summary>
        public bool ClosedLoopEnablePos { get => _closedLoopEnablePos; set => SetProperty(ref _closedLoopEnablePos, value); }

        /// <summary>
        /// 闭环功能开启按钮-负极
        /// </summary>
        public bool ClosedLoopEnableNeg { get => _closedLoopEnableNeg; set => SetProperty(ref _closedLoopEnableNeg, value); }

        /// <summary>
        /// 调节频次（S电芯数）
        /// </summary>
        public byte AdjustFrequency { get => _adjustFrequency; set => SetProperty(ref _adjustFrequency, value); }
        #endregion
    }
}