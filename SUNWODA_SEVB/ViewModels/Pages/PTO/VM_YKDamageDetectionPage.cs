using DocumentFormat.OpenXml.Bibliography;
using HalconDotNet;
using HandyControl.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using OpenCvSharp;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SharpCompress.Common;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.PTO;
using SUNWODA_SEVB.Data.Repositories;
using SUNWODA_SEVB.Tool.Helper;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Yolo8Core;
using ZZYingYanAICore;
using static Yolo8Core.BaseClass;
using MessageBox = HandyControl.Controls.MessageBox;

namespace SUNWODA_SEVB.ViewModels.Pages.PTO
{
    /// <summary>
    /// 雅康叠片机损
    /// </summary>
    [Module("VM_YKDamageDetectionPage", "雅康叠片机损AI检测系统v1.0")]
    public class VM_YKDamageDetectionPage : ViewModelBase
    {
        private IPredictor? StationAmodle;
        private IPredictor? StationBmodle;

        #region 命令属性定义

        // 文件选择命令
        public ICommand File_Button { get; private set; } = null!;

        // 文件夹选择命令
        public ICommand Folder_Button { get; private set; } = null!;

        //查看结果命令
        public ICommand ViewResultCommand { get; private set; } = null!;

        //跳转存储命令
        public ICommand JumpToStorageCommand { get; private set; } = null!;

        // 清空列表命令
        public ICommand ClearListCommand { get; private set; } = null!;
        // 统计清零命令
        public ICommand ClearStatisticsCommand { get; private set; } = null!;

        // 鼠标滚轮缩放
        public ICommand ZoomRealTimeImageCommand { get; set; } = null!;
        // 放大按钮
        public ICommand ZoomInRealTimeCommand { get; set; } = null!;
        // 缩小按钮
        public ICommand ZoomOutRealTimeCommand { get; set; } = null!;
        // 重置缩放
        public ICommand ResetRealTimeImageZoomCommand { get; set; } = null!;
        //添加停止检测命令
        public ICommand StopDetectionCommand { get; private set; } = null!;
        //选择绘制图片
        public ICommand SelectNegativeRoiImageCommand { get; set; } = null!;
        //删除绘制区域
        public ICommand DeleteNegativeRoiCommand { get; set; } = null!;
        //保存绘制区域
        public ICommand SaveNegativeRoiCommand { get; set; } = null!;
        //查看绘制区域
        public ICommand ViewNegativeRoiCommand { get; set; } = null!;
        // 选择正极绘制图片
        public ICommand SelectPositiveRoiImageCommand { get; set; } = null!;
        // 删除正极绘制区域
        public ICommand DeletePositiveRoiCommand { get; set; } = null!;
        // 保存正极绘制区域
        public ICommand SavePositiveRoiCommand { get; set; } = null!;
        // 查看正极绘制区域
        public ICommand ViewPositiveRoiCommand { get; set; } = null!;
        //取消绘制
        public ICommand CancelDrawRoiCommand { get; set; } = null!;

        // 图像交互命令
        // 鼠标按下命令（开始拖动）
        public ICommand ImageMouseDownCommand { get; set; } = null!;
        // 鼠标移动命令
        public ICommand ImageMouseMoveCommand { get; set; } = null!;
        // 鼠标松开命令
        public ICommand ImageMouseUpCommand { get; set; } = null!;


        public ICommand SaveUserIdCommand { get; set; } = null!;
        public ICommand DeleteUserIdCommand { get; set; } = null!;

        // 增减命令
        public ICommand IncrementMaxImagesCommand { get; set; } = null!;
        public ICommand DecrementMaxImagesCommand { get; set; } = null!;
        public ICommand IncrementTimeoutCommand { get; set; } = null!;
        public ICommand DecrementTimeoutCommand { get; set; } = null!;
        public ICommand SaveMaxImagesCommand { get; set; } = null!;
        public ICommand SaveTimeoutCommand { get; set; } = null!;

        // 监听路径选择命令
        public ICommand SelectMonitorFolderCommandA { get; set; } = null!;
        public ICommand SelectMonitorFolderCommandB { get; set; } = null!;

        // 参数管控新增命令
        public ICommand AddRow { get; set; } = null!;
        public ICommand DeleteRow { get; set; } = null!;
        public ICommand SaveData { get; set; } = null!;

        public ICommand InvalidateStorageCacheCommand { get; set; } = null!;

        public enum Station { A, B }

        private sealed class StationRunContext
        {
            public string Name { get; }

            public ConcurrentQueue<string> Queue { get; } = new();
            public ConcurrentDictionary<string, DateTime> ProcessedFiles { get; } = new();

            public AutoResetEvent QueueEvent { get; } = new(false);
            public SemaphoreSlim Semaphore { get; }

            public object QueueLock { get; } = new();
            public object ProcessingLock { get; } = new();

            public volatile bool IsThreadTerminating;
            public bool IsThreadRunning;
            public bool IsMonitorRunning;
            public bool IsQueueRunning;
            // 断线自动恢复防重入：0=空闲, 1=恢复中（配合 Interlocked 使用）
            public int RecoveryFlag;

            public CancellationTokenSource? Cts;
            public Task? ProcessingTask;
            public FileSystemWatcher? RootWatcher;

            public StationRunContext(string name, int maxConcurrent)
            {
                Name = name;
                Semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
            }
        }

        private readonly StationRunContext _ctxA;
        private readonly StationRunContext _ctxB;

        private readonly object _watcherLock = new();
        private readonly object _stationCountLock = new();

        private int _runningStationsCount = 0;

        private const int MaxConcurrentPerStation = 4;
        private const int ModelPoolSizePerStation = 5;

        private DateTime _lastClearTimestamp = DateTime.MinValue;
        #endregion


        // 定义CSV操作的线程锁
        private readonly object _csvLock = new object();
        // 存储当前检测任务的CSV文件路径
        private string? _currentCsvFilePath;
        // 全局内存缓存：键=相机编号（"1"-"4"），值=检测用的filled Region（DelRegion）
        private Dictionary<string, HObject> _detectionRegionCache = new Dictionary<string, HObject>();
        // 检测状态标志
        private bool _isDetecting = false;
        // 用于取消检测任务的令牌
        private CancellationTokenSource? _cts;

        // 缓存：路径 → 极性
        private readonly ConcurrentDictionary<string, string> _polarityCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 缓存待写入CSV的数据（线程安全队列）
        private readonly ConcurrentQueue<string[]> _csvCacheQueue = new ConcurrentQueue<string[]>();
        //离线缓存路径
        private string? _csvCacheTempPathOffline;
        // 在线模式日期记录
        private string _currentCsvDateOnline = string.Empty;
        // 离线模式：记录启动时间
        private DateTime? _offlineStartTime;
        // 离线模式：记录当前CSV路径
        private string? _currentOfflineCsvFilePath;

        // 标记是否正在执行“缓存补写”
        private bool _isFlushingCache = false;
        // 缓存持久化路径
        private string? _csvCacheTempPath;
        // 配置文件路径
        private string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "set_config.json");

        // 报表设置相关属性
        // 配置文件路径
        //private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "PushUserIdConfig.json");
        private readonly ILoggerService<VM_YKDamageDetectionPage> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPLCService _PLCService;


        private string StatisticsModuleCode = "";
        private static readonly DateTime TotalStatisticsDate = new DateTime(1900, 1, 1);


        // 输入的新UserID
        private string _newUserId = string.Empty;
        public string NewUserId
        {
            get => _newUserId;
            set
            {
                _newUserId = value;
                OnPropertyChanged(nameof(NewUserId));
                OnPropertyChanged(nameof(CanSaveUserId));
            }
        }

        // 已保存的UserID列表
        private ObservableCollection<string> _savedUserIds = new ObservableCollection<string>();
        public ObservableCollection<string> SavedUserIds
        {
            get => _savedUserIds;
            set
            {
                _savedUserIds = value;
                OnPropertyChanged(nameof(SavedUserIds));
            }
        }

        // 是否可保存（10位数字 + 未重复）
        public bool CanSaveUserId
        {
            get => !string.IsNullOrWhiteSpace(NewUserId)
                   && NewUserId.Length == 10
                   && NewUserId.All(char.IsDigit)
                   && !SavedUserIds.Contains(NewUserId);
        }

        public int MaxImagesPerReport
        {
            get { return _maxImagesPerReport; }
            set
            {
                // 边界检查：1-50之间的整数
                var newValue = Math.Max(1, Math.Min(50, value));
                if (SetProperty(ref _maxImagesPerReport, newValue))
                {
                    SaveConfig();
                }
            }
        }
        private int _maxImagesPerReport = 10; // 默认值

        public int ReportTimeoutMinutes
        {
            get { return _reportTimeoutMinutes; }
            set
            {
                // 边界检查：1-1440之间的整数（24小时）
                var newValue = Math.Max(1, Math.Min(1440, value));
                if (SetProperty(ref _reportTimeoutMinutes, newValue))
                {
                    SaveConfig();

                    // 重新初始化定时器
                    if (_reportTimer != null)
                    {
                        _reportTimer.Stop();
                        _reportTimer.Interval = ReportTimeoutMinutes * 60 * 1000;
                        if (_ngImageDict.Count > 0)
                        {
                            _reportTimer.Start();
                        }
                    }
                }
            }
        }

        private int _reportTimeoutMinutes = 30;

        // 存储NG图片及参数的实体类
        private class NgImageInfo
        {
            public string? ImagePath { get; set; }
            public string? Shift { get; set; } // 班次
            public string? Polarity { get; set; } // 正负极
            public string? Camera { get; set; } // 相机编号
            public string? CellCode { get; set; } // 电芯编码
            public string? ImageName { get; set; } // 图片名
            public string? Station { get; set; }
            public DateTime DetectionTime { get; set; } // 检测时间
        }

        // 修改队列结构：按电芯编码分组存储
        private readonly Dictionary<string, List<NgImageInfo>> _ngImageDict = new Dictionary<string, List<NgImageInfo>>();
        private readonly Queue<string> _cellCodeQueue = new Queue<string>(); // 记录所有电芯编码的顺序
        private readonly HashSet<string> _cellCodeSet = new HashSet<string>(); // 用于快速查找和去重
        private readonly object _queue1Lock = new object(); // 队列锁
        private System.Timers.Timer? _reportTimer; // 超时生成定时器
        //private const int MaxImagesPerReport = 10; // 每报表最多9张图
        private const double TimeoutSeconds = 60; // 超时时间（秒）

        private bool _isOfflineDetectionCompleted = false; //标记离线检测是否已完成

        // 缺陷配置文件路径
        private static readonly string ConfigFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string DefectFilePath = Path.Combine(ConfigFolder, "DefectParameters.json");



        #region 双工位


        //图片文件保存缓存字段
        private string? _cachedRootDirectory = null;
        private DateTime _rootDirLastChecked = DateTime.MinValue;
        private const int ROOT_DIR_CACHE_MINUTES = 5; // 每5分钟重新检测一次
        private const long MIN_FREE_SPACE_BYTES = 10L * 1024 * 1024 * 1024;
        private bool _hasNotifiedStorageDegraded = false; //避免重复弹窗的标志位
        private readonly object _storageDegradedLock = new object();
        private string? _lastUsedRootDirectory = null; // 记录上次使用的根目录，用于检测切换（仅切换时才打日志）
        private readonly HashSet<string> _warnedDrives = new HashSet<string>(StringComparer.OrdinalIgnoreCase); //已推送过警告的盘符集合


        // 工位监听文件路径目录
        private string? _selectedMonitorFolderPathA;
        public string? SelectedMonitorFolderPathA
        {
            get => _selectedMonitorFolderPathA;
            set => SetMonitorFolderPath(Station.A, value);
        }

        private string? _selectedMonitorFolderPathB;
        public string? SelectedMonitorFolderPathB
        {
            get => _selectedMonitorFolderPathB;
            set => SetMonitorFolderPath(Station.B, value);
        }

        private void SetMonitorFolderPath(Station station, string? value)
        {
            if (station == Station.A)
            {
                if (_selectedMonitorFolderPathA == value) return;
                _selectedMonitorFolderPathA = value;
                OnPropertyChanged(nameof(SelectedMonitorFolderPathA));
            }
            else
            {
                if (_selectedMonitorFolderPathB == value) return;
                _selectedMonitorFolderPathB = value;
                OnPropertyChanged(nameof(SelectedMonitorFolderPathB));
            }

            var onlineKeys = _polarityCache.Keys
                .Where(k => k.StartsWith("online|", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in onlineKeys)
            {
                _polarityCache.TryRemove(key, out _);
            }

            SaveConfig();
        }

        private string? GetMonitorFolderPath(Station station)
        {
            return station == Station.A
                ? SelectedMonitorFolderPathA
                : SelectedMonitorFolderPathB;
        }

        //当前在线监控的工位（A/B，默认空）
        private string? _currentMonitoringStation;
        public string? CurrentMonitoringStation
        {
            get => _currentMonitoringStation;
            set { _currentMonitoringStation = value; OnPropertyChanged(); }
        }

        //在线检测开关
        private bool _isOnlineDetectionEnabledA;
        public bool IsOnlineDetectionEnabledA
        {
            get => _isOnlineDetectionEnabledA;
            set => SetOnlineDetectionEnabled(Station.A, value);
        }

        private bool _isOnlineDetectionEnabledB;
        public bool IsOnlineDetectionEnabledB
        {
            get => _isOnlineDetectionEnabledB;
            set => SetOnlineDetectionEnabled(Station.B, value);
        }

        // 在线检测开关是否可用
        private bool _isOnlineToggleEnabledA = true;
        public bool IsOnlineToggleEnabledA
        {
            get => _isOnlineToggleEnabledA;
            set
            {
                if (_isOnlineToggleEnabledA == value) return;
                _isOnlineToggleEnabledA = value;
                OnPropertyChanged();
            }
        }

        private bool _isOnlineToggleEnabledB = true;
        public bool IsOnlineToggleEnabledB
        {
            get => _isOnlineToggleEnabledB;
            set
            {
                if (_isOnlineToggleEnabledB == value) return;
                _isOnlineToggleEnabledB = value;
                OnPropertyChanged();
            }
        }

        private bool GetOnlineDetectionEnabled(Station station)
        {
            return station == Station.A
                ? _isOnlineDetectionEnabledA
                : _isOnlineDetectionEnabledB;
        }

        private void SetOnlineDetectionBacking(Station station, bool value)
        {
            if (station == Station.A)
            {
                _isOnlineDetectionEnabledA = value;
                OnPropertyChanged(nameof(IsOnlineDetectionEnabledA));
            }
            else
            {
                _isOnlineDetectionEnabledB = value;
                OnPropertyChanged(nameof(IsOnlineDetectionEnabledB));
            }
        }

        private void SetOnlineToggleEnabled(Station station, bool value)
        {
            if (station == Station.A)
            {
                IsOnlineToggleEnabledA = value;
            }
            else
            {
                IsOnlineToggleEnabledB = value;
            }
        }

        private string GetOnlineDetectionPropertyName(Station station)
        {
            return station == Station.A
                ? nameof(IsOnlineDetectionEnabledA)
                : nameof(IsOnlineDetectionEnabledB);
        }


        /// <summary>
        /// 在线检测开关逻辑
        /// </summary>
        /// <param name="station"></param>
        /// <param name="value"></param>
        private void SetOnlineDetectionEnabled(Station station, bool value)
        {
            if (GetOnlineDetectionEnabled(station) == value) return;

            var ctx = GetContext(station);
            string stationName = ctx.Name;

            if (!value)
            {
                StopOnlineMonitoring(station);

                SetOnlineDetectionBacking(station, false);
                OnPropertyChanged(nameof(CanOperate));
                OnPropertyChanged(nameof(CanOperateA));
                OnPropertyChanged(nameof(CanOperateB));

                SetOnlineToggleEnabled(station, true);
                UpdateButtonStates();
                SaveConfig();

                AddLog($"[在线检测-{stationName}] ✅ 已关闭");
                return;
            }

            if (ctx.IsMonitorRunning)
            {
                MessageBox.Show(
                    $"{stationName}工位监控已在运行！",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string? folderPath = GetMonitorFolderPath(station);
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                if (!_isLoadingConfig)
                {
                    MessageBox.Show(
                        $"请先选择有效的{stationName}工位监控文件夹！",
                        "路径无效",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    AddLog($"[启动] {stationName}工位监控路径无效，跳过自动启动", CoreLogLevel.Warn);
                }

                SetOnlineDetectionBacking(station, false);
                MarkStationCheckedForSearch(station);
                return;
            }

            lock (_queue1Lock)
            {
                _isOfflineDetectionCompleted = false; // 确保在线心跳不会被离线残留状态拦截

                // 如果当前是第一个启动的在线工位，且队列完全为空，在这里确保报表时钟和环境准备就绪
                if (_runningStationsCount == 0 && _cellCodeQueue.Count == 0)
                {
                    InitReportFunction();
                    _reportTimer?.Stop();
                }
            }

            SetOnlineDetectionBacking(station, true);
            SetOnlineToggleEnabled(station, false);

            OnPropertyChanged(nameof(CanOperate));
            OnPropertyChanged(nameof(CanOperateA));
            OnPropertyChanged(nameof(CanOperateB));

            IsOfflineProgressVisible = Visibility.Collapsed;

            AddLog($"[在线检测-{stationName}] ⏳ 正在启动...");

            _ = Task.Run(async () =>
            {
                try
                {
                    await StartOnlineMonitoringAsync(station);

                    await UpdateUIStateAsync(() =>
                    {
                        lock (_stationCountLock)
                        {
                            _runningStationsCount++;
                        }

                        SetOnlineToggleEnabled(station, true);
                        OnPropertyChanged(nameof(CanOperate));
                        OnPropertyChanged(nameof(CanOperateA));
                        OnPropertyChanged(nameof(CanOperateB));

                        UpdateButtonStates();
                        SaveConfig();

                        AddLog($"[在线检测-{stationName}] ✅ 启动成功");
                    });
                }
                catch (Exception ex)
                {
                    await UpdateUIStateAsync(() =>
                    {
                        SetOnlineDetectionBacking(station, false);
                        SetOnlineToggleEnabled(station, true);

                        OnPropertyChanged(nameof(CanOperate));
                        OnPropertyChanged(nameof(CanOperateA));
                        OnPropertyChanged(nameof(CanOperateB));

                        UpdateButtonStates();

                        AddLog($"[在线检测-{stationName}] ❌ 启动失败: {ex.Message}", CoreLogLevel.Error);

                        MessageBox.Show(
                            $"{stationName}工位启动监控失败：\n{ex.Message}",
                            "启动异常",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
            });
        }
        /// <summary>
        /// 标记工位已检查以供搜索
        /// </summary>
        /// <param name="station"></param>
        private void MarkStationCheckedForSearch(Station station)
        {
            if (station == Station.A)
            {
                if (!IsStationA)
                {
                    __isStationA = true;
                    OnPropertyChanged(nameof(IsStationA));
                }
            }
            else
            {
                if (!IsStationB)
                {
                    _isStationB = true;
                    OnPropertyChanged(nameof(IsStationB));
                }
            }
        }
        /// <summary>
        /// 启动在线监控
        /// </summary>
        /// <param name="station"></param>
        /// <returns></returns>
        private async Task StartOnlineMonitoringAsync(Station station)
        {
            var ctx = GetContext(station);
            string stationName = ctx.Name;

            await Task.Run(() =>
            {
                bool alreadyRunning;

                lock (_stationCountLock)
                {
                    alreadyRunning = ctx.IsMonitorRunning;

                    if (!alreadyRunning)
                    {
                        CurrentMonitoringStation = stationName;
                        ctx.IsThreadTerminating = false;
                        ctx.IsThreadRunning = true;

                        StartRootWatcher(station);

                        ctx.IsMonitorRunning = true;
                    }
                }

                if (alreadyRunning)
                {
                    UpdateUIState(() =>
                        AddLog($"[在线检测-{stationName}] 监控已在运行，忽略重复启动"));
                    return;
                }

                UpdateUIState(() =>
                    AddLog($"[在线检测-{stationName}] 监控已启动 路径: {GetMonitorFolderPath(station)}"));

                StartConcurrentProcessing(station);
            });
        }


        private void StartRootWatcher(Station station)
        {
            var ctx = GetContext(station);
            string stationName = ctx.Name;
            string? folderPath = GetMonitorFolderPath(station);

            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                ctx.IsMonitorRunning = false;
                ctx.IsThreadRunning = false;

                throw new DirectoryNotFoundException(
                    $"{stationName}工位监控文件夹不存在，请先选择有效文件夹：{folderPath}");
            }

            lock (_watcherLock)
            {
                if (ctx.RootWatcher != null)
                {
                    ctx.RootWatcher.EnableRaisingEvents = false;
                    ctx.RootWatcher.Dispose();
                    ctx.RootWatcher = null;
                }

                var watcher = new FileSystemWatcher(folderPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName,
                    InternalBufferSize = 64 * 1024
                };

                watcher.Filters.Add("*.jpg");
                watcher.Filters.Add("*.jpeg");
                watcher.Filters.Add("*.bmp");
                watcher.Filters.Add("*.png");

                watcher.Created += (_, e) => OnFileCreated(station, e);
                watcher.Error += (_, e) => OnWatcherError(station, e);

                watcher.EnableRaisingEvents = true;
                ctx.RootWatcher = watcher;

                AddLog($"[{stationName}工位监听器] 已启动 - 路径: {folderPath}");
            }
        }

        private void OnFileCreated(Station station, FileSystemEventArgs e)
        {
            var ctx = GetContext(station);

            _ = Task.Run(async () =>
            {
                await ProcessNewFileAsync(
                    e.FullPath,
                    ctx.ProcessedFiles,
                    ctx.Name);
            });
        }

        private void OnWatcherError(Station station, ErrorEventArgs e)
        {
            var ctx = GetContext(station);
            string stationName = ctx.Name;

            Exception ex = e.GetException();

            AddLog($"[{stationName}工位-监控错误] 文件监控异常: {ex.Message}，启动自动恢复流程...", CoreLogLevel.Error);

            if (ex is InternalBufferOverflowException)
            {
                AddLog(
                    $"[{stationName}工位] 检测到缓冲区溢出，说明短时间内文件过多。",
                    CoreLogLevel.Warn);
            }

            TriggerAutoRecovery(station);
        }

        /// <summary>
        /// 监控断线后的自动恢复：持续探测路径可用性并重连，直到成功或用户主动关闭在线检测。
        /// 关键点：路径没回来时只“探活+等待”，不反复 new Watcher；路径一回来立刻重连，实现快速响应。
        /// </summary>
        private void TriggerAutoRecovery(Station station)
        {
            var ctx = GetContext(station);

            // 防重入：已有恢复循环在跑就直接返回
            if (Interlocked.CompareExchange(ref ctx.RecoveryFlag, 1, 0) != 0)
            {
                AddLog($"[{ctx.Name}工位] 自动恢复已在进行中，忽略本次触发");
                return;
            }

            _ = Task.Run(async () =>
            {
                string stationName = ctx.Name;
                int attempt = 0;

                const int BaseDelayMs = 3000;   // 起始 3 秒
                const int MaxDelayMs = 30000;  // 最长 30 秒（封顶，保证路径恢复后最多 30 秒内重连）
                const int NotifyEvery = 10;     // 每 10 次推送一次飞书，避免刷屏

                try
                {
                    // 1) 先把损坏的监控彻底停掉（释放 watcher、取消队列线程）
                    StopOnlineMonitoring(station);

                    // 2) 持续重试，直到成功或用户关闭
                    while (true)
                    {
                        if (!GetOnlineDetectionEnabled(station))
                        {
                            AddLog($"[{stationName}工位] 在线检测已关闭，终止自动恢复");
                            return;
                        }

                        attempt++;
                        int delay = Math.Min(BaseDelayMs * attempt, MaxDelayMs);  // 退避
                        await Task.Delay(delay).ConfigureAwait(false);

                        if (!GetOnlineDetectionEnabled(station))
                        {
                            AddLog($"[{stationName}工位] 在线检测已关闭，终止自动恢复");
                            return;
                        }

                        // 3) 探活：路径是否可访问（网络盘/共享重连的核心判断）
                        string? path = GetMonitorFolderPath(station);
                        bool pathOk;
                        try { pathOk = !string.IsNullOrWhiteSpace(path) && Directory.Exists(path); }
                        catch { pathOk = false; }

                        if (!pathOk)
                        {
                            if (attempt == 1 || attempt % NotifyEvery == 0)
                            {
                                AddLog($"[{stationName}工位] 监控路径仍不可访问（第 {attempt} 次探测），继续等待恢复：{path}", CoreLogLevel.Warn);
                                PushMessageToFeishu($"[{stationName}工位] 监控路径断开，正在持续等待恢复（第 {attempt} 次）。请检查 AI 电脑与供应商电脑的网络/共享连接。路径：{path}");
                            }
                            continue;   // 路径没回来，接着探测
                        }

                        // 4) 路径恢复 → 尝试重连
                        try
                        {
                            AddLog($"[{stationName}工位] 检测到路径恢复，尝试重连监控（第 {attempt} 次）...");
                            await StartOnlineMonitoringAsync(station).ConfigureAwait(false);

                            lock (_stationCountLock) { _runningStationsCount++; }

                            AddLog($"[{stationName}工位] ✅ 监控已自动恢复（累计尝试 {attempt} 次）");
                            PushMessageToFeishu($"[{stationName}工位] ✅ 监控已自动恢复正常运行。");
                            return;   // 成功，退出
                        }
                        catch (Exception ex)
                        {
                            // 刚探测到路径、创建 watcher 时又掉线 → 清理后继续重试
                            StopOnlineMonitoring(station);

                            if (attempt == 1 || attempt % NotifyEvery == 0)
                            {
                                AddLog($"[{stationName}工位] 第 {attempt} 次重连失败：{ex.Message}，继续重试...", CoreLogLevel.Error);
                                PushMessageToFeishu($"[{stationName}工位] 监控重连失败（第 {attempt} 次）：{ex.Message}，将持续重试。");
                            }
                            // 继续循环
                        }
                    }
                }
                catch (Exception fatal)
                {
                    AddLog($"[{stationName}工位] 自动恢复循环异常终止：{fatal.Message}", CoreLogLevel.Error);
                }
                finally
                {
                    Interlocked.Exchange(ref ctx.RecoveryFlag, 0);  // 释放标志，允许下次断线再次触发
                }
            });
        }

        private void StartConcurrentProcessing(Station station)
        {
            var ctx = GetContext(station);
            string stationName = ctx.Name;

            lock (ctx.ProcessingLock)
            {
                if (ctx.IsQueueRunning)
                {
                    AddLog($"[{stationName}队列] 队列处理已在运行，跳过重复启动");
                    return;
                }

                if (ctx.ProcessingTask != null && !ctx.ProcessingTask.IsCompleted)
                {
                    AddLog($"[{stationName}队列] 处理线程已在运行，跳过重复启动");
                    return;
                }

                ctx.Cts?.Cancel();
                ctx.Cts?.Dispose();
                ctx.Cts = new CancellationTokenSource();

                CancellationToken token = ctx.Cts.Token;

                ctx.ProcessingTask = Task.Run(async () =>
                {
                    await ProcessQueueConcurrent(station, token).ConfigureAwait(false);
                }, token);
            }
        }


        private async Task ProcessQueueConcurrent(Station station, CancellationToken token)
        {
            var ctx = GetContext(station);
            string stationName = ctx.Name;

            const int QueueWarningThreshold = 200;
            const int QueueCriticalThreshold = 600;

            int lastAlertCount = 0;

            ctx.IsQueueRunning = true;
            AddLog($"{stationName}队列线程启动");

            // 记录在途检测任务，停机时等它们跑完，避免半途释放模型
            var inFlight = new ConcurrentDictionary<Task, byte>();

            try
            {
                while (!token.IsCancellationRequested && !ctx.IsThreadTerminating)
                {
                   
                    string? filePath = null;
                    int currentQueueCount;

                    lock (ctx.QueueLock)
                    {
                        ctx.Queue.TryDequeue(out filePath);
                        currentQueueCount = ctx.Queue.Count;
                    }

                    HandleQueueBacklogWarning(
                        stationName,
                        currentQueueCount,
                        QueueWarningThreshold,
                        QueueCriticalThreshold,
                        ref lastAlertCount);

                    if (filePath == null)
                    {
                        int signaled = WaitHandle.WaitAny(
                            new WaitHandle[] { ctx.QueueEvent, token.WaitHandle },
                            TimeSpan.FromSeconds(30));

                        if (signaled == WaitHandle.WaitTimeout) continue;
                        if (token.IsCancellationRequested || ctx.IsThreadTerminating) break;
                        continue;
                    }

                    if (!File.Exists(filePath))
                    {
                        AddLog($"[{stationName}工位-队列] {Path.GetFileName(filePath)} 已删除,路径不存在，跳过");
                        continue;
                    }

                    await ctx.Semaphore.WaitAsync(token).ConfigureAwait(false);

                    string capturedPath = filePath;
                    Task worker = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessSingleQueuedFileAsync(station, capturedPath).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            AddLog($"[{stationName}工位-检测异常] {Path.GetFileName(capturedPath)}：{ex.Message}",
                                CoreLogLevel.Error);
                        }
                        finally
                        {
                            ctx.Semaphore.Release();   // 名额一定要还
                        }
                    }, CancellationToken.None);          // 已派发的任务允许跑完，不被 token 强行打断

                    inFlight.TryAdd(worker, 0);
                    _ = worker.ContinueWith(t => inFlight.TryRemove(t, out _), TaskScheduler.Default);        
                }
            }
            catch (OperationCanceledException)
            {
                AddLog($"{stationName}队列线程收到取消请求并退出");       
            }
            catch (Exception ex)
            {
                AddLog($"{stationName}队列线程处理过程中出现异常：{ex.Message}", CoreLogLevel.Error);
            }
            finally
            {
                try
                {
                    var remaining = inFlight.Keys.ToArray();
                    if (remaining.Length > 0)
                    {
                        AddLog($"{stationName}队列线程退出中，等待 {remaining.Length} 个在途任务完成...");
                        await Task.WhenAll(remaining).ConfigureAwait(false);
                    }
                }
                catch { }

                ctx.IsQueueRunning = false;
                AddLog($"{stationName}队列线程已终止");
            }
        }

        private void HandleQueueBacklogWarning(
            string stationName,
            int currentQueueCount,
            int warningThreshold,
            int criticalThreshold,
            ref int lastAlertCount)
        {
            if (currentQueueCount <= 0)
            {
                return;
            }

            bool shouldAlert =
                Math.Abs(currentQueueCount - lastAlertCount) >
                Math.Max(10, warningThreshold / 10);

            if (!shouldAlert)
            {
                return;
            }

            if (currentQueueCount >= criticalThreshold)
            {
                AddLog($"🔴 [{stationName}队列严重告警] 队列积压 {currentQueueCount} 个文件，超过临界值 {criticalThreshold}！");
                Growl.Warning($"检测队列积压 {currentQueueCount} 个文件，超过临界值 {criticalThreshold}！");
                PushMessageToFeishu($"[{stationName}工位队列严重告警] 检测队列积压 {currentQueueCount} 个文件，超过临界值 {criticalThreshold}！");

                lastAlertCount = currentQueueCount;
                return;
            }

            if (currentQueueCount >= warningThreshold)
            {
                AddLog($"⚠️ [{stationName}队列警告] 队列积压 {currentQueueCount} 个文件，超过警告值 {warningThreshold}");
                PushMessageToFeishu($"⚠️ [{stationName}队列警告] 队列积压 {currentQueueCount} 个文件，超过警告值 {warningThreshold}");

                lastAlertCount = currentQueueCount;
                return;
            }

            if (lastAlertCount >= warningThreshold && currentQueueCount < warningThreshold)
            {
                AddLog($"✅ [{stationName}队列恢复] 队列已降至 {currentQueueCount} 个文件，恢复正常");
                lastAlertCount = 0;
            }
        }



        #region 单张队列任务处理方法(online)
        private async Task ProcessSingleQueuedFileAsync(Station station, string filePath)
        {
            var ctx = GetContext(station);
            string stationName = ctx.Name;

            dynamic detectionData = new ExpandoObject();

            ParsePathToDynamicObject(detectionData, filePath, true);
            detectionData.Station = stationName;

            string roiKey = GetRoiKey(filePath, stationName);

            string detectResult = DamageDetection(
                filePath,
                roiKey,
                DetectionStatisticsSource.Online,
                out List<string> triggeredList);

            detectionData.TriggeredDefects = triggeredList;

            DateTime realDetectionTime = DateTime.Now;
            bool isNG = detectResult == "NG";

            AddImageToQueue(filePath, isNG, stationName, realDetectionTime, DetectionStatisticsSource.Online);

            if (detectResult.Equals("异常（空图片）", StringComparison.OrdinalIgnoreCase))
            {
                AddLog($"[{stationName}工位-结果] {Path.GetFileName(filePath)}：空图片，不统计");
                return;
            }

            bool isResultSaved =
                (detectResult == "OK" && IsSaveResultOK) ||
                (detectResult == "NG" && IsSaveResultNG);

            detectionData.ResultImagePath = isResultSaved
                ? GetResultImagePath(filePath, detectResult, true, DetectionStatisticsSource.Online)
                : null;

            detectionData.DetectionResult = detectResult;

            var totalStatistics = AddDetectionStatisticsToDatabase(detectResult,triggeredList,DetectionStatisticsSource.Online);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AddDetectionDataAndEnsureCapacity(detectionData);
                UpdateDisplayDataList();
                ApplyStatisticsToUI(totalStatistics);
            });
        }

        #endregion


        #endregion

        #region ROI绘制相关字段

        private static readonly string[] StationRoiKeys =
        {
            "A1", "A2", "A3", "A4","B1", "B2", "B3", "B4"
        };

        private readonly object _roiCacheLock = new object();
        private Dictionary<string, string> _cameraImagePaths =
            StationRoiKeys.ToDictionary(k => k, _ => string.Empty, StringComparer.OrdinalIgnoreCase);
        private string? _drawTitle;
        public string? DrawTitle
        {
            get => _drawTitle;
            set { _drawTitle = value; OnPropertyChanged(nameof(DrawTitle)); }
        }
        // 绘制ROI时的图片尺寸
        private int _drawImageWidth;
        private int _drawImageHeight;

        // 控制Flyout显示/隐藏
        private bool _isDrawFlyoutOpen;
        public bool IsDrawFlyoutOpen
        {
            get => _isDrawFlyoutOpen;
            set { _isDrawFlyoutOpen = value; OnPropertyChanged(); }
        }

        private int _currentCameraNumber;
        /// <summary>
        /// 当前选中的相机编号（1-4）
        /// </summary>
        public int CurrentCameraNumber
        {
            get => _currentCameraNumber;
            set { _currentCameraNumber = value; OnPropertyChanged(); }
        }

        // 当前显示的参考图片
        private ImageSource? _currentRoiImageSource;
        public ImageSource? CurrentRoiImageSource
        {
            get => _currentRoiImageSource;
            set { _currentRoiImageSource = value; OnPropertyChanged(); }
        }

        // A工位相机1-4的ROI状态
        private bool _hasRoiA1;
        public bool HasRoiA1 { get => _hasRoiA1; set { _hasRoiA1 = value; OnPropertyChanged(); } }
        private bool _hasRoiA2;
        public bool HasRoiA2 { get => _hasRoiA2; set { _hasRoiA2 = value; OnPropertyChanged(); } }
        private bool _hasRoiA3;
        public bool HasRoiA3 { get => _hasRoiA3; set { _hasRoiA3 = value; OnPropertyChanged(); } }
        private bool _hasRoiA4;
        public bool HasRoiA4 { get => _hasRoiA4; set { _hasRoiA4 = value; OnPropertyChanged(); } }

        // B工位相机状态
        private bool _hasRoiB1;
        public bool HasRoiB1 { get => _hasRoiB1; set { _hasRoiB1 = value; OnPropertyChanged(); } }
        private bool _hasRoiB2;
        public bool HasRoiB2 { get => _hasRoiB2; set { _hasRoiB2 = value; OnPropertyChanged(); } }
        private bool _hasRoiB3;
        public bool HasRoiB3 { get => _hasRoiB3; set { _hasRoiB3 = value; OnPropertyChanged(); } }
        private bool _hasRoiB4;
        public bool HasRoiB4 { get => _hasRoiB4; set { _hasRoiB4 = value; OnPropertyChanged(); } }


        // 记录已经提示过“未使用过滤”的相机
        private readonly ConcurrentDictionary<string, byte> _warnedNoDelRegionCameras = new();
        // 记录已经提示过“使用空区域”的相机
        private readonly ConcurrentDictionary<string, byte> _warnedGenerateFailCameras = new();
        // 记录已经提示过“兜底失败”的相机
        private readonly ConcurrentDictionary<string, byte> _warnedEmptyRegionCameras = new();
        private string? _currentRoiCameraNo;  // 当前操作的相机编号
        private InkCanvas? _inkCanvas;        // 绘制控件引用

        #region ROI辅助方法

        private static bool IsValidRoiKey(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                   && Regex.IsMatch(value.Trim(), "^[AB][1-4]$", RegexOptions.IgnoreCase);
        }

        private bool TryParseRoiKey(
            string? param,
            out string roiKey,
            out string station,
            out string cameraNo)
        {
            roiKey = string.Empty;
            station = string.Empty;
            cameraNo = string.Empty;

            if (!IsValidRoiKey(param))
            {
                return false;
            }

            roiKey = param!.Trim().ToUpperInvariant();
            station = roiKey.Substring(0, 1);
            cameraNo = roiKey.Substring(1, 1);
            return true;
        }

        private string GetRoiConfigDir()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig");
        }

