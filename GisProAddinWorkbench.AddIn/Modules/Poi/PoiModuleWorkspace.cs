using System.Windows;
using GisProAddinWorkbench.AddIn.Core.Modules;
using GisProAddinWorkbench.AddIn.DockPanes;

namespace GisProAddinWorkbench.AddIn.Modules.Poi;

internal sealed class PoiModuleWorkspace : IModuleWorkspace
{
    private readonly SurveyWorkbenchViewModel _viewModel = SurveyWorkbenchViewModel.Shared;
    private SurveyWorkbenchView? _workspace;

    public FrameworkElement CreateWorkspace(ModuleContext context)
    {
        if (_workspace is null)
        {
            _workspace = new SurveyWorkbenchView
            {
                DataContext = _viewModel
            };
        }

        _ = _viewModel.EnsureInitializedAsync();
        context.PublishLog("信息", "前期调研工作台已加载。");
        return _workspace;
    }
}
