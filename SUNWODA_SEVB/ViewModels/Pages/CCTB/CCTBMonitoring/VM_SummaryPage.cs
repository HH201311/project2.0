using CsvHelper;
using CsvHelper.Configuration;
using Mapster.Models;
using OpenCvSharp;
using OxyPlot;
using SkiaSharp;
using SqlSugar;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
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

        public override void OnInitialize()
        {
            InitializeProjectSettings();

            _ = RunOnUIThreadAsync(InitializeLineInfo);

            _ = RunOnUIThreadAsync(InitializeProductionInfo);

            _ = RunInBackgroundAsync(InitializeKeyControlParametersAsync);
            _ = RunInBackgroundAsync(InitializeMonitoringParametersAsync);

            _ = RunInBackgroundAsync(InitializeCPKWatcherConfigAsync);

            base.OnInitialize();
        }

        private void InitializeProjectSettings()
        {
            if (!_projectSettingRepository.Exists(model => model.Name == "Electrode" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("Electrode", CCTBCore.BelongToVM, "---", "string", 2)
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ProductionDataPath" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ProductionDataPath", CCTBCore.BelongToVM, "D:\\通用上位机\\闭环数据", "string", 2)
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "DataSourceType" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("DataSourceType", CCTBCore.BelongToVM, EDataSourceType.TCPClient.ToString(), typeof(EDataSourceType).Name, 2)
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "WeightDataType" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("WeightDataType", CCTBCore.BelongToVM, EWeightDataType.干膜净重分区数据.ToString(), typeof(EWeightDataType).Name, 2)
                );
            }
        }

        #region 拉线信息
        public LineInfoModel LineInfo { get; set; } = new();
        private void InitializeLineInfo()
        {
            var workships = _globalSettingRepository.GetByName("workship")?.Value?.Split(':') ?? new string[0];
            var electrode = _projectSettingRepository.GetByNameAndVM("Electrode", CCTBCore.BelongToVM)?.Value;
            var deviceName = _globalSettingRepository.GetByName("DeviceName")?.Value;
            var ipHeader = _globalSettingRepository.GetByName("IPHeader")?.Value ?? "";
            var networkInfo = NetworkHelper.GetNetworkInfo(ipHeader);

            LineInfo.LineBase = (workships?.Length > 0 ? workships[0] : "---") + "基地";
            LineInfo.Electrode = electrode ?? "---";
            LineInfo.LineName = workships?.Length > 2 ? workships[2] : "---";
            LineInfo.DeviceName = deviceName ?? "---";
            LineInfo.DeviceIp = networkInfo?.IpAddress ?? "---";
        }
        #endregion

        #region 生产信息
        public ProductionInfoModel ProductionInfo { get; set; } = new();

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
        #endregion

        #region 关键控制
        public ObservableCollection<ParametersModel<double>> KeyControlParameters { get; set; } = [];

        private async Task InitializeKeyControlParametersAsync()
        {
            var addresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 1 && !string.IsNullOrEmpty(o.ParameterName));
            var parameters = addresses.Select(o => { return new ParametersModel<double>(o.ParameterName, o.MonitorValue); }).ToList();
            await RunOnUIThreadAsync(() =>
            {
                parameters.ForEach(o => { KeyControlParameters.Add(o); });

                // 测试
                KeyControlParameters.Add(new ParametersModel<double>("机头A设定速度", 127d));
                KeyControlParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                KeyControlParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
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
        #endregion

        #region 设备监控
        public ObservableCollection<ParametersModel<double>> MonitoringParameters { get; set; } = [];

        private async Task InitializeMonitoringParametersAsync()
        {
            var addresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 2 && !string.IsNullOrEmpty(o.ParameterName));
            var parameters = addresses.Select(o => { return new ParametersModel<double>(o.ParameterName, o.MonitorValue); }).ToList();
            await RunOnUIThreadAsync(() =>
            {
                parameters.ForEach(o => { MonitoringParameters.Add(o); });

                // 测试
                MonitoringParameters.Add(new ParametersModel<double>("机头A设定速度", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B设定速度", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
                MonitoringParameters.Add(new ParametersModel<double>("机头B刀距设定间隙-左", 127));
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
        #endregion

        #region BY卷CPK
        private double cpkByRollMinA = 0, cpkByRollMaxA = 4, cpkByRollMinB = 0, cpkByRollMaxB = 4;
        public double CPKByRollMinA { get => cpkByRollMinA; set => SetProperty(ref cpkByRollMinA, value); }
        public double CPKByRollMaxA { get => cpkByRollMaxA; set => SetProperty(ref cpkByRollMaxA, value); }
        public double CPKByRollMinB { get => cpkByRollMinB; set => SetProperty(ref cpkByRollMinB, value); }
        public double CPKByRollMaxB { get => cpkByRollMaxB; set => SetProperty(ref cpkByRollMaxB, value); }

        public ObservableCollection<CPKByRollModel> CPKByRollModelsA { get; set; } = [];
        public ObservableCollection<CPKByRollModel> CPKByRollModelsB { get; set; } = [];

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="rollNames"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task DeleteCPKByRollModels(ESideType sideType, params string[] rollNames)
        {
            var models = sideType switch
            {
                ESideType.A => CPKByRollModelsA,
                ESideType.B => CPKByRollModelsB,
                _ => throw new NotImplementedException($"未实现[{sideType}]！"),
            };

            foreach (var rollName in rollNames)
            {
                var deleteModels = models.Where(o => o.RollName == rollName).ToList();
                if (deleteModels == null || deleteModels.Count == 0) continue;
                await RunOnUIThreadAsync(() => { deleteModels.ForEach(o => { models.Remove(o); }); });
            }
        }

        /// <summary>
        /// 重命名
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="oldRollName"></param>
        /// <param name="newRollName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task RenameCPKByRollModels(ESideType sideType, string oldRollName, string newRollName)
        {
            var models = sideType switch
            {
                ESideType.A => CPKByRollModelsA,
                ESideType.B => CPKByRollModelsB,
                _ => throw new NotImplementedException($"未实现[{sideType}]！"),
            };

            foreach (var model in models)
            {
                if (model.RollName != oldRollName) continue;
                await RunOnUIThreadAsync(() => { model.RollName = newRollName; });
            }
        }

        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task RefreshCPKByRollModels(ESideType sideType, params CPKByRollModel[] datas)
        {
            var models = sideType switch
            {
                ESideType.A => CPKByRollModelsA,
                ESideType.B => CPKByRollModelsB,
                _ => throw new NotImplementedException($"未实现[{sideType}]！"),
            };

            foreach (var data in datas)
            {
                await RunOnUIThreadAsync(() =>
                {
                    var index = models.ToList().FindLastIndex(o => o.RollName == data.RollName);
                    if (index >= 0)// 存在则更新
                    {
                        models[index] = data;
                        return;
                    }
                    // 不存在则新增
                    models.Add(data);
                    if (models.Count > 10) models.RemoveAt(0);
                });
            }
        }
        #endregion

        #region 单趟CPK
        private double cpkMinA = 0, cpkMaxA = 4, cpkMinB = 0, cpkMaxB = 4;
        public double CPKMinA { get => cpkMinA; set => SetProperty(ref cpkMinA, value); }
        public double CPKMaxA { get => cpkMaxA; set => SetProperty(ref cpkMaxA, value); }
        public double CPKMinB { get => cpkMinB; set => SetProperty(ref cpkMinB, value); }
        public double CPKMaxB { get => cpkMaxB; set => SetProperty(ref cpkMaxB, value); }

        public ObservableCollection<CPKModel> CPKModelsA { get; set; } = [];
        public ObservableCollection<CPKModel> CPKModelsB { get; set; } = [];

        /// <summary>
        /// 删除记录
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="rollNames"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task DeleteCPKModels(ESideType sideType, params string[] rollNames)
        {
            var models = sideType switch
            {
                ESideType.A => CPKModelsA,
                ESideType.B => CPKModelsB,
                _ => throw new NotImplementedException($"未实现[{sideType}]！"),
            };

            foreach (var rollName in rollNames)
            {
                var deleteModels = models.Where(o => o.RollName == rollName).ToList();
                if (deleteModels == null || deleteModels.Count == 0) continue;
                await RunOnUIThreadAsync(() => { deleteModels.ForEach(o => { models.Remove(o); }); });
            }
        }

        /// <summary>
        /// 重命名
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="oldRollName"></param>
        /// <param name="newRollName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task RenameCPKModels(ESideType sideType, string oldRollName, string newRollName)
        {
            var models = sideType switch
            {
                ESideType.A => CPKModelsA,
                ESideType.B => CPKModelsB,
                _ => throw new NotImplementedException($"未实现[{sideType}]！"),
            };

            foreach (var model in models)
            {
                if (model.RollName != oldRollName) continue;
                await RunOnUIThreadAsync(() => { model.RollName = newRollName; });
            }
        }

        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task RefreshCPKModels(ESideType sideType, params CPKModel[] datas)
        {
            var models = sideType switch
            {
                ESideType.A => CPKModelsA,
                ESideType.B => CPKModelsB,
                _ => throw new NotImplementedException($"未实现[{sideType}]！"),
            };

            foreach (var data in datas)
            {
                await RunOnUIThreadAsync(() =>
                {
                    var index = models.ToList().FindLastIndex(o => o.RollName == data.RollName && o.PassCount == data.PassCount);
                    if (index >= 0)// 存在则更新
                    {
                        models[index] = data;
                        return;
                    }
                    // 不存在则新增
                    models.Add(data);
                    if (models.Count > 40) models.RemoveAt(0);
                });
            }
        }
        #endregion

        #region CPK文件监控
        private string? productionDataPathCache;
        private EDataSourceType dataSourceTypeCache;
        private EWeightDataType weightDataTypeCache;
        private DebounceWatcher? cpkWatcherA;
        private DebounceWatcher? cpkWatcherB;

        /// <summary>
        /// 初始化cpk文件监控配置字段
        /// </summary>
        /// <returns></returns>
        private async Task InitializeCPKWatcherConfigAsync()
        {
            // 订阅更新
            CCTBCore.CPKWatcherConfigMessageNotificationEvent -= OnCPKWatcherConfigChangedAsunc;
            CCTBCore.CPKWatcherConfigMessageNotificationEvent += OnCPKWatcherConfigChangedAsunc;

            // 初始化一次
            var productionDataPath = _projectSettingRepository.GetByNameAndVM("ProductionDataPath", CCTBCore.BelongToVM)?.Value ?? "";
            var dataSourceTypeStr = _projectSettingRepository.GetByNameAndVM("DataSourceType", CCTBCore.BelongToVM)?.Value ?? "";
            var success = Enum.TryParse(dataSourceTypeStr, out EDataSourceType dataSourceType);
            if (!success)
            {
                _logger.Error($"CPK计算引用的数据源类型[{dataSourceTypeStr}]配置异常！");
                return;
            }
            var weightDataTypeStr = _projectSettingRepository.GetByNameAndVM("WeightDataType", CCTBCore.BelongToVM)?.Value ?? "";
            success = Enum.TryParse(weightDataTypeStr, out EWeightDataType weightDataType);
            if (!success)
            {
                _logger.Error($"CPK计算引用的数据类型[{weightDataTypeStr}]配置异常！");
                return;
            }

            var config = new CPKWatcherConfigMessage();
            config.ProductionDataPath = productionDataPath;
            config.DataSourceType = dataSourceType;
            config.WeightDataType = weightDataType;
            var cpkWatcherConfig = new MessageEventArgs<CPKWatcherConfigMessage>();
            cpkWatcherConfig.Message = config;
            OnCPKWatcherConfigChangedAsunc(this, cpkWatcherConfig);
        }

        /// <summary>
        /// cpk文件监控配置改变时触发
        /// </summary>
        /// <param name="obj"></param>
        private async void OnCPKWatcherConfigChangedAsunc(object? sender, MessageEventArgs<CPKWatcherConfigMessage> cpkWatcherConfig)
        {
            if (cpkWatcherConfig == null) return;
            var config = cpkWatcherConfig.Message;
            if (config == null || string.IsNullOrEmpty(config.ProductionDataPath)) return;
            if (productionDataPathCache == config.ProductionDataPath &&
                dataSourceTypeCache == config.DataSourceType &&
                weightDataTypeCache == config.WeightDataType) return;

            // 缓存配置
            productionDataPathCache = config.ProductionDataPath;
            dataSourceTypeCache = config.DataSourceType;
            weightDataTypeCache = config.WeightDataType;

            // 先清理再重新加载
            await RunOnUIThreadAsync(() =>
            {
                CPKModelsA.Clear();
                CPKModelsB.Clear();
                CPKByRollModelsA.Clear();
                CPKByRollModelsB.Clear();
            });

            // 初始化监控文件夹路径
            var watcherPath = Path.Combine(productionDataPathCache, "涂布重量数据", dataSourceTypeCache.ToString());
            if (!Directory.Exists(watcherPath)) Directory.CreateDirectory(watcherPath);

            // 先加载一次
            var fullPath = Path.Combine(watcherPath, DateTime.Today.ToString("yyyy-MM-dd"));
            await InitializeCPKParametersAsync(fullPath, ESideType.A);
            await InitializeCPKParametersAsync(fullPath, ESideType.B);

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
            //watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime;
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Attributes;
            watcher.DeletedDebunce += OnWeightDataDeleted;
            watcher.RenamedDebunce += OnWeightDataRenamed;
            watcher.ChangedDebunce += OnWeightDataChanged;
            if (sideType == ESideType.A)
            {
                cpkWatcherA?.Dispose();
                cpkWatcherA = watcher;
                cpkWatcherA.EnableRaisingEvents = true;
                return;
            }
            if (sideType == ESideType.B)
            {
                cpkWatcherB?.Dispose();
                cpkWatcherB = watcher;
                cpkWatcherB.EnableRaisingEvents = true;
                return;
            }
        }

        /// <summary>
        /// 获取SideType
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private ESideType GetSideTypeByWatcherFilter(string filter)
        {
            var sideType = filter switch
            {
                "*_A面.csv" => ESideType.A,
                "*_B面.csv" => ESideType.B,
                _ => throw new Exception($"未实现[{filter}]!")
            };
            return sideType;
        }

        /// <summary>
        /// 文件修改时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnWeightDataChanged(object sender, FileSystemEventArgs e)
        {
            if (!(sender is DebounceWatcher watcher)) return;
            if (e.ChangeType != WatcherChangeTypes.Changed) return;
            var sideType = GetSideTypeByWatcherFilter(watcher.Filter);
            // 查询文件是否匹配（日期）
            if (!IsMatch(e.FullPath, sideType)) return;

            // 更新
            await RefreshCPKOperation(e.FullPath, sideType);
        }

        /// <summary>
        /// 文件修改时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnWeightDataRenamed(object sender, RenamedEventArgs e)
        {
            if (!(sender is DebounceWatcher watcher)) return;
            if (e.ChangeType != WatcherChangeTypes.Renamed) return;
            var sideType = GetSideTypeByWatcherFilter(watcher.Filter);
            // 查询文件是否匹配（日期）
            if (!IsMatch(e.OldFullPath, sideType)) return;

            // 更新
            if (IsMatch(e.FullPath, sideType))
            {
                var oldRollName = Path.GetFileName(e.OldFullPath).Split('_').FirstOrDefault();
                if (string.IsNullOrEmpty(oldRollName)) return;
                var newRollName = Path.GetFileName(e.FullPath).Split("_").FirstOrDefault();
                if (string.IsNullOrEmpty(newRollName)) return;
                await RenameCPKByRollModels(sideType, oldRollName, newRollName);
                await RenameCPKModels(sideType, oldRollName, newRollName);
            }
            else// 删除
            {
                var rollName = Path.GetFileName(e.OldFullPath).Split('_').FirstOrDefault();
                if (string.IsNullOrEmpty(rollName)) return;
                await DeleteCPKByRollModels(sideType, rollName);
                await DeleteCPKModels(sideType, rollName);
            }
        }

        /// <summary>
        /// 文件修改时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnWeightDataDeleted(object sender, FileSystemEventArgs e)
        {
            if (!(sender is DebounceWatcher watcher)) return;
            if (e.ChangeType != WatcherChangeTypes.Deleted) return;
            var sideType = GetSideTypeByWatcherFilter(watcher.Filter);
            // 查询文件是否匹配（日期）
            if (!IsMatch(e.FullPath, sideType)) return;

            var rollName = Path.GetFileName(e.FullPath).Split('_').FirstOrDefault();
            if (string.IsNullOrEmpty(rollName)) return;
            await DeleteCPKByRollModels(sideType, rollName);
            await DeleteCPKModels(sideType, rollName);

            // 更新
            await RefreshCPKOperation(e.FullPath, sideType);
        }
        #endregion

        /// <summary>
        /// 查询文件是否匹配（日期）
        /// </summary>
        /// <param name="fullPath"></param>
        /// <param name="sideType"></param>
        /// <returns></returns>
        private bool IsMatch(string fullPath, ESideType sideType)
        {
            var pattern = @$"涂布重量数据\\{dataSourceTypeCache}\\{DateTime.Today:yyyy-MM-dd}\\.*_{sideType}面\.csv";
            var match = Regex.Match(fullPath, pattern);
            if (match == null) return false;
            return match.Success;
        }

        /// <summary>
        /// 获取最新的10条文件
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="sideType"></param>
        /// <returns></returns>
        private List<FileInfo>? GetLatestWeightDataFiles(string? directory, ESideType sideType)
        {
            if (directory == null || !Directory.Exists(directory)) return default;
            var dir = new DirectoryInfo(directory);
            var files = dir.GetFiles($"*_{sideType}面.csv");
            // 通过创建时间排序，获取最新的10卷(膜卷号存在)
            var cpkFiles = files.OrderBy(o => o.CreationTime).Where(o => !string.IsNullOrEmpty(o.Name.Split('_').FirstOrDefault())).Skip(files.Count() - 10).ToList();
            if (cpkFiles == null || cpkFiles.Count == 0) return default;
            return cpkFiles;
        }

        /// <summary>
        /// AB面文件修改触发后执行
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="sideType"></param>
        private async Task RefreshCPKOperation(string filePath, ESideType sideType)
        {
            try
            {
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
                    await RefreshCPKByRollParametersAsync(sideType, file);
                }
                // 如果是最新的文件，则更新--防呆
                if (cpkFiles.Last().Name == file.Name)
                {
                    await RefreshCPKParametersAsync(sideType, file);
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
        private async Task InitializeCPKParametersAsync(string path, ESideType sideType)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            var dir = new DirectoryInfo(path);
            var files = dir.GetFiles($"*_{sideType}面.csv");
            var cpkFiles = files.OrderBy(o => o.CreationTime).Where(o => !string.IsNullOrEmpty(o.Name.Split('_').FirstOrDefault())).Skip(files.Count() - 10).ToArray();

            // 更新CPKBy卷数据
            foreach (var cpkFile in cpkFiles)
                await RefreshCPKByRollParametersAsync(sideType, cpkFile);

            // 更新CPK数据
            await RefreshCPKParametersAsync(sideType, cpkFiles.LastOrDefault());
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
                ESideType.A => CPKByRollModelsA,
                ESideType.B => CPKByRollModelsB,
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
            var cpkData = new CPKByRollModel();
            cpkData.CreationTime = file.CreationTime;
            cpkData.RollName = file.Name.Split('_').FirstOrDefault();
            cpkData.DisplayName = file.Name.Split('_').FirstOrDefault();
            cpkData.CPKValue = weights.CPK(targetValue, toleranceValue);
            await RefreshCPKByRollModels(sideType, cpkData);
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
                ESideType.A => CPKModelsA,
                ESideType.B => CPKModelsB,
                _ => throw new NotImplementedException($"无[{sideType}]配置实现！"),
            };
            using var cpkDataBuffer = new RingBuffer<CPKModel>(40);
            var weightDatas = ReadWeightDataCsvSafely(file.FullName, Encoding.UTF8);
            foreach (var weightData in weightDatas)
            {
                if (weightData.WeightDataType != weightDataTypeCache) continue;

                var cpkData = new CPKModel();
                cpkData.CreationTime = file.CreationTime;
                cpkData.GatherTime = weightData.GatherTime;
                cpkData.RollName = file.Name.Split('_').FirstOrDefault();
                cpkData.PassCount = weightData.PassCount;
                cpkData.DisplayName = weightData.PassCount.ToString();
                cpkData.IsManual = true;// 待更新
                var weights = weightData.Weights.Where(o => o != 0).ToArray();
                cpkData.CPKValue = sideType switch
                {
                    ESideType.A => weights.CPK(weightData.TargetValueA, weightData.ToleranceValueA),
                    ESideType.B => weights.CPK(weightData.TargetValueB, weightData.ToleranceValueB),
                    _ => 0
                };
                cpkDataBuffer.Add(cpkData);
            }
            var cpkDatas = cpkDataBuffer.TakeAll().ToArray();
            await RefreshCPKModels(sideType, cpkDatas);
        }

        /// <summary>
        /// 安全读取&解析csv重量数据
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        private IEnumerable<WeightDataModel> ReadWeightDataCsvSafely(string filePath, Encoding encoding)
        {
            var _records = new List<WeightDataModel>();
            try
            {
                var config = WeightDataModelMap.GetCsvConfiguration();
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, encoding);
                using var csv = new CsvReader(sr, config);
                csv.Context.RegisterClassMap<WeightDataModelMap>();
                var records = csv.GetRecords<WeightDataModel>();
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
