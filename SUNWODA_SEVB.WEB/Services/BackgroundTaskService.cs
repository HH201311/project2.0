using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;

namespace SUNWODA_SEVB.WEB.Services
{
    public class BackgroundTaskService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService<BackgroundTaskService> _logger;
        private readonly IWebApiClient _apiClient;
        private readonly IWebConfiguration _configuration;
        private readonly WebStateService _stateService;


        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _heartbeatTask;
        private Task? _webWorkTask;

        private int _heartbeatErrorCount = 0;
        private bool _isFirstUpload = true;
        private bool _isFirstCheck = true;
        private bool _isDownloading = false;
        private readonly object _lockObject = new(); // 添加锁对象用于线程安全
        private volatile bool _shouldTriggerImmediateHeartbeat = false;

        public BackgroundTaskService(
            IServiceProvider serviceProvider,
            ILoggerService<BackgroundTaskService> logger,
            IWebApiClient apiClient,
            IWebConfiguration configuration,
            WebStateService stateService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        }

        public async Task StartAsync()
        {
            lock (_lockObject)
            {
                // 确保不重复启动
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    _logger.Warn("后台任务已经在运行中");
                    return;
                }

                _cancellationTokenSource = new CancellationTokenSource();
            }

            var token = _cancellationTokenSource.Token;

            // 启动心跳任务
            _heartbeatTask = Task.Run(() => HeartbeatTaskAsync(token), token);

            // 启动WebWork任务
            _webWorkTask = Task.Run(() => WebWorkTaskAsync(token), token);

            _logger.Info("后台任务已启动");
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            CancellationTokenSource? cts;
            Task? heartbeatTask;
            Task? webWorkTask;

            lock (_lockObject)
            {
                cts = _cancellationTokenSource;
                heartbeatTask = _heartbeatTask;
                webWorkTask = _webWorkTask;

                if (cts == null)
                {
                    _logger.Debug("后台任务未运行，无需停止");
                    return;
                }
            }

