using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HandyControl.Controls;
using HandyControl.Data;
using Microsoft.Win32;
using RemoteFileMonitor.Abstractions;
using RemoteFileMonitor.Options;
using RemoteFileMonitor.Services;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.JRRY;
using SUNWODA_SEVB.Services.Project.CTOL;
using SUNWODA_SEVB.Services.Project.JRRY;
using SUNWODA_SEVB.Views.Pages.JRRY;
using InspectionResultService = SUNWODA_SEVB.Services.Project.JRRY.InspectionResultService;
using MessageBox = HandyControl.Controls.MessageBox;

namespace SUNWODA_SEVB.ViewModels.Pages.JRRY
{
    [Module("VM_JRRedundancyPage", "极耳冗余 AI视觉检测系统V1.0")]
    public class VM_JRRedundancyPage : ViewModelBase
    {
        #region 字段

        private const string MonitorId = "JRRY";
        private const string ViewModelKey = "VM_JRRedundancyPage";

        private readonly IMonitorManager _monitorManager;
        private readonly RemoteFileMonitorConfigService _configService;
        private readonly ProcessingResultService _resultService;
        private readonly ResultAggregator _resultAggregator;
        private readonly InspectionResultService _inspectionResultService;
        private readonly IProjectSettingRepository _settingRepo;
        private readonly ILoggerService<VM_JRRedundancyPage> _logger;

        private MonitorControl? _monitor;

        // 监控参数
        private string _remotePath = string.Empty;
        private string _outputDirectory = string.Empty;
        private string _filter = "*.*";
        private bool _includeSubdirectories;
        private int _pollingIntervalSeconds = 5;
        private int _pollingOverlapSeconds = 1;

        // 状态与预览
        private int _processedCount;
        private string _lastProcessedFileName = "尚无";
        private ImageSource? _resultImage;

        // 独立会话计数器
        private int _sessionTotalCount;
        private int _sessionOkCount;

        // 历史统计
        private int _totalDetectionNum;
        private int _okDetectionNum;
        private int _ngDetectionNum;
        private DateTime _clearTime;

        // 数据查询
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today.AddDays(1);
        private int _totalCount;
        private int _pageIndex = 1;
        private int _maxPageCount = 1;
        // 查询时是否繁忙
        private bool _isBusy;
        // 存储当前查询的完整结果集
        private List<InspectionRecord> _allQueryResults = [];  

        // 尺寸标注
        private string _annotationImagePath = string.Empty;
        private AnnotationWindow? _annotationWindow;
        private ImageSource? _annotationImage;

        #endregion

        #region 属性

        /// <summary>
        /// 获取或设置处理结果的预览图像
        /// </summary>
        public ImageSource? ResultImage
        {
            get => _resultImage;
            set => SetProperty(ref _resultImage, value);
        }

        /// <summary>
        /// 获取或设置远程监控路径（网络共享文件夹）
        /// </summary>
        public string RemotePath
        {
            get => _remotePath;
            set => SetProperty(ref _remotePath, value);
        }

        /// <summary>
        /// 获取或设置输出目录（处理结果保存路径）
        /// </summary>
        public string OutputDirectory
        {
            get => _outputDirectory;
            set => SetProperty(ref _outputDirectory, value);
        }

        /// <summary>
        /// 获取或设置文件过滤器（如 *.jpg;*.png）
        /// </summary>
        public string Filter
        {
            get => _filter;
            set => SetProperty(ref _filter, value);
        }

        /// <summary>
        /// 获取或设置是否包含子目录进行监控
        /// </summary>
        public bool IncludeSubdirectories
        {
            get => _includeSubdirectories;
            set => SetProperty(ref _includeSubdirectories, value);
        }

        /// <summary>
        /// 获取或设置轮询间隔时间（秒）
        /// </summary>
        public int PollingIntervalSeconds
        {
            get => _pollingIntervalSeconds;
            set => SetProperty(ref _pollingIntervalSeconds, value);
        }

        /// <summary>
        /// 获取或设置轮询重叠时间（秒），用于处理文件写入未完成的情况
        /// </summary>
        public int PollingOverlapSeconds
        {
            get => _pollingOverlapSeconds;
            set => SetProperty(ref _pollingOverlapSeconds, value);
        }

        /// <summary>
        /// 获取当前监控实例是否正在运行
        /// </summary>
        public bool IsRunning => _monitor?.IsRunning ?? false;

        /// <summary>
        /// 获取当前是否可以编辑监控参数（运行中不可编辑）
        /// </summary>
        public bool CanEdit => !IsRunning;

        /// <summary>
        /// 获取当前监控状态文本（运行中/已停止）
        /// </summary>
        public string StatusText => IsRunning ? "运行中" : "已停止";

        /// <summary>
        /// 获取启动/停止按钮的文本，根据当前状态显示“启动”或“停止”
        /// </summary>
        public string ToggleButtonText => IsRunning ? "停止" : "启动";

        /// <summary>
        /// 获取或设置已处理的文件数量
        /// </summary>
        public int ProcessedCount
        {
            get => _processedCount;
            set => SetProperty(ref _processedCount, value);
        }

