using System.Text.Json.Serialization;
using GisProAddinWorkbench.AddIn.Core.Modules;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal sealed class ImageryModuleSettings : IModuleSettings
{
    public List<string> TianDiTuBaseKeys { get; set; } = new(ImageryCredentialDefaults.TianDiTuKeys);

    public List<string> TianDiTuSupplementalKeys { get; set; } = [];

    public List<string> JilinApiBaseKeys { get; set; } = new(ImageryCredentialDefaults.JilinApiKeys);

    public List<string> JilinApiSupplementalKeys { get; set; } = [];

    [JsonPropertyName("TianDiTuKeys")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LegacyTianDiTuKeys { get; set; }

    [JsonPropertyName("JilinApiKeys")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LegacyJilinApiKeys { get; set; }

    public List<string> AddedPresetIds { get; set; } = [];

    public string LastSelectedPresetId { get; set; } = "tdt-vec";

    public string LastFilterKey { get; set; } = "all";

    public string LastProviderFilterKey { get; set; } = "all";

    public string LastContentTypeFilterKey { get; set; } = "all";

    public string LastClassificationMode { get; set; } = "provider";

    public string LastLocalPath { get; set; } = string.Empty;

    public string LastServiceUrl { get; set; } = string.Empty;

    public bool AutoRenameLayer { get; set; } = true;

    public string LastSelectedToolId { get; set; } = "imagery-load";

    public string WfsConnectionMode { get; set; } = "Snapshot";

    public int WfsMaxFeatures { get; set; } = 3000;

    public int WfsPageSize { get; set; }

    public bool WfsUseSpatialExtent { get; set; }

    public string WfsExtentSource { get; set; } = "CurrentView";

    public string WfsExtentLayerName { get; set; } = string.Empty;

    public double? WfsCustomMinX { get; set; }

    public double? WfsCustomMinY { get; set; }

    public double? WfsCustomMaxX { get; set; }

    public double? WfsCustomMaxY { get; set; }

    public bool WfsSwapXY { get; set; } = false;

    public bool WfsSwapXYFilter { get; set; }

    [JsonIgnore]
    public IReadOnlyList<string> EffectiveTianDiTuKeys => MergeKeyLists(
        TianDiTuSupplementalKeys,
        TianDiTuBaseKeys,
        LegacyTianDiTuKeys);

    [JsonIgnore]
    public IReadOnlyList<string> EffectiveJilinApiKeys => MergeKeyLists(
        JilinApiSupplementalKeys,
        JilinApiBaseKeys,
        LegacyJilinApiKeys);

    public bool HasCredentialFor(ImageryCredentialKind credentialKind)
    {
        return credentialKind switch
        {
            ImageryCredentialKind.None => true,
            ImageryCredentialKind.TianDiTu => EffectiveTianDiTuKeys.Any(IsUsableCredential),
            ImageryCredentialKind.JilinOne => EffectiveJilinApiKeys.Any(IsUsableCredential),
            _ => false
        };
    }

    internal static List<string> NormalizeKeys(IEnumerable<string>? values)
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

    private static bool IsUsableCredential(string? value)
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
