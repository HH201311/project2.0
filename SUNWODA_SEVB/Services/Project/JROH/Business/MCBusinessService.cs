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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SUNWODA_SEVB.Services.Project.JROH.Business
{
    public class MCBusinessService : BusinessServiceBase<MCBusinessService>
    {
        private readonly ILoggerService<MCBusinessService> _logger;
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
        public override EBusinessType BusinessType => EBusinessType.MultiCorrect;

        /// <summary>
        /// 构造函数
        /// </summary>
        public MCBusinessService(
            ILoggerService<MCBusinessService> logger,
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

            // 清理资源
            gatherDataModels?.Dispose();
            calculateDatasCache?.Clear();
            _cts?.Cancel();
            _cts?.Dispose();

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
        /// PLC采集数据缓存
        /// </summary>
        private BlockingCollection<MCBusinessGatherModel> gatherDataModels = new();

        /// <summary>
        /// 计算过程数据缓存
        /// </summary>
        private List<(MCBusinessCalculateModel calculateData, MCBusinessGatherModel gatherData)> calculateDatasCache = new();
        #endregion

        #region csv数据文件监听
        private FileSystemWatcher? csvFileWatcher;

        /// <summary>
        /// 初始化CSV文件监听器
        /// </summary>
        /// <param name="path"></param>
        private void InitializeCSVFileWatcher(string? path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException($"[{BusinessName}]CSV文件监听根目录为空！");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            csvFileWatcher?.Dispose();
            csvFileWatcher = new FileSystemWatcher(path, "*.csv");
            csvFileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            csvFileWatcher.Renamed += OnCSVFileRenamed;
            csvFileWatcher.EnableRaisingEvents = true;
            csvFileWatcher.IncludeSubdirectories = true;
        }

        /// <summary>
        /// CSV文件重命名事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCSVFileRenamed(object sender, RenamedEventArgs e)
        {
            var gatherData = new MCBusinessGatherModel();

            OnDataReceived(gatherData);
        }

        /// <summary>
        /// 数据接收事件处理
        /// </summary>
        /// <param name="gatherData"></param>
        private void OnDataReceived(MCBusinessGatherModel gatherData)
        {
            var param = paramModel.CopyByJson();
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
                    foreach (var gatherData in gatherDataModels.GetConsumingEnumerable())
                    {
                        var pm = paramModel.CopyByJson();
                        if (pm == null) throw new ArgumentException($"[{BusinessName}]参数为空！");

                        var cm = new MCBusinessCalculateModel();
                        cm.CalculateTime = DateTime.Now;
                        try
                        {
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
                catch (Exception ex)
                {
                    _logger.Error($"[{BusinessName}]异常！", ex);
                    throw;
                }
            });
            await task.WaitAsync(ct);
        }

        /// <summary>
        /// 闭环计算逻辑
        /// </summary>
        /// <param name="paramName"></param>
        /// <param name="datas"></param>
        /// <param name="pm"></param>
        /// <param name="cm"></param>
        /// <returns></returns>
        private async Task ExcuteMCCalcualteAsync(string paramName, List<MCGatherData> datas, MCBusinessParamModel pm, MCCalculateModel cm)
        {
            if (datas == null || datas.Count == 0) return;
            var setValue = datas.Last().SetValue;
            var ds = datas.SelectMany(o => o.Values).Where(d => Math.Abs(d.detValue - d.tarValue) < 2).ToList();
            cm.TotalCount = ds.Count;
            cm.PosCount = ds.Count(d => d.detValue > d.tarValue);
            cm.NegCount = cm.TotalCount - cm.PosCount;
            if (cm.PosCount / cm.TotalCount > pm.K) cm.OffsetValue = -pm.K * pm.D;
            if (cm.NegCount / cm.TotalCount > pm.K) cm.OffsetValue = pm.K * pm.D;

            if (cm.OffsetValue == 0)
            {
                cm.EndMessage = "无需调机！";
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
