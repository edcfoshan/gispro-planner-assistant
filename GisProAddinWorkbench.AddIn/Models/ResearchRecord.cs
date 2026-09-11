namespace GisProAddinWorkbench.AddIn.Models;

internal sealed record ResearchRecord(
    string Provider,
    int ProviderRank,
    string TaskType,
    string Name,
    string Address,
    string Region,
    string Adcode,
    double? Longitude,
    double? Latitude,
    string MatchStatus,
    string SourceQuery,
    DateTimeOffset CreatedAt,
    string RawPayload,
    ResearchGeometry? Geometry);
