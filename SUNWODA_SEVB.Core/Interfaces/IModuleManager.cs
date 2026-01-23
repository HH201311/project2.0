using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Models;
using System.Windows.Controls;

namespace SUNWODA_SEVB.Core.Interfaces
{
    public interface IModuleManager
    {
        IReadOnlyList<ModuleInfo> Modules { get; }
        IReadOnlyList<ModuleInfo> GetModulesByType(ModuleType type);
        ModuleInfo? GetModule(string moduleName);
        void RegisterModule(ModuleInfo module);
        void AutoRegisterModules();
        void AutoRegisterModulesByName(params string[] moduleNames);
        object? GetViewModel(ModuleInfo? module);
        object? GetViewModel(string moduleName);
        Page? GetView(ModuleInfo? module);
        Page? GetView(string moduleName);
        Page? GetViewFromService(string moduleName);
        void ClearViewModelCache();
        event EventHandler<ModuleRegisteredEventArgs> ModuleRegistered;
    }

    public class ModuleRegisteredEventArgs : EventArgs
    {
        public ModuleInfo? Module { get; set; }
    }
}
