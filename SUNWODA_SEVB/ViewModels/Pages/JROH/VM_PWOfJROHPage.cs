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
using SUNWODA_SEVB.Core.Models.Project.JROH.PWBusinessModels;
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
    [Module("VM_PWOfJROHPage", "极片宽度闭环", Order = 0, Category = "VM_PWOfJROHPage")]
    partial class VM_PWOfJROHPage : ViewModelBase
    {
        private readonly ILoggerService<VM_PWOfJROHPage> _logger;
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
        public VM_PWOfJROHPage(
            ILoggerService<VM_PWOfJROHPage> logger,
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
        private double upperOfPos = 191300, lowerOfPos = 190300, upperOfNeg = 191500, lowerOfNeg = 190500;
        public double UpperOfPos { get => upperOfPos; set => SetProperty(ref upperOfPos, value); }
        public double LowerOfPos { get => lowerOfPos; set => SetProperty(ref lowerOfPos, value); }
        public double UpperOfNeg { get => upperOfNeg; set => SetProperty(ref upperOfNeg, value); }
        public double LowerOfNeg { get => lowerOfNeg; set => SetProperty(ref lowerOfNeg, value); }
        public ObservableCollection<(DateTime, double)> SeriesOfPos { get; set; } = [];
        public ObservableCollection<(DateTime, double)> SeriesOfNeg { get; set; } = [];

        /// <summary>
        /// 当接受数据时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDataReceived(object? sender, object e)
        {
            if (e is not (PWBusinessGatherModel gatherData, PWBusinessParamModel param)) return;

            // 确保在 UI 线程执行（如果事件可能来自后台线程）
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnDataReceived(sender, e));
                return;
            }

            if (gatherData != null)
            {
                SeriesOfPos.Add((DateTime.Now, gatherData.ModuleWidthOfPos));
                while (SeriesOfPos.Count > 1000) SeriesOfPos.RemoveAt(0);
                SeriesOfNeg.Add((DateTime.Now, gatherData.ModuleWidthOfNeg));
                while (SeriesOfNeg.Count > 1000) SeriesOfNeg.RemoveAt(0);
            }
            if (param != null)
            {
                UpperOfPos = param.PoleWidthOfPos_UpperLimit;
                LowerOfPos = param.PoleWidthOfPos_LowerLimit;
                UpperOfNeg = param.PoleWidthOfNeg_UpperLimit;
                LowerOfNeg = param.PoleWidthOfNeg_LowerLimit;
            }
        }
        #endregion

        #region 调机记录数据
        private DataTable adjustmentRecords = new();
        private DataView? adjustmentRecordsView;

        /// <summary>
        /// 调机记录界面绑定数据
        /// </summary>
        public DataView? AdjustmentRecordsView { get => adjustmentRecordsView; set => SetProperty(ref adjustmentRecordsView, value); }

        /// <summary>
        /// 当接收到数据时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAdjustmentRecordReceived(object? sender, object e)
        {
            if (e == null) return;
            if (e is not Dictionary<string, string> record) return;

            // 确保在 UI 线程执行（如果事件可能来自后台线程）
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnAdjustmentRecordReceived(sender, e));
                return;
            }

            // 动态添加缺失的列（如果需要）
            var columnsChanged = false;
            foreach (var key in record.Keys)
            {
                if (!adjustmentRecords.Columns.Contains(key))
                {
                    adjustmentRecords.Columns.Add(key, typeof(string));
                    columnsChanged = true;
                }
            }

            // 创建新行并填充数据
            var row = adjustmentRecords.NewRow();
            foreach (var kvp in record)
            {
                row[kvp.Key] = kvp.Value;
            }
            adjustmentRecords.Rows.Add(row);

            // 保持固定容量 20：删除最旧的行
            while (adjustmentRecords.Rows.Count > 20)
            {
                adjustmentRecords.Rows.RemoveAt(0);
            }

            // 如果列有变化，重新赋值 DataView 触发 UI 刷新列
            if (columnsChanged)
            {
                AdjustmentRecordsView = null;
                AdjustmentRecordsView = adjustmentRecords.DefaultView;
            }
        }
        #endregion

        #region 业务参数
        private UserRole userRole;

        /// <summary>
        /// 业务类型
        /// </summary>
        public EBusinessType BusinessType => EBusinessType.PoleWidth;

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