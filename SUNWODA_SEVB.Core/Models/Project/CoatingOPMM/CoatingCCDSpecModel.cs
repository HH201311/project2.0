using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CoatingOPMM
{
    public class CoatingCCDSpecModel
    {
        public double EdgeTabTargetValue { get; set; }
        public double EdgeTabWarnMin { get; set; }
        public double EdgeTabWarnMax { get; set; }
        public double EdgeTabLimitMin { get; set; }
        public double EdgeTabLimitMax { get; set; }

        public double MiddleTabTargetValue { get; set; }
        public double MiddleTabWarnMin { get; set; }
        public double MiddleTabWarnMax { get; set; }
        public double MiddleTabLimitMin { get; set; }
        public double MiddleTabLimitMax { get; set; }

        public double CILTargetValue { get; set; }
        public double CILWarnMin { get; set; }
        public double CILWarnMax { get; set; }
        public double CILLimitMin { get; set; }
        public double CILLimitMax { get; set; }

        public double CoatingTargetValue { get; set; }
        public double CoatingWarnMin { get; set; }
        public double CoatingWarnMax { get; set; }
        public double CoatingLimitMin { get; set; }
        public double CoatingLimitMax { get; set; }

        public double MisalignTargetValue { get; set; }
        public double MisalignWarnMin { get; set; }
        public double MisalignWarnMax { get; set; }
        public double MisalignLimitMin { get; set; }
        public double MisalignLimitMax { get; set; }
    }
}
