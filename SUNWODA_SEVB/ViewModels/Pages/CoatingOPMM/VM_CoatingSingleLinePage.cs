using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Component;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.CoatingOPMM;

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

        private readonly object gate = new();

        private string? title;
        private bool status;
        private string? statusInfo;
        private Brush? statusInfoBrush;
        private Brush? statusBarBrush;
        private FrameworkElement? faceAPage;
        private FrameworkElement? faceBPage;

        private double mainFontSize = 16;
        private double lineTitleFontSize = 30;
        private double littleTitleFontSize = 18;
        private double parameterVerticalSpacing = 30;

        public double MainFontSize
        {
            get => mainFontSize;
            set => SetProperty(ref mainFontSize, value);
        }

        public double LineTitleFontSize
        {
            get => lineTitleFontSize;
            set => SetProperty(ref lineTitleFontSize, value);
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

        public string? MESClientDataFilePath { get; set; }
        public FileSystemWatcher? Watcher { get; set; }
        public System.Timers.Timer? DebounceTimer { get; set; }

        public List<PLCRWAddress> OldPLCAddressMonitor { get; set; } = new List<PLCRWAddress>();

        public List<PLCParameterModel> OldPLCParameterMonitor { get; set; } = new List<PLCParameterModel>();

        public CoatingCCDSpecModel CoatingCCDSpecModel { get; set; } = new();

        public VM_CoatingSingleLinePage(ILoggerService<VM_CoatingOPMMPage> logger)
        {
            _logger = logger;
            FaceAPageViewModel = new VM_CoatingSingleFacePage(logger)
            {
                Title = "A",
                FaceALabel = "A面干膜",
                FaceBLabel = "B面湿膜",
            };
            FaceBPageViewModel = new VM_CoatingSingleFacePage(logger)
            {
                Title = "B",
                FaceALabel = "A面干膜",
                FaceBLabel = "B面干膜",
            };
            FaceAPage = FaceAPageViewModel.UIElement;
            FaceBPage = FaceBPageViewModel.UIElement;
        }

        public void AddAlarmInfo(AlarmInfo alarm)
        {
            // 如果已达到最大数量，移除最早的一条
            if (Alarms.Count >= 4)
            {
                Alarms.RemoveAt(0);
            }

            Alarms.Add(alarm);
        }

        public void StartFileWatcher(string filePath)
        {
            MESClientDataFilePath = filePath;
            if (!File.Exists(filePath))
            {
                _logger.Error($"文件不存在：{filePath}");
            }
            else
            {
                UpdateProductSn();
                var dir = Path.GetDirectoryName(filePath)!;
                var name = Path.GetFileName(filePath);

                Watcher?.Dispose();

                Watcher = new FileSystemWatcher(dir, name)
                {
                    NotifyFilter =
                        NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                };

                // 去抖：文件可能在短时间内多次写入
                DebounceTimer = new System.Timers.Timer(250) { AutoReset = false };
                DebounceTimer.Elapsed += (_, __) => OnDebouncedChanged();

                Watcher.Changed += OnChanged;
                Watcher.Renamed += OnChanged;
                Watcher.Created += OnChanged;

                Watcher.EnableRaisingEvents = true;
            }
        }

        private void OnDebouncedChanged()
        {
            try
            {
                UpdateProductSn();
            }
            catch (Exception ex)
            {
                _logger.Error($"读取异常", ex);
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            lock (gate)
            {
                DebounceTimer!.Stop();
                DebounceTimer!.Start();
            }
        }

        private void UpdateProductSn()
        {
            var productSn = GetProductSn(MESClientDataFilePath!);

            DeviceInfos!.FirstOrDefault(item => item.PName == "产品批号")!.PValue = $"{productSn}";

            DeviceInfos!.FirstOrDefault(item => item.PName == "膜卷号")!.PValue = $"{productSn}";
        }

        private string GetProductSn(string filePath)
        {
            string content = ReadFileSafely(filePath, Encoding.UTF8);
            var productSn = "";
            var match = Regex.Match(
                content,
                @"MachineID：*(.+)\r\nMoNumber：*(.+)\r\nPreProductSn：*(.+)\r\nOperatorID：*(.+)\r\nGroupCode：*(.+)\r\nProductSn：*(.+)\r\nModelNum：*(.+)\r\nOperatorNumber：*(.+)\r\n*(.+)"
            );
            if (match.Success)
            {
                productSn = match.Groups[6].Value.Trim();
            }
            else
            {
                _logger.Error("未找到 ProductSn 字段。");
            }
            return productSn;
        }

        private string ReadFileSafely(string path, Encoding encoding)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            using var sr = new StreamReader(fs, encoding, detectEncodingFromByteOrderMarks: true);
            return sr.ReadToEnd();
        }
    }
}
