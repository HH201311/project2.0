using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteFileMonitor.Options;
using SUNWODA_SEVB.Core.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Services.Project.CTOL
{
    /// <summary>
    /// 间隙值 CSV 按天写入服务。
    /// 输出文件：{exe上级目录}/正负极间隙/GapDefectDetail_{yyyyMMdd}.csv
    /// 列：电芯条码, 工位, 图片名, 检测结果, 负极边缘间隙, 正极边缘间隙, 正极中间间隙
    /// </summary>
    public sealed class GapCsvService
    {
        private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);
        private const string Header = "电芯条码,工位,图片名,检测结果,负极边缘间隙,正极边缘间隙,正极中间间隙";

        private readonly string _directory;
        private readonly ILoggerService<GapCsvService> _logger;

        // 每天一把锁，防止多线程并发写同一文件
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

        public GapCsvService(ILoggerService<GapCsvService> logger)
        {
            // exe 当前目录的上一级目录下的 "正负极间隙" 子目录
            var exeParent = Path.GetDirectoryName(
                AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!;
            _directory = Path.GetFullPath(Path.Combine(exeParent, "正负极间隙"));
            _logger = logger;
        }

        /// <summary>
        /// 将一张图片的间隙检测数据追加写入当天 CSV 文件。
        /// 若文件不存在则自动创建并写入标题行。
        /// </summary>
        /// <param name="barcode">电芯条码（从文件名第一段提取）</param>
        /// <param name="monitorId">工位 ID（LAB / LCD / RAB / RCD）</param>
        /// <param name="imageName">原始图片文件名（含扩展名）</param>
        /// <param name="detectionResult">检测结论：OK 或 缺陷类型名</param>
        /// <param name="gapValues">AI 输出的间隙最大值数组：[0]=负极边缘, [1]=正极边缘, [2]=正极中间；null 或长度不足时对应列留空。</param>
        public async Task AppendAsync(
            string barcode,
            string monitorId,
            string imageName,
            string detectionResult,
            double[]? gapValues,
            CancellationToken ct = default)
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var filePath = Path.Combine(_directory, $"GapDefectDetail_{date}.csv");
            var sem = _fileLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));

            var neg = gapValues is { Length: > 0 } ? gapValues[0].ToString("F3") : "";
            var pos = gapValues is { Length: > 1 } ? gapValues[1].ToString("F3") : "";
            var mid = gapValues is { Length: > 2 } ? gapValues[2].ToString("F3") : "";
            var line = $"{Escape(barcode)},{Escape(monitorId)},{Escape(imageName)},{Escape(detectionResult)},{neg},{pos},{mid}";

            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                bool isNew = !File.Exists(filePath);
                Directory.CreateDirectory(_directory);

                await using var writer = new StreamWriter(filePath, append: true, Utf8Bom);
                if (isNew)
                    await writer.WriteLineAsync(Header).ConfigureAwait(false);
                await writer.WriteLineAsync(line).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error("[GapCsv] 写入失败：{filePath}", ex);
            }
            finally
            {
                sem.Release();
                PruneOldLocks();
            }
        }

        private static string Escape(string s) =>
            s.Contains(',') || s.Contains('"') || s.Contains('\n')
                ? $"\"{s.Replace("\"", "\"\"")}\""
                : s;

        /// <summary>
        /// 清理 2 天以前的过期锁，防止长期运行时字典无限增长。
        /// 每次写入完成后调用，代价极低（字典条数通常 ≤ 3）。
        /// </summary>
        private void PruneOldLocks()
        {
            // yyyyMMdd 字符串可直接按字典序比较大小
            var cutoff = DateTime.Today.AddDays(-2).ToString("yyyyMMdd");
            foreach (var key in _fileLocks.Keys)
            {
                if (string.Compare(key, cutoff, StringComparison.Ordinal) < 0)
                {
                    if (_fileLocks.TryRemove(key, out var stale))
                        stale.Dispose();
                }
            }
        }
    }
}