        /// <summary>
        /// 获取或设置最后处理的文件名
        /// </summary>
        public string LastProcessedFileName
        {
            get => _lastProcessedFileName;
            set => SetProperty(ref _lastProcessedFileName, value);
        }

        /// <summary>
        /// 当前处理的条码，用于实时显示当前检测的电芯条码
        /// </summary>
        public string CurrentBarcode { get; set; } = "";

        /// <summary>
        /// 当前检测结果，用于实时显示当前电芯的检测结果（OK/NG）
        /// </summary>
        public string CurrentDetectionResult { get; set; } = "";

        /// <summary>
        /// 间距检测结果集合（左/右距离，左/右最小/最大高度距离）
        /// </summary>
        public ObservableCollection<DetectionItem> GapResults { get; set; } = [];

        /// <summary>
        /// 左侧开叉结果（面积、宽度、高度、角度1、角度2、周长、CIL）
        /// </summary>
        public ObservableCollection<DetectionItem> LeftForkResults { get; set; } = [];

        /// <summary>
        /// 右侧开叉结果
        /// </summary>
        public ObservableCollection<DetectionItem> RightForkResults { get; set; } = [];

        #region 实时缺陷统计（饼图、表格）

        /// <summary>
        /// 获取缺陷数据统计列表，用于实时显示各缺陷类型的数量和比例
        /// </summary>
        public ObservableCollection<DefectDataStatisticsModel> DefectDataList { get; set; } = [];

        /// <summary>
        /// 总图片检测数量
        /// </summary>
        public int TotalCount => _sessionTotalCount;

        /// <summary>
        /// 图片检测OK数量
        /// </summary>
        public int OkCount => _sessionOkCount;

        /// <summary>
        /// 获取 NG 检测数量（总数减去 OK 数量）
        /// </summary>
        public int NgCount => _sessionTotalCount - _sessionOkCount;

        /// <summary>
        /// 获取良率，计算公式为 OK 数量除以总检测数量，若总数为 0 则返回 0
        /// </summary>
        public double YieldRate => _sessionTotalCount == 0 ? 0 : (double)_sessionOkCount / _sessionTotalCount;

        /// <summary>
        /// 获取用于绘制饼图的缺陷统计数据（OK 数量、NG 数量）
        /// </summary>
        public List<double> DetectionSattisticalData => [OkCount, NgCount];

        /// <summary>
        /// 获取用于绘制饼图的缺陷统计标签（固定为 OK、NG）
        /// </summary>
        public List<string> DetectionSattisticalLabel => ["OK", "NG"];

        /// <summary>
        /// 获取用于绘制饼图的缺陷统计颜色（OK 绿色，NG 红色）
        /// </summary>
        public List<string> DetectionStatisticalLabelColor => ["#4ade80", "#ef4444"];

        /// <summary>
        /// 获取或设置监测结果列表，用于显示每个条码的检测结果（OK/NG）和缺陷类型
        /// </summary>
        public ObservableCollection<TestResultModel> TestResultList { get; } = [];

        #endregion

        #region 历史统计（清零后保留）

        /// <summary>
        /// 获取或设置总检测数量（历史累计值，清零后仍保留）
        /// </summary>
        public int TotalDetectionNum
        {
            get => _totalDetectionNum;
            set => SetProperty(ref _totalDetectionNum, value);
        }

        /// <summary>
        /// 获取或设置 OK 检测数量（历史累计值，清零后仍保留）
        /// </summary>
        public int OKDetectionNum
        {
            get => _okDetectionNum;
            set => SetProperty(ref _okDetectionNum, value);
        }

        /// <summary>
        /// 获取或设置 NG 检测数量（历史累计值，清零后仍保留）
        /// </summary>
        public int NGDetectionNum
        {
            get => _ngDetectionNum;
            set => SetProperty(ref _ngDetectionNum, value);
        }

        /// <summary>
        /// 获取或设置清零时间（上次清空统计数据的时间）
        /// </summary>
        public DateTime ClearTime
        {
            get => _clearTime;
            set => SetProperty(ref _clearTime, value);
        }

        /// <summary>
        /// 获取历史累计良率，计算公式为 OK 检测数量除以总检测数量，若总数为 0 则返回 100.0
        /// </summary>
        public double OKRate => TotalDetectionNum == 0 ? 100.0 : (double)OKDetectionNum / TotalDetectionNum * 100.0;

        #endregion

        #region 数据查询

        /// <summary>
        /// 获取或设置查询的起始日期，用于筛选检测记录
        /// </summary>
        public DateTime StartDate 
        { 
            get => _startDate; 
            set => SetProperty(ref _startDate, value); 
        }

        /// <summary>
        /// 获取或设置查询的结束日期，用于筛选检测记录
        /// </summary>
        public DateTime EndDate 
        { 
            get => _endDate; 
            set => SetProperty(ref _endDate, value); 
        }

        /// <summary>
        /// 获取或设置查询结果的总记录数，用于分页显示
        /// </summary>
        public ObservableCollection<DefectDataStatisticsModel> DefectSelectedItems { get; set; } = [];

        /// <summary>
        /// 获取或设置查询结果的总记录数，用于分页显示
        /// </summary>
        public int TotalQueryCount 
        { 
            get => _totalCount; 
            set => SetProperty(ref _totalCount, value); 
        }

