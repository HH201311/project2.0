using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;

namespace SUNWODA_SEVB.ViewModels.Pages.CoatingOPMMRemote
{
    [Module("CoatingOPMMRemote", "涂布工序远程监测系统")]
    public class VM_CoatingOPMMRemotePage : ViewModelBase
    {
        private readonly ILoggerService<VM_CoatingOPMMRemotePage> _logger;

        public VM_CoatingOPMMRemotePage(ILoggerService<VM_CoatingOPMMRemotePage> logger)
        {
            _logger = logger;
        }
    }
}
