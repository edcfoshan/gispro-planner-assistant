using System.IO;
using System.Globalization;
using System.Text;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.FaceToTxt;

internal sealed class FaceToTxtService
{
    private static readonly SpatialReference Geographic2000 = SpatialReferenceBuilder.CreateSpatialReference(4490);
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    private const string FieldPlaceholder = "(空)";

    internal async Task<FaceToTxtSourceSnapshot> InspectCurrentSelectionAsync()
    {
        try
        {
            return await QueuedTask.Run(InspectCurrentSelectionInternal);
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(FaceToTxtService), "读取当前选择集失败。", exception);
            return FaceToTxtSourceSnapshot.Empty($"读取当前选择集失败：{exception.Message}");
        }
    }

    internal async Task<FaceToTxtSourceSnapshot> InspectDatasetAsync(string catalogPath)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            return FaceToTxtSourceSnapshot.Empty("请先选择要导出的要素类。");
        }

        try
        {
            return await QueuedTask.Run(() => InspectDatasetInternal(catalogPath));
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(FaceToTxtService), "读取浏览数据集失败。", exception);
            return FaceToTxtSourceSnapshot.Empty($"读取数据集失败：{exception.Message}");
        }
    }

    internal async Task<FaceToTxtExportResult> ExportAsync(
        FaceToTxtExportSettings settings,
        FaceToTxtSourceSnapshot snapshot)
    {
        try
        {
            if (!snapshot.HasItems)
            {
                return new FaceToTxtExportResult
                {
                    Success = false,
                    Message = "当前没有可导出的面状要素。"
                };
            }

            if (string.IsNullOrWhiteSpace(settings.OutputDirectory))
            {
                return new FaceToTxtExportResult
                {
                    Success = false,
                    Message = "请先选择输出路径。"
                };
            }

            Directory.CreateDirectory(settings.OutputDirectory);

            var packages = await QueuedTask.Run(() => BuildPackagesInternal(settings, snapshot));
            var outputPaths = new List<string>();
            var warnings = new List<string>();

            foreach (var package in packages)
            {
                await File.WriteAllTextAsync(package.FilePath, package.Content, Utf8WithBom);
                outputPaths.Add(package.FilePath);
                warnings.AddRange(package.Warnings);
            }

            return new FaceToTxtExportResult
            {
                Success = true,
                Message = $"已导出 {outputPaths.Count} 个 TXT 文件。",
                OutputPaths = outputPaths,
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(FaceToTxtService), "导出 TXT 失败。", exception);
            return new FaceToTxtExportResult
            {
                Success = false,
                Message = $"导出失败：{exception.Message}"
            };
        }
    }

    private static FaceToTxtSourceSnapshot InspectCurrentSelectionInternal()
    {
        var activeMap = MapView.Active?.Map;
        if (activeMap is null)
        {
            return FaceToTxtSourceSnapshot.Empty("当前没有活动地图，无法读取选择集。");
        }

        var selection = activeMap.GetSelection().ToDictionary();
        if (selection.Count == 0)
        {
            return FaceToTxtSourceSnapshot.Empty("当前地图没有选择任何要素。");
        }

        var items = new List<FaceToTxtSourceItem>();
        var detailLines = new List<string>();

        foreach (var entry in selection)
        {
            if (entry.Key is not FeatureLayer featureLayer)
            {
                continue;
            }

            var objectIds = entry.Value?.Distinct().OrderBy(value => value).ToList() ?? [];
            if (objectIds.Count == 0)
            {
                continue;
            }

            try
            {
                var item = BuildSelectionSourceItem(featureLayer, objectIds);
                if (item is null)
                {
                    continue;
                }

                items.Add(item);
                detailLines.Add($"{item.DisplayName}：{item.FeatureCount} 个要素，{Math.Max(0, item.FieldOptions.Count - 1)} 个可选字段。");
            }
            catch (Exception exception)
            {
                DiagnosticsLogger.Write(nameof(FaceToTxtService), $"读取选择图层失败：{featureLayer.Name}", exception);
                detailLines.Add($"{featureLayer.Name}：{BuildFriendlySelectionDetail(exception.Message)}");
            }
        }

        if (items.Count == 0)
        {
            return new FaceToTxtSourceSnapshot
            {
                InputMode = FaceToTxtInputMode.CurrentSelection,
                SummaryText = "当前选择集中没有可导出的面状图层。",
                DetailText = detailLines.Count == 0
                    ? "当前选择集中没有可导出的面状图层。"
                    : string.Join(Environment.NewLine, detailLines)
            };
        }

        var summary = $"当前选择集包含 {items.Count} 个面状图层，共 {items.Sum(item => item.FeatureCount)} 个要素。";
        if (items.Count > 1)
        {
            detailLines.Insert(0, "提示：当前选择集包含多个图层，导出时会按图层逐个生成 TXT。");
        }

        return new FaceToTxtSourceSnapshot
        {
            InputMode = FaceToTxtInputMode.CurrentSelection,
            SummaryText = summary,
            DetailText = string.Join(Environment.NewLine, detailLines),
            Items = items,
            FieldOptions = BuildUnionFieldOptions(items),
            SpatialReference = items.Select(item => item.SpatialReference).FirstOrDefault(reference => reference is not null)
        };
    }

    private static FaceToTxtSourceItem? BuildSelectionSourceItem(
        FeatureLayer featureLayer,
        IReadOnlyList<long> objectIds)
    {
        var fieldOptions = BuildFieldOptions(featureLayer);
        var item = new FaceToTxtSourceItem
        {
            DisplayName = featureLayer.Name,
            SourcePath = SafeGetPath(featureLayer),
            DatasetName = SafeGetDatasetName(featureLayer),
            Layer = featureLayer,
            ObjectIds = objectIds,
            FieldOptions = fieldOptions,
            GeometryType = GeometryType.Polygon,
            FeatureCount = objectIds.Count
        };

        var records = ReadFeatureRecords(item);
        if (records.Count == 0)
        {
            return null;
        }

        item.SampleTextValues = BuildSampleTextValues(records);
        item.FeatureCount = records.Count;
        item.SpatialReference = records
            .Select(record => record.Geometry.SpatialReference)
            .FirstOrDefault(reference => reference is not null);
        return item;
    }

    private static FaceToTxtSourceSnapshot InspectDatasetInternal(string catalogPath)
    {
        var normalizedPath = NormalizePath(catalogPath);
        var item = ItemFactory.Instance.Create(normalizedPath);
        if (item is null)
        {
            return FaceToTxtSourceSnapshot.Empty("无法识别选择的数据集。");
        }

        if (!ItemFactory.Instance.CanGetDataset(item))
        {
            return FaceToTxtSourceSnapshot.Empty("所选项目不是可导出的要素类。");
        }

        using var dataset = ItemFactory.Instance.GetDataset(item);
        if (dataset is not FeatureClass featureClass)
        {
            return FaceToTxtSourceSnapshot.Empty("所选项目不是要素类。");
        }

        using var definition = featureClass.GetDefinition();
        if (definition.GetShapeType() != GeometryType.Polygon)
        {
            return FaceToTxtSourceSnapshot.Empty("所选要素类不是面状要素。");
        }

        var fieldOptions = BuildFieldOptions(definition);
        var spatialReference = definition.GetSpatialReference();
        var featureCount = (int)Math.Min(int.MaxValue, featureClass.GetCount());
        var sourceItem = new FaceToTxtSourceItem
        {
            DisplayName = item.Name,
            SourcePath = normalizedPath,
            DatasetName = definition.GetName(),
            CatalogPath = normalizedPath,
            FieldOptions = fieldOptions,
            GeometryType = definition.GetShapeType(),
            SpatialReference = spatialReference,
            FeatureCount = featureCount
        };

        var records = ReadFeatureRecords(featureClass, sourceItem);
        sourceItem.SampleTextValues = BuildSampleTextValues(records);

        return new FaceToTxtSourceSnapshot
        {
            InputMode = FaceToTxtInputMode.BrowseDataset,
            SummaryText = $"已选择数据集 {item.Name}，共 {featureCount} 个面要素。",
            DetailText = $"{definition.GetName()}：{featureCount} 个要素，{fieldOptions.Count - 1} 个可选字段。",
            Items = [sourceItem],
            FieldOptions = fieldOptions,
            SpatialReference = spatialReference
        };
    }

    private static List<FaceToTxtExportPackage> BuildPackagesInternal(
        FaceToTxtExportSettings settings,
        FaceToTxtSourceSnapshot snapshot)
    {
        var packages = new List<FaceToTxtExportPackage>();
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceItem in snapshot.Items)
        {
            var package = BuildPackageInternal(settings, sourceItem, usedFileNames);
            packages.Add(package);
        }

        return packages;
    }

    private static FaceToTxtExportPackage BuildPackageInternal(
        FaceToTxtExportSettings settings,
        FaceToTxtSourceItem sourceItem,
        HashSet<string> usedFileNames)
    {
        var records = ReadFeatureRecords(sourceItem);
        if (records.Count == 0)
        {
            throw new InvalidOperationException($"{sourceItem.DisplayName} 中没有可导出的面状要素。");
        }

        var bandNumber = DetermineBaseBandNumber(records[0].Geometry);
        var targetSpatialReference = SpatialReferenceBuilder.CreateSpatialReference(4488 + bandNumber);
        var displayBandNumber = bandNumber + settings.BandAdjustment;
        if (displayBandNumber <= 0)
        {
            throw new InvalidOperationException("坐标加带数后带号无效，请检查设置。");
        }

        var warnings = new List<string>();
        if (settings.ReverseLength)
        {
            warnings.Add("“反算图形边长”当前仅保留配置位，未写入 TXT。");
        }

        var fileNameStem = ResolveFileNameStem(settings, sourceItem, records);
        var filePath = ResolveUniqueFilePath(settings.OutputDirectory, fileNameStem, usedFileNames);

        var contentLines = new List<string>();
        if (settings.TxtMode == FaceToTxtTxtMode.StandardHeader)
        {
            contentLines.AddRange(BuildHeaderLines(settings, displayBandNumber));
            if (settings.CustomDescriptionEnabled && !string.IsNullOrWhiteSpace(settings.CustomDescriptionText))
            {
                contentLines.AddRange(
                    settings.CustomDescriptionText
                        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrWhiteSpace(line)));
            }

            contentLines.Add(string.Empty);
        }

        contentLines.Add("[地块坐标]");

        foreach (var record in records)
        {
            var projected = ProjectGeometry(record.Geometry, targetSpatialReference, settings.RepairGeometry);
            var parcelId = ResolveRequiredText(record, settings.ParcelIdField, sourceItem, "地块编号");
            var parcelName = ResolveRequiredText(record, settings.ParcelNameField, sourceItem, "地块名称");
            var parcelUse = ResolveRequiredText(record, settings.ParcelUseField, sourceItem, "地块用途");
            var landCode = ResolveRequiredText(record, settings.LandCodeField, sourceItem, "地类编码");
            var mapSheet = ResolveMapSheetText(settings, record, sourceItem, warnings);
            var areaText = ResolveAreaText(settings, record, projected);
            var pointCount = CountPoints(projected, settings.IncludeClosingPoint);
            contentLines.Add($"{pointCount},{areaText},{parcelId},{parcelName},面,{mapSheet},{parcelUse},{landCode},@");

            var ringIndex = 0;
            foreach (var ring in ExtractRings(projected))
            {
                ringIndex++;
                var cleanedRing = CleanRingPoints(ring);
                if (cleanedRing.Count < 3)
                {
                    continue;
                }

                var ringNumber = settings.NumberHoleRings ? ringIndex : 1;
                for (var pointIndex = 0; pointIndex < cleanedRing.Count; pointIndex++)
                {
                    var pointNumber = pointIndex + 1;
                    var point = cleanedRing[pointIndex];
                    contentLines.Add(
                        $"{FormatPointName(settings.CoordinatePrefix, pointNumber, settings.PointNumberDigits)},{ringNumber},{FormatCoordinate(point, settings, displayBandNumber)}");
                }

                if (settings.IncludeClosingPoint)
                {
                    var firstPoint = cleanedRing[0];
                    contentLines.Add(
                        $"{FormatPointName(settings.CoordinatePrefix, 1, settings.PointNumberDigits)},{ringNumber},{FormatCoordinate(firstPoint, settings, displayBandNumber)}");
                }
            }
        }

        var content = string.Join(Environment.NewLine, contentLines);
        return new FaceToTxtExportPackage(filePath, content, warnings);
    }

    private static List<ExportRecord> ReadFeatureRecords(FaceToTxtSourceItem sourceItem)
    {
        if (sourceItem.Layer is not null)
        {
            return ReadFeatureRecords(sourceItem.Layer, sourceItem);
        }

        using var featureClass = OpenFeatureClass(sourceItem);
        return ReadFeatureRecords(featureClass, sourceItem);
    }

    private static List<ExportRecord> ReadFeatureRecords(
        FeatureLayer featureLayer,
        FaceToTxtSourceItem sourceItem)
    {
        var records = new List<ExportRecord>();
        var queryFilter = new QueryFilter
        {
            SubFields = "*"
        };

        if (sourceItem.ObjectIds.Count > 0)
        {
            queryFilter.ObjectIDs = sourceItem.ObjectIds.ToList();
        }

        using var cursor = featureLayer.Search(queryFilter, null, null, null);
        while (cursor.MoveNext())
        {
            using var feature = cursor.Current as Feature;
            if (feature is null)
            {
                continue;
            }

            var geometry = feature.GetShape() as Polygon;
            if (geometry is null || geometry.IsEmpty)
            {
                continue;
            }

            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in sourceItem.FieldOptions.Where(option => !string.IsNullOrWhiteSpace(option.Value)))
            {
                try
                {
                    values[field.Value] = feature[field.Value];
                }
                catch
                {
                    values[field.Value] = null;
                }
            }

            records.Add(new ExportRecord(geometry, values));
        }

        return records;
    }

    private static List<ExportRecord> ReadFeatureRecords(
        FeatureClass featureClass,
        FaceToTxtSourceItem sourceItem)
    {
        var records = new List<ExportRecord>();
        var queryFilter = new QueryFilter
        {
            SubFields = "*"
        };

        if (sourceItem.ObjectIds.Count > 0)
        {
            queryFilter.ObjectIDs = sourceItem.ObjectIds.ToList();
        }

        using var cursor = featureClass.Search(queryFilter);
        while (cursor.MoveNext())
        {
            using var feature = cursor.Current as Feature;
            if (feature is null)
            {
                continue;
            }

            var geometry = feature.GetShape() as Polygon;
            if (geometry is null || geometry.IsEmpty)
            {
                continue;
            }

            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in sourceItem.FieldOptions.Where(option => !string.IsNullOrWhiteSpace(option.Value)))
            {
                try
                {
                    values[field.Value] = feature[field.Value];
                }
                catch
                {
                    values[field.Value] = null;
                }
            }

            records.Add(new ExportRecord(geometry, values));
        }

        return records;
    }

    private static IReadOnlyDictionary<string, string> BuildSampleTextValues(IReadOnlyList<ExportRecord> records)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            foreach (var field in record.Values)
            {
                if (values.ContainsKey(field.Key))
                {
                    continue;
                }

                var text = field.Value is null ? string.Empty : FormatValue(field.Value);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                values[field.Key] = text;
            }

            if (values.Count > 0 && record.Values.Count == 0)
            {
                break;
            }
        }

        return values;
    }

    private static FeatureClass OpenFeatureClass(FaceToTxtSourceItem sourceItem)
    {
        if (sourceItem.Layer is not null)
        {
            return sourceItem.Layer.GetFeatureClass();
        }

        if (string.IsNullOrWhiteSpace(sourceItem.CatalogPath))
        {
            throw new InvalidOperationException($"无法打开 {sourceItem.DisplayName} 的数据源。");
        }

        var item = ItemFactory.Instance.Create(NormalizePath(sourceItem.CatalogPath));
        if (item is null)
        {
            throw new InvalidOperationException($"无法识别 {sourceItem.DisplayName} 的数据源。");
        }

        if (!ItemFactory.Instance.CanGetDataset(item))
        {
            throw new InvalidOperationException($"无法从 {sourceItem.DisplayName} 打开要素类。");
        }

        var dataset = ItemFactory.Instance.GetDataset(item);
        if (dataset is not FeatureClass featureClass)
        {
            dataset.Dispose();
            throw new InvalidOperationException($"{sourceItem.DisplayName} 不是面状要素类。");
        }

        return featureClass;
    }

    private static List<SelectionOption<string>> BuildFieldOptions(FeatureLayer featureLayer)
    {
        var options = new List<SelectionOption<string>>
        {
            new(FieldPlaceholder, string.Empty)
        };

        foreach (var field in featureLayer.GetFieldDescriptions())
        {
            var fieldName = field.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                continue;
            }

            var fieldTypeName = field.Type.ToString();
            if (fieldTypeName.Equals("Geometry", StringComparison.OrdinalIgnoreCase) ||
                fieldTypeName.Equals("OID", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var alias = field.Alias?.Trim() ?? string.Empty;
            var label = string.IsNullOrWhiteSpace(alias) ||
                        string.Equals(fieldName, alias, StringComparison.OrdinalIgnoreCase)
                ? fieldName
                : $"{fieldName} ({alias})";

            if (options.Any(option => option.Value.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            options.Add(new SelectionOption<string>(label, fieldName));
        }

        return options;
    }

    private static List<SelectionOption<string>> BuildFieldOptions(TableDefinition definition)
    {
        var options = new List<SelectionOption<string>>
        {
            new(FieldPlaceholder, string.Empty)
        };

        foreach (var field in definition.GetFields())
        {
            if (field.FieldType is FieldType.Geometry or FieldType.OID)
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(field.AliasName) ||
                        string.Equals(field.Name, field.AliasName, StringComparison.OrdinalIgnoreCase)
                ? field.Name
                : $"{field.Name} ({field.AliasName})";

            if (options.Any(option => option.Value.Equals(field.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            options.Add(new SelectionOption<string>(label, field.Name));
        }

        return options;
    }

    private static IReadOnlyList<SelectionOption<string>> BuildUnionFieldOptions(IReadOnlyList<FaceToTxtSourceItem> items)
    {
        var lookup = new Dictionary<string, SelectionOption<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            foreach (var option in item.FieldOptions)
            {
                if (string.IsNullOrWhiteSpace(option.Value))
                {
                    continue;
                }

                if (lookup.ContainsKey(option.Value))
                {
                    continue;
                }

                lookup[option.Value] = option;
            }
        }

        return
        [
            new SelectionOption<string>(FieldPlaceholder, string.Empty),
            .. lookup.Values.OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static HashSet<string> BuildAvailableFieldSet(TableDefinition definition)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in definition.GetFields())
        {
            if (field.FieldType is FieldType.Geometry or FieldType.OID)
            {
                continue;
            }

            fields.Add(field.Name);
        }

        return fields;
    }

    private static string ResolveFileNameStem(
        FaceToTxtExportSettings settings,
        FaceToTxtSourceItem sourceItem,
        IReadOnlyList<ExportRecord> records)
    {
        var layerName = SanitizeFileName(sourceItem.DisplayName);
        if (settings.FileNameMode == FaceToTxtFileNameMode.LayerName)
        {
            return string.IsNullOrWhiteSpace(layerName) ? "txt-export" : layerName;
        }

        var namingFieldValue = ResolveFirstTextValue(records, settings.NamingField);
        if (string.IsNullOrWhiteSpace(namingFieldValue))
        {
            return string.IsNullOrWhiteSpace(layerName) ? "txt-export" : layerName;
        }

        namingFieldValue = SanitizeFileName(namingFieldValue);
        if (settings.FileNameMode == FaceToTxtFileNameMode.NamingField)
        {
            return namingFieldValue;
        }

        return string.Join("_", new[] { layerName, namingFieldValue }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string ResolveRequiredText(
        ExportRecord record,
        string fieldName,
        FaceToTxtSourceItem sourceItem,
        string friendlyName)
    {
        var text = ResolveText(record, fieldName);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"{sourceItem.DisplayName} 缺少必需字段“{friendlyName}”。");
        }

        return text;
    }

    private static string ResolveMapSheetText(
        FaceToTxtExportSettings settings,
        ExportRecord record,
        FaceToTxtSourceItem sourceItem,
        List<string> warnings)
    {
        var text = ResolveText(record, settings.MapSheetField);
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (settings.AutoMapSheet)
        {
            var autoField = FindBestField(sourceItem, ["图幅", "图幅号", "TFH", "Sheet", "sheet"]);
            if (!string.IsNullOrWhiteSpace(autoField))
            {
                text = ResolveText(record, autoField);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    warnings.Add($"图层 {sourceItem.DisplayName} 的图幅号自动匹配为 {autoField}。");
                    return text;
                }
            }
        }

        throw new InvalidOperationException($"{sourceItem.DisplayName} 缺少图幅号字段。");
    }

    private static string ResolveAreaText(
        FaceToTxtExportSettings settings,
        ExportRecord record,
        Polygon projected)
    {
        var areaText = ResolveText(record, settings.AreaField);
        if (!string.IsNullOrWhiteSpace(areaText) && double.TryParse(areaText, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return areaText;
        }

        if (!string.IsNullOrWhiteSpace(areaText) &&
            double.TryParse(areaText, NumberStyles.Float, CultureInfo.CurrentCulture, out _))
        {
            return areaText;
        }

        var area = Math.Abs(projected.Area);
        if (settings.AreaUnit == FaceToTxtAreaUnit.Hectare)
        {
            area /= 10000d;
        }

        return area.ToString($"F{settings.AreaDecimals}", CultureInfo.InvariantCulture);
    }

    private static string ResolveFirstTextValue(IReadOnlyList<ExportRecord> records, string fieldName)
    {
        foreach (var record in records)
        {
            var text = ResolveText(record, fieldName);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return string.Empty;
    }

    private static string ResolveText(ExportRecord record, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return string.Empty;
        }

        if (!record.Values.TryGetValue(fieldName, out var raw) || raw is null || raw is DBNull)
        {
            return string.Empty;
        }

        return FormatValue(raw);
    }

    private static string FindBestField(FaceToTxtSourceItem sourceItem, IEnumerable<string> keywords)
    {
        var values = sourceItem.FieldOptions
            .Where(option => !string.IsNullOrWhiteSpace(option.Value))
            .Select(option => option.Value)
            .ToArray();

        foreach (var keyword in keywords)
        {
            var match = values.FirstOrDefault(value => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
            {
                return match;
            }
        }

        return string.Empty;
    }

    private static List<Polygon> ExtractRings(Polygon polygon)
    {
        var rings = new List<Polygon>();
        foreach (var part in polygon.Parts)
        {
            var ringPoints = new List<MapPoint>();
            foreach (var segment in part)
            {
                if (ringPoints.Count == 0)
                {
                    ringPoints.Add(segment.StartPoint);
                }

                ringPoints.Add(segment.EndPoint);
            }

            if (ringPoints.Count > 0)
            {
                var ring = PolygonBuilderEx.CreatePolygon(ringPoints, polygon.SpatialReference);
                rings.Add(ring);
            }
        }

        if (rings.Count == 0)
        {
            rings.Add(polygon);
        }

        return rings;
    }

    private static List<MapPoint> CleanRingPoints(Polygon polygon)
    {
        var points = new List<MapPoint>();
        foreach (var point in polygon.Points)
        {
            if (points.Count == 0)
            {
                points.Add(point);
                continue;
            }

            var last = points[^1];
            if (AreSamePoint(last, point))
            {
                continue;
            }

            points.Add(point);
        }

        if (points.Count > 1 && AreSamePoint(points[0], points[^1]))
        {
            points.RemoveAt(points.Count - 1);
        }

        return points;
    }

    private static int CountPoints(Polygon polygon, bool includeClosingPoint)
    {
        var total = 0;
        foreach (var ring in ExtractRings(polygon))
        {
            var points = CleanRingPoints(ring);
            if (points.Count == 0)
            {
                continue;
            }

            total += points.Count;
            if (includeClosingPoint)
            {
                total++;
            }
        }

        return total;
    }

    private static int DetermineBaseBandNumber(Polygon geometry)
    {
        var centroid = GeometryEngine.Instance.Centroid(geometry);
        if (centroid is null)
        {
            throw new InvalidOperationException("无法获取图形中心点。");
        }

        var geographicCentroid = GeometryEngine.Instance.Project(centroid, Geographic2000) as MapPoint;
        if (geographicCentroid is null)
        {
            throw new InvalidOperationException("无法转换图形坐标系以计算带号。");
        }

        var longitude = geographicCentroid.X;
        var bandNumber = (int)Math.Floor(longitude / 3d) + 1;
        if (bandNumber <= 0)
        {
            throw new InvalidOperationException("计算带号失败。");
        }

        return bandNumber;
    }

    private static Polygon ProjectGeometry(
        Polygon geometry,
        SpatialReference targetSpatialReference,
        bool repairGeometry)
    {
        Geometry candidate = geometry;
        if (repairGeometry)
        {
            var simplified = GeometryEngine.Instance.SimplifyAsFeature(candidate);
            if (simplified is not null)
            {
                candidate = simplified;
            }
        }

        var projected = GeometryEngine.Instance.Project(candidate, targetSpatialReference) as Polygon;
        if (projected is null)
        {
            throw new InvalidOperationException("图形投影失败。");
        }

        if (repairGeometry)
        {
            var simplified = GeometryEngine.Instance.SimplifyAsFeature(projected);
            if (simplified is Polygon simplifiedPolygon)
            {
                projected = simplifiedPolygon;
            }
        }

        return projected;
    }

    private static string FormatPointName(string prefix, int pointNumber, int digits)
    {
        var normalizedPrefix = string.IsNullOrWhiteSpace(prefix) ? "J" : prefix.Trim();
        return $"{normalizedPrefix}{pointNumber.ToString($"D{Math.Max(1, digits)}", CultureInfo.InvariantCulture)}";
    }

    private static string FormatCoordinate(
        MapPoint point,
        FaceToTxtExportSettings settings,
        int bandNumber)
    {
        var x = point.X;
        var y = point.Y;

        if (settings.SwapXY)
        {
            (x, y) = (y, x);
        }

        var formattedFirst = x.ToString($"F{settings.CoordinateDecimals}", CultureInfo.InvariantCulture);
        var formattedSecond = BuildBandedCoordinate(y, bandNumber, settings.CoordinateDecimals);
        return $"{formattedFirst},{formattedSecond}";
    }

    private static string BuildBandedCoordinate(double coordinate, int bandNumber, int decimals)
    {
        var value = bandNumber * 1_000_000d + coordinate;
        return value.ToString($"F{Math.Max(0, decimals)}", CultureInfo.InvariantCulture);
    }

    private static List<string> BuildHeaderLines(FaceToTxtExportSettings settings, int bandNumber)
    {
        var lines = new List<string>
        {
            "[属性描述]",
            "格式版本号=1.01",
            $"数据生产单位={settings.ProductionUnit}",
            $"数据生产日期={settings.ProductionDateText}",
            $"坐标系={settings.CoordinateSystemName}",
            "几度分带=3",
            $"投影类型={settings.ProjectionTypeName}",
            $"计量单位={settings.UnitName}",
            $"带号={bandNumber}"
        };

        return lines;
    }

    private static string SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? string.Empty : sanitized;
    }

    private static string ResolveUniqueFilePath(
        string outputDirectory,
        string fileNameStem,
        HashSet<string> usedFileNames)
    {
        var baseStem = string.IsNullOrWhiteSpace(fileNameStem) ? "txt-export" : fileNameStem;
        var candidate = baseStem;
        var index = 2;

        while (usedFileNames.Contains(candidate))
        {
            candidate = $"{baseStem}_{index}";
            index++;
        }

        usedFileNames.Add(candidate);
        return Path.Combine(outputDirectory, $"{candidate}.txt");
    }

    private static string FormatValue(object value)
    {
        return value switch
        {
            string text => text.Trim(),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static bool AreSamePoint(MapPoint left, MapPoint right)
    {
        const double tolerance = 0.0000001d;
        return Math.Abs(left.X - right.X) < tolerance && Math.Abs(left.Y - right.Y) < tolerance;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.Trim());
    }

    private static string BuildFriendlySelectionDetail(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return "读取失败，请改用“浏览数据集”选择本地面要素类。";
        }

        if (rawMessage.Contains("does not have a valid definition", StringComparison.OrdinalIgnoreCase))
        {
            return "当前是在线服务图层，底层数据定义不可直接读取；请先导出到本地面要素类，或改用“浏览数据集”。";
        }

        return rawMessage;
    }

    private static string SafeGetDatasetName(FeatureLayer featureLayer)
    {
        try
        {
            using var table = featureLayer.GetTable();
            using var definition = table.GetDefinition();
            return string.IsNullOrWhiteSpace(definition.GetName()) ? featureLayer.Name : definition.GetName();
        }
        catch
        {
            return featureLayer.Name;
        }
    }

    private static string SafeGetPath(MapMember member)
    {
        try
        {
            return member.GetPath()?.LocalPath ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed record ExportRecord(Polygon Geometry, IReadOnlyDictionary<string, object?> Values);

    private sealed record FaceToTxtExportPackage(
        string FilePath,
        string Content,
        IReadOnlyList<string> Warnings);
}
