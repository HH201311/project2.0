using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.JROH;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Project.JROH.MisBusinessModels;
using SUNWODA_SEVB.Services.Project.JROH.Business;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.JROH
{
    /// <summary>
    /// 极耳错位闭环页面（导航&amp;按钮等界面功能）
    /// </summary>
    /// <remarks>
    /// <para>结构对齐 <c>VM_MCOfJROHPage</c>：进出页面自动启停业务、事件订阅曲线与调机记录、参数配置入口。</para>
    /// <para>⏸ 曲线通道与调机记录列均为模板占位（正极 / 负极两路），待现场资料确认后按真实通道扩展。</para>
    /// </remarks>
    [Module("VM_MisOfJROHPage", "极耳错位闭环", Order = 0, Category = "VM_MisOfJROHPage")]
    partial class VM_MisOfJROHPage : ViewModelBase
    {
        private readonly ILoggerService<VM_MisOfJROHPage> _logger;
        private readonly IPLCService _plcService;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;
        private readonly BusinessServiceFactory _businessFactory;
        private readonly BusinessServiceManager _businessManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_MisOfJROHPage(
            ILoggerService<VM_MisOfJROHPage> logger,
            IPLCService pLCService,
            IGlobalSettingRepository globalSettingRepository,
            IUsersRepository usersRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager,
            BusinessServiceFactory factory,
            BusinessServiceManager businessManager)
        {
            _logger = logger;
            _plcService = pLCService;
            _globalSettingRepository = globalSettingRepository;
            _usersRepository = usersRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
            _businessFactory = factory;
            _businessManager = businessManager;

            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
            UserStateChangedEvent.OnUserStateChanged += OnUserStateChanged;
        }

        #region 生命周期管理
        private CancellationTokenSource _globalCts = new();
        private bool _isDisposed;

        /// <summary>
        /// 异步初始化
        /// </summary>
        public override void OnInitialize()
        {
            base.OnInitialize();
        }

        /// <summary>
        /// 异步初始化（进入页面：订阅事件并启动业务）
        /// </summary>
        protected override async Task OnInitializeAsync()
        {
            var service = _businessFactory.GetService(BusinessType);
            service.MessageNotifation -= OnAdjustmentRecordReceived;
            service.MessageNotifation += OnAdjustmentRecordReceived;
            service.MessageNotifation -= OnDataReceived;
            service.MessageNotifation += OnDataReceived;

            // 业务流程模块
            await _businessManager.StartBusinessAsync(BusinessType);
            await base.OnInitializeAsync();
        }

        /// <summary>
        /// 清理资源（离开页面：解绑事件并停止业务）
        /// </summary>
        protected override async Task OnCleanupAsync()
        {
            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
            var service = _businessFactory.GetService(BusinessType);
            service.MessageNotifation -= OnAdjustmentRecordReceived;
            service.MessageNotifation -= OnDataReceived;

            await _businessManager.StopBusinessAsync(BusinessType);

            _isDisposed = true;
            _globalCts?.Cancel();
            _globalCts?.Dispose();

            await base.OnCleanupAsync();
        }
        #endregion

        #region 数据图表
        /// <summary>
        /// 正极极耳错位检测值曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> PositiveTabSeries1 { get; set; } = [];

        /// <summary>
        /// 正极极耳错位设定值曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> PositiveTabSeries2 { get; set; } = [];

        /// <summary>
        /// 负极极耳错位检测值曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> NegativeTabSeries1 { get; set; } = [];

        /// <summary>
        /// 负极极耳错位设定值曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> NegativeTabSeries2 { get; set; } = [];

        /// <summary>
        /// 单条曲线最多保留的采样点数
        /// </summary>
        private const int MaxPlotPointsPerSeries = 2000;

        /// <summary>
        /// 当接受数据时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDataReceived(object? sender, object e)
        {
            if (e is not (MisBusinessGatherModel gatherData, MisBusinessParamModel param)) return;

            // 确保在 UI 线程执行（事件来自后台消费线程）
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnDataReceived(sender, e));
                return;
            }

            if (gatherData == null) return;

            AppendSeries(PositiveTabSeries1, PositiveTabSeries2, gatherData.PositiveTabValues);
            AppendSeries(NegativeTabSeries1, NegativeTabSeries2, gatherData.NegativeTabValues);
        }

        /// <summary>
        /// 追加一路通道的检测值/设定值曲线，并按上限裁剪历史点
        /// </summary>
        /// <param name="detectSeries">检测值曲线</param>
        /// <param name="targetSeries">设定值曲线</param>
        /// <param name="data">通道采集数据</param>
        private static void AppendSeries(
            ObservableCollection<(DateTime, double)> detectSeries,
            ObservableCollection<(DateTime, double)> targetSeries,
            List<(DateTime time, float detValue, float tarValue)>? data)
        {
            if (data == null || data.Count == 0) return;

            foreach (var value in data)
            {
                detectSeries.Add((value.time, value.detValue));
                targetSeries.Add((value.time, value.tarValue));
            }

            TrimSeries(detectSeries);
            TrimSeries(targetSeries);
        }

        /// <summary>
        /// 按上限裁剪曲线历史点（长跑不无限增长，规范 G-06）
        /// </summary>
        private static void TrimSeries(ObservableCollection<(DateTime, double)> series)
        {
            while (series.Count > MaxPlotPointsPerSeries) series.RemoveAt(0);
        }
        #endregion

        #region 调机记录数据
        private DataTable posRecords = new(), negRecords = new();
        private DataView? posRecordsView, negRecordsView;

        /// <summary>
        /// 调机记录界面绑定数据-正极
        /// </summary>
        public DataView? PosRecordsView { get => posRecordsView; set => SetProperty(ref posRecordsView, value); }

        /// <summary>
        /// 调机记录界面绑定数据-负极
        /// </summary>
        public DataView? NegRecordsView { get => negRecordsView; set => SetProperty(ref negRecordsView, value); }

        /// <summary>
        /// 当接收到闭环计算结果时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAdjustmentRecordReceived(object? sender, object e)
        {
            if (e == null) return;
            if (e is not (string cl, Dictionary<string, string> record)) return;
            if (record is null || record.Count == 0) return;

            // 确保在 UI 线程执行
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnAdjustmentRecordReceived(sender, e));
                return;
            }

            if (cl == "PosCorrect" && RefreshRecords(record, ref posRecords))
            {
                PosRecordsView = null;
                PosRecordsView = posRecords.DefaultView;
                return;
            }

            if (cl == "NegCorrect" && RefreshRecords(record, ref negRecords))
            {
                NegRecordsView = null;
                NegRecordsView = negRecords.DefaultView;
            }
        }

        /// <summary>
        /// 追加一行调机记录（动态补列，保留最近 20 行）
        /// </summary>
        /// <param name="record"></param>
        /// <param name="records"></param>
        /// <returns>是否新增了列（需重新绑定 DataView）</returns>
        private bool RefreshRecords(Dictionary<string, string> record, ref DataTable records)
        {
            // 动态添加缺失的列（如果需要）
            var columnsChanged = false;
            foreach (var key in record.Keys)
            {
                if (!records.Columns.Contains(key))
                {
                    records.Columns.Add(key, typeof(string));
                    columnsChanged = true;
                }
            }

            // 创建新行并填充数据
            var row = records.NewRow();
            foreach (var kvp in record)
            {
                row[kvp.Key] = kvp.Value;
            }
            records.Rows.Add(row);

            // 保持固定容量 20：删除最旧的行
            while (records.Rows.Count > 20)
            {
                records.Rows.RemoveAt(0);
            }

            return columnsChanged;
        }
        #endregion

        #region 业务参数
        private UserRole userRole;

        /// <summary>
        /// 业务类型
        /// </summary>
        public EBusinessType BusinessType => EBusinessType.Mismatch;

        /// <summary>
        /// 当前的账号权限
        /// </summary>
        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        /// <summary>
        /// 业务参数配置命令
        /// </summary>
        public ICommand? BusinessParamCommand => new RelayCommand(OnBusinessParamCommandAsync);

        /// <summary>
        /// 用户状态变更事件处理
        /// </summary>
        /// <param name="userAccount"></param>
        private void OnUserStateChanged(string userAccount)
        {
            var roleId = _usersRepository.GetByUserAccount(userAccount)?.RoleId ?? 0;
            UserRole = (UserRole)roleId;
        }

        /// <summary>
        /// 打开参数界面
        /// </summary>
        /// <param name="arg"></param>
        private async void OnBusinessParamCommandAsync(object? arg)
        {
            var service = _businessFactory.GetService(BusinessType);
            await service.ShowBusinessParamConfigDialogAsync();
        }
        #endregion
    }
}
