using System.Windows;
using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal sealed class ImageryModuleWorkspace : IModuleWorkspace
{
    private readonly IModuleSettingsStore _settingsStore = new JsonModuleSettingsStore();
    private ImageryWorkspaceView? _workspaceView;

    public FrameworkElement CreateWorkspace(ModuleContext context)
    {
        if (_workspaceView is null)
        {
            _workspaceView = new ImageryWorkspaceView
            {
                DataContext = new ImageryWorkspaceViewModel(context, _settingsStore)
            };
        }

        return _workspaceView;
    }
}
