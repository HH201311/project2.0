using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dm.util;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.ExtendedProperties;
using HalconDotNet;
using HandyControl.Controls;
using HandyControl.Data;
using Masuit.Tools;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using RemoteFileMonitor.Abstractions;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CTOL;
using SUNWODA_SEVB.Core.Models.Web;
using SUNWODA_SEVB.Services.Project.CTOL;
using SUNWODA_SEVB.ViewModels.Pages.Common;
using static System.Windows.Forms.Design.AxImporter;

namespace SUNWODA_SEVB.ViewModels.Pages.CTOL
{
    [Module("VM_CTOLAutoDetectionPage", "在线CT AI检测系统v2.0")]
    public class VM_CTOLAutoDetectionPage : ViewModelBase
    {
        private readonly IMonitorManager _manager;
        private readonly RemoteFileMonitorConfigService _configFileService;
        private readonly GapThresholdService _gapThresholdService;
        private readonly ProcessingResultService _resultService;
        private readonly BarcodeGroupingService _groupingService;
        private readonly IOptions<BarcodeGroupingOptions> _groupingOptions;
        private readonly InspectionResultService _inspectionResultService;
        private readonly DailyUploadService _uploadService;
        private readonly IOptions<DailyUploadOptions> _dailyUploadOptions;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly VM_UserCenter _userCenterViewModel;
        private readonly INavigationService _navigationService;
        private readonly ILoggerService<VM_CTOLAutoDetectionPage> _logger;

        #region 字段
        private List<double>? _detectionSattisticalData;
        private List<string>? _detectionSattisticalLabel;
        private List<string>? _detectionStatisticalLabelColor;

        private int _totalDetectionNum;

        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today.AddDays(1);

        private bool _isBusy;
        private int _totalCount;
        private int _pageIndex = 1;
        private int _maxPageCount = 1;

        private bool _isSharedThresholdMode = true;
        private double _sharedGap45Limit = 0.3;
        private double _sharedGap46Limit = 0.8;

        private bool _isEnabled;
        private string _uploadTargetDirectory = string.Empty;
        private int _triggerHour;
        private int _triggerMinute;

        private CancellationTokenSource? _autoLogoutCancellationTokenSource;
        #endregion

        #region 属性
        /// <summary>
        /// 监控模型集
        /// </summary>
        public IReadOnlyList<MonitorControlViewModel>? Monitors { get; set; }

        /// <summary>
        /// 检测统计数据
        /// </summary>
        public List<double>? DetectionSattisticalData
        {
            get => _detectionSattisticalData;
            set => SetProperty(ref _detectionSattisticalData, value);
        }

        /// <summary>
        /// 检测统计标签
        /// </summary>
        public List<string>? DetectionSattisticalLabel
        {
            get => _detectionSattisticalLabel;
            set => SetProperty(ref _detectionSattisticalLabel, value);
        }

        /// <summary>
        /// 检测统计标签颜色
        /// </summary>
        public List<string>? DetectionStatisticalLabelColor
        {
            get => _detectionStatisticalLabelColor;
            set => SetProperty(ref _detectionStatisticalLabelColor, value);
        }

        /// <summary>
        /// 缺陷数据统计列表
        /// </summary>
        public ObservableCollection<DefectData>? DefectDataList { get; set; }

        /// <summary>
        /// 总检测数
        /// </summary>
        public int TotalDetectionNum
        {
            get => _totalDetectionNum;
            set => SetProperty(ref _totalDetectionNum, value);
        }

        public ObservableCollection<InspectionResult>? InspectionResultList { get; set; }

        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }
        public ObservableCollection<DefectData>? DefectSelectedItems { get; set; }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ObservableCollection<InspectionResult> Results { get; } = new();

        public ObservableCollection<InspectionResult> ShowResults { get; set; } = new();

        public int TotalCount
        {
            get => _totalCount;
            private set => SetProperty(ref _totalCount, value);
        }

        /// <summary>
        /// 页码
        /// </summary>
        public int PageIndex
        {
            get => _pageIndex;
            set => SetProperty(ref _pageIndex, value);
        }

        public int MaxPageCount
        {
            get => _maxPageCount;
            set => SetProperty(ref _maxPageCount, value);
        }

