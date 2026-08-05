using SUNWODA_SEVB.Core.Models.Project.CTOL;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using SUNWODA_SEVB.Services.Project.CTOL.Processing;

namespace SUNWODA_SEVB.Communication.CTOL
{
    /// <summary>
    /// 一个常驻的 C01OnlineCT.exe 算法子进程的客户端封装（单进程版）。
    ///
    /// 职责：
    ///   - 以 server 模式启动 C01OnlineCT.exe（模型只在该子进程内加载一次）；
    ///   - 通过 stdin 发送图片处理请求，通过 stdout 接收结果（一行一条 JSON）；
    ///   - 用 RequestId 把异步返回的结果与请求对应起来；
    ///   - 子进程崩溃时，下一次提交请求会自动重启并重新加载模型。
    ///
    /// 与 <see cref="ImageProcessorBase"/> 的关系：每个处理器实例独占一个本客户端，
    /// 即“一个工位 = 一个 C01OnlineCT 子进程”，工位之间天然隔离、互不影响。
    /// </summary>
    public sealed class CtSubprocessClient : IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Worker 端 ready 握手消息的特殊 RequestId。
        /// </summary>
        private const string ReadyRequestId = "__READY__";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions();

        private readonly string _workerExePath;
        private readonly int _stationId;
        private readonly TimeSpan _requestTimeout;
        private readonly Action<string>? _logInfo;
        private readonly Action<string>? _logError;

        private readonly object _stateLock = new object();
        private readonly SemaphoreSlim _startLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        private readonly ConcurrentDictionary<string, TaskCompletionSource<CtAlgorithmResult>> _pending
            = new ConcurrentDictionary<string, TaskCompletionSource<CtAlgorithmResult>>();

        private Process? _process;
        private StreamWriter? _stdin;
        private CancellationTokenSource? _readerCts;
        private TaskCompletionSource<bool>? _readyTcs;
        private bool _disposed;

        /// <summary>
        /// 本客户端绑定的工位编号（仅用于日志与工位校验）。
        /// </summary>
        public int StationId => _stationId;

