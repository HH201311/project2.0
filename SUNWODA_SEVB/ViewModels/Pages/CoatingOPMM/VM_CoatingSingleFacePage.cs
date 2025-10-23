using System.Collections.ObjectModel;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Component;

namespace SUNWODA_SEVB.ViewModels.Pages.CoatingOPMM
{
    public class VM_CoatingSingleFacePage : DialogViewModelBase
    {
        private readonly ILoggerService<VM_CoatingOPMMPage> _logger;

        private string? title;
        private List<double[]>? filmData;

        public string? Title
        {
            get => title;
            set => SetProperty(ref title, value);
        }

        public List<double[]>? FilmData
        {
            get => filmData;
            set => SetProperty(ref filmData, value);
        }

        public ObservableCollection<ParameterModel>? SpecificationInfos { get; set; }

        public ObservableCollection<ParameterModel>? DeviceMonitorInfos { get; set; }

        public ObservableCollection<CoatingSegmentModel>? RawSegments { get; set; }

        public VM_CoatingSingleFacePage(ILoggerService<VM_CoatingOPMMPage> logger)
        {
            _logger = logger;
        }
    }
}
