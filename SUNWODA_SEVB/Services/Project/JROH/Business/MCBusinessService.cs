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
            // 初始化各个任务
            InitializePLCSigalListenerMonitor();

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

            // 信号监听
            var signa1Task = PLCSigalListenerMonitorLoopAsync(plcSigalListener, ct);

            // 闭环计算
            var calculateTask = CalculateLoopAsync(ct);

            await Task.WhenAny(signa1Task, calculateTask);
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

        #region 信号监听/数据采集
        private ValueListener? plcSigalListener;

        /// <summary>
        /// 初始化PLC信号监听委托
        /// </summary>
        private void InitializePLCSigalListenerMonitor()
        {
            plcSigalListener = new ValueListener() { Name = "极片宽度闭环触发信号", Value = "0" };
            plcSigalListener.OnValueChangedEven -= OnPLCListenerValueChangedAsync;
            plcSigalListener.OnValueChangedEven += OnPLCListenerValueChangedAsync;
        }

        /// <summary>
        /// 初始化PLC信号监听循环
        /// </summary>
        /// <returns></returns>
        private async Task PLCSigalListenerMonitorLoopAsync(ValueListener? listener, CancellationToken ct)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            var task = Task.Run(async () =>
            {
                var rm = new Random();
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    try
                    {
                        //var d = new OHBusinessGatherModel();
                        //d.ModuleWidthOfPos = rm.Next(190000, 193000);
                        //d.ModuleWidthOfNeg = rm.Next(190000, 193000);
                        //OnDataReceived(d);

                        //var dic = new Dictionary<string, string>();
                        //dic["1231231"] = "asdfafas";
                        //dic["1231sad231"] = "asdgadsfafas";
                        //dic["123s1231"] = "asdfafas";
                        //dic["123vc1231"] = "asdfasdafas";
                        //dic["1231b231"] = "asdfsadafas";
                        //dic["12g31231"] = "asdfsadafas";
                        //dic["12331231"] = "asdfczafas";
                        //dic["12312j31"] = "asdfxZCafas";
                        //OnAdjustmentRecordReceived(dic);

                        listener.Value = GetPLCAddressValue(listener.Name);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Info($"PLC信号[{listener.Name}]监听线程任务已取消！");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"PLC信号[{listener.Name}]监听线程异常", ex);
                    }
                }
            });
            await task.WaitAsync(ct);
        }

        /// <summary>
        /// PLC数据采集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnPLCListenerValueChangedAsync(object? sender, EventArgs<string> args)
        {
            if (!(sender is ValueListener listener)) return;
            if (listener.Name != "极片宽度闭环触发信号") return;
            if (args.OldValue == args.NewValue) return;

            if (args.NewValue == "0")
            {
                _logger.Info($"[{listener.Name}]读取数据信号复位0");
                return;
            }

            if (args.NewValue == "1")
            {
                _logger.Debug($"[{listener.Name}]读取数据信号置位1");

                await Task.Delay(200);
                float valueFloat;
                var gatherModel = new MCBusinessGatherModel();
                gatherModel.GatherTime = DateTime.Now;
                gatherModel.VirtualCode = GetPLCAddressValue("虚拟码");
                gatherModel.ModuleWidthOfPosA = int.TryParse(GetPLCAddressValue("正极A面CCD模宽数据"), out var moduleWidth) ? moduleWidth : 0;
                gatherModel.ModuleWidthOfPosB = float.TryParse(GetPLCAddressValue("正极B面CCD模宽数据"), out valueFloat) ? (int)valueFloat : 0;
                gatherModel.ModuleWidthOfNeg = float.TryParse(GetPLCAddressValue("负极CCD模宽数据"), out valueFloat) ? (int)valueFloat : 0;
                gatherModel.ModuleWidthOfPos = float.TryParse(GetPLCAddressValue("正极CCD模宽数据"), out valueFloat) ? (int)valueFloat : 0;
                gatherModel.OriginYOfPos = float.TryParse(GetPLCAddressValue("正极飞行原点Y"), out valueFloat) ? (int)valueFloat : 0;
                gatherModel.OriginYOfNeg = float.TryParse(GetPLCAddressValue("负极飞行原点Y"), out valueFloat) ? (int)valueFloat : 0;

                // 缓存
                gatherDataModels.Add(gatherModel);
                OnDataReceived(gatherModel);
                return;
            }

            _logger.Error($"{listener.Name}读取数据信号为异常值[{args.NewValue}]");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vOfPos"></param>
        /// <param name="vOfNeg"></param>
        /// <param name="upperOfPos"></param>
        /// <param name="lowerOfPos"></param>
        /// <param name="upperOfNeg"></param>
        /// <param name="lowerOfNeg"></param>
        private void OnDataReceived(MCBusinessGatherModel gatherData)
        {
            var param = paramModel.CopyByJson();
            var message = (gatherData, param);
            RaiseMessageNotifation(this, message);
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
                    // 正极下发缓存（依据AdjustInterval求平均值下发）
                    var adjustValueOfPosCache = new List<int>();
                    // 负极下发缓存（依据AdjustInterval求平均值下发）
                    var adjustValueOfNegCache = new List<int>();
                    foreach (var gatherData in gatherDataModels.GetConsumingEnumerable())
                    {
                        var pm = paramModel.CopyByJson();
                        if (pm == null) throw new ArgumentException($"[{BusinessName}]参数为空！");
                        var cm = new MCBusinessCalculateModel();
                        cm.CalculateTime = DateTime.Now;
                        try
                        {

                        }
                        catch (Exception ex)
                        {
                            cm.EndMessage += ex.Message;
                            _logger.Error($"[{BusinessName}]计算异常！", ex);
                        }
                        finally
                        {
                            // 计算过程模型异步保存CSV
                            _ = Task.Run(() =>
                            {
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
        /// CSV文件：闭环结果保存
        /// </summary>
        /// <param name="plcData"></param>
        /// <param name="ccdData"></param>
        /// <param name="calculateData"></param>
        /// <returns></returns>
        private Dictionary<string, string> ConvertToDictionary(MCBusinessGatherModel gatherData, MCBusinessCalculateModel calculateData, MCBusinessParamModel paramModel)
        {
            var dic = new Dictionary<string, string>();
            dic.Add("采集时间", gatherData.GatherTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            dic.Add("虚拟码 ", gatherData.VirtualCode ?? string.Empty);
            dic.Add("负极驱动纠偏采集值", gatherData.ModuleWidthOfPosA.ToString());
            dic.Add("负极头部补偿采集值", gatherData.ModuleWidthOfPosB.ToString());
            dic.Add("负极尾部补偿采集值", gatherData.ModuleWidthOfPos.ToString());

            dic.Add("负极中部纠偏值", gatherData.ModuleWidthOfNeg.ToString());
            dic.Add("负极中部纠偏发送值", gatherData.OriginYOfPos.ToString());
            dic.Add("负极头部补偿发送值", gatherData.OriginYOfNeg.ToString());
            dic.Add("负极尾部补偿发送值", gatherData.OriginYOfNeg.ToString());

            dic.Add("正极驱动纠偏采集值", gatherData.ModuleWidthOfPosA.ToString());
            dic.Add("正极头部补偿采集值", gatherData.ModuleWidthOfPosB.ToString());
            dic.Add("正极尾部补偿采集值", gatherData.ModuleWidthOfPos.ToString());

            dic.Add("正极中部纠偏值", gatherData.ModuleWidthOfNeg.ToString());
            dic.Add("正极中部纠偏发送值", gatherData.OriginYOfPos.ToString());
            dic.Add("正极头部补偿发送值", gatherData.OriginYOfNeg.ToString());
            dic.Add("正极尾部补偿发送值", gatherData.OriginYOfNeg.ToString());

            dic.Add("闭环结果信息", calculateData.EndMessage ?? string.Empty);

            return dic;
        }
        #endregion
        #endregion
    }
}
