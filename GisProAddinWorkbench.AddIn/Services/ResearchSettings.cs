using System.Text.Json.Serialization;

namespace GisProAddinWorkbench.AddIn.Services;

internal sealed class ResearchSettings
{
    public string[] BaiduAkBase { get; set; } = [];

    public string[] BaiduAkSupplemental { get; set; } = [];

    public string[] AmapKeyBase { get; set; } = [];

    public string[] AmapKeySupplemental { get; set; } = [];

    [JsonPropertyName("BaiduAk")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? LegacyBaiduAk { get; set; }

    [JsonPropertyName("AmapKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? LegacyAmapKey { get; set; }

    public string DefaultOutputGdb { get; set; } = string.Empty;

    public int RequestTimeoutSeconds { get; set; } = 30;

    public int DefaultPageSize { get; set; } = 20;

    public int UiDensityPercent { get; set; } = 100;

    public int UiLeftPaneWidthPercent { get; set; } = 100;

    public int UiRightPaneMinWidthPercent { get; set; } = 100;

    public Dictionary<string, string> PytToolMappings { get; set; } =
        new(SurveyToolCatalog.BuildDefaultMappings(), StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public IReadOnlyList<string> EffectiveBaiduAk => MergeKeyLists(BaiduAkSupplemental, BaiduAkBase, LegacyBaiduAk);

    [JsonIgnore]
    public IReadOnlyList<string> EffectiveAmapKey => MergeKeyLists(AmapKeySupplemental, AmapKeyBase, LegacyAmapKey);

    internal static List<string> NormalizeKeyList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static List<string> MergeKeyLists(
        IEnumerable<string>? baseKeys,
        IEnumerable<string>? supplementalKeys,
        IEnumerable<string>? legacyKeys = null)
    {
        var merged = new List<string>();
        AppendDistinct(merged, baseKeys);
        AppendDistinct(merged, supplementalKeys);
        AppendDistinct(merged, legacyKeys);
        return merged;
    }

    private static void AppendDistinct(
        ICollection<string> target,
        IEnumerable<string>? values)
    {
        if (values is null)
        {
            return;
        }

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalized = value.Trim();
            if (target.Any(existing => existing.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            target.Add(normalized);
        }
    }
}
