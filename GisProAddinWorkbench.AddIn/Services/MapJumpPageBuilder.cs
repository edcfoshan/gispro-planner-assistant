using System.Globalization;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Services;

internal static class MapJumpPageBuilder
{
    private const string BaiduSource = "webapp.gispro.addinworkbench";
    private const string AmapSource = "gispro";

    internal static string BuildBaiduMapUrl(double bdLongitude, double bdLatitude)
    {
        return BuildBaiduMapUrl(
            bdLongitude,
            bdLatitude,
            DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
    }

    internal static string BuildBaiduMapUrl(double bdLongitude, double bdLatitude, string? cacheBust)
    {
        var lng = bdLongitude.ToString("F8", CultureInfo.InvariantCulture);
        var lat = bdLatitude.ToString("F8", CultureInfo.InvariantCulture);
        var title = Uri.EscapeDataString("点击位置");
        var src = Uri.EscapeDataString(BaiduSource);

        var url =
            $"https://api.map.baidu.com/marker?location={lat},{lng}" +
            $"&title={title}&content={title}&output=html&coord_type=bd09ll&zoom=18&src={src}";

        if (!string.IsNullOrWhiteSpace(cacheBust))
        {
            url += $"#ts={Uri.EscapeDataString(cacheBust)}";
        }

        return url;
    }

    internal static string BuildAmapMapUrl(double gcjLongitude, double gcjLatitude)
    {
        var lng = gcjLongitude.ToString("F8", CultureInfo.InvariantCulture);
        var lat = gcjLatitude.ToString("F8", CultureInfo.InvariantCulture);
        var title = Uri.EscapeDataString("点击位置");
        var src = Uri.EscapeDataString(AmapSource);
        return $"https://uri.amap.com/marker?position={lng},{lat}&name={title}&src={src}";
    }

    internal static MapWorkspaceRequest BuildBaiduWorkspaceRequest(
        double wgsLongitude,
        double wgsLatitude,
        double bdLongitude,
        double bdLatitude)
    {
        return new MapWorkspaceRequest(
            MapWorkspaceProvider.Baidu,
            BuildBaiduMapUrl(bdLongitude, bdLatitude),
            "百度地图定位",
            $"WGS84: {FormatCoordinate(wgsLongitude)}, {FormatCoordinate(wgsLatitude)} | BD09: {FormatCoordinate(bdLongitude)}, {FormatCoordinate(bdLatitude)}",
            "已切换到百度地图定位页面，并定位到当前点击位置。");
    }

    internal static MapWorkspaceRequest BuildAmapWorkspaceRequest(
        double wgsLongitude,
        double wgsLatitude,
        double gcjLongitude,
        double gcjLatitude)
    {
        return new MapWorkspaceRequest(
            MapWorkspaceProvider.Amap,
            BuildAmapMapUrl(gcjLongitude, gcjLatitude),
            "高德地图定位",
            $"WGS84: {FormatCoordinate(wgsLongitude)}, {FormatCoordinate(wgsLatitude)} | GCJ02: {FormatCoordinate(gcjLongitude)}, {FormatCoordinate(gcjLatitude)}",
            "已切换到高德地图定位页面，并定位到当前点击位置。");
    }

    private static string FormatCoordinate(double value)
    {
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }
}
