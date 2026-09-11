using ArcGIS.Core.Geometry;
using GisProAddinWorkbench.AddIn.Models;
using GisProAddinWorkbench.AddIn.Services;

namespace GisProAddinWorkbench.AddIn.Tools;

internal sealed class BaiduMapWorkbenchTool : MapWorkbenchToolBase
{
    protected override string ToolName => nameof(BaiduMapWorkbenchTool);

    protected override string JumpTargetName => "百度地图作业台";

    protected override async Task<MapWorkspaceRequest> BuildWorkspaceRequestAsync(MapPoint mapPoint)
    {
        var (wgsLon, wgsLat) = await MapJumpToolHelpers.ProjectToWgs84Async(mapPoint);
        var (bdLon, bdLat) = CoordinateTransforms.Wgs84ToBd09(wgsLon, wgsLat);
        return MapJumpPageBuilder.BuildBaiduWorkspaceRequest(wgsLon, wgsLat, bdLon, bdLat);
    }
}
