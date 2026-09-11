namespace GisProAddinWorkbench.AddIn.Models;

internal sealed record BatchGeocodeRequest(
    string InputTable,
    string AddressField,
    string? CityField,
    ResearchProviderType ProviderType,
    GeocodeSpeedLevel SpeedLevel,
    GeocodeOutputCoordSystem OutputCoordSystem,
    bool UseInMemoryWorkspace,
    string OutputGdb,
    string OutputFeatureClassName,
    int RequestTimeoutSeconds);
