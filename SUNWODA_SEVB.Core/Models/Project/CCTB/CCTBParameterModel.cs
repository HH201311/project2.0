using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class CCTBParameterModel<T> : ModelBase
    {
        private string? pName;
        private T? pValue;

        public string? PName
        {
            get => pName;
            set => SetProperty(ref pName, value);
        }

        public T? PValue
        {
            get => pValue;
            set => SetProperty(ref pValue, value);
        }

        public CCTBParameterModel(string pName, T? pValue)
        {
            PName = pName;
            PValue = pValue;
        }
    }
}
