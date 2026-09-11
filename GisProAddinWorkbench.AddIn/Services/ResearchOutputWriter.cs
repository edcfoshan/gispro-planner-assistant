using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Services;

internal sealed class ResearchOutputWriter
{
    internal async Task<ResearchWriteResult> WriteAsync(
        ResearchQuery query,
        IReadOnlyList<ResearchRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return new ResearchWriteResult([], ["本次任务没有可写出的记录。"]);
        }

        if (string.IsNullOrWhiteSpace(query.OutputWorkspace) || !Directory.Exists(query.OutputWorkspace))
        {
            throw new InvalidOperationException("输出 geodatabase 不存在，请先选择有效的 .gdb 路径。");
        }

        var outputWorkspace = query.OutputWorkspace;
        var tempRoot = Path.Combine(Path.GetTempPath(), "GuotuGiserToolbox", "research");
        Directory.CreateDirectory(tempRoot);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var baseName = SanitizeName($"survey_{query.TaskType.ToString().ToLowerInvariant()}_{timestamp}");
        var csvPath = Path.Combine(tempRoot, $"{baseName}.csv");
        await File.WriteAllTextAsync(csvPath, BuildCsv(records), new UTF8Encoding(true), cancellationToken);

        var artifacts = new List<ResearchOutputArtifact>();
        var warnings = new List<string>();

        var tableName = $"{baseName}_tbl";
        await ExecuteToolAsync(
            "conversion.TableToTable",
            Geoprocessing.MakeValueArray(csvPath, outputWorkspace, tableName),
            cancellationToken);
        artifacts.Add(new ResearchOutputArtifact("结果表", Path.Combine(outputWorkspace, tableName)));

        var pointRecords = records.Where(record => record.Geometry?.GeometryType == ResearchGeometryType.Point).ToList();
        if (pointRecords.Count > 0)
        {
            var pointName = $"{baseName}_pt";
            await ExecuteToolAsync(
                "management.XYTableToPoint",
                Geoprocessing.MakeValueArray(
                    csvPath,
                    Path.Combine(outputWorkspace, pointName),
                    "lon",
                    "lat",
                    SpatialReferences.WGS84.Wkt),
                cancellationToken);
            artifacts.Add(new ResearchOutputArtifact("点要素", Path.Combine(outputWorkspace, pointName)));
        }

        var polygonRecords = records.Where(record => record.Geometry?.GeometryType == ResearchGeometryType.Polygon).ToList();
        if (polygonRecords.Count > 0)
        {
            var polygonName = $"{baseName}_poly";
            var geoJsonPath = Path.Combine(tempRoot, $"{polygonName}.geojson");
            await File.WriteAllTextAsync(geoJsonPath, BuildGeoJson(polygonRecords), new UTF8Encoding(false), cancellationToken);

            await ExecuteToolAsync(
                "conversion.JSONToFeatures",
                Geoprocessing.MakeValueArray(
                    geoJsonPath,
                    Path.Combine(outputWorkspace, polygonName),
                    "POLYGON"),
                cancellationToken);

            artifacts.Add(new ResearchOutputArtifact("面要素", Path.Combine(outputWorkspace, polygonName)));
        }
        else if (query.TaskType == ResearchTaskType.District)
        {
            warnings.Add("本次行政区结果没有可直接入库的边界要素，仅输出复核记录表。");
        }

        return new ResearchWriteResult(artifacts, warnings);
    }

    private static async Task ExecuteToolAsync(string toolName, IReadOnlyList<string> parameters, CancellationToken cancellationToken)
    {
        var flags = GPExecuteToolFlags.AddOutputsToMap | GPExecuteToolFlags.GPThread;
        var result = await Geoprocessing.ExecuteToolAsync(toolName, parameters, null, null, flags);
        cancellationToken.ThrowIfCancellationRequested();

        if (result.IsFailed)
        {
            throw new InvalidOperationException($"{toolName} 执行失败: {result.ErrorMessages}");
        }
    }

    private static string BuildCsv(IEnumerable<ResearchRecord> records)
    {
        var builder = new StringBuilder();
        builder.AppendLine("provider,provider_rank,task_type,name,address,region,adcode,lon,lat,match_status,source_query,created_at,raw_payload");

        foreach (var record in records)
        {
            builder.AppendJoin(
                ',',
                EscapeCsv(record.Provider),
                record.ProviderRank.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(record.TaskType),
                EscapeCsv(record.Name),
                EscapeCsv(record.Address),
                EscapeCsv(record.Region),
                EscapeCsv(record.Adcode),
                record.Longitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                record.Latitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                EscapeCsv(record.MatchStatus),
                EscapeCsv(record.SourceQuery),
                EscapeCsv(record.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
                EscapeCsv(record.RawPayload));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildGeoJson(IEnumerable<ResearchRecord> polygonRecords)
    {
        var features = polygonRecords.Select(
            record => new Dictionary<string, object?>
            {
                ["type"] = "Feature",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["provider"] = record.Provider,
                    ["provider_rank"] = record.ProviderRank,
                    ["task_type"] = record.TaskType,
                    ["name"] = record.Name,
                    ["address"] = record.Address,
                    ["region"] = record.Region,
                    ["adcode"] = record.Adcode,
                    ["lon"] = record.Longitude,
                    ["lat"] = record.Latitude,
                    ["match_status"] = record.MatchStatus,
                    ["source_query"] = record.SourceQuery,
                    ["created_at"] = record.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                    ["raw_payload"] = record.RawPayload
                },
                ["geometry"] = BuildPolygonGeometry(record.Geometry)
            });

        var featureCollection = new Dictionary<string, object?>
        {
            ["type"] = "FeatureCollection",
            ["features"] = features
        };

        return JsonSerializer.Serialize(featureCollection, new JsonSerializerOptions { WriteIndented = true });
    }

    private static Dictionary<string, object?> BuildPolygonGeometry(ResearchGeometry? geometry)
    {
        var rings = geometry?.Rings?.Select(
            ring => ring.Select(point => new[] { point.Longitude, point.Latitude }).ToList()).ToList()
            ?? [];

        return new Dictionary<string, object?>
        {
            ["type"] = "Polygon",
            ["coordinates"] = rings
        };
    }

    private static string SanitizeName(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value}\"" : value;
    }
}
