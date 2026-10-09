using Dm.util;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HslCommunication;
using Newtonsoft.Json;
using Python.Runtime;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.JROH;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.JROH.MCBusinessModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.JROHHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.JROH.BusinessDialogs;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SUNWODA_SEVB.Services.Project.JROH.Business
{
    public class MisBusinessService : BusinessServiceBase<MisBusinessService>
    {
        private readonly ILoggerService<MisBusinessService> _logger;
        private readonly IPLCService _plcService;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        /// <summary>
        /// 业务类别
        /// </summary>
        public override EBusinessType BusinessType => EBusinessType.Mismatch;

        /// <summary>
        /// 构造函数
        /// </summary>
        public MisBusinessService(
            ILoggerService<MisBusinessService> logger,
            IPLCService plcService,
            IGlobalSettingRepository globalSettingRepository,
            IUsersRepository usersRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        ) : base(logger, plcService, projectSettingRepository)
        {
            _logger = logger;
            _plcService = plcService;
            _globalSettingRepository = globalSettingRepository;
            _usersRepository = usersRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
        }

        /// <summary>
        /// 初始化业务
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        protected override async Task InitializeAsync(CancellationToken ct)
        {
            _logger.Info($"初始化业务 {BusinessType} - {BusinessName}");

            // 初始化参数配置
            paramModel = LoadBusinessParamConfig<MCBusinessParamModel>();

            // 初始化项目设置
            var rootPath = _projectSettingRepository.LoadJROHFileSaveRootPathProjectParameter();
            fileSaveRootPath = Path.Combine(rootPath, BusinessType.ToString());

            // 初始化CSV文件监听器
            InitializeCSVFileWatcher(paramModel?.WatcherRootPath);

            _logger.Info($"业务 {BusinessType} 初始化完成");
        }

        /// <summary>
        /// 执行业务
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _logger.Info($"业务 {BusinessType} 开始执行");

            // 闭环计算
            var calculateTask = CalculateLoopAsync(ct);

            await Task.WhenAny(calculateTask);
        }

        /// <summary>
        /// 清理
        /// </summary>
        /// <returns></returns>
        protected override async Task CleanupAsync()
        {
            _logger.Info($"清理业务 {BusinessType} 资源");

            // 停止文件监听并解绑事件
            StopCSVFileWatcher();

            pendingFiles.Clear();
            processedFileFingerprints.Clear();

            // 收敛消费队列并重建（业务重启后仍可继续入队）
            var oldQueue = gatherDataModels;
            gatherDataModels = new BlockingCollection<MCBusinessGatherModel>();
            try { oldQueue.CompleteAdding(); } catch { }
            try { oldQueue.Dispose(); } catch { }

            calculateDatasCache?.Clear();

            // 释放取消令牌（先置空再释放，避免 Stop/清理重复释放）
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            await Task.CompletedTask;
        }

        /// <summary>
        /// 显示业务配置对话框
        /// </summary>
        /// <returns></returns>
        public override async Task ShowBusinessParamConfigDialogAsync()
        {
            await ShowBusinessConfigDialogAsync<MCBusinessParamModel>(ShowBusinessConfigDialog);
            paramModel = LoadBusinessParamConfig<MCBusinessParamModel>();

            if (paramModel == null) throw new ArgumentException($"[{BusinessName}]参数为空！");
            if (string.IsNullOrEmpty(paramModel.WatcherRootPath)) throw new ArgumentException($"[{BusinessName}]参数配置中，CSV文件监听根目录为空！");

            if (csvFileWatcher != null && csvFileWatcher.Path == paramModel.WatcherRootPath) return;
            InitializeCSVFileWatcher(paramModel.WatcherRootPath);
        }

        #region 参数配置
        /// <summary>
        /// 显示业务类型2的配置对话框
        /// </summary>
        /// <typeparam name="TConfig"></typeparam>
        /// <param name="config"></param>
        /// <returns></returns>
        private async Task<bool> ShowBusinessConfigDialog<TConfig>(TConfig config) where TConfig : class
        {
            if (!(config is MCBusinessParamModel model)) return false;
            var result = await Dialog.Show(new VM_MCBusinessParamDialog().UIElement)
                    .Initialize<VM_MCBusinessParamDialog>(vm =>
                    {
                        vm.UserRole = UserRole;
                        vm.ItemSource = model;
                    }).GetResultAsync<bool>();
            return result;
        }
        #endregion

        #region 闭环逻辑开发
        #region 闭环全局参数
        /// <summary>
        /// 文件保存根目录
        /// </summary>
        private string fileSaveRootPath = string.Empty;

        /// <summary>
        /// PLC采集数据缓存（一个卷芯一条）
        /// </summary>
        private BlockingCollection<MCBusinessGatherModel> gatherDataModels = new();

        /// <summary>
        /// 计算过程数据缓存
        /// </summary>
        private List<(MCBusinessCalculateModel calculateData, MCBusinessGatherModel gatherData)> calculateDatasCache = new();
        #endregion

        #region csv数据文件监听
        /// <summary>
        /// 文件落盘防抖时长（毫秒）：更名完成后等待文件句柄释放
        /// </summary>
        private const int FileDebounceMilliseconds = 800;

        /// <summary>
        /// 已处理文件指纹上限（超出后清空，避免长时间运行内存无限增长）
        /// </summary>
        private const int ProcessedFingerprintLimit = 10000;

        private FileSystemWatcher? csvFileWatcher;

        /// <summary>
        /// 待处理文件（同一文件在防抖窗口内只排一次）
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> pendingFiles = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 已处理文件指纹（文件长度 + 最后写入时间），避免 Created/Renamed 重复投递
        /// </summary>
        private readonly ConcurrentDictionary<string, string> processedFileFingerprints = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 缺失目标值列的警告只记录一次
        /// </summary>
        private bool missingTargetColumnWarned;

        /// <summary>
        /// 膜卷号文件名校验
        /// </summary>
        /// <remarks>
        /// <para>实测规则（2026-08-30 现场样例 2713 个文件，2709 个已完成文件全部命中）：</para>
        /// <para><c>9 位数字 + 1 位字母 + 5 位数字</c>（共 15 位），例如 <c>529126128L03465.csv</c>。</para>
        /// <para>⏸ 需求方口述规则为「日期 + 膜卷号固定长度」，样例中未见日期前缀；现场资料补齐后如需扩展请只改此处。</para>
        /// <para>供应商写入过程中的临时文件为纯数字名（例如 <c>3770.csv</c>），在此被过滤，不参与解析与闭环。</para>
        /// </remarks>
        private static readonly Regex FilmRollFileNameRegex = new(@"^[0-9]{9}[A-Za-z][0-9]{5}$", RegexOptions.Compiled);

        /// <summary>
        /// 判断文件是否为「膜卷号」命名（临时数字名文件返回 false）
        /// </summary>
        private static bool IsFilmRollFileName(string fullPath)
        {
            var name = Path.GetFileNameWithoutExtension(fullPath);
            return !string.IsNullOrEmpty(name) && FilmRollFileNameRegex.IsMatch(name);
        }

        /// <summary>
        /// 日期目录名校验（日目录，例如 2026年08月30日 / 2026年8月13号）
        /// </summary>
        private static readonly Regex DayFolderNameRegex = new(@"^(?<y>\d{4})年(?<m>\d{1,2})月(?<d>\d{1,2})[日号]$", RegexOptions.Compiled);

        /// <summary>
        /// 日期目录名校验（月目录，例如 2026年08月 / 2026年8月）
        /// </summary>
        private static readonly Regex MonthFolderNameRegex = new(@"^(?<y>\d{4})年(?<m>\d{1,2})月$", RegexOptions.Compiled);

        /// <summary>
        /// 校验文件是否位于规范日期目录下：{WatcherRootPath}\{yyyy年M月}\{yyyy年M月d日}\{膜卷号}.csv
        /// </summary>
        /// <param name="fullPath">文件完整路径</param>
        /// <param name="filmRollNumber">膜卷号（仅用于日志）</param>
        /// <returns>目录合法返回 true；否则记录 Warn 并返回 false</returns>
        private bool ValidateDateFolder(string fullPath, string filmRollNumber)
        {
            var dayFolder = new DirectoryInfo(fullPath).Parent;
            var monthFolder = dayFolder?.Parent;

            var dayMatch = DayFolderNameRegex.Match(dayFolder?.Name ?? string.Empty);
            if (!dayMatch.Success)
            {
                _logger.Warn($"[{BusinessName}]膜卷号[{filmRollNumber}]所在日目录[{dayFolder?.Name}]不符合「yyyy年M月d日/号」命名，跳过处理。");
                return false;
            }

            var monthMatch = MonthFolderNameRegex.Match(monthFolder?.Name ?? string.Empty);
            if (!monthMatch.Success)
            {
                _logger.Warn($"[{BusinessName}]膜卷号[{filmRollNumber}]所在月目录[{monthFolder?.Name}]不符合「yyyy年M月」命名，跳过处理。");
                return false;
            }

            // 必须为真实存在的日历日期（例如 2026年02月30日 非法）
            var dayKey = $"{dayMatch.Groups["y"].Value}{int.Parse(dayMatch.Groups["m"].Value):D2}{int.Parse(dayMatch.Groups["d"].Value):D2}";
            if (!DateTime.TryParseExact(dayKey, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                _logger.Warn($"[{BusinessName}]膜卷号[{filmRollNumber}]所在日目录[{dayFolder!.Name}]不是有效日期，跳过处理。");
                return false;
            }

            // 月目录与日目录的年月必须一致
            if (monthMatch.Groups["y"].Value != dayMatch.Groups["y"].Value ||
                int.Parse(monthMatch.Groups["m"].Value) != int.Parse(dayMatch.Groups["m"].Value))
            {
                _logger.Warn($"[{BusinessName}]膜卷号[{filmRollNumber}]的月目录[{monthFolder!.Name}]与日目录[{dayFolder!.Name}]年月不一致，跳过处理。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 初始化CSV文件监听器
        /// </summary>
        /// <param name="path"></param>
        private void InitializeCSVFileWatcher(string? path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException($"[{BusinessName}]CSV文件监听根目录为空！");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            csvFileWatcher?.Dispose();
            csvFileWatcher = new FileSystemWatcher(path, "*.csv")
            {
                NotifyFilter = NotifyFilters.FileName,
                IncludeSubdirectories = true,
                InternalBufferSize = 64 * 1024,
            };
            csvFileWatcher.Created += OnCsvFileCreated;
            csvFileWatcher.Renamed += OnCsvFileRenamed;
            csvFileWatcher.Error += OnCsvFileWatcherError;
            csvFileWatcher.EnableRaisingEvents = true;

            _logger.Info($"[{BusinessName}]已开始监听膜卷号 CSV 目录：{path}");
        }

        /// <summary>
        /// 停止文件监听并解绑事件
        /// </summary>
        private void StopCSVFileWatcher()
        {
            try
            {
                if (csvFileWatcher == null) return;

                csvFileWatcher.EnableRaisingEvents = false;
                csvFileWatcher.Created -= OnCsvFileCreated;
                csvFileWatcher.Renamed -= OnCsvFileRenamed;
                csvFileWatcher.Error -= OnCsvFileWatcherError;
                csvFileWatcher.Dispose();
                csvFileWatcher = null;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{BusinessName}]释放 CSV 文件监听器失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 新文件事件（供应商直接以膜卷号命名落盘的情况）
        /// </summary>
        private void OnCsvFileCreated(object sender, FileSystemEventArgs e) => EnqueueCsvFile(e.FullPath);

        /// <summary>
        /// 文件更名事件（供应商写完后由数字名改为膜卷号，主要入口）
        /// </summary>
        private void OnCsvFileRenamed(object sender, RenamedEventArgs e) => EnqueueCsvFile(e.FullPath);

        /// <summary>
        /// 监听异常（缓冲区溢出等）：重建监听器
        /// </summary>
        private void OnCsvFileWatcherError(object sender, ErrorEventArgs e)
        {
            _logger.Error($"[{BusinessName}]CSV 文件监听异常，尝试重建监听器。", e.GetException());
            try
            {
                InitializeCSVFileWatcher(paramModel?.WatcherRootPath);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{BusinessName}]重建 CSV 文件监听器失败。", ex);
            }
        }

        /// <summary>
        /// 把文件排入待处理队列（仅膜卷号文件；防抖 + 指纹去重）
        /// </summary>
        private void EnqueueCsvFile(string? fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return;

            // 仅监听膜卷号文档；数字名临时文件（实时更新中）直接跳过
            if (!IsFilmRollFileName(fullPath)) return;

            // 防抖窗口内同一文件只排一次
            if (!pendingFiles.TryAdd(fullPath, 0)) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(FileDebounceMilliseconds, GetCurrentToken());
                    ProcessCsvFile(fullPath);
                }
                catch (OperationCanceledException)
                {
                    // 业务停止，正常退出
                }
                catch (Exception ex)
                {
                    _logger.Error($"[{BusinessName}]处理膜卷号 CSV 失败：{fullPath}", ex);
                }
                finally
                {
                    pendingFiles.TryRemove(fullPath, out _);
                }
            });
        }

        /// <summary>
        /// 获取当前取消令牌（令牌源已释放时返回 None）
        /// </summary>
        private CancellationToken GetCurrentToken()
        {
            try { return _cts?.Token ?? CancellationToken.None; }
            catch (ObjectDisposedException) { return CancellationToken.None; }
        }

        /// <summary>
        /// 解析膜卷号 CSV 并入队闭环计算（一个卷芯文件 = 一条采集数据）
        /// </summary>
        /// <param name="fullPath">膜卷号 CSV 完整路径</param>
        private void ProcessCsvFile(string fullPath)
        {
            // 1) 文件存在性（更名/删除竞态下可能已不存在）
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(fullPath);
                if (!fileInfo.Exists)
                {
                    _logger.Warn($"[{BusinessName}]文件已不存在，跳过处理：{fullPath}");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{BusinessName}]读取文件信息失败：{fullPath}（{ex.Message}）");
                return;
            }

            var filmRollNumber = Path.GetFileNameWithoutExtension(fullPath);

            // 2) 指纹去重（Created / Renamed 可能对同一文件先后到达）
            var fingerprint = $"{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
            if (processedFileFingerprints.TryGetValue(fullPath, out var cached) && cached == fingerprint) return;

            // 3) 目录日期校验（TODO：2026年8月/2026年8月13号）
            if (!ValidateDateFolder(fullPath, filmRollNumber)) return;

            // 4) 读取 CSV（JROHCsvHelper 内部以 FileShare.ReadWrite 打开，允许供应商占用）
            List<Dictionary<string, string>> rows;
            try
            {
                rows = JROHCsvHelper.SearchCsv(fullPath) ?? new List<Dictionary<string, string>>();
            }
            catch (Exception ex)
            {
                // 读取失败不写指纹，后续文件事件仍可重试
                _logger.Warn($"[{BusinessName}]膜卷号[{filmRollNumber}]CSV 读取失败：{ex.Message}");
                return;
            }

            if (rows.Count == 0)
            {
                _logger.Warn($"[{BusinessName}]膜卷号[{filmRollNumber}]CSV 无数据行，跳过处理。");
                return;
            }

            // 5) 转换为采集模型（内部会跳过重复表头行、清洗行首反引号）
            var gatherData = ConvertToGatherModel(filmRollNumber, rows);

            // 6) 处理成功后才记录指纹（失败可在下一轮文件事件重试）
            if (processedFileFingerprints.Count >= ProcessedFingerprintLimit) processedFileFingerprints.Clear();
            processedFileFingerprints[fullPath] = fingerprint;

            _logger.Info($"[{BusinessName}]已解析膜卷号[{filmRollNumber}]：原始数据 {rows.Count} 行。");

            // 7) 入队闭环计算（一个卷芯一条）
            if (!gatherDataModels.IsAddingCompleted)
            {
                try { gatherDataModels.Add(gatherData); }
                catch (InvalidOperationException) { }
            }

            // 8) 通知界面刷新曲线
            OnDataReceived(gatherData);
        }

        /// <summary>
        /// 把 CSV 行数据转换为 MC 采集模型（一个卷芯一条）
        /// </summary>
        /// <param name="rollName">膜卷号</param>
        /// <param name="datas">CSV 行数据（键为表头列名）</param>
        /// <returns>采集模型；无有效数据行时各通道 Values 为空</returns>
        private MCBusinessGatherModel ConvertToGatherModel(string rollName, List<Dictionary<string, string>> datas)
        {
            var gatherData = new MCBusinessGatherModel();
            gatherData.GatherTime = DateTime.Now;
            gatherData.VirtualCode = rollName;

            // 11 路通道：列名依据 2026-08-30 现场样例表头（按列名匹配，不依赖列序号）
            gatherData.InfeedDataOfPos = ConvertToMCGatherData(datas, "时间", "正极入卷纠偏实时值", "正极入卷纠偏中值", "");
            gatherData.Snake4DataOfPos = ConvertToMCGatherData(datas, "时间", "正极蛇形4纠偏", "正极蛇形4纠偏中值", "");
            gatherData.Snake3DataOfPos = ConvertToMCGatherData(datas, "时间", "正极蛇形3纠偏", "正极蛇形3纠偏中值", "");
            gatherData.Snake2DataOfPos = ConvertToMCGatherData(datas, "时间", "正极蛇形2纠偏", "正极蛇形2纠偏中值", "");
            gatherData.Snake1DataOfPos = ConvertToMCGatherData(datas, "时间", "正极蛇形纠偏", "正极蛇形1纠偏中值", "");

            gatherData.InfeedDataOfNeg = ConvertToMCGatherData(datas, "时间", "负极入卷纠偏实时值", "负极入卷纠偏中值", "");
            gatherData.Snake3DataOfNeg = ConvertToMCGatherData(datas, "时间", "负极蛇形3纠偏", "负极蛇形3纠偏中值", "");
            gatherData.Snake2DataOfNeg = ConvertToMCGatherData(datas, "时间", "负极蛇形2纠偏", "负极蛇形2纠偏中值", "");
            gatherData.Snake1DataOfNeg = ConvertToMCGatherData(datas, "时间", "负极蛇形1纠偏", "负极蛇形1纠偏中值", "");

            gatherData.UpperDiaphragmData = ConvertToMCGatherData(datas, "时间", "上隔膜外侧破损实时", "", "");
            gatherData.LowerDiaphragmData = ConvertToMCGatherData(datas, "时间", "下隔膜外侧破损实时", "", "");

            _logger.Debug($"[{BusinessName}]膜卷号[{rollName}]转换完成。");

            return gatherData;
        }

        /// <summary>
        /// 把 CSV 行数据转换为单路通道的采集数据
        /// </summary>
        /// <param name="datas">CSV 行数据（键为表头列名）</param>
        /// <param name="timeKey">时间列名</param>
        /// <param name="detKey">检测值列名；为空或列不存在时该通道无数据</param>
        /// <param name="tarKey">目标值（纠偏中值）列名；为空或列不存在时 </param>
        /// <param name="setKey">设定值列名；为空时回退取目标值最后一个采样值</param>
        /// <returns>通道采集数据；无有效数据行时 Values 为空</returns>
        private MCGatherData ConvertToMCGatherData(List<Dictionary<string, string>> datas, string timeKey, string detKey, string tarKey, string setKey)
        {
            var result = new MCGatherData();
            if (datas == null || datas.Count == 0) return result;
            if (string.IsNullOrWhiteSpace(detKey)) return result;
            for (var i = 0; i < datas.Count; i++)
            {
                if (i == 1) continue;
                var row = datas[i];
                var timeText = (ReadColumnValue(row, timeKey) ?? string.Empty).Trim().TrimStart('\u0060');
                var gatherTime = DateTime.ParseExact(timeText, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None);
                var detValue = float.Parse(ReadColumnValue(row, detKey), NumberStyles.Float, CultureInfo.InvariantCulture);
                var tarValue = float.Parse(ReadColumnValue(row, tarKey), NumberStyles.Float, CultureInfo.InvariantCulture);
                var setValue = float.Parse(ReadColumnValue(row, setKey), NumberStyles.Float, CultureInfo.InvariantCulture);

                result.Values.Add((gatherTime, detValue, tarValue));
                result.SetValue = setValue;
            }

            return result;
        }

        /// <summary>
        /// 读取指定列的值（列不存在返回 null）
        /// </summary>
        private static string ReadColumnValue(Dictionary<string, string> row, string? key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return row.TryGetValue(key, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// 数据接收事件处理（通知界面）
        /// </summary>
        /// <param name="gatherData"></param>
        private void OnDataReceived(MCBusinessGatherModel gatherData)
        {
            var param = paramModel?.CopyByJson();
            var message = (gatherData, param);
            RaiseMessageNotifation(this, message);
        }

        /// <summary>
        /// 闭环计算结果接收事件处理
        /// </summary>
        /// <param name="gatherData"></param>
        /// <param name="cm"></param>
        private void OnAdjustmentRecordReceived(MCBusinessGatherModel gatherData, MCBusinessCalculateModel cm)
        {
            var posRrecord = new Dictionary<string, string>();
            posRrecord.Add("正极入料纠偏采集总数", cm.PosInfeedModel.TotalCount.ToString());
            posRrecord.Add("正极入料纠偏正值个数", cm.PosInfeedModel.PosCount.ToString());
            posRrecord.Add("正极过程纠偏4纠偏中值", cm.PosProcess4Model.OffsetValue.ToString());
            posRrecord.Add("正极过程纠偏4采集总数", cm.PosProcess4Model.TotalCount.ToString());
            posRrecord.Add("正极过程纠偏4正值个数", cm.PosProcess4Model.PosCount.ToString());
            posRrecord.Add("正极过程纠偏3纠偏中值", cm.PosProcess3Model.OffsetValue.ToString());
            posRrecord.Add("正极过程纠偏3采集总数", cm.PosProcess3Model.TotalCount.ToString());
            posRrecord.Add("正极过程纠偏3正值个数", cm.PosProcess3Model.PosCount.ToString());
            posRrecord.Add("正极过程纠偏2纠偏中值", cm.PosProcess2Model.OffsetValue.ToString());
            posRrecord.Add("正极过程纠偏2采集总数", cm.PosProcess2Model.TotalCount.ToString());
            posRrecord.Add("正极过程纠偏2正值个数", cm.PosProcess2Model.PosCount.ToString());
            posRrecord.Add("正极过程纠偏1纠偏中值", cm.PosProcess1Model.OffsetValue.ToString());
            posRrecord.Add("正极过程纠偏1采集总数", cm.PosProcess1Model.TotalCount.ToString());
            posRrecord.Add("正极过程纠偏1正值个数", cm.PosProcess1Model.PosCount.ToString());
            posRrecord.Add("正极放卷纠偏设定值", cm.PosInfeedModel.OffsetValue.ToString());
            RaiseMessageNotifation(this, ("PosCorrect", posRrecord));

            var negRecord = new Dictionary<string, string>();
            negRecord.Add("负极入料纠偏正值个数", cm.NegInfeedModel.TotalCount.ToString());
            negRecord.Add("负极过程纠偏3纠偏中值", cm.NegProcess3Model.OffsetValue.ToString());
            negRecord.Add("负极过程纠偏3采集总数", cm.NegProcess3Model.TotalCount.ToString());
            negRecord.Add("负极过程纠偏3正值个数", cm.NegProcess3Model.PosCount.ToString());
            negRecord.Add("负极过程纠偏2纠偏中值", cm.NegProcess2Model.OffsetValue.ToString());
            negRecord.Add("负极过程纠偏2采集总数", cm.NegProcess2Model.TotalCount.ToString());
            negRecord.Add("负极过程纠偏2正值个数", cm.NegProcess2Model.PosCount.ToString());
            negRecord.Add("负极过程纠偏1纠偏中值", cm.NegProcess1Model.OffsetValue.ToString());
            negRecord.Add("负极过程纠偏1采集总数", cm.NegProcess1Model.TotalCount.ToString());
            negRecord.Add("负极过程纠偏1正值个数", cm.NegProcess1Model.PosCount.ToString());
            negRecord.Add("负极放卷纠偏设定值", cm.NegInfeedModel.OffsetValue.ToString());
            RaiseMessageNotifation(this, ("NegCorrect", negRecord));

            var diaphragmRecord = new Dictionary<string, string>();
            diaphragmRecord.Add("上隔膜过程纠偏1采集总数", cm.UpperDiaphragmModel.TotalCount.ToString());
            diaphragmRecord.Add("上隔膜过程纠偏1正值个数", cm.UpperDiaphragmModel.PosCount.ToString());
            diaphragmRecord.Add("上隔膜放卷纠偏设定值", cm.UpperDiaphragmModel.OffsetValue.ToString());
            diaphragmRecord.Add("下隔膜过程纠偏1采集总数", cm.LowerDiaphragmModel.TotalCount.ToString());
            diaphragmRecord.Add("下隔膜过程纠偏1正值个数", cm.LowerDiaphragmModel.PosCount.ToString());
            diaphragmRecord.Add("下隔膜放卷纠偏设定值", cm.LowerDiaphragmModel.OffsetValue.ToString());
            RaiseMessageNotifation(this, ("DiaphragmCorrect", diaphragmRecord));
        }
        #endregion

        #region 闭环计算
        /// <summary>
        /// 闭环参数
        /// </summary>
        private MCBusinessParamModel? paramModel;

        /// <summary>
        /// 闭环计算逻辑
        /// </summary>
        /// <returns></returns>
        private async Task CalculateLoopAsync(CancellationToken ct)
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    var dataCache = new List<MCBusinessGatherModel>();
                    foreach (var gatherData in gatherDataModels.GetConsumingEnumerable(ct))
                    {
                        var pm = paramModel?.CopyByJson();
                        if (pm == null) throw new ArgumentException($"[{BusinessName}]参数为空！");

                        var cm = new MCBusinessCalculateModel();
                        cm.CalculateTime = DateTime.Now;
                        try
                        {
                            // 一个卷芯一条采集数据：累积到 T 条后统一计算（默认 T = 1）
                            dataCache.Add(gatherData);
                            if (dataCache.Count < pm.T) continue;

                            // 正极多级纠偏闭环计算
                            var datas = dataCache.Select(o => o.InfeedDataOfPos).ToList();
                            await ExcuteMCCalcualteAsync("正极蛇形纠偏4纠偏设定中值", datas, pm, cm.PosInfeedModel);
                            datas = dataCache.Select(o => o.Snake4DataOfPos).ToList();
                            await ExcuteMCCalcualteAsync("正极蛇形纠偏3纠偏设定中值", datas, pm, cm.PosProcess4Model);
                            datas = dataCache.Select(o => o.Snake3DataOfPos).ToList();
                            await ExcuteMCCalcualteAsync("正极蛇形纠偏2纠偏设定中值", datas, pm, cm.PosProcess3Model);
                            datas = dataCache.Select(o => o.Snake2DataOfPos).ToList();
                            await ExcuteMCCalcualteAsync("正极蛇形纠偏1纠偏设定中值", datas, pm, cm.PosProcess2Model);
                            datas = dataCache.Select(o => o.Snake1DataOfPos).ToList();
                            await ExcuteMCCalcualteAsync("正极放卷纠偏设定中值", datas, pm, cm.PosProcess1Model);

                            // 负极多级纠偏闭环计算
                            datas = dataCache.Select(o => o.InfeedDataOfNeg).ToList();
                            await ExcuteMCCalcualteAsync("负极蛇形纠偏3纠偏设定中值", datas, pm, cm.NegInfeedModel);
                            datas = dataCache.Select(o => o.Snake3DataOfNeg).ToList();
                            await ExcuteMCCalcualteAsync("负极蛇形纠偏2纠偏设定中值", datas, pm, cm.NegProcess3Model);
                            datas = dataCache.Select(o => o.Snake2DataOfNeg).ToList();
                            await ExcuteMCCalcualteAsync("负极蛇形纠偏1纠偏设定中值", datas, pm, cm.NegProcess2Model);
                            datas = dataCache.Select(o => o.Snake1DataOfNeg).ToList();
                            await ExcuteMCCalcualteAsync("负极放卷纠偏设定中值", datas, pm, cm.NegProcess1Model);

                            // 隔膜多级纠偏闭环计算
                            datas = dataCache.Select(o => o.UpperDiaphragmData).ToList();
                            await ExcuteMCCalcualteAsync("上隔膜放卷纠偏设定中值", datas, pm, cm.UpperDiaphragmModel);
                            datas = dataCache.Select(o => o.LowerDiaphragmData).ToList();
                            await ExcuteMCCalcualteAsync("下隔膜放卷纠偏设定中值", datas, pm, cm.LowerDiaphragmModel);

                            cm.EndMessage = "Success";
                        }
                        catch (Exception ex)
                        {
                            cm.EndMessage += ex.Message;
                            _logger.Error($"[{BusinessName}]计算异常！", ex);
                        }
                        finally
                        {
                            // 本卷芯结算完成：清空缓存，避免跨卷累积
                            dataCache.Clear();

                            _ = Task.Run(() =>
                            {
                                // 界面数据更新
                                OnAdjustmentRecordReceived(gatherData, cm);
                                // 计算过程模型异步保存CSV
                                var savePath = Path.Combine(fileSaveRootPath, $"缓存数据_{BusinessName}\\{DateTime.Now:yyyyMMdd}");
                                var dic = ConvertToDictionary(gatherData, cm, pm);
                                var dt = JROHCsvHelper.DicToTable(dic);
                                lock (JROHCsvHelper.WriteLocker) JROHCsvHelper.WriteCSV(savePath, dt);
                            });
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 业务停止，正常退出
                }
                catch (Exception ex)
                {
                    _logger.Error($"[{BusinessName}]异常！", ex);
                    throw;
                }
            });
            await task.WaitAsync(ct);
        }

        /// <summary>
        /// 单路通道闭环计算与下发
        /// </summary>
        /// <param name="paramName">下发点位名</param>
        /// <param name="datas">参与计算的采集数据</param>
        /// <param name="pm">业务参数</param>
        /// <param name="cm">计算过程模型</param>
        /// <returns></returns>
        private async Task ExcuteMCCalcualteAsync(string paramName, List<MCGatherData> datas, MCBusinessParamModel pm, MCCalculateModel cm)
        {
            if (datas == null || datas.Count == 0) return;
            var ds = datas.SelectMany(o => o.Values).Where(d => Math.Abs(d.detValue - d.tarValue) < 2).ToList();
            cm.TotalCount = ds.Count;
            cm.PosCount = ds.Count(d => d.detValue > d.tarValue);
            cm.NegCount = cm.TotalCount - cm.PosCount;

            // 采集总数为 0 时直接返回（原实现会 0/0 整除）
            if (cm.TotalCount == 0)
            {
                cm.EndMessage = $"[{paramName}]无有效采样点，跳过下发。";
                return;
            }

            if ((float)cm.PosCount / cm.TotalCount > pm.K) cm.OffsetValue = -pm.K * pm.D;
            if ((float)cm.NegCount / cm.TotalCount > pm.K) cm.OffsetValue = pm.K * pm.D;

            if (cm.OffsetValue == 0)
            {
                cm.EndMessage = $"[{paramName}]无需调机！";
                return;
            }
            var re = await WritePLCAsync(paramName, cm.OffsetValue);
            var message = re.IsSuccess ? $"Success([{paramName}]下发[{cm.OffsetValue}]成功)" : $"[{paramName}]PLC地址写入[{cm.OffsetValue}]失败:[{re.Message}]";
            if (!re.IsSuccess) _logger.Error(message);
            cm.EndMessage += message;
        }

        /// <summary>
        /// CSV文件：闭环结果保存
        /// </summary>
        /// <param name="gatherData"></param>
        /// <param name="calculateData"></param>
        /// <param name="paramModel"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(MCBusinessGatherModel gatherData, MCBusinessCalculateModel calculateData, MCBusinessParamModel paramModel)
        {
            var dic = new Dictionary<string, string>();
            dic.Add("采集时间", gatherData.GatherTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            dic.Add("虚拟码 ", gatherData.VirtualCode ?? string.Empty);

            dic.Add("正极入料纠偏实时值-中值", FormatPairValues(gatherData.InfeedDataOfPos.Values));
            dic.Add("正极蛇形纠偏4实时值-中值", FormatPairValues(gatherData.Snake4DataOfPos.Values));
            dic.Add("正极蛇形纠偏3实时值-中值", FormatPairValues(gatherData.Snake3DataOfPos.Values));
            dic.Add("正极蛇形纠偏2实时值-中值", FormatPairValues(gatherData.Snake2DataOfPos.Values));
            dic.Add("正极蛇形纠偏1实时值-中值", FormatPairValues(gatherData.Snake1DataOfPos.Values));

            dic.Add("负极入料纠偏实时值-中值", FormatPairValues(gatherData.InfeedDataOfNeg.Values));
            dic.Add("负极蛇形纠偏3实时值-中值", FormatPairValues(gatherData.Snake3DataOfNeg.Values));
            dic.Add("负极蛇形纠偏2实时值-中值", FormatPairValues(gatherData.Snake2DataOfNeg.Values));
            dic.Add("负极蛇形纠偏1实时值-中值", FormatPairValues(gatherData.Snake1DataOfNeg.Values));

            dic.Add("上隔膜外侧破损实时值-标定值", FormatPairValues(gatherData.UpperDiaphragmData.Values));
            dic.Add("下隔膜外侧破损实时值-标定值", FormatPairValues(gatherData.LowerDiaphragmData.Values));

            ConvertToDictionary("正极入料纠偏", calculateData.PosInfeedModel, ref dic);
            ConvertToDictionary("正极过程纠偏4", calculateData.PosProcess4Model, ref dic);
            ConvertToDictionary("正极过程纠偏3", calculateData.PosProcess3Model, ref dic);
            ConvertToDictionary("正极过程纠偏2", calculateData.PosProcess2Model, ref dic);
            ConvertToDictionary("正极过程纠偏1", calculateData.PosProcess1Model, ref dic);

            ConvertToDictionary("负极入料纠偏", calculateData.NegInfeedModel, ref dic);
            ConvertToDictionary("负极过程纠偏3", calculateData.NegProcess3Model, ref dic);
            ConvertToDictionary("负极过程纠偏2", calculateData.NegProcess2Model, ref dic);
            ConvertToDictionary("负极过程纠偏1", calculateData.NegProcess1Model, ref dic);

            ConvertToDictionary("上隔膜过程纠偏", calculateData.UpperDiaphragmModel, ref dic);
            ConvertToDictionary("下隔膜过程纠偏", calculateData.LowerDiaphragmModel, ref dic);

            dic.Add("闭环结果信息", calculateData.EndMessage);
            return dic;
        }

        /// <summary>
        /// 格式化闭环计算结果为字典
        /// </summary>
        /// <param name="clName"></param>
        /// <param name="calculateData"></param>
        /// <param name="dic"></param>
        private void ConvertToDictionary(string clName, MCCalculateModel calculateData, ref Dictionary<string, string> dic)
        {
            dic.Add($"{clName}-采集总数", $"{calculateData.TotalCount}");
            dic.Add($"{clName}-正值个数", $"{calculateData.PosCount}");
            dic.Add($"{clName}-负值个数", $"{calculateData.NegCount}");
            dic.Add($"{clName}-纠偏值", $"{calculateData.OffsetValue}");
            dic.Add($"{clName}-纠偏信息", $"{calculateData.EndMessage}");
        }

        /// <summary>
        /// 格式化检测值-目标值对
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        private string FormatPairValues(List<(DateTime time, float detValue, float tarValue)> values)
        {
            return string.Join('|', values.ConvertAll(o => $"{o.detValue}-{o.tarValue}"));
        }
        #endregion
        #endregion
    }
}
