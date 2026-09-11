using System.Globalization;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Services;

internal sealed class BaiduResearchProvider : ResearchProviderBase
{
    public override string ProviderName => "百度";

    public override int ProviderRank => 1;

    public override async Task<ResearchTaskResult> SearchPoiAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var requestUri = BuildUri(
                "https://api.map.baidu.com/place/v2/search",
                new Dictionary<string, string>
                {
                    ["query"] = query.Keyword,
                    ["region"] = query.RegionNameOrCode,
                    ["page_num"] = Math.Max(query.PageIndex - 1, 0).ToString(CultureInfo.InvariantCulture),
                    ["page_size"] = query.PageSize.ToString(CultureInfo.InvariantCulture),
                    ["scope"] = "2",
                    ["output"] = "json",
                    ["ak"] = apiKey
                });

            using var document = await GetJsonAsync(requestUri, cancellationToken);
            var root = document.RootElement;
            if (GetDouble(root, "status") is not 0)
            {
                return new ResearchTaskResult(
                    [],
                    [],
                    [$"{ProviderName} POI 接口返回失败: {GetString(root, "message")}"],
                    $"{ProviderName} POI 查询失败");
            }

            var records = new List<ResearchRecord>();
            foreach (var result in root.GetProperty("results").EnumerateArray())
            {
                var location = result.GetProperty("location");
                var bdLongitude = GetDouble(location, "lng");
                var bdLatitude = GetDouble(location, "lat");
                if (bdLongitude is null || bdLatitude is null)
                {
                    continue;
                }

                var (longitude, latitude) = CoordinateTransforms.Bd09ToWgs84(bdLongitude.Value, bdLatitude.Value);
                records.Add(
                    new ResearchRecord(
                        ProviderName,
                        ProviderRank,
                        query.TaskType.ToString(),
                        GetString(result, "name"),
                        GetString(result, "address"),
                        CombineRegion(GetString(result, "province"), GetString(result, "city"), GetString(result, "area")),
                        GetString(result, "adcode"),
                        longitude,
                        latitude,
                        "success",
                        query.SourceQuery,
                        DateTimeOffset.Now,
                        ToRawPayload(result),
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
                "https://api.map.baidu.com/geocoding/v3/",
                new Dictionary<string, string>
                {
                    ["address"] = query.RegionNameOrCode,
                    ["output"] = "json",
                    ["ak"] = apiKey
                });

            using var document = await GetJsonAsync(requestUri, cancellationToken);
            var root = document.RootElement;
            if (GetDouble(root, "status") is not 0)
            {
                return new ResearchTaskResult(
                    [],
                    [],
                    [$"{ProviderName} 行政区核对失败: {GetString(root, "msg")}"],
                    $"{ProviderName} 行政区查询失败");
            }

            var result = root.GetProperty("result");
            var location = result.GetProperty("location");
            var bdLongitude = GetDouble(location, "lng");
            var bdLatitude = GetDouble(location, "lat");
            double? longitude = null;
            double? latitude = null;
            if (bdLongitude is not null && bdLatitude is not null)
            {
                (longitude, latitude) = CoordinateTransforms.Bd09ToWgs84(bdLongitude.Value, bdLatitude.Value);
            }

            var record = new ResearchRecord(
                ProviderName,
                ProviderRank,
                query.TaskType.ToString(),
                query.RegionNameOrCode,
                string.Empty,
                query.RegionNameOrCode,
                string.Empty,
                longitude,
                latitude,
                "need_review",
                query.SourceQuery,
                DateTimeOffset.Now,
                ToRawPayload(result),
                null);

            return new ResearchTaskResult(
                [record],
                [$"{ProviderName} 首版不返回可直接入库的行政区边界，已生成复核记录。"],
                [],
                $"{ProviderName} 返回 1 条行政区核对记录");
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
                "https://api.map.baidu.com/geocoding/v3/",
                new Dictionary<string, string>
                {
                    ["address"] = query.Address,
                    ["city"] = query.RegionNameOrCode,
                    ["output"] = "json",
                    ["ak"] = apiKey
                });

            using var document = await GetJsonAsync(requestUri, cancellationToken);
            var root = document.RootElement;
            if (GetDouble(root, "status") is not 0)
            {
                return new ResearchTaskResult(
                    [],
                    [],
                    [$"{ProviderName} 编码接口返回失败: {GetString(root, "msg")}"],
                    $"{ProviderName} 地址编码失败");
            }

            var result = root.GetProperty("result");
            var location = result.GetProperty("location");
            var bdLongitude = GetDouble(location, "lng");
            var bdLatitude = GetDouble(location, "lat");
            if (bdLongitude is null || bdLatitude is null)
            {
                return new ResearchTaskResult([], [], [$"{ProviderName} 未返回有效坐标。"], $"{ProviderName} 地址编码失败");
            }

            var (longitude, latitude) = CoordinateTransforms.Bd09ToWgs84(bdLongitude.Value, bdLatitude.Value);
            var record = new ResearchRecord(
                ProviderName,
                ProviderRank,
                query.TaskType.ToString(),
                query.Address,
                query.Address,
                query.RegionNameOrCode,
                string.Empty,
                longitude,
                latitude,
                result.TryGetProperty("precise", out var preciseElement) && preciseElement.ToString() == "1" ? "success" : "fuzzy",
                query.SourceQuery,
                DateTimeOffset.Now,
                ToRawPayload(result),
                new ResearchGeometry(
                    ResearchGeometryType.Point,
                    new CoordinatePair(longitude, latitude),
                    null));

            return new ResearchTaskResult([record], [], [], $"{ProviderName} 返回 1 条编码结果");
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
                "https://api.map.baidu.com/reverse_geocoding/v3/",
                new Dictionary<string, string>
                {
                    ["location"] = $"{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}",
                    ["coordtype"] = "wgs84ll",
                    ["output"] = "json",
                    ["extensions_town"] = "true",
                    ["ak"] = apiKey
                });

            using var document = await GetJsonAsync(requestUri, cancellationToken);
            var root = document.RootElement;
            if (GetDouble(root, "status") is not 0)
            {
                return new ResearchTaskResult(
                    [],
                    [],
                    [$"{ProviderName} 逆编码接口返回失败: {GetString(root, "message")}"],
                    $"{ProviderName} 逆编码失败");
            }

            var result = root.GetProperty("result");
            var addressComponent = result.GetProperty("addressComponent");
            var record = new ResearchRecord(
                ProviderName,
                ProviderRank,
                query.TaskType.ToString(),
                GetString(result, "formatted_address"),
                GetString(result, "formatted_address"),
                CombineRegion(GetString(addressComponent, "province"), GetString(addressComponent, "city"), GetString(addressComponent, "district")),
                string.Empty,
                longitude,
                latitude,
                "success",
                query.SourceQuery,
                DateTimeOffset.Now,
                ToRawPayload(result),
                new ResearchGeometry(
                    ResearchGeometryType.Point,
                    new CoordinatePair(longitude, latitude),
                    null));

            return new ResearchTaskResult([record], [], [], $"{ProviderName} 返回 1 条逆编码结果");
        }
        catch (Exception exception)
        {
            return CreateFailure($"{ProviderName} 逆编码异常", exception);
        }
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
