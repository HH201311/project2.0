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
    public class TDCalculateModel : BaseCalculateModel
    {
        #region 采集数据
        /// <summary>
        /// T块数据
        /// </summary>
        public TBlockDataModel? TBlockData { get; set; }

        /// <summary>
        /// 闭环开关信号
        /// </summary>
        public float CLSwitchSignal { get; set; }

        /// <summary>
        /// 闭环开关信号：供应商
        /// </summary>
        public float CLSwitchSignal_Supplier { get; set; }
        #endregion

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
        /// 新T块调机值
        /// </summary>
        public TDControlOutputModel? NewT { get; set; }
        #endregion
    }
}
