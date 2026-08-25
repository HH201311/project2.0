using CsvHelper;
using CsvHelper.Configuration;
using Dm.util;
using Mapster.Models;
using OpenCvSharp;
using OxyPlot;
using SkiaSharp;
using SqlSugar;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.JRCW;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AssistanceModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.BusinessModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
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
    [Module("VM_SummaryPage", "总况", Order = 1, Category = "VM_CCTBMonitoringPage/VM_SummaryPage")]
    class VM_SummaryPage : ViewModelBase
    {
        private readonly ILoggerService<VM_SummaryPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IPLCService _plcService;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        private CancellationTokenSource _globalCts = new();
        private bool _isDisplaying = false;

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

        /// <summary>
        /// 初始化
        /// </summary>
        public override void OnInitialize()
        {
            InitializeProjectSettings();

            base.OnInitialize();
        }

        /// <summary>
        /// 异步初始化
        /// </summary>
        /// <returns></returns>
        protected override async Task OnInitializeAsync()
        {
            await InitializeLineInfoAsync(_globalCts.Token);

            await InitializeKeyControlParametersAsync(_globalCts.Token);
            await InitializeMonitoringParametersAsync(_globalCts.Token);

            await InitializeCPKWatcherConfigAsync(_globalCts.Token);

            await base.OnInitializeAsync();
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        protected override async Task OnCleanupAsync()
        {
            CCTBCore.SettingMessageNotificationEvent -= OnBasicFunctionSettingChangedAsync;
            CCTBCore.SettingMessageNotificationEvent -= OnSpecialFunctionSettingChangedAsync;

            _globalCts?.Cancel();
            _globalCts?.Dispose();

            await base.OnCleanupAsync();
        }

        /// <summary>
        /// 当离开界面时触发
        /// </summary>
        /// <returns></returns>
        protected override Task OnNavigatedFromAsync()
        {
            _isDisplaying = false;
            return base.OnNavigatedFromAsync();
        }

        /// <summary>
        /// 当进入界面时触发
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        protected override Task OnNavigatedToAsync(object? parameter)
        {
            _isDisplaying = true;

            return base.OnNavigatedToAsync(parameter);
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

        #region 双缓存机制：界面刷新应用

        #endregion

        #region 拉线信息
        public LineInfoModel LineInfo { get; set; } = new();

        /// <summary>
        /// 初始化拉信息异步任务
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task InitializeLineInfoAsync(CancellationToken ct)
        {
            var t = Task.Run(() =>
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
            });

            await t.WaitAsync(ct);
        }
        #endregion

        #region 关键控制
        public ObservableCollection<ParametersModel<double>> KeyControlParameters { get; set; } = [];

        /// <summary>
        /// 初始化关键控制异步任务
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task InitializeKeyControlParametersAsync(CancellationToken ct)
        {
            var addresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 1 && !string.IsNullOrEmpty(o.ParameterName));
            var parameters = addresses.Select(o => { return new ParametersModel<double>(o.ParameterName, o.MonitorValue); }).ToList();
            var t1 = RunOnUIThreadAsync(() => { parameters.ForEach(o => { KeyControlParameters.Add(o); }); });
            await t1.WaitAsync(ct);

            var t2 = Task.Run(async () =>
            {
                var rm = new Random();
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        var validAddresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 1 && !string.IsNullOrEmpty(o.ParameterName)).ToDictionary(o => o.ParameterName!, o => o);
                        await RunOnUIThreadAsync(() =>
                        {
                            foreach (var p in KeyControlParameters)
                            {
                                //p.PValue = 300 * rm.NextDouble();
                                //continue;
                                if (!string.IsNullOrEmpty(p.PName)) continue;
                                if (!validAddresses.TryGetValue(p.PName!, out var plcRWAddress)) continue;

                                var value = plcRWAddress.MonitorValue?.ToString().ToDouble();
                                p.PValue = value;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"生产监控/总况/关键控制 PLC地址数据更新异常！", ex);
                        await Task.Delay(5000);
                    }
                }
            });
            await t2.WaitAsync(ct);
        }
        #endregion

        #region 设备监控
        public ObservableCollection<ParametersModel<double>> MonitoringParameters { get; set; } = [];

        /// <summary>
        /// 初始化设备监控异步任务
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task InitializeMonitoringParametersAsync(CancellationToken ct)
        {
            var addresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 2 && !string.IsNullOrEmpty(o.ParameterName));
            var parameters = addresses.Select(o => { return new ParametersModel<double>(o.ParameterName, o.MonitorValue); }).ToList();

            var t1 = RunOnUIThreadAsync(() => { parameters.ForEach(o => { MonitoringParameters.Add(o); }); });
            await t1.WaitAsync(ct);

            var t2 = Task.Run(async () =>
            {
                var rm = new Random();
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        var validAddresses = _plcService.RWAddresses.Values.Where(o => o.CategoryId == 2 && !string.IsNullOrEmpty(o.ParameterName)).ToDictionary(o => o.ParameterName!, o => o);
                        await RunOnUIThreadAsync(() =>
                        {
                            foreach (var p in MonitoringParameters)
                            {
                                //p.PValue = 300 * rm.NextDouble();
                                //continue;
                                if (!string.IsNullOrEmpty(p.PName)) continue;
                                if (!validAddresses.TryGetValue(p.PName!, out var plcRWAddress)) continue;

                                var value = plcRWAddress.MonitorValue?.ToString().ToDouble();
                                p.PValue = value;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"生产监控/总况/设备监控 PLC地址数据更新异常！", ex);
                        await Task.Delay(5000);
                    }
                }
            });

            await t2.WaitAsync(ct);
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
        private string? watcherPathCache;
        private EWeightDataType weightDataTypeCache;
        private DebounceWatcher? cpkWatcherA;
        private DebounceWatcher? cpkWatcherB;

        /// <summary>
        /// 初始化cpk文件监控配置字段
        /// </summary>
        /// <returns></returns>
        private async Task InitializeCPKWatcherConfigAsync(CancellationToken ct)
        {
            var t = Task.Run(() =>
            {
                // 订阅更新
                CCTBCore.SettingMessageNotificationEvent -= OnBasicFunctionSettingChangedAsync;
                CCTBCore.SettingMessageNotificationEvent += OnBasicFunctionSettingChangedAsync;
                CCTBCore.SettingMessageNotificationEvent -= OnSpecialFunctionSettingChangedAsync;
                CCTBCore.SettingMessageNotificationEvent += OnSpecialFunctionSettingChangedAsync;

                var bf = CCTBCore.BasicFunctionSetting;
                var sf = CCTBCore.SpecialFunctionSetting;
                if (bf == null || sf == null) return;

                // 初始化一次
                var watcherPath = bf.GetFullPathOfCoatingData();
                if (string.IsNullOrEmpty(watcherPath)) return;
                var weightDataType = sf.FormChartDataType;

                ReloadCPKWatcherAsync(watcherPath, weightDataType);
            });
            await t.WaitAsync(ct);
        }

        /// <summary>
        /// cpk文件监控配置改变时触发
        /// </summary>
        /// <param name="obj"></param>
        private async void OnBasicFunctionSettingChangedAsync(object? sender, MessageEventArgs<object> e)
        {
            if (!(e.Message is BasicFunctionSettingModel bf)) return;
            var sf = CCTBCore.SpecialFunctionSetting;
            if (bf == null || sf == null) return;
            var watcherPath = bf.GetFullPathOfCoatingData();
            var weightDataType = sf.FormChartDataType;

            if (string.IsNullOrEmpty(watcherPath)) return;
            if (watcherPathCache == watcherPath && weightDataTypeCache == weightDataType) return;

            ReloadCPKWatcherAsync(watcherPath, weightDataType);
        }

        /// <summary>
        /// cpk文件监控配置改变时触发
        /// </summary>
        /// <param name="obj"></param>
        private async void OnSpecialFunctionSettingChangedAsync(object? sender, MessageEventArgs<object> e)
        {
            if (!(e.Message is SpecialFunctionSettingModel sf)) return;
            var bf = CCTBCore.BasicFunctionSetting;
            if (bf == null || sf == null) return;
            var watcherPath = bf.GetFullPathOfCoatingData();
            var weightDataType = sf.FormChartDataType;

            if (string.IsNullOrEmpty(watcherPath)) return;
            if (watcherPathCache == watcherPath && weightDataTypeCache == weightDataType) return;

            ReloadCPKWatcherAsync(watcherPath, weightDataType);
        }

        /// <summary>
        /// 重新加载文件监控配置
        /// </summary>
        /// <param name="watcherPath"></param>
        /// <param name="weightDataType"></param>
        private async void ReloadCPKWatcherAsync(string watcherPath, EWeightDataType weightDataType)
        {
            // 缓存配置
            watcherPathCache = watcherPath;
            weightDataTypeCache = weightDataType;

            // 先清理再重新加载
            await RunOnUIThreadAsync(() =>
            {
                CPKModelsA.Clear();
                CPKModelsB.Clear();
                CPKByRollModelsA.Clear();
                CPKByRollModelsB.Clear();
            });

            // 初始化监控文件夹路径
            if (!Directory.Exists(watcherPathCache)) Directory.CreateDirectory(watcherPathCache);

            // 先加载一次
            var fullPath = Path.Combine(watcherPathCache, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            await InitializeCPKParametersAsync(fullPath, ESideType.A);
            await InitializeCPKParametersAsync(fullPath, ESideType.B);

            // 初始化CPK文件监控实例
            InitializeCPKWatcher(watcherPathCache, ESideType.A);
            InitializeCPKWatcher(watcherPathCache, ESideType.B);
        }

        /// <summary>
        /// 初始化文件监控
        /// </summary>
        /// <param name="watcherPath"></param>
        /// <param name="sideType"></param>
        private void InitializeCPKWatcher(string watcherPath, ESideType sideType)
        {
            var watcher = new DebounceWatcher(watcherPath, $"*_{sideType}面.csv");
            watcher.Interval = 50;
            watcher.IncludeSubdirectories = true;
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
            // 将路径中的反斜杠转义为正则表达式中的反斜杠
            var escapedPath = Regex.Escape(watcherPathCache ?? string.Empty);
            var dateStr = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var escapedDateStr = Regex.Escape(dateStr);

            var pattern = @$"{escapedPath}\\{escapedDateStr}\\.*_{sideType}面\.csv";
            var match = Regex.Match(fullPath, pattern);
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

        private readonly SemaphoreSlim _cpkRefreshLockA = new(1, 1);
        private readonly SemaphoreSlim _cpkRefreshLockB = new(1, 1);
        /// <summary>
        /// AB面文件修改触发后执行
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="sideType"></param>
        private async Task RefreshCPKOperation(string filePath, ESideType sideType)
        {
            // 获取对应面的锁；如果正在刷新，直接丢弃本次请求
            var semaphore = sideType == ESideType.A ? _cpkRefreshLockA : _cpkRefreshLockB;
            if (!await semaphore.WaitAsync(0))
            {
                _logger.Debug($"{sideType}面 CPK刷新被跳过，上一次刷新尚未完成");
                return;
            }
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
            finally { semaphore.Release(); }
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
            float targetValue = 0, toleranceValue = 0;
            var weights = new List<float>();
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
        private IEnumerable<CoatingDataModel> ReadWeightDataCsvSafely(string filePath, Encoding encoding)
        {
            var _records = new List<CoatingDataModel>();
            try
            {
                var config = CoatingDataModelMap.GetCsvConfiguration();
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, encoding);
                using var csv = new CsvReader(sr, config);
                csv.Context.RegisterClassMap<CoatingDataModelMap>();
                var records = csv.GetRecords<CoatingDataModel>();
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
