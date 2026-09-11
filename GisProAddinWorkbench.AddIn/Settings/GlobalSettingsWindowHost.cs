using System.Windows;

namespace GisProAddinWorkbench.AddIn.Settings;

internal static class GlobalSettingsWindowHost
{
    private static GlobalSettingsWindow? _window;

    internal static void ShowWindow()
    {
        if (_window is null || !_window.IsLoaded)
        {
            _window = new GlobalSettingsWindow();
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
