namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>对齐旧 PackProductDetectionStatus（线程安全）。</summary>
    public sealed class Pack6ProductStatus
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, HashSet<int>> _receivedShots =
            new(StringComparer.Ordinal);

        public string ProductCode { get; }
        public Dictionary<string, Pack6QueueStatus> Queues { get; }
        public DateTime CreateTime { get; }
        public List<string> Defects { get; }
        public bool IsCompleted { get; set; }
        public string? WhiteLabelProductSn { get; private set; }
        public string? NameplateProductSn { get; private set; }
        /// <summary>白标签图内二维码，供与产品码/文件夹/铭牌一致校验。</summary>
        public string? WhiteLabelQrData { get; private set; }

        public Pack6ProductStatus(string code, IReadOnlyDictionary<string, int> queueRequirements)
        {
            ProductCode = code;
            Queues = queueRequirements.ToDictionary(
                kv => kv.Key,
                kv => new Pack6QueueStatus(kv.Value),
                StringComparer.OrdinalIgnoreCase);
            CreateTime = DateTime.UtcNow;
            Defects = [];
        }

        public void IncrementProcessed(string queueName)
        {
            lock (_lock)
            {
                if (Queues.TryGetValue(queueName, out var queue))
                {
                    queue.Processed++;
                    queue.LastUpdated = DateTime.UtcNow;
                }
            }
        }

        /// <summary>记录已收到的相机张序，供缺张超时时列出具体缺哪张。</summary>
        public void RememberShot(string? cameraName, int shotIndex)
        {
            if (string.IsNullOrWhiteSpace(cameraName) || shotIndex <= 0)
                return;
            lock (_lock)
            {
                if (!_receivedShots.TryGetValue(cameraName, out var set))
                {
                    set = [];
                    _receivedShots[cameraName] = set;
                }
                set.Add(shotIndex);
            }
        }

        /// <summary>
        /// 相对设计张数列出未收到的「相机-张序」，如 前-3,底1-2。
        /// 无张序记录时退化为「相机名缺N张」。
        /// </summary>
        public string DescribeMissingShots()
        {
            lock (_lock)
            {
                var missing = new List<string>();
                foreach (var cam in Pack6CameraCatalog.Cameras)
                {
                    _receivedShots.TryGetValue(cam.CameraName, out var got);
                    got ??= [];
                    for (var i = 1; i <= cam.DesignShotCount; i++)
                    {
                        if (!got.Contains(i))
                            missing.Add($"{cam.CameraName}-{i}");
                    }
                }

                if (missing.Count > 0)
                    return "缺张:" + string.Join(',', missing);

                var byQueue = new List<string>();
                foreach (var cam in Pack6CameraCatalog.Cameras)
                {
                    if (!Queues.TryGetValue(cam.QueueName, out var q))
                        continue;
                    var lack = q.Required - q.Processed;
                    if (lack > 0)
                        byQueue.Add($"{cam.DisplayTitle}缺{lack}张(需{q.Required}实{q.Processed})");
                }

                return byQueue.Count > 0
                    ? "缺张:" + string.Join('；', byQueue)
                    : "缺张:未齐套(明细未知)";
            }
        }

        public void AddDefect(string defect)
        {
            if (string.IsNullOrWhiteSpace(defect))
                return;
            lock (_lock)
            {
                Defects.Add(defect);
            }
        }

        public void RememberOcr(Pack6OcrMode mode, string productSn, string? qrData = null)
        {
            if (string.IsNullOrWhiteSpace(productSn))
                return;
            lock (_lock)
            {
                if (mode == Pack6OcrMode.WhiteLabelThreeCode)
                {
                    WhiteLabelProductSn = productSn.Trim().ToUpperInvariant();
                    if (!string.IsNullOrWhiteSpace(qrData))
                        WhiteLabelQrData = qrData.Trim().ToUpperInvariant();
                }
                else if (mode == Pack6OcrMode.NameplateProductSN)
                {
                    NameplateProductSn = productSn.Trim().ToUpperInvariant();
                }
            }
        }

        public bool CheckAllQueuesCompleted()
        {
            lock (_lock)
            {
                return Queues.Values.All(q => q.Processed >= q.Required);
            }
        }

        public bool CheckTimeout(TimeSpan timeout)
        {
            lock (_lock)
            {
                return Queues.Values.Any(q =>
                    q.Processed < q.Required &&
                    (DateTime.UtcNow - q.LastUpdated) > timeout);
            }
        }
    }

    public sealed class Pack6QueueStatus
    {
        public int Required { get; }
        public int Processed { get; set; }
        public DateTime LastUpdated { get; set; }

        public Pack6QueueStatus(int required)
        {
            Required = required;
            LastUpdated = DateTime.UtcNow;
        }
    }
}
