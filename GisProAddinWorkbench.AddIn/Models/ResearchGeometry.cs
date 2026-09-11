namespace GisProAddinWorkbench.AddIn.Models;

internal enum ResearchGeometryType
{
    Point = 1,
    Polygon = 2
}

internal sealed record CoordinatePair(double Longitude, double Latitude);

internal sealed record ResearchGeometry(
    ResearchGeometryType GeometryType,
    CoordinatePair? Point,
    IReadOnlyList<IReadOnlyList<CoordinatePair>>? Rings);
