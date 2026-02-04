using HalconDotNet;
using HandyControl.Controls;
using HelixToolkit.Wpf;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Properties;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using OpenCvSharp;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
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
using Yolo8Core;
using ZZYingYanAICore;
using static Yolo8Core.BaseClass;
using MessageBox = HandyControl.Controls.MessageBox;

namespace SUNWODA_SEVB.ViewModels.Pages.PTO
{
    [Module("VM_PTODamageDetectionPage", "PTO叠片 AI检测系统v1.0")]
    public class VM_PTODamageDetectionPage : ViewModelBase
    {

        private IPredictor? H246Zhengmodle;
        private IPredictor? H246Fumodle;

        // 文件选择命令
        public ICommand File_Button { get; private set; }

        // 文件夹选择命令
        public ICommand Folder_Button { get; private set; }

        //查看结果命令
        public ICommand ViewResultCommand { get; private set; }

        //跳转存储命令
        public ICommand JumpToStorageCommand { get; private set; }

        // 清空列表命令
        public ICommand ClearListCommand { get; private set; }
        // 统计清零命令
        public ICommand ClearStatisticsCommand { get; private set; }

        // 鼠标滚轮缩放
        public ICommand ZoomRealTimeImageCommand { get; }
        // 放大按钮
        public ICommand ZoomInRealTimeCommand { get; }
        // 缩小按钮
        public ICommand ZoomOutRealTimeCommand { get; }
        // 重置缩放
        public ICommand ResetRealTimeImageZoomCommand { get; }
        //添加停止检测命令
        public ICommand StopDetectionCommand { get; private set; }
        //选择绘制图片
        public ICommand SelectNegativeRoiImageCommand { get; }
        //删除绘制区域
        public ICommand DeleteNegativeRoiCommand { get; }
        //保存绘制区域
        public ICommand SaveNegativeRoiCommand { get; }
        //查看绘制区域
        public ICommand ViewNegativeRoiCommand { get; }
        // 选择正极绘制图片
        public ICommand SelectPositiveRoiImageCommand { get; }
        // 删除正极绘制区域
        public ICommand DeletePositiveRoiCommand { get; }
        // 保存正极绘制区域
        public ICommand SavePositiveRoiCommand { get; }
        // 查看正极绘制区域
        public ICommand ViewPositiveRoiCommand { get; }         

        //取消绘制
        public ICommand CancelDrawRoiCommand { get; }
        // 鼠标按下命令（开始拖动）
        public ICommand ImageMouseDownCommand { get; }
        // 鼠标移动命令
        public ICommand ImageMouseMoveCommand { get; }
        // 鼠标松开命令
        public ICommand ImageMouseUpCommand { get; }

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
        // 缓存：路径 → 相机编号
        private readonly ConcurrentDictionary<string, string> _cameraNumberCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // 缓存：图片路径 → 相机信息
        private readonly ConcurrentDictionary<string, string> _cameraInfoCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
        private readonly ILoggerService<VM_PTODamageDetectionPage> _logger;
        private readonly IServiceProvider _serviceProvider;





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

        #region 命令
        public ICommand SaveUserIdCommand { get; }
        public ICommand DeleteUserIdCommand { get; }
        #endregion
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

        // 增减命令
        public ICommand IncrementMaxImagesCommand { get; }
        public ICommand DecrementMaxImagesCommand { get; }
        public ICommand IncrementTimeoutCommand { get; }
        public ICommand DecrementTimeoutCommand { get; }
        public ICommand SaveMaxImagesCommand { get; }
        public ICommand SaveTimeoutCommand { get; }

        #region 双工位
        // A工位监控器
        private FileSystemWatcher? _onlineMonitorWatcherA;
        private readonly ConcurrentQueue<string> _safeDetectQueueA = new ConcurrentQueue<string>();
        private bool _isQueueRunningA=false;
        private readonly SemaphoreSlim _semaphoreA; // A工位并发控制
        private const int MaxConcurrentA = 3; // A工位最大并发数
        private readonly AutoResetEvent _queueEventA = new AutoResetEvent(false); // 初始为未触发状态.
        private readonly object _queueLockA = new object();

        // B工位监控器
        private FileSystemWatcher? _onlineMonitorWatcherB;
        private readonly ConcurrentQueue<string> _safeDetectQueueB = new ConcurrentQueue<string>();
        private bool _isQueueRunningB=false;
        private readonly SemaphoreSlim _semaphoreB; // B工位并发控制
        private const int MaxConcurrentB = 3; // B工位最大并发数
        private readonly AutoResetEvent _queueEventB = new AutoResetEvent(false);
        private readonly object _queueLockB = new object();
        private bool _isThreadTerminatingB = false;
        private bool _isThreadTerminatingA = false;

        // A工位监听文件路径目录
        private string? _selectedMonitorFolderPathA;
        public string? SelectedMonitorFolderPathA
        {
            get => _selectedMonitorFolderPathA;
            set
            {
                _selectedMonitorFolderPathA = value;
                OnPropertyChanged();
                SaveConfig();
            }
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
                SaveConfig();
            }
        }
        // 全局锁对象
        private bool _isInitializing = false;
        private readonly object _stationCountLock = new object();
        private int _runningStationsCount = 0;

