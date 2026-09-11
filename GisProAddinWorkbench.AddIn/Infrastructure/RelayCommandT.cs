using System.Windows.Input;

namespace GisProAddinWorkbench.AddIn.Infrastructure;

internal sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    internal RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (parameter is null)
        {
            return _canExecute?.Invoke(default) ?? true;
        }

        if (parameter is T typed)
        {
            return _canExecute?.Invoke(typed) ?? true;
        }

        return _canExecute?.Invoke(default) ?? true;
    }

    public void Execute(object? parameter)
    {
        if (parameter is T typed)
        {
            _execute(typed);
            return;
        }

        _execute(default);
    }

    internal void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
