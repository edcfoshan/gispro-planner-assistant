using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Services;

internal interface IResearchProvider
{
    string ProviderName { get; }

    int ProviderRank { get; }

    Task<ResearchTaskResult> SearchPoiAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken);

    Task<ResearchTaskResult> QueryDistrictAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken);

    Task<ResearchTaskResult> GeocodeAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken);

    Task<ResearchTaskResult> ReverseGeocodeAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken);
}
