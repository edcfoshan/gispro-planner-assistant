using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.Info;

internal sealed class InfoModuleGuideProvider : IModuleGuideProvider
{
    public IReadOnlyList<ModuleGuideStep> GetGuideSteps()
    {
        return
        [
            new ModuleGuideStep(
                1,
                "输入筛选关键字",
                "用项目关键词快速过滤资讯，例如行政区、专项类型或审批事项。",
                "关键字建议 2 到 8 个字，便于匹配。",
                "示例：关键字=存量更新"),
            new ModuleGuideStep(
                2,
                "按优先级查看",
                "可勾选“仅看置顶”优先查看重要政策和高优先级动态。",
                "先处理置顶资讯，再扩展浏览全部内容。",
                "示例：先看“省级重大项目选址审查要点”。"),
            new ModuleGuideStep(
                3,
                "保存个性化筛选",
                "把常用筛选保存为模块配置，减少重复输入。",
                "每个模块配置独立存储，不影响其它模块。",
                "示例：保存偏好关键字=国土空间规划。")
        ];
    }
}
