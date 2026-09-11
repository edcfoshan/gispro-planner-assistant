using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace GisProAddinWorkbench.AddIn.Tools;

internal static class MapJumpToolHelpers
{
    internal static async Task<(double Longitude, double Latitude)> ProjectToWgs84Async(MapPoint mapPoint)
    {
        return await QueuedTask.Run(() =>
        {
            if (mapPoint.SpatialReference is not null &&
                mapPoint.SpatialReference.Equals(SpatialReferences.WGS84))
            {
                return (mapPoint.X, mapPoint.Y);
            }

            var projected = (MapPoint?)GeometryEngine.Instance.Project(mapPoint, SpatialReferences.WGS84);
            if (projected is null)
            {
                return (mapPoint.X, mapPoint.Y);
            }

            return (projected.X, projected.Y);
        });
    }
}
