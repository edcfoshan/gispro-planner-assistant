using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Settings;

namespace GisProAddinWorkbench.AddIn;

public class AddInModule : Module
{
    internal const string ModuleId = "GisProAddinWorkbench_AddIn_Module";

    public static AddInModule? Current => FrameworkApplication.FindModule(ModuleId) as AddInModule;

    public AddInModule()
    {
        UiDensityService.Initialize();
        DiagnosticsLogger.Write(nameof(AddInModule), "Module constructed.");
    }

    protected override bool CanUnload()
    {
        DiagnosticsLogger.Write(nameof(AddInModule), "CanUnload called.");
        return true;
    }
}
