
using HalconDotNet;
using HandyControl.Controls;
using Microsoft.Win32;
using OpenCvSharp;
using OxyPlot;
using OxyPlot.Series;
//using ScottPlot;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;

using SUNWODA_SEVB.ViewModels.Pages.Common;
using SUNWODA_SEVB.ViewModels.Pages.Demo;
using SUNWODA_SEVB.WEB.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
//using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
//using TabRedundancyCore;
using Yolo8Core;
using static Yolo8Core.BaseClass;

namespace SUNWODA_SEVB.ViewModels.Pages.JRRY
{
    [Module("VM_EarTabRedundancy", "极耳冗余 AI检测系统v1.0")]
    class VM_EarTabRedundancyPage : ViewModelBase
    {
        private IPredictor? H246modle;
        //TabRedDetection ai = new TabRedDetection();
        // 文件选择命令（绑定到XAML按钮）
        public ICommand File_Button { get; private set; }

        // 文件夹选择命令
        public ICommand Folder_Button { get; private set; }
        //查看结果命令
        public ICommand ViewResultCommand { get; private set; }
        //停止检测命令
        public ICommand StopDetectionCommand { get; private set; }
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
        // 选择监控文件夹路径
        public ICommand SelectMonitorFolderCommand { get; private set; }
        public ICommand SaveSettingsCommand { get; private set; }
        // 重置按钮命令
        public ICommand ResetSettingsCommand { get; private set; }
        // 鼠标按下命令（开始拖动）
        public ICommand ImageMouseDownCommand { get; }
        // 鼠标移动命令（拖动中）
        public ICommand ImageMouseMoveCommand { get; }
        // 鼠标松开命令（结束拖动）
        public ICommand ImageMouseUpCommand { get; }


        #region 属性和字段定义
        private int okCount;
        private int ngCount;
        private int count_L0;
        private int count_L1;
        private int count_L2;
        private int count_L3;
        private string ratio_L0 = "0%";
        private string ratio_L1 = "0%";
        private string ratio_L2 = "0%";
        private string ratio_L3 = "0%";
        private bool _isSaveOriginalOK = false;
        private bool _isSaveOriginalNG = false;
        private bool _isSaveResultOK = false;
        private bool _isSaveResultNG = false;
        private string? _selectedImagePath;
        private string? _selectedFolderPath;
        private ImageSource? _currentResultImage;
        private ImageSource? _currentOriginalImage;
        private const double ZoomStep = 0.1;
        private const double MinScale = 0.2;
        private const double MaxScale = 5.0;
        private double _realTimeImageScaleX = 1.0;
        private bool _isDetecting = false;
        private CancellationTokenSource? _cts;
        // 定义CSV操作的线程锁
        private readonly object _csvLock = new object();
        // 存储当前检测任务的CSV文件路径
        private string? _currentCsvFilePath;
        // 文件夹监控器
        private FileSystemWatcher? _onlineMonitorWatcher;
        // 在线检测开关状态
        private bool _isOnlineDetectionEnabled;
        // 选中的监控文件夹路径
        private string? _selectedMonitorFolderPath = "";
        // 在线检测任务队列
        private readonly Queue<string> _onlineDetectQueue = new Queue<string>();
        // 在线检测队列处理锁
        private readonly object _queueLock = new object();
        // 根据CPU核心数设置
        private const int MaxConcurrent = 4;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(MaxConcurrent);
        // 线程安全队列
        private readonly ConcurrentQueue<string> _safeDetectQueue = new ConcurrentQueue<string>();
        // 原子标记：避免多线程下状态错误
        private volatile bool _isQueueRunning = false;
        // 配置文件路径
        private string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "set_config_JRRY.json");
        private int _historyTotalCount = 0;
        private int _historyOKCount = 0;
        private int _historyNGCount = 0;
        private int _historyCountL0;
        private int _historyCountL1;
        private int _historyCountL2;
        private int _historyCountL3;
        private readonly object _statisticsLock = new object();
        // 文件就绪重试配置
        private const int FileReadyMaxRetry = 30; // 最多重试30次
        private const int FileReadyRetryDelay = 1000;
        private double _l0Upper;
        private double _l0Lower;
        private double _l1Upper;
        private double _l1Lower;
        private double _l2Upper;
        private double _l2Lower;
        private double _l3Upper;
        private double _l3Lower;
        private string? _csvCacheTempPath;
        private readonly Queue<string[]> _csvCacheQueue = new Queue<string[]>();
        private bool _isFlushingCache;


        public double L0Upper
        {
            get => _l0Upper;
            set
            {
                _l0Upper = value;
                OnPropertyChanged();
            }
        }
        public double L0Lower
        {
            get => _l0Lower;
            set
            {
                _l0Lower = value;
                OnPropertyChanged();
            }
        }
        public double L1Upper
        {
            get => _l1Upper;
            set
            {
                _l1Upper = value;
                OnPropertyChanged();
            }
        }
        public double L1Lower
        {
            get => _l1Lower;
            set
            {
                _l1Lower = value;
                OnPropertyChanged();
            }
        }
        public double L2Upper
        {
            get => _l2Upper;
            set
            {
                _l2Upper = value;
                OnPropertyChanged();
            }
        }
        public double L2Lower
        {
            get => _l2Lower;
            set
            {
                _l2Lower = value;
                OnPropertyChanged();
            }
        }
        public double L3Upper
        {
            get => _l3Upper;
            set
            {
                _l3Upper = value;
                OnPropertyChanged();
            }
        }
        public double L3Lower
        {
            get => _l3Lower;
            set
            {
                _l3Lower = value;
                OnPropertyChanged();
            }
        }

