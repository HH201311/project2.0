using ControlzEx.Standard;
using DnsClient.Protocol;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools.Extension;
using HslCommunication;
using Masuit.Tools;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using Python.Runtime;
using SqlSugar;
using SUNWODA_SEVB.Component.UserControls;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Common.ObjectModel;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.JROH;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.JROH.Core;
using SUNWODA_SEVB.Core.Models.Project.JROH.MCBusinessModels;
using SUNWODA_SEVB.Services.Project.JROH.Business;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Input;
using System.Windows.Interop;

namespace SUNWODA_SEVB.ViewModels.Pages.JROH
{
    /// <summary>
    /// 导航&按钮等界面功能（性能优化版）
    /// </summary>
    [Module("VM_MCOfJROHPage", "多级联动纠偏闭环", Order = 0, Category = "VM_MCOfJROHPage")]
    partial class VM_MCOfJROHPage : ViewModelBase
    {
        private readonly ILoggerService<VM_MCOfJROHPage> _logger;
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
        public VM_MCOfJROHPage(
            ILoggerService<VM_MCOfJROHPage> logger,
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
        /// 异步初始化
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
        /// 清理资源
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
        /// 正极入料纠偏数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> InfeedOfPosSeries1 { get; set; } = [];

        /// <summary>
        /// 正极入料纠偏设定值数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> InfeedOfPosSeries2 { get; set; } = [];

        /// <summary>
        /// 正极蛇形4纠偏数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake4OfPosSeries1 { get; set; } = [];

        /// <summary>
        /// 正极蛇形4纠偏设定值数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake4OfPosSeries2 { get; set; } = [];

        /// <summary>
        /// 正极蛇形3纠偏数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake3OfPosSeries1 { get; set; } = [];

        /// <summary>
        /// 正极蛇形3纠偏设定值数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake3OfPosSeries2 { get; set; } = [];

        /// <summary>
        /// 正极蛇形2纠偏数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake2OfPosSeries1 { get; set; } = [];

        /// <summary>
        /// 正极蛇形2纠偏设定值数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake2OfPosSeries2 { get; set; } = [];

        /// <summary>
        /// 正极蛇形1纠偏数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake1OfPosSeries1 { get; set; } = [];

        /// <summary>
        /// 正极蛇形1纠偏设定值数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake1OfPosSeries2 { get; set; } = [];

        /// <summary>
        /// 负极入料纠偏数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> InfeedOfNegSeries1 { get; set; } = [];

        /// <summary>
        /// 负极入料纠偏设定值数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> InfeedOfNegSeries2 { get; set; } = [];

        /// <summary>
        /// 负极蛇形3纠偏数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake3OfNegSeries1 { get; set; } = [];

        /// <summary>
        /// 负极蛇形3纠偏设定值数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake3OfNegSeries2 { get; set; } = [];

        /// <summary>
        /// 负极蛇形2纠偏数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake2OfNegSeries1 { get; set; } = [];

        /// <summary>
        /// 负极蛇形2纠偏设定值数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake2OfNegSeries2 { get; set; } = [];

        /// <summary>
        /// 负极蛇形1纠偏数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake1OfNegSeries1 { get; set; } = [];

        /// <summary>
        /// 负极蛇形1纠偏设定值数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> Snake1OfNegSeries2 { get; set; } = [];

        /// <summary>
        /// 上隔膜纠偏数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> UpperDiaSeries1 { get; set; } = [];

        /// <summary>
        /// 上隔膜纠偏设定值数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> UpperDiaSeries2 { get; set; } = [];

        /// <summary>
        /// 下隔膜纠偏数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> LowerDiaSeries1 { get; set; } = [];

        /// <summary>
        /// 下隔膜纠偏设定值数据曲线
        /// </summary>
        public ObservableCollection<(DateTime, double)> LowerDiaSeries2 { get; set; } = [];

        private int count = 0;
        /// <summary>
        /// 当接受数据时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDataReceived(object? sender, object e)
        {
            if (e is not (MCBusinessGatherModel gatherData, MCBusinessParamModel param)) return;

            // 确保在 UI 线程执行（如果事件可能来自后台线程）
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnDataReceived(sender, e));
                return;
            }

