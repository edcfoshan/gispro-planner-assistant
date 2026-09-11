namespace GisProAddinWorkbench.AddIn.Core.Modules;

internal interface IModuleGuideProvider
{
    IReadOnlyList<ModuleGuideStep> GetGuideSteps();
}
