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
    public class MDTDCalculateModel : BaseCalculateModel
    {
        #region 采集数据
        /// <summary>
        /// 工艺公差
        /// </summary>
        public float Tolerance { get; set; }

        /// <summary>
        /// T块数据
        /// </summary>
        public TBlockDataModel? TBlockData { get; set; }

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
        /// <summary>
        /// 滞后时间(单位/s)-走带长度/速度
        /// </summary>
        public float Lag_DelayTime { get; set; }

        /// <summary>
        /// DIE头到钢辊间隙设定值(刀距)-左
        /// </summary>
        public float Dearance_Left { get; set; }

        /// <summary>
        /// DIE头到钢辊间隙设定值(刀距)-右
        /// </summary>
        public float Dearance_Right { get; set; }

        #region 拦截规则
        /// <summary>
        /// 判定结果
        /// </summary>
        public string Judg_HandelResult { get; set; } = "N";
        #endregion

        /// <summary>
        /// 泵速T快下发信息：算法输出
        /// </summary>
        public MDTDControlOutputModel? NewBT { get; set; }

        /// <summary>
        /// 泵速T快下发信息：算法输出
        /// </summary>
        public MULTIMDTDControlOutputModel? NewMultiBT { get; set; }
        #endregion
    }
}
