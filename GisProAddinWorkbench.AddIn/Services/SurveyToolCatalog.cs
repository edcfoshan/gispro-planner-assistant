using System.IO;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Services;

internal static class SurveyToolCatalog
{
    internal const string LegacyDistrictToolId = "poi.district";
    internal const string LegacyPoiToolId = "poi.poi";
    internal const string LegacyGeocodeToolId = "poi.geocode";

    internal const string AdminAreaBoundaryToolId = "survey.admin-area-boundary";
    internal const string AmapPoiToolId = "survey.amap-poi";
    internal const string BaiduPoiToolId = "survey.baidu-poi";
    internal const string AddressGeocodeToolId = "survey.address-geocode";
    internal const string ReverseGeocodeToolId = "survey.reverse-geocode";
    internal const string CoordTransformToolId = "survey.coord-transform";

    internal static string ToolboxPath => SurveyToolRuntime.GetBundledToolboxRelativeFilePath();

    private const string GuideFolderName = "Guide";

    internal static SurveyToolDocument CreateDefaultDocument()
    {
        var document = new SurveyToolDocument
        {
            Tools =
            [
                new SurveyToolItem
                {
                    GroupId = "admin",
                    GroupLabel = "行政区",
                    ToolId = AdminAreaBoundaryToolId,
                    DisplayName = "行政区边界导出",
                    Badge = "边界",
                    StatusLabel = "推荐",
                    Summary = "按省、市、区县级联选择，快速导出行政区边界面和失败记录。",
                    Scenario = "前期底图准备、区划核对、项目范围初始化时优先使用。",
                    Inputs = "省份、城市、区县、输出坐标模式、输出 GDB。",
                    Outputs = "行政区边界面要素类、失败记录表。",
                    KeyParameters = "平台、处理速度、输出 WGS84、输出库名称。",
                    Notes = "适合做项目边界底座。若需要所有县级结果，先确认输出库空间和命名规则。",
                    FlowSteps =
                    [
                        "先选省份，再逐级选择城市和区县。",
                        "确认输出坐标模式与输出 GDB。",
                        "执行后优先检查失败记录表，再把边界面加入项目。"
                    ],
                    IconKey = "admin-area",
                    ToolPathKey = AdminAreaBoundaryToolId,
                    ToolClassName = "AdminAreaBoundaryExport",
                    AccentHex = "#0F766E"
                },
                new SurveyToolItem
                {
                    GroupId = "poi",
                    GroupLabel = "POI 采集",
                    ToolId = AmapPoiToolId,
                    DisplayName = "高德 POI 批量采集",
                    Badge = "高德",
                    StatusLabel = "常用",
                    Summary = "按范围和分类批量采集 POI，适合正式作业和批量出图前的数据准备。",
                    Scenario = "需要稳定批量采集、按类别筛选、输出结构化 POI 点成果时使用。",
                    Inputs = "范围图层或手动范围、关键词、POI 大类与子类、最大获取数量。",
                    Outputs = "POI 点要素类、可选未完成矩形范围。",
                    KeyParameters = "范围输入方式、分类选择、最大数量、坐标模式、输出库。",
                    Notes = "推荐作为默认 POI 工具。若结果数量大，先小范围试跑，再逐步扩大。",
                    FlowSteps =
                    [
                        "先确定范围来源，再补充关键词或分类。",
                        "先用较小数量试跑，确认字段和坐标满足要求。",
                        "批量执行后，如有未完成范围，再按剩余范围补采。"
                    ],
                    IconKey = "amap-poi",
                    ToolPathKey = AmapPoiToolId,
                    ToolClassName = "AmapPoiInExtent",
                    AccentHex = "#2563EB"
                },
                new SurveyToolItem
                {
                    GroupId = "poi",
                    GroupLabel = "POI 采集",
                    ToolId = BaiduPoiToolId,
                    DisplayName = "百度 POI 区域采集",
                    Badge = "百度",
                    StatusLabel = "测试中",
                    Summary = "按区域和关键词抓取百度 POI，适合补充高德结果或做交叉核对。",
                    Scenario = "当高德结果不完整，或者需要双源比对时使用。",
                    Inputs = "范围图层或手动范围、POI 关键词、最大获取数量。",
                    Outputs = "百度 POI 点要素类。",
                    KeyParameters = "范围输入方式、关键词、最大数量、坐标模式、输出库。",
                    Notes = "当前建议作为复核工具使用，不建议直接作为唯一正式成果来源。",
                    FlowSteps =
                    [
                        "先确定范围，再输入明确关键词。",
                        "用较小数量试跑，确认返回字段和覆盖情况。",
                        "作为补充源与高德结果对照，必要时人工筛查。"
                    ],
                    IconKey = "baidu-poi",
                    ToolPathKey = BaiduPoiToolId,
                    ToolClassName = "BaiduPoiInExtent",
                    AccentHex = "#F59E0B",
                    IsExperimental = true
                },
                new SurveyToolItem
                {
                    GroupId = "geocode",
                    GroupLabel = "编码处理",
                    ToolId = AddressGeocodeToolId,
                    DisplayName = "地址地理编码",
                    Badge = "编码",
                    StatusLabel = "常用",
                    Summary = "把地址表批量转成点坐标，适合住址落点、设施落位和清单核对。",
                    Scenario = "有地址表、需要生成点位成果或后续叠加分析时使用。",
                    Inputs = "输入表或图层、地址字段、市级参考字段、平台、输出模式。",
                    Outputs = "地理编码点要素类、失败记录表。",
                    KeyParameters = "平台选择、处理速度、输出 WGS84、输出库名称。",
                    Notes = "优先检查地址字段质量。城市字段不是必填，但有助于提高匹配稳定性。",
                    FlowSteps =
                    [
                        "确认地址字段和城市字段是否干净完整。",
                        "先用少量样本验证匹配质量，再进行批量编码。",
                        "执行后重点检查失败表和 final_* 字段。"
                    ],
                    IconKey = "address-geocode",
                    ToolPathKey = AddressGeocodeToolId,
                    ToolClassName = "GeocodeToWgs84Points",
                    AccentHex = "#7C3AED"
                },
                new SurveyToolItem
                {
                    GroupId = "geocode",
                    GroupLabel = "编码处理",
                    ToolId = ReverseGeocodeToolId,
                    DisplayName = "逆地理编码",
                    Badge = "逆编",
                    StatusLabel = "辅助",
                    Summary = "根据点坐标反查附近 POI 或点信息，适合样点解释和现状核验。",
                    Scenario = "已有点位、想知道附近地物或反查地点属性时使用。",
                    Inputs = "点图层、输入坐标系、平台、搜索半径、POI 数量。",
                    Outputs = "附近 POI 点结果。",
                    KeyParameters = "平台、半径、每点返回数量、处理速度、输出库。",
                    Notes = "适合样点抽查和核验，不适合作为大规模 POI 主采集入口。",
                    FlowSteps =
                    [
                        "先确认点位坐标系与输入图层是否一致。",
                        "设置合理搜索半径，避免返回过多无关点。",
                        "执行后按点检查周边结果是否符合业务判断。"
                    ],
                    IconKey = "reverse-geocode",
                    ToolPathKey = ReverseGeocodeToolId,
                    ToolClassName = "ReverseGeocodeToPoi",
                    AccentHex = "#DC2626"
                },
                new SurveyToolItem
                {
                    GroupId = "coord",
                    GroupLabel = "坐标工具",
                    ToolId = CoordTransformToolId,
                    DisplayName = "坐标系转换",
                    Badge = "坐标",
                    StatusLabel = "基础",
                    Summary = "在 WGS84、CGCS2000、GCJ02、BD09 之间批量转换点坐标。",
                    Scenario = "多源数据坐标不统一，或者导入前需要先校准坐标系时使用。",
                    Inputs = "点图层、输入坐标系、目标坐标系、输出库。",
                    Outputs = "转换后的点要素类。",
                    KeyParameters = "输入坐标系、目标坐标系、是否使用临时空间、输出名称。",
                    Notes = "先确认源坐标系，不要把“坐标值错”误当成“坐标系错”。",
                    FlowSteps =
                    [
                        "确认输入点图层和源坐标系。",
                        "选择目标坐标系并确认输出位置。",
                        "执行后抽查典型点位，确认坐标偏移符合预期。"
                    ],
                    IconKey = "coord-transform",
                    ToolPathKey = CoordTransformToolId,
                    ToolClassName = "CoordTransformPoints",
                    AccentHex = "#0891B2"
                }
            ]
        };

        EnrichAssetPaths(document.Tools);
        return document;
    }

