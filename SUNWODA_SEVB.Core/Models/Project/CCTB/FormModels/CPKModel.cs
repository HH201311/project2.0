using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels
{
    public class CPKModel : ModelBase
    {
        private DateTime creationTime;
        private DateTime gatherTime;
        private string? rollName;
        private int passCount;
        private double cpkValue;
        private string? displayName;
        private bool isManual;

        public DateTime CreationTime
        {
            get => creationTime;
            set => SetProperty(ref creationTime, value);
        }

        public DateTime GatherTime
        {
            get => gatherTime;
            set => SetProperty(ref gatherTime, value);
        }

        public string? RollName
        {
            get => rollName;
            set => SetProperty(ref rollName, value);
        }

        public int PassCount
        {
            get => passCount;
            set => SetProperty(ref passCount, value);
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

        public bool IsManual
        {
            get => isManual;
            set => SetProperty(ref isManual, value);
        }
    }
}
