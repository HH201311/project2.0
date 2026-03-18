using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels
{
    public class CPKByRollModel : ModelBase
    {
        private DateTime creationTime;
        private string? rollName;
        private double cpkValue;
        private string? displayName;

        public DateTime CreationTime
        {
            get => creationTime;
            set => SetProperty(ref creationTime, value);
        }

        public string? RollName
        {
            get => rollName;
            set => SetProperty(ref rollName, value);
        }

        public double CPKValue
        {
            get => cpkValue;
            set => SetProperty(ref cpkValue, value);
        }

        public string? DisplayName
        {
            get => displayName;
            set => SetProperty(ref displayName, value);
        }
    }
}
