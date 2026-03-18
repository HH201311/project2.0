using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class ClosedLoopSettingModel : ModelBase
    {
        private byte rid_VelocityCondition = 20;
        private double rid_Threshold = 6.0;
        private byte rid_Count = 2;
        private byte rid_Interval = 3;

        private EBootUpType bootUp_EBootUpType = EBootUpType.DataNumber;
        private byte bootUp_ZeroCount = 3;
        private byte bootUp_BootTime = 60;
        private byte bootUp_RidCount = 3;

        private byte arithmetic_InputDataCount = 7;

        private double setB_Coefficient = 1;
        private double setT_Coefficient = 1;

        private bool isUseDynamicTargetValue = false;
        private float tChange = 50;
        private float tOffset = 0;

        #region 异常数据设置
        /// <summary>
        /// 速度判异-走带速度
        /// </summary>
        public byte Rid_VelocityCondition { get => rid_VelocityCondition; set => SetProperty(ref rid_VelocityCondition, value); }
        /// <summary>
        /// 阈值判异-剔除规格
        /// </summary>
        public double Rid_Threshold { get => rid_Threshold; set => SetProperty(ref rid_Threshold, value); }
        /// <summary>
        /// 阈值判异-判定个数
        /// </summary>
        public byte Rid_Count { get => rid_Count; set => SetProperty(ref rid_Count, value); }
        /// <summary>
        /// 间隔个数
        /// </summary>
        public byte Rid_Interval { get => rid_Interval; set => SetProperty(ref rid_Interval, value); }
        #endregion

        #region 开机判断设置
        /// <summary>
        /// 开机判断 条件
        /// </summary>
        public EBootUpType BootUp_EBootUpType { get => bootUp_EBootUpType; set => SetProperty(ref bootUp_EBootUpType, value); }
        /// <summary>
        /// 开机判定参数-趟数
        /// </summary>
        public byte BootUp_ZeroCount { get => bootUp_ZeroCount; set => SetProperty(ref bootUp_ZeroCount, value); }
        /// <summary>
        /// 开机判定参数-时间s
        /// </summary>
        public byte BootUp_BootTime { get => bootUp_BootTime; set => SetProperty(ref bootUp_BootTime, value); }
        /// <summary>
        /// 开机剔除趟数
        /// </summary>
        public byte BootUp_RidCount { get => bootUp_RidCount; set => SetProperty(ref bootUp_RidCount, value); }
        #endregion

        #region 算法信息配置
        /// <summary>
        /// 算法信息配置-入参趟数
        /// </summary>
        public byte Arithmetic_InputDataCount { get => arithmetic_InputDataCount; set => SetProperty(ref arithmetic_InputDataCount, value); }
        #endregion

        #region 调机下发配置
        /// <summary>
        /// PLC泵速下发系数
        /// </summary>
        public double SetB_Coefficient { get => setB_Coefficient; set => SetProperty(ref setB_Coefficient, value); }
        /// <summary>
        /// T块下发系数
        /// </summary>
        public double SetT_Coefficient { get => setT_Coefficient; set => SetProperty(ref setT_Coefficient, value); }
        #endregion

        #region 目标值设置
        /// <summary>
        /// 是否 使用动态目标值
        /// </summary>
        public bool IsUseDynamicTargetValue { get => isUseDynamicTargetValue; set => SetProperty(ref isUseDynamicTargetValue, value); }
        /// <summary>
        /// 净重目标值设定
        /// </summary>
        public float TChange { get => tChange; set => SetProperty(ref tChange, value); }
        /// <summary>
        /// 目标值偏移量设置
        /// </summary>
        public float TOffset { get => tOffset; set => SetProperty(ref tOffset, value); }
        #endregion
    }
}
