using System.Windows.Input;

namespace SUNWODA_SEVB.Core.Common.Commands
{
    /// <summary>
    /// 基础命令实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action execute, Predicate<object?>? canExecute = null)
        {
            _execute = (parameter) => execute?.Invoke();
            _canExecute = canExecute;
        }

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            ArgumentNullException.ThrowIfNull(execute);
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);
    }

    /// <summary>异步 ICommand 实现，执行中自动禁用按钮防重入。</summary>
    public sealed class RelayCommandAsync : ICommand
    {
        private readonly Func<Task> _execute;
        private bool _isExecuting;

        public RelayCommandAsync(Func<Task> execute) => _execute = execute;

        public bool CanExecute(object? parameter) => !_isExecuting;

        public event EventHandler? CanExecuteChanged;

        public async void Execute(object? parameter)
        {
            if (_isExecuting) return;
            _isExecuting = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                await _execute();
            }
            catch (Exception ex)
            {
                // async void 未捕获异常会变成进程级未处理异常（闪退）；调用方应自行 catch，此处作最后防线
                System.Diagnostics.Debug.WriteLine(
                    $"[RelayCommandAsync] 未处理异常: {ex}");
                System.Diagnostics.Trace.TraceError(
                    $"[RelayCommandAsync] 未处理异常: {ex}");
            }
            finally
            {
                _isExecuting = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// 泛型命令实现
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;

        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            if (parameter is null)
            {
                return typeof(T).IsClass || Nullable.GetUnderlyingType(typeof(T)) != null;
            }
            return parameter is T t && (_canExecute?.Invoke(t) ?? true);
        }

        public void Execute(object? parameter)
        {
            if (parameter is T t || (parameter is null && (typeof(T).IsClass || Nullable.GetUnderlyingType(typeof(T)) != null)))
            {
                _execute((T?)parameter);
            }
        }
    }
}