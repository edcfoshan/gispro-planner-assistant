using System.Windows;
using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.ColorStyle;

internal sealed class ColorStyleModuleWorkspace : IModuleWorkspace
{
    private readonly IModuleSettingsStore _settingsStore = new JsonModuleSettingsStore();
    private ColorStyleWorkspaceView? _workspaceView;

    public FrameworkElement CreateWorkspace(ModuleContext context)
    {
        if (_workspaceView is null)
        {
            _workspaceView = new ColorStyleWorkspaceView
            {
                DataContext = new ColorStyleWorkspaceViewModel(context, _settingsStore)
            };
        }

        return _workspaceView;
    }
}
