using System.Windows;
using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.Info;

internal sealed class InfoModuleWorkspace : IModuleWorkspace
{
    private readonly IModuleSettingsStore _settingsStore = new JsonModuleSettingsStore();
    private InfoWorkspaceView? _workspaceView;

    public FrameworkElement CreateWorkspace(ModuleContext context)
    {
        if (_workspaceView is null)
        {
            _workspaceView = new InfoWorkspaceView
            {
                DataContext = new InfoWorkspaceViewModel(context, _settingsStore)
            };
        }

        return _workspaceView;
    }
}
