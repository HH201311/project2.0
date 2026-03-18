using System.Collections.ObjectModel;
using System.Windows.Media;
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

        private double mainFontSize = 16;
        private double littleTitleFontSize = 18;
        private double parameterVerticalSpacing = 80;
        private Brush pageForeground = new SolidColorBrush(Colors.Black);
        private string plotBackgroundColorHex = "#FFFFFF";
        private string plotLabelFontColorHex = "#000000";

        public double MainFontSize
        {
            get => mainFontSize;
            set => SetProperty(ref mainFontSize, value);
        }

        public double LittleTitleFontSize
        {
            get => littleTitleFontSize;
            set => SetProperty(ref littleTitleFontSize, value);
        }

        public double ParameterVerticalSpacing
        {
            get => parameterVerticalSpacing;
            set => SetProperty(ref parameterVerticalSpacing, value);
        }

        public Brush PageForeground
        {
            get => pageForeground;
            set => SetProperty(ref pageForeground, value);
        }

        public string PlotBackgroundColorHex
        {
            get => plotBackgroundColorHex;
            set => SetProperty(ref plotBackgroundColorHex, value);
        }

        public string PlotLabelFontColorHex
        {
            get => plotLabelFontColorHex;
            set => SetProperty(ref plotLabelFontColorHex, value);
        }

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

        private double zoneBaseValue;
        public double ZoneBaseValue
        {
            get => zoneBaseValue;
            set => SetProperty(ref zoneBaseValue, value);
        }

        private Dictionary<string, double>? refValue;
        public Dictionary<string, double>? RefValue
        {
            get => refValue;
            set => SetProperty(ref refValue, value);
        }

        public ObservableCollection<ParameterModel>? SpecificationInfos { get; set; }

        public ObservableCollection<ParameterModel>? DeviceMonitorInfos { get; set; }

        public ObservableCollection<CoatingSegmentModel>? RawSegments { get; set; }

        private string? faceALabel;
        public string? FaceALabel
        {
            get => faceALabel;
            set => SetProperty(ref faceALabel, value);
        }

        private string? faceBLabel;
        public string? FaceBLabel
        {
            get => faceBLabel;
            set => SetProperty(ref faceBLabel, value);
        }

        private List<double> x = new();
        public List<double> X
        {
            get => x;
            set => SetProperty(ref x, value);
        }

        private List<double> y = new();
        public List<double> Y
        {
            get => y;
            set => SetProperty(ref y, value);
        }

        private double mmBaseLine = 300.0;

        public double MMBaseline
        {
            get => mmBaseLine;
            set => SetProperty(ref mmBaseLine, value);
        }

        private double warnLow = 299.70;
        public double WarnLow
        {
            get => warnLow;
            set => SetProperty(ref warnLow, value);
        }

        private double warnHigh = 300.30;
        public double WarnHigh
        {
            get => warnHigh;
            set => SetProperty(ref warnHigh, value);
        }

        private double specLow = 299.64;
        public double SpecLow
        {
            get => specLow;
            set => SetProperty(ref specLow, value);
        }

        private double specHigh = 300.36;
        public double SpecHigh
        {
            get => specHigh;
            set => SetProperty(ref specHigh, value);
        }

        public VM_CoatingSingleFacePage(ILoggerService<VM_CoatingOPMMPage> logger)
        {
            _logger = logger;
        }
    }
}