        /// <summary>
        /// 子进程是否处于运行状态。
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_stateLock)
                {
                    return _process is { HasExited: false };
                }
            }
        }

        /// <param name="workerExePath">C01OnlineCT.exe 完整路径。</param>
        /// <param name="stationId">绑定的工位编号；&lt;0 表示不做工位校验。</param>
        /// <param name="requestTimeout">单张图片处理超时时间，默认 2 分钟。</param>
        /// <param name="logInfo">普通日志回调（可空）。</param>
        /// <param name="logError">错误日志回调（可空）。</param>
        public CtSubprocessClient(
            string workerExePath,
            int stationId = 0,
            TimeSpan? requestTimeout = null,
            Action<string>? logInfo = null,
            Action<string>? logError = null)
        {
            _workerExePath = workerExePath;
            _stationId = stationId;
            _requestTimeout = requestTimeout ?? TimeSpan.FromMinutes(2);
            _logInfo = logInfo;
            _logError = logError;
        }

        /// <summary>
        /// 启动子进程并等待其完成模型加载（ready 握手）。可提前调用以预热。
        /// 已在运行则直接返回。
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                return;
            }

            await _startLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsRunning)
                {
                    return;
                }

                TaskCompletionSource<bool> readyTcs;

                lock (_stateLock)
                {
                    if (!File.Exists(_workerExePath))
                    {
                        throw new FileNotFoundException($"找不到 C01OnlineCT 子进程可执行文件：{_workerExePath}");
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = _workerExePath,
                        WorkingDirectory = Path.GetDirectoryName(_workerExePath)!,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    };
                    psi.ArgumentList.Add("--server");
                    psi.ArgumentList.Add("--station");
                    psi.ArgumentList.Add(_stationId.ToString());

                    var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                    process.Exited += OnProcessExited;

                    readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _readyTcs = readyTcs;

                    process.Start();

                    _process = process;
                    _stdin = process.StandardInput;
                    _stdin.AutoFlush = false;

                    _readerCts = new CancellationTokenSource();
                    _ = Task.Run(() => ReadStdoutLoopAsync(process, _readerCts.Token));
                    _ = Task.Run(() => ReadStderrLoopAsync(process, _readerCts.Token));
                }

                _logInfo?.Invoke($"[CT工位 {_stationId}] 子进程已启动 (PID={_process!.Id})，等待模型加载...");

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var readyTask = readyTcs.Task;
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5), linked.Token);

                var completed = await Task.WhenAny(readyTask, timeoutTask).ConfigureAwait(false);
                if (completed != readyTask)
                {
                    throw new TimeoutException($"[CT工位 {_stationId}] 子进程模型加载超时。");
                }

                linked.Cancel();
                await readyTask.ConfigureAwait(false); // 若失败会抛异常
                _logInfo?.Invoke($"[CT工位 {_stationId}] 模型加载完成，子进程就绪。");
            }
            finally
            {
                _startLock.Release();
            }
        }

        /// <summary>
        /// 提交一张图片进行处理，返回算法结果。
        /// 若子进程未运行会自动尝试启动（重新加载模型）。
        /// </summary>
        public async Task<CtAlgorithmResult> ProcessImageAsync(
            string imagePath,
            string outputDir,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!IsRunning)
            {
                await StartAsync(cancellationToken).ConfigureAwait(false);
            }

            string requestId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<CtAlgorithmResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[requestId] = tcs;

            var request = new CtWorkerRequest
            {
                RequestId = requestId,
                StationId = _stationId,
                ImagePath = imagePath,
                OutputDir = outputDir,
            };

            try
            {
                await WriteRequestAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _pending.TryRemove(requestId, out _);
                return Failure(requestId, imagePath, outputDir, $"发送请求失败：{ex.Message}");
            }

            using var timeoutCts = new CancellationTokenSource(_requestTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using (linked.Token.Register(() =>
            {
                if (_pending.TryRemove(requestId, out var pending))
                {
                    pending.TrySetResult(Failure(requestId, imagePath, outputDir,
                        cancellationToken.IsCancellationRequested ? "请求被取消。" : "请求处理超时。"));
                }
            }))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }

        /// <summary>向子进程发送退出命令并等待其结束。</summary>
        public async Task StopAsync()
        {
            Process? process;
            lock (_stateLock)
            {
                process = _process;
            }

            if (process == null || process.HasExited)
            {
                return;
            }

            try
            {
                await WriteRequestAsync(new CtWorkerRequest { Command = "exit" }, CancellationToken.None)
                    .ConfigureAwait(false);

                if (!process.WaitForExit(5000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                try { process.Kill(entireProcessTree: true); } catch { /* 忽略 */ }
            }
        }

        // -----------------------------------------------------------------
        // 内部实现
        // -----------------------------------------------------------------

        private async Task WriteRequestAsync(CtWorkerRequest request, CancellationToken cancellationToken)
        {
            StreamWriter? stdin;
            lock (_stateLock)
            {
                stdin = _stdin;
            }

            if (stdin == null)
            {
                throw new InvalidOperationException("子进程未启动。");
            }

            string json = JsonSerializer.Serialize(request, JsonOptions);

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await stdin.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
                await stdin.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task ReadStdoutLoopAsync(Process process, CancellationToken token)
        {
            try
            {
                using var reader = process.StandardOutput;
                string? line;
                while (!token.IsCancellationRequested &&
                       (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    line = line.Trim();
                    // 协议消息一定以 '{' 开头；其它输出视为日志忽略（避免底层 DLL 打印污染）。
                    if (line.Length == 0 || line[0] != '{')
                    {
                        if (line.Length > 0)
                        {
                            _logInfo?.Invoke($"[CT工位 {_stationId} stdout] {line}");
                        }
                        continue;
                    }

                    CtAlgorithmResult? result;
                    try
                    {
                        result = JsonSerializer.Deserialize<CtAlgorithmResult>(line, JsonOptions);
                    }
                    catch (Exception ex)
                    {
                        _logError?.Invoke($"[CT工位 {_stationId}] 结果解析失败：{ex.Message}; 原文={line}");
                        continue;
                    }

                    if (result == null)
                    {
                        continue;
                    }

                    if (result.RequestId == ReadyRequestId)
                    {
                        if (result.StationId >= 0 && _stationId >= 0 && result.StationId != _stationId)
                        {
                            _readyTcs?.TrySetException(new InvalidOperationException(
                                $"工位 {_stationId} 接收到工位 {result.StationId} 的握手消息，工位绑定错误。"));
                            continue;
                        }

                        if (result.Success)
                        {
                            _readyTcs?.TrySetResult(true);
                        }
                        else
                        {
                            _readyTcs?.TrySetException(
                                new InvalidOperationException($"工位 {_stationId} 模型加载失败：{result.ErrorReport}"));
                        }
                        continue;
                    }

                    // 工位硬隔离校验：结果必须来自本工位绑定的进程。
                    if (result.StationId >= 0 && _stationId >= 0 && result.StationId != _stationId)
                    {
                        _logError?.Invoke(
                            $"[CT工位 {_stationId}] 丢弃来自工位 {result.StationId} 的结果（工位不匹配）。");

                        if (_pending.TryRemove(result.RequestId, out var mismatched))
                        {
                            mismatched.TrySetResult(Failure(result.RequestId, result.ImagePath, result.OutputDir,
                                $"工位不匹配：期望工位 {_stationId}，实际收到工位 {result.StationId} 的结果。"));
                        }
                        continue;
                    }

                    if (_pending.TryRemove(result.RequestId, out var tcs))
                    {
                        tcs.TrySetResult(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logError?.Invoke($"[CT工位 {_stationId}] stdout 读取循环异常：{ex.Message}");
            }
        }

        private async Task ReadStderrLoopAsync(Process process, CancellationToken token)
        {
            try
            {
                using var reader = process.StandardError;
                string? line;
                while (!token.IsCancellationRequested &&
                       (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    if (line.Length > 0)
                    {
                        _logInfo?.Invoke($"[CT工位 {_stationId}] {line}");
                    }
                }
            }
            catch
            {
                // 进程退出时读取会中断，忽略。
            }
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            int exitCode = -1;
            try { exitCode = _process?.ExitCode ?? -1; } catch { /* 忽略 */ }

            _logError?.Invoke($"[CT工位 {_stationId}] 子进程已退出 (ExitCode={exitCode})。");

            _readyTcs?.TrySetException(
                new InvalidOperationException($"工位 {_stationId} 子进程在就绪前退出 (ExitCode={exitCode})。"));

            // 让所有在途请求失败，避免调用方永久等待。
            foreach (var kv in _pending.ToArray())
            {
                if (_pending.TryRemove(kv.Key, out var tcs))
                {
                    tcs.TrySetResult(Failure(kv.Key, "", "",
                        $"子进程异常退出 (ExitCode={exitCode})，请求未完成。"));
                }
            }

            lock (_stateLock)
            {
                _readerCts?.Cancel();
                _stdin = null;
            }
        }

        private CtAlgorithmResult Failure(string requestId, string imagePath, string outputDir, string error)
            => new CtAlgorithmResult
            {
                RequestId = requestId,
                StationId = _stationId,
                Success = false,
                ImagePath = imagePath,
                OutputDir = outputDir,
                ErrorReport = error,
            };

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // 忽略收尾异常
            }

            CleanupResources();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            try
            {
                Process? process;
                lock (_stateLock)
                {
                    process = _process;
                }
                if (process is { HasExited: false })
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // 忽略
            }

            CleanupResources();
        }

        private void CleanupResources()
        {
            lock (_stateLock)
            {
                try { _readerCts?.Cancel(); } catch { /* 忽略 */ }
                try { _readerCts?.Dispose(); } catch { /* 忽略 */ }
                try { _process?.Dispose(); } catch { /* 忽略 */ }
                _process = null;
                _stdin = null;
                _readerCts = null;
            }

            _startLock.Dispose();
            _writeLock.Dispose();
        }
    }
}