        /// <summary>
        /// 是否启用四路共享阈值模式。
        /// 切换时同步更新 <see cref="GapThresholdService.IsSharedMode"/> 并持久化到配置文件。
        /// </summary>
        public bool IsSharedThresholdMode
        {
            get => _isSharedThresholdMode;
            set
            {
                if (_isSharedThresholdMode == value)
                    return;
                _isSharedThresholdMode = value;
                _gapThresholdService.IsSharedMode = value;
                _configFileService.SaveSharedMode(value);
                OnPropertyChanged();
            }
        }

        /// <summary>共享阈值行（与各路独立 VM 中的 GapThresholdRows 结构相同）。</summary>
        public ObservableCollection<GapThresholdRowItem>? SharedGapThresholdRows { get; set; }

        public double SharedGap45Limit
        {
            get => _sharedGap45Limit;
            set
            {
                _sharedGap45Limit = value;
                OnPropertyChanged();
            }
        }

        public double SharedGap46Limit
        {
            get => _sharedGap46Limit;
            set
            {
                _sharedGap46Limit = value;
                OnPropertyChanged();
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public string UploadTargetDirectory
        {
            get => _uploadTargetDirectory;
            set => SetProperty(ref _uploadTargetDirectory, value);
        }

        public int TriggerHour
        {
            get => _triggerHour;
            set => SetProperty(ref _triggerHour, value);
        }

        public int TriggerMinute
        {
            get => _triggerMinute;
            set => SetProperty(ref _triggerMinute, value);
        }

        public ObservableCollection<DefectData>? UploadDefectSelectedItems { get; set; }

        public ICommand? ClearListCommand { get; set; }

        public ICommand? ClearStatisticsCommand { get; set; }

        public ICommand? QueryCommand { get; set; }

        public ICommand? ExportCommand { get; set; }

        public ICommand? PageUpdatedCommand { get; set; }

        /// <summary>将共享阈值写入服务并持久化</summary>
        public ICommand? SaveSettingsCommand { get; set; }

        public ICommand? ResetSettingsCommand { get; set; }

        /// <summary>四路监控全部处于停止状态时为 true，用于控制共享阈值编辑区的可编辑性。</summary>
        public bool AreAllMonitorsStopped => Monitors?.All(m => !m.IsRunning) ?? false;
        #endregion

        public VM_CTOLAutoDetectionPage(
            IMonitorManager manager,
            RemoteFileMonitorConfigService configFileService,
            GapThresholdService gapThresholdService,
            ProcessingResultService resultService,
            BarcodeGroupingService groupingService,
            IOptions<BarcodeGroupingOptions> groupingOptions,
            InspectionResultService inspectionResultService,
            DailyUploadService uploadService,
            IOptions<DailyUploadOptions> dailyUploadOptions,
            IProjectSettingRepository projectSettingRepository,
            VM_UserCenter userCenterViewModel,
            INavigationService navigationService,
            ILoggerService<VM_CTOLAutoDetectionPage> logger
        )
        {
            _manager = manager;
            _configFileService = configFileService;
            _gapThresholdService = gapThresholdService;
            _resultService = resultService;
            _groupingService = groupingService;
            _groupingOptions = groupingOptions;
            _inspectionResultService = inspectionResultService;
            _uploadService = uploadService;
            _dailyUploadOptions = dailyUploadOptions;
            _projectSettingRepository = projectSettingRepository;
            _userCenterViewModel = userCenterViewModel;
            _navigationService = navigationService;
            _logger = logger;
        }

        public override void OnInitialize()
        {
            _userCenterViewModel.PropertyChanged += UserCenterViewModel_PropertyChanged;

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"TotalDetectionNum"
                    && model.BelongToVM == "VM_CTOLAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"TotalDetectionNum",
                        "VM_CTOLAutoDetectionPage",
                        $"{TotalDetectionNum}",
                        "int",
                        8
                    )
                    {
                        Remark = $"总检测计数",
                    }
                );
            }
            else
            {
                var cout = _projectSettingRepository.GetSettingValue(
                    $"TotalDetectionNum",
                    "VM_CTOLAutoDetectionPage"
                );
                TotalDetectionNum = cout;
            }

