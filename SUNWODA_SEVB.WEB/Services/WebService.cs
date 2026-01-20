using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Web;
using SUNWODA_SEVB.Tool.Helper;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace SUNWODA_SEVB.WEB.Services
{
    public class WebService : IWebService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService<WebService> _logger;
        private readonly IWebApiClient _apiClient;
        private readonly IWebConfiguration _configuration;
        private readonly BackgroundTaskService _backgroundTaskService;
        private readonly WebStateService _stateService;

        // Windows API 结构体定义
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        // 通过状态服务访问状态
        public bool IsConnected => _stateService.IsConnected;
        public bool IsDeviceBound => _stateService.IsDeviceBound;
        public bool IsRunning => _stateService.IsRunning;

        public WebService(
            IServiceProvider serviceProvider,
            ILoggerService<WebService> logger,
            IWebApiClient apiClient,
            IWebConfiguration configuration,
            BackgroundTaskService backgroundTaskService,
            WebStateService stateService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _backgroundTaskService = backgroundTaskService ?? throw new ArgumentNullException(nameof(backgroundTaskService));
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        }

        public async Task StartAsync()
        {
            if (_stateService.IsRunning)
            {
                _logger.Warn("WEB服务已在运行中");
                return;
            }

            try
            {
                _logger.Info("启动WEB服务");

                if (!await _configuration.IsWebEnabledAsync())
                {
                    _logger.Info("WEB服务未启用，跳过启动");
                    return;
                }

                await _configuration.ReloadAsync();

                var settings = _configuration.GetSettings();
                if (!settings.IsWebEnabled)
                {
                    _logger.Info("WEB服务配置为禁用状态");
                    return;
                }

                if (!string.IsNullOrEmpty(settings.UUID))
                {
                    _apiClient.SetUuid(settings.UUID);
                    _stateService.UpdateDeviceBindingStatus(true);
                }

                await _backgroundTaskService.StartAsync();

                _stateService.UpdateRunningStatus(true);
                _logger.Info("WEB服务启动成功");
            }
            catch (Exception ex)
            {
                _logger.Error("WEB服务启动失败", ex);
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!_stateService.IsRunning)
            {
                return;
            }

            try
            {
                _logger.Info("停止WEB服务");

                // 停止后台任务
                await _backgroundTaskService.StopAsync();

                _stateService.UpdateRunningStatus(false);
                _stateService.UpdateConnectionStatus(false);
                _logger.Info("WEB服务已停止");
            }
            catch (Exception ex)
            {
                _logger.Error("WEB服务停止失败", ex);
                throw;
            }
        }

        public async Task<bool> BindDeviceAsync()
        {
            try
            {
                var settings = _configuration.GetSettings();
                var networkInfo = NetworkHelper.GetNetworkInfo(settings.IpHeader);

                if (string.IsNullOrEmpty(networkInfo.MacAddress))
                {
                    _logger.Warn("无法获取MAC地址");
                    return false;
                }

                var workshipParts = settings.Workship?.Split(':') ?? new string[4];
                var request = new DeviceBindingRequest
                {
                    park = workshipParts.ElementAtOrDefault(0) ?? "",
                    stage = workshipParts.ElementAtOrDefault(1) ?? "",
                    line = workshipParts.ElementAtOrDefault(2) ?? "",
                    workship = workshipParts.ElementAtOrDefault(3) ?? "",
                    proceduce = settings.DeviceName ?? "",
                    devId = settings.DeviceSn ?? "",
                    softName = settings.ZipFileName ?? "aa",
                    softVersion = settings.SoftVersion ?? "",
                    macId = networkInfo.MacAddress,
                    ip = networkInfo.IpAddress ?? ""
                };

                var response = await _apiClient.BindDeviceAsync(request);

                if (response.IsSuccess && !string.IsNullOrEmpty(response.Data))
                {
                    _stateService.UpdateDeviceBindingStatus(true);

                    if (_configuration is WebConfiguration webConfig && webConfig != null)
                    {
                        await webConfig.UpdateUuidAsync(response.Data);
                    }
                    else
                    {
                        _logger.Warn("无法更新UUID，配置对象类型不匹配");
                    }

                    _logger.Info($"设备绑定成功，UUID: {response.Data}");
                    return true;
                }

                _logger.Warn($"设备绑定失败: {response.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("设备绑定失败", ex);
                return false;
            }
        }

        public async Task<bool> CheckVersionAsync()
        {
            try
            {
                var settings = _configuration.GetSettings();

                if (!settings.EnableVersionCheck)
                {
                    _logger.Info("版本检查功能已禁用");
                    return false;
                }

                var softName = settings.ZipFileName ?? "aa" ;
                var softVersion = settings.SoftVersion ?? "";
                var deviceSn = settings.DeviceSn ?? "";

                if (string.IsNullOrEmpty(softName) || string.IsNullOrEmpty(softVersion) || string.IsNullOrEmpty(deviceSn))
                {
                    _logger.Warn("版本检查参数不完整");
                    return false;
                }

                var response = await _apiClient.CheckVersionAsync(softName, softVersion, deviceSn);


                if (response.IsSuccess && response.Data?.NeedUpdate == true)
                {
                    _logger.Info($"发现新版本: {response.Data.Info}",true);

                    // 确保下载参数不为null
                    var savePath = settings.SaveZipFilePath ?? Path.GetTempPath();
                    var uuid = settings.UUID ?? "";

                    if (string.IsNullOrEmpty(response.Data.Info))
                    {
                        _logger.Warn("新版本信息为空");
                        return false;
                    }

                    // 下载新版本
                    var downloadResponse = await _apiClient.DownloadFileAsync(
                        softName,
                        response.Data.Info,
                        savePath,
                        softName,
                        uuid);

                    if (downloadResponse.IsSuccess)
                    {
                        _logger.Info("新版本下载成功");

                        // 解压文件
                        var zipPath = Path.Combine(savePath, softName);
                        var extractPath = settings.ZipedFolderPath ?? Path.GetTempPath();

                        // 确保解压目录存在
                        if (!Directory.Exists(extractPath))
                        {
                            Directory.CreateDirectory(extractPath);
                        }

                        // 这里应该调用解压工具，暂时注释
                        // ZipHelper.UnZip(zipPath, extractPath);

                        // 更新数据库标记
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                            await globalSettingRepo.UpdateSettingValueAsync("NeedUploadSoft", "1");
                        }

                        return true;
                    }
                }


                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("版本检查失败", ex);
                return false;
            }
        }

        public async Task<bool> UploadPcInfoAsync()
        {
            try
            {
                var settings = _configuration.GetSettings();

                if (!settings.EnablePcInfoUpload)
                {
                    _logger.Info("PC信息上传功能已禁用");
                    return false;
                }
                var uuid = settings.UUID ?? "";
                if (string.IsNullOrEmpty(uuid))
                {
                    _logger.Warn("UUID为空，无法上传PC信息");
                    return false;
                }
                var pcInfo = await CollectPcInfoAsync();
                var response = await _apiClient.UploadPcInfoAsync(pcInfo, uuid);

                if (response.IsSuccess)
                {
                    _logger.Info("PC信息上传成功");
                    return true;
                }

                _logger.Warn($"PC信息上传失败: {response.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("PC信息上传失败", ex);
                return false;
            }
        }

        private async Task<PcInfoRequest> CollectPcInfoAsync()
        {
            var settings = _configuration.GetSettings();
            var memoryInfo = await GetDetailedMemoryInfoAsync();

            return new PcInfoRequest
            {
                devId = settings.DeviceSn,
                cpu = await GetCpuUsageAsync(),
                memory = memoryInfo.PrivateMemory,  // 软件私有内存作为主要内存显示
                physicalMemory = memoryInfo.PhysicalMemory,
                virtualMemory = memoryInfo.VirtualMemory,
                managedMemory = memoryInfo.ManagedMemory,
                systemMemory = memoryInfo.SystemMemory,  // 系统内存信息
                disk = await GetDiskInfoAsync(),
                equipmentState = await GetPcStatusAsync(),
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.FFF")
            };
        }

        private async Task<string> GetCpuUsageAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var startTime = DateTime.UtcNow;
                    var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

                    Task.Delay(100).Wait();

                    var endTime = DateTime.UtcNow;
                    var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

                    var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                    var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                    var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

                    return $"{Math.Round(cpuUsageTotal * 100, 2)}%";
                }
                catch
                {
                    return "N/A";
                }
            });
        }

        private async Task<MemoryInfo> GetDetailedMemoryInfoAsync()
        {
            return await Task.Run(() =>
            {
                var memInfo = new MemoryInfo();

                try
                {
                    var currentProcess = Process.GetCurrentProcess();
                    currentProcess.Refresh();

                    // 1. 进程内存信息
                    // 私有内存（进程独占的内存）
                    var privateMemoryMB = currentProcess.PrivateMemorySize64 / (1024.0 * 1024.0);
                    memInfo.PrivateMemory = FormatMemorySize(privateMemoryMB);

                    // 物理内存（工作集）
                    var workingSetMB = currentProcess.WorkingSet64 / (1024.0 * 1024.0);
                    var peakWorkingSetMB = currentProcess.PeakWorkingSet64 / (1024.0 * 1024.0);
                    memInfo.PhysicalMemory = $"{FormatMemorySize(workingSetMB)} (峰值: {FormatMemorySize(peakWorkingSetMB)})";

                    // 虚拟内存
                    var virtualMemoryMB = currentProcess.PagedMemorySize64 / (1024.0 * 1024.0);
                    var peakVirtualMemoryMB = currentProcess.PeakPagedMemorySize64 / (1024.0 * 1024.0);
                    memInfo.VirtualMemory = $"{FormatMemorySize(virtualMemoryMB)} (峰值: {FormatMemorySize(peakVirtualMemoryMB)})";

                    // GC托管内存
                    var managedMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                    var gen0 = GC.CollectionCount(0);
                    var gen1 = GC.CollectionCount(1);
                    var gen2 = GC.CollectionCount(2);
                    memInfo.ManagedMemory = $"{FormatMemorySize(managedMemoryMB)} (GC: Gen0={gen0}, Gen1={gen1}, Gen2={gen2})";

                    // 2. 系统内存信息
                    MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                    memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

                    if (GlobalMemoryStatusEx(ref memStatus))
                    {
                        var totalSystemMB = memStatus.ullTotalPhys / (1024.0 * 1024.0);
                        var availableSystemMB = memStatus.ullAvailPhys / (1024.0 * 1024.0);
                        var usedSystemMB = totalSystemMB - availableSystemMB;
                        var usagePercent = memStatus.dwMemoryLoad;

                        memInfo.SystemMemory = $"{FormatMemorySize(usedSystemMB)}/{FormatMemorySize(totalSystemMB)} ({usagePercent}%)";

                        // 计算进程占系统内存的百分比
                        var processPercent = workingSetMB / totalSystemMB * 100;
                        memInfo.ProcessMemoryPercent = $"{processPercent:F2}%";
                    }
                    else
                    {
                        memInfo.SystemMemory = "N/A";
                        memInfo.ProcessMemoryPercent = "N/A";
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug($"获取内存信息失败: {ex.Message}");

                    // 设置默认值
                    memInfo.PrivateMemory = "N/A";
                    memInfo.PhysicalMemory = "N/A";
                    memInfo.VirtualMemory = "N/A";
                    memInfo.ManagedMemory = "N/A";
                    memInfo.SystemMemory = "N/A";
                    memInfo.ProcessMemoryPercent = "N/A";
                }

                return memInfo;
            });
        }

        /// <summary>
        /// 格式化内存大小显示
        /// 小于1024MB显示为MB，大于等于1024MB显示为GB
        /// </summary>
        /// <param name="memoryInMB">内存大小（MB）</param>
        /// <returns>格式化后的字符串</returns>
        private string FormatMemorySize(double memoryInMB)
        {
            if (memoryInMB < 1024)
            {
                return $"{memoryInMB:F2}MB";
            }
            else
            {
                var memoryInGB = memoryInMB / 1024.0;
                return $"{memoryInGB:F2}GB";
            }
        }


        private async Task<string> GetDiskInfoAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var settings = _configuration.GetSettings();
                    var sqlPath = settings.SqlPath ?? @"C:\Program Files\MariaDB 10.3\data";

                    // 获取根路径
                    var rootPath = Path.GetPathRoot(sqlPath);
                    if (string.IsNullOrEmpty(rootPath))
                    {
                        rootPath = "C:\\";
                    }

                    // 检查驱动器是否存在
                    if (!Directory.Exists(rootPath))
                    {
                        _logger.Warn($"驱动器路径不存在: {rootPath}");
                        return "磁盘信息不可用";
                    }

                    var driveInfo = new DriveInfo(rootPath);

                    // 检查驱动器是否就绪
                    if (!driveInfo.IsReady)
                    {
                        return $"磁盘 {driveInfo.Name} 未就绪";
                    }

                    var totalSize = driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    var freeSpace = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    var usedSpace = totalSize - freeSpace;
                    var usagePercent = totalSize > 0 ? (usedSpace / totalSize) * 100 : 0;                  
                    
                    return $"磁盘: {driveInfo.Name}, 大小: {totalSize:F2}GB, 已用: {usedSpace:F2}GB, 使用率: {usagePercent:F2}%";
                }
                catch (Exception ex)
                {
                    _logger.Debug($"获取磁盘信息失败: {ex.Message}");
                    return "磁盘信息获取失败";
                }
            });
        }
        /// <summary>
        /// PLC状态获取（模拟实现）
        /// </summary>
        /// <returns></returns>
        private async Task<string> GetPcStatusAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // TODO: 从PC服务获取实际状态
                    // 暂时返回固定值
                    return "正常";
                }
                catch (Exception ex)
                {
                    _logger.Debug($"获取PC状态失败: {ex.Message}");
                    return "未知";
                }
            });
        }
        /// <summary>
        /// 内部类：内存信息
        /// </summary>
        private class MemoryInfo
        {
            public string PrivateMemory { get; set; } = "N/A";
            public string PhysicalMemory { get; set; } = "N/A";
            public string VirtualMemory { get; set; } = "N/A";
            public string ManagedMemory { get; set; } = "N/A";
            public string SystemMemory { get; set; } = "N/A";
            public string ProcessMemoryPercent { get; set; } = "N/A";
        }


    }
}