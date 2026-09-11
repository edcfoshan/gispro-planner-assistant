using System.Windows;
using System.Windows.Controls;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.DockPanes;

internal class WorkbenchDockPaneViewModel : DockPane
{
    internal const string DockPaneId = "GisProAddinWorkbench_AddIn_WorkbenchDockPane";
    private static readonly WorkbenchShellViewModel SharedViewModel = new();

    protected WorkbenchDockPaneViewModel()
    {
        Caption = "城乡规划作业台";
    }

    internal static void Show(string? moduleId = null)
    {
        if (FrameworkApplication.DockPaneManager.Find(DockPaneId) is WorkbenchDockPaneViewModel pane)
        {
            pane.Activate();
            if (!string.IsNullOrWhiteSpace(moduleId))
            {
                SharedViewModel.ActivateModule(moduleId);
            }
        }
    }

    internal static void ShowMapWorkspace(MapWorkspaceRequest request)
    {
        void OpenInWorkbench()
        {
            Show();
            SharedViewModel.ShowMapWorkspace(request);
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            OpenInWorkbench();
            return;
        }

        dispatcher.Invoke(OpenInWorkbench);
    }

    protected override Control OnCreateContent()
    {
        return new WorkbenchDockPaneView
        {
            DataContext = SharedViewModel
        };
    }
}
