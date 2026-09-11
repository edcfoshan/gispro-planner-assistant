using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal static class WfsExtentProjection
{
    internal static async Task<Envelope?> ProjectToWgs84Async(Envelope? extent)
    {
        if (extent is null)
        {
            return null;
        }

        if (extent.SpatialReference is not null && extent.SpatialReference.Equals(SpatialReferences.WGS84))
        {
            return extent;
        }

        try
        {
            return await QueuedTask.Run(() =>
            {
                var projected = GeometryEngine.Instance.Project(extent, SpatialReferences.WGS84);
                return projected?.Extent ?? extent;
            });
        }
        catch
        {
            return extent;
        }
    }
}
