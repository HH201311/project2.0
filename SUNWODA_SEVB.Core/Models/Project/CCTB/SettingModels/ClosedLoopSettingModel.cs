using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels
{
    /// <summary>
    /// 闭环参数模型类
    /// </summary>
    public class ClosedLoopSettingModel : ModelBase
    {
        private byte rid_VelocityCondition = 20;
        private float rid_Threshold = 6.0f;
        private byte rid_Count = 2;
        private byte rid_Interval = 3;

        private EBootUpType bootUp_EBootUpType = EBootUpType.DataNumber;
        private byte bootUp_ZeroCount = 3;
        private byte bootUp_BootTime = 60;
        private byte bootUp_RidCount = 3;
        private byte lag_DataCount;
        private byte lag_DearanceValue;

        private byte arithmetic_InputDataCount = 7;
        private PIDModel pid = new();

        private float setB_Coefficient = 1;
        private float setT_Coefficient = 1;

        private bool isEnableFlowTrendMA = false;
        private byte ewma_Count = 1;

        private float judgThreshold_Threshold = 2;
        private byte judgThreshold_Range = 5;
        private byte judgThreshold_Count = 3;

        private byte judgTrend_Count = 6;

        private float judgPosition_Threshold = 1;
        private byte judgPosition_Count = 9;

        private float judgRatio_Threshold = 2.5f;
        private float judgRatio_Ratio = 0.6f;
        private byte judgRatio_Range = 3;
        private byte judgRatio_Count = 2;

        private byte judgDynamic_Value = 99;

        private bool judgFluTrend_IsEnable = false;
        private int judgFluTrend_Count = 5;
        private float judgFluTrend_VariationThreshold = 0.5f;
        private float judgFluTrend_BoundaryThreshold = 0.5f;

        private float judgRange_Threshold = 10f;

        private float judgSpeed_Range = 0.02f;

        private bool isUseDynamicTargetValue = false;
        private float tChange = 50;
        private float tOffset = 0;

        #region 异常数据设置
        /// <summary>
        /// 速度判异-走带速度
        /// </summary>
        [Description("MD/TD/MDTD")]
        public byte Rid_VelocityCondition { get => rid_VelocityCondition; set => SetProperty(ref rid_VelocityCondition, value); }
        /// <summary>
        /// 阈值判异-剔除规格
        /// </summary>
        [Description("MD/TD/MDTD")]
        public float Rid_Threshold { get => rid_Threshold; set => SetProperty(ref rid_Threshold, value); }
        /// <summary>
        /// 阈值判异-判定个数
        /// </summary>
        [Description("MD/TD/MDTD")]
        public byte Rid_Count { get => rid_Count; set => SetProperty(ref rid_Count, value); }
        /// <summary>
        /// 间隔个数
        /// </summary>
        [Description("MD/TD/MDTD")]
        public byte Rid_Interval { get => rid_Interval; set => SetProperty(ref rid_Interval, value); }
        #endregion

        #region 开机判断设置
        /// <summary>
        /// 开机判断 条件
        /// </summary>
        [Description("MD/TD/MDTD")]
        public EBootUpType BootUp_EBootUpType { get => bootUp_EBootUpType; set => SetProperty(ref bootUp_EBootUpType, value); }
        /// <summary>
        /// 开机判定参数-趟数
        /// </summary>
        [Description("MD/TD/MDTD")]
        public byte BootUp_ZeroCount { get => bootUp_ZeroCount; set => SetProperty(ref bootUp_ZeroCount, value); }
        /// <summary>
        /// 开机判定参数-时间s
        /// </summary>
        [Description("MD/TD/MDTD")]
        public byte BootUp_BootTime { get => bootUp_BootTime; set => SetProperty(ref bootUp_BootTime, value); }
        /// <summary>
        /// 开机剔除趟数
        /// </summary>
        [Description("MD/TD/MDTD")]
        public byte BootUp_RidCount { get => bootUp_RidCount; set => SetProperty(ref bootUp_RidCount, value); }

        /// <summary>
        /// 滞后躺数
        /// </summary>
        [Description("MD")]
        public byte Lag_DataCount { get => lag_DataCount; set => SetProperty(ref lag_DataCount, value); }

        /// <summary>
        /// 刀距滞后变化值
        /// </summary>
        [Description("TD/MDTD")]
        public byte Lag_DearanceValue { get => lag_DearanceValue; set => SetProperty(ref lag_DearanceValue, value); }
        #endregion

        #region 算法信息配置
        /// <summary>
        /// 算法信息配置-入参趟数
        /// </summary>
        [Description("MD/TD/MDTD")]
        public byte Arithmetic_InputDataCount { get => arithmetic_InputDataCount; set => SetProperty(ref arithmetic_InputDataCount, value); }

        /// <summary>
        /// 算法信息配置-PID参数
        /// </summary>
        [Description("MD")]
        public PIDModel PID { get => pid; set => SetProperty(ref pid, value); }
        #endregion

        #region 调机下发配置
        /// <summary>
        /// PLC泵速下发系数
        /// </summary>
        [Description("MD/MDTD")]
        public float SetB_Coefficient { get => setB_Coefficient; set => SetProperty(ref setB_Coefficient, value); }
        /// <summary>
        /// T块下发系数
        /// </summary>
        [Description("TD/MDTD")]
        public float SetT_Coefficient { get => setT_Coefficient; set => SetProperty(ref setT_Coefficient, value); }
        #endregion

        #region MA运算设置
        /// <summary>
        /// 流量趋势计算MA使能
        /// </summary>
        [Description("MD")]
        public bool IsEnableFlowTrendMA { get => isEnableFlowTrendMA; set => SetProperty(ref isEnableFlowTrendMA, value); }
        /// <summary>
        /// 取值个数
        /// </summary>
        [Description("MD")]
        public byte EWMA_Count { get => ewma_Count; set => SetProperty(ref ewma_Count, value); }
        #endregion

        #region 阈值判定
        /// <summary>
        /// 调节阈值
        /// </summary>
        [Description("MD")]
        public float JudgThreshold_Threshold { get => judgThreshold_Threshold; set => SetProperty(ref judgThreshold_Threshold, value); }
        /// <summary>
        /// 判定范围
        /// </summary>
        [Description("MD")]
        public byte JudgThreshold_Range { get => judgThreshold_Range; set => SetProperty(ref judgThreshold_Range, value); }
        /// <summary>
        /// 累计个数
        /// </summary>
        [Description("MD")]
        public byte JudgThreshold_Count { get => judgThreshold_Count; set => SetProperty(ref judgThreshold_Count, value); }
        #endregion

        #region 趋势判定
        /// <summary>
        /// 连续个数
        /// </summary>
        [Description("MD")]
        public byte JudgTrend_Count { get => judgTrend_Count; set => SetProperty(ref judgTrend_Count, value); }
        #endregion

        #region 位置判定      
        /// <summary>
        /// 调节阈值
        /// </summary>
        [Description("MD")]
        public float JudgPosition_Threshold { get => judgPosition_Threshold; set => SetProperty(ref judgPosition_Threshold, value); }
        /// <summary>
        /// 连续个数
        /// </summary>
        [Description("MD")]
        public byte JudgPosition_Count { get => judgPosition_Count; set => SetProperty(ref judgPosition_Count, value); }
        #endregion

        #region 比例判定
        /// <summary>
        /// 调节阈值
        /// </summary>
        [Description("MD")]
        public float JudgRatio_Threshold { get => judgRatio_Threshold; set => SetProperty(ref judgRatio_Threshold, value); }
        /// <summary>
        /// 判定值
        /// </summary>
        [Description("MD")]
        public float JudgRatio_Ratio { get => judgRatio_Ratio; set => SetProperty(ref judgRatio_Ratio, value); }
        /// <summary>
        /// 判定范围
        /// </summary>
        [Description("MD")]
        public byte JudgRatio_Range { get => judgRatio_Range; set => SetProperty(ref judgRatio_Range, value); }
        /// <summary>
        /// 累计个数
        /// </summary>
        [Description("MD")]
        public byte JudgRatio_Count { get => judgRatio_Count; set => SetProperty(ref judgRatio_Count, value); }
        #endregion

        #region 动态判定
        /// <summary>
        /// 动态判定
        /// </summary>
        [Description("MD")]
        public byte JudgDynamic_Value { get => judgDynamic_Value; set => SetProperty(ref judgDynamic_Value, value); }
        #endregion

        #region 流量趋势判定
        /// <summary>
        /// 流量趋势判定使能
        /// </summary>
        [Description("MD")]
        public bool JudgFluTrend_IsEnable { get => judgFluTrend_IsEnable; set => SetProperty(ref judgFluTrend_IsEnable, value); }
        /// <summary>
        /// 连续个数
        /// </summary>
        [Description("MD")]
        public int JudgFluTrend_Count { get => judgFluTrend_Count; set => SetProperty(ref judgFluTrend_Count, value); }
        /// <summary>
        /// 流量趋势变化阈值
        /// </summary>
        [Description("MD")]
        public float JudgFluTrend_VariationThreshold { get => judgFluTrend_VariationThreshold; set => SetProperty(ref judgFluTrend_VariationThreshold, value); }
        /// <summary>
        /// 上下流量趋势阈值
        /// </summary>
        [Description("MD")]
        public float JudgFluTrend_BoundaryThreshold { get => judgFluTrend_BoundaryThreshold; set => SetProperty(ref judgFluTrend_BoundaryThreshold, value); }
        #endregion

        #region 极差拦截
        /// <summary>
        /// 极差拦截阈值
        /// </summary>
        [Description("MD")]
        public float JudgRange_Threshold { get => judgRange_Threshold; set => SetProperty(ref judgRange_Threshold, value); }
        #endregion

        #region 泵速拦截
        /// <summary>
        /// 泵速调节区间
        /// </summary>
        [Description("MD")]
        public float JudgSpeed_Range { get => judgSpeed_Range; set => SetProperty(ref judgSpeed_Range, value); }
        #endregion

        #region 目标值设置
        /// <summary>
        /// 是否 使用动态目标值
        /// </summary>
        [Description("MD/TD/MDTD")]
        public bool IsUseDynamicTargetValue { get => isUseDynamicTargetValue; set => SetProperty(ref isUseDynamicTargetValue, value); }
        /// <summary>
        /// 净重目标值设定
        /// </summary>
        [Description("MD/TD/MDTD")]
        public float TChange { get => tChange; set => SetProperty(ref tChange, value); }
        /// <summary>
        /// 目标值偏移量设置
        /// </summary>
        [Description("MD/TD/MDTD")]
        public float TOffset { get => tOffset; set => SetProperty(ref tOffset, value); }
        #endregion
    }
}
