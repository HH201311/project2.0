using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing.Charts;
using HalconDotNet;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using OpenCvSharp;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Project.PTO;
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
using static OpenTK.Graphics.OpenGL.GL;
using static Yolo8Core.BaseClass;
using MessageBox = HandyControl.Controls.MessageBox;

namespace SUNWODA_SEVB.ViewModels.Pages.PTO
{
    /// <summary>
    /// 雅康叠片机损
    /// </summary>
    [Module("VM_YKDamageDetectionPage","雅康叠片机损AI检测系统v1.0")]
    public class VM_YKDamageDetectionPage:ViewModelBase
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
        //// 缓存：路径 → 相机编号
        //private readonly ConcurrentDictionary<string, string> _cameraNumberCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        //// 缓存：图片路径 → 相机信息
        //private readonly ConcurrentDictionary<string, string> _cameraInfoCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
        public class NgImageInfo
        {
            public string? ImagePath { get; set; }
            public string? Shift { get; set; } // 班次
            public string? Polarity { get; set; } // 正负极
            public string? Camera { get; set; } // 相机编号
            public string? CellCode { get; set; } // 电芯编码
            public string? ImageName { get; set; } // 图片名
            public string? Station { get; set; }
            // 新增：检测时间
            public DateTime DetectionTime { get; set; }
        }

        // 修改队列结构：按电芯编码分组存储
        private readonly Dictionary<string, List<NgImageInfo>> _ngImageDict = new Dictionary<string, List<NgImageInfo>>();
        private readonly Queue<string> _cellCodeQueue = new Queue<string>(); // 记录所有电芯编码的顺序
        private readonly HashSet<string> _cellCodeSet = new HashSet<string>(); // 用于快速查找和去重
        private readonly object _queue1Lock = new object(); // 队列锁
        private System.Timers.Timer? _reportTimer; // 超时生成定时器
        //private const int MaxImagesPerReport = 10; // 每报表最多9张图
        private const double TimeoutSeconds = 60; // 超时时间（秒）
        private bool _isOfflineMode; // 标记当前是在线还是离线模式
        private bool _isOfflineDetectionCompleted = false; //标记离线检测是否已完成
        //public event Action<string, string, DateTime>? OnNgImageDetected;

        // 缺陷配置文件路径
        private static readonly string ConfigFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string DefectFilePath = Path.Combine(ConfigFolder, "DefectParameters.json");



        #region 双工位
        // A工位监控器
        //Y:\202604\30\260479941\正极\正极1234\OK\原图
        //X:\202604\30\260479942\负极\负极1234\OK\原图
        private FileSystemWatcher? _rootWatcherA;

        private readonly ConcurrentDictionary<string, DateTime> _processedFilesA = new();
        private readonly object _watcherLock = new();
        private readonly ConcurrentQueue<string> _safeDetectQueueA = new ConcurrentQueue<string>();
        private bool _isQueueRunningA = false;
        private readonly SemaphoreSlim _semaphoreA; // A工位并发控制
        private const int MaxConcurrentA = 3; // A工位最大并发数
        private readonly AutoResetEvent _queueEventA = new AutoResetEvent(false); // 初始为未触发状态.
        private readonly object _queueLockA = new object();
        private bool _isThreadTerminatingA = false;

        // B工位监控器
        private FileSystemWatcher? _rootWatcherB;
        private readonly ConcurrentDictionary<string, DateTime> _processedFilesB = new();
        private readonly ConcurrentQueue<string> _safeDetectQueueB = new ConcurrentQueue<string>();
        private bool _isQueueRunningB = false;
        private readonly SemaphoreSlim _semaphoreB = new SemaphoreSlim(3, 3); // B工位最大并发数
        private readonly AutoResetEvent _queueEventB = new AutoResetEvent(false);
        private readonly object _queueLockB = new object();
        private bool _isThreadTerminatingB = false;
        private bool _isBMonitorRunning = false;

        //图片文件保存缓存字段
        private string? _cachedRootDirectory = null;
        private DateTime _rootDirLastChecked = DateTime.MinValue;
        private const int ROOT_DIR_CACHE_MINUTES = 5; // 每5分钟重新检测一次
        private const long MIN_FREE_SPACE_BYTES = 1L * 1024 * 1024 * 1024;
        private bool _hasNotifiedStorageDegraded = false; //避免重复弹窗的标志位
        private readonly object _storageDegradedLock = new object();
        private string? _lastUsedRootDirectory = null; // 记录上次使用的根目录，用于检测切换（仅切换时才打日志）
        private readonly HashSet<string> _warnedDrives = new HashSet<string>(StringComparer.OrdinalIgnoreCase); //已推送过警告的盘符集合


        // A工位监听文件路径目录
        private string? _selectedMonitorFolderPathA;
        public string? SelectedMonitorFolderPathA
        {
            get => _selectedMonitorFolderPathA;
            set
            {
                _selectedMonitorFolderPathA = value;
                OnPropertyChanged();
                var onlineKeys = _polarityCache.Keys .Where(k => k.StartsWith("online|")).ToList();
                foreach (var key in onlineKeys)
                    _polarityCache.TryRemove(key, out _);
                SaveConfig();
            }
        }
        // 全局锁对象
        //private bool _isInitializing = false;
        private readonly object _stationCountLock = new object();
        private int _runningStationsCount = 0;

        //A工位在线检测开关
        private bool _isOnlineDetectionEnabledA;
        public bool IsOnlineDetectionEnabledA
        {
            get => _isOnlineDetectionEnabledA;
            set
            {

                if (_isOnlineDetectionEnabledA == value) return;

                //关闭逻辑
                if (!value)
                {
                    StopOnlineMonitoringA();
                    _isOnlineDetectionEnabledA = false;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanOperate));
                    IsOnlineToggleEnabledA = true;
                    UpdateButtonStates();
                    SaveConfig();
                    AddLog("[在线检测-A] ✅ 已关闭");
                    return;
                }

