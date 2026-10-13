using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.MisBusinessModels
{
    /// <summary>
    /// 极耳错位闭环参数类
    /// </summary>
    /// <remarks>
    /// <para>参数清单依据现场《CP控制参数 / 设备控制参数》表整理（2026-10-10）。</para>
    /// <para>「电芯结构」「周长一致性数据」「入料调节频次」为二选一取值，用 bool 承载（与 OH 业务「正极卷绕CCD检测面」同一做法），界面用开关切换文案。</para>
    /// <para>⚠️ 表中以符号给出的项（d / a / b / △S / L0标准 / L总 / D中 / D尾）**未给出具体数值**，默认值一律置 0；现场确认后以配置界面与
    /// <c>docs/JROH/配置键表.csv</c> 为准，禁止臆造（规范 B-09）。</para>
    /// </remarks>
    public class MisBusinessParamModel : ModelBase
    {
        #region CP控制参数
        private float _misalignmentOfTab;
        private bool _isSingleTab = true;
        private float _needleCircumference = 265;
        private float _nearEdgeOfPos = 24;
        private float _nearEdgeOfNeg = 24;
        private float _diaphragmLengthOfCell;
        private float _totalDiaphragmLengthPerRoll;

        /// <summary>
        /// 极耳错位量（表中记作 d）
        /// </summary>
        public float MisalignmentOfTab { get => _misalignmentOfTab; set => SetProperty(ref _misalignmentOfTab, value); }

        /// <summary>
        /// 电芯结构：true = 单极耳，false = 双极耳
        /// </summary>
        public bool IsSingleTab { get => _isSingleTab; set => SetProperty(ref _isSingleTab, value); }

        /// <summary>
        /// 卷针周长
        /// </summary>
        public float NeedleCircumference { get => _needleCircumference; set => SetProperty(ref _needleCircumference, value); }

        /// <summary>
        /// 正极近边距
        /// </summary>
        public float NearEdgeOfPos { get => _nearEdgeOfPos; set => SetProperty(ref _nearEdgeOfPos, value); }

        /// <summary>
        /// 负极近边距
        /// </summary>
        public float NearEdgeOfNeg { get => _nearEdgeOfNeg; set => SetProperty(ref _nearEdgeOfNeg, value); }

        /// <summary>
        /// 电芯隔膜长度（表中记作 L0标准）
        /// </summary>
        public float DiaphragmLengthOfCell { get => _diaphragmLengthOfCell; set => SetProperty(ref _diaphragmLengthOfCell, value); }

        /// <summary>
        /// 每卷隔膜总长（表中记作 L总）
        /// </summary>
        public float TotalDiaphragmLengthPerRoll { get => _totalDiaphragmLengthPerRoll; set => SetProperty(ref _totalDiaphragmLengthPerRoll, value); }
        #endregion

        #region 设备控制参数 - 缓存与卷针
        private int _cacheCountOfPos;
        private int _cacheCountOfNeg;
        private int _needleCount = 3;
        private float _diameterChangePerMm;
        private int _diameterAdjustFrequency = 1;
        private bool _isPosConsistency = true;

        /// <summary>
        /// 正极缓存个数（表中记作 a）
        /// </summary>
        public int CacheCountOfPos { get => _cacheCountOfPos; set => SetProperty(ref _cacheCountOfPos, value); }

        /// <summary>
        /// 负极缓存个数（表中记作 b）
        /// </summary>
        public int CacheCountOfNeg { get => _cacheCountOfNeg; set => SetProperty(ref _cacheCountOfNeg, value); }

        /// <summary>
        /// 设备卷针个数
        /// </summary>
        public int NeedleCount { get => _needleCount; set => SetProperty(ref _needleCount, value); }

        /// <summary>
        /// 1mm 周长变径（表中记作 △S）
        /// </summary>
        public float DiameterChangePerMm { get => _diameterChangePerMm; set => SetProperty(ref _diameterChangePerMm, value); }

        /// <summary>
        /// 变径调节频次
        /// </summary>
        public int DiameterAdjustFrequency { get => _diameterAdjustFrequency; set => SetProperty(ref _diameterAdjustFrequency, value); }

        /// <summary>
        /// 周长一致性数据：true = 正极，false = 负极
        /// </summary>
        public bool IsPosConsistency { get => _isPosConsistency; set => SetProperty(ref _isPosConsistency, value); }
        #endregion

        #region 设备控制参数 - 换卷收集数
        private int _collectCountOfDiaphragm = 5;
        private int _collectCountOfPos = 5;
        private int _collectCountOfNeg = 5;

        /// <summary>
        /// 换隔膜收集数
        /// </summary>
        public int CollectCountOfDiaphragm { get => _collectCountOfDiaphragm; set => SetProperty(ref _collectCountOfDiaphragm, value); }

        /// <summary>
        /// 换正极收集数
        /// </summary>
        public int CollectCountOfPos { get => _collectCountOfPos; set => SetProperty(ref _collectCountOfPos, value); }

        /// <summary>
        /// 换负极收集数
        /// </summary>
        public int CollectCountOfNeg { get => _collectCountOfNeg; set => SetProperty(ref _collectCountOfNeg, value); }
        #endregion

        #region 设备控制参数 - 限幅
        private float _maxLimitOfDieCut = 15;
        private float _minLimitOfDieCut = 2;
        private float _maxLimitOfDiameterChange = 30;
        private float _minLimitOfDiameterChange = 3;
        private float _maxLimitOfInfeed = 2;
        private float _minLimitOfInfeed = 0.5f;

        /// <summary>
        /// 模切最大限幅
        /// </summary>
        public float MaxLimitOfDieCut { get => _maxLimitOfDieCut; set => SetProperty(ref _maxLimitOfDieCut, value); }

        /// <summary>
        /// 模切最小限幅
        /// </summary>
        public float MinLimitOfDieCut { get => _minLimitOfDieCut; set => SetProperty(ref _minLimitOfDieCut, value); }

        /// <summary>
        /// 变径最大限幅
        /// </summary>
        public float MaxLimitOfDiameterChange { get => _maxLimitOfDiameterChange; set => SetProperty(ref _maxLimitOfDiameterChange, value); }

        /// <summary>
        /// 变径最小限幅
        /// </summary>
        public float MinLimitOfDiameterChange { get => _minLimitOfDiameterChange; set => SetProperty(ref _minLimitOfDiameterChange, value); }

        /// <summary>
        /// 入料最大限幅
        /// </summary>
        public float MaxLimitOfInfeed { get => _maxLimitOfInfeed; set => SetProperty(ref _maxLimitOfInfeed, value); }

        /// <summary>
        /// 入料最小限幅
        /// </summary>
        public float MinLimitOfInfeed { get => _minLimitOfInfeed; set => SetProperty(ref _minLimitOfInfeed, value); }
        #endregion

        #region 设备控制参数 - 调节频次
        private bool _is2nInfeedFrequency = true;

        /// <summary>
        /// 入料调节频次：true = 2n，false = 3n
        /// </summary>
        public bool Is2nInfeedFrequency { get => _is2nInfeedFrequency; set => SetProperty(ref _is2nInfeedFrequency, value); }
        #endregion

        #region 设备控制参数 - 前馈错位
        private float _feedforwardMiddleMisalignmentOfDiaphragm;
        private float _feedforwardEndMisalignmentOfDiaphragm;
        private float _feedforwardMiddleMisalignmentOfPos;
        private float _feedforwardEndMisalignmentOfPos;
        private float _feedforwardMiddleMisalignmentOfNeg;
        private float _feedforwardEndMisalignmentOfNeg;

        /// <summary>
        /// 隔膜前馈中间错位（表中记作 D中）
        /// </summary>
        public float FeedforwardMiddleMisalignmentOfDiaphragm { get => _feedforwardMiddleMisalignmentOfDiaphragm; set => SetProperty(ref _feedforwardMiddleMisalignmentOfDiaphragm, value); }

        /// <summary>
        /// 隔膜前馈首尾错位（表中记作 D尾）
        /// </summary>
        public float FeedforwardEndMisalignmentOfDiaphragm { get => _feedforwardEndMisalignmentOfDiaphragm; set => SetProperty(ref _feedforwardEndMisalignmentOfDiaphragm, value); }

        /// <summary>
        /// 正极前馈中间错位（表中记作 D中）
        /// </summary>
        public float FeedforwardMiddleMisalignmentOfPos { get => _feedforwardMiddleMisalignmentOfPos; set => SetProperty(ref _feedforwardMiddleMisalignmentOfPos, value); }

        /// <summary>
        /// 正极前馈首尾错位（表中记作 D尾）
        /// </summary>
        public float FeedforwardEndMisalignmentOfPos { get => _feedforwardEndMisalignmentOfPos; set => SetProperty(ref _feedforwardEndMisalignmentOfPos, value); }

        /// <summary>
        /// 负极前馈中间错位（表中记作 D中）
        /// </summary>
        public float FeedforwardMiddleMisalignmentOfNeg { get => _feedforwardMiddleMisalignmentOfNeg; set => SetProperty(ref _feedforwardMiddleMisalignmentOfNeg, value); }

        /// <summary>
        /// 负极前馈首尾错位（表中记作 D尾）
        /// </summary>
        public float FeedforwardEndMisalignmentOfNeg { get => _feedforwardEndMisalignmentOfNeg; set => SetProperty(ref _feedforwardEndMisalignmentOfNeg, value); }
        #endregion
    }
}
