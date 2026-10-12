using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>
    /// 界面统计持久化：三码/一次优率计数（全量）+ 底部检测日志（最近 MaxLogs 条）。
    /// 仅「统计清零」清空；进程重启后从 LocalPathPre\检测结果\Pack6_界面统计.json 恢复。
    /// </summary>
    public static class Pack6UiStatsStore
    {
        public const string FileName = "Pack6_界面统计.json";
        /// <summary>底部「检测日志」表保留条数（最新在前）。</summary>
        public const int MaxLogs = 100;

        public static string GetStorePath(Pack6Config config)
        {
            var root = string.IsNullOrWhiteSpace(config.LocalPathPre)
                ? @"D:\六面检上位机软件"
                : config.LocalPathPre.Trim();
            return Path.Combine(root, "检测结果", FileName);
        }

        public static void Save(
            Pack6Config config,
            IEnumerable<Pack6StationStat> stations,
            IEnumerable<Pack6UiResultLogDto> logs)
        {
            var path = GetStorePath(config);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var snap = new Pack6UiStatsSnapshot
            {
                SavedAt = DateTime.Now,
                Stations = stations
                    .Select(s => new Pack6UiStationDto
                    {
                        Station = s.Station,
                        Total = s.Total,
                        OK = s.OK,
                        NG = s.NG
                    })
                    .ToList(),
                ResultLogs = logs.Take(MaxLogs).ToList()
            };

            var json = JsonConvert.SerializeObject(snap, Formatting.Indented);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            File.Copy(tmp, path, overwrite: true);
            try { File.Delete(tmp); } catch { /* ignore */ }
        }

        public static bool TryLoad(Pack6Config config, out Pack6UiStatsSnapshot snapshot)
        {
            snapshot = new Pack6UiStatsSnapshot();
            try
            {
                var path = GetStorePath(config);
                if (!File.Exists(path))
                    return false;
                var json = File.ReadAllText(path, Encoding.UTF8);
                var loaded = JsonConvert.DeserializeObject<Pack6UiStatsSnapshot>(json);
                if (loaded is null)
                    return false;
                snapshot = loaded;
                snapshot.ResultLogs ??= [];
                snapshot.Stations ??= [];
                if (snapshot.ResultLogs.Count > MaxLogs)
                    snapshot.ResultLogs = snapshot.ResultLogs.Take(MaxLogs).ToList();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ApplyStations(IReadOnlyList<Pack6StationStat> targets, IEnumerable<Pack6UiStationDto> source)
        {
            foreach (var dto in source)
            {
                if (string.IsNullOrWhiteSpace(dto.Station))
                    continue;
                var row = targets.FirstOrDefault(s =>
                    string.Equals(s.Station, dto.Station, StringComparison.Ordinal));
                if (row is null)
                    continue;
                row.Total = Math.Max(0, dto.Total);
                row.OK = Math.Max(0, dto.OK);
                row.NG = Math.Max(0, dto.NG);
            }
        }
    }

    public sealed class Pack6UiStatsSnapshot
    {
        public DateTime SavedAt { get; set; }
        public List<Pack6UiStationDto> Stations { get; set; } = [];
        public List<Pack6UiResultLogDto> ResultLogs { get; set; } = [];
    }

    public sealed class Pack6UiStationDto
    {
        public string Station { get; set; } = string.Empty;
        public int Total { get; set; }
        public int OK { get; set; }
        public int NG { get; set; }
    }

    public sealed class Pack6UiResultLogDto
    {
        public string TimeText { get; set; } = string.Empty;
        public string Sn { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string NgDetail { get; set; } = string.Empty;
    }
}
