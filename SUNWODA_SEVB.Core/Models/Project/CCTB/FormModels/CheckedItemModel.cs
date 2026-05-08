using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels
{
    /// <summary>
    /// Represents an item with a checked state and display name for use in selection controls.
    /// </summary>
    public class CheckedItemModel<T> : ModelBase
    {
        private T? item;
        private bool isChecked;
        private string? displayName;

        public T? Item
        {
            get => item;
            set => SetProperty(ref item, value);
        }

        public bool IsChecked
        {
            get => isChecked;
            set => SetProperty(ref isChecked, value);
        }

        public string? DisplayName
        {
            get => displayName;
            set => SetProperty(ref displayName, value);
        }
    }
}
