using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.PCD
{
    public class LatestFrameChannel : IDisposable
    {
        private Mat? _latest;
        private readonly SemaphoreSlim _signal = new(0, 1);
        private readonly object _lock = new();
        private bool _disposed;

        public void Publish(Mat frame)
        {
            if (_disposed || frame == null || frame.Empty()) return;

            Mat? toDispose = null;
            lock (_lock)
            {
                toDispose = _latest;
                _latest = frame.Clone();
            }
            toDispose?.Dispose();

            if (_signal.CurrentCount == 0)
            {
                try { _signal.Release(); } catch (SemaphoreFullException) { }
            }
        }

        public async Task<Mat?> WaitAsync(CancellationToken token)
        {
            try { await _signal.WaitAsync(token); }
            catch (OperationCanceledException) { return null; }

            lock (_lock)
            {
                var m = _latest;
                _latest = null;
                return m;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_lock)
            {
                _latest?.Dispose();
                _latest = null;
            }
            _signal.Dispose();
        }
    }
}
