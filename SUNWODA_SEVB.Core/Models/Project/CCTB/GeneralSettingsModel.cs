using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class GeneralSettingsModel : ModelBase
    {
        private EWeightDataType weightDataType;
        private EMembraneZoneType membraneZoneType;
        private byte lag_DearanceConditiont;

        private byte setB_Digits;
        private bool setB_IsRound;

        private byte velocityCondition;
        private float targetOffset;

        private byte distanceBetweenAB;

        private EPartitionedType partitionedType;
        private List<PartitionedModel> partitionedModels = new();

        #region 数据处理设置
        /// <summary>
        /// 闭环数据源
        /// </summary>
        public EWeightDataType WeightDataType { get => weightDataType; set => SetProperty(ref weightDataType, value); }

        /// <summary>
        /// 膜区处理
        /// </summary>
        public EMembraneZoneType MembraneZoneType { get => membraneZoneType; set => SetProperty(ref membraneZoneType, value); }

        /// <summary>
        /// 刀距之后变化值
        /// </summary>
        public byte Lag_DearanceConditiont { get => lag_DearanceConditiont; set => SetProperty(ref lag_DearanceConditiont, value); }
        #endregion

        #region PLC设定泵速读取设置
        /// <summary>
        /// 小数位/精度
        /// </summary>
        public byte SetB_Digits { get => setB_Digits; set => SetProperty(ref setB_Digits, value); }

        /// <summary>
        /// 四舍五入
        /// </summary>
        public bool SetB_IsRound { get => setB_IsRound; set => SetProperty(ref setB_IsRound, value); }
        #endregion

        #region 数据判异/数采设置
        /// <summary>
        /// 走带速度
        /// </summary>
        public byte VelocityCondition { get => velocityCondition; set => SetProperty(ref velocityCondition, value); }

        /// <summary>
        /// 面密度目标值偏移量
        /// </summary>
        public float TargetOffset { get => targetOffset; set => SetProperty(ref targetOffset, value); }
        #endregion

        #region 设备参数设置       
        /// <summary>
        /// AB面走嗲间隔距离
        /// </summary>
        public byte DistanceBetweenAB { get => distanceBetweenAB; set => SetProperty(ref distanceBetweenAB, value); }
        #endregion

        #region 膜区设置
        /// <summary>
        /// 膜区设置
        /// </summary>
        public EPartitionedType PartitionedType { get => partitionedType; set => SetProperty(ref partitionedType, value); }

        /// <summary>
        /// 膜区配置
        /// </summary>
        public List<PartitionedModel> PartitionedModels { get => partitionedModels; set => SetProperty(ref partitionedModels, value); }
        #endregion
    }
}