        /// <summary>
        /// 获取或设置当前查询结果的页码索引（从 1 开始），用于分页显示
        /// </summary>
        public int PageIndex 
        { 
            get => _pageIndex; 
            set => SetProperty(ref _pageIndex, value); 
        }

        /// <summary>
        /// 获取或设置查询结果的最大页数，用于分页显示
        /// </summary>
        public int MaxPageCount 
        { 
            get => _maxPageCount; 
            set => SetProperty(ref _maxPageCount, value); 
        }

        /// <summary>
        /// 获取或设置查询结果列表，用于显示每条检测记录的时间、条码和缺陷类型
        /// </summary>
        public ObservableCollection<InspectionRecord> ShowResults { get; } = [];


        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        #endregion

        #region 参数设置

        /// <summary>
        /// 获取或设置参数列表，用于配置各项检测参数的上下限
        /// </summary>
        public ObservableCollection<ParameterItem> ParametersThresholdRows { get; } = [];        

        #endregion

        #region 尺寸标注

        /// <summary>
        /// 获取或设置标注图片路径，用于尺寸标注功能的图像来源
        /// </summary>
        public string AnnotationImagePath 
        { 
            get => _annotationImagePath; 
            set => SetProperty(ref _annotationImagePath, value); 
        }

        /// <summary>
        /// 尺寸标注的图像源
        /// </summary>
        public ImageSource? AnnotationImage
        {
            get => _annotationImage;
            set => SetProperty(ref _annotationImage, value);
        }

        #endregion

        #endregion

        #region  命令

        /// <summary>
        /// 获取启动/停止监控的命令，根据当前状态执行启动或停止操作
        /// </summary>
        public ICommand ToggleCommand { get; }

        /// <summary>
        /// 获取浏览远程监控路径的命令，弹出文件夹选择对话框以选择网络共享文件夹
        /// </summary>
        public ICommand BrowseRemotePathCommand { get; }
        
        /// <summary>
        /// 获取清空历史统计数据的命令，重置总检测数量、OK 数量、NG 数量和清零时间
        /// </summary>
        public ICommand ClearDetectionInfoCommand { get; }

        /// <summary>
        /// 获取查询检测记录的命令，根据起始日期、结束日期和缺陷类型筛选结果
        /// </summary>
        public ICommand QueryCommand { get; }

        /// <summary>
        /// 获取导出查询结果的命令，将当前查询结果导出为 Excel 文件
        /// </summary>
        public ICommand ExportCommand { get; }

        /// <summary>
        /// 获取分页更新的命令，当用户切换页码时触发查询以更新显示结果
        /// </summary>
        public ICommand PageUpdatedCommand { get; }

        /// <summary>
        /// 获取新增参数的命令，向参数列表中添加一个新的参数项
        /// </summary>
        public ICommand AddParameterCommand { get; }

        /// <summary>
        /// 获取删除参数的命令，根据指定的参数项从参数列表中移除该项
        /// </summary>
        public ICommand DeleteParameterCommand { get; }

        /// <summary>
        /// 获取保存参数设置的命令，将当前参数列表保存到配置文件或数据库中
        /// </summary>
        public ICommand SaveSettingsCommand { get; }        

        /// <summary>
        /// 获取启动尺寸标注的命令，打开标注窗口以进行图像标注
        /// </summary>
        public ICommand StartAnnotationCommand { get; }

        /// <summary>
        /// 获取加载标注图像的命令，从指定路径加载图像以进行尺寸标注
        /// </summary>
        public ICommand LoadImageCommand { get; }
        
        #endregion                                       

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="projectSettingRepository"></param>
        /// <param name="monitorManager"></param>
        /// <param name="configService"></param>
        /// <param name="resultService"></param>
        /// <param name="groupingService"></param>
        public VM_JRRedundancyPage
        (
            ILoggerService<VM_JRRedundancyPage> logger,
            IProjectSettingRepository projectSettingRepository,
            IMonitorManager monitorManager,
            RemoteFileMonitorConfigService configService,
            ProcessingResultService resultService,
            ResultAggregator resultAggregator,
            InspectionResultService inspectionResultService
        )
        {
            _logger = logger;
            _settingRepo = projectSettingRepository;
            _monitorManager = monitorManager;
            _configService = configService;
            _resultService = resultService;
            _resultAggregator = resultAggregator;
            _inspectionResultService = inspectionResultService;

            ToggleCommand = new RelayCommandAsync(ToggleAsync);
            BrowseRemotePathCommand = new RelayCommandAsync(BrowseRemotePathAsync);            
            ClearDetectionInfoCommand = new RelayCommandAsync(ClearDetectionInfoAsync);
            QueryCommand = new RelayCommandAsync(QueryAsync);
            ExportCommand = new RelayCommandAsync(ExportAsync);
            PageUpdatedCommand = new RelayCommand<FunctionEventArgs<int>>(PageUpdated);
            AddParameterCommand = new RelayCommand(AddParameter);
            DeleteParameterCommand = new RelayCommand<ParameterItem>(DeleteParameter);
            SaveSettingsCommand = new RelayCommandAsync(SaveSettingsAsync);                                                        
            StartAnnotationCommand = new RelayCommandAsync(OpenAnnotationWindow);
            LoadImageCommand = new RelayCommandAsync(LoadAnnotationImage);            
        }

