using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;
using System.ComponentModel;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class CCTBCPKModel : ModelBase
    {
        private DateTime creationTime;
        private double? cpkValue;
        private string? cpkLabel;
        private bool? isManual;

        public DateTime CreationTime
        {
            get => creationTime;
            set => SetProperty(ref creationTime, value);
        }

        public double? CPKValue
        {
            get => cpkValue;
            set => SetProperty(ref cpkValue, value);
        }

        public string? CPKLabel
        {
            get => cpkLabel;
            set => SetProperty(ref cpkLabel, value);
        }

        public bool? IsManual
        {
            get => isManual;
            set => SetProperty(ref isManual, value);
        }
    }
}
