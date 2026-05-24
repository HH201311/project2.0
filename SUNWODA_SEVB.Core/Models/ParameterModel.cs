using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models
{
    public class ParameterModel : ModelBase
    {
        private string? pName;
        private string? pValue;

        public string? PName
        {
            get => pName;
            set => SetProperty(ref pName, value);
        }

        public string? PValue
        {
            get => pValue;
            set
            {
                if (pValue != value) SetProperty(ref pValue, value);
            }
        }

        public ParameterModel(string pName, string pValue)
        {
            PName = pName;
            PValue = pValue;
        }
    }
}
