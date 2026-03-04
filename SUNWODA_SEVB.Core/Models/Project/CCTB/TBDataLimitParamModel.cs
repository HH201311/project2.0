using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;
using System.ComponentModel;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class DataLimitParamModel : ModelBase
    {
        public DataLimitParamModel() { }

        public DataLimitParamModel(double downDanger, double dowmWarning, double center, double upWarning, double upDanger)
        {
            upDangerLimit = upDanger;
            upWarningLimit = upWarning;
            centerLimit = center;
            downWarningLimit = dowmWarning;
            downDangerLimit = downDanger;
        }

        private double upDangerLimit;
        private double upWarningLimit;
        private double centerLimit;
        private double downWarningLimit;
        private double downDangerLimit;

        public double UpDangerLimit
        {
            get => upDangerLimit;
            set => SetProperty(ref upDangerLimit, value);
        }

        public double UpWarningLimit
        {
            get => upWarningLimit;
            set => SetProperty(ref upWarningLimit, value);
        }

        public double CenterLimit
        {
            get => centerLimit;
            set => SetProperty(ref centerLimit, value);
        }

        public double DownWarningLimit
        {
            get => downWarningLimit;
            set => SetProperty(ref downWarningLimit, value);
        }

        public double DownDangerLimit
        {
            get => downDangerLimit;
            set => SetProperty(ref downDangerLimit, value);
        }
    }
}
