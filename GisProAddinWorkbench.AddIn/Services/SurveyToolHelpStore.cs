using System.IO;
using System.Text.Json;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Services;

internal static class SurveyToolHelpStore
{
    private const string FileName = "tool-help.survey.json";

    internal static string GetHelpFilePath()
    {
        return Path.Combine(ResearchSettingsStore.GetSettingsDirectory(), FileName);
    }

    internal static async Task EnsureTemplateExistsAsync()
    {
        var path = GetHelpFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            return;
        }

        var json = JsonSerializer.Serialize(
            SurveyToolCatalog.CreateDefaultDocument(),
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        await File.WriteAllTextAsync(path, json);
    }

    internal static async Task<(IReadOnlyDictionary<string, SurveyToolItem> HelpByToolId, string Warning)> LoadWithFallbackAsync()
    {
        var defaults = BuildDictionary(SurveyToolCatalog.CreateDefaultDocument());
        var path = GetHelpFilePath();

        if (!File.Exists(path))
        {
            return (defaults, "未找到前期调研说明文件，已使用内置说明。");
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<SurveyToolDocument>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (document is null || document.Tools.Length == 0)
            {
                return (defaults, "前期调研说明文件为空，已回退到内置说明。");
            }

            var merged = MergeWithDefaults(defaults, document);
            var hasMissingTools = merged.Count < defaults.Count ||
                defaults.Keys.Any(toolId => !merged.ContainsKey(toolId));

            if (hasMissingTools)
            {
                return (merged, "前期调研说明文件缺少部分工具说明，已自动补齐内置内容。");
            }

            return (merged, string.Empty);
        }
        catch
        {
            return (defaults, "前期调研说明文件格式错误，已回退到内置说明。");
        }
    }

    private static IReadOnlyDictionary<string, SurveyToolItem> MergeWithDefaults(
        IReadOnlyDictionary<string, SurveyToolItem> defaults,
        SurveyToolDocument overrides)
    {
        var merged = defaults.ToDictionary(
            pair => pair.Key,
            pair => Clone(pair.Value),
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in overrides.Tools)
        {
            if (string.IsNullOrWhiteSpace(item.ToolId))
            {
                continue;
            }

            if (!merged.TryGetValue(item.ToolId, out var existing))
            {
                var newItem = Clone(item);
                SurveyToolCatalog.EnrichAssetPaths([newItem]);
                merged[item.ToolId] = newItem;
                continue;
            }

            merged[item.ToolId] = Merge(existing, item);
        }

        return merged;
    }

    private static IReadOnlyDictionary<string, SurveyToolItem> BuildDictionary(SurveyToolDocument document)
    {
        var dictionary = new Dictionary<string, SurveyToolItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in document.Tools)
        {
            if (string.IsNullOrWhiteSpace(tool.ToolId))
            {
                continue;
            }

            dictionary[tool.ToolId] = Clone(tool);
        }

        return dictionary;
    }

    private static SurveyToolItem Merge(SurveyToolItem defaults, SurveyToolItem overrides)
    {
        var merged = new SurveyToolItem
        {
            GroupId = Choose(overrides.GroupId, defaults.GroupId),
            GroupLabel = Choose(overrides.GroupLabel, defaults.GroupLabel),
            ToolId = defaults.ToolId,
            DisplayName = Choose(overrides.DisplayName, defaults.DisplayName),
            Badge = Choose(overrides.Badge, defaults.Badge),
            StatusLabel = Choose(overrides.StatusLabel, defaults.StatusLabel),
            Summary = Choose(overrides.Summary, defaults.Summary),
            Scenario = Choose(overrides.Scenario, defaults.Scenario),
            Inputs = Choose(overrides.Inputs, defaults.Inputs),
            Outputs = Choose(overrides.Outputs, defaults.Outputs),
            KeyParameters = Choose(overrides.KeyParameters, defaults.KeyParameters),
            Notes = Choose(overrides.Notes, defaults.Notes),
            FlowSteps = overrides.FlowSteps.Length > 0 ? overrides.FlowSteps : defaults.FlowSteps,
            IconKey = Choose(overrides.IconKey, defaults.IconKey),
            ToolPathKey = Choose(overrides.ToolPathKey, defaults.ToolPathKey),
            ToolClassName = Choose(overrides.ToolClassName, defaults.ToolClassName),
            AccentHex = Choose(overrides.AccentHex, defaults.AccentHex),
            IsExperimental = overrides.IsExperimental || defaults.IsExperimental
        };

        SurveyToolCatalog.EnrichAssetPaths([merged]);
        return merged;
    }

    private static SurveyToolItem Clone(SurveyToolItem source)
    {
        var clone = new SurveyToolItem
        {
            GroupId = source.GroupId,
            GroupLabel = source.GroupLabel,
            ToolId = source.ToolId,
            DisplayName = source.DisplayName,
            Badge = source.Badge,
            StatusLabel = source.StatusLabel,
            Summary = source.Summary,
            Scenario = source.Scenario,
            Inputs = source.Inputs,
            Outputs = source.Outputs,
            KeyParameters = source.KeyParameters,
            Notes = source.Notes,
            FlowSteps = source.FlowSteps.ToArray(),
            IconKey = source.IconKey,
            ToolPathKey = source.ToolPathKey,
            ToolClassName = source.ToolClassName,
            AccentHex = source.AccentHex,
            IsExperimental = source.IsExperimental
        };

        SurveyToolCatalog.EnrichAssetPaths([clone]);
        return clone;
    }

    private static string Choose(string candidate, string fallback)
    {
        return string.IsNullOrWhiteSpace(candidate) ? fallback : candidate.Trim();
    }
}
