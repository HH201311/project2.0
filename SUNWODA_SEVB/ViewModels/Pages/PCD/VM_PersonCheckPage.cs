using LibVLCSharp.Shared;
using OpenCvSharp;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Web;
using System.Collections.Concurrent;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;


namespace SUNWODA_SEVB.ViewModels.Pages.PCD
{
    /// <summary>
    /// 义乌人员合规检测
    /// </summary>
    [Module("VM_PersonCheckPage","义乌人员合规检测")]
    public class VM_PersonCheckPage : ViewModelBase
    {
        #region ======== 常量 & 配置 ========

        // ---- RTSP 流地址（按实际部署修改或改为配置文件读取） ----
        private const string RTSP_URL_CAM1 = "rtsp://admin:xwd123456@192.168.1.101:554/Streaming/Channels/101";
        private const string RTSP_URL_CAM2 = "rtsp://admin:xwd123456@192.168.1.102:554/Streaming/Channels/101";
        private const string RTSP_URL_CAM3 = "rtsp://admin:xwd123456@192.168.1.103:554/Streaming/Channels/101";

        // ---- 反射调用DLL路径（需放置于服务器共享路径） ----
        private const string DLL_PATH = @"\\SERVER\AIModels\YWPackActionDetect.dll";

        // ---- 模型文件路径 ----
        private const string TOP_MODEL_PATH = @"\\SERVER\AIModels\YWTop_Det.onnx";
        private const string PACK_MODEL_PATH = @"\\SERVER\AIModels\YW_PackMislay_Det.onnx";

        // ---- 检测区域 (顶部相机) ----
        private static readonly List<OpenCvSharp.Point> DefaultSpecifiedZone = new()
        {
            new OpenCvSharp.Point(680, 300),
            new OpenCvSharp.Point(1540, 300),
            new OpenCvSharp.Point(1540, 860),
            new OpenCvSharp.Point(680, 860)
        };

        // ---- 异常录像保存根目录 ----
        private const string ANOMALY_VIDEO_ROOT = @"D:\AnomalyVideos";

        // ---- 检测间隔(毫秒) ----
        private const int DETECTION_INTERVAL_MS = 500;

        // ---- 异常回放前后录制秒数 ----
        private const int ANOMALY_RECORD_SECONDS = 10;


        #endregion

        #region ======== 私有字段 ========

        private readonly LibVLC? _libVLC;
        private readonly Dispatcher? _dispatcher;
        private readonly ILoggerService<VM_PersonCheckPage> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebApiClient _webApiClient;

        // 实时流播放器
        private MediaPlayer? _liveMediaPlayer1;
        private MediaPlayer? _liveMediaPlayer2;
        private MediaPlayer? _liveMediaPlayer3;

        //// 异常回放播放器
        //private MediaPlayer _anomalyMediaPlayer1;
        //private MediaPlayer _anomalyMediaPlayer2;
        //private MediaPlayer _anomalyMediaPlayer3;

        // 取消令牌
        private CancellationTokenSource? _monitoringCts;

        // 反射加载的DLL程序集
        private Assembly? _aiAssembly;
        private Type? _aiType;

        // 模型句柄（IPredictor）
        private object? _topDetEngine;    // 顶部相机行为检测引擎
        private object? _packDetEngine;   // 电池包放反检测引擎

        // 时钟定时器
        private DispatcherTimer? _clockTimer;

        // 帧抓取环形缓冲（每个相机独立）
        private readonly ConcurrentDictionary<int, Mat> _latestFrames = new();

        // 状态
        private bool _isProcessing;
        private string _statusMessage = "就绪";
        private int _totalCheckCount;
        private int _violationCount;
        private string _latestViolationHint = "暂无";
        private DateTime _currentTime = DateTime.Now;
        private bool _hasAnomaly1;
        private bool _hasAnomaly2;
        private bool _hasAnomaly3;
        private bool _disposed;

        #endregion

        public VM_PersonCheckPage(ILoggerService<VM_PersonCheckPage> logger,IWebApiClient webApiClient,IServiceProvider serviceProvider)
        {
            _logger = logger;
            _webApiClient = webApiClient?? throw new ArgumentNullException(nameof(webApiClient));
            _serviceProvider = serviceProvider?? throw new ArgumentNullException(nameof(serviceProvider));

            _dispatcher = Application.Current.Dispatcher;



        }

    }
}
