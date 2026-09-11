using System.Globalization;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Services;

internal sealed class AmapResearchProvider : ResearchProviderBase
{
    public override string ProviderName => "高德";

    public override int ProviderRank => 2;

    public override async Task<ResearchTaskResult> SearchPoiAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var requestUri = BuildUri(
                "https://restapi.amap.com/v3/place/text",
                new Dictionary<string, string>
                {
                    ["key"] = apiKey,
                    ["keywords"] = query.Keyword,
                    ["region"] = query.RegionNameOrCode,
                    ["page"] = query.PageIndex.ToString(CultureInfo.InvariantCulture),
                    ["offset"] = query.PageSize.ToString(CultureInfo.InvariantCulture),
                    ["extensions"] = "all"
                });

            using var document = await GetJsonAsync(requestUri, cancellationToken);
            var root = document.RootElement;
            if (GetString(root, "status") != "1")
            {
                return new ResearchTaskResult(
                    [],
                    [],
                    [$"{ProviderName} POI 接口返回失败: {GetString(root, "info")} ({GetString(root, "infocode")})"],
                    $"{ProviderName} POI 查询失败");
            }

            var records = new List<ResearchRecord>();
            foreach (var poi in root.GetProperty("pois").EnumerateArray())
            {
                var location = GetString(poi, "location").Split(',');
                if (location.Length != 2 ||
                    !double.TryParse(location[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var gcjLongitude) ||
                    !double.TryParse(location[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var gcjLatitude))
                {
                    continue;
                }

                var (longitude, latitude) = CoordinateTransforms.Gcj02ToWgs84(gcjLongitude, gcjLatitude);
                records.Add(
                    new ResearchRecord(
                        ProviderName,
                        ProviderRank,
                        query.TaskType.ToString(),
                        GetString(poi, "name"),
                        GetString(poi, "address"),
                        CombineRegion(GetString(poi, "pname"), GetString(poi, "cityname"), GetString(poi, "adname")),
                        GetString(poi, "adcode"),
                        longitude,
                        latitude,
                        "success",
                        query.SourceQuery,
                        DateTimeOffset.Now,
                        ToRawPayload(poi),
                        new ResearchGeometry(
                            ResearchGeometryType.Point,
                            new CoordinatePair(longitude, latitude),
                            null)));
            }

            return new ResearchTaskResult(records, [], [], $"{ProviderName} 返回 {records.Count} 条 POI");
        }
        catch (Exception exception)
        {
            return CreateFailure($"{ProviderName} POI 查询异常", exception);
        }
    }

    public override async Task<ResearchTaskResult> QueryDistrictAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var requestUri = BuildUri(
                "https://restapi.amap.com/v3/config/district",
                new Dictionary<string, string>
                {
                    ["key"] = apiKey,
                    ["keywords"] = query.RegionNameOrCode,
                    ["subdistrict"] = "0",
                    ["extensions"] = "all"
                });

            using var document = await GetJsonAsync(requestUri, cancellationToken);
            var root = document.RootElement;
            if (GetString(root, "status") != "1")
            {
                return new ResearchTaskResult(
                    [],
                    [],
                    [$"{ProviderName} 行政区接口返回失败: {GetString(root, "info")} ({GetString(root, "infocode")})"],
                    $"{ProviderName} 行政区查询失败");
            }

            var records = new List<ResearchRecord>();
            var warnings = new List<string>();
            foreach (var district in root.GetProperty("districts").EnumerateArray())
            {
                ResearchGeometry? geometry = null;
                var polyline = GetString(district, "polyline");
                if (!string.IsNullOrWhiteSpace(polyline))
                {
                    var rings = ParsePolygonRings(polyline);
                    if (rings.Count > 0)
                    {
                        geometry = new ResearchGeometry(ResearchGeometryType.Polygon, null, rings);
                    }
                }

                if (geometry is null)
                {
                    warnings.Add($"{ProviderName} 行政区未返回可入库边界，结果已标记为需复核。");
                }

                double? longitude = null;
                double? latitude = null;
                var center = GetString(district, "center").Split(',');
                if (center.Length == 2 &&
                    double.TryParse(center[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var gcjLongitude) &&
                    double.TryParse(center[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var gcjLatitude))
                {
                    (longitude, latitude) = CoordinateTransforms.Gcj02ToWgs84(gcjLongitude, gcjLatitude);
                }

                records.Add(
                    new ResearchRecord(
                        ProviderName,
                        ProviderRank,
                        query.TaskType.ToString(),
                        GetString(district, "name"),
                        string.Empty,
                        CombineRegion(GetString(district, "province"), GetString(district, "citycode"), GetString(district, "name")),
                        GetString(district, "adcode"),
                        longitude,
                        latitude,
                        geometry is null ? "need_review" : "success",
                        query.SourceQuery,
                        DateTimeOffset.Now,
                        ToRawPayload(district),
                        geometry));
            }

            return new ResearchTaskResult(records, warnings, [], $"{ProviderName} 返回 {records.Count} 条行政区记录");
        }
        catch (Exception exception)
        {
            return CreateFailure($"{ProviderName} 行政区查询异常", exception);
        }
    }

