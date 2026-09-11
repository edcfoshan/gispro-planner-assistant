using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal sealed class ImageryModuleDefinition : IModuleDefinition
{
    public string ModuleId => "imagery";

    public string Name => "在线瓦片服务";

    public string Category => "数据准备";

    public int Order => 20;

    public string IconGlyph => "IMG";

    public IReadOnlyList<string> Capabilities =>
    [
        "影像与地图服务图库浏览",
        "点击卡片添加到内容列表",
        "密钥分组管理",
        "临时 lyrx 缓存",
        "支持地图服务、瓦片服务、要素服务与直连瓦片"
    ];
}
