using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.BusinessModels
{
    /// <summary>
    /// 计算过程数据模型
    /// </summary>
    public abstract class BaseCalculateModel
    {
        /// <summary>
        /// 闭环计算时间
        /// </summary>
        public DateTime CalculateTime { get; set; }

        #region 采集数据
        /// <summary>
        /// 上次采集时间
        /// </summary>
        public DateTime LastGatherTime { get; set; }

        /// <summary>
        /// 重量数据
        /// </summary>
        public List<float> Weights { get; set; } = [];

        /// <summary>
        /// 扫描类型描述
        /// </summary>
        public string? ScanType { get; set; }

        /// <summary>
        /// 目标设定值-从报文获取
        /// </summary>
        public float DynamicTargetValue { get; set; }

        /// <summary>
        /// 报文标识
        /// </summary>
        public string? MessageTag { get; set; }

        /// <summary>
        /// 机头模头进退刀信号
        /// </summary>
        public float KnifeSwitch { get; set; }

        /// <summary>
        /// PLC走带实时速度
        /// </summary>
        public float Velocity { get; set; }

        /// <summary>
        /// PLC走带设定速度
        /// </summary>
        public float VelocitySetting { get; set; }
        #endregion

        #region 其他配置参数
        /// <summary>
        /// 进退刀检测开关
        /// </summary>
        public bool IsUseJudgeOfKnife { get; set; }
        #endregion

        #region 过程数据
        /// <summary>
        /// 面密度数据均值
        /// </summary>
        public float WeightAvg { get; set; }

        /// <summary>
        /// 目标值
        /// </summary>
        public float TargetValue { get; set; }

        /// <summary>
        /// 是否剔除Y、N
        /// </summary>
        public bool Rid_JudgeResult { get; set; }

        /// <summary>
        /// 剔除原因描述
        /// </summary>
        public string? Rid_JudgeDescription { get; set; }

        /// <summary>
        /// 触发调节N\Y
        /// </summary>
        public bool JudgTrigger { get; set; }
        #endregion

        /// <summary>
        /// 结束信息
        /// </summary>
        public string? EndMessage { get; set; }
    }
}
