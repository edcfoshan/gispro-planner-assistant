namespace GisProAddinWorkbench.AddIn.Models;

internal sealed record ResearchTaskResult(
    IReadOnlyList<ResearchRecord> Records,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    string RequestSummary);
