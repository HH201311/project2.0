using System.Globalization;
using System.IO;
using System.Text;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive;
using SUNWODA_SEVB.Core.Models.Project.AIPredictive;

namespace SUNWODA_SEVB.Services.Project.AIPredictive
{
    /// <summary>
    /// 从 <see cref="AppContext.BaseDirectory"/>/AIPredictive/ 按厂家加载采集块 CSV。
    /// 先导 / 赢合互斥，同一时刻只加载一家点位。
    /// </summary>
    public sealed class AIPredictiveCollectBlockLoader : IAIPredictiveCollectBlockLoader
    {
        private readonly IAIPredictivePlcEndpointStore _endpointStore;
        private readonly ILoggerService<AIPredictiveCollectBlockLoader> _logger;

        public AIPredictiveCollectBlockLoader(
            IAIPredictivePlcEndpointStore endpointStore,
            ILoggerService<AIPredictiveCollectBlockLoader> logger)
        {
            _endpointStore = endpointStore;
            _logger = logger;
        }

        public IReadOnlyList<AIPredictiveCollectBlock> LoadAll()
        {
            var vendor = AIPredictiveVendorNames.Normalize(_endpointStore.Load().Vendor);
            return LoadAll(vendor);
        }

        public IReadOnlyList<AIPredictiveCollectBlock> LoadAll(string vendor)
        {
            vendor = AIPredictiveVendorNames.Normalize(vendor);
            var dir = Path.Combine(AppContext.BaseDirectory, "AIPredictive");
            var names = AIPredictiveVendorNames.CollectCsvFileNames(vendor);
            var files = names.Select(n => Path.Combine(dir, n)).ToArray();

            var blocks = new List<AIPredictiveCollectBlock>();
            foreach (var file in files)
            {
                if (!File.Exists(file))
                {
                    _logger.Error($"采集块 CSV 不存在（厂家={vendor}）: {file}", true);
                    continue;
                }

                blocks.AddRange(LoadFile(file));
            }

            // 统一覆盖为固定 CIP 端口，避免 CSV 内旧端口干扰
            blocks = blocks
                .Select(b => new AIPredictiveCollectBlock
                {
                    BlockId = b.BlockId,
                    Mode = b.Mode,
                    SymbolName = b.SymbolName,
                    Ip = b.Ip,
                    Port = AIPredictiveVendorNames.FixedCipPort,
                    PlatformType = b.PlatformType,
                    StartIndex = b.StartIndex,
                    ElementCount = b.ElementCount,
                    Points = b.Points,
                })
                .ToList();

            _logger.Info(
                $"AIPredictive 采集块加载完成：厂家={vendor}，文件命中={files.Count(File.Exists)}/{files.Length}，" +
                $"块数={blocks.Count}，点数={blocks.Sum(b => b.Points.Count)}",
                false);
            return blocks;
        }

        private static IEnumerable<AIPredictiveCollectBlock> LoadFile(string path)
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
                yield break;

            var headers = ParseCsvLine(headerLine);
            var map = headers
                .Select((h, i) => (h, i))
                .ToDictionary(x => x.h, x => x.i, StringComparer.Ordinal);

            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var cols = ParseCsvLine(line);
                string Get(string name) =>
                    map.TryGetValue(name, out var i) && i < cols.Count ? cols[i] : "";

                var modeText = Get("采集方式");
                if (!Enum.TryParse<AIPredictiveCollectMode>(modeText, ignoreCase: true, out var mode))
                    continue;

                var symbol = Get("符号名");
                if (string.IsNullOrWhiteSpace(symbol))
                    continue;

                _ = int.TryParse(Get("端口"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port);
                if (port <= 0)
                    port = 44818;

                _ = int.TryParse(Get("起始下标"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start);
                _ = int.TryParse(Get("建议读取长度_元素"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var len);
                if (len <= 0)
                    _ = int.TryParse(Get("跨度元素数"), NumberStyles.Integer, CultureInfo.InvariantCulture, out len);

                var platformType = Get("平台类型");
                var points = mode == AIPredictiveCollectMode.ArrayBatch
                    ? ParseArrayPoints(symbol, platformType, Get("成员或元素清单"))
                    : ParseStructPoints(symbol, Get("成员或元素清单"));

                yield return new AIPredictiveCollectBlock
                {
                    BlockId = Get("块ID"),
                    Mode = mode,
                    SymbolName = symbol,
                    Ip = Get("IP"),
                    Port = port,
                    PlatformType = platformType,
                    StartIndex = start,
                    ElementCount = len > 0 ? len : points.Count,
                    Points = points,
                };
            }
        }

        private static List<AIPredictivePointDef> ParseArrayPoints(string symbol, string platformType, string list)
        {
            var result = new List<AIPredictivePointDef>();
            if (string.IsNullOrWhiteSpace(list))
                return result;

            foreach (var item in list.Split(";;", StringSplitOptions.None))
            {
                if (string.IsNullOrWhiteSpace(item))
                    continue;
                var parts = item.Split('|');
                if (parts.Length < 2)
                    continue;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                    continue;
                var name = parts[1];
                if (string.IsNullOrWhiteSpace(name))
                    continue; // 缺失下标占位
                var process = parts.Length > 2 ? parts[2] : "";
                result.Add(new AIPredictivePointDef
                {
                    PointKey = $"{symbol}[{idx}]",
                    DisplayName = name,
                    PlatformType = platformType,
                    CipAddress = $"{symbol}[{idx}]",
                    ArrayIndex = idx,
                    Process = process,
                });
            }

            return result;
        }

        private static List<AIPredictivePointDef> ParseStructPoints(string root, string list)
        {
            var result = new List<AIPredictivePointDef>();
            if (string.IsNullOrWhiteSpace(list))
                return result;

            foreach (var item in list.Split(";;", StringSplitOptions.None))
            {
                if (string.IsNullOrWhiteSpace(item))
                    continue;
                var parts = item.Split('|');
                if (parts.Length < 4)
                    continue;
                var memberPath = parts[0];
                var platformType = parts[2];
                var name = parts[3];
                var process = parts.Length > 4 ? parts[4] : "";
                var address = $"{root}.{memberPath}";
                result.Add(new AIPredictivePointDef
                {
                    PointKey = address,
                    DisplayName = name,
                    PlatformType = platformType,
                    CipAddress = address,
                    Process = process,
                });
            }

            return result;
        }

        /// <summary>简易 RFC4180 行解析（支持引号字段）。</summary>
        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            result.Add(sb.ToString());
            return result;
        }
    }
}
