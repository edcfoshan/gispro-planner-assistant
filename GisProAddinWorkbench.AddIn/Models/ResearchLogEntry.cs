namespace GisProAddinWorkbench.AddIn.Models;

internal sealed record ResearchLogEntry(DateTimeOffset Timestamp, string Level, string Message);
