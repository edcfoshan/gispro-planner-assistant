using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Settings;

public partial class GlobalSettingsWindow : Window
{
    private readonly GlobalSettingsWindowViewModel _viewModel;

    public GlobalSettingsWindow()
    {
        InitializeComponent();
        try
        {
            UiDensityService.Initialize();
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(GlobalSettingsWindow), "初始化界面密度失败。", exception);
        }

        _viewModel = new GlobalSettingsWindowViewModel(Close);
        DataContext = _viewModel;
        Loaded += OnLoadedAsync;
        Closing += OnClosing;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(GlobalSettingsWindow), "加载设置窗口失败。", exception);
            MessageBox.Show(
                $"加载设置窗口失败：{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "设置",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.RestoreSavedAppearanceIfNeeded();
        _viewModel.LockAdmin();
        AdminPasswordBox.Password = string.Empty;
    }

    private void AdminPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
    }

    private void AdminUnlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsAdminUnlocked)
        {
            _viewModel.LockAdmin();
            AdminPasswordBox.Password = string.Empty;
            return;
        }

        if (_viewModel.TryUnlockAdmin(AdminPasswordBox.Password))
        {
            AdminPasswordBox.Password = string.Empty;
            return;
        }

        AdminPasswordBox.Focus();
    }
}
