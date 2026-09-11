using System.Windows;

namespace GisProAddinWorkbench.AddIn.DockPanes;

internal sealed class WorkbenchWindow : Window
{
    private static readonly SurveyWorkbenchViewModel SharedViewModel = SurveyWorkbenchViewModel.Shared;
    private static WorkbenchWindow? _instance;

    private WorkbenchWindow()
    {
        Title = "按区域查询POI - 备用窗口";
        Width = 1280;
        Height = 860;
        MinWidth = 900;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = new SurveyWorkbenchView
        {
            DataContext = SharedViewModel
        };
    }

    internal static void ShowWindow()
    {
        if (_instance is null || !_instance.IsLoaded)
        {
            _instance = new WorkbenchWindow();
            _instance.Closed += (_, _) => _instance = null;
            _instance.Show();
            _ = SharedViewModel.EnsureInitializedAsync();
            return;
        }

        if (_instance.WindowState == WindowState.Minimized)
        {
            _instance.WindowState = WindowState.Normal;
        }

        _instance.Activate();
        _ = SharedViewModel.EnsureInitializedAsync();
    }
}
