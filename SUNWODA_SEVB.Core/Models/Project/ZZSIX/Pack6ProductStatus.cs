namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>对齐旧 PackProductDetectionStatus（线程安全）。</summary>
    public sealed class Pack6ProductStatus
    {
        private readonly object _lock = new();

        public string ProductCode { get; }
        public Dictionary<string, Pack6QueueStatus> Queues { get; }
        public DateTime CreateTime { get; }
        public List<string> Defects { get; }
        public bool IsCompleted { get; set; }

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

        public void AddDefect(string defect)
        {
            if (string.IsNullOrWhiteSpace(defect))
                return;
            lock (_lock)
            {
                Defects.Add(defect);
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
