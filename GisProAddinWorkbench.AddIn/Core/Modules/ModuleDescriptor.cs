namespace GisProAddinWorkbench.AddIn.Core.Modules;

internal sealed record ModuleDescriptor(
    IModuleDefinition Definition,
    IModuleWorkspace Workspace,
    IModuleGuideProvider GuideProvider);