            if (gatherData != null)
            {
                if (count >= param.T)
                {
                    InfeedOfPosSeries1.Clear();
                    InfeedOfPosSeries2.Clear();
                    Snake4OfPosSeries1.Clear();
                    Snake4OfPosSeries2.Clear();
                    Snake3OfPosSeries1.Clear();
                    Snake3OfPosSeries2.Clear();
                    Snake2OfPosSeries1.Clear();
                    Snake2OfPosSeries2.Clear();
                    Snake1OfPosSeries1.Clear();
                    Snake1OfPosSeries2.Clear();
                    InfeedOfNegSeries1.Clear();
                    InfeedOfNegSeries2.Clear();
                    Snake3OfNegSeries1.Clear();
                    Snake3OfNegSeries2.Clear();
                    Snake2OfNegSeries1.Clear();
                    Snake2OfNegSeries2.Clear();
                    Snake1OfNegSeries1.Clear();
                    Snake1OfNegSeries2.Clear();
                    UpperDiaSeries1.Clear();
                    UpperDiaSeries2.Clear();
                    LowerDiaSeries1.Clear();
                    LowerDiaSeries2.Clear();
                }
                foreach (var value in gatherData.InfeedDataOfPos.Values)
                {
                    InfeedOfPosSeries1.Add((value.time, value.detValue));
                    InfeedOfPosSeries2.Add((value.time, value.tarValue));
                }
                foreach (var value in gatherData.Snake4DataOfPos.Values)
                {
                    Snake4OfPosSeries1.Add((value.time, value.detValue));
                    Snake4OfPosSeries2.Add((value.time, value.tarValue));
                }
                foreach (var value in gatherData.Snake3DataOfPos.Values)
                {
                    Snake3OfPosSeries1.Add((value.time, value.detValue));
                    Snake3OfPosSeries2.Add((value.time, value.tarValue));
                }
                foreach (var value in gatherData.Snake2DataOfPos.Values)
                {
                    Snake2OfPosSeries1.Add((value.time, value.detValue));
                    Snake2OfPosSeries2.Add((value.time, value.tarValue));
                }
                foreach (var value in gatherData.Snake1DataOfPos.Values)
                {
                    Snake1OfPosSeries1.Add((value.time, value.detValue));
                    Snake1OfPosSeries2.Add((value.time, value.tarValue));
                }
                foreach (var value in gatherData.InfeedDataOfNeg.Values)
                {
                    InfeedOfNegSeries1.Add((value.time, value.detValue));
                    InfeedOfNegSeries2.Add((value.time, value.tarValue));
                }
                foreach (var value in gatherData.Snake3DataOfNeg.Values)
                {
                    Snake3OfNegSeries1.Add((value.time, value.detValue));
                    Snake3OfNegSeries2.Add((value.time, value.tarValue));
                }
                foreach (var value in gatherData.Snake2DataOfNeg.Values)
                {
                    Snake2OfNegSeries1.Add((value.time, value.detValue));
                    Snake2OfNegSeries2.Add((value.time, value.tarValue));
                }
                foreach (var value in gatherData.Snake1DataOfNeg.Values)
                {
                    Snake1OfNegSeries1.Add((value.time, value.detValue));
                    Snake1OfNegSeries2.Add((value.time, value.tarValue));
                }
                foreach (var value in gatherData.UpperDiaphragmData.Values)
                {
                    UpperDiaSeries1.Add((value.time, value.detValue));
                    UpperDiaSeries2.Add((value.time, value.tarValue));
                }
                foreach (var value in gatherData.LowerDiaphragmData.Values)
                {
                    LowerDiaSeries1.Add((value.time, value.detValue));
                    LowerDiaSeries2.Add((value.time, value.tarValue));
                }

                count++;
            }
        }
        #endregion

        #region 调机记录数据
        private DataTable posRecords = new(), negRecords = new(), diaphragmRecords = new();
        private DataView? posRecordsView, negRecordsView, diaphragmRecordsView;

        /// <summary>
        /// 调机记录界面绑定数据-正极多级纠偏
        /// </summary>
        public DataView? PosRecordsView { get => posRecordsView; set => SetProperty(ref posRecordsView, value); }

        /// <summary>
        /// 调机记录界面绑定数据-负极多级纠偏
        /// </summary>
        public DataView? NegRecordsView { get => negRecordsView; set => SetProperty(ref negRecordsView, value); }

        /// <summary>
        /// 调机记录界面绑定数据-隔膜多级纠偏
        /// </summary>
        public DataView? DiaphragmRecordsView { get => diaphragmRecordsView; set => SetProperty(ref diaphragmRecordsView, value); }

        /// <summary>
        /// 当接收到数据时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAdjustmentRecordReceived(object? sender, object e)
        {
            if (e == null) return;
            if (e is not (string cl, Dictionary<string, string> record)) return;
            if (record is null || record.Count == 0) return;

            // 确保在 UI 线程执行（如果事件可能来自后台线程）
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnAdjustmentRecordReceived(sender, e));
                return;
            }

            // 代做判断
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
                return;
            }

            if (cl == "DiaphragmCorrect" && RefreshRecords(record, ref diaphragmRecords))
            {
                DiaphragmRecordsView = null;
                DiaphragmRecordsView = diaphragmRecords.DefaultView;
                return;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="record"></param>
        /// <param name="records"></param>
        /// <returns></returns>
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
        public EBusinessType BusinessType => EBusinessType.MultiCorrect;

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