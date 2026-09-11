
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
using System.Text.Json;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using GisProAddinWorkbench.AddIn.Models;
using Microsoft.VisualBasic.FileIO;

namespace GisProAddinWorkbench.AddIn.Services;

internal sealed class BatchGeocodeService
{
    private static readonly HttpClient HttpClient = new();
    private static int _baiduKeyCursor;
    private static int _amapKeyCursor;

    internal async Task<IReadOnlyList<string>> GetInputFieldsAsync(
        string inputTable,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputTable))
        {
            return [];
        }

        var csvPath = BuildTempCsvPath("schema");
        await ExportTableToCsvAsync(inputTable, csvPath, "1=0", cancellationToken);
        var (headers, _) = ReadCsv(csvPath);
        return headers;
    }

    internal async Task<BatchGeocodeResult> ExecuteAsync(
        BatchGeocodeRequest request,
        ResearchSettings settings,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var inputCsvPath = BuildTempCsvPath("input");
        await ExportTableToCsvAsync(request.InputTable, inputCsvPath, null, cancellationToken);

        var (headers, rows) = ReadCsv(inputCsvPath);
        if (!headers.Any(header => header.Equals(request.AddressField, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"地址字段不存在：{request.AddressField}");
        }

        var copyFields = headers
            .Where(CanCopyInputField)
            .ToList();

        var maxWorkers = CalculateMaxWorkers(request.SpeedLevel, request.ProviderType, settings);
        var warnings = new List<string>();
        var successRows = new List<Dictionary<string, string>>(rows.Count);
        var warningLock = new object();
        var successLock = new object();

        using var semaphore = new SemaphoreSlim(maxWorkers, maxWorkers);
        var tasks = rows.Select(
            (row, rowIndex) => ProcessRowWithSemaphoreAsync(
                semaphore,
                rowIndex,
                row,
                copyFields,
                request,
                settings,
                warnings,
                successRows,
                warningLock,
                successLock,
                cancellationToken));

        var rowResults = await Task.WhenAll(tasks);
        var failureCount = rowResults.Count(result => !result);

        var outputCsvPath = BuildTempCsvPath("output");
        await WriteOutputCsvAsync(outputCsvPath, copyFields, successRows, cancellationToken);

        var outputWorkspace = request.UseInMemoryWorkspace ? "in_memory" : request.OutputGdb.Trim();
        var outputFeatureClassName = request.OutputFeatureClassName.Trim();
        var outputFeatureClassPath = Path.Combine(outputWorkspace, outputFeatureClassName);

        await DeleteIfExistsAsync(outputFeatureClassPath, cancellationToken);

        var xField = request.OutputCoordSystem == GeocodeOutputCoordSystem.Wgs84
            ? "final_lng_wgs84"
            : "final_lng_gcj";
        var yField = request.OutputCoordSystem == GeocodeOutputCoordSystem.Wgs84
            ? "final_lat_wgs84"
            : "final_lat_gcj";

        await ExecuteToolAsync(
            "management.XYTableToPoint",
            Geoprocessing.MakeValueArray(
                outputCsvPath,
                outputFeatureClassPath,
                xField,
                yField,
                SpatialReferences.WGS84.Wkt),
            cancellationToken,
            addOutputsToMap: true);

        if (warnings.Count > 50)
        {
            warnings = warnings.Take(50).ToList();
            warnings.Add("告警过多，仅展示前 50 条。");
        }

        return new BatchGeocodeResult(
            outputFeatureClassPath,
            successRows.Count,
            failureCount,
            warnings);
    }

    private static async Task<bool> ProcessRowWithSemaphoreAsync(
        SemaphoreSlim semaphore,
        int rowIndex,
        IReadOnlyDictionary<string, string> row,
        IReadOnlyList<string> copyFields,
        BatchGeocodeRequest request,
        ResearchSettings settings,
        ICollection<string> warnings,
        ICollection<Dictionary<string, string>> successRows,
        object warningLock,
        object successLock,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var rowResult = await ProcessOneRowAsync(rowIndex, row, copyFields, request, settings, cancellationToken);
            if (!rowResult.Success)
            {
                if (!string.IsNullOrWhiteSpace(rowResult.Warning))
                {
                    lock (warningLock)
                    {
                        warnings.Add($"第 {rowIndex + 1} 行：{rowResult.Warning}");
                    }
                }

                return false;
            }

            lock (successLock)
            {
                successRows.Add(rowResult.OutputRow);
            }

            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task<RowProcessResult> ProcessOneRowAsync(
        int rowIndex,
        IReadOnlyDictionary<string, string> row,
        IReadOnlyList<string> copyFields,
        BatchGeocodeRequest request,
        ResearchSettings settings,
        CancellationToken cancellationToken)
    {
        var address = GetFieldValue(row, request.AddressField).Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            return RowProcessResult.CreateFail("地址为空。");
        }

        var city = string.IsNullOrWhiteSpace(request.CityField)
            ? string.Empty
            : GetFieldValue(row, request.CityField).Trim();

        var baiduResult = BaiduGeocodeResult.Empty();
        var amapResult = AmapGeocodeResult.Empty();

            switch (request.ProviderType)
            {
            case ResearchProviderType.Baidu:
                baiduResult = await GeocodeWithBaiduAsync(address, city, settings.EffectiveBaiduAk, request.RequestTimeoutSeconds, cancellationToken);
                break;
            case ResearchProviderType.Amap:
                amapResult = await GeocodeWithAmapAsync(address, city, settings.EffectiveAmapKey, request.RequestTimeoutSeconds, cancellationToken);
                break;
            default:
                baiduResult = await GeocodeWithBaiduAsync(address, city, settings.EffectiveBaiduAk, request.RequestTimeoutSeconds, cancellationToken);
                amapResult = await GeocodeWithAmapAsync(address, city, settings.EffectiveAmapKey, request.RequestTimeoutSeconds, cancellationToken);
                break;
            }

        var finalResult = ResolveFinalResult(request.ProviderType, baiduResult, amapResult);
        if (!finalResult.HasValue ||
            finalResult.Value.LongitudeGcj is null ||
            finalResult.Value.LatitudeGcj is null)
        {
            var reason = request.ProviderType switch
            {
                ResearchProviderType.Baidu => baiduResult.Reason,
                ResearchProviderType.Amap => amapResult.Reason,
                _ => $"百度：{baiduResult.Reason}；高德：{amapResult.Reason}"
            };

            return RowProcessResult.CreateFail(string.IsNullOrWhiteSpace(reason) ? "地理编码失败。" : reason);
        }

        var finalValue = finalResult.Value;

        var outputRow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["src_oid"] = rowIndex.ToString(CultureInfo.InvariantCulture)
        };

        foreach (var copyField in copyFields)
        {
            outputRow[$"IN_{copyField}"] = GetFieldValue(row, copyField);
        }

        outputRow["final_source"] = finalValue.Source;
        outputRow["final_city"] = city;
        outputRow["final_output_wgs84"] = request.OutputCoordSystem == GeocodeOutputCoordSystem.Wgs84 ? "是" : "否";
        outputRow["final_lng_gcj"] = ToNumberText(finalValue.LongitudeGcj);
        outputRow["final_lat_gcj"] = ToNumberText(finalValue.LatitudeGcj);
        outputRow["final_lng_wgs84"] = ToNumberText(finalValue.LongitudeWgs84);
        outputRow["final_lat_wgs84"] = ToNumberText(finalValue.LatitudeWgs84);
        outputRow["final_need_check"] = finalValue.NeedCheck;
        outputRow["final_check_reason"] = finalValue.Reason;

        outputRow["baidu_ok"] = baiduResult.Ok ? "是" : "否";
        outputRow["baidu_status"] = baiduResult.Status;
        outputRow["baidu_msg"] = baiduResult.Message;
        outputRow["baidu_key"] = baiduResult.MaskedKey;
        outputRow["baidu_lng_gcj"] = ToNumberText(baiduResult.LongitudeGcj);
        outputRow["baidu_lat_gcj"] = ToNumberText(baiduResult.LatitudeGcj);
        outputRow["baidu_precise"] = baiduResult.Precise;
        outputRow["baidu_conf"] = baiduResult.Confidence;
        outputRow["baidu_level"] = baiduResult.Level;
        outputRow["baidu_need_check"] = baiduResult.NeedCheck;
        outputRow["baidu_check_reason"] = baiduResult.Reason;

        outputRow["amap_ok"] = amapResult.Ok ? "是" : "否";
        outputRow["amap_status"] = amapResult.Status;
        outputRow["amap_info"] = amapResult.Info;
        outputRow["amap_infocode"] = amapResult.InfoCode;
        outputRow["amap_key"] = amapResult.MaskedKey;
        outputRow["amap_count"] = amapResult.Count;
        outputRow["amap_address"] = amapResult.FormattedAddress;
        outputRow["amap_lng_gcj"] = ToNumberText(amapResult.LongitudeGcj);
        outputRow["amap_lat_gcj"] = ToNumberText(amapResult.LatitudeGcj);
        outputRow["amap_level"] = amapResult.Level;
        outputRow["amap_need_check"] = amapResult.NeedCheck;
        outputRow["amap_check_reason"] = amapResult.Reason;

        return RowProcessResult.CreateSuccess(outputRow);
    }
    private static async Task<BaiduGeocodeResult> GeocodeWithBaiduAsync(
        string address,
        string city,
        IReadOnlyList<string> keys,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var keyPool = FilterKeys(keys);
        if (keyPool.Count == 0)
        {
            return BaiduGeocodeResult.Fail("NO_KEY", "缺少百度密钥。", string.Empty, "是", "缺少百度密钥。");
        }

        BaiduGeocodeResult? lastFailure = null;
        for (var i = 0; i < keyPool.Count; i++)
        {
            var keyIndex = Math.Abs(Interlocked.Increment(ref _baiduKeyCursor)) % keyPool.Count;
            var key = keyPool[keyIndex];

            try
            {
                var requestUri = BuildUri(
                    "https://api.map.baidu.com/geocoding/v3/",
                    new Dictionary<string, string>
                    {
                        ["address"] = address,
                        ["output"] = "json",
                        ["ak"] = key,
                        ["city"] = city
                    });

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
                using var response = await HttpClient.GetAsync(requestUri, timeoutCts.Token);
                var content = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    lastFailure = BaiduGeocodeResult.Fail(
                        ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                        response.ReasonPhrase ?? "HTTP_ERROR",
                        MaskKey(key),
                        "是",
                        $"百度请求失败：{response.ReasonPhrase}");
                    continue;
                }

                using var document = JsonDocument.Parse(content);
                var root = document.RootElement;
                var status = root.TryGetProperty("status", out var statusElement)
                    ? statusElement.ToString()
                    : "-1";
                var message = root.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString() ?? string.Empty
                    : string.Empty;

                if (status != "0")
                {
                    var reason = string.IsNullOrWhiteSpace(message)
                        ? "百度 API 返回错误。"
                        : $"百度 API 返回错误：{message}";
                    lastFailure = BaiduGeocodeResult.Fail(status, message, MaskKey(key), "是", reason);
                    continue;
                }

                if (!root.TryGetProperty("result", out var resultElement) ||
                    !resultElement.TryGetProperty("location", out var locationElement))
                {
                    lastFailure = BaiduGeocodeResult.Fail(status, message, MaskKey(key), "是", "百度未返回坐标。");
                    continue;
                }

                if (!locationElement.TryGetProperty("lng", out var lngElement) ||
                    !locationElement.TryGetProperty("lat", out var latElement) ||
                    !lngElement.TryGetDouble(out var longitudeBd09) ||
                    !latElement.TryGetDouble(out var latitudeBd09))
                {
                    lastFailure = BaiduGeocodeResult.Fail(status, message, MaskKey(key), "是", "百度返回坐标格式异常。");
                    continue;
                }

                var (longitudeGcj, latitudeGcj) = CoordinateTransforms.Bd09ToGcj02(longitudeBd09, latitudeBd09);
                var (longitudeWgs84, latitudeWgs84) = CoordinateTransforms.Bd09ToWgs84(longitudeBd09, latitudeBd09);
                var precise = resultElement.TryGetProperty("precise", out var preciseElement)
                    ? preciseElement.ToString()
                    : string.Empty;
                var confidence = resultElement.TryGetProperty("confidence", out var confidenceElement)
                    ? confidenceElement.ToString()
                    : string.Empty;
                var level = resultElement.TryGetProperty("level", out var levelElement)
                    ? levelElement.GetString() ?? string.Empty
                    : string.Empty;

                var needCheck = "否";
                var reasonText = string.Empty;
                if (double.TryParse(confidence, NumberStyles.Float, CultureInfo.InvariantCulture, out var confValue) &&
                    confValue < 50)
                {
                    needCheck = "是";
                    reasonText = $"百度置信度较低（{confidence}）";
                }

                if (level is "城市" or "省" or "国家")
                {
                    needCheck = "是";
                    reasonText = $"百度匹配等级较低（{level}）";
                }

                return BaiduGeocodeResult.OkResult(
                    status,
                    message,
                    MaskKey(key),
                    longitudeGcj,
                    latitudeGcj,
                    longitudeWgs84,
                    latitudeWgs84,
                    precise,
                    confidence,
                    level,
                    needCheck,
                    reasonText);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                lastFailure = BaiduGeocodeResult.Fail("TIMEOUT", "请求超时", MaskKey(key), "是", "百度请求超时。");
            }
            catch (Exception exception)
            {
                lastFailure = BaiduGeocodeResult.Fail("ERROR", exception.Message, MaskKey(key), "是", $"百度异常：{exception.Message}");
            }
        }

        return lastFailure ??
               BaiduGeocodeResult.Fail("RETRY_EXHAUSTED", "重试耗尽", string.Empty, "是", "百度重试耗尽。");
    }

    private static async Task<AmapGeocodeResult> GeocodeWithAmapAsync(
        string address,
        string city,
        IReadOnlyList<string> keys,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var keyPool = FilterKeys(keys);
        if (keyPool.Count == 0)
        {
            return AmapGeocodeResult.Fail("NO_KEY", "缺少高德密钥。", "NO_KEY", string.Empty, "0", "是", "缺少高德密钥。");
        }

        AmapGeocodeResult? lastFailure = null;
        for (var i = 0; i < keyPool.Count; i++)
        {
            var keyIndex = Math.Abs(Interlocked.Increment(ref _amapKeyCursor)) % keyPool.Count;
            var key = keyPool[keyIndex];

            try
            {
                var requestUri = BuildUri(
                    "https://restapi.amap.com/v3/geocode/geo",
                    new Dictionary<string, string>
                    {
                        ["address"] = address,
                        ["city"] = city,
                        ["key"] = key,
                        ["output"] = "json"
                    });

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
                using var response = await HttpClient.GetAsync(requestUri, timeoutCts.Token);
                var content = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    lastFailure = AmapGeocodeResult.Fail(
                        ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                        response.ReasonPhrase ?? "HTTP_ERROR",
                        "HTTP_ERROR",
                        MaskKey(key),
                        "0",
                        "是",
                        $"高德请求失败：{response.ReasonPhrase}");
                    continue;
                }

                using var document = JsonDocument.Parse(content);
                var root = document.RootElement;
                var status = root.TryGetProperty("status", out var statusElement)
                    ? statusElement.GetString() ?? "0"
                    : "0";
                var info = root.TryGetProperty("info", out var infoElement)
                    ? infoElement.GetString() ?? string.Empty
                    : string.Empty;
                var infoCode = root.TryGetProperty("infocode", out var infoCodeElement)
                    ? infoCodeElement.GetString() ?? string.Empty
                    : string.Empty;

                if (status != "1" || infoCode != "10000")
                {
                    var reason = $"高德 API 返回错误：{info} ({infoCode})";
                    lastFailure = AmapGeocodeResult.Fail(status, info, infoCode, MaskKey(key), "0", "是", reason);
                    continue;
                }

                if (!root.TryGetProperty("geocodes", out var geocodesElement) ||
                    geocodesElement.ValueKind != JsonValueKind.Array)
                {
                    lastFailure = AmapGeocodeResult.Fail(status, info, infoCode, MaskKey(key), "0", "是", "高德未返回 geocodes。");
                    continue;
                }

                var count = geocodesElement.GetArrayLength().ToString(CultureInfo.InvariantCulture);
                if (geocodesElement.GetArrayLength() == 0)
                {
                    lastFailure = AmapGeocodeResult.Fail(status, info, infoCode, MaskKey(key), count, "是", "高德未找到匹配结果。");
                    continue;
                }

                var geocode = geocodesElement[0];
                var location = geocode.TryGetProperty("location", out var locationElement)
                    ? locationElement.GetString() ?? string.Empty
                    : string.Empty;
                var xy = location.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (xy.Length != 2 ||
                    !double.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitudeGcj) ||
                    !double.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitudeGcj))
                {
                    lastFailure = AmapGeocodeResult.Fail(status, info, infoCode, MaskKey(key), count, "是", "高德返回坐标格式异常。");
                    continue;
                }

                var (longitudeWgs84, latitudeWgs84) = CoordinateTransforms.Gcj02ToWgs84(longitudeGcj, latitudeGcj);
                var level = geocode.TryGetProperty("level", out var levelElement)
                    ? levelElement.GetString() ?? string.Empty
                    : string.Empty;
                var formattedAddress = geocode.TryGetProperty("formatted_address", out var addressElement)
                    ? addressElement.GetString() ?? string.Empty
                    : string.Empty;

                var needCheck = level is "province" or "city" ? "是" : "否";
                var reasonText = needCheck == "是"
                    ? $"高德匹配等级较低（{level}）"
                    : string.Empty;

                return AmapGeocodeResult.OkResult(
                    status,
                    info,
                    infoCode,
                    MaskKey(key),
                    count,
                    formattedAddress,
                    longitudeGcj,
                    latitudeGcj,
                    longitudeWgs84,
                    latitudeWgs84,
                    level,
                    needCheck,
                    reasonText);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                lastFailure = AmapGeocodeResult.Fail("TIMEOUT", "请求超时", "TIMEOUT", MaskKey(key), "0", "是", "高德请求超时。");
            }
            catch (Exception exception)
            {
                lastFailure = AmapGeocodeResult.Fail("ERROR", exception.Message, "ERROR", MaskKey(key), "0", "是", $"高德异常：{exception.Message}");
            }
        }

        return lastFailure ??
               AmapGeocodeResult.Fail("RETRY_EXHAUSTED", "重试耗尽", "RETRY_EXHAUSTED", string.Empty, "0", "是", "高德重试耗尽。");
    }
    private static FinalGeocodeResult? ResolveFinalResult(
        ResearchProviderType providerType,
        BaiduGeocodeResult baidu,
        AmapGeocodeResult amap)
    {
        return providerType switch
        {
            ResearchProviderType.Baidu when baidu.Ok && baidu.LongitudeGcj is not null && baidu.LatitudeGcj is not null
                => new FinalGeocodeResult(
                    "Baidu",
                    baidu.LongitudeGcj,
                    baidu.LatitudeGcj,
                    baidu.LongitudeWgs84,
                    baidu.LatitudeWgs84,
                    baidu.NeedCheck,
                    baidu.Reason),
            ResearchProviderType.Amap when amap.Ok && amap.LongitudeGcj is not null && amap.LatitudeGcj is not null
                => new FinalGeocodeResult(
                    "Amap",
                    amap.LongitudeGcj,
                    amap.LatitudeGcj,
                    amap.LongitudeWgs84,
                    amap.LatitudeWgs84,
                    amap.NeedCheck,
                    amap.Reason),
            _ when baidu.Ok && baidu.LongitudeGcj is not null && baidu.LatitudeGcj is not null
                => new FinalGeocodeResult(
                    "Baidu",
                    baidu.LongitudeGcj,
                    baidu.LatitudeGcj,
                    baidu.LongitudeWgs84,
                    baidu.LatitudeWgs84,
                    baidu.NeedCheck,
                    baidu.Reason),
            _ when amap.Ok && amap.LongitudeGcj is not null && amap.LatitudeGcj is not null
                => new FinalGeocodeResult(
                    "Amap",
                    amap.LongitudeGcj,
                    amap.LatitudeGcj,
                    amap.LongitudeWgs84,
                    amap.LatitudeWgs84,
                    amap.NeedCheck,
                    amap.Reason),
            _ => null
        };
    }

    private static int CalculateMaxWorkers(
        GeocodeSpeedLevel speedLevel,
        ResearchProviderType providerType,
        ResearchSettings settings)
    {
        var baiduCount = FilterKeys(settings.EffectiveBaiduAk).Count;
        var amapCount = FilterKeys(settings.EffectiveAmapKey).Count;

        if (providerType == ResearchProviderType.Baidu)
        {
            amapCount = baiduCount;
        }
        else if (providerType == ResearchProviderType.Amap)
        {
            baiduCount = amapCount;
        }

        var maxBaiduQps = Math.Max(1, baiduCount * 30);
        var maxAmapQps = Math.Max(1, amapCount * 30);
        var maxTotalQps = Math.Min(maxBaiduQps, maxAmapQps);

        var multiplier = speedLevel switch
        {
            GeocodeSpeedLevel.Low => 0.25d,
            GeocodeSpeedLevel.Medium => 0.5d,
            GeocodeSpeedLevel.Fast => 0.75d,
            _ => 1.0d
        };

        var targetQps = maxTotalQps * multiplier;
        var workers = (int)Math.Ceiling(targetQps / 30d);
        return Math.Clamp(workers, 1, 64);
    }

    private static bool CanCopyInputField(string fieldName)
    {
        return !fieldName.Equals("OID", StringComparison.OrdinalIgnoreCase) &&
               !fieldName.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase) &&
               !fieldName.Equals("FID", StringComparison.OrdinalIgnoreCase) &&
               !fieldName.Equals("Shape", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFieldValue(IReadOnlyDictionary<string, string> row, string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return string.Empty;
        }

        foreach (var pair in row)
        {
            if (pair.Key.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static async Task ExportTableToCsvAsync(
        string inputTable,
        string outputCsvPath,
        string? whereClause,
        CancellationToken cancellationToken)
    {
        var args = string.IsNullOrWhiteSpace(whereClause)
            ? Geoprocessing.MakeValueArray(inputTable, outputCsvPath)
            : Geoprocessing.MakeValueArray(inputTable, outputCsvPath, whereClause);

        await ExecuteToolAsync("conversion.ExportTable", args, cancellationToken, addOutputsToMap: false);
    }

    private static async Task DeleteIfExistsAsync(string path, CancellationToken cancellationToken)
    {
        var existsResult = await Geoprocessing.ExecuteToolAsync(
            "management.Exists",
            Geoprocessing.MakeValueArray(path),
            null,
            null,
            GPExecuteToolFlags.GPThread);
        cancellationToken.ThrowIfCancellationRequested();

        if (!existsResult.IsFailed &&
            existsResult.ReturnValue is "true" or "True")
        {
            await ExecuteToolAsync(
                "management.Delete",
                Geoprocessing.MakeValueArray(path),
                cancellationToken,
                addOutputsToMap: false);
        }
    }

    private static async Task WriteOutputCsvAsync(
        string csvPath,
        IReadOnlyList<string> copyFields,
        IReadOnlyList<Dictionary<string, string>> rows,
        CancellationToken cancellationToken)
    {
        var headers = new List<string>
        {
            "src_oid"
        };
        headers.AddRange(copyFields.Select(field => $"IN_{field}"));
        headers.AddRange(
        [
            "final_source",
            "final_city",
            "final_output_wgs84",
            "final_lng_gcj",
            "final_lat_gcj",
            "final_lng_wgs84",
            "final_lat_wgs84",
            "final_need_check",
            "final_check_reason",
            "baidu_ok",
            "baidu_status",
            "baidu_msg",
            "baidu_key",
            "baidu_lng_gcj",
            "baidu_lat_gcj",
            "baidu_precise",
            "baidu_conf",
            "baidu_level",
            "baidu_need_check",
            "baidu_check_reason",
            "amap_ok",
            "amap_status",
            "amap_info",
            "amap_infocode",
            "amap_key",
            "amap_count",
            "amap_address",
            "amap_lng_gcj",
            "amap_lat_gcj",
            "amap_level",
            "amap_need_check",
            "amap_check_reason"
        ]);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

        foreach (var row in rows)
        {
            var values = headers.Select(
                header => row.TryGetValue(header, out var value)
                    ? EscapeCsv(value)
                    : string.Empty);
            builder.AppendLine(string.Join(",", values));
        }

        await File.WriteAllTextAsync(csvPath, builder.ToString(), new UTF8Encoding(true), cancellationToken);
    }

    private static (List<string> Headers, List<Dictionary<string, string>> Rows) ReadCsv(string csvPath)
    {
        using var parser = new TextFieldParser(csvPath, Encoding.UTF8)
        {
            HasFieldsEnclosedInQuotes = true
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            return ([], []);
        }

        var headers = parser.ReadFields()?.ToList() ?? [];
        var rows = new List<Dictionary<string, string>>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? [];
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                row[headers[i]] = i < fields.Length ? fields[i] : string.Empty;
            }

            rows.Add(row);
        }

        return (headers, rows);
    }
    private static string BuildTempCsvPath(string prefix)
    {
        var folder = Path.Combine(Path.GetTempPath(), "GuotuGiserToolbox", "batch-geocode");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{prefix}_{Guid.NewGuid():N}.csv");
    }

    private static List<string> FilterKeys(IEnumerable<string>? keys)
    {
        return keys?
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (key.Length <= 8)
        {
            return key;
        }

        return $"{key[..4]}**{key[^4..]}";
    }

    private static string ToNumberText(double? value)
    {
        return value?.ToString("0.########", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string EscapeCsv(string value)
    {
        value ??= string.Empty;
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value}\"" : value;
    }

    private static Uri BuildUri(string baseUri, IReadOnlyDictionary<string, string> query)
    {
        var builder = new UriBuilder(baseUri);
        var queryBuilder = new StringBuilder();
        foreach (var pair in query)
        {
            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            if (queryBuilder.Length > 0)
            {
                queryBuilder.Append('&');
            }

            queryBuilder.Append(WebUtility.UrlEncode(pair.Key));
            queryBuilder.Append('=');
            queryBuilder.Append(WebUtility.UrlEncode(pair.Value));
        }

        builder.Query = queryBuilder.ToString();
        return builder.Uri;
    }

    private static async Task ExecuteToolAsync(
        string toolName,
        IReadOnlyList<string> parameters,
        CancellationToken cancellationToken,
        bool addOutputsToMap)
    {
        var flags = addOutputsToMap
            ? GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddOutputsToMap
            : GPExecuteToolFlags.GPThread;

        var result = await Geoprocessing.ExecuteToolAsync(
            toolName,
            parameters,
            null,
            null,
            flags);
        cancellationToken.ThrowIfCancellationRequested();

        if (result.IsFailed)
        {
            throw new InvalidOperationException($"{toolName} 执行失败：{result.ErrorMessages}");
        }
    }

    private static void ValidateRequest(BatchGeocodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InputTable))
        {
            throw new InvalidOperationException("请先选择输入图层或表。");
        }

        if (string.IsNullOrWhiteSpace(request.AddressField))
        {
            throw new InvalidOperationException("请先选择地址字段。");
        }

        if (string.IsNullOrWhiteSpace(request.OutputFeatureClassName))
        {
            throw new InvalidOperationException("请填写输出要素类名称。");
        }

        if (!request.UseInMemoryWorkspace)
        {
            if (string.IsNullOrWhiteSpace(request.OutputGdb))
            {
                throw new InvalidOperationException("请填写输出 GDB。");
            }

            if (!request.OutputGdb.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("输出 GDB 必须是 .gdb 路径。");
            }
        }
    }

    private readonly record struct RowProcessResult(
        bool Success,
        Dictionary<string, string> OutputRow,
        string Warning)
    {
        internal static RowProcessResult CreateSuccess(Dictionary<string, string> outputRow) =>
            new(true, outputRow, string.Empty);

        internal static RowProcessResult CreateFail(string warning) =>
            new(false, [], warning);
    }

    private readonly record struct FinalGeocodeResult(
        string Source,
        double? LongitudeGcj,
        double? LatitudeGcj,
        double? LongitudeWgs84,
        double? LatitudeWgs84,
        string NeedCheck,
        string Reason);

    private readonly record struct BaiduGeocodeResult(
        bool Ok,
        string Status,
        string Message,
        string MaskedKey,
        double? LongitudeGcj,
        double? LatitudeGcj,
        double? LongitudeWgs84,
        double? LatitudeWgs84,
        string Precise,
        string Confidence,
        string Level,
        string NeedCheck,
        string Reason)
    {
        internal static BaiduGeocodeResult Empty() =>
            new(false, string.Empty, string.Empty, string.Empty, null, null, null, null, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        internal static BaiduGeocodeResult OkResult(
            string status,
            string message,
            string maskedKey,
            double longitudeGcj,
            double latitudeGcj,
            double longitudeWgs84,
            double latitudeWgs84,
            string precise,
            string confidence,
            string level,
            string needCheck,
            string reason) =>
            new(true, status, message, maskedKey, longitudeGcj, latitudeGcj, longitudeWgs84, latitudeWgs84, precise, confidence, level, needCheck, reason);

        internal static BaiduGeocodeResult Fail(
            string status,
            string message,
            string maskedKey,
            string needCheck,
            string reason) =>
            new(false, status, message, maskedKey, null, null, null, null, string.Empty, string.Empty, string.Empty, needCheck, reason);
    }

    private readonly record struct AmapGeocodeResult(
        bool Ok,
        string Status,
        string Info,
        string InfoCode,
        string MaskedKey,
        string Count,
        string FormattedAddress,
        double? LongitudeGcj,
        double? LatitudeGcj,
        double? LongitudeWgs84,
        double? LatitudeWgs84,
        string Level,
        string NeedCheck,
        string Reason)
    {
        internal static AmapGeocodeResult Empty() =>
            new(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null, null, null, null, string.Empty, string.Empty, string.Empty);

        internal static AmapGeocodeResult OkResult(
            string status,
            string info,
            string infoCode,
            string maskedKey,
            string count,
            string formattedAddress,
            double longitudeGcj,
            double latitudeGcj,
            double longitudeWgs84,
            double latitudeWgs84,
            string level,
            string needCheck,
            string reason) =>
            new(true, status, info, infoCode, maskedKey, count, formattedAddress, longitudeGcj, latitudeGcj, longitudeWgs84, latitudeWgs84, level, needCheck, reason);

        internal static AmapGeocodeResult Fail(
            string status,
            string info,
            string infoCode,
            string maskedKey,
            string count,
            string needCheck,
            string reason) =>
            new(false, status, info, infoCode, maskedKey, count, string.Empty, null, null, null, null, string.Empty, needCheck, reason);
    }
}
