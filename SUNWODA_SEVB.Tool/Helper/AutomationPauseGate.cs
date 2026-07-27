using System.Threading;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// CT 自动化协作式暂停门闩：暂停时阻塞 Wait/Delay 轮询，取消令牌可打断等待。
    /// </summary>
    public sealed class AutomationPauseGate
    {
        private readonly ManualResetEventSlim _gate = new(initialState: true);
        private int _paused;

        public bool IsPaused => Volatile.Read(ref _paused) != 0;

        public event EventHandler? PauseStateChanged;

        /// <summary>
        /// 切换暂停/继续。返回切换后是否处于暂停。
        /// </summary>
        public bool Toggle()
        {
            if (IsPaused)
            {
                Resume();
                return false;
            }

            Pause();
            return true;
        }

        public void Pause()
        {
            if (Interlocked.Exchange(ref _paused, 1) == 1)
            {
                return;
            }

            _gate.Reset();
            PauseStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Resume()
        {
            if (Interlocked.Exchange(ref _paused, 0) == 0)
            {
                return;
            }

            _gate.Set();
            PauseStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>流程结束或取消时调用，确保等待方被放行并清零暂停态。</summary>
        public void Reset()
        {
            Volatile.Write(ref _paused, 0);
            if (!_gate.IsSet)
            {
                _gate.Set();
            }

            PauseStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task WaitIfPausedAsync(CancellationToken cancellationToken)
        {
            if (!IsPaused)
            {
                return Task.CompletedTask;
            }

            return WaitWhilePausedCoreAsync(cancellationToken);
        }

        private async Task WaitWhilePausedCoreAsync(CancellationToken cancellationToken)
        {
            while (IsPaused)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // 分段等待，便于及时响应取消
                    await Task.Run(() => _gate.Wait(200, cancellationToken), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }
        }
    }
}
