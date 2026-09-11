namespace GisProAddinWorkbench.AddIn.Modules.Info;

internal sealed record InfoBulletin(
    string Title,
    string Source,
    DateTimeOffset PublishTime,
    bool IsPinned);
