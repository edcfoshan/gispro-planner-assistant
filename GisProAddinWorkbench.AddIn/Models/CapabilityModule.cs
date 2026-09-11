namespace GisProAddinWorkbench.AddIn.Models;

internal sealed record CapabilityModule(
    string Key,
    string Title,
    string Stage,
    string Goal,
    string DeliveryRoute,
    string Inputs,
    string Outputs,
    string Notes,
    string Status);