    internal static IReadOnlyDictionary<string, string> BuildDefaultMappings()
    {
        return CreateDefaultDocument()
            .Tools
            .ToDictionary(
                tool => tool.ToolPathKey,
                tool => BuildToolPath(tool.ToolClassName),
                StringComparer.OrdinalIgnoreCase);
    }

    internal static SurveyToolItem? FindTool(string toolId)
    {
        return CreateDefaultDocument()
            .Tools
            .FirstOrDefault(tool => tool.ToolId.Equals(toolId, StringComparison.OrdinalIgnoreCase));
    }

    internal static string BuildToolPath(string toolClassName)
    {
        return $@"{ToolboxPath}\{toolClassName}";
    }

    internal static string GetContentPath(params string[] segments)
    {
        return Path.Combine(new[] { SurveyToolRuntime.GetAddInRootDirectory() }.Concat(segments).ToArray());
    }

    internal static string GetOverviewImagePath()
    {
        return GetContentPath("Images", GuideFolderName, "survey-overview.png");
    }

    internal static string GetQuickReferenceImagePath()
    {
        return GetContentPath("Images", GuideFolderName, "survey-tool-quick-reference.png");
    }

    internal static bool ToolPathLooksValid(string toolPath)
    {
        var normalized = SurveyToolRuntime.NormalizeConfiguredToolPath(toolPath);
        return !string.IsNullOrWhiteSpace(normalized) && File.Exists(GetToolboxFilePath(normalized));
    }

