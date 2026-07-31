using SUNWODA_SEVB.Core.Attributes.Project.JRCW;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.Business2Models
{
    /// <summary>
    /// 卷绕极耳错位闭环的PLC采集数据模型
    /// </summary>
    public class Business2PLCDataModel
    {
        #region 采集参数
        /// <summary>
        /// PLC采集时间
        /// </summary>
        [DataColumn("PLC采集时间")]
        public DateTime GatherTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 卷针处虚拟码
        /// </summary>
        [DataColumn("卷针处虚拟码")]
        public string? ZJData_XNM { get; set; }

        /// <summary>
        /// 卷针号
        /// </summary>
        [DataColumn("卷针号")]
        public short ZJData_JZH { get; set; }

        /// <summary>
        /// 正极错位量
        /// </summary>
        [DataColumn("正极错位量")]
        public float MalpositionValueOfPositive { get; set; }

        /// <summary>
        /// 负极错位量
        /// </summary>
        [DataColumn("负极错位量")]
        public float MalpositionValueOfNegative { get; set; }

        /// <summary>
        /// 正极极耳角度
        /// </summary>
        [DataColumn("极耳角度", ColumnNameFormat = "正极第{0}个{1}")]
        public List<float> AngleOfPositiveBatteryTab { get; set; } = new();

        /// <summary>
        /// 负极极耳角度
        /// </summary>
        [DataColumn("极耳角度", ColumnNameFormat = "负极第{0}个{1}")]
        public List<float> AngleOfNegativeBatteryTab { get; set; } = new();

        /// <summary>
        /// 入料位置-正极
        /// </summary>
        [DataColumn("入料位置-正极")]
        public float FeedInletPositionOfPositive { get; set; }

        /// <summary>
        /// 入料位置-负极
        /// </summary>
        [DataColumn("入料位置-负极")]
        public float FeedInletPositionOfNegative { get; set; }

        /// <summary>
        /// 卷针1变径位置
        /// </summary>
        [DataColumn("卷针1变径位置")]
        public float SV_Pos1 { get; set; }

        /// <summary>
        /// 卷针2变径位置
        /// </summary>
        [DataColumn("卷针2变径位置")]
        public float SV_Pos2 { get; set; }

        /// <summary>
        /// 实时压花气压
        /// </summary>
        [DataColumn("实时压花气压")]
        public float StampingAirPressure { get; set; }
        #endregion

        #region 配置参数(目的:保存在csv中)
        /// <summary>
        /// 闭环阈值t1
        /// </summary>
        [DataColumn("t1")]
        public int T1 { get; set; }

        /// <summary>
        /// 闭环阈值t2
        /// </summary>
        [DataColumn("t2")]
        public int T2 { get; set; }

        /// <summary>
        /// 闭环阈值t3
        /// </summary>
        [DataColumn("t3")]
        public int T3 { get; set; }

        /// <summary>
        /// 闭环阈值t4
        /// </summary>
        [DataColumn("t4")]
        public int T4 { get; set; }

        /// <summary>
        /// 调节系数K值
        /// </summary>
        [DataColumn("k")]
        public double K { get; set; }
        #endregion
    }
}
