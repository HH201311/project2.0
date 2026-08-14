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
        public DateTime GatherTime { get; set; }

        /// <summary>
        /// 数采触发极耳层数
        /// </summary>
        [DataColumn("数采触发极耳层数")]
        public float TabCount { get; set; }

        /// <summary>
        /// 卷针处虚拟码
        /// </summary>
        [DataColumn("卷针处虚拟码")]
        public string? ZJData_XNM { get; set; }

        /// <summary>
        /// 卷针号
        /// </summary>
        [DataColumn("卷针号")]
        public float ZJData_JZH { get; set; }

        /// <summary>
        /// 正极错位量
        /// </summary>
        [DataColumn("正极错位量")]
        public float MVOfP { get; set; }

        /// <summary>
        /// 负极错位量
        /// </summary>
        [DataColumn("负极错位量")]
        public float MVOfN { get; set; }

        /// <summary>
        /// 正极极耳角度
        /// </summary>
        [DataColumn("极耳角度", ColumnNameFormat = "正极第{0}个{1}")]
        public List<float> AngleOfPBTab { get; set; } = new();

        /// <summary>
        /// 负极极耳角度
        /// </summary>
        [DataColumn("极耳角度", ColumnNameFormat = "负极第{0}个{1}")]
        public List<float> AngleOfNBTab { get; set; } = new();

        /// <summary>
        /// 入料位置-正极
        /// </summary>
        [DataColumn("入料位置-正极")]
        public float InPOfP { get; set; }

        /// <summary>
        /// 入料位置-负极
        /// </summary>
        [DataColumn("入料位置-负极")]
        public float InPOfN { get; set; }

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
        /// 卷针3变径位置
        /// </summary>
        [DataColumn("卷针3变径位置")]
        public float SV_Pos3 { get; set; }

        /// <summary>
        /// 实时压花气压
        /// </summary>
        [DataColumn("实时压花气压")]
        public float StampingAirPressure { get; set; }
        #endregion
    }
}
