using ArcGIS.Core.Geometry;
using GisProAddinWorkbench.AddIn.Models;
using GisProAddinWorkbench.AddIn.Services;

namespace GisProAddinWorkbench.AddIn.Tools;

internal sealed class AmapMapWorkbenchTool : MapWorkbenchToolBase
{
    protected override string ToolName => nameof(AmapMapWorkbenchTool);

    protected override string JumpTargetName => "高德地图作业台";

    protected override async Task<MapWorkspaceRequest> BuildWorkspaceRequestAsync(MapPoint mapPoint)
    {
        var (wgsLon, wgsLat) = await MapJumpToolHelpers.ProjectToWgs84Async(mapPoint);
        var (gcjLon, gcjLat) = CoordinateTransforms.Wgs84ToGcj02(wgsLon, wgsLat);
        return MapJumpPageBuilder.BuildAmapWorkspaceRequest(wgsLon, wgsLat, gcjLon, gcjLat);
    }
}
