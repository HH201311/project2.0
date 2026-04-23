using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations;

namespace SUNWODA_SEVB.Core.Models
{
    public class ModuleInfo : ModelBase
    {
        private string? _name;
        private bool _isSelected;

        public string? Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public string? DisplayName { get; set; }
        public string? Icon { get; set; }
        public int Order { get; set; }
        public string? Category { get; set; }
        public Type? ViewType { get; set; }
        public Type? ViewModelType { get; set; }
        public ModuleType Type { get; set; }
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        public string[]? RequiredPermissions { get; set; }
        public List<ModuleInfo> SubModules { get; set; } = new List<ModuleInfo>();
    }
}
