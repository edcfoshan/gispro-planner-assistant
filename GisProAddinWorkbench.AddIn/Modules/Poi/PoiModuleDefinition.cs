using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.Poi;

internal sealed class PoiModuleDefinition : IModuleDefinition
{
    public string ModuleId => "poi";

    public string Name => "前期调研";

    public string Category => "数据采集";

    public int Order => 10;

    public string IconGlyph => "POI";

    public IReadOnlyList<string> Capabilities =>
    [
        "4 类场景导航：行政区、POI、编码处理、坐标工具",
        "6 个真实 PYT 工具：边界导出、双源 POI、正逆地理编码、坐标转换",
        "插件内查看说明、流程图、路径状态",
        "打开 ArcGIS 原生工具执行并回看成果"
    ];
}
