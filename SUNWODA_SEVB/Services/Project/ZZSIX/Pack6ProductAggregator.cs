using System.Collections.Concurrent;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    public sealed class Pack6ProductAggregator : IZZSIXPack6ProductAggregator
    {
        private readonly ConcurrentDictionary<string, Pack6ProductStatus> _products =
            new(StringComparer.OrdinalIgnoreCase);

        public event Action<Pack6ProductResult>? ProductCompleted;

        public Pack6ProductResult? ApplyFrame(Pack6FrameResult frame, TimeSpan packTimeout)
        {
            var code = string.IsNullOrWhiteSpace(frame.ProductCode) ? "UNKNOWN" : frame.ProductCode;
            var status = _products.GetOrAdd(
                code,
                c => new Pack6ProductStatus(c, Pack6DefaultConfig.QueueRequirements));

            status.IncrementProcessed(frame.QueueName);
            foreach (var d in frame.Defects)
                status.AddDefect(d);

            // 顺带扫描超时产品
            foreach (var kv in _products.ToArray())
            {
                if (kv.Value.IsCompleted)
                    continue;
                if (!kv.Value.CheckTimeout(packTimeout))
                    continue;
                Complete(kv.Key, kv.Value, timedOut: true);
            }

            if (status.IsCompleted)
                return null;

            if (status.CheckAllQueuesCompleted())
                return Complete(code, status, timedOut: false);

            return null;
        }

        public void Reset() => _products.Clear();

        public IReadOnlyCollection<Pack6ProductStatus> GetInFlight() =>
            _products.Values.Where(p => !p.IsCompleted).ToList();

        private Pack6ProductResult Complete(string code, Pack6ProductStatus status, bool timedOut)
        {
            status.IsCompleted = true;
            _products.TryRemove(code, out _);
            var result = new Pack6ProductResult
            {
                ProductCode = code,
                IsOk = !timedOut && status.Defects.Count == 0,
                TimedOut = timedOut,
                Defects = status.Defects.ToList(),
                CompletedAt = DateTime.Now
            };
            ProductCompleted?.Invoke(result);
            return result;
        }
    }
}
