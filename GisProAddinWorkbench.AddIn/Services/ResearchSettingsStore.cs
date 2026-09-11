using System.Text.Json;
using System.IO;
using ArcGIS.Desktop.Core;

namespace GisProAddinWorkbench.AddIn.Services;

internal static class ResearchSettingsStore
{
    private const string SettingsFileName = "research-settings.json";

    internal static string GetSettingsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GuotuGiserToolbox");
    }

    internal static string GetSettingsPath()
    {
        return Path.Combine(GetSettingsDirectory(), SettingsFileName);
    }

    internal static void EnsureTemplateExists()
    {
        var directory = GetSettingsDirectory();
        Directory.CreateDirectory(directory);

        var settingsPath = GetSettingsPath();
        if (File.Exists(settingsPath))
        {
            return;
        }

        var template = CreateDefaultSettings();

        var json = JsonSerializer.Serialize(
            template,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(settingsPath, json);
    }

    internal static async Task<ResearchSettings> LoadAsync()
    {
        var settingsPath = GetSettingsPath();
        if (!File.Exists(settingsPath))
        {
            return CreateDefaultSettings();
        }

        await using var stream = File.OpenRead(settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<ResearchSettings>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        var normalized = settings ?? CreateDefaultSettings();
        var seedKeys = SurveyToolRuntime.LoadSeedKeys();

        var baiduBase = ResolveBaseKeys(
            normalized.BaiduAkBase,
            normalized.LegacyBaiduAk,
            seedKeys.BaiduAk);
        var baiduSupplemental = NormalizeSupplementalKeys(
            normalized.BaiduAkSupplemental,
            baiduBase);

        var amapBase = ResolveBaseKeys(
            normalized.AmapKeyBase,
            normalized.LegacyAmapKey,
            seedKeys.AmapKey);
        var amapSupplemental = NormalizeSupplementalKeys(
            normalized.AmapKeySupplemental,
            amapBase);

        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in SurveyToolCatalog.BuildDefaultMappings())
        {
            mappings[pair.Key] = pair.Value;
        }

        SurveyToolCatalog.ApplyLegacyMappings(normalized.PytToolMappings, mappings);

        foreach (var pair in normalized.PytToolMappings)
        {
            mappings[pair.Key] = SurveyToolRuntime.NormalizeConfiguredToolPath(pair.Value);
        }

        foreach (var key in mappings.Keys.ToList())
        {
            mappings[key] = SurveyToolRuntime.NormalizeConfiguredToolPath(mappings[key]);
        }

        return new ResearchSettings
        {
            BaiduAkBase = baiduBase.ToArray(),
            BaiduAkSupplemental = baiduSupplemental.ToArray(),
            AmapKeyBase = amapBase.ToArray(),
            AmapKeySupplemental = amapSupplemental.ToArray(),
            LegacyBaiduAk = null,
            LegacyAmapKey = null,
            DefaultOutputGdb = normalized.DefaultOutputGdb,
            RequestTimeoutSeconds = normalized.RequestTimeoutSeconds,
            DefaultPageSize = normalized.DefaultPageSize,
            UiDensityPercent = normalized.UiDensityPercent,
            UiLeftPaneWidthPercent = normalized.UiLeftPaneWidthPercent,
            UiRightPaneMinWidthPercent = normalized.UiRightPaneMinWidthPercent,
            PytToolMappings = mappings
        };
    }

    internal static async Task EnsureTemplateExistsAsync()
    {
        EnsureTemplateExists();
        await Task.CompletedTask;
    }

    internal static async Task SaveAsync(ResearchSettings settings)
    {
        var directory = GetSettingsDirectory();
        Directory.CreateDirectory(directory);

        var sanitized = new ResearchSettings
        {
            BaiduAkBase = ResearchSettings.NormalizeKeyList(settings.BaiduAkBase).ToArray(),
            BaiduAkSupplemental = NormalizeSupplementalKeys(
                settings.BaiduAkSupplemental,
                ResearchSettings.NormalizeKeyList(settings.BaiduAkBase)).ToArray(),
            AmapKeyBase = ResearchSettings.NormalizeKeyList(settings.AmapKeyBase).ToArray(),
            AmapKeySupplemental = NormalizeSupplementalKeys(
                settings.AmapKeySupplemental,
                ResearchSettings.NormalizeKeyList(settings.AmapKeyBase)).ToArray(),
            LegacyBaiduAk = null,
            LegacyAmapKey = null,
            DefaultOutputGdb = settings.DefaultOutputGdb,
            RequestTimeoutSeconds = settings.RequestTimeoutSeconds,
            DefaultPageSize = settings.DefaultPageSize,
            UiDensityPercent = settings.UiDensityPercent,
            UiLeftPaneWidthPercent = settings.UiLeftPaneWidthPercent,
            UiRightPaneMinWidthPercent = settings.UiRightPaneMinWidthPercent,
            PytToolMappings = settings.PytToolMappings.Count == 0
                ? new Dictionary<string, string>(SurveyToolCatalog.BuildDefaultMappings(), StringComparer.OrdinalIgnoreCase)
                : settings.PytToolMappings.ToDictionary(
                    pair => pair.Key,
                    pair => SurveyToolRuntime.ToPersistedConfiguredToolPath(pair.Value),
                    StringComparer.OrdinalIgnoreCase)
        };

        var settingsPath = GetSettingsPath();
        await using var stream = File.Create(settingsPath);
        await JsonSerializer.SerializeAsync(
            stream,
            sanitized,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }

    internal static string GetDefaultOutputGdb()
    {
        return Project.Current?.DefaultGeodatabasePath ?? string.Empty;
    }

    private static ResearchSettings CreateDefaultSettings()
    {
        var seedKeys = SurveyToolRuntime.LoadSeedKeys();
        return new ResearchSettings
        {
            DefaultOutputGdb = GetDefaultOutputGdb(),
            BaiduAkBase = seedKeys.BaiduAk,
            AmapKeyBase = seedKeys.AmapKey,
            BaiduAkSupplemental = [],
            AmapKeySupplemental = [],
            LegacyBaiduAk = null,
            LegacyAmapKey = null
        };
    }

    private static List<string> ResolveBaseKeys(
        IEnumerable<string>? configuredBase,
        IEnumerable<string>? legacyBase,
        IReadOnlyCollection<string> seedBase)
    {
        var baseKeys = ResearchSettings.NormalizeKeyList(configuredBase);
        if (baseKeys.Count == 0 && legacyBase is not null)
        {
            baseKeys = ResearchSettings.NormalizeKeyList(legacyBase);
        }

        if (baseKeys.Count == 0)
        {
            baseKeys = ResearchSettings.NormalizeKeyList(seedBase);
        }

        return baseKeys;
    }

    private static List<string> NormalizeSupplementalKeys(
        IEnumerable<string>? supplementalKeys,
        IReadOnlyCollection<string> baseKeys)
    {
        var normalized = ResearchSettings.NormalizeKeyList(supplementalKeys);
        if (normalized.Count == 0 || baseKeys.Count == 0)
        {
            return normalized;
        }

        return normalized
            .Where(value => baseKeys.All(existing => !existing.Equals(value, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
