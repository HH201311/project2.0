using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Component.UserControls;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Project.STC;
using SUNWODA_SEVB.Core.Models.Project.STC.Results;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Core.Models.Web;
using SUNWODA_SEVB.Services.Project;
using SUNWODA_SEVB.Tool.Helper;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;


namespace SUNWODA_SEVB.ViewModels.Pages.STC
{
    [Module("VM_P230ConnectorInstallerPage", "螺丝拧紧曲线")]
    public class VM_P230ConnectorInstallerPage : ViewModelBase
    {
        private readonly ISocketServer _socketServer;
        private readonly ILoggerService<VM_P230ConnectorInstallerPage> _logger;
        private readonly IModuleManager _moduleManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebApiClient _webApiClient; 
        private readonly IScrewPositionConfigService _screwConfigService;
        private readonly IScrewControllerService _screwControllerService;

        // 当前工序的控制器配置
        private ScrewControllerSettings? _screwSettings;
        // 数据库VM名称常量
        private const string VM_NAME = "ConConectorInstaller";

        #region 私有字段
        private bool _initializedView = false;
        private bool _isServerRunning;
        private int _port;
        private int _connectedClientCount;
        private ProcessInfo? _selectedProcess;
        private string _currentPackCode = string.Empty;
        private string _selectedScrewLabel = string.Empty;
        private bool _isLoadingCurveData;
        private ScrewPositionConfig? _selectedScrew;
        private string _screwCode = "001";
        private string? _selectedControllerIP;

        private string? _curveDataDirectory;

        //容器尺寸
        private double _containerWidth;
        private double _containerHeight;
        //图片尺寸
        private int _originalImageWidth;
        private int _originalImageHeight;
        //缩放比例
        private double _currentScale = 1.0;

        #endregion

        #region 集合属性
        /// <summary>
        /// 日志集合
        /// </summary>
        public ObservableCollection<LogEntry> Logs { get; } = new();

        /// <summary>
        /// 已连接客户端集合
        /// </summary>
        public ObservableCollection<ClientInfo> ConnectedClients { get; } = new();

        /// <summary>
        /// 工序列表
        /// </summary>
        public ObservableCollection<ProcessInfo> Processes { get; } = new();

        /// <summary>
        /// OK曲线数据集合
        /// </summary>
        public ObservableCollection<PlotDataSeries> OkPlotDataSeries { get; } = new();

        /// <summary>
        /// NG曲线数据集合
        /// </summary>
        public ObservableCollection<PlotDataSeries> NgPlotDataSeries { get; } = new();

        /// <summary>
        /// 螺丝位置配置集合
        /// </summary>
        public ObservableCollection<ScrewPositionConfig> ScrewPositions { get; } = new();
        /// <summary>
        /// IP 地址列表
        /// </summary>
        private ObservableCollection<ScrewControllerDetail> _controllerList = new();
        #endregion

