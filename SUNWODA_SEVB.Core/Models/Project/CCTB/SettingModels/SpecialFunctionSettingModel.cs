using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels
{
    /// <summary>
    /// 特殊功能参数模型类
    /// </summary>
    public class SpecialFunctionSettingModel : ModelBase
    {
        #region 指标计算:IndexCalculation
        private EWeightDataType formChartDataType = EWeightDataType.干膜净重分区数据;
        private EWeightDataType formLineDataType = EWeightDataType.干膜总重毫米数据;
        private double eliminateSpecificationsOfCPK = 10;

        /// <summary>
        /// 展示界面分区数据及CPK计算数据选择
        /// 备注：面密度界面柱状图&表格数据展示类型
        /// </summary>
        public EWeightDataType FormChartDataType { get => formChartDataType; set => SetProperty(ref formChartDataType, value); }

        /// <summary>
        /// 展示界面毫米数据选择
        /// 备注：面密度界面折线展示类型
        /// </summary>
        public EWeightDataType FormLineDataType { get => formLineDataType; set => SetProperty(ref formLineDataType, value); }

        /// <summary>
        /// CPK计算数据处理：异常数据剔除规格
        /// </summary>
        public double EliminateSpecificationsOfCPK { get => eliminateSpecificationsOfCPK; set => SetProperty(ref eliminateSpecificationsOfCPK, value); }
        #endregion

        #region 界面设置:InterfaceSettings
        #region 面密度数据图表参数
        private float upperLimitOfCoatingA = 380, lowerLimitOfCoatingA = 372, upperLimitOfCoatingB = 756, lowerLimitOfCoatingB = 748;
        private float targetValueOfCoatingA = 376, warningOffsetOfCoatingA = 2, dangerOffsetOfCoatingA = 4;
        private float targetValueOfCoatingB = 752, warningOffsetOfCoatingB = 2, dangerOffsetOfCoatingB = 4;

        /// <summary>
        /// A面刻度上限
        /// </summary>
        public float UpperLimitOfCoatingA { get => upperLimitOfCoatingA; set => SetProperty(ref upperLimitOfCoatingA, value); }

        /// <summary>
        /// A面刻度下限
        /// </summary>
        public float LowerLimitOfCoatingA { get => lowerLimitOfCoatingA; set => SetProperty(ref lowerLimitOfCoatingA, value); }

        /// <summary>
        /// B面刻度上限
        /// </summary>
        public float UpperLimitOfCoatingB { get => upperLimitOfCoatingB; set => SetProperty(ref upperLimitOfCoatingB, value); }

        /// <summary>
        /// B面刻度下限
        /// </summary>
        public float LowerLimitOfCoatingB { get => lowerLimitOfCoatingB; set => SetProperty(ref lowerLimitOfCoatingB, value); }

        /// <summary>
        /// A面目标值
        /// </summary>
        public float TargetValueOfCoatingA { get => targetValueOfCoatingA; set => SetProperty(ref targetValueOfCoatingA, value); }

        /// <summary>
        /// A面预警偏差值
        /// </summary>
        public float WarningOffsetOfCoatingA { get => warningOffsetOfCoatingA; set => SetProperty(ref warningOffsetOfCoatingA, value); }

        /// <summary>
        /// A面危险偏差值
        /// </summary>
        public float DangerOffsetOfCoatingA { get => dangerOffsetOfCoatingA; set => SetProperty(ref dangerOffsetOfCoatingA, value); }

        /// <summary>
        /// B面目标值
        /// </summary>
        public float TargetValueOfCoatingB { get => targetValueOfCoatingB; set => SetProperty(ref targetValueOfCoatingB, value); }

        /// <summary>
        /// B面预警偏差值
        /// </summary>
        public float WarningOffsetOfCoatingB { get => warningOffsetOfCoatingB; set => SetProperty(ref warningOffsetOfCoatingB, value); }

        /// <summary>
        /// B面危险偏差值
        /// </summary>
        public float DangerOffsetOfCoatingB { get => dangerOffsetOfCoatingB; set => SetProperty(ref dangerOffsetOfCoatingB, value); }
        #endregion

        #region T块数据图表参数
        private uint intervalOfTBlock = 200;
        private float upperLimitOfTBlock = -300, lowerLimitOfTBlock = 700, centerValueOfTBlock = 0;

        /// <summary>
        /// T块位置坐标轴刻度设置：刻度间隔
        /// </summary>
        public uint IntervalOfTBlock { get => intervalOfTBlock; set => SetProperty(ref intervalOfTBlock, value); }

        /// <summary>
        /// T块位置坐标轴刻度设置：刻度上限
        /// </summary>
        public float UpperLimitOfTBlock { get => upperLimitOfTBlock; set => SetProperty(ref upperLimitOfTBlock, value); }

        /// <summary>
        /// T块位置坐标轴刻度设置：刻度下限
        /// </summary>
        public float LowerLimitOfTBlock { get => lowerLimitOfTBlock; set => SetProperty(ref lowerLimitOfTBlock, value); }

        /// <summary>
        /// T块位置坐标轴刻度设置：刻度中线
        /// </summary>
        public float CenterValueOfTBlock { get => centerValueOfTBlock; set => SetProperty(ref centerValueOfTBlock, value); }
        #endregion
        #endregion
    }
}
