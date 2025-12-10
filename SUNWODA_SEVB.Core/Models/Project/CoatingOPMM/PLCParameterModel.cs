using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CoatingOPMM
{
    public class PLCParameterModel : ModelBase
    {
        private string? pName;
        private string? pType;
        private string? pValue;

        public string? PName
        {
            get => pName;
            set => SetProperty(ref pName, value);
        }

        public string? PType
        {
            get => pType;
            set => SetProperty(ref pType, value);
        }

        public string? PValue
        {
            get => pValue;
            set => SetProperty(ref pValue, value);
        }
    }
}
