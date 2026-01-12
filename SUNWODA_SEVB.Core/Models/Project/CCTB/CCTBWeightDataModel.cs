using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class CCTBWeightDataModel
    {
        [Name("采集时间")]
        public DateTime GatherTime { get; set; }
        [Name("AB面")]
        public ESideType SideType { get; set; }
        [Name("数据类型")]
        public EWeightDataType WeightDataType { get; set; }
        [Name("基材值")]
        public double SubstrateValue { get; set; }
        [Name("A面目标值")]
        public double TargetValueA { get; set; }
        [Name("B面目标值")]
        public double TargetValueB { get; set; }
        [Name("A面公差值")]
        public double ToleranceValueA { get; set; }
        [Name("B面公差值")]
        public double ToleranceValueB { get; set; }
        [Name("趟号")]
        public int PassCount { get; set; }
        [Name("扫描类型")]
        public int ScanType { get; set; }

        [Name("重量数据")]
        public List<double> Weights { get; set; } = new List<double>();

        [Description("真实的目标值A")]
        [Ignore]
        public double ActualTargetValueA
        {
            get
            {
                return WeightDataType switch
                {
                    EWeightDataType.干膜净重分区数据 => TargetValueA,
                    EWeightDataType.干膜净重毫米数据 => TargetValueA,
                    EWeightDataType.湿膜净重分区数据 => TargetValueA,
                    EWeightDataType.湿膜净重毫米数据 => TargetValueA,
                    EWeightDataType.干膜总重分区数据 => TargetValueA + SubstrateValue,
                    EWeightDataType.干膜总重毫米数据 => TargetValueA + SubstrateValue,
                    EWeightDataType.湿膜总重分区数据 => TargetValueA + SubstrateValue,
                    EWeightDataType.湿膜总重毫米数据 => TargetValueA + SubstrateValue,
                    _ => throw new NotImplementedException(($"不支持的数据类型: {WeightDataType}"))
                };
            }
        }

        [Description("真实的目标值B")]
        [Ignore]
        public double ActualTargetValueB
        {
            get
            {
                return WeightDataType switch
                {
                    EWeightDataType.干膜净重分区数据 => TargetValueB,
                    EWeightDataType.干膜净重毫米数据 => TargetValueB,
                    EWeightDataType.湿膜净重分区数据 => TargetValueB,
                    EWeightDataType.湿膜净重毫米数据 => TargetValueB,
                    EWeightDataType.干膜总重分区数据 => TargetValueB + SubstrateValue,
                    EWeightDataType.干膜总重毫米数据 => TargetValueB + SubstrateValue,
                    EWeightDataType.湿膜总重分区数据 => TargetValueB + SubstrateValue,
                    EWeightDataType.湿膜总重毫米数据 => TargetValueB + SubstrateValue,
                    _ => throw new NotImplementedException(($"不支持的数据类型: {WeightDataType}"))
                };
            }
        }
    }
}
