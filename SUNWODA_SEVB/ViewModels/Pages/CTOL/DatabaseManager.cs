using Microsoft.Data.Sqlite;
using SUNWODA_SEVB.Core.Interfaces;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;

namespace SUNWODA_SEVB.ViewModels.Pages.CTOL
{
    /// <summary>
    /// 每天一个 SQLite 文件，位于 BaseDirectory\.monitor\YYYY-MM-DD.db
    /// 线程安全（同一天串行化）；只保留最近 5 天的“每日对象”，跨日自动切换。
    /// 关键点：不共享 SqliteConnection，每次操作独立打开/关闭连接，彻底避免并发使用同一连接的问题。
    /// </summary>
    public sealed class DatabaseManager : IDisposable
    {
        private readonly ILoggerService _log;

        // key = "yyyy-MM-dd"
        private readonly ConcurrentDictionary<string, DayDb> _days = new();
        private readonly SemaphoreSlim _daysLock = new(1, 1);

        private const int RetentionDays = 5;

        public DatabaseManager(ILoggerService log)
        {
            _log = log;
        }

        /// <summary>文件路径是否已在指定日期的 DB 中记录为已处理</summary>
        public async Task<bool> IsProcessedAsync(string filePath, DateOnly date)
        {
            var day = await GetDayDbAsync(date);

            await day.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await EnsureSchemaOnceAsync(day).ConfigureAwait(false);

                await using var conn = new SqliteConnection(day.ConnectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1 FROM processed_files WHERE file_path = @p LIMIT 1;";
                cmd.Parameters.AddWithValue("@p", filePath);

                var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                return result is not null;
            }
            finally
            {
                day.Gate.Release();
            }
        }

        /// <summary>将文件标记为已处理</summary>
        public async Task MarkProcessedAsync(string filePath, DateOnly date, string source)
        {
            var day = await GetDayDbAsync(date);

            await day.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await EnsureSchemaOnceAsync(day).ConfigureAwait(false);

                await using var conn = new SqliteConnection(day.ConnectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                INSERT OR IGNORE INTO processed_files(file_path, processed_at, source)
                VALUES (@p, @t, @s);
                """;
                cmd.Parameters.AddWithValue("@p", filePath);
                cmd.Parameters.AddWithValue("@t", DateTimeOffset.UtcNow.ToString("O"));
                cmd.Parameters.AddWithValue("@s", source);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            finally
            {
                day.Gate.Release();
            }
        }

        private async Task<DayDb> GetDayDbAsync(DateOnly date)
        {
            var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (_days.TryGetValue(key, out var existing))
                return existing;

            await _daysLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // double-check
                if (_days.TryGetValue(key, out existing))
                    return existing;

                CleanupOldDays(date);

                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".monitor");
                Directory.CreateDirectory(dir);

                var dbPath = Path.Combine(dir, $"{key}.db");
                var cs = $"Data Source={dbPath};Cache=Shared";

                var created = new DayDb(key, dbPath, cs);
                _days[key] = created;

                _log.Info($"数据库加载 {key}: {dbPath}");
                return created;
            }
            finally
            {
                _daysLock.Release();
            }
        }

        /// <summary>
        /// 只保留 currentDate 及往前 RetentionDays 天；更早的 DayDb 全部移除并释放资源（semaphore）。
        /// </summary>
        private void CleanupOldDays(DateOnly currentDate)
        {
            var threshold = currentDate.AddDays(-RetentionDays);

            foreach (var kv in _days)
            {
                if (!DateOnly.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    continue;

                if (d < threshold)
                {
                    if (_days.TryRemove(kv.Key, out var old))
                    {
                        old.Dispose();
                        _log.Info($"移除旧数据库: {kv.Key}");
                    }
                }
            }
        }

        /// <summary>
        /// 确保表结构（每个 DayDb 只做一次）；调用方需要已持有 day.Gate。
        /// </summary>
        private static async Task EnsureSchemaOnceAsync(DayDb day)
        {
            if (day.SchemaEnsured)
                return;

            await using var conn = new SqliteConnection(day.ConnectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;

            CREATE TABLE IF NOT EXISTS processed_files (
                file_path    TEXT PRIMARY KEY,
                processed_at TEXT NOT NULL,
                source       TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_processed_at ON processed_files(processed_at);
            """;
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            day.SchemaEnsured = true;
        }

        public void Dispose()
        {
            foreach (var kv in _days)
                kv.Value.Dispose();

            _daysLock.Dispose();
        }

        private sealed class DayDb : IDisposable
        {
            public string Key { get; }
            public string DbPath { get; }
            public string ConnectionString { get; }
            public SemaphoreSlim Gate { get; } = new(1, 1);

            // 调用方在 Gate 内访问/修改，保证线程安全
            public bool SchemaEnsured { get; set; }

            public DayDb(string key, string dbPath, string connectionString)
            {
                Key = key;
                DbPath = dbPath;
                ConnectionString = connectionString;
            }

            public void Dispose()
            {
                Gate.Dispose();
            }
        }
    }
}
