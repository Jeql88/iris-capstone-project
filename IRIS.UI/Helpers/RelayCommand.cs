using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using IRIS.UI.Views.Dialogs;

namespace IRIS.UI.Helpers
{
    internal static class RelayCommandErrorReporter
    {
        public static void Report(Exception ex)
        {
            Debug.WriteLine($"[RelayCommand] Unhandled exception: {ex}");
            try
            {
                var dlg = new ConfirmationDialog(
                    "Unexpected Error",
                    ex.Message,
                    "ErrorCircle24",
                    "OK",
                    "Cancel",
                    false)
                {
                    Owner = Application.Current?.MainWindow
                };
                dlg.ShowDialog();
            }
            catch (Exception dialogEx)
            {
                Debug.WriteLine($"[RelayCommand] Failed to show error dialog: {dialogEx}");
            }
        }
    }

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

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute();

        public async void Execute(object? parameter)
        {
            try
            {
                if (_executeAsync != null)
                    await _executeAsync();
                else if (_executeAsyncWithParam != null)
                    await _executeAsyncWithParam(parameter);
                else
                    _executeSync?.Invoke();
            }
            catch (Exception ex)
            {
                RelayCommandErrorReporter.Report(ex);
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
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

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) =>
            _canExecute == null || _canExecute((T?)parameter);

        public void Execute(object? parameter)
        {
            try
            {
                _execute((T?)parameter);
            }
            catch (Exception ex)
            {
                RelayCommandErrorReporter.Report(ex);
            }
        }

        public void RaiseCanExecuteChanged() =>
            CommandManager.InvalidateRequerySuggested();
    }
}
