using DnsClient.Protocol;
using DocumentFormat.OpenXml.Bibliography;
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
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RecordModels;
using SUNWODA_SEVB.Data.Repositories;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
        /// 展示信息
        /// </summary>
        public ICommand? ShowMessageCommand => new RelayCommand(OnShowMessageAsync);

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
            var records = await _appLogRepository.GetUserRecordsPagedAsync(1, PageSize, StartDate, EndDate);
            _ = VM_CCTBMessageBoxDialog.InfoAsync($"已查询到[{records.Total}]条数据！", "提示");
            RefreshRecordInfo(1, records);
            startDateCache = StartDate;
            endDateCache = EndDate;
        }

        private CancellationTokenSource? cts = null;
        /// <summary>
        /// 当导出当前页按钮点击时触发
        /// </summary>
        private async void OnExportCurrentPageAsync()
        {
            if (cts != null)
            {
                var result = await VM_CCTBMessageBoxDialog.ShowAsync("[系统操作记录]正在导出中，是否取消？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
                cts.Cancel();
            }

            cts = new CancellationTokenSource();
            try
            {
                if (Records.Count == 0)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync("查询到0条数据，已停止导出！", "提示");
                    return;
                }

                var re1 = await VM_CCTBMessageBoxDialog.ShowAsync($"已查询到[{Records.Count}]条数据，是否确认导出？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (re1 != MessageBoxResult.Yes) return;

                var fileName = $"SystemRecords_{DateTime.Now:yyyyMMddHHmmss}.csv";
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fullPath = Path.Combine(desktopPath, "系统操作记录");
                if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
                var filePath = Path.Combine(fullPath, fileName);

                if (File.Exists(filePath))
                {
                    var re2 = await VM_CCTBMessageBoxDialog.ShowAsync($"[{filePath}]文件已存在，是否覆盖？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (re2 != MessageBoxResult.Yes) return;
                }

                var task = CsvSaveHelper.WriteToFileAsync(filePath, Records); ;
                await task.WaitAsync(cts.Token);
                await VM_CCTBMessageBoxDialog.SuccessAsync($"已成功导出{Records.Count}条[系统操作记录]，导出路径[{filePath}]!", "导出完成");
            }
            catch (Exception ex) when (ex is OperationCanceledException)
            {
                _logger.UserInfo("用户取消了导出操作[系统操作记录]。");
            }
            catch (Exception ex)
            {
                _logger.UserError($"[系统操作记录]导出过程中发生错误: {ex.Message}");
                await VM_CCTBMessageBoxDialog.ErrorAsync($"导出失败[系统操作记录]: {ex.Message}", "错误");
            }
            finally
            {
                cts.Dispose();
                cts = null;
            }
        }

        /// <summary>
        /// 当导出当前查询按钮点击时触发
        /// </summary>
        private async void OnExportCurrentQueryAsync()
        {
            if (cts != null)
            {
                var result = await VM_CCTBMessageBoxDialog.ShowAsync("[系统操作记录]正在导出中，是否取消？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
                cts?.Cancel();
                cts = null;
            }

            cts = new CancellationTokenSource();
            try
            {
                var fileName = $"SystemRecords_{DateTime.Now:yyyyMMddHHmmss}.csv";
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fullPath = Path.Combine(desktopPath, "系统操作记录");
                var filePath = Path.Combine(fullPath, fileName);

                if (File.Exists(filePath))
                {
                    var re1 = await VM_CCTBMessageBoxDialog.ShowAsync($"[{filePath}]文件已存在，是否覆盖？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (re1 != MessageBoxResult.Yes) return;
                    File.Delete(filePath);
                }

                var count = await _appLogRepository.GetUserRecordsCountAsync(startDateCache, endDateCache);
                if (count == 0)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync("查询到0条数据，已停止导出！", "提示");
                    return;
                }

                var re2 = await VM_CCTBMessageBoxDialog.ShowAsync($"已查询到[{count}]条数据，是否确认导出？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (re2 != MessageBoxResult.Yes) return;

                var rows = new List<Dictionary<string, string>>();
                var pageCount = (count + PageSize - 1) / PageSize;
                for (int i = 0; i < pageCount; i++)
                {
                    rows.Clear();
                    var records = await _appLogRepository.GetUserRecordsPagedAsync(i + 1, PageSize, startDateCache, endDateCache);
                    for (int j = 0; j < records.Items.Count; j++)
                    {
                        var record = ConvertToRecord(records.Items[j]);
                        record.ID = i * PageSize + j + 1;
                        rows.Add(ConvertToDic(record));
                    }
                    var task = Task.Run(() => CCTBCsvHelper.WriteCSV(filePath, CCTBCsvHelper.DicToTable(rows.ToArray())));
                    await task.WaitAsync(cts.Token);
                }

                await VM_CCTBMessageBoxDialog.SuccessAsync($"已成功导出{count}条[系统操作记录]，导出路径[{filePath}]!", "导出完成");
            }
            catch (Exception ex) when (ex is OperationCanceledException)
            {
                _logger.UserInfo("用户取消了导出操作[系统操作记录]。");
            }
            catch (Exception ex)
            {
                _logger.UserError($"[系统操作记录]导出过程中发生错误: {ex.Message}");
                await VM_CCTBMessageBoxDialog.ErrorAsync($"导出失败[系统操作记录]: {ex.Message}", "错误");
            }
            finally
            {
                cts.Dispose();
                cts = null;
            }
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

        /// <summary>
        /// 当点击信息时触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnShowMessageAsync(object? arg)
        {
            if (!(arg is string message)) return;
            if (string.IsNullOrEmpty(message)) return;

            await VM_CCTBMessageBoxDialog.InfoAsync(message, "系统操作记录");
        }

        #region 系统操作日志记录
        /// <summary>
        /// 每页条数
        /// </summary>
        private const int PageSize = 20;

        /// <summary>
        /// 缓存：用于导出使用
        /// </summary>
        private DateTime? startDateCache, endDateCache;

        private DateTime? startDate, endDate;
        private int totalCount, pageCount, currentPageNumber;
        private SystemRecordModel? selectedRecord;

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
        /// 当前选中的记录
        /// </summary>
        public SystemRecordModel? SelectedRecord { get => selectedRecord; set => SetProperty(ref selectedRecord, value); }

        /// <summary>
        /// 加载日志记录
        /// </summary>
        /// <param name="pageNumber"></param>
        private void LoadRecords(int pageNumber = 1)
        {
            var records = _appLogRepository.GetUserRecordsPaged(pageNumber, PageSize, StartDate, EndDate);
            RefreshRecordInfo(pageNumber, records);
        }

        /// <summary>
        /// 更新记录信息
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="records"></param>
        private void RefreshRecordInfo(int pageNumber, (List<AppLogModel> Items, int Total) records)
        {
            Records.Clear();
            TotalCount = records.Total;
            PageCount = (records.Total + PageSize - 1) / PageSize;
            CurrentPageNumber = pageNumber;

            for (int i = 0; i < records.Items.Count; i++)
            {
                var record = ConvertToRecord(records.Items[i]);
                record.ID = (pageNumber - 1) * PageSize + i + 1;
                Records.Add(record);
            }

            SelectedRecord = Records.FirstOrDefault();
        }

        private SystemRecordModel ConvertToRecord(AppLogModel log)
        {
            SystemRecordModel? record;
            try
            {
                record = JsonConvert.DeserializeObject<SystemRecordModel>(log.Message);
            }
            catch
            {
                record = null;
            }

            if (record == null)
            {
                record = new SystemRecordModel();
                record.Message = log.Message;
            }
            record.RecordLevel = log.LogLevel;
            record.RecordTime = log.LogTime;

            return record;
        }

        /// <summary>
        /// 装换为字典
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDic(SystemRecordModel record)
        {
            var result = new Dictionary<string, string>();
            result["排序"] = record.ID.ToString();
            result["用户姓名"] = record.Name?.ToString() ?? string.Empty;
            result["账号"] = record.Account?.ToString() ?? string.Empty;
            result["角色"] = record.Role?.ToString() ?? string.Empty;
            result["操作记录"] = record.Message?.ToString() ?? string.Empty;
            result["等级"] = record.RecordLevel?.ToString() ?? string.Empty;
            result["操作时间"] = record.RecordTime?.ToString() ?? string.Empty;

            return result;
        }
        #endregion
    }
}
