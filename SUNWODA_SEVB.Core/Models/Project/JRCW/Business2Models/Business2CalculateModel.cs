using SUNWODA_SEVB.Core.Attributes.Project.JRCW;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.Business2Models
{
    /// <summary>
    /// 卷绕极耳错位闭环计算过程模型
    /// </summary>
    public class Business2CalculateModel
    {
        /// <summary>
        /// 闭环计算时间
        /// </summary>
        [DataColumn("闭环计算时间")]
        public DateTime CalculateTime { get; set; } = DateTime.Now;

        #region PLC采集数据
        /// <summary>
        /// PLC采集数据条码
        /// </summary>
        [DataColumn("PLC采集数据条码")]
        public string? PLCBorcode { get; set; }
        #endregion

        #region 接带/换卷处理
        /// <summary>
        /// 卷针号
        /// </summary>
        [DataColumn("卷针号")]
        public short JZH { get; set; }

        /// <summary>
        /// 接带信号
        /// </summary>
        [DataColumn("接带信号")]
        public short Host_TapeSplicing { get; set; }

        /// <summary>
        /// 换卷信号
        /// </summary>
        [DataColumn("换卷信号")]
        public short Host_ReelAlternatioan { get; set; }

        /// <summary>
        /// 算法参数change
        /// </summary>
        [DataColumn("算法参数change")]
        public int ChangeOfArithmetic { get; set; }

        /// <summary>
        /// 推荐变径调整量
        /// </summary>
        [DataColumn("推荐变径调整量")]
        public double M { get; set; }
        #endregion

        #region 闭环算法
        /// <summary>
        /// J1缓存值
        /// </summary>
        [DataColumn("J1缓存值")]
        public float J1Cache { get; set; }

        /// <summary>
        /// J2缓存值
        /// </summary>
        [DataColumn("J2缓存值")]
        public float J2Cache { get; set; }

        /// <summary>
        /// JSingle缓存值
        /// </summary>
        [DataColumn("JSingle缓存值")]
        public int JSingleCache { get; set; }

        /// <summary>
        /// OverJNum缓存值
        /// </summary>
        [DataColumn("OverJNum缓存值")]
        public int OverJNumCache { get; set; }

        /// <summary>
        /// J1
        /// </summary>
        [DataColumn("J1")]
        public float J1 { get; set; }

        /// <summary>
        /// J2
        /// </summary>
        [DataColumn("J2")]
        public float J2 { get; set; }

        /// <summary>
        /// PressJ
        /// </summary>
        [DataColumn("PressJ")]
        public float PressJ { get; set; }

        /// <summary>
        /// Notes
        /// </summary>
        [DataColumn("Notes")]
        public string? Notes { get; set; }

        /// <summary>
        /// Dialog
        /// </summary>
        [DataColumn("Dialog")]
        public string? Dialog { get; set; }

        /// <summary>
        /// JSingle
        /// </summary>
        [DataColumn("JSingle")]
        public int JSingle { get; set; }

        /// <summary>
        /// OverJNum
        /// </summary>
        [DataColumn("OverJNum")]
        public int OverJNum { get; set; }
        #endregion

        /// <summary>
        /// 闭环结果信息
        /// </summary>
        [DataColumn("闭环结果信息")]
        public string? EndMessage { get; set; }
    }
}
