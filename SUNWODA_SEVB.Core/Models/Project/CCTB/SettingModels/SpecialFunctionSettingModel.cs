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
        private double upperLimitOfTBlock = 1000;
        private double lowerLimitOfTBlock = 1500;

        /// <summary>
        /// T块位置坐标轴刻度设置：刻度上限
        /// </summary>
        public double UpperLimitOfTBlock { get => upperLimitOfTBlock; set => SetProperty(ref upperLimitOfTBlock, value); }

        /// <summary>
        /// T块位置坐标轴刻度设置：刻度下限
        /// </summary>
        public double LowerLimitOfTBlock { get => lowerLimitOfTBlock; set => SetProperty(ref lowerLimitOfTBlock, value); }
        #endregion
    }
}
