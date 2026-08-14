using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels;
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
    public class MDCalculateModel : BaseCalculateModel
    {
        #region 采集数据
        /// <summary>
        /// 工艺公差
        /// </summary>
        public float Tolerance { get; set; }

        /// <summary>
        /// PLC设定泵速
        /// </summary>
        public Dictionary<byte, (float speed, float flag)> PumpSetValues { get; set; } = [];

        /// <summary>
        /// 流量数据缓存
        /// 注：键为泵速编号，从1开始
        /// </summary>
        public Dictionary<byte, List<float>> FluDatasCache { get; set; } = [];

        /// <summary>
        /// 非0刘爱玲数据均值
        /// 注：键为泵速编号，从1开始
        /// </summary>
        public Dictionary<byte, float> FluDataAvgsCache
        {
            get => FluDatasCache.ToDictionary(k => k.Key, v => v.Value.Where(o => o != 0).Average());
        }

        /// <summary>
        /// 泵1流量非0数据趋势值
        /// </summary>
        public float FluDataDeltaPump1 { get; set; }

        /// <summary>
        /// 泵2流量非0数据趋势值
        /// </summary>
        public float FluDataDeltaPump2 { get; set; }

        /// <summary>
        /// 闭环开关信号
        /// </summary>
        public float CLSwitchSignal { get; set; }

        /// <summary>
        /// 闭环开关信号：供应商
        /// </summary>
        public float CLSwitchSignal_Supplier { get; set; }
        #endregion

        /// <summary>
        /// B面获取A面数据限制时间/s
        /// </summary>
        public byte LimitTimeB { get; set; }

        #region 过程数据
        #region EWMA运算配置
        /// <summary>
        /// 数据最大值
        /// </summary>
        public float WeightMax { get; set; }

        /// <summary>
        /// 数据最小值
        /// </summary>
        public float WeightMin { get; set; }

        /// <summary>
        /// 数据极差
        /// </summary>
        public float WeightRange { get; set; }

        /// <summary>
        /// EA均均值-重量数据
        /// </summary>
        public float EWMA_Avg_WeightData { get; set; }

        /// <summary>
        /// 流量和面密度数据均值
        /// </summary>
        public float FluAndWeightValue { get; set; }

        /// <summary>
        /// EA均均值-流量数据
        /// </summary>
        public float EWMA_Avg_FluAndWeightValue { get; set; }

        /// <summary>
        /// A面调机值均值
        /// </summary>
        public float BAdjustAvgValue_A { get; set; }
        #endregion

        #region 判定规则
        /// <summary>
        /// 是否超出阈值 N\Y
        /// </summary>
        public bool JudgThreshold_Result { get; set; }
        #endregion

        #region 趋势判断
        /// <summary>
        /// 趋势判断 上增/下减
        /// </summary>
        public string? JudgTrend_Result { get; set; }
        #endregion

        #region 位置判断
        /// <summary>
        /// 判断规格线 上\中\下
        /// </summary>
        public string? JudgPosition_Position { get; set; }
        #endregion

        #region 比例判断       
        /// <summary>
        /// 超过比例阈值的值的数量
        /// </summary>
        public int JudgRatio__UpThresholdCount { get; set; } = 0;

        /// <summary>
        /// 低于比例阈值的值的数量
        /// </summary>
        public int JudgRatio__DownThresholdCount { get; set; } = 0;

        /// <summary>
        /// 是否超过比例过程判定值  Y/N
        /// </summary>
        public bool JudgRatio__Result { get; set; }
        #endregion

        #region 动态判定
        /// <summary>
        /// 动态判定结果
        /// </summary>
        public byte JudgDynamic_Result { get; set; }
        #endregion

        #region 流量趋势判断
        /// <summary>
        /// 流量趋势判断 流量和趋势判定/上层流量趋势判定/下层流量趋势判定
        /// </summary>
        public string? JudgFluTrend_Result { get; set; }
        #endregion

        /// <summary>
        /// 数据处理逻辑 SPC\阈值
        /// </summary>
        public string Judg_HandelType { get; set; } = "N";

        #region 拦截规则
        /// <summary>
        /// 判定结果
        /// </summary>
        public string Judg_HandelResult { get; set; } = "N";
        #endregion

        /// <summary>
        /// 泵速微调值
        /// </summary>
        public ExpertPIDOutputModel? NewBOfExpertPID { get; set; }

        /// <summary>
        /// 实际调机值
        /// </summary>
        public FlowPIDOutputModel? NewBOfFlow { get; set; }
        #endregion
    }
}
