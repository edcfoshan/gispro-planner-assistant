using GisProAddinWorkbench.AddIn.Modules.ColorStyle;
using GisProAddinWorkbench.AddIn.Modules.Imagery;
using GisProAddinWorkbench.AddIn.Modules.Info;
using GisProAddinWorkbench.AddIn.Modules.Poi;

namespace GisProAddinWorkbench.AddIn.Core.Modules;

internal static class ModuleRegistry
{
    internal static IReadOnlyList<ModuleDescriptor> CreateDefault()
    {
        var modules = new List<ModuleDescriptor>
        {
            new(
                new PoiModuleDefinition(),
                new PoiModuleWorkspace(),
                new PoiModuleGuideProvider()),
            new(
                new ImageryModuleDefinition(),
                new ImageryModuleWorkspace(),
                new ImageryModuleGuideProvider()),
            new(
                new InfoModuleDefinition(),
                new InfoModuleWorkspace(),
                new InfoModuleGuideProvider()),
            new(
                new ColorStyleModuleDefinition(),
                new ColorStyleModuleWorkspace(),
                new ColorStyleModuleGuideProvider())
        };

        return modules
            .OrderBy(module => module.Definition.Order)
            .ToArray();
    }
}
