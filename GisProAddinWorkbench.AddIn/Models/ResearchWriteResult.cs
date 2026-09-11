namespace GisProAddinWorkbench.AddIn.Models;

internal sealed record ResearchWriteResult(
    IReadOnlyList<ResearchOutputArtifact> Artifacts,
    IReadOnlyList<string> Warnings);
