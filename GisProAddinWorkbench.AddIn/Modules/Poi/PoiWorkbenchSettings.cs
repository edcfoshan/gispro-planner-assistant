using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.Poi;

internal sealed class PoiWorkbenchSettings : IModuleSettings
{
    public int RequestTimeoutSeconds { get; init; } = 30;

    public int DefaultPageSize { get; init; } = 20;
}
