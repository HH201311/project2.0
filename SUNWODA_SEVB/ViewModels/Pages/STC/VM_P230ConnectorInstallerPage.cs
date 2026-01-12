using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Component.UserControls;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Project.STC;
using SUNWODA_SEVB.Core.Models.Socket;
using System.Collections.ObjectModel;
using System.Reflection;
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
        private bool _initializedView = false;

        //private ModelViewerControl? _modelViewerControl;

        #region 私有字段
        private bool _isServerRunning;
        private int _port;
        private int _connectedClientCount;
        private ProcessInfo? _selectedProcess;
        private string _currentPackCode = string.Empty;
        private string _selectedScrewLabel = string.Empty;
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

        #endregion

        //public ModelViewerControl? ModelViewerControl
        //{
        //    get => _modelViewerControl;
        //    set => SetProperty(ref _modelViewerControl, value);
        //}
        public VM_P230ConnectorInstallerPage(
            ILoggerService<VM_P230ConnectorInstallerPage> logger,
            IModuleManager moduleManager,
             ISocketServer socketServer, IServiceProvider serviceProvider
        )
        {
            _logger = logger;
            _socketServer = socketServer;
            _moduleManager = moduleManager;
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            StartServerCommand = new RelayCommand(async _ => await StartServerAsync(), _ => !IsServerRunning);
            StopServerCommand = new RelayCommand(async _ => await StopServerAsync(), _ => IsServerRunning);
            ClearChartsCommand = new RelayCommand( _ => ClearCharts(), _ => !IsServerRunning);
            SimulateCommand = new RelayCommand( async _ => await SimulateAsync(), _ => !IsServerRunning);
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
                //var moduleName = typeof(VM_P230ConnectorInstallerPage).GetCustomAttribute<ModuleAttribute>()?.Name;
                //if (moduleName != null)
                //{
                //    await RunOnUIThreadAsync(() =>
                //    {
                //        var view = _moduleManager.GetViewFromService(moduleName);
                //        ModelViewerControl = view?.FindName("ModelViewer") as ModelViewerControl;
                //    });
                //}
                await LoadProcessesAsync();

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
                var processConfigJson = await projectSettingRepo.GetSettingValueAsync("ProcessList", "ConConectorInstaller");

                if (!string.IsNullOrEmpty(processConfigJson))
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ProcessInfo>? processList = JsonSerializer.Deserialize<List<ProcessInfo>>(processConfigJson, options);

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
                    ImagePath = "pack://application:,,,/Assets/Images/shanggai.png",
                    ScrewCount = 64
                });
                Processes.Add(new ProcessInfo
                {
                    Id = 2,
                    ProcessCode = "P230-02",
                    ProcessName = "螺丝复拧",
                    ImagePath = "/Assets/Images/Process02.png",
                    ScrewCount = 6
                });

                SelectedProcess = Processes.FirstOrDefault();
            });
        }
        /// <summary>
        /// 工序选择变化处理
        /// </summary>
        private void OnSelectedProcessChanged()
        {
            if (SelectedProcess != null)
            {
                LogInfo($"切换到工序: {SelectedProcess.ProcessName}");
                // 可以在这里添加切换工序后的其他逻辑
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
            LogInfo($"收到数据 [{e.ClientEndPoint}]: {message}");
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
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var data = JsonSerializer.Deserialize<TighteningResult>(message, options);
                if (data?.TestData == null || data.TestData.Count == 0)
                {
                    LogWarn($"端口{clientEndPoint}接收到的数据为空或无测试数据");
                    return;
                }

                // 更新Pack包编码
                RunOnUIThread(() =>
                {
                    CurrentPackCode = data.ProductSn ?? CurrentPackCode;
                });

                // 按 paramSort 分组处理（每个螺丝位置有扭矩和角度两条数据）
                var groupedBySort = data.TestData
                    .GroupBy(p => p.ParamSort)
                    .OrderBy(g => int.TryParse(g.Key, out var sortNum) ? sortNum : 0);

                foreach (var group in groupedBySort)
                {
                    var torqueParam = group.FirstOrDefault(p =>
                        p.ParamUnit?.Equals("N.M", StringComparison.OrdinalIgnoreCase) == true);
                    var angleParam = group.FirstOrDefault(p =>
                        p.ParamUnit?.Equals("°", StringComparison.OrdinalIgnoreCase) == true);

                    if (torqueParam == null) continue;

                    var screwLabel = ExtractScrewLabel(torqueParam.ParamName, group.Key);
                    var isItemOk = group.All(p => p.IsOk);

                    // 更新当前螺丝标签
                    RunOnUIThread(() => SelectedScrewLabel = screwLabel);

                    // 创建曲线数据（这里用扭矩值作为示例，实际可能需要曲线数据）
                    var series = CreatePlotSeries(screwLabel, torqueParam, angleParam);

                    // 根据结果添加到对应曲线集合
                    AddToPlotCollection(series, isItemOk);
                }

                // 记录整体结果
                var resultText = data.IsOk ? "OK" : "NG";
                var screwCount = groupedBySort.Count();
                var okCount = groupedBySort.Count(g => g.All(p => p.IsOk));

                LogInfo($"数据处理完成 - 产品: {data.ProductSn}, 结果: {resultText}, " +$"螺丝数: {screwCount}, OK: {okCount}, NG: {screwCount - okCount}");
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
        /// 添加到对应的曲线集合
        /// </summary>
        private void AddToPlotCollection(PlotDataSeries series, bool isOk)
        {
            RunOnUIThread(() =>
            {
                var collection = isOk ? OkPlotDataSeries : NgPlotDataSeries;
                collection.Add(series);

                // 限制最大曲线数量
                while (collection.Count > 10)
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
        /// 模拟测试
        /// </summary>
        private async Task SimulateAsync()
        {
            LogInfo("开始模拟测试...");

            // 模拟Pack包编码
            CurrentPackCode = $"PACK-{DateTime.Now:yyyyMMddHHmmss}";

            var random = new Random();

            // 模拟OK曲线
            for (int screwIndex = 1; screwIndex <= 3; screwIndex++)
            {
                SelectedScrewLabel = $"螺丝 #{screwIndex}";

                var dataPoints = new ObservableCollection<PlotDataPoint>();
                for (int i = 0; i < 100; i++)
                {
                    // 模拟正常的拧紧曲线：先快速上升，然后趋于平稳
                    double x = i * 0.1;
                    double y = 10 * (1 - Math.Exp(-x / 3)) + random.NextDouble() * 0.5;
                    dataPoints.Add(new PlotDataPoint(x, y));
                }

                var okSeries = new PlotDataSeries
                {
                    Name = $"螺丝 #{screwIndex} - {DateTime.Now:HH:mm:ss}",
                    DataPoints = dataPoints,
                    LineWidth = 2
                };

                RunOnUIThread(() => OkPlotDataSeries.Add(okSeries));

                await Task.Delay(500); // 模拟间隔
            }

            // 模拟NG曲线
            SelectedScrewLabel = "螺丝 #4 (异常)";
            var ngDataPoints = new ObservableCollection<PlotDataPoint>();
            for (int i = 0; i < 100; i++)
            {
                double x = i * 0.1;
                // 模拟异常曲线：中途下降
                double y = i < 50
                    ? 10 * (1 - Math.Exp(-x / 3)) + random.NextDouble() * 0.5
                    : 5 - random.NextDouble() * 2;
                ngDataPoints.Add(new PlotDataPoint(x, y));
            }

            var ngSeries = new PlotDataSeries
            {
                Name = $"螺丝 #4 (异常) - {DateTime.Now:HH:mm:ss}",
                DataPoints = ngDataPoints,
                LineWidth = 2
            };

            RunOnUIThread(() => NgPlotDataSeries.Add(ngSeries));

            LogInfo("模拟测试完成");
        }


        #endregion

        #region 定义日志显示
        private void AddLog(string message, CoreLogLevel level,bool isToDatabase = false)
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
                    _logger.Info(message, isToDatabase);
                    break;
                case CoreLogLevel.Warn:
                    _logger.Warn(message, isToDatabase);
                    break;
                case CoreLogLevel.Error:
                    _logger.Error(message, isToDatabase);
                    break;
                default:
                    _logger.Log(level, message);
                    break;
            }
        }

        // 方便不同等级log方法调用
        public void LogInfo(string msg) => AddLog(msg, CoreLogLevel.Info,true);
        public void LogWarn(string msg) => AddLog(msg, CoreLogLevel.Warn,true);
        public void LogError(string msg) => AddLog(msg, CoreLogLevel.Error,true);

        #endregion



    }


}