        public double _tempL0Upper;
        public double _tempL1Upper;
        public double _tempL2Upper;
        public double _tempL3Upper;
        public double _tempL0Lower;
        public double _tempL1Lower;
        public double _tempL2Lower;
        public double _tempL3Lower;
        public double TempL0Upper
        {
            get => _tempL0Upper;
            set
            {
                _tempL0Upper = value;
                OnPropertyChanged();
            }
        }
        public double TempL1Upper
        {
            get => _tempL1Upper;
            set
            {
                _tempL1Upper = value;
                OnPropertyChanged();
            }
        }
        public double TempL2Upper
        {
            get => _tempL2Upper;
            set
            {
                _tempL2Upper = value;
                OnPropertyChanged();
            }
        }
        public double TempL3Upper
        {
            get => _tempL3Upper;
            set
            {
                _tempL3Upper = value;
                OnPropertyChanged();
            }
        }
        public double TempL0Lower
        {
            get => _tempL0Lower;
            set
            {
                _tempL0Lower = value;
                OnPropertyChanged();
            }
        }
        public double TempL1Lower
        {
            get => _tempL1Lower;
            set
            {
                _tempL1Lower = value;
                OnPropertyChanged();
            }
        }
        public double TempL2Lower
        {
            get => _tempL2Lower;
            set
            {
                _tempL2Lower = value;
                OnPropertyChanged();
            }
        }
        public double TempL3Lower
        {
            get => _tempL3Lower;
            set
            {
                _tempL3Lower = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// 是否处于任何检测状态
        /// </summary>
        public bool IsInAnyDetection
        {
            get => !(IsFileDetecting || IsOnlineDetectionEnabled);
        }

        /// <summary>
        /// 阈值设置区域是否允许操作
        /// </summary>
        public bool IsThresholdSettingEnabled
        {
            get => IsInAnyDetection;
        }

        //在线检测开关是否可用
        private bool _isOnlineToggleEnabled = true;
        public bool IsOnlineToggleEnabled
        {
            get => _isOnlineToggleEnabled;
            set
            {
                _isOnlineToggleEnabled = value;
                OnPropertyChanged();
            }
        }

        //文件选择命令是否可用
        private bool _isFileSelectEnabled = true;
        public bool IsFileSelectEnabled
        {
            get => _isFileSelectEnabled;
            set
            {
                _isFileSelectEnabled = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        //文件夹选择命令是否可用
        private bool _isFolderSelectEnabled = true;
        public bool IsFolderSelectEnabled
        {
            get => _isFolderSelectEnabled;
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
                _isFileDetecting = value;
                //文件检测运行时禁用在线开关，结束时恢复
                IsOnlineToggleEnabled = !value;
                IsFileSelectEnabled = !value;
                IsFolderSelectEnabled = !value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInAnyDetection));
                OnPropertyChanged(nameof(IsThresholdSettingEnabled));
            }
        }

        /// <summary>
        /// 在线检测开关
        /// </summary>
        public bool IsOnlineDetectionEnabled
        {
            get => _isOnlineDetectionEnabled;
            set
            {
                if (_isOnlineDetectionEnabled == value) return;

                //在线检测开启逻辑
                if (value)
                {
                    // 校验监控路径
                    if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPath) || !Directory.Exists(SelectedMonitorFolderPath))
                    {
                        System.Windows.MessageBox.Show("请先选择有效的在线监控文件夹！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _isOnlineDetectionEnabled = false;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(IsThresholdSettingEnabled));
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
                OnPropertyChanged(nameof(IsThresholdSettingEnabled));
            }
        }

        /// <summary>
        /// 选中的监控文件夹路径
        /// </summary>
        public string? SelectedMonitorFolderPath
        {
            get => _selectedMonitorFolderPath;
            set
            {
                _selectedMonitorFolderPath = value;
                OnPropertyChanged();
                SaveConfig();
                // 若已开启在线检测，更新监控路径
                if (_isOnlineDetectionEnabled && _onlineMonitorWatcher != null)
                {
                    StopOnlineMonitoring();
                    StartOnlineMonitoring();

                }
            }
        }

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
        private System.Windows.Point _startMousePoint;
        private double _startTranslateX;
        private double _startTranslateY;


        //完整数据集合：存储所有检测记录
        private List<dynamic> _fullDetectionDataList = new List<dynamic>();
        // 检测数据集合/动态对象集合
        private ObservableCollection<dynamic> _detectionDataList;
        public ObservableCollection<dynamic> DetectionDataList
        {
            get => _detectionDataList;
            set { _detectionDataList = value; OnPropertyChanged(); }
        }
        // 当前显示的结果图
        public ImageSource? CurrentResultImage
        {
            get => _currentResultImage;
            set { _currentResultImage = value; OnPropertyChanged(); }
        }
        // 当前显示的原始图
        public ImageSource? CurrentOriginalImage
        {
            get => _currentOriginalImage;
            set { _currentOriginalImage = value; OnPropertyChanged(); }
        }

        public bool IsSaveOriginalOK
        {
            get => _isSaveOriginalOK;
            set { _isSaveOriginalOK = value; OnPropertyChanged(); SaveConfig(); }
        }
        public bool IsSaveOriginalNG
        {
            get => _isSaveOriginalNG;
            set { _isSaveOriginalNG = value; OnPropertyChanged(); SaveConfig(); }
        }
        public bool IsSaveResultOK
        {
            get => _isSaveResultOK;
            set { _isSaveResultOK = value; OnPropertyChanged(); SaveConfig(); }
        }
        public bool IsSaveResultNG
        {
            get => _isSaveResultNG;
            set { _isSaveResultNG = value; OnPropertyChanged(); SaveConfig(); }
        }
        // L0 计数（绑定UI）
        public int Count_L0
        {
            get => count_L0;
            set { count_L0 = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_L1
        {
            get => count_L1;
            set { count_L1 = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_L2
        {
            get => count_L2;
            set { count_L2 = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_L3
        {
            get => count_L3;
            set { count_L3 = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public string Ratio_L0
        {
            get => ratio_L0;
            set { ratio_L0 = value; OnPropertyChanged(); }
        }
        public string Ratio_L1
        {
            get => ratio_L1;
            set { ratio_L1 = value; OnPropertyChanged(); }
        }
        public string Ratio_L2
        {
            get => ratio_L2;
            set { ratio_L2 = value; OnPropertyChanged(); }
        }
        public string Ratio_L3
        {
            get => ratio_L3;
            set { ratio_L3 = value; OnPropertyChanged(); }
        }



        // 日志集合：用Tuple<string, string>存储（Item1=时间戳，Item2=内容）
        private ObservableCollection<Tuple<string, string>> _logTable;
        public ObservableCollection<Tuple<string, string>> LogTable
        {
            get => _logTable;
            set
            {
                _logTable = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// OKNG统计饼图
        /// </summary>
        public PlotModel? PieModel { get; set; }

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
                OnPropertyChanged(nameof(TotalDetectionCount));
                OnPropertyChanged(nameof(OKRate));
                OnPropertyChanged(nameof(NGRate));
                UpdatePieChartData();
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
                OnPropertyChanged(nameof(TotalDetectionCount));
                OnPropertyChanged(nameof(OKRate));
                OnPropertyChanged(nameof(NGRate));
                UpdatePieChartData();
            }
        }
        /// <summary>
        /// 总检测数
        /// </summary>
        public int TotalDetectionCount => OKCount + NGCount;

        /// <summary>
        /// 一次优率
        /// </summary>
        public string OKRate
        {
            get
            {
                if (TotalDetectionCount == 0)
                    return "0.0%";
                double rate = (double)OKCount / TotalDetectionCount * 100;
                return $"{rate:F2}%";
            }
        }
        public string NGRate
        {
            get
            {
                if (TotalDetectionCount == 0)
                    return "0.0%";
                double rate = (double)NGCount / TotalDetectionCount * 100;
                return $"{rate:F2}%";
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

        private void InitParameters()
        {
            if (!_projectsetting.Exists(model => model.Name == "L0Upper"))
            {
                _projectsetting.Add(
                    new ProjectSettingModel(
                        "L0Upper",
                        "VM_EarTabRedundancyPage",
                        "10",
                        "double",
                        1
                    )
                    {
                        Remark = "L0上限",
                    }
                );
            }
            if (!_projectsetting.Exists(model => model.Name == "L1Upper"))
            {
                _projectsetting.Add(
                    new ProjectSettingModel(
                        "L1Upper",
                        "VM_EarTabRedundancyPage",
                        "1.89",
                        "double",
                        1
                    )
                    {
                        Remark = "L1上限",
                    }
                );
            }
            if (!_projectsetting.Exists(model => model.Name == "L2Upper"))
            {
                _projectsetting.Add(
                    new ProjectSettingModel(
                        "L2Upper",
                        "VM_EarTabRedundancyPage",
                        "1.49",
                        "double",
                        1
                    )
                    {
                        Remark = "L2上限",
                    }
                );
            }
            if (!_projectsetting.Exists(model => model.Name == "L3Upper"))
            {
                _projectsetting.Add(
                    new ProjectSettingModel(
                        "L3Upper",
                        "VM_EarTabRedundancyPage",
                        "1.1",
                        "double",
                        1
                    )
                    {
                        Remark = "L3上限",
                    }
                );
            }
            if (!_projectsetting.Exists(model => model.Name == "L0Lower"))
            {
                _projectsetting.Add(
                    new ProjectSettingModel(
                        "L0Lower",
                        "VM_EarTabRedundancyPage",
                        "1.9",
                        "double",
                        1
                    )
                    {
                        Remark = "L0下限",
                    }
                );
            }
            if (!_projectsetting.Exists(model => model.Name == "L1Lower"))
            {
                _projectsetting.Add(
                    new ProjectSettingModel(
                        "L1Lower",
                        "VM_EarTabRedundancyPage",
                        "1.5",
                        "double",
                        1
                    )
                    {
                        Remark = "L1下限",
                    }
                );
            }
            if (!_projectsetting.Exists(model => model.Name == "L2Lower"))
            {
                _projectsetting.Add(
                    new ProjectSettingModel(
                        "L2Lower",
                        "VM_EarTabRedundancyPage",
                        "1.11",
                        "double",
                        1
                    )
                    {
                        Remark = "L2下限",
                    }
                );
            }
            if (!_projectsetting.Exists(model => model.Name == "L3Lower"))
            {
                _projectsetting.Add(
                    new ProjectSettingModel(
                        "L3Lower",
                        "VM_EarTabRedundancyPage",
                        "0",
                        "double",
                        1
                    )
                    {
                        Remark = "L3下限",
                    }
                );
            }
            if (!_projectsetting.Exists(model => model.Name == "SelectedMonitorFolderPath"))
            {
                _projectsetting.Add(
                    new ProjectSettingModel(
                        "SelectedMonitorFolderPath",
                        "VM_EarTabRedundancyPage",
                        "",
                        "string",
                        1
                    )
                    {
                        Remark = "监控路径",
                    }
                );
            }
            L0Upper = _projectsetting.GetSettingValue("L0Upper", "VM_EarTabRedundancyPage");
            L1Upper = _projectsetting.GetSettingValue("L1Upper", "VM_EarTabRedundancyPage");
            L2Upper = _projectsetting.GetSettingValue("L2Upper", "VM_EarTabRedundancyPage");
            L3Upper = _projectsetting.GetSettingValue("L3Upper", "VM_EarTabRedundancyPage");
            L0Lower = _projectsetting.GetSettingValue("L0Lower", "VM_EarTabRedundancyPage");
            L1Lower = _projectsetting.GetSettingValue("L1Lower", "VM_EarTabRedundancyPage");
            L2Lower = _projectsetting.GetSettingValue("L2Lower", "VM_EarTabRedundancyPage");
            L3Lower = _projectsetting.GetSettingValue("L3Lower", "VM_EarTabRedundancyPage");

        }

        private readonly ILoggerService _logger;
        private readonly IProjectSettingRepository _projectsetting;
        private readonly VM_UserCenter _userCenterViewModel;
        private readonly INavigationService _navigationService;
        private readonly IWebApiClient _webApiClient;
        #region 方法
        public VM_EarTabRedundancyPage(INavigationService navigationService, VM_UserCenter userCenterViewModel, IProjectSettingRepository projectsetting, ILoggerService<VM_EarTabRedundancyPage> logger, IWebApiClient webApiClient)
        {
            _logger = logger;
            _projectsetting = projectsetting;
            _userCenterViewModel = userCenterViewModel ?? throw new ArgumentNullException(nameof(userCenterViewModel));
            _navigationService = navigationService;
            _userCenterViewModel.PropertyChanged += UserCenterViewModel_PropertyChanged;
            _webApiClient = webApiClient;
            InitPieChart();
            _logTable = new ObservableCollection<Tuple<string, string>>();
            // 初始化动态数据集合
            DetectionDataList = new ObservableCollection<dynamic>();
            _logger.Info("系统启动，日志功能就绪", true);
            AddLog("系统启动，日志功能就绪"); // 初始日志

            InitParameters();
            SyncTempPropertiesWithFormalProperties();
            LoadConfig();

            //云端下载模型取消InitDlll的注释，以及模型算法里面的注释
            InitDll1();


            //本地加载模型
            //E:\极耳冗余\1013\best1011.onnx   E:\XRay-data\best1119.onnx
            //ai.ModelInit(@"E:\XRay-data\best1119.onnx", null, "尺寸测量", out H246modle, out string info);
            //if (!string.IsNullOrEmpty(info))
            //{
            //    AddLog($"模型初始化异常：{info}");
            //}

            ZoomRealTimeImageCommand = new RelayCommand<MouseWheelEventArgs>(OnZoomRealTimeImage);
            ZoomInRealTimeCommand = new RelayCommand(OnZoomInRealTime);
            ZoomOutRealTimeCommand = new RelayCommand(OnZoomOutRealTime);
            ResetRealTimeImageZoomCommand = new RelayCommand(OnResetRealTimeZoom);
            ImageMouseDownCommand = new RelayCommand<MouseButtonEventArgs>(OnImageMouseDown);
            ImageMouseMoveCommand = new RelayCommand<MouseEventArgs>(OnImageMouseMove);
            ImageMouseUpCommand = new RelayCommand<MouseEventArgs>(OnImageMouseUp);
            File_Button = new RelayCommand(SelectImageFile);
            Folder_Button = new RelayCommand(SelectImageFolder);
            StopDetectionCommand = new RelayCommand(ExecuteStopDetection);
            ClearListCommand = new RelayCommand(ExecuteClearList);
            ClearStatisticsCommand = new RelayCommand(ExecuteClearStatistics);
            _detectionDataList = new ObservableCollection<dynamic>();
            ViewResultCommand = new RelayCommand<dynamic>(ExecuteViewResult);
            SelectMonitorFolderCommand = new RelayCommand(SelectMonitorFolder);
            SaveSettingsCommand = new RelayCommand(SaveThresholdConfig);
            ResetSettingsCommand = new RelayCommand(ResetThresholds);
            _cts = new CancellationTokenSource();
            //logger = logger;

            // 启动每日8点推送统计
            StartDailyPushTask();
           
            // 程序退出时停止定时器，避免内存泄漏
            Application.Current.Exit += (s, e) => StopDailyPushTask();
        }

        // 用于在用户主动退出时取消自动退出计时器
        private CancellationTokenSource? _autoLogoutCancellationTokenSource;

        // 监听登录状态变化
        private void UserCenterViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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
                    // 已退出登录，取消计时器
                    CancelAutoLogoutTimer();
                }
            }
        }
        private void AutoNavigateBackAfterLogin()
        {
            // 导航回应用界面
            _navigationService.NavigateTo("VM_EarTabRedundancy");
        }

        // 启动自动退出计时器
        private void StartAutoLogoutTimer()
        {
            CancelAutoLogoutTimer();
            _autoLogoutCancellationTokenSource = new CancellationTokenSource();
            var token = _autoLogoutCancellationTokenSource.Token;

            Task.Delay(TimeSpan.FromMinutes(15), token)
                .ContinueWith(t =>
                {
                    if (!t.IsCanceled)
                    {
                        // 在UI线程上执行退出操作
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (_userCenterViewModel.IsLoggedIn)
                            {
                                AddLog("登录时间已超过15分钟，为保障账户安全，已自动退出登录。");
                                // 调用主框架的退出登录命令
                                _userCenterViewModel.LogoutCommand?.Execute(null);
                            }
                        });
                    }
                }, token);
        }

        /// <summary>
        /// 取消自动退出计时器
        /// </summary>
        private void CancelAutoLogoutTimer()
        {
            // 检查令牌源是否存在且未被取消
            if (_autoLogoutCancellationTokenSource != null && !_autoLogoutCancellationTokenSource.IsCancellationRequested)
            {
                _autoLogoutCancellationTokenSource.Cancel();
                // 释放资源
                _autoLogoutCancellationTokenSource.Dispose();
                _autoLogoutCancellationTokenSource = null;
            }
        }
        object? obj2;
        public MethodInfo? ActionGetPointMethod { get; set; }
        public MethodInfo? ModelInitMethod { get; set; }
        public object?[]? param { get; set; }
        public object?[]? acparam { get; set; }

        //TabRedDetection tabai= new TabRedDetection();
        private void InitDll1()
        {
            // 基础配置（DLL和模型共用的密钥）
            string appKey = "YW_CT";
            string publicKey = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDYKBFjaGyUhD/KBxy70UDYnLDR40ZeeDhI/suj6/CXc2NvAPbCvnbX0vTbbbnSEizhZCohiLszgecV2vv7DgxL8cxE2WK0ZdrrA4CIQVE7tsnSpiGNY++eGEQQKENRpi3qutaSa+qhPOPEdvElDWmisDP1S9/vv0fvs9fIuhfDlwIDAQAB";

            // DLL配置
            string dllPythonCode = "YW_LiXianXray_C05_DLL";
            string dllExecuteType = "PRED";

            // 模型配置
            string modelPythonCode = "YW_LiXianXray_C05_Model";
            string modelExecuteType = "PRED"; // 模型下载通常用TRAIN标识
            string modelFileName = "best1011.onnx"; // 下载后保存的模型文件名
            string modelSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelFileName);

            try
            {
                //先完成DLL的初始化
                var DLLPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TabRedundancyCore.dll");
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
                    AddLog($"[DLL加载] 本地DLL不存在，从Web下载...");
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

                    // 验证DLL有效性
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
                targetType = targetAssembly.GetType("TabRedundancyCore.TabRedDetection");
                if (targetType == null)
                {
                    AddLog("[反射] 未找到类型：TabRedundancyCore.TabRedDetection");
                    throw new NullReferenceException("DLL中未找到目标类");
                }
                obj2 = Activator.CreateInstance(targetType);
                if (obj2 == null)
                {
                    AddLog("[反射] 无法创建TabRedDetection实例");
                    throw new Exception("无法创建DLL类实例");
                }
                ModelInitMethod = targetType.GetMethod("ModelInit");
                ActionGetPointMethod = targetType.GetMethod("ModelActionJiEr");
                if (ModelInitMethod == null || ActionGetPointMethod == null)
                {
                    AddLog($"[反射] 缺少核心方法：ModelInit={(ModelInitMethod != null)}，ModelActionJiEr={(ActionGetPointMethod != null)}");
                    throw new Exception("DLL中未找到核心检测方法");
                }
                AddLog($"[DLL初始化完成] 类实例+方法均就绪");

                // Web下载模型
                AddLog($"\n=== 开始处理模型：{modelFileName} ===");
                //byte[] modelBytes = null;
                bool needDownload = false;
                // 先检查本地是否已有模型
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
                }
                else
                {
                    AddLog($"[模型加载] 本地模型不存在，标记为需要下载");
                    needDownload = true;
                }
                if (needDownload)
                {
                    // 本地无模型/模型无效，从Web下载
                    AddLog($"[模型下载] 本地无有效模型，从Web下载...");
                    // 生成模型下载的RSA签名
                    string modelSignature = RasHelper.RSAEncrypt(RasHelper.ToXmlPublicKey(publicKey), appKey);
                    if (string.IsNullOrEmpty(modelSignature))
                    {
                        AddLog("[模型下载] RSA签名生成失败！");
                        throw new Exception("RSA签名生成失败，无法下载模型");
                    }

                    // 调用WebHelper1下载模型
                    var modelReFile = WebHelper1.DownloadFile1(appKey, modelSignature, modelPythonCode, modelExecuteType);
                    if (modelReFile == null || !modelReFile.Success || modelReFile.DataStr == null || modelReFile.DataStr.Length == 0)
                    {
                        string errorMsg = modelReFile?.Message ?? "下载响应为空";
                        AddLog($"[模型下载] Web下载失败：{errorMsg}");
                        // 弹出程序目录，方便用户手动放置模型
                        Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);
                        throw new Exception($"从Web获取模型失败：{errorMsg}\n请手动将 {modelFileName} 放置到 {AppDomain.CurrentDomain.BaseDirectory}");
                    }

                    // 验证模型文件有效性
                    if (modelReFile.DataStr.Length < 1024 * 10) // 10KB阈值
                    {
                        AddLog($"[模型下载] 下载的模型文件过小（{modelReFile.DataStr.Length} 字节），视为无效");
                        throw new Exception("下载的模型文件无效（体积过小）");
                    }

                    // 保存模型到本地目录
                    File.WriteAllBytes(modelSavePath, modelReFile.DataStr);
                    AddLog($"[模型下载成功]大小：{modelReFile.DataStr.Length} 字节");
                }
                // 准备ModelInit参数
                string Gongwei = "尺寸测量";
                string? exInfo = string.Empty;
                string aabbb = string.Empty;
                string[] labels = new string[0];
                string modelPath = modelSavePath;
                //AddLog($"[模型路径] {modelPath}（是否存在：{File.Exists(modelPath)}）");

                param = new object?[6] { modelPath, null, Gongwei, null, aabbb, true };

                // 调用ModelInit
                if (ModelInitMethod == null || obj2 == null)
                {
                    AddLog("[检测异常] DLL未初始化（ModelInit方法或实例为空）");
                    throw new Exception("DLL未初始化，ModelInit方法或实例为空");
                }
                ModelInitMethod.Invoke(obj2, param);
                H246modle = param[3] as IPredictor;
                exInfo = param[4] as string;

                if (H246modle != null)
                {
                    if (File.Exists(modelSavePath))
                    {
                        try
                        {
                            File.Delete(modelSavePath);
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
                    AddLog($"[模型初始化失败] {error}，保留本地模型文件供排查：{modelSavePath}");
                    throw new Exception($"模型初始化失败：{error}");
                }
                if (!string.IsNullOrEmpty(exInfo))
                {
                    AddLog($"[模型初始化警告] {exInfo}");
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
        private readonly object _modelInvokeLock = new object();
        private string RedundancyDetection(string imagePath, string DetectionStatus)
        {
            // 图片读取重试机制
            int maxRetry = 5; // 最大重试次数
            int retryCount = 0;
            int delayMs = 3; // 每次重试延迟3ms
            Mat? imageMat = null;
            string? savedResultImagePath = null;
            string? savedOriginalImagePath = null;
            try
            {
                //读取图像并校验
                while (retryCount < maxRetry)
                {
                    imageMat = Cv2.ImRead(imagePath, ImreadModes.Color);
                    if (!imageMat.Empty())
                    {
                        break;
                    }
                    imageMat.Release(); // 释放空Mat
                    retryCount++;
                    //AddLog($"[图片读取重试] 第{retryCount}/{maxRetry}次：图片尚未写入完成，等待{delayMs}ms... 路径：{imagePath}");
                    Task.Delay(delayMs).Wait();
                }

                if (imageMat == null)
                {
                    string error = $"[检测异常] 图片为空（路径：{imagePath}）";
                    AddLog(error);
                    return error;
                }

                //校验AI模型
                if (H246modle == null)
                {
                    string error = "[检测异常] H246modle 未初始化";
                    AddLog(error);
                    return error;
                }


                //云端下载
                MeasureResult? detectresult = null;
                string Gongwei = "尺寸测量";
                string? aiError = string.Empty;
                // 加锁：同一时间仅一个线程调用模型
                lock (_modelInvokeLock)
                {
                    //tabai.ModelActionJiEr(H246modle, imageMat, Gongwei, out detectresult, out aiError);


                    // 每次调用重新初始化参数（避免多线程参数污染）
                    acparam = new object?[5] { H246modle, imageMat, Gongwei, default(MeasureResult), aiError };
                    // 执行反射调用（此时仅当前线程操作模型和参数）
                    ActionGetPointMethod?.Invoke(obj2, acparam);
                    // 从参数中提取结果（加锁内提取，确保结果完整）
                    detectresult = acparam[3] as MeasureResult;
                    aiError = acparam[4] as string;
                }


                ////本地加载模型
                //string measureType = "尺寸测量";
                //string aiError = string.Empty;
                //ai.ModelActionJiEr(H246modle, imageMat, measureType, out MeasureResult detectresult, out aiError);

                if (!string.IsNullOrWhiteSpace(aiError))
                {
                    string error = $"[AI错误] {aiError}（图像：{imagePath}）";
                    AddLog(error);
                    return error;
                }

                if (detectresult == null)
                {
                    string error = "[AI结果异常] MeasureResult 为null";
                    AddLog(error);
                    return error;
                }
                string fileName = Path.GetFileName(imagePath); // 拿到图片名
                //解析检测结果
                double? leftAvg = GetValidDistance(detectresult.LeftDistance, "左侧");
                double? rightAvg = GetValidDistance(detectresult.RightDistance, "右侧");
                float[] leftUpPoint = detectresult.LeftUpPoint;
                float[] leftDownPoint = detectresult.LeftDownPoint;
                float[] rightUpPoint = detectresult.RightUpPoint;
                float[] rightDownPoint = detectresult.RightDownPoint;
                string cellCode = GetCellCodeFromFileName(fileName);

                // 计算左侧风险等级
                string leftLevel = GetSingleRiskLevel(leftAvg);
                // 计算右侧风险等级
                string rightLevel = GetSingleRiskLevel(rightAvg);
                //取风险最高的等级（L3 > L2 > L1 > L0，未知时取已知侧）
                string RiskLevel = GetFinalRiskLevel(leftLevel, rightLevel);
                AddLog($"[AI检测成功] 左侧：{leftAvg}mm，右侧：{rightAvg}mm（图像：{imagePath}）");
                var finalresult = detectresult.Ok_Ng_Singal?.Trim().ToUpper() ?? "NG";
                Mat drawnMat = GenerateFinalResultImage(imagePath, cellCode, finalresult, leftAvg, rightAvg, RiskLevel, leftUpPoint, leftDownPoint, rightUpPoint, rightDownPoint);
                //处理OK结果
                if (finalresult == "OK")
                {
                    if (IsSaveResultOK)
                    {
                        savedResultImagePath = SaveDetectionResult(imagePath, drawnMat, finalresult, true);
                    }
                }
                //处理NG结果
                if (finalresult == "NG")
                {

                    if (IsSaveResultNG)
                    {
                        savedResultImagePath = SaveDetectionResult(imagePath, drawnMat, finalresult, true);

                    }
                }
                //保存结果图
                if (finalresult == "OK" && IsSaveOriginalOK)
                {
                    savedOriginalImagePath = SaveDetectionResult(imagePath, null, finalresult, false);
                }
                if (finalresult == "NG" && IsSaveOriginalNG)
                {
                    savedOriginalImagePath = SaveDetectionResult(imagePath, null, finalresult, false);
                }

                //string csvCellCode = GetCellCodeFromFileName(fileName); // 提取电芯码
                string csvPolarity = GetPolarityFromFileName(fileName); // 提取正负极


                WriteToCsv(
                        imagePath: imagePath,          // 原图路径（用于提取日期）
                        cellCode: cellCode,         // 提取的电芯码（也可用你算法中的cellCode变量）
                        polarity: csvPolarity,         // 提取的正负极
                        leftDl: leftAvg,               // 左侧距离（算法结果）
                        rightDr: rightAvg,             // 右侧距离（算法结果）
                        detectResult: finalresult,     // 检测结果（OK/NG）
                        riskLevel: RiskLevel           // 风险等级（算法结果）
                    );
                // 更新统计和UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 构造检测记录
                    dynamic detectionData = new ExpandoObject();
                    detectionData.cellCode = cellCode;
                    detectionData.OriginalImagePath = Path.GetFullPath(imagePath);
                    detectionData.ResultImagePath = savedResultImagePath;
                    detectionData.LeftDistance = leftAvg == -1 ? null : leftAvg;
                    detectionData.RightDistance = rightAvg == -1 ? null : rightAvg;
                    detectionData.DetectionResult = finalresult;
                    detectionData.RiskLevel = RiskLevel;
                    detectionData.DetectionStatus = DetectionStatus;
                    detectionData.DetectionTime = DateTime.Now; // 新增时间，用于排序

                    // 添加到列表
                    DetectionDataList.Add(detectionData);
                    _fullDetectionDataList.Add(detectionData);
                    UpdateDisplayDataList();

                    // 更新OK/NG统计
                    if (finalresult == "OK")
                        OKCount++;
                    else
                        NGCount++;

                    // 更新风险等级统计
                    switch (RiskLevel)
                    {
                        case "L0": Count_L0++; break;
                        case "L1": Count_L1++; break;
                        case "L2": Count_L2++; break;
                        case "L3": Count_L3++; break;

                    }
                    //更新最新检测结果缓存
                    _latestDetectionData = detectionData;

                    //如果当前不是查看历史状态，立即实时显示最新结果
                    lock (_viewStatusLocker)
                    {
                        if (!_isViewingHistory)
                        {
                            ShowLatestDetectionResult();
                        }
                        else
                        {
                            //AddLog($"[新图等待] 正在查看历史结果，5秒后自动更新最新图：{Path.GetFileName(imagePath)}");
                        }
                    }
                });

                //AddLog($"[AI检测成功] 左侧：{leftAvg:F2}mm，右侧：{rightAvg:F2}mm，结果：{finalresult}（图像：{imagePath}）");
                return $"检测结果：{finalresult}（左侧：{leftAvg:F2}mm，右侧：{rightAvg:F2}mm）";
            }
            catch (Exception ex)
            {
                string error = $"[检测异常] {ex.Message}（图像：{imagePath}）";
                AddLog(error);
                return error;
            }
            finally
            {
                // 释放OpenCV资源
                if (imageMat != null && !imageMat.IsDisposed)
                {
                    imageMat.Release();
                    imageMat.Dispose();
                }
            }
        }

        /// <summary>
        /// 判断单个距离的风险等级（左侧或右侧）
        /// </summary>
        /// <param name="distance">距离值（可空，null表示无数据）</param>
        /// <returns>风险等级（L0/L1/L2/L3/未知）</returns>
        private string GetSingleRiskLevel(double? distance)
        {
            // 无有效数据时返回"未知"
            if (!distance.HasValue)
                return "";

            double val = distance.Value;
            if (val > _l0Lower)
                return "L0";
            else if (val > _l1Lower && val < _l1Upper) // [1.5,1.9)
                return "L1";
            else if (val > _l2Lower && val < _l2Upper)  // (1.1,1.5)
                return "L2";
            else if (val < _l3Upper && val > _l3Lower)
                return "L3";
            else
                return "";
        }

        /// <summary>
        /// 根据左右侧等级取最终风险等级
        /// </summary>
        /// <param name="leftLevel">左侧等级</param>
        /// <param name="rightLevel">右侧等级</param>
        /// <returns>最终风险等级</returns>
        private string GetFinalRiskLevel(string leftLevel, string rightLevel)
        {
            //只要有一侧是L0，最终等级为L0
            if (leftLevel == "L0" || rightLevel == "L0")
            {
                return "L0";
            }

            //没有L0时，只要有一侧是L1，最终等级为L1
            if ((leftLevel == "L1" && (rightLevel == "L2" || rightLevel == "L3")) ||
                (rightLevel == "L1" && (leftLevel == "L2" || leftLevel == "L3")))
            {
                return "L1";
            }

            // 没有L0/L1时，一侧L2、一侧L3 → 最终L3
            if ((leftLevel == "L2" && rightLevel == "L3") ||
                (leftLevel == "L3" && rightLevel == "L2"))
            {
                return "L3";
            }

            // 同等级情况（两侧都是L1/L2/L3）
            if (leftLevel == rightLevel)
            {
                return leftLevel; // 两侧都是L1→L1，都是L2→L2，都是L3→L3
            }

            // 其他特殊情况（如一侧L1、一侧L1 → 已被规则4覆盖；含"未知"等级时取已知侧较高逻辑）
            return !string.IsNullOrEmpty(leftLevel) && leftLevel != "未知" ? leftLevel : rightLevel;
        }
        /// <summary>
        /// 提取唯一有效距离
        /// </summary>
        /// <param name="distanceArray">距离数组</param>
        /// <param name="direction">方向/param>
        /// <returns>有效距离，无有效数据则返回-1</returns>
        private double GetValidDistance(float[] distanceArray, string direction)
        {
            if (distanceArray == null || distanceArray.Length == 0)
            {
                AddLog($"[{direction}距离] 数组为空（无数据）");
                return -1.0; // 无数据返回-1
            }

            // 过滤无效值
            var validDistances = distanceArray
                .Where(d => d > 0 && !float.IsNaN(d) && !float.IsInfinity(d))
                .ToList();

            if (validDistances.Count == 0)
            {
                //AddLog($"[{direction}距离] 无有效数据（原始值：{string.Join(",", distanceArray)}）");
                return -1.0;
            }

            // 取第一个有效数据，保留2位小数
            double validValue = Math.Round(validDistances[0], 2);
            //AddLog($"[{direction}距离] 有效数据：{validValue}mm");
            return validValue;
        }
        // 修改返回类型为Mat，不再返回路径
        private Mat GenerateFinalResultImage(
            string originalPath,
            string cellCode,
            string detectResult,
            double? leftAvg = null,
            double? rightAvg = null,
            string? riskLevel = null,
            float[]? LeftUpPoint = null,
            float[]? LeftDownPoint = null,
            float[]? RightUpPoint = null,
            float[]? RightDownPoint = null
        )
        {
            Mat resultMat = new Mat();
            try
            {
                //读取原图并绘制红线
                Mat opencvMat = Cv2.ImRead(originalPath, ImreadModes.Color);
                if (opencvMat.Empty())
                {
                    AddLog($"[失败] 原图为空：{originalPath}");
                    return resultMat;
                }
                int imgWidth = opencvMat.Cols;
                int imgHeight = opencvMat.Rows;

                // 绘制参数（主线+横线共用）
                Scalar red = new Scalar(0, 0, 255); // 红线颜色
                int lineThickness = 2;              // 线宽
                int crossLineLength = 15;           // 两端横线长度
                HersheyFonts cvFont = HersheyFonts.HersheySimplex;
                double cvFontScale = 0.7;
                int cvTextThickness = 2;
                int textOffsetY = 65;
                int textOffset2Y = 80;

                // -------------------------- 左侧红线 + 两端横线 --------------------------
                if (leftAvg.HasValue && LeftUpPoint != null && LeftUpPoint.Length >= 2 && LeftDownPoint != null && LeftDownPoint.Length >= 2)
                {
                    OpenCvSharp.Point leftUp = new OpenCvSharp.Point((int)Math.Round(LeftUpPoint[0]), (int)Math.Round(LeftUpPoint[1]));
                    OpenCvSharp.Point leftDown = new OpenCvSharp.Point((int)Math.Round(LeftDownPoint[0]), (int)Math.Round(LeftDownPoint[1]));

                    // 1. 绘制主线（竖线）
                    Cv2.Line(opencvMat, leftUp, leftDown, red, lineThickness);

                    // 2. 上端横线（水平，垂直于主线）
                    OpenCvSharp.Point leftUpCrossStart = new OpenCvSharp.Point(leftUp.X - crossLineLength, leftUp.Y);
                    OpenCvSharp.Point leftUpCrossEnd = new OpenCvSharp.Point(leftUp.X + crossLineLength, leftUp.Y);
                    Cv2.Line(opencvMat, leftUpCrossStart, leftUpCrossEnd, red, lineThickness);

                    // 3. 下端横线
                    OpenCvSharp.Point leftDownCrossStart = new OpenCvSharp.Point(leftDown.X - crossLineLength, leftDown.Y);
                    OpenCvSharp.Point leftDownCrossEnd = new OpenCvSharp.Point(leftDown.X + crossLineLength, leftDown.Y);
                    Cv2.Line(opencvMat, leftDownCrossStart, leftDownCrossEnd, red, lineThickness);

                    // 4. 绘制下方距离文字
                    OpenCvSharp.Point leftTextCenter = new OpenCvSharp.Point((leftUp.X + leftDown.X) / 2, (leftUp.Y + leftDown.Y) / 2 + textOffsetY);
                    string leftText = $"{leftAvg.Value:F2}mm";
                    Cv2.GetTextSize(leftText, cvFont, cvFontScale, cvTextThickness, out int leftTextSize);
                    Cv2.PutText(opencvMat, leftText, new OpenCvSharp.Point(leftTextCenter.X - leftTextSize * 2.4, leftTextCenter.Y), cvFont, cvFontScale, red, cvTextThickness);

                    //AddLog($"[左侧绘制] 主线+两端横线完成（上端：{leftUpCrossStart}→{leftUpCrossEnd}，下端：{leftDownCrossStart}→{leftDownCrossEnd}）");
                }

                // -------------------------- 右侧红线 + 两端横线 --------------------------
                if (rightAvg.HasValue && RightUpPoint != null && RightUpPoint.Length >= 2 && RightDownPoint != null && RightDownPoint.Length >= 2)
                {
                    OpenCvSharp.Point rightUp = new OpenCvSharp.Point((int)Math.Round(RightUpPoint[0]), (int)Math.Round(RightUpPoint[1]));
                    OpenCvSharp.Point rightDown = new OpenCvSharp.Point((int)Math.Round(RightDownPoint[0]), (int)Math.Round(RightDownPoint[1]));

                    // 1. 绘制主线（竖线）
                    Cv2.Line(opencvMat, rightUp, rightDown, red, lineThickness);

                    // 2. 上端横线
                    OpenCvSharp.Point rightUpCrossStart = new OpenCvSharp.Point(rightUp.X - crossLineLength, rightUp.Y);
                    OpenCvSharp.Point rightUpCrossEnd = new OpenCvSharp.Point(rightUp.X + crossLineLength, rightUp.Y);
                    Cv2.Line(opencvMat, rightUpCrossStart, rightUpCrossEnd, red, lineThickness);

                    // 3. 下端横线
                    OpenCvSharp.Point rightDownCrossStart = new OpenCvSharp.Point(rightDown.X - crossLineLength, rightDown.Y);
                    OpenCvSharp.Point rightDownCrossEnd = new OpenCvSharp.Point(rightDown.X + crossLineLength, rightDown.Y);
                    Cv2.Line(opencvMat, rightDownCrossStart, rightDownCrossEnd, red, lineThickness);

                    // 4. 绘制下方距离文字
                    OpenCvSharp.Point rightTextCenter = new OpenCvSharp.Point((rightUp.X + rightDown.X) / 2, (rightUp.Y + rightDown.Y) / 2 + textOffset2Y);
                    string rightText = $"{rightAvg.Value:F2}mm";
                    Cv2.GetTextSize(rightText, cvFont, cvFontScale, cvTextThickness, out int rightTextSize);
                    Cv2.PutText(opencvMat, rightText, new OpenCvSharp.Point(rightTextCenter.X - rightTextSize * 2.4, rightTextCenter.Y), cvFont, cvFontScale, red, cvTextThickness);

                    //AddLog($"[右侧绘制] 主线+两端横线完成（上端：{rightUpCrossStart}→{rightUpCrossEnd}，下端：{rightDownCrossStart}→{rightDownCrossEnd}）");
                }

                // 转WPF底图
                BitmapImage wpfBitmap;
                using (var tempStream = new MemoryStream())
                {
                    int[] encodeParams = new int[] { (int)ImwriteFlags.PngCompression, 0 };
                    bool encodeSuccess = Cv2.ImEncode(".png", opencvMat, out byte[] encodedBytes, encodeParams);
                    if (!encodeSuccess)
                    {
                        AddLog($"[失败] Mat编码PNG失败");
                        opencvMat.CopyTo(resultMat);
                        return resultMat;
                    }

                    tempStream.Write(encodedBytes, 0, encodedBytes.Length);
                    tempStream.Position = 0;

                    wpfBitmap = new BitmapImage();
                    wpfBitmap.BeginInit();
                    wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    wpfBitmap.StreamSource = tempStream;
                    wpfBitmap.EndInit();
                    wpfBitmap.Freeze();
                }

                // WPF叠加中文
                DrawingVisual visual = new DrawingVisual();
                using (DrawingContext dc = visual.RenderOpen())
                {
                    dc.DrawImage(wpfBitmap, new System.Windows.Rect(0, 0, imgWidth, imgHeight));

                    Typeface typeface = new Typeface(new System.Windows.Media.FontFamily("Microsoft YaHei"), FontStyles.Normal, System.Windows.FontWeights.Bold, FontStretches.Normal);
                    double fontSize = 32;
                    System.Windows.Media.Brush greenBrush = System.Windows.Media.Brushes.Green;
                    System.Windows.Media.Brush redBrush = System.Windows.Media.Brushes.Red;
                    double yPos = 10;
                    double lineSpacing = 15;

                    FormattedText codeText = new FormattedText($"电芯条码：{cellCode}", CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, typeface, fontSize, greenBrush, 96);
                    dc.DrawText(codeText, new System.Windows.Point(10, yPos));
                    yPos += codeText.Height + lineSpacing;

                    FormattedText resultLabel = new FormattedText("检测结果：", CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, typeface, fontSize, greenBrush, 96);
                    FormattedText resultValue = new FormattedText(detectResult, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, typeface, fontSize, redBrush, 96);
                    dc.DrawText(resultLabel, new System.Windows.Point(10, yPos));
                    dc.DrawText(resultValue, new System.Windows.Point(10 + resultLabel.Width, yPos));
                    yPos += Math.Max(resultLabel.Height, resultValue.Height) + lineSpacing;

                    // -1表示无有效数据
                    double invalidValue = -1;

                    // 左侧距离：保留标签，值为-1时不显示“-1mm”
                    string leftDistanceText;
                    if (leftAvg.HasValue && leftAvg.Value != invalidValue)
                    {
                        leftDistanceText = $"{leftAvg.Value:F2}mm"; // 有效数值：显示“3.25mm”
                    }
                    else
                    {
                        leftDistanceText = ""; // 值为-1或null：不显示任何数值（仅保留标签）
                    }

                    // 绘制左侧标签（始终显示，仅数值部分动态变化）
                    FormattedText leftText = new FormattedText(
                        $"左侧极耳最低点到负极片的距离dl：{leftDistanceText}",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        fontSize,
                        greenBrush,
                        96
                    );
                    dc.DrawText(leftText, new System.Windows.Point(10, yPos));
                    yPos += leftText.Height + lineSpacing; // 无论是否有数值，都保留行高，避免布局错乱


                    // 右侧距离：同样保留标签，值为-1时隐藏“-1mm”
                    string rightDistanceText;
                    if (rightAvg.HasValue && rightAvg.Value != invalidValue)
                    {
                        rightDistanceText = $"{rightAvg.Value:F2}mm"; // 有效数值：显示“3.41mm”
                    }
                    else
                    {
                        rightDistanceText = "";
                    }

                    // 绘制右侧标签
                    FormattedText rightText = new FormattedText(
                        $"右侧极耳最低点到负极片的距离dr：{rightDistanceText}",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        fontSize,
                        greenBrush,
                        96
                    );
                    dc.DrawText(rightText, new System.Windows.Point(10, yPos));
                    yPos += rightText.Height + lineSpacing;


                    // 风险等级
                    FormattedText levelLabel = new FormattedText("风险等级：", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, greenBrush, 96);
                    FormattedText levelValue = new FormattedText(riskLevel ?? "无", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, redBrush, 96);
                    dc.DrawText(levelLabel, new System.Windows.Point(10, yPos));
                    dc.DrawText(levelValue, new System.Windows.Point(10 + levelLabel.Width, yPos));


                    // 其他缺陷提示
                    bool isLeftInvalid = !leftAvg.HasValue || leftAvg.Value == invalidValue;
                    bool isRightInvalid = !rightAvg.HasValue || rightAvg.Value == invalidValue;
                    if (isLeftInvalid && isRightInvalid)
                    {
                        FormattedText defectText = new FormattedText("缺陷类型：其他缺陷（无详细参数）", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 24, System.Windows.Media.Brushes.Orange, 96);
                        dc.DrawText(defectText, new System.Windows.Point(20, imgHeight - 50));
                    }
                }

                //转Mat返回
                RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                    imgWidth, imgHeight,
                    96, 96,
                    PixelFormats.Pbgra32
                );
                renderBitmap.Render(visual);

                int stride = renderBitmap.PixelWidth * 4;
                byte[] pixels = new byte[renderBitmap.PixelHeight * stride];
                renderBitmap.CopyPixels(pixels, stride, 0);

                resultMat = new Mat(renderBitmap.PixelHeight, renderBitmap.PixelWidth, MatType.CV_8UC4);
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, resultMat.Data, pixels.Length);
                Cv2.CvtColor(resultMat, resultMat, ColorConversionCodes.BGRA2BGR);

                return resultMat;
            }
            catch (Exception ex)
            {
                AddLog($"[异常] {ex.Message}");
                return resultMat;
            }
        }

        private string? SaveDetectionResult(string originalImagePath, Mat? imageMat, string result, bool isResultImage)
        {
            try
            {
                //生成保存路径
                string? savePath = GetResultImagePath(originalImagePath, result, isResultImage);
                if (string.IsNullOrEmpty(savePath) || savePath.Contains("[错误]"))
                {
                    AddLog($"[保存失败] {(isResultImage ? "结果图" : "原图")}路径无效：{savePath}");
                    return null;
                }

                //创建目标目录
                string? targetDir = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    //AddLog($"[保存准备] 创建{(isResultImage ? "结果图" : "原图")}目录：{targetDir}");
                }

                // 区分“原图复制”和“结果图写入”
                bool saveSuccess = false;
                if (!isResultImage) // 保存原图 → 用File.Copy
                {
                    // 校验源文件是否存在且可访问
                    if (!File.Exists(originalImagePath))
                    {
                        AddLog($"[复制失败] 源原图不存在：{originalImagePath}");
                        return null;
                    }

                    if (File.Exists(savePath))
                    {
                        File.Delete(savePath);
                        //AddLog($"[复制准备] 删除已存在的旧原图：{savePath}");
                    }

                    // 文件复制
                    using (var sourceStream = new FileStream(originalImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var targetStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        sourceStream.CopyTo(targetStream);
                        saveSuccess = true;
                    }
                }
                else // 保存结果图 → 继续用Cv2.ImWrite
                {
                    if (imageMat == null || imageMat.Empty())
                    {
                        AddLog($"[写入失败] 结果图Mat为空，无法保存");
                        return null;
                    }
                    saveSuccess = Cv2.ImWrite(savePath, imageMat);
                }

                if (saveSuccess)
                {
                    //AddLog($"[保存成功] {(isResultImage ? "结果图" : "原图")}：{savePath}");
                    return savePath;
                }
                else
                {
                    AddLog($"[保存失败] {(isResultImage ? "结果图写入" : "原图复制")}失败：{savePath}");
                    // 失败时删除空文件（避免残留无效文件）
                    if (File.Exists(savePath)) File.Delete(savePath);
                    return null;
                }
            }
            catch (IOException ex)
            {
                string errorMsg = $"[保存异常] {(isResultImage ? "结果图" : "原图")}：文件被占用或权限不足 → {ex.Message}";
                AddLog(errorMsg);
                return null;
            }
            catch (Exception ex)
            {
                string errorMsg = $"[保存异常] {(isResultImage ? "结果图" : "原图")}：{ex.Message}";
                AddLog(errorMsg);
                return null;
            }
        }

        private string? GetResultImagePath(string imagePath, string result, bool isResultImage)
        {
            try
            {
                // 根目录默认为D:
                string? rootDirectory = "D:";
                if (string.IsNullOrEmpty(rootDirectory))
                {
                    rootDirectory = !string.IsNullOrEmpty(SelectedFolderPath)
                        ? Path.GetPathRoot(SelectedFolderPath)
                        : Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
                }
                if (string.IsNullOrEmpty(rootDirectory))
                {
                    AddLog("[错误] 无法获取有效盘符");
                    return "[错误] 无法获取有效盘符";
                }

                // 基础参数
                string currentDate = DateTime.Now.ToString("yyyy-MM-dd"); // 当天日期（离线专用）
                string imageDateStr = GetImageDateFromPath(imagePath); // 从图片提取的日期
                string imageType = isResultImage ? "结果图" : "原图";
                string resultType = result;
                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                string fileExt = Path.GetExtension(imagePath);
                string newFileName = isResultImage ? $"{fileName}_{result}{fileExt}" : $"{fileName}{fileExt}";

                //用现有开关区分根目录和层级
                string baseDir = IsOnlineDetectionEnabled ? "JR_AI结果_在线" : "JR_AI结果_离线";
                List<string> pathSegments = new List<string> { rootDirectory, baseDir };

                // 离线模式（开关为false）
                if (!IsOnlineDetectionEnabled)
                {
                    pathSegments.Add(currentDate);
                }

                // 共同路径层级
                pathSegments.AddRange(new[] { imageDateStr, imageType, resultType, newFileName });

                string savePath = Path.Combine(pathSegments.ToArray());
                return Path.GetFullPath(savePath);
            }
            catch (Exception ex)
            {
                AddLog($"[路径构造失败] {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从图片路径中提取日期并格式化为“MM月dd日”
        /// </summary>
        /// <param name="imagePath">图片绝对路径</param>
        /// <returns>格式化后的日期字符串（如10月10日）</returns>
        /// <exception cref="Exception">无法从路径中提取日期时抛出</exception>
        private string GetImageDateFromPath(string imagePath)
        {
            // 正则匹配常见日期格式（可根据实际路径格式调整）
            var datePatterns = new List<Tuple<string, string>>
            {
                Tuple.Create(@"\b\d{4}-\d{2}-\d{2}\b", "yyyy-MM-dd"),       // 匹配2025-10-16
                Tuple.Create(@"\b\d{2}-\d{2}-\d{4}\b", "MM-dd-yyyy"),       // 匹配10-16-2025
                Tuple.Create(@"\b\d{8}\b", "yyyyMMdd"),                     // 匹配20251016
                Tuple.Create(@"\b\d{1,2}月\d{1,2}日\b", "M月d日")           // 直接匹配10月16日
            };

            foreach (var pattern in datePatterns)
            {
                var match = Regex.Match(imagePath, pattern.Item1);
                if (match.Success && DateTime.TryParseExact(
                    match.Value,
                    pattern.Item2,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime date))
                {
                    return date.ToString("yyyyMMdd");
                }
            }

            throw new Exception($"无法从图片路径「{imagePath}」中提取有效日期");
        }
        // 一键重置阈值到默认值
        private void ResetThresholds()
        {
            if (!_userCenterViewModel.IsLoggedIn)
            {
                MessageBoxResult result = System.Windows.MessageBox.Show(
                    "请先登录以执行此操作。\n点击“确定”将跳转到登录页面。",
                    "操作需要登录",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                // 只有OK按钮，导航到登录页
                if (result == MessageBoxResult.OK)
                {
                    _navigationService.NavigateTo("UserCenter");
                }
                return;
            }

            if (_userCenterViewModel.RoleID < (int)UserRole.Engineer)
            {
                System.Windows.MessageBox.Show(
                    "您的权限等级不足，无法执行此操作。\n需要【工程师】或以上权限。",
                    "权限不足",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }
            var result1 = System.Windows.MessageBox.Show(
                "确定要将阈值恢复为默认值吗？\n此操作会覆盖当前配置。",
                "确认重置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result1 != MessageBoxResult.Yes)
                return;

            _projectsetting.UpdateSettingValue("L0Upper", "VM_EarTabRedundancyPage", 10);
            _projectsetting.UpdateSettingValue("L1Upper", "VM_EarTabRedundancyPage", 1.89);
            _projectsetting.UpdateSettingValue("L2Upper", "VM_EarTabRedundancyPage", 1.49);
            _projectsetting.UpdateSettingValue("L3Upper", "VM_EarTabRedundancyPage", 1.1);
            _projectsetting.UpdateSettingValue("L0Lower", "VM_EarTabRedundancyPage", 1.9);
            _projectsetting.UpdateSettingValue("L1Lower", "VM_EarTabRedundancyPage", 1.5);
            _projectsetting.UpdateSettingValue("L2Lower", "VM_EarTabRedundancyPage", 1.11);
            _projectsetting.UpdateSettingValue("L3Lower", "VM_EarTabRedundancyPage", 0);
            InitParameters();
            SyncTempPropertiesWithFormalProperties();
            AddLog("阈值已恢复为默认值");

            // 同时保存到文件，下次启动也是默认值
            //SaveThresholdConfig();
        }
        private void SaveThresholdConfig()
        {
            if (!_userCenterViewModel.IsLoggedIn)
            {
                MessageBoxResult result = System.Windows.MessageBox.Show(
                    "请先登录以执行此操作。\n点击“确定”将跳转到登录页面。",
                    "操作需要登录",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                // 无论用户点击什么，导航到登录页
                if (result == MessageBoxResult.OK)
                {
                    _navigationService.NavigateTo("UserCenter");
                }
                return;
            }

            //检查权限级别
            if (_userCenterViewModel.RoleID < (int)UserRole.Engineer)
            {
                System.Windows.MessageBox.Show(
                    "您的权限等级不足，无法执行此操作。\n需要【工程师】或以上权限。",
                    "权限不足",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }
            try
            {
                _projectsetting.UpdateSettingValue("L0Upper", "VM_EarTabRedundancyPage", TempL0Upper);
                _projectsetting.UpdateSettingValue("L1Upper", "VM_EarTabRedundancyPage", TempL1Upper);
                _projectsetting.UpdateSettingValue("L2Upper", "VM_EarTabRedundancyPage", TempL2Upper);
                _projectsetting.UpdateSettingValue("L3Upper", "VM_EarTabRedundancyPage", TempL3Upper);
                _projectsetting.UpdateSettingValue("L0Lower", "VM_EarTabRedundancyPage", TempL0Lower);
                _projectsetting.UpdateSettingValue("L1Lower", "VM_EarTabRedundancyPage", TempL1Lower);
                _projectsetting.UpdateSettingValue("L2Lower", "VM_EarTabRedundancyPage", TempL2Lower);
                _projectsetting.UpdateSettingValue("L3Lower", "VM_EarTabRedundancyPage", TempL3Lower);
                InitParameters();
                SyncTempPropertiesWithFormalProperties();
                AddLog("阈值配置保存成功");
                Growl.SuccessGlobal("阈值配置保存成功！");
            }
            catch (Exception ex)
            {
                AddLog("更新设置失败");
                Growl.ErrorGlobal($"更新失败: {ex.Message}");
            }
            //// 用匿名类封装所有阈值
            //var config = new
            //{
            //    L0Upper,
            //    L0Lower,
            //    L1Upper,
            //    L1Lower,
            //    L2Upper,
            //    L2Lower,
            //    L3Upper,
            //    L3Lower
            //};

            //string json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
            //string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ThresholdConfig.json");
            //System.IO.File.WriteAllText(path, json);
            //AddLog("阈值配置已保存");
        }
        public void SyncTempPropertiesWithFormalProperties()
        {
            TempL0Upper = L0Upper;
            TempL1Upper = L1Upper;
            TempL2Upper = L2Upper;
            TempL3Upper = L3Upper;

            TempL0Lower = L0Lower;
            TempL1Lower = L1Lower;
            TempL2Lower = L2Lower;
            TempL3Lower = L3Lower;
            AddLog("临时属性已从正式属性同步。");
        }
        //private void LoadThresholdConfig()
        //{
        //    string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ThresholdConfig.json");
        //    if (System.IO.File.Exists(path))
        //    {
        //        try
        //        {
        //            string json = System.IO.File.ReadAllText(path);
        //            // 用 dynamic 接收反序列化结果
        //            dynamic config = Newtonsoft.Json.JsonConvert.DeserializeObject(json)!;
        //            if (config != null)
        //            {
        //                L0Upper = config.L0Upper;
        //                L0Lower = config.L0Lower;
        //                L1Upper = config.L1Upper;
        //                L1Lower = config.L1Lower;
        //                L2Upper = config.L2Upper;
        //                L2Lower = config.L2Lower;
        //                L3Upper = config.L3Upper;
        //                L3Lower = config.L3Lower;
        //                AddLog("已加载保存的阈值配置");
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            AddLog($"加载阈值配置失败：{ex.Message}，将使用默认值");
        //        }
        //    }
        //    else
        //    {
        //        AddLog("未找到配置文件，使用默认阈值");
        //    }
        //}
        private string? _currentMonitorPath;
        /// <summary>
        /// 选择在线检测的监控文件夹
        /// </summary>
        private void SelectMonitorFolder()
        {
            // 先检查权限
            if (!CheckPermission())
            {
                return;
            }

            var folderDialog = new OpenFolderDialog();

            // 映射原对话框属性
            folderDialog.Title = "选择在线检测监控文件夹";
            folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            folderDialog.ValidateNames = true;
            folderDialog.Multiselect = false;

            // 调整返回值判断逻辑
            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                // 替换 FileName 为 FolderName 获取选中路径
                SelectedMonitorFolderPath = folderDialog.FolderName;
                AddLog($"已选择在线监控文件夹：{SelectedMonitorFolderPath}");
            }
        }

        #region 在线检测 - 启动监控
        // 跨天监控相关
        private System.Timers.Timer? _dateCheckTimer;// 用于检查日期变化的定时器
        private string? _currentDateFolder; // 记录当前监控的日期文件夹（yyyyMMdd）
        // 文件锁定检测相关
        private const int MaxFileRetryCount = 5; // 最大重试次数
        private const int FileRetryIntervalMs = 1000; // 重试间隔
        /// <summary>
        /// 启动在线文件夹监控
        /// </summary>
        private void StartOnlineMonitoring()
        {
            // 避免重复启动
            if (_onlineMonitorWatcher != null && _onlineMonitorWatcher.EnableRaisingEvents)
            {
                AddLog("[在线检测] 监控已在运行，无需重复启动");
                return;
            }

            // 校验监控文件夹路径
            if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPath) || !Directory.Exists(SelectedMonitorFolderPath))
            {
                AddLog("[在线检测] 监控文件夹不存在，请先选择有效文件夹");
                IsOnlineDetectionEnabled = false; // 同步UI状态
                System.Windows.MessageBox.Show("监控文件夹不存在，请先选择有效文件夹", "在线检测错误",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                _currentDateFolder = DateTime.Now.ToString("yyyyMMdd");
                _currentMonitorPath = GetTargetMonitorPath();

                // 目录存在则直接用，不存在才创建
                if (!Directory.Exists(_currentMonitorPath))
                {
                    Directory.CreateDirectory(_currentMonitorPath);
                    AddLog($"[在线检测] 监控目录不存在，已自动创建：{_currentMonitorPath}");
                }
                else
                {
                    AddLog($"[在线检测] 监控目录已存在，直接使用：{_currentMonitorPath}");
                }

                // 初始化监控器
                _onlineMonitorWatcher = new FileSystemWatcher(_currentMonitorPath)
                {
                    Filter = "*.tif",
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true,
                    InternalBufferSize = 65536 // 增大缓冲区，避免高频文件丢失事件
                };

                _onlineMonitorWatcher.Created -= OnOnlineFileCreated;
                _onlineMonitorWatcher.Created += OnOnlineFileCreated;
                _onlineMonitorWatcher.Error += (s, e) => AddLog($"[监控错误] {e.GetException().Message}");

                _onlineMonitorWatcher.EnableRaisingEvents = true;
                //启动日期检查定时器
                if (_dateCheckTimer == null)
                {
                    _dateCheckTimer = new System.Timers.Timer(0.5 * 60 * 1000); // 5分钟，避免过于频繁
                    _dateCheckTimer.Elapsed += OnDateCheckTimerElapsed;
                    _dateCheckTimer.AutoReset = true; // 自动重复触发
                }
                if (!_dateCheckTimer.Enabled)
                {
                    _dateCheckTimer.Start();
                }

                AddLog($"[在线检测] 启动成功，监控路径：{_currentMonitorPath}");
                //IsOnlineDetectionEnabled = true;
            }
            catch (Exception ex)
            {
                AddLog($"[在线检测] 启动失败：{ex.Message}");
                IsOnlineDetectionEnabled = false;
                System.Windows.MessageBox.Show($"监控启动失败：{ex.Message}", "在线检测错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void OnDateCheckTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // 避免并发冲突，加锁保护
            lock (this)
            {
                try
                {
                    // 关闭监控时不执行切换
                    if (!IsOnlineDetectionEnabled)
                    {
                        AddLog($"[跨天监控] 在线检测已关闭，跳过路径切换（当前日期：{DateTime.Now.ToString("yyyyMMdd")}）");
                        return;
                    }

                    var newDateFolder = DateTime.Now.ToString("yyyyMMdd");
                    // 仅当日期真正变更时才切换（避免首次启动时误判）
                    if (newDateFolder != _currentDateFolder)
                    {
                        AddLog($"[跨天监控] 日期变更：{_currentDateFolder} → {newDateFolder}，开始切换监控路径...");

                        //停止当前监控
                        StopOnlineMonitoring();
                        StartOnlineMonitoring();

                        AddLog($"[跨天监控] 路径切换完成，新监控路径：{_currentMonitorPath}");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"[跨天监控] 路径切换失败：{ex.Message}\n堆栈：{ex.StackTrace}");
                    // 切换失败时尝试重启监控，避免监控中断
                    if (IsOnlineDetectionEnabled)
                    {
                        StartOnlineMonitoring();
                    }
                }
            }
        }

        /// <summary>
        /// 生成目标监控路径（基础路径 + 日期 + 固定子路径）
        /// </summary>
        private string GetTargetMonitorPath()
        {
            if (string.IsNullOrEmpty(SelectedMonitorFolderPath))
            {
                // 抛出异常
                throw new InvalidOperationException("监控文件夹路径未设置 (SelectedMonitorFolderPath is null or empty)。");
            }

            return Path.Combine(
                SelectedMonitorFolderPath,
                _currentDateFolder ?? DateTime.Now.ToString("yyyyMMdd"),
                "268勿动",
                "原图归一化",
                "NG"
            );
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
            // 停止定时器
            if (_dateCheckTimer != null)
            {
                _dateCheckTimer.Stop();
                _dateCheckTimer.Dispose();
                _dateCheckTimer = null;
                AddLog("[在线检测] 已停止跨天检测定时器");
            }

            _currentMonitorPath = string.Empty;
            // 清空线程安全队列，重置状态
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
                    var allowedExts = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp", ".tif" };
                    var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                    if (!allowedExts.Contains(ext))
                    {
                        AddLog($"[跳过] 非图片格式：{Path.GetFileName(e.FullPath)}");
                        return;
                    }

                    //等待文件写入完成
                    bool isFileReady = false;
                    for (int i = 0; i < FileReadyMaxRetry; i++)
                    {
                        if (IsFileAccessible(e.FullPath))
                        {
                            isFileReady = true;
                            break;
                        }
                        // 第5次重试时输出日志
                        if (i == 5)
                            //AddLog($"[等待文件] {Path.GetFileName(e.FullPath)} 正在被写入，等待中...");
                            await Task.Delay(FileReadyRetryDelay);
                    }
                    if (!isFileReady)
                    {
                        AddLog($"[丢弃] {Path.GetFileName(e.FullPath)} 多次重试仍被占用（可能被其他程序锁定）");
                        return;
                    }
                    if (!File.Exists(e.FullPath))
                    {
                        AddLog($"[丢弃] {Path.GetFileName(e.FullPath)} 已被删除");
                        return;
                    }

                    _safeDetectQueue.Enqueue(e.FullPath);
                    //AddLog($"[加入队列] {Path.GetFileName(e.FullPath)}（当前队列长度：{_safeDetectQueue.Count}）");

                    //启动并发处理
                    StartConcurrentProcessing();
                }
                catch (Exception ex)
                {
                    AddLog($"[文件处理异常] {e.FullPath}：{ex.Message}");
                }
            });
        }


        // 启动并发处理线程
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
        /// 并发处理队列中的文件
        /// </summary>
        private async Task ProcessQueueConcurrent()
        {
            await _semaphore.WaitAsync();
            try
            {
                // 循环处理队列，直到队列为空
                while (_safeDetectQueue.TryDequeue(out string? filePath))
                {
                    // 再次校验文件存在性
                    if (!File.Exists(filePath))
                    {
                        AddLog($"[队列处理] {Path.GetFileName(filePath)} 已删除，跳过");
                        continue;
                    }

                    try
                    {
                        //AddLog($"[开始检测] {Path.GetFileName(filePath)}");
                        string detectResult = RedundancyDetection(filePath, "在线");
                        bool isOk = detectResult.StartsWith("检测结果：OK", StringComparison.OrdinalIgnoreCase);

                        // 异步更新UI
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            // 构造检测记录
                            dynamic detectionData = new ExpandoObject();
                            detectionData.FileName = Path.GetFileName(filePath);
                            detectionData.FilePath = filePath;
                            detectionData.DetectionResult = isOk ? "OK" : "NG";
                            detectionData.DetectionTime = DateTime.Now;
                        });

                        AddLog($"[检测完成] {Path.GetFileName(filePath)} → {detectResult}（队列剩余：{_safeDetectQueue.Count}）");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[检测异常] {Path.GetFileName(filePath)}：{ex.Message}\n堆栈：{ex.StackTrace}");
                        // 异常时也更新UI日志
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            AddLog($"[检测错误] {Path.GetFileName(filePath)}：{ex.Message}");
                        });
                    }
                }
            }
            finally
            {
                _semaphore.Release(); // 释放并发许可

                // 所有任务处理完后更新状态
                if (_safeDetectQueue.Count == 0 && _semaphore.CurrentCount == MaxConcurrent)
                {
                    _isQueueRunning = false;
                    //AddLog("[队列处理] 所有任务完成，线程已释放");
                }
            }
        }
        #endregion


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

        //滚轮缩放逻辑
        private void OnZoomRealTimeImage(MouseWheelEventArgs? e)
        {
            if (e == null) return;

            if (e.Delta > 0)
            {
                // 放大：不超过最大比例
                RealTimeImageScaleX = Math.Min(RealTimeImageScaleX + ZoomStep, MaxScale);
                RealTimeImageScaleY = RealTimeImageScaleX;
            }
            else
            {
                // 缩小：不低于最小比例
                RealTimeImageScaleX = Math.Max(RealTimeImageScaleX - ZoomStep, MinScale);
                RealTimeImageScaleY = RealTimeImageScaleX;
            }

            e.Handled = true;
        }

        // 辅助方法：判断文件是否可访问
        private bool IsFileAccessible(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return stream.CanRead; // 能打开且可读，说明文件就绪
                }
            }
            catch (IOException) // 文件锁定/正在写入
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void OnZoomInRealTime()
        {
            RealTimeImageScaleX = Math.Min(RealTimeImageScaleX + ZoomStep, MaxScale);
            RealTimeImageScaleY = RealTimeImageScaleX;
        }

        private void OnZoomOutRealTime()
        {
            RealTimeImageScaleX = Math.Max(RealTimeImageScaleX - ZoomStep, MinScale);
            RealTimeImageScaleY = RealTimeImageScaleX;
        }

        private void OnResetRealTimeZoom()
        {
            RealTimeImageScaleX = 1.0;
            RealTimeImageScaleY = 1.0;
            RealTimeImageTranslateX = 0; // 回到X轴初始位置
            RealTimeImageTranslateY = 0; // 回到Y轴初始位置
        }
        /// <summary>
        /// 鼠标按下：记录拖动起始状态
        /// </summary>
        private void OnImageMouseDown(MouseButtonEventArgs? e)
        {
            if (e == null) return;
            if (e.Source is System.Windows.Controls.Image image)
            {
                _isImageDragging = true;
                // 记录起始鼠标位置
                _startMousePoint = e.GetPosition(image);
                // 记录起始偏移量
                _startTranslateX = RealTimeImageTranslateX;
                _startTranslateY = RealTimeImageTranslateY;
                // 捕获鼠标
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


        //清空列表
        private void ExecuteClearList()
        {
            // 判断“文件检测”或“在线检测”任一运行，都视为“检测开启”
            if (!IsInAnyDetection)
            {
                System.Windows.MessageBox.Show(
                     "当前正在执行检测任务或处于在线监控模式，\n暂无法清空列表！\n请先结束检测/关闭监控后再尝试。",
                     "操作限制",
                     MessageBoxButton.OK,
                     MessageBoxImage.Warning);

                AddLog("[清空列表] 检测中/监控中，操作终止");
                return;
            }
            MessageBoxResult result = System.Windows.MessageBox.Show(
                "确定要清空图片列表吗？此操作不可恢复。",
                "确认清空",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.OK)
            {
                DetectionDataList.Clear();
                AddLog("[清空列表] 用户确认，已清空数据");
                _fullDetectionDataList.Clear();
            }
            else
            {
                AddLog("[清空列表] 用户取消，操作终止");
            }
        }

        // 统计清零
        private void ExecuteClearStatistics()
        {
            // 判断“文件检测”或“在线检测”任一运行，都视为“检测开启”
            if (!IsInAnyDetection)
            {
                System.Windows.MessageBox.Show(
                     "当前正在执行检测任务或处于在线监控模式，\n暂无法重置统计数据！\n请先结束检测/关闭监控后再尝试。",
                     "操作限制",
                     MessageBoxButton.OK,
                     MessageBoxImage.Warning);

                AddLog("[统计清零] 检测中/监控中，操作终止");
                return;
            }
            // 弹出确认对话框
            MessageBoxResult result = System.Windows.MessageBox.Show(
                "确定要重置统计数据吗？此操作不可恢复。",
                "确认清零",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.OK)
            {
                _fullDetectionDataList.Clear();
                OKCount = 0;
                NGCount = 0;
                Count_L0 = 0;
                Count_L1 = 0;
                Count_L2 = 0;
                Count_L3 = 0;
                OnPropertyChanged(nameof(TotalDetectionCount));
                OnPropertyChanged(nameof(OKCount));
                OnPropertyChanged(nameof(NGCount));
                OnPropertyChanged(nameof(Count_L0));
                OnPropertyChanged(nameof(Count_L1));
                OnPropertyChanged(nameof(Count_L2));
                OnPropertyChanged(nameof(Count_L3));
                UpdatePieChartData();
                AddLog("[统计清零] 用户确认，已重置统计");
            }
            else
            {
                AddLog("[统计清零] 用户取消，操作终止");
            }
        }

        //修改停止检测命令的执行逻辑
        private void ExecuteStopDetection()
        {
            // 判断“文件检测”或“在线检测”任一运行
            bool isAnyDetectionRunning = _isDetecting || IsOnlineDetectionEnabled;
            if (!isAnyDetectionRunning)
            {
                System.Windows.MessageBox.Show("当前检测未开启", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            //根据当前检测类型，动态提示确认信息
            string detectType = string.Empty;
            if (_isDetecting && IsOnlineDetectionEnabled)
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


            var result = System.Windows.MessageBox.Show(
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
                    _isDetecting = false;
                    IsFileDetecting = false;
                    if (IsOnlineDetectionEnabled)
                    {
                        StopOnlineMonitoring();
                        IsOnlineDetectionEnabled = false;
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"停止检测失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        /// <summary>
        /// 计算 L0-L3 的占比
        /// </summary>
        private void UpdateRiskRatios()
        {
            // 总计数 = L0+L1+L2+L3
            int totalCount = Count_L0 + Count_L1 + Count_L2 + Count_L3;
            // 避免除以零
            if (totalCount == 0)
            {
                Ratio_L0 = "0.00%";
                Ratio_L1 = "0.00%";
                Ratio_L2 = "0.00%";
                Ratio_L3 = "0.00%";
                return;
            }
            // 计算各等级占比
            Ratio_L0 = $"{(double)Count_L0 / totalCount * 100:F2}%";
            Ratio_L1 = $"{(double)Count_L1 / totalCount * 100:F2}%";
            Ratio_L2 = $"{(double)Count_L2 / totalCount * 100:F2}%";
            Ratio_L3 = $"{(double)Count_L3 / totalCount * 100:F2}%";
            SaveConfig();
        }
        /// <summary>
        /// 初始化饼状图
        /// </summary>
        private void InitPieChart()
        {
            PieModel = new PlotModel
            {
                Title = "状态占比",
                IsLegendVisible = false,
                PlotMargins = new OxyThickness(20)
            };

            var pieSeries = new PieSeries
            {
                StrokeThickness = 1,
                InsideLabelPosition = 0.5,
                InsideLabelFormat = "",
                AngleSpan = 360,
                StartAngle = 90
            };

            // 初始值（Label 在构造函数中传）
            pieSeries.Slices.Add(new PieSlice("OK", 0)
            {
                Fill = OxyColor.FromArgb(255, 47, 128, 237) // #2F80ED
            });
            pieSeries.Slices.Add(new PieSlice("NG", 0)
            {
                Fill = OxyColor.FromArgb(255, 255, 152, 0) // #FF9800
            });

            PieModel.Series.Add(pieSeries);
        }

        // 更新饼图数据
        private void UpdatePieChartData()
        {
            if (PieModel?.Series[0] is PieSeries pieSeries)
            {
                pieSeries.Slices.Clear();

                //int total = OKCount + NGCount;
                // 处理初始状态：总计数为0时，强制显示 OK=100.00%
                if (TotalDetectionCount == 0)
                {
                    pieSeries.Slices.Add(new PieSlice("OK 100.00%", 1)
                    {
                        Fill = OxyColor.FromArgb(255, 47, 128, 237)
                    });
                    pieSeries.Slices.Add(new PieSlice("NG 0.00%", 0)
                    {
                        Fill = OxyColor.FromArgb(255, 255, 152, 0)
                    });
                }
                else
                {
                    pieSeries.Slices.Add(new PieSlice($"OK {OKRate}", OKCount)
                    {
                        Fill = OxyColor.FromArgb(255, 47, 128, 237)
                    });
                    pieSeries.Slices.Add(new PieSlice($"NG {NGRate}", NGCount)
                    {
                        Fill = OxyColor.FromArgb(255, 255, 152, 0)
                    });
                }

                PieModel.InvalidatePlot(true);
            }
        }

        /// <summary>
        /// 选择单个图片文件逻辑
        /// </summary>
        /// <param name="parameter"></param>
        private async void SelectImageFile()
        {

            var allowedExtensions = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp", ".tif" };
            bool isSelected = false;

            while (!isSelected)
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "图片文件|*.bmp;*.jpg;*.jpeg;*.png;*.gif;*.tiff;*.webp;*.tif|所有文件|*.*",
                    FilterIndex = 1,
                    Title = "选择检测图片（仅支持图片格式）",
                    Multiselect = false
                };

                bool? dialogResult = openFileDialog.ShowDialog();
                if (dialogResult != true) break;

                string selectedPath = openFileDialog.FileName;
                if (string.IsNullOrWhiteSpace(selectedPath))
                {
                    System.Windows.MessageBox.Show("请选择一张图片文件后再点击确定！", "未选择图片", MessageBoxButton.OK, MessageBoxImage.Warning);
                    continue;
                }

                string fileExtension = Path.GetExtension(selectedPath).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    System.Windows.MessageBox.Show(
                        $"所选文件格式不支持！\n请选择以下格式的图片：\n{string.Join("、", allowedExtensions)}",
                        "格式错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    continue;
                }

                // 构造保存设置描述文本
                StringBuilder saveSettings = new StringBuilder();
                if (IsSaveOriginalOK) saveSettings.Append("保存OK原图；");
                if (IsSaveOriginalNG) saveSettings.Append("保存NG原图；");
                if (IsSaveResultOK) saveSettings.Append("保存OK结果图；");
                if (IsSaveResultNG) saveSettings.Append("保存NG结果图；");

                string saveInfo = saveSettings.Length > 0
                    ? $"保存设置：{saveSettings.ToString().TrimEnd('；')}"
                    : "保存设置：不保存任何图片";

                // 显示确认框并判断结果
                var confirm = System.Windows.MessageBox.Show(
                    saveInfo,
                    "确认检测", MessageBoxButton.OKCancel, MessageBoxImage.Question);

                if (confirm != MessageBoxResult.OK)
                {
                    continue;
                }

                // 用户确认后，执行检测逻辑
                isSelected = true;
                SelectedImagePath = selectedPath;
                AddLog($"选择图片路径：{selectedPath}\t{saveInfo}");
                await GetDataFromSingleFile(selectedPath);
            }
        }
        private async Task GetDataFromSingleFile(string filePath)
        {
            IsFileDetecting = true;
            _isDetecting = true;
            try
            {
                AddLog($"开始检测单张图片：{Path.GetFileName(filePath)}");
                await Task.Run(() =>
                {
                    // 仅调用检测，不重复添加数据
                    string detectResult = RedundancyDetection(filePath, "离线");
                    if (detectResult.Equals("异常（空图片）", StringComparison.OrdinalIgnoreCase))
                    {
                        AddLog($"[单张检测] 空图片不统计：{Path.GetFileName(filePath)}");
                        return;
                    }
                });
            }
            catch (Exception ex)
            {
                AddLog($"{ex}");
            }
            finally
            {
                _isDetecting = false;
                IsFileDetecting = false;
            }
        }
        /// <summary>
        /// 更新显示集合，仅保留完整集合中最新的50条记录
        /// </summary>
        private void UpdateDisplayDataList()
        {
            DetectionDataList.Clear();
            // 筛选所有有效数据，按检测时间倒序，取最新50条
            var latestData = _fullDetectionDataList
                .Where(data => data != null && !string.IsNullOrEmpty(data?.DetectionResult))
                .OrderByDescending(data => data.DetectionTime)
                .Take(300)
                .ToList();

            // 添加到显示列表
            foreach (var data in latestData)
            {
                DetectionDataList.Add(data);
            }
            //AddLog($"[列表更新] 显示最新{latestData.Count}条检测数据（总计：{_fullDetectionDataList.Count}条）");
        }
        #region 文件夹选择逻辑
        /// <summary>
        /// 文件夹选择
        /// </summary>
        private async void SelectImageFolder()
        {
            var allowedExtensions = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp", ".tif" };

            try
            {
                var folderDialog = new OpenFolderDialog();

                // 对话框属性配置
                folderDialog.Title = "选择包含图片的文件夹（支持子目录）";
                folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                folderDialog.ValidateNames = true;
                folderDialog.Multiselect = false;

                // 显示对话框并获取结果
                bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);

                // 取消后重置状态
                if (result != true)
                {
                    ResetFolderSelectionStatus();
                    return;
                }

                // 构造保存设置描述文本
                StringBuilder saveSettings = new StringBuilder();
                if (IsSaveOriginalOK) saveSettings.Append("保存OK原图；");
                if (IsSaveOriginalNG) saveSettings.Append("保存NG原图；");
                if (IsSaveResultOK) saveSettings.Append("保存OK结果图；");
                if (IsSaveResultNG) saveSettings.Append("保存NG结果图；");

                string saveInfo = saveSettings.Length > 0
                    ? $"保存设置：{saveSettings.ToString().TrimEnd('；')}"
                    : "保存设置：不保存任何图片";

                // 获取选中的文件夹路径
                string selectedFolder = folderDialog.FolderName;

                // 获取文件夹中所有支持的图片
                List<string> allImageFiles = GetImageFilesInFolder(selectedFolder, allowedExtensions);
                UpdateFolderSelectionStatus(selectedFolder, allImageFiles.Count);

                // 无图片文件提示
                if (allImageFiles.Count == 0)
                {
                    var retryResponse = System.Windows.MessageBox.Show(
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

                // 确认批量检测
                var confirm = System.Windows.MessageBox.Show(
                    saveInfo,
                    "确认批量检测", MessageBoxButton.OKCancel, MessageBoxImage.Question);

                if (confirm == MessageBoxResult.OK)
                {
                    AddLog($"选择图片路径：{selectedFolder}\t{saveInfo}");
                    await GetDataFromFolder(selectedFolder, allImageFiles);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Windows.MessageBox.Show($"访问文件夹失败：没有权限访问该文件夹\n{ex.Message}", "权限不足", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"访问文件夹失败：没有权限访问该文件夹\n{ex.Message}");
                ResetFolderSelectionStatus();
            }
            catch (PathTooLongException ex)
            {
                System.Windows.MessageBox.Show($"文件夹路径过长：请选择路径较短的文件夹\n{ex.Message}", "路径过长", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"文件夹路径过长：请选择路径较短的文件夹\n{ex.Message}");
                ResetFolderSelectionStatus();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"文件夹选择过程出错：{ex.Message}", "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetFolderSelectionStatus();
            }
        }

        /// <summary>
        /// 文件夹批量检测
        /// </summary>
        private async Task GetDataFromFolder(string folderPath, List<string> imageFiles)
        {
            //简化筛选逻辑
            List<string> filteredFiles = imageFiles.ToList(); // 去除无意义的循环添加

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
            try
            {
                await Task.Run(() =>
                {
                    int currentBatchCount = 0; // 批次计数

                    // 遍历筛选后的文件列表
                    foreach (var filePath in filteredFiles)
                    {
                        if (token.IsCancellationRequested)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                AddLog("检测已终止，正在清理资源...");
                            });
                            break;
                        }
                        if (!File.Exists(filePath))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                AddLog($"[跳过] 文件不存在：{Path.GetFileName(filePath)}");
                            });
                            continue;
                        }

                        try
                        {
                            // 记录单张检测开始时间
                            long startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                            if (token.IsCancellationRequested) break;
                            string detectResult = RedundancyDetection(filePath, "离线");
                            if (detectResult.Equals("异常（空图片）", StringComparison.OrdinalIgnoreCase))
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    AddLog($"[跳过] 空图片：{Path.GetFileName(filePath)}");
                                });
                                continue;
                            }

                            // 计算单张检测耗时
                            long endTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                            long singleDetectMs = endTime - startTime;
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                AddLog($"[单张耗时] {Path.GetFileName(filePath)}：{singleDetectMs}ms");
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                AddLog($"[检测失败] {Path.GetFileName(filePath)}，原因：{ex.Message}");
                            });
                        }
                    }

                    // 检测结束或终止后的最终处理
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _isDetecting = false;
                        if (token.IsCancellationRequested)
                        {
                            AddLog($"[检测终止] 已处理 {currentBatchCount}/{totalCount} 张图片");
                        }
                        else
                        {
                            OnDetectionCompleted(totalCount);
                            AddLog($"[批量完成] 共处理 {totalCount} 张图片，全部检测结束");
                        }
                        //UpdateDisplayDataList(); // 最终刷新一次UI，确保数据完整
                    });
                }, token);
            }
            catch (OperationCanceledException)
            {
                // 任务被取消时的处理
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AddLog("检测已被用户取消");
                    _isDetecting = false;
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"批量检测异常：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    AddLog($"[批量异常] {ex.Message}");
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
        private List<string> GetImageFilesInFolder(string folderPath, HashSet<string> allowedExtensions)
        {
            // 实现图片文件筛选逻辑
            return Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(file => allowedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();
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

        // 查看历史结果相关状态
        private bool _isViewingHistory = false; // 是否处于查看历史状态
        private System.Timers.Timer? _viewHistoryTimer; // 5秒自动刷新定时器
        private dynamic? _latestDetectionData = null; // 缓存最新的检测结果
        private readonly object _viewStatusLocker = new object();
        //记录开始查看历史时的最新检测数据
        private dynamic? _latestDetectionDataAtViewStart = null;

        /// <summary>
        /// 显示最新的实时检测结果
        /// </summary>
        private void ShowLatestDetectionResult()
        {
            // 清空当前显示
            CurrentOriginalImage = null;
            CurrentResultImage = null;

            // 优先使用缓存的最新结果，无缓存则从完整列表取最后一条
            dynamic? latestData = _latestDetectionData;
            if (latestData == null && _fullDetectionDataList != null && _fullDetectionDataList?.Count > 0)
            {
                // 从完整列表取最后一条
                latestData = _fullDetectionDataList?.Last();
                _latestDetectionData = latestData;
            }

            if (latestData == null)
            {
                AddLog("[实时显示] 暂无检测记录");
                return;
            }

            // 提取最新结果的图片路径
            string? resultImagePath = null;
            string? originalImagePath = null;
            if (latestData is ExpandoObject expandoData)
            {
                var dataDict = (IDictionary<string, object?>)expandoData;
                if (dataDict.TryGetValue("ResultImagePath", out object? resPathObj) && resPathObj is string resPath)
                    resultImagePath = resPath;
                if (dataDict.TryGetValue("OriginalImagePath", out object? origPathObj) && origPathObj is string origPath)
                    originalImagePath = origPath;
            }

            // 加载最新图片
            if (!string.IsNullOrEmpty(originalImagePath) && File.Exists(originalImagePath))
                LoadImage(originalImagePath, isResultImage: false);
            else
                AddLog($"[实时显示] 最新原图不存在：{originalImagePath}");

            if (!string.IsNullOrEmpty(resultImagePath) && File.Exists(resultImagePath))
                LoadImage(resultImagePath, isResultImage: true);
            else
                AddLog($"[实时显示] 最新结果图不存在：{resultImagePath}");

            //AddLog($"[实时显示] 已更新最新检测结果：{latestData.cellCode}");
        }

        // 执行查看结果命令
        private void ExecuteViewResult(dynamic? data)
        {
            // 停止可能正在运行的旧定时器
            lock (_viewStatusLocker)
            {
                if (_viewHistoryTimer != null)
                {
                    _viewHistoryTimer.Stop();
                    _viewHistoryTimer.Dispose();
                    _viewHistoryTimer = null;
                }
                // 标记为查看历史状态
                _isViewingHistory = true;
                //记录此时的最新检测数据
                _latestDetectionDataAtViewStart = _latestDetectionData;
            }

            // 初始清空图片
            CurrentOriginalImage = null;
            CurrentResultImage = null;

            if (data == null)
            {
                System.Windows.MessageBox.Show("请先在表格中选择一条检测记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                // 重置查看状态
                lock (_viewStatusLocker)
                {
                    _isViewingHistory = false;
                    _latestDetectionDataAtViewStart = null;
                }
                return;
            }

            string? resultImagePath = null;
            string? originalImagePath = null;

            // 正确提取路径
            if (data is ExpandoObject expandoData)
            {
                var dataDict = (IDictionary<string, object?>)expandoData;
                // 提取结果图路径
                if (dataDict.TryGetValue("ResultImagePath", out object? resPathObj) && resPathObj is string resPath)
                {
                    resultImagePath = resPath;
                }
                // 提取原图路径
                if (dataDict.TryGetValue("OriginalImagePath", out object? origPathObj) && origPathObj is string origPath)
                {
                    originalImagePath = origPath;
                }
            }

            // 加载历史图片
            if (!string.IsNullOrEmpty(originalImagePath) && File.Exists(originalImagePath))
            {
                LoadImage(originalImagePath, isResultImage: false);
            }
            else
            {
                AddLog($"[查看失败] 原图不存在：{originalImagePath}");
                CurrentOriginalImage = null;
            }

            if (!string.IsNullOrEmpty(resultImagePath) && File.Exists(resultImagePath))
            {
                LoadImage(resultImagePath, isResultImage: true);
            }
            else
            {
                AddLog($"[查看失败] 结果图不存在：{resultImagePath}");
                CurrentResultImage = null;
            }

            // 启动5秒定时器：到期后判断是否有新图
            lock (_viewStatusLocker)
            {
                _viewHistoryTimer = new System.Timers.Timer(5000); // 5秒 = 5000毫秒
                _viewHistoryTimer.AutoReset = false; // 仅执行一次
                _viewHistoryTimer.Elapsed += (s, e) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        lock (_viewStatusLocker)
                        {
                            // 对比当前最新数据是否与查看历史时的最新数据不同
                            bool hasNewData = _latestDetectionData != _latestDetectionDataAtViewStart;

                            if (hasNewData)
                            {
                                // 有新图：切换回最新实时结果
                                _isViewingHistory = false;
                                _viewHistoryTimer?.Dispose();
                                _viewHistoryTimer = null;
                                _latestDetectionDataAtViewStart = null; // 清除基准

                                ShowLatestDetectionResult();
                                AddLog("[自动刷新] 5秒已到且有新检测结果，切换回实时显示");
                            }
                            else
                            {
                                // 无新图：保持历史查看状态，不切换
                                _viewHistoryTimer?.Dispose();
                                _viewHistoryTimer = null;
                                AddLog("[自动刷新] 5秒已到但无新检测结果，保持历史图片显示");
                            }
                        }
                    });
                };
                _viewHistoryTimer.Start();
            }
        }

        // 通用图片加载方法
        private bool LoadImage(string imagePath, bool isResultImage)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                AddLog($"[加载失败] 图片不存在：{imagePath}");
                return false;
            }