                // 防重复启动
                if (_isAMonitorRunning)
                {
                    MessageBox.Show("A工位监控已在运行！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathA) || !Directory.Exists(SelectedMonitorFolderPathA))
                {
                    if (!_isLoadingConfig)  // 只在非加载阶段弹窗
                    {
                        MessageBox.Show("请先选择有效的A工位监控文件夹！", "路径无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        AddLog("[启动] A工位监控路径无效，跳过自动启动", CoreLogLevel.Warn);
                    }
                    _isOnlineDetectionEnabledA = false;
                    OnPropertyChanged();
                    return;
                }

                // 更新UI状态
                _isOnlineDetectionEnabledA = true;
                IsOnlineToggleEnabledA = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanOperate));
                IsOfflineProgressVisible = Visibility.Collapsed;
                _isOfflineMode = false;
                AddLog("[在线检测-A] ⏳ 正在启动...");

                //异步启动
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await StartOnlineMonitoringAsyncA();

                        // 启动成功后更新状态
                        await UpdateUIStateAsync(() =>
                        {
                            lock (_stationCountLock)
                            {
                                _runningStationsCount++;
                            }
                            IsOnlineToggleEnabledA = true;
                            OnPropertyChanged(nameof(CanOperate));
                            UpdateButtonStates();
                            SaveConfig();
                            AddLog("[在线检测-A] ✅ 启动成功");
                        });
                    }
                    catch (Exception ex)
                    {
                        // 启动失败回滚状态
                        await UpdateUIStateAsync(() =>
                        {
                            _isOnlineDetectionEnabledA = false;
                            IsOnlineToggleEnabledA = true;
                            OnPropertyChanged(nameof(IsOnlineDetectionEnabledA));
                            OnPropertyChanged(nameof(CanOperate));
                            UpdateButtonStates();

                            AddLog($"[在线检测-A] ❌ 启动失败: {ex.Message}", CoreLogLevel.Error);
                            MessageBox.Show(
                                $"A工位启动监控失败：\n{ex.Message}",
                                "启动异常",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        });
                    }
                });
            }
        }

        /// <summary>
        /// 异步启动A工位在线监控
        /// </summary>
        private async Task StartOnlineMonitoringAsyncA()
        {
            await Task.Run(() =>
            {
                bool alreadyRunning = false;
                lock (_stationCountLock)
                {
                    alreadyRunning = _isAMonitorRunning;
                    if (!alreadyRunning)
                    {
                        CurrentMonitoringStation = "A";
                        _isThreadTerminatingA = false;
                        StartRootWatcherA();        // 不涉及UI，锁内安全
                        _isAMonitorRunning = true;
                    }
                }

                if (alreadyRunning)
                {
                    UpdateUIState(() => AddLog("[在线检测-A] 监控已在运行，忽略重复启动"));
                    return;
                }

                // ✅ UI更新移到锁外，避免死锁
                UpdateUIState(() => AddLog($"[在线检测-A] 监控已启动  路径: {SelectedMonitorFolderPathA}"));
                StartConcurrentProcessingA();   // 启动队列处理，也在锁外
            });
        }



        // B工位监听文件路径目录
        private string? _selectedMonitorFolderPathB;
        public string? SelectedMonitorFolderPathB
        {
            get => _selectedMonitorFolderPathB;
            set
            {
                _selectedMonitorFolderPathB = value;
                OnPropertyChanged();
                var onlineKeys = _polarityCache.Keys.Where(k => k.StartsWith("online|")).ToList();
                foreach (var key in onlineKeys)
                    _polarityCache.TryRemove(key, out _);
                SaveConfig();
            }
        }
        // B工位在线检测开关属性
        private bool _isOnlineToggleEnabledB = true;
        public bool IsOnlineToggleEnabledB
        {
            get => _isOnlineToggleEnabledB;
            set { _isOnlineToggleEnabledB = value; OnPropertyChanged(); }
        }
       
        private bool _isOnlineDetectionEnabledB;
        public bool IsOnlineDetectionEnabledB
        {
            get => _isOnlineDetectionEnabledB;
            set
            {
                if (_isOnlineDetectionEnabledB == value) return;

                if (!value)
                {
                    StopOnlineMonitoringB();
                    _isOnlineDetectionEnabledB = false;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanOperate));
                    IsOnlineToggleEnabledB = true; // 解除禁用状态
                    UpdateButtonStates();
                    SaveConfig();
                    AddLog("[在线检测-B] ✅ 已关闭");
                    return;
                }

                // 防重复启动
                if (_isBMonitorRunning)
                {
                    MessageBox.Show("B工位监控已在运行！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathB) || !Directory.Exists(SelectedMonitorFolderPathB))
                {              
                    if (!_isLoadingConfig)  // 只在非加载阶段弹窗
                    {
                        MessageBox.Show("请先选择有效的B工位监控文件夹！", "路径无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        AddLog("[启动] B工位监控路径无效，跳过自动启动", CoreLogLevel.Warn);
                    }
                    _isOnlineDetectionEnabledB = false;
                    OnPropertyChanged();
                    return;
                }

                // 更新UI状态
                _isOnlineDetectionEnabledB = true;
                IsOnlineToggleEnabledB = false; // 启动中禁用开关
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanOperate));
                IsOfflineProgressVisible = Visibility.Collapsed;
                _isOfflineMode = false;
                AddLog("[在线检测-B] ⏳ 正在启动...");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await StartOnlineMonitoringAsyncB();
                        await UpdateUIStateAsync(() =>
                        {
                            lock (_stationCountLock)
                            {
                                _runningStationsCount++;
                            }
                            IsOnlineToggleEnabledB = true;
                            OnPropertyChanged(nameof(CanOperate));
                            UpdateButtonStates();
                            SaveConfig();
                            AddLog("[在线检测-B] ✅ 启动成功");
                        });
                    }
                    catch (Exception ex)
                    {
                        await UpdateUIStateAsync(() =>
                        {
                            _isOnlineDetectionEnabledB = false;
                            IsOnlineToggleEnabledB = true;
                            OnPropertyChanged(nameof(IsOnlineDetectionEnabledB));
                            OnPropertyChanged(nameof(CanOperate));
                            UpdateButtonStates();

                            AddLog($"[在线检测-B] ❌ 启动失败: {ex.Message}", CoreLogLevel.Error);
                            MessageBox.Show(
                                $"B工位启动监控失败：\n{ex.Message}",
                                "启动异常",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        });
                    }
                });
            }
        }




        #endregion

        #region 在线检测相关变量
        
        // 在线检测任务队列
        private readonly Queue<string> _onlineDetectQueue = new Queue<string>();
        // 在线检测队列处理锁
        private readonly object _queueLock = new object();

        #endregion

        private Dictionary<string, string> _cameraImagePaths = new Dictionary<string, string>
        {
            { "1", "" }, { "2", "" }, { "3", "" }, { "4", "" }
        };
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

        //当前在线监控的工位（A/B，默认空）
        private string? _currentMonitoringStation;
        public string? CurrentMonitoringStation
        {
            get => _currentMonitoringStation;
            set { _currentMonitoringStation = value; OnPropertyChanged(); }
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
        public bool HasRoiA1 { get => _hasRoiA1; set { _hasRoiA1 = value; OnPropertyChanged(); }}
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
        private readonly HashSet<string> _warnedNoDelRegionCameras = new HashSet<string>();
        // 记录已经提示过“使用空区域”的相机
        private readonly HashSet<string> _warnedEmptyRegionCameras = new HashSet<string>();
        // 记录已经提示过“兜底失败”的相机
        private readonly HashSet<string> _warnedGenerateFailCameras = new HashSet<string>();
        private string? _currentRoiCameraNo;  // 当前操作的相机编号
        private InkCanvas? _inkCanvas;        // 绘制控件引用

        private Dictionary<string, List<System.Windows.Point>> _roiCache = new Dictionary<string, List<System.Windows.Point>>();

        // 存储：文件夹路径 → 工位（A/B）
        private Dictionary<string, string> _folderToStation = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);




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

        // 在线检测开关A是否可用
        private bool _isOnlineToggleEnabledA = true;
        public bool IsOnlineToggleEnabledA
        {
            get => _isOnlineToggleEnabledA;
            set
            {
                _isOnlineToggleEnabledA = value;
                OnPropertyChanged();
            }
        }

        #region 离线处理
        //文件选择命令
        private bool _isFileSelectEnabled = true;
        public bool IsFileSelectEnabled
        {
            get => _isFileSelectEnabled;
            private set  // ✅ 改为 private,仅内部更新
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
        /// 统一的按钮状态更新方法
        /// </summary>
        private void UpdateButtonStates()
        {
            // 文件/文件夹按钮: 仅当"未检测中"且"在线检测未开启"时可用
            bool canOperateFiles = !IsFileDetecting && !IsOnlineDetectionEnabledA && !IsOnlineDetectionEnabledB;
            IsFileSelectEnabled = canOperateFiles;
            IsFolderSelectEnabled = canOperateFiles;

            // 在线检测开关: 仅当"未进行文件检测"时可用
            IsOnlineToggleEnabledA = !IsFileDetecting;
            IsOnlineToggleEnabledB = !IsFileDetecting;

            // 强制刷新 CanOperate（防止属性变更未传播）
            OnPropertyChanged(nameof(CanOperate));
            OnPropertyChanged(nameof(CanOperateA));
            OnPropertyChanged(nameof(CanOperateB));

            AddLog($"[状态检查] IsFileDetecting={IsFileDetecting}, " +
             $"IsOnlineDetectionEnabledA={IsOnlineDetectionEnabledA}, " +
             $"文件按钮={canOperateFiles}, 在线开关={IsOnlineToggleEnabledA}",
             CoreLogLevel.Debug);

            // 异常状态预警
            if (IsFileDetecting && IsOnlineDetectionEnabledA)
            {
                AddLog("[状态冲突] 检测到文件检测与在线检测同时开启!", CoreLogLevel.Error);
                // 可选: 弹出提示框
                MessageBox.Show("检测到系统状态异常:文件检测与在线检测不能同时进行\n请联系管理员", "状态冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            string stateDesc = IsFileDetecting ? "离线检测中" :
              (IsOnlineDetectionEnabledA || IsOnlineDetectionEnabledB) ? "在线检测中" : "空闲";
            AddLog($"[系统状态] {stateDesc}", CoreLogLevel.Info, isToDatabase: true);
        }

        #region 在线检测相关属性


        /// <summary>
        /// 选中的监控文件夹路径
        /// </summary>
        //public string SelectedMonitorFolderPath
        //{
        //    get => _selectedMonitorFolderPath;
        //    set
        //    {
        //        _selectedMonitorFolderPath = value;
        //        OnPropertyChanged();
        //        if (_isOnlineDetectionEnabled && _onlineMonitorWatcher != null)
        //        {
        //            StopOnlineMonitoring();
        //            StartOnlineMonitoring();
        //        }
        //    }
        //}
        #endregion

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
                CalculateDetectionStatistics();
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
                CalculateDetectionStatistics();
            }
        }

        private bool _isCameraA1;
        /// <summary>相机1是否勾选</summary>
        public bool IsCameraA1
        {
            get => _isCameraA1;
            set { _isCameraA1 = value; OnPropertyChanged(); SaveConfig();
                if (!value && !IsCameraA2 && !IsCameraA3 && !IsCameraA4 && IsStationA)
                    IsStationA = false;
            }
        }

        private bool _isCameraA2;
        /// <summary>相机2是否勾选</summary>
        public bool IsCameraA2
        {
            get => _isCameraA2;
            set { _isCameraA2 = value; OnPropertyChanged(); SaveConfig(); 
                if (!value && !IsCameraA2 && !IsCameraA3 && !IsCameraA4 && IsStationA)
                    IsStationA = false;
            }
        }

        private bool _isCameraA3;
        /// <summary>相机3是否勾选</summary>
        public bool IsCameraA3
        {
            get => _isCameraA3;
            set { _isCameraA3 = value; OnPropertyChanged(); SaveConfig();
                if (!value && !IsCameraA2 && !IsCameraA3 && !IsCameraA4 && IsStationA)
                    IsStationA = false;
            }
        }

        private bool _isCameraA4;
        /// <summary>相机4是否勾选</summary>
        public bool IsCameraA4
        {
            get => _isCameraA4;
            set { _isCameraA4 = value; OnPropertyChanged(); SaveConfig();
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
        private int _historyTotalCount = 0;    // 历史总检测数
        private int _historyOKCount = 0;       // 历史OK数
        private int _historyNGCount = 0;       // 历史NG数
        private Dictionary<string, int> _historyDefectNGCount = new Dictionary<string, int>();  // 历史各缺陷NG数
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

        //Assembly ass;
        //Type tp;
        object? obj2;

        YingYanAI ai = new YingYanAI();
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

        // 并发控制：根据CPU核心数设置
        private const int MaxConcurrent = 4;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(MaxConcurrent);
        // 线程安全队列
        private readonly ConcurrentQueue<string> _safeDetectQueue = new ConcurrentQueue<string>();
        // 原子标记：避免多线程下状态错误
        private volatile bool _isQueueRunning = false;
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
                SaveConfig();
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

        #region 单张图片选择
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
            // 定义支持的图片格式白名单（HashSet保证O(1)高效查找）
            var allowedExtensions = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp" };

            try
            {
                // 核心修改：移除 using 语句，直接创建 WPF 原生 OpenFolderDialog
                // 1. 创建WPF原生文件夹选择对话框（替代第三方组件，适配WPF生态）
                var folderDialog = new OpenFolderDialog();

                // 映射原对话框属性（WPF 原生属性适配）
                // 2. 配置对话框交互属性（提升用户体验）
                folderDialog.Title = "选择包含图片的文件夹（支持子目录）";// 明确用途，提示支持子目录
                // 默认打开“我的图片”文件夹
                folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                folderDialog.ValidateNames = true;// 校验文件夹名称合法性（避免非法字符）
                folderDialog.Multiselect = false;// 禁止多选，仅支持单文件夹批量检测

                // 3. 显示对话框（绑定主窗口，保证模态层级正确）
                bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);

                // 4. 用户取消选择：重置状态并退出
                if (result != true)
                {
                    ResetFolderSelectionStatus();
                    return;
                }

                // 构造保存设置描述文本
                // 5. 拼接当前保存设置描述文本（可视化展示，提升操作透明度）
                StringBuilder saveSettings = new StringBuilder();
                if (IsSaveOriginalOK) saveSettings.Append("保存OK原图；");
                if (IsSaveOriginalNG) saveSettings.Append("保存NG原图；");
                if (IsSaveResultOK) saveSettings.Append("保存OK结果图；");
                if (IsSaveResultNG) saveSettings.Append("保存NG结果图；");

                string saveInfo = saveSettings.Length > 0
                    ? $"保存设置：{saveSettings.ToString().TrimEnd('；')}"
                    : "保存设置：不保存任何图片";
                string selectedFolder = folderDialog.FolderName;

                // 6. 第一层校验：文件夹路径有效性（非空+实际存在）
                if (string.IsNullOrWhiteSpace(selectedFolder) || !Directory.Exists(selectedFolder))
                {
                    MessageBox.Show("选择的文件夹无效或不存在，请重新选择！", "无效文件夹", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ResetFolderSelectionStatus();
                    return;
                }

                // 7. 获取文件夹中所有支持格式的图片（包含子目录）
                List<string> allImageFiles = GetImageFilesInFolder(selectedFolder, allowedExtensions);
                UpdateFolderSelectionStatus(selectedFolder, allImageFiles.Count);

                // 8. 第二层校验：文件夹内是否有图片文件
                if (allImageFiles.Count == 0)
                {
                    var retryResponse = MessageBox.Show(
                        $"所选文件夹「{Path.GetFileName(selectedFolder)}」中未发现任何图片文件！\n" +
                        $"支持的格式：{string.Join("、", allowedExtensions)}\n" +
                        "是否重新选择文件夹？",
                        "无图片文件", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (retryResponse == MessageBoxResult.Yes)
                    {
                        SelectImageFolder();
                    }
                    return;
                }

                // 9. 第三层校验：按筛选条件过滤图片（工位/相机规则）
                List<string> filteredFiles = FilterImageFiles(allImageFiles);
                if (filteredFiles.Count == 0)
                {
                    MessageBox.Show(
                        $"所选文件夹中未找到符合当前筛选条件（工位：{(IsStationA ? "A工位 " : "")}{(IsStationB ? "B工位 " : "")}；的图片！\n" +
                        "请调整勾选条件后重新选择文件夹。",
                        "无符合条件的图片", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 10. 最终确认：展示检测数量、筛选条件、保存设置，让用户确认
                //var confirm = MessageBox.Show(
                //    $"将对文件夹「{Path.GetFileName(selectedFolder)}」中符合筛选条件的 {filteredFiles.Count} 张图片进行批量检测，是否继续？\n" +
                //    $"筛选条件：工位：{(IsPositive ? "正极 " : "")}{(IsNegative ? "负极 " : "")}；相机：{(IsCamera1 ? "1 " : "")}{(IsCamera2 ? "2 " : "")}{(IsCamera3 ? "3 " : "")}{(IsCamera4 ? "4 " : "")} \n" +
                //    saveInfo,
                //    "确认批量检测", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                var confirm = MessageBox.Show(
                   $"将对文件夹「{Path.GetFileName(selectedFolder)}」中符合筛选条件的 {filteredFiles.Count} 张图片进行批量检测，是否继续？\n" +
                   $"筛选条件：工位：{(IsStationA ? "A工位 " : "")}{(IsStationB ? "B工位 " : "")}\n" +
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

                //bool isCameraMatch = (!IsCamera1 && !IsCamera2 && !IsCamera3 && !IsCamera4) ||
                //                     (IsCamera1 && camera == "1") ||
                //                     (IsCamera2 && camera == "2") ||
                //                     (IsCamera3 && camera == "3") ||
                //                     (IsCamera4 && camera == "4");

                if (isPolarityMatch)//&& isCameraMatch
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

        public VM_YKDamageDetectionPage(ILoggerService<VM_YKDamageDetectionPage> logger, IWebApiClient webApiClient, IServiceProvider serviceProvider, IPLCService PLCService)
        {
            _logger = logger;
            _webApiClient = webApiClient ?? throw new ArgumentNullException(nameof(webApiClient));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _PLCService = PLCService ?? throw new ArgumentNullException(nameof(PLCService));

            //同步初始化
            Settings.License = LicenseType.Community;
            //步骤1: 先初始化所有同步资源（信号量等）
            _semaphoreA = new SemaphoreSlim(MaxConcurrentA, MaxConcurrentA);
            //步骤2: 初始化命令和基础数据
            InitializeCommands();
            InitReportFunction();           
            LoadUserIds();
            InitParameters();
            //步骤3: 加载配置
            LoadConfig();

            // 步骤4: 异步加载耗时资源
            _ = Task.Run(async () =>
            {
                try
                {
                    // 1. 异步加载模型
                    await InitializeModelsAsync();

                    // 2. 异步加载ROI配置
                    await LoadRoiConfigsAsync();

                    // 3. 在UI线程完成最后初始化
                    await UpdateUIStateAsync(() =>
                    {
                        LoadDefectInitialData();
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
            _semaphoreA = new SemaphoreSlim(MaxConcurrentA, MaxConcurrentA);
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

            SelectMonitorFolderCommandA = new RelayCommand(SelectMonitorFolderA);//A工位监听路径
            SelectMonitorFolderCommandB = new RelayCommand(SelectMonitorFolderB);//B工位监听路径
            SelectNegativeRoiImageCommand = new RelayCommand<string>(SelectNegativeRoiImage);//选择绘制图片
            DeleteNegativeRoiCommand = new RelayCommand(DeleteNegativeRoi);//弹窗-删除当前绘制内容
            SaveNegativeRoiCommand = new RelayCommand(SaveNegativeRoiAsHalconRegion);//弹窗-保存ROI
            ViewNegativeRoiCommand = new RelayCommand<string>(ViewNegativeRoi);//查看绘制结果
            CancelDrawRoiCommand = new RelayCommand(CancelDrawRoi);//弹窗-取消ROI绘制

            InvalidateStorageCacheCommand = new RelayCommand(InvalidateRootDirectoryCache);

            // 正极ROI命令初始化
            SelectPositiveRoiImageCommand = new RelayCommand<string>(SelectPositiveRoiImage);
            DeletePositiveRoiCommand = new RelayCommand(DeletePositiveRoi);
            SavePositiveRoiCommand = new RelayCommand(SavePositiveRoiAsHalconRegion);  // 复用保存逻辑
            ViewPositiveRoiCommand = new RelayCommand<string>(ViewPositiveRoi);

            SaveUserIdCommand = new RelayCommand(SaveUserId);
            DeleteUserIdCommand = new RelayCommand(DeleteUserId);

            AddRow = new RelayCommand<string?>(OnAddRow);
            DeleteRow = new RelayCommand<DefectParameter?>(OnDeleteRow);
            SaveData = new RelayCommand<string?>(OnSaveData);
        }


        /// <summary>
        /// 异步加载onnx模型（正极/负极）
        /// </summary>
        private async Task InitializeModelsAsync()
        {
            await Task.Run(() =>
            {
                string[] labels = new string[0];
                StringBuilder errorMessages = new StringBuilder();

                try
                {
                    // 加载正极模型
                    string positiveModelPath = @"C:\Windows\bestA.onnx";
                    if (File.Exists(positiveModelPath))
                    {
                        ai.ModelInit(positiveModelPath, labels, "PTO雅康叠片",
                            out StationAmodle, out string error1);

                        if (!string.IsNullOrEmpty(error1))
                            errorMessages.AppendLine($"A工位模型: {error1}");
                        else
                            UpdateUIState(() => AddLog("✓ A工位模型加载成功"));
                    }
                    else
                    {
                        errorMessages.AppendLine($"A工位模型文件不存在: {positiveModelPath}");
                        Growl.Warning("A工位模型文件不存在");
                    }

                    // 加载负极模型
                    string negativeModelPath = @"C:\Windows\bestB.onnx";
                    if (File.Exists(negativeModelPath))
                    {
                        ai.ModelInit(negativeModelPath, labels, "PTO雅康叠片",
                            out StationBmodle, out string error2);

                        if (!string.IsNullOrEmpty(error2))
                            errorMessages.AppendLine($"B工位模型: {error2}");
                        else
                            UpdateUIState(() => AddLog("✓ B工位模型加载成功"));
                    }
                    else
                    {
                        errorMessages.AppendLine($"B工位模型文件不存在: {negativeModelPath}");
                        Growl.Warning("B工位模型文件不存在");
                    }

                    // 统一处理错误
                    if (errorMessages.Length > 0)
                    {
                        UpdateUIState(() =>
                            AddLog($"⚠ 模型初始化警告:{errorMessages}", CoreLogLevel.Warn));
                    }
                }
                catch (Exception ex)
                {
                    UpdateUIState(() =>
                        AddLog($"❌ 模型加载异常: {ex.Message}", CoreLogLevel.Error));
                }
            });
        }

        /// <summary>
        /// 异步加载所有ROI配置
        /// </summary>
        private async Task LoadRoiConfigsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    string roiDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig");

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
                        try
                        {
                            string fileName = Path.GetFileNameWithoutExtension(file);
                            var parts = fileName.Split(new[] { "_ROI" }, StringSplitOptions.RemoveEmptyEntries);

                            if (parts.Length != 1)
                            {
                                failCount++;
                                continue;
                            }

                            string station = parts[0];

                            UpdateUIState(() =>
                            {
                                UpdateRoiStatus(station, true);
                                AddLog($"已加载 {station} 工位ROI");
                            });

                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            UpdateUIState(() =>
                                AddLog($"ROI加载失败 ({Path.GetFileName(file)}): {ex.Message}"));
                        }
                    }

                    UpdateUIState(() =>
                        AddLog($"ROI配置加载完成: 成功 {successCount} 个, 失败 {failCount} 个"));
                }
                catch (Exception ex)
                {
                    UpdateUIState(() =>
                        AddLog($"ROI配置目录扫描失败: {ex.Message}", CoreLogLevel.Error));
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
        private void LoadUserIds()
        {
            try
            {
                // 读取配置文件
                using var scope = _serviceProvider.CreateScope();
                var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                var settingValue = projectSettingRepo.GetSettingValue("PushUserId", "VM_YKDamageDetection") as string;
                var savedList = settingValue?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(id => id.Trim())
                      .ToList() ?? new List<string>();

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

                projectSettingRepo.UpdateSettingValue("PushUserId", "VM_YKDamageDetection", string.Join(",", users));
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
        /// 选择绘制图片
        /// 选择指定工位+相机的负极ROI参考图片，并初始化绘制画布（核心流程：参数校验→文件选择→图片加载→历史ROI恢复→打开绘制弹窗）
        /// 设计要点：严格的参数校验、UI线程安全操作、WPF图片加载规范、历史ROI复用、异常容错
        /// </summary>
        /// <param name="param">参数格式：工位+相机号（如"A1"、"B3"），为空/格式错误时直接返回</param>
        private void SelectNegativeRoiImage(string? param)
        {
            // 1. 第一层参数校验：参数为空/长度不足（至少2位，如"A1"），直接返回并记录错误日志
            if (string.IsNullOrEmpty(param) || param.Length < 2)
            {
                AddLog("ROI绘制无效参数", CoreLogLevel.Warn);
                return;
            }
            // 解析参数：第一位为工位（A/B），剩余部分为相机号（1-4）
            string station = param[0].ToString();
            string cameraNo = param.Substring(1);

            // 校验工位和相机号合法性
            // 2. 第二层合法性校验：工位仅支持A/B，相机号仅支持1-4
            if (!new[] { "A", "B" }.Contains(station) || !new[] { "1", "2", "3", "4" }.Contains(cameraNo))
            {
                AddLog($"[错误] 工位{station}或相机{cameraNo}无效");
                return;
            }

            //更新当前相机号
            // 3. 更新全局状态：记录当前操作的相机号，更新绘制标题（通知UI刷新）
            _currentRoiCameraNo = param;// 保存当前ROI操作的工位+相机号
            DrawTitle = $"绘制误判区域 - {station}工位相机{cameraNo}";// 绘制弹窗标题，适配当前操作
            OnPropertyChanged(nameof(DrawTitle));// 触发INotifyPropertyChanged，更新UI上的标题显示

            if (int.TryParse(cameraNo, out int num))
            {
                CurrentCameraNumber = num;
            }

            var openFileDialog = new OpenFileDialog
            {
                Filter = "图片文件|*.bmp;*.jpg;*.jpeg;*.png",
                Title = $"选择{station}工位相机{cameraNo}的参考图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string imagePath = openFileDialog.FileName;
                _cameraImagePaths[param] = imagePath;

                // 清空画布和旧图
                if (_inkCanvas != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _inkCanvas.Strokes.Clear();
                        CurrentRoiImageSource = null;
                        OnPropertyChanged(nameof(CurrentRoiImageSource));
                    });
                }

                // 加载新图片
                try
                {
                    using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                    {
                        stream.Position = 0;
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
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"图片加载失败：{ex.Message}", "错误");
                    return;
                }

                //加载该工位+相机的历史ROI
                LoadHistoryRoiToCanvas(param, _drawImageWidth, _drawImageHeight);
                IsDrawFlyoutOpen = true;
            }
        }


        /// <summary>
        /// 加载历史ROI
        /// </summary>
        private void LoadHistoryRoiToCanvas(string? param, int currentImageWidth, int currentImageHeight)
        {
            if (string.IsNullOrEmpty(param) || _inkCanvas == null || currentImageWidth == 0 || currentImageHeight == 0)
                return;

            // 解析参数
            string station = param[0].ToString();
            string cameraNo = param.Substring(1);

            // 历史ROI文件路径
            var roiDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig");
            string hobjPath = Path.Combine(roiDir, $"{station}{cameraNo}_ROI.hobj"); // 如Negative_A1_ROI.hobj
            string sizePath = Path.Combine(roiDir, $"{station}{cameraNo}_Size.txt");

            // 无历史ROI，直接返回
            if (!File.Exists(hobjPath) || !File.Exists(sizePath))
                return;

            try
            {
                // 读取历史尺寸
                string[] sizeParts = File.ReadAllText(sizePath).Split(',');
                if (sizeParts.Length < 2 || !int.TryParse(sizeParts[0], out int historyWidth) || !int.TryParse(sizeParts[1], out int historyHeight))
                    return;

                // 读取XLD数据
                HObject? roiXld = null;
                HTuple xldRow = new HTuple();
                HTuple xldCol = new HTuple();
                try
                {
                    HOperatorSet.ReadObject(out roiXld, hobjPath);
                    HOperatorSet.GetContourXld(roiXld, out xldRow, out xldCol);

                    if (xldRow.Length == 0 || xldCol.Length == 0)
                        return;

                    // 计算缩放比例
                    double scaleX = (double)currentImageWidth / historyWidth;
                    double scaleY = (double)currentImageHeight / historyHeight;
                    double canvasScaleX = _inkCanvas.ActualWidth / currentImageWidth;
                    double canvasScaleY = _inkCanvas.ActualHeight / currentImageHeight;

                    // 转换坐标并添加到画布
                    var stylusPoints = new StylusPointCollection();
                    for (int i = 0; i < xldRow.Length; i++)
                    {
                        double x = xldCol[i].D * scaleX * canvasScaleX;
                        double y = xldRow[i].D * scaleY * canvasScaleY;
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
                finally
                {
                    roiXld?.Dispose();
                    xldRow?.Dispose();
                    xldCol?.Dispose();
                }
            }
            catch
            {
                // 加载失败不提示
            }
        }

        /// <summary>
        /// 弹窗-删除当前绘制内容
        /// </summary>
        private void DeleteNegativeRoi()
        {
            if (_inkCanvas == null || string.IsNullOrEmpty(_currentRoiCameraNo))
                return;

            _inkCanvas.Strokes.Clear();

            // 如果是查看模式，_drawImageWidth 可能为 0，需要先读取
            if (_drawImageWidth == 0 || _drawImageHeight == 0)
            {
                var sizePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig", $"{_currentRoiCameraNo}_Size.txt");
                if (File.Exists(sizePath))
                {
                    var parts = File.ReadAllText(sizePath).Split(',');
                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0], out _drawImageWidth);
                        int.TryParse(parts[1], out _drawImageHeight);
                    }
                }
            }
            SaveNegativeRoiAsHalconRegion();
        }

        /// <summary>
        /// 保存ROI
        /// </summary>
        private void SaveNegativeRoiAsHalconRegion()
        {
            if (_inkCanvas == null)
            {
                MessageBox.Show("画布未初始化！", "错误");
                AddLog("画布未初始化！");
                return;
            }

            if (string.IsNullOrEmpty(_currentRoiCameraNo))
            {
                MessageBox.Show("未选择目标相机，请重新操作！", "错误");
                return;
            }

            // 解析当前工位和相机号
            string station = _currentRoiCameraNo;
            //string cameraNo = _currentRoiCameraNo.Substring(1);

            try
            {
                if (_drawImageWidth == 0 || _drawImageHeight == 0)
                {
                    MessageBox.Show("请选择需要绘制的图片后再修改！", "错误");
                    AddLog("未选择绘制图片，无法获取原图尺寸！");
                    return;
                }

                var roiDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig");
                Directory.CreateDirectory(roiDir);
                // 带工位的文件路径
                string hobjPath = Path.Combine(roiDir, $"{station}_ROI.hobj");
                string sizePath = Path.Combine(roiDir, $"{station}_Size.txt");

                // 清空ROI的情况
                if (_inkCanvas.Strokes.Count == 0)
                {
                    if (File.Exists(hobjPath))
                        File.Delete(hobjPath);

                    // 保存尺寸和图片路径
                    string imagePath = _cameraImagePaths.TryGetValue(_currentRoiCameraNo, out var path) ? path : "";
                    File.WriteAllText(sizePath, $"{_drawImageWidth},{_drawImageHeight},{imagePath}");

                    // 更新缓存
                    if (_detectionRegionCache.ContainsKey(_currentRoiCameraNo))
                    {
                        _detectionRegionCache[_currentRoiCameraNo]?.Dispose();
                        _detectionRegionCache.Remove(_currentRoiCameraNo);
                    }
                    //UpdateRoiStatus(station, cameraNo, false);

                    MessageBox.Show($"{station}工位的ROI已清空并保存！", "成功");
                    AddLog($"{station}工位的ROI已清空并保存！");
                    return;
                }

                double canvasWidth = _inkCanvas.ActualWidth;
                double canvasHeight = _inkCanvas.ActualHeight;

                if (canvasWidth == 0 || canvasHeight == 0)
                {
                    MessageBox.Show("画布尺寸无效！", "错误");
                    return;
                }

                double scaleX = _drawImageWidth / canvasWidth;
                double scaleY = _drawImageHeight / canvasHeight;

                HTuple row = new HTuple();
                HTuple col = new HTuple();

                foreach (var stroke in _inkCanvas.Strokes)
                {
                    foreach (var sp in stroke.StylusPoints)
                    {
                        double originalX = sp.X * scaleX;
                        double originalY = sp.Y * scaleY;
                        row = row.TupleConcat(originalY);
                        col = col.TupleConcat(originalX);
                    }
                }

                if (row.Length > 0 && col.Length > 0)
                {
                    row = row.TupleConcat(row[0]);
                    col = col.TupleConcat(col[0]);
                }

                HObject? roiXld = null;
                HObject? delRegion = null;
                HObject? dummyImage = null;
                try
                {
                    HOperatorSet.GenContourPolygonXld(out roiXld, row, col);
                    HOperatorSet.GenImageConst(out dummyImage, "byte", _drawImageWidth, _drawImageHeight);
                    HOperatorSet.GenRegionContourXld(roiXld, out delRegion, "filled");

                    HOperatorSet.WriteObject(roiXld, hobjPath);
                    string imagePath = _cameraImagePaths.TryGetValue(_currentRoiCameraNo, out var path) ? path : "";
                    File.WriteAllText(sizePath, $"{_drawImageWidth},{_drawImageHeight},{imagePath}");

                    // 更新缓存
                    if (_detectionRegionCache.TryGetValue(_currentRoiCameraNo, out var old))
                    {
                        old?.Dispose();
                        _detectionRegionCache.Remove(_currentRoiCameraNo);
                    }
                    _detectionRegionCache[_currentRoiCameraNo] = delRegion;
                    delRegion = null; // 所有权已转移，防止 finally 中误释放
                }
                finally
                {
                    delRegion?.Dispose();
                    roiXld?.Dispose();
                    dummyImage?.Dispose();
                    row?.Dispose();
                    col?.Dispose();
                }
                //UpdateRoiStatus(station, cameraNo, true);
                UpdateRoiStatus(station, _inkCanvas.Strokes.Count > 0);

                IsDrawFlyoutOpen = false;
                MessageBox.Show($"{station}工位相机的ROI已保存！", "成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存ROI失败：{ex.Message}", "错误");
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
        /// 查看ROI绘制结果
        /// </summary>
        private void ViewNegativeRoi(string? param)
        {
            if (_inkCanvas == null)
            {
                MessageBox.Show("画布正在初始化，请再次点击查看！", "提示");
                return;
            }

            // 解析参数
            if (string.IsNullOrEmpty(param) || param.Length < 2)
            {
                AddLog("[查看ROI] 无效参数（格式应为“A1”“B3”等）");
                return;
            }
            string station = param[0].ToString();
            string cameraNo = param.Substring(1);

            _currentRoiCameraNo = param; // 同步当前标识
            DrawTitle = $"绘制负极误判区域 - {station}工位相机{cameraNo}";

            var roiDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig");
            string hobjPath = Path.Combine(roiDir, $"{station}_ROI.hobj");
            string sizePath = Path.Combine(roiDir, $"{station}_Size.txt");

            if (!File.Exists(hobjPath) || !File.Exists(sizePath))
            {
                MessageBox.Show("ROI文件或尺寸文件不存在！", "提示");
                return;
            }

            try
            {
                string[] sizeParts = File.ReadAllText(sizePath).Split(',');
                if (sizeParts.Length != 3 || !int.TryParse(sizeParts[0], out int drawWidth) || !int.TryParse(sizeParts[1], out int drawHeight))
                {
                    MessageBox.Show("尺寸文件格式错误！", "错误");
                    return;
                }
                string imagePath = sizeParts[2];

                // 加载图片
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    using (var stream = new FileStream(imagePath, FileMode.Open))
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = stream;
                        bmp.EndInit();
                        bmp.Freeze();
                        CurrentRoiImageSource = bmp;
                    }
                }

                // 读取XLD并显示
                HObject? roiXld = null;
                HTuple xldRow = new HTuple();
                HTuple xldCol = new HTuple();
                try
                {
                    HOperatorSet.ReadObject(out roiXld, hobjPath);
                    HOperatorSet.GetContourXld(roiXld, out xldRow, out xldCol);

                    AddLog($"[查看ROI] {station}工位相机{cameraNo}，坐标点数量：row={xldRow.Length}, col={xldCol.Length}");

                    if (xldRow.Length == 0 || xldCol.Length == 0)
                    {
                        MessageBox.Show("从XLD提取不到坐标，请重新保存ROI！", "错误");
                        return;
                    }

                    double scaleX = _inkCanvas.ActualWidth / drawWidth;
                    double scaleY = _inkCanvas.ActualHeight / drawHeight;
                    ClearCanvasOnly();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var stylusPoints = new StylusPointCollection();
                        for (int i = 0; i < xldRow.Length; i++)
                        {
                            double x = xldCol[i].D * scaleX;
                            double y = xldRow[i].D * scaleY;
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
                        _inkCanvas.UpdateLayout();
                    });
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
            }
        }

        private void ClearCanvasOnly()
        {
            if (_inkCanvas == null) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                _inkCanvas.Strokes.Clear();
            });
        }
        /// <summary>
        /// 取消绘制并关闭Flyout
        /// </summary>
        private void CancelDrawRoi()
        {
            IsDrawFlyoutOpen = false;
        }


        /// <summary>
        /// 启动时加载所有ROI配置
        /// </summary>
        private void LoadAllHalconRoiConfigs()
        {
            try
            {
                var roiDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig");
                if (!Directory.Exists(roiDir))
                    return;

                // 扫描带工位的ROI文件
                var hobjFiles = Directory.GetFiles(roiDir, "*_ROI.hobj");
                foreach (var file in hobjFiles)
                {
                    try
                    {
                        // 从文件名提取工位和相机号
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        var parts = fileName.Split(new[] { "_ROI" }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 1) continue;
                        string stationCamera = parts[0];
                        if (stationCamera.Length < 2) continue;

                        string station = stationCamera;
                        //string cameraNo = stationCamera.Substring(1);
                        UpdateRoiStatus(station, true);

                        AddLog($"已加载{station}工位的ROI：{file}");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"加载ROI文件失败（{file}）：{ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"ROI配置加载失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 选择正极ROI参考图片并初始化绘制画布
        /// </summary>
        private void SelectPositiveRoiImage(string? param)
        {
            if (string.IsNullOrEmpty(param))
            {
                AddLog("正极ROI绘制无效参数", CoreLogLevel.Warn);
                return;
            }

            string station = param;  // 如"正极"

            // 校验工位合法性
            if (!new[] { "正极", "负极" }.Contains(station))
            {
                AddLog($"[错误] 工位{station}无效", CoreLogLevel.Warn);
                return;
            }

            _currentRoiCameraNo = param;
            DrawTitle = $"绘制误判区域 - {station}工位";
            OnPropertyChanged(nameof(DrawTitle));

            var openFileDialog = new OpenFileDialog
            {
                Filter = "图片文件|*.bmp;*.jpg;*.jpeg;*.png",
                Title = $"选择{station}工位的参考图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string imagePath = openFileDialog.FileName;
                _cameraImagePaths[param] = imagePath;

                // 清空画布
                if (_inkCanvas != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _inkCanvas.Strokes.Clear();
                        CurrentRoiImageSource = null;
                        OnPropertyChanged(nameof(CurrentRoiImageSource));
                    });
                }

                // 加载新图片
                try
                {
                    using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                    {
                        stream.Position = 0;
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
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"图片加载失败:{ex.Message}", "错误");
                    return;
                }

                // 加载历史ROI
                LoadHistoryRoiToCanvas(param, _drawImageWidth, _drawImageHeight);
                IsDrawFlyoutOpen = true;
            }
        }

        /// <summary>
        /// 删除正极当前绘制内容(复用负极逻辑)
        /// </summary>
        private void DeletePositiveRoi()
        {
            // 直接调用已有的删除逻辑
            DeleteNegativeRoi();
        }

        /// <summary>
        /// 查看正极ROI绘制结果
        /// </summary>
        private void ViewPositiveRoi(string? param)
        {
            if (_inkCanvas == null)
            {
                MessageBox.Show("画布正在初始化,请再次点击查看!", "提示");
                return;
            }

            if (string.IsNullOrEmpty(param) || param.Length < 2)
            {
                AddLog("[查看ROI] 无效参数");
                return;
            }

            string station = param;  // 如"正极"

            _currentRoiCameraNo = param;
            DrawTitle = $"绘制正极误判区域 - {station}工位";

            var roiDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig");
            string hobjPath = Path.Combine(roiDir, $"{station}_ROI.hobj");
            string sizePath = Path.Combine(roiDir, $"{station}_Size.txt");

            if (!File.Exists(hobjPath) || !File.Exists(sizePath))
            {
                MessageBox.Show("ROI文件或尺寸文件不存在!", "提示");
                return;
            }

            try
            {
                string[] sizeParts = File.ReadAllText(sizePath).Split(',');
                if (sizeParts.Length != 3 || !int.TryParse(sizeParts[0], out int drawWidth) || !int.TryParse(sizeParts[1], out int drawHeight))
                {
                    MessageBox.Show("尺寸文件格式错误!", "错误");
                    return;
                }
                string imagePath = sizeParts[2];

                // 加载图片
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    using (var stream = new FileStream(imagePath, FileMode.Open))
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = stream;
                        bmp.EndInit();
                        bmp.Freeze();
                        CurrentRoiImageSource = bmp;
                    }
                }

                // 读取XLD数据并显示
                HObject? roiXld = null;
                HTuple xldRow = new HTuple();
                HTuple xldCol = new HTuple();
                try
                {
                    HOperatorSet.ReadObject(out roiXld, hobjPath);
                    HOperatorSet.GetContourXld(roiXld, out xldRow, out xldCol);

                    if (xldRow.Length == 0 || xldCol.Length == 0)
                    {
                        MessageBox.Show("从XLD提取不到坐标,请重新保存ROI!", "错误");
                        return;
                    }

                    double scaleX = _inkCanvas.ActualWidth / drawWidth;
                    double scaleY = _inkCanvas.ActualHeight / drawHeight;
                    ClearCanvasOnly();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var stylusPoints = new StylusPointCollection();
                        for (int i = 0; i < xldRow.Length; i++)
                        {
                            double x = xldCol[i].D * scaleX;
                            double y = xldRow[i].D * scaleY;
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
                        _inkCanvas.UpdateLayout();
                    });
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
                MessageBox.Show($"加载ROI失败:{ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 保存正极ROI区域
        /// </summary>
        private void SavePositiveRoiAsHalconRegion()
        {
            if (_inkCanvas == null)
            {
                MessageBox.Show("画布未初始化!", "错误");
                AddLog("画布未初始化!");
                return;
            }

            if (string.IsNullOrEmpty(_currentRoiCameraNo))
            {
                MessageBox.Show("未选择目标相机,请重新操作!", "错误");
                return;
            }

            // 解析当前工位和相机号
            string station = _currentRoiCameraNo;

            try
            {
                if (_drawImageWidth == 0 || _drawImageHeight == 0)
                {
                    MessageBox.Show("请选择需要绘制的图片后再修改!", "错误");
                    AddLog("未选择绘制图片,无法获取原图尺寸!");
                    return;
                }

                var roiDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig");
                Directory.CreateDirectory(roiDir);

                // 正极ROI文件路径(关键差异:文件名前缀为"正极")
                string hobjPath = Path.Combine(roiDir, $"{station}_ROI.hobj");
                string sizePath = Path.Combine(roiDir, $"{station}_Size.txt");

                // 清空ROI的情况
                if (_inkCanvas.Strokes.Count == 0)
                {
                    if (File.Exists(hobjPath))
                        File.Delete(hobjPath);

                    // 保存尺寸和图片路径
                    string imagePath = _cameraImagePaths.TryGetValue(_currentRoiCameraNo, out var path) ? path : "";
                    File.WriteAllText(sizePath, $"{_drawImageWidth},{_drawImageHeight},{imagePath}");

                    // 更新缓存
                    if (_detectionRegionCache.ContainsKey(_currentRoiCameraNo))
                    {
                        _detectionRegionCache[_currentRoiCameraNo]?.Dispose();
                        _detectionRegionCache.Remove(_currentRoiCameraNo);
                    }

                    MessageBox.Show($"{station}工位的ROI已清空并保存!", "成功");
                    AddLog($"{station}工位的ROI已清空并保存!");
                    return;
                }

                double canvasWidth = _inkCanvas.ActualWidth;
                double canvasHeight = _inkCanvas.ActualHeight;

                if (canvasWidth == 0 || canvasHeight == 0)
                {
                    MessageBox.Show("画布尺寸无效!", "错误");
                    return;
                }

                double scaleX = _drawImageWidth / canvasWidth;
                double scaleY = _drawImageHeight / canvasHeight;

                HTuple row = new HTuple();
                HTuple col = new HTuple();

                foreach (var stroke in _inkCanvas.Strokes)
                {
                    foreach (var sp in stroke.StylusPoints)
                    {
                        double originalX = sp.X * scaleX;
                        double originalY = sp.Y * scaleY;
                        row = row.TupleConcat(originalY);
                        col = col.TupleConcat(originalX);
                    }
                }

                if (row.Length > 0 && col.Length > 0)
                {
                    row = row.TupleConcat(row[0]);
                    col = col.TupleConcat(col[0]);
                }

                HObject? roiXld = null;
                HObject? delRegion = null;
                HObject? dummyImage = null;
                try
                {
                    HOperatorSet.GenContourPolygonXld(out roiXld, row, col);
                    HOperatorSet.GenImageConst(out dummyImage, "byte", 1920, 1200);
                    HOperatorSet.GenRegionContourXld(roiXld, out delRegion, "filled");

                    HOperatorSet.WriteObject(roiXld, hobjPath);
                    string imagePath = _cameraImagePaths.TryGetValue(_currentRoiCameraNo, out var path) ? path : "";
                    File.WriteAllText(sizePath, $"{_drawImageWidth},{_drawImageHeight},{imagePath}");

                    // 更新缓存
                    if (_detectionRegionCache.ContainsKey(_currentRoiCameraNo))
                    {
                        _detectionRegionCache[_currentRoiCameraNo]?.Dispose();
                        _detectionRegionCache.Remove(_currentRoiCameraNo);
                    }
                    _detectionRegionCache.Add(_currentRoiCameraNo, delRegion);
                }
                finally
                {
                    roiXld?.Dispose();
                    row?.Dispose();
                    col?.Dispose();
                }

                UpdateRoiStatus(station, true);

                IsDrawFlyoutOpen = false;
                MessageBox.Show($"{station}工位的ROI已保存!", "成功");
                AddLog($"{station}工位的ROI已保存!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存ROI失败:{ex.Message}", "错误");
                AddLog($"保存正极ROI异常:{ex.Message}");
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
        private HObject? GenerateDelRegionFromXldFile(string stationCamera)
        {

            string station = stationCamera[0].ToString();
            string cameraNo = stationCamera.Substring(1);
            HObject? roiXld = null;
            HObject? delRegion = null;
            HObject? dummyImage;
            try
            {
                string roiDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig");
                // 使用带工位的文件名
                string hobjPath = Path.Combine(roiDir, $"Negative_{stationCamera}_ROI.hobj");

                if (!File.Exists(hobjPath))
                {
                    if (!_warnedGenerateFailCameras.Contains(stationCamera))
                    {
                        //AddLog($"[兜底失败] {station}工位相机{cameraNo}的XLD文件不存在：{hobjPath}");
                        _warnedGenerateFailCameras.Add(stationCamera);
                    }
                    return null;
                }

                // 读取XLD并生成Region
                HOperatorSet.ReadObject(out roiXld, hobjPath);
                HOperatorSet.GenImageConst(out dummyImage, "byte", 1920, 1200);
                HOperatorSet.GenRegionContourXld(roiXld, out delRegion, "filled");
                return delRegion;
            }
            catch (Exception ex)
            {
                AddLog($"[兜底异常] {station}工位相机{cameraNo}：{ex.Message}");
                delRegion?.Dispose();
                roiXld?.Dispose();
                return null;
            }
            finally
            {
                roiXld?.Dispose();
            }
        }
        #endregion

        #region 在线检测 - 选择监控文件夹

        #region 双工位路径选择方法
        // A工位监听路径选择文件夹
        private void SelectMonitorFolderA()
        {
            // 1. 创建WPF内置的文件夹选择对话框实例（需引用Microsoft.Win32命名空间）
            var folderDialog = new OpenFolderDialog();
            // 2. 设置对话框标题，明确告知用户选择用途
            folderDialog.Title = "选择A工位在线检测监控文件夹";
            // 3. 设置初始打开目录为系统“我的图片”文件夹，提升用户体验
            folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            // 4. 验证文件夹名称的合法性（避免无效路径）
            folderDialog.ValidateNames = true;
            // 5. 禁止多选文件夹（符合“选择单个监控文件夹”的业务需求）
            folderDialog.Multiselect = false;
            // 6. 显示对话框，指定父窗口为应用主窗口（保证对话框模态且居中）
            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            // 7. 判断用户是否确认选择（点击“确定”按钮）
            if (result == true)
            {
                // 8. 关键：获取选中的文件夹路径（注意是FolderName而非FileName，FileName用于文件选择）
                SelectedMonitorFolderPathA = folderDialog.FolderName;
                // 9. 记录操作日志，便于追溯用户操作
                AddLog($"[A工位] 已选择在线监控文件夹：{SelectedMonitorFolderPathA}");
            }
        }
        // B工位监听路径选择文件夹
        private void SelectMonitorFolderB()
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "选择B工位在线检测监控文件夹",
                ValidateNames = true,
                Multiselect = false,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };
            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                SelectedMonitorFolderPathB = folderDialog.FolderName;
                AddLog($"[B工位] 已选择在线监控文件夹：{SelectedMonitorFolderPathB}");
            }
        }

        #endregion

        #region 双工位启动监控
        // 监控运行状态标记
        private bool _isAMonitorRunning = false;

        // 工位启动监控（监听异常时底层启动逻辑）
        private void StartOnlineMonitoring(Station station)
        {
            // 获取工位特定的变量和方法
            var (isThreadRunning, isMonitorRunning, isThreadTerminating, folderPath,
                 startWatcher, stopWatchers, startProcessing) = GetStationResources(station);

            if (isThreadRunning) return;
            SetStationThreadRunning(station, true);

            lock (_stationCountLock)
            {
                if (isMonitorRunning)
                {
                    AddLog($"[{station}工位] 在线监控已启动，无需重复执行");
                    return;
                }

                CurrentMonitoringStation = station.ToString();
                SetStationThreadTerminating(station, false);

                try
                {
                    startWatcher?.Invoke();
                    SetStationMonitorRunning(station, true);

                    AddLog($"[{station}工位] 已启动监控，缓冲区已扩容。路径：{folderPath}");
                    startProcessing?.Invoke();
                }
                catch (Exception ex)
                {
                    AddLog($"[{station}工位] 启动监控失败: {ex.Message}", CoreLogLevel.Error);
                    stopWatchers?.Invoke();
                    SetStationThreadRunning(station, false);
                }
            }
        }

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
        /// 获取工位资源
        /// </summary>
        private (bool isThreadRunning, bool isMonitorRunning, bool isThreadTerminating, string? folderPath,Action startWatcher, Action stopWatchers, Action startProcessing) GetStationResources(Station station)
        {
            return station switch
            {
                Station.A => (
                    _isThreadRunningA,
                    _isAMonitorRunning,
                    _isThreadTerminatingA,
                    SelectedMonitorFolderPathA,
                    StartRootWatcherA,
                    StopAllWatchersA,
                    StartConcurrentProcessingA
                ),
                Station.B => (
                    _isThreadRunningB,
                    _isBMonitorRunning,
                    _isThreadTerminatingB,
                    SelectedMonitorFolderPathB,
                    StartRootWatcherB,
                    StopAllWatchersB,
                    StartConcurrentProcessingB
                ),
                _ => throw new ArgumentException($"未知工位: {station}")
            };
        }

        /// <summary>
        /// 设置工位线程运行状态
        /// </summary>
        private void SetStationThreadRunning(Station station, bool value)
        {
            if (station == Station.A)
                _isThreadRunningA = value;
            else if (station == Station.B)
                _isThreadRunningB = value;
        }

        /// <summary>
        /// 设置工位监控运行状态
        /// </summary>
        private void SetStationMonitorRunning(Station station, bool value)
        {
            if (station == Station.A)
                _isAMonitorRunning = value;
            else if (station == Station.B)
                _isBMonitorRunning = value;
        }

        /// <summary>
        /// 设置工位线程终止标志
        /// </summary>
        private void SetStationThreadTerminating(Station station, bool value)
        {
            if (station == Station.A)
                _isThreadTerminatingA = value;
            else if (station == Station.B)
                _isThreadTerminatingB = value;
        }










        /// <summary>
        /// 启动主监听器
        /// </summary>
        private void StartRootWatcherA()
        {
            if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathA) || !Directory.Exists(SelectedMonitorFolderPathA))
            {
                AddLog("[A工位] 监控文件夹不存在，请先选择有效文件夹");
                _isAMonitorRunning = false;
                MessageBox.Show("A工位监控文件夹不存在，请先选择有效文件夹", "在线检测错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            lock (_watcherLock)
            {
                if (_rootWatcherA != null)
                {
                    _rootWatcherA.EnableRaisingEvents = false;
                    _rootWatcherA.Dispose();
                }

                _rootWatcherA = new FileSystemWatcher(SelectedMonitorFolderPathA)
                { 
                    IncludeSubdirectories = true,  
                    NotifyFilter = NotifyFilters.FileName,
                    InternalBufferSize = 64 * 1024 
                };
                _rootWatcherA.Filters.Add("*.jpg");
                _rootWatcherA.Filters.Add("*.jpeg");
                _rootWatcherA.Filters.Add("*.bmp");
                _rootWatcherA.Filters.Add("*.png");
                _rootWatcherA.Created += OnFileCreatedA;
                _rootWatcherA.Error += OnWatcherErrorA;
                _rootWatcherA.EnableRaisingEvents = true;

                AddLog($"[A工位监听器] 已启动 - 路径: {SelectedMonitorFolderPathA}");
            }
        }


        /// <summary>
        /// 文件创建事件
        /// </summary>
        private void OnFileCreatedA(object sender, FileSystemEventArgs e)
        {
            _ = Task.Run(async () => await ProcessNewFileAsync(e.FullPath, _processedFilesA, "A"));
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
                
                if (!File.Exists(filePath) )
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




        /// <summary>
        /// 处理 FileSystemWatcher 的内部错误（如缓冲区溢出）
        /// </summary>
        private void OnWatcherErrorA(object sender, ErrorEventArgs e)
        {
            // 获取异常信息
            Exception ex = e.GetException();
            AddLog($"[A工位-监控严重错误] 文件监控发生异常: {ex.Message}。尝试自动重启监控...", CoreLogLevel.Error);

            // 缓冲区溢出是最常见的原因
            if (ex is InternalBufferOverflowException)
            {
                AddLog("[A工位] 检测到缓冲区溢出，说明短时间内文件过多。", CoreLogLevel.Warn);
            }

            // 必须在非 UI 线程中重启，防止死锁，但因为涉及 UI 状态，需谨慎操作
            Task.Run(async () =>
            {
                // 1. 先停止现有监控
                StopOnlineMonitoringA();

                // 2. 稍作等待，让系统资源释放
                await Task.Delay(2000);

                // 3. 重新检查开关状态，如果用户可以接受自动重启，则再次启动
                // 注意：这需要操作 UI 线程绑定的属性或直接调用内部逻辑
                if (IsOnlineDetectionEnabledA)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        AddLog("[A工位] 正在执行自动重启恢复监控...");
                        // 重新触发启动逻辑（注意避免死循环，这里直接调用底层启动逻辑）
                        StartOnlineMonitoring(Station.A);

                        // 恢复计数（因为 StopOnlineMonitoringA 会减计数）
                        lock (_stationCountLock)
                        {
                            _runningStationsCount++;
                        }
                    });
                }
            });
        }


        private async Task StartOnlineMonitoringAsyncB()
        {
            await Task.Run(() =>
            {
                bool alreadyRunning = false;
                lock (_stationCountLock)
                {
                    alreadyRunning = _isAMonitorRunning;
                    if (!alreadyRunning)
                    {
                        CurrentMonitoringStation = "B";
                        _isThreadTerminatingB = false;
                        StartRootWatcherB();        // 不涉及UI，锁内安全
                        _isAMonitorRunning = true;
                    }
                }

                if (alreadyRunning)
                {
                    UpdateUIState(() => AddLog("[在线检测-B] 监控已在运行，忽略重复启动"));
                    return;
                }

                // ✅ UI更新移到锁外，避免死锁
                UpdateUIState(() => AddLog($"[在线检测-B] 监控已启动  路径: {SelectedMonitorFolderPathA}"));
                StartConcurrentProcessingB();   // 启动队列处理，也在锁外
            });
        }

        /// <summary>
        /// B工位并发启动器
        /// </summary>
        private void StartConcurrentProcessingB()
        {
            lock (_processingLockB)
            {
                if (_isQueueRunningB) return;

                _ctsB?.Cancel();
                _ctsB?.Dispose();
                _ctsB = new CancellationTokenSource();

                _ = Task.Run(async () => await ProcessQueueConcurrentB(_ctsB.Token), _ctsB.Token);
            }
        }

        private void StartRootWatcherB()
        {
            if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathB) || !Directory.Exists(SelectedMonitorFolderPathB))
            {
                AddLog("[B工位] 监控文件夹不存在，请先选择有效文件夹");
                _isBMonitorRunning = false;
                MessageBox.Show("B工位监控文件夹不存在，请先选择有效文件夹", "在线检测错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            lock (_watcherLock)
            {
                if(_rootWatcherB != null)
                {
                    _rootWatcherB.EnableRaisingEvents = false;
                    _rootWatcherB.Dispose();
                }

                _rootWatcherB = new FileSystemWatcher(SelectedMonitorFolderPathB!)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName,
                    InternalBufferSize = 64 * 1024
                };
                _rootWatcherB.Filters.Add("*.jpg");
                _rootWatcherB.Filters.Add("*.jpeg");
                _rootWatcherB.Filters.Add("*.bmp");
                _rootWatcherB.Filters.Add("*.png");
                _rootWatcherB.Created += OnFileCreatedB;
                _rootWatcherB.Error += OnWatcherErrorB; 
                _rootWatcherB.EnableRaisingEvents = true;
                AddLog($"[B监听器] 已启动 － 路径:{SelectedMonitorFolderPathB}");
            }
        }

        /// <summary>
        /// 文件创建事件
        /// </summary>
        private void OnFileCreatedB(object sender, FileSystemEventArgs e)
        {
            _ = Task.Run(async () => await ProcessNewFileAsync(e.FullPath, _processedFilesB, "B"));
        }

        /// <summary>
        /// 处理 FileSystemWatcher 的内部错误（如缓冲区溢出）
        /// </summary>
        private void OnWatcherErrorB(object sender, ErrorEventArgs e)
        {
            // 获取异常信息
            Exception ex = e.GetException();
            AddLog($"[B工位-监控严重错误] 文件监控发生异常: {ex.Message}。尝试自动重启监控...", CoreLogLevel.Error);

            // 缓冲区溢出是最常见的原因
            if (ex is InternalBufferOverflowException)
            {
                AddLog("[B工位] 检测到缓冲区溢出，说明短时间内文件过多。", CoreLogLevel.Warn);
            }

            // 必须在非 UI 线程中重启，防止死锁，但因为涉及 UI 状态，需谨慎操作
            Task.Run(async () =>
            {
                // 1. 先停止现有监控
                StopOnlineMonitoringB();

                // 2. 稍作等待，让系统资源释放
                await Task.Delay(2000);

                // 3. 重新检查开关状态，如果用户可以接受自动重启，则再次启动
                // 注意：这需要操作 UI 线程绑定的属性或直接调用内部逻辑
                if (IsOnlineDetectionEnabledB)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        AddLog("[B工位] 正在执行自动重启恢复监控...");
                        // 重新触发启动逻辑（注意避免死循环，这里直接调用底层启动逻辑）
                        StartOnlineMonitoring(Station.B);

                        // 恢复计数（因为 StopOnlineMonitoringA 会减计数）
                        lock (_stationCountLock)
                        {
                            _runningStationsCount++;
                        }
                    });
                }
            });
        }
        #endregion

        #region 双工位停止监控
        // A工位停止监控
        private void StopOnlineMonitoringA()
        {
            lock (_stationCountLock)
            {
                if (!_isAMonitorRunning)
                {
                    //AddLog("[A工位] 在线监控未启动，无需执行停止操作");
                    return;
                }

                //if (_onlineMonitorWatcherA != null)
                //{
                //    DisposeWatcherA();
                //}

                // 标记线程终止并透过 CancellationToken 取消处理任务
                _isThreadTerminatingA = true;
                StopAllWatchersA();


                //_processedFiles.Clear();

                //StopConcurrentProcessingA();
                _isAMonitorRunning = false;
                _isThreadRunningA = false;

                AddLog("[A工位] 监控已停止");


                //// 取消 processing task
                //lock (_processingLockA)
                //{
                //    try
                //    {
                //        _ctsA?.Cancel();
                //    }
                //    catch { }

                //    // 唤醒可能在等待的线程
                //    _queueEventA.Set();
                //}

                //// 清空队列（如果你希望保留未处理项，先注释掉下面两行）
                //lock (_queueLockA)
                //{
                //    _safeDetectQueueA.Clear();
                //}

                //_isQueueRunningA = false;

                //if (_runningStationsCount > 0)
                //{
                //    _runningStationsCount--;
                //}

                //CheckAndGenerateReport();

                //// 重置运行状态
                //_isAMonitorRunning = false;
                //_isThreadRunningA = false;
                //AddLog("[A工位] 在线监控已停止（StopOnlineMonitoringA 完成）");
            }
        }
        private void StopAllWatchersA()
        {
            lock (_watcherLock)
            {
                // 停止主监听器
                if (_rootWatcherA != null)
                {
                    _rootWatcherA.EnableRaisingEvents = false;
                    _rootWatcherA.Dispose();
                    _rootWatcherA = null;
                }
            }
        }
        //B 工位停止监控
        private void StopOnlineMonitoringB()
        {
            lock (_stationCountLock)
            {
                if (!_isBMonitorRunning) return;
                _isThreadTerminatingB = true;
                _isBMonitorRunning = false;
                _isThreadRunningB = false;
            }

            StopAllWatchersB();

            lock (_processingLockB)
            {
                _ctsB?.Cancel();
                _ctsB?.Dispose();
                _ctsB = null;
            }

            _queueEventB.Set(); // 唤醒可能阻塞的处理线程，让其退出
            AddLog("[B工位] 监控已停止");
        }
        private void StopAllWatchersB()
        {
            lock (_watcherLock)
            {
                // 停止子监听器
                if (_rootWatcherB != null)
                {
                    _rootWatcherB.EnableRaisingEvents = false;
                    _rootWatcherB.Dispose();
                    _rootWatcherB = null;
                }
            }
        }
        #endregion



        #region 双工位并发处理启动
        private Task? _processingTaskA = null;
        private Task? _processingTaskB = null;
        private CancellationTokenSource? _ctsA = null;
        private CancellationTokenSource? _ctsB = null;
        private readonly object _processingLockA = new object();
        private readonly object _processingLockB = new object();

        // A工位启动并发处理
        private void StartConcurrentProcessingA()
        {
            // 加锁保护：保证队列处理任务的原子操作（防止多线程同时启动重复的处理任务）
            lock (_processingLockA)
            {
                if (_isQueueRunningA)
                {
                    AddLog("[A队列] 队列处理已在运行，跳过重复启动");
                    return;
                }

                // 任务状态校验：若已有处理任务在运行（未完成），直接返回，避免重复启动
                // _processingTaskA != null：存在未释放的任务实例；!_processingTaskA.IsCompleted：任务仍在运行
                if (_processingTaskA != null && !_processingTaskA.IsCompleted)
                {
                    AddLog("[A队列] 处理线程已在运行，跳过重复启动");
                    return;
                }
                // 1.创建新的取消令牌源：用于后续主动终止队列处理任务（如停止检测时取消）
                if (_ctsA != null)
                {
                    _ctsA.Cancel();
                    _ctsA.Dispose();
                }
                _ctsA = new CancellationTokenSource();

                // 提取取消令牌，传递给异步任务
                CancellationToken token = _ctsA.Token;
                // 2. 启动异步任务：在后台线程执行队列处理逻辑（避免阻塞UI线程）
                _processingTaskA = Task.Run(async () =>
                {
                    // 标记A队列处理线程已启动（供外部判断队列状态）
                    _isQueueRunningA = true;
                    AddLog("A队列线程启动");
                    try
                    {
                        // 核心逻辑：异步执行A队列的并发处理（传入取消令牌，支持中途取消）
                        // ConfigureAwait(false)：不捕获当前同步上下文（UI上下文），提升异步性能，避免UI线程阻塞
                        await ProcessQueueConcurrentA(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        AddLog("A队列线程收到取消请求并退出");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"A队列线程未处理异常：{ex.Message}");
                    }
                    finally
                    {
                        _isQueueRunningA = false;
                        AddLog("A队列线程已终止");
                    }
                }, token);// 将取消令牌传递给Task.Run，支持任务级别的取消（如任务未启动时直接取消）
            }
        }


        #endregion

        #region 双工位队列处理
        /// <summary>
        ///  A工位队列并发处理：A工位异步并发处理检测队列核心方法（消费队列中的文件路径，执行图片损伤检测）
        /// 核心设计：优雅退出（取消令牌+线程终止标记）+ 低CPU占用（WaitHandle等待）+ 并发控制（信号量）+ 线程安全（锁）+ UI线程安全更新
        /// </summary>
        /// <param name="token">取消令牌：用于接收“停止检测”的取消信号，实现优雅退出</param>
        /// <returns>异步任务</returns>
        private async Task ProcessQueueConcurrentA(CancellationToken token)
        {
            // 队列报警阈值配置
            const int QueueWarningThreshold = 100;
            const int QueueCriticalThreshold = 500;
            int lastAlertCount = 0;

            _isQueueRunningA = true; // 确保标记为运行状态
            AddLog("A队列线程启动");

            try
            {
                while (!token.IsCancellationRequested && !_isThreadTerminatingA)
                {
                    try
                    {
                        string? filePath = null;
                        int currentQueueCount = 0;  // 当前队列数量
                                                    // 1. 线程安全从队列取待检测文件路径（lock保证多生产者/消费者下的队列安全）
                        lock (_queueLockA)
                        {
                            _safeDetectQueueA.TryDequeue(out filePath);
                            currentQueueCount = _safeDetectQueueA.Count;
                        }

                        // 2. 队列过载报警检测
                        if (currentQueueCount > 0)
                        {
                            // 仅当队列数量变化超过阈值的10%时才重新报警（避免频繁报警）
                            bool shouldAlert = Math.Abs(currentQueueCount - lastAlertCount) > Math.Max(10, QueueWarningThreshold * 0.1);

                            if (shouldAlert)
                            {
                                if (currentQueueCount >= QueueCriticalThreshold)
                                {
                                    AddLog($"🔴 [队列严重告警] 队列积压 {currentQueueCount} 个文件，超过临界值 {QueueCriticalThreshold}！");
                                    Growl.Warning($"检测队列积压 {currentQueueCount} 个文件，超过临界值 {QueueCriticalThreshold}！");

                                    lastAlertCount = currentQueueCount;
                                }
                                else if (currentQueueCount >= QueueWarningThreshold)
                                {
                                    AddLog($"⚠️ [队列警告] 队列积压 {currentQueueCount} 个文件，超过警告值 {QueueWarningThreshold}");
                                    lastAlertCount = currentQueueCount;
                                }
                                else if (lastAlertCount >= QueueWarningThreshold && currentQueueCount < QueueWarningThreshold)
                                {
                                    AddLog($"✅ [队列恢复] 队列已降至 {currentQueueCount} 个文件，恢复正常");
                                    lastAlertCount = 0;
                                }
                            }
                        }

                        // 3. 队列无数据时：进入等待状态（避免空循环占用100%CPU）
                        if (filePath == null)
                        {
                            // ⚡ 核心优化：WaitHandle.WaitAny 监听两个信号，实现“有任务就唤醒，有取消就退出，超时就重试”
                            int signaledIndex = WaitHandle.WaitAny(new WaitHandle[]
                            {
                            // 监听的信号集合：
                            _queueEventA, token.WaitHandle}, TimeSpan.FromSeconds(30));

                                if (signaledIndex == WaitHandle.WaitTimeout)
                                    continue;
                                if (token.IsCancellationRequested || _isThreadTerminatingA)
                                    break;
                                continue;
                        }

                        // 3. 检测取消信号：若已取消，直接抛出取消异常（触发外层catch处理）
                        token.ThrowIfCancellationRequested();

                        // 4. 校验文件存在性：文件已被删除则跳过，避免后续检测异常
                        if (!File.Exists(filePath))
                        {
                            AddLog($"[A工位-队列] {Path.GetFileName(filePath)} 已删除，跳过");
                            continue;
                        }
                        // 5. 信号量等待：控制并发处理数量（避免同时处理过多文件导致CPU/内存耗尽）
                        await _semaphoreA.WaitAsync(token).ConfigureAwait(false);


                        // 6. 核心：图片检测与结果处理逻辑
                        try
                        {
                            // 6.1 动态对象存储检测数据（灵活扩展字段）
                            dynamic detectionData = new ExpandoObject();
                            // 解析文件路径到动态对象（提取如文件名、路径、时间等信息）
                            ParsePathToDynamicObject(detectionData, filePath,true);
                            detectionData.Station = "A";// 标记工位为A

                            string roiKey = GetRoiKey(filePath, "A");

                            // 6.2 执行损伤检测：核心业务逻辑，返回OK/NG/异常                        
                            string detectResult = DamageDetection(filePath, roiKey, out List<string> triggeredList); 
                            detectionData.TriggeredDefects = triggeredList;
                            DateTime realDetectionTime = DateTime.Now;
                            bool isNG = detectResult == "NG";// 判断是否为不良品

                            // 6.3 将图片添加到结果队列（用于后续报表/展示）
                            AddImageToQueue(filePath, isNG, "A", realDetectionTime);
                            // 6.4 空图片异常处理：不统计，直接跳过后续逻辑
                            if (detectResult.Equals("异常（空图片）", StringComparison.OrdinalIgnoreCase))
                            {
                                AddLog($"[A工位-结果] {Path.GetFileName(filePath)}：空图片，不统计");
                                continue;
                            }
                            // 6.5 判断是否保存结果图片（根据配置：OK保存/NG保存）
                            bool isResultSaved = (detectResult == "OK" && IsSaveResultOK) || (detectResult == "NG" && IsSaveResultNG);
                            // 获取结果图片保存路径（若需要保存）
                            detectionData.ResultImagePath = isResultSaved ? GetResultImagePath(filePath, detectResult, true) : null;
                            detectionData.DetectionResult = detectResult;
                            // 6.6 跨线程更新UI：后台线程不能直接操作UI，需通过Dispatcher切回UI线程
                            // InvokeAsync：异步执行，避免阻塞检测线程
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                AddDetectionDataAndEnsureCapacity(detectionData); // 安全添加并清理
                                UpdateDisplayDataList();// 更新展示用的数据列表
                                CalculateDetectionStatistics();// 重新计算检测统计（OK数、NG数、OK率等）
                            });
                        }
                        catch (Exception ex)
                        {
                            AddLog($"[A工位-检测异常] {Path.GetFileName(filePath)}：{ex.Message}");
                        }
                        finally
                        {
                            // 7. 释放信号量：无论检测成功/失败/异常，必须释放，避免信号量耗尽导致死锁
                            _semaphoreA.Release();
                        }

                    }
                    catch (OperationCanceledException)
                    {
                        AddLog("A队列线程收到取消请求并退出");
                        break;
                    }
                    catch (Exception ex)
                    {
                        AddLog($"A队列线程处理过程中出现异常：{ex.Message}");
                        // 不退出线程，继续处理下一个文件
                        await Task.Delay(1000); // 等待1秒后继续
                    }

                }
            }
            finally
            {
                _isQueueRunningA = false;
                AddLog("A队列线程已终止");
            }

        }


        public void EnqueueTask(string filePath,string stationName)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                AddLog($"[{stationName}工位-入队] 无效路径，跳过入队");
                return;
            }

            try
            {
                if (stationName == "A")
                {
                    lock (_queueLockA)
                    {
                        _safeDetectQueueA.Enqueue(filePath);
                    }

                    _queueEventA.Set(); // 唤醒等待线程（线程由 StartOnlineMonitoringA 启动并保持）
                                        // AddLog($"[A工位-入队] 新任务入队：{Path.GetFileName(filePath)} (队列大小未知)");
                    if (!_isQueueRunningA)
                    {
                        AddLog($"[{stationName}工位-入队] 检测到处理线程未运行，尝试重新启动");
                        StartConcurrentProcessingA();
                    }
                }
                else if (stationName == "B")
                {
                    lock (_queueLockB)
                    {
                        _safeDetectQueueB.Enqueue(filePath);
                    }

                    _queueEventB.Set();

                    if (!_isQueueRunningB)
                    {
                        AddLog($"[{stationName}工位-入队] 检测到处理线程未运行，尝试重新启动");
                        StartConcurrentProcessingB();
                    }
                }
                else
                {
                    AddLog($"[入队] 未知工位: {stationName}", CoreLogLevel.Error);
                    return;
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
            _fullDetectionDataList.Add(detectionData);

            if (_fullDetectionDataList.Count > 2000)
            {
                var itemsToRemove = _fullDetectionDataList.Take(200).ToList();

                int removedTotal = itemsToRemove.Count;
                int removedOK = itemsToRemove.Count(d =>
                {
                    var dict = d as IDictionary<string, object>;
                    return dict != null && dict.TryGetValue("DetectionResult", out var res) && res?.ToString() == "OK";
                });
                int removedNG = removedTotal - removedOK;

                lock (_statisticsLock)
                {
                    _historyTotalCount += removedTotal;
                    _historyOKCount += removedOK;
                    _historyNGCount += removedNG;

                    foreach (var d in itemsToRemove)
                    {
                        var dict = d as IDictionary<string, object>;
                        if (dict != null && dict.TryGetValue("DetectionResult", out var res) && res?.ToString() == "NG")
                        {
                            if (dict.TryGetValue("TriggeredDefects", out var defectsObj) && defectsObj is List<string> defects)
                            {
                                foreach (var defect in defects)
                                {
                                    if (!_historyDefectNGCount.ContainsKey(defect)) _historyDefectNGCount[defect] = 0;
                                    _historyDefectNGCount[defect]++;
                                }
                            }
                        }
                    }
                }

                _fullDetectionDataList.RemoveRange(0, 200);
            }
        }

        private async Task ProcessQueueConcurrentB(CancellationToken token)
        {
            _isQueueRunningB = true;
            AddLog("B队列线程启动");
            try
            {
                while (!token.IsCancellationRequested && !_isThreadTerminatingB)
                {
                    string? filePath = null;
                    lock (_queueLockB) { _safeDetectQueueB.TryDequeue(out filePath); }

                    if (filePath == null)
                    {
                        int signaledIndex = WaitHandle.WaitAny(new WaitHandle[] { _queueEventB, token.WaitHandle }, TimeSpan.FromSeconds(30));
                        if (signaledIndex == WaitHandle.WaitTimeout) continue;
                        if (token.IsCancellationRequested || _isThreadTerminatingB) break;
                        continue;
                    }

                    token.ThrowIfCancellationRequested();
                    if (!File.Exists(filePath))
                    {
                        AddLog($"[B工位-队列] {Path.GetFileName(filePath)} 已删除，跳过");
                        continue;
                    }

                    // 获取并发许可
                    await _semaphoreB.WaitAsync(token).ConfigureAwait(false);

                    try
                    {
                        // 执行检测逻辑 
                        dynamic detectionData = new ExpandoObject();
                        ParsePathToDynamicObject(detectionData, filePath,true);
                        detectionData.Station = "B";

                        string roiKey = GetRoiKey(filePath, "B");
                        string detectResult = DamageDetection(filePath, roiKey, out List<string> triggeredList);
                        detectionData.TriggeredDefects = triggeredList;
                        DateTime realDetectionTime = DateTime.Now;
                        bool isNG = detectResult == "NG";

                        AddImageToQueue(filePath, isNG, "B", realDetectionTime);

                        if (detectResult.Equals("异常（空图片）", StringComparison.OrdinalIgnoreCase)) continue;

                        bool isResultSaved = (detectResult == "OK" && IsSaveResultOK) || (detectResult == "NG" && IsSaveResultNG);
                        detectionData.ResultImagePath = isResultSaved ? GetResultImagePath(filePath, detectResult, true) : null;
                        detectionData.DetectionResult = detectResult;

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            AddDetectionDataAndEnsureCapacity(detectionData);
                            UpdateDisplayDataList();
                            CalculateDetectionStatistics();
                        });
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[B工位-检测异常] {Path.GetFileName(filePath)}：{ex.Message}", CoreLogLevel.Error);
                    }
                    finally { _semaphoreB.Release(); }
                }
            }
            catch (OperationCanceledException)
            {
                AddLog("B队列线程收到取消请求并退出");
            }
            catch (Exception ex)
            {
                AddLog($"B队列线程处理过程中出现异常：{ex.Message}");
            }
            finally
            {
                _isQueueRunningB = false;
                AddLog("B队列线程已终止");
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

        //private string GetRoiKeyFromImagePath(string imagePath, string station)
        //{
        //    string cameraNo = ExtractCameraNumber(imagePath);  // "1"-"4"
        //    return $"{station}{cameraNo}";  // "A1", "B3" 等
        //}
        //private string ExtractCameraNumber(string imagePath)
        //{
        //    // 从路径提取相机号
        //    // 路径格式: X:\202605\09\260584409\负极\负极1\OK\原图\260584409-1-20260509-000430-530.png
        //    // 需要提取 "负极1" 中的 "1"
        //    if (string.IsNullOrEmpty(imagePath))
        //        return string.Empty;
        //    var pathParts = imagePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        //    // 查找包含"负极"或"正极"的部分
        //    for (int i = pathParts.Length - 1; i >= 0; i--)
        //    {
        //        var part = pathParts[i];
        //        if (part.Contains("负极") )
        //        {
        //            var match = Regex.Match(part, @"\d+$");
        //            if (match.Success)
        //            {
        //                return match.Value;
        //            }
        //            break; 
        //        }
        //    }

        //    return string.Empty;
        //}

        #endregion
        #endregion

        #region 在线检测 - 启动监控
        /// <summary>
        /// 启动在线文件夹监控
        /// </summary>
        //private void StartOnlineMonitoring()
        //{
        //    if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPath) || !Directory.Exists(SelectedMonitorFolderPath))
        //    {
        //        AddLog("[在线检测] 监控文件夹不存在，请先选择有效文件夹");
        //        IsOnlineDetectionEnabled = false;
        //        MessageBox.Show("监控文件夹不存在，请先选择有效文件夹", "在线检测错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //        return;
        //    }

        //    // 初始化监控器
        //    _onlineMonitorWatcher = new FileSystemWatcher(SelectedMonitorFolderPath)
        //    {
        //        Filter = "*.*",
        //        IncludeSubdirectories = true,
        //        NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
        //        EnableRaisingEvents = true
        //    };

        //    // 绑定新文件创建事件
        //    _onlineMonitorWatcher.Created += OnOnlineFileCreated;

        //    AddLog($"[在线检测] 已启动监控，路径：{SelectedMonitorFolderPath}");
        //    AddLog($"[在线检测] 当前筛选条件：极性（正极：{IsPositive}，负极：{IsNegative}），相机（1：{IsCamera1}，2：{IsCamera2}，3：{IsCamera3}，4：{IsCamera4}）");
        //}
        #endregion

        #region 在线检测 - 停止监控

        #endregion

        #region 在线检测 - 新文件处理
       

        //判断文件是否可访问
        private bool IsFileAccessible(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return stream.CanRead;
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        //离线测试时启动并发处理线程
        private void StartConcurrentProcessing()
        {
            // 只要队列有数据，且线程数未达上限，就启动新线程
            while (_safeDetectQueue.Count > 0 && _semaphore.CurrentCount > 0)
            {
                if (!_isQueueRunning)
                {
                    _isQueueRunning = true;
                }
                _ = ProcessQueueConcurrent(); // 启动新的并发处理任务
            }
        }
        #endregion

        #region 离线检测 - 队列处理

        /// <summary>
        /// 并发处理队列
        /// </summary>
        private async Task ProcessQueueConcurrent()
        {
            await _semaphore.WaitAsync();
            try
            {
                // 循环处理队列，直到队列空
                while (_safeDetectQueue.TryDequeue(out string? filePath))
                {
                    if (!File.Exists(filePath))
                    {
                        AddLog($"[队列处理] {Path.GetFileName(filePath)} 已删除，跳过");
                        continue;
                    }

                    try
                    {
                        string station = GetStationFromImagePath(filePath, true); // 只获取 "A" 或 "B"
                        string roiKey = GetRoiKey(filePath, station);

                        dynamic detectionData = new ExpandoObject();
                        ParsePathToDynamicObject(detectionData, filePath);
                        string detectResult = DamageDetection(filePath, roiKey, out List<string> triggeredList);
                        detectionData.TriggeredDefects = triggeredList;

                        if (detectResult.Equals("异常（空图片）", StringComparison.OrdinalIgnoreCase))
                        {
                            AddLog($"[检测结果] {Path.GetFileName(filePath)}：空图片，不统计");
                            continue;
                        }

                        // 结果保存+CSV
                        bool isResultSaved = (detectResult == "OK" && IsSaveResultOK) || (detectResult == "NG" && IsSaveResultNG);
                        detectionData.ResultImagePath = isResultSaved ? GetResultImagePath(filePath, detectResult, true) : null;
                        detectionData.DetectionResult = detectResult;
                        WriteToCsv(filePath, detectResult, true);

                        // 异步UI更新，不阻塞检测
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            AddDetectionDataAndEnsureCapacity(detectionData);
                            UpdateDisplayDataList();
                            CalculateDetectionStatistics();
                        });

                        //AddLog($"[检测完成] {Path.GetFileName(filePath)} → {detectResult}（队列剩余：{_safeDetectQueue.Count}）");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[检测异常] {Path.GetFileName(filePath)}：{ex.Message}\n堆栈：{ex.StackTrace}");
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            AddLog($"[检测错误] {Path.GetFileName(filePath)}：{ex.Message}");
                        });
                    }
                }
            }
            finally
            {
                _semaphore.Release(); // 释放并发许可，允许新任务启动
                if (_safeDetectQueue.Count == 0 && _semaphore.CurrentCount == MaxConcurrent)
                {
                    _isQueueRunning = false;
                    AddLog("[队列处理] 所有任务完成，线程已释放");
                }
            }
        }
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
                    var dllReFile = WebHelper1.downloadFile1(appKey, dllSignature, dllPythonCode, dllExecuteType);
                    if (dllReFile == null || !dllReFile.success || dllReFile.dataStr == null || dllReFile.dataStr.Length == 0)
                    {
                        string errorMsg = dllReFile?.message ?? "下载响应为空";
                        AddLog($"[DLL加载] Web下载失败：{errorMsg}");
                        throw new Exception($"从Web获取DLL失败：{errorMsg}");
                    }
                    AddLog($"[DLL加载] Web下载成功，大小：{dllReFile.dataStr.Length} 字节");

                    // 验证DLL有效性（MZ头）
                    if (dllReFile.dataStr.Length < 2 || dllReFile.dataStr[0] != 0x4D || dllReFile.dataStr[1] != 0x5A)
                    {
                        AddLog("[DLL加载] 下载的不是有效DLL（缺少MZ标识）");
                        throw new Exception("下载的文件不是有效DLL");
                    }

                    // 加载内存DLL
                    targetAssembly = Assembly.Load(dllReFile.dataStr);
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
                //    string modelSignature = RasHelper.RSAEncrypt(RasHelper.ToXmlPublicKey(publicKey), appKey);
                //    if (string.IsNullOrEmpty(modelSignature))
                //    {
                //        AddLog("[模型下载] RSA签名生成失败！");
                //        throw new Exception("RSA签名生成失败，无法下载模型");
                //    }

                //    // 调用WebHelper1下载模型
                //    var modelReFile = WebHelper1.downloadFile1(appKey, modelSignature, modelPythonCode, modelExecuteType);
                //    if (modelReFile == null || !modelReFile.success || modelReFile.dataStr == null || modelReFile.dataStr.Length == 0)
                //    {
                //        string errorMsg = modelReFile?.message ?? "下载响应为空";
                //        AddLog($"[模型下载] Web下载失败：{errorMsg}");
                //        // 弹出程序目录，方便用户手动放置模型
                //        Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);
                //        throw new Exception($"从Web获取模型失败：{errorMsg}\n请手动将 {modelFileName} 放置到 {AppDomain.CurrentDomain.BaseDirectory}");
                //    }

                //    // 验证模型文件有效性（至少10KB，避免空文件）
                //    if (modelReFile.dataStr.Length < 1024 * 10) // 10KB阈值
                //    {
                //        AddLog($"[模型下载] 下载的模型文件过小（{modelReFile.dataStr.Length} 字节），视为无效");
                //        throw new Exception("下载的模型文件无效（体积过小）");
                //    }

                //    // 保存模型到本地目录
                //    File.WriteAllBytes(modelSavePath, modelReFile.dataStr);
                //    AddLog($"[模型下载成功] 已保存到：{modelSavePath}（大小：{modelReFile.dataStr.Length} 字节）");
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

        
        private string DamageDetection(string imagePath, string? station, out List<string> triggeredDefects)
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
                //string stationCamerano = $"{station}";
                //if (!_detectionRegionCache.TryGetValue(stationCamerano, out delRegion))
                //{
                //    delRegion = GenerateDelRegionFromXldFile(stationCamerano);
                //    if (delRegion != null)
                //    {
                //        _detectionRegionCache[stationCamerano] = delRegion;
                //        AddLog($"[检测文件兜底] {station}，从XLD生成DelRegion并缓存");
                //    }
                //    else if (!_warnedNoDelRegionCameras.Contains(stationCamerano))
                //    {
                //        AddLog($"[检测文件兜底] {station}，未使用负极误判区域过滤（无ROI）");
                //        _warnedNoDelRegionCameras.Add(stationCamerano);
                //    }
                //}

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
                    ai.ModelActionDiePian(StationBmodle, imageMat, Gongwei, delRegion, out defectList, out exInfo);
                }
                if (!string.IsNullOrEmpty(exInfo))
                {
                    AddLog($"文件：{imagePath};[AI检测警告]: {exInfo}", CoreLogLevel.Warn, isToDatabase: true);
                    //SaveDetectionResultToPath(imagePath, imageMat, "检测异常", false);
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

                        bool dimensionExceeds = heightExceeds || widthExceeds || areaExceeds;

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
                        // ------------------ 参数可调 ------------------
                        //int startX = 240;                    // 左上角起始 x（调整为左侧）
                        //int startY = 340;                    // 顶部 y
                        //int lineHeight = 150;                // 每行高度
                        ///*int padding = 15; */                  // 内边距
                        //                                        //int bgWidth = 600;                  // 背景宽度（根据文字长度调整）
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
                if (delRegion != null && !_detectionRegionCache.ContainsValue(delRegion))
                {
                    delRegion.Dispose();
                }

                #endregion
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


        private void PutTextChinese(Mat mat, string text, OpenCvSharp.Point pos, float fontSize, Scalar color)
        {
            try
            {
                // 将 Mat 转为 Bitmap
                using (var bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(mat))
                using (var g = Graphics.FromImage(bitmap))
                {
                    // 设置高质量绘图模式，防止锯齿
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    // 将 OpenCV 的 BGR 颜色转换为 System.Drawing 的 RGB 颜色
                    System.Drawing.Color brushColor = System.Drawing.Color.FromArgb(255, (int)color.Val2, (int)color.Val1, (int)color.Val0);

                    // 定义中文字体 (例如微软雅黑)，字体大小需根据图片分辨率调整
                    // 假设 fontSize 1.0 对应 32px 字体
                    float emSize = fontSize * 32;
                    using (Font font = new Font("Microsoft YaHei", emSize, System.Drawing.FontStyle.Bold))
                    using (SolidBrush brush = new SolidBrush(brushColor))
                    {
                        g.DrawString(text, font, brush, new PointF(pos.X, pos.Y));
                    }

                    // 将绘制好的 Bitmap 转回 Mat 并覆盖原图
                    using (var newMat = OpenCvSharp.Extensions.BitmapConverter.ToMat(bitmap))
                    {
                        newMat.CopyTo(mat);
                    }
                }
            }
            catch (Exception ex)
            {
                // 兜底：如果 GDI+ 绘制失败，回退到 OpenCV 英文绘制，避免程序崩溃
                AddLog($"[中文绘制异常] {ex.Message}");
                Cv2.PutText(mat, "Text_Error", pos, HersheyFonts.HersheySimplex, fontSize, color, 2);
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
            // 判断“文件检测”或“在线检测”任一运行，都视为“检测开启”
            bool isAnyDetectionRunning = _isDetecting || IsOnlineDetectionEnabledA;
            if (!isAnyDetectionRunning)
            {
                MessageBox.Show("当前检测未开启", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string detectType = string.Empty;
            if (_isDetecting && IsOnlineDetectionEnabledA)
            {
                detectType = "文件/文件夹检测和在线检测";
            }
            else if (_isDetecting)
            {
                detectType = "文件/文件夹检测";
            }
            else
            {
                detectType = "在线检测";
            }


            var result = MessageBox.Show(
                "是否确定停止检测？当前进度将被中断。",
                "确认停止",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 取消检测任务
                    _cts?.Cancel();
                    _cts?.Dispose();
                    _cts = null;
                    await Task.WhenAll(
                        Task.Run(() => StopOnlineMonitoringA())
                    //Task.Run(() => StopOnlineMonitoringB())
                    );

                    // 同步更新开关状态
                    IsOnlineDetectionEnabledA = false;
                    //IsOnlineDetectionEnabledB = false;
                    _isDetecting = false;
                    IsFileDetecting = false;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 强制更新进度为当前实际完成值
                        double currentProgress = Math.Round(
                            (double)_completedOfflineImageCount / _totalOfflineImageCount * 100, 1);
                        OfflineProgressValue = currentProgress;
                        OfflineProgressTip = $"检测已终止，已完成 {_completedOfflineImageCount}/{_totalOfflineImageCount} 张";
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"停止检测失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
        /// 单张图片检测,异步处理
        /// </summary>
        private async Task GetDataFromSingleFile(string filePath)
        {
            // 设置检测状态
            IsFileDetecting = true;
            _isDetecting = true;
            _isOfflineMode = true;
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
                    string detectResult = DamageDetection(filePath, roiKey, out List<string> triggeredList);
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
                        ? GetResultImagePath(filePath, detectResult, true)
                        : null;
                    detectionData.DetectionResult = detectResult;
                    // 写入CSV
                    WriteToCsv(filePath, detectResult, false);
                    //若为NG结果，加入队列并生成报表
                    if (detectResult.Equals("NG", StringComparison.OrdinalIgnoreCase))
                    {
                        //将NG图片加入队列
                        AddImageToQueue(filePath, true, "离线", realDetectionTime);

                        //AddLog($"[单张检测] NG图片已加入队列：{Path.GetFileName(filePath)}，当前电芯组进度：{_cellCodeQueue.Count}/{MaxImagesPerReport}");
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 先添加到完整集合
                        AddDetectionDataAndEnsureCapacity(detectionData);

                        // 更新显示列表(仅显示NG结果)
                        UpdateDisplayDataList();

                        // 更新统计数据
                        CalculateDetectionStatistics();

                        // 更新进度条为100%
                        OfflineProgressValue = 100;
                        OfflineProgressTip = "检测完成!";
                    });
                    lock (_queue1Lock)
                    {
                        if (_cellCodeQueue.Count > 0) // 确认有数据才生成
                        {
                            AddLog($"[单张检测] 强制生成报表（当前队列数：{_cellCodeQueue.Count}）");
                            GenerateReport().Wait();
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
            _isOfflineMode = true;
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
                if (!IsStationA && !IsStationB)
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
                    foreach (var filePath in filteredFiles)
                    {
                        if (token.IsCancellationRequested)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                double currentProgress = Math.Round(
                                    (double)_completedOfflineImageCount / totalCount * 100, 1);
                                OfflineProgressValue = currentProgress;
                                OfflineProgressTip = $"检测已终止，已完成 {_completedOfflineImageCount}/{totalCount} 张";
                                AddLog("检测已终止，正在清理资源...");
                            });
                            break;
                        }
                        long startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                        if (!File.Exists(filePath))
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() => { });
                            UpdateOfflineProgressAfterSingleImage();
                            continue;
                        }

                        try
                        {
                            var station = GetStationFromImagePath(filePath);
                            string roiKey = GetRoiKey(filePath, station);

                            dynamic detectionData = new ExpandoObject();
                            ParsePathToDynamicObject(detectionData, filePath);
                            if (token.IsCancellationRequested) break;
                            //string detectResult = DamageDetection(filePath, station);
                            string detectResult = DamageDetection(filePath, roiKey, out List<string> triggeredList);
                            detectionData.TriggeredDefects = triggeredList;

                            DateTime realDetectionTime = DateTime.Now;
                            bool isNG = detectResult == "NG";
                            AddImageToQueue(filePath, isNG, "离线", realDetectionTime);

                            if (detectResult.Equals("异常（空图片）", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (detectResult.Equals("异常（空图片）", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            bool isBatchResultSaved = (detectResult == "OK" && IsSaveResultOK) || (detectResult == "NG" && IsSaveResultNG);
                            detectionData.ResultImagePath = isBatchResultSaved
                                ? GetResultImagePath(filePath, detectResult, true)
                                : null;
                            detectionData.DetectionResult = detectResult;
                            WriteToCsv(filePath, detectResult, false);

                            currentBatchCount++;
                            if (currentBatchCount % batchUpdateInterval == 0 || currentBatchCount == totalCount)
                            {
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    AddDetectionDataAndEnsureCapacity(detectionData);
                                    UpdateDisplayDataList();
                                    CalculateDetectionStatistics();
                                });
                            }
                            else
                            {
                                //只添加数据，不刷新UI
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    AddDetectionDataAndEnsureCapacity(detectionData);
                                });
                            }
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
                        CalculateDetectionStatistics();
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
            // 筛选所有NG数据，按时间倒序取最新50条
            var targetNgList = _fullDetectionDataList
                .Where(data => data != null
                            && !string.IsNullOrEmpty(data?.DetectionResult)
                            && data?.DetectionResult.Equals("NG", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(data => data.DetectionTime)
                .Take(50)
                .ToList();

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var toRemove = DetectionDataList
                    .Where(item => !targetNgList.Contains(item))
                    .ToList();

                // 先移除旧数据
                foreach (var item in toRemove)
                {
                    DetectionDataList.Remove(item);
                }
                // 按目标列表顺序添加
                var toAdd = targetNgList
                    .Where(item => !DetectionDataList.Contains(item))
                    .ToList();

                foreach (var item in toAdd)
                {
                    // 插入到对应位置
                    int index = targetNgList.IndexOf(item);
                    if (index < DetectionDataList.Count)
                    {
                        DetectionDataList.Insert(index, item);
                    }
                    else
                    {
                        DetectionDataList.Add(item);
                    }
                }

                while (DetectionDataList.Count > 50)
                {
                    DetectionDataList.RemoveAt(DetectionDataList.Count - 1);
                }
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

        // 清空检测数据和统计
        private void ClearDetectionData()
        {
            DetectionDataList.Clear();
            CurrentResultImagePath = null;
            SelectedDetectionData = null;
            CalculateDetectionStatistics();
        }

        //计算检测统计数据
        private void CalculateDetectionStatistics()
        {
            int newTotalVal = 0, newOKVal = 0, newNGVal = 0;
            double newRateVal = 0;
            Dictionary<string, int> historyCopy;
            Dictionary<string, int> currentCopy;

            lock (_statisticsLock)
            {
                //计算 _fullDetectionDataList 中“新检测数据”的统计值
                var filteredData = _fullDetectionDataList
                    .Where(data =>
                    {
                        // 1. 基础空值检查
                        if (data == null) return false;
                        var dataDict = data as IDictionary<string, object>;
                        if (dataDict == null) return false;

                        // 2. 必须包含检测结果和极性信息
                        if (!dataDict.ContainsKey("DetectionResult")) return false;
                        if (!dataDict.TryGetValue("PartType", out var partTypeObj) || partTypeObj == null) return false;

                        string partType = partTypeObj?.ToString() ?? string.Empty;
                        if (string.IsNullOrEmpty(partType)) return false;

                        // 3. 核心筛选逻辑                     
                        //bool matchStationA = IsStationA && partType == "A";
                        //bool matchStationB = IsStationB && partType == "B";
                        bool matchStationA = IsStationA && dataDict.TryGetValue("Station", out var sa) && sa?.ToString() == "A";
                        bool matchStationB = IsStationB && dataDict.TryGetValue("Station", out var sb) && sb?.ToString() == "B";

                        return matchStationA || matchStationB;
                    }).ToList();

                //统计新数据中的 OK/NG 数量
                int newTotalCount = filteredData.Count;
                int newOKCount = filteredData.Count(data =>
                {
                    var dataDict = (IDictionary<string, object>)data;
                    return dataDict.TryGetValue("DetectionResult", out var resultObj)
                            && resultObj?.ToString() == "OK";
                });
                int newNGCount = newTotalCount - newOKCount;


                // 将计算结果存入局部变量
                newTotalVal = _historyTotalCount + newTotalCount;
                newOKVal = _historyOKCount + newOKCount;
                newNGVal = _historyNGCount + newNGCount;
                newRateVal = newTotalVal > 0 ? Math.Round((double)newOKVal / newTotalVal * 100, 2) : 0;

                //AddLog($"[统计计算] 历史值+新数据值：总={_historyTotalCount}+{newTotalCount}={TotalNeDetectionCount}，OK={_historyOKCount}+{newOKCount}={OKCount}，NG={_historyNGCount}+{newNGCount}={NGCount}");

                // 【核心：计算当前列表中的各类别 NG 数量】
                var currentDefectNGCounts = new Dictionary<string, int>();
                foreach (var data in filteredData)
                {
                    var dataDict = (IDictionary<string, object>)data;
                    if (dataDict.TryGetValue("DetectionResult", out var resObj) && resObj?.ToString() == "NG")
                    {
                        if (dataDict.TryGetValue("TriggeredDefects", out var defectsObj) && defectsObj is List<string> defects)
                        {
                            foreach (var defect in defects)
                            {
                                if (!currentDefectNGCounts.ContainsKey(defect)) currentDefectNGCounts[defect] = 0;
                                currentDefectNGCounts[defect]++;
                            }
                        }
                    }
                }
                currentCopy = new Dictionary<string, int>(currentDefectNGCounts);
                historyCopy = new Dictionary<string, int>(_historyDefectNGCount);

            }
            Action updateAction = () =>
            {
                foreach (var item in DefectDataGrid)
                {
                    if (string.IsNullOrEmpty(item.DefectName)) continue;

                    int hist = historyCopy.ContainsKey(item.DefectName) ? historyCopy[item.DefectName] : 0;
                    int curr = currentCopy.ContainsKey(item.DefectName) ? currentCopy[item.DefectName] : 0;

                    // 同步更新属性
                    item.NGCount = hist + curr;
                    item.YieldRate = newTotalVal > 0
                        ? Math.Round((double)(newTotalVal - item.NGCount) / newTotalVal * 100, 2)
                        : 100.00;
                }

                // 赋值核心统计字段
                TotalNeDetectionCount = newTotalVal;
                OKCount = newOKVal;
                NGCount = newNGVal;

                // 【重点】：在这个时刻，上方的 DefectDataGrid 已经完全更新完毕。
                // 此时再触发 OKRate 的 Set 访问器（进而引发 SaveConfig），抓取到的就是最新的数据。
                OKRate = newRateVal;
            };


            // 安全调度：如果在 UI 线程则直接执行，如果不在则阻塞式 Invoke
            if (Application.Current.Dispatcher.CheckAccess())
            {
                updateAction();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(updateAction);
            }
        }

        //清空列表
        private void ExecuteClearList()
        {
            MessageBoxResult result = MessageBox.Show(
                "确定要清空图片列表吗？此操作不可恢复。",
                "确认清空",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.OK)
            {
                lock (_statisticsLock)
                {
                    // 将当前统计值保存到历史变量
                    _historyTotalCount = TotalNeDetectionCount;
                    _historyOKCount = OKCount;
                    _historyNGCount = NGCount;

                    // 清空列表和数据源
                    foreach (var item in DefectDataGrid)
                    {
                        if (string.IsNullOrEmpty(item.DefectName)) continue;
                        _historyDefectNGCount[item.DefectName] = item.NGCount;
                    }
                    _fullDetectionDataList.Clear();
                    AddLog($"[清空列表] 已保留当前统计值到历史：总={_historyTotalCount}, OK={_historyOKCount}, NG={_historyNGCount}");
                }
                DetectionDataList.Clear();
                _currentCsvFilePath = null;
                SaveConfig(); // 保存配置,防止重启后丢失
            }
            else
            {
                AddLog("[清空列表] 用户取消,操作终止");
            }
        }

        //统计清零
        private void ExecuteClearStatistics()
        {
            MessageBoxResult result = MessageBox.Show(
                "确定要重置统计数据吗？此操作不可恢复。",
                "确认清零",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.OK)
            {
                lock (_statisticsLock)
                {
                    //重置“历史基础值字段”
                    _historyTotalCount = 0;
                    _historyOKCount = 0;
                    _historyNGCount = 0;
                    AddLog($"[统计清零] 已重置历史基础值：历史总检测数={_historyTotalCount}，历史OK数={_historyOKCount}，历史NG数={_historyNGCount}", isToDatabase: true);

                    _fullDetectionDataList.Clear();
                    _historyDefectNGCount.Clear();
                }

                DetectionDataList.Clear();
                TotalNeDetectionCount = 0;
                OKCount = 0;
                NGCount = 0;
                OKRate = 0;
                _currentCsvFilePath = null;

                foreach (var item in DefectDataGrid)
                {
                    item.NGCount = 0;
                    item.YieldRate = 100;
                }
                AddLog($"[统计清零] 已重置当前统计属性：总检测数={TotalNeDetectionCount}，OK数={OKCount}，NG数={NGCount}，OK率={OKRate}%", isToDatabase: true);

                OnPropertyChanged(nameof(TotalNeDetectionCount));
                OnPropertyChanged(nameof(OKCount));
                OnPropertyChanged(nameof(NGCount));
                OnPropertyChanged(nameof(OKRate));
                SaveConfig();
                AddLog($"[统计清零] 已保存清零状态到配置文件，下次启动将从0开始统计");
            }
            else
            {
                AddLog("[统计清零] 用户取消，操作终止");
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
                data.Polarity = filePath.Contains(@"\负极\")?"负极":"正极";
                data.PartType = data.Polarity;
            }
            catch (Exception ex)
            {
                AddLog($"路径解析异常：{ex.Message}，路径：{filePath}");
            }
        }
        // 保存检测结果图像的方法（IME复判目录）
        /*private void SaveToReviewFolder(string originalImagePath, Mat imageMat, string result, bool isResultImage)
        {
            try
            {
                // 1. 调用路径生成方法，获取图像的最终保存路径
                string? savePath = GetResultImagePath(originalImagePath, result, isResultImage);
                // 校验保存路径是否有效，无效则记录日志并退出方法
                if (string.IsNullOrEmpty(savePath))
                {
                    AddLog("[保存失败] 无法生成保存路径");
                    return;
                }
                // 2. 提取保存路径中的目录部分，确保目录存在
                string? directory = Path.GetDirectoryName(savePath);
                // 目录路径非空且不存在时，创建对应的目录（避免保存时因目录不存在报错）
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                // 3. 使用OpenCV的ImWrite方法保存图像到指定路径
                bool saveSuccess = Cv2.ImWrite(savePath, imageMat);
                if (saveSuccess)
                {
                    // Debug.WriteLine($"[保存成功] 图像已保存至: {savePath}");
                }
                else
                {
                    AddLog($"[保存失败] 无法保存图像至: {savePath}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[保存异常] {ex.Message}");
            }
        }*/


        #region 参数管控 
        /// <summary>
        /// 保存到参数管控目录（参数管控目录结构与复盘目录类似，但根目录不同）
        /// </summary>
        /// <param name="originalImagePath"></param>
        /// <param name="imageMat"></param>
        /// <param name="result"></param>
        /// <param name="isResultImage"></param>
        /*private void SaveToParamControlFolder(string originalImagePath, Mat imageMat, string result, bool isResultImage)
        {
            try
            {
                // 获取保存路径
                string? savePath = GetResultImagePath1(originalImagePath, result, isResultImage);
                if (string.IsNullOrEmpty(savePath))
                {
                    AddLog("[保存失败] 无法生成保存路径");
                    return;
                }

                // 创建目录
                string? directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 保存图像
                bool saveSuccess = Cv2.ImWrite(savePath, imageMat);
                if (saveSuccess)
                {
                    // Debug.WriteLine($"[保存成功] 图像已保存至: {savePath}");
                }
                else
                {
                    AddLog($"[保存失败] 无法保存图像至: {savePath}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[保存异常] {ex.Message}");
            }
        }*/


        #endregion

        // 生成保存路径
        /// <summary>
        /// 获取结果图/原图的存储路径
        /// </summary>
        /// <param name="imagePath">原始图片路径</param>
        /// <param name="result">检测结果（OK/NG）</param>
        /// <param name="isResultImage">是否为结果图（true=结果图，false=原图）</param>
        /// <returns>构造后的完整路径，失败返回null</returns>
        private string? GetResultImagePath(string imagePath, string result, bool isResultImage)
        {
            try
            {
                // ===================== 第一步：判断离线/在线模式 =====================
                bool isOffline = _isOfflineMode;
                string lineStatus = "离线";
                // 标准化原始图像路径
                string normalizedImagePath = Path.GetFullPath(imagePath);

                // A工位在线判断：如果原始图像路径属于A工位监控目录，则强制切换为在线模式
                if (!string.IsNullOrEmpty(SelectedMonitorFolderPathA))
                {
                    string normalizedMonitorA = Path.GetFullPath(SelectedMonitorFolderPathA);
                    // 拼接空字符串确保路径以目录分隔符结尾（如"D:\监控A\"），避免前缀匹配错误
                    normalizedMonitorA = Path.Combine(normalizedMonitorA, "");
                    if (normalizedImagePath.StartsWith(normalizedMonitorA, StringComparison.OrdinalIgnoreCase))
                    {
                        isOffline = false;
                        lineStatus = "在线";
                    }
                }
                if (!string.IsNullOrEmpty(SelectedMonitorFolderPathB))
                {
                    string normalizedMonitorB = Path.GetFullPath(SelectedMonitorFolderPathB);
                    normalizedMonitorB = Path.Combine(normalizedMonitorB, "");
                    if (normalizedImagePath.StartsWith(normalizedMonitorB, StringComparison.OrdinalIgnoreCase))
                    {
                        isOffline = false;
                        lineStatus = "在线";
                    }
                }

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
                if(dateFolder != currentDate)
                {
                    dateFolder =  isOffline? $"{currentDate}_{dateFolder}":dateFolder;
                    AddLog($"[日期提取] 从路径提取的日期文件夹 '{dateFolder}' 与当前日期 '{currentDate}' 不匹配，已使用当前日期作为兜底");
                }

                // 从原始路径提取业务属性参数（均做兜底处理，避免空值导致路径拼接失败）
                string stationAB = GetStationFromImagePath(imagePath, !isOffline);
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
            string[] preferredDrives = {  "E:", "F:" };
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
        private void SaveDetectionResultToPath(string originalImagePath, Mat? imageMat,string result, bool isResultImage)
        {
            try
            {
                // 根据目标选择对应的路径生成方法
                string? savePath = GetResultImagePath(originalImagePath, result, isResultImage);

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

                if (isResultImage && imageMat != null)
                {
                    bool saveSuccess = Cv2.ImWrite(savePath, imageMat);
                    if (!saveSuccess)
                    {
                        AddLog($"[保存失败] OpenCV 无法写入图像，路径：{savePath}");
                    }
                }
                else
                {
                    // 【原图逻辑】直接使用文件系统操作，零性能损耗
                    if (File.Exists(originalImagePath))
                    {
                        // 在线模式使用剪切(Move)清理监控目录，离线模式使用复制(Copy)保护测试图库
                        if (_isOfflineMode)
                        {
                            File.Copy(originalImagePath, savePath, overwrite: true);
                        }
                        else
                        {
                            File.Move(originalImagePath, savePath, overwrite: true);
                        }
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
                            new[] {"日期", "工位","班次", "电芯码", "正负极", "相机","图片名称", "检测结果" }
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
                string polarity = GetStationFromImagePath(imagePath,isOnline);
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

                var match = Regex.Match(fileName, @"20\d{8}[-_]?\d{6}");
                if (match.Success)
                {
                    string timeStr = match.Value;

                    // 移除分隔符，统一为 yyyyMMddHHmmss 格式
                    string normalizedTimeStr = Regex.Replace(timeStr, @"[-_]", "");

                    // 索引 0-3是年, 4-5是月, 6-7是日, 8-9是时
                    if (timeStr.Length >= 10)
                    {
                        string hourStr = timeStr.Substring(8, 2);

                        if (int.TryParse(hourStr, out int hour))
                        {
                            // 4. 判定班次
                            // 白班：08:00 - 19:59 (即 hour >= 8 且 hour < 20)
                            // 晚班：20:00 - 07:59 (即 hour >= 20 或 hour < 8)
                            string shift = (hour >= 8 && hour < 20) ? "白班" : "晚班";
                            AddLog($"[班次识别成功] 文件：{fileName}，时间：{normalizedTimeStr}，小时：{hour}，班次：{shift}");
                            return shift;
                        }
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

        ///// <summary>
        ///// 根据当前路径判断所属工位
        ///// </summary>
        ///// <param name="currentPath">需要判断的文件/文件夹路径</param>
        //private string GetStationFromPath(string currentPath)
        //{
        //    if (string.IsNullOrEmpty(currentPath))
        //    {
        //        AddLog("[工位识别警告] 传入路径为空，无法判断工位");
        //        return "未知";
        //    }

        //    try
        //    {
        //        string normalizedPath = currentPath.Replace("：", ":").Replace("/", "\\");
        //        normalizedPath = Path.GetFullPath(normalizedPath);

        //        // 结构：.../虚拟码/工位/极性/文件名.jpg
        //        try
        //        {
        //            FileInfo fileInfo = new FileInfo(normalizedPath);
        //            DirectoryInfo? polarityDir = fileInfo.Directory; // 获取“负极”文件夹

        //            if (polarityDir != null)
        //            {
        //                // 校验上一级是否是极性文件夹（包含"正极"或"负极"），以此定位
        //                if (polarityDir.Name.Contains("极"))
        //                {
        //                    DirectoryInfo? stationDir = polarityDir.Parent; // 获取“大面检测”文件夹
        //                    if (stationDir != null)
        //                    {
        //                        string extractedStation = stationDir.Name;
        //                        return extractedStation; // 直接返回 "大面检测"
        //                    }
        //                }

        //            }
        //        }
        //        catch
        //        {
        //            AddLog($"[工位识别错误] 路径解析失败，采用兜底策略");
        //        }

        //        string normalizedA = string.IsNullOrEmpty(SelectedMonitorFolderPathA) ? "" : Path.GetFullPath(SelectedMonitorFolderPathA);
        //        string normalizedB = string.IsNullOrEmpty(SelectedMonitorFolderPathB) ? "" : Path.GetFullPath(SelectedMonitorFolderPathB);

        //        bool isOnlineA = !string.IsNullOrEmpty(normalizedA) && normalizedPath.StartsWith(normalizedA, StringComparison.OrdinalIgnoreCase);
        //        bool isOnlineB = !string.IsNullOrEmpty(normalizedB) && normalizedPath.StartsWith(normalizedB, StringComparison.OrdinalIgnoreCase);

        //        if (isOnlineA) return "A"; // 或者根据业务需求，将 A 映射为 "大面检测"
        //        if (isOnlineB) return "B";

        //        AddLog($"[工位识别警告] 路径不符合目录结构且未匹配监控文件夹：{currentPath}");
        //        return "未知";
        //    }
        //    catch (Exception ex)
        //    {
        //        AddLog($"[工位识别错误] 路径处理失败：{ex.Message}（路径：{currentPath}）");
        //        return "未知";
        //    }
        //}

        private bool _isThreadRunningA = false;
        private bool _isThreadRunningB = false;
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

                if ( dict.TryGetValue("IsOnlineDetectionEnabledA", out var enableA)
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
                if ( dict.TryGetValue("SelectedMode", out var modeValue))
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
                    TotalNeDetectionCount,
                    OKCount,
                    NGCount,
                    OKRate,
                    SelectedMode = _selectedMode.ToString(),
                    HistoryDefectNGCount = currentTotalDefectNGCount
                };

                string json = System.Text.Json.JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });
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

        private void CheckAndGenerateReport()
        {
            if (_isOfflineMode)
                return;

            // 临时变量存储需要的参数
            int queueCount = 0;
            int runningStations = 0;
            bool needGenerate = false;
            bool needFinalClean = false;

            lock (_queue1Lock)
            {
                queueCount = _cellCodeQueue.Count;
                runningStations = _runningStationsCount;
                needGenerate = queueCount >= MaxImagesPerReport || (runningStations == 0 && queueCount > 0);
                needFinalClean = runningStations == 0;

                // 若需要生成报表，先记录当前队列数据
                if (needGenerate)
                {
                    //AddLog($"[条件触发] 准备生成报表（队列数：{queueCount}，工位数：{runningStations}）");
                }
                else
                {
                    //AddLog($"[队列状态] 当前电芯编码数量：{queueCount}/{MaxImagesPerReport}");
                }

                // 停止定时器
                if (needFinalClean && _reportTimer != null)
                {
                    _reportTimer.Stop();
                    //AddLog("[定时器] 所有工位已停止，定时器已停止");
                }
            }

            //释放锁后，再启动后台线程生成报表
            if (needGenerate)
            {
                _ = Task.Run(() =>
                {
                    // 后台线程单独获取锁，执行生成逻辑
                    lock (_queue1Lock)
                    {
                        GenerateReport().Wait();
                    }
                });
            }

            //释放锁后，执行最终清理
            if (needFinalClean)
            {
                GenerateOnlineFinalReport();
            }
        }

        /// <summary>
        /// 核心方法：进行最终清理工作
        /// </summary>
        private void GenerateOnlineFinalReport()
        {
            if (_isOfflineMode)
                return;

            lock (_queue1Lock)
            {
                bool isStationAEnabled = _isOnlineDetectionEnabledA;
                //bool isStationBEnabled = _isOnlineDetectionEnabledB;
                bool bothStopped = !isStationAEnabled;

                //AddLog($"[最终清理检查] A状态：{isStationAEnabled}, B状态：{isStationBEnabled}, 双停：{bothStopped}");

                // 只有当双工位都停止时才进行最终清理
                if (bothStopped)
                {
                    //AddLog("[最终清理] 双工位全关，进行最终清理");

                    // 释放定时器
                    if (_reportTimer != null)
                    {
                        _reportTimer.Stop();
                        _reportTimer.Dispose();
                        _reportTimer = null;
                        //AddLog("[定时器] 双工位全关，定时器已释放");
                    }

                    // 重置检测完成标志和运行工位计数
                    _isOfflineDetectionCompleted = false;
                    _runningStationsCount = 0; // 重置计数器
                    //AddLog("[状态重置] 检测完成标志和运行工位计数已重置");
                }
            }
        }

        /// <summary>
        /// 离线检测全部完成后，主动生成剩余报表
        /// </summary>
        private async Task GenerateOfflineFinalReport()
        {
            if (!_isOfflineMode || _isOfflineDetectionCompleted)
                return;

            // 停止超时定时器
            _reportTimer?.Stop();

            //若队列中有剩余电芯编码，逐个生成报表直到队列为空
            while (_cellCodeQueue.Count > 0)
            {
                await GenerateReport();
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

            //先取消订阅
            //OnNgImageDetected -= AddNgImageToQueue;
            //OnNgImageDetected += AddNgImageToQueue;

            _isOfflineDetectionCompleted = false;

            // 标记初始化完成
            _isReportFunctionInited = true;
            AddLog("[报表功能] 初始化完成");
        }

        /// <summary>
        /// 添加NG图片到队列
        /// </summary>
        private void AddNgImageToQueue(string imagePath, string station, DateTime detectionTime)
        {
            AddImageToQueue(imagePath, true, station, detectionTime);
        }
        /// <summary>
        /// 添加图片到队列
        /// </summary>
        private void AddImageToQueue(string imagePath, bool isNG, string station, DateTime detectionTime)
        {
            lock (_queue1Lock)
            {
                // 仅当"离线检测已完成且未重置"时才拒绝添加
                if (_isOfflineMode && _isOfflineDetectionCompleted)
                {
                    AddLog($"[离线检测已完成] 拒绝添加新图片：{imagePath}（请先重置检测状态）");
                    return;
                }

                var cellCode = GetCellNameFromPath(imagePath);

                // 检查是否是新电芯编码
                bool isNewCellCode = _cellCodeSet.Add(cellCode);

                //所有新电芯编码都添加到队列，无论检测结果
                if (isNewCellCode)
                {
                    _cellCodeQueue.Enqueue(cellCode);

                    // 重置定时器
                    if (!_isOfflineDetectionCompleted && _reportTimer != null)
                    {
                        _reportTimer.Stop();
                        _reportTimer.Start();
                        //AddLog($"[定时器重置] 超时时间重置为 {ReportTimeoutMinutes} 分钟");
                    }

                    // 检查是否达到电芯编码数量阈值
                    if (_cellCodeQueue.Count >= MaxImagesPerReport)
                    {
                        AddLog($"[条件触发] 电芯编码数量达到阈值 {MaxImagesPerReport}，处理当前电芯组");
                        //GenerateReport().Wait();
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await GenerateReport();
                            }
                            catch (Exception ex)
                            {
                                AddLog($"[报表生成异常] {ex.Message}", CoreLogLevel.Error);
                            }
                        });

                        _reportTimer?.Stop(); // 重新开始计时
                        if (_reportTimer != null && _cellCodeQueue.Count > 0)
                        {
                            _reportTimer.Start();
                            //AddLog($"[定时器重启] 重新开始 {ReportTimeoutMinutes} 分钟计时");
                        }
                    }
                }

                // 如果是NG图片，添加到NG字典中
                if (isNG)
                {
                    var ngInfo = new NgImageInfo
                    {
                        ImagePath = imagePath,
                        Shift = GetShiftFromPath(imagePath),
                        Polarity = "负极",
                        CellCode = cellCode,
                        ImageName = Path.GetFileName(imagePath),
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
                AddLog($"[超时触发] 达到 {ReportTimeoutMinutes} 分钟超时时间，开始异步处理");

                // 使用 Task.Run 触发，不阻塞定时器线程
                Task.Run(async () =>
                {
                    try
                    {
                        await GenerateReport();

                        // 报表生成完后，如果需要，在此处重启定时器（需注意线程安全）
                        // 建议在 GenerateReport 内部或通过回调处理定时器重置
                        lock (_queue1Lock)
                        {
                            if (_reportTimer != null && !_isOfflineMode)
                            {
                                _reportTimer.Stop();
                                _reportTimer.Start();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[超时任务异常] {ex.Message}", CoreLogLevel.Error);
                    }
                });
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
        private async Task GenerateReport()
        {
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
                string modeSubDir = _isOfflineMode ? "离线检测报表" : "在线检测报表";

                // 确定日期文件夹：离线模式使用第一张图片的日期，在线模式使用当前日期
                string dateFolderName = DateTime.Now.ToString("yyyy-MM-dd");
                if (_isOfflineMode && imagesToReport.Count > 0 && !string.IsNullOrEmpty(imagesToReport[0].ImagePath))
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
                string baseFileName = $"PTO与行大面缺陷检测报告_{reportNo}.pdf";
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
                string productionDate = _isOfflineMode && imagesToReport.Count > 0
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
                                    row.RelativeItem().AlignCenter().Text("PTO与行大面缺陷检测报告").Style(ReportStyle.Title);
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

                                AddRow("检测设备：", "与行——大面检测工位");
                                AddRow("生产日期：", productionDate);
                                AddRow("生成时间：", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                AddRow("检测模式：", _isOfflineMode ? "离线检测" : "在线检测");
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
                                    string? resultImagePath = GetResultImagePath(imgInfo.ImagePath!, "NG", true);

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
                    string json = File.ReadAllText(DefectFilePath);
                    var list = JsonSerializer.Deserialize<List<DefectParameter>>(json);

                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            // 重要：重新触发属性变更，确保 UI 能正确绑定
                            item.DefectName = item.DefectName;   // 触发 PropertyChanged
                            DefectDataGrid.Add(item);
                        }
                        AddLog($"从 {DefectFilePath} 成功加载 {DefectDataGrid.Count} 条缺陷参数");
                        CalculateDetectionStatistics();
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

        /// <summary>
        /// 资源清理（防止内存泄漏）
        /// </summary>
        public void Dispose()
        {
            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
        }





    }
}
