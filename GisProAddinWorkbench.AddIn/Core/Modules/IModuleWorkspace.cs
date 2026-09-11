using System.Windows;

namespace GisProAddinWorkbench.AddIn.Core.Modules;

internal interface IModuleWorkspace
{
    FrameworkElement CreateWorkspace(ModuleContext context);
}
