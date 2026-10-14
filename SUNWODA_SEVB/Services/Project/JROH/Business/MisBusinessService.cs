using HandyControl.Controls;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.JROH;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Models.Project.JROH.MisBusinessModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper.JROHHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.JROH.BusinessDialogs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Services.Project.JROH.Business
{
    /// <summary>
    /// 极耳错位闭环业务服务（模板骨架）
    /// </summary>
    /// <remarks>
    /// <para>结构对齐同项目的 PW / OH / MC 三条业务：<c>InitializeAsync</c>（载参数 + 拼落盘路径）→ <c>ExecuteAsync</c>（闭环消费循环）→ <c>CleanupAsync</c>（释放）。</para>
    /// <para>⏸ <b>采集通道与闭环算法均未实现</b>（现场资料未到）：采集侧由后续在 <c>InitializeAsync</c> 内按资料接入（PLC 触发 / CCD / 文件监听），并把一条电芯数据交给 <see cref="EnqueueGatherData"/>；
    /// 算法侧补齐 <see cref="TryCalculateAsync"/>。本骨架**不读写任何 PLC 点位**，避免臆造点位（规范 B-09）。</para>
    /// </remarks>
    public class MisBusinessService : BusinessServiceBase<MisBusinessService>
    {
        private readonly ILoggerService<MisBusinessService> _logger;
        private readonly IProjectSettingRepository _projectSettingRepository;

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
            IProjectSettingRepository projectSettingRepository
        ) : base(logger, plcService, projectSettingRepository)
        {
            _logger = logger;
            _projectSettingRepository = projectSettingRepository;
        }

        /// <summary>
        /// 初始化业务
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        protected override Task InitializeAsync(CancellationToken ct)
        {
            _logger.Info($"初始化业务 {BusinessType} - {BusinessName}");

            // 初始化参数配置
            paramModel = LoadBusinessParamConfig<MisBusinessParamModel>();
            if (paramModel == null) throw new ArgumentException($"[{BusinessName}]参数为空！");

            // 初始化业务文件落盘根目录：{FileSaveRootPath}/{EBusinessType}
            var rootPath = _projectSettingRepository.LoadJROHFileSaveRootPathProjectParameter();
            fileSaveRootPath = Path.Combine(rootPath, BusinessType.ToString());

            // ⏸ TODO(极耳错位资料)：在此接入采集通道（PLC 触发 + CCD 取数 / CSV 文件监听等），
            //   一条电芯（或一个卷芯）组装成 MisBusinessGatherModel 后调用 EnqueueGatherData 入队。
            //   采集信号名、点位名与地址一律以 docs/JROH/plc点位表.csv 为准，禁止在代码里臆造。
            _logger.Warn($"[{BusinessName}]采集通道尚未接入（模板骨架）：本轮只运行闭环消费循环，不会有采集数据入队。");

            _logger.Info($"业务 {BusinessType} 初始化完成");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 执行业务
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _logger.Info($"业务 {BusinessType} 开始执行");

            // 闭环计算（采集数据入队后由该循环串行消费）
            await CalculateLoopAsync(ct);
        }

        /// <summary>
        /// 清理
        /// </summary>
        /// <returns></returns>
        protected override async Task CleanupAsync()
        {
            _logger.Info($"清理业务 {BusinessType} 资源");

            // ⏸ TODO(极耳错位资料)：采集通道接入后，此处同步停止监听/解绑事件

            // 收敛消费队列并重建（业务重启后仍可继续入队）
            var oldQueue = gatherDataModels;
            gatherDataModels = new BlockingCollection<MisBusinessGatherModel>();
            try { oldQueue.CompleteAdding(); }
            catch (Exception ex) { _logger.Warn($"[{BusinessName}]停止采集队列失败：{ex.Message}"); }
            try { oldQueue.Dispose(); }
            catch (Exception ex) { _logger.Warn($"[{BusinessName}]释放采集队列失败：{ex.Message}"); }

            // 释放取消令牌（先摘引用再释放，避免 Stop/清理重复释放时抛 ObjectDisposedException）
            var cts = _cts;
            _cts = null;
            try { cts?.Cancel(); }
            catch (ObjectDisposedException) { /* 已释放，忽略 */ }
            try { cts?.Dispose(); }
            catch (ObjectDisposedException) { /* 已释放，忽略 */ }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 显示业务配置对话框
        /// </summary>
        /// <returns></returns>
        public override async Task ShowBusinessParamConfigDialogAsync()
        {
            await ShowBusinessConfigDialogAsync<MisBusinessParamModel>(ShowBusinessConfigDialog);

            // 对话框关闭后重新加载（与其余三条业务一致）
            paramModel = LoadBusinessParamConfig<MisBusinessParamModel>();
            if (paramModel == null) throw new ArgumentException($"[{BusinessName}]参数为空！");
        }

        #region 参数配置
        /// <summary>
        /// 显示极耳错位闭环的配置对话框
        /// </summary>
        /// <typeparam name="TConfig"></typeparam>
        /// <param name="config"></param>
        /// <returns></returns>
        private async Task<bool> ShowBusinessConfigDialog<TConfig>(TConfig config) where TConfig : class
        {
            if (config is not MisBusinessParamModel model) return false;
            var result = await Dialog.Show(new VM_MisBusinessParamDialog().UIElement)
                    .Initialize<VM_MisBusinessParamDialog>(vm =>
                    {
                        vm.UserRole = UserRole;
                        vm.ItemSource = model;
                    }).GetResultAsync<bool>();
            return result;
        }
        #endregion

        #region 闭环全局参数
        /// <summary>
        /// 业务参数
        /// </summary>
        private MisBusinessParamModel? paramModel;

        /// <summary>
        /// 文件保存根目录
        /// </summary>
        private string fileSaveRootPath = string.Empty;

        /// <summary>
        /// 采集数据缓存（一条电芯 / 一个卷芯一条）
        /// </summary>
        private BlockingCollection<MisBusinessGatherModel> gatherDataModels = new();
        #endregion

        #region 采集入口（供后续接入的采集通道调用）
        /// <summary>
        /// 采集数据入队：由后续接入的采集通道（PLC 触发 / CCD / 文件监听）调用
        /// </summary>
        /// <param name="gatherData">一条电芯（或一个卷芯）的采集数据</param>
        protected void EnqueueGatherData(MisBusinessGatherModel? gatherData)
        {
            if (gatherData == null) return;

            if (gatherDataModels.IsAddingCompleted)
            {
                // 业务正在停止，本条件不再入队属正常收尾
                _logger.Warn($"[{BusinessName}]业务正在停止，采集数据不再入队。");
                return;
            }

            try { gatherDataModels.Add(gatherData); }
            catch (InvalidOperationException)
            {
                // 队列已 CompleteAdding（业务正在停止）或队列已释放（ObjectDisposedException 为其子类），正常收尾
            }

            // 通知界面刷新曲线
            OnDataReceived(gatherData);
        }
        #endregion

        #region 闭环计算
        /// <summary>
        /// 闭环计算逻辑（串行消费采集队列）
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task CalculateLoopAsync(CancellationToken ct)
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    foreach (var gatherData in gatherDataModels.GetConsumingEnumerable(ct))
                    {
                        var pm = paramModel?.CopyByJson();
                        if (pm == null) throw new ArgumentException($"[{BusinessName}]参数为空！");

                        var cm = new MisBusinessCalculateModel { CalculateTime = DateTime.Now };
                        try
                        {
                            var isCalculated = await TryCalculateAsync(gatherData, pm, cm);
                            cm.EndMessage = isCalculated
                                ? "Success"
                                : $"[{BusinessName}]闭环计算逻辑未实现（模板骨架）：本次只落盘、不下发，待现场资料补齐。";
                        }
                        catch (Exception ex)
                        {
                            cm.EndMessage += ex.Message;
                            _logger.Error($"[{BusinessName}]计算异常！", ex);
                        }
                        finally
                        {
                            // 界面数据更新 + 计算过程模型落盘（不阻塞消费循环）
                            OnAdjustmentRecordReceived(gatherData, cm);
                            SaveCalculateResult(gatherData, cm);
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
        /// 单次极耳错位闭环计算与下发（⏸ 模板占位，待现场资料实现）
        /// </summary>
        /// <param name="gatherData">一条电芯（或一个卷芯）的采集数据</param>
        /// <param name="pm">业务参数</param>
        /// <param name="cm">计算过程模型</param>
        /// <returns>是否真正完成闭环计算（true = 已按资料算过；false = 骨架未实现）</returns>
        /// <remarks>
        /// <para>资料到位后按以下顺序实现（对照 <c>docs/代码开发规范.md</c> 第二篇）：</para>
        /// <para>1) 从 <paramref name="gatherData"/> 取各通道采样，做有效性校验（采样数为 0 直接返回，避免 0/0）；</para>
        /// <para>2) 按现场工艺判据算出「是否调机 + 下发值」（含限幅、死区；是否使用方向修正系数待现场确认）；</para>
        /// <para>3) 本业务参数表未含闭环开关项：是否允许下发需先与现场确认，未确认前一律只记「推荐下发值」、<b>不写 PLC</b>（规范 A-03）；</para>
        /// <para>4) 下发用 <c>WritePLCAsync("&lt;点位名&gt;", 值)</c>，点位名以 <c>docs/JROH/plc点位表.csv</c> 为准，<b>禁止臆造</b>（规范 B-09）；</para>
        /// <para>5) 写值必须判成败并记录/上报（规范 B-01），关键信号建议读回确认（规范 B-02）。</para>
        /// </remarks>
        private Task<bool> TryCalculateAsync(MisBusinessGatherModel gatherData, MisBusinessParamModel pm, MisBusinessCalculateModel cm)
        {
            _logger.Warn($"[{BusinessName}]闭环计算逻辑未实现（模板骨架）：采集数据只落盘、不下发。虚拟码[{gatherData?.VirtualCode}]");
            return Task.FromResult(false);
        }

        /// <summary>
        /// 数据接收事件处理（通知界面刷新曲线）
        /// </summary>
        /// <param name="gatherData"></param>
        private void OnDataReceived(MisBusinessGatherModel gatherData)
        {
            var param = paramModel?.CopyByJson();
            RaiseMessageNotifation(this, (gatherData, param));
        }

        /// <summary>
        /// 闭环计算结果接收事件处理（通知界面刷新调机记录）
        /// </summary>
        /// <param name="gatherData"></param>
        /// <param name="cm"></param>
        private void OnAdjustmentRecordReceived(MisBusinessGatherModel gatherData, MisBusinessCalculateModel cm)
        {
            var posRecord = new Dictionary<string, string>
            {
                { "正极是否调机", cm.IsAdjustOfPos ? "是" : "否" },
                { "正极调机下发值", cm.IssuedValueOfPos.ToString() },
            };
            RaiseMessageNotifation(this, ("PosCorrect", posRecord));

            var negRecord = new Dictionary<string, string>
            {
                { "负极是否调机", cm.IsAdjustOfNeg ? "是" : "否" },
                { "负极调机下发值", cm.IssuedValueOfNeg.ToString() },
            };
            RaiseMessageNotifation(this, ("NegCorrect", negRecord));
        }

        /// <summary>
        /// 闭环结果落盘（不阻塞消费循环）
        /// </summary>
        /// <param name="gatherData"></param>
        /// <param name="cm"></param>
        private void SaveCalculateResult(MisBusinessGatherModel gatherData, MisBusinessCalculateModel cm)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var savePath = Path.Combine(fileSaveRootPath, $"缓存数据_{BusinessName}\\{DateTime.Now:yyyyMMdd}");
                    var dic = ConvertToDictionary(gatherData, cm);
                    var dt = JROHCsvHelper.DicToTable(dic);
                    lock (JROHCsvHelper.WriteLocker) JROHCsvHelper.WriteCSV(savePath, dt);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[{BusinessName}]闭环结果落盘失败！", ex);
                }
            });
        }

        /// <summary>
        /// CSV文件：闭环结果保存
        /// </summary>
        /// <param name="gatherData"></param>
        /// <param name="calculateData"></param>
        /// <returns></returns>
        /// <remarks>⏸ 列名随采集/计算字段变化，资料到位后同步更新 <c>docs/JROH/配置键表.csv</c> 的「CSV输出列」。</remarks>
        private Dictionary<string, string> ConvertToDictionary(MisBusinessGatherModel gatherData, MisBusinessCalculateModel calculateData)
        {
            var dic = new Dictionary<string, string>
            {
                { "采集时间", gatherData.GatherTime.ToString("yyyy-MM-dd HH:mm:ss.fff") },
                { "虚拟码 ", gatherData.VirtualCode ?? string.Empty },

                { "正极极耳错位实时值-目标值", FormatPairValues(gatherData.PositiveTabValues) },
                { "负极极耳错位实时值-目标值", FormatPairValues(gatherData.NegativeTabValues) },

                { "正极是否调机", calculateData.IsAdjustOfPos ? "是" : "否" },
                { "正极调机下发值", calculateData.IssuedValueOfPos.ToString() },
                { "负极是否调机", calculateData.IsAdjustOfNeg ? "是" : "否" },
                { "负极调机下发值", calculateData.IssuedValueOfNeg.ToString() },
                { "闭环结果信息", calculateData.EndMessage },
            };
            return dic;
        }

        /// <summary>
        /// 格式化检测值-目标值对
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        private string FormatPairValues(List<(DateTime time, float detValue, float tarValue)>? values)
        {
            if (values == null || values.Count == 0) return string.Empty;
            return string.Join('|', values.ConvertAll(o => $"{o.detValue}-{o.tarValue}"));
        }
        #endregion
    }
}