        public override void OnInitialize()
        {
            _monitor = _monitorManager.Controls.FirstOrDefault(c => c.Id == MonitorId);
            if (_monitor == null)
            {
                _logger.Error($"监控实例 {MonitorId} 未找到，请检查 remote_file_monitor_config.json 是否包含此 Id。");
                MessageBox.Show($"找不到监控实例 \"{MonitorId}\"，请确认配置文件中已添加该目标。", "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LoadOptionsFromMonitor();

            _monitor.StateChanged += (_, _) => RunOnUIThread(RefreshRunningState);

            // 图像处理完成 → 预览
            _resultService.ImageProcessed += (monitorId, outputPath) =>
            {
                if (!monitorId.Equals(MonitorId, StringComparison.OrdinalIgnoreCase)) return;

                RunOnUIThread(() => UpdatePreview(outputPath));
            };

            // 订阅检测结果事件
            _resultAggregator.ResultArrived += result =>
            {
                RunOnUIThread(() => OnDetectionResultReceived(result));
            };

            LoadParameters();

            // 订阅参数集合变更事件
            ParametersThresholdRows.CollectionChanged += OnParametersCollectionChanged;

            // 订阅每个参数项的名称变更（支持编辑参数名时更新缺陷类型）
            foreach (var p in ParametersThresholdRows)
                p.PropertyChanged += OnParameterItemPropertyChanged;

            // 基于当前参数列表初始化缺陷类型列表
            SyncDefectListFromParameters();

            DefectSelectedItems.Clear();
            foreach (var item in DefectDataList)
                DefectSelectedItems.Add(item);

            LoadHistoricalStats();

            // 初始化
            //CurrentBarcode = "SN123456789evb";
            //CurrentDetectionResult = "OK";
            //GapResults.Add(new DetectionItem { Name = "左侧距离", Value = 0.32, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //GapResults.Add(new DetectionItem { Name = "左侧最小高度距离", Value = 0.56, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //GapResults.Add(new DetectionItem { Name = "左侧最大高度距离", Value = 12.87, ValueColor = new SolidColorBrush(Colors.Red) });
            //GapResults.Add(new DetectionItem { Name = "右侧距离", Value = 0.33, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //GapResults.Add(new DetectionItem { Name = "右侧最小高度距离", Value = 0.57, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //GapResults.Add(new DetectionItem { Name = "右侧最大高度距离", Value = 0.99, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //LeftForkResults.Add(new DetectionItem { Name = "面积", Value = 0.32, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //LeftForkResults.Add(new DetectionItem { Name = "宽度", Value = 0.56, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //LeftForkResults.Add(new DetectionItem { Name = "高度", Value = 0.98, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //LeftForkResults.Add(new DetectionItem { Name = "角度1", Value = 0.33, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //LeftForkResults.Add(new DetectionItem { Name = "角度2", Value = 0.33, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //LeftForkResults.Add(new DetectionItem { Name = "周长", Value = 0.99, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //LeftForkResults.Add(new DetectionItem { Name = "CIL", Value = 0.90, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //RightForkResults.Add(new DetectionItem { Name = "面积", Value = 0.32, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //RightForkResults.Add(new DetectionItem { Name = "宽度", Value = 0.56, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //RightForkResults.Add(new DetectionItem { Name = "高度", Value = 10.98, ValueColor = new SolidColorBrush(Colors.Red) });
            //RightForkResults.Add(new DetectionItem { Name = "角度1", Value = 0.33, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //RightForkResults.Add(new DetectionItem { Name = "角度2", Value = 0.33, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //RightForkResults.Add(new DetectionItem { Name = "周长", Value = 0.99, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //RightForkResults.Add(new DetectionItem { Name = "CIL", Value = 0.90, ValueColor = new SolidColorBrush(Colors.LightGreen) });
            //// 初始化检测数据列表
            //TestResultList.Clear();
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "左侧距离不良", DetectionResult = "NG" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "右侧最大高度不良、面积不良", DetectionResult = "NG" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "面积不良", DetectionResult = "NG" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //TestResultList.Add(new TestResultModel { Barcode = "SN123456", DefectType = "", DetectionResult = "OK" });
            //// 初始化数据检索列表
            //ShowResults.Clear();
            //ShowResults.Add(new InspectionRecord { Time = DateTime.Now, Barcode = "SN123456", DefectType = "面积不良" });
            //ShowResults.Add(new InspectionRecord { Time = DateTime.Now, Barcode = "SN123456", DefectType = "左侧距离不良" });
            //ShowResults.Add(new InspectionRecord { Time = DateTime.Now, Barcode = "SN123456", DefectType = "左侧距离不良" });
            //ShowResults.Add(new InspectionRecord { Time = DateTime.Now, Barcode = "SN123456", DefectType = "左侧距离不良" });
            //ShowResults.Add(new InspectionRecord { Time = DateTime.Now, Barcode = "SN123456", DefectType = "左侧距离不良" });
            //ShowResults.Add(new InspectionRecord { Time = DateTime.Now, Barcode = "SN123456", DefectType = "左侧距离不良" });
            
            RefreshStatisticsBinding();

            base.OnInitialize();
        }

        private void OnDetectionResultReceived(DetectionResult result)
        {
            CurrentBarcode = result.Barcode;
            CurrentDetectionResult = result.IsOK ? "OK" : "NG";
            OnPropertyChanged(nameof(CurrentBarcode));
            OnPropertyChanged(nameof(CurrentDetectionResult));

            // 在 UI 线程为每个测量项创建正确的 Brush
            foreach (var item in result.GapResults)
                item.ValueColor = new SolidColorBrush(item.IsOK ? Colors.LightGreen : Colors.Red);
            foreach (var item in result.LeftForkResults)
                item.ValueColor = new SolidColorBrush(item.IsOK ? Colors.LightGreen : Colors.Red);
            foreach (var item in result.RightForkResults)
                item.ValueColor = new SolidColorBrush(item.IsOK ? Colors.LightGreen : Colors.Red);

            GapResults = new ObservableCollection<DetectionItem>(result.GapResults);
            LeftForkResults = new ObservableCollection<DetectionItem>(result.LeftForkResults);
            RightForkResults = new ObservableCollection<DetectionItem>(result.RightForkResults);
            OnPropertyChanged(nameof(GapResults));
            OnPropertyChanged(nameof(LeftForkResults));
            OnPropertyChanged(nameof(RightForkResults));

            string category = result.IsOK ? "OK" : string.Join("、", result.DefectTypes);
            UpdateStatistics(result.Barcode, category);

            // 异步保存检测结果，不阻塞 UI
            _ = Task.Run(() => _inspectionResultService.SaveAsync(
                result.Barcode,
                string.Join("、", result.DefectTypes),
                result.IsOK ? "OK" : "NG"));
        }

        private void OnParametersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    foreach (ParameterItem newParam in e.NewItems!)
                    {
                        string defectType = ParamNameToDefectType(newParam.ParameterName);
                        // 避免重复添加
                        if (!DefectDataList.Any(d => d.DefectType == defectType))
                        {
                            var defectData = new DefectDataStatisticsModel { DefectType = defectType, Count = 0, Ratio = 0 };
                            DefectDataList.Add(defectData);
                            DefectSelectedItems.Add(defectData);
                        }
                        // 监听名称变更
                        newParam.PropertyChanged += OnParameterItemPropertyChanged;
                    }
                    break;

                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    foreach (ParameterItem removedParam in e.OldItems!)
                    {
                        string defectType = ParamNameToDefectType(removedParam.ParameterName);
                        var toRemove = DefectDataList.FirstOrDefault(d => d.DefectType == defectType);
                        if (toRemove != null)
                        {
                            DefectDataList.Remove(toRemove);
                            DefectSelectedItems.Remove(toRemove);
                        }
                        // 取消监听
                        removedParam.PropertyChanged -= OnParameterItemPropertyChanged;
                    }
                    break;                
            }

            RefreshStatisticsBinding();
        }


