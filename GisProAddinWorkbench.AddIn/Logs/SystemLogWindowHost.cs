using System.Windows;

namespace GisProAddinWorkbench.AddIn.Logs;

internal static class SystemLogWindowHost
{
    private static SystemLogWindow? _window;

    internal static void ShowWindow()
    {
        if (_window is null || !_window.IsLoaded)
        {
            _window = new SystemLogWindow();
            _window.Closed += (_, _) => _window = null;
            _window.Show();
            return;
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }
}
