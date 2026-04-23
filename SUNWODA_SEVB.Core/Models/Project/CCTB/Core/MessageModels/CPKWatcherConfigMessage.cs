using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels
{
    public class CPKWatcherConfigMessage
    {
        public string? ProductionDataPath { get; set; }
        public EDataSourceType DataSourceType { get; set; }
        public EWeightDataType WeightDataType { get; set; }
    }
}
