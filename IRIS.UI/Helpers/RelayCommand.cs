using System.Windows.Input;

namespace IRIS.UI.Helpers
{
    public class RelayCommand : ICommand
    {
        private readonly Func<Task>? _executeAsync;
        private readonly Func<object?, Task>? _executeAsyncWithParam;
        private readonly Action? _executeSync;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Func<Task> execute, Func<bool> canExecute)
        {
            _executeAsync = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Func<object?, Task> execute, Func<bool> canExecute)
        {
            _executeAsyncWithParam = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            _executeSync = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute();

        public async void Execute(object? parameter)
        {
            if (_executeAsync != null)
                await _executeAsync();
            else if (_executeAsyncWithParam != null)
                await _executeAsyncWithParam(parameter);
            else
                _executeSync?.Invoke();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) =>
            _canExecute == null || _canExecute((T?)parameter);

        public void Execute(object? parameter) =>
            _execute((T?)parameter);

        public void RaiseCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