    internal static string GetToolboxFilePath(string toolPath)
    {
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            return string.Empty;
        }

        var normalized = toolPath.Trim().Replace('/', '\\');
        var markerIndex = normalized.IndexOf(".pyt", StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return normalized;
        }

        return normalized[..(markerIndex + 4)];
    }

    internal static void EnrichAssetPaths(IEnumerable<SurveyToolItem> tools)
    {
        foreach (var tool in tools)
        {
            tool.IllustrationPath = GetContentPath("Images", GuideFolderName, $"survey-card-{tool.IconKey}.png");
            tool.GuideImagePath = GetContentPath("Images", GuideFolderName, $"survey-guide-{tool.IconKey}.png");
        }
    }

    internal static void ApplyLegacyMappings(
        IReadOnlyDictionary<string, string> existing,
        IDictionary<string, string> mappings)
    {
        CopyIfPresent(existing, LegacyDistrictToolId, AdminAreaBoundaryToolId, mappings);
        CopyIfPresent(existing, LegacyPoiToolId, AmapPoiToolId, mappings);
        CopyIfPresent(existing, LegacyGeocodeToolId, AddressGeocodeToolId, mappings);
    }

    private static void CopyIfPresent(
        IReadOnlyDictionary<string, string> existing,
        string legacyKey,
        string newKey,
        IDictionary<string, string> mappings)
    {
        if (!existing.TryGetValue(legacyKey, out var value))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        mappings[newKey] = value.Trim();
    }
}
