using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Masuit.Tools;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Component.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RecordModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBRecords
{
    [Module("VM_AlarmRecordPage", "报警记录", Order = 2, Category = "VM_CCTBRecordsPage/VM_AlarmRecordPage")]
    class VM_AlarmRecordPage : ViewModelBase
    {

        private readonly ILoggerService<VM_AlarmRecordPage> _logger;
        private readonly IAppLogRepository _appLogRepository;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

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
        public VM_AlarmRecordPage(
            ILoggerService<VM_AlarmRecordPage> logger,
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
        }

        /// <summary>
        /// 导航结束时触发
        /// </summary>
        /// <param name="parameter"></param>
        public override void OnNavigatedTo(object? parameter)
        {
            LoadRecordsAsync();
            base.OnNavigatedTo(parameter);
        }

        /// <summary>
        /// 当查询按钮点击时触发
        /// </summary>
        private async void OnQueryAsync()
        {
            var records = await _appLogRepository.GetAdjustmentRecordsPagedAsync(1, PageSize, StartDate, EndDate);
            _ = VM_CCTBMessageBoxDialog.InfoAsync($"已查询到[{records.Total}]条数据！", "提示");
            RefreshRecordInfo(1, records);
            startDateCache = StartDate;
            endDateCache = EndDate;
        }

        /// <summary>
        /// 当导出当前页按钮点击时触发
        /// </summary>
        private async void OnExportCurrentPageAsync()
        {
            try
            {
                if (Records.Count == 0)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync("查询到0条数据，已停止导出！", "提示");
                    return;
                }

                var re1 = await VM_CCTBMessageBoxDialog.ShowAsync($"已查询到[{Records.Count}]条数据，是否确认导出？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (re1 != MessageBoxResult.Yes) return;

                var fileName = $"AdjustmentRecords_{DateTime.Now:yyyyMMddHHmmss}.csv";
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fullPath = Path.Combine(desktopPath, "报警记录");
                if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
                var filePath = Path.Combine(fullPath, fileName);

                if (File.Exists(filePath))
                {
                    var re2 = await VM_CCTBMessageBoxDialog.ShowAsync($"[{filePath}]文件已存在，是否覆盖？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (re2 != MessageBoxResult.Yes) return;
                }

                await Dialog.Show(new VM_CCTBProgressBarDialog().UIElement)
                      .Initialize<VM_CCTBProgressBarDialog>(async o =>
                      {
                          await o.StartExportAsync(async (token) =>
                          {
                              var sw = new Stopwatch();
                              sw.Start();
                              o.UpdateProgress(0, string.Empty);
                              var task = CsvSaveHelper.WriteToFileAsync(filePath, Records); ;
                              await task.WaitAsync(token);
                              sw.Stop();
                              await Task.Delay((int)(2000 - sw.ElapsedMilliseconds));
                              o.IsIndeterminate = false;
                              if (!token.IsCancellationRequested) o.UpdateProgress(100, timeLeft: $"成功导出 {Records.Count} 条数据！");
                          }, true);
                      }).GetResultAsync<bool>();
            }
            catch (Exception ex)
            {
                _logger.UserError($"[报警记录]导出过程中发生错误: {ex.Message}");
                await VM_CCTBMessageBoxDialog.ErrorAsync($"导出失败[报警记录]: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 当导出当前查询按钮点击时触发
        /// </summary>
        private async void OnExportCurrentQueryAsync()
        {
            try
            {
                var count = await _appLogRepository.GetAdjustmentRecordsCountAsync(startDateCache, endDateCache);
                if (count == 0)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync("查询到0条数据，已停止导出！", "提示");
                    return;
                }

                var re1 = await VM_CCTBMessageBoxDialog.ShowAsync($"已查询到[{count}]条数据，是否确认导出？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (re1 != MessageBoxResult.Yes) return;

                var fileName = $"AdjustmentRecords_{DateTime.Now:yyyyMMddHHmmss}.csv";
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fullPath = Path.Combine(desktopPath, "报警记录");
                var filePath = Path.Combine(fullPath, fileName);

                if (File.Exists(filePath))
                {
                    var re2 = await VM_CCTBMessageBoxDialog.ShowAsync($"[{filePath}]文件已存在，是否覆盖？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (re2 != MessageBoxResult.Yes) return;
                    File.Delete(filePath);
                }

                await Dialog.Show(new VM_CCTBProgressBarDialog().UIElement)
                      .Initialize<VM_CCTBProgressBarDialog>(async o =>
                      {
                          await o.StartExportAsync(async (token) =>
                          {
                              var sw = new Stopwatch();
                              sw.Start();
                              o.UpdateProgress(0, string.Empty);

                              var rows = new List<Dictionary<string, string>>();
                              var pageCount = (count + PageSize - 1) / PageSize;
                              var periodTime = 10000 / pageCount;
                              o.IsIndeterminate = !(pageCount > 3);
                              for (int i = 0; i < pageCount; i++)
                              {
                                  rows.Clear();
                                  var records = await _appLogRepository.GetAdjustmentRecordsPagedAsync(i + 1, PageSize, startDateCache, endDateCache);
                                  for (int j = 0; j < records.Items.Count; j++)
                                  {
                                      var record = ConvertToRecord(records.Items[j]);
                                      record.ID = i * PageSize + j + 1;
                                      rows.Add(ConvertToDic(record));
                                  }
                                  var task = Task.Run(() => CCTBCsvHelper.WriteCSV(filePath, CCTBCsvHelper.DicToTable(rows.ToArray())));
                                  await task.WaitAsync(token);
                                  if (pageCount > 3)
                                  {
                                      var exportCount = i * PageSize + rows.Count;
                                      o.UpdateProgress((double)exportCount / (double)count * 100, timeLeft: $"成功导出 {exportCount} 条数据！");
                                      await Task.Delay(periodTime > 1 ? 1 : periodTime);
                                  }
                              }

                              sw.Stop();
                              var delayTime = 2000 - sw.ElapsedMilliseconds;
                              if (delayTime > 0) await Task.Delay((int)delayTime);
                              o.IsIndeterminate = false;
                              if (!token.IsCancellationRequested) o.UpdateProgress(100, timeLeft: $"成功导出 {count} 条数据！");
                          }, false);
                      }).GetResultAsync<bool>();
            }
            catch (Exception ex)
            {
                _logger.UserError($"[报警记录]导出过程中发生错误: {ex.Message}");
                await VM_CCTBMessageBoxDialog.ErrorAsync($"导出失败[报警记录]: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 当跳转到指定页面是触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnGotoPageAsync(object? arg)
        {
            if (!(arg is int pageNumber)) return;
            LoadRecordsAsync(pageNumber);
        }

        /// <summary>
        /// 当点击信息时触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnShowMessageAsync(object? arg)
        {
            if (!(arg is string message)) return;
            if (string.IsNullOrEmpty(message)) return;

            await VM_CCTBMessageBoxDialog.InfoAsync(message, "报警记录");
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
        private AlarmRecordModel? selectedRecord;

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
        public ObservableCollection<AlarmRecordModel> Records { get; set; } = [];

        /// <summary>
        /// 当前选中的记录
        /// </summary>
        public AlarmRecordModel? SelectedRecord { get => selectedRecord; set => SetProperty(ref selectedRecord, value); }

        private CancellationTokenSource? cts_LoadRecords;
        /// <summary>
        /// 加载日志记录
        /// </summary>
        /// <param name="pageNumber"></param>
        private async void LoadRecordsAsync(int pageNumber = 1)
        {
            Records.Clear();
            if (cts_LoadRecords != null) cts_LoadRecords.Cancel();
            cts_LoadRecords = new CancellationTokenSource();
            try
            {
                var task = _appLogRepository.GetAlarmRecordsPagedAsync(pageNumber, PageSize, StartDate, EndDate);
                var records = await task.WaitAsync(cts_LoadRecords.Token);
                RefreshRecordInfo(pageNumber, records);
            }
            catch (Exception ex) when (ex is OperationCanceledException) { return; }
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

        private AlarmRecordModel ConvertToRecord(AppLogModel log)
        {
            AlarmRecordModel? record;
            try
            {
                record = JsonConvert.DeserializeObject<AlarmRecordModel>(log.Message);
            }
            catch
            {
                record = null;
            }

            if (record == null)
            {
                record = new AlarmRecordModel();
                record.AlarmMessage = "DeserializeObjectError";
            }
            record.RecordTime = log.LogTime;

            return record;
        }

        /// <summary>
        /// 装换为字典
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDic(AlarmRecordModel record)
        {
            var result = new Dictionary<string, string>();
            result["排序"] = record.ID.ToString();
            result["极面"] = record.SideType ?? string.Empty;
            result["报警内容"] = record.AlarmMessage ?? string.Empty;
            result["操作时间"] = record.RecordTime?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? string.Empty;

            return result;
        }
        #endregion
    }
}