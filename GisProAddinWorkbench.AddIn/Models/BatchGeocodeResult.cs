namespace GisProAddinWorkbench.AddIn.Models;

internal sealed record BatchGeocodeResult(
    string OutputFeatureClassPath,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> Warnings);
