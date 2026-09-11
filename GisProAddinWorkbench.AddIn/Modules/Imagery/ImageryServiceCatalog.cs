using System.Security.Cryptography;
using System.Net.Http;
using System.Text;
using System.Windows.Media;
using System.Xml.Linq;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal enum ImageryCredentialKind
{
    None,
    TianDiTu,
    JilinOne
}

internal enum ImageryServiceKind
{
    TileTemplate,
    DirectTileTemplate,
    MapServer,
    Wmts,
    Wfs
}

internal enum ImageryLayerRole
{
    Base,
    Annotation,
    Terrain,
    Boundary,
    Feature
}

internal enum ImageryClassificationMode
{
    Provider,
    ContentType
}

internal sealed record ImageryServicePreset(
    string Id,
    string Provider,
    string DisplayName,
    string Category,
    string Summary,
    string Tag,
    string Symbol,
    string ReferenceCode,
    ImageryServiceKind ServiceKind,
    ImageryCredentialKind CredentialKind,
    ImageryLayerRole LayerRole,
    int SortOrder,
    string SourceUrl,
    string? PreviewUrl,
    Brush AccentBrush,
    Brush AccentSoftBrush);

internal static partial class ImageryServiceCatalog
{
    private static readonly HttpClient WfsCapabilitiesHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly IReadOnlyList<ImageryServicePreset> Presets =
    [
        CreateTilePreset(
            "tdt-vec",
            "天地图",
            "矢量地图",
            "底图",
            "适合作为城市底图与规划分析的基础底图。",
            "矢",
            "天",
            "vec",
            ImageryCredentialKind.TianDiTu,
            ImageryLayerRole.Base,
            10,
            CreateBrush("#1D4ED8"),
            CreateBrush("#DBEAFE")),
        CreateTilePreset(
            "tdt-cva",
            "天地图",
            "矢量注记",
            "注记",
            "与矢量底图配套的地名与标注图层。",
            "注",
            "天",
            "cva",
            ImageryCredentialKind.TianDiTu,
            ImageryLayerRole.Annotation,
            20,
            CreateBrush("#2563EB"),
            CreateBrush("#E0E7FF")),
        CreateTilePreset(
            "tdt-img",
            "天地图",
            "影像地图",
            "底图",
            "适合影像判读、现状分析与快速核对。",
            "图",
            "天",
            "img",
            ImageryCredentialKind.TianDiTu,
            ImageryLayerRole.Base,
            30,
            CreateBrush("#0891B2"),
            CreateBrush("#CFFAFE")),
        CreateTilePreset(
            "tdt-cia",
            "天地图",
            "影像注记",
            "注记",
            "影像底图配套的文字注记图层。",
            "注",
            "天",
            "cia",
            ImageryCredentialKind.TianDiTu,
            ImageryLayerRole.Annotation,
            40,
            CreateBrush("#0EA5E9"),
            CreateBrush("#E0F2FE")),
        CreateTilePreset(
            "tdt-ter",
            "天地图",
            "地形晕渲",
            "地形",
            "用于展示地表起伏与地形阴影的专题底图。",
            "地",
            "天",
            "ter",
            ImageryCredentialKind.TianDiTu,
            ImageryLayerRole.Terrain,
            50,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1")),
        CreateTilePreset(
            "tdt-cta",
            "天地图",
            "地形注记",
            "注记",
            "地形底图配套的地名与标注图层。",
            "注",
            "天",
            "cta",
            ImageryCredentialKind.TianDiTu,
            ImageryLayerRole.Annotation,
            60,
            CreateBrush("#14B8A6"),
            CreateBrush("#D1FAE5")),
        CreateTilePreset(
            "tdt-ibo",
            "天地图",
            "全球境界",
            "边界",
            "用于全球行政边界与空间参考核对。",
            "界",
            "天",
            "ibo",
            ImageryCredentialKind.TianDiTu,
            ImageryLayerRole.Boundary,
            70,
            CreateBrush("#475569"),
            CreateBrush("#E2E8F0")),
        CreateTilePreset(
            "jl1-2021",
            "吉林一号",
            "2021年全球一张图",
            "年度",
            "吉林一号 2021 年度全球一张图预设。",
            "2021",
            "吉",
            "",
            ImageryCredentialKind.JilinOne,
            ImageryLayerRole.Base,
            80,
            CreateBrush("#059669"),
            CreateBrush("#D1FAE5")),
        CreateTilePreset(
            "jl1-2023",
            "吉林一号",
            "2023年度全国高质量一张图",
            "年度",
            "吉林一号 2023 年度全国高质量一张图预设。",
            "2023",
            "吉",
            "",
            ImageryCredentialKind.JilinOne,
            ImageryLayerRole.Base,
            90,
            CreateBrush("#0D9488"),
            CreateBrush("#CCFBF1")),
        CreateTilePreset(
            "jl1-2024",
            "吉林一号",
            "2024年度全国高质量一张图",
            "年度",
            "吉林一号 2024 年度全国高质量一张图预设。",
            "2024",
            "吉",
            "",
            ImageryCredentialKind.JilinOne,
            ImageryLayerRole.Base,
            100,
            CreateBrush("#16A34A"),
            CreateBrush("#DCFCE7")),
        CreateTilePreset(
            "jl1-global",
            "吉林一号",
            "全球一张图",
            "全球",
            "吉林一号全球一张图预设，适合跨区域浏览与核对。",
            "全球",
            "吉",
            "",
            ImageryCredentialKind.JilinOne,
            ImageryLayerRole.Base,
            110,
            CreateBrush("#7C3AED"),
            CreateBrush("#EDE9FE")),
        CreateMapServerPreset(
            "esri-world-imagery",
            "Esri",
            "全球影像",
            "影像",
            "ArcGIS Online 全球影像底图，适合快速定位与现状浏览。",
            "影像",
            "世",
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer",
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/export?bbox=-180,-85,180,85&bboxSR=4326&size=256,144&format=png32&f=image",
            ImageryLayerRole.Base,
            120,
            CreateBrush("#0F6AB4"),
            CreateBrush("#DCEBFA")),
        CreateMapServerPreset(
            "world-street-map",
            "Esri",
            "街道地图",
            "道路",
            "ArcGIS Online 中文街道底图，适合快速定位与路网参考。",
            "道路",
            "路",
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer",
            BuildMapServerPreviewUrl("https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer"),
            ImageryLayerRole.Base,
            121,
            CreateBrush("#475569"),
            CreateBrush("#E2E8F0")),
        CreateMapServerPreset(
            "world-topo-map",
            "Esri",
            "地形底图",
            "地形",
            "ArcGIS Online 地形底图，适合地形判读与背景参考。",
            "地形",
            "山",
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer",
            BuildMapServerPreviewUrl("https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer"),
            ImageryLayerRole.Base,
            122,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1")),
        CreateMapServerPreset(
            "usa-topo-maps",
            "Esri",
            "美国地形图",
            "地形",
            "ArcGIS Online 美国地形图，适合北美区域核对。",
            "地形",
            "美",
            "https://server.arcgisonline.com/ArcGIS/rest/services/USA_Topo_Maps/MapServer",
            BuildMapServerPreviewUrl("https://server.arcgisonline.com/ArcGIS/rest/services/USA_Topo_Maps/MapServer"),
            ImageryLayerRole.Base,
            123,
            CreateBrush("#0EA5E9"),
            CreateBrush("#E0F2FE")),
        CreateMapServerPreset(
            "world-physical-map",
            "Esri",
            "自然地理图",
            "地形",
            "ArcGIS Online 自然地理底图，适合地形与地理背景浏览。",
            "地形",
            "自",
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Physical_Map/MapServer",
            BuildMapServerPreviewUrl("https://server.arcgisonline.com/ArcGIS/rest/services/World_Physical_Map/MapServer"),
            ImageryLayerRole.Base,
            124,
            CreateBrush("#059669"),
            CreateBrush("#D1FAE5")),
        CreateMapServerPreset(
            "world-shaded-relief",
            "Esri",
            "晕渲底图",
            "地形",
            "ArcGIS Online 晕渲底图，适合地形起伏表达。",
            "地形",
            "晕",
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Shaded_Relief/MapServer",
            BuildMapServerPreviewUrl("https://server.arcgisonline.com/ArcGIS/rest/services/World_Shaded_Relief/MapServer"),
            ImageryLayerRole.Terrain,
            125,
            CreateBrush("#7C3AED"),
            CreateBrush("#EDE9FE")),
        CreateMapServerPreset(
            "world-terrain-base",
            "Esri",
            "地形底图",
            "地形",
            "ArcGIS Online 地形底图，适合地形背景与参考。",
            "地形",
            "山",
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Terrain_Base/MapServer",
            BuildMapServerPreviewUrl("https://server.arcgisonline.com/ArcGIS/rest/services/World_Terrain_Base/MapServer"),
            ImageryLayerRole.Terrain,
            126,
            CreateBrush("#2563EB"),
            CreateBrush("#DBEAFE")),
        CreateMapServerPreset(
            "natgeo-world-map",
            "Esri",
            "国家地理风格",
            "专题",
            "ArcGIS Online 国家地理风格底图，适合专题浏览与背景参考。",
            "专题",
            "国",
            "https://server.arcgisonline.com/ArcGIS/rest/services/NatGeo_World_Map/MapServer",
            BuildMapServerPreviewUrl("https://server.arcgisonline.com/ArcGIS/rest/services/NatGeo_World_Map/MapServer"),
            ImageryLayerRole.Base,
            127,
            CreateBrush("#F97316"),
            CreateBrush("#FFEDD5")),
        .. CreateWaybackPresets(),
        CreateDirectTilePreset(
            "amap-style-6",
            "高德",
            "影像图",
            "影像",
            "高德影像底图，适合现状判读与快速浏览。",
            "影像",
            "影",
            "style6",
            "https://webst01.is.autonavi.com/appmaptile?style=6&x={x}&y={y}&z={z}",
            "https://webst01.is.autonavi.com/appmaptile?style=6&x=0&y=0&z=1",
            ImageryLayerRole.Base,
            150,
            CreateBrush("#EA580C"),
            CreateBrush("#FFEDD5")),
        CreateDirectTilePreset(
            "amap-style-8",
            "高德",
            "道路地图",
            "道路",
            "高德中文道路地图，适合快速定位与路网参考。",
            "道路",
            "路",
            "style8",
            "https://wprd01.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={x}&y={y}&z={z}",
            "https://wprd01.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x=0&y=0&z=1",
            ImageryLayerRole.Base,
            160,
            CreateBrush("#F97316"),
            CreateBrush("#FFEDD5")),
        CreateWfsPreset(
            "tdt-wfs-aanp",
            "天地图",
            "自然地名（2017）",
            "WFS",
            "天地图 WFS 要素图层，自然地名，数据截止到 2017 年，适合行政区核对。",
            "要素",
            "要",
            "AANP",
            "http://gisserver.tianditu.gov.cn/TDTService/wfs",
            null,
            ImageryLayerRole.Feature,
            170,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1")),
        CreateWfsPreset(
            "tdt-wfs-agnp",
            "天地图",
            "居民地地名（2017）",
            "WFS",
            "天地图 WFS 要素图层，居民地地名，数据截止到 2017 年。",
            "要素",
            "要",
            "AGNP",
            "http://gisserver.tianditu.gov.cn/TDTService/wfs",
            null,
            ImageryLayerRole.Feature,
            180,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1")),
        CreateWfsPreset(
            "tdt-wfs-boua",
            "天地图",
            "行政区面（2017）",
            "WFS",
            "天地图 WFS 要素图层，行政区面，数据截止到 2017 年。",
            "要素",
            "要",
            "BOUA",
            "http://gisserver.tianditu.gov.cn/TDTService/wfs",
            null,
            ImageryLayerRole.Feature,
            190,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1")),
        CreateWfsPreset(
            "tdt-wfs-boul",
            "天地图",
            "行政境界（2017）",
            "WFS",
            "天地图 WFS 要素图层，行政境界，数据截止到 2017 年。",
            "要素",
            "要",
            "BOUL",
            "http://gisserver.tianditu.gov.cn/TDTService/wfs",
            null,
            ImageryLayerRole.Feature,
            200,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1")),
        CreateWfsPreset(
            "tdt-wfs-hyda",
            "天地图",
            "水系面（2017）",
            "WFS",
            "天地图 WFS 要素图层，水系面，数据截止到 2017 年。",
            "要素",
            "要",
            "HYDA",
            "http://gisserver.tianditu.gov.cn/TDTService/wfs",
            null,
            ImageryLayerRole.Feature,
            210,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1")),
        CreateWfsPreset(
            "tdt-wfs-hydl",
            "天地图",
            "水系线（2017）",
            "WFS",
            "天地图 WFS 要素图层，水系线，数据截止到 2017 年。",
            "要素",
            "要",
            "HYDL",
            "http://gisserver.tianditu.gov.cn/TDTService/wfs",
            null,
            ImageryLayerRole.Feature,
            220,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1")),
        CreateWfsPreset(
            "tdt-wfs-lrdl",
            "天地图",
            "铁路线（2017）",
            "WFS",
            "天地图 WFS 要素图层，铁路线，数据截止到 2017 年。",
            "要素",
            "要",
            "LRDL",
            "http://gisserver.tianditu.gov.cn/TDTService/wfs",
            null,
            ImageryLayerRole.Feature,
            230,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1")),
        CreateWfsPreset(
            "tdt-wfs-lrrl",
            "天地图",
            "公路线（2017）",
            "WFS",
            "天地图 WFS 要素图层，公路线，数据截止到 2017 年。",
            "要素",
            "要",
            "LRRL",
            "http://gisserver.tianditu.gov.cn/TDTService/wfs",
            null,
            ImageryLayerRole.Feature,
            240,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1")),
        CreateWfsPreset(
            "tdt-wfs-resa",
            "天地图",
            "居民地面（2017）",
            "WFS",
            "天地图 WFS 要素图层，居民地面，数据截止到 2017 年。",
            "要素",
            "要",
            "RESA",
            "http://gisserver.tianditu.gov.cn/TDTService/wfs",
            null,
            ImageryLayerRole.Feature,
            250,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1")),
        CreateWfsPreset(
            "tdt-wfs-resp",
            "天地图",
            "居民地点（2017）",
            "WFS",
            "天地图 WFS 要素图层，居民地点，数据截止到 2017 年。",
            "要素",
            "要",
            "RESP",
            "http://gisserver.tianditu.gov.cn/TDTService/wfs",
            null,
            ImageryLayerRole.Feature,
            260,
            CreateBrush("#0F766E"),
            CreateBrush("#CCFBF1"))
    ];

    internal static IReadOnlyList<ImageryServicePreset> GetPresets()
    {
        return Presets;
    }

    internal static string BuildLayerName(ImageryServicePreset preset)
    {
        return $"{preset.Provider}-{preset.DisplayName}（{GetServiceKindLabel(preset.ServiceKind)}）";
    }

    internal static string BuildDescription(ImageryServicePreset preset, string url)
    {
        return $"来源：{preset.Provider}；类型：{GetServiceKindLabel(preset.ServiceKind)}；服务地址：{url}";
    }

    internal static string BuildServiceUrl(ImageryServicePreset preset, string credential)
    {
        return preset.ServiceKind switch
        {
            ImageryServiceKind.TileTemplate => preset.CredentialKind switch
            {
                ImageryCredentialKind.TianDiTu => BuildTianDiTuUrl(preset.ReferenceCode, credential),
                ImageryCredentialKind.JilinOne => BuildJilinUrl(preset.ReferenceCode, credential),
                _ => string.Empty
            },
            ImageryServiceKind.DirectTileTemplate or ImageryServiceKind.MapServer or ImageryServiceKind.Wmts or ImageryServiceKind.Wfs => preset.SourceUrl,
            _ => preset.SourceUrl
        };
    }

    internal static string BuildCacheFileName(ImageryServicePreset preset, string credential)
    {
        var seed = string.IsNullOrWhiteSpace(credential)
            ? (string.IsNullOrWhiteSpace(preset.SourceUrl) ? preset.ReferenceCode : preset.SourceUrl)
            : credential;
        return $"{preset.Id}-{BuildSeedHash(seed)}.lyrx";
    }

    internal static string? GetActiveCredential(ImageryModuleSettings settings, ImageryCredentialKind credentialKind)
    {
        if (credentialKind == ImageryCredentialKind.None)
        {
            return string.Empty;
        }

        IEnumerable<string> candidates = credentialKind switch
        {
            ImageryCredentialKind.TianDiTu => settings.EffectiveTianDiTuKeys,
            ImageryCredentialKind.JilinOne => settings.EffectiveJilinApiKeys,
            _ => []
        };

        foreach (var candidate in candidates)
        {
            if (IsValidCredential(candidate))
            {
                return candidate.Trim();
            }
        }

        return null;
    }

    internal static string? GetRandomCredential(ImageryModuleSettings settings, ImageryCredentialKind credentialKind)
    {
        if (credentialKind == ImageryCredentialKind.None)
        {
            return string.Empty;
        }

        var candidates = (credentialKind switch
        {
            ImageryCredentialKind.TianDiTu => settings.EffectiveTianDiTuKeys,
            ImageryCredentialKind.JilinOne => settings.EffectiveJilinApiKeys,
            _ => []
        })
        .Where(IsValidCredential)
        .Select(candidate => candidate.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var index = RandomNumberGenerator.GetInt32(candidates.Count);
        return candidates[index];
    }

    internal static bool HasConfiguredCredential(ImageryModuleSettings settings, ImageryCredentialKind credentialKind)
    {
        return credentialKind == ImageryCredentialKind.None || !string.IsNullOrWhiteSpace(GetActiveCredential(settings, credentialKind));
    }

    internal static string GetCredentialLabel(ImageryCredentialKind credentialKind)
    {
        return credentialKind switch
        {
            ImageryCredentialKind.None => "无需密钥",
            ImageryCredentialKind.TianDiTu => "天地图 tk",
            ImageryCredentialKind.JilinOne => "吉林一号 api",
            _ => "密钥"
        };
    }

    internal static string GetServiceKindLabel(ImageryServiceKind serviceKind)
    {
        return serviceKind switch
        {
            ImageryServiceKind.TileTemplate => "\u74e6\u7247",
            ImageryServiceKind.DirectTileTemplate => "\u76f4\u8fde\u74e6\u7247",
            ImageryServiceKind.MapServer => "\u5730\u56fe\u670d\u52a1",
            ImageryServiceKind.Wmts => "\u74e6\u7247\u670d\u52a1",
            ImageryServiceKind.Wfs => "\u8981\u7d20\u670d\u52a1",
            _ => "\u670d\u52a1"
        };
    }

    internal static string GetClassificationModeLabel(ImageryClassificationMode mode)
    {
        return mode switch
        {
            ImageryClassificationMode.Provider => "服务商",
            ImageryClassificationMode.ContentType => "瓦片类型",
            _ => "服务商"
        };
    }

    internal static string GetProviderLabel(ImageryServicePreset preset)
    {
        if (TryResolveProviderLabel(preset.SourceUrl, preset.CredentialKind, out var providerLabel))
        {
            return providerLabel;
        }

        if (!string.IsNullOrWhiteSpace(preset.Provider))
        {
            return preset.Provider.Trim();
        }

        return "其他/未识别";
    }

    internal static string GetContentTypeLabel(ImageryServicePreset preset)
    {
        return preset.Id switch
        {
            "tdt-vec" => "矢量底图",
            "tdt-img" => "影像底图",
            "tdt-cva" or "tdt-cia" or "tdt-cta" => "注记",
            "tdt-ter" => "地形",
            "tdt-ibo" => "边界",
            "jl1-2021" or "jl1-2023" or "jl1-2024" or "jl1-global" => "年度影像",
            "esri-world-imagery" or "amap-style-6" => "影像底图",
            "world-street-map" => "道路地图",
            "world-topo-map" or "usa-topo-maps" or "world-physical-map" or "world-shaded-relief" or "world-terrain-base" => "地形",
            "natgeo-world-map" => "专题图",
            "esri-wayback-r02" => "历史影像",
            "amap-style-8" => "道路地图",
            _ when preset.ServiceKind == ImageryServiceKind.Wfs || preset.LayerRole == ImageryLayerRole.Feature => "要素服务",
            _ => InferContentTypeLabel(preset)
        };
    }

    internal static IReadOnlyList<string> GetProviderFilterLabels()
    {
        return Presets
            .Select(GetProviderLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetProviderGroupOrder)
            .ThenBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static IReadOnlyList<string> GetContentTypeFilterLabels()
    {
        return Presets
            .Select(GetContentTypeLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetContentTypeGroupOrder)
            .ThenBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static string GetClassificationGroupLabel(ImageryServicePreset preset, ImageryClassificationMode mode)
    {
        return mode switch
        {
            ImageryClassificationMode.Provider => GetProviderLabel(preset),
            ImageryClassificationMode.ContentType => GetContentTypeLabel(preset),
            _ => GetProviderLabel(preset)
        };
    }

    internal static int GetClassificationGroupOrder(ImageryServicePreset preset, ImageryClassificationMode mode)
    {
        return mode switch
        {
            ImageryClassificationMode.Provider => GetProviderGroupOrder(GetProviderLabel(preset)),
            ImageryClassificationMode.ContentType => GetContentTypeGroupOrder(GetContentTypeLabel(preset)),
            _ => 999
        };
    }

    internal static string GetClassificationSummary(ImageryServicePreset preset, ImageryClassificationMode mode)
    {
        return $"{GetClassificationModeLabel(mode)}：{GetClassificationGroupLabel(preset, mode)}";
    }

    internal static string GetWfsDatasetName(ImageryServicePreset preset)
    {
        return preset.ReferenceCode?.Trim() ?? string.Empty;
    }

    internal static bool ShouldSwapWfsCoordinates(ImageryServicePreset preset)
    {
        return preset.ServiceKind == ImageryServiceKind.Wfs &&
               (preset.CredentialKind == ImageryCredentialKind.TianDiTu ||
                preset.SourceUrl.Contains("tianditu.gov.cn", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool ShouldSwapWfsFilterAxes(ImageryServicePreset preset)
    {
        return ShouldSwapWfsCoordinates(preset);
    }

    internal static async Task<string> ResolveWfsDatasetNameAsync(
        ImageryServicePreset preset,
        string? credential = null,
        CancellationToken cancellationToken = default)
    {
        var fallback = GetWfsDatasetName(preset);
        if (preset.ServiceKind != ImageryServiceKind.Wfs || string.IsNullOrWhiteSpace(preset.SourceUrl))
        {
            return fallback;
        }

        var capabilitiesUrl = BuildWfsCapabilitiesUrl(preset.SourceUrl, credential);

        try
        {
            var capabilitiesText = await WfsCapabilitiesHttpClient.GetStringAsync(capabilitiesUrl, cancellationToken);
            var featureTypeName = ResolveWfsDatasetNameFromCapabilities(capabilitiesText, preset);
            if (!string.IsNullOrWhiteSpace(featureTypeName))
            {
                return featureTypeName;
            }
        }
        catch
        {
            // Keep the add flow resilient. We fall back to the preset reference code.
        }

        return fallback;
    }

    internal static string BuildLyrxText(ImageryServicePreset preset, string layerName, string description, string url)
    {
        return LyrxTemplate
            .Replace("__LAYER_NAME__", layerName, StringComparison.Ordinal)
            .Replace("__DESCRIPTION__", description, StringComparison.Ordinal)
            .Replace("__URL__", url, StringComparison.Ordinal)
            .Replace("__URL_XML__", url.Replace("&", "&amp;", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    internal static string BuildStatusSummary(ImageryModuleSettings settings)
    {
        var tiandituCount = settings.EffectiveTianDiTuKeys.Count(IsValidCredential);
        var jilinCount = settings.EffectiveJilinApiKeys.Count(IsValidCredential);
        var directCount = Presets.Count(preset => preset.CredentialKind == ImageryCredentialKind.None);
        return $"天地图密钥 {tiandituCount} 组，吉林一号密钥 {jilinCount} 组，免密服务 {directCount} 项";
    }

    internal static string BuildThumbnailUrl(ImageryServicePreset preset, string credential)
    {
        return preset.ServiceKind switch
        {
            ImageryServiceKind.TileTemplate when !string.IsNullOrWhiteSpace(credential) => BuildTilePreviewUrl(preset, credential),
            ImageryServiceKind.DirectTileTemplate => BuildDirectTilePreviewUrl(preset.PreviewUrl ?? preset.SourceUrl),
            ImageryServiceKind.MapServer => BuildMapServerPreviewUrl(preset.PreviewUrl ?? preset.SourceUrl),
            ImageryServiceKind.Wmts => preset.PreviewUrl ?? string.Empty,
            ImageryServiceKind.Wfs => preset.PreviewUrl ?? string.Empty,
            _ => string.Empty
        };
    }

    private static string BuildTilePreviewUrl(ImageryServicePreset preset, string credential)
    {
        var (level, row, col) = GetPreviewTilePosition();
        return preset.CredentialKind switch
        {
            ImageryCredentialKind.TianDiTu => $"https://t0.tianditu.gov.cn/{preset.ReferenceCode}_w/wmts?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetTile&LAYER={preset.ReferenceCode}&STYLE=default&FORMAT=tiles&TILEMATRIXSET=w&TILEMATRIX={level}&TILEROW={row}&TILECOL={col}&tk={credential}",
            ImageryCredentialKind.JilinOne => $"https://api.jl1mall.com/getMap?TileMatrix={level}&TileCol={col}&TileRow={row}&sch=wmts&route=1&mk={preset.ReferenceCode}&tk={credential}",
            _ => string.Empty
        };
    }

    private static string BuildDirectTilePreviewUrl(string sourceUrl)
    {
        return sourceUrl
            .Replace("{x}", "0", StringComparison.Ordinal)
            .Replace("{y}", "0", StringComparison.Ordinal)
            .Replace("{z}", "1", StringComparison.Ordinal);
    }

    private static string BuildWfsCapabilitiesUrl(string sourceUrl, string? credential = null)
    {
        var normalized = sourceUrl.Trim().TrimEnd('?', '&');
        var separator = normalized.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var url = $"{normalized}{separator}service=WFS&request=GetCapabilities&version=2.0.0";

        if (!string.IsNullOrWhiteSpace(credential))
        {
            url += $"&tk={Uri.EscapeDataString(credential.Trim())}";
        }

        return url;
    }

    private static string? ResolveWfsDatasetNameFromCapabilities(string capabilitiesText, ImageryServicePreset preset)
    {
        if (string.IsNullOrWhiteSpace(capabilitiesText))
        {
            return null;
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(capabilitiesText, LoadOptions.None);
        }
        catch
        {
            return null;
        }

        var featureTypes = document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "FeatureType", StringComparison.OrdinalIgnoreCase))
            .Select(featureType => new
            {
                Name = GetWfsChildValue(featureType, "Name"),
                Title = GetWfsChildValue(featureType, "Title")
            })
            .Where(featureType => !string.IsNullOrWhiteSpace(featureType.Name))
            .ToList();

        if (featureTypes.Count == 0)
        {
            return null;
        }

        var referenceCode = preset.ReferenceCode?.Trim() ?? string.Empty;
        var displayName = preset.DisplayName?.Trim() ?? string.Empty;

        var match = featureTypes.FirstOrDefault(featureType =>
            IsWfsFeatureTypeMatch(featureType.Name!, featureType.Title, referenceCode, displayName));
        if (!string.IsNullOrWhiteSpace(match?.Name))
        {
            return match.Name;
        }

        if (featureTypes.Count == 1)
        {
            return featureTypes[0].Name;
        }

        match = featureTypes.FirstOrDefault(featureType =>
            string.Equals(GetWfsShortName(featureType.Name!), displayName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(match?.Name))
        {
            return match.Name;
        }

        return null;
    }

    private static bool IsWfsFeatureTypeMatch(string name, string? title, string referenceCode, string displayName)
    {
        if (string.Equals(name, referenceCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(title, referenceCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(title, displayName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var shortName = GetWfsShortName(name);
        return string.Equals(shortName, referenceCode, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(shortName, displayName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetWfsChildValue(XElement featureType, string childName)
    {
        return featureType
            .Elements()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, childName, StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim() ?? string.Empty;
    }

    private static string GetWfsShortName(string name)
    {
        var colonIndex = name.IndexOf(':');
        return colonIndex >= 0 && colonIndex < name.Length - 1
            ? name[(colonIndex + 1)..]
            : name;
    }

    private static string BuildMapServerPreviewUrl(string sourceUrl)
    {
        var normalized = sourceUrl.TrimEnd('/');
        return $"{normalized}/export?bbox=-180,-85,180,85&bboxSR=4326&size=256,144&format=png32&f=image";
    }

    private static string BuildWaybackPreviewUrl(string mapLayerId)
    {
        var (level, row, col) = GetPreviewTilePosition();
        return $"https://wayback.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/GoogleMapsCompatible/MapServer/tile/{mapLayerId}/{level}/{row}/{col}";
    }

    private static (int Level, int Row, int Col) GetPreviewTilePosition()
    {
        const int level = 5;
        const double longitude = 104.0;
        const double latitude = 35.0;

        var tileCount = Math.Pow(2, level);
        var col = (int)Math.Floor((longitude + 180.0) / 360.0 * tileCount);
        var latitudeRadians = latitude * Math.PI / 180.0;
        var row = (int)Math.Floor((1.0 - Math.Log(Math.Tan(latitudeRadians) + 1.0 / Math.Cos(latitudeRadians)) / Math.PI) / 2.0 * tileCount);
        return (level, row, col);
    }

    private static string BuildTianDiTuUrl(string serviceCode, string tk)
    {
        return $"https://t0.tianditu.gov.cn/{serviceCode}_w/wmts?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetTile&LAYER={serviceCode}&STYLE=default&FORMAT=tiles&TILEMATRIXSET=w&TILEMATRIX={{level}}&TILEROW={{row}}&TILECOL={{col}}&tk={tk}";
    }

    private static string BuildJilinUrl(string mk, string api)
    {
        return $"https://api.jl1mall.com/getMap?TileMatrix={{level}}&TileCol={{col}}&TileRow={{row}}&sch=wmts&route=1&mk={mk}&tk={api}";
    }

    private static bool IsValidCredential(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Length != 32)
        {
            return false;
        }

        foreach (var ch in normalized)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveProviderLabel(string sourceUrl, ImageryCredentialKind credentialKind, out string providerLabel)
    {
        if (TryResolveProviderLabelFromUrl(sourceUrl, out providerLabel))
        {
            return true;
        }

        providerLabel = credentialKind switch
        {
            ImageryCredentialKind.TianDiTu => "天地图",
            ImageryCredentialKind.JilinOne => "吉林一号",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(providerLabel);
    }

    private static bool TryResolveProviderLabelFromUrl(string sourceUrl, out string providerLabel)
    {
        providerLabel = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(sourceUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("tianditu.gov.cn", StringComparison.Ordinal))
        {
            providerLabel = "天地图";
            return true;
        }

        if (host.Contains("jl1mall.com", StringComparison.Ordinal))
        {
            providerLabel = "吉林一号";
            return true;
        }

        if (host.Contains("arcgisonline.com", StringComparison.Ordinal) ||
            host.Contains("arcgis.com", StringComparison.Ordinal))
        {
            providerLabel = "Esri";
            return true;
        }

        if (host.Contains("autonavi.com", StringComparison.Ordinal))
        {
            providerLabel = "高德";
            return true;
        }

        if (host.Contains("sinica.edu.tw", StringComparison.Ordinal))
        {
            providerLabel = "中研院";
            return true;
        }

        if (host.Contains("baidu.com", StringComparison.Ordinal))
        {
            providerLabel = "百度";
            return true;
        }

        return false;
    }

    private static string InferContentTypeLabel(ImageryServicePreset preset)
    {
        if (preset.LayerRole == ImageryLayerRole.Annotation)
        {
            return "注记";
        }

        if (preset.LayerRole == ImageryLayerRole.Terrain)
        {
            return "地形";
        }

        if (preset.LayerRole == ImageryLayerRole.Boundary)
        {
            return "边界";
        }

        var category = preset.Category?.Trim() ?? string.Empty;
        var displayName = preset.DisplayName?.Trim() ?? string.Empty;
        var referenceCode = preset.ReferenceCode?.Trim() ?? string.Empty;

        if (category.Contains("道路", StringComparison.OrdinalIgnoreCase) ||
            displayName.Contains("道路", StringComparison.OrdinalIgnoreCase) ||
            referenceCode.Equals("style8", StringComparison.OrdinalIgnoreCase))
        {
            return "道路地图";
        }

        if (category.Contains("影像", StringComparison.OrdinalIgnoreCase) ||
            displayName.Contains("影像", StringComparison.OrdinalIgnoreCase) ||
            displayName.Contains("航照", StringComparison.OrdinalIgnoreCase) ||
            displayName.Contains("空照", StringComparison.OrdinalIgnoreCase) ||
            referenceCode.Contains("aerialphoto", StringComparison.OrdinalIgnoreCase) ||
            referenceCode.Contains("airphoto", StringComparison.OrdinalIgnoreCase))
        {
            return "历史影像";
        }

        if (category.Contains("历史", StringComparison.OrdinalIgnoreCase) ||
            displayName.Contains("Wayback", StringComparison.OrdinalIgnoreCase) ||
            referenceCode.StartsWith("WB_", StringComparison.OrdinalIgnoreCase))
        {
            return "历史底图";
        }

        if (category.Contains("影像", StringComparison.OrdinalIgnoreCase) ||
            displayName.Contains("影像", StringComparison.OrdinalIgnoreCase))
        {
            return "影像底图";
        }

        if (category.Contains("底图", StringComparison.OrdinalIgnoreCase))
        {
            return "底图";
        }

        if (category.Contains("专题", StringComparison.OrdinalIgnoreCase))
        {
            return "专题图";
        }

        return string.IsNullOrWhiteSpace(category) ? "其他/未识别" : category;
    }

    private static int GetProviderGroupOrder(string providerLabel)
    {
        return providerLabel switch
        {
            "天地图" => 10,
            "吉林一号" => 20,
            "Esri" => 30,
            "中研院" => 40,
            "高德" => 50,
            "百度" => 60,
            _ => 999
        };
    }

    private static int GetContentTypeGroupOrder(string contentTypeLabel)
    {
        return contentTypeLabel switch
        {
            "矢量底图" => 10,
            "影像底图" => 20,
            "注记" => 30,
            "地形" => 40,
            "边界" => 50,
            "年度影像" => 60,
            "历史影像" => 70,
            "历史底图" => 80,
            "道路地图" => 90,
            "要素服务" => 100,
            "底图" => 110,
            "专题图" => 120,
            _ => 999
        };
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private static string BuildSeedHash(string seed)
    {
        var normalized = seed.Trim();
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }

    private static ImageryServicePreset CreateTilePreset(
        string id,
        string provider,
        string displayName,
        string category,
        string summary,
        string tag,
        string symbol,
        string referenceCode,
        ImageryCredentialKind credentialKind,
        ImageryLayerRole layerRole,
        int sortOrder,
        Brush accentBrush,
        Brush accentSoftBrush)
    {
        return new ImageryServicePreset(
            id,
            provider,
            displayName,
            category,
            summary,
            tag,
            symbol,
            referenceCode,
            ImageryServiceKind.TileTemplate,
            credentialKind,
            layerRole,
            sortOrder,
            string.Empty,
            null,
            accentBrush,
            accentSoftBrush);
    }

    private static ImageryServicePreset CreateDirectTilePreset(
        string id,
        string provider,
        string displayName,
        string category,
        string summary,
        string tag,
        string symbol,
        string referenceCode,
        string sourceUrl,
        string previewUrl,
        ImageryLayerRole layerRole,
        int sortOrder,
        Brush accentBrush,
        Brush accentSoftBrush)
    {
        return new ImageryServicePreset(
            id,
            provider,
            displayName,
            category,
            summary,
            tag,
            symbol,
            referenceCode,
            ImageryServiceKind.DirectTileTemplate,
            ImageryCredentialKind.None,
            layerRole,
            sortOrder,
            sourceUrl,
            previewUrl,
            accentBrush,
            accentSoftBrush);
    }

    private static ImageryServicePreset CreateMapServerPreset(
        string id,
        string provider,
        string displayName,
        string category,
        string summary,
        string tag,
        string symbol,
        string sourceUrl,
        string previewUrl,
        ImageryLayerRole layerRole,
        int sortOrder,
        Brush accentBrush,
        Brush accentSoftBrush)
    {
        return new ImageryServicePreset(
            id,
            provider,
            displayName,
            category,
            summary,
            tag,
            symbol,
            sourceUrl,
            ImageryServiceKind.MapServer,
            ImageryCredentialKind.None,
            layerRole,
            sortOrder,
            sourceUrl,
            previewUrl,
            accentBrush,
            accentSoftBrush);
    }

    private static ImageryServicePreset CreateWmtsPreset(
        string id,
        string provider,
        string displayName,
        string category,
        string summary,
        string tag,
        string symbol,
        string layerName,
        string sourceUrl,
        string? previewUrl,
        ImageryLayerRole layerRole,
        int sortOrder,
        Brush accentBrush,
        Brush accentSoftBrush)
    {
        return new ImageryServicePreset(
            id,
            provider,
            displayName,
            category,
            summary,
            tag,
            symbol,
            layerName,
            ImageryServiceKind.Wmts,
            ImageryCredentialKind.None,
            layerRole,
            sortOrder,
            sourceUrl,
            previewUrl,
            accentBrush,
            accentSoftBrush);
    }

    private static ImageryServicePreset CreateWfsPreset(
        string id,
        string provider,
        string displayName,
        string category,
        string summary,
        string tag,
        string symbol,
        string layerName,
        string sourceUrl,
        string? previewUrl,
        ImageryLayerRole layerRole,
        int sortOrder,
        Brush accentBrush,
        Brush accentSoftBrush)
    {
        return new ImageryServicePreset(
            id,
            provider,
            displayName,
            category,
            summary,
            tag,
            symbol,
            layerName,
            ImageryServiceKind.Wfs,
            ImageryCredentialKind.TianDiTu,
            layerRole,
            sortOrder,
            sourceUrl,
            previewUrl,
            accentBrush,
            accentSoftBrush);
    }

    private const string LyrxTemplate = """
{
  "type" : "CIMLayerDocument",
  "version" : "3.6.0",
  "build" : 59527,
  "layers" : [
    "CIMPATH=__/_________________.json"
  ],
  "layerDefinitions" : [
    {
      "type" : "CIMTiledServiceLayer",
      "name" : "__LAYER_NAME__",
      "uRI" : "CIMPATH=__/_________________.json",
      "sourceModifiedTime" : {
        "type" : "TimeInstant"
      },
      "metadataURI" : "CIMPATH=Metadata/ae504da5a5825db5eea27508716ddfeb.xml",
      "useSourceMetadata" : true,
      "description" : "__DESCRIPTION__",
      "layerElevation" : {
        "type" : "CIMLayerElevationSurface",
        "elevationSurfaceLayerURI" : "CIMPATH=__/1f9bcb6655dc4beebb687066c7ccc8ef.json"
      },
      "expanded" : true,
      "layerType" : "Operational",
      "layerScaleVisibilityOptions" : {
        "type" : "CIMLayerScaleVisibilityOptions",
        "showLayerAtAllScales" : true
      },
      "showLegends" : true,
      "transparency" : 30,
      "visibility" : true,
      "displayCacheType" : "Permanent",
      "maxDisplayCacheAge" : 5,
      "showPopups" : true,
      "serviceLayerID" : -1,
      "refreshRate" : -1,
      "refreshRateUnit" : "esriTimeUnitsSeconds",
      "blendingMode" : "Alpha",
      "allowDrapingOnIntegratedMesh" : true,
      "serviceConnection" : {
        "type" : "CIMStandardServiceConnection",
        "description" : "__DESCRIPTION__",
        "serviceType" : "1",
        "url" : "__URL__"
      }
    }
  ],
  "binaryReferences" : [
    {
      "type" : "CIMBinaryReference",
      "uRI" : "CIMPATH=Metadata/ae504da5a5825db5eea27508716ddfeb.xml",
      "data" : "<?xml version=\"1.0\"?>\r\n<metadata xml:lang=\"zh\"><Esri><CreaDate>20260318</CreaDate><CreaTime>12000000</CreaTime><ArcGISFormat>1.0</ArcGISFormat><SyncOnce>TRUE</SyncOnce></Esri><dataIdInfo><idCitation><resTitle>__LAYER_NAME__</resTitle></idCitation><idAbs>__DESCRIPTION__</idAbs><idCredit></idCredit><idPurp></idPurp><resConst><Consts><useLimit></useLimit></Consts></resConst></dataIdInfo></metadata>\r\n"
    }
  ],
  "rGBColorProfile" : "sRGB IEC61966-2.1",
  "cMYKColorProfile" : "U.S. Web Coated (SWOP) v2",
  "elevationSurfaceLayerDefinitions" : [
    {
      "type" : "CIMElevationSurfaceLayer",
      "name" : "鍦伴潰",
      "uRI" : "CIMPATH=__/1f9bcb6655dc4beebb687066c7ccc8ef.json",
      "sourceModifiedTime" : {
        "type" : "TimeInstant"
      },
      "useSourceMetadata" : true,
      "description" : "鍦伴潰",
      "expanded" : true,
      "layer3DProperties" : {
        "type" : "CIM3DLayerProperties",
        "castShadows" : true,
        "isLayerLit" : true,
        "layerFaceCulling" : "None",
        "preloadTextureCutoffHigh" : 0.29999999999999999,
        "preloadTextureCutoffLow" : 0.59999999999999998,
        "textureCutoffHigh" : 0.29999999999999999,
        "textureCutoffLow" : 0.59999999999999998,
        "useCompressedTextures" : true,
        "verticalExaggeration" : 1,
        "exaggerationMode" : "ScaleZ",
        "lighting" : "OneSideDataNormal",
        "optimizeMarkerTransparency" : true
      },
      "layerType" : "Operational",
      "showLegends" : false,
      "visibility" : true,
      "displayCacheType" : "Permanent",
      "maxDisplayCacheAge" : 5,
      "showPopups" : true,
      "serviceLayerID" : -1,
      "refreshRate" : -1,
      "refreshRateUnit" : "esriTimeUnitsSeconds",
      "blendingMode" : "Alpha",
      "allowDrapingOnIntegratedMesh" : true,
      "elevationMode" : "BaseGlobeSurface",
      "verticalExaggeration" : 1,
      "color" : {
        "type" : "CIMRGBColor",
        "values" : [
          255,
          255,
          255,
          100
        ]
      },
      "surfaceTINShadingMode" : "Smooth"
    }
  ]
}
""";
}
