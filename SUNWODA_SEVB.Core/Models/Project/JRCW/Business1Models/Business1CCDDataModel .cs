using SUNWODA_SEVB.Core.Attributes.Project.JRCW;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.Business1Models
{
	/// <summary>
	/// 卷绕极耳错位闭环的CCD采集数据模型
	/// </summary>
	public class Business1CCDDataModel
    {
        /// <summary>
        /// CCD采集时间
        /// </summary>
        [DataColumn("CCD采集时间")]
        public DateTime GatherTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 扫码处虚拟码
        /// </summary>
        [DataColumn("扫码处虚拟码")]
        public string? CCD_ScanCode { get; set; }

        /// <summary>
        /// Barcode
        /// </summary>
        [DataColumn("Barcode")]
        public string? CCD_Barcode { get; set; }

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
        /// 正极极耳远边距
        /// </summary>
        [DataColumn("正极极耳远边距")]
        public float FarDistanceOfPositiveBatteryTab { get; set; }

        /// <summary>
        /// 正极极耳近边距
        /// </summary>
        [DataColumn("正极极耳近边距")]
        public float NearDistanceOfPositiveBatteryTab { get; set; }

        /// <summary>
        /// 负极极耳远边距
        /// </summary>
        [DataColumn("负极极耳远边距")]
        public float FarDistanceOfNegativeBatteryTab { get; set; }

        /// <summary>
        /// 负极极耳近边距
        /// </summary>
        [DataColumn("负极极耳近边距")]
        public float NearDistanceOfNegativeBatteryTab { get; set; }
	}
}
