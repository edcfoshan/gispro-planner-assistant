using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.Info;

internal sealed class InfoModuleSettings : IModuleSettings
{
    public string PreferredKeyword { get; init; } = string.Empty;

    public bool ShowPinnedOnly { get; init; }

    public string LastSelectedToolId { get; init; } = "info-search";
}
