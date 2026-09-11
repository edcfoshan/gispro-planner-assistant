using System.Globalization;

namespace GisProAddinWorkbench.AddIn.Services;

internal static class BaiduStreetViewPageBuilder
{
    private const string MarkerSource = "webapp.gispro.addinworkbench";

    internal static string BuildMarkerUrl(double bdLongitude, double bdLatitude)
    {
        return BuildMarkerUrl(bdLongitude, bdLatitude, null);
    }

    internal static string BuildMarkerUrl(double bdLongitude, double bdLatitude, string? cacheBust)
    {
        var lng = bdLongitude.ToString("F8", CultureInfo.InvariantCulture);
        var lat = bdLatitude.ToString("F8", CultureInfo.InvariantCulture);
        var title = Uri.EscapeDataString("点击位置");
        var src = Uri.EscapeDataString(MarkerSource);

        var url =
            $"https://api.map.baidu.com/marker?location={lat},{lng}" +
            $"&title={title}&content={title}&output=html&coord_type=bd09ll&zoom=18&src={src}";

        if (!string.IsNullOrWhiteSpace(cacheBust))
        {
            url += $"#ts={Uri.EscapeDataString(cacheBust)}";
        }

        return url;
    }

    internal static string BuildPublicStreetViewUrl(double bdLongitude, double bdLatitude, string? ak = null, string? panoId = null)
    {
        var src = Uri.EscapeDataString(MarkerSource);

        string url;
        if (!string.IsNullOrWhiteSpace(panoId))
        {
            url = $"https://api.map.baidu.com/pano/?pid={Uri.EscapeDataString(panoId)}&src={src}";
        }
        else
        {
            var lng = bdLongitude.ToString("F8", CultureInfo.InvariantCulture);
            var lat = bdLatitude.ToString("F8", CultureInfo.InvariantCulture);
            url = $"https://api.map.baidu.com/pano/?x={lng}&y={lat}&coord_type=bd09ll&src={src}";
        }

        if (!string.IsNullOrWhiteSpace(ak))
        {
            url += $"&ak={Uri.EscapeDataString(ak.Trim())}";
        }

        return url;
    }

    internal static string BuildMarkerWorkspaceHtml(
        double wgsLongitude,
        double wgsLatitude,
        double bdLongitude,
        double bdLatitude,
        string cacheBust)
    {
        _ = wgsLongitude;
        _ = wgsLatitude;
        return BuildRedirectHtml(
            BuildMarkerUrl(bdLongitude, bdLatitude, cacheBust),
            BuildPublicStreetViewUrl(bdLongitude, bdLatitude));
    }

    internal static string Build(
        string ak,
        double wgsLongitude,
        double wgsLatitude,
        double bdLongitude,
        double bdLatitude)
    {
        _ = wgsLongitude;
        _ = wgsLatitude;
        return BuildRedirectHtml(
            BuildMarkerUrl(bdLongitude, bdLatitude, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)),
            BuildPublicStreetViewUrl(bdLongitude, bdLatitude, ak));
    }

    private static string BuildRedirectHtml(string mapUrl, string streetViewUrl)
    {
        return "<!DOCTYPE html>\n" +
               "<html lang=\"zh-CN\">\n" +
               "<head>\n" +
               "  <meta charset=\"utf-8\" />\n" +
               "  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\n" +
               "  <title>百度地图定位</title>\n" +
               "  <script>\n" +
               "    window.addEventListener('load', function () {\n" +
               "      window.setTimeout(function () {\n" +
               $"        window.location.replace('{mapUrl}');\n" +
               "      }, 30);\n" +
               "    });\n" +
               "  </script>\n" +
               "</head>\n" +
               "<body>\n" +
               $"  <a href=\"{mapUrl}\">打开百度地图</a>\n" +
               $"  <a href=\"{streetViewUrl}\" target=\"_blank\">浏览器打开街景</a>\n" +
               "</body>\n" +
               "</html>";
    }
}
