namespace GisProAddinWorkbench.AddIn.Guides;

public sealed record GuideImageData(
    string Title,
    string Subtitle,
    IReadOnlyList<string> Steps,
    string Footer,
    string? StaticImagePath = null);