        #region 绑定属性
        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }
        public bool IsServerRunning
        {
            get => _isServerRunning;
            set => SetProperty(ref _isServerRunning, value);
        }
        public int ConnectedClientCount
        {
            get => _connectedClientCount;
            set => SetProperty(ref _connectedClientCount, value);
        }
        /// <summary>
        /// 当前选中的工序
        /// </summary>
        public ProcessInfo? SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                if (SetProperty(ref _selectedProcess, value))
                {
                    OnSelectedProcessChanged();
                }
            }
        }

        /// <summary>
        /// 当前Pack包编码
        /// </summary>
        public string CurrentPackCode
        {
            get => _currentPackCode;
            set => SetProperty(ref _currentPackCode, value);
        }

        /// <summary>
        /// 当前选中的螺丝标签
        /// </summary>
        public string SelectedScrewLabel
        {
            get => _selectedScrewLabel;
            set => SetProperty(ref _selectedScrewLabel, value);
        }

        public bool IsLoadingCurveData
        {
            get => _isLoadingCurveData;
            set => SetProperty(ref _isLoadingCurveData, value);
        }

        /// <summary>
        /// 当前选中的螺丝
        /// </summary>
        public ScrewPositionConfig? SelectedScrew
        {
            get => _selectedScrew;
            set => SetProperty(ref _selectedScrew, value);
        }
        public string ScrewCode
        {
            get => _screwCode;
            set => SetProperty(ref _screwCode, value);
        }
        /// <summary>
        /// 控制器列表（用于下拉框）
        /// </summary>
        public ObservableCollection<ScrewControllerDetail> ControllerList
        {
            get => _controllerList;
            set => SetProperty(ref _controllerList, value);
        }

        /// <summary>
        /// 当前选中的控制器IP
        /// </summary>
        public string? SelectedControllerIP
        {
            get => _selectedControllerIP;
            set
            {
                if (SetProperty(ref _selectedControllerIP, value))
                {
                    LogInfo($"已选择控制器: {value}");
                }
            }
        }

        #endregion

        #region 命令
        /// <summary>
        /// 启动服务命令
        /// </summary>
        public ICommand StartServerCommand { get;  }

        /// <summary>
        /// 停止服务命令
        /// </summary>
        public ICommand StopServerCommand { get;  }

        /// <summary>
        /// 清空曲线命令
        /// </summary>
        public ICommand ClearChartsCommand { get;  }

        /// <summary>
        /// 模拟测试命令
        /// </summary>
        public ICommand SimulateCommand { get;  }
        /// <summary>
        /// 重置曲线状态命令
        /// </summary>
        public ICommand ResetScrewStatusCommand { get; }
        /// <summary>
        /// 获取曲线数据命令
        /// </summary>
        public ICommand FetchCurveDataCommand { get; }
        /// <summary>
        /// 选择螺丝命令 
        /// </summary>
        public ICommand SelectScrewCommand { get; }
        /// <summary>
        /// 加载CSV文件命令
        /// </summary>
        public ICommand LoadCsvFileCommand { get; }

        #endregion

        public VM_P230ConnectorInstallerPage(
            ILoggerService<VM_P230ConnectorInstallerPage> logger,
            IModuleManager moduleManager,
            ISocketServer socketServer, 
            IServiceProvider serviceProvider,
            IWebApiClient webApiClient,           // 注入现有服务
            IScrewPositionConfigService screwConfigService,
            IScrewControllerService screwControllerService)
        {
            _logger = logger;
            _socketServer = socketServer;
            _moduleManager = moduleManager;
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _webApiClient = webApiClient ?? throw new ArgumentNullException(nameof(webApiClient));
            _screwConfigService = screwConfigService ?? throw new ArgumentNullException(nameof(screwConfigService));
            _screwControllerService = screwControllerService ?? throw new ArgumentNullException(nameof(screwControllerService));

            StartServerCommand = new RelayCommand(async _ => await StartServerAsync(), _ => !IsServerRunning);
            StopServerCommand = new RelayCommand(async _ => await StopServerAsync(), _ => IsServerRunning);
            ClearChartsCommand = new RelayCommand( _ => ClearCharts(), _ => !IsServerRunning);
            SimulateCommand = new RelayCommand( async _ => await SimulateAsync(), _ => !IsServerRunning);
            ResetScrewStatusCommand = new RelayCommand(_ => ResetAllScrewStatus(), _ => true);
            FetchCurveDataCommand = new RelayCommand(async _ => await FetchCurveDataManuallyAsync(), _ => !IsLoadingCurveData);
            LoadCsvFileCommand = new RelayCommand(async _ => await LoadCsvFileAsync(), _ => !IsServerRunning);
            SelectScrewCommand = new RelayCommand<ScrewPositionConfig>(OnScrewSelected);
            // 订阅事件
            _socketServer.ClientConnected += OnClientConnected;
            _socketServer.ClientDisconnected += OnClientDisconnected;
            _socketServer.DataReceived += OnDataReceived;
            _socketServer.ErrorOccurred += OnErrorOccurred;
        }

        #region 初始化
        protected override async Task OnNavigatedToAsync(object? parameter)
        {
            if (!_initializedView)
            {
                await LoadProcessesAsync();//工序配置
                await LoadScrewControllerSettingsAsync();//螺丝枪控制器配置

                _initializedView = true;
            }

            await base.OnNavigatedToAsync(parameter);
        }

        /// <summary>
        /// 从数据库加载工序配置
        /// </summary>
        private async Task LoadProcessesAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                // 获取工序配置JSON
                var processConfigJson = await projectSettingRepo.GetByNameAndVMAsync("ProcessList", "ConConectorInstaller");

                if (!string.IsNullOrEmpty(processConfigJson?.Value))
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    List<ProcessInfo>? processList = JsonSerializer.Deserialize<List<ProcessInfo>>(processConfigJson.Value, options);

                    if (processList is { Count: > 0 })
                    {
                        RunOnUIThread(() =>
                        {
                            Processes.Clear();
                            foreach (var process in processList)
                            {
                                Processes.Add(process);
                            }
                            // 默认选中第一个工序
                            SelectedProcess = Processes.FirstOrDefault();
                        });

                        LogInfo($"成功加载 {processList.Count} 个工序配置");
                        return;
                    }
                }

                LogWarn("数据库中无工序配置，使用默认配置");
                LoadDefaultProcesses();
            }
            catch (Exception ex)
            {
                LogError($"加载工序配置失败: {ex.Message}");
                LoadDefaultProcesses();
            }
        }

        /// <summary>
        /// 加载螺丝控制器配置
        /// </summary>
        private async Task LoadScrewControllerSettingsAsync()
        {
            try
            {
                _screwSettings = await _screwControllerService.LoadSettingsAsync(VM_NAME);

                // 获取所有控制器详细信息
                var controllerDetails = _screwControllerService.GetControllerDetails(_screwSettings);
                LogInfo($"获取到 {controllerDetails.Count} 个控制器详细信息");

                if (controllerDetails.Count > 0)
                {
                    RunOnUIThread(() =>
                    {
                        ControllerList.Clear();
                        foreach (var detail in controllerDetails)
                        {
                            ControllerList.Add(detail);
                            LogInfo($"控制器配置 - 名称: {detail.Name}, IP: {detail.IP}");//, 来源: {detail.Source}
                        }

                        // 默认选中第一个控制器
                        SelectedControllerIP = ControllerList.FirstOrDefault()?.IP;
                    });

                    var allIPs = _screwControllerService.GetAllControllerIPs(_screwSettings);
                    LogInfo($"螺丝控制器配置已加载 - 共{controllerDetails.Count}个控制器, IP列表: {string.Join(", ", allIPs)}");
                }
                else
                {
                    LogWarn("螺丝控制器配置加载成功，但未找到有效的控制器配置");
                    _screwSettings = new ScrewControllerSettings(); // 使用默认配置
                }

                //// 获取所有控制器IP
                //var allIPs = _screwControllerService.GetAllControllerIPs(_screwSettings);
                //var ipList = string.Join(", ", allIPs);

                //LogInfo($"螺丝控制器配置已加载 - IPs: {ipList}");
            }
            catch (Exception ex)
            {
                LogError($"加载螺丝控制器配置失败: {ex.Message}");
                _screwSettings = new ScrewControllerSettings(); // 使用默认配置
            }
        }
        /// <summary>
        /// 加载默认工序配置
        /// </summary>
        private void LoadDefaultProcesses()
        {
            RunOnUIThread(() =>
            {
                Processes.Clear();
                Processes.Add(new ProcessInfo
                {
                    Id = 1,
                    ProcessCode = "P230-01",
                    ProcessName = "上盖螺丝拧紧",
                    ImagePath = "D:\\sunwoda2.0.0.1\\SUNWODA_SEVB\\Assets\\Images\\shanggai.png",
                    ScrewCount = 64
                });
                Processes.Add(new ProcessInfo
                {
                    Id = 2,
                    ProcessCode = "P230-02",
                    ProcessName = "螺丝复拧",
                    ImagePath = "D:\\sunwoda2.0.0.1\\SUNWODA_SEVB\\Assets\\Images\\shanggai.png",
                    ScrewCount = 6
                });

                SelectedProcess = Processes.FirstOrDefault();
            });
        }
        /// <summary>
        /// 工序选择变化处理
        /// </summary>
        //private void OnSelectedProcessChanged()
        //{
        //    if (SelectedProcess != null)
        //    {
        //        LogInfo($"切换到工序: {SelectedProcess.ProcessName}");
        //        //LogInfo($"图片路径: {SelectedProcess.ImagePath}");
        //        LoadDefaultScrewPositions();
        //    }
        //}

        /// <summary>
        /// 加载默认螺丝位置配置
        /// </summary>
        private void LoadDefaultScrewPositions()
        {
            // 设置默认图片尺寸
            _originalImageWidth = 2752;
            _originalImageHeight = 1536;

            RunOnUIThread(() =>
            {
                ScrewPositions.Clear();
                var screwCount = SelectedProcess?.ScrewCount ?? 64;
                var positions = GenerateGridScrewPositions(screwCount);

                foreach (var pos in positions)
                {
                    ScrewPositions.Add(pos);
                }
            });

            // 重新计算坐标
            RecalculateScrewCanvasPositions();
        }

        /// <summary>
        /// 螺丝选中处理
        /// </summary>
        private void OnScrewSelected(ScrewPositionConfig? screw)
        {
            if (screw == null) return;

            // 更新选中状态
            SelectedScrew = screw;
            SelectedScrewLabel = screw.DisplayLabel;

            // 记录日志
            LogInfo($"选中螺丝: {screw.DisplayLabel}, " +
                    $"状态: {screw.Status}");

            // 如果有扭矩/角度值，显示详情
            if (screw.TorqueValue.HasValue)
            {
                LogInfo($"  扭矩: {screw.TorqueValue:F2} N·m" +
                        (screw.AngleValue.HasValue ? $", 角度: {screw.AngleValue:F1}°" : ""));
            }
        }







        #endregion














        #region Socket服务
        private async Task InitializePortAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();
                var dataPort = await projectSettingRepo.GetByNameAndVMAsync("Port", "ConConectorInstaller");
                if (int.TryParse(dataPort?.Value, out int portFromDb) && portFromDb > 0)
                {
                    Port = portFromDb;
                }
                else
                {
                    Port = 8080;
                    LogInfo($"数据库中无端口配置，使用默认端口: {Port}");
                }

                var csvPath = await projectSettingRepo.GetByNameAndVMAsync("CsvPath", "ConConectorInstaller");
                if(string.IsNullOrEmpty(csvPath?.Value))
                {
                    LogWarn("数据库中无CSV路径配置，使用默认路径: ./CsvData");
                    _curveDataDirectory = Path.Combine(AppContext.BaseDirectory, "CSVData");
                }
                else
                {
                    _curveDataDirectory = csvPath.Value;
                    LogInfo($"CSV路径配置: {_curveDataDirectory}");
                }
            }
            catch (Exception ex)
            {
                Port = 8080;
                LogError($"获取端口配置失败，使用默认端口 {Port}. 异常: {ex.Message}");
            }
        }

        private async Task StartServerAsync()
        {
            if (IsServerRunning) return;
            await InitializePortAsync();

            var success = await _socketServer.StartAsync(0, "ConConectorInstaller",null);
            if (success)
            {
                IsServerRunning = true;
                LogInfo($"服务启动成功，端口: {Port}");
            }
            else
            {           
                LogError("服务启动失败");
            }
        }
        private async Task StopServerAsync()
        {
            if (!IsServerRunning) return;

            // 临时取消错误事件订阅
            _socketServer.ErrorOccurred -= OnErrorOccurred;

            try
            {
                await _socketServer.StopAsync();
            }
            finally
            {
                // 重新订阅
                _socketServer.ErrorOccurred += OnErrorOccurred;
            }

            IsServerRunning = false;
            ConnectedClients.Clear();
            LogInfo("服务已停止");
        }
        private async Task BroadcastMessageAsync(string? message)
        {
            if (string.IsNullOrEmpty(message)) return;

            await _socketServer.BroadcastAsync(message);
            LogInfo($"发送消息: {message}");
        }
        private async Task DisconnectClientAsync(string? endPoint)
        {
            if (string.IsNullOrEmpty(endPoint)) return;

            await _socketServer.DisconnectClientAsync(endPoint);
            LogInfo($"断开客户端: {endPoint}");
        }

        private void OnClientConnected(object? sender, ClientConnectionEventArgs e)
        {
            RunOnUIThread(() =>
            {
                ConnectedClients.Add(new ClientInfo
                {
                    Id = e.ClientId,
                    EndPoint = e.EndPoint,
                    ConnectedTime = e.ConnectionTime
                });
                ConnectedClientCount = _socketServer.ConnectedClientCount;
            });
            LogInfo($"客户端连接: {e.EndPoint}");
        }

        private void OnClientDisconnected(object? sender, ClientConnectionEventArgs e)
        {
            RunOnUIThread(() =>
            {
                var client = ConnectedClients.FirstOrDefault(c => c.Id == e.ClientId);
                if (client != null)
                {
                    ConnectedClients.Remove(client);
                }
                ConnectedClientCount = _socketServer.ConnectedClientCount;
            });
            LogInfo($"客户端断开: {e.EndPoint}");
        }
        //！ 数据接收处理===========================================================
        private void OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            var message = e.GetDataAsString();
            LogInfo($"收到客户端[{e.ClientEndPoint}]数据 : {message}");
            // 解析数据并更新曲线
            ProcessReceivedData(e.ClientEndPoint, message);
        }

        private void OnErrorOccurred(object? sender, ServerErrorEventArgs e)
        {
            LogError($"错误: {e.Message}");
        }
        public async ValueTask DisposeAsync()
        {
            _socketServer.ClientConnected -= OnClientConnected;
            _socketServer.ClientDisconnected -= OnClientDisconnected;
            _socketServer.DataReceived -= OnDataReceived;
            _socketServer.ErrorOccurred -= OnErrorOccurred;

            await _socketServer.DisposeAsync();
        }
        #endregion

        #region 数据处理
        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        private void ProcessReceivedData(string clientEndPoint, string message)
        {
            // 二次确认忽略心跳消息
            if (string.IsNullOrWhiteSpace(message) ||
                message.Equals("Send", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };        
                var data = JsonSerializer.Deserialize<TighteningResult>(message, options);

                if (data?.TestData == null || data.TestData.Count == 0)
                {
                    LogWarn($"端口{clientEndPoint}接收到的数据为空或无测试数据");
                    return;
                }

                // 更新Pack包编码
                RunOnUIThread(() => CurrentPackCode = data.ProductSn ?? CurrentPackCode);

                // 启动异步处理（不阻塞Socket接收）
                _ = ProcessTighteningDataAsync(data);
            }
            catch (JsonException ex)
            {
                LogError($"JSON解析失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogError($"处理数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步处理拧紧数据
        /// </summary>
        private async Task ProcessTighteningDataAsync(TighteningResult data)
        {
            if (data == null)
            {
                LogWarn("接收到的拧紧数据为空");
                return;
            }

            if (_screwSettings == null)
            {
                LogWarn("螺丝控制器配置未加载");
                return;
            }

            // 检查TestData是否为空
            if (data.TestData == null || data.TestData.Count == 0)
            {
                LogWarn($"产品 {data.ProductSn ?? "未知"} 的测试数据为空");
                return;
            }

            try
            {
                IsLoadingCurveData = true;
                // 清理
                BeginCollectCurveData();

                // Step 1: 识别所有螺丝枪并按枪分组
                var tightenGunGroups = IdentifyTightenGuns(data.TestData);
                if (tightenGunGroups.Count == 0)
                {
                    LogWarn("未识别到有效的螺丝枪数据");
                    return;
                }
                LogInfo($"识别到 {tightenGunGroups.Count} 把螺丝枪: {string.Join(", ", tightenGunGroups.Keys)}");

                // Step 2: 批量获取所有螺丝枪的TR值
                var trValues = await GetTrValuesForGunsAsync(tightenGunGroups.Keys);
                if (trValues.Count == 0)
                {
                    LogWarn("Tr值获取失败，请检查！");
                    return;
                }
                // Step 3: 按 paramSort 分组处理
                var groupedBySort = data.TestData
                    .GroupBy(p => p.ParamSort)
                    .OrderBy(g => int.TryParse(g.Key, out var sortNum) ? sortNum : 0)
                    .ToList();
                if (groupedBySort.Count == 0)
                {
                    LogWarn("按螺丝分组后无有效数据");
                    return;
                }

                // Step 4: 处理每个螺丝
                var processTasks = new List<Task>();
                var semaphore = new SemaphoreSlim(1); // 限制并发数为1

                foreach (var group in groupedBySort)
                {
                    // 使用信号量控制并发
                    await semaphore.WaitAsync();

                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessSingleScrewAsync(group, data.ProductSn, trValues);
                            await Task.Delay(10); // 避免过快处理
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    processTasks.Add(task);
                }

                // 等待所有任务完成
                await Task.WhenAll(processTasks);

                if (!string.IsNullOrEmpty(data.ProductSn))
                {
                    await SaveProductCurveDataAsync(data.ProductSn);
                }

                // 记录整体结果
                var resultText = data.IsOk ? "OK" : "NG";
                var screwCount = groupedBySort.Count;
                var okCount = groupedBySort.Count(g => g.All(p => p?.IsOk == true));

                LogInfo($"数据处理完成 - 产品: {data.ProductSn}, 结果: {resultText}, " +
                        $"螺丝数: {screwCount}, OK: {okCount}, NG: {screwCount - okCount}");
            }
            catch (Exception ex)
            {
                LogError($"处理拧紧数据异常: {ex.Message}");
            }
            finally
            {
                IsLoadingCurveData = false;
            }
        }

        /// <summary>
        /// 识别并按螺丝枪分组
        /// </summary>
        private Dictionary<string, List<TighteningParam>> IdentifyTightenGuns(List<TighteningParam> testData)
        {
            var result = new Dictionary<string, List<TighteningParam>>(StringComparer.OrdinalIgnoreCase);
            // 空值检查
            if (testData == null || testData.Count == 0)
            {
                return result;
            }

            foreach (var param in testData)
            {
                if (param == null)
                    continue;
                // 只处理扭矩参数（因为角度参数的TightenGun可能为空）
                if (param.ParamUnit?.Equals("N.M", StringComparison.OrdinalIgnoreCase) != true)
                    continue;

                var gun = param.TightenGun;
                if (string.IsNullOrEmpty(gun))
                {
                    gun = "t1"; // 未指定枪的使用默认\
                    LogWarn($"参数 {param.ParamName} 未指定螺丝枪，使用默认枪 t1");
                }

                if (!result.TryGetValue(gun, out var list))
                {
                    list = new List<TighteningParam>();
                    result[gun] = list;
                }
                list.Add(param);
            }

            return result;
        }

        /// <summary>
        /// 批量获取螺丝枪的TR值
        /// </summary>
        private async Task<Dictionary<string, float>> GetTrValuesForGunsAsync(IEnumerable<string> tightenGuns)
        {
            var trValues = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            if (tightenGuns == null || !tightenGuns.Any())
            {
                return trValues;
            }
            if (_screwSettings == null)
            {
                LogWarn("螺丝控制器配置为空，无法获取TR值");
                return trValues;
            }

            // 按控制器IP分组
            var gunsByController = new Dictionary<string, List<string>>();

            foreach (var gun in tightenGuns)
            {
                var controllerIP = _screwControllerService.GetControllerIPByTightenGun(_screwSettings, gun);
                if (string.IsNullOrEmpty(controllerIP))
                {
                    controllerIP = _screwSettings.ControllerIP ??
                                   _screwSettings.ControllerIPs.FirstOrDefault() ??
                                   "unknown";
                    LogWarn($"螺丝枪 {gun} 未找到IP映射，使用默认IP: {controllerIP}");
                }
                else
                {
                    LogInfo($"螺丝枪 {gun} → 控制器IP: {controllerIP}");
                }

                if (!gunsByController.TryGetValue(controllerIP, out var gunList))
                {
                    gunList = new List<string>();
                    gunsByController[controllerIP] = gunList;
                }
                gunList.Add(gun);
            }

            // 每个控制器只请求一次TR值
            foreach (var (controllerIP, guns) in gunsByController)
            {
                if (string.IsNullOrEmpty(controllerIP) || controllerIP == "unknown")
                {
                    LogWarn($"螺丝枪 [{string.Join(", ", guns)}] 无法确定控制器IP，使用默认TR值1.0");
                    foreach (var gun in guns)
                    {
                        trValues[gun] = 1.0f;
                    }
                    continue;
                }

                try
                {
                    var trResult = await _screwControllerService.GetTrValueAsync(controllerIP, VM_NAME);

                    if (trResult.IsSuccess)
                    {
                        foreach (var gun in guns)
                        {
                            trValues[gun] = trResult.TrValue;
                        }
                        var cacheInfo = trResult.FromCache ? " (缓存)" : "";
                        LogInfo($"控制器 {controllerIP} TR值: {trResult.TrValue:F4}{cacheInfo}");
                    }
                    else
                    {
                        LogWarn($"获取控制器 {controllerIP} TR值失败: {trResult.Message}，使用默认值1.0");
                        foreach (var gun in guns)
                        {
                            trValues[gun] = 1.0f;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"获取控制器 {controllerIP} TR值异常: {ex.Message}，使用默认值1.0");
                    foreach (var gun in guns)
                    {
                        trValues[gun] = 1.0f;
                    }
                }
            }

            return trValues;
        }

        /// <summary>
        /// 处理单个螺丝的数据
        /// </summary>
        private async Task ProcessSingleScrewAsync(
            IGrouping<string?, TighteningParam> group,
            string? productSn,
            Dictionary<string, float> trValues)
        {

            if (group == null || !group.Any())
            {
                return;
            }

            trValues ??= new Dictionary<string, float>();

            var torqueParam = group.FirstOrDefault(p =>
                p != null && string.Equals(p.ParamUnit, "N.M", StringComparison.OrdinalIgnoreCase));

            var angleParam = group.FirstOrDefault(p =>
                p != null && string.Equals(p.ParamUnit, "°", StringComparison.OrdinalIgnoreCase));

            if (torqueParam == null)
            {
                LogWarn($"螺丝组 {group.Key} 未找到扭矩参数");
                return;
            }

            var sortIndex = int.TryParse(group.Key, out var idx) ? idx : 0;
            var isItemOk = group.All(p => p?.IsOk == true);
            var screwLabel = $"螺丝 #{sortIndex}";

            try
            {
                // 更新螺丝位置状态
                UpdateScrewStatus(sortIndex, isItemOk ? ScrewStatus.OK : ScrewStatus.NG,
                    torqueParam.NumericValue, angleParam?.NumericValue);

                RunOnUIThread(() => SelectedScrewLabel = screwLabel);

                // 获取详细曲线数据
                if (_screwSettings?.EnableCurveDataFetch == true &&
                    !string.IsNullOrEmpty(torqueParam.WorkCycleId))
                {
                    await FetchAndAddCurveDataAsync(
                        torqueParam,
                        angleParam,
                        screwLabel,
                        productSn,
                        trValues,
                        isItemOk);
                }
            }
            catch (Exception ex)
            {
                LogError($"处理螺丝 {screwLabel} 数据异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取并添加曲线数据
        /// </summary>
        private async Task FetchAndAddCurveDataAsync(
            TighteningParam torqueParam,
            TighteningParam? angleParam,
            string screwLabel,
            string? productSn,
            Dictionary<string, float> trValues,
            bool isOk)
        {
            if (torqueParam == null)
            {
                LogWarn($"螺丝 {screwLabel} 扭矩参数为空");
                return;
            }

            if (_screwSettings == null)
            {
                LogWarn($"螺丝 {screwLabel} 控制器配置未加载");
                return;
            }

            trValues ??= new Dictionary<string, float>();

            try
            {
                // 确定控制器IP
                var tightenGun = string.IsNullOrWhiteSpace(torqueParam.TightenGun)? "default": torqueParam.TightenGun;
                var controllerIP = _screwControllerService.GetControllerIPByTightenGun( _screwSettings, tightenGun);

                // ⬅️ 添加映射结果日志
                var mappingSource = "配置映射";
                if (string.IsNullOrEmpty(controllerIP))
                {
                    controllerIP = _screwSettings.ControllerIP ??
                                   _screwSettings.ControllerIPs.FirstOrDefault();
                    mappingSource = "默认回退";
                }

                if (string.IsNullOrEmpty(controllerIP))
                {
                    LogWarn($"螺丝 {screwLabel} 无法确定控制器IP (螺丝枪: {tightenGun})");
                    return;
                }

                LogInfo($"{screwLabel} - 螺丝枪: {tightenGun}, 控制器IP: {controllerIP} ({mappingSource})");
                // 获取TR值
                var trValue = trValues.TryGetValue(tightenGun, out var tr) ? tr : 1.0f;

                // 解析workCycleId
                if (string.IsNullOrWhiteSpace(torqueParam.WorkCycleId) || !int.TryParse(torqueParam.WorkCycleId, out int workCycleId) || workCycleId <= 0)
                {
                    LogWarn($"螺丝 {screwLabel} workCycleId无效: {torqueParam.WorkCycleId ?? "null"}");
                    return;
                }

                // Step 1: 根据workCycleId(eorCycleCount)获取eorProcessCycleNumber
                var processCycleResult = await _screwControllerService.GetProcessCycleNumberByCycleCountAsync(controllerIP, workCycleId);

                //if (!processCycleResult.IsSuccess || processCycleResult.ProcessCycleNumber <= 0)
                //{
                //    LogError($"螺丝 {screwLabel} 获取ProcessCycleNumber失败: {processCycleResult.Message}");
                //    HandyControl.Controls.Growl.Error($"螺丝 {screwLabel} 获取ProcessCycleNumber失败: {processCycleResult.Message}");
                //    return;
                //}

                //var traceLogId = processCycleResult.ProcessCycleNumber;
                if (!processCycleResult.IsSuccess || processCycleResult.DocId <= 0)
                {
                    LogError($"螺丝 {screwLabel} 获取ProcessCycleNumber失败: {processCycleResult.Message}");
                    HandyControl.Controls.Growl.Error($"螺丝 {screwLabel} 获取ProcessCycleNumber失败: {processCycleResult.Message}");
                    return;
                }

                var traceLogId = processCycleResult.DocId;


                LogInfo($"{screwLabel} - workCycleId: {workCycleId} → traceLogId: {traceLogId}");

                // Step 2: 使用traceLogId获取曲线数据
                ScrewCurveDataResult? curveResult = null;
                var maxRetries = _screwSettings.RetryCount;
                var retryInterval = _screwSettings.RetryIntervalMs;

                for (int retry = 0; retry <= maxRetries; retry++)
                {
                    curveResult = await _screwControllerService.GetCurveDataAsync(controllerIP, traceLogId, trValue, VM_NAME);

                    if (curveResult?.IsSuccess == true && curveResult.DataPoints?.Count > 0)
                        break;

                    if (retry < maxRetries)
                    {
                        LogInfo($"{screwLabel} 曲线获取第{retry + 1}次失败，{retryInterval}ms后重试...");
                        await Task.Delay(retryInterval);
                    }
                }

                // Step 3: 添加曲线到集合
                if (curveResult?.IsSuccess == true && curveResult.DataPoints?.Count > 0)
                {
                    var series = CreatePlotSeriesFromCurveData(screwLabel, curveResult);
                    AddToPlotCollection(series, isOk);

                    //添加曲线数据到保存集合
                    var screwIndex = ExtractScrewIndex(screwLabel);
                    AddScrewCurveData(productSn ?? string.Empty,screwIndex,screwLabel, curveResult, tightenGun, isOk, torqueParam, angleParam);

                    var maxTorque = curveResult.DataPoints.Max(p => p.Torque);
                    var finalAngle = curveResult.DataPoints.Last().Angle;
                    LogInfo($"✓ {screwLabel} 曲线获取成功 - 数据点: {curveResult.DataPoints.Count}, " +
                           $"最大扭矩: {maxTorque:F3} N·m, 最终角度: {finalAngle:F1}°");
                }
                else
                {
                    LogWarn($"✗ {screwLabel} 曲线获取失败: {curveResult?.Message ?? "未知错误"}");
                }
            }
            catch (Exception ex)
            {
                LogError($"获取{screwLabel} 曲线异常: {ex.Message}");
            }
        }




        /// <summary>
        /// 使用现有 WebApiClient 获取详细曲线数据
        /// </summary>
        private async Task FetchDetailedCurveDataAsync(List<ScrewCurveRequest> requests)
        {
            if (_screwSettings == null)
            {
                LogWarn("螺丝控制器配置未加载，无法获取曲线数据");
                return;
            }

            if (!_screwSettings.EnableCurveDataFetch)
            {
                LogInfo("曲线数据获取功能已禁用");
                return;
            }

            if (requests == null || requests.Count == 0)
            {
                return;
            }

            try
            {
                IsLoadingCurveData = true;
                LogInfo($"开始获取 {requests.Count} 个螺丝的详细曲线数据...");

                // 预先批量获取所有需要的TR值
                var tightenGuns = requests
                    .Where(r => !string.IsNullOrEmpty(r.TightenGun))
                    .Select(r => r.TightenGun!)
                    .Distinct();

                var batchTrResult = await _screwControllerService.GetBatchTrValuesAsync(
                    _screwSettings, tightenGuns);

                if (!batchTrResult.IsSuccess)
                {
                    LogWarn($"批量获取TR值失败: {batchTrResult.Message}");
                }

                int successCount = 0;
                int failCount = 0;

                foreach (var request in requests)
                {
                    try
                    {
                        // 确定控制器地址（优先使用请求中指定的，否则使用配置）
                        var controllerIP = !string.IsNullOrEmpty(request.ControllerIP)
                              ? request.ControllerIP
                              : _screwControllerService.GetControllerIPByTightenGun(
                                  _screwSettings, request.TightenGun ?? string.Empty);

                        if (string.IsNullOrEmpty(controllerIP))
                        {
                            LogWarn($"螺丝 {request.ScrewLabel} 无法确定控制器IP");
                            failCount++;
                            continue;
                        }

                        // 1. 获取TR值
                        float trValue = 1.0f;
                        if (!string.IsNullOrEmpty(request.TightenGun))
                        {
                            trValue = batchTrResult.GetTrValue(request.TightenGun, 1.0f);
                        }
                        else
                        {
                            var trResult = await _screwControllerService.GetTrValueAsync(controllerIP);
                            if (trResult.IsSuccess)
                            {
                                trValue = trResult.TrValue;
                            }
                        }

                        // 2. 获取TraceLogId
                        int traceLogId = await ResolveTraceLogIdAsync(
                            request, controllerIP);
                        if (traceLogId <= 0)
                        {
                            LogWarn($"螺丝 {request.ScrewLabel} 无法确定TraceLogId");
                            continue;
                        }

                        // 3. 获取曲线数据（带重试）
                        ScrewCurveDataResult? curveResult = null;
                        for (int retry = 0; retry <= _screwSettings.RetryCount; retry++)
                        {
                            curveResult = await _screwControllerService.GetCurveDataAsync(
                                controllerIP, traceLogId, trValue);

                            if (curveResult.IsSuccess && curveResult.DataPoints.Count > 0)
                                break;

                            if (retry < _screwSettings.RetryCount)
                            {
                                await Task.Delay(_screwSettings.RetryIntervalMs);
                            }
                        }

                        if (curveResult?.IsSuccess == true && curveResult.DataPoints.Count > 0)
                        {
                            var series = CreatePlotSeriesFromCurveData(
                                request.ScrewLabel ?? $"螺丝 #{request.ParamSort}",
                                curveResult);
                            AddToPlotCollection(series, true);
                            successCount++;
                        }
                        else
                        {
                            LogWarn($"螺丝 {request.ScrewLabel} 曲线获取失败: {curveResult?.Message}");
                            failCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"获取螺丝 {request.ScrewLabel} 曲线异常: {ex.Message}");
                    }
                }

                LogInfo($"曲线数据获取完成，成功: {successCount}/{requests.Count}");
            }
            catch (Exception ex)
            {
                LogError($"获取详细曲线数据失败: {ex.Message}");
            }
            finally
            {
                IsLoadingCurveData = false;
            }
        }

        /// <summary>
        /// 解析TraceLogId
        /// </summary>
        private async Task<int> ResolveTraceLogIdAsync(
            ScrewCurveRequest request, string controllerIP)
        {
            // 如果请求中直接包含TraceLogId
            if (!string.IsNullOrEmpty(request.workCycleId) &&
               int.TryParse(request.workCycleId, out int workCycleId) &&
               workCycleId > 0)
            {
                return workCycleId;
            }

            // 通过EorCycleCount查询
            if (!string.IsNullOrEmpty(request.EorCycleCount) &&
                int.TryParse(request.EorCycleCount, out int cycleCount))
            {
                var cycleResult = await _screwControllerService.GetProcessCycleInfoAsync(
                    controllerIP, cycleCount);
                if (cycleResult.IsSuccess)
                {
                    return cycleResult.Id;
                }
            }

            return 0;
        }

        /// <summary>
        /// 从曲线数据创建绘图系列
        /// </summary>
        private PlotDataSeries CreatePlotSeriesFromCurveData(
            string screwLabel, ScrewCurveDataResult curveData)
        {
            var label = screwLabel ?? "未知螺丝";
            var dataPoints = new ObservableCollection<PlotDataPoint>();

            if (curveData?.DataPoints != null && curveData.DataPoints.Count > 0)
            {
                int index = 1;
                foreach (var point in curveData.DataPoints)
                {
                    dataPoints.Add(new PlotDataPoint(index, point.Torque));
                    index++;
                }
            }

            return new PlotDataSeries
            {
                //Name = $"{screwLabel} (曲线) - {DateTime.Now:HH:mm:ss}",
                Name = $"{screwLabel} (曲线)",
                DataPoints = dataPoints,
                LineWidth = 1.5f
            };
        }

        #region 手动获取曲线数据
        private readonly SemaphoreSlim _curveDataSemaphore = new(1, 1);
        /// <summary>
        /// 手动获取曲线数据
        /// </summary>     
        private async Task FetchCurveDataManuallyAsync()
        {
            // 1. 并发控制
            if (!await _curveDataSemaphore.WaitAsync(0))
            {
                LogWarn("正在获取曲线数据，请稍候...");
                return;
            }

            try
            {
                // 2. 前置条件检查
                if (SelectedScrew == null)
                {
                    LogWarn("请先选择一个螺丝位置");
                    return;
                }

                if (_screwSettings == null)
                {
                    LogWarn("螺丝控制器配置未加载，请检查配置");
                    return;
                }

                // 3. 检查是否选择了控制器IP
                if (string.IsNullOrEmpty(SelectedControllerIP))
                {
                    LogWarn("请先选择控制器IP");
                    return;
                }

                IsLoadingCurveData = true;

                // 4. 使用手动选择模式确定控制器IP
                var controllerIP = DetermineControllerIP(SelectedScrew, useManualSelection: true);
                if (string.IsNullOrEmpty(controllerIP))
                {
                    LogWarn($"无法确定螺丝 {SelectedScrew.DisplayLabel} 对应的控制器IP");
                    return;
                }

                LogInfo($"手动获取曲线数据 - 螺丝: {SelectedScrew.DisplayLabel}, " +
                       $"螺丝枪: {SelectedScrew.TightenGun ?? "N/A"}, " +
                       $"控制器IP: {controllerIP} (手动选择)");

                // 5. 获取TR值
                var trResult = await GetTrValueWithLogging(controllerIP);
                if (!trResult.IsSuccess)
                {
                    return;
                }

                // 6. 解析TraceLogId
                if (!TryParseTraceLogId(out int traceLogId))
                {
                    return;
                }

                // 7. 获取曲线数据
                var curveResult = await FetchCurveWithRetry(controllerIP, traceLogId, trResult.TrValue);

                // 8. 处理结果
                ProcessCurveResult(curveResult);
            }
            catch (Exception ex)
            {
                LogError($"手动获取曲线异常: {ex.Message}");
            }
            finally
            {
                IsLoadingCurveData = false;
                _curveDataSemaphore.Release();
            }
        }

        /// <summary>
        /// 确定控制器IP
        /// </summary>
        private string? DetermineControllerIP(ScrewPositionConfig screw, bool useManualSelection = false)
        {
            if (_screwSettings == null) return null;

            // 手动模式：优先使用下拉框选择的IP
            if (useManualSelection && !string.IsNullOrEmpty(SelectedControllerIP))
            {
                return SelectedControllerIP;
            }

            // 自动模式：根据螺丝枪映射获取
            if (!string.IsNullOrEmpty(screw.TightenGun))
            {
                var mappedIP = _screwControllerService.GetControllerIPByTightenGun(
                    _screwSettings, screw.TightenGun);
                if (!string.IsNullOrEmpty(mappedIP))
                {
                    return mappedIP;
                }
            }

            // 回退到默认IP
            return SelectedControllerIP ?? _screwSettings.ControllerIP ?? _screwSettings.ControllerIPs.FirstOrDefault();
        }

        /// <summary>
        /// 获取TR值（带日志）
        /// </summary>
        private async Task<ScrewTrValueResult> GetTrValueWithLogging(string controllerIP)
        {
            var trResult = await _screwControllerService.GetTrValueAsync(controllerIP, VM_NAME);

            if (trResult.IsSuccess)
            {
                var cacheInfo = trResult.FromCache ? $" (缓存于 {trResult.CachedTime:HH:mm:ss})" : "";
                LogInfo($"TR值: {trResult.TrValue:F4}{cacheInfo}");
            }
            else
            {
                LogWarn($"获取TR值失败: {trResult.Message}");
            }

            return trResult;
        }

        /// <summary>
        /// 解析TraceLogId
        /// </summary>
        private bool TryParseTraceLogId(out int traceLogId)
        {
            traceLogId = 0;

            if (string.IsNullOrWhiteSpace(ScrewCode))
            {
                LogWarn("请输入TraceLogId");
                return false;
            }

            if (!int.TryParse(ScrewCode.Trim(), out traceLogId))
            {
                LogWarn($"TraceLogId格式错误: '{ScrewCode}'，请输入有效的整数");
                return false;
            }

            if (traceLogId <= 0)
            {
                LogWarn($"TraceLogId必须为正整数，当前值: {traceLogId}");
                return false;
            }

            LogInfo($"TraceLogId: {traceLogId}");
            return true;
        }

        /// <summary>
        /// 获取曲线数据（带重试）
        /// </summary>
        private async Task<ScrewCurveDataResult> FetchCurveWithRetry(string controllerIP, int traceLogId, float trValue)
        {
            var maxRetries = _screwSettings?.RetryCount ?? 3;
            var retryInterval = _screwSettings?.RetryIntervalMs ?? 1000;

            ScrewCurveDataResult? result = null;

            for (int retry = 0; retry <= maxRetries; retry++)
            {
                result = await _screwControllerService.GetCurveDataAsync(controllerIP, traceLogId, trValue, VM_NAME);

                if (result.IsSuccess && result.DataPoints.Count > 0)
                {
                    return result;
                }

                if (retry < maxRetries)
                {
                    LogInfo($"第 {retry + 1} 次获取失败: {result?.Message}，{retryInterval}ms 后重试...");
                    await Task.Delay(retryInterval);
                }
            }

            return result ?? new ScrewCurveDataResult { Message = "获取失败，已达最大重试次数" };
        }

        /// <summary>
        /// 处理曲线获取结果
        /// </summary>
        private void ProcessCurveResult(ScrewCurveDataResult curveResult)
        {
            if (SelectedScrew == null) return;

            if (curveResult.IsSuccess && curveResult.DataPoints.Count > 0)
            {
                // 创建曲线系列
                var series = CreatePlotSeriesFromCurveData(SelectedScrew.DisplayLabel, curveResult);

                // 添加到对应集合
                var isOk = SelectedScrew.Status != ScrewStatus.NG;
                AddToPlotCollection(series, isOk);

                // 日志输出统计信息
                LogInfo($"✓ 曲线获取成功，数据点: {curveResult.DataPoints.Count}");

                var maxTorque = curveResult.DataPoints.Max(p => p.Torque);
                var finalAngle = curveResult.DataPoints.Last().Angle;
                LogInfo($"  最大扭矩: {maxTorque:F3} N·m, 最终角度: {finalAngle:F1}°");
            }
            else
            {
                LogWarn($"✗ 曲线获取失败: {curveResult.Message}");
            }
        }
        #endregion
        /// <summary>
        /// 更新螺丝位置状态
        /// </summary>
        private void UpdateScrewStatus(int screwIndex, ScrewStatus status, double? torque = null, double? angle = null)
        {
            RunOnUIThread(() =>
            {
                var screwPos = ScrewPositions.FirstOrDefault(s => s.Index == screwIndex);
                if (screwPos != null)
                {
                    screwPos.Status = status;
                    screwPos.TorqueValue = torque;
                    screwPos.AngleValue = angle;
                }
            });
        }
        /// <summary>
        /// 重置所有螺丝状态
        /// </summary>
        private void ResetAllScrewStatus()
        {
            RunOnUIThread(() =>
            {
                foreach (var screw in ScrewPositions)
                {
                    screw.Status = ScrewStatus.Pending;
                    screw.TorqueValue = null;
                    screw.AngleValue = null;
                }
            });
            LogInfo("已重置所有螺丝状态");
        }

        /// <summary>
        /// 创建基础曲线数据（最终值）
        /// </summary>
        private PlotDataSeries CreateBasicPlotSeries(string screwLabel, TighteningParam torque, TighteningParam? angle)
        {
            var dataPoints = new ObservableCollection<PlotDataPoint>();

            if (double.TryParse(torque.ParamFirstLower, out var lower) &&
                double.TryParse(torque.ParamFirstUpper, out var upper))
            {
                dataPoints.Add(new PlotDataPoint(0, lower));
                dataPoints.Add(new PlotDataPoint(1, torque.NumericValue));
                dataPoints.Add(new PlotDataPoint(2, upper));
            }
            else
            {
                dataPoints.Add(new PlotDataPoint(0, torque.NumericValue));
            }

            var angleInfo = angle != null ? $", {angle.ParamValue}{angle.ParamUnit}" : "";
            return new PlotDataSeries
            {
                Name = $"{screwLabel} ({torque.ParamValue}{torque.ParamUnit}{angleInfo}) - {DateTime.Now:HH:mm:ss}",
                DataPoints = dataPoints,
                LineWidth = 2
            };
        }
        /// <summary>
        /// 创建详细曲线数据
        /// </summary>
        private PlotDataSeries CreateDetailedPlotSeries(ScrewCurveResponse curveResult)
        {
            var dataPoints = new ObservableCollection<PlotDataPoint>();

            if (curveResult.Angles != null && curveResult.Torques != null)
            {
                for (int i = 0; i < curveResult.Angles.Length && i < curveResult.Torques.Length; i++)
                {
                    dataPoints.Add(new PlotDataPoint(curveResult.Angles[i], curveResult.Torques[i]));
                }
            }

            return new PlotDataSeries
            {
                Name = $"{curveResult.ScrewLabel} (详细) - {curveResult.CollectionTime:HH:mm:ss}",
                DataPoints = dataPoints,
                LineWidth = (float)1.5
            };
        }
        /// <summary>
        /// 添加到曲线集合
        /// </summary>
        private void AddToPlotCollection(PlotDataSeries series, bool isOk)
        {
            RunOnUIThread(() =>
            {
                var collection = isOk ? OkPlotDataSeries : NgPlotDataSeries;
                collection.Add(series);

                //var screwCount = SelectedProcess?.ScrewCount ?? 64;
                //var maxCount = Math.Clamp(screwCount * 2, 20, 200);
                var maxCount = 150;

                while (collection.Count > maxCount)
                {
                    collection.RemoveAt(0);
                }
            });
        }

        /// <summary>
        /// 清空所有曲线
        /// </summary>
        private void ClearCharts()
        {
            RunOnUIThread(() =>
            {
                OkPlotDataSeries.Clear();
                NgPlotDataSeries.Clear();
            });
            LogInfo("已清空所有曲线");
        }
        /// <summary>
        /// 从参数名提取螺丝标签
        /// </summary>
        private string ExtractScrewLabel(string? paramName, string? sortIndex)
        {
            // "上盖螺栓固定扭矩1" -> "螺丝 #1"
            if (!string.IsNullOrEmpty(sortIndex))
            {
                return $"螺丝 #{sortIndex}";
            }
            return paramName ?? "未知螺丝";
        }
        /// <summary>
        /// 创建曲线数据系列
        /// </summary>
        private PlotDataSeries CreatePlotSeries(string screwLabel, TighteningParam torque, TighteningParam? angle)
        {
            var dataPoints = new ObservableCollection<PlotDataPoint>();

            // 由于实际数据只有最终值，没有过程曲线
            // 这里可以显示：上下限范围 + 实际值的柱状/点图
            // 或者如果有曲线数据源，需要另外处理

            // 示例：创建简单的数据点（扭矩值）
            if (double.TryParse(torque.ParamFirstLower, out var lower) &&
                double.TryParse(torque.ParamFirstUpper, out var upper))
            {
                // 添加上下限参考线的点
                dataPoints.Add(new PlotDataPoint(0, lower));  // 下限
                dataPoints.Add(new PlotDataPoint(1, torque.NumericValue));  // 实际值
                dataPoints.Add(new PlotDataPoint(2, upper));  // 上限
            }
            else
            {
                dataPoints.Add(new PlotDataPoint(0, torque.NumericValue));
            }

            return new PlotDataSeries
            {
                Name = $"{screwLabel} ({torque.ParamValue}{torque.ParamUnit}) - {DateTime.Now:HH:mm:ss}",
                DataPoints = dataPoints,
                LineWidth = 2
            };
        }

        /// <summary>
        /// 模拟测试
        /// </summary>
        private async Task SimulateAsync()
        {
            LogInfo("开始模拟测试...");
            ResetAllScrewStatus();

            CurrentPackCode = $"PACK-{DateTime.Now:yyyyMMddHHmmss}";
            var random = new Random();

            var okSeriesList = new List<PlotDataSeries>();
            var ngSeriesList = new List<PlotDataSeries>();

            var totalScrews = Math.Min(ScrewPositions.Count, 8);
            for (int screwIndex = 1; screwIndex <= totalScrews; screwIndex++)
            {
                var isOk = random.NextDouble() > 0.15;
                var dataPoints = new ObservableCollection<PlotDataPoint>();

                for (int i = 0; i < 100; i++)
                {
                    double x = i * 0.1;
                    double y = isOk
                        ? 10 * (1 - Math.Exp(-x / 3)) + random.NextDouble() * 0.5
                        : (i < 50 ? 10 * (1 - Math.Exp(-x / 3)) : 5 - random.NextDouble() * 2);
                    dataPoints.Add(new PlotDataPoint(x, y));
                }

                var series = new PlotDataSeries
                {
                    Name = $"螺丝 #{screwIndex} - {DateTime.Now:HH:mm:ss}",
                    DataPoints = dataPoints,
                    LineWidth = 2
                };

                if (isOk)
                    okSeriesList.Add(series);
                else
                    ngSeriesList.Add(series);

                UpdateScrewStatus(screwIndex, isOk ? ScrewStatus.OK : ScrewStatus.NG,
                    dataPoints.Last().Y, dataPoints.Last().X);

                await Task.Delay(100);
            }

            RunOnUIThread(() =>
            {
                OkPlotDataSeries.Clear();
                NgPlotDataSeries.Clear();

                foreach (var series in okSeriesList)
                    OkPlotDataSeries.Add(series);

                foreach (var series in ngSeriesList)
                    NgPlotDataSeries.Add(series);

                SelectedScrewLabel = "模拟完成";
            });

            LogInfo($"模拟测试完成 - OK: {okSeriesList.Count}, NG: {ngSeriesList.Count}");
        }

        /// <summary>
        /// 加载CSV文件并显示曲线
        /// </summary>
        private async Task LoadCsvFileAsync()
        {
            try
            {
                // 创建文件选择对话框
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择CSV曲线数据文件",
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    InitialDirectory = _curveDataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Multiselect = false
                };

                var result = openFileDialog.ShowDialog();
                if (result != true)
                {
                    return;
                }

                var filePath = openFileDialog.FileName;
                LogInfo($"开始加载CSV文件: {filePath}");

                IsLoadingCurveData = true;
                BeginCollectCurveData(); // 清空现有曲线

                // 读取CSV文件
                var records = await ReadCsvFileAsync(filePath);

                if (records == null || records.Count == 0)
                {
                    LogWarn("CSV文件无有效数据");
                    return;
                }

                // 按螺丝分组
                var screwGroups = records
                    .GroupBy(r => r.ScrewIndex)
                    .OrderBy(g => g.Key)
                    .ToList();

                LogInfo($"成功加载 {screwGroups.Count} 个螺丝的曲线数据");

                // 处理每个螺丝的数据
                foreach (var group in screwGroups)
                {
                    var screwIndex = group.Key;
                    var screwRecords = group.OrderBy(r => r.PointIndex).ToList();

                    if (screwRecords.Count == 0) continue;

                    var firstRecord = screwRecords.First();
                    var screwLabel = firstRecord.ScrewLabel ?? $"螺丝 #{screwIndex}";
                    var isOk = firstRecord.IsOk;

                    // 创建曲线数据
                    var dataPoints = new ObservableCollection<PlotDataPoint>();
                    foreach (var record in screwRecords)
                    {
                        dataPoints.Add(new PlotDataPoint(record.PointIndex, record.Torque));
                    }

                    var series = new PlotDataSeries
                    {
                        Name = $"{screwLabel} (历史数据)",
                        DataPoints = dataPoints,
                        LineWidth = 1.5f
                    };

                    // 添加到对应的曲线集合
                    AddToPlotCollection(series, isOk);

                    // 更新螺丝位置状态
                    UpdateScrewStatus(screwIndex, isOk ? ScrewStatus.OK : ScrewStatus.NG,
                        firstRecord.Torque, firstRecord.Angle);

                    await Task.Delay(50); // 避免UI卡顿
                }

                // 更新产品编码(从文件名提取)
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                RunOnUIThread(() => CurrentPackCode = fileName);

                LogInfo($"CSV数据加载完成 - 共 {screwGroups.Count} 个螺丝");
                HandyControl.Controls.Growl.Success($"成功加载 {screwGroups.Count} 个螺丝的曲线数据");
            }
            catch (Exception ex)
            {
                LogError($"加载CSV文件失败: {ex.Message}");
                HandyControl.Controls.Growl.Error($"加载失败: {ex.Message}");
            }
            finally
            {
                IsLoadingCurveData = false;
            }
        }

        /// <summary>
        /// 读取CSV文件
        /// </summary>
        private async Task<List<ScrewCurveRecord>> ReadCsvFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var reader = new StreamReader(filePath, Encoding.UTF8);
                    using var csv = new CsvHelper.CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(
                        System.Globalization.CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        MissingFieldFound = null,
                        BadDataFound = null
                    });

                    // 读取表头
                    csv.Read();
                    csv.ReadHeader();
                    var headers = csv.HeaderRecord;

                    if (headers == null || headers.Length == 0)
                    {
                        LogWarn("CSV文件表头为空");
                        return new List<ScrewCurveRecord>();
                    }

                    var records = new List<ScrewCurveRecord>();

                    // 读取数据行
                    while (csv.Read())
                    {
                        var pointIndex = csv.GetField<int>("PointIndex");
                        var recordTime = csv.GetField<DateTime>("RecordTime");

                        // 遍历所有螺丝列
                        foreach (var header in headers)
                        {
                            if (!header.StartsWith("Screw")) continue;

                            var parts = header.Split('_');
                            if (parts.Length < 2) continue;

                            var screwPrefix = parts[0]; // 如 "Screw1"
                            var field = parts[1];       // 如 "Angle", "Torque"

                            // 提取螺丝索引
                            var screwIndexStr = screwPrefix.Replace("Screw", "");
                            if (!int.TryParse(screwIndexStr, out var screwIndex)) continue;

                            // 只在读取到Torque字段时创建记录
                            if (field == "Torque")
                            {
                                var torque = csv.GetField<double>($"{screwPrefix}_Torque");
                                var angle = csv.GetField<double>($"{screwPrefix}_Angle");
                                var label = csv.GetField<string>($"{screwPrefix}_Label");
                                var gun = csv.GetField<string>($"{screwPrefix}_Gun");
                                var isOk = csv.GetField<bool>($"{screwPrefix}_IsOk");

                                // 可选字段
                                var current = csv.TryGetField<double?>($"{screwPrefix}_Current", out var curr) ? curr : null;
                                var torqueLower = csv.TryGetField<double?>($"{screwPrefix}_TorqueLower", out var tl) ? tl : null;
                                var torqueUpper = csv.TryGetField<double?>($"{screwPrefix}_TorqueUpper", out var tu) ? tu : null;
                                var angleLower = csv.TryGetField<double?>($"{screwPrefix}_AngleLower", out var al) ? al : null;
                                var angleUpper = csv.TryGetField<double?>($"{screwPrefix}_AngleUpper", out var au) ? au : null;

                                records.Add(new ScrewCurveRecord
                                {
                                    PointIndex = pointIndex,
                                    RecordTime = recordTime,
                                    ScrewIndex = screwIndex,
                                    ScrewLabel = label,
                                    TightenGun = gun,
                                    Angle = angle,
                                    Torque = torque,
                                    Current = current,
                                    TorqueLower = torqueLower,
                                    TorqueUpper = torqueUpper,
                                    AngleLower = angleLower,
                                    AngleUpper = angleUpper,
                                    IsOk = isOk
                                });
                            }
                        }
                    }

                    return records;
                }
                catch (Exception ex)
                {
                    LogError($"读取CSV文件异常: {ex.Message}");
                    return new List<ScrewCurveRecord>();
                }
            });
        }

        #endregion

        #region 定义日志显示
        private void AddLog(string message, CoreLogLevel level,bool isToDatabase = false, [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
            };

            // 添加到界面集合
            RunOnUIThread(() =>
            {
                Logs.Add(entry);
                if (Logs.Count > 100) Logs.RemoveAt(0);
            });

            // 写入日志文件
            switch (level)
            {
                case CoreLogLevel.Info:
                    _logger.Info(message, isToDatabase, memberName, filePath, lineNumber);
                    break;
                case CoreLogLevel.Warn:
                    _logger.Warn(message, isToDatabase, memberName, filePath, lineNumber);
                    break;
                case CoreLogLevel.Error:
                    _logger.Error(message, isToDatabase, memberName, filePath, lineNumber);
                    break;
                default:
                    _logger.Log(level, message, isToDatabase, memberName, filePath, lineNumber);
                    break;
            }
        }

        // 方便不同等级log方法调用
        public void LogInfo(string msg, [CallerMemberName] string memberName = "",[CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) => AddLog(msg, CoreLogLevel.Info, true, memberName, filePath, lineNumber);
        public void LogWarn(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) => AddLog(msg, CoreLogLevel.Warn,true, memberName, filePath, lineNumber);
        public void LogError(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) => AddLog(msg, CoreLogLevel.Error,true, memberName, filePath, lineNumber);

        #endregion

        #region 螺丝位置配置

        /// <summary>
        /// 更新容器尺寸
        /// </summary>
        public void UpdateContainerSize(double width, double height)
        {
            if (width <= 0 || height <= 0) return;
            if (Math.Abs(_containerWidth - width) < 0.1 && Math.Abs(_containerHeight - height) < 0.1)
                return;
            _containerWidth = width;
            _containerHeight = height;
            LogInfo($"容器尺寸更新: {width:F0} x {height:F0}");
            RecalculateScrewCanvasPositions();
        }

        /// <summary>
        /// 重新计算所有螺丝的Canvas坐标
        /// </summary>
        private void RecalculateScrewCanvasPositions()
        {
            if (_containerWidth <= 0 || _containerHeight <= 0)
            {
                return;
            }

            if (_originalImageWidth <= 0 || _originalImageHeight <= 0)
            {
                LogWarn("原始图片尺寸无效，跳过坐标计算");
                return;
            }

            // 计算缩放比例（Uniform 拉伸模式）
            double scaleX = _containerWidth / _originalImageWidth;
            double scaleY = _containerHeight / _originalImageHeight;
            _currentScale = Math.Min(scaleX, scaleY);

            // 计算图片实际显示尺寸
            double displayWidth = _originalImageWidth * _currentScale;
            double displayHeight = _originalImageHeight * _currentScale;

            // 计算图片在容器中的偏移量（居中显示）
            double offsetX = (_containerWidth - displayWidth) / 2;
            double offsetY = (_containerHeight - displayHeight) / 2;

            LogInfo($"缩放比例: {_currentScale:F4}, 偏移: ({offsetX:F1}, {offsetY:F1})");

            RunOnUIThread(() =>
            {
                foreach (var screw in ScrewPositions)
                {
                    // 计算缩放后的显示半径
                    screw.DisplayRadius = screw.Radius * _currentScale;

                    // 计算 Canvas 坐标
                    // 1. 将原始像素坐标缩放到显示尺寸
                    // 2. 加上偏移量（图片居中）
                    // 3. 减去半径（使圆心对准坐标点）
                    double scaledX = screw.X * _currentScale;
                    double scaledY = screw.Y * _currentScale;

                    screw.CanvasLeft = offsetX + scaledX - screw.DisplayRadius;
                    screw.CanvasTop = offsetY + scaledY - screw.DisplayRadius;
                }
                LogInfo($"已重新计算 {ScrewPositions.Count} 个螺丝位置，缩放比例: {_currentScale:F3}");
            });
        }

        /// <summary>
        /// 工序选择变化处理
        /// </summary>
        private async void OnSelectedProcessChanged()
        {
            if (SelectedProcess != null)
            {
                LogInfo($"切换到工序: {SelectedProcess.ProcessName}");
                await LoadScrewPositionsFromJsonAsync();

                // 加载完配置后，如果容器尺寸已知，立即重新计算坐标
                if (_containerWidth > 0 && _containerHeight > 0)
                {
                    RecalculateScrewCanvasPositions();
                }
            }
        }

        /// <summary>
        /// 从JSON文件加载螺丝位置配置
        /// </summary>
        private async Task LoadScrewPositionsFromJsonAsync()
        {
            if (SelectedProcess == null) return;

            try
            {
                var config = await _screwConfigService.LoadConfigAsync(SelectedProcess.ProcessCode);

                if (config != null && config.ScrewPositions.Count > 0)
                {
                    _originalImageWidth = config.ImageWidth;
                    _originalImageHeight = config.ImageHeight;

                    RunOnUIThread(() =>
                    {
                        ScrewPositions.Clear();
                        foreach (var pos in config.ScrewPositions)
                        {
                            pos.Status = ScrewStatus.Pending; // 重置状态
                            // 如果配置中没有设置半径，使用默认值
                            if (pos.Radius <= 0)
                            {
                                pos.Radius = config.DefaultRadius;
                            }
                            ScrewPositions.Add(pos);
                        }
                    });

                    LogInfo($"成功加载 {config.ScrewPositions.Count} 个螺丝位置配置");
                    LogInfo($"原始图片尺寸: {_originalImageWidth} x {_originalImageHeight}");

                    // 重新计算坐标
                    RecalculateScrewCanvasPositions();
                }
                else
                {
                    LogWarn($"未找到工序 {SelectedProcess.ProcessCode} 的配置文件，生成默认配置");
                    await GenerateAndSaveDefaultConfigAsync();
                }
            }
            catch (Exception ex)
            {
                LogError($"加载螺丝位置配置失败: {ex.Message}");
                LoadDefaultScrewPositions();
            }
        }

        /// <summary>
        /// 生成并保存默认配置
        /// </summary>
        private async Task GenerateAndSaveDefaultConfigAsync()
        {
            if (SelectedProcess == null) return;

            // 设置默认图片尺寸
            _originalImageWidth = 2752;
            _originalImageHeight = 1536;

            var config = new ScrewPositionConfigFile
            {
                ProcessCode = SelectedProcess.ProcessCode,
                ProcessName = SelectedProcess.ProcessName,
                ImageWidth = _originalImageWidth,
                ImageHeight = _originalImageHeight,
                DefaultRadius = 10,
                ScrewPositions = GenerateGridScrewPositions(SelectedProcess.ScrewCount)
            };

            var saved = await _screwConfigService.SaveConfigAsync(config);
            if (saved)
            {
                var filePath = await _screwConfigService.GetConfigFilePathAsync(SelectedProcess.ProcessCode);
                LogInfo($"已生成默认配置文件: {filePath}");
            }

            RunOnUIThread(() =>
            {
                ScrewPositions.Clear();
                foreach (var pos in config.ScrewPositions)
                {
                    ScrewPositions.Add(pos);
                }
            });

            RecalculateScrewCanvasPositions();
        }

        /// <summary>
        /// 生成默认配置（根据图片上的螺丝分布）
        /// </summary>
        private ScrewPositionConfigFile GenerateDefaultConfig(string processCode, int screwCount)
        {
            var config = new ScrewPositionConfigFile
            {
                ProcessCode = processCode,
                ProcessName = SelectedProcess?.ProcessName ?? "",
                ImageWidth = 1516,
                ImageHeight = 758,
                DefaultRadius = 10
            };

            if (processCode == "P230-01")
            {
                // 根据上盖图片生成64颗螺丝位置
                config.ScrewPositions = GenerateTopCoverScrewPositions();
            }
            else
            {
                // 其他工序使用网格布局
                config.ScrewPositions = GenerateGridScrewPositions(screwCount);
            }

            return config;
        }

        /// <summary>
        /// 生成上盖螺丝位置（根据实际图片）
        /// </summary>
        private List<ScrewPositionConfig> GenerateTopCoverScrewPositions()
        {
            var positions = new List<ScrewPositionConfig>();
            int index = 1;
            double defaultRadius = 8;

            // ========== 顶部边缘螺丝 ==========
            double[] topEdgeX = { 19.5, 25.5, 31.5, 37.5, 43.5, 56.5, 62.5, 68.5, 74.5, 80.5 };
            foreach (var x in topEdgeX)
            {
                positions.Add(new ScrewPositionConfig
                {
                    Index = index++,
                    X = x,
                    Y = 2.5,
                    Radius = defaultRadius,
                    ParamName = $"顶边螺丝{index - 1}"
                });
            }

            // ========== 底部边缘螺丝 ==========
            foreach (var x in topEdgeX)
            {
                positions.Add(new ScrewPositionConfig
                {
                    Index = index++,
                    X = x,
                    Y = 97.5,
                    Radius = defaultRadius,
                    ParamName = $"底边螺丝{index - 11}"
                });
            }

            // ========== 左侧边缘螺丝 ==========
            double[] leftEdgeY = { 8, 20, 32, 44, 56, 68, 80, 92 };
            foreach (var y in leftEdgeY)
            {
                positions.Add(new ScrewPositionConfig
                {
                    Index = index++,
                    X = 2.5,
                    Y = y,
                    Radius = defaultRadius,
                    ParamName = $"左边螺丝{index - 21}"
                });
            }

            // ========== 右侧边缘螺丝 ==========
            foreach (var y in leftEdgeY)
            {
                positions.Add(new ScrewPositionConfig
                {
                    Index = index++,
                    X = 97.5,
                    Y = y,
                    Radius = defaultRadius,
                    ParamName = $"右边螺丝{index - 29}"
                });
            }

            // ========== 四角螺丝 ==========
            var corners = new[] { (4.5, 4.5), (95.5, 4.5), (4.5, 95.5), (95.5, 95.5) };
            foreach (var (cx, cy) in corners)
            {
                positions.Add(new ScrewPositionConfig
                {
                    Index = index++,
                    X = cx,
                    Y = cy,
                    Radius = defaultRadius + 2,
                    ParamName = $"角螺丝{index - 37}"
                });
            }

            // ========== 中间大孔螺丝 ==========
            positions.Add(new ScrewPositionConfig
            {
                Index = index++,
                X = 50,
                Y = 32,
                Radius = 12,
                ParamName = "中心上螺丝"
            });

            positions.Add(new ScrewPositionConfig
            {
                Index = index++,
                X = 50,
                Y = 78,
                Radius = 12,
                ParamName = "中心下螺丝"
            });

            // ========== 补充其他位置螺丝以达到64颗 ==========
            // 内部区域螺丝（根据需要调整）
            double[] innerX = { 15, 30, 45, 55, 70, 85 };
            double[] innerY = { 25, 50, 75 };

            foreach (var y in innerY)
            {
                foreach (var x in innerX)
                {
                    if (positions.Count >= 64) break;

                    // 避开中心大孔位置
                    if (Math.Abs(x - 50) < 10 && (Math.Abs(y - 32) < 10 || Math.Abs(y - 78) < 10))
                        continue;

                    positions.Add(new ScrewPositionConfig
                    {
                        Index = index++,
                        X = x,
                        Y = y,
                        Radius = defaultRadius,
                        ParamName = $"内部螺丝{index - 42}"
                    });
                }
                if (positions.Count >= 64) break;
            }

            return positions;
        }

        /// <summary>
        /// 生成网格布局螺丝位置
        /// </summary>
        private List<ScrewPositionConfig> GenerateGridScrewPositions(int count)
        {
            var positions = new List<ScrewPositionConfig>();
            var cols = (int)Math.Ceiling(Math.Sqrt(count));
            var rows = (int)Math.Ceiling((double)count / cols);

            // 边距（像素）
            double marginX = _originalImageWidth * 0.1;
            double marginY = _originalImageHeight * 0.1;

            // 可用区域
            double availableWidth = _originalImageWidth - 2 * marginX;
            double availableHeight = _originalImageHeight - 2 * marginY;

            double xStep = availableWidth / Math.Max(cols - 1, 1);
            double yStep = availableHeight / Math.Max(rows - 1, 1);

            for (int i = 0; i < count; i++)
            {
                var row = i / cols;
                var col = i % cols;

                positions.Add(new ScrewPositionConfig
                {
                    Index = i + 1,
                    X = marginX + col * xStep,  // 像素坐标
                    Y = marginY + row * yStep,  // 像素坐标
                    Radius = 10,                // 像素半径
                    ParamName = $"螺丝{i + 1}",
                    TightenGun = $"螺丝枪{(i % 4) + 1}"
                });
            }

            return positions;
        }



        #endregion

        #region 曲线数据保存
        
        // 用于收集当前产品所有螺丝的曲线数据
        private readonly List<ScrewCurveRecord> _currentProductCurveRecords = new();
        private readonly object _curveRecordsLock = new();

        /// <summary>
        /// 开始收集新产品的曲线数据（在处理新产品数据前调用）
        /// </summary>
        private void BeginCollectCurveData()
        {
            lock (_curveRecordsLock)
            {
                _currentProductCurveRecords.Clear();
            }
            ClearCharts();
        }

        /// <summary>
        /// 添加单个螺丝的曲线数据到集合
        /// </summary>
        private void AddScrewCurveData(
            string productSn,
            int screwIndex,
            string screwLabel,
            ScrewCurveDataResult curveData,
            string? tightenGun,
            bool isOk,
            TighteningParam? torqueParam = null,  // 传入工艺参数
            TighteningParam? angleParam = null)
        {
            if (curveData?.DataPoints == null || curveData.DataPoints.Count == 0)
                return;

            var recordTime = DateTime.Now;
            // 解析工艺参数上下限
            double? torqueLower = ParseDouble(torqueParam?.ParamFirstLower);
            double? torqueUpper = ParseDouble(torqueParam?.ParamFirstUpper);

            double? angleLower = ParseDouble(angleParam?.ParamFirstLower);
            double? angleUpper = ParseDouble(angleParam?.ParamFirstUpper);

            lock (_curveRecordsLock)
            {
                for (int i = 0; i < curveData.DataPoints.Count; i++)
                {
                    var point = curveData.DataPoints[i];
                    _currentProductCurveRecords.Add(new ScrewCurveRecord
                    {
                        PointIndex = i + 1,
                        RecordTime = recordTime,
                        ScrewIndex = screwIndex,
                        ScrewLabel = screwLabel,
                        TightenGun = tightenGun,

                        // 实测值
                        Angle = point.Angle,
                        Torque = point.Torque,
                        Current = point.Current,

                        // 工艺参数
                        TorqueLower = torqueLower,
                        TorqueUpper = torqueUpper,
                        AngleLower = angleLower,
                        AngleUpper = angleUpper,

                        IsOk = isOk
                    });
                }
            }
        }

        private double? ParseDouble(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return double.TryParse(value, out var result) ? result : null;
        }
        /// <summary>
        /// 从数据点获取电流值
        /// </summary>
        private double? GetCurrentFromDataPoint(CurveDataPoint point)
        {
            // 方式1：如果 CurveDataPoint 有 Current 属性
            //return point.Current;

            // 方式2：如果电流存储在其他属性中
            // return point.I;  // 或其他属性名

            // 方式3：如果需要通过反射获取（不推荐，性能差）
            var currentProp = point.GetType().GetProperty("Current")
                           ?? point.GetType().GetProperty("I")
                           ?? point.GetType().GetProperty("current");

            if (currentProp != null)
            {
                var value = currentProp.GetValue(point);
                if (value is double d) return d;
                if (value is float f) return f;
                if (value is decimal dec) return (double)dec;
            }

            return null;
        }
        /// <summary>
        /// 保存当前产品的所有曲线数据到CSV文件
        /// </summary>
        private async Task SaveProductCurveDataAsync(string productSn)
        {
            List<ScrewCurveRecord> recordsToSave;

            lock (_curveRecordsLock)
            {
                if (_currentProductCurveRecords.Count == 0)
                {
                    LogWarn($"产品 {productSn} 无曲线数据可保存");
                    return;
                }
                recordsToSave = new List<ScrewCurveRecord>(_currentProductCurveRecords);
            }

            try
            {

                // 创建按日期分类的子目录
                var dateFolder = DateTime.Now.ToString("yyyyMMdd");
                var saveDirectory = Path.Combine(_curveDataDirectory ?? "./CSVData", dateFolder);

                if (!Directory.Exists(saveDirectory))
                {
                    Directory.CreateDirectory(saveDirectory);
                }

                // 文件名使用 ProductSn
                var safeProductSn = GetSafeFileName(productSn);
                var fileName = $"{safeProductSn}.csv";
                var filePath = Path.Combine(saveDirectory, fileName);

                // 如果文件已存在，添加时间戳避免覆盖
                if (File.Exists(filePath))
                {
                    fileName = $"{safeProductSn}_{DateTime.Now:HHmmss}.csv";
                    filePath = Path.Combine(saveDirectory, fileName);
                }

                // 写入CSV文件
                //await WriteToFileAsync(filePath, recordsToSave);
                await SaveAsHorizontalFormatAsync(filePath, productSn, recordsToSave);

                var screwCount = recordsToSave.Select(r => r.ScrewIndex).Distinct().Count();
                LogInfo($"曲线数据已保存: {filePath}");
                LogInfo($"  螺丝数量: {screwCount}, 数据点总数: {recordsToSave.Count}");
            }
            catch (Exception ex)
            {
                LogError($"保存曲线数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取安全的文件名（移除非法字符）
        /// </summary>
        private string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return $"Unknown_{DateTime.Now:HHmmss}";

            var invalidChars = Path.GetInvalidFileNameChars();
            var safeName = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());

            return string.IsNullOrEmpty(safeName) ? $"Unknown_{DateTime.Now:HHmmss}" : safeName;
        }

        /// <summary>
        /// 以横向格式保存CSV
        /// </summary>
        /* private async Task SaveAsHorizontalFormatAsync(string filePath, string productSn, List<ScrewCurveRecord> records)
         {
             // 按螺丝分组
             var screwGroups = records
                 .GroupBy(r => r.ScrewIndex)
                 .OrderBy(g => g.Key)
                 .ToList();

             if (screwGroups.Count == 0)
                 return;

             // 找出最大数据点数量
             var maxPointCount = screwGroups.Max(g => g.Count());

             var sb = new StringBuilder();

             // ========== 构建表头 ==========
             var headerParts = new List<string> { "PointIndex", "RecordTime", "ProductSn" };

             foreach (var group in screwGroups)
             {
                 var screwIndex = group.Key;
                 var firstRecord = group.First();
                 var prefix = $"Screw{screwIndex}";

                 headerParts.Add($"{prefix}_Label");
                 headerParts.Add($"{prefix}_Angle");
                 headerParts.Add($"{prefix}_Torque");
                 headerParts.Add($"{prefix}_Current");
                 headerParts.Add($"{prefix}_Gun");
                 headerParts.Add($"{prefix}_IsOk");
             }

             sb.AppendLine(string.Join(",", headerParts));

             // ========== 构建数据行 ==========
             // 将每个螺丝的数据转为字典，便于按PointIndex查找
             var screwDataDict = screwGroups.ToDictionary(
                 g => g.Key,
                 g => g.OrderBy(r => r.PointIndex).ToList()
             );

             var recordTime = records.FirstOrDefault()?.RecordTime ?? DateTime.Now;

             for (int pointIdx = 0; pointIdx < maxPointCount; pointIdx++)
             {
                 var rowParts = new List<string>
                 {
                     (pointIdx + 1).ToString(),
                     recordTime.ToString("yyyy-MM-dd HH:mm:ss"),
                     EscapeCsvField(productSn)
                 };

                 foreach (var group in screwGroups)
                 {
                     var screwIndex = group.Key;
                     var dataList = screwDataDict[screwIndex];

                     if (pointIdx < dataList.Count)
                     {
                         var point = dataList[pointIdx];
                         rowParts.Add(EscapeCsvField(point.ScrewLabel ?? ""));
                         rowParts.Add(point.Angle.ToString("F6"));
                         rowParts.Add(point.Torque.ToString("F6"));
                         rowParts.Add(point.Current?.ToString("F6") ?? "");
                         rowParts.Add(EscapeCsvField(point.TightenGun ?? ""));
                         rowParts.Add(point.IsOk ? "TRUE" : "FALSE");
                     }
                     else
                     {
                         // 该螺丝在此点位无数据，填充空值
                         rowParts.AddRange(new[] { "", "", "", "", "", "" });
                     }
                 }

                 sb.AppendLine(string.Join(",", rowParts));
             }

             await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
         }*/

        /// <summary>
        /// 以横向格式保存CSV（使用CsvHelper + DataTable）
        /// </summary>
        private async Task SaveAsHorizontalFormatAsync(
            string filePath,
            string productSn,
            List<ScrewCurveRecord> records)
        {
            // 按螺丝分组
            var screwGroups = records
                .GroupBy(r => r.ScrewIndex)
                .OrderBy(g => g.Key)
                .ToList();

            if (screwGroups.Count == 0) return;

            var maxPointCount = screwGroups.Max(g => g.Count());
            var dataTable = new DataTable();

            // ===== 构建表头 =====
            dataTable.Columns.Add("PointIndex", typeof(int));
            dataTable.Columns.Add("RecordTime", typeof(DateTime));

            foreach (var group in screwGroups)
            {
                var prefix = $"Screw{group.Key}";

                // 基础信息（仅在第一行有值的列）
                dataTable.Columns.Add($"{prefix}_Label", typeof(string));
                dataTable.Columns.Add($"{prefix}_Gun", typeof(string));

                // 实测值
                dataTable.Columns.Add($"{prefix}_Angle", typeof(double));
                dataTable.Columns.Add($"{prefix}_Torque", typeof(double));
                dataTable.Columns.Add($"{prefix}_Current", typeof(double));

                // 工艺参数（上下限）
                dataTable.Columns.Add($"{prefix}_TorqueLower", typeof(double));
                dataTable.Columns.Add($"{prefix}_TorqueUpper", typeof(double));
                dataTable.Columns.Add($"{prefix}_AngleLower", typeof(double));
                dataTable.Columns.Add($"{prefix}_AngleUpper", typeof(double));

                // 结果
                dataTable.Columns.Add($"{prefix}_IsOk", typeof(bool));
            }

            // ===== 填充数据 =====
            var screwDataDict = screwGroups.ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.PointIndex).ToList()
            );

            var recordTime = records.FirstOrDefault()?.RecordTime ?? DateTime.Now;

            for (int pointIdx = 0; pointIdx < maxPointCount; pointIdx++)
            {
                var row = dataTable.NewRow();
                row["PointIndex"] = pointIdx + 1;
                row["RecordTime"] = recordTime;

                foreach (var group in screwGroups)
                {
                    var prefix = $"Screw{group.Key}";
                    var dataList = screwDataDict[group.Key];

                    if (pointIdx < dataList.Count)
                    {
                        var point = dataList[pointIdx];

                        row[$"{prefix}_Label"] = point.ScrewLabel ?? "";
                        row[$"{prefix}_Gun"] = point.TightenGun ?? "";
                        row[$"{prefix}_Angle"] = point.Angle;
                        row[$"{prefix}_Torque"] = point.Torque;
                        row[$"{prefix}_Current"] = ToDbValue(point.Current);
                        row[$"{prefix}_TorqueLower"] = ToDbValue(point.TorqueLower);
                        row[$"{prefix}_TorqueUpper"] = ToDbValue(point.TorqueUpper);
                        row[$"{prefix}_AngleLower"] = ToDbValue(point.AngleLower);
                        row[$"{prefix}_AngleUpper"] = ToDbValue(point.AngleUpper);
                        row[$"{prefix}_IsOk"] = point.IsOk;
                    }
                }

                dataTable.Rows.Add(row);
            }

            // 使用 CsvHelper 的 DataTable 方法写入
            await Task.Run(() => CsvSaveHelper.WriteToFileAsync(filePath, dataTable));
        }

        /// <summary>
        /// 将可空值转换为 DataTable 兼容的值
        /// </summary>
        private static object ToDbValue(double? value)
        {
            return value.HasValue ? (object)value.Value : DBNull.Value;
        }

        /// <summary>
        /// 从螺丝标签中提取序号
        /// </summary>
        private int ExtractScrewIndex(string screwLabel)
        {
            // "螺丝 #1" -> 1
            if (string.IsNullOrEmpty(screwLabel))
                return 0;

            var match = System.Text.RegularExpressions.Regex.Match(screwLabel, @"#(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var index))
            {
                return index;
            }

            return 0;
        }

        #endregion

    }


}

