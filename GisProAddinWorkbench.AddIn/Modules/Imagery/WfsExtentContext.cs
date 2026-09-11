using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal sealed record WfsLayerExtentCandidate(string Label, Envelope? Extent);

internal sealed class WfsExtentContext
{
    public Envelope? CurrentVisibleExtent { get; init; }

    public Envelope? AllLayersExtent { get; init; }

    public IReadOnlyList<WfsLayerExtentCandidate> LayerCandidates { get; init; } = [];

    internal static async Task<WfsExtentContext> CreateAsync()
    {
        var currentVisibleExtent = await WfsExtentProjection.ProjectToWgs84Async(MapView.Active?.Extent);

        var rawLayerCandidates = await QueuedTask.Run(() =>
        {
            var map = MapView.Active?.Map;
            if (map is null)
            {
                return Array.Empty<(string Label, Envelope? Extent)>();
            }

            var items = new List<(string Label, Envelope? Extent)>();
            foreach (var layer in map.GetLayersAsFlattenedList())
            {
                if (layer is GroupLayer)
                {
                    continue;
                }

                try
                {
                    var extent = layer.QueryExtent();
                    var label = BuildLayerLabel(layer);
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        items.Add((label.Trim(), extent));
                    }
                }
                catch
                {
                    // Ignore layers that cannot provide an extent.
                }
            }

            return items.ToArray();
        });

        var projectedCandidates = await Task.WhenAll(rawLayerCandidates.Select(async candidate =>
            new WfsLayerExtentCandidate(candidate.Label, await WfsExtentProjection.ProjectToWgs84Async(candidate.Extent))));

        var allLayersExtent = BuildUnionExtent(projectedCandidates.Select(candidate => candidate.Extent));

        return new WfsExtentContext
        {
            CurrentVisibleExtent = currentVisibleExtent,
            AllLayersExtent = allLayersExtent,
            LayerCandidates = projectedCandidates
        };
    }

    internal WfsLayerExtentCandidate? FindLayer(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        return LayerCandidates.FirstOrDefault(candidate =>
            candidate.Label.Equals(label.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildLayerLabel(Layer layer)
    {
        var label = layer.Name?.Trim();
        return string.IsNullOrWhiteSpace(label)
            ? layer.GetType().Name
            : label;
    }

    private static Envelope? BuildUnionExtent(IEnumerable<Envelope?> extents)
    {
        var validExtents = extents.Where(extent => extent is not null).Cast<Envelope>().ToList();
        if (validExtents.Count == 0)
        {
            return null;
        }

        if (validExtents.Count == 1)
        {
            return validExtents[0];
        }

        var minX = validExtents.Min(extent => extent.XMin);
        var minY = validExtents.Min(extent => extent.YMin);
        var maxX = validExtents.Max(extent => extent.XMax);
        var maxY = validExtents.Max(extent => extent.YMax);

        return EnvelopeBuilderEx.CreateEnvelope(minX, minY, maxX, maxY, SpatialReferences.WGS84);
    }
}
