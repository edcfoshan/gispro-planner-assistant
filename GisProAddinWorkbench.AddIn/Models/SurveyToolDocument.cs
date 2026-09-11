using System.Text.Json.Serialization;

namespace GisProAddinWorkbench.AddIn.Models;

internal sealed class SurveyToolDocument
{
    public SurveyToolItem[] Tools { get; init; } = [];
}

internal sealed class SurveyToolItem
{
    public string GroupId { get; init; } = string.Empty;

    public string GroupLabel { get; init; } = string.Empty;

    public string ToolId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Badge { get; init; } = string.Empty;

    public string StatusLabel { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Scenario { get; init; } = string.Empty;

    public string Inputs { get; init; } = string.Empty;

    public string Outputs { get; init; } = string.Empty;

    public string KeyParameters { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string[] FlowSteps { get; init; } = [];

    public string IconKey { get; init; } = string.Empty;

    public string ToolPathKey { get; init; } = string.Empty;

    public string ToolClassName { get; init; } = string.Empty;

    public string AccentHex { get; init; } = "#2563EB";

    public bool IsExperimental { get; init; }

    [JsonIgnore]
    public string IllustrationPath { get; set; } = string.Empty;

    [JsonIgnore]
    public string GuideImagePath { get; set; } = string.Empty;
}