            DetectionStatisticalLabelColor = new List<string>()
            {
                "#66BB6A",
                "#EF5350",
                "#FFA726",
                "#FFCA28",
                "#FF8A65",
                "#EC407A",
                "#42A5F5",
                "#26C6DA",
                "#2645DA",
                "#7E57C2",
                "#AB47BC",
                "#8D6E63",
                "#78909C",
                "#504B4B",
                "#020202",
            };

            var opts = _groupingOptions.Value;
            // 初始化全局分类项（顺序与单路一致）
            DefectDataList = new ObservableCollection<DefectData>();
            DefectDataList.Add(new DefectData { DefectType = "OK" });
            foreach (var d in opts.DefectTypePriorities)
                DefectDataList.Add(new DefectData { DefectType = d });
            DefectDataList.Add(new DefectData { DefectType = opts.AlgorithmExceptionFolderName });
            DefectDataList.Add(new DefectData { DefectType = opts.TimeoutFolderName });

            // 从配置文件读取（优先），回退到 IOptions 注入值
            var saved = _configFileService.LoadDailyUploadOptions();
            var current = saved ?? _dailyUploadOptions.Value;

            IsEnabled = current.Enabled;
            UploadTargetDirectory = current.UploadTargetDirectory;
            TriggerHour = current.TriggerHour;
            TriggerMinute = current.TriggerMinute;

            DetectionSattisticalLabel = new List<string>();
            DetectionSattisticalData = new List<double>();
            DefectSelectedItems = new ObservableCollection<DefectData>();
            UploadDefectSelectedItems = new ObservableCollection<DefectData>();
            foreach (var item in DefectDataList)
            {
                if (
                    !_projectSettingRepository.Exists(model =>
                        model.Name == $"{item.DefectType}"
                        && model.BelongToVM == "VM_CTOLAutoDetectionPage"
                    )
                )
                {
                    _projectSettingRepository.Add(
                        new ProjectSettingModel(
                            $"{item.DefectType}",
                            "VM_CTOLAutoDetectionPage",
                            $"{item.Count}",
                            "int",
                            8
                        )
                        {
                            Remark = $"{item.DefectType}计数",
                        }
                    );
                    DetectionSattisticalLabel.Add(item.DefectType!);
                    DetectionSattisticalData.Add(item.Count);
                }
                else
                {
                    var cout = _projectSettingRepository.GetSettingValue(
                        $"{item.DefectType}",
                        "VM_CTOLAutoDetectionPage"
                    );
                    item.Count = cout;
                    item.Ratio = TotalDetectionNum == 0 ? 0.0 : cout * 1.0 / TotalDetectionNum;
                    DetectionSattisticalLabel.Add(item.DefectType!);
                    DetectionSattisticalData.Add(cout);
                }

                if (current.UploadDefectTypes.Contains($"{item.DefectType}"))
                    UploadDefectSelectedItems.Add(item);
            }

            InspectionResultList = new ObservableCollection<InspectionResult>();
            // 监听全局 BarcodeFinalized 事件汇总
            _groupingService.BarcodeFinalized += (monitorId, barcode, category) =>
            {
                RunOnUIThread(() =>
                {
                    TotalDetectionNum++;
                    _projectSettingRepository.UpdateSettingValue(
                        "TotalDetectionNum",
                        "VM_CTOLAutoDetectionPage",
                        TotalDetectionNum
                    );
                    var item = DefectDataList.FirstOrDefault(x =>
                        x.DefectType!.Equals(category, StringComparison.OrdinalIgnoreCase)
                    );
                    if (item is not null)
                    {
                        item.Count++;
                        _projectSettingRepository.UpdateSettingValue(
                            $"{item.DefectType}",
                            "VM_CTOLAutoDetectionPage",
                            item.Count
                        );
                    }

                    DetectionSattisticalData.Clear();
                    DetectionSattisticalData = null;
                    var detectionSattisticalData = new List<double>();
                    foreach (var d in DefectDataList)
                    {
                        d.Ratio = d.Count * 1.0 / TotalDetectionNum;
                        detectionSattisticalData.Add(d.Count);
                    }
                    DetectionSattisticalData = detectionSattisticalData;

                    var detectionResult = category switch
                    {
                        "OK" => "OK",
                        var c when c.StartsWith("算法") => "算法异常",
                        var c when c.StartsWith("超时") => "超时",
                        _ => "NG",
                    };

                    var result = new InspectionResult
                    {
                        StationId = monitorId,
                        Barcode = barcode,
                        DefectType = category,
                        DetectionResult = detectionResult,
                    };

                    if (InspectionResultList.Count >= 100)
                    {
                        InspectionResultList.Remove(InspectionResultList.Last());
                    }
                    InspectionResultList.Insert(0, result);
                });
            };

