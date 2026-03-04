using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels
{
    public class ParamOptionModel : ModelBase
    {
        private EParamOption paramOption;
        private bool isSelected;
        private string? displayName;

        public EParamOption ParamOption
        {
            get => paramOption;
            set => SetProperty(ref paramOption, value);
        }

        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        public string? DisplayName
        {
            get => displayName;
            set => SetProperty(ref displayName, value);
        }
    }
}
