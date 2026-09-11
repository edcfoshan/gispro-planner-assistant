using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal sealed class ImageryModuleGuideProvider : IModuleGuideProvider
{
    public IReadOnlyList<ModuleGuideStep> GetGuideSteps()
    {
        return
        [
            new ModuleGuideStep(
                1,
                "筛选服务",
                "先用顶部两个下拉按服务商和类型缩小范围，再从卡片流里挑选目标在线瓦片服务。",
                "服务商下拉用于按来源筛选，类型下拉用于按内容类型筛选，两个条件同时生效。",
                "示例：服务商选“天地图”，类型选“影像底图”。"),
            new ModuleGuideStep(
                2,
                "查看状态",
                "卡片角标会显示服务类型和可用状态，包括直连、密钥就绪、待配置等信息。",
                "需要密钥的服务先到设置里补齐密钥；直连服务可以直接添加。",
                "示例：先看角标，再决定是否点击添加。"),
            new ModuleGuideStep(
                3,
                "添加到地图",
                "点击任意卡片即可把对应服务加入当前 ArcGIS Pro 地图内容列表。",
                "重复添加前会先检查是否已存在，避免生成重复图层。",
                "示例：点击“全球影像”后直接加入底图。")
        ];
    }
}