            // ── 初始化共享阈值 ────────────────────────────────────────
            var sg = _configFileService.LoadSharedThresholds() ?? new();
            double GetS(string key, double def) => sg.TryGetValue(key, out var v) ? v : def;

            SharedGapThresholdRows = new ObservableCollection<GapThresholdRowItem>
            {
                new(
                    "1-X", // ViewType
                    true, // HasNegDiff
                    GetS("Gap1Limit", 0.5), // OhLower
                    GetS("Gap2Limit", 2.75), // OhUpper
                    GetS("Gap3Limit", 2.2), // NegDiff
                    GetS("Gap4Limit", 0.7), // FjEnd
                    GetS("Gap5Limit", 0.45), // PosEnd
                    GetS("Gap6Limit", 0.45) //PosMiddle
                ),
                new(
                    "1-Y",
                    true,
                    GetS("Gap7Limit", 0.5),
                    GetS("Gap8Limit", 2.4),
                    GetS("Gap9Limit", 2.4),
                    GetS("Gap10Limit", 0.7),
                    GetS("Gap11Limit", 0.45),
                    GetS("Gap12Limit", 0.45)
                ),
                new(
                    "2-X",
                    false,
                    GetS("Gap13Limit", 0.5),
                    GetS("Gap14Limit", 2.0),
                    0,
                    GetS("Gap15Limit", 0.7),
                    GetS("Gap16Limit", 0.45),
                    GetS("Gap17Limit", 0.45)
                ),
                new(
                    "2-Y",
                    false,
                    GetS("Gap18Limit", 0.5),
                    GetS("Gap19Limit", 2.4),
                    0,
                    GetS("Gap20Limit", 0.7),
                    GetS("Gap21Limit", 0.45),
                    GetS("Gap22Limit", 0.45)
                ),
                new(
                    "3-X",
                    false,
                    GetS("Gap23Limit", 0.5),
                    GetS("Gap24Limit", 2.75),
                    0,
                    GetS("Gap25Limit", 0.7),
                    GetS("Gap26Limit", 0.45),
                    GetS("Gap27Limit", 0.45)
                ),
                new(
                    "3-Y",
                    false,
                    GetS("Gap28Limit", 0.5),
                    GetS("Gap29Limit", 2.4),
                    0,
                    GetS("Gap30Limit", 0.7),
                    GetS("Gap31Limit", 0.45),
                    GetS("Gap32Limit", 0.45)
                ),
                new(
                    "4-X",
                    true,
                    GetS("Gap33Limit", 0.5),
                    GetS("Gap34Limit", 2.0),
                    GetS("Gap35Limit", 2.2),
                    GetS("Gap36Limit", 0.7),
                    GetS("Gap37Limit", 0.45),
                    GetS("Gap38Limit", 0.45)
                ),
                new(
                    "4-Y",
                    true,
                    GetS("Gap39Limit", 0.5),
                    GetS("Gap40Limit", 2.4),
                    GetS("Gap41Limit", 2.4),
                    GetS("Gap42Limit", 0.7),
                    GetS("Gap43Limit", 0.45),
                    GetS("Gap44Limit", 0.45)
                ),
            };
            _sharedGap45Limit = GetS("Gap45Limit", 0.3);
            _sharedGap46Limit = GetS("Gap46Limit", 0.8);

            // 加载上次的模式并同步到服务
            _isSharedThresholdMode = _configFileService.LoadSharedMode();
            _gapThresholdService.IsSharedMode = _isSharedThresholdMode;
            if (_isSharedThresholdMode)
                _gapThresholdService.SetShared(BuildSharedThresholdDict());

