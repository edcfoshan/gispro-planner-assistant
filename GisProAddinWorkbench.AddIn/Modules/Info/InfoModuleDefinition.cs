using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.Info;

internal sealed class InfoModuleDefinition : IModuleDefinition
{
    public string ModuleId => "info";

    public string Name => "资讯";

    public string Category => "行业信息";

    public int Order => 30;

    public string IconGlyph => "INFO";

    public IReadOnlyList<string> Capabilities =>
    [
        "政策资讯浏览",
        "关键词筛选",
        "项目关联提示",
        "置顶收藏"
    ];
}