        private void OnParameterItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ParameterItem.ParameterName))
            {
                if (sender is ParameterItem param)
                {
                    // 简单处理：移除旧缺陷类型，添加新缺陷类型
                    // 但需要知道旧名称，这里我们可以在 ParameterItem 中缓存旧名称，或干脆重建整个缺陷列表
                    // 为简化，这里采用全量刷新方式（性能影响很小）
                    RebuildDefectListFromParameters();
                }
            }
        }

        private void RebuildDefectListFromParameters()
        {
            // 保留现有的计数？这取决于业务需求。通常名称变更后原计数可能失去意义，这里选择清零并重建。
            DefectDataList.Clear();
            DefectSelectedItems.Clear();
            foreach (var param in ParametersThresholdRows)
            {
                string defectType = ParamNameToDefectType(param.ParameterName);
                var data = new DefectDataStatisticsModel { DefectType = defectType, Count = 0, Ratio = 0 };
                DefectDataList.Add(data);
                DefectSelectedItems.Add(data);
            }
            RefreshStatisticsBinding();
        }

        private void SyncDefectListFromParameters()
        {
            DefectDataList.Clear();
            foreach (var param in ParametersThresholdRows)
            {
                string defectType = ParamNameToDefectType(param.ParameterName);
                if (!DefectDataList.Any(d => d.DefectType == defectType))
                    DefectDataList.Add(new DefectDataStatisticsModel { DefectType = defectType, Count = 0, Ratio = 0 });
            }
        }

        /// <summary>
        /// 启动或停止监控
        /// </summary>
        /// <returns></returns>
        private async Task ToggleAsync()
        {
            if (_monitor == null) return;
            if (_monitor.IsRunning)
            {
                await _monitorManager.StopMonitorAsync(MonitorId);
            }
            else
            {
                var options = BuildOptions();
                _monitorManager.UpdateOptions(MonitorId, options);
                _configService.UpdateTarget(options);
                await _monitorManager.StartMonitorAsync(MonitorId);
            }
        }

        /// <summary>
        /// 浏览远程路径
        /// </summary>
        /// <returns></returns>
        private async Task BrowseRemotePathAsync()
        {
            var dlg = new OpenFolderDialog { Title = "选择输入文件夹", InitialDirectory = RemotePath };
            if (dlg.ShowDialog(Application.Current.MainWindow) == true)
                RemotePath = dlg.FolderName;
            await Task.CompletedTask;
        }        

        /// <summary>
        /// 清空当前统计数据
        /// </summary>
        /// <returns></returns>
        private async Task ClearDetectionInfoAsync()
        {
            if (MessageBox.Show("确定要清空当前统计数据吗？当前会话数据将累加到历史统计",
                "确认清零", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

            // 累加到历史统计
            TotalDetectionNum = _sessionTotalCount;
            OKDetectionNum = _sessionOkCount;
            NGDetectionNum = _sessionTotalCount - _sessionOkCount;
            ClearTime = DateTime.Now;
            SaveHistoricalStats();

            // 清空列表
            _sessionTotalCount = 0;
            _sessionOkCount = 0;
            TestResultList.Clear();
            foreach (var d in DefectDataList) { d.Count = 0; d.Ratio = 0; }

            RefreshStatisticsBinding();
        }

        
        private async Task QueryAsync()
        {
            IsBusy = true;
            try
            {
                var start = StartDate;
                var end = EndDate;

                // 收集选中类型；全选时传 null（跳过类型过滤，查全部）
                var selected = DefectSelectedItems?.Select(x => x.DefectType!).ToList();
                // 如果全选（即 DefectSelectedItems 包含了 DefectDataList 的所有项），则传 null
                if (selected != null && selected.Count == DefectDataList.Count)
                    selected = null;

                var data = await _inspectionResultService.QueryAsync(start, end, selected);

                // 将 InspectionResult 转换为 InspectionRecord 用于 UI 显示
                _allQueryResults = [.. data.Select(r => new InspectionRecord
                {
                    Time = r.Time,
                    Barcode = r.Barcode,
                    DefectType = r.DefectType
                })];

                // 更新分页信息
                TotalQueryCount = _allQueryResults.Count;
                MaxPageCount = (int)Math.Ceiling(TotalQueryCount / 10.0);
                PageIndex = 1;

                // 显示第一页数据
                ShowResults.Clear();
                var firstPage = _allQueryResults.Take(10).ToList();
                foreach (var item in firstPage)
                    ShowResults.Add(item);
            }
            finally
            {
                IsBusy = false;
            }
        }


        private async Task ExportAsync()
        {
            if (_allQueryResults.Count == 0)
            {
                MessageBox.Show(
                    "当前无数据可导出，请先执行查询。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
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
                // 需要重新查询 InspectionResult 以导出原始数据（含 DetectionResult 等字段）
                var selected = DefectSelectedItems?.Select(x => x.DefectType!).ToList();
                if (selected != null && selected.Count == DefectDataList.Count)
                    selected = null;

                var results = await _inspectionResultService.QueryAsync(StartDate, EndDate, selected);
                await InspectionResultService.ExportCsvAsync(results, dlg.FileName);

                MessageBox.Show(
                    $"已成功导出 {results.Count} 条记录。\n{dlg.FileName}",
                    "导出成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"导出失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }


        private void PageUpdated(FunctionEventArgs<int>? e)
        {
            if (e != null)
            {
                PageIndex = e.Info;
                ShowResults.Clear();
                var pageData = _allQueryResults.Skip((e.Info - 1) * 10).Take(10).ToList();
                foreach (var item in pageData)
                    ShowResults.Add(item);
            }
        }

        /// <summary>
        /// 添加新的参数项到列表
        /// </summary>
        private void AddParameter()
        {
            var newParam = new ParameterItem { ParameterName = "新标准", UpperLimit = 1.0, LowerLimit = 0.0 };
            ParametersThresholdRows.Add(newParam);
        }

        /// <summary>
        /// 删除指定的参数项
        /// </summary>
        /// <param name="item"></param>
        private void DeleteParameter(ParameterItem? item)
        {
            if (item != null) ParametersThresholdRows.Remove(item);
        }

        /// <summary>
        /// 保存当前参数设置到数据库
        /// </summary>
        /// <returns></returns>
        private async Task SaveSettingsAsync()
        {
            SaveParameters();
            _logger.Info("设置保存成功");
            Growl.InfoGlobal("设置保存成功");
        }

        /// <summary>
        /// 打开尺寸标注窗口
        /// </summary>
        /// <returns></returns>
        private async Task OpenAnnotationWindow()
        {
            // 如果窗口已打开，则关闭并重新创建
            _annotationWindow?.Close();
            _annotationWindow = null;

            // 准备要传递的图像
            ImageSource? imageToAnnotate = AnnotationImage;

            if (imageToAnnotate == null)
            {
                MessageBox.Show("请先加载一张图片用于标注。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var annotationVM = new AnnotationWindowViewModel();
            annotationVM.SetImage(imageToAnnotate);

            _annotationWindow = new AnnotationWindow(annotationVM);
            // 监听窗口关闭事件以获取结果（如需要将长度回传给主界面）
            _annotationWindow.Closed += (_, _) =>
            {
                // 这里可以读取 annotationVM.AnnotationTotalLength 并保存到主界面的某个属性
                // 例如：AnnotationTotalLength = annotationVM.AnnotationTotalLength;
                _annotationWindow = null;
            };

            _annotationWindow.ShowDialog();
            await Task.CompletedTask;
        }

        /// <summary>
        /// 加载标注图像
        /// </summary>
        /// <returns></returns>
        private async Task LoadAnnotationImage()
        {
            var initialDir = string.IsNullOrEmpty(AnnotationImagePath)
                                ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                                : Path.GetDirectoryName(AnnotationImagePath);

            // 使用文件打开对话框选择图片
            var dlg = new OpenFileDialog
            {
                Title = "选择标注图片",
                InitialDirectory = initialDir,
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (dlg.ShowDialog(Application.Current.MainWindow) == true)
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(dlg.FileName));
                    AnnotationImage = bitmap;
                    AnnotationImagePath = dlg.FileName;
                }
                catch (Exception ex)
                {
                    _logger.Error($"加载标注图片失败：{ex.Message}");
                }
            }

            await Task.CompletedTask;
        }        

        /// <summary>
        /// 从监控实例加载参数到 ViewModel 属性
        /// </summary>
        private void LoadOptionsFromMonitor()
        {
            if (_monitor == null) return;

            var o = _monitor.Options;
            RemotePath = o.RemotePath;
            OutputDirectory = o.OutputDirectory ?? "";
            Filter = o.Filter;
            IncludeSubdirectories = o.IncludeSubdirectories;
            PollingIntervalSeconds = o.PollingIntervalSeconds;
            PollingOverlapSeconds = o.PollingOverlapSeconds;
        }

        /// <summary>
        /// 根据 ViewModel 属性构建 MonitorTargetOptions 对象
        /// </summary>
        /// <returns></returns>
        private MonitorTargetOptions BuildOptions() => new()
        {
            Id = MonitorId,
            RemotePath = RemotePath,
            OutputDirectory = OutputDirectory,
            Filter = Filter,
            IncludeSubdirectories = IncludeSubdirectories,
            PollingIntervalSeconds = PollingIntervalSeconds,
            PollingOverlapSeconds = PollingOverlapSeconds,
            WatcherBufferSizeBytes = _monitor?.Options.WatcherBufferSizeBytes ?? 65536,
            AutoStart = _monitor?.Options.AutoStart ?? false
        };

        /// <summary>
        /// 刷新运行状态相关的绑定属性
        /// </summary>
        private void RefreshRunningState()
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ToggleButtonText));           
        }

        /// <summary>
        /// 更新预览图像
        /// </summary>
        /// <param name="outputPath"></param>
        private void UpdatePreview(string outputPath)
        {
            ProcessedCount++;
            LastProcessedFileName = Path.GetFileName(outputPath);
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(outputPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                ResultImage = bmp;
            }
            catch (Exception ex) { _logger.Error($"预览加载失败：{ex.Message}"); }            
        }        

        /// <summary>
        /// 更新缺陷统计数据
        /// </summary>
        /// <param name="barcode"></param>
        /// <param name="category"></param>
        private void UpdateStatistics(string barcode, string category)
        {
            bool isOk = category.Equals("OK", StringComparison.OrdinalIgnoreCase);
            _sessionTotalCount++;
            if (isOk) _sessionOkCount++;            

            // 如果是 NG，更新缺陷细分统计
            if (!isOk)
            {
                var defectTypes = category.Split('、', StringSplitOptions.RemoveEmptyEntries);
                foreach (var type in defectTypes)
                {
                    var item = DefectDataList.FirstOrDefault(d => d.DefectType == type);
                    if (item != null) item.Count++;
                    else DefectDataList.Add(new DefectDataStatisticsModel { DefectType = type, Count = 1 });
                }
                foreach (var d in DefectDataList)
                    d.Ratio = _sessionTotalCount == 0 ? 0 : (double)d.Count / _sessionTotalCount;
            }

            var result = new TestResultModel
            {
                Barcode = barcode,
                DefectType = category,
                DetectionResult = isOk ? "OK" : "NG"
            };

            if (TestResultList.Count >= 100) 
                TestResultList.RemoveAt(TestResultList.Count - 1);
            TestResultList.Insert(0, result);

            RefreshStatisticsBinding();
        }

        /// <summary>
        /// 刷新统计相关的绑定属性
        /// </summary>
        private void RefreshStatisticsBinding()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(OkCount));
            OnPropertyChanged(nameof(NgCount));
            OnPropertyChanged(nameof(YieldRate));
            OnPropertyChanged(nameof(DetectionSattisticalData));
            OnPropertyChanged(nameof(DetectionSattisticalLabel));
            OnPropertyChanged(nameof(DetectionStatisticalLabelColor));
        }

        /// <summary>
        /// 加载历史统计数据（总检测数、OK数、NG数、清零时间）
        /// </summary>
        private void LoadHistoricalStats()
        {
            TotalDetectionNum = GetSettingInt("TotalDetectionNum");
            OKDetectionNum = GetSettingInt("OKDetectionNum");
            NGDetectionNum = GetSettingInt("NGDetectionNum");
            ClearTime = Convert.ToDateTime(GetSettingString("ClearTime"));
        }

        /// <summary>
        /// 保存历史统计数据（总检测数、OK数、NG数、清零时间）
        /// </summary>
        private void SaveHistoricalStats()
        {
            SetSettingInt("TotalDetectionNum", TotalDetectionNum, "总检测数量");
            SetSettingInt("OKDetectionNum", OKDetectionNum, "OK检测数量");
            SetSettingInt("NGDetectionNum", NGDetectionNum, "NG检测数量");
            SetSettingString("ClearTime", ClearTime.ToString(), "清除时间");
        }

        /// <summary>
        /// 加载参数标准设置到 ParametersThresholdRows
        /// </summary>
        private void LoadParameters()
        {
            ParametersThresholdRows.Clear();
            var models = _settingRepo.GetList(m => m.BelongToVM == ViewModelKey && m.Remark.StartsWith("JRRY_Param_"));
            if (models == null || models.Count == 0)
            {
                // 数据库中没有参数，使用默认集合
                foreach (var p in GetDefaultParameters())
                    ParametersThresholdRows.Add(p);
                return;
            }

            foreach (var model in models.OrderBy(m => m.Name))
            {
                try
                {
                    var param = System.Text.Json.JsonSerializer.Deserialize<ParameterItem>(model.Value ?? "");
                    if (param != null)
                        ParametersThresholdRows.Add(param);
                }
                catch { /* 忽略损坏数据 */ }
            }
        }


        private List<ParameterItem> GetDefaultParameters()
        {
            return
            [
                new() { ParameterName = "左侧距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "右侧距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "左最小高度距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "右最小高度距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "左最大高度距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "右最大高度距离标准", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "开叉面积", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "开叉角度1", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "开叉角度2", UpperLimit = -1.00, LowerLimit = 10.00 },
                new() { ParameterName = "开叉CIL", UpperLimit = -1.00, LowerLimit = 10.00 }
            ];
        }

        /// <summary>
        /// 保存参数标准设置到数据库
        /// </summary>
        private void SaveParameters()
        {
            var old = _settingRepo.GetList(m => m.BelongToVM == ViewModelKey && m.Remark.StartsWith("JRRY_Param_"));
            foreach (var m in old) _settingRepo.Delete(m);

            for (int i = 0; i < ParametersThresholdRows.Count; i++)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(ParametersThresholdRows[i]);
                _settingRepo.Add(new ProjectSettingModel(ParametersThresholdRows[i].ParameterName, ViewModelKey, json, "string", 0) { Remark = $"JRRY_Param_{i:000}" });
            }
        }

        /// <summary>
        /// 获取整数类型的设置值，如果不存在则返回默认值
        /// </summary>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        private int GetSettingInt(string name, int defaultValue = 0)
        {
            dynamic? val = _settingRepo.GetSettingValue(name, ViewModelKey);
            if (val == null) return defaultValue;
            // 将动态值转为字符串再解析；也可直接 (int)val，但动态转换可能失败，故用 TryParse 保险
            string? strVal = val.ToString();
            return int.TryParse(strVal, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// 设置整数类型的设置值，如果不存在则新增
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="description"></param>
        private void SetSettingInt(string name, int value, string description)
        {
            if (_settingRepo.Exists(m => m.Name == name && m.BelongToVM == ViewModelKey))
                _settingRepo.UpdateSettingValue(name, ViewModelKey, value);
            else
                _settingRepo.Add(new ProjectSettingModel(name, ViewModelKey, value.ToString(), "int", 0) { Remark = description });
        }

        /// <summary>
        /// 获取字符串类型的设置值，如果不存在则返回 null
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string? GetSettingString(string name) =>
            _settingRepo.GetSettingValue(name, ViewModelKey);

        /// <summary>
        /// 设置字符串类型的设置值，如果不存在则新增
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="description"></param>
        private void SetSettingString(string name, string value, string description)
        {
            if (_settingRepo.Exists(m => m.Name == name && m.BelongToVM == ViewModelKey))
                _settingRepo.UpdateSettingValue(name, ViewModelKey, value);
            else
                _settingRepo.Add(new ProjectSettingModel(name, ViewModelKey, value, "string", 0) { Remark = description });
        }

        /// <summary>
        /// 将参数名称转换为缺陷类型名称，例如将 "左侧距离标准" 转换为 "左侧距离不良"
        /// </summary>
        /// <param name="paramName"></param>
        /// <returns></returns>
        private string ParamNameToDefectType(string paramName)
        {
            if (paramName.EndsWith("标准"))
                return string.Concat(paramName.AsSpan(0, paramName.Length - 2), "不良");
            return paramName + "不良";
        }

        // 辅助方法：根据已有的 Brush 创建新的 SolidColorBrush（在 UI 线程）
        private SolidColorBrush CreateBrushFromItem(Brush? original)
        {
            if (original is SolidColorBrush scb)
                return new SolidColorBrush(scb.Color);
            // 默认颜色
            return new SolidColorBrush(Colors.Gray);
        }
    }
}