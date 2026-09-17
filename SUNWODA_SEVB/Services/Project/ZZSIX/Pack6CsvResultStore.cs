using System.IO;
using System.Text;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    public sealed class Pack6CsvResultStore : IZZSIXPack6ResultStore
    {
        private readonly ILoggerService<Pack6CsvResultStore> _logger;
        private readonly SemaphoreSlim _ioLock = new(1, 1);

        public Pack6CsvResultStore(ILoggerService<Pack6CsvResultStore> logger)
        {
            _logger = logger;
        }

        public Task EnsureFoldersAsync(Pack6Config config)
        {
            Directory.CreateDirectory(GetResultCsvDirectory(config));
            Pack6ResultOverlayHelper.EnsureAndPrune(config);
            return Task.CompletedTask;
        }

        public string GetResultCsvDirectory(Pack6Config config) =>
            Path.Combine(config.LocalPathPre, "检测结果");

        public async Task AppendProductResultAsync(Pack6Config config, Pack6ProductResult result)
        {
            await _ioLock.WaitAsync();
            try
            {
                var dir = GetResultCsvDirectory(config);
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{DateTime.Now:yyyy_MM_dd}_Pack6.csv");
                var needHeader = !File.Exists(path);
                await using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                await using var writer = new StreamWriter(fs, Encoding.UTF8);
                if (needHeader)
                    await writer.WriteLineAsync("DateTime,SN,Result,NGDetails,TimedOut");

                var ng = result.Defects.Count == 0 ? string.Empty : string.Join('|', result.Defects);
                await writer.WriteLineAsync(
                    $"{result.CompletedAt:yyyy-MM-dd HH:mm:ss},{Escape(result.ProductCode)},{(result.IsOk ? "OK" : "NG")},{Escape(ng)},{result.TimedOut}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"写入 Pack6 CSV 失败: {ex.Message}", false);
            }
            finally
            {
                _ioLock.Release();
            }
        }

        public async Task AppendFrameResultAsync(Pack6Config config, Pack6FrameResult frame)
        {
            if (frame.OcrMode == Pack6OcrMode.None)
                return;

            await _ioLock.WaitAsync();
            try
            {
                var dir = GetResultCsvDirectory(config);
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{DateTime.Now:yyyy_MM_dd}_Pack6_OCR.csv");
                var needHeader = !File.Exists(path);
                await using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                await using var writer = new StreamWriter(fs, Encoding.UTF8);
                if (needHeader)
                {
                    await writer.WriteLineAsync(
                        "DateTime,SN,Queue,Station,OcrMode,Result,QrData,PartSN,ProductSN,EngineMessage,NGDetails,ImagePath");
                }

                var ng = frame.Defects.Count == 0 ? string.Empty : string.Join('|', frame.Defects);
                await writer.WriteLineAsync(
                    $"{frame.DetectedAt:yyyy-MM-dd HH:mm:ss}," +
                    $"{Escape(frame.ProductCode)},{Escape(frame.QueueName)},{Escape(frame.StationName)},{frame.OcrMode}," +
                    $"{(frame.IsOk ? "OK" : "NG")},{Escape(frame.QrData)},{Escape(frame.PartSN)},{Escape(frame.ProductSN)}," +
                    $"{Escape(frame.EngineMessage)},{Escape(ng)},{Escape(frame.FilePath)}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"写入 Pack6 OCR CSV 失败: {ex.Message}", false);
            }
            finally
            {
                _ioLock.Release();
            }
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }
    }
}
