using System.Windows.Input;

namespace GisProAddinWorkbench.AddIn.Infrastructure;

internal sealed class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _executeAsync;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isRunning;

    internal AsyncRelayCommand(Func<T?, Task> executeAsync, Func<T?, bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (_isRunning)
        {
            return false;
        }

        if (parameter is T typed)
        {
            return _canExecute?.Invoke(typed) ?? true;
        }

        return _canExecute?.Invoke(default) ?? true;
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isRunning = true;
        RaiseCanExecuteChanged();

        try
        {
            await _executeAsync(parameter is T typed ? typed : default);
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    internal void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
