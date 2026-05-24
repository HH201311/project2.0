using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteFileMonitor.Options;
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
    /// 单条电芯检测结果记录，对应数据库表 InspectionResults。
    /// 每个 barcode 完成分组聚合后写入一条。
    /// </summary>
    public sealed class InspectionResult
    {
        public long Id { get; set; }

        /// <summary>检测完成时间（本地时间）。</summary>
        public DateTime Time { get; set; }

        /// <summary>工位编号，即 monitorId（如 LAB / LCD / RAB / RCD）。</summary>
        public string StationId { get; set; } = string.Empty;

        /// <summary>电芯条码。</summary>
        public string Barcode { get; set; } = string.Empty;

        /// <summary>
        /// 缺陷类型。OK 时为 "OK"；NG 时为具体缺陷名称（如 "OH不良"）；
        /// 算法异常时为 "算法异常"；超时未达八张时为 "超时未达八张"。
        /// </summary>
        public string DefectType { get; set; } = string.Empty;

        /// <summary>综合判定结果："OK" / "NG" / "算法异常" / "超时"。</summary>
        public string DetectionResult { get; set; } = string.Empty;
    }

    /// <summary>
    /// 检测结果数据库上下文（单文件 SQLite，跨天查询）。
    /// 数据库路径由 <see cref="InspectionResultService"/> 在启动时通过 EnsureCreated 创建。
    /// </summary>
    public sealed class InspectionDbContext : DbContext
    {
        public DbSet<InspectionResult> InspectionResults { get; set; } = null!;

        public InspectionDbContext(DbContextOptions<InspectionDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<InspectionResult>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.StationId).HasMaxLength(50).IsRequired();
                e.Property(x => x.Barcode).HasMaxLength(200).IsRequired();
                e.Property(x => x.DefectType).HasMaxLength(100).IsRequired();
                e.Property(x => x.DetectionResult).HasMaxLength(20).IsRequired();

                // 时间范围查询（主要查询轴）
                e.HasIndex(x => x.Time)
                 .HasDatabaseName("IX_InspectionResults_Time");

                // 工位 + 时间联合查询
                e.HasIndex(x => new { x.StationId, x.Time })
                 .HasDatabaseName("IX_InspectionResults_StationId_Time");

                // 缺陷类型过滤
                e.HasIndex(x => x.DefectType)
                 .HasDatabaseName("IX_InspectionResults_DefectType");
            });
        }
    }

    /// <summary>
    /// 检测结果持久化与查询服务。
    ///
    /// <para><b>存储策略：按天分文件。</b>
    /// 文件路径：<c>{DatabaseDirectory}/inspection_{yyyyMMdd}.db</c>。
    /// 每天一个 SQLite 文件，与 AppDbContext 的分日模式保持一致，
    /// 单文件数据量恒定，查询性能不随时间退化，也方便按天归档清理。</para>
    ///
    /// <para>跨日期查询时按日期范围枚举各天文件，在内存中合并后返回。</para>
    ///
    /// <para>导出：<see cref="ExportCsvAsync"/> 将查询结果写为 UTF-8 BOM CSV 文件。</para>
    /// </summary>
    public sealed class InspectionResultService
    {
        private readonly string _dbDirectory;
        private readonly ILogger<InspectionResultService> _logger;

        // 已执行过 EnsureCreated 的 DB 文件路径缓存（避免重复调用）
        private readonly ConcurrentDictionary<string, bool> _initialized =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] CsvHeader =
            ["时间", "工位", "条码", "缺陷类型", "检测结果"];

        public InspectionResultService(
            IOptions<MonitorOptions> monitorOptions,
            ILogger<InspectionResultService> logger)
        {
            _dbDirectory = Path.GetFullPath(monitorOptions.Value.DatabaseDirectory);
            _logger = logger;
        }

        // ==================== 写入 ====================

        /// <summary>
        /// 将一条 barcode 检测结果写入当天的数据库文件。
        /// </summary>
        public async Task SaveAsync(
            string monitorId, string barcode, string category,
            CancellationToken ct = default)
        {
            try
            {
                var detectionResult = category switch
                {
                    "OK" => "OK",
                    var c when c.StartsWith("算法") => "算法异常",
                    var c when c.StartsWith("超时") => "超时",
                    _ => "NG",
                };

                var result = new InspectionResult
                {
                    Time = DateTime.Now,
                    StationId = monitorId,
                    Barcode = barcode,
                    DefectType = category,
                    DetectionResult = detectionResult,
                };

                await using var db = OpenOrCreate(DateTime.Today);
                db.InspectionResults.Add(result);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Inspection] 写入检测结果失败：{Barcode}", barcode);
            }
        }

        // ==================== 查询 ====================

        /// <summary>
        /// 按时间范围和缺陷类型列表查询检测结果，自动跨越多个日期分文件，
        /// 在内存中合并后按时间降序返回。
        /// <paramref name="defectTypes"/> 为 null 或空时不限制缺陷类型（全选）。
        /// </summary>
        public async Task<List<InspectionResult>> QueryAsync(
            DateTime startTime,
            DateTime endTime,
            IEnumerable<string>? defectTypes = null,
            CancellationToken ct = default)
        {
            var all = new List<InspectionResult>();
            var types = defectTypes?.ToList();

            // 按天枚举各分文件，跳过不存在的日期
            for (var date = startTime.Date; date <= endTime.Date; date = date.AddDays(1))
            {
                await using var db = OpenExisting(date);
                if (db is null) continue;

                var q = db.InspectionResults
                    .AsNoTracking()
                    .Where(r => r.Time >= startTime && r.Time <= endTime);

                if (types is { Count: > 0 })
                {
                    q = q.Where(r => types.Contains(r.DefectType));
                    all.AddRange(await q.ToListAsync(ct));
                }
            }

            return all.OrderByDescending(r => r.Time).ToList();
        }

        // ==================== CSV 导出 ====================

        /// <summary>
        /// 将 <paramref name="results"/> 写为 UTF-8 BOM CSV 文件，覆盖目标路径。
        /// </summary>
        public static async Task ExportCsvAsync(
            IEnumerable<InspectionResult> results,
            string filePath,
            CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", CsvHeader));

            foreach (var r in results)
            {
                sb.AppendLine(string.Join(",",
                    r.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                    EscapeCsv(r.StationId),
                    EscapeCsv(r.Barcode),
                    EscapeCsv(r.DefectType),
                    EscapeCsv(r.DetectionResult)));
            }

            // UTF-8 with BOM — Excel 打开中文不乱码
            await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(true), ct);
        }

        // ==================== 私有辅助 ====================

        /// <summary>打开（或首次创建）指定日期的分文件 DbContext。</summary>
        private InspectionDbContext OpenOrCreate(DateTime date)
        {
            var path = GetDbPath(date);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var ctx = BuildContext(path);
            if (_initialized.TryAdd(path, true))
                ctx.Database.EnsureCreated();
            return ctx;
        }

        /// <summary>打开已存在的指定日期分文件；文件不存在时返回 null。</summary>
        private InspectionDbContext? OpenExisting(DateTime date)
        {
            var path = GetDbPath(date);
            if (!File.Exists(path)) return null;
            _initialized.TryAdd(path, true); // 文件已存在视为已初始化
            return BuildContext(path);
        }

        private string GetDbPath(DateTime date) =>
            Path.Combine(_dbDirectory, "inspection_result", $"inspection_{date:yyyyMMdd}.db");

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
