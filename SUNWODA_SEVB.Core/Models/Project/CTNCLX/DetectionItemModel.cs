using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.Project.CTNCLX
{
    public class DetectionItemModel : ModelBase
    {
        private string? name;
        public string? Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }

        private bool isChecked;
        public bool IsChecked
        {
            get => isChecked;
            set => SetProperty(ref isChecked, value);
        }

        private bool isEnabled;
        public bool IsEnabled
        {
            get => isEnabled;
            set => SetProperty(ref isEnabled, value);
        }

        public DetectionItemModel(string itemName)
        {
            Name = itemName;
        }

        public DetectionItemModel(string itemName, bool itemIsEnabled) : this(itemName)
        {
            IsEnabled = itemIsEnabled;
        }

        public DetectionItemModel(string itemName, bool itemIsEnabled, bool itemIsChecked) : this(itemName, itemIsEnabled)
        {
            IsChecked = itemIsChecked;
        }
    }
}
