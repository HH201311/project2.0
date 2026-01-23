using CsvHelper;
using CsvHelper.Configuration;
using Mapster.Models;
using SkiaSharp;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Data.Repositories;
using SUNWODA_SEVB.PLC;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.WEB.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Joins;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;
using TouchSocket.Core;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBMonitoring
{
    [Module("VM_SummaryPage", "系统设置", Order = 0, Category = "VM_CCTBMonitoringPage/VM_SummaryPage")]
    class VM_SummaryPage : ViewModelBase
    {
        private readonly ILoggerService<VM_SummaryPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IPLCService _plcService;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_SummaryPage(
            ILoggerService<VM_SummaryPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IPLCService plcService,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _plcService = plcService;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
        }

        protected override async Task OnInitializeAsync()
        {
            InitializeProjectSettings();

            _ = RunOnUIThreadAsync(InitializeLineInfo);

            _ = RunOnUIThreadAsync(InitializeProductionInfo);

            _ = RunInBackgroundAsync(InitializeKeyControlParametersAsync);
            _ = RunInBackgroundAsync(InitializeMonitoringParametersAsync);

            _ = RunInBackgroundAsync(InitializeCPKWatcherConfigAsync);

            await base.OnInitializeAsync();
        }

        public CCTBLineInfoModel LineInfo { get; set; } = new();
        public CCTBProductionInfoModel ProductionInfo { get; set; } = new();
        public ObservableCollection<CCTBParametersModel<double>> KeyControlParameters { get; set; } = [];
        public ObservableCollection<CCTBParametersModel<double>> MonitoringParameters { get; set; } = [];

        private double cpkMin = 0, cpkMax = 4, cpkStep = 0.5;
        public double CPKMin { get => cpkMin; set => SetProperty(ref cpkMin, value); }
        public double CPKMax { get => cpkMax; set => SetProperty(ref cpkMax, value); }
        public double CPKStep { get => cpkStep; set => SetProperty(ref cpkStep, value); }

        public ObservableCollection<CCTBCPKModel> CPKParametersA { get; set; } = [];
        public ObservableCollection<CCTBCPKModel> CPKParametersB { get; set; } = [];
        public ObservableCollection<CCTBCPKModel> CPKByRollParametersA { get; set; } = [];
        public ObservableCollection<CCTBCPKModel> CPKByRollParametersB { get; set; } = [];

        private void InitializeProjectSettings()
        {
            if (!_projectSettingRepository.Exists(model => model.Name == "Electrode" && model.BelongToVM == "VM_CCTBMainWindow"))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("Electrode", "VM_CCTBMainWindow", "---", "string", 2)
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ProductionDataPath" && model.BelongToVM == "VM_CCTBMainWindow"))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ProductionDataPath", "VM_CCTBMainWindow", "D:\\通用上位机\\闭环数据", "string", 2)
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "DataSourceType" && model.BelongToVM == "VM_CCTBMainWindow"))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("DataSourceType", "VM_CCTBMainWindow", EDataSourceType.TCPClient.ToString(), typeof(EDataSourceType).Name, 2)
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "WeightDataType" && model.BelongToVM == "VM_CCTBMainWindow"))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("WeightDataType", "VM_CCTBMainWindow", EWeightDataType.干膜净重分区数据.ToString(), typeof(EWeightDataType).Name, 2)
                );
            }
        }

        private void InitializeLineInfo()
        {
            var workships = _globalSettingRepository.GetByName("workship")?.Value?.Split(':') ?? new string[0];
            var electrode = _projectSettingRepository.GetByNameAndVM("Electrode", "VM_CCTBMainWindow")?.Value;
            var deviceName = _globalSettingRepository.GetByName("DeviceName")?.Value;
            var ipHeader = _globalSettingRepository.GetByName("IPHeader")?.Value ?? "";
            var networkInfo = NetworkHelper.GetNetworkInfo(ipHeader);

            LineInfo.LineBase = (workships?.Length > 0 ? workships[0] : "---") + "基地";
            LineInfo.Electrode = electrode ?? "---";
            LineInfo.LineName = workships?.Length > 2 ? workships[2] : "---";
            LineInfo.DeviceName = deviceName ?? "---";
            LineInfo.DeviceIp = networkInfo?.IpAddress ?? "---";
        }

        private void InitializeProductionInfo()
        {
            ProductionInfo.CellModel = "314AH";
            ProductionInfo.FilmRollNumber = "AY47X1125L2804";
            ProductionInfo.NetWeightSpecification_A = 324;
            ProductionInfo.NetWeightSpecificationLimit_A = 4;
            ProductionInfo.NetWeightSpecification_B = 648;
            ProductionInfo.NetWeightSpecificationLimit_B = 7;
            ProductionInfo.CCDSpecification_MembraneZone = 375;
            ProductionInfo.CCDSpecificationLimit_MembraneZone = 0.5;
            ProductionInfo.CCDSpecification_BatteryTabSide = 33;
            ProductionInfo.CCDSpecificationLimit_BatteryTabSide = 0.5;
            ProductionInfo.CCDSpecification_BatteryTabMiddle = 33;
            ProductionInfo.CCDSpecificationLimit_BatteryTabMiddle = 0.5;
            ProductionInfo.CCDSpecification_Ceramic = 5;
            ProductionInfo.CCDSpecificationLimit_Ceramic = 1;
            ProductionInfo.CCDSpecificationLimit_Mismatch = 0.5;

            //  -TODO   后续注册到数据更新处
        }

        private void OnProductionInfoChanged()
        {

        }

        private async Task InitializeKeyControlParametersAsync()
        {
            var addresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 1 && !string.IsNullOrEmpty(o.ParameterName));
            var parameters = addresses.Select(o => { return new CCTBParametersModel<double>(o.ParameterName, o.MonitorValue); }).ToList();
            await RunOnUIThreadAsync(() =>
            {
                parameters.ForEach(o => { KeyControlParameters.Add(o); });

                // 测试
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头A设定速度", 127d));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
            });

            var rm = new Random();
            while (true)
            {
                try
                {
                    var validAddresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 1 && !string.IsNullOrEmpty(o.ParameterName)).ToDictionary(o => o.ParameterName!, o => o);
                    await RunOnUIThreadAsync(() =>
                    {
                        foreach (var p in KeyControlParameters)
                        {
                            p.PValue = 300 * rm.NextDouble();
                            continue;
                            //if (!string.IsNullOrEmpty(p.PName)) continue;
                            //if (validAddresses.TryGetValue(p.PName!, out var plcRWAddress))
                            //{
                            //    var value = plcRWAddress.MonitorValue?.ToString().ToDouble();
                            //    p.PValue = value;
                            //}
                        }
                    });

                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    _logger.Error($"生产监控/总况/关键控制 PLC地址数据更新异常！", ex);
                    await Task.Delay(5000);
                }
            }
        }

        private async Task InitializeMonitoringParametersAsync()
        {
            var addresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 2 && !string.IsNullOrEmpty(o.ParameterName));
            var parameters = addresses.Select(o => { return new CCTBParametersModel<double>(o.ParameterName, o.MonitorValue); }).ToList();
            await RunOnUIThreadAsync(() =>
            {
                parameters.ForEach(o => { MonitoringParameters.Add(o); });

                // 测试
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头A设定速度", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new CCTBParametersModel<double>("机头B刀距设定间隙-左", 127));
            });

            var rm = new Random();
            while (true)
            {
                try
                {
                    var validAddresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 2 && !string.IsNullOrEmpty(o.ParameterName)).ToDictionary(o => o.ParameterName!, o => o);
                    await RunOnUIThreadAsync(() =>
                    {
                        foreach (var p in MonitoringParameters)
                        {
                            p.PValue = 300 * rm.NextDouble();
                            continue;
                            //if (!string.IsNullOrEmpty(p.PName)) continue;
                            //if (validAddresses.TryGetValue(p.PName!, out var plcRWAddress))
                            //{
                            //    var value = plcRWAddress.MonitorValue?.ToString().ToDouble();
                            //    p.PValue = value;
                            //}
                        }
                    });

                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    _logger.Error($"生产监控/总况/设备监控 PLC地址数据更新异常！", ex);
                    await Task.Delay(5000);
                }
            }
        }

        private string? productionDataPathCache;
        private EDataSourceType dataSourceTypeCache;
        private EWeightDataType weightDataTypeCache;
        private DebounceWatcher? cpkWatcherA;
        private DebounceWatcher? cpkWatcherB;

        /// <summary>
        /// 初始化cpk文件监控配置字段
        /// </summary>
        /// <returns></returns>
        private Task InitializeCPKWatcherConfigAsync()
        {
            var productionDataPath = _projectSettingRepository.GetByNameAndVM("ProductionDataPath", "VM_CCTBMainWindow")?.Value ?? "";
            var dataSourceTypeStr = _projectSettingRepository.GetByNameAndVM("DataSourceType", "VM_CCTBMainWindow")?.Value ?? "";
            var success = Enum.TryParse(dataSourceTypeStr, out EDataSourceType dataSourceType);
            if (!success)
            {
                _logger.Error($"CPK计算引用的数据源类型[{dataSourceTypeStr}]配置异常！");
                return Task.CompletedTask;
            }
            var weightDataTypeStr = _projectSettingRepository.GetByNameAndVM("WeightDataType", "VM_CCTBMainWindow")?.Value ?? "";
            success = Enum.TryParse(weightDataTypeStr, out EWeightDataType weightDataType);
            if (!success)
            {
                _logger.Error($"CPK计算引用的数据类型[{weightDataTypeStr}]配置异常！");
                return Task.CompletedTask;
            }

            // -TODO  配置界面注册OnCPKWatcherConfigChanged

            OnCPKWatcherConfigChanged((productionDataPath, dataSourceType, weightDataType));
            return Task.CompletedTask;
        }

        /// <summary>
        /// cpk文件监控配置改变时触发
        /// </summary>
        /// <param name="obj"></param>
        private async Task OnCPKWatcherConfigChanged(object obj)
        {
            if (!(obj is (string productionDataPath, EDataSourceType dataSourceType, EWeightDataType weightDataType))) return;
            if (productionDataPathCache == productionDataPath &&
                dataSourceTypeCache == dataSourceType &&
                weightDataTypeCache == weightDataType) return;

            // 缓存配置
            productionDataPathCache = productionDataPath;
            dataSourceTypeCache = dataSourceType;
            weightDataTypeCache = weightDataType;

            // 先清理再重新加载
            await RunOnUIThreadAsync(() =>
            {
                CPKParametersA?.Clear();
                CPKParametersB?.Clear();
                CPKByRollParametersA?.Clear();
                CPKByRollParametersB?.Clear();
            });

            // 初始化监控文件夹路径
            var watcherPath = Path.Combine(productionDataPathCache, "涂布重量数据", dataSourceTypeCache.ToString());
            if (!Directory.Exists(watcherPath)) Directory.CreateDirectory(watcherPath);

            // 先加载一次
            var fullPath = Path.Combine(watcherPath, DateTime.Today.ToString("yyyy-MM-dd"));
            _ = RunInBackgroundAsync(() => { return InitializeCPKParametersAsync(fullPath, ESideType.A); });
            _ = RunInBackgroundAsync(() => { return InitializeCPKParametersAsync(fullPath, ESideType.B); });

            // 初始化CPK文件监控实例
            InitializeCPKWatcher(watcherPath, ESideType.A);
            InitializeCPKWatcher(watcherPath, ESideType.B);
        }

        /// <summary>
        /// 初始化文件监控
        /// </summary>
        /// <param name="watcherPath"></param>
        /// <param name="sideType"></param>
        private void InitializeCPKWatcher(string watcherPath, ESideType sideType)
        {
            var watcher = new DebounceWatcher(watcherPath, $"*_{sideType}面.csv");
            watcher.Interval = 250;
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime;
            if (sideType == ESideType.A)
            {
                watcher.CreatedDebunce += OnWeightDataAChanged;
                watcher.RenamedDebunce += OnWeightDataAChanged;
                watcher.ChangedDebunce += OnWeightDataAChanged;

                cpkWatcherA?.Dispose();
                cpkWatcherA = watcher;
                cpkWatcherA.EnableRaisingEvents = true;
                return;
            }
            if (sideType == ESideType.B)
            {
                watcher.CreatedDebunce += OnWeightDataBChanged;
                watcher.RenamedDebunce += OnWeightDataBChanged;
                watcher.ChangedDebunce += OnWeightDataBChanged;

                cpkWatcherB?.Dispose();
                cpkWatcherB = watcher;
                cpkWatcherB.EnableRaisingEvents = true;
                return;
            }
        }

        /// <summary>
        /// A面文件修改时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWeightDataAChanged(object sender, FileSystemEventArgs e)
        {
            if (!(sender is DebounceWatcher cpkWatcher)) return;

            var sideType = ESideType.A;
            RefreshCPKOperation(e.FullPath, sideType);
        }

        /// <summary>
        /// B面文件修改时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWeightDataBChanged(object sender, FileSystemEventArgs e)
        {
            if (!(sender is DebounceWatcher cpkWatcher)) return;

            var sideType = ESideType.B;
            RefreshCPKOperation(e.FullPath, sideType);
        }

        /// <summary>
        /// AB面文件修改触发后执行
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="sideType"></param>
        private void RefreshCPKOperation(string filePath, ESideType sideType)
        {
            try
            {
                var pattern = @$"涂布重量数据\\{dataSourceTypeCache}\\{DateTime.Today:yyyy-MM-dd}\\.*_{sideType}面\.csv";
                var match = Regex.Match(filePath, pattern);
                if (!match.Success) return;

                var path = Path.GetDirectoryName(filePath);
                var dir = new DirectoryInfo(path!);
                var files = dir.GetFiles($"*_{sideType}面.csv");
                // 通过创建时间排序，获取最新的10卷(膜卷号存在)
                var cpkFiles = files.OrderBy(o => o.CreationTime).Where(o => !string.IsNullOrEmpty(o.Name.Split('_').FirstOrDefault())).Skip(files.Count() - 10).ToList();
                if (cpkFiles == null || cpkFiles.Count == 0) return;
                var file = new FileInfo(filePath);
                // 如果是最新的近10条文件，则更新--防呆
                if (cpkFiles.Exists(o => o.Name == file.Name))
                {
                    _ = RunInBackgroundAsync(() => { return RefreshCPKByRollParametersAsync(sideType, file); });
                }
                // 如果是最新的文件，则更新--防呆
                if (cpkFiles.Last().Name == file.Name)
                {
                    _ = RunInBackgroundAsync(() => { return RefreshCPKParametersAsync(sideType, file); });
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"{sideType}面重量数据读取异常(CSV计算)[{filePath}]", ex);
            }
        }

        /// <summary>
        /// 初始化加载AB面文件计算CPK
        /// </summary>
        /// <param name="path"></param>
        /// <param name="sideType"></param>
        /// <returns></returns>
        private Task InitializeCPKParametersAsync(string? path, ESideType sideType)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return Task.CompletedTask;
            var dir = new DirectoryInfo(path);
            var files = dir.GetFiles($"*_{sideType}面.csv");
            var cpkFiles = files.OrderBy(o => o.CreationTime).Where(o => !string.IsNullOrEmpty(o.Name.Split('_').FirstOrDefault())).Skip(files.Count() - 10).ToArray();

            _ = RunInBackgroundAsync(async () =>
            {
                foreach (var cpkFile in cpkFiles)
                    await RefreshCPKByRollParametersAsync(sideType, cpkFile);
                return Task.CompletedTask;
            });

            _ = RunInBackgroundAsync(() => { return RefreshCPKParametersAsync(sideType, cpkFiles.LastOrDefault()); });

            return Task.CompletedTask;
        }

        /// <summary>
        /// 更新CPKBy卷柱状图
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task RefreshCPKByRollParametersAsync(ESideType sideType, FileInfo? file)
        {
            if (file == null || !file.Exists) return;
            var cpkByRollParameters = sideType switch
            {
                ESideType.A => CPKByRollParametersA,
                ESideType.B => CPKByRollParametersB,
                _ => throw new NotImplementedException($"无[{sideType}]配置实现！"),
            };
            double targetValue = 0, toleranceValue = 0;
            var weights = new List<double>();
            var weightDatas = ReadWeightDataCsvSafely(file.FullName, Encoding.UTF8);
            foreach (var weightData in weightDatas)
            {
                if (weightData.WeightDataType != weightDataTypeCache) continue;

                weights.AddRange(weightData.Weights.Where(o => o != 0));
                targetValue = sideType switch
                {
                    ESideType.A => weightData.TargetValueA,
                    ESideType.B => weightData.TargetValueB,
                    _ => 0
                };
                toleranceValue = sideType switch
                {
                    ESideType.A => weightData.ToleranceValueA,
                    ESideType.B => weightData.ToleranceValueB,
                    _ => 0
                };
            }
            var cpkData = new CCTBCPKModel();
            cpkData.CreationTime = file.CreationTime;
            cpkData.CPKLabel = file.Name.Split('_').FirstOrDefault();
            cpkData.CPKValue = weights.CPK(targetValue, toleranceValue);
            await RunOnUIThreadAsync(() =>
            {
                var index = cpkByRollParameters.ToList().FindLastIndex(o => o.CreationTime == cpkData.CreationTime && o.CPKLabel == cpkData.CPKLabel);
                if (index >= 0)
                {
                    cpkByRollParameters[index] = cpkData;
                    return;
                }
                cpkByRollParameters.Add(cpkData);
                if (cpkByRollParameters.Count > 10)
                    cpkByRollParameters.RemoveAt(0);
            });
        }

        /// <summary>
        /// 更新CPKBy趟折线图
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task RefreshCPKParametersAsync(ESideType sideType, FileInfo? file)
        {
            if (file == null || !file.Exists) return;
            var cpkParameters = sideType switch
            {
                ESideType.A => CPKParametersA,
                ESideType.B => CPKParametersB,
                _ => throw new NotImplementedException($"无[{sideType}]配置实现！"),
            };
            using var cpkDataBuffer = new RingBuffer<CCTBCPKModel>(40);
            var weightDatas = ReadWeightDataCsvSafely(file.FullName, Encoding.UTF8);
            foreach (var weightData in weightDatas)
            {
                if (weightData.WeightDataType != weightDataTypeCache) continue;

                var cpkData = new CCTBCPKModel();
                cpkData.CreationTime = weightData.GatherTime;
                cpkData.CPKLabel = weightData.PassCount.ToString();
                var weights = weightData.Weights.Where(o => o != 0).ToArray();
                cpkData.CPKValue = sideType switch
                {
                    ESideType.A => weights.CPK(weightData.TargetValueA, weightData.ToleranceValueA),
                    ESideType.B => weights.CPK(weightData.TargetValueB, weightData.ToleranceValueB),
                    _ => 0
                };
                cpkDataBuffer.Add(cpkData);
            }
            var cpkDatas = cpkDataBuffer.TakeAll();
            await RunOnUIThreadAsync(() =>
            {
                foreach (var cpkData in cpkDatas)
                {
                    var exists = cpkParameters.ToList().Exists(o => o.CreationTime == cpkData.CreationTime && o.CPKLabel == cpkData.CPKLabel);
                    if (exists) continue;
                    cpkParameters.Add(cpkData);
                    if (cpkParameters.Count > 40)
                        cpkParameters.RemoveAt(0);
                }
            });
        }

        /// <summary>
        /// 安全读取&解析csv重量数据
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        private IEnumerable<CCTBWeightDataModel> ReadWeightDataCsvSafely(string filePath, Encoding encoding)
        {
            var _records = new List<CCTBWeightDataModel>();
            try
            {
                var config = CCTBWeightDataModelMap.GetCsvConfiguration();
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, encoding);
                using var csv = new CsvReader(sr, config);
                csv.Context.RegisterClassMap<CCTBWeightDataModelMap>();
                var records = csv.GetRecords<CCTBWeightDataModel>();
                _records.AddRange(records);
            }
            catch (Exception ex)
            {
                _logger.Error($"CSV文件读取异常[{filePath}]", ex);
                yield break;
            }
            foreach (var record in _records) yield return record;
        }

        private string GetProductSn(string filePath)
        {
            var content = ReadFileSafely(filePath, Encoding.UTF8);
            var productSn = "";
            var match = Regex.Match(
                content,
                @"MachineID：*(.+)\r\nMoNumber：*(.+)\r\nPreProductSn：*(.+)\r\nOperatorID：*(.+)\r\nGroupCode：*(.+)\r\nProductSn：*(.+)\r\nModelNum：*(.+)\r\nOperatorNumber：*(.+)\r\n*(.+)"
            );
            if (match.Success)
            {
                productSn = match.Groups[6].Value.Trim();
            }
            else
            {
                _logger.Error("未找到 ProductSn 字段。");
            }
            return productSn;
        }

        private string ReadFileSafely(string path, Encoding encoding)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs, encoding, detectEncodingFromByteOrderMarks: true);
            return sr.ReadToEnd();
        }
    }
}
