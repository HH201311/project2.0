using HandyControl.Controls;
using HandyControl.Data;
using SqlSugar;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Services;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.ViewModels.Pages.HawkEye;
using SUNWODA_SEVB.Views.Pages.CCTB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB
{
    [Module("VM_CCTBMonitoringPage", "系统记录", Order = 0, Category = "VM_CCTBMonitoringPage")]
    class VM_CCTBMonitoringPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CCTBMonitoringPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        private Page? _navigationPage;
        private ModuleInfo? _selectedModule;
        private bool _isNavigating;
        private string _loadingMessage = "";

        // 添加导航防抖
        private CancellationTokenSource? _navigationCts;
        private DateTime _lastNavigationTime = DateTime.MinValue;
        private const int NAVIGATION_DEBOUNCE_MS = 100;

        public Page? NavigationPage
        {
            get => _navigationPage;
            set => SetProperty(ref _navigationPage, value);
        }

        public ModuleInfo? SelectedModule
        {
            get => _selectedModule;
            set
            {
                SetProperty(ref _selectedModule, value);
                if (value != null && !IsNavigating)
                {
                    // 使用防抖机制避免频繁导航
                    NavigateToModuleDebounced(value);
                }
            }
        }

        public bool IsNavigating
        {
            get => _isNavigating;
            set => SetProperty(ref _isNavigating, value);
        }

        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        public ICommand? SummaryCommand { get; set; }
        public ICommand? TBDataCommand { get; set; }
        public ICommand? CCDDataCommand { get; set; }
        public ICommand? AlarmRecordingsCommand { get; set; }
        public ICommand? ControlRecordingsCommand { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_CCTBMonitoringPage(
            ILoggerService<VM_CCTBMonitoringPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
        }

        public override void OnInitialize()
        {
            InitNavigation();

            AlarmRecordingsCommand = new RelayCommand(AlarmRecordingsOpen);
            ControlRecordingsCommand = new RelayCommand(ControlRecordingsOpen);

            base.OnInitialize();
        }

        private void InitNavigation()
        {
            // 显示加载提示
            IsNavigating = true;
            LoadingMessage = "正在初始化导航系统...";

            // 加载模块
            _moduleManager.AutoRegisterModulesByName("VM_SummaryPage", "VM_TBDataPage", "VM_CCDDataPage");

            SummaryCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_SummaryPage"));
            TBDataCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_TBDataPage"));
            CCDDataCommand = new RelayCommand(() => NavigateToSpecialModuleAsync("VM_CCDDataPage"));

            LoadingMessage = "";
            IsNavigating = false;
            NavigateToDefaultAsync();
        }

        private async void NavigateToDefaultAsync()
        {
            var defaultModule = _moduleManager.GetModule("VM_SummaryPage");
            if (defaultModule != null)
            {
                defaultModule.IsSelected = true;
                SelectedModule = defaultModule;
            }
        }

        private async void NavigateToModuleDebounced(ModuleInfo module)
        {
            // 取消之前的导航
            _navigationCts?.Cancel();
            _navigationCts = new CancellationTokenSource();

            var now = DateTime.Now;
            var timeSinceLastNav = (now - _lastNavigationTime).TotalMilliseconds;

            // 如果距离上次导航时间太短，等待一下
            if (timeSinceLastNav < NAVIGATION_DEBOUNCE_MS)
            {
                await Task.Delay(
                    NAVIGATION_DEBOUNCE_MS - (int)timeSinceLastNav,
                    _navigationCts.Token
                );
            }

            if (!_navigationCts.Token.IsCancellationRequested)
            {
                _lastNavigationTime = DateTime.Now;
                NavigateToModuleAsync(module);
            }
        }

        private async void NavigateToModuleAsync(ModuleInfo module)
        {
            if (string.IsNullOrEmpty(module.Name))
                return;

            try
            {
                IsNavigating = true;
                LoadingMessage = $"正在加载 {module.DisplayName}...";

                NavigationPage = _moduleManager.GetView(module.Name);
            }
            catch (Exception ex)
            {
                _logger?.Error($"导航到 {module.Name} 时发生错误", ex, true);
            }
            finally
            {
                // 延迟一点清除加载状态，避免闪烁
                await Task.Delay(50);
                IsNavigating = false;
                LoadingMessage = "";
            }
        }

        private async void NavigateToSpecialModuleAsync(string moduleName)
        {
            if (IsNavigating)
                return;

            var module = _moduleManager.GetModule(moduleName);

            if (module != null)
            {
                if (SelectedModule != null)
                    SelectedModule.IsSelected = false;
                SelectedModule = module;
            }
        }

        public override void OnNavigatedTo(object? parameter)
        {
            Growl.Success(new GrowlInfo
            {
                Message = "Success：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
            Growl.Info(new GrowlInfo
            {
                Message = "Info：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
            Growl.Warning(new GrowlInfo
            {
                Message = "Warning：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = true,
                StaysOpen = false,
                WaitTime = 5,
            });
            Growl.Error(new GrowlInfo
            {
                Message = "Error：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
            Growl.Fatal(new GrowlInfo
            {
                Message = "Fatal：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
            Growl.Ask(new GrowlInfo
            {
                Message = "Ask：通讯错误提示通讯错误提示通讯错误提示通讯错误提示通讯错误提示...",
                Token = "ProductionMonitor",
                ShowDateTime = false,
                StaysOpen = false,
                WaitTime = 5,
            });
            base.OnNavigatedTo(parameter);
        }

        protected override async Task OnCleanupAsync()
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();

            await base.OnCleanupAsync();
        }

        int count = 0;
        int passCount = 0;
        Random rm = new Random();
        string barCode = "AASDJNALK123J";
        string header = "采集时间,报文标识,AB面,数据类型,基材值,A面目标值,B面目标值,A面公差值,B面公差值,趟号,扫描类型,重量1,重量2,重量3,重量4,重量5,重量6,重量7,重量8,重量9,重量10,重量11,重量12,重量13,重量14,重量15,重量16,重量17,重量18,重量19,重量20,重量21,重量22,重量23,重量24,重量25";
        private void AlarmRecordingsOpen()
        {
            //var asdasdas = "388.2805,390.5524,390.2676,390.7733,388.345,388.8252,387.6052,387.4778,388.1957,388.2536,389.3824,388.3875,389.1212,387.9193,388.368,387.2703,387.8078,387.5919,386.6514,387.5937,387.1837,387.7261,386.118,386.6774,386.5619,386.9858,386.5417,388.0247,389.4489,387.677,389.0315,388.0116,386.4086,386.6678,385.9702,386.0884,381.9575";
            //var vsss = asdasdas.Split(',').Select(o => o.ToDouble());
            //var cpk = vsss.CPK(388, 4);
            var data = $"2025-12-26 08:58,BT,B,干膜净重分区数据,53,324,330,14,17,{passCount++},-1";
            for (int i = 0; i < 25; i++)
            {
                var weightData = rm.NextDouble() * 40 + 300;
                data += $",{weightData}";
            }
            var path = $"D:\\通用上位机\\闭环数据\\涂布重量数据\\TCPClient\\{DateTime.Today:yyyy-MM-dd}\\" + barCode + count + "_A面.csv";
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
            var fe = File.Exists(path);

            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            using var sw = new StreamWriter(fs, Encoding.UTF8);
            var wd = new CCTBWeightDataModel();
            var config = CCTBWeightDataModelMap.GetCsvConfiguration();
            using var csv = new CsvHelper.CsvWriter(sw, config);
            csv.Context.RegisterClassMap<CCTBWeightDataModelMap>();
            if (!fe || fs.Length == 0)
            {
                csv.WriteHeader(typeof(CCTBWeightDataModel));
                csv.NextRecord(); // 移动到下一行
            }
            csv.WriteRecord(wd);
            csv.NextRecord(); // 移动到下一行
            csv.Flush();

            //if (!fe) sw.WriteLine(header);
            //for (int i = 0; i < 1; i++) sw.WriteLine(data);
            //var path1 = $"D:\\通用上位机\\闭环数据\\涂布重量数据\\TCPClient\\{DateTime.Today:yyyy-MM-dd}\\" + barCode + count + "_B面.csv";
            //var fe1 = File.Exists(path1);
            //using var sw1 = new StreamWriter(path1, true, Encoding.UTF8);
            //if (!fe1) sw1.WriteLine(header);
            //for (int i = 0; i < 1; i++) sw1.WriteLine(data);
        }

        private void ControlRecordingsOpen()
        {
            count++;
        }
    }
}