            try
            {
                // 发送取消信号
                cts.Cancel();

                // 等待任务完成
                var tasks = new List<Task>();
                if (heartbeatTask != null)
                {
                    tasks.Add(heartbeatTask);
                }
                if (webWorkTask != null)
                {
                    tasks.Add(webWorkTask);
                }

                if (tasks.Count > 0)
                {
                    // 设置超时时间，防止无限等待
                    using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        try
                        {
                            await Task.WhenAll(tasks).WaitAsync(timeoutCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.Debug("后台任务已正常取消");
                        }
                        catch (TimeoutException)
                        {
                            _logger.Warn("等待后台任务停止超时");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("停止后台任务时发生异常", ex);
            }
            finally
            {
                // 清理资源
                cts?.Dispose();

                lock (_lockObject)
                {
                    _cancellationTokenSource = null;
                    _heartbeatTask = null;
                    _webWorkTask = null;
                }

                _logger.Info("后台任务已停止");
            }
        }

        private async Task HeartbeatTaskAsync(CancellationToken cancellationToken)
        {
            var lastHeartbeatTime = DateTime.MinValue;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, cancellationToken);

                    var settings = _configuration.GetSettings();

                    if (!settings.EnableHeartbeat)
                    {
                        continue;
                    }

                    // 检查是否需要立即触发（设备刚绑定成功）
                    bool forceHeartbeat = _shouldTriggerImmediateHeartbeat;
                    if (forceHeartbeat)
                    {
                        _shouldTriggerImmediateHeartbeat = false;
                    }

                    bool intervalElapsed = (DateTime.Now - lastHeartbeatTime).TotalSeconds >= settings.HeartbeatIntervalSeconds;

                    if (!forceHeartbeat && !intervalElapsed) continue;

                    // 使用状态服务检查设备绑定状态
                    if (!_stateService.IsDeviceBound)
                    {
                        continue;
                    }
                    
                    // 使用验证方法
                    if (!settings.IsHeartbeatConfigValid())
                    {
                        _logger.Warn("心跳配置不完整，跳过心跳");
                        await Task.Delay(5000, cancellationToken);
                        continue;
                    }

                    lastHeartbeatTime = DateTime.Now;

                    var response = await _apiClient.SendHeartbeatAsync(
                        settings.DeviceSn!, 
                        "正常",
                        settings.SoftVersion ?? "Unknown",
                        settings.UUID!);

                    if (response.IsSuccess)
                    {
                        _heartbeatErrorCount = 0;
                        _stateService.UpdateConnectionStatus(true);// 使用状态服务更新状态(后续界面的web状态更新根据此)
                    }
                    else
                    {
                        _heartbeatErrorCount++;
                        _logger.Debug($"心跳失败 ({_heartbeatErrorCount}/3): {response.Message}");
                    }

                    if (_heartbeatErrorCount >= 3)
                    {
                        _stateService.UpdateConnectionStatus(false);
                        _heartbeatErrorCount = 0;
                        _logger.Warn("WEB连接断开");
                    }
                }
                catch (TaskCanceledException)
                {
                    // 正常取消，退出循环
                    break;
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，退出循环
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("心跳任务异常", ex);

                    // 避免在异常情况下过于频繁的重试
                    try
                    {
                        await Task.Delay(60000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.Debug("心跳任务已退出");
        }

        private async Task WebWorkTaskAsync(CancellationToken cancellationToken)
        {
            var lastDeviceBindingTime = DateTime.Now;
            var lastUploadPcInfoTime = DateTime.Now;
            var lastCheckVersionTime = DateTime.Now;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, cancellationToken);

                    var settings = _configuration.GetSettings();

                    // 设备绑定
                    if (!_stateService.IsDeviceBound &&
                       (DateTime.Now - lastDeviceBindingTime).TotalSeconds > settings.DeviceBindingRetrySeconds)
                    {
                        lastDeviceBindingTime = DateTime.Now;

                        // 通过ServiceProvider获取WebService实例
                        var webService = _serviceProvider.GetRequiredService<IWebService>();
                        var bindSuccess = await webService.BindDeviceAsync();

                        if (bindSuccess)
                        {
                            _logger.Info("设备绑定成功");

                            _shouldTriggerImmediateHeartbeat = true;
                            try
                            {
                                var currentSettings = _configuration.GetSettings();
                                if (currentSettings.IsHeartbeatConfigValid())
                                {
                                    var heartbeatResponse = await _apiClient.SendHeartbeatAsync(
                                        currentSettings.DeviceSn!,
                                        "正常",
                                        currentSettings.SoftVersion ?? "Unknown",
                                        currentSettings.UUID!);

                                    if (heartbeatResponse.IsSuccess)
                                    {
                                        _stateService.UpdateConnectionStatus(true);
                                        _logger.Info("设备绑定后首次心跳成功，连接已就绪");
                                    }
                                    else
                                    {
                                        _logger.Warn($"设备绑定后首次心跳失败: {heartbeatResponse.Message}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Warn($"设备绑定后发送心跳异常: {ex.Message}");
                            }
                        }
                       
                    }

                    // PC信息上传
                    if (_stateService.IsConnected && settings.EnablePcInfoUpload &&
                         ((_isFirstUpload || (DateTime.Now - lastUploadPcInfoTime).TotalMinutes > settings.PcInfoUploadIntervalMinutes)))
                    {
                        lastUploadPcInfoTime = DateTime.Now;

                        if (_isFirstUpload)
                        {
                            await Task.Delay(3000, cancellationToken);
                        }

                        var webService = _serviceProvider.GetRequiredService<IWebService>();
                        var success = await webService.UploadPcInfoAsync();
                        if (success)
                        {
                            _isFirstUpload = false;
                        }
                    }

                    // 版本检查
                    if (_stateService.IsConnected && !_isDownloading && settings.EnableVersionCheck &&
                         ((_isFirstCheck || (DateTime.Now - lastCheckVersionTime).TotalSeconds > settings.VersionCheckIntervalSeconds)))
                    {
                        lastCheckVersionTime = DateTime.Now;
                        _isFirstCheck = false;

                        var needUpload = await GetNeedUploadSoftAsync();
                        if (needUpload == "0")
                        {
                            _isDownloading = true;

                            try
                            {
                                var webService = _serviceProvider.GetRequiredService<IWebService>();
                                var hasNewVersion = await webService.CheckVersionAsync();
                                if (hasNewVersion)
                                {
                                    _logger.Info("发现新版本并下载成功");
                                }
                            }
                            finally
                            {
                                _isDownloading = false;
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // 正常取消，退出循环
                    break;
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，退出循环
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("WebWork任务异常", ex);

                    // 避免在异常情况下过于频繁的重试
                    try
                    {
                        await Task.Delay(5000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.Debug("WebWork任务已退出");
        }

        private async Task<string> GetNeedUploadSoftAsync()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                    return await globalSettingRepo.GetSettingValueAsync<string>("NeedUploadSoft") ?? "0";
                }
            }
            catch (Exception ex)
            {
                _logger.Error("获取NeedUploadSoft失败", ex);
                return "0";
            }
        }

        // 添加一个方法来检查服务是否正在运行
        public bool IsRunning
        {
            get
            {
                lock (_lockObject)
                {
                    return _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested;
                }
            }
        }
    }
}