        private string GetRoiHobjPath(string roiKey)
        {
            return Path.Combine(GetRoiConfigDir(), $"{roiKey}_ROI.hobj");
        }

        private string GetRoiSizePath(string roiKey)
        {
            return Path.Combine(GetRoiConfigDir(), $"{roiKey}_Size.txt");
        }

        #endregion


        #endregion

        /// <summary>
        /// 复合属性：是否处于任何检测状态
        /// </summary>
        public bool CanOperate
        {
            get => !IsFileDetecting && !IsOnlineDetectionEnabledA && !IsOnlineDetectionEnabledB;
        }

        /// <summary>
        /// A工位专属操作权限：仅受离线检测和A工位在线状态影响
        /// (用于：A工位监听路径修改、A工位相机ROI绘制)
        /// </summary>
        public bool CanOperateA
        {
            get => !IsFileDetecting && !IsOnlineDetectionEnabledA;
        }

        /// <summary>
        /// B工位专属操作权限：仅受离线检测和B工位在线状态影响
        /// (用于：B工位监听路径修改、B工位相机ROI绘制)
        /// </summary>
        public bool CanOperateB
        {
            get => !IsFileDetecting && !IsOnlineDetectionEnabledB;
        }



        #region 离线处理
        //文件选择命令
        private bool _isFileSelectEnabled = true;
        public bool IsFileSelectEnabled
        {
            get => _isFileSelectEnabled;
            private set
            {
                _isFileSelectEnabled = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        //文件夹选择命令
        private bool _isFolderSelectEnabled = true;
        public bool IsFolderSelectEnabled
        {
            get => _isFolderSelectEnabled;
            private set
            {
                _isFolderSelectEnabled = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion


        // 文件/文件夹检测是否正在运行
        private bool _isFileDetecting;
        public bool IsFileDetecting
        {
            get => _isFileDetecting;
            set
            {
                lock (_stateLock)
                {
                    if (_isFileDetecting != value)
                    {
                        if (value && IsOnlineDetectionEnabledA)
                        {
                            AddLog("[状态保护] 文件检测启动,自动关闭在线检测", CoreLogLevel.Warn);
                            Growl.Warning("检测到在线检测与文件检测冲突，已自动关闭在线检测");
                            IsOnlineDetectionEnabledA = false;
                        }

                        _isFileDetecting = value;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(CanOperate));
                        UpdateButtonStates();
                    }
                }
            }
        }


        /// <summary>
        /// 统一的按钮和界面状态更新方法（确保全自动互斥状态机一致性）
        /// </summary>
        private void UpdateButtonStates()
        {
            // 1. 判断当前的运行状态
            bool isOfflineRunning = IsFileDetecting;
            bool isOnlineRunning = IsOnlineDetectionEnabledA || IsOnlineDetectionEnabledB;

            // 2. 文件夹/文件选择按钮: 仅当【没有正在进行批量检测】且【在线监控彻底关闭】时可用
            bool canOperateFiles = !isOfflineRunning && !isOnlineRunning;
            IsFileSelectEnabled = canOperateFiles;
            IsFolderSelectEnabled = canOperateFiles;

            // 3. 在线检测开关: 仅当【未进行离线文件批量检测】时才可以操作
            IsOnlineToggleEnabledA = !isOfflineRunning;
            IsOnlineToggleEnabledB = !isOfflineRunning;

            // 4. 强制向前端 UI 刷新关联的操作权限绑定（包括工位 CheckBox 容器的 IsEnabled 绑定）
            OnPropertyChanged(nameof(CanOperate));
            OnPropertyChanged(nameof(CanOperateA));
            OnPropertyChanged(nameof(CanOperateB));

            // 5. 极端状态冲突下的自动兜底防错
            if (isOfflineRunning && isOnlineRunning)
            {
                AddLog("[状态硬冲突] 监测到系统离线批量检测与在线监控并存！触发底层硬拦截...", CoreLogLevel.Error);
                // 强制切断在线监控，保护离线批量任务不卡死
                if (IsOnlineDetectionEnabledA) StopOnlineMonitoring(Station.A);
                if (IsOnlineDetectionEnabledB) StopOnlineMonitoring(Station.B);
                IsOnlineDetectionEnabledA = false;
                IsOnlineDetectionEnabledB = false;
                MessageBox.Show("系统检测到离线和在线任务发生异常冲突，已强制进入安全防护，优先维持批量检测运行。", "系统安全拦截");
            }
        }



        // 进度条当前值（0~100）
        private double _offlineProgressValue = 0.0;
        public double OfflineProgressValue
        {
            get => _offlineProgressValue;
            set
            {
                //检查是否为有效数字
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    _offlineProgressValue = 0.0;
                }
                else
                {
                    _offlineProgressValue = value;
                }
                OnPropertyChanged();
            }
        }

        private Visibility _isOfflineProgressVisible = Visibility.Collapsed; // 默认进度条默认隐藏
        /// <summary>
        /// 离线进度条是否可见
        /// </summary>
        public Visibility IsOfflineProgressVisible
        {
            get => _isOfflineProgressVisible;
            set
            {
                _isOfflineProgressVisible = value;
                OnPropertyChanged();
            }
        }

        //进度提示文本（已完成XX%，预计还需YY）
        private string? _offlineProgressTip;
        public string? OfflineProgressTip
        {
            get => _offlineProgressTip;
            set { _offlineProgressTip = value; OnPropertyChanged(); }
        }

        private const int BatchSize = 200; // 每200张为一个批次
        private int _currentBatchCount = 0; // 当前批次已完成数量（0~200）
        private long _currentBatchTotalMs = 0; // 当前批次总耗时（毫秒）
        private double _lastBatchAvgMs = 0; // 上一个完整批次的平均耗时（毫秒/张）
        private readonly object _batchLock = new object(); // 批次数据锁
        private int _totalOfflineImageCount;
        private int _completedOfflineImageCount;
        private readonly IWebApiClient _webApiClient;
        // 进度更新间隔改为批次大小
        private const int _progressUpdateInterval = BatchSize;

        // 缩放参数
        private const double ZoomStep = 0.1;
        private const double MinScale = 0.2;
        private const double MaxScale = 5.0;

        // 图片拖动X轴偏移
        private double _realTimeImageTranslateX;
        public double RealTimeImageTranslateX
        {
            get => _realTimeImageTranslateX;
            set { _realTimeImageTranslateX = value; OnPropertyChanged(); }
        }

        // 图片拖动Y轴偏移
        private double _realTimeImageTranslateY;
        public double RealTimeImageTranslateY
        {
            get => _realTimeImageTranslateY;
            set { _realTimeImageTranslateY = value; OnPropertyChanged(); }
        }

        // 是否正在拖动
        private bool _isImageDragging;
        private System.Windows.Point _startMousePoint; // 拖动起始鼠标位置
        private double _startTranslateX; // 拖动起始X偏移
        private double _startTranslateY; // 拖动起始Y偏移

        #region 图像检索筛选属性
        //图像检索勾选A工位
        private bool __isStationA = false;
        public bool IsStationA
        {
            get => __isStationA;
            set
            {
                __isStationA = value;
                OnPropertyChanged();
                SaveConfig();

                // 勾选A工位时，自动勾选所有未勾选的相机
                if (value)
                {
                    if (!IsCameraA1) IsCameraA1 = true;
                    if (!IsCameraA2) IsCameraA2 = true;
                    if (!IsCameraA3) IsCameraA3 = true;
                    if (!IsCameraA4) IsCameraA4 = true;
                }
                else
                {
                    // 取消勾选A工位时，自动取消相机1、2、3、4
                    if (IsCameraA1) IsCameraA1 = false;
                    if (IsCameraA2) IsCameraA2 = false;
                    if (IsCameraA3) IsCameraA3 = false;
                    if (IsCameraA4) IsCameraA4 = false;
                }
                CalculateDetectionStatistics(DetectionStatisticsSource.Online);
            }
        }

        private bool _isStationB = false;

        /// <summary>B工位是否勾选</summary>
        public bool IsStationB
        {
            get => _isStationB;
            set
            {
                _isStationB = value;
                OnPropertyChanged();
                SaveConfig();

                // 勾选B工位时，自动勾选B相机
                if (value)
                {
                    if (!IsCameraB1) IsCameraB1 = true;
                    if (!IsCameraB2) IsCameraB2 = true;
                    if (!IsCameraB3) IsCameraB3 = true;
                    if (!IsCameraB4) IsCameraB4 = true;
                }
                else
                {
                    // 取消勾选B工位时，自动取消B相机
                    if (IsCameraB1) IsCameraB1 = false;
                    if (IsCameraB2) IsCameraB2 = false;
                    if (IsCameraB3) IsCameraB3 = false;
                    if (IsCameraB4) IsCameraB4 = false;
                }
                CalculateDetectionStatistics(DetectionStatisticsSource.Online);
            }
        }

        private bool _isCameraA1;
        /// <summary>相机1是否勾选</summary>
        public bool IsCameraA1
        {
            get => _isCameraA1;
            set
            {
                _isCameraA1 = value; OnPropertyChanged(); SaveConfig();
                if (!value && !IsCameraA2 && !IsCameraA3 && !IsCameraA4 && IsStationA)
                    IsStationA = false;
            }
        }

        private bool _isCameraA2;
        /// <summary>相机2是否勾选</summary>
        public bool IsCameraA2
        {
            get => _isCameraA2;
            set
            {
                _isCameraA2 = value; OnPropertyChanged(); SaveConfig();
                if (!value && !IsCameraA2 && !IsCameraA3 && !IsCameraA4 && IsStationA)
                    IsStationA = false;
            }
        }

        private bool _isCameraA3;
        /// <summary>相机3是否勾选</summary>
        public bool IsCameraA3
        {
            get => _isCameraA3;
            set
            {
                _isCameraA3 = value; OnPropertyChanged(); SaveConfig();
                if (!value && !IsCameraA2 && !IsCameraA3 && !IsCameraA4 && IsStationA)
                    IsStationA = false;
            }
        }

        private bool _isCameraA4;
        /// <summary>相机4是否勾选</summary>
        public bool IsCameraA4
        {
            get => _isCameraA4;
            set
            {
                _isCameraA4 = value; OnPropertyChanged(); SaveConfig();
                if (!value && !IsCameraA2 && !IsCameraA3 && !IsCameraA4 && IsStationA)
                    IsStationA = false;
            }
        }

        private bool _isCameraB1;
        /// <summary>相机5是否勾选</summary>
        public bool IsCameraB1
        {
            get => _isCameraB1;
            set
            {
                _isCameraB1 = value;
                OnPropertyChanged();
                SaveConfig();

                // 相机5、6、7、8都被取消时，自动取消B工位
                if (!value && !IsCameraB2 && !IsCameraB3 && !IsCameraB4 && IsStationB)
                    IsStationB = false;
            }
        }

        private bool _isCameraB2;
        /// <summary>相机6是否勾选</summary>
        public bool IsCameraB2
        {
            get => _isCameraB2;
            set
            {
                _isCameraB2 = value;
                OnPropertyChanged();
                SaveConfig();

                if (!value && !IsCameraB1 && !IsCameraB3 && !IsCameraB4 && IsStationB)
                    IsStationB = false;
            }
        }

        private bool _isCameraB3;
        /// <summary>相机7是否勾选</summary>
        public bool IsCameraB3
        {
            get => _isCameraB3;
            set
            {
                _isCameraB3 = value;
                OnPropertyChanged();
                SaveConfig();

                if (!value && !IsCameraB1 && !IsCameraB2 && !IsCameraB4 && IsStationB)
                    IsStationB = false;
            }
        }

        private bool _isCameraB4;
        /// <summary>相机8是否勾选</summary>
        public bool IsCameraB4
        {
            get => _isCameraB4;
            set
            {
                _isCameraB4 = value;
                OnPropertyChanged();
                SaveConfig();

                if (!value && !IsCameraB1 && !IsCameraB2 && !IsCameraB3 && IsStationB)
                    IsStationB = false;
            }
        }
        #endregion

        // 实时检测图缩放比例
        private double _realTimeImageScaleX = 1.0;
        public double RealTimeImageScaleX
        {
            get => _realTimeImageScaleX;
            set { _realTimeImageScaleX = value; OnPropertyChanged(); }
        }

        private double _realTimeImageScaleY = 1.0;
        public double RealTimeImageScaleY
        {
            get => _realTimeImageScaleY;
            set { _realTimeImageScaleY = value; OnPropertyChanged(); }
        }

        #region 核心数据存储
        // 完整数据集合：存储所有检测记录
        private List<dynamic> _fullDetectionDataList = new List<dynamic>();

        // 检测数据集合
        private ObservableCollection<dynamic> _detectionDataList = new ObservableCollection<dynamic>();
        public ObservableCollection<dynamic> DetectionDataList
        {
            get => _detectionDataList;
            set { _detectionDataList = value; OnPropertyChanged(); }
        }

        private dynamic? _selectedDetectionData;
        public dynamic? SelectedDetectionData
        {
            get => _selectedDetectionData;
            set { _selectedDetectionData = value; OnPropertyChanged(); }
        }
        #endregion


        #region 属性和字段定义
        private int totalNeDetectionCount;
        private int okCount;
        private int ngCount;
        private double okRate;
        private string? _imageName;
        private string? _polarity;
        private string? _cameraInformation;
        private string? _detectionResult;
        private string? _imagePath;
        private string? _resultImagePath;

        //private Dictionary<string, int> _historyDefectNGCount = new Dictionary<string, int>();  // 历史各缺陷NG数
        private readonly object _statisticsLock = new object(); //统计锁
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp"
        };
        private bool _isLoadingResultImage = false;
        private readonly object _stateLock = new object();


        // 日志集合：用Tuple<string, string>存储（Item1=时间戳，Item2=内容）
        private ObservableCollection<Tuple<string, string>> _logTable = new ObservableCollection<Tuple<string, string>>();
        public ObservableCollection<Tuple<string, string>> LogTable
        {
            get => _logTable;
            set { _logTable = value; OnPropertyChanged(); }
        }

        object? obj2;

        YingYanAI ai = new YingYanAI();
        YingYanAI aiNew = new YingYanAI();
        /// <summary>
        /// 抓点方法
        /// </summary>
        public MethodInfo? ActionGetPointMethod { get; set; }
        public MethodInfo? ModelInitMethod { get; set; }

        private HObject? originImage;

        private HObject? detectionResultImage;

        /// <summary>
        /// 原图
        /// </summary>
        public HObject? OriginImage
        {
            get => originImage;
            set
            {
                originImage?.Dispose();
                originImage = value;
                OnPropertyChanged();
            }
        }

        // 线程安全队列
        private readonly ConcurrentQueue<string> _safeDetectQueue = new ConcurrentQueue<string>();
        // 原子标记：避免多线程下状态错误
        //private volatile bool _isQueueRunning = false;
        // 文件就绪重试配置
        private const int FileReadyMaxRetry = 30; // 最多重试30次
        private const int FileReadyRetryDelay = 1000; // 每次重试间隔1秒

        /// <summary>
        /// 检测结果图
        /// </summary>
        public HObject? DetectionResultImage
        {
            get => detectionResultImage;
            set
            {
                detectionResultImage?.Dispose();
                detectionResultImage = value;
                OnPropertyChanged();
            }
        }

        // 总检测耗时
        private long _totalDetectMilliseconds = 0;
        // 已完成检测的图片数量
        private int _completedCountForTimeCalc = 0;
        //前20张不参与平均
        private const int WarmupCount = 20;
        public object?[]? zhengParam { get; set; }
        public object?[]? fuParam { get; set; }
        public object[]? acparam { get; set; }

        // 选中的图片路径
        private string? _selectedImagePath;
        private string? _selectedFolderPath;
        //结果图显示路径
        private string? _currentResultImagePath;

        // ---------- 原图存储开关 ----------
        private bool _isSaveOriginalOK;
        public bool IsSaveOriginalOK
        {
            get => _isSaveOriginalOK;
            set { _isSaveOriginalOK = value; OnPropertyChanged(); SaveConfig(); }
        }

        private bool _isSaveOriginalNG;
        public bool IsSaveOriginalNG
        {
            get => _isSaveOriginalNG;
            set { _isSaveOriginalNG = value; OnPropertyChanged(); SaveConfig(); }
        }

        // ---------- 结果图存储开关 ----------
        private bool _isSaveResultOK;
        public bool IsSaveResultOK
        {
            get => _isSaveResultOK;
            set { _isSaveResultOK = value; OnPropertyChanged(); SaveConfig(); }
        }

        private bool _isSaveResultNG;
        public bool IsSaveResultNG
        {
            get => _isSaveResultNG;
            set { _isSaveResultNG = value; OnPropertyChanged(); SaveConfig(); }
        }

        // 当前显示的结果图
        private ImageSource? _currentResultImage;
        public ImageSource? CurrentResultImage
        {
            get => _currentResultImage;
            set { _currentResultImage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 结果图显示路径
        /// </summary>
        public string? CurrentResultImagePath
        {
            get => _currentResultImagePath;
            set { _currentResultImagePath = value; OnPropertyChanged(); }
        }
        /// <summary>
        /// 图片名
        /// </summary>
        public string? ImageName
        {
            get => _imageName;
            set { _imageName = value; OnPropertyChanged(); }
        }
        /// <summary>
        /// 正负极
        /// </summary>
        public string? Polarity
        {
            get => _polarity;
            set { _polarity = value; OnPropertyChanged(); }
        }
        /// <summary>
        /// 工位信息
        /// </summary>
        public string? CameraInformation
        {
            get => _cameraInformation;
            set { _cameraInformation = value; OnPropertyChanged(); }
        }
        /// <summary>
        /// 检测结果
        /// </summary>
        public string? DetectionResult
        {
            get => _detectionResult;
            set { _detectionResult = value; OnPropertyChanged(); }
        }
        /// <summary>
        /// 图片路径
        /// </summary>
        public string? ImagePath
        {
            get => _imagePath;
            set { _imagePath = value; OnPropertyChanged(); }
        }
        /// <summary>
        /// 结果图路径
        /// </summary>
        public string? ResultImagePath
        {
            get => _resultImagePath;
            set { _resultImagePath = value; OnPropertyChanged(); }
        }
        /// <summary>
        /// OKNG数
        /// </summary>
        public double[] OKNGNum { get; set; } = new double[2];
        /// <summary>
        /// OK数
        /// </summary>
        public int OKCount
        {
            get => okCount;
            set
            {
                okCount = value;
                OnPropertyChanged();
                //SaveConfig();
            }
        }
        /// <summary>
        /// NG数
        /// </summary>
        public int NGCount
        {
            get => ngCount;
            set
            {
                ngCount = value;
                OnPropertyChanged();
                //SaveConfig();
            }
        }
        /// <summary>
        /// 负极总检测数
        /// </summary>
        public int TotalNeDetectionCount
        {
            get => totalNeDetectionCount;
            set
            {
                totalNeDetectionCount = value;
                OnPropertyChanged();
                //SaveConfig();
            }
        }
        /// <summary>
        /// 一次优率
        /// </summary>
        public double OKRate
        {
            get => okRate;
            set
            {
                okRate = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// 选择单张图片路径
        /// </summary>
        public string? SelectedImagePath
        {
            get => _selectedImagePath;
            set { _selectedImagePath = value; OnPropertyChanged(); }
        }
        /// <summary>
        /// 文件夹选择
        /// </summary>
        public string? SelectedFolderPath
        {
            get => _selectedFolderPath;
            set
            {
                _selectedFolderPath = value;
            }
        }
        private string? _currentLog;

        /// <summary>
        /// 绑定的日志属性
        /// </summary>
        public string? CurrentLog
        {
            get => _currentLog;
            set
            {
                _currentLog = value;
                OnPropertyChanged();
            }
        }

        private DetectionMode _selectedMode = DetectionMode.全检;
        public DetectionMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (SetProperty(ref _selectedMode, value))
                {
                    SaveConfig();
                }
            }
        }



        // 缺陷检测结果列表
        private List<AIResultModel>? _detectionResults;
        public List<AIResultModel>? DetectionResults
        {
            get => _detectionResults;
            set { _detectionResults = value; OnPropertyChanged(); }
        }

        #region 权限控制属性

        private bool _isEditAllowed;
        /// <summary>
        /// 是否允许编辑（用于绑定按钮的 IsEnabled）
        /// True = 非Guest用户，允许点击
        /// </summary>
        public bool IsEditAllowed
        {
            get => _isEditAllowed;
            set => SetProperty(ref _isEditAllowed, value);
        }

        private bool _isParamGridReadOnly;
        /// <summary>
        /// 表格是否只读（用于绑定 DataGrid 的 IsReadOnly）
        /// True = Guest用户，只读
        /// False = 非Guest用户，可编辑
        /// </summary>
        public bool IsParamGridReadOnly
        {
            get => _isParamGridReadOnly;
            set => SetProperty(ref _isParamGridReadOnly, value);
        }

        #endregion

        #endregion

        public VM_YKDamageDetectionPage(ILoggerService<VM_YKDamageDetectionPage> logger, IWebApiClient webApiClient, IServiceProvider serviceProvider, IPLCService PLCService)
        {
            _logger = logger;
            _webApiClient = webApiClient ?? throw new ArgumentNullException(nameof(webApiClient));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _PLCService = PLCService ?? throw new ArgumentNullException(nameof(PLCService));


            //同步初始化
            Settings.License = LicenseType.Community;
            InitializeHalconRoiRuntime();
            _ctxA = new StationRunContext("A", MaxConcurrentPerStation);
            _ctxB = new StationRunContext("B", MaxConcurrentPerStation);

            //初始化命令和基础数据
            InitializeCommands();
            InitReportFunction();
            InitParameters();
            LoadConfig();

            // 异步加载耗时资源
            _ = Task.Run(async () =>
            {
                try
                {
                    // 加载用户ID（异步）
                    await LoadUserIdsAsync();

                    // 1. 异步加载模型
                    await InitializeModelsAsync();

                    // 2. 异步加载ROI配置
                    await LoadRoiConfigsAsync();

                    // 3. 在UI线程完成最后初始化
                    await UpdateUIStateAsync(() =>
                    {
                        LoadDefectInitialData();
                        EnsureDetectionStatisticsTable();
                        LoadStatisticsFromDatabaseToUI();
                        AddLog("✅ 所有资源加载完成，软件已就绪");
                    });
                    await StartMonitoringFromConfigAsync();
                }
                catch (Exception ex)
                {
                    await UpdateUIStateAsync(() =>
                    {
                        AddLog($"❌ 资源加载失败：{ex.Message}", CoreLogLevel.Error);
                        MessageBox.Show($"初始化失败：{ex.Message}\n\n请检查模型文件和配置",
                            "启动异常", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });

        }

        /// <summary>
        /// 按工位获取上下文
        /// </summary>
        private StationRunContext GetContext(Station station)
        {
            return station == Station.A ? _ctxA : _ctxB;
        }



        #region 单张图片选择(offine)
        /// <summary>
        /// 异步选择单张检测图片（核心交互逻辑：弹窗选择→格式校验→保存设置确认→触发单文件检测）
        /// 设计特点：循环选择（容错）、格式白名单校验、保存设置可视化、异步检测不阻塞UI
        /// </summary>
        private async void SelectImageFile()
        {
            // 1. 定义支持的图片格式白名单（HashSet保证O(1)高效查找）
            var allowedExtensions = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp" };
            // 标记是否已选中合法图片并确认检测（控制循环退出）
            bool isSelected = false;
            // 2. 循环选择图片：用户选错格式/取消确认时，重新弹出选择框（容错设计）
            while (!isSelected)
            {
                var openFileDialog = new OpenFileDialog
                {
                    // 文件筛选器：先显示支持的图片格式，再显示所有文件（引导用户选择正确格式）
                    Filter = "图片文件|*.bmp;*.jpg;*.jpeg;*.png;*.gif;*.tiff;*.webp|所有文件|*.*",
                    FilterIndex = 1, // 默认选中第一个筛选器（图片文件）
                    Title = "选择检测图片（仅支持图片格式）",// 对话框标题，明确用途
                    Multiselect = false // 禁止多选，仅允许选择单张图片
                };
                // 4. 显示文件选择对话框，获取用户操作结果
                bool? dialogResult = openFileDialog.ShowDialog();
                // 用户点击“取消”/关闭对话框，退出循环
                if (dialogResult != true) break;
                // 5. 校验选中路径有效性：避免空路径/空白路径
                string selectedPath = openFileDialog.FileName;
                if (string.IsNullOrWhiteSpace(selectedPath))
                {
                    MessageBox.Show("请选择一张图片文件后再点击确定！", "未选择图片", MessageBoxButton.OK, MessageBoxImage.Warning);
                    continue;// 重新进入循环，让用户再次选择
                }
                // 6. 校验文件格式：转换为小写避免大小写敏感（如.PNG和.png）
                string fileExtension = Path.GetExtension(selectedPath).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    // 提示不支持的格式，并列出所有支持的格式，提升用户体验
                    MessageBox.Show($"所选文件格式不支持！\n请选择以下格式的图片：\n{string.Join("、", allowedExtensions)}",
                                     "格式错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    continue;// 重新进入循环，让用户选择合法格式
                }

                // 7. 拼接当前保存设置描述文本（可视化展示用户的保存配置）
                StringBuilder saveSettings = new StringBuilder();
                if (IsSaveOriginalOK) saveSettings.Append("保存OK原图；");
                if (IsSaveOriginalNG) saveSettings.Append("保存NG原图；");
                if (IsSaveResultOK) saveSettings.Append("保存OK结果图；");
                if (IsSaveResultNG) saveSettings.Append("保存NG结果图；");
                // 处理无保存设置的情况，避免空文本
                string saveInfo = saveSettings.Length > 0
                    ? $"保存设置：{saveSettings.ToString().TrimEnd('；')}"
                    : "保存设置：不保存任何图片";

                // 8. 弹出确认框：让用户确认检测及当前保存设置，防止误操作
                var confirm = MessageBox.Show(saveInfo, "确认检测", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.OK)
                {
                    continue;
                }

                // 9. 用户确认后，执行核心逻辑：标记选中状态+重置CSV路径+更新选中图片路径+触发单文件检测
                isSelected = true;// 退出循环的关键标记
                _currentCsvFilePath = null; // 重置当前CSV文件路径（单文件检测无需CSV）
                SelectedImagePath = selectedPath;// 更新选中图片路径（绑定UI/供检测逻辑使用）
                await GetDataFromSingleFile(selectedPath);// 异步执行单文件检测，不阻塞UI
            }
        }
        #endregion


        #region 文件夹选择逻辑
        /// <summary>
        /// 文件夹选择---针对离线模式
        /// 异步选择图片文件夹执行批量检测（核心流程：文件夹选择→多层校验→图片筛选→用户确认→批量检测）
        /// 设计特点：WPF原生文件夹对话框适配、多层有效性校验、筛选条件可视化、分类异常处理、状态一致性保障
        /// </summary>
        private async void SelectImageFolder()
        {
            if (IsOnlineDetectionEnabledA || IsOnlineDetectionEnabledB)
            {
                MessageBox.Show("当前【在线实时监控】正在运行中！\n请先关闭在线监控，再进行离线批量检测。",
                                "状态冲突预警", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (IsStationA == IsStationB)
            {
                string warningMsg = IsStationA
                    ? "【文件夹批量检测】不支持双工位同时并行！\n请在界面上仅勾选其中一个目标工位（A或B）后再试。"
                    : "未选中任何目标工位！\n请先在界面上勾选需要执行批量检测的工位（A工位 或 B工位）。";

                MessageBox.Show(warningMsg, "工位配置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                AddLog($"[批量检测拒绝] {warningMsg}", CoreLogLevel.Warn);
                return;
            }

            var allowedExtensions = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp" };

            try
            {
                var folderDialog = new OpenFolderDialog
                {
                    Title = $"选择【{(IsStationA ? "A" : "B")}工位】批量检测图片文件夹（支持子目录）",
                    ValidateNames = true,
                    Multiselect = false,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                };
                bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);

                if (result != true)
                {
                    ResetFolderSelectionStatus();
                    return;
                }

                string selectedFolder = folderDialog.FolderName;

                if (string.IsNullOrWhiteSpace(selectedFolder) || !Directory.Exists(selectedFolder))
                {
                    MessageBox.Show("选择的文件夹无效或不存在，请重新选择！", "无效文件夹", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ResetFolderSelectionStatus();
                    return;
                }

                List<string> allImageFiles = GetImageFilesInFolder(selectedFolder, allowedExtensions);
                UpdateFolderSelectionStatus(selectedFolder, allImageFiles.Count);

                if (allImageFiles.Count == 0)
                {
                    var retryResponse = MessageBox.Show(
                         $"所选文件夹中未发现任何图片文件！\n支持的格式：{string.Join("、", allowedExtensions)}\n是否重新选择文件夹？",
                         "无图片文件", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (retryResponse == MessageBoxResult.Yes) SelectImageFolder();
                    return;
                }

                // 按筛选条件过滤图片（工位/相机规则）
                List<string> filteredFiles = FilterImageFiles(allImageFiles);
                if (filteredFiles.Count == 0)
                {
                    MessageBox.Show($"所选文件夹中未找到符合当前所选工位（{(IsStationA ? "A工位" : "B工位")}）相对路径特征的图片！",
                            "无符合条件的图片", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                StringBuilder saveSettings = new StringBuilder();
                if (IsSaveOriginalOK) saveSettings.Append("保存OK原图；");
                if (IsSaveOriginalNG) saveSettings.Append("保存NG原图；");
                if (IsSaveResultOK) saveSettings.Append("保存OK结果图；");
                if (IsSaveResultNG) saveSettings.Append("保存NG结果图；");
                string saveInfo = saveSettings.Length > 0 ? $"保存设置：{saveSettings.ToString().TrimEnd('；')}" : "保存设置：不保存任何图片";

                var confirm = MessageBox.Show(
                     $"将对文件夹中符合筛选条件的 {filteredFiles.Count} 张图片进行离线批量检测，是否继续？\n" +
                     $"当前工位：{(IsStationA ? "A工位" : "B工位")}\n" +
                     saveInfo,
                     "确认批量检测", MessageBoxButton.OKCancel, MessageBoxImage.Question);

                if (confirm == MessageBoxResult.OK)
                {
                    _currentCsvFilePath = null;
                    IsFileDetecting = true;
                    await GetDataFromFolder(filteredFiles);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"访问文件夹失败：没有权限访问该文件夹\n{ex.Message}", "权限不足", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetFolderSelectionStatus();
            }
            catch (PathTooLongException ex)
            {
                MessageBox.Show($"文件夹路径过长：请选择路径较短的文件夹\n{ex.Message}", "路径过长", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetFolderSelectionStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"文件夹选择过程出错：{ex.Message}", "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetFolderSelectionStatus();
            }
            finally
            {
                IsFileDetecting = false;
            }
        }

        /// <summary>
        /// 并行筛选+缓存，大幅提升大量文件处理速度
        /// </summary>
        private List<string> FilterImageFiles(List<string> allImageFiles)
        {

            var filtered = new ConcurrentBag<string>();

            // 并行遍历
            Parallel.ForEach(allImageFiles, filePath =>
            {
                string station = GetStationFromImagePath(filePath);
                //string camera = GetCameraNumberFromPath(filePath);

                bool isPolarityMatch = (!IsStationA && !IsStationB) ||
                                       (IsStationA && station == "A") ||
                                       (IsStationB && station == "B");

                if (isPolarityMatch)
                {
                    filtered.Add(filePath);
                }
            });
            return filtered.ToList();
        }

        // 更新文件夹选择状态
        private void UpdateFolderSelectionStatus(string folderPath, int imageCount)
        {
            SelectedFolderPath = folderPath;
        }

        //重置文件夹选择状态
        private void ResetFolderSelectionStatus()
        {
            SelectedFolderPath = null;
        }
        #endregion

        private void InitParameters()
        {
            LogTable = new ObservableCollection<Tuple<string, string>>();
            // 初始化动态数据集合
            DetectionDataList = new ObservableCollection<dynamic>();

            UserStateChangedEvent.OnUserStateChanged += OnUserStateChanged;
            CheckAuthority();
            AddLog("系统启动，日志功能就绪"); // 初始日志

            ResetOfflineProgress();//重置离线检测进度状态
        }

        private void InitializeHalconRoiRuntime()
        {
            try
            {
                HOperatorSet.SetSystem("clip_region", "false");        
            }
            catch (Exception ex)
            {
                AddLog($"[HALCON初始化] 设置 clip_region=false 失败：{ex.Message}", CoreLogLevel.Warn);
            }
        }

        /// <summary>
        /// 初始化所有命令（从构造函数中抽取）
        /// </summary>
        private void InitializeCommands()
        {
            File_Button = new RelayCommand<object>(
                        execute: parameter => SelectImageFile(),//文件选择
                        canExecute: parameter => IsFileSelectEnabled);
            Folder_Button = new RelayCommand<object>(
                        execute: parameter => SelectImageFolder(),//文件夹选择
                        canExecute: parameter => IsFolderSelectEnabled);
            IncrementMaxImagesCommand = new RelayCommand(() => MaxImagesPerReport++);
            DecrementMaxImagesCommand = new RelayCommand(() =>
            {
                if (MaxImagesPerReport > 1) MaxImagesPerReport--;
            });

            IncrementTimeoutCommand = new RelayCommand(() => ReportTimeoutMinutes++);
            DecrementTimeoutCommand = new RelayCommand(() =>
            {
                if (ReportTimeoutMinutes > 1) ReportTimeoutMinutes--;
            });

            SaveMaxImagesCommand = new RelayCommand(SaveMaxImagesSettings);
            SaveTimeoutCommand = new RelayCommand(SaveTimeoutSettings);
            ViewResultCommand = new RelayCommand<dynamic>(ExecuteViewResult);//查看NG图片结果
            JumpToStorageCommand = new RelayCommand(ExecuteJumpToStorage);//跳转存储
            ClearListCommand = new RelayCommand(ExecuteClearList);//清空列表
            ClearStatisticsCommand = new RelayCommand(ExecuteClearStatistics);//统计清零
            StopDetectionCommand = new RelayCommand(ExecuteStopDetection);//停止检测命令
            //InitDll1();//初始化云端 本地加载注释掉这一行
            ZoomRealTimeImageCommand = new RelayCommand<MouseWheelEventArgs>(OnZoomRealTimeImage);//鼠标滚轮缩放命令
            ZoomInRealTimeCommand = new RelayCommand(OnZoomInRealTime);//图片放大
            ZoomOutRealTimeCommand = new RelayCommand(OnZoomOutRealTime);//图片缩小
            ResetRealTimeImageZoomCommand = new RelayCommand(OnResetRealTimeZoom);//图片双击还原
            ImageMouseDownCommand = new RelayCommand<MouseButtonEventArgs>(OnImageMouseDown);//鼠标左键按下命令
            ImageMouseMoveCommand = new RelayCommand<MouseEventArgs>(OnImageMouseMove);//鼠标移动命令
            ImageMouseUpCommand = new RelayCommand<MouseEventArgs>(OnImageMouseUp);//鼠标左键松开

            SelectMonitorFolderCommandA = new RelayCommand(() => SelectMonitorFolder(Station.A)); //A工位监听路径
            SelectMonitorFolderCommandB = new RelayCommand(() => SelectMonitorFolder(Station.B));//B工位监听路径

            SelectNegativeRoiImageCommand = new RelayCommand<string>(SelectStationRoiImage);//选择绘制图片
            DeleteNegativeRoiCommand = new RelayCommand(DeleteStationRoi);//弹窗-删除当前绘制内容
            SaveNegativeRoiCommand = new RelayCommand(SaveStationRoiAsHalconRegion);//弹窗-保存ROI
            ViewNegativeRoiCommand = new RelayCommand<string>(ViewStationRoi);//查看绘制结果
            CancelDrawRoiCommand = new RelayCommand(CancelDrawRoi);//弹窗-取消ROI绘制

            // 正极ROI命令初始化
            SelectPositiveRoiImageCommand = new RelayCommand<string>(SelectStationRoiImage);
            DeletePositiveRoiCommand = new RelayCommand(DeleteStationRoi);
            SavePositiveRoiCommand = new RelayCommand(SaveStationRoiAsHalconRegion);  // 复用保存逻辑
            ViewPositiveRoiCommand = new RelayCommand<string>(ViewStationRoi);

            InvalidateStorageCacheCommand = new RelayCommand(InvalidateRootDirectoryCache);

            SaveUserIdCommand = new RelayCommand(SaveUserId);
            DeleteUserIdCommand = new RelayCommand(DeleteUserId);

            AddRow = new RelayCommand<string?>(OnAddRow);
            DeleteRow = new RelayCommand<DefectParameter?>(OnDeleteRow);
            SaveData = new RelayCommand<string?>(OnSaveData);
        }


        private async Task InitializeModelsAsync()
        {
            await Task.Run(() =>
            {
                string[] labels = new string[0];
                StringBuilder errorMessages = new StringBuilder();

                try
                {
                    string AModelPath = @"C:\Windows\bestA.onnx";
                    string BModelPath = @"C:\Windows\bestB.onnx";

                    if (!File.Exists(AModelPath) || !File.Exists(BModelPath))
                    {
                        Growl.Warning("模型文件不存在");
                        MessageBox.Show("推理模型文件不存在，请检查文件路径。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    AddLog($"[模型初始化] 开始为A/B工位各创建 {ModelPoolSizePerStation} 个模型实例；每工位最大并发={MaxConcurrentPerStation}");

                    for (int i = 0; i < ModelPoolSizePerStation; i++)
                    {
                        // A工位初始化
                        var aiA = new YingYanAI();
                        aiA.ModelInit(AModelPath, labels, "PTO雅康叠片", out IPredictor modelA, out string errorA);

                        if (modelA != null)
                        {
                            _stationAPool.Enqueue(new AIWorker { AIInstance = aiA, Model = modelA });
                        }
                        else
                        {
                            errorMessages.AppendLine($"A工位模型实例 {i} 初始化失败: {errorA}");
                        }

                        // B工位初始化
                        var aiB = new YingYanAI();
                        aiB.ModelInit(BModelPath, labels, "PTO雅康叠片", out IPredictor modelB, out string errorB);

                        if (modelB != null)
                        {
                            _stationBPool.Enqueue(new AIWorker { AIInstance = aiB, Model = modelB });
                        }
                        else
                        {
                            errorMessages.AppendLine($"B工位模型实例 {i} 初始化失败: {errorB}");
                        }
                    }

                    UpdateUIState(() => AddLog($"✓ 模型池预热完成。A工位可用实例: {_stationAPool.Count}, B工位可用实例: {_stationBPool.Count}"));

                    if (errorMessages.Length > 0)
                    {
                        UpdateUIState(() => AddLog($"⚠ 模型初始化警告:\n{errorMessages}", CoreLogLevel.Warn));
                    }
                }
                catch (Exception ex)
                {
                    UpdateUIState(() => AddLog($"❌ 模型池加载异常: {ex.Message}", CoreLogLevel.Error));
                }
            });
        }



        /// <summary>
        /// 保存新UserID（验证+去重+持久化）
        /// </summary>
        private void SaveUserId()
        {
            // 二次验证
            if (!CanSaveUserId)
            {
                MessageBox.Show("请输入10位未重复的数字ID！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var savedId = NewUserId;

                SavedUserIds.Add(NewUserId);
                SaveUserIdsToDataBase();
                // 清空输入框
                NewUserId = string.Empty;

                MessageBox.Show($"用户ID: {savedId} 保存成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除指定UserID
        /// </summary>
        /// <param name="idObj">要删除的ID</param>
        private void DeleteUserId(object? idObj)
        {
            if (idObj is not string id || string.IsNullOrWhiteSpace(id))
            {
                return;
            }
            var isAuthorized = IsAuthorizedUser();
            if (!isAuthorized)
            {
                return;
            }
            try
            {
                // 从ObservableCollection移除
                if (SavedUserIds.Contains(id))
                {
                    SavedUserIds.Remove(id);
                    SaveUserIdsToDataBase();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 加载已保存的UserID列表
        /// </summary>
        private async Task LoadUserIdsAsync()
        {
            try
            {
                // 读取配置文件
                using var scope = _serviceProvider.CreateScope();
                var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();
                StatisticsModuleCode = await globalSettingRepo.GetSettingValueAsync<string>("CurrentVMName", "Default");


                var settingValue = await projectSettingRepo.GetSettingValueAsync("PushUserId", StatisticsModuleCode) as string;
                var savedList = settingValue?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(id => id.Trim())
                      .ToList() ?? new List<string>();

                if (savedList.Count > 0)
                {
                    AddLog($"加载已保存的用户ID列表: {string.Join(", ", savedList)}");
                }
                // 转换为ObservableCollection
                SavedUserIds = new ObservableCollection<string>(savedList);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载用户ID列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                // 初始化空列表
                SavedUserIds = new ObservableCollection<string>();
            }
        }

        private void EnsureDetectionStatisticsTable()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var statisticsRepo = scope.ServiceProvider.GetRequiredService<IDetectionStatisticsRepository>();

                statisticsRepo.EnsureTable();

                AddLog("[统计表初始化]完成");
            }
            catch (Exception ex)
            {
                AddLog($"[统计表初始化失败] {ex.Message}", CoreLogLevel.Error);
            }
        }


        private void LoadStatisticsFromDatabaseToUI(
            DetectionStatisticsSource sourceType = DetectionStatisticsSource.All)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var statisticsRepo = scope.ServiceProvider
                    .GetRequiredService<IDetectionStatisticsRepository>();

                var totalStatistics = statisticsRepo.GetOrCreate(
                    StatisticsModuleCode,
                    sourceType,
                    DetectionStatisticsScope.Total,
                    TotalStatisticsDate);

                ApplyStatisticsToUI(totalStatistics);
            }
            catch (Exception ex)
            {
                AddLog($"[统计加载失败-{sourceType}] {ex.Message}", CoreLogLevel.Error);
            }
        }

        private void ApplyStatisticsToUI(DetectionStatisticsModel? statistics)
        {
            if (statistics == null) return;

            TotalNeDetectionCount = statistics.TotalCount;
            OKCount = statistics.OKCount;
            NGCount = statistics.NGCount;
            OKRate = statistics.TotalCount > 0
                ? Math.Round((double)statistics.OKCount / statistics.TotalCount * 100, 2)
                : 0;

            var defectMap = DetectionStatisticsRepository.DeserializeDefectMap(statistics.DefectNGJson);


            foreach (var item in DefectDataGrid)
            {
                if (string.IsNullOrWhiteSpace(item.DefectName))
                {
                    continue;
                }

                item.NGCount = defectMap.TryGetValue(item.DefectName, out int defectNgCount)
                    ? defectNgCount
                    : 0;

                item.YieldRate = statistics.TotalCount > 0
                    ? Math.Round((double)(statistics.TotalCount - item.NGCount) / statistics.TotalCount * 100, 2)
                    : 100.00;
            }

            OnPropertyChanged(nameof(TotalNeDetectionCount));
            OnPropertyChanged(nameof(OKCount));
            OnPropertyChanged(nameof(NGCount));
            OnPropertyChanged(nameof(OKRate));
        }

        /// <summary>
        /// 持久化UserID列表到配置文件
        /// </summary>
        private void SaveUserIdsToDataBase()
        {
            try
            {
                var users = SavedUserIds.ToList();

                using var scope = _serviceProvider.CreateScope();
                var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                projectSettingRepo.UpdateSettingValue("PushUserId", StatisticsModuleCode, string.Join(",", users));
            }
            catch (Exception ex)
            {
                throw new Exception($"持久化配置失败：{ex.Message}", ex);
            }
        }


        private void CheckAuthority()
        {
            try
            {
                string currentUserName = "guest";
                using var scope = _serviceProvider.CreateScope();

                var repo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                var user = repo.GetByName("CurrentUserAccount");

                if (user != null) currentUserName = user.Value;

                bool isAuthorized = !string.Equals(currentUserName, "guest", StringComparison.OrdinalIgnoreCase);

                if (isAuthorized)
                {
                    // 授权用户：按钮可用，表格可编辑
                    IsEditAllowed = true;
                    IsParamGridReadOnly = false;
                }
                else
                {
                    // 访客用户：按钮禁用，表格只读
                    IsEditAllowed = false;
                    IsParamGridReadOnly = true;
                }

                AddLog($"[权限检查] 当前用户: {currentUserName}, 权限状态: {(isAuthorized ? "可编辑" : "只读")}");
            }
            catch (Exception ex)
            {
                AddLog($"[权限检查异常] {ex.Message}", CoreLogLevel.Error);
                // 发生异常时，强制降级为只读模式
                IsEditAllowed = false;
                IsParamGridReadOnly = true;
            }
        }

        private bool IsAuthorizedUser()
        {
            try
            {
                string currentUserName = "guest";
                using var scope = _serviceProvider.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                var user = repo.GetByName("CurrentUserAccount");
                if (user != null) currentUserName = user.Value;
                bool isAuthorized = !string.Equals(currentUserName, "guest", StringComparison.OrdinalIgnoreCase);
                if (!isAuthorized)
                {
                    MessageBox.Show("当前用户没有权限执行此操作！", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                AddLog($"[权限验证异常] {ex.Message}", CoreLogLevel.Error);
                MessageBox.Show("权限验证过程中发生错误，请联系管理员！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }


        /// <summary>
        /// 用户状态变更处理
        /// </summary>
        private void OnUserStateChanged(string userAccount)
        {
            // 确保在 UI 线程执行
            Application.Current.Dispatcher.Invoke(() =>
            {
                CheckAuthority();
                AddLog($"[权限刷新] 检测到用户变更为 {userAccount}，权限已更新");
            });
        }
        /// <summary>
        /// 获取组合后的UserID字符串（格式：Id1,Id2,Id3）
        /// </summary>
        /// <returns>逗号分隔的ID字符串</returns>
        public string GetCombinedUserIdString()
        {
            // 过滤空值 + 去重 + 逗号拼接
            return string.Join(",", SavedUserIds
                                     .Where(id => !string.IsNullOrWhiteSpace(id)) // 过滤空ID
                                     .Distinct()); // 去重
        }


        /// <summary>
        /// 接收View传递的InkCanvas引用
        /// </summary>
        public void SetInkCanvas(InkCanvas inkCanvas)
        {
            _inkCanvas = inkCanvas;
        }

        #region ROI
        /// <summary>
        /// 计算 InkCanvas (Stretch="Uniform") 与真实图像之间的坐标映射关系
        /// </summary>
        private void GetCanvasToImageMapping(out double offsetX, out double offsetY,
            out double scaleX, out double scaleY,
            int imagePixelWidth, int imagePixelHeight)
        {
            offsetX = 0; offsetY = 0; scaleX = 1; scaleY = 1;
            if (_inkCanvas == null) return;

            _inkCanvas.UpdateLayout();
            double canvasWidth = _inkCanvas.ActualWidth;
            double canvasHeight = _inkCanvas.ActualHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            // 优先使用 ImageBrush 底层 DIP 尺寸，保证与 WPF 渲染一致
            double dipWidth = imagePixelWidth;
            double dipHeight = imagePixelHeight;
            if (_inkCanvas.Background is ImageBrush brush && brush.ImageSource is BitmapSource bmp)
            {
                dipWidth = bmp.Width;
                dipHeight = bmp.Height;
            }

            double imageAspect = dipWidth / dipHeight;
            double canvasAspect = canvasWidth / canvasHeight;
            double renderWidth, renderHeight;

            if (imageAspect > canvasAspect)
            {
                // 图片更"宽"：上下留白
                renderWidth = canvasWidth;
                renderHeight = canvasWidth / imageAspect;
                offsetX = 0;
                offsetY = (canvasHeight - renderHeight) / 2.0;
            }
            else
            {
                // 图片更"高"：左右留白
                renderHeight = canvasHeight;
                renderWidth = canvasHeight * imageAspect;
                offsetX = (canvasWidth - renderWidth) / 2.0;
                offsetY = 0;
            }

            // 屏幕像素 -> 物理像素 的缩放比
            scaleX = imagePixelWidth / renderWidth;
            scaleY = imagePixelHeight / renderHeight;
        }
        /// <summary>
        /// 选择指定 A/B 工位 + 相机编号的 ROI 参考图片
        /// 参数格式：A1、A2、A3、A4、B1、B2、B3、B4
        /// </summary>
        private void SelectStationRoiImage(string? param)
        {
            if (!TryParseRoiKey(param, out string roiKey, out string station, out string cameraNo))
            {
                AddLog($"[ROI绘制] 无效参数：{param}，格式应为 A1-A4 或 B1-B4", CoreLogLevel.Warn);
                return;
            }

            _currentRoiCameraNo = roiKey;
            DrawTitle = $"绘制区域 - {station}工位相机{cameraNo}";

            if (int.TryParse(cameraNo, out int num))
            {
                CurrentCameraNumber = num;
            }

            var openFileDialog = new OpenFileDialog
            {
                Filter = "图片文件|*.bmp;*.jpg;*.jpeg;*.png",
                Title = $"选择{station}工位相机{cameraNo}的参考图片"
            };

            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            string imagePath = openFileDialog.FileName;
            _cameraImagePaths[roiKey] = imagePath;

            if (_inkCanvas != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _inkCanvas.Strokes.Clear();
                    CurrentRoiImageSource = null;
                    OnPropertyChanged(nameof(CurrentRoiImageSource));
                });
            }

            try
            {
                using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();

                CurrentRoiImageSource = bitmap;
                OnPropertyChanged(nameof(CurrentRoiImageSource));

                _drawImageWidth = bitmap.PixelWidth;
                _drawImageHeight = bitmap.PixelHeight;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"图片加载失败：{ex.Message}", "错误");
                AddLog($"[ROI绘制] 图片加载失败：{ex.Message}", CoreLogLevel.Error);
                return;
            }

            LoadHistoryRoiToCanvas(roiKey, _drawImageWidth, _drawImageHeight);
            IsDrawFlyoutOpen = true;
        }

        /// <summary>
        /// 加载历史 ROI 到画布
        /// </summary>
        private void LoadHistoryRoiToCanvas(string? roiKey, int currentImageWidth, int currentImageHeight)
        {
            if (!IsValidRoiKey(roiKey) || _inkCanvas == null || currentImageWidth <= 0 || currentImageHeight <= 0)
            {
                return;
            }

            roiKey = roiKey!.Trim().ToUpperInvariant();

            string hobjPath = GetRoiHobjPath(roiKey);
            string sizePath = GetRoiSizePath(roiKey);

            if (!File.Exists(hobjPath) || !File.Exists(sizePath))
            {
                return;
            }

            HObject? roiXld = null;
            HTuple xldRow = new HTuple();
            HTuple xldCol = new HTuple();

            try
            {
                string[] sizeParts = File.ReadAllText(sizePath).Split(',');
                if (sizeParts.Length < 2 ||
                    !int.TryParse(sizeParts[0], out int historyWidth) ||
                    !int.TryParse(sizeParts[1], out int historyHeight) ||
                    historyWidth <= 0 ||
                    historyHeight <= 0)
                {
                    return;
                }
                if (!TryReadRoiContourPoints(hobjPath, out xldRow, out xldCol))
                {
                    AddLog($"[ROI历史加载] {roiKey} 读取轮廓点失败", CoreLogLevel.Warn);
                    return;
                }

                if (xldRow.Length == 0 || xldCol.Length == 0)
                {
                    return;
                }

                double canvasWidth = _inkCanvas.ActualWidth > 0 ? _inkCanvas.ActualWidth : currentImageWidth;
                double canvasHeight = _inkCanvas.ActualHeight > 0 ? _inkCanvas.ActualHeight : currentImageHeight;

                GetCanvasToImageMapping(out double offsetX, out double offsetY,
                     out double scaleX, out double scaleY, historyWidth, historyHeight);

                var stylusPoints = new StylusPointCollection();

                for (int i = 0; i < xldRow.Length; i++)
                {
                    double x = (xldCol[i].D / scaleX) + offsetX;
                    double y = (xldRow[i].D / scaleY) + offsetY;
                    stylusPoints.Add(new StylusPoint(x, y));
                }

                var historyStroke = new Stroke(stylusPoints)
                {
                    DrawingAttributes = new DrawingAttributes
                    {
                        Color = System.Windows.Media.Color.FromArgb(150, 100, 149, 237),
                        Width = 2,
                        Height = 2
                    }
                };

                _inkCanvas.Strokes.Add(historyStroke);
            }
            catch (Exception ex)
            {
                AddLog($"[ROI历史加载失败] {roiKey}：{ex.Message}", CoreLogLevel.Warn);
            }
            finally
            {
                roiXld?.Dispose();
                xldRow?.Dispose();
                xldCol?.Dispose();
            }
        }

        /// <summary>
        /// 保存当前 A/B 工位相机 ROI
        /// </summary>
        private void SaveStationRoiAsHalconRegion()
        {
            if (_inkCanvas == null)
            {
                MessageBox.Show("画布未初始化！", "错误");
                AddLog("[ROI保存] 画布未初始化", CoreLogLevel.Warn);
                return;
            }

            if (!TryParseRoiKey(_currentRoiCameraNo, out string roiKey, out string station, out string cameraNo))
            {
                MessageBox.Show("未选择有效的目标工位相机，请重新操作！", "错误");
                AddLog($"[ROI保存] 无效ROI Key：{_currentRoiCameraNo}", CoreLogLevel.Warn);
                return;
            }

            if (_drawImageWidth <= 0 || _drawImageHeight <= 0)
            {
                MessageBox.Show("请选择需要绘制的图片后再保存！", "错误");
                AddLog("[ROI保存] 未选择绘制图片，无法获取原图尺寸", CoreLogLevel.Warn);
                return;
            }

            try
            {
                string roiDir = GetRoiConfigDir();
                Directory.CreateDirectory(roiDir);

                string hobjPath = GetRoiHobjPath(roiKey);
                string sizePath = GetRoiSizePath(roiKey);

                string imagePath = _cameraImagePaths.TryGetValue(roiKey, out var path) ? path : string.Empty;

                // 清空 ROI
                if (_inkCanvas.Strokes.Count == 0)
                {
                    if (File.Exists(hobjPath))
                    {
                        File.Delete(hobjPath);
                    }

                    File.WriteAllText(sizePath, $"{_drawImageWidth},{_drawImageHeight},{imagePath}");

                    RemoveDetectionRegionCache(roiKey);

                    UpdateRoiStatus(roiKey, false);

                    IsDrawFlyoutOpen = false;
                    MessageBox.Show($"{station}工位相机{cameraNo}的ROI已清空并保存！", "成功");
                    AddLog($"[ROI保存] {roiKey} ROI已清空");
                    return;
                }

                double canvasWidth = _inkCanvas.ActualWidth;
                double canvasHeight = _inkCanvas.ActualHeight;

                if (canvasWidth <= 0 || canvasHeight <= 0)
                {
                    MessageBox.Show("画布尺寸无效！", "错误");
                    AddLog("[ROI保存] 画布尺寸无效", CoreLogLevel.Warn);
                    return;
                }

                GetCanvasToImageMapping(out double offsetX, out double offsetY,
                    out double scaleX, out double scaleY, _drawImageWidth, _drawImageHeight);

                HTuple row = new HTuple();
                HTuple col = new HTuple();

                foreach (var stroke in _inkCanvas.Strokes)
                {
                    foreach (var sp in stroke.StylusPoints)
                    {
                        double originalX = (sp.X - offsetX) * scaleX;
                        double originalY = (sp.Y - offsetY) * scaleY;

                        originalX = Math.Max(0, Math.Min(originalX, _drawImageWidth - 1));
                        originalY = Math.Max(0, Math.Min(originalY, _drawImageHeight - 1));

                        row = row.TupleConcat(originalY);
                        col = col.TupleConcat(originalX);
                    }
                }

                if (row.Length <3 || col.Length <3)
                {
                    MessageBox.Show("ROI点数量不足，至少需要绘制3个点形成闭合区域！", "错误");
                    AddLog($"[ROI保存失败] {roiKey} ROI点数量不足：row={row.Length}, col={col.Length}", CoreLogLevel.Warn);
                    return;
                }

                row = row.TupleConcat(row[0]);
                col = col.TupleConcat(col[0]);

                HObject? roiXld = null;
                HObject? delRegion = null;

                try
                {
                    HOperatorSet.GenContourPolygonXld(out roiXld, row, col);
                    HOperatorSet.GenRegionContourXld(roiXld, out delRegion, "filled");

                    HOperatorSet.GenContourPolygonXld(out roiXld, row, col);
                    HOperatorSet.GenRegionContourXld(roiXld, out delRegion, "filled");

                    double area = GetRegionArea(delRegion);

                    if (area <= 0)
                    {
                        MessageBox.Show($"{roiKey} ROI保存失败：生成的Region面积为0，请重新绘制闭合区域！", "错误");
                        AddLog($"[ROI保存失败] {roiKey} 生成Region面积为0", CoreLogLevel.Error);
                        return;
                    }

                    bool saveOk = SaveRoiRegionToDiskAndVerify(
                        roiKey,
                        delRegion,
                        hobjPath,
                        sizePath,
                        _drawImageWidth,
                        _drawImageHeight,
                        imagePath);

                    if (!saveOk)
                    {
                        return;
                    }

                    // 磁盘校验成功后再更新缓存
                    ReplaceDetectionRegionCache(roiKey, delRegion);
                }
                finally
                {
                    delRegion?.Dispose();
                    roiXld?.Dispose();
                    row?.Dispose();
                    col?.Dispose();
                }

                UpdateRoiStatus(roiKey, true);

                IsDrawFlyoutOpen = false;
                MessageBox.Show($"{station}工位相机{cameraNo}的ROI已保存！", "成功");
                AddLog($"[ROI保存] {roiKey} ROI已保存");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存ROI失败：{ex.Message}", "错误");
                AddLog($"[ROI保存异常] {_currentRoiCameraNo}：{ex.Message}", CoreLogLevel.Error);
            }
        }

        private HObject? ReadDetectionRegionFromRoiFile(string filePath)
        {
            HObject? obj = null;
            HObject? region = null;

            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    AddLog($"[ROI读取失败] 文件不存在：{filePath}", CoreLogLevel.Warn);
                    return null;
                }

                // 关键：读取ROI前关闭裁剪
                HOperatorSet.SetSystem("clip_region", "false");

                HOperatorSet.ReadObject(out obj, filePath);

                if (obj == null || !obj.IsInitialized())
                {
                    AddLog($"[ROI读取失败] ReadObject后对象未初始化：{filePath}", CoreLogLevel.Warn);
                    return null;
                }

                region = ConvertRoiObjectToDetectionRegion(obj);

                if (region == null || !region.IsInitialized())
                {
                    AddLog($"[ROI读取失败] ROI对象无法转换为Region：{filePath}", CoreLogLevel.Warn);
                    return null;
                }

                double area = GetRegionArea(region);

                AddLog($"[ROI读取诊断] 文件={Path.GetFileName(filePath)}，读回面积={area:F2}");

                if (area <= 0)
                {
                    region.Dispose();
                    AddLog($"[ROI读取失败] ROI读回面积为0：{filePath}", CoreLogLevel.Error);
                    return null;
                }

                return region;
            }
            catch (Exception ex)
            {
                region?.Dispose();
                AddLog($"[ROI读取异常] {filePath}：{ex.Message}", CoreLogLevel.Error);
                return null;
            }
            finally
            {
                obj?.Dispose();
            }
        }

        private HObject? ConvertRoiObjectToDetectionRegion(HObject roiObj)
        {
            HObject? region = null;
            HTuple? objClassTuple = null;

            try
            {
                if (roiObj == null || !roiObj.IsInitialized())
                {
                    AddLog("[ROI转换失败] 输入对象未初始化", CoreLogLevel.Warn);
                    return null;
                }

                HOperatorSet.GetObjClass(roiObj, out objClassTuple);

                string objClass = objClassTuple.Length > 0
                    ? objClassTuple[0].S.ToLowerInvariant()
                    : string.Empty;

                AddLog($"[ROI转换] 对象类型原始值 = '{objClass}'");

                if (objClass.Contains("region"))
                {
                    HOperatorSet.CopyObj(roiObj, out region, 1, -1);

                    double area = GetRegionArea(region);
                    AddLog($"[ROI转换] 识别为Region类型，复制后面积={area:F2}");

                    return area > 0 ? region : DisposeAndReturnNull(region);
                }

                if (objClass.Contains("xld"))
                {
                    HOperatorSet.GenRegionContourXld(roiObj, out region, "filled");

                    double area = GetRegionArea(region);
                    AddLog($"[ROI转换] 识别为XLD类型，转换为filled Region，面积={area:F2}");

                    return area > 0 ? region : DisposeAndReturnNull(region);
                }

                AddLog($"[ROI转换失败] 不支持的ROI对象类型：{objClass}", CoreLogLevel.Error);
                return null;
            }
            catch (Exception ex)
            {
                region?.Dispose();
                AddLog($"[ROI转换异常] {ex.Message}", CoreLogLevel.Error);
                return null;
            }
            finally
            {
                objClassTuple?.Dispose();
            }
        }

        private HObject? DisposeAndReturnNull(HObject? obj)
        {
            obj?.Dispose();
            return null;
        }

        private double GetRegionArea(HObject? region)
        {
            if (region == null || !region.IsInitialized())
            {
                return 0;
            }

            HTuple? area = null;
            HTuple? row = null;
            HTuple? col = null;

            try
            {
                HOperatorSet.AreaCenter(region, out area, out row, out col);

                double totalArea = 0;
                for (int i = 0; i < area.Length; i++)
                {
                    totalArea += area[i].D;
                }

                return totalArea;
            }
            catch (Exception ex)
            {
                AddLog($"[ROI面积计算异常] {ex.Message}", CoreLogLevel.Warn);
                return 0;
            }
            finally
            {
                area?.Dispose();
                row?.Dispose();
                col?.Dispose();
            }
        }

        private bool TryReadRoiContourPoints(string hobjPath, out HTuple rows, out HTuple cols)
        {
            rows = new HTuple();
            cols = new HTuple();

            HObject? roiObj = null;
            HObject? contourXld = null;
            HTuple? objClassTuple = null;

            try
            {
                HOperatorSet.SetSystem("clip_region", "false");

                HOperatorSet.ReadObject(out roiObj, hobjPath);

                if (roiObj == null || !roiObj.IsInitialized())
                {
                    return false;
                }

                HOperatorSet.GetObjClass(roiObj, out objClassTuple);

                string objClass = objClassTuple.Length > 0
                    ? objClassTuple[0].S.ToLowerInvariant()
                    : string.Empty;

                if (objClass.Contains("region"))
                {
                    HOperatorSet.GenContourRegionXld(roiObj, out contourXld, "border");
                    HOperatorSet.GetContourXld(contourXld, out rows, out cols);
                    return rows.Length > 0 && cols.Length > 0;
                }

                if (objClass.Contains("xld"))
                {
                    HOperatorSet.GetContourXld(roiObj, out rows, out cols);
                    return rows.Length > 0 && cols.Length > 0;
                }

                AddLog($"[ROI查看] 不支持的ROI对象类型：{objClass}", CoreLogLevel.Warn);
                return false;
            }
            catch (Exception ex)
            {
                rows?.Dispose();
                cols?.Dispose();

                rows = new HTuple();
                cols = new HTuple();

                AddLog($"[ROI查看] 读取轮廓失败：{ex.Message}", CoreLogLevel.Warn);
                return false;
            }
            finally
            {
                roiObj?.Dispose();
                contourXld?.Dispose();
                objClassTuple?.Dispose();
            }
        }

        /// <summary>
        ///更新ROI状态属性
        /// </summary>
        private void UpdateRoiStatus(string station, bool isEnabled)
        {
            switch (station)
            {
                case "A1": HasRoiA1 = isEnabled; break;
                case "A2": HasRoiA2 = isEnabled; break;
                case "A3": HasRoiA3 = isEnabled; break;
                case "A4": HasRoiA4 = isEnabled; break;
                case "B1": HasRoiB1 = isEnabled; break;
                case "B2": HasRoiB2 = isEnabled; break;
                case "B3": HasRoiB3 = isEnabled; break;
                case "B4": HasRoiB4 = isEnabled; break;
            }
        }

        /// <summary>
        /// 删除当前 ROI 绘制内容并保存为空 ROI
        /// </summary>
        private void DeleteStationRoi()
        {
            if (_inkCanvas == null || string.IsNullOrWhiteSpace(_currentRoiCameraNo))
            {
                return;
            }

            _inkCanvas.Strokes.Clear();

            if (_drawImageWidth <= 0 || _drawImageHeight <= 0)
            {
                string sizePath = GetRoiSizePath(_currentRoiCameraNo.Trim().ToUpperInvariant());

                if (File.Exists(sizePath))
                {
                    string[] parts = File.ReadAllText(sizePath).Split(',');

                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0], out _drawImageWidth);
                        int.TryParse(parts[1], out _drawImageHeight);
                    }
                }
            }

            SaveStationRoiAsHalconRegion();
        }

        /// <summary>
        /// 查看指定 A/B 工位相机 ROI
        /// </summary>
        private void ViewStationRoi(string? param)
        {
            if (_inkCanvas == null)
            {
                MessageBox.Show("画布正在初始化，请再次点击查看！", "提示");
                return;
            }

            if (!TryParseRoiKey(param, out string roiKey, out string station, out string cameraNo))
            {
                AddLog($"[查看ROI] 无效参数：{param}，格式应为 A1-A4 或 B1-B4", CoreLogLevel.Warn);
                return;
            }

            _currentRoiCameraNo = roiKey;
            DrawTitle = $"查看检测区域 - {station}工位相机{cameraNo}";

            string hobjPath = GetRoiHobjPath(roiKey);
            string sizePath = GetRoiSizePath(roiKey);

            if (!File.Exists(hobjPath) || !File.Exists(sizePath))
            {
                MessageBox.Show($"{station}工位相机{cameraNo}的ROI文件或尺寸文件不存在！", "提示");
                return;
            }

            try
            {
                string[] sizeParts = File.ReadAllText(sizePath).Split(',');

                if (sizeParts.Length < 2 ||
                    !int.TryParse(sizeParts[0], out int drawWidth) ||
                    !int.TryParse(sizeParts[1], out int drawHeight) ||
                    drawWidth <= 0 ||
                    drawHeight <= 0)
                {
                    MessageBox.Show("尺寸文件格式错误！", "错误");
                    return;
                }

                string imagePath = sizeParts.Length >= 3 ? sizeParts[2] : string.Empty;

                if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                {
                    using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = stream;
                    bmp.EndInit();
                    bmp.Freeze();

                    CurrentRoiImageSource = bmp;
                    OnPropertyChanged(nameof(CurrentRoiImageSource));
                }

                HObject? roiXld = null;
                HTuple xldRow = new HTuple();
                HTuple xldCol = new HTuple();
                try
                {
                    if (!TryReadRoiContourPoints(hobjPath, out xldRow, out xldCol))
                    {
                        MessageBox.Show("从ROI文件提取不到轮廓点，请重新保存ROI！", "错误");
                        return;
                    }

                    if (xldRow.Length == 0 || xldCol.Length == 0)
                    {
                        MessageBox.Show("从XLD提取不到坐标，请重新保存ROI！", "错误");
                        return;
                    }
                    IsDrawFlyoutOpen = true;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _inkCanvas.UpdateLayout();
                        GetCanvasToImageMapping(out double offsetX, out double offsetY, out double scaleX, out double scaleY, drawWidth, drawHeight);
                        _inkCanvas.Strokes.Clear();

                        var stylusPoints = new StylusPointCollection();

                        for (int i = 0; i < xldRow.Length; i++)
                        {
                            double x = (xldCol[i].D / scaleX) + offsetX;
                            double y = (xldRow[i].D / scaleY) + offsetY;
                            stylusPoints.Add(new StylusPoint(x, y));
                        }

                        var stroke = new Stroke(stylusPoints)
                        {
                            DrawingAttributes = new DrawingAttributes
                            {
                                Color = System.Windows.Media.Color.FromArgb(200, 231, 76, 60),
                                Width = 3,
                                Height = 3
                            }
                        };

                        _inkCanvas.Strokes.Add(stroke);
                    }, DispatcherPriority.ContextIdle);

                }
                finally
                {
                    roiXld?.Dispose();
                    xldRow?.Dispose();
                    xldCol?.Dispose();
                }
                IsDrawFlyoutOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载ROI失败：{ex.Message}", "错误");
                AddLog($"[查看ROI异常] {roiKey}：{ex.Message}", CoreLogLevel.Error);
            }

        }


        /// <summary>
        /// 仅清空当前ROI画布上的笔迹。
        /// 不关闭绘制弹窗，不清空背景图片，不修改当前ROI目标。
        /// </summary>
        private void ClearCanvasOnly()
        {
            if (_inkCanvas == null)
            {
                AddLog("[ROI画布] InkCanvas未初始化，无法清空画布", CoreLogLevel.Warn);
                return;
            }

            var app = Application.Current;

            if (app?.Dispatcher == null ||
                app.Dispatcher.HasShutdownStarted ||
                app.Dispatcher.HasShutdownFinished)
            {
                return;
            }

            try
            {
                if (app.Dispatcher.CheckAccess())
                {
                    _inkCanvas.Strokes.Clear();
                    _inkCanvas.UpdateLayout();
                }
                else
                {
                    app.Dispatcher.Invoke(() =>
                    {
                        _inkCanvas.Strokes.Clear();
                        _inkCanvas.UpdateLayout();
                    });
                }
            }
            catch (Exception ex)
            {
                AddLog($"[ROI画布] 清空画布失败：{ex.Message}", CoreLogLevel.Error);
            }
        }


        /// <summary>
        /// 取消绘制并关闭Flyout
        /// </summary>
        private void CancelDrawRoi()
        {
            IsDrawFlyoutOpen = false;
        }

        /// <summary>
        /// 异步加载所有 A/B 工位相机 ROI 配置
        /// </summary>
        private async Task LoadRoiConfigsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    string roiDir = GetRoiConfigDir();

                    if (!Directory.Exists(roiDir))
                    {
                        UpdateUIState(() => AddLog("ROI配置目录不存在，跳过加载"));
                        return;
                    }

                    var hobjFiles = Directory.GetFiles(roiDir, "*_ROI.hobj");

                    if (hobjFiles.Length == 0)
                    {
                        UpdateUIState(() => AddLog("未找到ROI配置文件"));
                        return;
                    }

                    int successCount = 0;
                    int failCount = 0;

                    foreach (var file in hobjFiles)
                    {
                        HObject? delRegion = null;

                        try
                        {
                            string fileName = Path.GetFileNameWithoutExtension(file);
                            string roiKey = fileName.Replace("_ROI", "", StringComparison.OrdinalIgnoreCase)
                                                    .Trim()
                                                    .ToUpperInvariant();

                            if (!IsValidRoiKey(roiKey))
                            {
                                failCount++;
                                UpdateUIState(() =>
                                    AddLog($"[ROI加载] 文件名格式不正确，跳过：{Path.GetFileName(file)}", CoreLogLevel.Warn));
                                continue;
                            }

                            delRegion = ReadDetectionRegionFromRoiFile(file);

                            if (delRegion == null || !delRegion.IsInitialized())
                            {
                                failCount++;
                                UpdateUIState(() =>
                                {
                                    UpdateRoiStatus(roiKey, false);
                                    AddLog($"[ROI加载失败] {roiKey} ROI读取失败或为空：{file}", CoreLogLevel.Error);
                                });
                                continue;
                            }

                            double area = GetRegionArea(delRegion);

                            if (area <= 0)
                            {
                                failCount++;
                                delRegion.Dispose();

                                UpdateUIState(() =>
                                {
                                    UpdateRoiStatus(roiKey, false);
                                    AddLog($"[ROI加载失败] {roiKey} ROI面积为0：{file}", CoreLogLevel.Error);
                                });

                                continue;
                            }

                            ReplaceDetectionRegionCache(roiKey, delRegion);

                            successCount++;

                            UpdateUIState(() =>
                            {
                                UpdateRoiStatus(roiKey, true);
                                AddLog($"✅ 已加载 {roiKey} ROI到检测缓存，面积={area:F2}，文件={file}");
                            });
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            UpdateUIState(() =>
                                AddLog($"ROI加载失败 ({Path.GetFileName(file)}): {ex.Message}", CoreLogLevel.Error));
                        }
                        finally
                        {
                            delRegion?.Dispose();
                        }
                    }

                    UpdateUIState(() =>
                        AddLog($"ROI配置加载完成：成功 {successCount} 个，失败 {failCount} 个"));
                }
                catch (Exception ex)
                {
                    UpdateUIState(() =>
                        AddLog($"ROI配置目录扫描失败：{ex.Message}", CoreLogLevel.Error));
                }
            });
        }

        private void ReplaceDetectionRegionCache(string key, HObject region)
        {
            if (string.IsNullOrWhiteSpace(key) || region == null || !region.IsInitialized())
            {
                return;
            }

            HObject? cacheCopy = null;

            try
            {
                HOperatorSet.CopyObj(region, out cacheCopy, 1, -1);

                lock (_roiCacheLock)
                {
                    if (_detectionRegionCache.TryGetValue(key, out var oldRegion))
                    {
                        oldRegion?.Dispose();
                        _detectionRegionCache.Remove(key);
                    }

                    _detectionRegionCache[key] = cacheCopy;
                    cacheCopy = null;
                }

                AddLog($"[ROI缓存] 已更新 {key} ROI缓存");
            }
            catch (Exception ex)
            {
                cacheCopy?.Dispose();
                AddLog($"[ROI缓存] 更新 {key} ROI缓存失败：{ex.Message}", CoreLogLevel.Error);
            }
        }


        private bool TryGetDetectionRegionCache(string key, out HObject? region)
        {
            region = null;

            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            lock (_roiCacheLock)
            {
                if (_detectionRegionCache.TryGetValue(key, out var cachedRegion) &&
                    cachedRegion != null &&
                    cachedRegion.IsInitialized())
                {
                    HOperatorSet.CopyObj(cachedRegion, out region, 1, -1);
                    return region != null && region.IsInitialized();
                }
            }

            return false;
        }

        private void RemoveDetectionRegionCache(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            lock (_roiCacheLock)
            {
                if (_detectionRegionCache.TryGetValue(key, out var oldRegion))
                {
                    oldRegion?.Dispose();
                    _detectionRegionCache.Remove(key);
                }
            }
        }

        private bool SaveRoiRegionToDiskAndVerify(
    string roiKey,
    HObject region,
    string hobjPath,
    string sizePath,
    int imageWidth,
    int imageHeight,
    string imagePath)
        {
            if (!IsValidRoiKey(roiKey))
            {
                AddLog($"[ROI保存失败] roiKey无效：{roiKey}", CoreLogLevel.Error);
                return false;
            }

            if (region == null || !region.IsInitialized())
            {
                AddLog($"[ROI保存失败] {roiKey} Region未初始化", CoreLogLevel.Error);
                return false;
            }

            double memoryArea = GetRegionArea(region);
            AddLog($"[ROI保存诊断] {roiKey} 内存Region面积={memoryArea:F2}");

            if (memoryArea <= 0)
            {
                MessageBox.Show($"{roiKey} ROI保存失败：Region面积为0，请重新绘制闭合区域！", "错误");
                AddLog($"[ROI保存失败] {roiKey} 内存Region面积为0", CoreLogLevel.Error);
                return false;
            }

            string? roiDir = Path.GetDirectoryName(hobjPath);
            if (string.IsNullOrWhiteSpace(roiDir))
            {
                AddLog($"[ROI保存失败] ROI目录无效：{hobjPath}", CoreLogLevel.Error);
                return false;
            }

            Directory.CreateDirectory(roiDir);

            string tempPath = Path.Combine(
                roiDir,
                $"{Path.GetFileNameWithoutExtension(hobjPath)}_tmp_{DateTime.Now:yyyyMMddHHmmssfff}.hobj");

            HObject? readBack = null;

            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                HOperatorSet.SetSystem("clip_region", "false");

                // 关键：保存检测用Region，而不是roiXld
                HOperatorSet.WriteObject(region, tempPath);

                readBack = ReadDetectionRegionFromRoiFile(tempPath);

                if (readBack == null || !readBack.IsInitialized())
                {
                    AddLog($"[ROI保存失败] {roiKey} 临时文件读回失败：{tempPath}", CoreLogLevel.Error);
                    MessageBox.Show($"{roiKey} ROI保存失败：临时文件读回失败！", "错误");
                    return false;
                }

                double diskArea = GetRegionArea(readBack);
                AddLog($"[ROI保存诊断] {roiKey} 临时文件读回面积={diskArea:F2}");

                if (diskArea <= 0)
                {
                    AddLog($"[ROI保存失败] {roiKey} 临时文件读回面积为0：{tempPath}", CoreLogLevel.Error);
                    MessageBox.Show($"{roiKey} ROI保存失败：磁盘读回面积为0！", "错误");
                    return false;
                }

                if (File.Exists(hobjPath))
                {
                    File.Delete(hobjPath);
                }

                File.Move(tempPath, hobjPath);

                File.WriteAllText(sizePath, $"{imageWidth},{imageHeight},{imagePath}");

                AddLog($"[ROI保存成功] {roiKey} 已保存并校验通过：面积={diskArea:F2}，路径={hobjPath}");
                return true;
            }
            catch (Exception ex)
            {
                AddLog($"[ROI保存异常] {roiKey}：{ex.Message}", CoreLogLevel.Error);
                MessageBox.Show($"{roiKey} ROI保存异常：{ex.Message}", "错误");
                return false;
            }
            finally
            {
                readBack?.Dispose();

                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // 删除临时文件失败不影响主流程
                }
            }
        }

        #endregion
        #region 辅助方法
        /// <summary>
        /// 在视觉树中查找子元素
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && ((FrameworkElement)child).Name == childName)
                    return typedChild;

                var grandChild = FindVisualChild<T>(child, childName);
                if (grandChild != null)
                    return grandChild;
            }
            return null;
        }

        /// <summary>
        /// 从XLD文件生成Region
        /// </summary>
        private HObject? GenerateDelRegionFromXldFile(string roiKey)
        {
            if (!IsValidRoiKey(roiKey))
            {
                AddLog($"[ROI兜底] 无效ROI Key：{roiKey}", CoreLogLevel.Warn);
                return null;
            }

            roiKey = roiKey.Trim().ToUpperInvariant();

            try
            {
                string hobjPath = GetRoiHobjPath(roiKey);

                if (!File.Exists(hobjPath))
                {
                    if (_warnedGenerateFailCameras.TryAdd(roiKey, 0))
                    {
                        AddLog($"[ROI兜底] {roiKey} 的ROI文件不存在：{hobjPath}", CoreLogLevel.Warn);
                    }

                    return null;
                }

                HObject? region = ReadDetectionRegionFromRoiFile(hobjPath);

                if (region == null || !region.IsInitialized())
                {
                    if (_warnedGenerateFailCameras.TryAdd(roiKey, 0))
                    {
                        AddLog($"[ROI兜底失败] {roiKey} ROI读取失败或为空：{hobjPath}", CoreLogLevel.Error);
                    }

                    region?.Dispose();
                    return null;
                }

                double area = GetRegionArea(region);

                if (area <= 0)
                {
                    if (_warnedGenerateFailCameras.TryAdd(roiKey, 0))
                    {
                        AddLog($"[ROI兜底失败] {roiKey} ROI面积为0：{hobjPath}", CoreLogLevel.Error);
                    }

                    region.Dispose();
                    return null;
                }

                AddLog($"[ROI兜底] {roiKey} ROI加载成功，面积={area:F2}");
                return region;
            }
            catch (Exception ex)
            {
                AddLog($"[ROI兜底异常] {roiKey}：{ex.Message}", CoreLogLevel.Error);
                return null;
            }
        }
        #endregion

        #region 在线检测 - 选择监控文件夹

        #region 双工位路径选择方法
        private void SelectMonitorFolder(Station station)
        {
            string stationName = station == Station.A ? "A" : "B";

            var folderDialog = new OpenFolderDialog
            {
                Title = $"选择{stationName}工位在线检测监控文件夹",
                ValidateNames = true,
                Multiselect = false,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);

            if (result != true)
            {
                return;
            }

            if (station == Station.A)
            {
                SelectedMonitorFolderPathA = folderDialog.FolderName;
            }
            else
            {
                SelectedMonitorFolderPathB = folderDialog.FolderName;
            }

            AddLog($"[{stationName}工位] 已选择在线监控文件夹：{folderDialog.FolderName}");
        }

        #endregion

        #region 双工位启动监控


        /// <summary>
        /// 在所有资源加载完成后，根据配置启动监控
        /// </summary>
        private async Task StartMonitoringFromConfigAsync()
        {
            // 等待UI完全就绪
            await Task.Delay(500);

            await UpdateUIStateAsync(async () =>
            {
                // A工位
                if (_isOnlineDetectionEnabledA &&
                    !string.IsNullOrWhiteSpace(SelectedMonitorFolderPathA) &&
                    Directory.Exists(SelectedMonitorFolderPathA))
                {
                    try
                    {
                        _isOnlineDetectionEnabledA = false;  // 重置以便Setter生效
                        IsOnlineDetectionEnabledA = true;    // 触发正常的启动逻辑
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[启动恢复] A工位监控启动失败：{ex.Message}", CoreLogLevel.Error);
                    }
                }

                await Task.Delay(1000);
                // B工位同理
                if (_isOnlineDetectionEnabledB &&
                    !string.IsNullOrWhiteSpace(SelectedMonitorFolderPathB) &&
                    Directory.Exists(SelectedMonitorFolderPathB))
                {
                    try
                    {
                        _isOnlineDetectionEnabledB = false;
                        IsOnlineDetectionEnabledB = true;
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[启动恢复] B工位监控启动失败：{ex.Message}", CoreLogLevel.Error);
                    }
                }
            });
        }


        /// <summary>
        /// 处理新文件
        /// </summary>
        private async Task ProcessNewFileAsync(string filePath, ConcurrentDictionary<string, DateTime> processedFiles, string stationName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath)) return;

                if (filePath.Contains(@"\截图\") || !filePath.Contains(@"\负极\"))
                {
                    return;
                }

                // 防重复处理，记录加入时间
                if (!processedFiles.TryAdd(filePath, DateTime.Now)) return;

                // 内存防爆清理：当记录超过 5000 条时，清理 1 小时前的数据
                if (processedFiles.Count > 5000)
                {
                    var threshold = DateTime.Now.AddHours(-1);
                    var keysToRemove = processedFiles.Where(kvp => kvp.Value < threshold).Select(kvp => kvp.Key).ToList();
                    foreach (var key in keysToRemove) processedFiles.TryRemove(key, out _);
                }

                string extension = Path.GetExtension(filePath);
                if (!AllowedImageExtensions.Contains(extension))
                {
                    processedFiles.TryRemove(filePath, out _);
                    return;
                }

                // 使用异步等待，不阻塞 FileSystemWatcher
                if (!await WaitForFileReadyAsync(filePath, TimeSpan.FromSeconds(3)))
                {
                    AddLog($"[文件超时] {Path.GetFileName(filePath)}", CoreLogLevel.Warn);
                    processedFiles.TryRemove(filePath, out _);
                    return;
                }

                if (!File.Exists(filePath))
                {
                    processedFiles.TryRemove(filePath, out _);
                    return;
                }

                string expectedFolder = _selectedMode switch
                {
                    DetectionMode.OK => @"\OK\",
                    DetectionMode.NG => @"\NG\",
                    DetectionMode.全检 => @"\原图\",
                    _ => @"\原图\"
                };

                if (!filePath.Contains(expectedFolder))
                {
                    processedFiles.TryRemove(filePath, out _);
                    return;
                }

                if (!IsDetectionModeMatch(filePath))
                {
                    processedFiles.TryRemove(filePath, out _);
                    return;
                }

                EnqueueTask(filePath, stationName);
                //AddLog($"[监听器] 检测到文件并入队: {Path.GetFileName(filePath)}");
                AddLog($"[{stationName}监听器] 检测到文件并入队: {filePath}");
            }
            catch (Exception ex)
            {
                AddLog($"[{stationName}监听器] 处理文件异常: {ex.Message}", CoreLogLevel.Error);
                processedFiles.TryRemove(filePath, out _);
            }
        }

        /// <summary>
        /// 等待文件就绪(可读取)
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="timeout">超时时间</param>
        /// <returns>文件是否就绪</returns>
        private async Task<bool> WaitForFileReadyAsync(string filePath, TimeSpan timeout, int pollInterval = 50, bool requireExclusive = true)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            var share = requireExclusive ? FileShare.None : FileShare.Read;
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < timeout)
            {
                if (!File.Exists(filePath)) return false;

                try
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, share);
                    return true; // 成功打开，文件就绪
                }
                catch (FileNotFoundException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
                catch (IOException)
                {
                    // 使用 await Task.Delay 释放线程，绝不能用 Thread.Sleep
                    await Task.Delay(pollInterval);
                }
            }
            return false; // 超时
        }


        #endregion

        #region 双工位停止监控
        private void StopOnlineMonitoring(Station station)
        {
            var ctx = GetContext(station);
            string stationName = ctx.Name;

            bool wasRunning;

            lock (_stationCountLock)
            {
                wasRunning = ctx.IsMonitorRunning;

                if (!wasRunning)
                {
                    return;
                }

                ctx.IsThreadTerminating = true;
                ctx.IsMonitorRunning = false;
                ctx.IsThreadRunning = false;

                if (_runningStationsCount > 0)
                {
                    _runningStationsCount--;
                }
            }

            StopAllWatchers(station);

            lock (ctx.ProcessingLock)
            {
                ctx.Cts?.Cancel();
                ctx.Cts?.Dispose();
                ctx.Cts = null;
            }

            ctx.QueueEvent.Set();

            AddLog($"[{stationName}工位] 监控已停止");
        }

        private void StopAllWatchers(Station station)
        {
            var ctx = GetContext(station);

            lock (_watcherLock)
            {
                if (ctx.RootWatcher != null)
                {
                    ctx.RootWatcher.EnableRaisingEvents = false;
                    ctx.RootWatcher.Dispose();
                    ctx.RootWatcher = null;
                }
            }
        }
        #endregion





        #region 双工位队列处理



        public void EnqueueTask(string filePath, string stationName)
        {
            if (!Enum.TryParse(stationName, ignoreCase: true, out Station station))
            {
                AddLog($"[入队] 未知工位: {stationName}", CoreLogLevel.Error);
                return;
            }

            EnqueueTask(filePath, station);
        }

        private void EnqueueTask(string filePath, Station station)
        {
            var ctx = GetContext(station);
            string stationName = ctx.Name;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                AddLog($"[{stationName}工位-入队] 无效路径，跳过入队");
                return;
            }

            try
            {
                lock (ctx.QueueLock)
                {
                    ctx.Queue.Enqueue(filePath);
                }

                ctx.QueueEvent.Set();

                if (!ctx.IsQueueRunning)
                {
                    AddLog($"[{stationName}工位-入队] 检测到处理线程未运行，尝试重新启动");
                    StartConcurrentProcessing(station);
                }
            }
            catch (Exception ex)
            {
                AddLog($"[{stationName}工位-入队] 异常: {ex.Message}", CoreLogLevel.Error);
            }
        }


        /// <summary>
        /// 清理统计缓存
        /// </summary>
        /// <param name="detectionData"></param>
        private void AddDetectionDataAndEnsureCapacity(dynamic detectionData)
        {
            var dict = detectionData as IDictionary<string, object>;
            if (dict != null && dict.TryGetValue("DetectionResult", out var res) && res?.ToString() != "NG")
            {
                return; // 如果不是 NG，直接丢弃，不占用内存
            }

            lock (_statisticsLock)
            {
                // 若该数据产生于"最后一次清空"之前，作废丢弃
                DateTime produceTime = DateTime.Now;
                if (dict != null && dict.TryGetValue("DetectionTime", out var dt)
                    && dt is DateTime parsedTime)
                {
                    produceTime = parsedTime;
                }

                if (produceTime <= _lastClearTimestamp)
                {
                    AddLog($"[清空保护] 丢弃清空时刻之前产生的滞后记录");
                    return;
                }

                _fullDetectionDataList.Add(detectionData);

                if (_fullDetectionDataList.Count > 200)
                {
                    int removeCount = _fullDetectionDataList.Count - 100;
                    _fullDetectionDataList.RemoveRange(0, removeCount);
                }
            }
        }

        private string ExtractCameraNumber(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return string.Empty;

            var pathParts = imagePath.Split(new[] { '\\', '/' },
                StringSplitOptions.RemoveEmptyEntries);

            for (int i = pathParts.Length - 1; i >= 0; i--)
            {
                var part = pathParts[i];
                if (part.Contains("负极") || part.Contains("正极"))
                {
                    var match = Regex.Match(part, @"\d+$");
                    if (match.Success) return match.Value;
                    break;
                }
            }
            return string.Empty;
        }


        #endregion
        #endregion


        // 新增日志到第一行
        public void AddLog(string message, CoreLogLevel level = CoreLogLevel.Info, bool isToDatabase = false,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            // 校验日志内容
            if (string.IsNullOrWhiteSpace(message))
                return;

            switch (level)
            {
                case CoreLogLevel.Info:
                    _logger?.Info(message, isToDatabase, memberName, filePath, lineNumber);
                    break;
                case CoreLogLevel.Warn:
                    _logger?.Warn(message, true, memberName, filePath, lineNumber);
                    break;
                case CoreLogLevel.Error:
                    _logger?.Error(message, true, memberName, filePath, lineNumber);
                    break;
                default:
                    _logger?.Log(level, message, isToDatabase, memberName, filePath, lineNumber);
                    break;
            }

            string uiMessage = $"[{level}]{message}";
            // 创建日志项
            var newLog = Tuple.Create(
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                uiMessage
            );

            // 仅当内容有效时添加到日志集合

            var app = Application.Current;
            if (app?.Dispatcher == null)
                return;  // 应用正在关闭，直接丢弃UI日志

            var dispatcher = app.Dispatcher;
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return;

            try
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    if (LogTable == null) return;

                    LogTable.Insert(0, newLog);

                    // 保持最多100条
                    while (LogTable.Count > 100)
                    {
                        LogTable.RemoveAt(LogTable.Count - 1);
                    }
                }), DispatcherPriority.Background);   // 使用 Background 优先级更安全
            }
            catch (Exception ex) when (ex is ObjectDisposedException ||
                                       ex is InvalidOperationException ||
                                       ex.Message.Contains("shutdown", StringComparison.OrdinalIgnoreCase))
            {
                // 忽略关闭过程中的正常异常
            }

        }
        private void InitDll1()
        {
            // 基础配置（DLL和模型共用的密钥）
            string appKey = "HZ_PTO_LAMINATION";
            string publicKey = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCYVehyrP0NkreL9a98NMEd9TlOEvQbdSifL3NRnogNuBiH7l+sJyAPA8yU1bKT7ce0tqki+0RJuidiKd07Y3nyxGI65r1STUpUO+A/+zjFo+RIIzhk8STQ7nzvhU3OxAcpL6DPFR4eVuNnyyoEf//fU2aSRZDX/WPHmobgsVqyIQIDAQAB";

            // DLL配置
            string dllPythonCode = "PTO_JiSunDiePian_dll";
            string dllExecuteType = "PRED";

            // 模型配置（下载标识PTO_JiSunDiePian_model，保存名为Diepian.onnx）
            //string modelPythonCode = "PTO_JiSunDiePian_model";
            //string modelExecuteType = "PRED"; // 模型下载通常用TRAIN标识

            //string modelFileName = "Diepian.onnx"; // 下载后保存的模型文件名
            //string modelSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelFileName);
            // 正极模型文件
            string zhengModelFileName = "正极.onnx";
            var zhengModelSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, zhengModelFileName);

            // 负极模型文件
            string fuModelFileName = "负极.onnx";
            var fuModelSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fuModelFileName);

            try
            {
                //先完成DLL的初始化（原有逻辑不变）
                var DLLPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "YingYanAlgoCore.dll");
                Assembly? targetAssembly = null;
                Type? targetType = null;

                AddLog("=== 开始初始化：DLL + 模型 ===");
                // 加载DLL（本地优先，不存在则Web下载）
                if (File.Exists(DLLPath))
                {
                    AddLog($"[DLL加载] 发现本地DLL：{DLLPath}，开始加载...");
                    targetAssembly = Assembly.LoadFrom(DLLPath);
                    AddLog($"[DLL加载] 本地DLL加载成功，程序集：{targetAssembly.FullName}");
                }
                else
                {
                    AddLog($"[DLL加载] 本地DLL不存在，从Web下载（标识：{dllPythonCode}）...");
                    // RSA签名生成
                    string xmlPublicKey = RasHelper.ToXmlPublicKey(publicKey);
                    string dllSignature = RasHelper.RSAEncrypt(xmlPublicKey, appKey);
                    if (string.IsNullOrEmpty(dllSignature))
                    {
                        AddLog("[DLL加载] RSA签名生成失败！");
                        throw new Exception("RSA签名生成失败，无法下载DLL");
                    }

                    // Web下载DLL
                    var dllReFile = WebHelper1.DownloadFile1(appKey, dllSignature, dllPythonCode, dllExecuteType);
                    if (dllReFile == null || !dllReFile.Success || dllReFile.DataStr == null || dllReFile.DataStr.Length == 0)
                    {
                        string errorMsg = dllReFile?.Message ?? "下载响应为空";
                        AddLog($"[DLL加载] Web下载失败：{errorMsg}");
                        throw new Exception($"从Web获取DLL失败：{errorMsg}");
                    }
                    AddLog($"[DLL加载] Web下载成功，大小：{dllReFile.DataStr.Length} 字节");

                    // 验证DLL有效性（MZ头）
                    if (dllReFile.DataStr.Length < 2 || dllReFile.DataStr[0] != 0x4D || dllReFile.DataStr[1] != 0x5A)
                    {
                        AddLog("[DLL加载] 下载的不是有效DLL（缺少MZ标识）");
                        throw new Exception("下载的文件不是有效DLL");
                    }

                    // 加载内存DLL
                    targetAssembly = Assembly.Load(dllReFile.DataStr);
                    AddLog($"[DLL加载] 内存DLL加载成功，程序集：{targetAssembly.FullName}");
                }

                // 反射获取DLL中的类和方法
                targetType = targetAssembly.GetType("ZZYingYanAICore.YingYanAI");
                if (targetType == null)
                {
                    AddLog("[反射] 未找到类型：ZZYingYanAICore.YingYanAI");
                    throw new NullReferenceException("DLL中未找到目标类");
                }
                obj2 = Activator.CreateInstance(targetType);
                if (obj2 == null)
                {
                    AddLog("[反射] 无法创建YingYanAI实例");
                    throw new Exception("无法创建DLL类实例");
                }
                ModelInitMethod = targetType.GetMethod("ModelInit");
                ActionGetPointMethod = targetType.GetMethod("ModelActionDiePian");
                if (ModelInitMethod == null || ActionGetPointMethod == null)
                {
                    AddLog($"[反射] 缺少核心方法：ModelInit={(ModelInitMethod != null)}，ModelActionDiePian={(ActionGetPointMethod != null)}");
                    throw new Exception("DLL中未找到核心检测方法");
                }
                AddLog($"[DLL初始化完成] 类实例+方法均就绪");

                // Web下载模型（Diepian.onnx）
                AddLog($"\n=== 开始处理模型：{zhengModelFileName}、{fuModelFileName} ===");
                //bool needDownload = false;
                // 先检查本地是否已有模型
                IsFileExist(zhengModelSavePath, out bool needZhengDownload);
                IsFileExist(fuModelSavePath, out bool needFuDownload);
                //if (needDownload)
                //{
                //    // 本地无模型/模型无效，从Web下载
                //    AddLog($"[模型下载] 本地无有效模型，从Web下载（标识：{modelPythonCode}）...");
                //    // 生成模型下载的RSA签名（与DLL共用密钥）
                //    string modelSignature = RasHelper.RSAEncrypt(RasHelper.ToXmlPublicKey(publicKey), AppKey);
                //    if (string.IsNullOrEmpty(modelSignature))
                //    {
                //        AddLog("[模型下载] RSA签名生成失败！");
                //        throw new Exception("RSA签名生成失败，无法下载模型");
                //    }

                //    // 调用WebHelper1下载模型
                //    var modelReFile = WebHelper1.DownloadFile1(AppKey, modelSignature, modelPythonCode, modelExecuteType);
                //    if (modelReFile == null || !modelReFile.Success || modelReFile.DataStr == null || modelReFile.DataStr.Length == 0)
                //    {
                //        string errorMsg = modelReFile?.Message ?? "下载响应为空";
                //        AddLog($"[模型下载] Web下载失败：{errorMsg}");
                //        // 弹出程序目录，方便用户手动放置模型
                //        Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);
                //        throw new Exception($"从Web获取模型失败：{errorMsg}\n请手动将 {modelFileName} 放置到 {AppDomain.CurrentDomain.BaseDirectory}");
                //    }

                //    // 验证模型文件有效性（至少10KB，避免空文件）
                //    if (modelReFile.DataStr.Length < 1024 * 10) // 10KB阈值
                //    {
                //        AddLog($"[模型下载] 下载的模型文件过小（{modelReFile.DataStr.Length} 字节），视为无效");
                //        throw new Exception("下载的模型文件无效（体积过小）");
                //    }

                //    // 保存模型到本地目录
                //    File.WriteAllBytes(modelSavePath, modelReFile.DataStr);
                //    AddLog($"[模型下载成功] 已保存到：{modelSavePath}（大小：{modelReFile.DataStr.Length} 字节）");
                //}
                // 准备ModelInit参数
                string ZhengGongwei = "PTO与行大面正极";
                string FuGongwei = "PTO与行大面负极";
                string? exInfo = string.Empty;
                string aabbb = string.Empty;
                string[] labels = new string[0];
                string zhengModelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "正极.onnx");
                AddLog($"[模型路径] {zhengModelPath}（是否存在：{File.Exists(zhengModelPath)}）");
                string fuModelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "负极.onnx");
                AddLog($"[模型路径] {fuModelPath}（是否存在：{File.Exists(fuModelPath)}）");

                zhengParam = new object?[6] { zhengModelPath, labels, ZhengGongwei, null, aabbb, true };
                fuParam = new object?[6] { fuModelPath, labels, FuGongwei, null, aabbb, true };

                // 调用ModelInit
                if (ModelInitMethod == null || obj2 == null)
                {
                    AddLog("[检测异常] DLL未初始化（ModelInit方法或实例为空）");
                    throw new Exception("DLL未初始化，ModelInit方法或实例为空");
                }
                ModelInitMethod.Invoke(obj2, zhengParam);
                StationAmodle = zhengParam[3] as IPredictor;
                exInfo = zhengParam[4] as string;

                if (zhengParam != null)
                {
                    if (File.Exists(zhengModelSavePath))
                    {
                        try
                        {
                            File.Delete(zhengModelSavePath);
                            AddLog($"[模型清理] 加载成功，已删除本地模型文件");
                        }
                        catch (Exception ex)
                        {
                            // 捕获文件占用异常（如DLL未释放句柄），仅警告不中断程序
                            AddLog($"[模型清理警告] 加载成功，但无法删除本地文件，错误：{ex.Message}");
                        }
                    }
                }
                else
                {
                    // 加载失败，保留本地文件
                    string error = string.IsNullOrEmpty(exInfo) ? "未知错误" : exInfo;
                    AddLog($"[正极模型初始化失败] {error}，保留本地模型文件供排查：{zhengModelSavePath}");
                    throw new Exception($"模型初始化失败：{error}");
                }
                if (!string.IsNullOrEmpty(exInfo))
                {
                    AddLog($"[正极模型初始化警告] {exInfo}");
                }

                ModelInitMethod.Invoke(obj2, fuParam);
                StationBmodle = fuParam[3] as IPredictor;
                exInfo = fuParam[4] as string;

                if (fuParam != null)
                {
                    if (File.Exists(fuModelSavePath))
                    {
                        try
                        {
                            File.Delete(fuModelSavePath);
                            AddLog($"[模型清理] 加载成功，已删除本地模型文件");
                        }
                        catch (Exception ex)
                        {
                            // 捕获文件占用异常（如DLL未释放句柄），仅警告不中断程序
                            AddLog($"[模型清理警告] 加载成功，但无法删除本地文件，错误：{ex.Message}");
                        }
                    }
                }
                else
                {
                    // 加载失败，保留本地文件
                    string error = string.IsNullOrEmpty(exInfo) ? "未知错误" : exInfo;
                    AddLog($"[负极模型初始化失败] {error}，保留本地模型文件供排查：{fuModelSavePath}");
                    throw new Exception($"模型初始化失败：{error}");
                }
                if (!string.IsNullOrEmpty(exInfo))
                {
                    AddLog($"[负极模型初始化警告] {exInfo}");
                }

                AddLog("[模型初始化] 成功，获取IPredictor实例");
                AddLog("\n=== DLL + 模型 初始化全部完成，可执行检测 ===");
            }
            catch (BadImageFormatException ex)
            {
                AddLog($"[初始化异常] DLL格式错误（32/64位不兼容）：{ex.Message}");
                throw new Exception($"DLL格式错误：{ex.Message}", ex);
            }
            catch (WebException ex)
            {
                string errType = ex.Status == WebExceptionStatus.Timeout ? "网络超时" : "网络错误";
                AddLog($"[初始化异常] {errType}：{ex.Message}");
                throw new Exception($"Web下载异常（{errType}）：{ex.Message}", ex);
            }
            catch (Exception ex)
            {
                AddLog($"[初始化异常] 未知错误：{ex.Message}\n堆栈：{ex.StackTrace}");
                throw new Exception($"初始化失败：{ex.Message}", ex);
            }
        }

        private void IsFileExist(string modelSavePath, out bool needDownload)
        {
            if (File.Exists(modelSavePath))
            {
                // 验证本地模型文件有效性
                FileInfo modelFileInfo = new FileInfo(modelSavePath);
                if (modelFileInfo.Length < 1024) // 小于1KB视为无效
                {
                    AddLog($"[模型加载] 本地模型文件过小（{modelFileInfo.Length} 字节），视为无效，重新下载...");
                    File.Delete(modelSavePath); // 删除无效文件
                }
                else
                {
                    AddLog($"[模型加载] 发现有效本地模型：{modelSavePath}（大小：{modelFileInfo.Length} 字节），无需下载");
                    //return; // 本地模型有效，直接结束初始化
                }
                needDownload = false;
            }
            else
            {
                AddLog($"[模型加载] 本地模型不存在，标记为需要下载");
                needDownload = true;
            }
        }

        /// <summary>
        /// 算法检测
        /// </summary>
        /// <param name="imagePath"></param>
        /// <param name="station"></param>
        /// <param name="triggeredDefects"></param>
        /// <returns></returns>
        /*private string DamageDetection(string imagePath, string? station, out List<string> triggeredDefects)
        {
            #region 参数定义
            // 线宽常量 - 可根据需要调整
            const int RECTANGLE_THICKNESS = 5;  // 框线宽度
            const int TEXT_THICKNESS = 4;       // 文字粗细
            const double FONT_SCALE = 5;      // 字体大小

            float testScore;
            string testName;
            float testWidth;
            float testHeight;
            float testArea;
            #endregion

            #region 初始化变量
            triggeredDefects = new List<string>();
            List<AIResultModel>? defectList = null;
            Mat? imageMat = null;
            string Gongwei = "PTO雅康叠片";
            string exInfo;
            string finalResult = "异常";
            HObject? delRegion = null;
            string polarity = GetStationFromImagePath(imagePath, !_isOfflineMode);

            if (polarity == null)
            {
                AddLog($"[检测异常] 图片路径解析失败或非AB工位：{imagePath}");
                return "异常（路径解析失败/非AB工位）";
            }
            #endregion
            try
            {
                #region ROI区域加载
                string roiKey = IsValidRoiKey(station)
                    ? station!.Trim().ToUpperInvariant()
                    : GetRoiKey(imagePath, polarity);

                if (IsValidRoiKey(roiKey))
                {
                    try
                    {
                        // 优先从缓存复制一份本次检测专用 Region，避免并发检测时误释放缓存对象
                        lock (_roiCacheLock)
                        {
                            if (_detectionRegionCache.TryGetValue(roiKey, out var cachedRegion) &&
                                cachedRegion != null &&
                                cachedRegion.IsInitialized())
                            {
                                HOperatorSet.CopyObj(cachedRegion, out delRegion, 1, -1);
                            }
                        }

                        // 缓存没有则从文件加载，并写入缓存
                        if (delRegion == null || !delRegion.IsInitialized())
                        {
                            HObject? loadedRegion = GenerateDelRegionFromXldFile(roiKey);

                            if (loadedRegion != null && loadedRegion.IsInitialized())
                            {
                                lock (_roiCacheLock)
                                {
                                    if (_detectionRegionCache.TryGetValue(roiKey, out var old))
                                    {
                                        old?.Dispose();
                                        _detectionRegionCache.Remove(roiKey);
                                    }

                                    _detectionRegionCache[roiKey] = loadedRegion;
                                    HOperatorSet.CopyObj(loadedRegion, out delRegion, 1, -1);
                                }

                                AddLog($"[ROI加载] {roiKey} 从XLD生成DelRegion并缓存");
                            }
                            else if (!_warnedNoDelRegionCameras.Contains(roiKey))
                            {
                                AddLog($"[ROI加载] {roiKey} 未配置ROI，本次检测不使用误判区域过滤", CoreLogLevel.Warn);
                                _warnedNoDelRegionCameras.Add(roiKey);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[ROI加载异常] {roiKey}：{ex.Message}", CoreLogLevel.Error);
                    }
                }


                if (delRegion == null || !delRegion.IsInitialized())
                {
                    HOperatorSet.GenEmptyRegion(out delRegion);
                }
                #endregion

                #region 图片读取（重试机制）
                int maxRetry = 5;
                int retryCount = 0;
                int delayMs = 3;
                bool isImageValid = false;

                while (retryCount < maxRetry)
                {
                    Mat tempMat = Cv2.ImRead(imagePath, ImreadModes.Color);
                    if (!tempMat.Empty())
                    {
                        imageMat = tempMat;
                        isImageValid = true;
                        break;
                    }
                    else
                    {
                        tempMat.Dispose();
                    }

                    retryCount++;
                    Task.Delay(delayMs).Wait();
                }

                if (!isImageValid || imageMat == null)
                {
                    string emptyLog = $"[检测异常] 图片为空（经{maxRetry}次重试仍无法读取），绝对路径：{imagePath}";
                    imageMat?.Dispose();
                    AddLog(emptyLog);
                    return "异常（空图片）";
                }
                #endregion

                #region 模型校验
                if (StationAmodle == null)
                {
                    AddLog("[错误] 正极模型为 null，无法调用 ModelActionDiePian");
                    imageMat?.Release();
                    return "异常 H246Zhengmodle 为 null";
                }
                if (StationBmodle == null)
                {
                    AddLog("[错误] 负极模型为 null，无法调用 ModelActionDiePian");
                    imageMat?.Release();
                    return "异常 H246Fumodle 为 null";
                }
                #endregion

                #region AI检测
                if (polarity == "A")
                {
                    ai.ModelActionDiePian(StationAmodle, imageMat, Gongwei, delRegion, out defectList, out exInfo);
                }
                else
                {
                    aiNew.ModelActionDiePian(StationBmodle, imageMat, Gongwei, delRegion, out defectList, out exInfo);
                }

                // 记录 AI 检测结果（包括 exInfo 和 defectList 内容）
                if (!string.IsNullOrEmpty(exInfo) || (defectList != null && defectList.Count > 0))
                {
                    StringBuilder detectionResultLog = new StringBuilder();
                    detectionResultLog.Append($"文件：{imagePath};[AI检测结果] ");

                    if (!string.IsNullOrEmpty(exInfo))
                    {
                        detectionResultLog.Append($"警告信息: {exInfo} | ");
                    }

                    if (defectList != null && defectList.Count > 0)
                    {
                        detectionResultLog.Append($"检测到 {defectList.Count} 个缺陷: ");
                        var defectSummary = string.Join("; ", defectList.Select(d => $"{d.Name}(Score:{d.Score:F5})"));
                        detectionResultLog.Append(defectSummary);
                    }
                    else
                    {
                        detectionResultLog.Append("未检测到缺陷");
                    }

                    AddLog(detectionResultLog.ToString(), CoreLogLevel.Warn, isToDatabase: true);
                }
                #endregion

                #region 核心判定逻辑（优化重点）
                // 默认结果为OK
                finalResult = "OK";

                // 用于存储真正超过阈值的缺陷
                List<AIResultModel> realDefects = new List<AIResultModel>();

                if (defectList != null && defectList.Count > 0)
                {
                    foreach (var defect in defectList)
                    {
                        testName = defect.Name;
                        testScore = defect.Score;
                        testHeight = defect.Height;
                        testWidth = defect.Width;
                        testArea = defect.Area;

                        //日志记录
                        string detailMsg = $"文件名：{imagePath} | 工位：{polarity} | [检测缺陷信息] 类型:{testName} | " + $"置信度:{testScore:F3} | " + $"宽:{testWidth:F6} | " + $"高:{testHeight:F6} | " + $"面积:{testArea:F6} | ";
                        AddLog(detailMsg, isToDatabase: true);

                        // 查找对应的阈值配置
                        var thresholdConfig = DefectDataGrid.FirstOrDefault(
                            item => item.DefectName == testName && item.IsEnable);
                        if (thresholdConfig == null)
                        {
                            AddLog($"[判定跳过] 缺陷类型 [{testName}] 未找到匹配的启用配置，不参与判定。");
                            continue;
                        }
                        //置信度判定
                        bool scoreExceeds = testScore >= thresholdConfig.Credibility;
                        if (!scoreExceeds)
                        {
                            AddLog($"[判定跳过] 缺陷类型 [{testName}] 置信度 {testScore:F3} " +
                                   $"< 阈值 {thresholdConfig.Credibility:F3}，不满足置信度要求。");
                            continue;
                        }
                        // 长宽面积判定
                        bool heightExceeds = Math.Abs(testHeight) > Math.Abs(thresholdConfig.Height);
                        bool widthExceeds = Math.Abs(testWidth) > Math.Abs(thresholdConfig.Width);
                        bool areaExceeds = Math.Abs(testArea) > Math.Abs(thresholdConfig.Area);

                        //bool dimensionExceeds = heightExceeds || widthExceeds || areaExceeds;
                        bool dimensionExceeds = true;


                        if (dimensionExceeds)
                        {
                            // ---------- 构建NG原因明细 ----------
                            List<string> reasons = new List<string>();
                            if (heightExceeds)
                                reasons.Add($"高:{testHeight:F6} > {thresholdConfig.Height:F6}");
                            if (widthExceeds)
                                reasons.Add($"宽:{testWidth:F6} > {thresholdConfig.Width:F6}");
                            if (areaExceeds)
                                reasons.Add($"面积:{testArea:F6} > {thresholdConfig.Area:F6}");

                            string ngMsg = $"[判定NG] 缺陷类型 [{testName}] | " +
                                           $"置信度:{testScore:F3} >= {thresholdConfig.Credibility:F3} | " +
                                           $"超标项：{string.Join("；", reasons)}";
                            AddLog(ngMsg, isToDatabase: true);

                            realDefects.Add(defect);
                        }
                        else
                        {
                            // 置信度满足但尺寸均未超标
                            AddLog($"[判定OK] 缺陷类型 [{testName}] 置信度满足，" +
                                   $"但长({testHeight:F6}/{thresholdConfig.Height:F6})、" +
                                   $"宽({testWidth:F6}/{thresholdConfig.Width:F6})、" +
                                   $"面积({testArea:F6}/{thresholdConfig.Area:F6}) 均未超过阈值。");
                        }
                    }

                    // 只有存在真正超过阈值的缺陷时，才判定为NG
                    if (realDefects.Count > 0)
                    {
                        finalResult = "NG";
                        // 提取本次触发的缺陷类别名称并去重，赋值给 out 参数
                        triggeredDefects = realDefects.Select(d => d.Name).Distinct().ToList();
                    }


                }
                #endregion

                #region 结果绘制与保存
                if (finalResult == "OK")
                {
                    // 绘制OK标记
                    Cv2.PutText(imageMat, "OK", new OpenCvSharp.Point(150, 180),
                                HersheyFonts.HersheySimplex, FONT_SCALE, new Scalar(0, 255, 0), TEXT_THICKNESS);

                    if (IsSaveResultOK)
                    {
                        SaveDetectionResultToPath(imagePath, imageMat, finalResult, true);
                    }
                }
                else // finalResult == "NG"
                {
                    AddLog($"检测完成：{imagePath} → 结果：{finalResult}，缺陷数量：{realDefects.Count}");

                    using var drawMat = imageMat.Clone();   // 克隆原图用于绘制，防止修改原 imageMat

                    // 获取当前图像尺寸
                    int imgW = drawMat.Width;
                    int imgH = drawMat.Height;

                    // 假设原始设计坐标基于 3840x3840
                    const double baseW = 3840.0;
                    const double baseH = 3840.0;

                    // 根据当前图像大小计算缩放比例
                    double scale = Math.Min(imgW / baseW, imgH / baseH);

                    // 限制缩放范围，避免极小图文字太小，极大图文字太大
                    scale = Math.Clamp(scale, 0.45, 2.5);

                    // 自适应参数
                    int rectangleThickness = Math.Max(1, (int)(RECTANGLE_THICKNESS * scale));
                    int defectLabelOffsetY = Math.Max(20, (int)(80 * scale));

                    float defectLabelFontScale = (float)(1.0 * scale);
                    float listFontScale = (float)(1.6 * scale);

                    int startX = Math.Max(10, (int)(240 * scale));
                    int startY = Math.Max(40, (int)(340 * scale));
                    int lineHeight = Math.Max(35, (int)(150 * scale));

                    // 绘制缺陷框和标签
                    foreach (var defect in realDefects)
                    {
                        Cv2.Rectangle(drawMat, defect.Rectangle, new Scalar(0, 0, 255), RECTANGLE_THICKNESS);

                        // 可选：在缺陷旁边标注缺陷名称
                        int labelX = defect.Rectangle.X;
                        int labelY = defect.Rectangle.Y - defectLabelOffsetY;
                        // 防止文字跑到图像外面
                        labelX = Math.Clamp(labelX, 0, imgW - 1);
                        labelY = Math.Clamp(labelY, 30, imgH - 1);

                        var labelPos = new OpenCvSharp.Point(labelX, labelY);
                        PutTextChinese(drawMat, defect.Name, labelPos, defectLabelFontScale, new Scalar(0, 0, 255));
                    }

                    // 绘制缺陷信息列表（左上角）
                    if (realDefects.Count > 0)
                    {
                        string title = "缺陷列表        置信度   宽(mm)    高(mm)   面积(mm²)";

                        // 标题行
                        PutTextChinese(drawMat, title,
                            new OpenCvSharp.Point(startX, startY),
                            listFontScale, new Scalar(255, 0, 0));

                        // 逐行绘制缺陷信息（统一用 drawMat）
                        for (int i = 0; i < realDefects.Count; i++)
                        {
                            var d = realDefects[i];
                            int y = startY + (i + 1) * lineHeight;
                            // 如果超过图像底部，则停止绘制，避免越界
                            if (y > imgH - 20)
                            {
                                break;
                            }

                            string line = $"{i + 1}. {d.Name,-10} {d.Score,-8:F3} {d.Width,-10:F3} {d.Height,-10:F3} {d.Area,-10:F3}";

                            PutTextChinese(drawMat,
                                line,
                                new OpenCvSharp.Point(startX, y),
                                listFontScale, new Scalar(40, 40, 220));   // 深蓝色文字
                        }
                    }

                    // 在左上角画大字 NG（调整位置为左侧，避免与列表重叠）
                    double ngFontScale = 5.2 * scale;
                    int ngThickness = Math.Max(2, (int)(12 * scale));

                    int ngX = Math.Max(10, (int)(150 * scale));
                    int ngY = Math.Max(80, (int)(180 * scale));
                    Cv2.PutText(drawMat, "NG", new OpenCvSharp.Point(ngX, ngY),
                            HersheyFonts.HersheyComplex,
                            ngFontScale,
                            new Scalar(0, 0, 255),
                            ngThickness);

                    // 保存绘制后的图像
                    if (IsSaveResultNG)
                    {
                        // 在保存前添加日志检查
                        AddLog($"准备保存 NG 图：drawMat 尺寸 {drawMat.Width}x{drawMat.Height}，是否为空：{drawMat.Empty()}");

                        SaveDetectionResultToPath(imagePath, drawMat, finalResult, true);


                        // 发送信号进行设备报警 (使用弃元 _ 接收Task，使其在后台执行，不阻塞当前视觉检测流程)
                        //_ = TriggerPLCAlarmAsync(station ?? "未知");
                    }


                }
                #endregion

                // 原图保存
                if (IsSaveOriginalOK && finalResult == "OK")
                {
                    using (Mat originalMat = Cv2.ImRead(imagePath, ImreadModes.Color))
                    {
                        SaveDetectionResultToPath(imagePath, null, finalResult, false);
                    }
                }
                if (IsSaveOriginalNG && finalResult == "NG")
                {
                    using (Mat originalMat = Cv2.ImRead(imagePath, ImreadModes.Color))
                    {
                        SaveDetectionResultToPath(imagePath, originalMat, finalResult, false);
                    }
                }

                return finalResult;
            }
            finally
            {
                #region 资源释放（确保始终执行）

                // 1. 释放 defectList 中所有 AIResultModel 的 HObject 资源
                ReleaseDefectList(defectList);

                // 2. 释放 imageMat
                imageMat?.Dispose();

                // 3. 释放 delRegion（仅当不在缓存中时）
                delRegion?.Dispose();

                #endregion
            }


        }
*/

        private string DamageDetection(string imagePath, string? station, DetectionStatisticsSource sourceType, out List<string> triggeredDefects)
        {
            #region 参数定义
            const int RECTANGLE_THICKNESS = 5;
            const int TEXT_THICKNESS = 4;
            const double FONT_SCALE = 5;

            float testScore;
            string testName;
            float testWidth;
            float testHeight;
            float testArea;
            #endregion

            #region 初始化变量
            triggeredDefects = new List<string>();
            List<AIResultModel>? defectList = null;
            Mat? imageMat = null;
            string Gongwei = "PTO雅康叠片";
            string exInfo;
            string finalResult = "异常";
            HObject? delRegion = null;
            bool isOnline = sourceType == DetectionStatisticsSource.Online;
            string polarity = GetStationFromImagePath(imagePath, isOnline);

            if (polarity == null)
            {
                AddLog($"[检测异常] 图片路径解析失败或非AB工位：{imagePath}");
                return "异常（路径解析失败/非AB工位）";
            }
            #endregion

            AIWorker? worker = null;
            ConcurrentQueue<AIWorker> targetPool = polarity == "A" ? _stationAPool : _stationBPool;

            try
            {
                #region ROI区域加载
                string roiKey = IsValidRoiKey(station)
                    ? station!.Trim().ToUpperInvariant()
                    : GetRoiKey(imagePath, polarity);

                if (IsValidRoiKey(roiKey))
                {
                    try
                    {
                        if (!TryGetDetectionRegionCache(roiKey, out delRegion))
                        {
                            HObject? loadedRegion = GenerateDelRegionFromXldFile(roiKey);

                            if (loadedRegion != null && loadedRegion.IsInitialized())
                            {
                                ReplaceDetectionRegionCache(roiKey, loadedRegion);

                                HOperatorSet.CopyObj(loadedRegion, out delRegion, 1, -1);

                                loadedRegion.Dispose();

                                AddLog($"[ROI加载] {roiKey} 从磁盘读取Region并缓存");
                            }
                            else if (_warnedNoDelRegionCameras.TryAdd(roiKey, 0))
                            {
                                AddLog($"[ROI加载] {roiKey} 未配置ROI，本次检测不使用区域过滤", CoreLogLevel.Warn);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[ROI加载异常] {roiKey}：{ex.Message}", CoreLogLevel.Error);
                    }
                }

                if (delRegion == null || !delRegion.IsInitialized())
                {
                    //HOperatorSet.GenEmptyRegion(out delRegion);
                    delRegion = null;
                }
                #endregion

                #region 图片读取（重试机制）
                int maxRetry = 5;
                int retryCount = 0;
                int delayMs = 3;
                bool isImageValid = false;

                while (retryCount < maxRetry)
                {
                    Mat tempMat = Cv2.ImRead(imagePath, ImreadModes.Color);
                    if (!tempMat.Empty())
                    {
                        imageMat = tempMat;
                        isImageValid = true;
                        break;
                    }
                    else
                    {
                        tempMat.Dispose();
                    }

                    retryCount++;
                    Task.Delay(delayMs).Wait();
                }

                if (!isImageValid || imageMat == null)
                {
                    string emptyLog = $"[检测异常] 图片为空（经{maxRetry}次重试仍无法读取），绝对路径：{imagePath}";
                    imageMat?.Dispose();
                    AddLog(emptyLog);
                    return "异常（空图片）";
                }
                #endregion

                #region 从对象池借用模型工作者
                if (!targetPool.TryDequeue(out worker))
                {
                    AddLog($"[模型池不足] {polarity}工位等待500ms后仍无可用模型实例，跳过本次推理", CoreLogLevel.Error);
                    return "异常 模型池耗尽";
                }
                #endregion

                #region AI检测
                worker.AIInstance.ModelActionDiePian(worker.Model, imageMat, Gongwei, delRegion!, out defectList, out exInfo);

                if (!string.IsNullOrEmpty(exInfo) || (defectList != null && defectList.Count > 0))
                {
                    StringBuilder detectionResultLog = new StringBuilder();
                    detectionResultLog.Append($"文件：{imagePath};[AI检测结果] ");

                    if (!string.IsNullOrEmpty(exInfo))
                    {
                        detectionResultLog.Append($"警告信息: {exInfo} | ");
                    }

                    if (defectList != null && defectList.Count > 0)
                    {
                        detectionResultLog.Append($"检测到 {defectList.Count} 个缺陷: ");
                        var defectSummary = string.Join("; ", defectList.Select(d => $"{d.Name}(Score:{d.Score:F5})"));
                        detectionResultLog.Append(defectSummary);
                    }
                    else
                    {
                        detectionResultLog.Append("未检测到缺陷");
                    }

                    AddLog(detectionResultLog.ToString(), CoreLogLevel.Warn, isToDatabase: true);
                }
                #endregion

                #region 核心判定逻辑
                finalResult = "OK";
                List<AIResultModel> realDefects = new List<AIResultModel>();

                if (defectList != null && defectList.Count > 0)
                {
                    foreach (var defect in defectList)
                    {
                        testName = defect.Name;
                        testScore = defect.Score;
                        testHeight = defect.Height;
                        testWidth = defect.Width;
                        testArea = defect.Area;

                        string detailMsg = $"文件名：{imagePath} | 工位：{polarity} | [检测缺陷信息] 类型:{testName} | " +
                                           $"置信度:{testScore:F3} | 宽:{testWidth:F6} | 高:{testHeight:F6} | 面积:{testArea:F6} | ";
                        AddLog(detailMsg, isToDatabase: true);

                        var thresholdConfig = DefectDataGrid.FirstOrDefault(
                            item => item.DefectName == testName && item.IsEnable);
                        if (thresholdConfig == null)
                        {
                            AddLog($"[判定跳过] 缺陷类型 [{testName}] 未找到匹配的启用配置，不参与判定。");
                            continue;
                        }

                        bool scoreExceeds = testScore >= thresholdConfig.Credibility;
                        if (!scoreExceeds)
                        {
                            AddLog($"[判定跳过] 缺陷类型 [{testName}] 置信度 {testScore:F3} < 阈值 {thresholdConfig.Credibility:F3}，不满足置信度要求。");
                            continue;
                        }

                        bool heightExceeds = Math.Abs(testHeight) > Math.Abs(thresholdConfig.Height);
                        bool widthExceeds = Math.Abs(testWidth) > Math.Abs(thresholdConfig.Width);
                        bool areaExceeds = Math.Abs(testArea) > Math.Abs(thresholdConfig.Area);
                        bool dimensionExceeds = true;

                        if (dimensionExceeds)
                        {
                            List<string> reasons = new List<string>();
                            if (heightExceeds)
                                reasons.Add($"高:{testHeight:F6} > {thresholdConfig.Height:F6}");
                            if (widthExceeds)
                                reasons.Add($"宽:{testWidth:F6} > {thresholdConfig.Width:F6}");
                            if (areaExceeds)
                                reasons.Add($"面积:{testArea:F6} > {thresholdConfig.Area:F6}");

                            string ngMsg = $"[判定NG] 缺陷类型 [{testName}] | 置信度:{testScore:F3} >= {thresholdConfig.Credibility:F3} | 超标项：{string.Join("；", reasons)}";
                            AddLog(ngMsg, isToDatabase: true);

                            realDefects.Add(defect);
                        }
                        else
                        {
                            AddLog($"[判定OK] 缺陷类型 [{testName}] 置信度满足，但长({testHeight:F6}/{thresholdConfig.Height:F6})、宽({testWidth:F6}/{thresholdConfig.Width:F6})、面积({testArea:F6}/{thresholdConfig.Area:F6}) 均未超过阈值。");
                        }
                    }

                    if (realDefects.Count > 0)
                    {
                        finalResult = "NG";
                        triggeredDefects = realDefects.Select(d => d.Name).Distinct().ToList();
                    }
                }
                #endregion

                #region 结果绘制与保存
                if (finalResult == "OK")
                {
                    Cv2.PutText(imageMat, "OK", new OpenCvSharp.Point(150, 180),
                                HersheyFonts.HersheySimplex, FONT_SCALE, new Scalar(0, 255, 0), TEXT_THICKNESS);

                    if (IsSaveResultOK)
                    {
                        SaveDetectionResultToPath(imagePath, imageMat, finalResult, true,sourceType);
                    }
                }
                else
                {
                    AddLog($"检测完成：{imagePath} → 结果：{finalResult}，缺陷数量：{realDefects.Count}");

                    using var drawMat = imageMat.Clone();

                    int imgW = drawMat.Width;
                    int imgH = drawMat.Height;

                    const double baseW = 3840.0;
                    const double baseH = 3840.0;

                    double scale = Math.Min(imgW / baseW, imgH / baseH);
                    scale = Math.Clamp(scale, 0.45, 2.5);

                    int rectangleThickness = Math.Max(1, (int)(RECTANGLE_THICKNESS * scale));
                    int defectLabelOffsetY = Math.Max(20, (int)(80 * scale));

                    float defectLabelFontScale = (float)(1.0 * scale);
                    float listFontScale = (float)(1.6 * scale);

                    int startX = Math.Max(10, (int)(240 * scale));
                    int startY = Math.Max(40, (int)(340 * scale));
                    int lineHeight = Math.Max(35, (int)(150 * scale));

                    var textItems = new List<(string text, OpenCvSharp.Point pos, float fontSize, Scalar color)>();

                    foreach (var defect in realDefects)
                    {
                        Cv2.Rectangle(drawMat, defect.Rectangle, new Scalar(0, 0, 255), RECTANGLE_THICKNESS);

                        int labelX = defect.Rectangle.X;
                        int labelY = defect.Rectangle.Y - defectLabelOffsetY;
                        labelX = Math.Clamp(labelX, 0, imgW - 1);
                        labelY = Math.Clamp(labelY, 30, imgH - 1);

                        textItems.Add((defect.Name, new OpenCvSharp.Point(labelX, labelY), defectLabelFontScale, new Scalar(0, 0, 255)));
                    }

                    if (realDefects.Count > 0)
                    {
                        string title = "缺陷列表        置信度   宽(mm)    高(mm)   面积(mm²)";

                        textItems.Add((title, new OpenCvSharp.Point(startX, startY), listFontScale, new Scalar(255, 0, 0)));

                        for (int i = 0; i < realDefects.Count; i++)
                        {
                            var d = realDefects[i];
                            int y = startY + (i + 1) * lineHeight;
                            if (y > imgH - 20)
                            {
                                break;
                            }

                            string line = $"{i + 1}. {d.Name,-10} {d.Score,-8:F3} {d.Width,-10:F3} {d.Height,-10:F3} {d.Area,-10:F3}";

                            textItems.Add((line, new OpenCvSharp.Point(startX, y), listFontScale, new Scalar(40, 40, 220)));
                        }
                    }

                    DrawTextsChinese(drawMat, textItems);

                    double ngFontScale = 5.2 * scale;
                    int ngThickness = Math.Max(2, (int)(12 * scale));

                    int ngX = Math.Max(10, (int)(150 * scale));
                    int ngY = Math.Max(80, (int)(180 * scale));
                    Cv2.PutText(drawMat, "NG", new OpenCvSharp.Point(ngX, ngY),
                            HersheyFonts.HersheyComplex,
                            ngFontScale,
                            new Scalar(0, 0, 255),
                            ngThickness);

                    if (IsSaveResultNG)
                    {
                        AddLog($"准备保存 NG 图：drawMat 尺寸 {drawMat.Width}x{drawMat.Height}，是否为空：{drawMat.Empty()}");
                        SaveDetectionResultToPath(imagePath, drawMat, finalResult, true,sourceType);
                    }
                }
                #endregion

                if (IsSaveOriginalOK && finalResult == "OK")
                {
                    using (Mat originalMat = Cv2.ImRead(imagePath, ImreadModes.Color))
                    {
                        SaveDetectionResultToPath(imagePath, null, finalResult, false, sourceType);
                    }
                }
                if (IsSaveOriginalNG && finalResult == "NG")
                {
                    using (Mat originalMat = Cv2.ImRead(imagePath, ImreadModes.Color))
                    {
                        SaveDetectionResultToPath(imagePath, originalMat, finalResult, false, sourceType);
                    }
                }

                return finalResult;
            }
            finally
            {
                // 【极度重要】无论成功失败都必须归还模型到对象池
                if (worker != null)
                {
                    targetPool.Enqueue(worker);
                }

                ReleaseDefectList(defectList);
                imageMat?.Dispose();
                delRegion?.Dispose();
            }
        }


        /// <summary>
        /// 触发设备报警（基于配置参数名）
        /// </summary>
        /// <param name="station">工位信息</param>
        private async Task TriggerPLCAlarmAsync(string station)
        {
            try
            {
                // 1. 根据约定的参数名称，从配置池中找到报警地址对象                
                var alarmAddress = _PLCService.RWAddresses.Values.FirstOrDefault(a => a.ParameterName == "报警");

                if (alarmAddress == null)
                {
                    AddLog($"[{station}工位报警失败] 未在PLC配置中找到参数名为'设备NG报警'的地址", CoreLogLevel.Warn);
                    return;
                }

                // 2. 通过地址ID获取关联的PLC ID
                int? plcId = _PLCService.GetPLCIdForAddress(alarmAddress.ID);
                if (!plcId.HasValue)
                {
                    AddLog($"[{station}工位报警失败] 无法根据地址ID({alarmAddress.ID})获取PLC ID", CoreLogLevel.Warn);
                    return;
                }

                // 3. 根据 PLC ID 找到对应的 PLC 对象
                if (!_PLCService.PLCs.TryGetValue(plcId.Value, out var plc))
                {
                    AddLog($"[{station}工位报警失败] 绑定的PLC(ID:{plcId})未找到", CoreLogLevel.Warn);
                    return;
                }

                // 4. 确保 PLC 连接
                bool connected = await _PLCService.EnsurePLCConnectedAsync(plc);
                if (!connected)
                {
                    AddLog($"[{station}工位报警失败] PLC({plc.Name})未连接，无法发送报警信号", CoreLogLevel.Warn);
                    return;
                }

                // 5. 赋予报警触发值并写入
                alarmAddress.MonitorValue = 1;
                await _PLCService.WriteAddressAsync(plc, alarmAddress);
                AddLog($"[{station}工位设备报警] NG信号已发送至 {plc.Name} 的 {alarmAddress.Address} 点位");

            }
            catch (Exception ex)
            {
                AddLog($"[{station}工位设备报警异常] {ex.Message}", CoreLogLevel.Error);
            }
        }

        private void ReleaseDefectList(List<AIResultModel>? defectList)
        {
            if (defectList == null || defectList.Count == 0)
                return;

            foreach (var defect in defectList)
            {
                try
                {
                    // 释放 Region
                    if (defect.Region != null && defect.Region.IsInitialized())
                    {
                        defect.Region.Dispose();
                    }

                    // 释放 Img
                    if (defect.Img != null && defect.Img.IsInitialized())
                    {
                        defect.Img.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    // 记录释放异常但不中断流程
                    Debug.WriteLine($"[资源释放警告] AIResultModel释放异常: {ex.Message}");
                }
            }

            // 清空列表
            defectList.Clear();
        }


        private void DrawTextsChinese(Mat mat, List<(string text, OpenCvSharp.Point pos, float fontSize, Scalar color)> items)
        {
            if (mat == null || mat.Empty() || items == null || items.Count == 0) return;
            try
            {
                using var bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(mat);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    foreach (var (text, pos, fontSize, color) in items)
                    {
                        var c = System.Drawing.Color.FromArgb(255,
                            (int)color.Val2, (int)color.Val1, (int)color.Val0);
                        using var font = new Font("Microsoft YaHei", fontSize * 32, System.Drawing.FontStyle.Bold);
                        using var brush = new SolidBrush(c);
                        g.DrawString(text, font, brush, new PointF(pos.X, pos.Y));
                    }
                }
                using var newMat = OpenCvSharp.Extensions.BitmapConverter.ToMat(bitmap);
                newMat.CopyTo(mat);
            }
            catch (Exception ex)
            {
                AddLog($"[中文批量绘制异常] {ex.Message}", CoreLogLevel.Warn);
            }
        }



        /// <summary>
        /// 复制空图片到指定目录
        /// </summary>
        private void CopyEmptyImageToTarget(string absoluteSourcePath)
        {
            try
            {
                // 基础路径校验
                if (string.IsNullOrWhiteSpace(absoluteSourcePath) || !File.Exists(absoluteSourcePath))
                {
                    AddLog($"[空图复制] 源文件不存在或路径为空：{absoluteSourcePath}");
                    return;
                }

                // 构造目标路径参数
                string? sourceRoot = "E:";
                string currentDate = DateTime.Now.ToString("yyyyMMdd");
                string emptyImageDirName = "空图";

                // 提取源路径相对结构
                string? sourceDir = Path.GetDirectoryName(absoluteSourcePath);
                if (sourceRoot == null || sourceDir == null)
                {
                    AddLog($"[错误] 无法获取有效路径信息，源路径：{absoluteSourcePath}");
                    return;
                }
                string relativeSourceDir = Path.GetRelativePath(sourceRoot, sourceDir);

                // 组合目标路径
                string targetDir = Path.Combine(
                    sourceRoot,
                    "IME复判",
                    currentDate,
                    emptyImageDirName,
                    relativeSourceDir
                );
                string targetFilePath = Path.Combine(targetDir, Path.GetFileName(absoluteSourcePath));
                // 创建目标目录
                Directory.CreateDirectory(targetDir);
                // 复制文件
                File.Copy(absoluteSourcePath, targetFilePath, overwrite: true);
                AddLog($"[空图复制] 成功：{absoluteSourcePath} → {targetFilePath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                AddLog($"[空图复制异常] 无权限访问目标目录：{ex.Message}");
            }
            catch (IOException ex)
            {
                AddLog($"[空图复制异常] 文件读写错误（可能被占用）：{ex.Message}");
            }
            catch (Exception ex)
            {
                AddLog($"[空图复制异常] 未知错误：{ex.Message}\n堆栈：{ex.StackTrace}");
            }
        }
        private async void ExecuteStopDetection()
        {
            bool isOfflineRunning = IsFileDetecting;
            bool isOnlineARunning = IsOnlineDetectionEnabledA;
            bool isOnlineBRunning = IsOnlineDetectionEnabledB;

            if (!isOfflineRunning && !isOnlineARunning && !isOnlineBRunning)
            {
                MessageBox.Show("当前没有任何检测任务在运行", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 组合弹窗提示内容
            string runningTaskName = isOfflineRunning
                ? $"【{(IsStationA ? "A工位" : "B工位")} 离线文件夹批量检测】"
                : $"【在线实时监控 ({(isOnlineARunning ? "A工位 " : "")}{(isOnlineBRunning ? "B工位" : "")})】";

            var result = MessageBox.Show(
                $"是否确定停止当前正在运行的 {runningTaskName}？\n未完成的任务进度将被中断。",
                "确认终止检测", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                AddLog($"[系统指令] 用户中止了 {runningTaskName}，正在有序安全释放资源...", CoreLogLevel.Warn);

                // 1. 中止离线任务 (交给后台循环优雅退出)
                if (isOfflineRunning)
                {
                    _cts?.Cancel();
                    AddLog("[离线检测] 终止信号发送成功，等待当前处理中的单张图片完成...");
                }

                // 2. 中止在线监控任务
                var stopTasks = new List<Task>();

                if (isOnlineARunning)
                {
                    stopTasks.Add(Task.Run(() => StopOnlineMonitoring(Station.A)));
                    IsOnlineDetectionEnabledA = false;
                }

                if (isOnlineBRunning)
                {
                    stopTasks.Add(Task.Run(() => StopOnlineMonitoring(Station.B)));
                    IsOnlineDetectionEnabledB = false;
                }

                if (stopTasks.Count > 0)
                {
                    await Task.WhenAll(stopTasks);
                }

                // 3. 统一刷新状态
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                AddLog($"[终止异常] 停止任务时发生严重错误: {ex.Message}", CoreLogLevel.Error);
                MessageBox.Show($"停止检测失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 执行查看结果命令
        private async void ExecuteViewResult(dynamic? data)
        {
            if (data == null)
            {
                MessageBox.Show("未选中任何检测数据，请先在表格中选择一条记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // ✅ 添加标志位,防止重复触发
            if (_isLoadingResultImage)
            {
                AddLog("[查看结果] 正在加载其他图片,请稍后");
                return;
            }

            _isLoadingResultImage = true;
            CurrentResultImage = null;

            //string? resultImagePath = null;


            try
            {
                string? resultImagePath = GetResultImagePathFromData(data);
                if (string.IsNullOrEmpty(resultImagePath) || !File.Exists(resultImagePath))
                {
                    MessageBox.Show("结果图不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                BitmapImage? bitmap = await Task.Run(() => LoadBitmapImage(resultImagePath));
                await Application.Current.Dispatcher.InvokeAsync(() => {
                    CurrentResultImage = bitmap;
                }, DispatcherPriority.Normal);

                //if (data is ExpandoObject expandoData)
                //{
                //    var dataDict = (IDictionary<string, object?>)expandoData;
                //    // 判断键存在 + 类型转换
                //    if (dataDict.TryGetValue("ResultImagePath", out object? pathObj)
                //        && pathObj is string pathStr
                //        && !string.IsNullOrWhiteSpace(pathStr))
                //    {
                //        resultImagePath = Path.GetFullPath(pathStr);
                //    }
                //}
                //// 兼容其他可能的参数类型
                //else if (data is string strPath && !string.IsNullOrWhiteSpace(strPath))
                //{
                //    resultImagePath = Path.GetFullPath(strPath);
                //}

                //// 路径有效性二次校验
                //if (string.IsNullOrEmpty(resultImagePath))
                //{
                //    AddLog($"[查看失败] 结果图路径为空（动态对象中未找到有效 ResultImagePath）");
                //    MessageBox.Show("结果图路径未生成，请先完成检测", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                //    return;
                //}
                //if (!File.Exists(resultImagePath))
                //{
                //    AddLog($"[查看失败] 结果图文件不存在，路径：{resultImagePath}");
                //    string tips = $"文件可能已被移动/删除，默认存储路径为：\n{Path.GetDirectoryName(resultImagePath)}";
                //    MessageBox.Show($"结果图不存在或路径无效！\n{tips}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                //    return;
                //}


                //// 在后台线程加载图片,避免阻塞UI
                //BitmapImage? bitmap = await Task.Run(() =>
                //{
                //    using (var fileStream = new FileStream(resultImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                //    {
                //        if (fileStream.Length == 0)
                //        {
                //            throw new IOException("结果图文件为空（可能是检测时保存失败）");
                //        }

                //        var bmp = new BitmapImage();
                //        bmp.BeginInit();
                //        bmp.CacheOption = BitmapCacheOption.OnLoad;
                //        bmp.StreamSource = fileStream;
                //        bmp.EndInit();

                //        if (!bmp.IsFrozen)
                //        {
                //            bmp.Freeze(); // 冻结后可跨线程访问
                //        }

                //        return bmp;
                //    }
                //});
                //// 🟢 使用BeginInvoke异步更新UI,不阻塞后台线程
                //await Application.Current.Dispatcher.InvokeAsync(() =>
                //{
                //    CurrentResultImage = bitmap;
                //}, System.Windows.Threading.DispatcherPriority.Background); // 降低优先级,让进度条先更新
                //AddLog($"[查看成功] 已加载结果图：{resultImagePath}（分辨率：{bitmap.PixelWidth}×{bitmap.PixelHeight}）");
            }
            //catch (UnauthorizedAccessException ex)
            //{
            //    AddLog($"[加载失败] 无权限访问结果图：{ex.Message}，路径：{resultImagePath}");
            //    MessageBox.Show($"无权限访问图片文件，请检查文件夹权限！\n路径：{resultImagePath}", "权限错误", MessageBoxButton.OK, MessageBoxImage.Error);
            //}
            //catch (FileFormatException ex)
            //{
            //    AddLog($"[加载失败] 图片格式不支持：{ex.Message}，路径：{resultImagePath}");
            //    MessageBox.Show($"图片格式无效（仅支持 BMP/JPG/PNG/TIFF 等常见格式）！\n路径：{resultImagePath}", "格式错误", MessageBoxButton.OK, MessageBoxImage.Error);
            //}
            //catch (IOException ex)
            //{
            //    AddLog($"[加载失败] 文件读写异常：{ex.Message}，路径：{resultImagePath}");
            //    MessageBox.Show($"图片文件被占用或损坏！\n建议：关闭其他可能打开该文件的程序后重试。", "文件异常", MessageBoxButton.OK, MessageBoxImage.Error);
            //}
            catch (Exception ex)
            {
                AddLog($"[查看结果失败] {ex.Message}", CoreLogLevel.Error);
                MessageBox.Show($"加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoadingResultImage = false;
            }
        }

        // ✅ 提取路径解析逻辑
        private string? GetResultImagePathFromData(dynamic data)
        {
            if (data is ExpandoObject expandoData)
            {
                var dataDict = (IDictionary<string, object?>)expandoData;
                if (dataDict.TryGetValue("ResultImagePath", out object? pathObj)
                    && pathObj is string pathStr
                    && !string.IsNullOrWhiteSpace(pathStr))
                {
                    return Path.GetFullPath(pathStr);
                }
            }
            else if (data is string strPath && !string.IsNullOrWhiteSpace(strPath))
            {
                return Path.GetFullPath(strPath);
            }
            return null;
        }

        // ✅ 提取图片加载逻辑
        private BitmapImage LoadBitmapImage(string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fileStream.Length == 0)
                    throw new IOException("文件为空");

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = fileStream;
                bitmap.EndInit();

                if (!bitmap.IsFrozen)
                    bitmap.Freeze();

                return bitmap;
            }
        }




        // 执行跳转存储命令
        private void ExecuteJumpToStorage(object? parameter)
        {
            if (parameter == null)
            {
                MessageBox.Show("未选中任何检测数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string? resultImagePath = null;
            if (parameter is ExpandoObject expando)
            {
                // 从动态对象字典中获取路径
                var dataDict = (IDictionary<string, object?>)expando;
                if (dataDict.TryGetValue("ResultImagePath", out object? pathObj) && pathObj is string path)
                {
                    resultImagePath = path;
                }
            }

            else if (parameter is string strPath)
            {
                resultImagePath = strPath;
            }

            if (string.IsNullOrWhiteSpace(resultImagePath))
            {
                MessageBox.Show("结果图路径为空", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //处理路径格式
            string fullPath = Path.GetFullPath(resultImagePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                MessageBox.Show($"结果图所在文件夹不存在：{directory}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // 选中文件，若文件不存在则只打开文件夹
                string argument = File.Exists(fullPath)
                    ? $"/select, \"{fullPath}\""
                    : $"\"{directory}\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = argument,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"跳转失败：{ex.Message}\n路径：{fullPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 单张图片检测,异步处理offline
        /// </summary>
        private async Task GetDataFromSingleFile(string filePath)
        {
            // 设置检测状态
            IsFileDetecting = true;
            _isDetecting = true;         
            _isOfflineDetectionCompleted = false;
            _currentOfflineCsvFilePath = null;
            ResetOfflineProgress(1);
            IsOfflineProgressVisible = Visibility.Visible;
            try
            {
                AddLog($"开始检测单张图片：{Path.GetFileName(filePath)}");
                await Task.Run(() =>
                {
                    string station = GetStationFromImagePath(filePath); // 只获取 "A" 或 "B"
                    string roiKey = GetRoiKey(filePath, station);

                    dynamic detectionData = new ExpandoObject();
                    ParsePathToDynamicObject(detectionData, filePath);
                    //string detectResult = DamageDetection(filePath, station);
                    string detectResult = DamageDetection(filePath, roiKey, DetectionStatisticsSource.Offline,out List<string> triggeredList);
                    detectionData.TriggeredDefects = triggeredList;
                    DateTime realDetectionTime = DateTime.Now;
                    if (detectResult.Equals("异常（空图片）", StringComparison.OrdinalIgnoreCase))
                    {
                        //AddLog($"[单张检测] 空图片不统计：{Path.GetFileName(filePath)}");
                        return;
                    }
                    //生成结果图路径并保存
                    bool isResultSaved = (detectResult == "OK" && IsSaveResultOK) || (detectResult == "NG" && IsSaveResultNG);
                    detectionData.ResultImagePath = isResultSaved
                        ? GetResultImagePath(filePath, detectResult, true, DetectionStatisticsSource.Offline)
                        : null;
                    detectionData.DetectionResult = detectResult;
                    // 写入CSV
                    WriteToCsv(filePath, detectResult, false);
                    //若为NG结果，加入队列并生成报表
                    if (detectResult.Equals("NG", StringComparison.OrdinalIgnoreCase))
                    {
                        //将NG图片加入队列
                        AddImageToQueue(filePath, true, "离线", realDetectionTime, DetectionStatisticsSource.Offline);

                        //AddLog($"[单张检测] NG图片已加入队列：{Path.GetFileName(filePath)}，当前电芯组进度：{_cellCodeQueue.Count}/{MaxImagesPerReport}");
                    }

                    var totalStatistics = AddDetectionStatisticsToDatabase(detectResult, triggeredList,DetectionStatisticsSource.Offline);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 先添加到完整集合
                        AddDetectionDataAndEnsureCapacity(detectionData);

                        // 更新显示列表(仅显示NG结果)
                        UpdateDisplayDataList();

                        // 更新统计数据
                        ApplyStatisticsToUI(totalStatistics);

                        // 更新进度条为100%
                        OfflineProgressValue = 100;
                        OfflineProgressTip = "检测完成!";
                    });
                    lock (_queue1Lock)
                    {
                        if (_cellCodeQueue.Count > 0) // 确认有数据才生成
                        {
                            AddLog($"[单张检测] 强制生成报表（当前队列数：{_cellCodeQueue.Count}）");
                            GenerateReport(DetectionStatisticsSource.Offline).Wait();
                            // 生成后清空队列
                            _cellCodeQueue.Clear();
                            _cellCodeSet.Clear();
                        }
                        else
                        {
                            AddLog($"[单张检测] 无NG数据，无需生成报表");
                        }
                    }


                });
            }
            catch (Exception ex)
            {
                AddLog($"单张图片检测失败：{ex.Message}");
                MessageBox.Show($"单张图片检测失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 重置检测状态
                _isDetecting = false;
                IsFileDetecting = false;
                _isOfflineDetectionCompleted = true;
            }
        }

        /// <summary>
        ///  离线模式 文件夹批量检测,带进度条
        /// </summary>
        private async Task GetDataFromFolder(List<string> imageFiles)
        {
            List<string> filteredFiles = new List<string>();
            IsOfflineProgressVisible = Visibility.Visible; 
            _isOfflineDetectionCompleted = false;
            _currentOfflineCsvFilePath = null;
            InitReportFunction();
            //每20张更新1次UI
            int batchUpdateInterval = 20;
            int currentBatchCount = 0;
            foreach (var filePath in imageFiles)
            {
                //路径提取
                string station = GetStationFromImagePath(filePath);
                bool isPolarityMatch = false;
                if (!IsStationA && !IsStationB) //默认匹配
                {
                    isPolarityMatch = true;
                }
                else
                {
                    if ((IsStationA && station == "A") || (IsStationB && station == "B"))
                    {
                        isPolarityMatch = true;
                    }
                }

                //加入筛选列表
                if (isPolarityMatch)
                {
                    filteredFiles.Add(filePath);
                }
            }

            if (filteredFiles.Count == 0)
            {
                AddLog("筛选完成：无匹配图片");
                return;
            }

            int totalCount = filteredFiles.Count;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _isDetecting = true;
            IsFileDetecting = true;
            ResetOfflineProgress(totalCount);
            _totalDetectMilliseconds = 0;
            _completedCountForTimeCalc = 0;

            try
            {
                await Task.Run(async () =>
                {
                    // 遍历筛选后的文件列表
                    for (int i = 0; i < filteredFiles.Count; i++)
                    {
                        var filePath = filteredFiles[i];

                        if (token.IsCancellationRequested)
                            break;

                        long startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                        try
                        {
                            if (!File.Exists(filePath))
                            {
                                UpdateOfflineProgressAfterSingleImage();
                                continue;
                            }

                            string station = GetStationFromImagePath(filePath);
                            string roiKey = GetRoiKey(filePath, station);

                            dynamic detectionData = new ExpandoObject();
                            ParsePathToDynamicObject(detectionData, filePath);

                            string detectResult = DamageDetection(filePath, roiKey, DetectionStatisticsSource.Offline, out List<string> triggeredList);
                            detectionData.TriggeredDefects = triggeredList;

                            // 空图片不统计、不入报表队列
                            if (detectResult.Equals("异常（空图片）", StringComparison.OrdinalIgnoreCase))
                            {
                                AddLog($"[离线批量] 空图片不统计：{Path.GetFileName(filePath)}");
                                continue;
                            }

                            DateTime realDetectionTime = DateTime.Now;
                            bool isNG = detectResult.Equals("NG", StringComparison.OrdinalIgnoreCase);

                            // 建议放到空图片判断之后
                            AddImageToQueue(filePath, isNG, "离线", realDetectionTime, DetectionStatisticsSource.Offline);

                            bool isBatchResultSaved =
                                (detectResult.Equals("OK", StringComparison.OrdinalIgnoreCase) && IsSaveResultOK) ||
                                (detectResult.Equals("NG", StringComparison.OrdinalIgnoreCase) && IsSaveResultNG);

                            detectionData.ResultImagePath = isBatchResultSaved
                                ? GetResultImagePath(filePath, detectResult, true, DetectionStatisticsSource.Offline)
                                : null;

                            detectionData.DetectionResult = detectResult;

                            WriteToCsv(filePath, detectResult, false);

                            currentBatchCount++;
                            // 关键修改：每张有效图片都必须写统计
                            var totalStatistics = AddDetectionStatisticsToDatabase(detectResult, triggeredList, DetectionStatisticsSource.Offline);

                            bool shouldRefreshUI = currentBatchCount % batchUpdateInterval == 0 || currentBatchCount == totalCount;

                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                // 该方法本身只保留 NG 到内存列表，OK 会被丢弃
                                AddDetectionDataAndEnsureCapacity(detectionData);

                                if (shouldRefreshUI)
                                {
                                    UpdateDisplayDataList();
                                    ApplyStatisticsToUI(totalStatistics);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                AddLog($"检测失败：{Path.GetFileName(filePath)}，原因：{ex.Message}");
                            });
                        }
                        finally
                        {
                            long endTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                            long singleDetectMs = endTime - startTime;

                            RecordSingleDetectTime(singleDetectMs);
                            Interlocked.Add(ref _totalDetectMilliseconds, singleDetectMs);
                            Interlocked.Increment(ref _completedCountForTimeCalc);

                            UpdateOfflineProgressAfterSingleImage();
                        }
                    }

                    // 检测结束或终止后的最终进度更新
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        CalculateDetectionStatistics(DetectionStatisticsSource.Offline);
                        _isDetecting = false;

                        // 最终进度确认
                        double finalProgress = Math.Round(
                            (double)_completedOfflineImageCount / totalCount * 100, 1);
                        OfflineProgressValue = finalProgress;

                        if (token.IsCancellationRequested)
                        {
                            OfflineProgressTip = $"检测已终止，已完成 {_completedOfflineImageCount}/{totalCount} 张（{finalProgress}%）"; OnDetectionCompleted(totalCount);
                            OnDetectionCompleted(totalCount);
                            await GenerateOfflineFinalReport();
                            AddLog($"检测已终止！已处理 {_completedOfflineImageCount}/{totalCount} 张图片");
                        }
                        else
                        {
                            OfflineProgressTip = "已完成100.0%，检测结束！";
                            OnDetectionCompleted(totalCount);
                            await GenerateOfflineFinalReport();
                            AddLog($"批量检测完成！共处理 {totalCount} 张符合条件的图片");
                        }
                    });
                }, token);
            }
            catch (OperationCanceledException)
            {
                // 任务被取消时的处理
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AddLog("检测已被用户取消");
                    _isDetecting = false;
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"批量检测异常：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _isDetecting = false;
                IsFileDetecting = false;
            }
        }

        /// <summary>
        /// 更新显示集合
        /// </summary>
        private void UpdateDisplayDataList()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                List<dynamic> targetNgList;

                // 在锁内取快照，保证与清空操作互斥
                lock (_statisticsLock)
                {
                    targetNgList = _fullDetectionDataList
                        .Where(data => data != null)
                        .OrderByDescending(data => data.DetectionTime)
                        .Take(50)
                        .ToList();
                }

                // 后续对 DetectionDataList 的增删（已在 UI 线程，安全）
                var toRemove = DetectionDataList
                    .Where(item => !targetNgList.Contains(item))
                    .ToList();
                foreach (var item in toRemove)
                    DetectionDataList.Remove(item);

                var toAdd = targetNgList
                    .Where(item => !DetectionDataList.Contains(item))
                    .ToList();
                foreach (var item in toAdd)
                {
                    int index = targetNgList.IndexOf(item);
                    if (index < DetectionDataList.Count)
                        DetectionDataList.Insert(index, item);
                    else
                        DetectionDataList.Add(item);
                }

                while (DetectionDataList.Count > 50)
                    DetectionDataList.RemoveAt(DetectionDataList.Count - 1);
            });
        }

        /// <summary>
        /// 释放 AIResultModel 中的非托管资源
        /// </summary>
        private void ReleaseAIResultResources(List<AIResultModel> results)
        {
            if (results == null || results.Count == 0)
                return;

            foreach (var result in results)
            {
                //释放Halcon缺陷区域（HObject）
                if (result.Region != null)
                {
                    result.Region.Dispose();
                    AddLog($"释放Halcon缺陷区域：{result.Name}");
                }

                //释放OpenCV缺陷截图（Mat）
                if (result.Img != null)
                {
                    result.Img.Dispose();
                    AddLog($"释放OpenCV缺陷截图：{result.Name}");
                }
            }
        }

        private List<string> GetImageFilesInFolder(string folderPath, HashSet<string> allowedExtensions)
        {
            // 实现图片文件筛选逻辑
            return Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(file => allowedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();
        }

        //计算检测统计数据
        private void CalculateDetectionStatistics(DetectionStatisticsSource sourceType)
        {
            LoadStatisticsFromDatabaseToUI(sourceType);
        }

        //清空检测列表
        private void ExecuteClearList()
        {
            // 检查列表本身是否已经为空，避免无意义的操作
            if (DetectionDataList.Count == 0 && _fullDetectionDataList.Count == 0)
            {
                MessageBox.Show("当前图片列表已经是空状态，无需清空。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            MessageBoxResult result = MessageBox.Show(
                "确定要清空图片列表吗？此操作不可恢复。",
                "确认清空",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.OK)
            {
                AddLog("[清空列表] 用户取消，操作终止");
                return;
            }

            try
            {
                lock (_statisticsLock)
                {
                    // 关键：先推进清空时间戳，让此刻之前产生、尚未落地的滞后回调全部作废
                    _lastClearTimestamp = DateTime.Now;

                    _fullDetectionDataList.Clear();

                    // ExecuteClearList 由 UI 线程触发，可直接操作 UI 集合
                    DetectionDataList.Clear();
                    CurrentResultImagePath = null;
                    SelectedDetectionData = null;
                    CurrentResultImage = null;
                }

                AddLog("[清空列表] 成功清空界面显示队列与内存缓存");
            }
            catch (Exception ex)
            {
                AddLog($"[清空列表失败] 错误原因: {ex.Message}", CoreLogLevel.Error);
                MessageBox.Show($"清空列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //统计清零
        private void ExecuteClearStatistics()
        {
            if (IsFileDetecting || IsOnlineDetectionEnabledA || IsOnlineDetectionEnabledB)
            {
                MessageBox.Show("【生产安全警告】当前检测任务正在运行！\n严禁在检测过程中重置或清零产量及优率统计数据！\n请先停止检测。",
                                "核心操作被拒绝", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "确定要重置统计数据吗？此操作不可恢复。",
                "确认清零",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.OK)
            {
                AddLog("[统计清零] 用户取消，操作终止");
                return;
            }

            try
            {
                AddLog("[统计清零] 开始执行本地 SQLite 数据库及内存重置...", CoreLogLevel.Warn);

                // 2. 统一作用域
                lock (_statisticsLock)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var statisticsRepo = scope.ServiceProvider.GetRequiredService<IDetectionStatisticsRepository>();

                    // 2.1 清除数据库底层数据
                    statisticsRepo.ClearTotalAndToday(StatisticsModuleCode, DateTime.Today);

                    // 2.2 同步清理内存集合
                    _fullDetectionDataList.Clear();

                    // 2.3 必须在 Lock 块内完成对 UI 敏感属性的重置，确保多线程视界一致
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DetectionDataList.Clear();
                        CurrentResultImagePath = null;
                        SelectedDetectionData = null;
                    });

                    // 2.4 从数据库重新获取全新（归零）的统计实体对象
                    var emptyStatistics = statisticsRepo.GetOrCreate(
                        StatisticsModuleCode,
                        DetectionStatisticsSource.All,
                        DetectionStatisticsScope.Total,
                        TotalStatisticsDate);

                    // 2.5 刷新界面上的各缺陷产量、优率 UI 控件显示
                    ApplyStatisticsToUI(emptyStatistics);
                }

                AddLog("[统计清零] 数据库及界面优率看板已全线同步清零", isToDatabase: true);
                MessageBox.Show("统计数据已成功归零！", "重置成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"[统计清零失败] 发生异常: {ex.Message}", CoreLogLevel.Error);
                MessageBox.Show($"统计清零失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        /// <summary>
        /// 处理实时图片的鼠标滚轮缩放事件
        /// </summary>
        /// <param name="e">鼠标滚轮事件参数，包含滚轮滚动方向/增量等信息</param>
        private void OnZoomRealTimeImage(MouseWheelEventArgs? e)
        {
            if (e == null) return;

            // e.Delta：滚轮增量（正值=向前滚动=放大，负值=向后滚动=缩小）
            if (e.Delta > 0)
            {
                // 放大逻辑：
                // 1. 缩放比例 += 缩放步长（ZoomStep）
                // 2. Math.Min 限制：放大后的比例不超过最大缩放比例（MaxScale），避免无限放大
                // 3. ScaleX/ScaleY 同步赋值，保证图片等比例缩放（不变形）
                RealTimeImageScaleX = Math.Min(RealTimeImageScaleX + ZoomStep, MaxScale);
                RealTimeImageScaleY = RealTimeImageScaleX;
            }
            else
            {
                // 缩小逻辑：
                // 1. 缩放比例 -= 缩放步长（ZoomStep）
                // 2. Math.Max 限制：缩小后的比例不低于最小缩放比例（MinScale），避免无限缩小
                // 3. ScaleX/ScaleY 同步赋值，保证图片等比例缩放（不变形）
                RealTimeImageScaleX = Math.Max(RealTimeImageScaleX - ZoomStep, MinScale);
                RealTimeImageScaleY = RealTimeImageScaleX;
            }
            //阻止事件冒泡
            e.Handled = true;
        }

        /// <summary>
        /// 手动放大实时图片（按钮/快捷键触发）
        /// 核心逻辑：等比例放大，且不超过最大缩放比例
        /// </summary>
        private void OnZoomInRealTime()
        {
            // 1. 放大X轴缩放比例：当前比例 + 缩放步长，且不超过最大缩放比例（MaxScale）
            // Math.Min 防止放大比例超出上限（比如最大5倍，就不会超过5）
            RealTimeImageScaleX = Math.Min(RealTimeImageScaleX + ZoomStep, MaxScale);
            // 2. Y轴同步X轴比例，保证图片等比例缩放（避免拉伸变形）
            RealTimeImageScaleY = RealTimeImageScaleX;
        }

        /// <summary>
        /// 手动缩小实时图片（按钮/快捷键触发）
        /// 核心逻辑：等比例缩小，且不低于最小缩放比例
        /// </summary>
        private void OnZoomOutRealTime()
        {
            RealTimeImageScaleX = Math.Max(RealTimeImageScaleX - ZoomStep, MinScale);
            RealTimeImageScaleY = RealTimeImageScaleX;
        }

        /// <summary>
        /// 重置实时图片的缩放和平移（恢复初始状态）
        /// 核心逻辑：缩放比例归1（原始大小），平移位置归0（初始位置）
        /// </summary>
        private void OnResetRealTimeZoom()
        {
            // 1. 缩放比例重置为1.0（图片原始大小）
            RealTimeImageScaleX = 1.0;
            RealTimeImageScaleY = 1.0;
            // 2. 平移位置重置为0（X/Y轴都回到初始位置，消除所有拖拽平移的偏移）
            RealTimeImageTranslateX = 0; // 回到X轴初始位置
            RealTimeImageTranslateY = 0; // 回到Y轴初始位置
        }

        /// <summary>
        /// 处理图片鼠标左键按下事件：初始化拖拽状态，记录起始信息，为后续拖拽平移做准备
        /// </summary>
        /// <param name="e">鼠标按钮事件参数，包含事件源、鼠标位置等关键信息</param>
        private void OnImageMouseDown(MouseButtonEventArgs? e)
        {
            if (e == null) return;
            if (e.Source is System.Windows.Controls.Image image)
            {
                // 标记图片进入拖拽状态（供MouseMove事件判断是否执行拖拽逻辑）
                _isImageDragging = true;
                // 记录起始鼠标位置
                // 记录鼠标按下时，相对于Image控件的初始坐标（以控件左上角为原点）
                // 作用：后续移动时计算鼠标偏移量的基准点
                _startMousePoint = e.GetPosition(image);
                // 记录起始偏移量
                // 记录拖拽开始前，图片已有的平移偏移量（RealTimeImageTranslateX/Y）
                // 作用：基于已有偏移量叠加新的拖拽距离，避免拖拽时图片跳回原点
                _startTranslateX = RealTimeImageTranslateX;
                _startTranslateY = RealTimeImageTranslateY;
                // 捕获鼠标：将鼠标事件锁定到当前Image控件
                // 效果：即使鼠标移出Image控件，后续的MouseMove/MouseUp事件仍会触发，保证拖拽连续性
                image.CaptureMouse();
            }
            e.Handled = true; // 防止事件冒泡
        }

        /// <summary>
        /// 鼠标移动：计算拖动偏移
        /// </summary>
        private void OnImageMouseMove(MouseEventArgs? e)
        {
            if (e == null) return;
            if (!_isImageDragging || e.Source is not System.Windows.Controls.Image image)
                return;

            // 计算当前鼠标位置与起始位置的偏移
            System.Windows.Point currentMousePoint = e.GetPosition(image);
            double offsetX = currentMousePoint.X - _startMousePoint.X;
            double offsetY = currentMousePoint.Y - _startMousePoint.Y;

            // 更新拖动偏移
            RealTimeImageTranslateX = _startTranslateX + offsetX;
            RealTimeImageTranslateY = _startTranslateY + offsetY;
            e.Handled = true;
        }

        /// <summary>
        /// 鼠标松开/离开：结束拖动
        /// </summary>
        private void OnImageMouseUp(MouseEventArgs? e)
        {
            if (e == null) return;
            if (e.Source is System.Windows.Controls.Image image)
            {
                _isImageDragging = false;
                image.ReleaseMouseCapture();
            }
            e.Handled = true;
        }

        /// <summary>
        /// 记录单张图片的检测耗时
        /// </summary>
        /// <param name="elapsedMilliseconds">单张检测耗时（毫秒）</param>
        public void RecordSingleDetectTime(long elapsedMilliseconds)
        {
            lock (_batchLock)
            {
                // 累计当前批次的数量和总耗时
                _currentBatchCount++;
                _currentBatchTotalMs += elapsedMilliseconds;

                // 若当前批次满200张，计算平均耗时并保存为“上一批次平均”
                if (_currentBatchCount >= BatchSize)
                {
                    _lastBatchAvgMs = (double)_currentBatchTotalMs / _currentBatchCount;
                    // 重置当前批次）
                    _currentBatchCount = 0;
                    _currentBatchTotalMs = 0;
                }
            }
        }
        /// <summary>
        /// 重置离线进度条
        /// </summary>
        /// <param name="totalImageCount">本次检测的总图片数量</param>
        public void ResetOfflineProgress(int totalImageCount = 0)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _totalOfflineImageCount = totalImageCount;
                _completedOfflineImageCount = 0;

                // 重置批次统计数据
                lock (_batchLock)
                {
                    _currentBatchCount = 0;
                    _currentBatchTotalMs = 0;
                    _lastBatchAvgMs = 0;
                }

                OfflineProgressValue = 0;
                OfflineProgressTip = "准备开始检测...";
            });
        }

        /// <summary>
        /// 单张图片检测完成后，按批次更新进度
        /// </summary>
        public void UpdateOfflineProgressAfterSingleImage()
        {
            // 累加已完成总数量
            Interlocked.Increment(ref _completedOfflineImageCount);
            int completed = _completedOfflineImageCount;
            int total = _totalOfflineImageCount;

            // 检查总图片数是否有效
            if (total <= 0)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OfflineProgressValue = 0;
                    OfflineProgressTip = $"已完成 {completed} 张，总图片数未知";
                });
                return;
            }

            bool isBatchCompleted = (completed % _progressUpdateInterval == 0);
            bool isLastImage = (completed == total);
            if (!isBatchCompleted && !isLastImage)
            {
                return;
            }

            // 获取上一批次的平均耗时
            double avgMsPerImage;
            lock (_batchLock)
            {
                // 优先用"上一个完整批次"的平均
                if (_lastBatchAvgMs > 0)
                {
                    avgMsPerImage = _lastBatchAvgMs;
                }
                // 若还没有完整批次，用当前批次的临时平均
                else if (_currentBatchCount > 0)
                {
                    avgMsPerImage = (double)_currentBatchTotalMs / _currentBatchCount;
                }
                else
                {
                    avgMsPerImage = 15; // 初始15毫秒/张
                }
            }

            // 限制单张耗时在合理范围
            double avgSecondsPerImage = Math.Clamp(avgMsPerImage / 1000, 0.01, 5.0);


            Application.Current.Dispatcher.Invoke(() =>
            {
                double progress = 0;
                if (total > 0 && completed >= 0)
                {
                    progress = (double)completed / total * 100;
                    progress = Math.Round(Math.Min(progress, 100), 1);
                    if (double.IsNaN(progress) || double.IsInfinity(progress))
                    {
                        progress = 0;
                    }
                }

                // 设置进度值
                OfflineProgressValue = progress;

                // 剩余数量和剩余时间
                int remainingCount = Math.Max(0, total - completed);
                double remainingSeconds = remainingCount * avgSecondsPerImage;
                string remainingTimeText = FormatTime(remainingSeconds);

                // 更新提示文本
                if (isLastImage)
                {
                    OfflineProgressTip = $"已完成100.0%，检测结束！";
                }
                else
                {
                    OfflineProgressTip = $"已完成{progress}%（{completed}/{total}），预计还需{remainingTimeText}";
                }
            });
        }

        /// <summary>
        /// 将秒数格式化为「XX分XX秒」或「XX秒」
        /// </summary>
        private string FormatTime(double totalSeconds)
        {
            if (totalSeconds < 0) return "未知";

            int minutes = (int)totalSeconds / 60;
            int seconds = (int)totalSeconds % 60;

            if (minutes > 0)
            {
                return $"{minutes}分{seconds}秒";
            }
            else
            {
                return $"{seconds}秒";
            }
        }


        // 解析路径到动态对象
        private void ParsePathToDynamicObject(dynamic data, string filePath, bool isOnline = false)
        {
            data.ImagePath = filePath;
            data.ImageName = "未知图片";
            data.Polarity = "未知极性";
            data.Camerainformation = "未知相机";
            data.ResultImagePath = null;
            data.PartType = "未知类型";
            data.DetectionTime = DateTime.Now;

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            try
            {
                data.ImageName = Path.GetFileName(filePath);
                string? dirPath = Path.GetDirectoryName(filePath);
                if (dirPath == null)
                {
                    AddLog($"[错误] 无法获取图片目录: {filePath}");
                    return;
                }

                //DirectoryInfo currentDir = new DirectoryInfo(dirPath);

                data.Camerainformation = GetRoiKey(filePath, GetStationFromImagePath(filePath, isOnline));
                //data.Polarity = GetStationFromImagePath(filePath, isOnline);
                data.Polarity = filePath.Contains(@"\负极\") ? "负极" : "正极";
                data.PartType = data.Polarity;
            }
            catch (Exception ex)
            {
                AddLog($"路径解析异常：{ex.Message}，路径：{filePath}");
            }
        }


        // 生成保存路径
        /// <summary>
        /// 获取结果图/原图的存储路径
        /// </summary>
        /// <param name="imagePath">原始图片路径</param>
        /// <param name="result">检测结果（OK/NG）</param>
        /// <param name="isResultImage">是否为结果图（true=结果图，false=原图）</param>
        /// <returns>构造后的完整路径，失败返回null</returns>
        private string? GetResultImagePath(string imagePath, string result, bool isResultImage, DetectionStatisticsSource sourceType)
        {
            try
            {
                // ===================== 第一步：判断离线/在线模式 =====================
                bool isOffline = sourceType == DetectionStatisticsSource.Offline;
                string lineStatus = isOffline ? "离线" : "在线";

                // ===================== 第二步：确定根目录（容错适配） =====================
                string? rootDirectory = GetValidRootDirectory();

                if (string.IsNullOrEmpty(rootDirectory))
                {
                    AddLog("[严重错误] 无法获取任何有效的存储盘符（D/E盘已满或不存在，且备用路径无效）", CoreLogLevel.Error);
                    return null;
                }

                // ===================== 第三步：构造路径核心参数 =====================
                // 日期参数：在线模式用当前时间，离线模式用初始化的固定日期
                string currentDate = DateTime.Now.ToString("yyyyMMdd");

                // 从原始路径提取日期文件夹（失败则用当前日期兜底）
                string dateFolder = GetDateFromPath(imagePath);
                if (string.IsNullOrEmpty(dateFolder))
                {
                    dateFolder = DateTime.Now.ToString("yyyyMMdd");
                }
                if (dateFolder != currentDate)
                {
                    dateFolder = isOffline ? $"{currentDate}_{dateFolder}" : dateFolder;
                    AddLog($"[日期提取] 从路径提取的日期文件夹 '{dateFolder}' 与当前日期 '{currentDate}' 不匹配，已使用当前日期作为兜底");
                }

                // 从原始路径提取业务属性参数（均做兜底处理，避免空值导致路径拼接失败）
                string stationAB = GetStationFromImagePath(imagePath, sourceType == DetectionStatisticsSource.Online);
                string roiKey = GetRoiKey(imagePath, stationAB);// A1,B2

                string cellName = GetCellNameFromPath(imagePath);// 电芯编码
                if (string.IsNullOrEmpty(cellName)) cellName = "未知电芯";

                // 提取原始文件的名称和扩展名（兜底处理）
                string fileName = Path.GetFileNameWithoutExtension(imagePath);// 无扩展名的文件名
                if (string.IsNullOrEmpty(fileName)) fileName = "未知文件";
                string fileExt = Path.GetExtension(imagePath);
                if (string.IsNullOrEmpty(fileExt))
                {
                    fileExt = ".jpg";
                    AddLog("[扩展名] 提取失败，使用默认值: .jpg");
                }

                // 区分图像类型（原图/结果图）
                string imageType = isResultImage ? "结果图" : "原图";
                // 构造新文件名：结果图追加检测结果，原图保持原名
                string newFileName = isResultImage
                    ? $"{roiKey}_{fileName}_{result}{fileExt}"
                    : $"{roiKey}_{fileName}{fileExt}";

                // ===================== 第四步：拼接完整路径 =====================
                // 初始化路径片段列表（基础结构：根目录\IME复判\在线状态\日期\工位\检测结果\原结图\文件名）
                List<string> pathParts = new List<string>
                {
                    rootDirectory, "IME复判", lineStatus, dateFolder, $"{stationAB}工位", result, imageType, newFileName
                };

                // 过滤空路径片段（避免拼接出无效路径如"\\空值\\文件.jpg"）
                if (string.IsNullOrEmpty(pathParts[0]) || string.IsNullOrEmpty(pathParts[pathParts.Count - 1]))
                {
                    AddLog("[错误] 根目录或文件名为空，无法拼接路径", CoreLogLevel.Error);
                    return null;
                }

                // 拼接所有路径片段并标准化（解决路径分隔符、重复目录等问题）
                string savePath = Path.GetFullPath(Path.Combine(pathParts.ToArray()));
                // 验证路径长度（Windows限制260字符）
                if (savePath.Length > 260)
                {
                    AddLog($"[警告] 路径长度超过260字符限制: {savePath.Length}", CoreLogLevel.Warn);
                }

                //AddLog($"[路径构造成功] {imageType}路径: {savePath}");
                return savePath;
            }
            catch (ArgumentException ex)
            {
                AddLog($"[路径构造失败] 路径参数无效: {ex.Message}", CoreLogLevel.Error);
                return null;
            }
            catch (Exception ex)
            {
                AddLog($"[路径构造失败] {ex.GetType().Name}: {ex.Message}", CoreLogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// 获取有效的根目录（优先D盘，空间不足转E盘，最后回退到文档）
        /// </summary>
        private string? GetValidRootDirectory()
        {
            // 缓存有效则直接返回，无需每次都扫描盘符
            if (_cachedRootDirectory != null && (DateTime.Now - _rootDirLastChecked).TotalMinutes < ROOT_DIR_CACHE_MINUTES)
            {
                return _cachedRootDirectory;
            }

            // 1. 优先检查列表
            //string[] preferredDrives = { "D:", "E:", "F:" };
            string[] preferredDrives = { "E:", "F:" };
            string? selectedDir = null;

            foreach (var driveName in preferredDrives)
            {
                try
                {
                    // 检查驱动器是否存在且就绪
                    DriveInfo drive = new DriveInfo(driveName + "\\");
                    if (!drive.IsReady)
                    {
                        continue;
                    }

                    if (drive.AvailableFreeSpace > MIN_FREE_SPACE_BYTES)
                    {
                        selectedDir = driveName;
                        _warnedDrives.Remove(driveName);
                        break; // 找到满足条件的盘符，停止扫描
                    }
                    else
                    {
                        // 空间不足时记录警告（实际业务问题，必须记录）
                        if (_warnedDrives.Add(driveName)) // Add返回true表示是新增的
                        {
                            double freeSpaceGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                            string pushContent = $"[存储警告] {driveName} 剩余可用空间：{freeSpaceGB:F2} GB，" +
                                                 $"不满足最低要求 {MIN_FREE_SPACE_BYTES / 1024 / 1024 / 1024} GB";
                            Growl.Warning(pushContent);
                            AddLog($"[存储警告] {driveName} 剩余空间不足，尝试切换至备用盘...",
                                   CoreLogLevel.Warn);

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var response = await _webApiClient.PushMessageAsync(pushContent);
                                    if (response.Code != 200)
                                        _logger.Warn($"消息推送失败：{response.Message}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.Warn($"消息推送异常：{ex.Message}");
                                }
                            });
                        }
                    }
                }
                catch (DriveNotFoundException)
                {
                    AddLog($"[存储检查] {driveName} 驱动器不存在，跳过", CoreLogLevel.Debug);
                }
                catch (Exception ex)
                {
                    AddLog($"[存储检查] {driveName} 检查异常：{ex.Message}，跳过", CoreLogLevel.Warn);
                }
            }

            // ── 首选盘符均不可用，进入兜底逻辑 ──────────────────────────────
            if (selectedDir == null)
            {
                // 兜底方案A：系统文档目录
                try
                {
                    string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (Directory.Exists(docPath))
                    {
                        string? docRoot = Path.GetPathRoot(docPath);
                        if (!string.IsNullOrEmpty(docRoot))
                        {
                            DriveInfo docDrive = new DriveInfo(docRoot);
                            if (docDrive.IsReady && docDrive.AvailableFreeSpace > 500L * 1024 * 1024)
                            {
                                selectedDir = docPath;
                                AddLog($"[存储降级] 首选盘符（D/E/F/G）均不可用，已切换至文档目录：{docPath}", CoreLogLevel.Warn);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"[存储降级] 文档目录检查失败：{ex.Message}", CoreLogLevel.Warn);
                }

                // 兜底方案B：程序运行目录
                if (selectedDir == null)
                {
                    try
                    {
                        string appPath = AppDomain.CurrentDomain.BaseDirectory;
                        string? rootPath = Path.GetPathRoot(appPath);
                        if (!string.IsNullOrEmpty(rootPath))
                        {
                            DriveInfo appDrive = new DriveInfo(rootPath);
                            if (appDrive.IsReady && appDrive.AvailableFreeSpace > 500L * 1024 * 1024)
                            {
                                selectedDir = appPath;
                                AddLog($"[存储降级] 文档目录也不可用，已切换至程序运行目录：{appPath}", CoreLogLevel.Warn);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[存储降级] 程序目录检查失败：{ex.Message}", CoreLogLevel.Warn);
                    }
                }

                // 所有方案均失败
                if (selectedDir == null)
                {
                    AddLog("[严重错误] 无法获取任何有效的存储路径，所有盘符均不可用！", CoreLogLevel.Error);
                    // 清空缓存，下次调用时强制重新检测
                    _cachedRootDirectory = null;
                    return null;
                }

                // 进入降级路径时通知用户（仅弹一次）
                NotifyStorageDegraded(selectedDir);
            }
            // ── 仅在根目录发生切换时才记录日志 ──────────────────────────────
            if (selectedDir != _lastUsedRootDirectory)
            {
                if (_lastUsedRootDirectory == null)
                {
                    // 首次初始化
                    AddLog($"[存储初始化] 当前存储根目录：{selectedDir}");
                }
                else
                {
                    // 发生了切换（如从 D 盘切换到 E 盘）
                    AddLog($"[存储切换] 根目录由 {_lastUsedRootDirectory} 切换为 {selectedDir}", CoreLogLevel.Warn);
                }
                _lastUsedRootDirectory = selectedDir;
            }

            // ── 更新缓存 ──────────────────────────────────────────────────────
            _cachedRootDirectory = selectedDir;
            _rootDirLastChecked = DateTime.Now;

            return _cachedRootDirectory;
        }
        /// <summary>
        /// 存储路径降级时通知用户（仅弹窗一次，避免频繁打扰）
        /// </summary>
        private void NotifyStorageDegraded(string fallbackPath)
        {
            lock (_storageDegradedLock)
            {
                if (_hasNotifiedStorageDegraded) return;
                _hasNotifiedStorageDegraded = true;
            }

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Growl.Warning($"D/E盘存储空间不足！\n图片已切换保存至：\n{fallbackPath}\n请及时清理磁盘空间！");
            });
        }

        /// <summary>
        /// 主动使根目录缓存失效（如手动清理磁盘后，强制重新检测）
        /// </summary>
        public void InvalidateRootDirectoryCache()
        {
            _cachedRootDirectory = null;
            _hasNotifiedStorageDegraded = false; // 同时重置弹窗标志，允许再次通知
            _warnedDrives.Clear();
            AddLog("[存储缓存] 已手动使根目录缓存失效，下次保存时将重新检测盘符", CoreLogLevel.Warn);
        }



        #region 参数管控-保存路径


        /// <summary>
        /// 保存检测结果图像到指定目标目录（重构后的统一入口）
        /// </summary>
        /// <param name="originalImagePath">原始图片路径（用于生成保存路径）</param>
        /// <param name="imageMat">待保存的图像 Mat 对象</param>
        /// <param name="result">检测结果（OK / NG）</param>
        /// <param name="isResultImage">true = 结果图，false = 原图</param>
        /// <param name="target">保存目标目录</param>
        private void SaveDetectionResultToPath(string originalImagePath, Mat? imageMat, string result, bool isResultImage, DetectionStatisticsSource sourceType)
        {
            try
            {
                // 根据目标选择对应的路径生成方法
                string? savePath = GetResultImagePath(originalImagePath, result, isResultImage,sourceType);

                if (string.IsNullOrEmpty(savePath))
                {
                    AddLog($"[保存失败] 无法生成保存路径，目标：IME复判，原始路径：{originalImagePath}");
                    return;
                }

                string? directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (isResultImage)
                {
                    if (imageMat == null || imageMat.Empty())
                    {
                        AddLog($"[保存失败] 结果图为空：{originalImagePath}", CoreLogLevel.Warn);
                        return;
                    }

                    Cv2.ImWrite(savePath, imageMat);
                }
                else
                {
                    if (!File.Exists(originalImagePath))
                    {
                        AddLog($"[保存失败] 原图不存在：{originalImagePath}", CoreLogLevel.Warn);
                        return;
                    }

                    if (sourceType == DetectionStatisticsSource.Offline)
                    {
                        File.Copy(originalImagePath, savePath, overwrite: true);
                    }
                    else
                    {
                        File.Move(originalImagePath, savePath, overwrite: true);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"图像[保存异常] ，原因：{ex.Message}");
            }
        }


        #endregion

        /// <summary>
        /// 检查文件路径是否符合当前的检测模式 (全检/OK/NG)
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>如果不符合模式则返回 false</returns>
        private bool IsDetectionModeMatch(string filePath)
        {
            // 这里建议转为大写进行判断，防止文件名大小写不一致 (例如 Ok, ok, OK)
            // 如果你的系统严格区分大小写，可以去掉 .ToUpper()
            string pathUpper = filePath.ToUpper();
            bool isMatch = false;

            switch (SelectedMode)
            {
                case DetectionMode.OK:
                    // 如果选了OK模式，路径必须不包含 "NG"
                    isMatch = !pathUpper.Contains("NG");
                    break;

                case DetectionMode.NG:
                    // 如果选了NG模式，路径必须包含 "NG"
                    isMatch = pathUpper.Contains("NG");
                    break;

                case DetectionMode.全检:
                    isMatch = true;
                    break;
                default:
                    // 默认拦截，防止未知模式
                    isMatch = false;
                    break;
            }
            if (!isMatch)
            {
                AddLog($"[模式不匹配] 文件:{filePath}, 当前模式:{SelectedMode}, 路径特征:NG={pathUpper.Contains("NG")}", CoreLogLevel.Info);
            }

            return isMatch;
        }

        /// <summary>
        /// 获取工位标识（返回 "A" 或 "B"，供业务判断使用）
        /// </summary>
        private string GetStationFromImagePath(string imagePath, bool isOnline = false)
        {
            // 构造区分 isOnline 的缓存键
            string cacheKey = $"{(isOnline ? "online" : "offline")}|{imagePath}";

            if (_polarityCache.TryGetValue(cacheKey, out string? cached)
                && cached != "工位解析失败") // ✅ 错误值不缓存
                return cached;

            try
            {
                string station = isOnline
                    ? GetStationOnlineMode(imagePath)
                    : GetStationOfflineMode(imagePath);

                // ✅ 只缓存有效值
                if (station != "未知极性" && station != "错误设置")
                {
                    _polarityCache[cacheKey] = station;
                }
                return station;
            }
            catch (Exception ex)
            {
                AddLog($"[工位提取失败] {ex.Message}，路径：{imagePath}");
                return "工位解析失败"; // ✅ 不写入缓存
            }
        }

        /// <summary>
        /// 获取 ROI 缓存键（返回 "A1"、"B2" 等，供 ROI 查找使用）
        /// </summary>
        private string GetRoiKey(string imagePath, string station)
        {
            string cameraNo = ExtractCameraNumber(imagePath);
            if (string.IsNullOrEmpty(cameraNo))
            {
                AddLog($"[ROI键警告] 无法提取相机号，路径：{imagePath}，工位：{station}");
                return station; // 兜底返回工位
            }
            return $"{station}{cameraNo}";
        }



        /// <summary>
        /// 在线模式：根据路径判定工位
        /// </summary>
        private string GetStationOnlineMode(string imagePath)
        {
            if (IsStationA && imagePath.Contains(SelectedMonitorFolderPathA!))
                return Station.A.ToString();

            if (IsStationB && imagePath.Contains(SelectedMonitorFolderPathB!))
                return Station.B.ToString();

            return "未知极性";
        }

        /// <summary>
        /// 离线模式：根据配置判定工位
        /// </summary>
        private string GetStationOfflineMode(string imagePath)
        {
            // 验证配置有效性
            if (IsStationA && IsStationB)
            {
                LogAndNotify("离线测试不允许同时选中A工位和B工位！", imagePath);
                return "错误设置";
            }

            if (!IsStationA && !IsStationB)
            {
                LogAndNotify("离线测试必须选中A工位或B工位！", imagePath);
                return "错误设置";
            }

            return IsStationA ? Station.A.ToString() : Station.B.ToString();
        }

        /// <summary>
        /// 记录日志并显示提示
        /// </summary>
        private void LogAndNotify(string message, string imagePath)
        {
            AddLog($"{message}，路径：{imagePath}", CoreLogLevel.Warn);
            Growl.Warning(message);
        }

        /// <summary>
        /// 从路径提取电芯号
        /// </summary>
        private string GetCellNameFromPath(string imagePath)
        {
            try
            {
                // 方式1：优先从文件名中提取电芯码（最直接）
                string fileName = Path.GetFileName(imagePath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    string[] parts = fileName.Split('-');
                    if (parts.Length > 0 && parts[0].All(char.IsDigit) && parts[0].Length == 9)
                    {
                        return parts[0];
                    }
                }

                // 方式2：备选方案 - 从目录路径中提取
                string? dirPath = Path.GetDirectoryName(imagePath);
                if (dirPath == null)
                {
                    AddLog($"[错误] 无法获取图片目录: {imagePath}");
                    return "异常（无法获取图片目录）";
                }

                DirectoryInfo currentDir = new DirectoryInfo(dirPath);

                // 向上查找"负极"目录
                DirectoryInfo? tempDir = currentDir;
                while (tempDir != null)
                {
                    if (tempDir.Name.Contains("负极"))
                    {
                        // 找到"负极"目录后，向上两级获取电芯号
                        DirectoryInfo? cellDir = tempDir.Parent?.Parent;
                        if (cellDir != null && cellDir.Name.All(char.IsDigit) && cellDir.Name.Length == 8)
                        {
                            return cellDir.Name;
                        }
                        break;
                    }
                    tempDir = tempDir.Parent;
                }

                // 方式3：备选方案 - 向上查找纯数字目录（排除日期格式）
                tempDir = currentDir;
                while (tempDir != null)
                {
                    if (tempDir.Name.All(char.IsDigit) && tempDir.Name.Length == 8)
                    {
                        return tempDir.Name;
                    }
                    tempDir = tempDir.Parent;
                }

                AddLog($"[电芯号提取警告] 无法从文件名或目录路径提取电芯号: {imagePath}");
                return "未知电芯号";
            }
            catch (Exception ex)
            {
                AddLog($"[电芯号提取失败] {ex.Message}，路径：{imagePath}");
                return "电芯号解析失败";
            }
        }


        #region CSV检测结果表格相关
        /// <summary>
        /// 初始化CSV文件
        /// </summary>
        /// <param name="isOnline">是否为在线模式</param>
        private void InitCsvFile(bool isOnline)
        {
            lock (_csvLock)
            {
                try
                {
                    string rootPath = "D:";
                    if (!Directory.Exists(rootPath))
                    {
                        throw new Exception("D盘不存在，无法初始化CSV文件");
                    }

                    string csvRoot = isOnline
                        ? Path.Combine(rootPath, "IME复判", "在线检测结果CSV")
                        : Path.Combine(rootPath, "IME复判", "离线检测结果CSV");
                    Directory.CreateDirectory(csvRoot);

                    string fileName;
                    if (isOnline)
                    {
                        // 在线模式：按天生成
                        string today = DateTime.Now.ToString("yyyy-MM-dd");
                        fileName = $"{today}-在线检测结果.csv";
                        _currentCsvDateOnline = today;
                    }
                    else
                    {
                        // 离线模式：首次启动时记录启动时间
                        if (!_offlineStartTime.HasValue)
                        {
                            _offlineStartTime = DateTime.Now; // 记录离线启动时间
                        }
                        fileName = $"{_offlineStartTime.Value:yyyy-MM-dd-HH-mm-ss}-离线检测结果.csv";
                        _currentOfflineCsvFilePath = Path.Combine(csvRoot, fileName);
                    }

                    // 离线模式使用独立的路径变量
                    _currentCsvFilePath = isOnline ? Path.Combine(csvRoot, fileName) : _currentOfflineCsvFilePath;

                    // 初始化缓存路径
                    if (!string.IsNullOrEmpty(_currentCsvFilePath))
                    {
                        if (isOnline)
                        {
                            _csvCacheTempPath = Path.ChangeExtension(_currentCsvFilePath, ".csv.cache");
                        }
                        else
                        {
                            _csvCacheTempPathOffline = Path.ChangeExtension(_currentCsvFilePath, ".csv.cache");
                        }
                    }

                    // 写入表头
                    if (!File.Exists(_currentCsvFilePath))
                    {
                        var header = new List<string[]>
                        {
                            new[] {"日期", "工位","班次", "电芯码", "正负极", "图片名称", "检测结果" }
                        };
                        WriteCsvLines(header, isOnline);
                        AddLog($"[CSV初始化] 已创建文件：{_currentCsvFilePath}");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"[CSV初始化失败] {ex.Message}");
                    _currentCsvFilePath = isOnline ? null : _currentOfflineCsvFilePath;
                    if (isOnline) _csvCacheTempPath = null;
                    else _csvCacheTempPathOffline = null;
                }
            }
        }

        /// <summary>
        /// 向CSV文件写入一行检测结果
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <param name="detectionResult">检测结果</param>
        /// <param name="isOnline">是否为在线模式</param>
        private void WriteToCsv(string imagePath, string detectionResult, bool isOnline)
        {
            // 在线模式：跨天重新初始化
            if (isOnline)
            {
                string currentDate = DateTime.Now.ToString("yyyy-MM-dd");
                if (string.IsNullOrEmpty(_currentCsvFilePath) || _currentCsvDateOnline != currentDate)
                {
                    InitCsvFile(isOnline);
                }
            }
            // 离线模式：仅在首次写入时初始化
            else
            {
                if (string.IsNullOrEmpty(_currentOfflineCsvFilePath))
                {
                    InitCsvFile(isOnline);
                }
            }

            // 路径无效时返回
            string? targetCsvPath = isOnline ? _currentCsvFilePath : _currentOfflineCsvFilePath;
            if (string.IsNullOrEmpty(targetCsvPath))
            {
                AddLog($"[CSV写入失败] 文件未初始化，路径：{imagePath}");
                return;
            }

            try
            {
                // 提取信息
                string date = GetDateFromPath(imagePath);
                string shift = GetShiftFromPath(imagePath);
                string cellCode = GetCellNameFromPath(imagePath);
                //string polarity = GetStationFromImagePath(imagePath,isOnline);
                string polarity = "负极";
                string imageName = Path.GetFileName(imagePath);

                // 离线模式复用用在线相同的工位获取逻辑
                string station = GetRoiKey(imagePath, GetStationFromImagePath(imagePath, isOnline)); ;

                var dataLine = new[] { date, station, shift, cellCode, polarity, imageName, detectionResult };
                WriteCsvLines(new List<string[]> { dataLine }, isOnline);
            }
            catch (Exception ex)
            {
                AddLog($"[CSV写入失败] {ex.Message}，路径：{imagePath}");
            }
        }


        /// 批量写入CSV行
        /// </summary>
        /// <param name="lines">要写入的行数据</param>
        /// <param name="isOnline">是否为在线模式</param>
        private void WriteCsvLines(List<string[]> lines, bool isOnline)
        {
            lock (_csvLock)
            {
                try
                {
                    // 原有路径检查
                    if (string.IsNullOrEmpty(_currentCsvFilePath))
                    {
                        AddLog("[CSV写入失败] 当前CSV文件路径为空，请先初始化");
                        string? cacheTempPath = isOnline ? _csvCacheTempPath : _csvCacheTempPathOffline;
                        if (!string.IsNullOrEmpty(cacheTempPath))
                        {
                            lines.ForEach(line => _csvCacheQueue.Enqueue(line));
                            SaveCacheToTempFile(isOnline);
                        }
                        else
                        {
                            AddLog("[CSV缓存] 缓存路径未初始化，暂不缓存");
                        }
                        return;
                    }

                    // 原有直接写入逻辑
                    bool writeSuccess = TryWriteLinesDirectly(lines);
                    if (writeSuccess)
                    {
                        // 缓存补写逻辑
                        if (_csvCacheQueue.Count > 0)
                        {
                            string? cacheTempPath = isOnline ? _csvCacheTempPath : _csvCacheTempPathOffline;
                            if (!string.IsNullOrEmpty(cacheTempPath))
                            {
                                Task.Run(() => FlushCacheToCsv(isOnline)); // 传入模式参数
                            }
                        }
                        return;
                    }

                    // 原有缓存逻辑
                    lines.ForEach(line => _csvCacheQueue.Enqueue(line));
                    SaveCacheToTempFile(isOnline);
                    StartCacheMonitor(isOnline);
                }
                catch (Exception ex)
                {
                    AddLog($"[CSV写入异常] {ex.Message}");
                    string? cacheTempPath = isOnline ? _csvCacheTempPath : _csvCacheTempPathOffline;
                    if (!string.IsNullOrEmpty(cacheTempPath))
                    {
                        lines.ForEach(line => _csvCacheQueue.Enqueue(line));
                        SaveCacheToTempFile(isOnline);
                    }
                }
            }
        }

        /// <summary>
        /// 尝试直接写入文件
        /// </summary>
        private bool TryWriteLinesDirectly(List<string[]> lines)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentCsvFilePath))
                {
                    // 可以记录日志、抛出异常或采取其他适当的措施
                    AddLog("错误：CSV 文件路径为空，无法写入数据。");
                    throw new InvalidOperationException("CSV file path is not set.");
                }
                using (var fs = new FileStream(
                    _currentCsvFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read))
                using (var writer = new StreamWriter(fs, Encoding.UTF8))
                {
                    foreach (var line in lines)
                    {
                        string safeLine = string.Join(",", line.Select(field => $"\"{field.Replace("\"", "\"\"")}\""));
                        writer.WriteLine(safeLine);
                    }
                }
                return true;
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process"))
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        /// <summary>
        /// 将缓存持久化到临时文件
        /// </summary>
        private void SaveCacheToTempFile(bool isOnline)
        {
            try
            {
                //根据模式选择缓存路径
                string? cacheTempPath = isOnline ? _csvCacheTempPath : _csvCacheTempPathOffline;

                if (string.IsNullOrEmpty(cacheTempPath))
                {
                    AddLog($"[缓存持久化] 缓存路径未初始化，暂不持久化");
                    return;
                }

                var cacheLines = _csvCacheQueue.Select(line => string.Join(",", line)).ToList();
                File.WriteAllLines(cacheTempPath, cacheLines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                AddLog($"[缓存持久化失败] {ex.Message}");
            }
        }


        /// <summary>
        /// 启动定时任务检测文件是否释放（适配在线/离线模式）
        /// </summary>
        private void StartCacheMonitor(bool isOnline)
        {
            if (_isFlushingCache) return;

            _isFlushingCache = true;
            Task.Run(async () =>
            {
                try
                {
                    // 每5秒检测一次文件状态
                    while (_csvCacheQueue.Count > 0)
                    {
                        bool flushSuccess = FlushCacheToCsv(isOnline);
                        if (flushSuccess)
                        {
                            // 核心修改：清理对应模式的缓存文件
                            string? cacheTempPath = isOnline ? _csvCacheTempPath : _csvCacheTempPathOffline;
                            if (File.Exists(cacheTempPath))
                                File.Delete(cacheTempPath);
                            break;
                        }

                        await Task.Delay(5000);
                    }
                }
                finally
                {
                    _isFlushingCache = false;
                }
            });
        }

        /// <summary>
        /// 将缓存数据写入CSV
        /// </summary>
        private bool FlushCacheToCsv(bool isOnline)
        {
            lock (_csvLock)
            {
                if (_csvCacheQueue.Count == 0) return true;

                var linesToWrite = new List<string[]>();
                try
                {
                    // 原有逻辑保留：取出缓存数据
                    while (_csvCacheQueue.TryDequeue(out var line))
                    {
                        linesToWrite.Add(line);
                    }

                    string? cacheTempPath = isOnline ? _csvCacheTempPath : _csvCacheTempPathOffline;
                    if (string.IsNullOrEmpty(_currentCsvFilePath))
                    {
                        AddLog("错误：CSV 文件路径为空，无法写入数据。");
                        throw new InvalidOperationException("CSV file path is not set.");
                    }

                    using (var fs = new FileStream(
                        _currentCsvFilePath,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.Read))
                    using (var writer = new StreamWriter(fs, Encoding.UTF8))
                    {
                        foreach (var line in linesToWrite)
                        {
                            string safeLine = string.Join(",", line.Select(field => $"\"{field.Replace("\"", "\"\"")}\""));
                            writer.WriteLine(safeLine);
                        }
                    }

                    // 清理对应模式的缓存文件
                    if (!string.IsNullOrEmpty(cacheTempPath) && File.Exists(cacheTempPath))
                    {
                        File.Delete(cacheTempPath);
                        AddLog($"[缓存清理] 已删除临时缓存文件：{cacheTempPath}");
                    }

                    return true;
                }
                catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                {
                    foreach (var line in linesToWrite.AsEnumerable().Reverse())
                    {
                        _csvCacheQueue.Enqueue(line);
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    AddLog($"[缓存补写失败] {ex.Message}");

                    foreach (var line in linesToWrite.AsEnumerable().Reverse())
                    {
                        _csvCacheQueue.Enqueue(line);
                    }
                    return false;
                }
            }
        }

        /// <summary>
        /// 检测完成后统一处理
        /// </summary>
        private void OnDetectionCompleted(int totalProcessed)
        {
            //AddLog($"[检测完成] 共处理 {totalProcessed} 张图片，所有检测任务已结束");

            if (_csvCacheQueue.Count > 0)
            {
                AddLog($"[缓存状态] 发现 {_csvCacheQueue.Count} 行数据暂存于缓存（CSV可能被Excel占用）");
            }
            else
            {
                AddLog($"[缓存状态] 所有数据已写入CSV，无缓存积压");
            }

            // 检查Excel是否仍占用CSV，需要则弹出提示
            CheckCsvFileInUseAndPrompt();
        }

        /// <summary>
        /// 检查CSV是否被占用，若是则弹出提示
        /// </summary>
        private void CheckCsvFileInUseAndPrompt()
        {
            if (string.IsNullOrEmpty(_currentCsvFilePath))
            {
                return; // CSV路径无效，无需检查
            }

            // 尝试以“独占写入”方式打开文件
            bool isFileInUse = IsFileInUse(_currentCsvFilePath);

            if (isFileInUse && _csvCacheQueue.Count > 0)
            {
                ShowExcelOpenPrompt();
            }
            else if (isFileInUse)
            {
                AddLog("[提示] 检测已完成，但CSV文件仍被Excel占用（不影响已写入数据）");
            }
        }

        /// <summary>
        /// 判断文件是否被占用
        /// </summary>
        private bool IsFileInUse(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                // 尝试以“独占写入”方式打开
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process"))
            {
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private void ShowExcelOpenPrompt()
        {
            // WPF UI线程判断
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(new Action(ShowExcelOpenPrompt));
                return;
            }

            // 判断当前模式
            bool isOnline = !string.IsNullOrEmpty(_currentCsvDateOnline);
            // 获取当前模式对应的CSV路径
            string? csvPath = isOnline ? _currentCsvFilePath : _currentOfflineCsvFilePath;
            if (string.IsNullOrEmpty(csvPath))
            {
                AddLog("[提示] CSV路径无效，无需重试");
                return;
            }

            bool isRetry = true;
            while (isRetry)
            {
                // 显示初始提示
                MessageBoxResult result = MessageBox.Show(
                    "检测已完成，但检测结果CSV文件仍被Excel占用，缓存数据未写入。\n\n" +
                    "请关闭Excel后点击“确定”重试，点击“取消”放弃重试。",
                    "提示：CSV文件被占用",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.OK)
                {
                    //用户点击“确定”：先检查文件是否已释放
                    if (IsFileInUse(csvPath))
                    {
                        // 文件仍被占用 → 提示继续关闭，保持循环
                        MessageBox.Show(
                            "CSV文件仍被占用，请确认已完全关闭Excel后重试！",
                            "提示：文件未释放",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                        isRetry = true;
                    }
                    else
                    {
                        // 文件已释放 → 尝试写入缓存
                        bool flushSuccess = FlushCacheToCsv(isOnline);
                        if (flushSuccess)
                        {
                            MessageBox.Show(
                                "缓存数据已成功写入CSV文件！",
                                "操作成功",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            );
                        }
                        else
                        {
                            MessageBox.Show(
                                "文件已释放，但数据写入失败，请稍后手动重试。",
                                "写入失败",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
                        }
                        isRetry = false;
                    }
                }
                else
                {
                    AddLog("[用户操作] 已取消重试写入，缓存数据将在下次启动程序时补写");
                    isRetry = false;
                }
            }
        }
        #endregion

        /// <summary>
        /// 从图像文件路径中解析提取日期文件夹名称
        /// 目录层级约定：日期文件夹 → 班次文件夹 → 电芯号文件夹 → 正负极文件夹 → 相机文件夹 → 图片文件
        /// </summary>
        /// <param name="imagePath">原始图像的完整文件路径</param>
        /// <returns>提取到的日期文件夹名称（含异常兜底值）</returns>
        private string GetDateFromPath(string imagePath)
        {
            string defaultDate = DateTime.Today.ToString("yyyyMMdd");

            if (string.IsNullOrEmpty(imagePath))
            {
                AddLog("[日期提取警告] 传入路径为空");
                return defaultDate;
            }

            try
            {
                string fullPath = Path.GetFullPath(imagePath);
                string[] pathParts = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // 先检查月日分开的格式 (YYYYMM + DD)
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    string currentPart = pathParts[i];
                    string nextPart = pathParts[i + 1];

                    // 检查当前部分是否为 YYYYMM 格式，下一部分是否为 DD 格式
                    if (Regex.IsMatch(currentPart, @"^20\d{2}(0[1-9]|1[0-2])$") &&
                        Regex.IsMatch(nextPart, @"^(0[1-9]|[12]\d|3[01])$"))
                    {
                        string combinedDate = currentPart + nextPart;

                        // 验证组合后的日期是否有效
                        if (DateTime.TryParseExact(combinedDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime validDate))
                        {
                            //AddLog($"[日期提取成功] 从路径提取日期：{validDate:yyyyMMdd}");
                            return validDate.ToString("yyyyMMdd");
                        }
                    }
                }

                Regex dateRegex = new Regex(@"^(20\d{2})[-_]?(0[1-9]|1[0-2])[-_]?(0[1-9]|[12]\d|3[01])$");
                for (int i = pathParts.Length - 1; i >= 0; i--)
                {
                    string part = pathParts[i];

                    // 初步筛选：正则匹配形状
                    if (dateRegex.IsMatch(part))
                    {
                        // 二次校验：确保是真实存在的日期（排除 20260230 这种非法日期）
                        // 尝试解析多种常见格式
                        string[] formats = { "yyyyMMdd", "yyyy-MM-dd", "yyyy_MM_dd" };
                        if (DateTime.TryParseExact(part, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime validDate))
                        {
                            // 找到有效日期，将其统一格式化为 yyyyMMdd 返回
                            return validDate.ToString("yyyyMMdd");
                        }
                    }
                }
                AddLog($"[日期提取提示] 路径中未发现标准日期格式，使用默认日期：{defaultDate}，路径：{imagePath}");
                return defaultDate;
            }
            catch (Exception ex)
            {
                // 捕获所有异常（如路径权限不足、目录不存在等），记录日志并返回兜底值
                AddLog($"[日期提取失败] {ex.Message}，路径：{imagePath}");
                return "日期解析失败";
            }
        }

        /// <summary>
        /// 从路径提取班次
        /// </summary>
        private string GetShiftFromPath(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                {
                    return "未知班次";
                }

                string fileName = Path.GetFileName(imagePath);

                var match = Regex.Match(fileName, @"(20\d{6})([-_])(\d{6})");
                if (match.Success)
                {
                    string dateStr = match.Groups[1].Value;  // 20260518
                    string separator = match.Groups[2].Value; // -
                    string timeStr = match.Groups[3].Value;

                    // 提取小时（前两位）
                    string hourStr = timeStr.Substring(0, 2);

                    if (int.TryParse(hourStr, out int hour))
                    {
                        // 白班：08:00 - 19:59 (即 hour >= 8 且 hour < 20)
                        // 晚班：20:00 - 07:59 (即 hour >= 20 或 hour < 8)
                        string shift = (hour >= 8 && hour < 20) ? "白班" : "晚班";
                        string fullDateTime = $"{dateStr}{separator}{timeStr}";
                        AddLog($"[班次识别成功] 文件：{fileName}，时间：{fullDateTime}，小时：{hour}，班次：{shift}");
                        return shift;
                    }
                }
                AddLog($"[班次识别警告] 文件名中未找到标准时间格式：{fileName}");
                return "未知班次";

            }
            catch (Exception ex)
            {
                AddLog($"[班次提取失败] {ex.Message}，路径：{imagePath}");
                return "班次解析失败";
            }
        }



        private bool _isLoadingConfig = false;

        /// <summary>
        /// 从JSON配置文件加载程序所有配置项（监控路径、筛选规则、统计数据等）
        /// 核心设计：文件存在性校验→JSON反序列化→类型安全解析→数值范围限制→线程安全更新统计→异常容错
        /// </summary>
        private void LoadConfig()
        {
            if (!File.Exists(_configFilePath)) return;

            try
            {
                _isLoadingConfig = true;
                string json = File.ReadAllText(_configFilePath);
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (dict == null) return;


                // 解析字符串类型配置：
                if (dict.TryGetValue("SelectedMonitorFolderPathA", out var pathA))
                    SelectedMonitorFolderPathA = pathA?.ToString() ?? string.Empty;
                if (dict.TryGetValue("SelectedMonitorFolderPathB", out var pathB))
                    SelectedMonitorFolderPathB = pathB?.ToString() ?? string.Empty;

                if (dict.TryGetValue("IsOnlineDetectionEnabledA", out var enableA)
                    && bool.TryParse(enableA?.ToString(), out bool isEnabledA))
                {
                    _isOnlineDetectionEnabledA = isEnabledA;
                }
                if (dict.TryGetValue("IsOnlineDetectionEnabledB", out var enableB)
                    && bool.TryParse(enableB?.ToString(), out bool isEnabledB))
                {
                    _isOnlineDetectionEnabledB = isEnabledB;
                }

                // 工位选择
                if (dict.TryGetValue("IsStationA", out var stationA) && bool.TryParse(stationA?.ToString(), out bool isStationAVal))
                    __isStationA = isStationAVal;   // 直接赋私有字段，避免触发级联逻辑

                if (dict.TryGetValue("IsStationB", out var stationB) && bool.TryParse(stationB?.ToString(), out bool isStationBVal))
                    _isStationB = isStationBVal;

                // 相机勾选状态读取
                if (dict.TryGetValue("IsCameraA1", out var ca1) && bool.TryParse(ca1?.ToString(), out bool ca1v)) _isCameraA1 = ca1v;
                if (dict.TryGetValue("IsCameraA2", out var ca2) && bool.TryParse(ca2?.ToString(), out bool ca2v)) _isCameraA2 = ca2v;
                if (dict.TryGetValue("IsCameraA3", out var ca3) && bool.TryParse(ca3?.ToString(), out bool ca3v)) _isCameraA3 = ca3v;
                if (dict.TryGetValue("IsCameraA4", out var ca4) && bool.TryParse(ca4?.ToString(), out bool ca4v)) _isCameraA4 = ca4v;
                if (dict.TryGetValue("IsCameraB1", out var cb1) && bool.TryParse(cb1?.ToString(), out bool cb1v)) _isCameraB1 = cb1v;
                if (dict.TryGetValue("IsCameraB2", out var cb2) && bool.TryParse(cb2?.ToString(), out bool cb2v)) _isCameraB2 = cb2v;
                if (dict.TryGetValue("IsCameraB3", out var cb3) && bool.TryParse(cb3?.ToString(), out bool cb3v)) _isCameraB3 = cb3v;
                if (dict.TryGetValue("IsCameraB4", out var cb4) && bool.TryParse(cb4?.ToString(), out bool cb4v)) _isCameraB4 = cb4v;

                // 5. 解析加载解析模式
                if (dict.TryGetValue("SelectedMode", out var modeValue))
                {
                    // 将字符串解析为枚举
                    if (Enum.TryParse<DetectionMode>(modeValue?.ToString(), out var parsedMode))
                    {
                        _selectedMode = parsedMode; // 直接赋值给私有字段，避免触发 SaveConfig
                        OnPropertyChanged(nameof(SelectedMode)); // 手动通知 UI 更新
                    }
                    else
                    {
                        AddLog($"[配置加载警告] 检测模式值无效：{modeValue}，使用默认值");
                    }
                }

                // 结果保存配置
                if (dict.TryGetValue("IsSaveResultOK", out var isSaveResultOK) && bool.TryParse(isSaveResultOK?.ToString(), out bool isSaveResultOKVal))
                    _isSaveResultOK = isSaveResultOKVal;

                if (dict.TryGetValue("IsSaveResultNG", out var isSaveResultNG) && bool.TryParse(isSaveResultNG?.ToString(), out bool isSaveResultNGVal))
                    _isSaveResultNG = isSaveResultNGVal;

                if (dict.TryGetValue("IsSaveOriginalOK", out var isSaveOriginalOK) && bool.TryParse(isSaveOriginalOK?.ToString(), out bool isSaveOriginalOKVal))
                    _isSaveOriginalOK = isSaveOriginalOKVal;

                if (dict.TryGetValue("IsSaveOriginalNG", out var isSaveOriginalNG) && bool.TryParse(isSaveOriginalNG?.ToString(), out bool isSaveOriginalNGVal))
                    _isSaveOriginalNG = isSaveOriginalNGVal;

                // 7. 解析整数类型配置：每份报告最大图片数（添加范围限制，防止非法数值）
                // Math.Max(1, Math.Min(50, ...))：限制值在1-50之间，避免0/负数/超大值导致业务异常
                if (dict != null && dict.TryGetValue("MaxImagesPerReport", out var maxImages))
                {
                    if (int.TryParse(maxImages.ToString(), out int maxImagesValue))
                    {
                        MaxImagesPerReport = Math.Max(1, Math.Min(50, maxImagesValue));
                    }
                }
                // 8. 解析整数类型配置：报告超时时间（分钟），范围限制1-1440（1天）
                if (dict != null && dict.TryGetValue("ReportTimeoutMinutes", out var timeout))
                {
                    if (int.TryParse(timeout.ToString(), out int timeoutValue))
                    {
                        ReportTimeoutMinutes = Math.Max(1, Math.Min(1440, timeoutValue));
                    }
                }

                // ✅ 加载完成后统一通知UI更新（此时不会触发监控启动）
                OnPropertyChanged(nameof(SelectedMonitorFolderPathA));
                OnPropertyChanged(nameof(SelectedMonitorFolderPathB));
                OnPropertyChanged(nameof(IsOnlineDetectionEnabledA));
                OnPropertyChanged(nameof(IsOnlineDetectionEnabledB));
                OnPropertyChanged(nameof(IsStationA));
                OnPropertyChanged(nameof(IsStationB));
                OnPropertyChanged(nameof(IsCameraA1)); OnPropertyChanged(nameof(IsCameraA2));
                OnPropertyChanged(nameof(IsCameraA3)); OnPropertyChanged(nameof(IsCameraA4));
                OnPropertyChanged(nameof(IsCameraB1)); OnPropertyChanged(nameof(IsCameraB2));
                OnPropertyChanged(nameof(IsCameraB3)); OnPropertyChanged(nameof(IsCameraB4));
                OnPropertyChanged(nameof(IsSaveResultOK));
                OnPropertyChanged(nameof(IsSaveResultNG));
                OnPropertyChanged(nameof(IsSaveOriginalOK));
                OnPropertyChanged(nameof(IsSaveOriginalNG));
            }
            catch (Exception ex)
            {
                AddLog($"加载配置失败：{ex.Message}");
            }
            finally
            {
                _isLoadingConfig = false;
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfig()
        {
            if (_isLoadingConfig) return;
            try
            {
                // 获取统计数据量
                var currentTotalDefectNGCount = new Dictionary<string, int>();
                if (DefectDataGrid != null)
                {
                    // 使用 ToList() 避免多线程下的集合修改异常
                    var gridItems = DefectDataGrid.ToList();
                    foreach (var item in gridItems)
                    {
                        if (!string.IsNullOrEmpty(item.DefectName) && item.NGCount > 0)
                        {
                            currentTotalDefectNGCount[item.DefectName] = item.NGCount;
                        }
                    }
                }

                // 构建匿名对象，仅包含需要保存的属性
                var configData = new
                {
                    SelectedMonitorFolderPathA,
                    IsOnlineDetectionEnabledA,
                    SelectedMonitorFolderPathB,
                    IsOnlineDetectionEnabledB,

                    IsStationA,
                    IsStationB,
                    IsCameraA1,
                    IsCameraA2,
                    IsCameraA3,
                    IsCameraA4,
                    IsCameraB1,
                    IsCameraB2,
                    IsCameraB3,
                    IsCameraB4,

                    IsSaveResultOK,
                    IsSaveResultNG,
                    IsSaveOriginalOK,
                    IsSaveOriginalNG,

                    MaxImagesPerReport,
                    ReportTimeoutMinutes,
                    SelectedMode = _selectedMode.ToString(),

                };

                string json = JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);

            }
            catch (Exception ex)
            {
                AddLog($"保存配置失败：{ex.Message}");
            }
        }

        private void SaveMaxImagesSettings()
        {
            try
            {
                // 边界检查
                if (MaxImagesPerReport < 1 || MaxImagesPerReport > 50)
                {
                    AddLog($"报表内最大电芯数量必须在1-50之间，当前值：{MaxImagesPerReport}");
                    return;
                }

                // 加载现有配置
                Dictionary<string, object> configDict;
                if (File.Exists(_configFilePath))
                {
                    string existingJson = File.ReadAllText(_configFilePath);
                    configDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson) ?? new Dictionary<string, object>();
                }
                else
                {
                    configDict = new Dictionary<string, object>();
                }

                // 更新最大电芯数量配置
                configDict["MaxImagesPerReport"] = MaxImagesPerReport;

                // 保存到JSON文件
                string json = System.Text.Json.JsonSerializer.Serialize(configDict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);

                AddLog($"报表内最大电芯数量已保存：{MaxImagesPerReport}");
            }
            catch (Exception ex)
            {
                AddLog($"保存最大电芯数量设置失败：{ex.Message}");
            }
        }

        private void SaveTimeoutSettings()
        {
            try
            {
                // 边界检查
                if (ReportTimeoutMinutes < 1 || ReportTimeoutMinutes > 1440)
                {
                    AddLog($"生成报表最大间隔时间必须在1-1440分钟之间，当前值：{ReportTimeoutMinutes}");
                    return;
                }

                // 加载现有配置
                Dictionary<string, object> configDict;
                if (File.Exists(_configFilePath))
                {
                    string existingJson = File.ReadAllText(_configFilePath);
                    configDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson) ?? new Dictionary<string, object>();
                }
                else
                {
                    configDict = new Dictionary<string, object>();
                }

                // 更新超时时间配置
                configDict["ReportTimeoutMinutes"] = ReportTimeoutMinutes;

                // 保存到JSON文件
                string json = System.Text.Json.JsonSerializer.Serialize(configDict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);

                AddLog($"生成报表最大间隔时间已保存：{ReportTimeoutMinutes}分钟");
            }
            catch (Exception ex)
            {
                AddLog($"保存报表超时时间设置失败：{ex.Message}");
            }
        }


        /// <summary>
        /// 离线检测全部完成后，主动生成剩余报表
        /// </summary>
        private async Task GenerateOfflineFinalReport()
        {
            if ( _isOfflineDetectionCompleted)
                return;

            // 停止超时定时器
            _reportTimer?.Stop();

            //若队列中有剩余电芯编码，逐个生成报表直到队列为空
            while (_cellCodeQueue.Count > 0)
            {
                await GenerateReport(DetectionStatisticsSource.Offline);
            }

            //标记检测已完成，防止重复调用
            _isOfflineDetectionCompleted = true;
            AddLog("[离线报表完成] 所有剩余电芯编码已处理完成");
        }

        // 新增私有字段：标记报表功能是否已初始化
        private bool _isReportFunctionInited = false;

        public void InitReportFunction()
        {
            // 仅初始化一次，避免重复创建定时器/订阅事件
            if (_isReportFunctionInited) return;

            // 使用用户设置的超时时间，而不是硬编码值
            _reportTimer = new System.Timers.Timer(ReportTimeoutMinutes * 60 * 1000);
            _reportTimer.Elapsed += OnReportTimeout;
            _reportTimer.AutoReset = false;

            _isOfflineDetectionCompleted = false;

            // 标记初始化完成
            _isReportFunctionInited = true;
            AddLog("[报表功能] 初始化完成");
        }




        /// <summary>
        /// 添加图片到队列
        /// </summary>
        private void AddImageToQueue(string imagePath, bool isNG, string station, DateTime detectionTime, DetectionStatisticsSource sourceType)
        {
            bool isOffline = sourceType == DetectionStatisticsSource.Offline;
            bool isOnline = sourceType == DetectionStatisticsSource.Online;

            lock (_queue1Lock)
            {
                // 1. 拦截已完成的离线检测
                if (isOffline && _isOfflineDetectionCompleted)
                {
                    AddLog($"[离线检测已完成] 拒绝添加新图片：{imagePath}");
                    return;
                }

                var cellCode = GetCellNameFromPath(imagePath);

                // 2. 在线模式下：如果这是自上一次报表清空后的【第一条新NG数据】
                if (isOnline && _cellCodeQueue.Count == 0 && _reportTimer != null)
                {
                    _reportTimer.Stop();
                    _reportTimer.Start();
                    AddLog($"[报表功能] 新周期检测到首个NG，开始 {ReportTimeoutMinutes} 分钟倒计时心跳...");
                }

                // 3. 检查是否是新电芯编码
                bool isNewCellCode = _cellCodeSet.Add(cellCode);

                //所有新电芯编码都添加到队列，无论检测结果
                if (isNewCellCode)
                {
                    _cellCodeQueue.Enqueue(cellCode);

                    // 4. 检查是否达到电芯编码数量阈值
                    if (_cellCodeQueue.Count >= MaxImagesPerReport)
                    {
                        AddLog($"[条件触发] 电芯编码数量达到阈值 {MaxImagesPerReport}，开始生成报表...");

                        // 达标时，先彻底关停定时器，防止在异步任务生成期间定时器误触发超时
                        _reportTimer?.Stop();

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await GenerateReport(sourceType);
                            }
                            catch (Exception ex)
                            {
                                AddLog($"[报表生成异常] {ex.Message}", CoreLogLevel.Error);
                            }
                        });
                    }
                }

                // 5. 如果是NG图片，添加到NG字典中
                if (isNG)
                {
                    string imgKey = GetRoiKey(imagePath, station);
                    var ngInfo = new NgImageInfo
                    {
                        ImagePath = imagePath,
                        Shift = GetShiftFromPath(imagePath),
                        Polarity = "负极",
                        CellCode = cellCode,
                        ImageName = $"{imgKey}_{Path.GetFileName(imagePath)}",
                        Station = station,
                        DetectionTime = detectionTime
                    };

                    if (!_ngImageDict.ContainsKey(cellCode))
                    {
                        _ngImageDict[cellCode] = new List<NgImageInfo>();
                        AddLog($"[首次NG] 电芯编码 {cellCode} 首次检测到NG");
                    }
                    _ngImageDict[cellCode].Add(ngInfo);
                }
            }
        }

        /// <summary>
        /// 超时生成报表
        /// </summary>
        private void OnReportTimeout(object? sender, ElapsedEventArgs e)
        {
            bool shouldGenerate = false;
            lock (_queue1Lock)
            {
                // 检查是否在生成中，防止重复触发（建议增加一个标志位 _isGeneratingReport）
                if (_cellCodeQueue.Count > 0)
                {
                    shouldGenerate = true;
                }
            }
            if (shouldGenerate)
            {
                AddLog($"[超时触发] 达到 {ReportTimeoutMinutes} 分钟超时时间，且队列存在积压数据，开始处理...");

                // 使用 Task.Run 触发，不阻塞定时器线程
                Task.Run(async () =>
                {
                    try
                    {
                        await GenerateReport(DetectionStatisticsSource.Online);
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[超时任务异常] {ex.Message}", CoreLogLevel.Error);
                    }
                });
            }
            else
            {
                // 如果超时了但队列是空的（比如正常生产下由于达标刚清空了队列），定时器也必须重启，否则时钟就停了
                lock (_queue1Lock)
                {
                    if (_reportTimer != null )
                    {
                        _reportTimer.Stop();
                        _reportTimer.Start();
                    }
                }
            }
        }

        private int GetMaxReportIndex(string targetDir)
        {
            int maxIndex = 0;
            try
            {
                if (!Directory.Exists(targetDir)) return maxIndex;

                // 遍历文件夹内所有PDF，解析前缀序号
                foreach (string filePath in Directory.GetFiles(targetDir, "*.pdf"))
                {
                    string fileName = Path.GetFileName(filePath);
                    string[] nameParts = fileName.Split('.', 2); // 按第一个"."分割（序号.文件名）
                    if (nameParts.Length < 2) continue;

                    // 转换为数字并更新最大值
                    if (int.TryParse(nameParts[0], out int currentIndex) && currentIndex > maxIndex)
                    {
                        maxIndex = currentIndex;
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"[获取最大序号失败] {ex.Message}");
            }
            return maxIndex;
        }

        /// <summary>
        /// 生成报表
        /// </summary>
        private async Task GenerateReport(DetectionStatisticsSource sourceType)
        {
            bool isOffline = sourceType == DetectionStatisticsSource.Offline;

            // 获取当前组的电芯编码
            List<string> groupCellCodes = new List<string>();
            List<string> ngCellCodesInGroup = new List<string>();
            List<NgImageInfo> imagesToReport = new List<NgImageInfo>();

            lock (_queue1Lock)
            {
                if (_cellCodeQueue.Count == 0) return;
                // 获取当前组的所有电芯编码
                int count = Math.Min(MaxImagesPerReport, _cellCodeQueue.Count);
                for (int i = 0; i < count; i++)
                {
                    if (!_cellCodeQueue.TryDequeue(out string? cellCode)) break;
                    groupCellCodes.Add(cellCode);
                    _cellCodeSet.Remove(cellCode);

                    // 1.3 提取NG数据并立即清理字典 (Thread-Safe)
                    if (_ngImageDict.ContainsKey(cellCode))
                    {
                        ngCellCodesInGroup.Add(cellCode);
                        var allNgImages = _ngImageDict[cellCode];

                        // 提取前5张用于报表
                        var takeImages = allNgImages.Take(5).ToList();
                        imagesToReport.AddRange(takeImages);

                        // 记录日志：如果图片被截断
                        if (allNgImages.Count > 5)
                        {
                            AddLog($"[报表图片截断] 电芯 {cellCode} 共有 {allNgImages.Count} 张NG图，仅保留前5张");
                        }

                        // 立即移除字典数据，防止内存泄漏，并避免后续需要再次加锁
                        _ngImageDict.Remove(cellCode);
                        AddLog($"[数据清理] 已归档并清理电芯 {cellCode} 的缓存数据");
                    }
                }
            }

            // ===================== 阶段 2：校验与前置判断 (无锁) =====================

            if (groupCellCodes.Count == 0)
            {
                AddLog("[报表生成] 队列提取为空，无需生成");
                return;
            }

            AddLog($"[电芯组处理] 处理 {groupCellCodes.Count} 个电芯，其中 {ngCellCodesInGroup.Count} 个NG，共 {imagesToReport.Count} 张图片");

            // 如果没有NG图片，任务结束（数据已在阶段1从队列和字典中移除，属于正常处理完毕）
            if (imagesToReport.Count == 0)
            {
                AddLog("[报表生成] 本组无NG图片，跳过PDF报表生成");
                return;
            }

            // ===================== 阶段 3：IO 密集型操作 (无锁，异步) =====================
            string savePath = string.Empty;

            try
            {
                // 3.1 构造路径
                string baseReportDir = Path.Combine("D:", "IME复判", "检测报表");
                string modeSubDir = isOffline ? "离线检测报表" : "在线检测报表";

                // 确定日期文件夹：离线模式使用第一张图片的日期，在线模式使用当前日期
                string dateFolderName = DateTime.Now.ToString("yyyy-MM-dd");
                if (isOffline && imagesToReport.Count > 0 && !string.IsNullOrEmpty(imagesToReport[0].ImagePath))
                {
                    // 尝试获取离线任务的日期，如果获取失败则兜底使用当前日期
                    string imgDate = GetDateFromPath(imagesToReport[0].ImagePath!);
                    if (!string.IsNullOrEmpty(imgDate) && imgDate.Length == 8)
                    {
                        dateFolderName = DateTime.ParseExact(imgDate, "yyyyMMdd", null).ToString("yyyy-MM-dd");
                    }
                }

                string finalReportDir = Path.Combine(baseReportDir, modeSubDir, dateFolderName);
                if (!Directory.Exists(finalReportDir))
                {
                    Directory.CreateDirectory(finalReportDir);
                }

                // 3.2 计算文件名
                int maxIndex = GetMaxReportIndex(finalReportDir);
                int startIndex = maxIndex + 1;
                string reportNo = DateTime.Now.ToString("yyyyMMddHHmmss");
                string baseFileName = $"PTO雅康缺陷检测报告_{reportNo}.pdf";
                string fileName = $"{startIndex}.{baseFileName}";
                savePath = Path.Combine(finalReportDir, fileName);

                // 文件名防冲突重试
                int retryCounter = 1;
                while (File.Exists(savePath) && retryCounter < 100)
                {
                    fileName = $"{startIndex + retryCounter}.{baseFileName}";
                    savePath = Path.Combine(finalReportDir, fileName);
                    retryCounter++;
                }

                // 3.3 生成 PDF
                string productionDate = isOffline && imagesToReport.Count > 0
                    ? GetDateFromPath(imagesToReport[0].ImagePath!)
                    : DateTime.Now.ToString("yyyy-MM-dd");

                AddLog($"[报表生成] 开始绘制PDF: {savePath}");

                // QuestPDF 生成逻辑
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(ReportStyle.Content);

                        // 页眉
                        page.Header().Element(header =>
                        {
                            header.Column(col =>
                            {
                                col.Item().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(5).Row(row =>
                                {
                                    row.RelativeItem().AlignCenter().Text("PTO雅康缺陷检测报告").Style(ReportStyle.Title);
                                });
                                col.Item().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).BorderTop(0).Padding(5).AlignRight().Text($"报表编号：{reportNo}");
                            });
                        });

                        // 内容
                        string groupInfo = $"本组共 {groupCellCodes.Count} 个电芯，其中 {ngCellCodesInGroup.Count} 个NG电芯，共 {imagesToReport.Count} 张NG图片";
                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            // 基础表格
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(3);
                                });

                                void AddRow(string label, string value)
                                {
                                    table.Cell().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Background(QuestPDF.Helpers.Colors.Grey.Lighten4).Padding(5).Text(label).SemiBold();
                                    table.Cell().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(5).Text(value);
                                }

                                AddRow("检测设备：", "雅康检测工位");
                                AddRow("生产日期：", productionDate);
                                AddRow("生成时间：", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                AddRow("检测模式：", isOffline ? "离线检测" : "在线检测");
                                AddRow("统计概览：", groupInfo);
                                AddRow("NG电芯码：", string.Join("、", ngCellCodesInGroup));
                            });

                            col.Item().Height(20);
                            col.Item().Text("NG缺陷详情").FontSize(14).Bold();
                            col.Item().Height(5);

                            // 图片详情表格
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1); // 序号
                                    columns.RelativeColumn(5); // 图片
                                    columns.RelativeColumn(4); // 信息
                                });

                                // 表头
                                void AddHeader(string text)
                                {
                                    table.Cell().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Background(QuestPDF.Helpers.Colors.Grey.Lighten4).Padding(5).AlignCenter().Text(text).SemiBold();
                                }
                                AddHeader("序号");
                                AddHeader("缺陷结果图");
                                AddHeader("参数信息");

                                int cellIndex = 1;
                                foreach (var imgInfo in imagesToReport)
                                {
                                    // 序号
                                    table.Cell().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).AlignMiddle().AlignCenter().Text($"{cellIndex}");

                                    // 图片
                                    var imgCell = table.Cell().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(5).AlignMiddle().AlignCenter();
                                    string? resultImagePath = GetResultImagePath(imgInfo.ImagePath!, "NG", true, sourceType);

                                    if (!string.IsNullOrEmpty(resultImagePath) && File.Exists(resultImagePath))
                                    {
                                        try
                                        {
                                            //imgCell.Height(150).Image(resultImagePath).UseOriginalImage().FitArea();
                                            // 压缩图片
                                            byte[]? compressedImage = CompressImage(resultImagePath, quality: 95, maxWidth: 2000);

                                            if (compressedImage != null && compressedImage.Length > 0)
                                            {
                                                // 使用压缩后的字节数组加载图片
                                                imgCell.Height(219).Image(compressedImage).UseOriginalImage().FitArea();

                                                // 可选:记录压缩效果
                                                long originalSize = new FileInfo(resultImagePath).Length;
                                                double compressionRatio = (1 - (double)compressedImage.Length / originalSize) * 100;
                                                AddLog($"[图片压缩] {Path.GetFileName(resultImagePath)} " +
                                                       $"原大小:{originalSize / 1024}KB,压缩后:{compressedImage.Length / 1024}KB " +
                                                       $"(压缩率:{compressionRatio:F1}%)", CoreLogLevel.Info, isToDatabase: true);
                                            }
                                            else
                                            {
                                                // 压缩失败时回退到原图
                                                imgCell.Height(219).Image(resultImagePath).UseOriginalImage().FitArea();
                                                AddLog($"[图片压缩] 压缩失败,使用原图:{resultImagePath}", CoreLogLevel.Warn);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            imgCell.Text("图片加载失败");
                                        }
                                    }
                                    else
                                    {
                                        imgCell.Text("文件缺失");
                                    }

                                    // 信息
                                    table.Cell().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(5).Column(infoCol =>
                                    {
                                        infoCol.Spacing(2);
                                        infoCol.Item().Text($"电芯编码：{imgInfo.CellCode}").Bold();
                                        infoCol.Item().Text($"班次：{imgInfo.Shift}");
                                        infoCol.Item().Text($"极性：{imgInfo.Polarity}");
                                        infoCol.Item().Text($"时间：{imgInfo.DetectionTime:yyyy-MM-dd HH:mm:ss}");
                                        infoCol.Item().Text($"图片文件：{imgInfo.ImageName}");
                                    });

                                    cellIndex++;
                                }
                            });
                        });

                        // 页脚
                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("第 ");
                            x.CurrentPageNumber();
                            x.Span(" 页 / 共 ");
                            x.TotalPages();
                            x.Span(" 页");
                        });
                    });
                }).GeneratePdf(savePath);

                AddLog($"[报表生成完毕] 路径：{savePath}");
            }
            catch (Exception ex)
            {
                AddLog($"[报表生成失败] {ex.Message}", CoreLogLevel.Error);
                // 注意：即使失败，数据也已经在阶段1被清理，不会导致死循环或内存泄漏。
                // 这种"Fail-Fast"策略在工业场景比重试更安全。
                return;
            }

            // ===================== 阶段 4：网络推送 (无锁，异步) =====================

            if (!string.IsNullOrEmpty(savePath) && File.Exists(savePath))
            {
                try
                {
                    AddLog($"[报表推送] 开始推送：{Path.GetFileName(savePath)}");
                    var response = await _webApiClient.PushFileAsync(savePath);

                    if (response.Code != 200)
                    {
                        AddLog($"[推送失败] Code:{response.Code}, Msg:{response.Message}", CoreLogLevel.Warn);
                    }
                    else
                    {
                        AddLog($"[推送成功] 报表已上传");
                    }
                }
                catch (Exception pushEx)
                {
                    AddLog($"[推送异常] {pushEx.Message}", CoreLogLevel.Warn);
                }
            }
        }



        // 新增:图片压缩辅助方法
        /// <summary>
        /// 压缩图片并返回字节数组
        /// </summary>
        /// <param name="imagePath">原图路径</param>
        /// <param name="quality">质量(1-100,推荐60-80)</param>
        /// <param name="maxWidth">最大宽度(像素)</param>
        /// <returns>压缩后的图片字节数组</returns>
        private byte[]? CompressImage(string imagePath, int quality = 70, int maxWidth = 800)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    AddLog($"[图片压缩] 文件不存在:{imagePath}", CoreLogLevel.Warn);
                    return null;
                }

                using (var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var originalImage = System.Drawing.Image.FromStream(fs))
                {
                    // 计算缩放比例
                    int newWidth = originalImage.Width > maxWidth ? maxWidth : originalImage.Width;
                    int newHeight = (int)(originalImage.Height * ((double)newWidth / originalImage.Width));

                    using (var resizedImage = new Bitmap(newWidth, newHeight))
                    using (var graphics = Graphics.FromImage(resizedImage))
                    {
                        // 高质量缩放
                        graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                        graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);

                        // 转换为JPEG并压缩
                        using (var ms = new MemoryStream())
                        {
                            var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                                .FirstOrDefault(e => e.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);

                            if (jpegEncoder != null)
                            {
                                var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                                encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                                    System.Drawing.Imaging.Encoder.Quality, (long)quality);

                                resizedImage.Save(ms, jpegEncoder, encoderParams);
                            }
                            else
                            {
                                resizedImage.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                            }

                            return ms.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"[图片压缩异常] {imagePath}:{ex.Message}", CoreLogLevel.Error);
                return null;
            }
        }

        static class ReportStyle
        {
            public static TextStyle Title => TextStyle.Default.FontSize(20).SemiBold().FontFamily("SimHei"); // 黑体
            public static TextStyle Header => TextStyle.Default.FontSize(10).SemiBold().FontFamily("SimSun"); // 宋体
            public static TextStyle Content => TextStyle.Default.FontSize(10).FontFamily("SimSun");
        }

        #region UI线程优化辅助方法

        /// <summary>
        /// 统一的UI更新方法，减少Dispatcher调用开销
        /// </summary>
        private void UpdateUIState(Action uiAction)
        {
            if (uiAction == null) return;

            var app = Application.Current;
            if (app == null)
            {
                // 应用正在关闭，直接在当前线程执行（或忽略）
                try { uiAction(); } catch { }
                return;
            }

            var dispatcher = app.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                try { uiAction(); } catch { }
                return;
            }

            try
            {
                if (dispatcher.CheckAccess())
                {
                    uiAction();
                }
                else
                {
                    dispatcher.Invoke(uiAction, DispatcherPriority.Background);
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException ||
                                       ex is InvalidOperationException ||
                                       ex.Message.Contains("shutdown"))
            {
                // 忽略关闭过程中的正常异常
            }
        }

        /// <summary>
        /// 异步执行UI更新
        /// </summary>
        private Task UpdateUIStateAsync(Action uiAction)
        {
            if (uiAction == null) return Task.CompletedTask;

            var app = Application.Current;
            if (app == null) return Task.CompletedTask;

            var dispatcher = app.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return Task.CompletedTask;

            try
            {
                return dispatcher.InvokeAsync(uiAction, DispatcherPriority.Background).Task;
            }
            catch (Exception) // 忽略关闭过程中的异常
            {
                return Task.CompletedTask;
            }
        }

        #endregion


        #region 参数管控


        public ObservableCollection<DefectParameter> DefectDataGrid { get; set; } = [];

        // 私有字段：存储选中项的实际值
        private DefectParameter? _selectedItem;
        // 公共属性：对外暴露选中项，供UI绑定
        public DefectParameter? SelectedItem
        {
            // 取值器：返回私有字段的值
            get => _selectedItem;
            // 赋值器：调用SetProperty方法更新值并触发通知
            set => SetProperty(ref _selectedItem, value);
        }

        private void LoadDefectInitialData()
        {
            try
            {
                // 确保配置文件夹存在
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }

                if (File.Exists(DefectFilePath))
                {
                    string json = File.ReadAllText(DefectFilePath, Encoding.UTF8);
                    var list = JsonSerializer.Deserialize<List<DefectParameter>>(json);

                    if (list != null && list.Count > 0)
                    {
                        foreach (var item in list)
                        {
                            // 重要：重新触发属性变更，确保 UI 能正确绑定
                            item.DefectName = item.DefectName;   // 触发 PropertyChanged
                            DefectDataGrid.Add(item);
                        }
                        string defectNames = string.Join("、", list.Select(x => x.DefectName));
                        AddLog($"从 {DefectFilePath} 成功加载 {DefectDataGrid.Count} 条缺陷参数：{defectNames}");

                        CalculateDetectionStatistics(DetectionStatisticsSource.Online);
                        return;
                    }
                }

                // 文件不存在 或 解析失败 → 加载默认数据
                AddLog("未找到缺陷参数配置文件，使用默认数据", CoreLogLevel.Info);

                DefectDataGrid.Add(new DefectParameter
                {
                    DefectName = "缺陷",
                    Credibility = 0.3,
                    Width = 20,
                    Height = 5,
                    Area = 100,
                    IsEnable = true
                });

                DefectDataGrid.Add(new DefectParameter
                {
                    DefectName = "凹坑",
                    Credibility = 0.35,
                    Width = 15,
                    Height = 6,
                    Area = 100,
                    IsEnable = true
                });

                // 可选：第一次运行时自动保存一次默认值
                // OnSaveData("初始默认值");
            }
            catch (JsonException jex)
            {
                AddLog($"缺陷参数 JSON 格式错误：{jex.Message}", CoreLogLevel.Error);
                MessageBox.Show("配置文件格式错误，将使用默认数据。", "加载失败", MessageBoxButton.OK, MessageBoxImage.Warning);

            }
            catch (Exception ex)
            {
                AddLog($"加载缺陷参数失败：{ex.Message}", CoreLogLevel.Error);
                MessageBox.Show($"读取配置文件失败：{ex.Message}\n将使用默认数据。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 新增行（优化线程逻辑，避免UI干涉+空值校验强化）
        /// </summary>
        private void OnAddRow(string? parameter)
        {
            //强化空值校验：包含全空格的情况也直接返回
            if (string.IsNullOrWhiteSpace(parameter)) return;

            try
            {
                //1.后台线程初始化新行数据（纯数据操作，不占用UI线程）
                var newDefect = new DefectParameter
                {
                    DefectName = "缺陷类别",
                    Width = 0,
                    Height = 0,
                    Area = 0,
                    Credibility = 0,
                    IsEnable = false
                };

                RunOnUIThread(() =>
                {
                    DefectDataGrid.Insert(0, newDefect);

                });
            }
            catch (Exception ex)
            {
                //通用异常处理：捕获并提示，避免程序崩溃
                MessageBox.Show($"新增行失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除行（防空引用）
        /// </summary>
        private void OnDeleteRow(DefectParameter? item)
        {
            // 强化前置校验：覆盖所有无效场景
            if (item == null || DefectDataGrid == null || !DefectDataGrid.Contains(item))
            {
                AddLog($"删除行失败：无效的删除项（{item?.DefectName ?? "空项"}）", CoreLogLevel.Warn);
                return;
            }
            try
            {
                RunOnUIThread(() =>
                {
                    DefectDataGrid.Remove(item);

                    // 优化：清空选中项（避免UI绑定到已删除的对象导致异常）
                    if (SelectedItem == item)
                    {
                        SelectedItem = null;
                    }
                });
                AddLog($"删除行成功：{item.DefectName}");
            }
            catch (Exception ex)
            {
                // 通用异常处理：捕获并提示具体原因
                MessageBox.Show($"删除行失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                CurrentLog = $"删除行失败：{item.DefectName}，原因：{ex.Message}";
            }
        }

        /// <summary>
        /// 保存数据（适配项目框架）
        /// </summary>
        private void OnSaveData(string? parameter)
        {
            try
            {
                // 确保文件夹存在
                Directory.CreateDirectory(ConfigFolder);

                // 序列化时可以选择忽略默认值、Null值等（视需求）
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,                    // 美化格式，便于人工查看
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    // NumberHandling = JsonNumberHandling.AllowReadingFromString,  // 如果前端可能传字符串数字
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                };

                string json = JsonSerializer.Serialize(DefectDataGrid.ToList(), options);

                File.WriteAllText(DefectFilePath, json);

                string msg = $"[{parameter ?? "未知"}] 工位缺陷参数保存成功\n共 {DefectDataGrid.Count} 条记录";
                AddLog(msg);
                MessageBox.Show(msg, "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                string err = $"保存缺陷参数失败：{ex.Message}";
                AddLog(err, CoreLogLevel.Error);
                MessageBox.Show(err, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion


        private DetectionStatisticsModel? AddDetectionStatisticsToDatabase(
            string detectResult,
            IEnumerable<string>? triggeredDefects,
            DetectionStatisticsSource sourceType)
        {
            if (!string.Equals(detectResult, "OK", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(detectResult, "NG", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            const int MaxRetry = 3;
            const int BaseDelayMs = 150;

            lock (_statisticsLock)
            {
                for (int attempt = 1; attempt <= MaxRetry; attempt++)
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var statisticsRepo = scope.ServiceProvider.GetRequiredService<IDetectionStatisticsRepository>();

                        return statisticsRepo.Increment(
                            StatisticsModuleCode,
                            DateTime.Today,
                            sourceType,
                            detectResult,
                            triggeredDefects);
                    }
                    catch (Exception ex) when (
                        attempt < MaxRetry &&
                        (ex.Message.Contains("Cannot Open when State is Connecting") ||
                         ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)))
                    {
                        int delay = BaseDelayMs * attempt;
                        AddLog($"[统计写入重试 {attempt}/{MaxRetry}] 连接状态异常，{delay}ms 后重试：{ex.Message}",
                            CoreLogLevel.Warn);
                        Thread.Sleep(delay);
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[统计写入失败-{sourceType}] {ex.Message}", CoreLogLevel.Error);
                        return null;
                    }
                }
            }

            AddLog($"[统计写入失败-{sourceType}] 已重试 {MaxRetry} 次，放弃本次写入", CoreLogLevel.Error);
            return null;
        }

        private void PushMessageToFeishu(string message, int retryCount = 2)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            _ = PushMessageAsync(message, retryCount);
        }

        private async Task PushMessageAsync(string message, int retryCount)
        {
            for (int i = 1; i <= retryCount; i++)
            {
                try
                {
                    var response = await _webApiClient.PushMessageAsync(message);
                    if (response.Code == 200)
                    {
                        _logger.Info($"✅ 飞书消息推送成功");
                        return;
                    }
                    _logger.Warn($"❌ 飞书推送失败：{response.Message}");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"❌ 飞书推送异常：{ex.Message}");
                    if (i < retryCount)
                        await Task.Delay(1000);
                }
            }
        }

        /// <summary>
        /// 资源清理（防止内存泄漏）
        /// </summary>
        public void Dispose()
        {
            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
        }



        // 1. 定义一个包装类，将算法实例和模型句柄绑定在一起
        public class AIWorker
        {
            public YingYanAI AIInstance { get; set; } = null!;
            public IPredictor Model { get; set; } = null!;
        }

        // 2. 为 A 和 B 工位定义线程安全的模型队列（即对象池）
        private ConcurrentQueue<AIWorker> _stationAPool = new ConcurrentQueue<AIWorker>();
        private ConcurrentQueue<AIWorker> _stationBPool = new ConcurrentQueue<AIWorker>();
    }
  
}
