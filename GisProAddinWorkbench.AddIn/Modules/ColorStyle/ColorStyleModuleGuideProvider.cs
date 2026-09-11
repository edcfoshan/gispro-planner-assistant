using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.ColorStyle;

internal sealed class ColorStyleModuleGuideProvider : IModuleGuideProvider
{
    public IReadOnlyList<ModuleGuideStep> GetGuideSteps()
    {
        return
        [
            new ModuleGuideStep(
                1,
                "选择预设配色",
                "先根据项目用途选择图层配色预设，例如规划蓝绿或报批对比。",
                "默认建议使用“规划蓝绿”。",
                "示例：配色预设=规划蓝绿"),
            new ModuleGuideStep(
                2,
                "确定作用范围",
                "决定是否应用到当前地图，避免误改其它项目图层。",
                "跨项目场景下建议先取消勾选并在目标地图验证。",
                "示例：应用到当前地图=是"),
            new ModuleGuideStep(
                3,
                "应用并保存",
                "点击应用后确认效果，再保存模块配置供后续复用。",
                "保存后会写入独立模块配置文件。",
                "示例：状态显示“配色模块配置已保存”。")
        ];
    }
}
