using ArcGIS.Core.Geometry;
using GisProAddinWorkbench.AddIn.Services;

namespace GisProAddinWorkbench.AddIn.Tools;

internal sealed class BaiduMapJumpTool : MapJumpToolBase
{
    protected override string ToolName => nameof(BaiduMapJumpTool);

    protected override string JumpTargetName => "百度地图";

    protected override async Task<string> BuildJumpUrlAsync(MapPoint mapPoint)
    {
        var (wgsLon, wgsLat) = await ProjectToWgs84Async(mapPoint);
        var (bdLon, bdLat) = CoordinateTransforms.Wgs84ToBd09(wgsLon, wgsLat);
        return MapJumpPageBuilder.BuildBaiduMapUrl(bdLon, bdLat);
    }
}
