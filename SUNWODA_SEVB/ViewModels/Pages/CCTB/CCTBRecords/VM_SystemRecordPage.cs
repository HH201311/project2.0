using DnsClient.Protocol;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Wordprocessing;
using HandyControl.Controls;
using Masuit.Tools;
using NetTaste;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SharpCompress.Common;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Component.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RecordModels;
using SUNWODA_SEVB.Data.Repositories;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBRecords
{
    [Module("VM_SystemRecordPage", "系统操作", Order = 2, Category = "VM_CCTBRecordsPage/VM_SystemRecordPage")]
    class VM_SystemRecordPage : ViewModelBase
    {
        private readonly ILoggerService<VM_SystemRecordPage> _logger;
        private readonly IAppLogRepository _appLogRepository;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;
        private readonly OperationUtil _operationUtil;

        /// <summary>
        /// 查询按钮
        /// </summary>
        public ICommand? QueryCommand => new RelayCommand(OnQueryAsync);

        /// <summary>
        /// 导出当前页按钮
        /// </summary>
        public ICommand? ExportCurrentPageCommand => new RelayCommand(OnExportCurrentPageAsync);

        /// <summary>
        /// 导出当前查询按钮
        /// </summary>
        public ICommand? ExportCurrentQueryCommand => new RelayCommand(OnExportCurrentQueryAsync);

        /// <summary>
        /// 切换查询页
        /// </summary>
        public ICommand? GotoPageCommand => new RelayCommand(OnGotoPageAsync);

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_SystemRecordPage(
            ILoggerService<VM_SystemRecordPage> logger,
            IAppLogRepository appLogRepository,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        )
        {
            _logger = logger;
            _appLogRepository = appLogRepository;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;

            _operationUtil = new OperationUtil(projectSettingRepository);
        }

        /// <summary>
        /// 导航结束时触发
        /// </summary>
        /// <param name="parameter"></param>
        public override void OnNavigatedTo(object? parameter)
        {
            LoadRecords();
            base.OnNavigatedTo(parameter);
        }

        /// <summary>
        /// 当查询按钮点击时触发
        /// </summary>
        private async void OnQueryAsync()
        {
            LoadRecords();
        }

        /// <summary>
        /// 当导出当前页按钮点击时触发
        /// </summary>
        private async void OnExportCurrentPageAsync()
        {
            var message = "点击报警信息中的'点击异常复位'按钮";
            _logger.UserTrace(message);
            _logger.UserDebug(message);
            _logger.UserInfo(message);
            _logger.UserWarn(message);
            _logger.UserError(message);
            _logger.UserFatal(message);
        }

        /// <summary>
        /// 当导出当前查询按钮点击时触发
        /// </summary>
        private async void OnExportCurrentQueryAsync()
        {

        }

        /// <summary>
        /// 当跳转到指定页面是触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnGotoPageAsync(object? arg)
        {
            if (!(arg is int pageNumber)) return;
            LoadRecords(pageNumber);
        }

        #region 系统操作日志记录
        /// <summary>
        /// 每页条数
        /// </summary>
        private const int PerPageCount = 20;

        private DateTime? startDate;
        private DateTime? endDate;
        private int totalCount;
        private int pageCount;
        private int currentPageNumber;

        /// <summary>
        /// 查询起始时间
        /// </summary>
        public DateTime? StartDate { get => startDate; set => SetProperty(ref startDate, value); }

        /// <summary>
        /// 查询结束时间
        /// </summary>
        public DateTime? EndDate { get => endDate; set => SetProperty(ref endDate, value); }

        /// <summary>
        /// 记录总条数
        /// </summary>
        public int TotalCount { get => totalCount; set => SetProperty(ref totalCount, value); }

        /// <summary>
        /// 记录总页数
        /// </summary>
        public int PageCount { get => pageCount; set => SetProperty(ref pageCount, value); }

        /// <summary>
        /// 当前页面编号
        /// </summary>
        public int CurrentPageNumber { get => currentPageNumber; set => SetProperty(ref currentPageNumber, value); }

        /// <summary>
        /// 日志记录
        /// </summary>
        public ObservableCollection<SystemRecordModel> Records { get; set; } = [];

        /// <summary>
        /// 加载日志记录
        /// </summary>
        /// <param name="pageNumber"></param>
        private void LoadRecords(int pageNumber = 1)
        {
            var records = _appLogRepository.GetUserRecordsPaged(pageNumber, PerPageCount, StartDate, EndDate);

            RunOnUIThread(() =>
            {
                Records.Clear();
                TotalCount = records.Total;
                PageCount = records.Total % PerPageCount == 0 ? records.Total / PerPageCount : (records.Total / PerPageCount + 1);
                CurrentPageNumber = pageNumber;

                for (int i = 0; i < records.Items.Count; i++)
                {
                    SystemRecordModel? record;
                    try
                    {
                        record = JsonConvert.DeserializeObject<SystemRecordModel>(records.Items[i].Message);
                    }
                    catch
                    {
                        record = null;
                    }

                    if (record == null)
                    {
                        record = new SystemRecordModel();
                        record.Message = records.Items[i].Message;
                    }
                    record.ID = (pageNumber - 1) * PerPageCount + i + 1;
                    record.RecordLevel = records.Items[i].LogLevel;
                    record.RecordTime = records.Items[i].LogTime;
                    Records.Add(record);
                }
            });
        }
        #endregion
    }
}
