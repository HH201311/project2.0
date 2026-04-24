using Microsoft.Win32;
using RemoteFileMonitor.Abstractions;
using RemoteFileMonitor.Options;
using RemoteFileMonitor.Services;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models.Project.CTOL;
using SUNWODA_SEVB.Services.Project.CTOL;
using SUNWODA_SEVB.ViewModels.Pages.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SUNWODA_SEVB.ViewModels.Pages.CTOL
{
    /// <summary>
    /// 单路监控的 ViewModel，绑定界面上一张监控卡片。
    ///
    /// 字段可在停止状态下编辑；点击"启动"时先保存参数到配置文件再启动。
    /// </summary>
    public sealed class MonitorControlViewModel : ViewModelBase
    {
        private readonly MonitorControl _control;
        private readonly IMonitorManager _manager;
        private readonly RemoteFileMonitorConfigService _configFileService;
        private readonly GapThresholdService _gapThresholdService;
        private readonly VM_UserCenter _userCenterViewModel;
        private readonly INavigationService _navigationService;
        private readonly ILoggerService _logger;

        // ── 可编辑字段（与运行参数解耦，停机期间自由修改）──────────────
        private string _remotePath;
        private string _outputDirectory;
        private string _filter;
        private bool _includeSubdirectories;
        private int _pollingIntervalSeconds;
        private int _pollingOverlapSeconds;

        // ── 处理结果统计（实时更新）──────────────────────────────────
        private int _processedCount;
        private ImageSource? _lastProcessedImage;
        private string _lastProcessedFileName = "尚无";

        // ── Barcode 分组统计 ────────────────────────────────────
        private int _totalBarcodeCount;
        public string Id => _control.Id;

        // ── 间隙阈值（停机状态下可编辑）──────────────────────────────
        /// <summary>
        /// 按视野分组的间隙阈值，每行对应一种视野类型（"1-X" ~ "4-Y"）。
        /// 在界面上以 DataGrid 呈现，停机时可直接编辑。
        /// </summary>
        public ObservableCollection<GapThresholdRowItem> GapThresholdRows { get; }

        /// <summary>单图内相邻正极层 OH 差值上限（掉片初判，对应 Gap45Limit）。</summary>
        private double _gap45Limit = 0.3;
        public double Gap45Limit
        {
            get => _gap45Limit;
            set => SetProperty(ref _gap45Limit, value);
        }

        /// <summary>四张 Y 图联合掉片判断阈值 |1Y+3Y−2Y−4Y|（对应 Gap46Limit）。</summary>
        private double _gap46Limit = 0.3;
        public double Gap46Limit
        {
            get => _gap46Limit;
            set => SetProperty(ref _gap46Limit, value);
        }

        /// <summary>四张 Y 图联合掉片判断阈值 |1Y−3Y| 或 |2Y−4Y|（对应 Gap47Limit）。</summary>
        private double _gap47Limit = 0.5;
        public double Gap47Limit
        {
            get => _gap47Limit;
            set => SetProperty(ref _gap47Limit, value);
        }

        public bool IsRunning => _control.IsRunning;
        public bool CanEdit => !_control.IsRunning;

        public string StatusText => IsRunning ? "运行中" : "已停止";
        public string ToggleButtonText => IsRunning ? "停止" : "启动";

        public int ProcessedCount
        {
            get => _processedCount;
            private set => SetProperty(ref _processedCount, value);
        }

        public ImageSource? LastProcessedImage
        {
            get => _lastProcessedImage;
            private set => SetProperty(ref _lastProcessedImage, value);
        }

        public string LastProcessedFileName
        {
            get => _lastProcessedFileName;
            private set => SetProperty(ref _lastProcessedFileName, value);
        }

        public int TotalBarcodeCount
        {
            get => _totalBarcodeCount;
            private set => SetProperty(ref _totalBarcodeCount, value);
        }

        public ObservableCollection<DefectData> DefectCounts { get; } = new();

        // ── 绑定属性 ──────────────────────────────────────────────────
        public string RemotePath
        {
            get => _remotePath;
            set => SetProperty(ref _remotePath, value);
        }

        public string OutputDirectory
        {
            get => _outputDirectory;
            set => SetProperty(ref _outputDirectory, value);
        }

        public string Filter
        {
            get => _filter;
            set => SetProperty(ref _filter, value);
        }

        public bool IncludeSubdirectories
        {
            get => _includeSubdirectories;
            set => SetProperty(ref _includeSubdirectories, value);
        }

        public int PollingIntervalSeconds
        {
            get => _pollingIntervalSeconds;
            set => SetProperty(ref _pollingIntervalSeconds, value);
        }

        public int PollingOverlapSeconds
        {
            get => _pollingOverlapSeconds;
            set => SetProperty(ref _pollingOverlapSeconds, value);
        }

        public ICommand ToggleCommand { get; }
        public ICommand BrowseRemotePathCommand { get; }
        public ICommand BrowseOutputDirectoryCommand { get; }

        public MonitorControlViewModel(
            MonitorControl control,
            IMonitorManager manager,
            RemoteFileMonitorConfigService configFileService,
            GapThresholdService gapThresholdService,
            ProcessingResultService resultService,
            BarcodeGroupingService groupingService,
            BarcodeGroupingOptions groupingOptions,
            VM_UserCenter userCenterViewModel,
            INavigationService navigationService,
            ILoggerService logger
        )
        {
            _control = control;
            _manager = manager;
            _configFileService = configFileService;
            _gapThresholdService = gapThresholdService;
            _userCenterViewModel = userCenterViewModel;
            _navigationService = navigationService;
            _logger = logger;

            // 初始化 barcode 统计分类（OK + 各缺陷类型 + 超时）
            DefectCounts.Add(new DefectData { DefectType = "OK" });
            foreach (var d in groupingOptions.DefectTypePriorities)
                DefectCounts.Add(new DefectData { DefectType = d });
            DefectCounts.Add(new DefectData { DefectType = groupingOptions.TimeoutFolderName });

            // 初始化间隙阈值行（从配置文件加载覆盖值，无则使用处理器内置默认值）
            var g = configFileService.LoadThresholds(control.Id) ?? new();
            double Get(string key, double def) => g.TryGetValue(key, out var v) ? v : def;
            GapThresholdRows = new ObservableCollection<GapThresholdRowItem>
            {
                new(
                    "1-X", // ViewType
                    true, // HasNegDiff
                    Get("Gap1Limit", 0.5), // OhLower
                    Get("Gap2Limit", 2.75), // OhUpper
                    Get("Gap3Limit", 2.2), // NegDiff
                    Get("Gap4Limit", 0.64), // FjEnd
                    Get("Gap5Limit", 0.45), // PosEnd
                    Get("Gap6Limit", 0.45) // PosMiddle
                ),
                new(
                    "1-Y",
                    true,
                    Get("Gap7Limit", 0.5),
                    Get("Gap8Limit", 2.4),
                    Get("Gap9Limit", 2.4),
                    Get("Gap10Limit", 0.64),
                    Get("Gap11Limit", 0.45),
                    Get("Gap12Limit", 0.45)
                ),
                new(
                    "2-X",
                    false,
                    Get("Gap13Limit", 0.5),
                    Get("Gap14Limit", 2.0),
                    0,
                    Get("Gap15Limit", 0.64),
                    Get("Gap16Limit", 0.45),
                    Get("Gap17Limit", 0.45)
                ),
                new(
                    "2-Y",
                    false,
                    Get("Gap18Limit", 0.5),
                    Get("Gap19Limit", 2.4),
                    0,
                    Get("Gap20Limit", 0.64),
                    Get("Gap21Limit", 0.45),
                    Get("Gap22Limit", 0.45)
                ),
                new(
                    "3-X",
                    false,
                    Get("Gap23Limit", 0.5),
                    Get("Gap24Limit", 2.75),
                    0,
                    Get("Gap25Limit", 0.64),
                    Get("Gap26Limit", 0.45),
                    Get("Gap27Limit", 0.45)
                ),
                new(
                    "3-Y",
                    false,
                    Get("Gap28Limit", 0.5),
                    Get("Gap29Limit", 2.4),
                    0,
                    Get("Gap30Limit", 0.64),
                    Get("Gap31Limit", 0.45),
                    Get("Gap32Limit", 0.45)
                ),
                new(
                    "4-X",
                    true,
                    Get("Gap33Limit", 0.5),
                    Get("Gap34Limit", 2.0),
                    Get("Gap35Limit", 2.2),
                    Get("Gap36Limit", 0.64),
                    Get("Gap37Limit", 0.45),
                    Get("Gap38Limit", 0.45)
                ),
                new(
                    "4-Y",
                    true,
                    Get("Gap39Limit", 0.5),
                    Get("Gap40Limit", 2.4),
                    Get("Gap41Limit", 2.4),
                    Get("Gap42Limit", 0.64),
                    Get("Gap43Limit", 0.45),
                    Get("Gap44Limit", 0.45)
                ),
            };
            _gap45Limit = Get("Gap45Limit", 0.3);
            _gap46Limit = Get("Gap46Limit", 0.3);
            _gap47Limit = Get("Gap47Limit", 0.5);

            // 当前阈值嵌入到服务（处理器每张图开始时将读取）
            gapThresholdService.Set(control.Id, BuildGapThresholdDict());

            // 初始化编辑字段
            _remotePath = control.Options.RemotePath;
            _outputDirectory = control.Options.OutputDirectory ?? string.Empty;
            _filter = control.Options.Filter;
            _includeSubdirectories = control.Options.IncludeSubdirectories;
            _pollingIntervalSeconds = control.Options.PollingIntervalSeconds;
            _pollingOverlapSeconds = control.Options.PollingOverlapSeconds;

            // 状态变更时刷新 UI（从后台线程安全地回到 UI 线程）
            control.StateChanged += (_, _) =>
                RunOnUIThread(() =>
                {
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(CanEdit));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(ToggleButtonText));
                });

            // 每处理完一张立即通知 UI 更新预览（BeginInvoke 不阻塞处理线程）
            resultService.ImageProcessed += (monitorId, outputPath) =>
            {
                if (!monitorId.Equals(Id, StringComparison.OrdinalIgnoreCase))
                    return;

                RunOnUIThread(() =>
                {
                    ProcessedCount++;
                    LastProcessedFileName = Path.GetFileName(outputPath);

                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(outputPath, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad; // 立即读入内存，释放文件句柄
                        bmp.EndInit();
                        bmp.Freeze(); // 跨线程安全
                        LastProcessedImage = bmp;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{Id}] 预览加载失败：{ex.Message}");
                    }
                });
            };

            // barcode 分组完成时更新统计
            groupingService.BarcodeFinalized += (monitorId, _, category) =>
            {
                if (!monitorId.Equals(Id, StringComparison.OrdinalIgnoreCase))
                    return;
                RunOnUIThread(() =>
                {
                    TotalBarcodeCount++;
                    var item = DefectCounts.FirstOrDefault(x =>
                        x.DefectType!.Equals(category, StringComparison.OrdinalIgnoreCase)
                    );
                    if (item is not null)
                        item.Count++;

                    foreach (var d in DefectCounts)
                    {
                        d.Ratio = d.Count * 1.0 / TotalBarcodeCount;
                    }
                });
            };

            ToggleCommand = new RelayCommandAsync(ToggleAsync);
            BrowseRemotePathCommand = new RelayCommandAsync(BrowseRemotePathAsync);
            BrowseOutputDirectoryCommand = new RelayCommandAsync(BrowseOutputDirectoryAsync);
        }

        // ==================== 启停逻辑 ====================

        private async Task ToggleAsync()
        {
            if (_control.IsRunning)
            {
                await _manager.StopMonitorAsync(Id);
            }
            else
            {
                // 应用编辑后的参数
                var newOptions = BuildOptions();
                _manager.UpdateOptions(Id, newOptions);

                // 持久化监控参数到配置文件
                _configFileService.UpdateTarget(newOptions);

                // 将阈值写入服务（处理器下次处理图片时将读取）并持久化
                var thresholds = BuildGapThresholdDict();
                _gapThresholdService.Set(Id, thresholds);
                _configFileService.SaveThresholds(Id, thresholds);

                await _manager.StartMonitorAsync(Id);
            }
        }

        // ==================== 浏览文件夹逻辑 ====================
        private async Task BrowseRemotePathAsync()
        {
            // 检查权限
            if (!CheckPermission())
                return;

            var folderDialog = new OpenFolderDialog();
            folderDialog.Title = $"选择{Id}在线检测监控文件夹";
            folderDialog.InitialDirectory = RemotePath;
            folderDialog.ValidateNames = true;
            folderDialog.Multiselect = false;
            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                RemotePath = folderDialog.FolderName;
                _logger.Info($"{Id} 已选择在线监控文件夹：{RemotePath}");
            }

            await Task.CompletedTask;
        }

        private async Task BrowseOutputDirectoryAsync()
        {
            // 检查权限
            if (!CheckPermission())
                return;

            var folderDialog = new OpenFolderDialog();
            folderDialog.Title = $"选择{Id}输出结果文件夹";
            folderDialog.InitialDirectory = OutputDirectory;
            folderDialog.ValidateNames = true;
            folderDialog.Multiselect = false;
            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                OutputDirectory = folderDialog.FolderName;
                _logger.Info($"{Id} 已选择输出结果文件夹：{OutputDirectory}");
            }

            await Task.CompletedTask;
        }

        // ==================== 构建参数对象 ====================

        internal MonitorTargetOptions BuildOptions() =>
            new()
            {
                Id = Id,
                RemotePath = RemotePath,
                OutputDirectory = OutputDirectory,
                Filter = Filter,
                IncludeSubdirectories = IncludeSubdirectories,
                PollingIntervalSeconds = PollingIntervalSeconds,
                PollingOverlapSeconds = PollingOverlapSeconds,
                // 保留不在界面上暴露的隐式参数
                WatcherBufferSizeBytes = _control.Options.WatcherBufferSizeBytes,
                AutoStart = _control.Options.AutoStart,
            };

        private Dictionary<string, double> BuildGapThresholdDict()
        {
            var rows = GapThresholdRows;
            // rows 顺序固定：0=1-X, 1=1-Y, 2=2-X, 3=2-Y, 4=3-X, 5=3-Y, 6=4-X, 7=4-Y
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
                ["Gap45Limit"] = Gap45Limit,
                ["Gap46Limit"] = Gap46Limit,
                ["Gap47Limit"] = Gap47Limit,
            };
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
    }

    /// <summary>
    /// 单个视野类型的间隙判断阈值行，供 DataGrid 绑定使用。
    /// 停机状态下（<see cref="MonitorControlViewModel.CanEdit"/> == true）单元格可直接编辑。
    /// </summary>
    public sealed class GapThresholdRowItem : INotifyPropertyChanged
    {
        /// <summary>视野类型标识，如 "1-X"、"2-Y"（只读，用作行标签）。</summary>
        public string ViewType { get; }

        /// <summary>该视野是否参与负极差判断（1-X/1-Y/4-X/4-Y 为 true）。</summary>
        public bool HasNegDiff { get; }

        private double _ohLower,
            _ohUpper,
            _negDiff,
            _fjEnd,
            _posEnd,
            _posMiddle;

        /// <summary>OH 下限</summary>
        public double OhLower
        {
            get => _ohLower;
            set
            {
                _ohLower = value;
                OnPropertyChanged();
            }
        }

        /// <summary>OH 上限</summary>
        public double OhUpper
        {
            get => _ohUpper;
            set
            {
                _ohUpper = value;
                OnPropertyChanged();
            }
        }

        /// <summary>负极差阈值（HasNegDiff 为 false 时此列不可编辑）</summary>
        public double NegDiff
        {
            get => _negDiff;
            set
            {
                _negDiff = value;
                OnPropertyChanged();
            }
        }

        /// <summary>负极边缘间隙阈值</summary>
        public double FjEnd
        {
            get => _fjEnd;
            set
            {
                _fjEnd = value;
                OnPropertyChanged();
            }
        }

        /// <summary>正极边缘间隙阈值</summary>
        public double PosEnd
        {
            get => _posEnd;
            set
            {
                _posEnd = value;
                OnPropertyChanged();
            }
        }

        /// <summary>正极中间间隙阈值</summary>
        public double PosMiddle
        {
            get => _posMiddle;
            set
            {
                _posMiddle = value;
                OnPropertyChanged();
            }
        }

        public GapThresholdRowItem(
            string viewType,
            bool hasNegDiff,
            double ohLower,
            double ohUpper,
            double negDiff,
            double fjEnd,
            double posEnd,
            double posMiddle
        )
        {
            ViewType = viewType;
            HasNegDiff = hasNegDiff;
            _ohLower = ohLower;
            _ohUpper = ohUpper;
            _negDiff = negDiff;
            _fjEnd = fjEnd;
            _posEnd = posEnd;
            _posMiddle = posMiddle;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(
            [System.Runtime.CompilerServices.CallerMemberName] string? name = null
        ) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
