namespace GisProAddinWorkbench.AddIn.Models;

internal sealed record ResearchQuery(
    ResearchProviderType ProviderType,
    ResearchTaskType TaskType,
    GeocodeMode GeocodeMode,
    string Keyword,
    string RegionNameOrCode,
    string Address,
    string Coordinates,
    int PageIndex,
    int PageSize,
    int RequestTimeoutSeconds,
    string OutputWorkspace,
    string SourceQuery,
    string TargetSpatialReferenceName);
