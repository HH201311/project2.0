using System.Collections.Concurrent;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    public sealed class Pack6ProductAggregator : IZZSIXPack6ProductAggregator
    {
        private readonly ConcurrentDictionary<string, Pack6ProductStatus> _products =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, byte> _closed =
            new(StringComparer.OrdinalIgnoreCase);

        public event Action<Pack6ProductResult>? ProductCompleted;

        public Pack6ProductResult? ApplyFrame(Pack6FrameResult frame, TimeSpan packTimeout)
        {
            _ = packTimeout; // 缺张超时改由管线按 FirstSeen 驱动，避免与保序提交竞态
            var code = Normalize(frame.ProductCode);
            if (_closed.ContainsKey(code))
                return null;

            var status = _products.GetOrAdd(
                code,
                c => new Pack6ProductStatus(c, Pack6DefaultConfig.QueueRequirements));

            if (status.IsCompleted)
                return null;

            status.IncrementProcessed(frame.QueueName);
            var camName = frame.CameraName;
            if (string.IsNullOrWhiteSpace(camName) &&
                Pack6CameraCatalog.ByQueueName.TryGetValue(frame.QueueName, out var def))
                camName = def.CameraName;
            status.RememberShot(camName, frame.ShotIndex);
            if (frame.OcrMode != Pack6OcrMode.None)
            {
                status.NoteThreeCodeFrame(frame.OcrMode, frame.IsOk);
                status.RememberOcr(frame.OcrMode, frame.ProductSN, frame.QrData);
            }
            foreach (var d in frame.Defects)
                status.AddDefect(d);

            if (status.CheckAllQueuesCompleted())
                return Complete(code, status, timedOut: false);

            return null;
        }

        public Pack6ProductResult? ForceComplete(string productCode, bool timedOut, string? defectReason)
        {
            var code = Normalize(productCode);
            if (_closed.ContainsKey(code))
                return null;

            var status = _products.GetOrAdd(
                code,
                c => new Pack6ProductStatus(c, Pack6DefaultConfig.QueueRequirements));
            if (status.IsCompleted)
                return null;

            var missing = status.DescribeMissingShots();
            var detail = string.IsNullOrWhiteSpace(defectReason)
                ? $"缺张超时；{missing}"
                : $"{defectReason}；{missing}";
            status.AddDefect(detail);

            return Complete(code, status, timedOut);
        }

        public bool IsClosed(string productCode) =>
            _closed.ContainsKey(Normalize(productCode));

        public void Reset()
        {
            _products.Clear();
            _closed.Clear();
        }

        public IReadOnlyCollection<Pack6ProductStatus> GetInFlight() =>
            _products.Values.Where(p => !p.IsCompleted).ToList();

        private Pack6ProductResult Complete(string code, Pack6ProductStatus status, bool timedOut)
        {
            status.IsCompleted = true;
            _closed[code] = 0;
            _products.TryRemove(code, out _);
            var result = new Pack6ProductResult
            {
                ProductCode = code,
                IsOk = !timedOut && status.Defects.Count == 0,
                TimedOut = timedOut,
                Defects = status.Defects.ToList(),
                CompletedAt = DateTime.Now,
                ThreeCodeChecked = status.HasAnyThreeCodeOcr,
                ThreeCodeOk = status.IsThreeCodeSetOk
            };
            ProductCompleted?.Invoke(result);
            return result;
        }

        private static string Normalize(string? code) =>
            string.IsNullOrWhiteSpace(code) ? "UNKNOWN" : code.Trim().ToUpperInvariant();
    }
}
