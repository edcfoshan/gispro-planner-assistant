using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.ColorStyle;

internal sealed class ColorStyleModuleDefinition : IModuleDefinition
{
    public string ModuleId => "color-style";

    public string Name => "配色";

    public string Category => "成果表达";

    public int Order => 40;

    public string IconGlyph => "CLR";

    public IReadOnlyList<string> Capabilities =>
    [
        "规划图层规则配色",
        "图例模板",
        "地类分级颜色预设",
        "样式配置保存"
    ];
}