            ClearListCommand = new RelayCommandAsync(ClearInspectionResultList);
            ClearStatisticsCommand = new RelayCommandAsync(ClearStatistics);
            QueryCommand = new RelayCommandAsync(QueryDefectSelectedData);
            ExportCommand = new RelayCommandAsync(ExportAsync);
            PageUpdatedCommand = new RelayCommand<FunctionEventArgs<int>>(PageUpdated);
            SaveSettingsCommand = new RelayCommandAsync(SaveSettings);
            ResetSettingsCommand = new RelayCommandAsync(ResetSettings);

            Monitors = _manager
                .Controls.Select(c => new MonitorControlViewModel(
                    c,
                    _manager,
                    _configFileService,
                    _gapThresholdService,
                    _resultService,
                    _groupingService,
                    opts,
                    _userCenterViewModel,
                    _navigationService,
                    _logger
                ))
                .ToList()
                .AsReadOnly();

            // 任意一路运行状态变化时，重新通知 AreAllMonitorsStopped
            foreach (var m in Monitors)
                m.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MonitorControlViewModel.IsRunning))
                        OnPropertyChanged(nameof(AreAllMonitorsStopped));
                };
            base.OnInitialize();
        }

        public static ImageSource LoadImageFromFile(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad; // 读入内存后释放文件占用
            bmp.EndInit();
            bmp.Freeze(); // 跨线程/性能更好
            return bmp;
        }

        private Task ClearInspectionResultList()
        {
            // 弹出确认对话框
            MessageBoxResult result = HandyControl.Controls.MessageBox.Show(
                "确定要清空图片监测结果列表吗？此操作不可恢复。",
                "确认清空",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.OK)
            {
                InspectionResultList?.Clear();
                _logger.Info("[清空监测列表] 用户确认，已清空数据");
            }
            else
            {
                _logger.Info("[清空监测列表] 用户取消，操作终止");
            }
            return Task.CompletedTask;
        }

        private Task ClearStatistics()
        {
            MessageBoxResult result = HandyControl.Controls.MessageBox.Show(
                "确定要重置统计数据吗？此操作不可恢复。",
                "确认清零",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.OK)
            {
                InspectionResultList?.Clear();
                TotalDetectionNum = 0;
                DetectionSattisticalData?.Clear();
                DetectionSattisticalData = null;
                var detectionSattisticalData = new List<double>();
                _projectSettingRepository.UpdateSettingValue(
                    "TotalDetectionNum",
                    "VM_CTOLAutoDetectionPage",
                    TotalDetectionNum
                );
                foreach (var d in DefectDataList!)
                {
                    d.Ratio = 0.0;
                    d.Count = 0;
                    _projectSettingRepository.UpdateSettingValue(
                        $"{d.DefectType}",
                        "VM_CTOLAutoDetectionPage",
                        d.Count
                    );
                    detectionSattisticalData!.Add(d.Count);
                }
                DetectionSattisticalData = detectionSattisticalData;

                _logger.Info("[统计清零] 用户确认，已重置统计");
            }
            else
            {
                _logger.Info("[统计清零] 用户取消，操作终止");
            }
            return Task.CompletedTask;
        }

        private async Task QueryDefectSelectedData()
        {
            IsBusy = true;
            try
            {
                var start = StartDate;
                var end = EndDate;

                // 收集选中类型；全选时传 null（跳过类型过滤，查全部）
                var selected = DefectSelectedItems!.Select(x => x.DefectType!).ToList();

                var data = await _inspectionResultService.QueryAsync(start, end, selected);

                Results.Clear();
                ShowResults.Clear();
                foreach (var r in data)
                    Results.Add(r);
                MaxPageCount = (int)Math.Ceiling(Results.Count / 10.0);
                ShowResults.AddRange(Results.Take(10).ToList());
                TotalCount = Results.Count;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExportAsync()
        {
            if (Results.Count == 0)
            {
                HandyControl.Controls.MessageBox.Show(
                    "当前无数据可导出，请先执行查询。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "导出检测记录",
                Filter = "CSV 文件|*.csv",
                FileName = $"检测记录_{StartDate:yyyyMMdd_HHmmss}-{EndDate:yyyyMMdd_HHmmss}.csv",
                DefaultExt = "csv",
                OverwritePrompt = true,
            };

            if (dlg.ShowDialog() != true)
                return;

            IsBusy = true;
            try
            {
                await InspectionResultService.ExportCsvAsync(Results, dlg.FileName);
                HandyControl.Controls.MessageBox.Show(
                    $"已成功导出 {Results.Count} 条记录。\n{dlg.FileName}",
                    "导出成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show(
                    $"导出失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void PageUpdated(FunctionEventArgs<int>? info)
        {
            if (info != null)
            {
                ShowResults.Clear();
                ShowResults.AddRange(Results.Skip((info.Info - 1) * 10).Take(10).ToList());
            }
        }

        private Task SaveSettings()
        {
            if (!CheckPermission())
                return Task.CompletedTask;

            try
            {
                var thresholds = BuildSharedThresholdDict();
                _gapThresholdService.SetShared(thresholds);
                _configFileService.SaveSharedThresholds(thresholds);

                var dailyUploadOpts = BuildDailyUploadOptions();
                _configFileService.SaveDailyUploadOptions(dailyUploadOpts);
                _uploadService.UpdateOptions(dailyUploadOpts);
                _logger.Info("保存设置成功");
                Growl.InfoGlobal("保存设置成功");
            }
            catch (Exception ex)
            {
                _logger.Error("保存设置失败", ex);
            }

            return Task.CompletedTask;
        }

        private Task ResetSettings()
        {
            if (!CheckPermission())
                return Task.CompletedTask;

            // 确认用户是否要重置
            var result1 = HandyControl.Controls.MessageBox.Show(
                "确定要将阈值重置为上一次保存的值吗？\n此操作会覆盖当前未保存的修改。",
                "确认重置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );
            if (result1 != MessageBoxResult.Yes)
                return Task.CompletedTask;

            var rows = SharedGapThresholdRows!;
            // rows 顺序固定：0=1-X, 1=1-Y, 2=2-X, 3=2-Y, 4=3-X, 5=3-Y, 6=4-X, 7=4-Y
            rows[0].OhLower = 0.5;
            rows[0].OhUpper = 2.75;
            rows[0].NegDiff = 2.2;
            rows[0].FjEnd = 0.64;
            rows[0].PosEnd = 0.45;
            rows[0].PosMiddle = 0.45;
            rows[1].OhLower = 0.5;
            rows[1].OhUpper = 2.4;
            rows[1].NegDiff = 2.4;
            rows[1].FjEnd = 0.64;
            rows[1].PosEnd = 0.45;
            rows[1].PosMiddle = 0.45;
            rows[2].OhLower = 0.5;
            rows[2].OhUpper = 2.0;
            rows[2].NegDiff = 0;
            rows[2].FjEnd = 0.64;
            rows[2].PosEnd = 0.45;
            rows[2].PosMiddle = 0.45;
            rows[3].OhLower = 0.5;
            rows[3].OhUpper = 2.4;
            rows[3].NegDiff = 0;
            rows[3].FjEnd = 0.64;
            rows[3].PosEnd = 0.45;
            rows[3].PosMiddle = 0.45;
            rows[4].OhLower = 0.5;
            rows[4].OhUpper = 2.75;
            rows[4].NegDiff = 0;
            rows[4].FjEnd = 0.64;
            rows[4].PosEnd = 0.45;
            rows[4].PosMiddle = 0.45;
            rows[5].OhLower = 0.5;
            rows[5].OhUpper = 2.4;
            rows[5].NegDiff = 0;
            rows[5].FjEnd = 0.64;
            rows[5].PosEnd = 0.45;
            rows[5].PosMiddle = 0.45;
            rows[6].OhLower = 0.5;
            rows[6].OhUpper = 2.0;
            rows[6].NegDiff = 2.2;
            rows[6].FjEnd = 0.64;
            rows[6].PosEnd = 0.45;
            rows[6].PosMiddle = 0.45;
            rows[7].OhLower = 0.5;
            rows[7].OhUpper = 2.4;
            rows[7].NegDiff = 2.4;
            rows[7].FjEnd = 0.64;
            rows[7].PosEnd = 0.45;
            rows[7].PosMiddle = 0.45;
            SharedGap45Limit = 0.3;
            SharedGap46Limit = 0.8;
            _logger.Info("重置阈值设置成功");
            Growl.InfoGlobal("重置阈值设置成功");
            return Task.CompletedTask;
        }

        // 辅助方法：检查是否有权限执行操作
        private bool CheckPermission()
        {
            // 检查是否登录
            if (!_userCenterViewModel.IsLoggedIn)
            {
                // 使用 MessageBox 弹出登录提示
                MessageBoxResult result = HandyControl.Controls.MessageBox.Show(
                    "请先登录以执行此操作。\n点击“确定”将跳转到登录页面。",
                    "操作需要登录",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                // 这里只有OK按钮，导航到登录页
                if (result == MessageBoxResult.OK)
                {
                    _navigationService.NavigateTo("UserCenter");
                }
                return false;
            }

            //检查权限级别
            if (_userCenterViewModel.RoleID < (int)UserRole.Engineer)
            {
                // 使用 MessageBox 弹出权限不足提示
                HandyControl.Controls.MessageBox.Show(
                    "您的权限等级不足，无法执行此操作。\n需要【工程师】或以上权限。",
                    "权限不足",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return false;
            }

            // 检查通过
            return true;
        }

        private DailyUploadOptions BuildDailyUploadOptions() =>
            new()
            {
                Enabled = _isEnabled,
                UploadTargetDirectory = _uploadTargetDirectory,
                TriggerHour = _triggerHour,
                TriggerMinute = _triggerMinute,
                UploadDefectTypes = UploadDefectSelectedItems!
                    .Select(i => $"{i.DefectType}")
                    .ToList(),
            };

        private Dictionary<string, double> BuildSharedThresholdDict()
        {
            var rows = SharedGapThresholdRows!;
            return new Dictionary<string, double>
            {
                ["Gap1Limit"] = rows[0].OhLower,
                ["Gap2Limit"] = rows[0].OhUpper,
                ["Gap3Limit"] = rows[0].NegDiff,
                ["Gap4Limit"] = rows[0].FjEnd,
                ["Gap5Limit"] = rows[0].PosEnd,
                ["Gap6Limit"] = rows[0].PosMiddle,
                ["Gap7Limit"] = rows[1].OhLower,
                ["Gap8Limit"] = rows[1].OhUpper,
                ["Gap9Limit"] = rows[1].NegDiff,
                ["Gap10Limit"] = rows[1].FjEnd,
                ["Gap11Limit"] = rows[1].PosEnd,
                ["Gap12Limit"] = rows[1].PosMiddle,
                ["Gap13Limit"] = rows[2].OhLower,
                ["Gap14Limit"] = rows[2].OhUpper,
                ["Gap15Limit"] = rows[2].FjEnd,
                ["Gap16Limit"] = rows[2].PosEnd,
                ["Gap17Limit"] = rows[2].PosMiddle,
                ["Gap18Limit"] = rows[3].OhLower,
                ["Gap19Limit"] = rows[3].OhUpper,
                ["Gap20Limit"] = rows[3].FjEnd,
                ["Gap21Limit"] = rows[3].PosEnd,
                ["Gap22Limit"] = rows[3].PosMiddle,
                ["Gap23Limit"] = rows[4].OhLower,
                ["Gap24Limit"] = rows[4].OhUpper,
                ["Gap25Limit"] = rows[4].FjEnd,
                ["Gap26Limit"] = rows[4].PosEnd,
                ["Gap27Limit"] = rows[4].PosMiddle,
                ["Gap28Limit"] = rows[5].OhLower,
                ["Gap29Limit"] = rows[5].OhUpper,
                ["Gap30Limit"] = rows[5].FjEnd,
                ["Gap31Limit"] = rows[5].PosEnd,
                ["Gap32Limit"] = rows[5].PosMiddle,
                ["Gap33Limit"] = rows[6].OhLower,
                ["Gap34Limit"] = rows[6].OhUpper,
                ["Gap35Limit"] = rows[6].NegDiff,
                ["Gap36Limit"] = rows[6].FjEnd,
                ["Gap37Limit"] = rows[6].PosEnd,
                ["Gap38Limit"] = rows[6].PosMiddle,
                ["Gap39Limit"] = rows[7].OhLower,
                ["Gap40Limit"] = rows[7].OhUpper,
                ["Gap41Limit"] = rows[7].NegDiff,
                ["Gap42Limit"] = rows[7].FjEnd,
                ["Gap43Limit"] = rows[7].PosEnd,
                ["Gap44Limit"] = rows[7].PosMiddle,
                ["Gap45Limit"] = SharedGap45Limit,
                ["Gap46Limit"] = SharedGap46Limit,
            };
        }

        // 监听登录状态变化
        private void UserCenterViewModel_PropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs e
        )
        {
            if (e.PropertyName == nameof(VM_UserCenter.IsLoggedIn))
            {
                if (_userCenterViewModel.IsLoggedIn)
                {
                    // 登录成功，启动15分钟自动退出计时器
                    AutoNavigateBackAfterLogin();
                    StartAutoLogoutTimer();
                    Growl.InfoGlobal("登录成功，15分钟后将自动退出。");
                }
                else
                {
                    // 退出登录，取消计时器
                    CancelAutoLogoutTimer();
                }
            }
        }

        private void AutoNavigateBackAfterLogin()
        {
            // 导航回应用界面
            _navigationService.NavigateTo("VM_CTOLAutoDetectionPage");
        }

        // 启动自动退出计时器
        private void StartAutoLogoutTimer()
        {
            // 取消旧计时器
            CancelAutoLogoutTimer();
            _autoLogoutCancellationTokenSource = new CancellationTokenSource();
            var token = _autoLogoutCancellationTokenSource.Token;

            Task.Delay(TimeSpan.FromMinutes(15), token)
                .ContinueWith(
                    t =>
                    {
                        if (!t.IsCanceled)
                        {
                            RunOnUIThread(() =>
                            {
                                if (_userCenterViewModel.IsLoggedIn)
                                {
                                    _logger.Info(
                                        "登录时间已超过15分钟，为保障账户安全，已自动退出登录。"
                                    );
                                    // 调用主框架的退出登录命令
                                    _userCenterViewModel.LogoutCommand?.Execute(null);
                                }
                            });
                        }
                    },
                    token
                );
        }

        /// <summary>
        /// 取消自动退出计时器
        /// </summary>
        private void CancelAutoLogoutTimer()
        {
            // 检查令牌源是否存在且未被取消
            if (
                _autoLogoutCancellationTokenSource != null
                && !_autoLogoutCancellationTokenSource.IsCancellationRequested
            )
            {
                _autoLogoutCancellationTokenSource.Cancel();
                _autoLogoutCancellationTokenSource.Dispose();
                _autoLogoutCancellationTokenSource = null;
            }
        }
    }

    public class CheckComboBoxHelper : DependencyObject
    {
        public static IList GetSelectedItems(DependencyObject obj)
        {
            return (IList)obj.GetValue(SelectedItemsProperty);
        }

        public static void SetSelectedItems(DependencyObject obj, int value)
        {
            obj.SetValue(SelectedItemsProperty, value);
        }

        public string SelectedItems
        {
            get { return (string)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems",
                typeof(IEnumerable),
                typeof(CheckComboBoxHelper),
                new PropertyMetadata(null, OnSelectedItemsChanged)
            );

        private static void OnSelectedItemsChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        )
        {
            if (d is CheckComboBox cbox)
            {
                if (e.OldValue != null)
                {
                    cbox.SelectionChanged -= OnSelectionChanged;
                }
                if (e.NewValue is IList list)
                {
                    cbox.SelectedItems.Clear();
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            cbox.SelectedItems.Add(item);
                        }
                        cbox.OnApplyTemplate();
                        cbox.SelectionChanged += OnSelectionChanged;
                    }
                }
            }
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dataSource = GetSelectedItems((sender as DependencyObject)!);
            // 添加用户选中的项
            foreach (var item in e.AddedItems)
            {
                if (dataSource.IndexOf(item) == -1)
                    dataSource.Add(item);
            }
            // 删除用户取消选中的项
            foreach (var item in e.RemovedItems)
            {
                if (dataSource.IndexOf(item) >= 0)
                    dataSource.Remove(item);
            }
        }
    }
}
