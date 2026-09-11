using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.ColorStyle;

internal sealed class ColorStyleModuleSettings : IModuleSettings
{
    public string PaletteName { get; init; } = "规划蓝绿";

    public bool ApplyToCurrentMap { get; init; } = true;

    public string LastSelectedToolId { get; init; } = "colorstyle-apply";
}
