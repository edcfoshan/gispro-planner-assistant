using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.Poi;

internal sealed class PoiModuleGuideProvider : IModuleGuideProvider
{
    public IReadOnlyList<ModuleGuideStep> GetGuideSteps()
    {
        return
        [
            new ModuleGuideStep(
                1,
                "先选工具，不要先猜参数",
                "先在 4 类场景里判断是行政区、POI、编码处理还是坐标工具，再进入具体工具。",
                "不确定时先看“前期调研总览图”和“工具速查图”。",
                "示例：有地址表需要落点，就先选“地址地理编码”。"),
            new ModuleGuideStep(
                2,
                "先看说明，再检查路径",
                "右侧先看典型输入、输出、关键参数和建议流程，再确认当前工具路径是否有效。",
                "如路径无效，先打开设置页修正，不要直接开始执行。",
                "示例：先检查路径是否指向 百度高德Geocode.pyt\\GeocodeToWgs84Points。"),
            new ModuleGuideStep(
                3,
                "在 ArcGIS 工具面板执行",
                "前期调研模块负责导航和解释，真正执行时会打开 ArcGIS 原生 Geoprocessing 面板。",
                "先小样本试跑，再做批量执行。",
                "示例：POI 工具先用较小范围和较小数量确认结果。"),
            new ModuleGuideStep(
                4,
                "回到项目里检查成果",
                "工具执行完成后，回看输出要素类、失败记录和字段完整性。",
                "重点检查坐标是否正确、字段是否齐全、失败表是否需要补跑。",
                "示例：地理编码后优先检查失败记录表和 final_* 字段。")
        ];
    }
}
