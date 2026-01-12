using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;
using System.Collections.ObjectModel;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class CCTBParametersModel<T> : ModelBase
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
            set
            {
                if (PValues == null) PValues = new ObservableCollection<T?>();
                PValues.Add(value);
                if (PValues.Count > 30) PValues.RemoveAt(0);

                SetProperty(ref pValue, value);
            }
        }

        public ObservableCollection<T?>? PValues { get; set; } = [];

        public CCTBParametersModel(string pName, T? pValue)
        {
            PName = pName;
            PValue = pValue;
        }
    }
}