            try
            {
                using (var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = fileStream;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    // 仅在查看时赋值，平时为null
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (isResultImage) CurrentResultImage = bitmap;
                        else CurrentOriginalImage = bitmap;
                    });
                    return true;
                }
            }
            catch (Exception ex)
            {
                AddLog($"[加载异常] {ex.Message}（路径：{imagePath}）");
                return false;
            }
        }


        private string? _currentCsvDate;

        /// <summary>
        /// 初始化CSV文件（适配新表头）
        /// </summary>
        private void InitCsvFile(string imagePath)
        {
            lock (_csvLock)
            {
                try
                {
                    // 基础参数
                    string rootPath = "D:";
                    string fileName = "极耳检测结果.csv";
                    string today = DateTime.Now.ToString("yyyy-MM-dd"); // 当天日期（离线专用）
                    string imageDateStr = GetImageDateFromPath(imagePath);

                    // 校验根目录
                    if (!Directory.Exists(rootPath))
                    {
                        throw new Exception("D盘不存在，无法初始化CSV文件");
                    }

                    //根据在线/离线构建路径
                    List<string> pathSegments = new List<string> { rootPath };
                    if (IsOnlineDetectionEnabled)
                    {
                        // 在线路径：D:\JR_AI结果_在线\【图片提取的日期】\CSV
                        pathSegments.AddRange(new[] { "JR_AI结果_在线", imageDateStr });
                    }
                    else
                    {
                        // 离线路径：D:\JR_AI结果_离线\【当天日期】\【图片提取的日期】\CSV
                        pathSegments.AddRange(new[] { "JR_AI结果_离线", today, imageDateStr });
                    }

                    //生成完整目录和文件路径
                    string csvDir = Path.Combine(pathSegments.ToArray());
                    Directory.CreateDirectory(csvDir); // 确保目录存在
                    _currentCsvFilePath = Path.Combine(csvDir, fileName);

                    //缓存路径
                    _csvCacheTempPath = !string.IsNullOrEmpty(_currentCsvFilePath)
                        ? Path.ChangeExtension(_currentCsvFilePath, ".csv.cache")
                        : null;

                    while (_csvCacheQueue.TryDequeue(out _)) { }

                    // 首次创建时写入表头
                    if (!File.Exists(_currentCsvFilePath))
                    {
                        var header = new List<string[]>
                        {
                            new[] { "日期", "电芯码", "正负极", "左侧距离dl", "右侧距离dr", "检测结果", "风险等级" }
                        };
                        WriteCsvLines(header);
                        AddLog($"[CSV初始化] 已创建CSV文件：{_currentCsvFilePath}");
                    }

                    _currentCsvDate = today;
                    AddLog($"[CSV初始化] 当前CSV日期已更新为：{_currentCsvDate}");

                    //启动跨天检查定时器
                    if (!IsOnlineDetectionEnabled)
                    {
                        InitCsvDateCheckTimer();
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"[CSV初始化失败] {ex.Message}");
                    _currentCsvFilePath = null;
                    _csvCacheTempPath = null;
                    _currentCsvDate = null;
                    while (_csvCacheQueue.TryDequeue(out _)) { }
                }
            }
        }

        /// <summary>
        /// 获取最近一次检测的图片路径
        /// </summary>
        private string? GetLatestImagePath()
        {
            // 优先从最新检测数据中获取
            if (_latestDetectionData is ExpandoObject latestExpando)
            {
                var dict = (IDictionary<string, object?>)latestExpando;
                if (dict.TryGetValue("OriginalImagePath", out object? pathObj) && pathObj is string path)
                {
                    return path;
                }
            }

            // 从完整检测列表中取最后一条
            if (_fullDetectionDataList != null && _fullDetectionDataList.Count > 0)
            {
                var lastData = _fullDetectionDataList.Last();
                if (lastData is ExpandoObject lastExpando)
                {
                    var dict = (IDictionary<string, object?>)lastExpando;
                    if (dict.TryGetValue("OriginalImagePath", out object? pathObj) && pathObj is string path)
                    {
                        return path;
                    }
                }
            }
            return null;
        }
        // 初始化跨天检查定时器
        private void InitCsvDateCheckTimer()
        {
            if (_dateCheckTimer == null)
            {
                _dateCheckTimer = new System.Timers.Timer(0.5 * 60 * 1000); // 30秒
                _dateCheckTimer.Elapsed += OnCsvDateCheckTimerElapsed;
                _dateCheckTimer.AutoReset = true;
                _dateCheckTimer.Start();
                //AddLog("[CSV日期检查] 跨天检查定时器已启动（每30分钟检查一次）");
            }
        }
        // 跨天检查：若日期变更，切换到新CSV文件
        private void OnCsvDateCheckTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            lock (_csvLock)
            {
                try
                {
                    string today = DateTime.Now.ToString("yyyy-MM-dd");
                    // 仅在在线模式下进行跨天检查
                    if (IsOnlineDetectionEnabled && !string.IsNullOrEmpty(_currentCsvDate) && _currentCsvDate != today)
                    {
                        AddLog($"[CSV跨天切换] 日期变更：{_currentCsvDate} → {today}，开始切换文件...");

                        //处理前一天的缓存数据
                        if (_csvCacheQueue.Count > 0 && !string.IsNullOrEmpty(_currentCsvFilePath))
                        {
                            AddLog($"[CSV跨天切换] 处理前一天缓存数据（{_csvCacheQueue.Count}条）...");
                            bool flushSuccess = FlushCacheToCsv();
                            if (flushSuccess)
                            {
                                AddLog($"[CSV跨天切换] 前一天缓存数据已写入旧文件");
                            }
                            else
                            {
                                AddLog($"[CSV跨天切换] 前一天缓存数据写入失败，已放弃");
                                while (_csvCacheQueue.TryDequeue(out _)) { } // 清空旧缓存
                            }
                        }

                        //获取最近的图片路径
                        string? latestImagePath = GetLatestImagePath();
                        if (string.IsNullOrEmpty(latestImagePath))
                        {
                            AddLog($"[CSV跨天切换] 未找到最近图片，使用当天日期作为图片日期默认值");
                            latestImagePath = Path.Combine(today, "default.jpg"); // 临时路径，用于提取日期
                        }

                        //初始化新的当天文件
                        InitCsvFile(latestImagePath);

                        AddLog($"[CSV跨天切换] 已切换到当天文件：{_currentCsvFilePath}");
                    }
                    // 离线模式直接跳过跨天检查
                    else if (!IsOnlineDetectionEnabled)
                    {
                        // AddLog($"[CSV跨天检查] 离线模式，不进行跨天切换");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"[CSV跨天检查异常] {ex.Message}");
                }
            }
        }
        /// <summary>
        /// 向CSV写入一行极耳检测结果
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <param name="cellCode">电芯码</param>
        /// <param name="polarity">正负极</param>
        /// <param name="leftDl">左侧距离dl</param>
        /// <param name="rightDr">右侧距离dr</param>
        /// <param name="detectResult">检测结果（OK/NG）</param>
        /// <param name="riskLevel">风险等级</param>
        private void WriteToCsv(
                 string imagePath,
                 string cellCode,
                 string polarity,
                 double? leftDl,
                 double? rightDr,
                 string detectResult,
                 string riskLevel)
        {
            // 写入前检查日期是否变更，若变更则重新初始化
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(_currentCsvDate) || _currentCsvDate != today)
            {
                //AddLog($"[CSV写入] 检测到日期变更，重新初始化文件...");
                InitCsvFile(imagePath);
            }

            if (string.IsNullOrEmpty(_currentCsvFilePath))
            {
                AddLog($"[CSV写入失败] 文件未初始化，无法写入数据");
                return;
            }

            try
            {
                // 从路径提取日期
                string date = GetImageDateFromPath(imagePath);

                // 处理空值
                string leftDlStr = (leftDl.HasValue && leftDl.Value != -1) ? leftDl.Value.ToString("F2") : "无";
                string rightDrStr = (rightDr.HasValue && rightDr.Value != -1) ? rightDr.Value.ToString("F2") : "无";
                string safeRiskLevel = riskLevel ?? "无";

                // 构建数据行
                var dataLine = new[]
                {
                    date,           // 日期
                    cellCode,       // 电芯码
                    polarity,       // 正负极
                    leftDlStr,      // 左侧距离dl
                    rightDrStr,     // 右侧距离dr
                    detectResult,   // 检测结果
                    safeRiskLevel   // 风险等级
                };

                // 批量写入
                WriteCsvLines(new List<string[]> { dataLine });
            }
            catch (Exception ex)
            {
                AddLog($"[CSV写入失败] {ex.Message}，图片路径：{imagePath}");
            }
        }


        /// <summary>
        /// 批量写入CSV行
        /// </summary>
        private void WriteCsvLines(List<string[]> lines)
        {
            lock (_csvLock)
            {
                try
                {
                    if (string.IsNullOrEmpty(_currentCsvFilePath))
                    {
                        AddLog("[CSV写入失败] 当前CSV文件路径为空，请先初始化");
                        if (!string.IsNullOrEmpty(_csvCacheTempPath))
                        {
                            lines.ForEach(line => _csvCacheQueue.Enqueue(line));
                            SaveCacheToTempFile();
                        }
                        else
                        {
                            AddLog("[CSV缓存] 缓存路径未初始化，暂不缓存");
                        }
                        return;
                    }

                    // 尝试直接写入
                    bool writeSuccess = TryWriteLinesDirectly(lines);
                    if (writeSuccess)
                    {
                        if (_csvCacheQueue.Count > 0 && !string.IsNullOrEmpty(_csvCacheTempPath))
                        {
                            Task.Run(() => FlushCacheToCsv());
                        }
                        return;
                    }

                    // 写入失败则缓存
                    lines.ForEach(line => _csvCacheQueue.Enqueue(line));
                    SaveCacheToTempFile();
                    StartCacheMonitor();
                }
                catch (Exception ex)
                {
                    AddLog($"[CSV写入异常] {ex.Message}");
                    if (!string.IsNullOrEmpty(_csvCacheTempPath))
                    {
                        lines.ForEach(line => _csvCacheQueue.Enqueue(line));
                        SaveCacheToTempFile();
                    }
                }
            }
        }

        //检查是否有权限执行操作
        private bool CheckPermission()
        {
            if (!_userCenterViewModel.IsLoggedIn)
            {
                MessageBoxResult result = System.Windows.MessageBox.Show(
                    "请先登录以执行此操作！\n点击“确定”跳转到登录页。",
                    "操作需要登录",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information
                );
                if (result == MessageBoxResult.OK)
                {
                    _navigationService.NavigateTo("UserCenter");
                }
                return false;
            }

            //检查权限级别
            if (_userCenterViewModel.RoleID < (int)UserRole.Engineer)
            {
                System.Windows.MessageBox.Show(
                    "您的权限等级不足，无法执行此操作。\n需要【工程师】或以上权限。",
                    "权限不足",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return false;
            }
            return true;
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
                        // 处理特殊字符
                        string safeLine = string.Join(",", line.Select(field => $"\"{field.Replace("\"", "\"\"")}\""));
                        writer.WriteLine(safeLine);
                    }
                }
                return true;
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process"))
            {
                return false; // 文件被占用，返回失败
            }
            catch (Exception)
            {
                return false;
            }
        }


        /// <summary>
        /// 缓存持久化到临时文件
        /// </summary>
        private void SaveCacheToTempFile()
        {
            try
            {
                if (string.IsNullOrEmpty(_csvCacheTempPath) || _csvCacheQueue.Count == 0)
                {
                    string msg = string.IsNullOrEmpty(_csvCacheTempPath) ? "缓存路径未初始化" : "缓存队列为空";
                    AddLog($"[缓存持久化] {msg}，暂不持久化");
                    return;
                }

                //确保缓存文件目录存在
                string? cacheDir = Path.GetDirectoryName(_csvCacheTempPath);
                if (!string.IsNullOrEmpty(cacheDir) && !Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                    AddLog($"缓存目录不存在，已创建：{cacheDir}");
                }

                // 写入缓存数据
                var cacheLines = _csvCacheQueue.Select(line => string.Join(",", line.Select(field => $"\"{field.Replace("\"", "\"\"")}\""))).ToList();
                File.WriteAllLines(_csvCacheTempPath, cacheLines, Encoding.UTF8);
                //AddLog($"[缓存持久化] 已写入{cacheLines.Count}条数据到缓存文件：{_csvCacheTempPath}");
            }
            catch (Exception ex)
            {
                AddLog($"[缓存持久化失败] {ex.Message}（数据暂存内存）");
            }
        }


        /// <summary>
        /// 启动缓存监控任务
        /// </summary>
        private void StartCacheMonitor()
        {
            if (_isFlushingCache) return;

            _isFlushingCache = true;
            Task.Run(async () =>
            {
                try
                {
                    while (_csvCacheQueue.Count > 0)
                    {
                        bool flushSuccess = FlushCacheToCsv();
                        if (flushSuccess)
                        {
                            if (File.Exists(_csvCacheTempPath))
                                File.Delete(_csvCacheTempPath);
                            break;
                        }
                        await Task.Delay(5000); // 每5秒重试一次
                    }
                }
                finally
                {
                    _isFlushingCache = false;
                }
            });
        }


        /// <summary>
        /// 缓存数据补写到CSV
        /// </summary>
        private bool FlushCacheToCsv()
        {
            lock (_csvLock)
            {
                if (_csvCacheQueue.Count == 0) return true;
                if (string.IsNullOrEmpty(_currentCsvFilePath))
                {
                    AddLog($"[缓存补写失败] CSV主文件路径未初始化");
                    return false;
                }

                var linesToWrite = new List<string[]>();
                try
                {
                    // 取出缓存数据
                    while (_csvCacheQueue.TryDequeue(out var line))
                    {
                        linesToWrite.Add(line);
                    }

                    // 关键修复1：主文件被删除时，重新创建并写入表头
                    if (!File.Exists(_currentCsvFilePath))
                    {
                        var header = new List<string[]>
                {
                    new[] { "日期", "电芯码", "正负极", "左侧距离dl", "右侧距离dr", "检测结果", "风险等级" }
                };
                        WriteCsvLines(header); // 重新创建表头
                        AddLog($"[缓存补写] CSV主文件已被删除，重新创建：{_currentCsvFilePath}");
                    }

                    // 写入CSV（追加模式）
                    using (var fs = new FileStream(
                        _currentCsvFilePath,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite)) // 允许读写共享，减少文件占用冲突
                    using (var writer = new StreamWriter(fs, Encoding.UTF8))
                    {
                        foreach (var line in linesToWrite)
                        {
                            // 关键修复2：字段转义（处理逗号、引号等特殊字符，避免CSV格式错乱）
                            string safeLine = string.Join(",", line.Select(field =>
                                $"\"{field?.Replace("\"", "\"\"") ?? string.Empty}\""));
                            writer.WriteLine(safeLine);
                        }
                    }

                    // 清理缓存文件（仅当写入成功后）
                    if (!string.IsNullOrEmpty(_csvCacheTempPath) && File.Exists(_csvCacheTempPath))
                    {
                        File.Delete(_csvCacheTempPath);
                        AddLog($"[缓存清理] 已删除临时缓存文件：{_csvCacheTempPath}");
                    }

                    //AddLog($"[缓存补写成功] 已写入{linesToWrite.Count}条数据到CSV：{_currentCsvFilePath}");
                    return true;
                }
                catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                {
                    // 回退数据到缓存（逆序入队，保持原顺序）
                    foreach (var line in linesToWrite.AsEnumerable().Reverse())
                    {
                        _csvCacheQueue.Enqueue(line);
                    }
                    AddLog($"[缓存补写失败] CSV文件被其他程序占用，数据已回退到缓存");
                    return false;
                }
                catch (Exception ex)
                {
                    AddLog($"[缓存补写失败] {ex.Message}\n堆栈：{ex.StackTrace}");
                    // 回退数据到缓存
                    foreach (var line in linesToWrite.AsEnumerable().Reverse())
                    {
                        _csvCacheQueue.Enqueue(line);
                    }
                    return false;
                }
            }
        }


        /// <summary>
        /// 检测完成后处理
        /// </summary>
        private void OnDetectionCompleted(int totalProcessed)
        {
            AddLog($"[检测完成] 共处理 {totalProcessed} 张图片，任务结束");

            if (_csvCacheQueue.Count > 0)
            {
                AddLog($"[缓存状态] 发现 {_csvCacheQueue.Count} 行数据暂存缓存（可能被Excel占用）");
            }
            else
            {
                AddLog($"[缓存状态] 所有数据已写入CSV，无缓存积压");
            }

            CheckCsvFileInUseAndPrompt();
        }


        /// <summary>
        /// 检查CSV是否被占用并提示
        /// </summary>
        private void CheckCsvFileInUseAndPrompt()
        {
            if (string.IsNullOrEmpty(_currentCsvFilePath)) return;

            bool isFileInUse = IsFileInUse(_currentCsvFilePath);

            if (isFileInUse && _csvCacheQueue.Count > 0)
            {
                ShowExcelOpenPrompt();
            }
            else if (isFileInUse)
            {
                AddLog("[提示] CSV文件仍被Excel占用（不影响已写入数据）");
            }
        }


        /// <summary>
        /// 判断文件是否被占用
        /// </summary>
        private bool IsFileInUse(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
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


        /// <summary>
        /// 显示Excel占用提示
        /// </summary>
        private void ShowExcelOpenPrompt()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(new Action(ShowExcelOpenPrompt));
                return;
            }

            MessageBoxResult result = System.Windows.MessageBox.Show(
                "检测已完成，但CSV文件仍被Excel占用，缓存数据未写入。\n\n请关闭Excel后点击“确定”重试。",
                "CSV文件被占用",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.OK)
            {
                bool flushSuccess = FlushCacheToCsv();
                if (flushSuccess)
                {
                    System.Windows.MessageBox.Show("缓存数据已成功写入！", "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show("仍无法写入，请确认已关闭Excel。", "写入失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 从图片名提取电芯码
        /// </summary>
        private string GetCellCodeFromFileName(string fileName)
        {
            try
            {
                // 去掉文件扩展名（如“00PCBK5N01C05YF8K0207776正极.png”→“00PCBK5N01C05YF8K0207776正极”）
                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrEmpty(nameWithoutExt))
                {
                    return "未知电芯码";
                }
                string codePart = nameWithoutExt;
                if (nameWithoutExt.Contains("_"))
                {
                    // 分割取第一个_前面的部分（如“123_456正极”→“123”；“abc_def_ghi”→“abc”）
                    // 使用Split避免空字符串，确保分割后取有效前缀
                    string[] splitParts = nameWithoutExt.Split(new[] { '_' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    codePart = splitParts.Length > 0 ? splitParts[0] : codePart;
                }

                // 检测末尾是否包含“正极”或“负极”，提取前面的部分作为电芯码
                if (codePart.EndsWith("正极"))
                {
                    // 去掉末尾“正极”（如“00PCBK5N01C05YF8K0207776正极”→“00PCBK5N01C05YF8K0207776”）
                    return codePart.Substring(0, codePart.Length - 2);
                }
                else if (codePart.EndsWith("负极"))
                {
                    // 去掉末尾“负极”
                    return codePart.Substring(0, codePart.Length - 2);
                }
                else
                {
                    return codePart;
                }
            }
            catch
            {
                return "未知电芯码";
            }
        }
        /// <summary>
        /// 从图片名提取正负极
        /// </summary>
        private string GetPolarityFromFileName(string fileName)
        {
            try
            {
                // 1. 去掉文件扩展名
                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrEmpty(nameWithoutExt))
                {
                    return "未知极性";
                }

                // 2. 直接判断末尾是否为“正极”或“负极”
                if (nameWithoutExt.EndsWith("正极"))
                {
                    return "正极";
                }
                else if (nameWithoutExt.EndsWith("负极"))
                {
                    return "负极";
                }
                else
                {
                    // 未匹配到已知极性
                    return "未知极性";
                }
            }
            catch
            {
                return "未知极性";
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfig()
        {
            if (!File.Exists(_configFilePath)) return;

            try
            {
                string json = File.ReadAllText(_configFilePath);
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                // 监控路径
                if (dict != null && dict.TryGetValue("SelectedMonitorFolderPath", out var pathA))
                    SelectedMonitorFolderPath = pathA.ToString();


                // 结果图保存
                if (dict != null && dict.TryGetValue("IsSaveResultOK", out var saveresultOkObj))
                {
                    // 使用空值条件运算符 (?.) 安全地调用 ToString()
                    bool.TryParse(saveresultOkObj?.ToString(), out bool isSaveResultOkValue);
                    IsSaveResultOK = isSaveResultOkValue;
                }

                if (dict != null && dict.TryGetValue("IsSaveResultNG", out var saveresultNgObj))
                {
                    bool.TryParse(saveresultNgObj?.ToString(), out bool isSaveResultNgValue);
                    IsSaveResultNG = isSaveResultNgValue;
                }

                // 原图保存
                if (dict != null && dict.TryGetValue("IsSaveOriginalOK", out var saveoriginalOkObj))
                {
                    bool.TryParse(saveoriginalOkObj?.ToString(), out bool isSaveOriginalOkValue);
                    IsSaveOriginalOK = isSaveOriginalOkValue;
                }

                if (dict != null && dict.TryGetValue("IsSaveOriginalNG", out var saveoriginalNgObj))
                {
                    bool.TryParse(saveoriginalNgObj?.ToString(), out bool isSaveOriginalNgValue);
                    IsSaveOriginalNG = isSaveOriginalNgValue;
                }

                if (dict != null && dict.TryGetValue("OKCount", out var okCount))
                {
                    int.TryParse(okCount.ToString(), out _historyOKCount);
                }
                if (dict != null && dict.TryGetValue("NGCount", out var ngCount))
                {
                    int.TryParse(ngCount.ToString(), out _historyNGCount);
                }

                // 风险等级计数
                if (dict != null && dict.TryGetValue("Count_L0", out var l0Count))
                {
                    int.TryParse(l0Count.ToString(), out _historyCountL0);
                }
                if (dict != null && dict.TryGetValue("Count_L1", out var l1Count))
                {
                    int.TryParse(l1Count.ToString(), out _historyCountL1);
                }
                if (dict != null && dict.TryGetValue("Count_L2", out var l2Count))
                {
                    int.TryParse(l2Count.ToString(), out _historyCountL2);
                }
                if (dict != null && dict.TryGetValue("Count_L3", out var l3Count))
                {
                    int.TryParse(l3Count.ToString(), out _historyCountL3);
                }


                //加载完成后，先用历史值更新UI
                lock (_statisticsLock)
                {
                    OKCount = _historyOKCount;
                    NGCount = _historyNGCount;

                    Count_L0 = _historyCountL0;
                    Count_L1 = _historyCountL1;
                    Count_L2 = _historyCountL2;
                    Count_L3 = _historyCountL3;
                }
                UpdateRiskRatios();
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
                var configData = new
                {
                    SelectedMonitorFolderPath,
                    IsSaveResultOK,
                    IsSaveResultNG,
                    IsSaveOriginalOK,
                    IsSaveOriginalNG,
                    TotalDetectionCount,
                    OKCount,
                    NGCount,
                    OKRate,
                    Count_L0,
                    Count_L1,
                    Count_L2,
                    Count_L3,
                };

                // 序列化为JSON
                string json = System.Text.Json.JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                AddLog($"保存配置失败：{ex.Message}");
            }
        }

        #endregion
        #endregion

        // 定时推送定时器
        private System.Timers.Timer? _dailyPushTimer;
        /// <summary>
        /// 启动每日8点推送任务
        /// </summary>
        private void StartDailyPushTask()
        {
            ScheduleNextPush();
        }

        /// <summary>
        /// 计算并设置下一次推送的定时器
        /// </summary>
        private void ScheduleNextPush()
        {
            // 停止旧计时器（如果在运行）
            if (_dailyPushTimer != null)
            {
                _dailyPushTimer.Stop();
                _dailyPushTimer.Dispose();
            }

            // 1. 获取当前时间
            DateTime now = DateTime.Now;

            // 2. 计算目标时间（今天的8点）
            DateTime targetTime = new DateTime(now.Year, now.Month, now.Day, 8, 0, 0);

            // 3. 如果当前时间已经过了今天8点，则目标时间设为明天8点
            if (now >= targetTime)
            {
                targetTime = targetTime.AddDays(1);
            }

            // 4. 计算延迟毫秒数
            double initialDelay = (targetTime - now).TotalMilliseconds;

            // 防止出现负数或极小值的极端情况
            if (initialDelay <= 0) initialDelay = 1000;

            // 5. 初始化定时器 (只执行一次，不要AutoReset)
            _dailyPushTimer = new System.Timers.Timer(initialDelay);
            _dailyPushTimer.AutoReset = false; // 关键：执行一次后自动停止，等待重新调度
            _dailyPushTimer.Elapsed += async (s, e) =>
            {
                try
                {
                    await ExecuteDailyPush();
                }
                finally
                {
                    // 任务完成后，递归调用调度方法，重新计算明天的8点
                    ScheduleNextPush();
                }
            };
            _dailyPushTimer.Start();

            // 可选：记录日志方便排查
            AddLog($"下次推送时间已设定：{targetTime:yyyy-MM-dd HH:mm:ss} (延迟 {initialDelay/1000/3600:F2} 小时)",isToDatabase:true);
        }

        /// <summary>
        /// 执行每日推送
        /// </summary>
        private async Task ExecuteDailyPush()
        {
            try
            {
                // 安全检查：如果被意外触发（比如时间没到8点），强行中止或仅记录日志
                if (DateTime.Now.Hour < 8)
                {
                    AddLog("检测到非8点触发推送，任务已跳过，等待下次调度。",CoreLogLevel.Warn);
                    return;
                }

                // 统计时间范围：昨天8:00 ~ 今天8:00（白班+夜班）
                DateTime yesterday = DateTime.Now.AddDays(-1);
                DateTime dayShiftStart = new DateTime(yesterday.Year, yesterday.Month, yesterday.Day, 8, 0, 0);   // 白班开始
                DateTime dayShiftEnd = new DateTime(yesterday.Year, yesterday.Month, yesterday.Day, 20, 0, 0);  // 白班结束/夜班开始
                DateTime nightShiftEnd = new DateTime(yesterday.AddDays(1).Year, yesterday.AddDays(1).Month, yesterday.AddDays(1).Day, 8, 0, 0); // 夜班结束

                //初始化统计变量
                int l0Day = 0, l1Day = 0, l2Day = 0, l3Day = 0; // 白班L0-L3
                int l0Night = 0, l1Night = 0, l2Night = 0, l3Night = 0; // 夜班L0-L3
                int totalCount = 0, okCount = 0, ngCount = 0;
                //decimal passRate = 0;

                //筛选昨日所有在线检测数据
                var yesterdayData = _fullDetectionDataList.Where(d =>
                    d.DetectionTime >= dayShiftStart &&
                    d.DetectionTime <= nightShiftEnd &&
                    d.DetectionStatus == "在线").ToList();
                totalCount = yesterdayData.Count;

                if (totalCount > 0)
                {
                    // 白班数据统计（8:00-20:00）
                    var dayData = yesterdayData.Where(d => d.DetectionTime <= dayShiftEnd).ToList();
                    l0Day = dayData.Count(d => d.RiskLevel == "L0");
                    l1Day = dayData.Count(d => d.RiskLevel == "L1");
                    l2Day = dayData.Count(d => d.RiskLevel == "L2");
                    l3Day = dayData.Count(d => d.RiskLevel == "L3");

                    //夜班数据统计（20:00-8:00）
                    var nightData = yesterdayData.Where(d => d.DetectionTime > dayShiftEnd).ToList();
                    l0Night = nightData.Count(d => d.RiskLevel == "L0");
                    l1Night = nightData.Count(d => d.RiskLevel == "L1");
                    l2Night = nightData.Count(d => d.RiskLevel == "L2");
                    l3Night = nightData.Count(d => d.RiskLevel == "L3");

                    // OK/NG统计
                    okCount = yesterdayData.Count(d => d.DetectionResult == "OK");
                    ngCount = totalCount - okCount;
                }

                string pushContent = $@"{yesterday:yyyy年MM月dd日} 白晚班X-ray测试检测数据如下：\nL0级冗余：白班{l0Day}PCS，夜班{l0Night}PCS\nL1级冗余：白班{l1Day}PCS，夜班{l1Night}PCS\nL2级冗余：白班{l2Day}PCS，夜班{l2Night}PCS\nL3级冗余：白班{l3Day}PCS，夜班{l3Night}PCS\n检测总数：{totalCount}PCS  OK数：{okCount}PCS  NG数：{ngCount}PCS";
                //await MessagePushHelper.PushMessageAsync(pushContent);
                var response = await _webApiClient.PushMessageAsync(pushContent);

                if (response.Code != 200)
                {
                    _logger.Warn($"消息推送失败：{response.Message}");
                }


            }
            catch (Exception ex)
            {
                AddLog($"[每日推送异常] {ex.Message}");
            }
            finally
            {
                CleanLastPushDayData();
            }
        }
        /// <summary>
        /// 停止每日推送任务
        /// </summary>
        private void StopDailyPushTask()
        {
            _dailyPushTimer?.Stop();
            _dailyPushTimer?.Dispose();
            _dailyPushTimer = null;
        }
        /// <summary>
        /// 推送后清理「前一天（已统计）」的所有数据（先归档再删除）
        /// </summary>
        private void CleanLastPushDayData()
        {
            if (_fullDetectionDataList == null || _fullDetectionDataList.Count == 0)
            {
                AddLog("检测数据列表为空，无需清理");
                return;
            }

            // 定义时间节点：
            DateTime today = DateTime.Now.Date;          // 今日（本次推送日 T）
            DateTime lastPushDay = today.AddDays(-2);    // 上一次推送的统计日（T-2，即用户说的“前一天推送的”）

            // 筛选「上一次推送日」的所有数据（T-2）
            var lastPushDayData = _fullDetectionDataList.Where(d => d.DetectionTime.Date == lastPushDay).ToList();

            if (lastPushDayData.Count == 0)
            {
                AddLog($"上一次推送日（{lastPushDay:yyyy-MM-dd}）无检测数据，无需清理");
                return;
            }

            //从内存中移除「上一次推送日」的数据
            _fullDetectionDataList.RemoveAll(d => d.DetectionTime.Date == lastPushDay);

            AddLog($"清理完成：移除上一次推送日（{lastPushDay:yyyy-MM-dd}）的{lastPushDayData.Count}条数据，当前列表保留【本次推送日的前一天（{today.AddDays(-1):yyyy-MM-dd}）+ 今日】数据");
        }


    };

}