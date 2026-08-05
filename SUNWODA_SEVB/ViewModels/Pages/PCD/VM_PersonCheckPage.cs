using HandyControl.Controls;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Project.PCD;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YOLODetFW48;
using YWPackActionDetect;
using static YWPackActionDetect.AI;



namespace SUNWODA_SEVB.ViewModels.Pages.PCD
{
    /// <summary>
    /// 义乌人员合规检测
    /// </summary>
    [Module("VM_PersonCheckPage", "义乌人员合规检测")]
    public class VM_PersonCheckPage : ViewModelBase, IDisposable
    {
        #region ======== 常量 & 配置 ========

        // ---- RTSP 流地址 ----
        private string RTSP_URL_CAM1 = string.Empty;
        private string RTSP_URL_CAM2 = string.Empty;
        private string RTSP_URL_CAM3 = string.Empty;

        // ---- DLL和模型文件路径 ----
        private string Top_Det_Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "TopCameraDet.onnx");
        private string PackA_Det_Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "CameraA.onnx");
        private string PackB_Det_Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "CameraB.onnx");


        // ---- 检测区域 (顶部相机) ----
        //private static readonly List<OpenCvSharp.Point> DefaultSpecifiedZone = new()
        //{
        //    new OpenCvSharp.Point(680, 300),
        //    new OpenCvSharp.Point(1580, 300),
        //    new OpenCvSharp.Point(1580, 860),
        //    new OpenCvSharp.Point(680, 860)
        //};
        private List<OpenCvSharp.Point> _specifiedZone = new();

        public List<OpenCvSharp.Point> SpecifiedZone
        {
            get => _specifiedZone;
            private set => SetProperty(ref _specifiedZone, value);
        }

        // 绑定到 UI 的四个点坐标
        private double _zonePoint1X = 680, _zonePoint1Y = 300;
        private double _zonePoint2X = 1580, _zonePoint2Y = 300;
        private double _zonePoint3X = 1580, _zonePoint3Y = 860;
        private double _zonePoint4X = 680, _zonePoint4Y = 860;

        public double ZonePoint1X { get => _zonePoint1X; set { if (SetProperty(ref _zonePoint1X, value)) UpdateZonePoints(); } }
        public double ZonePoint1Y { get => _zonePoint1Y; set { if (SetProperty(ref _zonePoint1Y, value)) UpdateZonePoints(); } }
        public double ZonePoint2X { get => _zonePoint2X; set { if (SetProperty(ref _zonePoint2X, value)) UpdateZonePoints(); } }
        public double ZonePoint2Y { get => _zonePoint2Y; set { if (SetProperty(ref _zonePoint2Y, value)) UpdateZonePoints(); } }
        public double ZonePoint3X { get => _zonePoint3X; set { if (SetProperty(ref _zonePoint3X, value)) UpdateZonePoints(); } }
        public double ZonePoint3Y { get => _zonePoint3Y; set { if (SetProperty(ref _zonePoint3Y, value)) UpdateZonePoints(); } }
        public double ZonePoint4X { get => _zonePoint4X; set { if (SetProperty(ref _zonePoint4X, value)) UpdateZonePoints(); } }
        public double ZonePoint4Y { get => _zonePoint4Y; set { if (SetProperty(ref _zonePoint4Y, value)) UpdateZonePoints(); } }

        // ---- 录像保存根目录 ----
        private const string VIDEO_RECORD_ROOT = @"E:\监控原始视频";
        private const string ANOMALY_IMAGE_ROOT = @"D:\监控不合规图片";

        // 相机类型子目录名（与 StartRecording 中 type 参数保持一致）
        private static readonly string[] CAMERA_TYPE_DIRS = { "顶部", "左部", "右部" };

        // ---清理时间---
        private const int RETENTION_DAYS = 30;

        // ffmpeg 最大连续重启次数，超过后需人工介入
        private const int FFMPEG_MAX_RESTART_COUNT = 10;
        private readonly ConcurrentDictionary<int, int> _ffmpegRestartCounts = new();

        // ---- 看门狗：文件健康巡检 ----
        private const int WATCHDOG_INTERVAL_SEC = 60;                // 每60秒巡检一次
        private const int WATCHDOG_FILE_STALE_MINUTES = 5;          // 最新文件超过5分钟未更新 → 判定异常
        private const long WATCHDOG_MIN_FILE_SIZE_BYTES = 1024;     // 文件小于1KB → 判定异常
        private System.Threading.Timer? _watchdogTimer;
        // 每个相机 → 最后成功写入的文件路径 (从ffmpeg stderr解析)
        private readonly ConcurrentDictionary<int, string> _lastOutputFile = new();
        // 每个相机 → 上次检查时文件大小 (用于判断文件是否在增长)
        private readonly ConcurrentDictionary<int, long> _lastCheckedFileSize = new();
        // 每个相机 → 连续异常计数 (连续N次异常才触发重启，避免误报)
        private readonly ConcurrentDictionary<int, int> _watchdogFailCount = new();
        private const int WATCHDOG_MAX_FAIL_COUNT = 3;

        #region ffmpeg
        // ffmpeg 进程字典的统一锁
        private readonly object _ffmpegLock = new();

        // 防止重复启动监控
        private readonly SemaphoreSlim _startStopGate = new(1, 1);

        // 防止同一个 camId 被重复安排重启
        private readonly ConcurrentDictionary<int, byte> _ffmpegRestartScheduled = new();

        // 记录每个 ffmpeg 的启动时间，用于判断是否“快速失败”
        private readonly ConcurrentDictionary<int, DateTime> _ffmpegStartTimes = new();

        // 防止 WatchdogCheck 重入
        private int _watchdogChecking = 0;

        // 多久算一次稳定运行；稳定运行后再退出，可重置连续失败计数
        private static readonly TimeSpan FFMPEG_STABLE_RUN_TIME = TimeSpan.FromMinutes(5);
        #endregion



        // ---- 相机状态追踪 ----
        // 每个相机 → RTSP拉流是否正常
        private readonly ConcurrentDictionary<int, bool> _captureHealthy = new();
        // 每个相机 → 录像是否正常 (由看门狗更新)
        private readonly ConcurrentDictionary<int, bool> _recordingHealthy = new();

        #endregion

        #region ======== 私有字段 ========
        private readonly ConcurrentDictionary<int, Process> _ffmpegProcesses = new();
        private static readonly System.Drawing.Font font = new System.Drawing.Font("微软雅黑", 18, System.Drawing.FontStyle.Bold);

        private IPredictor? topDetEngine, misLayDetEngineA, misLayDetEngineB;
        private readonly Dispatcher? _dispatcher;

        private readonly ILoggerService<VM_PersonCheckPage> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebApiClient _webApiClient;

        private readonly List<LatestFrameChannel> _frameChannels = new();

        // 文件夹维护定时器
        private DispatcherTimer? _folderMaintenanceTimer;

        // 取消令牌
        private CancellationTokenSource? _monitoringCts;

        // 锁
        private static readonly object _drawLock = new();

        // 时钟定时器
        private DispatcherTimer? _clockTimer;

        // 状态
        private bool _isProcessing;
        private string _statusMessage = "就绪";

        private DateTime _currentTime = DateTime.Now;
        private bool _disposed;

        // 内存对象
        private WriteableBitmap? _liveBitmap1, _liveBitmap2, _liveBitmap3;
        private WriteableBitmap? _anomalyBitmap1, _anomalyBitmap2, _anomalyBitmap3;

        private readonly ConcurrentDictionary<string, DateTime> _lastAnomalySaveTime = new();
        private const int ANOMALY_SAVE_COOLDOWN_SEC = 5;  // 同类异常 5 秒内只存一次

        private readonly AI ai = new();
        private readonly AI aiNew = new AI();
        #endregion

        #region 属性
        public ObservableCollection<LogItemModel> AlarmLogs { get; } = new ObservableCollection<LogItemModel>();

        private ImageSource? _liveDisplayImage1;
        public ImageSource? LiveDisplayImage1
        {
            get => _liveDisplayImage1;
            set => SetProperty(ref _liveDisplayImage1, value);
        }

        private ImageSource? _liveDisplayImage2;
        public ImageSource? LiveDisplayImage2
        {
            get => _liveDisplayImage2;
            set => SetProperty(ref _liveDisplayImage2, value);
        }

        private ImageSource? _liveDisplayImage3;
        public ImageSource? LiveDisplayImage3
        {
            get => _liveDisplayImage3;
            set => SetProperty(ref _liveDisplayImage3, value);
        }
        public ImageSource? AnomalyDisplayImage1
        {
            get => _anomalyBitmap1;
            set => SetProperty(ref _anomalyBitmap1, value as WriteableBitmap);
        }
        public ImageSource? AnomalyDisplayImage2
        {
            get => _anomalyBitmap2;
            set => SetProperty(ref _anomalyBitmap2, value as WriteableBitmap);
        }
        public ImageSource? AnomalyDisplayImage3
        {
            get => _anomalyBitmap3;
            set => SetProperty(ref _anomalyBitmap3, value as WriteableBitmap);
        }

        public DateTime CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }


        private string _anomalyInfo1 = string.Empty;
        public string AnomalyInfo1
        {
            get => _anomalyInfo1;
            set => SetProperty(ref _anomalyInfo1, value);
        }

        private string _anomalyInfo2 = string.Empty;
        public string AnomalyInfo2
        {
            get => _anomalyInfo2;
            set => SetProperty(ref _anomalyInfo2, value);
        }

        private string _anomalyInfo3 = string.Empty;
        public string AnomalyInfo3
        {
            get => _anomalyInfo3;
            set => SetProperty(ref _anomalyInfo3, value);
        }

        // ---- 相机状态 ----
        private string _cam1Status = "未启动";
        public string Cam1Status { get => _cam1Status; set => SetProperty(ref _cam1Status, value); }
        private Brush _cam1StatusColor = Brushes.Gray;
        public Brush Cam1StatusColor { get => _cam1StatusColor; set => SetProperty(ref _cam1StatusColor, value); }

        private string _cam2Status = "未启动";
        public string Cam2Status { get => _cam2Status; set => SetProperty(ref _cam2Status, value); }
        private Brush _cam2StatusColor = Brushes.Gray;
        public Brush Cam2StatusColor { get => _cam2StatusColor; set => SetProperty(ref _cam2StatusColor, value); }

        private string _cam3Status = "未启动";
        public string Cam3Status { get => _cam3Status; set => SetProperty(ref _cam3Status, value); }
        private Brush _cam3StatusColor = Brushes.Gray;
        public Brush Cam3StatusColor { get => _cam3StatusColor; set => SetProperty(ref _cam3StatusColor, value); }

        private int DetectionIntervalMs { get; set; } = 100; // 默认 10 FPS -> 100ms

        private int _framesPerSecond = 10;
        public int FramesPerSecond
        {
            get => _framesPerSecond;
            set
            {
                if (SetProperty(ref _framesPerSecond, value))
                {
                    if (_framesPerSecond <= 0) _framesPerSecond = 1;
                    DetectionIntervalMs = 1000 / _framesPerSecond;
                    UpdateParams("FramesPerSecond", _framesPerSecond);
                }
            }
        }
        // ---- 状态 ----
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        public RelayCommand StartMonitoringCommand { get; }
        public RelayCommand StopMonitoringCommand { get; }
        public RelayCommand SaveZoneCommand { get; }
        #endregion

        public VM_PersonCheckPage(ILoggerService<VM_PersonCheckPage> logger, IWebApiClient webApiClient, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _webApiClient = webApiClient ?? throw new ArgumentNullException(nameof(webApiClient));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            _dispatcher = Application.Current.Dispatcher;

            StartMonitoringCommand = new RelayCommand(async _ => await StartMonitoringAsync(), _ => !IsProcessing);
            StopMonitoringCommand = new RelayCommand(_ => StopMonitoring(), _ => IsProcessing);
            SaveZoneCommand = new RelayCommand(async _ => await SaveZoneToDatabaseAsync());

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now;
            _clockTimer.Start();

            LoadDataBaseParams();

            // 自动启动监控
            _ = AutoStartAsync();
        }

        /// <summary>
        /// 构造完成后自动启动监控（延迟等待数据库参数加载完成）
        /// </summary>
        private async Task AutoStartAsync()
        {
            try
            {
                // 等待参数从数据库加载完成（LoadDataBaseParams 是 async void）
                await Task.Delay(2000);

                if (_disposed) return;

                AddLog("系统自动启动监控...");
                await StartMonitoringAsync();
            }
            catch (Exception ex)
            {
                AddLog($"自动启动失败: {ex.Message}", CoreLogLevel.Error);
            }
        }

        #region 方法

        private async Task StartMonitoringAsync()
        {
            await _startStopGate.WaitAsync();

            try
            {
                if (_disposed)
                    return;

                if (IsProcessing && _monitoringCts != null && !_monitoringCts.IsCancellationRequested)
                {
                    AddLog("监控已经在运行，忽略重复启动请求", CoreLogLevel.Warn);
                    return;
                }

                IsProcessing = true;
                StatusMessage = "正在初始化AI引擎...";

                if (!await Task.Run(InitializeAIEngines))
                {
                    StatusMessage = "AI引擎初始化失败";
                    IsProcessing = false;
                    return;
                }

                _monitoringCts = new CancellationTokenSource();
                var token = _monitoringCts.Token;

                StatusMessage = "正在连接摄像机...";

                StartDiskMaintenance();
                StartLiveStreams(token);
                StartCameraPipelines(token);

                for (int i = 1; i <= 3; i++)
                {
                    _captureHealthy[i] = false;
                    _recordingHealthy[i] = false;
                    RefreshCameraStatus(i);
                }

                StatusMessage = "监控运行中";
                AddLog("系统启动，3路摄像机监控已开启");
            }
            catch (Exception ex)
            {
                StatusMessage = $"启动失败: {ex.Message}";
                IsProcessing = false;
                AddLog($"启动异常: {ex.Message}", CoreLogLevel.Error);
            }
            finally
            {
                _startStopGate.Release();
            }
        }
        /// <summary>
        /// 停止监控：停止检测 → 停止RTSP流 → 释放引擎
        /// </summary>
        private void StopMonitoring()
        {
            try
            {
                StatusMessage = "正在停止监控...";
                try { _monitoringCts?.Cancel(); } catch { }
                StopLiveStreams();

                foreach (var ch in _frameChannels) ch.Dispose();
                _frameChannels.Clear();

                _folderMaintenanceTimer?.Stop();
                _folderMaintenanceTimer = null;

                // 清理看门狗状态
                _lastOutputFile.Clear();
                _lastCheckedFileSize.Clear();
                _watchdogFailCount.Clear();
                _captureHealthy.Clear();
                _recordingHealthy.Clear();

                IsProcessing = false;
                StatusMessage = "已停止";
                AddLog("系统已停止监控");
                RefreshAllCameraStatus();
            }
            catch (Exception ex)
            {
                StatusMessage = $"停止异常: {ex.Message}";
                AddLog($"停止异常: {ex.Message}", CoreLogLevel.Error);
            }
        }

        #region 配置
        private async void LoadDataBaseParams()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                    var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                    var vmName = await globalSettingRepo.GetSettingValueAsync<string>("CurrentVMName", "Default");
                    int dbFps = await projectSettingRepo.GetSettingValueAsync("framesPerSecond", vmName) ?? 10;

                    _framesPerSecond = dbFps > 0 ? dbFps : 10;
                    DetectionIntervalMs = 1000 / _framesPerSecond;
                    OnPropertyChanged(nameof(FramesPerSecond));

                    var rtspUrlCam1 = await projectSettingRepo.GetSettingValueAsync("RtspUrlCam1", vmName);
                    if (rtspUrlCam1 == null)
                    {
                        string message = "未找到顶部Camera的RTSP地址配置，使用默认值";
                        Growl.Warning(message);
                        AddLog(message, CoreLogLevel.Warn);
                        RTSP_URL_CAM1 = "rtsp://admin:xwd123456@192.168.1.101:554/cam/realmonitor?channel=1&subtype=0";
                    }
                    else
                    {
                        RTSP_URL_CAM1 = rtspUrlCam1;
                    }
                    var rtspUrlCam2 = await projectSettingRepo.GetSettingValueAsync("RtspUrlCam2", vmName);
                    if (rtspUrlCam2 == null)
                    {
                        string message = "未找到左部Camera的RTSP地址配置，使用默认值";
                        Growl.Warning(message);
                        AddLog(message, CoreLogLevel.Warn);
                        RTSP_URL_CAM2 = "rtsp://admin:xwd123456@192.168.1.102:554/cam/realmonitor?channel=1&subtype=0";
                    }
                    else
                    {
                        RTSP_URL_CAM2 = rtspUrlCam2;
                    }
                    var rtspUrlCam3 = await projectSettingRepo.GetSettingValueAsync("RtspUrlCam3", vmName);
                    if (rtspUrlCam3 == null)
                    {
                        string message = "未找到右部Camera的RTSP地址配置，使用默认值";
                        Growl.Warning(message);
                        AddLog(message, CoreLogLevel.Warn);
                        RTSP_URL_CAM3 = "rtsp://admin:xwd123456@192.168.1.103:554/cam/realmonitor?channel=1&subtype=0";
                    }
                    else
                    {
                        RTSP_URL_CAM3 = rtspUrlCam3;
                    }

                    LoadSpecifiedZone(projectSettingRepo, vmName);
                }

            }
            catch (Exception ex)
            {
                AddLog($"加载数据库参数异常: {ex.Message}", CoreLogLevel.Error);
            }
        }

        private void UpdateParams(string Name, dynamic Value)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                projectSettingRepo.UpdateSettingValue(Name, "VM_PersonCheckPage", Value);
            }
            catch (Exception ex)
            {
                AddLog($"更新数据库参数{Name}异常: {ex.Message}", CoreLogLevel.Error);
            }
        }



        #endregion

        #region 检测区域配置

        private void LoadSpecifiedZone(IProjectSettingRepository repo, string vmName)
        {
            try
            {
                _zonePoint1X = GetDoubleSetting(repo, "ZonePoint1X", vmName, 680);
                _zonePoint1Y = GetDoubleSetting(repo, "ZonePoint1Y", vmName, 300);
                _zonePoint2X = GetDoubleSetting(repo, "ZonePoint2X", vmName, 1580);
                _zonePoint2Y = GetDoubleSetting(repo, "ZonePoint2Y", vmName, 300);
                _zonePoint3X = GetDoubleSetting(repo, "ZonePoint3X", vmName, 1580);
                _zonePoint3Y = GetDoubleSetting(repo, "ZonePoint3Y", vmName, 860);
                _zonePoint4X = GetDoubleSetting(repo, "ZonePoint4X", vmName, 680);
                _zonePoint4Y = GetDoubleSetting(repo, "ZonePoint4Y", vmName, 860);

                UpdateZonePoints();
                OnPropertyChanged(nameof(ZonePoint1X));
                OnPropertyChanged(nameof(ZonePoint1Y));
                OnPropertyChanged(nameof(ZonePoint2X));
                OnPropertyChanged(nameof(ZonePoint2Y));
                OnPropertyChanged(nameof(ZonePoint3X));
                OnPropertyChanged(nameof(ZonePoint3Y));
                OnPropertyChanged(nameof(ZonePoint4X));
                OnPropertyChanged(nameof(ZonePoint4Y));
            }
            catch (Exception ex)
            {
                AddLog($"加载检测区域配置失败: {ex.Message}", CoreLogLevel.Warn);
                // 使用默认值
                ResetToDefaultZone();
            }
        }

        private double GetDoubleSetting(IProjectSettingRepository repo, string name, string vmName, double defaultVal)
        {
            var model = repo.GetByNameAndVM(name, vmName);
            if (model == null || string.IsNullOrEmpty(model.Value))
                return defaultVal;

            return double.TryParse(model.Value, out var val) ? val : defaultVal;
        }

        private void UpdateZonePoints()
        {
            SpecifiedZone = new List<OpenCvSharp.Point>
            {
                new OpenCvSharp.Point((int)_zonePoint1X, (int)_zonePoint1Y),
                new OpenCvSharp.Point((int)_zonePoint2X, (int)_zonePoint2Y),
                new OpenCvSharp.Point((int)_zonePoint3X, (int)_zonePoint3Y),
                new OpenCvSharp.Point((int)_zonePoint4X, (int)_zonePoint4Y)
            };
        }

        private void ResetToDefaultZone()
        {
            ZonePoint1X = 680; ZonePoint1Y = 300;
            ZonePoint2X = 1580; ZonePoint2Y = 300;
            ZonePoint3X = 1580; ZonePoint3Y = 860;
            ZonePoint4X = 680; ZonePoint4Y = 860;
        }

        private async Task SaveZoneToDatabaseAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();
                var vmName = "VM_PersonCheckPage";

                await repo.UpdateSettingValueAsync("ZonePoint1X", vmName, (int)_zonePoint1X);
                await repo.UpdateSettingValueAsync("ZonePoint1Y", vmName, (int)_zonePoint1Y);
                await repo.UpdateSettingValueAsync("ZonePoint2X", vmName, (int)_zonePoint2X);
                await repo.UpdateSettingValueAsync("ZonePoint2Y", vmName, (int)_zonePoint2Y);
                await repo.UpdateSettingValueAsync("ZonePoint3X", vmName, (int)_zonePoint3X);
                await repo.UpdateSettingValueAsync("ZonePoint3Y", vmName, (int)_zonePoint3Y);
                await repo.UpdateSettingValueAsync("ZonePoint4X", vmName, (int)_zonePoint4X);
                await repo.UpdateSettingValueAsync("ZonePoint4Y", vmName, (int)_zonePoint4Y);

                AddLog("检测区域配置已保存到数据库", CoreLogLevel.Info);
                Growl.Success("检测区域保存成功");
            }
            catch (Exception ex)
            {
                AddLog($"保存检测区域失败: {ex.Message}", CoreLogLevel.Error);
                Growl.Error("保存失败");
            }
        }

        #endregion


        #region ====拉流/检测/存图等核心功能====
        private void StartCameraPipelines(CancellationToken token)
        {
            foreach (var cfg in BuildCameraConfigs())
            {
                var frameCh = new LatestFrameChannel();
                _frameChannels.Add(frameCh);
                //拉流
                _ = Task.Run(() => CaptureLoopAsync(cfg, frameCh, token), token);
                //检测
                _ = Task.Run(() => DetectionLoopAsync(cfg, frameCh, token), token);
            }
        }

        private List<CameraConfig> BuildCameraConfigs() => new()
        {
            new CameraConfig
            {
                CamId = 1, Label = "Top", DisplayName = "顶部", RtspUrl = RTSP_URL_CAM1,
                LiveChannel = FrameChannel.Live1, AnomalyChannel = FrameChannel.Anomaly1,
                AnomalyType = "人员行为检测",
                SetAnomalyInfo = s => AnomalyInfo1 = s,
                Detect = mat =>
                {
                    ai.TopCameraDetect(mat, topDetEngine!, SpecifiedZone,
                                       out Mat result, out var results, out string err);
                    return new DetectResult { ResultMat = result, Results = results, Error = err };
                }
            },
            new CameraConfig
            {
                CamId = 2, Label = "Left", DisplayName = "左部", RtspUrl = RTSP_URL_CAM2,
                LiveChannel = FrameChannel.Live2, AnomalyChannel = FrameChannel.Anomaly2,
                AnomalyType = "电池包放反&铁架标签漏贴",
                SetAnomalyInfo = s => AnomalyInfo2 = s,
                Detect = mat =>
                {
                    ai.PackMislayDetect(mat, misLayDetEngineA!,
                                        out Mat result, out var results, out string err);
                    return new DetectResult { ResultMat = result, Results = results, Error = err };
                }
            },
            new CameraConfig
            {
                CamId = 3, Label = "Right", DisplayName = "右部", RtspUrl = RTSP_URL_CAM3,
                LiveChannel = FrameChannel.Live3, AnomalyChannel = FrameChannel.Anomaly3,
                AnomalyType = "电池包放反&铁架标签漏贴",
                SetAnomalyInfo = s => AnomalyInfo3 = s,
                Detect = mat =>
                {
                    aiNew.PackMislayDetect(mat, misLayDetEngineB!,
                                        out Mat result, out var results, out string err);
                    return new DetectResult { ResultMat = result, Results = results, Error = err };
                }
            },
        };

        private async Task CaptureLoopAsync(CameraConfig cfg, LatestFrameChannel detectCh, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                VideoCapture? cap = null;
                try
                {
                    cap = new VideoCapture(cfg.RtspUrl);
                    if (!cap.IsOpened())
                    {
                        _captureHealthy[cfg.CamId] = false;
                        RefreshCameraStatus(cfg.CamId);
                        AddLog($"{cfg.DisplayName}相机RTSP流打开失败，3秒后重试", CoreLogLevel.Warn);
                        await Task.Delay(3000, token);
                        continue;
                    }
                    _captureHealthy[cfg.CamId] = true;
                    RefreshCameraStatus(cfg.CamId);
                    AddLog($"{cfg.DisplayName}相机RTSP流已连接");

                    while (!token.IsCancellationRequested)
                    {
                        using var frame = new Mat();
                        if (!cap.Read(frame) || frame.Empty())
                        {
                            _captureHealthy[cfg.CamId] = false;
                            RefreshCameraStatus(cfg.CamId);
                            AddLog($"{cfg.DisplayName}相机读帧失败，准备重连", CoreLogLevel.Warn);
                            break;
                        }
                        detectCh.Publish(frame);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    AddLog($"{cfg.DisplayName}相机拉流异常: {ex.Message}", CoreLogLevel.Error);
                    try { await Task.Delay(2000, token); } catch { break; }
                }
                finally { cap?.Dispose(); }
            }
        }



        private async Task DetectionLoopAsync(CameraConfig cfg, LatestFrameChannel ch, CancellationToken token)
        {
            var lastDetect = DateTime.MinValue;
            while (!token.IsCancellationRequested)
            {
                var frame = await ch.WaitAsync(token);
                if (frame == null) continue;

                Mat? resultMat = null;
                try
                {
                    if ((DateTime.Now - lastDetect).TotalMilliseconds < DetectionIntervalMs) continue;
                    lastDetect = DateTime.Now;

                    // ① 全帧检测
                    var detect = cfg.Detect(frame);
                    resultMat = detect.ResultMat;

                    if (!string.IsNullOrEmpty(detect.Error))
                        AddLog($"{cfg.DisplayName}相机检测错误: {detect.Error}", CoreLogLevel.Warn);

                    // ② 决定使用哪张图作为显示帧
                    Mat displayFrame = resultMat ?? frame;

                    // ③ 判定 OK / NG
                    var fails = detect.Results?.Where(r => !r.IsOk).ToList();
                    bool isAnomaly = fails != null && fails.Count > 0;

                    // ④ 实时窗口（1/2/3）始终更新，无论 OK 还是 NG
                    OnFrameReceived(cfg.LiveChannel, displayFrame, string.Empty);

                    // ⑤ 仅在 NG 时推送到异常窗口（4/5/6）+ 存图 + 文字提示
                    if (isAnomaly)
                    {
                        string anomalyText = string.Join(" | ", fails!.Select(r => $"{r.Name}:{r.Status}"));
                        cfg.SetAnomalyInfo(anomalyText);
                        OnFrameReceived(cfg.AnomalyChannel, displayFrame, anomalyText);

                        // 存图防抖：按"相机ID + 异常类型 + 异常描述"作为 key
                        string saveKey = $"{cfg.CamId}|{cfg.AnomalyType}|{anomalyText}";
                        var now = DateTime.Now;
                        if (!_lastAnomalySaveTime.TryGetValue(saveKey, out var last)
                            || (now - last).TotalSeconds >= ANOMALY_SAVE_COOLDOWN_SEC)
                        {
                            _lastAnomalySaveTime[saveKey] = now;
                            SaveAnomalyImage(cfg.CamId, cfg.AnomalyType, displayFrame, anomalyText);
                        }
                    }
                    //else
                    //{
                    //    // OK 时清空文字提示
                    //    cfg.SetAnomalyInfo(string.Empty);
                    //}

                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { AddLog($"{cfg.DisplayName}检测异常: {ex.Message}", CoreLogLevel.Error); }
                finally
                {
                    resultMat?.Dispose();
                    frame.Dispose();
                }
            }
        }


        #endregion


        #region ======== 磁盘管理（30 天保留）========
        private void StartDiskMaintenance()
        {
            EnsureDirectoriesExist();
            _ = Task.Run(CleanupOldData);

            _folderMaintenanceTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
            _folderMaintenanceTimer.Tick += (s, e) =>
            {
                EnsureDirectoriesExist();
                if (DateTime.Now.Hour == 3) _ = Task.Run(CleanupOldData);
            };
            _folderMaintenanceTimer.Start();
        }

        private void EnsureDirectoriesExist()
        {
            try
            {
                foreach (var root in new[] { VIDEO_RECORD_ROOT, ANOMALY_IMAGE_ROOT })
                    foreach (var date in new[] { DateTime.Today, DateTime.Today.AddDays(1) })
                    {
                        string dateDir = Path.Combine(root, date.ToString("yyyy-MM-dd"));
                        if (!Directory.Exists(dateDir)) Directory.CreateDirectory(dateDir);

                        // 预创建相机类型子目录，防止跨天时 ffmpeg 因目录不存在而崩溃
                        if (root == VIDEO_RECORD_ROOT)
                        {
                            foreach (var camType in CAMERA_TYPE_DIRS)
                            {
                                string subDir = Path.Combine(dateDir, camType);
                                if (!Directory.Exists(subDir)) Directory.CreateDirectory(subDir);
                            }
                        }
                    }
            }
            catch (Exception ex) { AddLog($"维护文件夹异常: {ex.Message}", CoreLogLevel.Warn); }
        }

        private void CleanupOldData()
        {
            try
            {
                var cutoff = DateTime.Today.AddDays(-RETENTION_DAYS);
                int dirs = 0, files = 0;

                dirs += CleanupByDateFolder(VIDEO_RECORD_ROOT, cutoff);
                dirs += CleanupByDateFolder(ANOMALY_IMAGE_ROOT, cutoff);
                files += CleanupOrphanFiles(VIDEO_RECORD_ROOT, cutoff, "*.mp4");
                files += CleanupOrphanFiles(ANOMALY_IMAGE_ROOT, cutoff, "*.jpg");

                if (dirs > 0 || files > 0)
                    AddLog($"磁盘清理：删除过期目录 {dirs} 个，孤立文件 {files} 个");
            }
            catch (Exception ex) { AddLog($"磁盘清理异常: {ex.Message}", CoreLogLevel.Error); }
        }

        private int CleanupByDateFolder(string root, DateTime cutoff)
        {
            if (!Directory.Exists(root)) return 0;
            int count = 0;
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                string name = Path.GetFileName(dir);
                if (!DateTime.TryParseExact(name, "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var d)) continue;

                if (d < cutoff)
                {
                    try { Directory.Delete(dir, true); count++; }
                    catch (Exception ex) { _logger?.Warn($"删除目录失败 {dir}: {ex.Message}"); }
                }
            }
            return count;
        }

        private int CleanupOrphanFiles(string root, DateTime cutoff, string pattern)
        {
            if (!Directory.Exists(root)) return 0;
            int count = 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        if (File.GetLastWriteTime(f) < cutoff) { File.Delete(f); count++; }
                    }
                    catch (Exception ex) { _logger?.Warn($"删除文件失败 {f}: {ex.Message}"); }
                }
            }
            catch (Exception ex) { _logger?.Warn($"扫描 {root} 失败: {ex.Message}"); }
            return count;
        }
        #endregion



        /// <summary>
        /// 接收并更新指定通道的图像帧
        /// </summary>
        /// <param name="channelId">通道编号 (1, 2, 3...)</param>
        /// <param name="mat">OpenCV 图像矩阵</param>
        public void OnFrameReceived(FrameChannel channel, Mat mat, string label = "")
        {
            // 根据传入的通道 ID，路由到对应的状态和属性
            switch (channel)
            {
                case FrameChannel.Live1:
                    _liveBitmap1 = UpdateVideoFrame(mat, _liveBitmap1, img => LiveDisplayImage1 = img);
                    break;
                case FrameChannel.Live2:
                    _liveBitmap2 = UpdateVideoFrame(mat, _liveBitmap2, img => LiveDisplayImage2 = img);
                    break;
                case FrameChannel.Live3:
                    _liveBitmap3 = UpdateVideoFrame(mat, _liveBitmap3, img => LiveDisplayImage3 = img);
                    break;
                case FrameChannel.Anomaly1:
                    _anomalyBitmap1 = UpdateVideoFrame(mat, _anomalyBitmap1,
                        img => AnomalyDisplayImage1 = img,  // 仅初始化时绑定
                        () => AnomalyInfo1 = label);        // 每一帧都强制同步覆盖文字
                    break;
                case FrameChannel.Anomaly2:
                    _anomalyBitmap2 = UpdateVideoFrame(mat, _anomalyBitmap2,
                        img => AnomalyDisplayImage2 = img,
                        () => AnomalyInfo2 = label);
                    break;
                case FrameChannel.Anomaly3:
                    _anomalyBitmap3 = UpdateVideoFrame(mat, _anomalyBitmap3,
                        img => AnomalyDisplayImage3 = img,
                        () => AnomalyInfo3 = label);
                    break;
                default:
                    AddLog($"收到未知通道的帧数据:{channel}", CoreLogLevel.Warn);
                    break;
            }
        }

        private WriteableBitmap? UpdateVideoFrame(Mat mat, WriteableBitmap? currentWb, Action<ImageSource> onNewBitmapCreated, Action? onFrameSync = null)
        {
            if (mat == null || mat.Empty()) return currentWb;

            int width = mat.Width;
            int height = mat.Height;
            int channels = mat.Channels();
            int stride = (int)mat.Step();
            int totalBytes = stride * height;
            PixelFormat format = channels == 1 ? PixelFormats.Gray8 : PixelFormats.Bgr24;

            WriteableBitmap? resultWb = currentWb;

            var dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return currentWb;

            try
            {
                dispatcher.Invoke(() =>
                {
                    if (resultWb == null
                         || resultWb.PixelWidth != width
                         || resultWb.PixelHeight != height
                         || resultWb.Format != format)
                    {
                        resultWb = new WriteableBitmap(width, height, 96, 96, format, null);
                        onNewBitmapCreated(resultWb);
                    }
                    resultWb.WritePixels(
                        new Int32Rect(0, 0, width, height),
                        mat.Data,
                        totalBytes,
                        stride
                    );
                    onFrameSync?.Invoke();
                    //resultWb.WritePixels(new Int32Rect(0, 0, width, height), mat.Data, totalBytes, stride);
                });
            }
            catch (TaskCanceledException) { /* 应用关闭中，忽略 */ }
            catch (OperationCanceledException) { /* 同上 */ }

            return resultWb;
        }

        #region ======== FFmpeg 录像（带日志过滤）========

        private void StartLiveStreams(CancellationToken token)
        {
            // 分别启动3个相机的后台独立录制
            StartRecording(1, RTSP_URL_CAM1, "顶部", token);
            StartRecording(2, RTSP_URL_CAM2, "左部", token);
            StartRecording(3, RTSP_URL_CAM3, "右部", token);

            // 启动文件健康巡检看门狗
            StartWatchdog();
        }
        public void StartRecording(int camId, string rtspUrl, string type, CancellationToken token)
        {
            if (token.IsCancellationRequested || _disposed)
                return;

            if (string.IsNullOrWhiteSpace(rtspUrl))
            {
                _logger?.Error($"相机 {camId} RTSP 地址为空");
                return;
            }

            if (!CheckDiskSpace(VIDEO_RECORD_ROOT, 10))
            {
                AddLog($"相机 {camId} 磁盘空间不足，无法启动录制", CoreLogLevel.Error);
                Growl.Error($"相机 {camId} 磁盘空间不足，无法启动录制");
                return;
            }

            string safeType = ConvertToSafeTypeName(string.IsNullOrWhiteSpace(type) ? "Default" : type);
            CreateRequiredDirectories(VIDEO_RECORD_ROOT, safeType);

            string outPattern = Path.Combine(
                VIDEO_RECORD_ROOT,
                "%Y-%m-%d",
                safeType,
                $"Cam{camId}_%H-%M-%S.ts");

            lock (_ffmpegLock)
            {
                // 关键：同一个 camId 已经有活着的 ffmpeg，则不再启动新的
                if (_ffmpegProcesses.TryGetValue(camId, out var existing))
                {
                    try
                    {
                        if (!existing.HasExited)
                        {
                            AddLog($"相机 {camId} ({type}) ffmpeg 已在运行，忽略重复启动", CoreLogLevel.Warn);
                            return;
                        }
                    }
                    catch
                    {
                        // 进程状态不可读，按异常进程清理
                    }

                    _ffmpegProcesses.TryRemove(camId, out _);
                    try { existing.Dispose(); } catch { }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments =
                        $"-loglevel warning -y " +
                        $"-rtsp_transport tcp " +
                        $"-i \"{rtspUrl}\" " +
                        $"-an -c:v copy " +
                        $"-f segment -segment_time 3600 -segment_atclocktime 1 -reset_timestamps 1 -strftime 1 " +
                        $"\"{outPattern}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                var process = new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(e.Data))
                        return;

                    var line = e.Data;

                    TryParseSegmentOutput(line, camId);

                    if (line.StartsWith("frame=") ||
                        line.StartsWith("size=") ||
                        line.Contains("bitrate=") ||
                        line.StartsWith("Press [q]"))
                        return;

                    switch (ClassifyFfmpegLog(line))
                    {
                        case CoreLogLevel.Error:
                            _logger?.Error($"[录制-Cam{camId}] {line}");
                            break;

                        case CoreLogLevel.Warn:
                            _logger?.Warn($"[录制-Cam{camId}] {line}");
                            break;
                    }
                };

                process.Exited += (s, e) =>
                {
                    OnFfmpegExited(camId, rtspUrl, type, token, (Process)s!);
                };

                try
                {
                    process.Start();
                    process.BeginErrorReadLine();

                    // 这里不要用 TryAdd 静默失败；在锁内直接登记当前进程
                    _ffmpegProcesses[camId] = process;
                    _ffmpegStartTimes[camId] = DateTime.Now;

                    // 不要在这里清空 _ffmpegRestartCounts
                    // 否则连续重启次数永远累计不起来

                    _recordingHealthy[camId] = true;
                    RefreshCameraStatus(camId);

                    AddLog($"相机 {camId} ({type}) 后台整点切片录制已启动");
                }
                catch (Exception ex)
                {
                    AddLog($"相机 {camId} 录制进程启动失败: {ex.Message}", CoreLogLevel.Error);

                    try { process.Dispose(); } catch { }
                }
            }
        }

        private void OnFfmpegExited(
            int camId,
            string rtspUrl,
            string type,
            CancellationToken token,
            Process exitedProcess)
        {
            int exitCode = GetExitCode(exitedProcess);
            bool isCurrentProcess = false;

            lock (_ffmpegLock)
            {
                // 关键：只允许当前登记的那个 Process 移除自己
                // 防止旧进程 Exited 误删新进程
                if (_ffmpegProcesses.TryGetValue(camId, out var current)
                    && ReferenceEquals(current, exitedProcess))
                {
                    _ffmpegProcesses.TryRemove(camId, out _);
                    isCurrentProcess = true;
                }
            }

            try { exitedProcess.Dispose(); } catch { }

            if (!isCurrentProcess)
            {
                AddLog($"相机 {camId} ({type}) 旧 ffmpeg 进程退出，忽略其重启逻辑", CoreLogLevel.Warn);
                return;
            }

            _recordingHealthy[camId] = false;
            RefreshCameraStatus(camId);
            // 进程退出时清除跟踪状态
            _lastOutputFile.TryRemove(camId, out _);
            _lastCheckedFileSize.TryRemove(camId, out _);
            _watchdogFailCount.TryRemove(camId, out _);

            if (_disposed || token.IsCancellationRequested)
                return;

            // 如果之前稳定运行超过 5 分钟，再退出时认为不是连续快速失败
            if (_ffmpegStartTimes.TryGetValue(camId, out var startTime))
            {
                var runTime = DateTime.Now - startTime;
                if (runTime >= FFMPEG_STABLE_RUN_TIME)
                {
                    _ffmpegRestartCounts[camId] = 0;
                }
            }

            int restartCount = _ffmpegRestartCounts.AddOrUpdate(camId, 1, (_, c) => c + 1);

            if (restartCount > FFMPEG_MAX_RESTART_COUNT)
            {
                AddLog($"相机 {camId} ({type}) 录制进程连续重启超过 {FFMPEG_MAX_RESTART_COUNT} 次，停止自动重连，请人工检查",CoreLogLevel.Error);
                PushMessageToFeishu($"相机 {camId} ({type}) 录制进程连续重启超过 {FFMPEG_MAX_RESTART_COUNT} 次，停止自动重连，请人工检查");
                
                return;
            }

            AddLog(
                $"相机 {camId} ({type}) 录制进程退出 ExitCode={exitCode}，第 {restartCount} 次重试，3秒后重启",
                CoreLogLevel.Warn);

            ScheduleFfmpegRestart(camId, rtspUrl, type, token, TimeSpan.FromSeconds(3));
        }

        private void ScheduleFfmpegRestart(
            int camId,
            string rtspUrl,
            string type,
            CancellationToken token,
            TimeSpan delay)
        {
            // 同一个 camId 只允许存在一个待执行的重启任务
            if (!_ffmpegRestartScheduled.TryAdd(camId, 0))
            {
                AddLog($"相机 {camId} ({type}) 已有待执行的 ffmpeg 重启任务，忽略重复调度", CoreLogLevel.Warn);
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, token);

                    if (_disposed || token.IsCancellationRequested)
                        return;

                    StartRecording(camId, rtspUrl, type, token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    AddLog($"相机 {camId} ({type}) 重启失败: {ex.Message}", CoreLogLevel.Error);
                }
                finally
                {
                    _ffmpegRestartScheduled.TryRemove(camId, out _);
                }
            });
        }

        /// <summary>
        /// 从 ffmpeg stderr 中解析 segment muxer 当前正在写入的文件路径
        /// </summary>
        private void TryParseSegmentOutput(string logLine, int camId)
        {
            try
            {
                // 匹配: [segment @ xxx] Opening 'E:\path\to\file.mkv' for writing
                const string marker = "Opening '";
                int openIdx = logLine.IndexOf(marker, StringComparison.Ordinal);
                if (openIdx < 0) return;

                int startIdx = openIdx + marker.Length;
                int endIdx = logLine.IndexOf("'", startIdx);
                if (endIdx < 0) return;

                string filePath = logLine.Substring(startIdx, endIdx - startIdx);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    _lastOutputFile[camId] = filePath;
                    // 新文件开始，重置大小追踪
                    _lastCheckedFileSize.TryRemove(camId, out _);
                }
            }
            catch
            {
                // 解析失败静默忽略，不影响主流程
            }
        }

        private static CoreLogLevel ClassifyFfmpegLog(string line)
        {
            if (line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("could not", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("no such", StringComparison.OrdinalIgnoreCase) >= 0)
                return CoreLogLevel.Error;

            if (line.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("deprecated", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("non-monotonous", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0)
                return CoreLogLevel.Warn;

            return CoreLogLevel.Info; // 丢弃
        }

        private void StopLiveStreams()
        {
            StopWatchdog();

            foreach (var kvp in _ffmpegProcesses.ToArray())
            {
                int camId = kvp.Key;
                var process = kvp.Value;

                if (process == null)
                    continue;

                try
                {
                    if (!process.HasExited)
                    {
                        try
                        {
                            process.StandardInput.WriteLine("q");
                            process.StandardInput.Close();
                        }
                        catch (IOException) { }
                        catch (InvalidOperationException) { }

                        if (!process.WaitForExit(5000))
                        {
                            try { process.Kill(); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"关闭相机 {camId} 进程异常: {ex.Message}", CoreLogLevel.Warn);
                }
                finally
                {
                    try { process.Dispose(); } catch { }
                }
            }

            _ffmpegProcesses.Clear();
            _ffmpegRestartScheduled.Clear();
            _ffmpegStartTimes.Clear();
            _ffmpegRestartCounts.Clear();

            AddLog("所有后台视频录制已停止");
        }

        #region ======== 看门狗：文件健康巡检 ========

        /// <summary>
        /// 启动录播文件健康巡检看门狗
        /// </summary>
        private void StartWatchdog()
        {
            StopWatchdog();
            _watchdogFailCount.Clear();
            _lastCheckedFileSize.Clear();
            _lastOutputFile.Clear();

            _watchdogTimer = new System.Threading.Timer(
                _ => WatchdogCheck(),
                null,
                TimeSpan.FromSeconds(WATCHDOG_INTERVAL_SEC),
                TimeSpan.FromSeconds(WATCHDOG_INTERVAL_SEC));

            AddLog("录播文件健康巡检看门狗已启动");
        }

        /// <summary>
        /// 停止看门狗
        /// </summary>
        private void StopWatchdog()
        {
            try
            {
                _watchdogTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _watchdogTimer?.Dispose();
                _watchdogTimer = null;
            }
            catch (ObjectDisposedException) { _watchdogTimer = null; }
            catch { }
        }

        /// <summary>
        /// 看门狗巡检：逐相机检查进程状态 + 输出文件健康度
        /// </summary>
        private void WatchdogCheck()
        {
            if (_disposed)
                return;

            // 防止 Timer 回调重入
            if (Interlocked.Exchange(ref _watchdogChecking, 1) == 1)
                return;

            try
            {
                foreach (var kvp in _ffmpegProcesses.ToArray())
                {
                    int camId = kvp.Key;
                    var process = kvp.Value;

                    try
                    {
                        bool isHealthy = CheckProcessHealth(camId, process);
                        bool hasFileOutput = CheckFileOutput(camId);

                        if (!isHealthy || !hasFileOutput)
                        {
                            _recordingHealthy[camId] = false;
                            RefreshCameraStatus(camId);

                            int failCount = _watchdogFailCount.AddOrUpdate(camId, 1, (_, c) => c + 1);

                            AddLog(
                                $"看门狗: 相机{camId} 录播异常 (进程={!isHealthy},文件={!hasFileOutput})，连续失败 {failCount}/{WATCHDOG_MAX_FAIL_COUNT}",
                                CoreLogLevel.Warn);

                            if (failCount >= WATCHDOG_MAX_FAIL_COUNT)
                            {
                                AddLog($"看门狗: 相机{camId} 连续 {failCount} 次异常，强制重启录制进程", CoreLogLevel.Error);
                                ForceRestartFfmpeg(camId, process);
                                _watchdogFailCount.TryRemove(camId, out _);
                            }
                        }
                        else
                        {
                            _recordingHealthy[camId] = true;
                            RefreshCameraStatus(camId);

                            int prevCount = _watchdogFailCount.TryRemove(camId, out var c) ? c : 0;
                            if (prevCount > 0)
                                AddLog($"看门狗: 相机{camId} 录播已恢复正常");
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"看门狗巡检相机{camId}异常: {ex.Message}", CoreLogLevel.Warn);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _watchdogChecking, 0);
            }
        }

        /// <summary>
        /// 检查 FFmpeg 进程是否存活且响应正常
        /// </summary>
        private bool CheckProcessHealth(int camId, Process? process)
        {
            if (process == null) return false;

            try
            {
                if (process.HasExited)
                {
                    AddLog($"看门狗: 相机{camId} 录制进程已退出(ExitCode={GetExitCode(process)})", CoreLogLevel.Warn);
                    return false;
                }

                // 检查进程是否已僵尸（虽然没退出但不响应）
                // 通过检查进程的 Responding 属性来判定
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    // 有窗口句柄的才检查 Responding（FFmpeg createNoWindow=true，通常无窗口）
                    if (!process.Responding) return false;
                }

                return true;
            }
            catch (InvalidOperationException)
            {
                // 进程已退出但 HasExited 还没更新
                return false;
            }
            catch
            {
                return true; // 无法判断时假定健康
            }
        }

        /// <summary>
        /// 检查输出文件是否健康，每次巡检主动扫目录以捕获整点切片新文件
        /// </summary>
        private bool CheckFileOutput(int camId)
        {
            try
            {
                // ★ 修改：不再只依赖 _lastOutputFile，每次都扫目录
                // 原因：-loglevel warning 过滤了 segment muxer 的 "Opening '...'" INFO 日志
                // 导致整点切片后 _lastOutputFile 长期指向已停止写入的旧文件
                string? latestFromDir = FindLatestCameraFile(camId);
                if (latestFromDir != null)
                {
                    string? currentTracked = _lastOutputFile.TryGetValue(camId, out var t) ? t : null;
                    if (currentTracked != latestFromDir)
                    {
                        _lastOutputFile[camId] = latestFromDir;
                        _lastCheckedFileSize.TryRemove(camId, out _); // 文件路径切换时重置大小基准
                        if (currentTracked != null)                   // 非首次赋值才记录切换日志
                            AddLog($"看门狗: 相机{camId} 检测到新分段文件: {Path.GetFileName(latestFromDir)}");
                    }
                }

                if (_lastOutputFile.TryGetValue(camId, out string? trackedPath)
                    && !string.IsNullOrWhiteSpace(trackedPath))
                {
                    return ValidateFileHealth(camId, trackedPath);
                }

                AddLog($"看门狗: 相机{camId} 今日输出目录无录像文件", CoreLogLevel.Warn);
                return false;
            }
            catch (Exception ex)
            {
                AddLog($"看门狗: 检查相机{camId}文件输出异常: {ex.Message}", CoreLogLevel.Warn);
                return false;
            }
        }

        /// <summary>
        /// 扫描当日目录，按最后写入时间降序返回最新录像文件
        /// </summary>
        private string? FindLatestCameraFile(int camId)
        {
            try
            {
                string? camTypeDir = camId switch
                {
                    1 => "顶部",
                    2 => "左部",
                    3 => "右部",
                    _ => null
                };
                if (camTypeDir == null) return null;

                string searchDir = Path.Combine(
                    VIDEO_RECORD_ROOT,
                    DateTime.Today.ToString("yyyy-MM-dd"),
                    camTypeDir);

                if (!Directory.Exists(searchDir)) return null;

                return new DirectoryInfo(searchDir)
                    .GetFiles($"Cam{camId}_*.ts")
                    .OrderByDescending(f => f.Name)
                    .FirstOrDefault()
                    ?.FullName;
            }
            catch
            {
                return null; // 扫描失败静默忽略，不影响主流程
            }
        }

        /// <summary>
        /// 验证单个文件的健康状态：最后写入时间 & 文件大小增长
        /// </summary>
        private bool ValidateFileHealth(int camId, string filePath)
        {
            if (!File.Exists(filePath))
            {
                AddLog($"看门狗: 相机{camId} 录播文件不存在: {Path.GetFileName(filePath)}", CoreLogLevel.Warn);
                return false;
            }

            var fileInfo = new FileInfo(filePath);

            // 1. 检查最后写入时间是否过期
            var staleness = DateTime.Now - fileInfo.LastWriteTime;
            if (staleness.TotalMinutes > WATCHDOG_FILE_STALE_MINUTES)
            {
                AddLog($"看门狗: 相机{camId} 录播文件 {staleness.TotalMinutes:F1} 分钟未更新: " +
                       $"{Path.GetFileName(filePath)}", CoreLogLevel.Warn);
                return false;
            }

            // 2. 检查文件大小是否过小
            if (fileInfo.Length < WATCHDOG_MIN_FILE_SIZE_BYTES)
            {
                // 文件建立不足 90 秒，给 ffmpeg 时间写入首帧，不计入失败
                if ((DateTime.Now - fileInfo.CreationTime).TotalSeconds < 90)
                    return true;

                AddLog($"看门狗: 相机{camId} 录播文件过小({fileInfo.Length} bytes): " +
                       $"{Path.GetFileName(filePath)}", CoreLogLevel.Warn);
                return false;
            }

            // 3. 检查文件大小是否在增长（避免写入卡死）
            if (_lastCheckedFileSize.TryGetValue(camId, out long prevSize))
            {
                if (fileInfo.Length == prevSize && staleness.TotalSeconds > WATCHDOG_INTERVAL_SEC)
                {
                    AddLog($"看门狗: 相机{camId} 录播文件大小停滞({fileInfo.Length} bytes): " +
                           $"{Path.GetFileName(filePath)}", CoreLogLevel.Warn);
                    return false;
                }
            }
            _lastCheckedFileSize[camId] = fileInfo.Length;

            return true;
        }

        /// <summary>
        /// 强制重启 FFmpeg 进程：Kill 当前进程，Exited 事件会触发自动重连
        /// </summary>
        private void ForceRestartFfmpeg(int camId, Process? process)
        {
            if (process == null)
                return;

            bool isCurrentProcess = false;

            lock (_ffmpegLock)
            {
                if (_ffmpegProcesses.TryGetValue(camId, out var current)
                    && ReferenceEquals(current, process))
                {
                    isCurrentProcess = true;
                }
            }

            if (!isCurrentProcess)
            {
                AddLog($"看门狗: 相机{camId} 目标进程已不是当前进程，忽略强制重启", CoreLogLevel.Warn);
                return;
            }
            // Kill 前清除文件跟踪，确保强制重启前旧状态不残留
            _lastOutputFile.TryRemove(camId, out _);
            _lastCheckedFileSize.TryRemove(camId, out _);

            try
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception ex)
                    {
                        AddLog($"看门狗: Kill 相机{camId} ffmpeg 失败: {ex.Message}", CoreLogLevel.Warn);
                    }
                }
            }
            catch
            {
            }

            // 兜底：如果 Kill 后 Exited 没有触发，则手动清理并调度一次重启
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(3000);

                    if (_disposed || (_monitoringCts?.IsCancellationRequested ?? true))
                        return;

                    bool needManualRestart = false;

                    lock (_ffmpegLock)
                    {
                        if (_ffmpegProcesses.TryGetValue(camId, out var current)
                            && ReferenceEquals(current, process))
                        {
                            _ffmpegProcesses.TryRemove(camId, out _);
                            needManualRestart = true;
                        }
                    }

                    if (!needManualRestart)
                        return;

                    try { process.Dispose(); } catch { }

                    var token = _monitoringCts?.Token ?? CancellationToken.None;
                    var cfg = GetRecordingConfig(camId);

                    if (!string.IsNullOrWhiteSpace(cfg.rtspUrl))
                    {
                        AddLog($"看门狗: 相机{camId} Exited 未及时触发，执行兜底重启", CoreLogLevel.Warn);
                        ScheduleFfmpegRestart(camId, cfg.rtspUrl, cfg.type, token, TimeSpan.FromSeconds(1));
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"看门狗强制重启相机{camId}失败: {ex.Message}", CoreLogLevel.Error);
                }
            });
        }

        private (string rtspUrl, string type) GetRecordingConfig(int camId)
        {
            return camId switch
            {
                1 => (RTSP_URL_CAM1, "顶部"),
                2 => (RTSP_URL_CAM2, "左部"),
                3 => (RTSP_URL_CAM3, "右部"),
                _ => (string.Empty, "Unknown")
            };
        }

        private static int GetExitCode(Process? p)
        {
            try { return p?.ExitCode ?? -1; }
            catch { return -1; }
        }

        /// <summary>
        /// 刷新指定相机的UI状态指示
        /// </summary>
        public void RefreshCameraStatus(int camId)
        {
            bool captureOk = _captureHealthy.TryGetValue(camId, out var c) && c;
            bool recordingOk = _recordingHealthy.TryGetValue(camId, out var r) && r;

            string status;
            Brush color;

            if (!IsProcessing)
            {
                status = "未启动";
                color = Brushes.Gray;
            }
            else if (!captureOk)
            {
                status = "拉流断开";
                color = Brushes.Red;
            }
            else if (!recordingOk && _ffmpegProcesses.ContainsKey(camId))
            {
                // 只有在 ffmpeg 进程存在但录制不正常时才标记为录制异常
                status = "录制异常";
                color = Brushes.Orange;
            }
            else
            {
                status = "正常";
                color = Brushes.Green;
            }

            switch (camId)
            {
                case 1: Cam1Status = status; Cam1StatusColor = color; break;
                case 2: Cam2Status = status; Cam2StatusColor = color; break;
                case 3: Cam3Status = status; Cam3StatusColor = color; break;
            }
        }

        /// <summary>
        /// 刷新全部相机状态
        /// </summary>
        private void RefreshAllCameraStatus()
        {
            for (int i = 1; i <= 3; i++) RefreshCameraStatus(i);
        }

        #endregion

        /// <summary>
        /// 预创建视频录制所需的日期及类型子目录（今天 + 明天）
        /// </summary>
        private void CreateRequiredDirectories(string root, string safeType)
        {
            if (!CheckDiskSpace(root))
            {
                AddLog($"磁盘空间不足，无法创建录制目录", CoreLogLevel.Error);
                return;
            }

            var dates = new[] { DateTime.Today, DateTime.Today.AddDays(1) };
            foreach (var date in dates)
            {
                string dateFolder = date.ToString("yyyy-MM-dd");
                string fullPath = Path.Combine(root, dateFolder, safeType);
                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);
            }
        }


        private bool CheckDiskSpace(string path, double minSpaceGb = 1.0)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    AddLog("磁盘路径为空，无法检查磁盘空间", CoreLogLevel.Error);
                    return false;
                }
                // 获取路径所在的驱动器
                string? driveName = Path.GetPathRoot(path);
                if (string.IsNullOrWhiteSpace(driveName))
                {
                    AddLog($"无法获取路径 {path} 的驱动器信息", CoreLogLevel.Error);
                    return false;
                }
                var driveInfo = new DriveInfo(driveName);

                long minSpaceBytes = (long)(minSpaceGb * 1024 * 1024 * 1024);
                long availableSpace = driveInfo.AvailableFreeSpace;

                if (availableSpace < minSpaceBytes)
                {
                    string message = $"磁盘 {driveName} 剩余空间不足。" +
                        $"要求: {minSpaceGb}GB, 实际: {availableSpace / (1024.0 * 1024 * 1024):F2}GB";
                    AddLog(message, CoreLogLevel.Warn);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var response = await _webApiClient.PushMessageAsync(message);
                            if (response.Code != 200)
                                _logger.Warn($"消息推送失败：{response.Message}");
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"消息推送异常：{ex.Message}");
                        }
                    });
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                AddLog($"检查磁盘空间时出错: {ex.Message}", CoreLogLevel.Error);
                return false;
            }
        }
        #endregion

        #region 算法

        private bool InitializeAIEngines()
        {
            try
            {
                string[] labels = new string[] { "Pack", "Person", "Hand", "Hook" };
                ai.InitYoLoDetectEngine(Top_Det_Path, labels, true, out topDetEngine, out string errmess);
                if (topDetEngine == null || !string.IsNullOrEmpty(errmess))
                {
                    StatusMessage = $"顶部相机行为检测引擎初始化失败: {errmess}";
                    AddLog($"顶部相机行为检测引擎初始化失败: {errmess}", CoreLogLevel.Error);
                    return false;
                }
                string[] laylabes = new string[] { "Hook", "Red", "Precursor", "Label", "Wheel" };
                ai.InitYoLoDetectEngine(PackA_Det_Path, laylabes, true, out misLayDetEngineA, out errmess);
                if (misLayDetEngineA == null || !string.IsNullOrEmpty(errmess))
                {
                    StatusMessage = $"CameraA模型初始化失败: {errmess}";
                    AddLog($"CameraA模型初始化失败: {errmess}", CoreLogLevel.Error);
                    return false;
                }
                ai.InitYoLoDetectEngine(PackB_Det_Path, laylabes, true, out misLayDetEngineB, out errmess);
                if (misLayDetEngineB == null || !string.IsNullOrEmpty(errmess))
                {
                    StatusMessage = $"CameraB模型初始化失败: {errmess}";
                    AddLog($"Camera模型初始化失败: {errmess}", CoreLogLevel.Error);
                    return false;
                }
                AddLog("AI引擎初始化成功", isToDatabase: true);
                return true;

            }
            catch (Exception ex)
            {
                AddLog($"AI引擎初始化异常: {ex.Message}", CoreLogLevel.Error);
                return false;
            }
        }



        /// <summary>
        /// 异步保存异常图像，并叠加文字信息
        /// </summary>
        private void SaveAnomalyImage(int camId, string detectType, Mat frame, string anomalyDesc)
        {
            if (frame == null || frame.Empty()) return;

            Mat saveMat = frame.Clone();

            Task.Run(() =>
            {
                try
                {
                    using (saveMat)
                    {
                        // 1. 在图片上叠加文字 (水印)            
                        string safeDetectType = string.IsNullOrWhiteSpace(detectType) ? "Unknown" : detectType.Trim();
                        string safeAnomalyDesc = string.IsNullOrWhiteSpace(anomalyDesc) ? "" : anomalyDesc.Trim();

                        string drawText = $"{safeDetectType}: {safeAnomalyDesc}";

                        DrawTextWithChinese(saveMat, drawText);

                        // 2. 目录巡检
                        string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                        string dir = Path.Combine(ANOMALY_IMAGE_ROOT, dateFolder);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                        // 3. 写入文件
                        string detectTypeForFile = ConvertToSafeFileName(safeDetectType);
                        string fileName = $"Cam{camId}_{detectTypeForFile}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
                        string fullPath = Path.Combine(dir, fileName);

                        SaveMatAsJpeg(saveMat, fullPath, 100);


                        // 记录日志（异步）
                        _logger?.Warn($"[后台存图] 相机{camId}异常图已保存: {fileName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error($"异步存图失败: {ex.Message}");
                }
            });
        }


        private void DrawTextWithChinese(Mat mat, string text)
        {
            if (mat == null || mat.Empty() || string.IsNullOrWhiteSpace(text)) return;

            lock (_drawLock)
            {
                using (var bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(mat))
                using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    float x = 20;
                    float y = 50;

                    // 测量文字大小
                    System.Drawing.SizeF textSize = graphics.MeasureString(text, font);

                    // 绘制半透明黑色背景，提高可读性
                    using (var backgroundBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(160, 0, 0, 0)))
                    {
                        graphics.FillRectangle(
                            backgroundBrush,
                            x - 8,
                            y - 6,
                            textSize.Width + 16,
                            textSize.Height + 12
                        );
                    }

                    // 绘制阴影
                    using (var shadowBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Black))
                    {
                        graphics.DrawString(text, font, shadowBrush, x + 2, y + 2);
                    }

                    // 绘制前景文字
                    using (var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Red))
                    {
                        graphics.DrawString(text, font, textBrush, x, y);
                    }

                    graphics.Flush();

                    using (var tempMat = OpenCvSharp.Extensions.BitmapConverter.ToMat(bitmap))
                    {
                        tempMat.CopyTo(mat);
                    }
                }
            }

        }

        private string ConvertToSafeFileName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Unknown";

            string safeText = string.Join(
                "_",
                text.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)
            );

            safeText = safeText.Trim().TrimEnd('.');

            return string.IsNullOrWhiteSpace(safeText) ? "Unknown" : safeText;
        }

        private void SaveMatAsJpeg(Mat mat, string fullPath, int quality = 95)
        {
            if (mat == null || mat.Empty())
                return;

            int[] encodeParams =
            {
                (int)ImwriteFlags.JpegQuality, quality
            };

            Cv2.ImEncode(".jpg", mat, out byte[] imageBytes, encodeParams);

            File.WriteAllBytes(fullPath, imageBytes);
        }


        private string ConvertToSafeTypeName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Unknown";

            text = text.Trim();

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                text = text.Replace(c, '_');
            }

            return text;
        }

        #endregion



        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { StopMonitoring(); } catch { /* ignore */ }

            try
            {
                _clockTimer?.Stop();
                _clockTimer = null;
            }
            catch { }

            (topDetEngine as IDisposable)?.Dispose();
            (misLayDetEngineA as IDisposable)?.Dispose();
            (misLayDetEngineB as IDisposable)?.Dispose();
            topDetEngine = null;
            misLayDetEngineA = null;
            misLayDetEngineB = null;

            _monitoringCts?.Dispose();
            _monitoringCts = null;

            GC.SuppressFinalize(this);
        }


        //日志
        public void AddLog(string message, CoreLogLevel level = CoreLogLevel.Info, bool isToDatabase = false,
           [CallerMemberName] string memberName = "",
           [CallerFilePath] string filePath = "",
           [CallerLineNumber] int lineNumber = 0)
        {
            // 校验日志内容
            if (string.IsNullOrWhiteSpace(message))
                return;
            SolidColorBrush textColor;
            switch (level)
            {
                case CoreLogLevel.Info:
                    textColor = Brushes.Black;
                    _logger?.Info(message, true, memberName, filePath, lineNumber);
                    break;
                case CoreLogLevel.Warn:
                    textColor = Brushes.DarkOrange;
                    _logger?.Warn(message, true, memberName, filePath, lineNumber);
                    break;
                case CoreLogLevel.Error:
                    textColor = Brushes.Red;
                    _logger?.Error(message, true, memberName, filePath, lineNumber);
                    break;
                default:
                    textColor = Brushes.Black;
                    _logger?.Log(level, message, isToDatabase, memberName, filePath, lineNumber);
                    break;
            }

            var newLog = new LogItemModel
            {
                Message = $"[{DateTime.Now:MM/dd HH:mm:ss}] [{level}] {message}",
                LogColor = textColor
            };

            // 仅当内容有效时添加到日志集合
            var dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;

            try
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    AlarmLogs.Insert(0, newLog);
                    if (AlarmLogs.Count > 100) AlarmLogs.RemoveAt(AlarmLogs.Count - 1);
                }));
            }
            catch (TaskCanceledException) { }
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
        #endregion

        internal sealed class CameraConfig
        {
            public int CamId { get; init; }                       // 1/2/3
            public string Label { get; init; } = "";              // "Top"/"Left"/"Right"
            public string DisplayName { get; init; } = "";        // "顶部"/"左部"/"右部"
            public string RtspUrl { get; init; } = "";
            public FrameChannel LiveChannel { get; init; }        // UI 预览通道
            public FrameChannel AnomalyChannel { get; init; }     // UI 异常通道
            public string AnomalyType { get; init; } = "";        // 文件名标签
            public Action<string> SetAnomalyInfo { get; init; } = _ => { };
            public Func<Mat, DetectResult> Detect { get; init; } = _ => default;
        }

        internal readonly struct DetectResult
        {
            public Mat? ResultMat { get; init; }
            public List<AIResults>? Results { get; init; }
            public string? Error { get; init; }
        }
    }




    public class LogItemModel
    {
        public string Message { get; set; } = string.Empty;
        public SolidColorBrush LogColor { get; set; } = Brushes.Black;
    }

    public enum FrameChannel
    {
        Live1 = 1, Live2 = 2, Live3 = 3,
        Anomaly1 = 4, Anomaly2 = 5, Anomaly3 = 6
    }
}
