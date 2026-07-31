using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using RemoteFileMonitor.Data;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Data;
using SUNWODA_SEVB.Logging;
using SUNWODA_SEVB.Logging.Targets;
using SUNWODA_SEVB.PLC;
using SUNWODA_SEVB.Services;
using SUNWODA_SEVB.Services.Project;
using SUNWODA_SEVB.Services.Project.CTOL;
using SUNWODA_SEVB.Tool.Configuration;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.ViewModels.Windows.Common;
using SUNWODA_SEVB.ViewModels.Windows.Project;
using SUNWODA_SEVB.Views.Windows.Common;
using SUNWODA_SEVB.Views.Windows.Project;
using SUNWODA_SEVB.WEB;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using SUNWODA_SEVB.MES;

namespace SUNWODA_SEVB
{
    public partial class App : Application
    {
        private IHost? _host;
        private System.Timers.Timer _logCleanupTimer = new System.Timers.Timer();
        private CancellationTokenSource _shutdownCts = new CancellationTokenSource();
        private Mutex? _mutex;
        private const string ServiceName = "WatchDogService";
        private string WatchDogExePath =>
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "WatchDogService\\SUNWODA_SEVB.WatchDogService.exe"
            );

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            //RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            try
            {
                MonitorAppIsRunning();
                // 确保配置文件存在且有效
                ConfigurationService.EnsureValidConfig();
                // 步骤1：构建Host
                _host = BuildHost();

                // 步骤2：启动Host
                await _host.StartAsync();

                var appLogger = _host.Services.GetRequiredService<ILoggerService<App>>();

                // 步骤3：初始化数据库（核心步骤，必须首先完成）
                if (!await InitializeDatabaseWithRetry(appLogger))
                {
                    await ShowErrorAndExit("数据库初始化失败！请检查连接配置。");
                    return;
                }

                // 步骤4：配置NLog数据库目标（数据库就绪后）
                await ConfigureNLogDatabase(appLogger);

                // 步骤5：初始化应用设置(账户、项目)
                await InitializeApplicationSettings(appLogger);

                // 记录应用启动信息
                LogApplicationStartup(appLogger);

                // 步骤6：设置全局异常处理
                SetupGlobalExceptionHandling();

                // 步骤7：启动后台任务
                StartBackgroundTasks();

                // 步骤9：判断是否启用python线程
                await InitPythonEnvironment();

                await InitCTOLEnvironment();

                // 步骤10：创建并显示主窗口
                ShowMainWindow(appLogger);
            }
            catch (Exception ex)
            {
                await HandleStartupError(ex);
            }
        }

        /// <summary>
        /// 监测程序是否在运行，运行则不启动新实例并将已运行程序置顶最前面
        /// </summary>
        private void MonitorAppIsRunning()
        {
            bool createdNew;

            // 给互斥量取个唯一的全局名字
            _mutex = new Mutex(true, "Global\\SUNWODA_SEVBMutex", out createdNew);

            if (!createdNew)
            {
                var current = Process.GetCurrentProcess();

                foreach (var process in Process.GetProcessesByName(current.ProcessName))
                {
                    if (process.Id != current.Id)
                    {
                        WinApiHelper.SetForegroundWindow(process.MainWindowHandle);
                    }
                }
                // 显示提示信息
                MessageBox.Show(
                    "软件已在运行中，请勿重复启动。\n已为您切换到运行中的窗口。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// 构建Host
        /// </summary>
        private IHost BuildHost()
        {
            return Host.CreateDefaultBuilder()
                .UseContentRoot(AppDomain.CurrentDomain.BaseDirectory)
                .ConfigureAppConfiguration(
                    (context, config) =>
                    {
                        config.Sources.Clear();
                        config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);

                        // 使用支持加密的JSON配置文件加载器
                        config.AddEncryptedJsonFile(
                            path: "appsettings.json",
                            optional: false,
                            reloadOnChange: true,
                            encryptionKey: null,
                            encryptionIV: null
                        );

                        // 环境特定的配置文件
                        config.AddEncryptedJsonFile(
                            path: $"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                            optional: true,
                            reloadOnChange: true
                        );

                        // 添加远程监控文件配置
                        config.AddJsonFile(
                            "remote_file_monitor_config.json",
                            optional: true,
                            reloadOnChange: false
                        );

                        config.AddEnvironmentVariables();
                    }
                )
                .ConfigureServices(
                    (context, services) =>
                    {
                        ConfigurationHelper.SetConfiguration(context.Configuration);
                        ConfigureServices(services, context.Configuration);
                    }
                )
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
                    logging.AddNLog();
                })
                .Build();
        }

        private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // 注册配置
            services.AddSingleton(configuration);

            // 添加日志服务
            services.AddNLogServices();

            // 内存缓存
            services.AddMemoryCache();

            // 使用新的方法注册 SqlSugar 和所有数据服务
            services.AddDataServices();

            // 添加数据库服务
            services.AddSingleton<IDatabaseService, DatabaseService>();

            // 添加PLC服务
            services.AddPLCService();

            // 注册MES服务
            services.AddMesServices();

            // 添加Web服务
            services.AddWebServices();

            // 添加MVVM框架服务
            services.AddMvvmFramework();

            // 添加CTOL远程文件监控服务，后续开发（利用 appsettings.json 来决定是否添加）
            //services.AddCTOLRemoteFileMonitor(configuration);

            // 添加JRRY远程文件监控服务
            //services.AddJRRYRemoteFileMonitor(configuration);

            // 注册项目相关服务(后期合并)
            services.AddProjectServices();

            // 注册主窗口的ViewModel和View
            services.AddSingleton<VM_MainWindow>();
            services.AddSingleton<MainWindow>(serviceProvider =>
            {
                var viewModel = serviceProvider.GetRequiredService<VM_MainWindow>();
                var window = new MainWindow { DataContext = viewModel };
                viewModel.NavigationFrame = window.NavigationFrame;
                viewModel.InitNavigation();
                return window;
            });

            // 注册主窗口的ViewModel和View
            services.AddSingleton<VM_CCTBMainWindow>();
            services.AddSingleton<CCTBMainWindow>(serviceProvider =>
            {
                var viewModel = serviceProvider.GetRequiredService<VM_CCTBMainWindow>();
                var window = new CCTBMainWindow { DataContext = viewModel };
                viewModel.NavigationFrame = window.NavigationFrame;
                viewModel.InitNavigation();
                return window;
            });
        }

        /// <summary>
        /// 初始化数据库（带重试机制）
        /// </summary>
        private async Task<bool> InitializeDatabaseWithRetry(
            ILoggerService<App> logger,
            int maxRetries = 3
        )
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    logger.Info($"开始初始化数据库 (尝试 {attempt}/{maxRetries})...");

                    // 调用扩展方法进行数据库初始化
                    var result = await _host!.Services.InitializeDatabaseAsync();

                    if (result)
                    {
                        logger.Info("数据库初始化成功");
                        return true;
                    }

                    logger.Warn($"数据库初始化失败 (尝试 {attempt}/{maxRetries})");
                }
                catch (Exception ex)
                {
                    logger.Error(
                        $"数据库初始化异常 (尝试 {attempt}/{maxRetries}): {ex.Message}",
                        ex
                    );
                }

                if (attempt < maxRetries)
                {
                    var delay = attempt * 2000; // 递增延迟
                    logger.Info($"等待 {delay}ms 后重试...");
                    await Task.Delay(delay);
                }
            }

            return false;
        }

        /// <summary>
        /// 配置NLog数据库目标
        /// </summary>
        private async Task ConfigureNLogDatabase(ILoggerService<App> logger)
        {
            try
            {
                // 短暂延迟确保数据库表完全就绪
                await Task.Delay(500);

                // 初始化DatabaseLogTarget
                DatabaseLogTarget.Initialize(_host!.Services);

                // 加载NLog配置文件
                var env = _host.Services.GetRequiredService<IHostEnvironment>();
                var config = _host.Services.GetRequiredService<IConfiguration>();
                var nlogConfigPath = Path.Combine(env.ContentRootPath, "NLog.config");

                if (File.Exists(nlogConfigPath))
                {
                    LogManager.Configuration = new XmlLoggingConfiguration(nlogConfigPath);

                    // 设置数据库连接字符串变量
                    var connectionString = config.GetConnectionString("DefaultConnection");
                    if (!string.IsNullOrWhiteSpace(connectionString))
                    {
                        LogManager.Configuration.Variables["dbConnection"] = connectionString;
                    }

                    logger.Info("NLog数据库目标配置成功");
                }
                else
                {
                    logger.Warn($"NLog配置文件不存在: {nlogConfigPath}");
                }
            }
            catch (Exception ex)
            {
                // NLog配置失败不应该阻止应用启动
                logger.Error($"NLog数据库目标配置失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 初始化应用设置
        /// </summary>
        private async Task InitializeApplicationSettings(ILoggerService<App> logger)
        {
            using (var scope = _host!.Services.CreateScope())
            {
                try
                {
                    var globalSettingRepo =
                        scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();

                    // 确保当前用户设置
                    var currentUser = await globalSettingRepo.GetSettingValueAsync(
                        "CurrentUserAccount"
                    );
                    if (currentUser != "guest")
                    {
                        await globalSettingRepo.UpdateSettingValueAsync(
                            "CurrentUserAccount",
                            "guest"
                        );
                        logger.Info("初始化默认用户账户为 guest");
                    }

                    // 加载默认项目
                    var defaultProject = await globalSettingRepo.GetSettingValueAsync(
                        "DefaultProject"
                    );
                    if (!string.IsNullOrEmpty(defaultProject))
                    {
                        var projectRepo =
                            scope.ServiceProvider.GetRequiredService<IWorkSpaceProjectRepository>();

                        // 使用异步方法更新项目状态
                        var updateResult = await projectRepo.UpdateIsEnabledAsync(
                            defaultProject,
                            true
                        );

                        if (!updateResult)
                        {
                            logger.Warn($"未找到默认项目: {defaultProject}");
                        }
                        else
                        {
                            logger.Info($"已启用默认项目: {defaultProject}");
                        }
                    }

                    // 是否启用看门狗服务
                    var isEnableWatchDogService = await globalSettingRepo.GetSettingValueAsync(
                        "IsEnableWatchDogService"
                    );
                    if (isEnableWatchDogService)
                    {
                        if (!ServiceHelper.IsServiceInstalled(ServiceName))
                        {
                            ServiceHelper.InstallService(WatchDogExePath, ServiceName);
                            ServiceHelper.SetServiceDescription();
                            ServiceHelper.StartService(ServiceName);
                            logger.Info("看门狗已启用");
                        }
                    }
                    else
                    {
                        if (ServiceHelper.IsServiceInstalled(ServiceName))
                        {
                            ServiceHelper.StopService(ServiceName);
                            ServiceHelper.UninstallService(ServiceName);
                            logger.Info("看门狗已禁用");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"初始化应用设置失败: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 记录应用启动信息
        /// </summary>
        private void LogApplicationStartup(ILoggerService<App> logger)
        {
            var env = _host!.Services.GetRequiredService<IHostEnvironment>();
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            logger.Info("========================================", true);
            logger.Info("          应用程序启动成功", true);
            logger.Info("========================================", true);
            logger.Info($"启动时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", true);
            logger.Info($"应用版本: {version}");
            logger.Info($"运行环境: {env.EnvironmentName}");
            logger.Info($"基础路径: {AppDomain.CurrentDomain.BaseDirectory}");
            logger.Info("========================================", true);
        }

        private void SetupGlobalExceptionHandling()
        {
            // 应用程序域未处理异常
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var logger = LogManager.GetLogger("UnhandledException");
                if (args.ExceptionObject is Exception ex)
                {
                    logger.Fatal(ex, "发生未处理的域异常", true);
                }
                else
                {
                    logger.Fatal(
                        "发生未处理的域异常: {0}",
                        args.ExceptionObject?.ToString() ?? "null"
                    );
                }
            };

            // WPF调度程序未处理异常
            DispatcherUnhandledException += (sender, args) =>
            {
                var logger = LogManager.GetLogger("DispatcherUnhandledException");
                logger.Fatal(args.Exception, "发生未处理的调度程序异常");

                // 标记为已处理，防止应用程序崩溃
                args.Handled = true;

                // 显示错误消息
                MessageBox.Show(
                    $"发生未处理的错误: {args.Exception.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            };

            // Task未处理异常
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                var logger = LogManager.GetLogger("UnobservedTaskException");
                logger.Error(args.Exception, "发生未观察的任务异常");
                args.SetObserved();
            };
        }

        /// <summary>
        /// 启动后台任务
        /// </summary>
        private void StartBackgroundTasks()
        {
            // 启动日志清理任务
            _ = Task.Run(async () => await RunLogCleanupTask(), _shutdownCts.Token);
        }

        /// <summary>
        /// 启动日志清理任务
        /// </summary>
        private async Task RunLogCleanupTask()
        {
            var logger = _host?.Services?.GetService<ILoggerService<App>>();
            var logManagementService = _host?.Services?.GetService<ILogManagementService>();

            // 延迟启动，确保应用完全初始化
            await Task.Delay(2000, _shutdownCts.Token);

            try
            {
                // 执行初始清理
                logger?.Info("执行本地日志清理...");
                logManagementService?.CleanupOldLogs(99);
                await CleanupDatabaseLogs(logger);
                logger?.Info("本地旧日志清理完成");
            }
            catch (Exception ex)
            {
                logger?.Error($"初始日志清理失败: {ex.Message}", ex);
            }

            // 设置每天执行的定时任务
            _logCleanupTimer.Interval = TimeSpan.FromDays(1).TotalMilliseconds; // 每24小时触发一次
            _logCleanupTimer.Elapsed += LogCleanupElapsed;
            _logCleanupTimer.Start();

            //// 定时清理循环
            //while (!_shutdownCts.Token.IsCancellationRequested)
            //{
            //    try
            //    {
            //        // 计算下次执行时间（每天凌晨2点）
            //        var now = DateTime.Now;
            //        var nextRun = now.Date.AddDays(1).AddHours(2);
            //        var delay = nextRun - now;

            //        await Task.Delay(delay, _shutdownCts.Token);

            //        if (_shutdownCts.Token.IsCancellationRequested)
            //            break;

            //        logger?.Info("执行定时日志清理...");
            //        logManagementService?.CleanupOldLogs(99);
            //        await CleanupDatabaseLogs(logger);
            //        logger?.Info("定时日志清理完成");
            //    }
            //    catch (TaskCanceledException)
            //    {
            //        break;
            //    }
            //    catch (Exception ex)
            //    {
            //        logger?.Error($"定时日志清理失败: {ex.Message}", ex);
            //    }
            //}
        }

        private void LogCleanupElapsed(object? sender, ElapsedEventArgs e)
        {
            var logger = _host?.Services?.GetService<ILoggerService<App>>();
            var logManagementService = _host?.Services?.GetService<ILogManagementService>();

            try
            {
                if (_shutdownCts.Token.IsCancellationRequested)
                    return;

                logger?.Info("执行定时日志清理...");
                logManagementService?.CleanupOldLogs(99);
                _ = CleanupDatabaseLogs(logger);
                logger?.Info("定时日志清理完成");
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger?.Error($"定时日志清理失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 清理数据库日志
        /// </summary>
        private async Task CleanupDatabaseLogs(ILoggerService<App>? logger)
        {
            if (_host == null)
                return;

            using (var scope = _host.Services.CreateScope())
            {
                try
                {
                    var config = _host.Services.GetRequiredService<IConfiguration>();
                    var appLogRetentionDays = config.GetValue<int>(
                        "LogCleanup:AppLogRetentionDays"
                    );
                    var interfaceLogRetentionDays = config.GetValue<int>(
                        "LogCleanup:InterfaceLogRetentionDays"
                    );
                    var maxDatabaseSizeMB = config.GetValue<int>("LogCleanup:MaxDatabaseSizeMB");

                    var appLogRepo = scope.ServiceProvider.GetRequiredService<IAppLogRepository>();
                    var mesLogRepo =
                        scope.ServiceProvider.GetRequiredService<IMesInterfaceLogRepository>();
                    var webLogRepo =
                        scope.ServiceProvider.GetRequiredService<IWebInterfaceLogRepository>();

                    // 清理应用日志（7天）
                    var appLogTimeBasedCount = await appLogRepo.DeleteOldLogsAsync(
                        appLogRetentionDays
                    );

                    // 检查应用日志数据库大小，如果超过100M则按大小清理
                    var databaseSizeMB = await appLogRepo.GetDatabaseSizeAsync();
                    int appLogSizeBasedCount = 0;
                    if (databaseSizeMB > maxDatabaseSizeMB)
                    {
                        appLogSizeBasedCount = await appLogRepo.DeleteLogsBySize(maxDatabaseSizeMB);
                        logger?.Info(
                            $"应用日志按大小清理完成，删除了 {appLogSizeBasedCount} 条日志，当前数据库大小: {databaseSizeMB:F2}MB"
                        );
                    }

                    // 清理MES日志（30天）
                    var mesLogCount = await mesLogRepo.DeleteOldLogsAsync(
                        interfaceLogRetentionDays
                    );

                    // 清理Web日志（30天）
                    var webLogCount = await webLogRepo.DeleteOldLogsAsync(
                        interfaceLogRetentionDays
                    );

                    logger?.Info(
                        $"数据库日志清理完成 - 应用日志(时间): {appLogTimeBasedCount}条, 应用日志(大小): {appLogSizeBasedCount}条, MES日志: {mesLogCount}条, Web日志: {webLogCount}条"
                    );
                }
                catch (Exception ex)
                {
                    logger?.Error($"数据库日志清理失败: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 初始化python环境
        /// </summary>
        private async Task InitPythonEnvironment()
        {
            var logger = _host?.Services?.GetService<ILoggerService<App>>();
            using var scope = _host!.Services.CreateScope();
            try
            {
                var globalSettingRepo =
                    scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();

                var IsInitPythonEnvironment = await globalSettingRepo.GetSettingValueAsync(
                    "IsInitPythonEnvironment"
                );
                var PythonSearchDir = await globalSettingRepo.GetSettingValueAsync(
                    "PythonSearchDir"
                );
                var PythonInstallDir = await globalSettingRepo.GetSettingValueAsync(
                    "PythonInstallDir"
                );
                if (IsInitPythonEnvironment == true)
                {
                    PythonConfigHelper.Start();
                    if (string.IsNullOrEmpty(PythonSearchDir))
                    {
                        PythonConfigHelper.InitPythonConfig(PythonInstallDir);
                    }
                    else
                    {
                        var pySearchDir = PythonSearchDir?.Split(';');
                        PythonConfigHelper.InitPythonConfig(PythonInstallDir, pySearchDir);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Error($"python模块环境初始化失败: {ex.Message}", ex);
            }
        }

        private async Task InitCTOLEnvironment()
        {
            var logger = _host?.Services?.GetService<ILoggerService<App>>();
            using var scope = _host!.Services.CreateAsyncScope();
            try
            {
                var globalSettingRepo =
                    scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                var DefaultProject = await globalSettingRepo.GetSettingValueAsync(
                    "DefaultProject"
                );
                if (!String.IsNullOrEmpty($"{DefaultProject}") && $"{DefaultProject}".Contains("CTOL"))
                {
                    // 确保 Watermark 数据库已创建
                    await scope.ServiceProvider.GetRequiredService<WatermarkDbContext>().Database.EnsureCreatedAsync();

                    // 订阅 BarcodeFinalized 事件，将每个 barcode 结果写入当天分文件数据库
                    var inspectionService = _host.Services.GetRequiredService<InspectionResultService>();
                    var groupingService = _host.Services.GetRequiredService<BarcodeGroupingService>();
                    groupingService.BarcodeFinalized += (monitorId, barcode, category) =>
                        _ = inspectionService.SaveAsync(monitorId, barcode, category);
                }
            }
            catch (Exception ex)
            {
                logger?.Error($"CTOL环境初始化失败: {ex.Message}", ex);
            }
            
        }

        /// <summary>
        /// 显示主窗口
        /// </summary>
        private void ShowMainWindow(ILoggerService<App>? logger)
        {
            var globalSettingRepo = _host!.Services.GetService<IGlobalSettingRepository>();
            if (globalSettingRepo == null)
            {
                var mainWindow = _host!.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
                return;
            }
            var appStartMainWindow = globalSettingRepo!.GetSettingValue("AppStartMainWindow");
            if (appStartMainWindow == null || string.IsNullOrEmpty(appStartMainWindow))
            {
                var isAddSuccess = globalSettingRepo!.Add(
                    new Core.Models.Data.GlobalSettingModel()
                    {
                        Name = "AppStartMainWindow",
                        Value = "MainWindow",
                        Type = "string",
                        RoleRank = 2,
                        Remark = "启动窗口",
                    }
                );
            }
            appStartMainWindow = globalSettingRepo!.GetSettingValue("AppStartMainWindow");
            if (appStartMainWindow == "MainWindow")
            {
                var mainWindow = _host!.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
                return;
            }
            if (appStartMainWindow == "CCTBMainWindow")
            {
                var mainWindow = _host!.Services.GetRequiredService<CCTBMainWindow>();
                mainWindow.Show();
                return;
            }
            throw new Exception(
                $"未能加载[{appStartMainWindow}]程序！请检查[AppStartMainWindow]数据库配置！"
            );
        }

        /// <summary>
        /// 处理启动错误
        /// </summary>
        private async Task HandleStartupError(Exception ex)
        {
            var logger = LogManager.GetLogger("AppStartup");
            logger.Fatal(ex, "应用程序启动失败");

            await ShowErrorAndExit($"应用程序启动失败:\n{ex.Message}");
        }

        /// <summary>
        /// 显示错误并退出
        /// </summary>
        private async Task ShowErrorAndExit(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);

            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }

            Shutdown();
            Environment.Exit(1);
        }

        /// <summary>
        /// 清理应用日志的结果
        /// </summary>
        private class AppLogCleanupResult
        {
            public int TimeBasedCount { get; set; }
            public int SizeBasedCount { get; set; }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                // 取消后台任务
                _shutdownCts.Cancel();
                _logCleanupTimer.Stop();

                var appLogger = _host?.Services?.GetService<ILoggerService<App>>();

                appLogger?.Info("========================================", true);
                appLogger?.Info("          应用程序正在退出", true);
                appLogger?.Info("========================================", true);
                appLogger?.Info($"退出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", true);

                // 清理日志资源
                var logManagementService = _host?.Services?.GetService<ILogManagementService>();
                logManagementService?.Flush();

                // 停止Host
                if (_host != null)
                {
                    await _host.StopAsync(TimeSpan.FromSeconds(5));
                    _host.Dispose();
                }
            }
            finally
            {
                // 最后关闭日志系统
                LogManager.Shutdown();
                base.OnExit(e);
            }
        }
    }
}
