using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteFileMonitor.Options;
using SUNWODA_SEVB.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Services.Project.CTOL
{
    /// <summary>
    /// 每日自动上传配置，对应 appsettings 中的 "DailyUpload" 节。
    ///
    /// 功能：程序检测到新的一天后，将前一天各工位输出目录下
    /// 指定缺陷类型子文件夹内容复制到集中上传目录。
    /// 目标目录结构：{UploadTargetDirectory}/{工位Id}/{yyyyMMdd}/{缺陷类型}/
    /// </summary>
    public sealed class DailyUploadOptions
    {
        /// <summary>是否启用每日自动上传。默认 false。</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// 上传目标根目录（绝对路径或相对于程序目录的路径）。
        /// 最终目录结构：{UploadTargetDirectory}/{工位Id}/{yyyyMMdd}/{缺陷类型}/
        /// </summary>
        public string UploadTargetDirectory { get; set; } = "upload";

        /// <summary>
        /// 要上传的缺陷类型文件夹名称列表。
        /// 为空时上传每个工位输出日期目录下所有子文件夹（OK 条件下的独立 barcode 目录除外）。
        /// </summary>
        public List<string> UploadDefectTypes { get; set; } = new();

        /// <summary>触发上传的小时（0–23），默认 0（午夜）。</summary>
        public int TriggerHour { get; set; } = 0;

        /// <summary>触发上传的分钟（0–59），默认 5（00:05）。</summary>
        public int TriggerMinute { get; set; } = 5;
    }

    /// <summary>
    /// 每日自动上传后台服务。
    ///
    /// 每分钟检测当前时间：当日期发生进位且时间已过 TriggerHour:TriggerMinute 后，
    /// 将前一天各工位输出目录下指定缺陷类型的文件夹复制到集中上传目录。
    ///
    /// BarcodeGroupingService 整理后的实际输出结构：
    ///   {OutputDirectory}/{日期目录(任意名)}/{缺陷类型}/{barcodeId}/
    ///   日期目录名格式不固定，通过 LastWriteTime.Date 匹配目标日期。
    ///
    /// 上传目标结构与来源保持一致（保留原始日期目录名）：
    ///   {UploadTargetDirectory}/{工位Id}/{日期目录}/{缺陷类型}/{barcodeId}/
    ///
    /// 支持运行时通过 <see cref="UpdateOptions"/> 热更新配置（UI 修改后立即生效）。
    /// </summary>
    public sealed class DailyUploadService : BackgroundService
    {
        private DailyUploadOptions _options;
        private readonly MonitorOptions _monitorOptions;
        private readonly ILoggerService<DailyUploadService> _logger;

        /// <summary>记录最近一次「已完成上传」的日期（即前一天的日期）。</summary>
        private DateOnly _lastUploadedYesterday = DateOnly.MinValue;

        public DailyUploadService(
            IOptions<DailyUploadOptions> options,
            IOptions<MonitorOptions> monitorOptions,
            ILoggerService<DailyUploadService> logger)
        {
            _options = options.Value;
            _monitorOptions = monitorOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// 热更新每日上传配置（UI 修改后调用，无需重启服务即可生效）。
        /// </summary>
        public void UpdateOptions(DailyUploadOptions newOptions)
        {
            _options = newOptions;
            _logger.Info($"[DailyUpload] 配置已更新：Enabled={newOptions.Enabled}，目标目录={newOptions.UploadTargetDirectory}");
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _logger.Info($"[DailyUpload] 服务已启动（Enabled={_options.Enabled}），每分钟检测上传时机。");

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (!_options.Enabled) continue;

                try
                {
                    await CheckAndUploadAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error("[DailyUpload] 上传检测循环发生未处理异常。", ex);
                }
            }
        }

        // ==================== 触发判断 ====================

        private async Task CheckAndUploadAsync(CancellationToken ct)
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var yesterday = today.AddDays(-1);

            // 已过触发时间，且昨天的数据尚未上传
            bool triggerTimePassed =
                now.Hour > _options.TriggerHour ||
                (now.Hour == _options.TriggerHour && now.Minute >= _options.TriggerMinute);

            if (!triggerTimePassed || _lastUploadedYesterday >= yesterday)
                return;

            _logger.Info($"[DailyUpload] 检测到新的一天 {today}，开始上传 {yesterday} 的数据。");

            await UploadForDateAsync(yesterday, ct).ConfigureAwait(false);
            _lastUploadedYesterday = yesterday;
        }

        // ==================== 上传核心逻辑 ====================

        private async Task UploadForDateAsync(DateOnly date, CancellationToken ct)
        {
            var uploadRoot = GetAbsolutePath(_options.UploadTargetDirectory);

            foreach (var target in _monitorOptions.Targets)
            {
                if (string.IsNullOrEmpty(target.OutputDirectory))
                    continue;

                var outputRoot = GetAbsolutePath(target.OutputDirectory);
                if (!Directory.Exists(outputRoot))
                {
                    _logger.Info(
                        $"[DailyUpload] 工位 {target.Id} 输出目录不存在：{outputRoot}，跳过。");
                    continue;
                }

                // 日期目录名格式不固定，通过 LastWriteTime.Date 匹配目标日期
                // 结构：{OutputDirectory}/{日期目录(任意名)}/{缺陷类型}/{barcodeId}/
                var dateDirs = Directory.EnumerateDirectories(outputRoot)
                    .Where(d => DateOnly.FromDateTime(new DirectoryInfo(d).LastWriteTime) == date)
                    .ToList();

                if (dateDirs.Count == 0)
                {
                    _logger.Info(
                        "[DailyUpload] 工位 {target.Id} 未找到 {date} 对应的日期目录，跳过。");
                    continue;
                }

                foreach (var sourceDateDir in dateDirs)
                {
                    var dateDirName = Path.GetFileName(sourceDateDir)!;

                    // 确定要上传的缺陷类型：配置中指定
                    IEnumerable<string> defectTypes = _options.UploadDefectTypes;


                    // 确定要上传的缺陷类型：配置中指定，或自动枚举日期目录下所有子文件夹
                    //IEnumerable<string> defectTypes = _options.UploadDefectTypes.Count > 0
                    //    ? _options.UploadDefectTypes
                    //    : Directory.EnumerateDirectories(sourceDateDir)
                    //               .Select(Path.GetFileName)
                    //               .Where(d => d is not null)
                    //               .Cast<string>();

                    foreach (var defectType in defectTypes)
                    {
                        ct.ThrowIfCancellationRequested();

                        // 来源：{OutputDirectory}/{日期目录}/{缺陷类型}/
                        var sourceDefectDir = Path.Combine(sourceDateDir, defectType);
                        if (!Directory.Exists(sourceDefectDir))
                            continue;

                        // 目标保留原始日期目录名，结构与来源完全一致：
                        // {UploadTargetDirectory}/{工位Id}/{日期目录}/{缺陷类型}/
                        var destDir = Path.Combine(uploadRoot, target.Id, dateDirName, defectType);

                        try
                        {
                            await CopyDirectoryAsync(sourceDefectDir, destDir, ct).ConfigureAwait(false);
                            _logger.Info(
                                $"[DailyUpload] 已上传 {target.Id}/{dateDirName}/{defectType} → {destDir}");
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("[DailyUpload] 上传失败 {target.Id}/{dateDirName}/{defectType}", ex);
                        }
                    }
                }
            }

            _logger.Info("[DailyUpload] {date} 全部工位上传完成。");
        }

        // ==================== 辅助方法 ====================

        /// <summary>将相对路径转换为以 AppContext.BaseDirectory 为基准的绝对路径。</summary>
        private static string GetAbsolutePath(string path) =>
            Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(path, AppContext.BaseDirectory);

        private static async Task CopyDirectoryAsync(string source, string dest, CancellationToken ct)
        {
            Directory.CreateDirectory(dest);

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(source, file);
                var destFile = Path.Combine(dest, relativePath);

                var destFileDir = Path.GetDirectoryName(destFile);
                if (destFileDir is not null)
                    Directory.CreateDirectory(destFileDir);

                // 已存在则跳过（幂等，避免重复覆盖）
                if (!File.Exists(destFile))
                    await Task.Run(() => File.Copy(file, destFile), ct).ConfigureAwait(false);
            }
        }
    }
}
