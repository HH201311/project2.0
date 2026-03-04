using CsvHelper.Configuration.Attributes;
using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class GeneralSettingsModel : ModelBase
    {
        private EWeightDataType weightDataType;
        private EMembraneZoneType membraneZoneType;
        private byte lag_DearanceConditiont;

        private byte setB_Digits;
        private bool setB_Round;


        private EPartitionedType partitionedType;

        public EWeightDataType WeightDataType { get => weightDataType; set => SetProperty(ref weightDataType, value); }
        public EMembraneZoneType MembraneZoneType { get => membraneZoneType; set => SetProperty(ref membraneZoneType, value); }



        public EPartitionedType PartitionedType { get => partitionedType; set => SetProperty(ref partitionedType, value); }

    }
}