        //A工位在线检测开关
        private bool _isOnlineDetectionEnabledA;
        public bool IsOnlineDetectionEnabledA
        {
            get => _isOnlineDetectionEnabledA;
            set
            {
                // 1. 初始化报表功能（前置操作：确保报表相关逻辑已就绪）
                InitReportFunction();
                // 2. 重复值校验：若新值与旧值一致，直接返回，避免重复执行逻辑（性能优化+防止重复启停）
                if (_isOnlineDetectionEnabledA == value) return;
                // 3. 隐藏离线检测进度条：在线检测开启时，离线进度条无需显示
                IsOfflineProgressVisible = Visibility.Collapsed;
                // 4. 在线检测开启逻辑（value=true：用户勾选开关）
                if (value)
                {
                    // 4.1 校验监控文件夹有效性：必须选择且存在有效文件夹，否则禁止开启
                    if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathA) || !Directory.Exists(SelectedMonitorFolderPathA))
                    {
                        // 提示用户补全配置，重置状态并通知UI（避免属性值不一致）
                        MessageBox.Show("请先选择有效的在线监控文件夹！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _isOnlineDetectionEnabledA = false;
                        OnPropertyChanged();// 通知UI刷新为未勾选状态
                        return;
                    }
                    // 4.2 启动A工位在线监控（核心业务逻辑：如启动文件夹监控、相机采集等）
                    StartOnlineMonitoringA();
                    // 4.3 标记当前为非离线模式（区分在线/离线检测）
                    _isOfflineMode = false;
                    // 4.4 线程安全更新运行工位数量（lock保证多线程下计数准确）
                    lock (_stationCountLock)
                    {
                        _runningStationsCount++;
                    }
                    AddLog("[A工位-在线检测] 开关已开启，文件选择功能已禁用");
                }
                // 5. 在线检测关闭逻辑（value=false：用户取消勾选开关）
                else
                {
                    // 5.1 停止A工位在线监控（核心业务逻辑：如停止文件夹监控、释放资源等）
                    StopOnlineMonitoringA();
                    AddLog("[A工位] 已停止监控文件夹");
                }
                // 6. 更新私有字段（最终确认状态变更）
                _isOnlineDetectionEnabledA = value;
                // 7. 触发属性变更通知：保证UI同步更新
                OnPropertyChanged();// 通知自身属性变更（IsOnlineDetectionEnabledA）
                OnPropertyChanged(nameof(IsInAnyDetectionA));// 通知A工位是否在检测中
                OnPropertyChanged(nameof(IsInAnyDetection));// 相机检测
                OnPropertyChanged(nameof(IsFileSelectEnabled));// 通知文件选择控件是否可用
                OnPropertyChanged(nameof(IsFolderSelectEnabled));// 通知文件夹选择控件是否可用

                SaveConfig();
            }
        }

        //B工位在线检测开关
        private bool _isOnlineDetectionEnabledB;
        public bool IsOnlineDetectionEnabledB
        {
            get => _isOnlineDetectionEnabledB;
            set
            {
                InitReportFunction();

                // ⭐ 初始化阶段不触发任何逻辑（关键）
                if (_isInitializing)
                {
                    _isOnlineDetectionEnabledB = value;
                    OnPropertyChanged();
                    return;
                }

                if (_isOnlineDetectionEnabledB == value) return;
                IsOfflineProgressVisible = Visibility.Collapsed;

                // 在线检测开启逻辑
                if (value)
                {
                    if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathB) || !Directory.Exists(SelectedMonitorFolderPathB))
                    {
                        MessageBox.Show("请先选择有效的在线监控文件夹！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _isOnlineDetectionEnabledB = false;
                        OnPropertyChanged();
                        return;
                    }

                    StartOnlineMonitoringB();
                    _isOfflineMode = false;
                    lock (_stationCountLock)
                    {
                        _runningStationsCount++;
                    }
                    AddLog("[B工位-在线检测] 开关已开启，文件选择功能已禁用");
                }
                else
                {
                    StopOnlineMonitoringB();
                    AddLog("[B工位] 已停止监控文件夹");
                }

                _isOnlineDetectionEnabledB = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInAnyDetectionB));
                OnPropertyChanged(nameof(IsInAnyDetection));
                OnPropertyChanged(nameof(IsFileSelectEnabled));
                OnPropertyChanged(nameof(IsFolderSelectEnabled));

                SaveConfig();
            }
        }

        // A工位监听路径选择命令
        public ICommand SelectMonitorFolderCommandA { get; }

        //B工位监听路径选择命令
        public ICommand SelectMonitorFolderCommandB { get; }
        #endregion

        #region 在线检测相关变量
        // 文件夹监控器
        private FileSystemWatcher? _onlineMonitorWatcher;
        // 在线检测开关状
        private bool _isOnlineDetectionEnabled;
        // 选中的监控文件夹路径
        private string _selectedMonitorFolderPath = "";
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

        // 相机1-4的ROI状态
        private bool _hasRoiPositive;
        public bool HasRoiPositive
        {
            get => _hasRoiPositive;
            set { _hasRoiPositive = value; OnPropertyChanged(); }
        }

        private bool _hasNegativeRoiA2;
        public bool HasNegativeRoiA2
        {
            get => _hasNegativeRoiA2;
            set { _hasNegativeRoiA2 = value; OnPropertyChanged(); }
        }

        private bool _hasNegativeRoiA3;
        public bool HasNegativeRoiA3
        {
            get => _hasNegativeRoiA3;
            set { _hasNegativeRoiA3 = value; OnPropertyChanged(); }
        }

        private bool _hasNegativeRoiA4;
        public bool HasNegativeRoiA4
        {
            get => _hasNegativeRoiA4;
            set { _hasNegativeRoiA4 = value; OnPropertyChanged(); }
        }

        // B工位相机状态
        private bool _hasRoiNegative;
        public bool HasRoiNegative
        {
            get => _hasRoiNegative;
            set { _hasRoiNegative = value; OnPropertyChanged(); }
        }

        private bool _hasNegativeRoiB2;
        public bool HasNegativeRoiB2
        {
            get => _hasNegativeRoiB2;
            set { _hasNegativeRoiB2 = value; OnPropertyChanged(); }
        }

        private bool _hasNegativeRoiB3;
        public bool HasNegativeRoiB3
        {
            get => _hasNegativeRoiB3;
            set { _hasNegativeRoiB3 = value; OnPropertyChanged(); }
        }

        private bool _hasNegativeRoiB4;
        public bool HasNegativeRoiB4
        {
            get => _hasNegativeRoiB4;
            set { _hasNegativeRoiB4 = value; OnPropertyChanged(); }
        }

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


        public bool IsInAnyDetectionB
        {
            get => !(IsFileDetecting || IsOnlineDetectionEnabledB);
        }

        //通过反向判断 IsFileDetecting 和 IsOnlineDetectionEnabledA 两个状态的组合结果来确定值，核心作用是精准控制 A 工位 “浏览...” 按钮的可用状态
        public bool IsInAnyDetectionA
        {
            get => !(IsFileDetecting || IsOnlineDetectionEnabledA);
        }

        /// <summary>
        /// 复合属性：是否处于任何检测状态
        /// </summary>
        public bool IsInAnyDetection
        {
            get => !(IsFileDetecting || IsOnlineDetectionEnabledA);
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

        //在线检测开关B是否可用
        private bool _isOnlineToggleEnabledB = true;
        public bool IsOnlineToggleEnabledB
        {
            get => _isOnlineToggleEnabledB;
            set
            {
                _isOnlineToggleEnabledB = value;
                OnPropertyChanged();
            }
        }

        //文件选择命令
        private bool _isFileSelectEnabled = true;
        public bool IsFileSelectEnabled
        {
            get => _isFileSelectEnabled
               && !IsFileDetecting
               && !IsOnlineDetectionEnabledA
               && !IsOnlineDetectionEnabledB;
            set
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
            get => _isFolderSelectEnabled
               && !IsFileDetecting
               && !IsOnlineDetectionEnabledA
               && !IsOnlineDetectionEnabledB;
            set
            {
                _isFolderSelectEnabled = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // 文件/文件夹检测是否正在运行
        private bool _isFileDetecting;
        public bool IsFileDetecting
        {
            get => _isFileDetecting;
            set
            {
                // 第一步：更新私有字段（基础赋值）
                _isFileDetecting = value;
                // 第二步：同步更新多个UI控制属性（状态联动）
                // 文件检测中 → A工位在线检测开关禁用；检测停止 → 启用
                IsOnlineToggleEnabledA = !value;
                // 文件检测中 → B工位在线检测开关禁用；检测停止 → 启用
                IsOnlineToggleEnabledB = !value;
                // 文件检测中 → 文件选择功能禁用；检测停止 → 启用
                IsFileSelectEnabled = !value;
                // 文件检测中 → 文件夹选择功能禁用；检测停止 → 启用
                IsFolderSelectEnabled = !value;
                // 第三步：触发属性变更通知（保证UI实时更新）
                OnPropertyChanged();// 通知自身（IsFileDetecting）变更
                OnPropertyChanged(nameof(IsInAnyDetection));// 通知全局检测状态变更
                OnPropertyChanged(nameof(IsInAnyDetectionA));// 通知A工位检测状态变更
                OnPropertyChanged(nameof(IsInAnyDetectionB));// 通知B工位检测状态变更
            }
        }

        #region 在线检测相关属性
        /// <summary>
        /// 在线检测开关
        /// </summary>
        public bool IsOnlineDetectionEnabled
        {
            get => _isOnlineDetectionEnabled;
            set
            {
                if (_isOnlineDetectionEnabled == value) return;

                // 在线检测开启逻辑
                if (value)
                {
                    if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPath) || !Directory.Exists(SelectedMonitorFolderPath))
                    {
                        MessageBox.Show("请先选择有效的在线监控文件夹！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _isOnlineDetectionEnabled = false;
                        OnPropertyChanged();
                        return;
                    }

                    StartOnlineMonitoring();
                    IsFileSelectEnabled = false;
                    IsFolderSelectEnabled = false;
                    AddLog("[在线检测] 开关已开启，文件选择功能已禁用");
                }
                //在线检测关闭逻辑
                else
                {
                    StopOnlineMonitoring();
                    IsFileSelectEnabled = true;
                    IsFolderSelectEnabled = true;
                    AddLog("[在线检测] 开关已关闭，文件选择功能已恢复");
                }

                _isOnlineDetectionEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInAnyDetection));
            }
        }

        /// <summary>
        /// 选中的监控文件夹路径
        /// </summary>
        public string SelectedMonitorFolderPath
        {
            get => _selectedMonitorFolderPath;
            set
            {
                _selectedMonitorFolderPath = value;
                OnPropertyChanged();
                if (_isOnlineDetectionEnabled && _onlineMonitorWatcher != null)
                {
                    StopOnlineMonitoring();
                    StartOnlineMonitoring();
                }
            }
        }
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
        //图像检索勾选正极
        private bool _isPositive = false;
        public bool IsPositive
        {
            get => _isPositive;
            set
            {
                _isPositive = value;
                OnPropertyChanged();
                SaveConfig();

                // 勾选正极时，自动勾选所有未勾选的相机
                //if (value)
                //{
                //    if (!IsCamera1) IsCamera1 = true;
                //    if (!IsCamera2) IsCamera2 = true;
                //    if (!IsCamera3) IsCamera3 = true;
                //    if (!IsCamera4) IsCamera4 = true;
                //}
                CalculateDetectionStatistics();
            }
        }

        private bool _isNegative = false;

        /// <summary>负极是否勾选</summary>
        public bool IsNegative
        {
            get => _isNegative;
            set
            {
                _isNegative = value;
                OnPropertyChanged();
                SaveConfig();

                // 勾选负极时，自动勾选所有未勾选的相机
                //if (value)
                //{
                //    if (!IsCamera1) IsCamera1 = true;
                //    if (!IsCamera2) IsCamera2 = true;
                //    if (!IsCamera3) IsCamera3 = true;
                //    if (!IsCamera4) IsCamera4 = true;
                //}
                CalculateDetectionStatistics();
            }
        }

        private bool _isCamera1;
        /// <summary>相机1是否勾选</summary>
        public bool IsCamera1
        {
            get => _isCamera1;
            set { _isCamera1 = value; OnPropertyChanged(); SaveConfig(); }
        }

        private bool _isCamera2;
        /// <summary>相机2是否勾选</summary>
        public bool IsCamera2
        {
            get => _isCamera2;
            set { _isCamera2 = value; OnPropertyChanged(); SaveConfig(); }
        }

        private bool _isCamera3;
        /// <summary>相机3是否勾选</summary>
        public bool IsCamera3
        {
            get => _isCamera3;
            set { _isCamera3 = value; OnPropertyChanged(); SaveConfig(); }
        }

        private bool _isCamera4;
        /// <summary>相机4是否勾选</summary>
        public bool IsCamera4
        {
            get => _isCamera4;
            set { _isCamera4 = value; OnPropertyChanged(); SaveConfig(); }
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
        private readonly object _statisticsLock = new object(); //统计锁

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
                SaveConfig();
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
                SaveConfig();
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
                SaveConfig();
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
        /// 文件夹选择
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
                        $"所选文件夹中未找到符合当前筛选条件（工位：{(IsPositive ? "正极 " : "")}{(IsNegative ? "负极 " : "")}；的图片！\n" +
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
                   $"筛选条件：工位：{(IsPositive ? "正极 " : "")}{(IsNegative ? "负极 " : "")}\n" +
                   saveInfo,
                   "确认批量检测", MessageBoxButton.OKCancel, MessageBoxImage.Question);

                if (confirm == MessageBoxResult.OK)
                {
                    _currentCsvFilePath = null;
                    await GetDataFromFolder( filteredFiles);
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
        }

        /// <summary>
        /// 并行筛选+缓存，大幅提升大量文件处理速度
        /// </summary>
        private List<string> FilterImageFiles(List<string> allImageFiles)
        {
            // 清空旧缓存
            _cameraInfoCache.Clear();
            _cameraNumberCache.Clear();

            var filtered = new ConcurrentBag<string>();

            // 并行遍历
            Parallel.ForEach(allImageFiles, filePath =>
            {
                string polarity = GetPolarityFromPath(filePath);
                //string camera = GetCameraNumberFromPath(filePath);

                bool isPolarityMatch = (!IsPositive && !IsNegative) ||
                                       (IsPositive && polarity == "正极") ||
                                       (IsNegative && polarity == "负极");

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
            AddLog("系统启动，日志功能就绪"); // 初始日志
            string aabbb = string.Empty;
            string[] labels = new string[0];
            #region 本地加载
            //取消下面注释
            ai.ModelInit(@"C:\Windows\正极.onnx", labels, "PTO与行大面正极", out H246Zhengmodle, out string errorInfo1);
            ai.ModelInit(@"C:\Windows\负极.onnx", labels, "PTO与行大面负极", out H246Fumodle, out string errorInfo2);
            if (!string.IsNullOrEmpty(errorInfo1) || !string.IsNullOrEmpty(errorInfo2))
            {
                AddLog($"模型初始化异常：{errorInfo1}、{errorInfo2}");
            }
            #endregion
            ResetOfflineProgress();
        }

        public VM_PTODamageDetectionPage(ILoggerService<VM_PTODamageDetectionPage> logger, IWebApiClient webApiClient, IServiceProvider serviceProvider)
        {
            _logger = logger;
         

            File_Button = new RelayCommand<object>(
                        execute: parameter => SelectImageFile(),//文件选择
                        canExecute: parameter => IsFileSelectEnabled && !IsFileDetecting
             );
            Folder_Button = new RelayCommand<object>(
                        execute: parameter => SelectImageFolder(),//文件夹选择
                        canExecute: parameter => IsFolderSelectEnabled && !IsFileDetecting
            );
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
            _semaphoreA = new SemaphoreSlim(MaxConcurrentA, MaxConcurrentA);
            _semaphoreB = new SemaphoreSlim(MaxConcurrentB, MaxConcurrentB);
            SelectMonitorFolderCommandA = new RelayCommand(SelectMonitorFolderA);//A工位监听路径
            SelectMonitorFolderCommandB = new RelayCommand(SelectMonitorFolderB);//B工位监听路径
            SelectNegativeRoiImageCommand = new RelayCommand<string>(SelectNegativeRoiImage);//选择绘制图片
            DeleteNegativeRoiCommand = new RelayCommand(DeleteNegativeRoi);//弹窗-删除当前绘制内容
            SaveNegativeRoiCommand = new RelayCommand(SaveNegativeRoiAsHalconRegion);//弹窗-保存ROI
            ViewNegativeRoiCommand = new RelayCommand<string>(ViewNegativeRoi);//查看绘制结果
            CancelDrawRoiCommand = new RelayCommand(CancelDrawRoi);//弹窗-取消ROI绘制

            // 正极ROI命令初始化
            SelectPositiveRoiImageCommand = new RelayCommand<string>(SelectPositiveRoiImage);
            DeletePositiveRoiCommand = new RelayCommand(DeletePositiveRoi);
            SavePositiveRoiCommand = new RelayCommand(SavePositiveRoiAsHalconRegion);  // 复用保存逻辑
            ViewPositiveRoiCommand = new RelayCommand<string>(ViewPositiveRoi);

            LoadAllHalconRoiConfigs();
            InitReportFunction();
            _isReportFunctionInited = false;
            SaveUserIdCommand = new RelayCommand(SaveUserId);
            DeleteUserIdCommand = new RelayCommand(DeleteUserId);

            _webApiClient = webApiClient ?? throw new ArgumentNullException(nameof(webApiClient));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            // 加载已保存的ID列表
            InitParameters();
            LoadConfig();
            LoadUserIds();


            #region 参数管控  
            UserStateChangedEvent.OnUserStateChanged += OnUserStateChanged;
            CheckAuthority();
            LoadDefectInitialData();
            AddRow = new RelayCommand<string?>(OnAddRow);
            DeleteRow = new RelayCommand<DefectParameter?>(OnDeleteRow);
            SaveData = new RelayCommand<string?>(OnSaveData);
            #endregion
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

                var settingValue = projectSettingRepo.GetSettingValue("UserIDs", "VM_PTODamageDetection") as string;
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

                projectSettingRepo.UpdateSettingValue("UserIDs", "VM_PTODamageDetection",string.Join(",", users));
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
            if (string.IsNullOrEmpty(param))
            {
                AddLog("ROI绘制无效参数",CoreLogLevel.Warn);
                return;
            }
            // 解析参数：第一位为工位（A/B），剩余部分为相机号（1-4）
            string station = param;

            // 校验工位和相机号合法性
            // 2. 第二层合法性校验：工位仅支持A/B，相机号仅支持1-4
            if (!new[] { "正极", "负极" }.Contains(station) )
            {
                AddLog($"[错误] 工位{station}无效",CoreLogLevel.Warn);
                return;
            }

            //更新当前相机号
            // 3. 更新全局状态：记录当前操作的相机号，更新绘制标题（通知UI刷新）
            _currentRoiCameraNo = param;// 保存当前ROI操作的工位+相机号
            DrawTitle = $"绘制误判区域 - {station}工位";// 绘制弹窗标题，适配当前操作
            OnPropertyChanged(nameof(DrawTitle));// 触发INotifyPropertyChanged，更新UI上的标题显示

            //if (int.TryParse(cameraNo, out int num))
            //{
            //    CurrentCameraNumber = num;
            //}

            var openFileDialog = new OpenFileDialog
            {
                Filter = "图片文件|*.bmp;*.jpg;*.jpeg;*.png",
                Title = $"选择{station}工位的参考图片"
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
            string station = param;
            //string cameraNo = param.Substring(1);

            // 历史ROI文件路径
            var roiDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig");
            string hobjPath = Path.Combine(roiDir, $"{station}_ROI.hobj"); // 如Negative_A1_ROI.hobj
            string sizePath = Path.Combine(roiDir, $"{station}_Size.txt");

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
                var sizePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ROIConfig", $"Negative_Camera{_currentRoiCameraNo}_Size.txt");
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
                //UpdateRoiStatus(station, cameraNo, true);

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
                //case "A":
                //    switch (cameraNo)
                //    {
                //        case "1": HasNegativeRoiA1 = isEnabled; break;
                //        case "2": HasNegativeRoiA2 = isEnabled; break;
                //        case "3": HasNegativeRoiA3 = isEnabled; break;
                //        case "4": HasNegativeRoiA4 = isEnabled; break;
                //    }
                //    break;
                //case "B":
                //    switch (cameraNo)
                //    {
                //        case "1": HasNegativeRoiB1 = isEnabled; break;
                //        case "2": HasNegativeRoiB2 = isEnabled; break;
                //        case "3": HasNegativeRoiB3 = isEnabled; break;
                //        case "4": HasNegativeRoiB4 = isEnabled; break;
                //    }
                //    break;
                case"正极":
                    HasRoiPositive = isEnabled;
                    break;
                case "负极":
                    HasRoiNegative = isEnabled;
                    break;
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
            string hobjPath = Path.Combine(roiDir, $"Negative_{station}{cameraNo}_ROI.hobj");
            string sizePath = Path.Combine(roiDir, $"Negative_{station}{cameraNo}_Size.txt");

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
                        var parts = fileName.Split(new[] {  "_ROI" }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 1) continue;
                        string stationCamera = parts[0];
                        if (stationCamera.Length < 2) continue;

                        string station = stationCamera;
                        //string cameraNo = stationCamera.Substring(1);
                        UpdateRoiStatus(station,  true);

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
            var folderDialog = new OpenFolderDialog();
            folderDialog.Title = "选择B工位在线检测监控文件夹";
            folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            folderDialog.ValidateNames = true;
            folderDialog.Multiselect = false;
            // 调整返回值判断
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
        private bool _isBMonitorRunning = false;

        // A工位启动监控
        private void StartOnlineMonitoringA()
        {
            // 第一层防护：线程运行状态校验，防止重复启动（快速返回，避免进入锁逻辑）
            if (_isThreadRunningA)
                return; // 若线程已在运行，直接返回，避免重复执行后续逻辑
            // 标记线程开始运行（第一层状态锁）
            _isThreadRunningA = true;
            // 加锁保护：保证多线程下A工位监控状态的原子操作（防止并发启动导致的资源冲突）
            lock (_stationCountLock)
            {
                // 第二层防护：A工位监控运行状态校验，进一步防止重复启动
                if (_isAMonitorRunning)
                {
                    AddLog("[A工位] 在线监控已启动，无需重复执行");
                    return;
                }
                // 标记当前正在监控的工位为A（全局状态，便于日志/业务逻辑区分工位）
                CurrentMonitoringStation = "A";
                // 校验监控文件夹有效性：路径为空或不存在时，终止启动并提示
                if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathA) || !Directory.Exists(SelectedMonitorFolderPathA))
                {
                    AddLog("[A工位] 监控文件夹不存在，请先选择有效文件夹");
                    _isAMonitorRunning = false;// 重置监控状态
                    MessageBox.Show("A工位监控文件夹不存在，请先选择有效文件夹", "在线检测错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                // 重置线程终止标记（确保监控线程不会被误终止）
                _isThreadTerminatingA = false;
                // 销毁旧的文件监控器（FileSystemWatcher）：避免资源泄漏，保证每次启动都是全新实例
                if (_onlineMonitorWatcherA != null)
                {
                    try
                    {
                        // 1. 禁用事件触发：停止监听文件创建事件
                        _onlineMonitorWatcherA.EnableRaisingEvents = false;
                        // 2. 解绑事件：防止旧实例仍绑定事件导致重复处理
                        _onlineMonitorWatcherA.Created -= OnOnlineFileCreatedA;
                        // 3. 释放资源：销毁旧实例
                        _onlineMonitorWatcherA.Dispose();
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[A工位] 销毁旧Watcher异常：{ex.Message}");
                    }
                    finally
                    {
                        // 无论是否异常，都重置为null，避免引用旧实例
                        _onlineMonitorWatcherA = null;
                    }
                }
                // 创建新的文件监控器（FileSystemWatcher），配置监控规则
                _onlineMonitorWatcherA = new FileSystemWatcher(SelectedMonitorFolderPathA)
                {
                    // 监控所有类型文件（可按需修改，如"*.png"仅监控图片）
                    Filter = "*.*",
                    IncludeSubdirectories = true,// 监控目标文件夹的子目录
                    // 仅监听文件名/创建时间变更（精准触发）
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true// 启用事件触发（核心：开启监控）
                };
                // 绑定文件创建事件：当监控文件夹下有新文件创建时，触发OnOnlineFileCreatedA处理逻辑
                _onlineMonitorWatcherA.Created += OnOnlineFileCreatedA;
                // 标记A工位监控已启动（第二层状态锁）
                _isAMonitorRunning = true;
                AddLog($"[A工位] 已启动监控，路径：{SelectedMonitorFolderPathA}");
                AddLog($"[A工位] 筛选条件：极性（正极：{IsPositive}，负极：{IsNegative}）");//，相机（1：{IsCamera1}，2：{IsCamera2}，3：{IsCamera3}，4：{IsCamera4}）");
                // 启动A工位的并发队列处理：新文件创建后，放入队列异步处理（避免阻塞监控线程）
                StartConcurrentProcessingA();
            }
        }

        // B工位启动监控
        private void StartOnlineMonitoringB()
        {
            lock (_stationCountLock)
            {
                if (_isBMonitorRunning)
                {
                    AddLog("[B工位] 在线监控已启动，无需重复执行");
                    return;
                }

                CurrentMonitoringStation = "B";

                if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathB) || !Directory.Exists(SelectedMonitorFolderPathB))
                {
                    AddLog("[B工位] 监控文件夹不存在，请先选择有效文件夹");
                    _isBMonitorRunning = false;
                    MessageBox.Show("B工位监控文件夹不存在，请先选择有效文件夹", "在线检测错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _isThreadTerminatingB = false;

                // 销毁旧Watcher
                if (_onlineMonitorWatcherB != null)
                {
                    try
                    {
                        _onlineMonitorWatcherB.EnableRaisingEvents = false;
                        _onlineMonitorWatcherB.Created -= OnOnlineFileCreatedB;
                        _onlineMonitorWatcherB.Dispose();
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[B工位] 销毁旧Watcher异常：{ex.Message}");
                    }
                    finally
                    {
                        _onlineMonitorWatcherB = null;
                    }
                }

                // 新建Watcher
                _onlineMonitorWatcherB = new FileSystemWatcher(SelectedMonitorFolderPathB)
                {
                    Filter = "*.*",
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };
                _onlineMonitorWatcherB.Created += OnOnlineFileCreatedB;
                _isBMonitorRunning = true;
                AddLog($"[B工位] 已启动监控，路径：{SelectedMonitorFolderPathB}");
                AddLog($"[B工位] 筛选条件：极性（正极：{IsPositive}，负极：{IsNegative}）");//，相机（1：{IsCamera1}，2：{IsCamera2}，3：{IsCamera3}，4：{IsCamera4}）");
                //队列处理
                StartConcurrentProcessingB();
            }
        }
        #endregion

        #region 双工位停止监控
        // A工位停止监控
        private void StopOnlineMonitoringA()
        {
            _isThreadTerminatingA = true;
            _isThreadRunningA = false;
            lock (_stationCountLock)
            {
                if (!_isAMonitorRunning)
                {
                    //AddLog("[A工位] 在线监控未启动，无需执行停止操作");
                    return;
                }

                if (_onlineMonitorWatcherA != null)
                {
                    try
                    {
                        _onlineMonitorWatcherA.EnableRaisingEvents = false;
                        _onlineMonitorWatcherA.Created -= OnOnlineFileCreatedA;
                        _onlineMonitorWatcherA.Dispose();
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[A工位] 停止Watcher时异常：{ex.Message}");
                    }
                    finally
                    {
                        _onlineMonitorWatcherA = null;
                    }
                }

                // 标记线程终止并透过 CancellationToken 取消处理任务
                _isThreadTerminatingA = true;

                // 取消 processing task
                lock (_processingLockA)
                {
                    try
                    {
                        _ctsA?.Cancel();
                    }
                    catch { }

                    // 唤醒可能在等待的线程
                    _queueEventA.Set();
                }

                // 清空队列（如果你希望保留未处理项，先注释掉下面两行）
                lock (_queueLockA)
                {
                    _safeDetectQueueA.Clear();
                }

                _isQueueRunningA = false;

                if (_runningStationsCount > 0)
                {
                    _runningStationsCount--;
                }

                CheckAndGenerateReport();

                // 重置运行状态
                _isAMonitorRunning = false;
                _isThreadRunningA = false;
                AddLog("[A工位] 在线监控已停止（StopOnlineMonitoringA 完成）");
            }
        }
        // B工位停止监控
        private void StopOnlineMonitoringB()
        {
            lock (_stationCountLock)
            {
                if (!_isBMonitorRunning)
                {
                    //AddLog("[B工位] 在线监控未启动，无需执行停止操作");
                    return;
                }

                if (_onlineMonitorWatcherB != null)
                {
                    try
                    {
                        _onlineMonitorWatcherB.EnableRaisingEvents = false;
                        _onlineMonitorWatcherB.Created -= OnOnlineFileCreatedB;
                        _onlineMonitorWatcherB.Dispose();
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[B工位] 停止Watcher时异常：{ex.Message}");
                    }
                    finally
                    {
                        _onlineMonitorWatcherB = null;
                    }
                }

                // 标记线程终止并透过 CancellationToken 取消处理任务
                _isThreadTerminatingB = true;

                // 取消 processing task
                lock (_processingLockB)
                {
                    try
                    {
                        _ctsB?.Cancel();
                    }
                    catch { }

                    // 唤醒可能在等待的线程
                    _queueEventB.Set();
                }

                // 清空队列（如果你希望保留未处理项，先注释掉下面两行）
                lock (_queueLockB)
                {
                    _safeDetectQueueB.Clear();
                }

                _isQueueRunningB = false;

                if (_runningStationsCount > 0)
                {
                    _runningStationsCount--;
                }

                CheckAndGenerateReport();

                // 重置运行状态
                _isBMonitorRunning = false;

                AddLog("[B工位] 在线监控已停止（StopOnlineMonitoringB 完成）");
            }
        }
        #endregion

        #region 双工位新文件处理
        // A工位新文件事件
        private void OnOnlineFileCreatedA(object sender, FileSystemEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var allowedExts = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp" };
                    var ext = Path.GetExtension(e.FullPath).ToLower();
                    if (!allowedExts.Contains(ext))
                    {
                        AddLog($"[A工位-跳过] 非图片格式：{Path.GetFileName(e.FullPath)}");
                        return;
                    }

                    bool isFileReady = false;
                    for (int i = 0; i < FileReadyMaxRetry; i++)
                    {
                        if (IsFileAccessible(e.FullPath))
                        {
                            isFileReady = true;
                            break;
                        }
                        await Task.Delay(FileReadyRetryDelay);
                    }
                    if (!isFileReady)
                    {
                        AddLog($"[A工位-丢弃] {Path.GetFileName(e.FullPath)} 多次重试仍未就绪");
                        return;
                    }

                    if (!File.Exists(e.FullPath))
                    {
                        AddLog($"[A工位-丢弃] {Path.GetFileName(e.FullPath)} 已被删除");
                        return;
                    }

                    //筛选条件
                    string polarity = GetPolarityFromPath(e.FullPath, true);

                    //检查极性选择
                    if (!IsPositive && !IsNegative)
                    {
                        AddLog($"未选择正极或者负极，跳过文件：{Path.GetFileName(e.FullPath)}",CoreLogLevel.Warn);
                        Growl.Warning("未选择正极或者负极，跳过文件处理！");
                    }
                    bool isPolarityMatch = (IsPositive && polarity == "正极") || (IsNegative && polarity == "负极");                  
                    if (!isPolarityMatch)
                    {
                        return;
                    }

                    if (!IsDetectionModeMatch(e.FullPath))
                    {
                        
                        AddLog($"[工位-跳过] 当前检测模式不匹配 ({SelectedMode})：{Path.GetFileName(e.FullPath)}", CoreLogLevel.Warn);
                        return;
                    }

                    EnqueueTaskA(e.FullPath);
                }
                catch (Exception ex)
                {
                    AddLog($"[A工位-文件处理异常] {e.FullPath}：{ex.Message}");
                }
            });
        }

        // B工位新文件事件
        private void OnOnlineFileCreatedB(object sender, FileSystemEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var allowedExts = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp" };
                    var ext = Path.GetExtension(e.FullPath).ToLower();
                    if (!allowedExts.Contains(ext))
                    {
                        AddLog($"[B工位-跳过] 非图片格式：{Path.GetFileName(e.FullPath)}");
                        return;
                    }

                    bool isFileReady = false;
                    for (int i = 0; i < FileReadyMaxRetry; i++)
                    {
                        if (IsFileAccessible(e.FullPath))
                        {
                            isFileReady = true;
                            break;
                        }
                        await Task.Delay(FileReadyRetryDelay);
                    }
                    if (!isFileReady)
                    {
                        AddLog($"[B工位-丢弃] {Path.GetFileName(e.FullPath)} 多次重试仍未就绪");
                        return;
                    }

                    if (!File.Exists(e.FullPath))
                    {
                        AddLog($"[B工位-丢弃] {Path.GetFileName(e.FullPath)} 已被删除");
                        return;
                    }

                    //筛选条件
                    string polarity = GetPolarityFromPath(e.FullPath, true);
                    string camera = GetCameraNumberFromPath(e.FullPath);
                    bool isPolarityMatch = (!IsPositive && !IsNegative) || (IsPositive && polarity == "正极") || (IsNegative && polarity == "负极");
                    //bool isCameraMatch = (!IsCamera1 && !IsCamera2 && !IsCamera3 && !IsCamera4) || (IsCamera1 && camera == "1") || (IsCamera2 && camera == "2") || (IsCamera3 && camera == "3") || (IsCamera4 && camera == "4");

                    if (!isPolarityMatch)//|| !isCameraMatch
                    {
                        return;
                    }

                    EnqueueTaskB(e.FullPath);
                }
                catch (Exception ex)
                {
                    AddLog($"[B工位-文件处理异常] {e.FullPath}：{ex.Message}");
                }
            });
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

        // B工位启动并发处理
        private void StartConcurrentProcessingB()
        {
            lock (_processingLockB)
            {
                if (_isQueueRunningB)
                {
                    AddLog("[B队列] 队列处理已在运行，跳过重复启动");
                    return;
                }
                if (_processingTaskB != null && !_processingTaskB.IsCompleted)
                {
                    AddLog("[B队列] 处理线程已在运行，跳过重复启动");
                    return;
                }

                _ctsB = new CancellationTokenSource();
                CancellationToken token = _ctsB.Token;
                _processingTaskB = Task.Run(async () =>
                {
                    _isQueueRunningB = true;
                    AddLog("B队列线程启动");
                    try
                    {
                        await ProcessQueueConcurrentB(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        AddLog("B队列线程收到取消请求并退出");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"B队列线程未处理异常：{ex.Message}");
                    }
                    finally
                    {
                        _isQueueRunningB = false;
                        AddLog("B队列线程已终止");
                    }
                }, token);
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
            // 循环处理队列：直到收到取消信号 或 线程终止标记触发（双重保障，确保能优雅退出）
            while (!token.IsCancellationRequested && !_isThreadTerminatingA)
            {
                string? filePath = null;
                // 1. 线程安全从队列取待检测文件路径（lock保证多生产者/消费者下的队列安全）
                lock (_queueLockA)
                {
                    // TryDequeue：尝试出队，无数据时返回false，filePath为null
                    _safeDetectQueueA.TryDequeue(out filePath);
                }
                // 2. 队列无数据时：进入等待状态（避免空循环占用100%CPU）
                if (filePath == null)
                {
                    // ⚡ 核心优化：WaitHandle.WaitAny 监听两个信号，实现“有任务就唤醒，有取消就退出，超时就重试”
                    int signaledIndex = WaitHandle.WaitAny(new WaitHandle[]
                    {
                        // 监听的信号集合：
                        // _queueEventA：队列有新文件时触发的唤醒信号（生产者入队时Set）
                        // token.WaitHandle：取消令牌的等待句柄（取消时触发）
                        _queueEventA,
                        token.WaitHandle
                    }, TimeSpan.FromSeconds(30));

                    if (signaledIndex == WaitHandle.WaitTimeout)
                        continue; // 超时：重新进入循环，再次尝试取队列数据
                    if (token.IsCancellationRequested || _isThreadTerminatingA)
                        break; // 收到取消信号/线程终止标记：退出循环，结束处理
                    continue; // 收到队列唤醒信号（_queueEventA）：重新进入循环，取新入队的文件
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
                // ConfigureAwait(false)：不捕获UI上下文，提升异步性能
                await _semaphoreA.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    // 6. 核心：图片检测与结果处理逻辑
                    try
                    {
                        // 6.1 动态对象存储检测数据（灵活扩展字段）
                        dynamic detectionData = new ExpandoObject();
                        // 解析文件路径到动态对象（提取如文件名、路径、时间等信息）
                        ParsePathToDynamicObject(detectionData, filePath);
                        detectionData.Station = "A";// 标记工位为A
                        // 6.2 执行损伤检测：核心业务逻辑，返回OK/NG/异常
                        string detectResult = DamageDetection(filePath, "正极");
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
                            _fullDetectionDataList.Add(detectionData);// 添加到完整检测数据列表
                            UpdateDisplayDataList();// 更新展示用的数据列表
                            CalculateDetectionStatistics();// 重新计算检测统计（OK数、NG数、OK率等）
                        });
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[A工位-检测异常] {Path.GetFileName(filePath)}：{ex.Message}");
                    }
                }
                finally
                {
                    // 7. 释放信号量：无论检测成功/失败/异常，必须释放，避免信号量耗尽导致死锁
                    _semaphoreA.Release();
                }
            }
        }

        // B工位队列并发处理
        private async Task ProcessQueueConcurrentB(CancellationToken token)
        {
            // 使用你的信号量逻辑（保留原有的信号量等待/释放逻辑）
            // 这里不再使用 await _semaphoreA.WaitAsync() 包裹整个方法，因为你在每个并行检测里需要信号量控制
            while (!token.IsCancellationRequested && !_isThreadTerminatingB)
            {
                string? filePath = null;
                // 尝试从队列中取任务
                lock (_queueLockB)
                {
                    _safeDetectQueueB.TryDequeue(out filePath);
                }

                if (filePath == null)
                {
                    // 等待唤醒或超时
                    bool signaled = _queueEventB.WaitOne(TimeSpan.FromSeconds(30));
                    if (!signaled)
                    {
                        // 周期性检查取消令牌
                        continue;
                    }
                    else
                    {
                        // 收到信号，继续循环尝试取任务
                        continue;
                    }
                }

                // 如果被取消，则退出
                token.ThrowIfCancellationRequested();

                if (!File.Exists(filePath))
                {
                    AddLog($"[B工位-队列] {Path.GetFileName(filePath)} 已删除，跳过");
                    continue;
                }

                // 使用信号量限制并发（和你原来逻辑一致）
                await _semaphoreB.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    // 处理单张图片（保持原有处理流程）
                    try
                    {
                        dynamic detectionData = new ExpandoObject();
                        ParsePathToDynamicObject(detectionData, filePath);
                        detectionData.Station = "B";
                        string detectResult = DamageDetection(filePath, "负极");
                        DateTime realDetectionTime = DateTime.Now;
                        bool isNG = detectResult == "NG";
                        AddImageToQueue(filePath, isNG, "B", realDetectionTime);
                        if (detectResult.Equals("异常（空图片）", StringComparison.OrdinalIgnoreCase))
                        {
                            AddLog($"[B工位-结果] {Path.GetFileName(filePath)}：空图片，不统计");
                            continue;
                        }

                        bool isResultSaved = (detectResult == "OK" && IsSaveResultOK) || (detectResult == "NG" && IsSaveResultNG);
                        detectionData.ResultImagePath = isResultSaved ? GetResultImagePath(filePath, detectResult, true) : null;
                        detectionData.DetectionResult = detectResult;
                        WriteToCsv(filePath, detectResult, true);

                        // UI 更新放到 Dispatcher
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            _fullDetectionDataList.Add(detectionData);
                            UpdateDisplayDataList();
                            CalculateDetectionStatistics();
                        });
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[B工位-检测异常] {Path.GetFileName(filePath)}：{ex.Message}");
                    }
                }
                finally
                {
                    _semaphoreB.Release();
                }
            } // end while
        }
        public void EnqueueTaskA(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                //AddLog("[A工位-入队] 无效路径，跳过入队");
                return;
            }

            lock (_queueLockA)
            {
                _safeDetectQueueA.Enqueue(filePath);
            }

            _queueEventA.Set(); // 唤醒等待线程（线程由 StartOnlineMonitoringA 启动并保持）
                                // AddLog($"[A工位-入队] 新任务入队：{Path.GetFileName(filePath)} (队列大小未知)");
        }

        // B工位入队方法
        public void EnqueueTaskB(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                //AddLog("[B工位-入队] 无效路径，跳过入队");
                return;
            }

            lock (_queueLockB)
            {
                _safeDetectQueueB.Enqueue(filePath);
            }

            _queueEventB.Set(); // 唤醒等待线程（线程由 StartOnlineMonitoringA 启动并保持）
                                // AddLog($"[B工位-入队] 新任务入队：{Path.GetFileName(filePath)} (队列大小未知)");
        }
        #endregion
        #endregion

        #region 在线检测 - 启动监控
        /// <summary>
        /// 启动在线文件夹监控
        /// </summary>
        private void StartOnlineMonitoring()
        {
            if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPath) || !Directory.Exists(SelectedMonitorFolderPath))
            {
                AddLog("[在线检测] 监控文件夹不存在，请先选择有效文件夹");
                IsOnlineDetectionEnabled = false;
                MessageBox.Show("监控文件夹不存在，请先选择有效文件夹", "在线检测错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 初始化监控器
            _onlineMonitorWatcher = new FileSystemWatcher(SelectedMonitorFolderPath)
            {
                Filter = "*.*",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            // 绑定新文件创建事件
            _onlineMonitorWatcher.Created += OnOnlineFileCreated;

            AddLog($"[在线检测] 已启动监控，路径：{SelectedMonitorFolderPath}");
            AddLog($"[在线检测] 当前筛选条件：极性（正极：{IsPositive}，负极：{IsNegative}），相机（1：{IsCamera1}，2：{IsCamera2}，3：{IsCamera3}，4：{IsCamera4}）");
        }
        #endregion

        #region 在线检测 - 停止监控
        /// <summary>
        /// 停止在线文件夹监控
        /// </summary>
        private void StopOnlineMonitoring()
        {
            if (_onlineMonitorWatcher != null)
            {
                _onlineMonitorWatcher.EnableRaisingEvents = false;
                _onlineMonitorWatcher.Created -= OnOnlineFileCreated;
                _onlineMonitorWatcher.Dispose();
                _onlineMonitorWatcher = null;
                AddLog("[在线检测] 已停止监控文件夹");
            }
            _safeDetectQueue.Clear();
            _isQueueRunning = false;
            AddLog($"[在线检测] 已清空队列（剩余任务数：0）");
        }
        #endregion

        #region 在线检测 - 新文件处理
        /// <summary>
        /// 监控到新文件时触发
        /// </summary>
        private void OnOnlineFileCreated(object sender, FileSystemEventArgs e)
        {
            // 异步处理，不阻塞FileSystemWatcher线程
            _ = Task.Run(async () =>
            {
                try
                {
                    var allowedExts = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp" };
                    var ext = Path.GetExtension(e.FullPath).ToLower();
                    if (!allowedExts.Contains(ext))
                    {
                        AddLog($"[跳过] 非图片格式：{Path.GetFileName(e.FullPath)}");
                        return;
                    }

                    //动态等待文件就绪
                    bool isFileReady = false;
                    for (int i = 0; i < FileReadyMaxRetry; i++)
                    {
                        if (IsFileAccessible(e.FullPath))
                        {
                            isFileReady = true;
                            break;
                        }
                        //AddLog($"[等待文件] {Path.GetFileName(e.FullPath)} 未就绪（{i + 1}/{FileReadyMaxRetry}）");
                        await Task.Delay(FileReadyRetryDelay); // 异步等待，不阻塞线程
                    }
                    if (!isFileReady)
                    {
                        AddLog($"[丢弃] {Path.GetFileName(e.FullPath)} 多次重试仍未就绪（可能被占用）");
                        return;
                    }

                    if (!File.Exists(e.FullPath))
                    {
                        AddLog($"[丢弃] {Path.GetFileName(e.FullPath)} 已被删除");
                        return;
                    }

                    string polarity = GetPolarityFromPath(e.FullPath, true);
                    string camera = GetCameraNumberFromPath(e.FullPath);
                    bool isPolarityMatch = (!IsPositive && !IsNegative) || (IsPositive && polarity == "正极") || (IsNegative && polarity == "负极");
                    bool isCameraMatch = (!IsCamera1 && !IsCamera2 && !IsCamera3 && !IsCamera4) || (IsCamera1 && camera == "1") || (IsCamera2 && camera == "2") || (IsCamera3 && camera == "3") || (IsCamera4 && camera == "4");

                    if (!isPolarityMatch || !isCameraMatch)
                    {
                        //AddLog($"[筛选跳过] {Path.GetFileName(e.FullPath)}（极性：{polarity}，相机：{camera}，筛选条件：正极={IsPositive},负极={IsNegative},相机1-4={IsCamera1},{IsCamera2},{IsCamera3},{IsCamera4}）");
                        return;
                    }

                    _safeDetectQueue.Enqueue(e.FullPath);
                    //AddLog($"[加入队列] {Path.GetFileName(e.FullPath)}（当前队列长度：{_safeDetectQueue.Count}）");
                    StartConcurrentProcessing();
                }
                catch (Exception ex)
                {
                    AddLog($"[文件处理异常] {e.FullPath}：{ex.Message}");
                }
            });
        }

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

        //启动并发处理线程
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

        #region 在线检测 - 队列处理

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
                        var station = GetElectrodeType(filePath);
                        dynamic detectionData = new ExpandoObject();
                        ParsePathToDynamicObject(detectionData, filePath);
                        string detectResult = DamageDetection(filePath, station);

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
                            _fullDetectionDataList.Add(detectionData);
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

        private string? GetElectrodeType(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            string fileName = Path.GetFileName(filePath);

            if (fileName.Contains("正极")) return "正极";
            if (fileName.Contains("负极")) return "负极";

            string directoryPath = Path.GetDirectoryName(filePath) ?? "";

            if (directoryPath.Contains("正极")) return "正极";
            if (directoryPath.Contains("负极")) return "负极";

            // 记录异常日志
            AddLog($"[WARN] 无法判定电极类型: {filePath}",CoreLogLevel.Warn);
            return null;  // 返回null而非抛异常
        }


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

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                LogTable.Insert(0, newLog);
                if (LogTable.Count > 100)
                {
                    LogTable.RemoveAt(LogTable.Count - 1);
                }
            })
            );

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
                H246Zhengmodle = zhengParam[3] as IPredictor;
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
                H246Fumodle = fuParam[3] as IPredictor;
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

        private bool _isPositivePolarity = false;// 全局布尔变量：true 表示正极，false 表示负极
        private string DamageDetection(string imagePath, string? station)
        {
            #region 参数定义
            // 线宽常量 - 可根据需要调整
            const int RECTANGLE_THICKNESS = 5;  // 框线宽度
            const int TEXT_THICKNESS = 4;       // 文字粗细
            const double FONT_SCALE = 5;      // 字体大小

            float testScore;    // 置信度
            string testName;    // 缺陷类别
            float testWidth;    // 宽度
            float testHeight;   // 长度
            float testArea;     // 面积
            #endregion

            #region 极性判定
            _isPositivePolarity = imagePath.Contains("正极");
            #endregion

            #region 初始化变量
            List<AIResultModel>? defectList = null;
            Mat? imageMat = null;
            string ZhengGongwei = "PTO与行大面正极";
            string FuGongwei = "PTO与行大面负极";
            string exInfo;
            string finalResult = "异常";
            HObject? delRegion = null;
            string polarity = GetPolarityFromPath(imagePath);

            if (polarity == null)
            {
                AddLog($"[检测异常] 图片路径解析失败或非Negative极性：{imagePath}");
                return "异常（路径解析失败/非Negative极性）";
            }
            #endregion
            try
            {
                #region ROI区域加载
                string stationCamerano = $"{station}";
                if (!_detectionRegionCache.TryGetValue(stationCamerano, out delRegion))
                {
                    delRegion = GenerateDelRegionFromXldFile(stationCamerano);
                    if (delRegion != null)
                    {
                        _detectionRegionCache[stationCamerano] = delRegion;
                        AddLog($"[检测文件兜底] {station}，从XLD生成DelRegion并缓存");
                    }
                    else if (!_warnedNoDelRegionCameras.Contains(stationCamerano))
                    {
                        AddLog($"[检测文件兜底] {station}，未使用负极误判区域过滤（无ROI）");
                        _warnedNoDelRegionCameras.Add(stationCamerano);
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
                if (H246Zhengmodle == null)
                {
                    AddLog("[错误] 正极模型为 null，无法调用 ModelActionDiePian");
                    imageMat?.Release();
                    return "异常 H246Zhengmodle 为 null";
                }
                if (H246Fumodle == null)
                {
                    AddLog("[错误] 负极模型为 null，无法调用 ModelActionDiePian");
                    imageMat?.Release();
                    return "异常 H246Fumodle 为 null";
                }
                #endregion

                #region AI检测
                if (_isPositivePolarity)
                {
                    ai.ModelActionDiePian(H246Zhengmodle, imageMat, ZhengGongwei, delRegion, out defectList, out exInfo);
                }
                else
                {
                    ai.ModelActionDiePian(H246Fumodle, imageMat, FuGongwei, delRegion, out defectList, out exInfo);
                }
                if (!string.IsNullOrEmpty(exInfo))
                {
                    AddLog($"文件：{imagePath};[AI检测警告]: {exInfo}", CoreLogLevel.Info, isToDatabase: true);
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
                        string detailMsg = $"文件名：{imagePath} | 极性：{polarity} | [检测缺陷信息] 类型:{testName} | " + $"置信度:{testScore:F3} | " + $"宽:{testWidth:F6} | " + $"高:{testHeight:F6} | " + $"面积:{testArea:F6} | ";
                        AddLog(detailMsg);

                        // 查找对应的阈值配置
                        var thresholdConfig = DefectDataGrid.FirstOrDefault(
                            item => item.DefectName == testName && item.IsEnable);

                        if (thresholdConfig != null)
                        {
                            // 只有当缺陷超过阈值时才判定为NG
                            bool exceedsThreshold =
                                testHeight > thresholdConfig.Height ||
                                testWidth > thresholdConfig.Width ||
                                testArea > (thresholdConfig.Area) ||
                                testScore > thresholdConfig.Credibility;

                            if (exceedsThreshold)
                            {
                                realDefects.Add(defect);
                            }
                        }
                    }

                    // 只有存在真正超过阈值的缺陷时，才判定为NG
                    if (realDefects.Count > 0)
                    {
                        finalResult = "NG";
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
                        SaveDetectionResult(imagePath, imageMat, finalResult, true);
                        SaveDetectionResult1(imagePath, imageMat, finalResult, true);
                    }
                }
                else // finalResult == "NG"
                {
                    AddLog($"检测完成：{imagePath} → 结果：{finalResult}，缺陷数量：{realDefects.Count}");

                    using var drawMat = imageMat.Clone();   // 克隆原图用于绘制，防止修改原 imageMat

                    // 绘制缺陷框和标签
                    foreach (var defect in realDefects)
                    {
                        Cv2.Rectangle(drawMat, defect.Rectangle, new Scalar(0, 0, 255), RECTANGLE_THICKNESS);

                        // 可选：在缺陷旁边标注缺陷名称
                        var labelPos = new OpenCvSharp.Point(defect.Rectangle.X, defect.Rectangle.Y - 80);
                        PutTextChinese(drawMat, defect.Name, labelPos, 1.0f, new Scalar(0, 0, 255));
                    }

                    // 绘制缺陷信息列表（左上角）
                    if (realDefects.Count > 0)
                    {
                        // ------------------ 参数可调 ------------------
                        int startX = 240;                    // 左上角起始 x（调整为左侧）
                        int startY = 340;                    // 顶部 y
                        int lineHeight = 150;                // 每行高度
                        /*int padding = 15; */                  // 内边距
                        //int bgWidth = 600;                  // 背景宽度（根据文字长度调整）

                      
                        // 标题行
                        PutTextChinese(drawMat,
                            "缺陷列表        置信度   宽(mm)    高(mm)   面积(mm²)",
                            new OpenCvSharp.Point(startX, startY),
                            1.6f, new Scalar(255, 0, 0));

                        // 逐行绘制缺陷信息（统一用 drawMat）
                        for (int i = 0; i < realDefects.Count; i++)
                        {
                            var d = realDefects[i];
                            int y = startY + (i + 1) * lineHeight;

                            string line = $"{i + 1}. {d.Name,-10} {d.Score,-8:F3} {d.Width,-10:F3} {d.Height,-10:F3} {d.Area,-10:F3}";

                            PutTextChinese(drawMat,
                                line,
                                new OpenCvSharp.Point(startX, y),
                                1.6f, new Scalar(40, 40, 220));   // 深蓝色文字
                        }
                    }

                    // 在左上角画大字 NG（调整位置为左侧，避免与列表重叠）
                    Cv2.PutText(drawMat, "NG",
                        new OpenCvSharp.Point(150, 180),
                        HersheyFonts.HersheyComplex, 5.2,
                        new Scalar(0, 0, 255), 12);

                    // 保存绘制后的图像
                    if (IsSaveResultNG)
                    {
                        // 在保存前添加日志检查
                        AddLog($"准备保存 NG 图：drawMat 尺寸 {drawMat.Width}x{drawMat.Height}，是否为空：{drawMat.Empty()}");

                        SaveDetectionResult(imagePath, drawMat, finalResult, true);
                        SaveDetectionResult1(imagePath, drawMat, finalResult, true);
                    }
                }
                #endregion

                // 原图保存
                if (IsSaveOriginalOK && finalResult == "OK")
                {
                    using (Mat originalMat = Cv2.ImRead(imagePath, ImreadModes.Color))
                    {
                        SaveDetectionResult(imagePath, originalMat, finalResult, false);
                    }
                }
                if (IsSaveOriginalNG && finalResult == "NG")
                {
                    using (Mat originalMat = Cv2.ImRead(imagePath, ImreadModes.Color))
                    {
                        SaveDetectionResult(imagePath, originalMat, finalResult, false);
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
                string? sourceRoot = "D:";
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
            bool isAnyDetectionRunning = _isDetecting || IsOnlineDetectionEnabledA || IsOnlineDetectionEnabledB;
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
                        Task.Run(() => StopOnlineMonitoringA()),
                        Task.Run(() => StopOnlineMonitoringB())
                    );

                    // 同步更新开关状态
                    IsOnlineDetectionEnabledA = false;
                    IsOnlineDetectionEnabledB = false;
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
        private void ExecuteViewResult(dynamic? data)
        {
            if (data == null)
            {
                MessageBox.Show("未选中任何检测数据，请先在表格中选择一条记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            CurrentResultImage = null;
            string? resultImagePath = null;
            if (data is ExpandoObject expandoData)
            {
                var dataDict = (IDictionary<string, object?>)expandoData;
                // 判断键存在 + 类型转换
                if (dataDict.TryGetValue("ResultImagePath", out object? pathObj)
                    && pathObj is string pathStr
                    && !string.IsNullOrWhiteSpace(pathStr))
                {
                    resultImagePath = Path.GetFullPath(pathStr);
                }
            }
            // 兼容其他可能的参数类型
            else if (data is string strPath && !string.IsNullOrWhiteSpace(strPath))
            {
                resultImagePath = Path.GetFullPath(strPath);
            }

            // 路径有效性二次校验
            if (string.IsNullOrEmpty(resultImagePath))
            {
                AddLog($"[查看失败] 结果图路径为空（动态对象中未找到有效 ResultImagePath）");
                MessageBox.Show("结果图路径未生成，请先完成检测", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!File.Exists(resultImagePath))
            {
                AddLog($"[查看失败] 结果图文件不存在，路径：{resultImagePath}");
                string tips = $"文件可能已被移动/删除，默认存储路径为：\n{Path.GetDirectoryName(resultImagePath)}";
                MessageBox.Show($"结果图不存在或路径无效！\n{tips}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // 图片加载
                using (var fileStream = new FileStream(resultImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fileStream.Length == 0)
                    {
                        throw new IOException("结果图文件为空（可能是检测时保存失败）");
                    }

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = fileStream;
                    bitmap.EndInit();

                    // 冻结图片,允许跨线程访问
                    if (!bitmap.IsFrozen)
                    {
                        bitmap.Freeze();
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentResultImage = bitmap;
                    });

                    AddLog($"[查看成功] 已加载结果图：{resultImagePath}（分辨率：{bitmap.PixelWidth}×{bitmap.PixelHeight}）");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                AddLog($"[加载失败] 无权限访问结果图：{ex.Message}，路径：{resultImagePath}");
                MessageBox.Show($"无权限访问图片文件，请检查文件夹权限！\n路径：{resultImagePath}", "权限错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (FileFormatException ex)
            {
                AddLog($"[加载失败] 图片格式不支持：{ex.Message}，路径：{resultImagePath}");
                MessageBox.Show($"图片格式无效（仅支持 BMP/JPG/PNG/TIFF 等常见格式）！\n路径：{resultImagePath}", "格式错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException ex)
            {
                AddLog($"[加载失败] 文件读写异常：{ex.Message}，路径：{resultImagePath}");
                MessageBox.Show($"图片文件被占用或损坏！\n建议：关闭其他可能打开该文件的程序后重试。", "文件异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AddLog($"[加载失败] 未知错误：{ex.Message}，堆栈：{ex.StackTrace}，路径：{resultImagePath}");
                MessageBox.Show($"图片加载失败：{ex.Message}\n路径：{resultImagePath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            IsOfflineProgressVisible = Visibility.Visible;
            try
            {
                AddLog($"开始检测单张图片：{Path.GetFileName(filePath)}");
                await Task.Run(() =>
                {
                    var station = GetElectrodeType(filePath);
                    dynamic detectionData = new ExpandoObject();
                    ParsePathToDynamicObject(detectionData, filePath);
                    string detectResult = DamageDetection(filePath, station);
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

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        //先添加到完整集合
                        _fullDetectionDataList.Add(detectionData);
                        UpdateDisplayDataList();
                    });


                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CalculateDetectionStatistics();
                    });
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
        /// 文件夹批量检测,带进度条
        /// </summary>
        private async Task GetDataFromFolder( List<string> imageFiles)
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
                string polarity = GetPolarityFromPath(filePath);
                string camera = GetCameraNumberFromPath(filePath);
                bool isPolarityMatch = false;
                if (!IsPositive && !IsNegative)
                {
                    isPolarityMatch = true;
                }
                else
                {
                    if ((IsPositive && polarity == "正极") || (IsNegative && polarity == "负极"))
                    {
                        isPolarityMatch = true;
                    }
                }

                //加入筛选列表
                if (isPolarityMatch )
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
                            var station = GetElectrodeType(filePath);
                            dynamic detectionData = new ExpandoObject();
                            ParsePathToDynamicObject(detectionData, filePath);
                            if (token.IsCancellationRequested) break;
                            string detectResult = DamageDetection(filePath, station);
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
                                    _fullDetectionDataList.Add(detectionData);
                                    UpdateDisplayDataList();
                                    CalculateDetectionStatistics();
                                });
                            }
                            else
                            {
                                //只添加数据，不刷新UI
                                _fullDetectionDataList.Add(detectionData);
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
                        if (!dataDict.TryGetValue("PartType", out var partTypeObj)) return false;
                        if (partTypeObj == null) return false;

                        string partType = partTypeObj?.ToString() ?? string.Empty;
                        if (string.IsNullOrEmpty(partType)) return false;

                        // 3. 核心筛选逻辑
                        // 如果勾选正极 且 数据类型是正极 -> 保留
                        bool matchPositive = IsPositive && partType == "正极";
                        // 如果勾选负极 且 数据类型是负极 -> 保留
                        bool matchNegative = IsNegative && partType == "负极";

                        // 只要满足其中一个条件即可
                        return matchPositive || matchNegative;
                    })
                    .ToList();

                //统计新数据中的 OK/NG 数量
                int newTotalCount = filteredData.Count;
                int newOKCount = filteredData.Count(data =>
                {
                    var dataDict = (IDictionary<string, object>)data;
                    return dataDict.TryGetValue("DetectionResult", out var resultObj)
                            && resultObj?.ToString() == "OK";
                });
                int newNGCount = newTotalCount - newOKCount;


                //最终统计值 = 历史基础值 + 新数据值
                TotalNeDetectionCount = _historyTotalCount + newTotalCount;
                OKCount = _historyOKCount + newOKCount;
                NGCount = _historyNGCount + newNGCount;

                //计算 OK率
                OKRate = TotalNeDetectionCount > 0
                    ? Math.Round((double)OKCount / TotalNeDetectionCount * 100, 2)
                    : 0;

                //AddLog($"[统计计算] 历史值+新数据值：总={_historyTotalCount}+{newTotalCount}={TotalNeDetectionCount}，OK={_historyOKCount}+{newOKCount}={OKCount}，NG={_historyNGCount}+{newNGCount}={NGCount}");
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
                DetectionDataList.Clear();
                AddLog("[清空列表] 用户确认，已清空数据");
                _fullDetectionDataList.Clear();
                _currentCsvFilePath = null;
            }
            else
            {
                AddLog("[清空列表] 用户取消，操作终止");
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
                    AddLog($"[统计清零] 已重置历史基础值：历史总检测数={_historyTotalCount}，历史OK数={_historyOKCount}，历史NG数={_historyNGCount}");

                    _fullDetectionDataList.Clear();
                    //AddLog($"[统计清零] 已清空新检测数据列表（_fullDetectionDataList）");
                    TotalNeDetectionCount = 0;
                    OKCount = 0;
                    NGCount = 0;
                    OKRate = 0;
                    //AddLog($"[统计清零] 已重置当前统计属性：总检测数={TotalNeDetectionCount}，OK数={OKCount}，NG数={NGCount}，OK率={OKRate}%");
                    _currentCsvFilePath = null;
                }
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
        private void ParsePathToDynamicObject(dynamic data, string filePath)
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

                DirectoryInfo currentDir = new DirectoryInfo(dirPath);
                data.Camerainformation = currentDir.Name;
                data.Polarity = GetPolarityFromPath(filePath);
                data.PartType = data.Polarity;
            }
            catch (Exception ex)
            {
                AddLog($"路径解析异常：{ex.Message}，路径：{filePath}");
            }
        }
        // 保存检测结果图像的方法
        private void SaveDetectionResult(string originalImagePath, Mat imageMat, string result, bool isResultImage)
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
        }


        #region 参数管控 
        private void SaveDetectionResult1(string originalImagePath, Mat imageMat, string result, bool isResultImage)
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
        }

        #endregion

        // 生成保存路径
        private string? _offlineCurrentDate;

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
                // 初始默认使用全局离线模式标识
                bool isOffline = _isOfflineMode;
                // 标准化原始图像路径（统一路径格式，避免因相对路径/大小写导致的判断错误）
                string normalizedImagePath = Path.GetFullPath(imagePath);

                // A工位在线判断：如果原始图像路径属于A工位监控目录，则强制切换为在线模式
                if (!string.IsNullOrEmpty(SelectedMonitorFolderPathA))
                {
                    string normalizedMonitorA = Path.GetFullPath(SelectedMonitorFolderPathA);
                    // 拼接空字符串确保路径以目录分隔符结尾（如"D:\监控A\"），避免前缀匹配错误
                    normalizedMonitorA = Path.Combine(normalizedMonitorA, "");
                    // 不区分大小写判断路径前缀，兼容不同系统的路径格式
                    if (normalizedImagePath.StartsWith(normalizedMonitorA, StringComparison.OrdinalIgnoreCase))
                    {
                        isOffline = false;
                    }
                }

                // B工位在线判断：逻辑同A工位，只要属于任一工位监控目录则为在线模式
                if (!string.IsNullOrEmpty(SelectedMonitorFolderPathB))
                {
                    string normalizedMonitorB = Path.GetFullPath(SelectedMonitorFolderPathB);
                    normalizedMonitorB = Path.Combine(normalizedMonitorB, "");
                    if (normalizedImagePath.StartsWith(normalizedMonitorB, StringComparison.OrdinalIgnoreCase))
                    {
                        isOffline = false;
                    }
                }

                // 离线模式特殊处理：固定日期（避免离线时系统时间变动导致路径混乱）
                if (isOffline)
                {
                    if (string.IsNullOrEmpty(_offlineCurrentDate))
                    {
                        try
                        {
                            // 初始化离线模式的固定日期（格式：yyyyMMdd）
                            _offlineCurrentDate = DateTime.Now.ToString("yyyyMMdd");
                            _offlineStartTime = DateTime.Now;
                            AddLog($"[离线启动] 启动当天日期（currentDate）固定为：{_offlineCurrentDate}");
                        }
                        catch (Exception ex)
                        {
                            // 系统时间异常时使用默认日期兜底，避免路径构造失败
                            _offlineCurrentDate = "20251028";
                            AddLog($"[离线初始化失败] 系统时间异常：{ex.Message}，强制使用默认日期：{_offlineCurrentDate}");
                        }
                    }
                }

                // ===================== 第二步：确定根目录（容错适配） =====================
                // 默认优先使用D盘作为根目录
                string? rootDirectory = "D:";
                // 容错1：D盘不存在则切换到系统文档目录
                if (!Directory.Exists(rootDirectory))
                {
                    rootDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    AddLog($"[根目录适配] D盘不存在，切换为文档目录：{rootDirectory}");
                }
                // 容错2：文档目录无效则切换到程序运行目录
                if (string.IsNullOrEmpty(rootDirectory) || !Directory.Exists(rootDirectory))
                {
                    rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    AddLog($"[根目录适配] 文档目录无效，切换为程序目录：{rootDirectory}");
                }
                // 最终容错：根目录仍无效则返回null
                if (string.IsNullOrEmpty(rootDirectory))
                {
                    AddLog("[错误] 无法获取有效盘符");
                    return null;
                }

                // ===================== 第三步：构造路径核心参数 =====================
                // 日期参数：在线模式用当前时间，离线模式用初始化的固定日期
                string currentDate = isOffline ? _offlineCurrentDate! : DateTime.Now.ToString("yyyyMMdd");
                // 从原始路径提取日期文件夹（失败则用当前日期兜底）
                string dateFolder = GetDateFromPath(imagePath);
                if (string.IsNullOrEmpty(dateFolder)) dateFolder = DateTime.Now.ToString("yyyyMMdd");

                // 从原始路径提取业务属性参数（均做兜底处理，避免空值导致路径拼接失败）
                string polarity = GetPolarityFromPath(imagePath);// 极性（如正极/负极）
                if (string.IsNullOrEmpty(polarity)) polarity = "未知极性";
                string cellName = GetCellNameFromPath(imagePath);// 电芯名称
                if (string.IsNullOrEmpty(cellName)) cellName = "未知电芯";
                //string cameraInfo = GetCameraInfoFromPath(imagePath); // 相机信息
                //if (string.IsNullOrEmpty(cameraInfo)) cameraInfo = "未知相机";

                // 提取原始文件的名称和扩展名（兜底处理）
                string fileName = Path.GetFileNameWithoutExtension(imagePath);// 无扩展名的文件名
                if (string.IsNullOrEmpty(fileName)) fileName = "未知文件";
                string fileExt = Path.GetExtension(imagePath);// 文件扩展名（如.jpg）
                if (string.IsNullOrEmpty(fileExt)) fileExt = ".jpg";

                // 区分图像类型（原图/结果图）
                string imageType = isResultImage ? "结果图" : "原图";
                // 构造新文件名：结果图追加检测结果，原图保持原名
                string newFileName = isResultImage
                    ? $"{cellName}_{fileName}_{result}{fileExt}"
                    : $"{cellName}_{fileName}{fileExt}";


                string station = string.Empty; ;
                // 在线模式：在路径中插入工位信息（位置：日期文件夹前）
                if (!isOffline)
                {
                    station = GetStationFromPath(imagePath);
                    if (string.IsNullOrEmpty(station)) station = "未知工位";
                    //pathParts.Insert(3, station); // 插入位置：索引3（currentDate之后，dateFolder之前）
                }

                // ===================== 第四步：拼接完整路径 =====================
                // 初始化路径片段列表（基础结构：根目录\IME复判\日期\日期文件夹\图像类型\检测结果\极性\文件名）
                List<string> pathParts = new List<string>
                {
                    rootDirectory, "IME复判", currentDate,station, dateFolder,
                    imageType, result, polarity, newFileName
                };



                // 过滤空路径片段（避免拼接出无效路径如"\\空值\\文件.jpg"）
                pathParts = pathParts.Where(part => !string.IsNullOrEmpty(part)).ToList();
                if (pathParts.Count == 0)
                {
                    AddLog("[错误] 路径片段为空，无法拼接");
                    return null;
                }

                // 拼接所有路径片段并标准化（解决路径分隔符、重复目录等问题）
                string savePath = Path.GetFullPath(Path.Combine(pathParts.ToArray()));
                AddLog($"[路径构造成功] {imageType}路径：{savePath}");
                return savePath;
            }
            catch (Exception ex)
            {
                AddLog($"[路径构造失败] {ex.Message}");
                return null;
            }
        }

        #region 参数管控-保存路径
        private string? GetResultImagePath1(string imagePath, string result, bool isResultImage)
        {
            try
            {
                //离线/在线模式判断
                bool isOffline = _isOfflineMode;
                string normalizedImagePath = Path.GetFullPath(imagePath);

                // A工位在线判断
                if (!string.IsNullOrEmpty(SelectedMonitorFolderPathA))
                {
                    string normalizedMonitorA = Path.GetFullPath(SelectedMonitorFolderPathA);
                    normalizedMonitorA = Path.Combine(normalizedMonitorA, "");
                    if (normalizedImagePath.StartsWith(normalizedMonitorA, StringComparison.OrdinalIgnoreCase))
                    {
                        isOffline = false;
                    }
                }

                // B工位在线判断
                if (!string.IsNullOrEmpty(SelectedMonitorFolderPathB))
                {
                    string normalizedMonitorB = Path.GetFullPath(SelectedMonitorFolderPathB);
                    normalizedMonitorB = Path.Combine(normalizedMonitorB, "");
                    if (normalizedImagePath.StartsWith(normalizedMonitorB, StringComparison.OrdinalIgnoreCase))
                    {
                        isOffline = false;
                    }
                }

                // 离线模式
                if (isOffline)
                {
                    if (string.IsNullOrEmpty(_offlineCurrentDate))
                    {
                        try
                        {
                            _offlineCurrentDate = DateTime.Now.ToString("yyyyMMdd");
                            _offlineStartTime = DateTime.Now;
                            AddLog($"[离线启动] 启动当天日期（currentDate）固定为：{_offlineCurrentDate}");
                        }
                        catch (Exception ex)
                        {
                            _offlineCurrentDate = "20251028";
                            AddLog($"[离线初始化失败] 系统时间异常：{ex.Message}，强制使用默认日期：{_offlineCurrentDate}");
                        }
                    }
                }

                //根目录构造
                string? rootDirectory = "D:";
                if (!Directory.Exists(rootDirectory))
                {
                    rootDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    AddLog($"[根目录适配] D盘不存在，切换为文档目录：{rootDirectory}");
                }
                if (string.IsNullOrEmpty(rootDirectory) || !Directory.Exists(rootDirectory))
                {
                    rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    AddLog($"[根目录适配] 文档目录无效，切换为程序目录：{rootDirectory}");
                }
                if (string.IsNullOrEmpty(rootDirectory))
                {
                    AddLog("[错误] 无法获取有效盘符");
                    return null;
                }

                // 核心路径参数构造
                string currentDate = isOffline ? _offlineCurrentDate! : DateTime.Now.ToString("yyyyMMdd");
                string dateFolder = GetDateFromPath(imagePath);
                if (string.IsNullOrEmpty(dateFolder)) dateFolder = DateTime.Now.ToString("yyyyMMdd");

                string polarity = GetPolarityFromPath(imagePath);
                if (string.IsNullOrEmpty(polarity)) polarity = "未知极性";
                string cellName = GetCellNameFromPath(imagePath);
                if (string.IsNullOrEmpty(cellName)) cellName = "未知电芯";
                string cameraInfo = GetCameraInfoFromPath(imagePath);
                if (string.IsNullOrEmpty(cameraInfo)) cameraInfo = "未知相机";

                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                if (string.IsNullOrEmpty(fileName)) fileName = "未知文件";
                string fileExt = Path.GetExtension(imagePath);
                if (string.IsNullOrEmpty(fileExt)) fileExt = ".jpg";

                string imageType = isResultImage ? "结果图" : "原图";
                string newFileName = isResultImage
                    ? $"{cellName}_{fileName}_{result}{fileExt}"
                    : $"{cellName}_{fileName}{fileExt}";

                //路径拼接
                List<string> pathParts = new List<string>
                {
                    rootDirectory, "IME参数管控", currentDate, dateFolder,
                    imageType, result, polarity, cameraInfo, newFileName
                };

                // 在线模式插入工位
                if (!isOffline)
                {
                    string station = GetStationFromPath(imagePath) + "工位";
                    if (string.IsNullOrEmpty(station)) station = "未知工位";
                    pathParts.Insert(3, station);
                }

                // 过滤空参数
                pathParts = pathParts.Where(part => !string.IsNullOrEmpty(part)).ToList();
                if (pathParts.Count == 0)
                {
                    AddLog("[错误] 路径片段为空，无法拼接");
                    return null;
                }

                string savePath = Path.GetFullPath(Path.Combine(pathParts.ToArray()));
                //AddLog($"[路径构造成功] {imageType}路径：{savePath}");
                return savePath;
            }
            catch (Exception ex)
            {
                AddLog($"[路径构造失败] {ex.Message}");
                return null;
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

            switch (SelectedMode)
            {
                case DetectionMode.OK:
                    // 如果选了OK模式，路径必须包含 "OK"
                    return pathUpper.Contains("OK");

                case DetectionMode.NG:
                    // 如果选了NG模式，路径必须包含 "NG"
                    return pathUpper.Contains("NG");

                case DetectionMode.全检:
                    return true;
                default:
                    // 默认拦截，防止未知模式
                    return false;
            }
        }

        /// <summary>
        /// 从路径提取正负极
        /// </summary>
        private string GetPolarityFromPath(string imagePath, bool isOnline = false)
        {
            // 先查缓存
            if (_polarityCache.TryGetValue(imagePath, out string? cached))
                return cached;

            try
            {              
                if (isOnline)
                {
                    if (imagePath.Contains("大面检测"))
                    {
                        if (imagePath.Contains("正极"))
                        {
                            return "正极";
                        }
                        else
                        {
                            return "负极";
                        }
                    }
                    else
                    {
                        return "未知极性";
                    }
                }

                if (imagePath.Contains("正极"))
                {
                    return "正极";
                }
                else
                {
                    return "负极";
                }
            }
            catch (Exception ex)
            {
                AddLog($"[极性提取失败] {ex.Message}，路径：{imagePath}");
                _polarityCache[imagePath] = "极性解析失败";
                return "极性解析失败";
            }
        }
        /// <summary>
        /// 从路径提取相机信息
        /// </summary>
        private string GetCameraInfoFromPath(string imagePath)
        {
            //先查缓存，命中直接返回
            if (_cameraInfoCache.TryGetValue(imagePath, out string? cachedInfo))
                return cachedInfo;

            try
            {
                string? dirPath = Path.GetDirectoryName(imagePath);
                if (dirPath == null)
                {
                    string error = "异常（无法获取图片目录）";
                    _cameraInfoCache.TryAdd(imagePath, error);
                    AddLog($"[错误] 无法获取图片目录: {imagePath}");
                    return error;
                }

                string cameraDirName = Path.GetFileName(dirPath);
                string result = !string.IsNullOrEmpty(cameraDirName) ? cameraDirName : "未知相机";
                _cameraInfoCache.TryAdd(imagePath, result);
                return result;
            }
            catch (Exception ex)
            {
                string error = "相机解析失败";
                _cameraInfoCache.TryAdd(imagePath, error);
                AddLog($"[相机信息提取失败] {ex.Message}，路径：{imagePath}");
                return error;
            }
        }
        /// <summary>
        /// 从路径提取相机编号
        /// </summary>
        private string GetCameraNumberFromPath(string imagePath)
        {
            string cameraInfo = GetCameraInfoFromPath(imagePath);

            //查相机编号缓存
            if (_cameraNumberCache.TryGetValue(cameraInfo, out string? cachedNumber))
                return cachedNumber;

            //处理异常情况
            if (string.IsNullOrEmpty(cameraInfo) || cameraInfo.StartsWith("未知"))
            {
                _cameraNumberCache.TryAdd(cameraInfo, cameraInfo);
                return cameraInfo;
            }

            //提取数字编号
            string numberPart = new string(cameraInfo.Where(char.IsDigit).ToArray());
            string result = string.IsNullOrEmpty(numberPart) ? cameraInfo : numberPart;

            //线程安全添加缓存
            _cameraNumberCache.TryAdd(cameraInfo, result);
            return result;
        }

        /// <summary>
        /// 从路径提取电芯号
        /// </summary>
        private string GetCellNameFromPath(string imagePath)
        {
            try
            {
                string? dirPath = Path.GetDirectoryName(imagePath);
                if (dirPath == null)
                {
                    AddLog($"[错误] 无法获取图片目录: {imagePath}");
                    return "异常（无法获取图片目录）";
                }

                DirectoryInfo currentDir = new DirectoryInfo(dirPath);

                // 向上查找纯数字目录（电芯号）
                DirectoryInfo? tempDir = currentDir;
                while (tempDir != null)
                {
                    if (tempDir.Name.All(char.IsDigit))
                    {
                        return tempDir.Name;
                    }
                    tempDir = tempDir.Parent;
                }

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
                string polarity = GetPolarityFromPath(imagePath);
                string camera = GetCameraNumberFromPath(imagePath);
                string imageName = Path.GetFileName(imagePath);

                // 离线模式复用用在线相同的工位获取逻辑
                string station = GetStationFromPath(imagePath);

                var dataLine = new[] { date, station, shift, cellCode, polarity, camera, imageName, detectionResult };
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
                string? dirPath = Path.GetDirectoryName(imagePath);
                if (dirPath == null)
                {
                    AddLog($"[错误] 无法获取图片目录: {imagePath}");
                    return "异常（无法获取图片目录）";
                }

                DirectoryInfo currentDir = new DirectoryInfo(dirPath);

                // 层级：相机 → 正负极 → 电芯号 → 班次
                DirectoryInfo cameraDir = currentDir;
                DirectoryInfo? polarityDir = cameraDir?.Parent;
                DirectoryInfo? cellDir = polarityDir?.Parent;
                DirectoryInfo? shiftDir = cellDir?.Parent;

                if (shiftDir != null)
                {
                    string shiftName = shiftDir.Name;
                    if (shiftName == "白班" || shiftName == "晚班")
                    {
                        return shiftName;
                    }
                    return $"{shiftName}（非标准班次名）";
                }
                return "未知班次";
            }
            catch (Exception ex)
            {
                AddLog($"[班次提取失败] {ex.Message}，路径：{imagePath}");
                return "班次解析失败";
            }
        }

        /// <summary>
        /// 根据当前路径判断所属工位
        /// </summary>
        /// <param name="currentPath">需要判断的文件/文件夹路径</param>
        private string GetStationFromPath(string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath))
            {
                AddLog("[工位识别警告] 传入路径为空，无法判断工位");
                return "未知";
            }

            try
            {
                string normalizedPath = currentPath.Replace("：", ":").Replace("/", "\\");
                normalizedPath = Path.GetFullPath(normalizedPath);

                // 结构：.../虚拟码/工位/极性/文件名.jpg
                try
                {
                    FileInfo fileInfo = new FileInfo(normalizedPath);
                    DirectoryInfo? polarityDir = fileInfo.Directory; // 获取“负极”文件夹

                    if (polarityDir != null)
                    {
                        // 校验上一级是否是极性文件夹（包含"正极"或"负极"），以此定位
                        if (polarityDir.Name.Contains("极"))
                        {
                            DirectoryInfo? stationDir = polarityDir.Parent; // 获取“大面检测”文件夹
                            if (stationDir != null)
                            {
                                string extractedStation = stationDir.Name;
                                return extractedStation; // 直接返回 "大面检测"
                            }
                        }

                    }
                }
                catch
                {
                    AddLog($"[工位识别错误] 路径解析失败，采用兜底策略");
                }

                string normalizedA = string.IsNullOrEmpty(SelectedMonitorFolderPathA) ? "" : Path.GetFullPath(SelectedMonitorFolderPathA);
                string normalizedB = string.IsNullOrEmpty(SelectedMonitorFolderPathB) ? "" : Path.GetFullPath(SelectedMonitorFolderPathB);

                bool isOnlineA = !string.IsNullOrEmpty(normalizedA) && normalizedPath.StartsWith(normalizedA, StringComparison.OrdinalIgnoreCase);
                bool isOnlineB = !string.IsNullOrEmpty(normalizedB) && normalizedPath.StartsWith(normalizedB, StringComparison.OrdinalIgnoreCase);

                if (isOnlineA) return "A"; // 或者根据业务需求，将 A 映射为 "大面检测"
                if (isOnlineB) return "B";

                AddLog($"[工位识别警告] 路径不符合目录结构且未匹配监控文件夹：{currentPath}");
                return "未知";
            }
            catch (Exception ex)
            {
                AddLog($"[工位识别错误] 路径处理失败：{ex.Message}（路径：{currentPath}）");
                return "未知";
            }
        }

        private bool _isThreadRunningA = false;

        /// <summary>
        /// 从JSON配置文件加载程序所有配置项（监控路径、筛选规则、统计数据等）
        /// 核心设计：文件存在性校验→JSON反序列化→类型安全解析→数值范围限制→线程安全更新统计→异常容错
        /// </summary>
        private void LoadConfig()
        {
            // 前置校验：配置文件不存在时直接返回，避免读取不存在文件的异常
            if (!File.Exists(_configFilePath)) return;

            try
            {
                // 1. 读取配置文件的JSON字符串（同步读取，适合小体积配置文件）
                string json = File.ReadAllText(_configFilePath);
                // 2. 反序列化为字典（灵活适配配置项，无需定义强类型类；key=配置项名称，value=配置值）
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                // 监控路径
                // 3. 解析字符串类型配置：监控文件夹路径（A/B工位）
                // TryGetValue：避免键不存在导致的KeyNotFoundException；?.ToString()：避免值为null的空引用
                if (dict != null && dict.TryGetValue("SelectedMonitorFolderPathA", out var pathA))
                    SelectedMonitorFolderPathA = pathA?.ToString() ?? string.Empty;
                if (dict != null && dict.TryGetValue("SelectedMonitorFolderPathB", out var pathB))
                    SelectedMonitorFolderPathB = pathB?.ToString() ?? string.Empty;

                if (dict != null && dict.TryGetValue("IsOnlineDetectionEnabledA", out var enableA)
                    && bool.TryParse(enableA?.ToString(), out bool isEnabledA))
                {
                    // 赋值给属性会触发 Setter，自动执行 StartOnlineMonitoringA
                    IsOnlineDetectionEnabledA = isEnabledA;
                }

                if (dict != null && dict.TryGetValue("IsOnlineDetectionEnabledB", out var enableB)
                    && bool.TryParse(enableB?.ToString(), out bool isEnabledB))
                {
                    IsOnlineDetectionEnabledB = isEnabledB;
                }

                // 工位选择
                // 4. 解析布尔类型配置：工位筛选规则（正极/负极）
                // 双层校验：先取到值→再尝试解析为bool，避免类型不匹配导致的格式异常
                if (dict != null && dict.TryGetValue("IsPositive", out var positive) && bool.TryParse(positive?.ToString(), out bool isPositiveValue))
                    IsPositive = isPositiveValue;
                if (dict != null && dict.TryGetValue("IsNegative", out var negative) && bool.TryParse(negative?.ToString(), out bool isNegativeValue))
                    IsNegative = isNegativeValue;

                // 5. 解析加载解析模式
                if (dict != null && dict.TryGetValue("SelectedMode", out var modeValue))
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

                // 6. 解析布尔类型配置：结果图片保存规则（OK/NG结果/原始图片）
                if (dict != null && dict.TryGetValue("IsSaveResultOK", out var saveresultOk) && bool.TryParse(saveresultOk?.ToString(), out bool isSaveResultOKValue))
                    IsSaveResultOK = isSaveResultOKValue;
                if (dict != null && dict.TryGetValue("IsSaveResultNG", out var saveresultNg) && bool.TryParse(saveresultNg?.ToString(), out bool isSaveResultNGValue))
                    IsSaveResultNG = isSaveResultNGValue;
                if (dict != null && dict.TryGetValue("IsSaveOriginalOK", out var saveoriginalOk) && bool.TryParse(saveoriginalOk?.ToString(), out bool isSaveOriginalOKValue))
                    IsSaveOriginalOK = isSaveOriginalOKValue;
                if (dict != null && dict.TryGetValue("IsSaveOriginalNG", out var saveoriginalNg) && bool.TryParse(saveoriginalNg?.ToString(), out bool isSaveOriginalNGValue))
                    IsSaveOriginalNG = isSaveOriginalNGValue;

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
                // 9. 解析历史统计数据：总检测数/OK数/NG数（先存入历史变量，后续统一更新）
                if (dict != null && dict.TryGetValue("TotalNeDetectionCount", out var totalCount))
                {
                    if (int.TryParse(totalCount.ToString(), out int totalCountValue))
                    {
                        _historyTotalCount = totalCountValue;
                    }
                }
                if (dict != null && dict.TryGetValue("OKCount", out var okCount))
                {
                    if (int.TryParse(okCount.ToString(), out int okCountValue))
                    {
                        _historyOKCount = okCountValue;
                    }
                }
                if (dict != null && dict.TryGetValue("NGCount", out var ngCount))
                {
                    if (int.TryParse(ngCount.ToString(), out int ngCountValue))
                    {
                        _historyNGCount = ngCountValue;
                    }
                }
                // 加载历史OK率
                if (dict != null && dict.TryGetValue("OKRate", out var okRate))
                {
                    if (double.TryParse(okRate.ToString(), out double okRateValue))
                    {
                        OKRate = okRateValue;
                    }
                }

                // 加载完成后，先用历史值更新UI
                // 11. 线程安全更新统计数据到UI绑定属性
                // lock保证多线程下统计数据的原子更新（避免读取配置时其他线程修改统计值）
                lock (_statisticsLock)
                {
                    TotalNeDetectionCount = _historyTotalCount;
                    OKCount = _historyOKCount;
                    NGCount = _historyNGCount;
                }
                // 12. 记录加载日志：便于追溯配置加载结果，排查数据异常
                AddLog($"[统计加载] 历史统计数据：总检测数={_historyTotalCount}，OK数={_historyOKCount}，NG数={_historyNGCount}");
            }
            catch (Exception ex)
            {
                AddLog($"加载配置失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfig()
        {
            try
            {
                // 构建匿名对象，仅包含需要保存的属性
                // 1. 构建匿名对象封装需要持久化的配置属性
                // 优势：快速聚合需保存的属性，无需定义额外的配置实体类；属性名与原变量名一致，序列化后JSON字段名直观
                // 注意：仅包含业务需要保存的核心配置，避免冗余数据
                var configData = new
                {
                    SelectedMonitorFolderPathA,
                    SelectedMonitorFolderPathB,
                    IsOnlineDetectionEnabledA,
                    IsOnlineDetectionEnabledB,
                    IsPositive,
                    IsNegative,
                    IsCamera1,
                    IsCamera2,
                    IsCamera3,
                    IsCamera4,
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
                    SelectedMode = _selectedMode.ToString()
                };

                // 2. 将匿名对象序列化为格式化的JSON字符串
                // JsonSerializerOptions { WriteIndented = true }：启用缩进格式化，生成的JSON易读（调试/人工修改友好）
                // 若不设置此参数，JSON会压缩为一行，节省空间但可读性差
                string json = System.Text.Json.JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });
                // 3. 将JSON字符串写入配置文件
                // File.WriteAllText：自动处理文件创建/覆盖逻辑（文件不存在则创建，存在则覆盖）
                // 注意：该方法会一次性写入所有内容，适合小体积配置文件（配置文件通常几KB，无性能问题）
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
                bool isStationBEnabled = _isOnlineDetectionEnabledB;
                bool bothStopped = !isStationAEnabled && !isStationBEnabled;

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
                        GenerateReport().Wait();
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
                        Polarity = GetPolarityFromPath(imagePath),
                        Camera = GetCameraNumberFromPath(imagePath),
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
            lock (_queue1Lock)
            {
                // 若离线检测已完成，跳过超时生成
                if (_isOfflineMode && _isOfflineDetectionCompleted)
                {
                    AddLog("[离线检测已完成] 跳过超时报表生成");
                    return;
                }

                if (_cellCodeQueue.Count > 0)
                {
                    AddLog($"[超时触发] 达到 {ReportTimeoutMinutes} 分钟超时时间，处理当前电芯组");
                    GenerateReport().Wait();

                    // 重新启动定时器
                    if (_cellCodeQueue.Count > 0 && _reportTimer != null)
                    {
                        _reportTimer.Start();
                        AddLog($"[定时器重启] 重新开始 {ReportTimeoutMinutes} 分钟计时");
                    }
                }
                else
                {
                    AddLog($"[超时检查] 达到 {ReportTimeoutMinutes} 分钟超时时间，但队列为空，无需生成报表");
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
        private async Task GenerateReport()
        {
            // 获取当前组的电芯编码
            List<string> groupCellCodes = new List<string>();
            List<string> ngCellCodesInGroup = new List<string>();

            lock (_queue1Lock)
            {
                // 获取当前组的所有电芯编码
                int count = Math.Min(MaxImagesPerReport, _cellCodeQueue.Count);
                for (int i = 0; i < count; i++)
                {
                    string cellCode = _cellCodeQueue.Dequeue();
                    groupCellCodes.Add(cellCode);
                    _cellCodeSet.Remove(cellCode);

                    // 检查该电芯是否有NG图片
                    if (_ngImageDict.ContainsKey(cellCode))
                    {
                        ngCellCodesInGroup.Add(cellCode);
                    }
                }

                if (groupCellCodes.Count == 0)
                {
                    AddLog("[报表生成] 没有电芯编码需要生成报表");
                    return;
                }

                AddLog($"[电芯组处理] 处理 {groupCellCodes.Count} 个电芯，其中 {ngCellCodesInGroup.Count} 个有NG图片");

                // 如果组内没有NG电芯，直接返回，不生成报表
                if (ngCellCodesInGroup.Count == 0)
                {
                    AddLog($"[报表生成] 当前电芯组没有NG电芯，跳过报表生成");
                    return;
                }
            }

            // 收集所有要生成报表的NG图片
            List<NgImageInfo> imagesToReport = new List<NgImageInfo>();
            foreach (string cellCode in ngCellCodesInGroup)
            {
                if (_ngImageDict.ContainsKey(cellCode))
                {
                    //仅取每个电芯的前5张NG图
                    var allNgImages = _ngImageDict[cellCode];
                    var takeImages = allNgImages.Take(5).ToList(); // 限制前5张
                    imagesToReport.AddRange(takeImages);

                    // 日志提示：超过5张的部分被截断
                    if (allNgImages.Count > 5)
                    {
                        AddLog($"[报表图片截断] 电芯 {cellCode} 共有 {allNgImages.Count} 张NG图，仅保留前5张到报表");
                    }
                }
            }

            if (imagesToReport.Count == 0)
            {
                AddLog("[报表生成] 没有NG图片需要生成报表");
                return;
            }

            AddLog($"[报表生成] 开始生成报表，组包含 {groupCellCodes.Count} 个电芯，其中 {ngCellCodesInGroup.Count} 个NG电芯，共 {imagesToReport.Count} 张NG图片");

            // 报表路径与日期
            string baseReportDir = Path.Combine("D:", "IME复判", "检测报表");
            string modeSubDir = _isOfflineMode ? "离线检测报表" : "在线检测报表";
            string dateFolderName;
            string finalReportDir;

            if (_isOfflineMode)
            {
                DateTime offlineRunDate = _offlineStartTime.HasValue
                    ? _offlineStartTime.Value.Date
                    : DateTime.Now.Date;
                dateFolderName = offlineRunDate.ToString("yyyy-MM-dd");

            }
            else
            {
                dateFolderName = DateTime.Now.ToString("yyyy-MM-dd");
            }

            finalReportDir = Path.Combine(baseReportDir, modeSubDir, dateFolderName);
            Directory.CreateDirectory(finalReportDir);

            int maxIndex = GetMaxReportIndex(finalReportDir); // 获取文件夹内最大序号
            int startIndex = maxIndex + 1; // 新报表序号 = 最大序号 + 1
            // 报表文件名与保存路径
            string reportNo = DateTime.Now.ToString("yyyyMMddHHmmss");
            string baseFileName = $"PTO格林晟叠片缺陷检测报告_{reportNo}.pdf";
            string fileName = $"{startIndex}.{baseFileName}";
            string savePath = Path.Combine(finalReportDir, fileName);


            string productionDate = _isOfflineMode
                 ? GetDateFromPath(imagesToReport[0].ImagePath!)
                 : DateTime.Now.ToString("yyyy-MM-dd");

            // 处理文件名冲突
            if (File.Exists(savePath))
            {
                AddLog($"[文件名冲突] 文件已存在：{fileName}，尝试递增序号");
                int counter = startIndex + 1;
                do
                {
                    fileName = $"{counter}.{baseFileName}";
                    savePath = Path.Combine(finalReportDir, fileName);
                    counter++;
                } while (File.Exists(savePath) && counter <= 999);
                AddLog($"[文件名解决] 使用新文件名：{Path.GetFileName(savePath)}");
            }

            try
            {
                // 生成PDF
                using (var writer = new PdfWriter(savePath))
                using (var pdfDoc = new PdfDocument(writer))
                using (var document = new Document(pdfDoc, iText.Kernel.Geom.PageSize.A4))
                {
                    // 中文字体设置
                    PdfFont chineseFont = PdfFontFactory.CreateFont(
                        "STSong-Light", "UniGB-UCS2-H", PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
                    document.SetFont(chineseFont);
                    document.SetMargins(30, 30, 30, 30);

                    // 全局样式
                    var gridBorder = new iText.Layout.Borders.SolidBorder(0.5f);
                    var headerBg = iText.Kernel.Colors.ColorConstants.LIGHT_GRAY;

                    // 主表格
                    var mainTable = new iText.Layout.Element.Table(1).UseAllAvailableWidth();
                    mainTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                    // 标题区块
                    var titleSubTable = new iText.Layout.Element.Table(1).UseAllAvailableWidth();
                    titleSubTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                    titleSubTable.AddCell(new iText.Layout.Element.Cell()
                        .Add(new iText.Layout.Element.Paragraph("PTO叠片缺陷检测报告")
                            .SetFontSize(15)
                            .SetBold()
                            .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER))
                        .SetBorderTop(gridBorder)
                        .SetBorderRight(gridBorder)
                        .SetBorderLeft(gridBorder)
                        .SetBorderBottom(gridBorder));

                    titleSubTable.AddCell(new iText.Layout.Element.Cell()
                        .Add(new iText.Layout.Element.Paragraph($"报表编号：{reportNo}")
                            .SetFontSize(12)
                            .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT))
                        .SetBorderTop(gridBorder)
                        .SetBorderRight(gridBorder)
                        .SetBorderLeft(gridBorder)
                        .SetBorderBottom(gridBorder));

                    mainTable.AddCell(new iText.Layout.Element.Cell().Add(titleSubTable)
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(0));

                    // 基本信息区块
                    var infoSubTable = new iText.Layout.Element.Table(UnitValue.CreatePercentArray(new float[] { 1, 3 })).UseAllAvailableWidth();
                    infoSubTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                    // 手动添加第一行
                    infoSubTable.AddCell(new iText.Layout.Element.Cell()
                        .Add(new iText.Layout.Element.Paragraph("检测设备：").SetBold())
                        .SetBackgroundColor(headerBg)
                        .SetBorderTop(iText.Layout.Borders.Border.NO_BORDER)
                        .SetBorderRight(gridBorder)
                        .SetBorderLeft(gridBorder)
                        .SetBorderBottom(gridBorder)
                        .SetPadding(5));
                    infoSubTable.AddCell(new iText.Layout.Element.Cell()
                        .Add(new iText.Layout.Element.Paragraph("格林晟叠片"))
                        .SetBorderTop(iText.Layout.Borders.Border.NO_BORDER)
                        .SetBorderRight(gridBorder)
                        .SetBorderLeft(gridBorder)
                        .SetBorderBottom(gridBorder)
                        .SetPadding(5));

                    AddInfoRowToSubTable(infoSubTable, gridBorder, headerBg, "生产日期：", productionDate);
                    AddInfoRowToSubTable(infoSubTable, gridBorder, headerBg, "报表生成时间：", DateTime.Now.ToString("yyyy-MM-dd HH: mm: ss"));
                    AddInfoRowToSubTable(infoSubTable, gridBorder, headerBg, "模式：", _isOfflineMode ? "离线检测" : "在线检测");

                    // 显示组信息和NG电芯信息
                    string groupInfo = $"本组共 {groupCellCodes.Count} 个电芯，其中 {ngCellCodesInGroup.Count} 个NG电芯，共 {imagesToReport.Count} 张NG图片";
                    var groupInfoCell = new iText.Layout.Element.Cell()
                        .Add(new iText.Layout.Element.Paragraph("当前报表内容：").SetBold())
                        .SetBackgroundColor(headerBg)
                        .SetBorder(gridBorder)
                        .SetBorderBottom(gridBorder)
                        .SetPadding(5);
                    infoSubTable.AddCell(groupInfoCell);
                    infoSubTable.AddCell(new iText.Layout.Element.Cell()
                        .Add(new iText.Layout.Element.Paragraph(groupInfo))
                        .SetBorder(gridBorder)
                        .SetBorderBottom(gridBorder)
                        .SetPadding(5));

                    //NG电芯编码行
                    string cellCodesString = string.Join("、", ngCellCodesInGroup);
                    var ngCellCodesCell = new iText.Layout.Element.Cell()
                        .Add(new iText.Layout.Element.Paragraph("NG电芯编码：").SetBold())
                        .SetBackgroundColor(headerBg)
                        .SetBorder(gridBorder)
                        .SetBorderBottom(gridBorder)
                        .SetPadding(5);
                    infoSubTable.AddCell(ngCellCodesCell);
                    infoSubTable.AddCell(new iText.Layout.Element.Cell()
                        .Add(new iText.Layout.Element.Paragraph(cellCodesString))
                        .SetBorder(gridBorder)
                        .SetBorderBottom(gridBorder)
                        .SetPadding(5));

                    mainTable.AddCell(new iText.Layout.Element.Cell().Add(infoSubTable)
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(0));

                    // NG缺陷详情区块
                    var detailSubTable = new iText.Layout.Element.Table(1).UseAllAvailableWidth();
                    detailSubTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                    detailSubTable.AddCell(new iText.Layout.Element.Cell()
                        .Add(new iText.Layout.Element.Paragraph("NG缺陷图片详情")
                            .SetFontSize(14)
                            .SetBold()
                            .SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT))
                        .SetBorderTop(iText.Layout.Borders.Border.NO_BORDER)
                        .SetBorderRight(gridBorder)
                        .SetBorderLeft(gridBorder)
                        .SetBorderBottom(iText.Layout.Borders.Border.NO_BORDER)
                        .SetPadding(5));

                    // 详情数据表格
                    var detailDataTable = new iText.Layout.Element.Table(UnitValue.CreatePercentArray(new float[] { 1, 3, 6 })).UseAllAvailableWidth();
                    detailDataTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                    // 详情表头
                    detailDataTable.AddCell(CreateHeaderCell("序号", gridBorder, headerBg));
                    detailDataTable.AddCell(CreateHeaderCell("缺陷结果图", gridBorder, headerBg));
                    detailDataTable.AddCell(CreateHeaderCell("参数信息", gridBorder, headerBg));

                    // 按电芯编码分组显示图片，序号按电芯编码计数
                    int cellIndex = 1;
                    foreach (string cellCode in ngCellCodesInGroup)
                    {
                        if (!_ngImageDict.ContainsKey(cellCode))
                        {
                            AddLog($"[报表生成警告] 电芯编码 {cellCode} 在字典中不存在");
                            continue;
                        }

                        var cellImages = _ngImageDict[cellCode].Take(5).ToList(); // 仅取前5张
                        bool isFirstImageForCell = true;

                        foreach (var imgInfo in cellImages)
                        {
                            // 序号 - 每个电芯编码的第一个图片显示序号，后续图片不显示
                            if (isFirstImageForCell)
                            {
                                detailDataTable.AddCell(CreateDataCell($"{cellIndex}", gridBorder)
                                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                                    .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE));
                                isFirstImageForCell = false;
                            }
                            else
                            {
                                detailDataTable.AddCell(CreateDataCell("", gridBorder)
                                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                                    .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE));
                            }

                            // 缺陷结果图
                            iText.Layout.Element.Cell imageCell = new iText.Layout.Element.Cell()
                                .SetBorder(gridBorder)
                                .SetPadding(5)
                                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                                .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE);

                            try
                            {
                                string? resultImagePath = GetResultImagePath(imgInfo.ImagePath!, "NG", true);
                                if (File.Exists(resultImagePath))
                                {
                                    ImageData imageData = ImageDataFactory.Create(resultImagePath);
                                    iText.Layout.Element.Image image = new iText.Layout.Element.Image(imageData)
                                        .ScaleToFit(150, 120)
                                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);
                                    imageCell.Add(image);
                                }
                                else
                                {
                                    imageCell.Add(new iText.Layout.Element.Paragraph("结果图不存在").SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                                    AddLog($"[报表图片警告] 结果图不存在：{resultImagePath}");
                                }
                            }
                            catch (Exception ex)
                            {
                                imageCell.Add(new iText.Layout.Element.Paragraph($"加载失败：{ex.Message}").SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));
                                AddLog($"[报表图片错误] 加载图片失败：{ex.Message}");
                            }
                            detailDataTable.AddCell(imageCell);

                            // 参数信息 - 每个图片都显示完整的电芯编码和其他信息
                            var paramContainer = new iText.Layout.Element.Div().SetWidth(UnitValue.CreatePercentValue(80));
                            string stationText = string.IsNullOrEmpty(imgInfo.Station) ? "未知" : imgInfo.Station;


                            // 每个图片都显示电芯编码
                            paramContainer.Add(new iText.Layout.Element.Paragraph($"电芯编码：{imgInfo.CellCode}").SetBold().SetMarginBottom(0));
                            paramContainer.Add(new iText.Layout.Element.Paragraph($"班次：{imgInfo.Shift}").SetMarginBottom(0));
                            paramContainer.Add(new iText.Layout.Element.Paragraph($"正负极：{imgInfo.Polarity}").SetMarginBottom(0));
                            paramContainer.Add(new iText.Layout.Element.Paragraph($"相机：{imgInfo.Camera}").SetMarginBottom(0));
                            paramContainer.Add(new iText.Layout.Element.Paragraph($"工位：{stationText}").SetMarginBottom(0));
                            paramContainer.Add(new iText.Layout.Element.Paragraph($"检测时间：{imgInfo.DetectionTime:yyyy-MM-dd HH: mm: ss}").SetMarginBottom(0));

                            string? fullImageName = imgInfo.ImageName;
                            if (fullImageName == null)
                            {
                                throw new ArgumentNullException(nameof(fullImageName), "图片文件名不能为空");
                            }
                            int extIndex = fullImageName.LastIndexOf('.');
                            string fileNamePart = extIndex > 0 ? fullImageName.Substring(0, extIndex) : fullImageName;
                            string extWithDot = extIndex > 0 ? fullImageName.Substring(extIndex) : "";

                            string processedExt = extWithDot;
                            if (extWithDot.Length >= 3)
                            {
                                processedExt = $"{extWithDot[0]}{extWithDot[1]} {extWithDot.Substring(2)}";
                            }

                            paramContainer.Add(new iText.Layout.Element.Paragraph($"图片名：{fileNamePart}{processedExt}")
                                .SetFontSize(10));
                            paramContainer.SetFontSize(10);

                            var paramCell = new iText.Layout.Element.Cell()
                                .Add(paramContainer)
                                .SetBorder(gridBorder)
                                .SetPadding(5)
                                .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.TOP)
                                .SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT);
                            detailDataTable.AddCell(paramCell);
                        }

                        // 处理完一个电芯编码的所有图片后，序号递增
                        cellIndex++;
                    }

                    detailSubTable.AddCell(new iText.Layout.Element.Cell().Add(detailDataTable)
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(0));
                    mainTable.AddCell(new iText.Layout.Element.Cell().Add(detailSubTable)
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(0));

                    // 将主表格添加到文档
                    document.Add(mainTable);
                }

                // 报表生成完成后，从字典中移除已处理的NG电芯编码
                lock (_queue1Lock)
                {
                    foreach (string cellCode in ngCellCodesInGroup)
                    {
                        if (_ngImageDict.ContainsKey(cellCode))
                        {
                            _ngImageDict.Remove(cellCode);
                        }
                    }
                }
                try
                {
                    AddLog($"[报表推送] 开始推送报表：{savePath}");
                    string combinedUserId = GetCombinedUserIdString();
                    //await MessagePushHelper.PushFileAsync(savePath, combinedUserId);// 后续需确认上传的id信息有哪些，可以配置到数据库中

                    var response = await _webApiClient.PushFileAsync(savePath);
                    AddLog($"[报表推送成功] 报表已推送");
                }
                catch (Exception pushEx)
                {
                    // 推送失败不影响报表生成，仅记录日志
                    AddLog($"[报表推送失败] 推送报表 {savePath} 失败：{pushEx.Message}");
                    AddLog($"[报表推送失败] 堆栈跟踪：{pushEx.StackTrace}");
                }

                AddLog($"[报表生成成功] 本组包含 {groupCellCodes.Count} 个电芯，其中 {ngCellCodesInGroup.Count} 个NG电芯，共 {imagesToReport.Count} 张NG图片，保存路径：{savePath}");
            }
            catch (Exception ex)
            {
                AddLog($"[报表生成错误] 生成报表失败：{ex.Message}");
                AddLog($"[报表生成错误] 堆栈跟踪：{ex.StackTrace}");
            }
        }

        private void AddInfoRowToSubTable(iText.Layout.Element.Table table, iText.Layout.Borders.Border border, iText.Kernel.Colors.Color bgColor, string label, string value)
        {
            // 标签单元格 - 统一设置所有边框
            table.AddCell(new iText.Layout.Element.Cell()
                .Add(new iText.Layout.Element.Paragraph(label).SetBold())
                .SetBackgroundColor(bgColor)
                .SetBorder(border)
                .SetPadding(5));

            // 值单元格 - 统一设置所有边框
            table.AddCell(new iText.Layout.Element.Cell()
                .Add(new iText.Layout.Element.Paragraph(value))
                .SetBorder(border)
                .SetPadding(5));
        }

        //表头单元格创建
        private iText.Layout.Element.Cell CreateHeaderCell(string text, iText.Layout.Borders.Border border, iText.Kernel.Colors.Color bgColor)
        {
            return new iText.Layout.Element.Cell()
                .Add(new iText.Layout.Element.Paragraph(text)
                    .SetBold()
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER))
                .SetBackgroundColor(bgColor)
                .SetBorder(border)
                .SetPadding(5);
        }

        //数据单元格创建
        private iText.Layout.Element.Cell CreateDataCell(string text, iText.Layout.Borders.Border border)
        {
            return new iText.Layout.Element.Cell()
                .Add(new iText.Layout.Element.Paragraph(text)
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER))
                .SetBorder(border)
                .SetPadding(5);
        }

        #region 参数管控
        // 参数管控新增命令
        public ICommand AddRow { get; }
        public ICommand DeleteRow { get; }
        public ICommand SaveData { get; }

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
                Directory.CreateDirectory(ConfigFolder);

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
                                                                 // 或者直接使用 Add 并让集合通知
                            DefectDataGrid.Add(item);
                        }
                        AddLog($"从 {DefectFilePath} 成功加载 {DefectDataGrid.Count} 条缺陷参数");
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
                AddLog($"删除行失败：无效的删除项（{item?.DefectName ?? "空项"}）",CoreLogLevel.Warn) ;
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
