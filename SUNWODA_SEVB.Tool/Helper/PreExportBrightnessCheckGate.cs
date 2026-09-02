using System.Threading;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 切片导出前亮度/视图人工校验门闩。
    /// 与 <see cref="AutomationPauseGate"/>（Alt+P）相互独立：本门闩只阻塞导出前等待，
    /// 不改变协作式暂停态，也不与 Alt+P 的继续语义共用。
    /// </summary>
    public sealed class PreExportBrightnessCheckGate
    {
        private readonly object _sync = new();
        private TaskCompletionSource<bool>? _tcs;
        private int _awaiting;

        public bool IsAwaiting => Volatile.Read(ref _awaiting) != 0;

        public event EventHandler? StateChanged;

        /// <summary>开始一轮导出前校验等待（会取消上一轮未完成的等待）。</summary>
        public void Begin()
        {
            TaskCompletionSource<bool>? old;
            lock (_sync)
            {
                old = _tcs;
                _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Volatile.Write(ref _awaiting, 1);
            }

            old?.TrySetCanceled();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>操作员点击「校验完毕」后调用，放行导出。</summary>
        public void SignalCompleted()
        {
            TaskCompletionSource<bool>? tcs;
            lock (_sync)
            {
                tcs = _tcs;
            }

            tcs?.TrySetResult(true);
        }

        /// <summary>流程取消或结束时清出门闩。</summary>
        public void Reset()
        {
            TaskCompletionSource<bool>? tcs;
            lock (_sync)
            {
                tcs = _tcs;
                _tcs = null;
                Volatile.Write(ref _awaiting, 0);
            }

            tcs?.TrySetCanceled();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 等待操作员确认。调用方取消令牌触发时抛出 <see cref="OperationCanceledException"/>。
        /// </summary>
        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool>? tcs;
            lock (_sync)
            {
                tcs = _tcs;
            }

            if (tcs == null)
            {
                return;
            }

            try
            {
                await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_tcs, tcs))
                    {
                        _tcs = null;
                    }

                    Volatile.Write(ref _awaiting, 0);
                }

                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
