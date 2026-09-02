using System.Threading;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>人工干预等待结果。</summary>
    public enum ManualInterventionWaitResult
    {
        /// <summary>操作员点击「放入异常文件夹并跳过」。</summary>
        CompletedByOperator = 0,

        /// <summary>等待超时（默认 10 分钟）。</summary>
        TimedOut = 1,

        /// <summary>操作员点击「人工处理完成」，人工 321 已做完，跳过视觉校验继续后续步骤。</summary>
        Manual321Completed = 2
    }

    /// <summary>
    /// 流程异常/超时时的人工干预门闩：暂停自动化等待操作员确认，或超时后放行。
    /// </summary>
    public sealed class ManualInterventionGate
    {
        private readonly object _sync = new();
        private TaskCompletionSource<ManualInterventionWaitResult>? _tcs;
        private int _awaiting;

        public bool IsAwaiting => Volatile.Read(ref _awaiting) != 0;

        public event EventHandler? StateChanged;

        /// <summary>开始一轮人工干预等待（会取消上一轮未完成的等待）。</summary>
        public void Begin()
        {
            TaskCompletionSource<ManualInterventionWaitResult>? old;
            lock (_sync)
            {
                old = _tcs;
                _tcs = new TaskCompletionSource<ManualInterventionWaitResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                Volatile.Write(ref _awaiting, 1);
            }

            old?.TrySetCanceled();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>操作员确认「放入异常文件夹并跳过」。</summary>
        public void SignalCompleted()
        {
            SignalResult(ManualInterventionWaitResult.CompletedByOperator);
        }

        /// <summary>操作员确认「人工处理完成」：人工 321 已做完，跳过视觉校验。</summary>
        public void SignalManual321Completed()
        {
            SignalResult(ManualInterventionWaitResult.Manual321Completed);
        }

        /// <summary>兼容旧调用名。</summary>
        public void SignalRetry() => SignalManual321Completed();

        private void SignalResult(ManualInterventionWaitResult result)
        {
            TaskCompletionSource<ManualInterventionWaitResult>? tcs;
            lock (_sync)
            {
                tcs = _tcs;
            }

            tcs?.TrySetResult(result);
        }

        /// <summary>流程取消或结束时清出门闩。</summary>
        public void Reset()
        {
            TaskCompletionSource<ManualInterventionWaitResult>? tcs;
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
        /// 等待操作员确认或超时。调用方取消令牌触发时抛出 <see cref="OperationCanceledException"/>。
        /// </summary>
        public async Task<ManualInterventionWaitResult> WaitAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            TaskCompletionSource<ManualInterventionWaitResult>? tcs;
            lock (_sync)
            {
                tcs = _tcs;
            }

            if (tcs == null)
            {
                return ManualInterventionWaitResult.TimedOut;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout > TimeSpan.Zero)
            {
                timeoutCts.CancelAfter(timeout);
            }

            try
            {
                return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return ManualInterventionWaitResult.TimedOut;
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
