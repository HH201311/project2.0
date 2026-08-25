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
        public DateTime CalculateTime { get; set; }

        #region 接带/换卷处理
        /// <summary>
        /// 卷针号
        /// </summary>
        [DataColumn("闭环开启信号")]
        public float CLEnableSigle { get; set; }

        /// <summary>
        /// 接带信号
        /// </summary>
        [DataColumn("接带信号")]
        public float Host_TapeSplicing { get; set; }

        /// <summary>
        /// 换卷信号
        /// </summary>
        [DataColumn("换卷信号")]
        public float Host_ReelAlternatioan { get; set; }

        /// <summary>
        /// 算法参数change
        /// </summary>
        [DataColumn("算法参数change")]
        public int ChangeOfArithmetic { get; set; }

        /// <summary>
        /// 推荐变径调整量
        /// </summary>
        [DataColumn("推荐变径调整量")]
        public float M { get; set; }
        #endregion

        #region 错位量计算数据
        /// <summary>
        /// 实时电芯的首极耳角度-正极
        /// </summary>
        [DataColumn("实时电芯的首极耳角度-正极")]
        public float Θb1 { get; set; }

        /// <summary>
        /// 实时电芯的尾极耳角度-正极
        /// </summary>
        [DataColumn("实时电芯的尾极耳角度-正极")]
        public float Θbn { get; set; }

        /// <summary>
        /// 实时电芯的首极耳角度-负极
        /// </summary>
        [DataColumn("实时电芯的首极耳角度-负极")]
        public float Θa1 { get; set; }

        /// <summary>
        /// 实时电芯的尾极耳角度-负极
        /// </summary>
        [DataColumn("实时电芯的尾极耳角度-负极")]
        public float Θan { get; set; }

        /// <summary>
        /// 稳定极耳错位-正极
        /// </summary>
        [DataColumn("稳定极耳错位-正极")]
        public float S_ΔDb { get; set; }

        /// <summary>
        /// 稳定极耳错位-负极
        /// </summary>
        [DataColumn("稳定极耳错位-负极")]
        public float S_ΔDa { get; set; }

        /// <summary>
        /// 缺失错位补偿-正极
        /// </summary>
        [DataColumn("缺失错位补偿-正极")]
        public float Bb { get; set; }

        /// <summary>
        /// 缺失错位补偿-负极
        /// </summary>
        [DataColumn("缺失错位补偿-负极")]
        public float Ba { get; set; }

        /// <summary>
        /// 正极错位量
        /// </summary>
        [DataColumn("正极错位量")]
        public float ΔDb { get; set; }

        /// <summary>
        /// 负极错位量
        /// </summary>
        [DataColumn("负极错位量")]
        public float ΔDa { get; set; }
        #endregion

        #region 闭环算法
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
        /// J3
        /// </summary>
        [DataColumn("J3")]
        public float J3 { get; set; }

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
