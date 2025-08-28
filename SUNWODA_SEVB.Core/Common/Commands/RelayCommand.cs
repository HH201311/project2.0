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