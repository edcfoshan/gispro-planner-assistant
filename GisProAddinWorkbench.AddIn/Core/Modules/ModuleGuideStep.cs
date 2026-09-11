namespace GisProAddinWorkbench.AddIn.Core.Modules;

internal sealed record ModuleGuideStep(
    int Order,
    string Title,
    string Description,
    string Checklist,
    string Example);
