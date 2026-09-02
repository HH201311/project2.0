using ControlzEx.Standard;
using DnsClient.Protocol;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools.Extension;
using HslCommunication;
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
        private double targetOfInput = 0, targetOfCourse = 0, targetOfUnreel = 0;

        /// <summary>
        /// 入料纠偏设定值
        /// </summary>
        public double TargetOfInput { get => targetOfInput; set => SetProperty(ref targetOfInput, value); }

        /// <summary>
        /// 过程纠偏设定值
        /// </summary>
        public double TargetOfCourse { get => targetOfCourse; set => SetProperty(ref targetOfCourse, value); }

        /// <summary>
        /// 放卷纠偏设定值
        /// </summary>
        public double TargetOfUnreel { get => targetOfUnreel; set => SetProperty(ref targetOfUnreel, value); }

        /// <summary>
        /// 入料纠偏检测值
        /// </summary>
        public ObservableCollection<(DateTime, double)> InputSeriesOfTop { get; set; } = [];

        /// <summary>
        /// 过程纠偏检测值
        /// </summary>
        public ObservableCollection<(DateTime, double)> CourseSeriesOfTop { get; set; } = [];

        /// <summary>
        /// 放卷纠偏检测值
        /// </summary>
        public ObservableCollection<(DateTime, double)> UnreelSeriesOfBottom { get; set; } = [];

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
                InputSeriesOfTop.Add((DateTime.Now, gatherData.ModuleWidthOfPos));
                while (InputSeriesOfTop.Count > 1000) InputSeriesOfTop.RemoveAt(0);
                CourseSeriesOfTop.Add((DateTime.Now, gatherData.ModuleWidthOfNeg));
                while (CourseSeriesOfTop.Count > 1000) CourseSeriesOfTop.RemoveAt(0);
                UnreelSeriesOfBottom.Add((DateTime.Now, gatherData.ModuleWidthOfPos));
                while (UnreelSeriesOfBottom.Count > 1000) UnreelSeriesOfBottom.RemoveAt(0);
            }
        }
        #endregion

        #region 调机记录数据
        private DataTable inputRecords = new(), courseRecords = new(), unreelRecords = new();
        private DataView? inputRecordsView, courseRecordsView, unreelRecordsView;

        /// <summary>
        /// 调机记录界面绑定数据-入料纠偏
        /// </summary>
        public DataView? InputRecordsView { get => inputRecordsView; set => SetProperty(ref inputRecordsView, value); }

        /// <summary>
        /// 调机记录界面绑定数据-过程纠偏
        /// </summary>
        public DataView? CourseRecordsView { get => courseRecordsView; set => SetProperty(ref courseRecordsView, value); }

        /// <summary>
        /// 调机记录界面绑定数据-方娟纠偏
        /// </summary>
        public DataView? UnreelRecordsView { get => unreelRecordsView; set => SetProperty(ref unreelRecordsView, value); }

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
            if (cl == "InputCorrect" && RefreshRecords(record, ref inputRecords))
            {
                InputRecordsView = null;
                InputRecordsView = inputRecords.DefaultView;
                return;
            }

            if (cl == "CourseCorrect" && RefreshRecords(record, ref courseRecords))
            {
                CourseRecordsView = null;
                CourseRecordsView = courseRecords.DefaultView;
                return;
            }

            if (cl == "UnreelCorrect" && RefreshRecords(record, ref unreelRecords))
            {
                UnreelRecordsView = null;
                UnreelRecordsView = unreelRecords.DefaultView;
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