    public override async Task<ResearchTaskResult> GeocodeAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var requestUri = BuildUri(
                "https://restapi.amap.com/v3/geocode/geo",
                new Dictionary<string, string>
                {
                    ["key"] = apiKey,
                    ["address"] = query.Address,
                    ["city"] = query.RegionNameOrCode
                });

            using var document = await GetJsonAsync(requestUri, cancellationToken);
            var root = document.RootElement;
            if (GetString(root, "status") != "1")
            {
                return new ResearchTaskResult(
                    [],
                    [],
                    [$"{ProviderName} 编码接口返回失败: {GetString(root, "info")} ({GetString(root, "infocode")})"],
                    $"{ProviderName} 地址编码失败");
            }

            var records = new List<ResearchRecord>();
            foreach (var geocode in root.GetProperty("geocodes").EnumerateArray())
            {
                var location = GetString(geocode, "location").Split(',');
                if (location.Length != 2 ||
                    !double.TryParse(location[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var gcjLongitude) ||
                    !double.TryParse(location[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var gcjLatitude))
                {
                    continue;
                }

                var (longitude, latitude) = CoordinateTransforms.Gcj02ToWgs84(gcjLongitude, gcjLatitude);
                records.Add(
                    new ResearchRecord(
                        ProviderName,
                        ProviderRank,
                        query.TaskType.ToString(),
                        GetString(geocode, "formatted_address"),
                        query.Address,
                        CombineRegion(GetString(geocode, "province"), GetString(geocode, "city"), GetString(geocode, "district")),
                        GetString(geocode, "adcode"),
                        longitude,
                        latitude,
                        "success",
                        query.SourceQuery,
                        DateTimeOffset.Now,
                        ToRawPayload(geocode),
                        new ResearchGeometry(
                            ResearchGeometryType.Point,
                            new CoordinatePair(longitude, latitude),
                            null)));
            }

            return new ResearchTaskResult(records, [], [], $"{ProviderName} 返回 {records.Count} 条编码结果");
        }
        catch (Exception exception)
        {
            return CreateFailure($"{ProviderName} 地址编码异常", exception);
        }
    }

    public override async Task<ResearchTaskResult> ReverseGeocodeAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var (longitude, latitude) = ParseCoordinates(query.Coordinates);
            var requestUri = BuildUri(
                "https://restapi.amap.com/v3/geocode/regeo",
                new Dictionary<string, string>
                {
                    ["key"] = apiKey,
                    ["location"] = $"{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}",
                    ["extensions"] = "all",
                    ["radius"] = "1000"
                });

            using var document = await GetJsonAsync(requestUri, cancellationToken);
            var root = document.RootElement;
            if (GetString(root, "status") != "1")
            {
                return new ResearchTaskResult(
                    [],
                    [],
                    [$"{ProviderName} 逆编码接口返回失败: {GetString(root, "info")} ({GetString(root, "infocode")})"],
                    $"{ProviderName} 逆编码失败");
            }

            var regeocode = root.GetProperty("regeocode");
            var addressComponent = regeocode.GetProperty("addressComponent");
            var (wgsLongitude, wgsLatitude) = CoordinateTransforms.Gcj02ToWgs84(longitude, latitude);
            var record = new ResearchRecord(
                ProviderName,
                ProviderRank,
                query.TaskType.ToString(),
                GetString(regeocode, "formatted_address"),
                GetString(regeocode, "formatted_address"),
                CombineRegion(GetString(addressComponent, "province"), GetString(addressComponent, "city"), GetString(addressComponent, "district")),
                GetString(addressComponent, "adcode"),
                wgsLongitude,
                wgsLatitude,
                "success",
                query.SourceQuery,
                DateTimeOffset.Now,
                ToRawPayload(regeocode),
                new ResearchGeometry(
                    ResearchGeometryType.Point,
                    new CoordinatePair(wgsLongitude, wgsLatitude),
                    null));

            return new ResearchTaskResult([record], [], [], $"{ProviderName} 返回 1 条逆编码结果");
        }
        catch (Exception exception)
        {
            return CreateFailure($"{ProviderName} 逆编码异常", exception);
        }
    }

    private static List<IReadOnlyList<CoordinatePair>> ParsePolygonRings(string polyline)
    {
        var rings = new List<IReadOnlyList<CoordinatePair>>();
        foreach (var polygonText in polyline.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var points = new List<CoordinatePair>();
            foreach (var pointText in polygonText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var xy = pointText.Split(',');
                if (xy.Length != 2 ||
                    !double.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var gcjLongitude) ||
                    !double.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var gcjLatitude))
                {
                    continue;
                }

                var (longitude, latitude) = CoordinateTransforms.Gcj02ToWgs84(gcjLongitude, gcjLatitude);
                points.Add(new CoordinatePair(longitude, latitude));
            }

            if (points.Count >= 3)
            {
                if (points[0] != points[^1])
                {
                    points.Add(points[0]);
                }

                rings.Add(points);
            }
        }

        return rings;
    }

    private static (double Longitude, double Latitude) ParseCoordinates(string coordinates)
    {
        var parts = coordinates.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude))
        {
            throw new InvalidOperationException("逆编码坐标必须为“经度,纬度”格式。");
        }

        return (longitude, latitude);
    }
}
