using CsvHelper.Configuration.Attributes;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.BusinessModels
{
    /// <summary>
    /// 采集的重量数据
    /// </summary>
    public class CoatingDataModel
    {
        /// <summary>
        /// 采集时间
        /// </summary>
        [Name("采集时间")]
        public DateTime GatherTime { get; set; }

        /// <summary>
        /// AB面
        /// </summary>
        [Name("AB面")]
        public ESideType SideType { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        [Name("数据类型")]
        public EWeightDataType WeightDataType { get; set; }

        /// <summary>
        /// 基材值
        /// </summary>
        [Name("基材值")]
        public float SubstrateValue { get; set; }

        /// <summary>
        /// A面目标值
        /// </summary>
        [Name("A面目标值")]
        public float TargetValueA { get; set; }

        /// <summary>
        /// B面目标值
        /// </summary>
        [Name("B面目标值")]
        public float TargetValueB { get; set; }

        /// <summary>
        /// A面公差值
        /// </summary>
        [Name("A面公差值")]
        public float ToleranceValueA { get; set; }

        /// <summary>
        /// B面公差值
        /// </summary>
        [Name("B面公差值")]
        public float ToleranceValueB { get; set; }

        /// <summary>
        /// 趟号
        /// </summary>
        [Name("趟号")]
        public float PassCount { get; set; }

        /// <summary>
        /// 扫描类型
        /// </summary>
        [Name("扫描类型")]
        public float ScanType { get; set; }

        /// <summary>
        /// 重量数据
        /// </summary>
        [Name("重量数据")]
        public List<float> Weights { get; set; } = new List<float>();
    }
}
