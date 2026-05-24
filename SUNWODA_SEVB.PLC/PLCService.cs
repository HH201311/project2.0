using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.ModBus;
using HslCommunication.Profinet.Inovance;
using HslCommunication.Profinet.Keyence;
using HslCommunication.Profinet.Melsec;
using HslCommunication.Profinet.Omron;
using HslCommunication.Profinet.Panasonic;
using HslCommunication.Profinet.Siemens;
using Org.BouncyCastle.Utilities;
using SUNWODA_SEVB.Core.Enumerations.PLC;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Tool.Extension;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SUNWODA_SEVB.PLC
{
    public class PLCService : IPLCService, IDisposable, IAsyncDisposable
    {
        private readonly ILoggerService<PLCService> _logger;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IPLCRWConfigRepository _plcRWConfigRepository;
        private readonly IPLCAddressConfigRepository _plcAddressConfigRepository;
        private readonly IGlobalSettingRepository _globalSettingRepository;

        private readonly ConcurrentDictionary<int, PLCDevice> _plcs = new();
        private readonly ConcurrentDictionary<int, PLCRWConfigModel> _rwConfigs = new();
        private readonly ConcurrentDictionary<int, PLCRWAddress> _rwAddresses = new();
        private readonly ConcurrentDictionary<string, ConnectInfo> _connectionStatus = new();

        private readonly CancellationTokenSource _serviceCts = new();
        private readonly List<Task> _runningTasks = new();
        private readonly SemaphoreSlim _initSemaphore = new(1, 1);

        private volatile bool _isInitialized;
        private volatile bool _disposed;

        // 编译正则表达式以提高性能
        private static readonly Regex AddressNumberRegex = new(@"[^\d.\d]", RegexOptions.Compiled);
        private static readonly Regex AddressArrayNumberRegex = new(@"^.*\[(\d+)\]$", RegexOptions.Compiled);
        private static readonly Regex NumericRegex = new(
            @"^[+-]?\d*[.]?\d*$",
            RegexOptions.Compiled
        );

        public bool IsInitialized => _isInitialized;
        public IReadOnlyDictionary<int, PLCDevice> PLCs => _plcs;
        public IReadOnlyDictionary<string, ConnectInfo> ConnectionStatus => _connectionStatus;
        public IReadOnlyDictionary<int, PLCRWAddress> RWAddresses => _rwAddresses;

        private bool IsCycleReadPLC { get; set; }

        private bool IsCycleWritePLC { get; set; }

        private int PLCConnectTime { get; set; }

        public PLCService(
            ILoggerService<PLCService> logger,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IPLCRWConfigRepository plcRWConfigRepository,
            IPLCAddressConfigRepository plcAddressConfigRepository
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _globalSettingRepository =
                globalSettingRepository
                ?? throw new ArgumentNullException(nameof(globalSettingRepository));
            _plcConfigRepository =
                plcConfigRepository ?? throw new ArgumentNullException(nameof(plcConfigRepository));
            _plcRWConfigRepository =
                plcRWConfigRepository
                ?? throw new ArgumentNullException(nameof(plcRWConfigRepository));
            _plcAddressConfigRepository =
                plcAddressConfigRepository
                ?? throw new ArgumentNullException(nameof(plcAddressConfigRepository));
        }

        // <summary>
        /// 初始化PLC服务
        /// </summary>
        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PLCService));

            await _initSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_isInitialized)
                    return true;

                _logger.Info("开始初始化PLC服务");

                if (!Authorization.SetAuthorizationCode("04f14563-6515-49be-a713-3ece0d21cc3e"))
                {
                    _logger.Error("HSL激活失败", true);
                    return false;
                }

                IsCycleReadPLC =
                    await _globalSettingRepository.GetSettingValueAsync("IsCycleReadPLC") ?? false;
                IsCycleWritePLC =
                    await _globalSettingRepository.GetSettingValueAsync("IsCycleWritePLC") ?? false;
                PLCConnectTime =
                    await _globalSettingRepository.GetSettingValueAsync("PLCConnectTime") ?? 5000;

                // 加载配置
                await LoadConfigurationsAsync(cancellationToken);

                if (IsCycleReadPLC || IsCycleWritePLC)
                {
                    await InitializeConnectionsAsync(cancellationToken);
                }

                // 启动后台服务
                StartBackgroundServices();

                _isInitialized = true;
                _logger.Info("PLC服务初始化完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("初始化PLC服务失败", ex, true);
                return false;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private async Task LoadConfigurationsAsync(CancellationToken cancellationToken)
        {
            var plcConfigs = await _plcConfigRepository.GetEnabledConfigsAsync();
            var loadTasks = plcConfigs.Select(async plcConfig =>
            {
                try
                {
                    // 创建PLC设备
                    var plc = CreatePLC(plcConfig);
                    _plcs[plcConfig.ID] = plc;
                    _connectionStatus[plcConfig.Name] = new ConnectInfo(plcConfig.Name, false);

                    // 加载读写配置
                    var rwConfigs = await _plcRWConfigRepository.GetEnabledConfigsAsync(
                        plcConfig.ID
                    );
                    foreach (var rwConfig in rwConfigs)
                    {
                        _rwConfigs[rwConfig.ID] = rwConfig;

                        var rwBaseAddress = new PLCRWAddress(
                            rwConfig.ID,        // 使用配置ID作为地址ID
                            rwConfig.PLCID,
                            rwConfig.ID,
                            1,                  // CategoryID 默认为1
                            rwConfig.AreaName,      // 使用配置名称作为参数名
                            rwConfig.RWMode,           
                            (ushort)rwConfig.Length,
                            rwConfig.StartAddress,
                            "",
                            rwConfig.Name ?? "",
                            0,                  // 偏移量为0
                            0                   // 位偏移为0
                        );

                        // 确保不重复添加
                        _rwAddresses[rwConfig.ID] = rwBaseAddress;
                    }

                    // 加载地址配置
                    var addressConfigs = await _plcAddressConfigRepository.GetMonitorAddressesAsync(
                        plcConfig.ID
                    );
                    foreach (var addressConfig in addressConfigs)
                    {
                        if (_rwConfigs.TryGetValue(addressConfig.PLCRWID, out var rwConfig))
                        {
                            if (!plcConfig.BrandSpecificationProtocal.Contains("CIP"))
                            {
                                var rwAddress = new PLCRWAddress(
                                    addressConfig.ID,
                                    addressConfig.PLCID,
                                    addressConfig.PLCRWID,
                                    addressConfig.CategoryID,
                                    addressConfig.ParameterName,
                                    addressConfig.Type,
                                    addressConfig.Length,
                                    addressConfig.Address,
                                    addressConfig.Unit,
                                    addressConfig.Remark,
                                    GetAddressNumber(addressConfig.Address)
                                        - rwConfig.StartAddress.ToInt(),
                                    GetAddressBitNumber(addressConfig.Address)
                                );
                                _rwAddresses[addressConfig.ID] = rwAddress;
                            }
                            else
                            {
                                var rwAddress = new PLCRWAddress(
                                    addressConfig.ID,
                                    addressConfig.PLCID,
                                    addressConfig.PLCRWID,
                                    addressConfig.CategoryID,
                                    addressConfig.ParameterName,
                                    addressConfig.Type,
                                    addressConfig.Length,
                                    addressConfig.Address,
                                    addressConfig.Unit,
                                    addressConfig.Remark,
                                    (GetArrayAddressNumber(addressConfig.Address)
                                        - rwConfig.StartAddress.ToInt()) * plc.AddressWordLength
                                );
                                _rwAddresses[addressConfig.ID] = rwAddress;
                            }
                        }
                    }

                    _logger.Info($"加载PLC配置完成: {plcConfig.Name}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"加载PLC配置失败: {plcConfig.Name}", ex, true);
                }
            });

            await Task.WhenAll(loadTasks);

            _logger.Info($"加载完成 - PLC设备数量: {_plcs.Count}, 地址数量: {_rwAddresses.Count}");
        }

        /// <summary>
        /// 创建PLC设备
        /// </summary>
        private PLCDevice CreatePLC(PLCConfigModel plcConfig)
        {
            var brandProtocol = ParsePLCBrandProtocol(plcConfig.BrandSpecificationProtocal);
            var dataSortRule = ParseDataSortRule(plcConfig.DataSortRule);

            var plc = new PLCDevice(
                plcConfig.ID,
                plcConfig.Name,
                brandProtocol,
                plcConfig.IP,
                plcConfig.Port,
                dataSortRule,
                plcConfig.CycleReadTime,
                plcConfig.CycleWriteTime
            );

            ConfigureDeviceConnection(plc);
            return plc;
        }

        public PLCDevice? GetPLCByName(string name)
        {
            if (PLCs != null)
            {
                return PLCs.Values.FirstOrDefault(o => o.Name == name);
            }
            return null;
        }

        /// <summary>
        /// 配置设备连接
        /// </summary>
        private void ConfigureDeviceConnection(PLCDevice plc)
        {
            plc.Device = plc.BrandSpecificationProtocal switch
            {
                PLCBrandSpecificationProtocal.Siemens_S7NetS1500 => ConfigureSiemens(
                    plc,
                    SiemensPLCS.S1500
                ),
                PLCBrandSpecificationProtocal.Siemens_S7NetS1200 => ConfigureSiemens(
                    plc,
                    SiemensPLCS.S1200
                ),
                PLCBrandSpecificationProtocal.Siemens_S7NetS400 => ConfigureSiemens(
                    plc,
                    SiemensPLCS.S400,
                    0,
                    3
                ),
                PLCBrandSpecificationProtocal.Siemens_S7NetS300 => ConfigureSiemens(
                    plc,
                    SiemensPLCS.S300
                ),
                PLCBrandSpecificationProtocal.Siemens_S7NetS200 => ConfigureSiemens(
                    plc,
                    SiemensPLCS.S200
                ),
                PLCBrandSpecificationProtocal.Siemens_S7NetS200Smart => ConfigureSiemens(
                    plc,
                    SiemensPLCS.S200Smart
                ),
                PLCBrandSpecificationProtocal.Omron_Fins_TCP => ConfigureOmron(plc),
                PLCBrandSpecificationProtocal.Omron_Connected_CIP => ConfigureOmron(plc),
                PLCBrandSpecificationProtocal.Melsec_MC_Binary => ConfigureMelsec(plc),
                PLCBrandSpecificationProtocal.Melsec_MC_Ascii => ConfigureMelsec(plc),
                PLCBrandSpecificationProtocal.Melsec_A1E_Binary => ConfigureMelsec(plc),
                PLCBrandSpecificationProtocal.Panasonic_Mewtocol_OverTcp => ConfigurePanasonic(plc),
                PLCBrandSpecificationProtocal.Keyence_MC_Binary => ConfigureKeyence(plc),
                PLCBrandSpecificationProtocal.Keyence_MC_Ascii => ConfigureKeyence(plc),
                PLCBrandSpecificationProtocal.Inovance_AM_Tcp => ConfigureInovance(
                    plc,
                    InovanceSeries.AM
                ),
                PLCBrandSpecificationProtocal.Inovance_H3U_Tcp => ConfigureInovance(
                    plc,
                    InovanceSeries.H3U
                ),
                PLCBrandSpecificationProtocal.Inovance_H5U_Tcp => ConfigureInovance(
                    plc,
                    InovanceSeries.H5U
                ),
                PLCBrandSpecificationProtocal.Modbus_Tcp => ConfigureModbus(plc),
                _ => throw new NotSupportedException(
                    $"不支持的PLC协议: {plc.BrandSpecificationProtocal}"
                ),
            };
        }

        /// <summary>
        /// 初始化所有PLC连接
        /// </summary>
        private async Task InitializeConnectionsAsync(CancellationToken cancellationToken)
        {
            _logger.Info("开始初始化PLC连接");

            var connectTasks = _plcs.Values.Select(async plc =>
            {
                try
                {
                    _logger.Info($"正在连接PLC: {plc.Name}");
                    var connected = await ConnectWithRetryAsync(plc, 3, cancellationToken);
                    if (connected)
                    {
                        _logger.Info($"PLC连接成功: {plc.Name}");
                    }
                    else
                    {
                        _logger.Warn($"PLC连接失败，将在后台重试: {plc.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"初始化PLC连接异常: {plc.Name}", ex, true);
                }
            });

            await Task.WhenAll(connectTasks);
        }

        /// <summary>
        /// 带重试的连接方法
        /// </summary>
        private async Task<bool> ConnectWithRetryAsync(
            PLCDevice plc,
            int maxRetries,
            CancellationToken cancellationToken
        )
        {
            for (int i = 0; i < maxRetries; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                try
                {
                    var result = await Task.Run(
                        async () =>
                            await (
                                plc.Device is not null
                                    ? plc.Device.ConnectServerAsync()
                                    : Task.FromResult(new OperateResult("Device属性为空"))
                            ),
                        cancellationToken
                    );

                    plc.IsConnect = result?.IsSuccess ?? false;

                    if (plc.IsConnect)
                    {
                        return true;
                    }

                    _logger.Warn(
                        $"连接尝试 {i + 1}/{maxRetries} 失败: {plc.Name}, 原因: {result?.Message}",
                        true
                    );

                    // 根据不同的PLC类型使用不同的重试延迟
                    var delay = GetRetryDelay(plc.BrandSpecificationProtocal, i);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.Error($"连接PLC异常 (尝试 {i + 1}/{maxRetries}): {plc.Name}", ex, true);
                    await Task.Delay(2000, cancellationToken);
                }
            }

            return false;
        }

        /// <summary>
        /// 根据PLC类型获取重试延迟
        /// </summary>
        private int GetRetryDelay(PLCBrandSpecificationProtocal protocol, int retryCount)
        {
            // 需要自定义PLC重试连接时间可在此设置
            return protocol switch
            {
                _ => (retryCount + 1) * PLCConnectTime, // 默认5秒
            };
        }

        /// <summary>
        /// 启动后台服务
        /// </summary>
        private void StartBackgroundServices()
        {
            foreach (var plc in _plcs.Values)
            {
                // 读取任务
                if (IsCycleReadPLC)
                {
                    var readTask = Task.Run(
                        async () => await ReadCycleAsync(plc, _serviceCts.Token)
                    );
                    _runningTasks.Add(readTask);
                }

                // 写入任务
                if (IsCycleWritePLC)
                {
                    var writeTask = Task.Run(
                        async () => await WriteCycleAsync(plc, _serviceCts.Token)
                    );
                    _runningTasks.Add(writeTask);
                }
            }

            // 状态监控任务
            var monitorTask = Task.Run(
                async () => await MonitorConnectionsAsync(_serviceCts.Token)
            );
            _runningTasks.Add(monitorTask);
        }

        /// <summary>
        /// 读取循环
        /// </summary>
        private async Task ReadCycleAsync(PLCDevice plc, CancellationToken cancellationToken)
        {
            var sw = new Stopwatch();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    sw.Restart();
                    await Task.Delay(plc.CycleReadTime, cancellationToken);

                    if (!await EnsureConnectedAsync(plc, cancellationToken))
                        continue;

                    var rwConfigs = _rwConfigs.Values.Where(c =>
                        c.PLCID == plc.ID
                        && string.Equals(c.RWMode, "R", StringComparison.OrdinalIgnoreCase)
                    );

                    await Parallel.ForEachAsync(
                        rwConfigs,
                        cancellationToken,
                        async (rwConfig, ct) =>
                        {
                            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            cts.CancelAfter(TimeSpan.FromMilliseconds(plc.CycleReadTime));
                            try
                            {
                                var task = ReadAddressRangeAsync(plc, rwConfig, ct);
                                await task.WaitAsync(cts.Token);
                            }
                            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                            {
                                _logger.Error($"读取地址段失败[超时(PLC读取循环时间):{plc.CycleReadTime} ms]: {rwConfig.Name}", true);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"读取地址段失败: {rwConfig.Name}", ex, true);
                            }
                        }
                    );

                    sw.Stop();
                    if (sw.ElapsedMilliseconds - plc.CycleReadTime > plc.CycleReadTime)
                        _logger.Debug($"PLC[{plc.Name}]读取循环耗时: {sw.ElapsedMilliseconds} ms");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("PLC读取循环异常: {plc.Name}", ex, true);
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }

        /// <summary>
        /// 读取地址范围
        /// </summary>
        private async Task ReadAddressRangeAsync(
            PLCDevice plc,
            PLCRWConfigModel rwConfig,
            CancellationToken cancellationToken
        )
        {
            string address = "";
            if (rwConfig.AddressType == 0) // 标签地址
            {
                address = $"{rwConfig.AreaName}";
            }
            else if (rwConfig.AddressType == 1) // 地址块
            {
                address = $"{rwConfig.AreaName}{rwConfig.StartAddress}";
            }
            else // 默认
            {
                address = $"{rwConfig.AreaName}{rwConfig.StartAddress}";
            }
            //var result = await Task.Run(
            //    () => plc.Device?.Read(address, (ushort)rwConfig.Length),
            //    cancellationToken
            //);

            // 不能触发cancellationToken取消操作，修改为下面的方式执行     -By2210120078  -20260522
            //var result = await Task.Run(
            //    async () =>
            //        await (plc.Device is not null
            //                ? plc.Device.ReadAsync(address, (ushort)rwConfig.Length)
            //                : Task.FromResult(new OperateResult<byte[]>() { IsSuccess = false, Message = "Device属性为空", })
            //        ),
            //    cancellationToken
            //);

            // 直接使用WaitAsync等待PLC读取任务完成或超时，避免PLC读取操作无法响应cancellationToken取消的情况     -By2210120078  -20260522
            //var task = plc.Device is not null
            //    ? plc.Device.ReadAsync(address, (ushort)rwConfig.Length)
            //    : Task.FromResult(new OperateResult<byte[]>() { IsSuccess = false, Message = "Device属性为空" });
            //var result = await task.WaitAsync(cancellationToken);

            // 替换为同步方法Read，尝试解决ReadAsync偶发读取耗时过长问题     -By2210120078  -20260522
            var task = plc.Device is not null
                ? Task.Run(() => plc.Device.Read(address, (ushort)rwConfig.Length))
                : Task.FromResult(new OperateResult<byte[]>() { IsSuccess = false, Message = "Device属性为空" });
            var result = await task.WaitAsync(cancellationToken);

            plc.IsConnect = result?.IsSuccess ?? false;
            if (plc.IsConnect)
            {
                UpdateAddressValues(plc, rwConfig, result!.Content);
            }
            else
            {
                _logger.Warn($"读取失败: {address}, 原因: {result?.Message}", true);
            }
        }

        public async Task<bool> ReadAddressAsync(PLCDevice plc, PLCRWAddress address, int bitIndex)
        {
            var result = await plc.ReadAsync(address.Address!, address.Length);

            plc.IsConnect = result?.IsSuccess ?? false;
            if (plc.IsConnect)
            {
                try
                {
                    address.MonitorValue = ConvertValue(
                        result!.Content,
                        address.Type,
                        plc.ByteTransform,
                        plc.AddressWordLength,
                        0,
                        bitIndex,
                        address.Length
                    );
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error($"解析地址数据失败: {address.ParameterName}", ex, true);
                    return false;
                }
            }
            else
            {
                _logger.Warn($"读取失败: {address.Address}, 原因: {result?.Message}", true);
                return false;
            }
        }

        /// <summary>
        /// 更新地址值
        /// </summary>
        private void UpdateAddressValues(PLCDevice plc, PLCRWConfigModel rwConfig, byte[] data)
        {
            var addresses = _rwAddresses.Values.Where(a => a.AddressRangeId == rwConfig.ID);

            Parallel.ForEach(
                addresses,
                address =>
                {
                    try
                    {
                        if (rwConfig.AddressType == 0 && address.Type?.ToUpperInvariant() == "STRING")
                        {
                            ushort len = BitConverter.ToUInt16(data, 0);
                            address.MonitorValue = Encoding.UTF8.GetString(data, 2, len);
                        }
                        else
                        {
                            address.MonitorValue = ConvertValue(
                            data,
                            address.Type,
                            plc.ByteTransform,
                            plc.AddressWordLength,
                            address.Index,
                            address.BitIndex,
                            address.Length
                        );
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"解析地址数据失败: {address.ParameterName}", ex, true);
                    }
                }
            );
        }

        /// <summary>
        /// 写入循环
        /// </summary>
        private async Task WriteCycleAsync(PLCDevice plc, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(plc.CycleWriteTime, cancellationToken);

                    if (!await EnsureConnectedAsync(plc, cancellationToken))
                        continue;

                    var rwConfigs = _rwConfigs.Values.Where(c =>
                        c.PLCID == plc.ID
                        && string.Equals(c.RWMode, "W", StringComparison.OrdinalIgnoreCase)
                    );

                    foreach (var rwConfig in rwConfigs)
                    {
                        var addresses = _rwAddresses.Values.Where(a =>
                            a.AddressRangeId == rwConfig.ID && a.Index < rwConfig.Length
                        );

                        await Parallel.ForEachAsync(
                            addresses,
                            cancellationToken,
                            async (address, ct) =>
                            {
                                try
                                {
                                    await WriteAddressAsync(plc, address, ct);
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error(
                                        $"写入地址失败: {address.ParameterName}",
                                        ex,
                                        true
                                    );
                                }
                            }
                        );
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error($"PLC写入循环异常: {plc.Name}", ex, true);
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }

        /// <summary>
        /// 写入地址
        /// </summary>
        private async Task WriteAddressAsync(
            PLCDevice plc,
            PLCRWAddress address,
            CancellationToken cancellationToken
        )
        {
            if (address.MonitorValue == null)
                return;

            var result = await Task.Run(
                async () =>
                    await plc.WriteAsync(address.Address, address.Type, address.MonitorValue),
                cancellationToken
            );

            if (!result.IsSuccess)
            {
                _logger.Warn(
                    $"写入失败: {address.Address}, 值: {address.MonitorValue}, 原因: {result.Message}",
                    true
                );
            }
        }

        /// <summary>
        /// 写入地址
        /// </summary>
        /// <param name="plc"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        public async Task WriteAddressAsync(PLCDevice plc, PLCRWAddress address)
        {
            if (address.MonitorValue == null)
                return;

            var result = await plc.WriteAsync(address.Address, address.Type, address.MonitorValue);

            if (!result.IsSuccess)
            {
                _logger.Warn(
                    $"写入失败: {address.Address}, 值: {address.MonitorValue}, 原因: {result.Message}",
                    true
                );
            }
        }

        /// <summary>
        /// 监控连接状态
        /// </summary>
        private async Task MonitorConnectionsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var plc in _plcs.Values)
                    {
                        if (_connectionStatus.TryGetValue(plc.Name, out var status))
                        {
                            status.Status = plc.IsConnect;
                        }
                    }

                    try
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug("监控连接状态延时异常", ex);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("监控连接状态异常", ex, true);
                }
            }
        }

        /// <summary>
        /// 确保连接
        /// </summary>
        private async Task<bool> EnsureConnectedAsync(PLCDevice plc, CancellationToken cancellationToken)
        {
            if (plc.IsConnect)
                return true;

            try
            {
                var sw = new Stopwatch();
                sw.Start();
                // 使用带重试的连接
                var connected = await ConnectWithRetryAsync(plc, 2, cancellationToken);

                if (!connected)
                {
                    _logger.Warn($"无法建立PLC连接: {plc.Name}");
                }

                sw.Stop();
                _logger.Debug($"{plc.Name}执行重连[耗时{sw.ElapsedMilliseconds} ms]：{(connected ? "成功" : "失败")}");

                return connected;
            }
            catch (Exception ex)
            {
                _logger.Error("连接PLC异常: {plc.Name}", ex, true);
                return false;
            }
        }

        /// <summary>
        /// 转换值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static dynamic? ConvertValue(
            byte[] bytes,
            string? dataType,
            IByteTransform? byteTransform,
            int addressWordLength,
            int index,
            int bitIndex,
            int length = 1
        )
        {
            if (byteTransform == null || string.IsNullOrEmpty(dataType))
                return null;

            var offset = index * addressWordLength;
            var type = dataType.ToUpperInvariant();

            return type switch
            {
                "STRING" => length == 1
                    ? byteTransform.TransString(bytes, offset, length, Encoding.UTF8)[0]
                    : byteTransform.TransString(bytes, offset, length, Encoding.UTF8),
                "FLOAT" => length == 1
                    ? byteTransform.TransSingle(bytes, offset, length)[0]
                    : byteTransform.TransSingle(bytes, offset, length),
                "DOUBLE" => length == 1
                    ? byteTransform.TransDouble(bytes, offset, length)[0]
                    : byteTransform.TransDouble(bytes, offset, length),
                "BYTE" => length == 1
                    ? byteTransform.TransByte(bytes, offset, length)[0]
                    : byteTransform.TransByte(bytes, offset, length),
                "SHORT" => length == 1
                    ? byteTransform.TransInt16(bytes, offset, length)[0]
                    : byteTransform.TransInt16(bytes, offset, length),
                "USHORT" => length == 1
                    ? byteTransform.TransUInt16(bytes, offset, length)[0]
                    : byteTransform.TransUInt16(bytes, offset, length),
                "INT" => length == 1
                    ? byteTransform.TransInt32(bytes, offset, length)[0]
                    : byteTransform.TransInt32(bytes, offset, length),
                "UINT" => length == 1
                    ? byteTransform.TransUInt32(bytes, offset, length)[0]
                    : byteTransform.TransUInt32(bytes, offset, length),
                "LONG" => length == 1
                    ? byteTransform.TransInt64(bytes, offset, length)[0]
                    : byteTransform.TransInt64(bytes, offset, length),
                "ULONG" => length == 1
                    ? byteTransform.TransUInt64(bytes, offset, length)[0]
                    : byteTransform.TransUInt64(bytes, offset, length),
                "BOOL" => length == 1
                    ? byteTransform.TransBool(
                        BitConverter.GetBytes(byteTransform.TransInt16(bytes, offset)),
                        offset,
                        bitIndex + 1
                    )[bitIndex]
                    : byteTransform
                        .TransBool(
                            BitConverter.GetBytes(byteTransform.TransInt16(bytes, offset)),
                            offset,
                            bitIndex + 1 + length
                        )
                        .Skip(bitIndex)
                        .Take(length)
                        .ToArray(),
                _ => null,
            };
        }

        /// <summary>
        /// 获取地址编号
        /// </summary>
        private static int GetAddressNumber(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return 0;

            if (source.Contains("DB", StringComparison.OrdinalIgnoreCase))
            {
                source = AddressNumberRegex.Replace(source, "");
                var parts = source.Split('.');
                if (parts.Length >= 2 && decimal.TryParse(parts[1], out var value))
                {
                    return (int)Math.Floor(value);
                }
            }
            else
            {
                source = AddressNumberRegex.Replace(source, "");
                if (!string.IsNullOrWhiteSpace(source) && NumericRegex.IsMatch(source))
                {
                    if (decimal.TryParse(source, out var value))
                    {
                        return (int)Math.Floor(value);
                    }
                }
            }

            return 0;
        }

        private static int GetArrayAddressNumber(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return 0;

            var match = AddressArrayNumberRegex.Match(source);

            if (match.Success)
            {
                string number = match.Groups[1].Value;  // 获取数字部分

                if (number != null && decimal.TryParse(number, out var value))
                {
                    return (int)Math.Floor(value);
                }
            }

            return 0;
        }

        private static int GetAddressBitNumber(string source)
        {
            string[] result = source.Split('.');
            if (result.Length > 2)
                return int.Parse(result[2]);
            else if (result.Length == 2)
                return int.Parse(result[1]);
            else
                return 0;
        }

        // 辅助方法
        private static PLCBrandSpecificationProtocal ParsePLCBrandProtocol(string? value) =>
            value?.ToUpperInvariant() switch
            {
                "SIEMENS_S7NETS200" => PLCBrandSpecificationProtocal.Siemens_S7NetS200,
                "SIEMENS_S7NETS200SMART" => PLCBrandSpecificationProtocal.Siemens_S7NetS200Smart,
                "SIEMENS_S7NETS300" => PLCBrandSpecificationProtocal.Siemens_S7NetS300,
                "SIEMENS_S7NETS400" => PLCBrandSpecificationProtocal.Siemens_S7NetS400,
                "SIEMENS_S7NETS1200" => PLCBrandSpecificationProtocal.Siemens_S7NetS1200,
                "SIEMENS_S7NETS1500" => PLCBrandSpecificationProtocal.Siemens_S7NetS1500,
                "OMRON_FINS_TCP" => PLCBrandSpecificationProtocal.Omron_Fins_TCP,
                "OMRON_CONNECTED_CIP" => PLCBrandSpecificationProtocal.Omron_Connected_CIP,
                "MELSEC_MC_BINARY" => PLCBrandSpecificationProtocal.Melsec_MC_Binary,
                "MELSEC_MC_ASCII" => PLCBrandSpecificationProtocal.Melsec_MC_Ascii,
                "MELSEC_A1E_BINARY" => PLCBrandSpecificationProtocal.Melsec_A1E_Binary,
                "PANASONIC_MEWTOCOL_OVERTCP" =>
                    PLCBrandSpecificationProtocal.Panasonic_Mewtocol_OverTcp,
                "KEYENCE_MC_BINARY" => PLCBrandSpecificationProtocal.Keyence_MC_Binary,
                "KEYENCE_MC_ASCII" => PLCBrandSpecificationProtocal.Keyence_MC_Ascii,
                "INOVANCE_AM_TCP" => PLCBrandSpecificationProtocal.Inovance_AM_Tcp,
                "INOVANCE_H3U_TCP" => PLCBrandSpecificationProtocal.Inovance_H3U_Tcp,
                "INOVANCE_H5U_TCP" => PLCBrandSpecificationProtocal.Inovance_H5U_Tcp,
                "MODBUS_TCP" => PLCBrandSpecificationProtocal.Modbus_Tcp,
                _ => throw new FormatException($"未找到PLC型号: {value}"),
            };

        private static DataSortRule ParseDataSortRule(string? value) =>
            value?.ToUpperInvariant() switch
            {
                "ABCD" => DataSortRule.ABCD,
                "BADC" => DataSortRule.BADC,
                "CDAB" => DataSortRule.CDAB,
                "DCBA" => DataSortRule.DCBA,
                _ => DataSortRule.Default,
            };

        // 设备配置方法
        private static DeviceTcpNet ConfigureSiemens(
            PLCDevice plc,
            SiemensPLCS type,
            byte rack = 0,
            byte slot = 0
        )
        {
            var siemens = new SiemensS7Net(type, plc.IP) { Port = plc.Port };
            if (type == SiemensPLCS.S400)
            {
                if (rack > 0)
                    siemens.Rack = rack;
                if (slot > 0)
                    siemens.Slot = slot;
            }
            plc.ByteTransform = siemens.ByteTransform;
            plc.AddressWordLength = 1;
            return siemens;
        }

        private static DeviceTcpNet? ConfigureOmron(PLCDevice plc)
        {
            switch (plc.BrandSpecificationProtocal)
            {
                case PLCBrandSpecificationProtocal.Omron_Fins_TCP:
                    {
                        var omron = new OmronFinsNet(plc.IP, plc.Port);
                        if (plc.ByteDataFormat is not null)
                            omron.ByteTransform.DataFormat = (DataFormat)plc.ByteDataFormat;
                        plc.ByteTransform = omron.ByteTransform;
                        plc.AddressWordLength = 2;
                        return omron;
                    }
                case PLCBrandSpecificationProtocal.Omron_Connected_CIP:
                    {
                        var omron = new OmronConnectedCipNet(plc.IP, plc.Port);
                        if (plc.ByteDataFormat is not null)
                            omron.ByteTransform.DataFormat = (DataFormat)plc.ByteDataFormat;
                        plc.ByteTransform = omron.ByteTransform;
                        plc.AddressWordLength = 2;
                        return omron;
                    }
                default:
                    return default;
            }

        }

        private static DeviceTcpNet? ConfigureMelsec(PLCDevice plc)
        {
            switch (plc.BrandSpecificationProtocal)
            {
                case PLCBrandSpecificationProtocal.Melsec_MC_Binary:
                    {
                        var melsec = new MelsecMcNet(plc.IP, plc.Port);
                        //melsec.ByteTransform.IsStringReverseByteWord = false;
                        plc.ByteTransform = melsec.ByteTransform;
                        plc.AddressWordLength = 2;
                        return melsec;
                    }
                case PLCBrandSpecificationProtocal.Melsec_MC_Ascii:
                    {
                        var melsec = new MelsecMcAsciiNet(plc.IP, plc.Port)
                        {
                            ByteTransform = { IsStringReverseByteWord = true },
                        };
                        plc.ByteTransform = melsec.ByteTransform;
                        plc.AddressWordLength = 2;
                        return melsec;
                    }
                case PLCBrandSpecificationProtocal.Melsec_A1E_Binary:
                    {
                        var melsec = new MelsecA1ENet(plc.IP, plc.Port);
                        //melsec.ByteTransform.IsStringReverseByteWord = false;
                        plc.ByteTransform = melsec.ByteTransform;
                        plc.AddressWordLength = 2;
                        return melsec;
                    }
                default:
                    return default;
            }
        }

        private static DeviceTcpNet ConfigurePanasonic(PLCDevice plc)
        {
            var panasonic = new PanasonicMewtocolOverTcp(plc.IP, plc.Port);
            //panasonic.ByteTransform.IsStringReverseByteWord = false;
            plc.ByteTransform = panasonic.ByteTransform;
            plc.AddressWordLength = 2;
            return panasonic;
        }

        private static DeviceTcpNet? ConfigureKeyence(PLCDevice plc)
        {
            switch (plc.BrandSpecificationProtocal)
            {
                case PLCBrandSpecificationProtocal.Keyence_MC_Binary:
                    {
                        var keyence = new KeyenceMcNet(plc.IP, plc.Port);
                        //keyence.ByteTransform.IsStringReverseByteWord = false;
                        plc.ByteTransform = keyence.ByteTransform;
                        plc.AddressWordLength = 2;
                        return keyence;
                    }

                case PLCBrandSpecificationProtocal.Keyence_MC_Ascii:
                    {
                        var keyence = new KeyenceMcAsciiNet(plc.IP, plc.Port);
                        keyence.ByteTransform.IsStringReverseByteWord = true;
                        plc.ByteTransform = keyence.ByteTransform;
                        plc.AddressWordLength = 2;
                        return keyence;
                    }

                default:
                    return null;
            }
        }

        private static DeviceTcpNet ConfigureInovance(PLCDevice plc, InovanceSeries inovanceSeries)
        {
            var inovance = new InovanceTcpNet(plc.IP, plc.Port);
            inovance.Series = inovanceSeries;
            if (plc.ByteDataFormat is not null)
                inovance.ByteTransform.DataFormat = (DataFormat)plc.ByteDataFormat;
            plc.ByteTransform = inovance.ByteTransform;
            plc.AddressWordLength = 2;
            return inovance;
        }

        private static DeviceTcpNet ConfigureModbus(PLCDevice plc)
        {
            var modbus = new ModbusTcpNet(plc.IP, plc.Port);
            if (plc.ByteDataFormat is not null)
                modbus.ByteTransform.DataFormat = (DataFormat)plc.ByteDataFormat;
            plc.ByteTransform = modbus.ByteTransform;
            plc.AddressWordLength = 2;
            return modbus;
        }

        /// <summary>
        /// 确保PLC已连接（尝试重连）
        /// </summary>
        /// <param name="plc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<bool> EnsurePLCConnectedAsync(PLCDevice plc, CancellationToken cancellationToken = default)
        {
            if (plc == null) return false;
            if (plc.IsConnect) return true;
            return await EnsureConnectedAsync(plc, cancellationToken);
        }

        /// <summary>
        /// 根据地址ID获取关联的PLC ID
        /// </summary>
        public int? GetPLCIdForAddress(int addressId)
        {
            if (_rwAddresses.TryGetValue(addressId, out var address))
            {
                if (_rwConfigs.TryGetValue(address.AddressRangeId, out var config))
                {
                    return config.PLCID;
                }
            }
            return null;
        }

        // 资源清理
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _serviceCts.Cancel();

            try
            {
                Task.WaitAll(_runningTasks.ToArray(), TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex)
            {
                _logger.Warn("等待任务完成时出现异常", ex, true);
            }

            foreach (var plc in _plcs.Values)
            {
                try
                {
                    plc.Device?.ConnectClose();
                    plc.Device?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Error($"关闭设备连接失败: {plc.Name}", ex, true);
                }
            }

            _serviceCts.Dispose();
            _initSemaphore.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            _serviceCts.Cancel();

            try
            {
                await Task.WhenAll(_runningTasks);
            }
            catch (Exception ex)
            {
                _logger.Warn("等待任务完成时出现异常", ex, true);
            }

            foreach (var plc in _plcs.Values)
            {
                try
                {
                    plc.Device?.ConnectClose();
                    plc.Device?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Error($"关闭设备连接失败: {plc.Name}", ex, true);
                }
            }

            _serviceCts.Dispose();
            _initSemaphore.Dispose();
        }
    }
}
