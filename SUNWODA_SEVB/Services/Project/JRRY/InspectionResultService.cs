using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SUNWODA_SEVB.Services.Project.CTOL;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace SUNWODA_SEVB.Services.Project.JRRY
{
    public class InspectionResultService(ILogger<InspectionResultService> logger)
    {
        private readonly string _dbDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inspection_result_jrry");
        private static readonly ConcurrentDictionary<string, bool> _initialized = new(StringComparer.OrdinalIgnoreCase);

        public async Task SaveAsync(string barcode, string defectType, string detectionResult, CancellationToken ct = default)
        {
            try
            {
                var result = new InspectionResult
                {
                    Time = DateTime.Now,
                    StationId = "JRRY",
                    Barcode = barcode,
                    DefectType = defectType,
                    DetectionResult = detectionResult,
                };

                await using var db = OpenOrCreate(DateTime.Today);
                db.InspectionResults.Add(result);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[JRRY] 写入检测结果失败：{Barcode}", barcode);
            }
        }

        public async Task<List<InspectionResult>> QueryAsync
        (
            DateTime startTime,
            DateTime endTime,
            IEnumerable<string>? defectTypes = null,
            CancellationToken ct = default
        )
        {
            var all = new List<InspectionResult>();
            var types = defectTypes?.ToList();

            // 按天枚举各个数据库分文件
            for (var date = startTime.Date; date <= endTime.Date; date = date.AddDays(1))
            {
                await using var db = OpenExisting(date);
                if (db is null) continue;

                // 仅做时间范围过滤，缺陷类型在内存中过滤（避免复杂 SQL）
                var q = db.InspectionResults
                    .AsNoTracking()
                    .Where(r => r.Time >= startTime && r.Time <= endTime);

                all.AddRange(await q.ToListAsync(ct));
            }

            // 如果指定了缺陷类型，进行内存模糊匹配（OR 逻辑）
            if (types is { Count: > 0 })
            {
                all = [.. all.Where(r =>
                {
                    if (string.IsNullOrEmpty(r.DefectType)) return false;
                    // 将 DefectType 字段按 “、” 分割成单个类型
                    var recordTypes = r.DefectType.Split('、', StringSplitOptions.RemoveEmptyEntries);
                    // 任意一个记录类型包含在查询类型列表中即满足条件
                    return recordTypes.Any(rt => types.Contains(rt, StringComparer.OrdinalIgnoreCase));
                })];
            }

            return [.. all.OrderByDescending(r => r.Time)];
        }

        public static async Task ExportCsvAsync(IEnumerable<InspectionResult> results, string filePath, CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            sb.AppendLine("时间,条码,缺陷类型,检测结果");
            foreach (var r in results)
            {
                sb.AppendLine($"{r.Time:yyyy-MM-dd HH:mm:ss},{EscapeCsv(r.Barcode)},{EscapeCsv(r.DefectType)},{EscapeCsv(r.DetectionResult)}");
            }
            await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(true), ct);
        }

        private InspectionDbContext OpenOrCreate(DateTime date)
        {
            var path = GetDbPath(date);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var ctx = BuildContext(path);
            if (_initialized.TryAdd(path, true))
                ctx.Database.EnsureCreated();
            return ctx;
        }

        private InspectionDbContext? OpenExisting(DateTime date)
        {
            var path = GetDbPath(date);
            if (!File.Exists(path)) return null;
            _initialized.TryAdd(path, true);
            return BuildContext(path);
        }

        private string GetDbPath(DateTime date) =>
            Path.Combine(_dbDirectory, $"inspection_{date:yyyyMMdd}.db");

        private static InspectionDbContext BuildContext(string path)
        {
            var opt = new DbContextOptionsBuilder<InspectionDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            return new InspectionDbContext(opt);
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}