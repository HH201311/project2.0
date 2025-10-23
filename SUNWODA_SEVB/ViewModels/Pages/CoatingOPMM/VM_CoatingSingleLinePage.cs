using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Component;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace SUNWODA_SEVB.ViewModels.Pages.CoatingOPMM
{
    public class VM_CoatingSingleLinePage : DialogViewModelBase
    {
        private readonly ILoggerService<VM_CoatingOPMMPage> _logger;

        private readonly Brush NormalStatusBarBrush = new SolidColorBrush(
            Color.FromRgb(233, 250, 241)
        );
        private readonly Brush AbnormalStatusBarBrush = new SolidColorBrush(
            Color.FromRgb(255, 234, 234)
        );
        private readonly Brush NormalStatusInfoBrush = new SolidColorBrush(
            Color.FromRgb(41, 204, 122)
        );
        private readonly Brush AbnormalStatusInfoBrush = new SolidColorBrush(
            Color.FromRgb(255, 51, 51)
        );

        private string? title;
        private bool status;
        private string? statusInfo;
        private Brush? statusInfoBrush;
        private Brush? statusBarBrush;
        private FrameworkElement? faceAPage;
        private FrameworkElement? faceBPage;

        public VM_CoatingSingleFacePage? FaceAPageViewModel { get; set; }
        public VM_CoatingSingleFacePage? FaceBPageViewModel { get; set; }
        public FrameworkElement? FaceAPage
        {
            get => faceAPage;
            set => SetProperty(ref faceAPage, value);
        }
        public FrameworkElement? FaceBPage
        {
            get => faceBPage;
            set => SetProperty(ref faceBPage, value);
        }

        public string? Title
        {
            get => title;
            set => SetProperty(ref title, value);
        }

        public bool Status
        {
            get => status;
            set
            {
                SetProperty(ref status, value);
                StatusInfo = value ? "正常" : "异常";
                StatusBarBrush = value ? NormalStatusBarBrush : AbnormalStatusBarBrush;
                StatusInfoBrush = value ? NormalStatusInfoBrush : AbnormalStatusInfoBrush;
            }
        }

        public string? StatusInfo
        {
            get => statusInfo;
            set => SetProperty(ref statusInfo, value);
        }

        public Brush? StatusInfoBrush
        {
            get => statusInfoBrush;
            set => SetProperty(ref statusInfoBrush, value);
        }

        public Brush? StatusBarBrush
        {
            get => statusBarBrush;
            set => SetProperty(ref statusBarBrush, value);
        }

        public ObservableCollection<ColumnHeader>? FaceADryDataTableColumns { get; set; }

        public ObservableCollection<RowData>? FaceADryDataTableRows { get; set; }

        public ObservableCollection<ColumnHeader>? FaceBWetDataTableColumns { get; set; }

        public ObservableCollection<RowData>? FaceBWetDataTableRows { get; set; }

        public ObservableCollection<ColumnHeader>? FaceBDryDataTableColumns { get; set; }

        public ObservableCollection<RowData>? FaceBDryDataTableRows { get; set; }

        public ObservableCollection<ColumnHeader>? FaceADryAndBWetDataTableColumns { get; set; }

        public ObservableCollection<RowData>? FaceADryAndBWetDataTableRows { get; set; }

        public ObservableCollection<ColumnHeader>? FaceADryAndBDryDataTableColumns { get; set; }

        public ObservableCollection<RowData>? FaceADryAndBDryDataTableRows { get; set; }

        public ObservableCollection<ParameterModel>? DeviceInfos { get; set; }

        public ObservableCollection<ParameterModel>? GeneralParameterInfos { get; set; }

        public ObservableCollection<AlarmInfo> Alarms { get; set; } = new();

        public VM_CoatingSingleLinePage(ILoggerService<VM_CoatingOPMMPage> logger)
        {
            _logger = logger;
            FaceAPageViewModel = new VM_CoatingSingleFacePage(logger) { Title = "A" };
            FaceBPageViewModel = new VM_CoatingSingleFacePage(logger) { Title = "B" };
            FaceAPage = FaceAPageViewModel.UIElement;
            FaceBPage = FaceBPageViewModel.UIElement;
        }

        public void AddAlarmInfo(AlarmInfo alarm)
        {
            // 如果已达到最大数量，移除最早的一条
            if (Alarms.Count >= 3)
            {
                Alarms.RemoveAt(0);
            }

            Alarms.Add(alarm);
        }
    }
}
