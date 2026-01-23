using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CoatingOPMM
{
    public class JudgeResult : ModelBase
    {
        private string? zone;
        private string? result;

        public string? Zone
        {
            get => zone;
            set => SetProperty(ref zone, value);
        }

        public string? Result
        {
            get => result;
            set => SetProperty(ref result, value);
        }
    }
}
