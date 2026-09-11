using GisProAddinWorkbench.AddIn.Models;
using GisProAddinWorkbench.AddIn.Services;

namespace GisProAddinWorkbench.AddIn.Guides;

internal static class GuideCatalog
{
    internal static GuideImageData SystemGuide()
    {
        return new GuideImageData(
            "系统分组使用引导",
            "用于版本与声明查看与参数设置，建议每次作业前先完成。",
            [
                "点击“版本与声明”，核对版本号、更新时间和变更说明。",
                "点击“设置”，在窗口内填写密钥、默认输出库、超时和分页。",
                "保存后关闭窗口，再进入业务分组执行任务。"
            ],
            "建议顺序：版本与声明 -> 设置 -> 进入前期调研/在线瓦片服务/配色。");
    }

    internal static GuideImageData SurveyGuide()
    {
        return new GuideImageData(
            "前期调研总览图",
            "围绕行政区、POI、编码处理、坐标工具四类场景组织 6 个真实 PYT 工具。",
            [
                "先根据目标选择工具，而不是先猜参数。",
                "优先在插件里看说明和路径状态，再打开 ArcGIS 原生工具。",
                "执行完成后回到项目里检查成果图层和失败记录。"
            ],
            "建议先看总览图，再根据具体任务打开对应工具说明。",
            SurveyToolCatalog.GetOverviewImagePath());
    }

    internal static GuideImageData SurveyQuickReferenceGuide()
    {
        return new GuideImageData(
            "前期调研工具速查图",
            "用一句话帮使用者判断当前任务该打开哪个工具。",
            [
                "需要边界底座：行政区边界导出。",
                "需要采集 POI：先用高德 POI，百度 POI 作为补充复核。",
                "有地址表：用地址地理编码；有点位要反查：用逆地理编码。"
            ],
            "遇到坐标不统一时，优先先做坐标系转换，再进入后续流程。",
            SurveyToolCatalog.GetQuickReferenceImagePath());
    }

    internal static IReadOnlyList<GuideImageData> SurveyIntroPages()
    {
        return new[]
        {
            SurveyGuide(),
            SurveyQuickReferenceGuide(),
            new GuideImageData(
                "前期调研工具选择说明",
                "先看工具列表，再看右侧当前工具说明和路径状态，确认后再打开 ArcGIS 工具。",
                [
                    "先从左侧分组里选择目标工具，而不是先去猜参数。比如行政区、POI、编码处理、坐标转换分别对应不同场景。",
                    "选中工具后，先看右侧的工具摘要、输入输出、关键参数和使用提示，再决定是否执行。",
                    "确认工具路径已配置且可用后，再点击“打开 ArcGIS 工具”进入正式执行。",
                    "如果不确定要用哪个工具，先回到总览页，再对照工具速查图重新判断。"
                ],
                "建议把它当作“先选工具、再执行”的操作清单，减少在 ArcGIS 工具间来回切换。"),
            new GuideImageData(
                "视频页预留",
                "此页先作为视频播放位占位，后续可直接替换为录屏教学或讲解视频。",
                [
                    "当前版本先用图片翻页方式介绍前期调研模块。",
                    "后续如果补充视频，可以直接把这一页替换成视频内容。",
                    "引导窗的翻页结构已经预留好，不需要改主流程。"
                ],
                "预留位：后续可接入 mp4、录屏或在线讲解视频。")
        };
    }

    internal static GuideImageData SurveyToolGuide(SurveyToolItem tool)
    {
        return new GuideImageData(
            tool.DisplayName,
            tool.Summary,
            tool.FlowSteps.Length > 0 ? tool.FlowSteps : ["查看工具参数后执行。"],
            $"典型场景：{tool.Scenario}",
            tool.GuideImagePath);
    }

    internal static GuideImageData ImageryGuide()
    {
        return new GuideImageData(
            "在线瓦片服务分组使用引导",
            "用于本地影像和服务影像的加载与参数配置。",
            [
                "点击“在线瓦片服务”进入影像模块。",
                "填写本地路径或服务地址，至少提供一项。",
                "执行示例后确认状态，再保存模块配置。"
            ],
            "建议先加载小范围样例数据，再应用到完整项目。");
    }

    internal static GuideImageData ColorStyleGuide()
    {
        return new GuideImageData(
            "配色分组使用引导",
            "用于规划图层样式与制图配色预设管理。",
            [
                "点击“配色”进入配色模块。",
                "选择配色预设，确认是否应用到当前地图。",
                "应用效果后保存配置，便于后续复用。"
            ],
            "推荐优先使用“规划蓝绿”预设，保证成果风格一致。");
    }
}
