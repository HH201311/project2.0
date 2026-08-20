using System;
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
        /// <summary>
        /// 嵌套单测等场景：临时忽略暂停门闩（主流程仍阻塞在 WaitWhilePausedCore 的 while 内）。
        /// </summary>
        private int _ignorePauseForNested;

        public bool IsPaused => Volatile.Read(ref _paused) != 0;

        public event EventHandler? PauseStateChanged;

        /// <summary>
        /// 在暂停态下执行嵌套工作（如阶段单测）时调用：WaitIfPausedAsync 立即放行，
        /// 但不 Resume，主流程仍停在暂停循环中。
        /// </summary>
        public IDisposable BeginNestedWorkIgnoringPause()
        {
            Interlocked.Increment(ref _ignorePauseForNested);
            return new NestedPauseIgnoreScope(this);
        }

        private sealed class NestedPauseIgnoreScope : IDisposable
        {
            private AutomationPauseGate? _owner;

            public NestedPauseIgnoreScope(AutomationPauseGate owner) => _owner = owner;

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                if (owner != null)
                {
                    Interlocked.Decrement(ref owner._ignorePauseForNested);
                }
            }
        }

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
            if (!IsPaused || Volatile.Read(ref _ignorePauseForNested) > 0)
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
