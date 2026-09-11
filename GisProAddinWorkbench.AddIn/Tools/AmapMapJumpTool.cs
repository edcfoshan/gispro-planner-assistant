using ArcGIS.Core.Geometry;
using GisProAddinWorkbench.AddIn.Services;

namespace GisProAddinWorkbench.AddIn.Tools;

internal sealed class AmapMapJumpTool : MapJumpToolBase
{
    protected override string ToolName => nameof(AmapMapJumpTool);

    protected override string JumpTargetName => "高德地图";

    protected override async Task<string> BuildJumpUrlAsync(MapPoint mapPoint)
    {
        var (wgsLon, wgsLat) = await ProjectToWgs84Async(mapPoint);
        var (gcjLon, gcjLat) = CoordinateTransforms.Wgs84ToGcj02(wgsLon, wgsLat);
        return MapJumpPageBuilder.BuildAmapMapUrl(gcjLon, gcjLat);
    }
}
