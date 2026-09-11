using System.IO;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal enum ImageryAddOutcome
{
    Success,
    MissingCredential,
    NoActiveMap,
    AlreadyExists,
    Failed
}

internal sealed record ImageryAddResult(
    ImageryAddOutcome Outcome,
    string Message,
    string? LayerName = null,
    string? CacheFilePath = null)
{
    public bool IsSuccess => Outcome == ImageryAddOutcome.Success;
}

internal sealed class ImageryLayerService
{
    private readonly string _cacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GuotuGiserToolbox",
        "imagery-cache");

    internal async Task<ImageryAddResult> AddPresetAsync(
        ImageryServicePreset preset,
        ImageryModuleSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.HasCredentialFor(preset.CredentialKind))
        {
            return new ImageryAddResult(
                ImageryAddOutcome.MissingCredential,
                $"未配置 {ImageryServiceCatalog.GetCredentialLabel(preset.CredentialKind)}，请先到设置里补充。");
        }

        var credential = ImageryServiceCatalog.GetActiveCredential(settings, preset.CredentialKind) ?? string.Empty;
        if (preset.CredentialKind != ImageryCredentialKind.None && string.IsNullOrWhiteSpace(credential))
        {
            return new ImageryAddResult(
                ImageryAddOutcome.MissingCredential,
                $"未配置 {ImageryServiceCatalog.GetCredentialLabel(preset.CredentialKind)}，请先到设置里补充。");
        }

        var map = MapView.Active?.Map;
        if (map is null)
        {
            return new ImageryAddResult(
                ImageryAddOutcome.NoActiveMap,
                "当前没有可用的地图视图，请先打开一个地图。");
        }

        var layerName = ImageryServiceCatalog.BuildLayerName(preset);
        var existing = await QueuedTask.Run(() => map.FindLayers(layerName, true).FirstOrDefault());
        if (existing is not null)
        {
            return new ImageryAddResult(
                ImageryAddOutcome.AlreadyExists,
                $"图层已存在于 Contents：{layerName}",
                layerName);
        }

        try
        {
            return preset.ServiceKind switch
            {
                ImageryServiceKind.TileTemplate => await AddTileTemplateLayerAsync(map, preset, credential, layerName, cancellationToken),
                ImageryServiceKind.DirectTileTemplate => await AddTileTemplateLayerAsync(map, preset, string.Empty, layerName, cancellationToken),
                ImageryServiceKind.MapServer => await AddMapServerLayerAsync(map, preset, layerName),
                ImageryServiceKind.Wmts => await AddWmtsLayerAsync(map, preset, layerName),
                ImageryServiceKind.Wfs => new ImageryAddResult(
                    ImageryAddOutcome.Failed,
                    "WFS 请先打开参数窗口后再添加。"),
                _ => new ImageryAddResult(
                    ImageryAddOutcome.Failed,
                    $"暂不支持当前服务类型：{ImageryServiceCatalog.GetServiceKindLabel(preset.ServiceKind)}")
            };
        }
        catch (Exception exception)
        {
            return new ImageryAddResult(
                ImageryAddOutcome.Failed,
                $"添加服务失败：{exception.Message}");
        }
    }

    private async Task<ImageryAddResult> AddTileTemplateLayerAsync(
        Map map,
        ImageryServicePreset preset,
        string credential,
        string layerName,
        CancellationToken cancellationToken)
    {
        var serviceUrl = ImageryServiceCatalog.BuildServiceUrl(preset, credential);
        var cacheFilePath = BuildCacheFilePath(preset, credential);
        var lyrxText = ImageryServiceCatalog.BuildLyrxText(
            preset,
            layerName,
            ImageryServiceCatalog.BuildDescription(preset, serviceUrl),
            serviceUrl);

        Directory.CreateDirectory(_cacheDirectory);
        await File.WriteAllTextAsync(cacheFilePath, lyrxText, cancellationToken);

        await QueuedTask.Run(() =>
        {
            var layerDocument = new LayerDocument(cacheFilePath);
            var layerParams = new LayerCreationParams(layerDocument.GetCIMLayerDocument())
            {
                Name = layerName,
                MapMemberPosition = GetMapMemberPosition(preset.LayerRole)
            };

            LayerFactory.Instance.CreateLayer<Layer>(layerParams, map);
        });

        return new ImageryAddResult(
            ImageryAddOutcome.Success,
            $"已添加到 Contents：{layerName}",
            layerName,
            cacheFilePath);
    }

    private async Task<ImageryAddResult> AddMapServerLayerAsync(
        Map map,
        ImageryServicePreset preset,
        string layerName)
    {
        await QueuedTask.Run(() =>
        {
            var layerParams = new LayerCreationParams(new Uri(preset.SourceUrl))
            {
                Name = layerName,
                MapMemberPosition = GetMapMemberPosition(preset.LayerRole)
            };

            LayerFactory.Instance.CreateLayer<Layer>(layerParams, map);
        });

        return new ImageryAddResult(
            ImageryAddOutcome.Success,
            $"已添加到 Contents：{layerName}",
            layerName);
    }

    private async Task<ImageryAddResult> AddWmtsLayerAsync(
        Map map,
        ImageryServicePreset preset,
        string layerName)
    {
        await QueuedTask.Run(() =>
        {
            var serverConnection = new CIMProjectServerConnection
            {
                URL = preset.SourceUrl,
                ServerType = ServerType.WMTS
            };

            var wmtsConnection = new CIMWMTSServiceConnection
            {
                ServerConnection = serverConnection,
                LayerName = preset.ReferenceCode
            };

            var layerParams = new LayerCreationParams(wmtsConnection)
            {
                Name = layerName,
                MapMemberPosition = GetMapMemberPosition(preset.LayerRole)
            };

            LayerFactory.Instance.CreateLayer<Layer>(layerParams, map);
        });

        return new ImageryAddResult(
            ImageryAddOutcome.Success,
            $"已添加到 Contents：{layerName}",
            layerName);
    }

    internal async Task<ImageryAddResult> AddWfsLayerAsync(
        ImageryServicePreset preset,
        WfsLayerOptions options,
        string credential,
        CancellationToken cancellationToken = default)
    {
        if (preset.CredentialKind != ImageryCredentialKind.None && string.IsNullOrWhiteSpace(credential))
        {
            return new ImageryAddResult(
                ImageryAddOutcome.MissingCredential,
                $"未配置 {ImageryServiceCatalog.GetCredentialLabel(preset.CredentialKind)}，请先到设置里补充。");
        }

        var map = MapView.Active?.Map;
        if (map is null)
        {
            return new ImageryAddResult(
                ImageryAddOutcome.NoActiveMap,
                "当前没有可用的地图视图，请先打开一个地图。");
        }

        var layerName = ImageryServiceCatalog.BuildLayerName(preset);
        var existing = await QueuedTask.Run(() => map.FindLayers(layerName, true).FirstOrDefault());
        if (existing is not null)
        {
            return new ImageryAddResult(
                ImageryAddOutcome.AlreadyExists,
                $"图层已存在于 Contents：{layerName}",
                layerName);
        }

        if (options.ConnectionMode == WfsConnectionMode.Snapshot &&
            ImageryServiceCatalog.ShouldSwapWfsCoordinates(preset))
        {
            return await AddTianDiTuSnapshotLayerAsync(
                map,
                preset,
                options,
                credential,
                layerName,
                cancellationToken);
        }

        var datasetName = await ImageryServiceCatalog.ResolveWfsDatasetNameAsync(preset, credential, cancellationToken);
        var datasetCandidates = BuildWfsDatasetCandidates(datasetName, preset);
        var optionCandidates = BuildWfsOptionCandidates(preset, options);
        FeatureLayer? createdLayer = null;
        Exception? lastCreateException = null;
        var lastCreateStrategy = string.Empty;
        var successfulDatasetCandidate = string.Empty;
        var successfulOptionLabel = string.Empty;
        var lastValidationMessage = string.Empty;

        DiagnosticsLogger.Write(
            nameof(ImageryLayerService),
            $"WFS add request: preset={preset.Id}; datasets={string.Join("|", datasetCandidates)}; " +
            $"optionCandidates={string.Join(" | ", optionCandidates.Select(candidate => candidate.Label))}; " +
            $"extentSource={options.ExtentSource}; useSpatialExtent={options.UseSpatialExtent}; " +
            $"bbox={options.BuildAppliedBboxValue() ?? "<none>"}");

        await QueuedTask.Run(() =>
        {
            foreach (var optionCandidate in optionCandidates)
            {
                foreach (var candidate in datasetCandidates)
                {
                    if (TryCreateValidatedWfsLayer(
                            map,
                            preset,
                            candidate,
                            optionCandidate.Options,
                            credential,
                            layerName,
                            createWithStandardConnection: true,
                            out var layer,
                            out var validationMessage,
                            out var createException))
                    {
                        createdLayer = layer;
                        lastCreateException = null;
                        lastValidationMessage = string.Empty;
                        lastCreateStrategy = "标准数据连接";
                        successfulDatasetCandidate = candidate;
                        successfulOptionLabel = optionCandidate.Label;
                        break;
                    }

                    if (createException is not null)
                    {
                        lastCreateException = createException;
                        lastValidationMessage = string.Empty;
                        lastCreateStrategy = $"标准数据连接({candidate})";
                        DiagnosticsLogger.Write(
                            nameof(ImageryLayerService),
                            $"WFS 标准连接创建失败：{preset.Id} / {candidate} / {optionCandidate.Label}",
                            createException);
                    }
                    else if (!string.IsNullOrWhiteSpace(validationMessage))
                    {
                        lastCreateException = null;
                        lastValidationMessage = validationMessage;
                        lastCreateStrategy = $"标准数据连接({candidate})";
                        DiagnosticsLogger.Write(
                            nameof(ImageryLayerService),
                            $"WFS 标准连接已创建但验证未通过：{preset.Id} / {candidate} / {optionCandidate.Label} / {validationMessage}");
                    }
                }

                if (createdLayer is not null)
                {
                    break;
                }

                foreach (var candidate in datasetCandidates)
                {
                    if (TryCreateValidatedWfsLayer(
                            map,
                            preset,
                            candidate,
                            optionCandidate.Options,
                            credential,
                            layerName,
                            createWithStandardConnection: false,
                            out var layer,
                            out var validationMessage,
                            out var createException))
                    {
                        createdLayer = layer;
                        lastCreateException = null;
                        lastValidationMessage = string.Empty;
                        lastCreateStrategy = "WFS服务连接";
                        successfulDatasetCandidate = candidate;
                        successfulOptionLabel = optionCandidate.Label;
                        break;
                    }

                    if (createException is not null)
                    {
                        lastCreateException = createException;
                        lastValidationMessage = string.Empty;
                        lastCreateStrategy = $"WFS服务连接({candidate})";
                        DiagnosticsLogger.Write(
                            nameof(ImageryLayerService),
                            $"WFS 服务连接创建失败：{preset.Id} / {candidate} / {optionCandidate.Label}",
                            createException);
                    }
                    else if (!string.IsNullOrWhiteSpace(validationMessage))
                    {
                        lastCreateException = null;
                        lastValidationMessage = validationMessage;
                        lastCreateStrategy = $"WFS服务连接({candidate})";
                        DiagnosticsLogger.Write(
                            nameof(ImageryLayerService),
                            $"WFS 服务连接已创建但验证未通过：{preset.Id} / {candidate} / {optionCandidate.Label} / {validationMessage}");
                    }
                }

                if (createdLayer is not null)
                {
                    break;
                }
            }
        });

        if (createdLayer is null)
        {
            var candidateList = string.Join("、", datasetCandidates);
            var strategyText = string.IsNullOrWhiteSpace(lastCreateStrategy)
                ? "WFS"
                : $"{lastCreateStrategy} / {string.Join(" -> ", optionCandidates.Select(candidate => candidate.Label))}";
            var message = !string.IsNullOrWhiteSpace(lastValidationMessage)
                ? $"无法添加数据（{strategyText}）：{candidateList}{Environment.NewLine}{lastValidationMessage}"
                : lastCreateException is null || string.IsNullOrWhiteSpace(lastCreateException.Message)
                    ? $"无法添加数据（{strategyText}）：{candidateList}"
                    : $"无法添加数据（{strategyText}）：{candidateList}{Environment.NewLine}{lastCreateException.Message}";

            return new ImageryAddResult(
                ImageryAddOutcome.Failed,
                message);
        }

        DiagnosticsLogger.Write(
            nameof(ImageryLayerService),
            $"WFS add success: preset={preset.Id}; strategy={lastCreateStrategy}; option={successfulOptionLabel}; dataset={successfulDatasetCandidate}; " +
            $"bbox={options.BuildAppliedBboxValue() ?? "<none>"}");

        if (createdLayer is not null && MapView.Active is not null)
        {
            try
            {
                await MapView.Active.ZoomToAsync(createdLayer, false, null, false);
            }
            catch
            {
                // WFS 图层已成功添加时，不让缩放失败影响主流程。
            }
        }

        return new ImageryAddResult(
            ImageryAddOutcome.Success,
            $"已将 WFS 图层加入内容列表：{layerName}",
            layerName);
    }

    private static FeatureLayer CreateWfsLayerWithServiceConnection(
        Map map,
        ImageryServicePreset preset,
        string layerNameCandidate,
        WfsLayerOptions options,
        string credential,
        string layerName)
    {
        var wfsConnection = new CIMWFSServiceConnection
        {
            ServerConnection = new CIMProjectServerConnection
            {
                URL = preset.SourceUrl,
                ServerType = ServerType.WFS
            },
            LayerName = layerNameCandidate,
            Version = "2.0.0",
            CapabilitiesParameters = options.BuildCapabilitiesParameters(credential)
        };

        var layerParams = new LayerCreationParams(wfsConnection)
        {
            Name = layerName,
            MapMemberPosition = GetMapMemberPosition(preset.LayerRole)
        };

        return LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, map);
    }

    private static FeatureLayer CreateWfsLayerWithStandardConnection(
        Map map,
        ImageryServicePreset preset,
        string layerNameCandidate,
        WfsLayerOptions options,
        string credential,
        string layerName)
    {
        var workspaceConnectionString = options.BuildWorkspaceConnectionString(preset.SourceUrl, credential);
        var dataConnection = new CIMStandardDataConnection
        {
            WorkspaceFactory = WorkspaceFactory.WFS,
            WorkspaceConnectionString = workspaceConnectionString,
            Dataset = layerNameCandidate,
            DatasetType = esriDatasetType.esriDTFeatureClass
        };

        var layerParams = new LayerCreationParams(dataConnection)
        {
            Name = layerName,
            MapMemberPosition = GetMapMemberPosition(preset.LayerRole)
        };

        return LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, map);
    }

    private static bool TryCreateValidatedWfsLayer(
        Map map,
        ImageryServicePreset preset,
        string datasetCandidate,
        WfsLayerOptions options,
        string credential,
        string layerName,
        bool createWithStandardConnection,
        out FeatureLayer? layer,
        out string? validationMessage,
        out Exception? createException)
    {
        layer = null;
        validationMessage = null;
        createException = null;

        try
        {
            layer = createWithStandardConnection
                ? CreateWfsLayerWithStandardConnection(map, preset, datasetCandidate, options, credential, layerName)
                : CreateWfsLayerWithServiceConnection(map, preset, datasetCandidate, options, credential, layerName);
        }
        catch (Exception exception)
        {
            createException = exception;
            return false;
        }

        validationMessage = ValidateCreatedWfsLayer(layer, options);
        if (string.IsNullOrWhiteSpace(validationMessage))
        {
            return true;
        }

        RemoveLayerSafely(map, layer);
        layer = null;
        return false;
    }

    private static string? ValidateCreatedWfsLayer(FeatureLayer layer, WfsLayerOptions options)
    {
        if (options.ConnectionMode != WfsConnectionMode.Snapshot)
        {
            return null;
        }

        try
        {
            using var table = layer.GetTable();
            if (table.GetCount() <= 0)
            {
                return "创建成功，但返回要素数为 0。";
            }
        }
        catch (Exception exception)
        {
            return $"创建成功，但读取要素数失败：{exception.Message}";
        }

        var expectedExtent = options.BuildExpectedExtentEnvelope();
        if (expectedExtent is null)
        {
            return null;
        }

        try
        {
            var actualExtent = layer.QueryExtent();
            if (actualExtent is null)
            {
                return "创建成功，但图层没有可用范围。";
            }

            var actualExtentWgs84 = ProjectEnvelopeToWgs84(actualExtent);
            if (!GeometryEngine.Instance.Intersects(actualExtentWgs84, expectedExtent))
            {
                return $"创建成功，但图层范围与输入范围不相交。输入={FormatEnvelope(expectedExtent)}；输出={FormatEnvelope(actualExtentWgs84)}";
            }
        }
        catch (Exception exception)
        {
            return $"创建成功，但验证图层范围失败：{exception.Message}";
        }

        return null;
    }

    private static Envelope ProjectEnvelopeToWgs84(Envelope envelope)
    {
        if (envelope.SpatialReference is not null && envelope.SpatialReference.Equals(SpatialReferences.WGS84))
        {
            return envelope;
        }

        try
        {
            var projected = GeometryEngine.Instance.Project(envelope, SpatialReferences.WGS84);
            return projected?.Extent ?? envelope;
        }
        catch
        {
            return envelope;
        }
    }

    private static void RemoveLayerSafely(Map map, Layer layer)
    {
        try
        {
            map.RemoveLayer(layer);
        }
        catch
        {
            // Keep cleanup best-effort.
        }
    }

    private static string FormatEnvelope(Envelope envelope)
    {
        return string.Join(
            ", ",
            $"xmin={envelope.XMin.ToString("0.###############", CultureInfo.InvariantCulture)}",
            $"ymin={envelope.YMin.ToString("0.###############", CultureInfo.InvariantCulture)}",
            $"xmax={envelope.XMax.ToString("0.###############", CultureInfo.InvariantCulture)}",
            $"ymax={envelope.YMax.ToString("0.###############", CultureInfo.InvariantCulture)}");
    }

    private static IReadOnlyList<(string Label, WfsLayerOptions Options)> BuildWfsOptionCandidates(
        ImageryServicePreset preset,
        WfsLayerOptions baseOptions)
    {
        var candidates = new List<(string Label, WfsLayerOptions Options)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string label, bool swapXY, bool swapXYFilter)
        {
            var key = $"{swapXY}:{swapXYFilter}";
            if (!seen.Add(key))
            {
                return;
            }

            candidates.Add((label, CloneWfsOptions(baseOptions, swapXY, swapXYFilter)));
        }

        if (ImageryServiceCatalog.ShouldSwapWfsFilterAxes(preset))
        {
            AddCandidate("geometry+filter swap", true, true);
            AddCandidate("filter-only swap", false, true);
        }

        AddCandidate("original", baseOptions.SwapXY, baseOptions.SwapXYFilter);
        return candidates;
    }

    private static WfsLayerOptions CloneWfsOptions(
        WfsLayerOptions source,
        bool swapXY,
        bool swapXYFilter)
    {
        return new WfsLayerOptions
        {
            ConnectionMode = source.ConnectionMode,
            MaxFeatures = source.MaxFeatures,
            PageSize = source.PageSize,
            UseSpatialExtent = source.UseSpatialExtent,
            ExtentSource = source.ExtentSource,
            ExtentLayerName = source.ExtentLayerName,
            MinX = source.MinX,
            MinY = source.MinY,
            MaxX = source.MaxX,
            MaxY = source.MaxY,
            SwapXY = swapXY,
            SwapXYFilter = swapXYFilter
        };
    }

    private async Task<ImageryAddResult> AddTianDiTuSnapshotLayerAsync(
        Map map,
        ImageryServicePreset preset,
        WfsLayerOptions options,
        string credential,
        string layerName,
        CancellationToken cancellationToken)
    {
        var datasetName = await ImageryServiceCatalog.ResolveWfsDatasetNameAsync(preset, credential, cancellationToken);
        if (string.IsNullOrWhiteSpace(datasetName))
        {
            return new ImageryAddResult(ImageryAddOutcome.Failed, "无法识别当前 WFS 图层的 typeName。");
        }

        var bbox = options.BuildAppliedBboxValue();
        if (string.IsNullOrWhiteSpace(bbox))
        {
            return new ImageryAddResult(ImageryAddOutcome.Failed, "快照模式缺少有效范围，无法抓取 WFS 要素。");
        }

        var requestUri = BuildTianDiTuGeoJsonRequestUri(preset.SourceUrl, datasetName, options.MaxFeatures, bbox, credential);
        DiagnosticsLogger.Write(
            nameof(ImageryLayerService),
            $"WFS snapshot request: preset={preset.Id}; dataset={datasetName}; bbox={bbox}; url={requestUri}");

        string geoJsonText;
        try
        {
            using var httpClient = new HttpClient();
            geoJsonText = await httpClient.GetStringAsync(requestUri, cancellationToken);
        }
        catch (Exception exception)
        {
            return new ImageryAddResult(
                ImageryAddOutcome.Failed,
                $"抓取 WFS 快照失败：{exception.Message}");
        }

        if (!TryReadGeoJsonSnapshot(geoJsonText, out var snapshotPayload, out var parseError))
        {
            DiagnosticsLogger.Write(
                nameof(ImageryLayerService),
                $"WFS snapshot parse failed: preset={preset.Id}; dataset={datasetName}; error={parseError}");
            return new ImageryAddResult(
                ImageryAddOutcome.Failed,
                $"WFS 返回结果无法解析：{parseError}");
        }

        var featureCount = snapshotPayload.Features.Count;
        if (featureCount <= 0)
        {
            return new ImageryAddResult(
                ImageryAddOutcome.Failed,
                "当前范围内未返回任何矢量要素，请调整范围后重试。");
        }

        var tempDirectory = Path.Combine(_cacheDirectory, "wfs-snapshots");
        Directory.CreateDirectory(tempDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var baseName = SanitizeName($"{preset.Id}_{timestamp}");
        var geoJsonPath = Path.Combine(tempDirectory, $"{baseName}.geojson");
        await File.WriteAllTextAsync(geoJsonPath, geoJsonText, cancellationToken);

        var outputGeoJsonPath = Path.Combine(
            tempDirectory,
            $"{SanitizeName($"wfs_{preset.ReferenceCode}_{timestamp}")}.geojson");

        File.Copy(geoJsonPath, outputGeoJsonPath, overwrite: true);

        string? outputFeatureClassPath = null;
        FeatureLayer? createdLayer = null;
        try
        {
            (createdLayer, outputFeatureClassPath) = await QueuedTask.Run(() =>
            {
                return CreateSnapshotFeatureLayer(
                    map,
                    preset,
                    layerName,
                    tempDirectory,
                    timestamp,
                    snapshotPayload);
            });
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(
                nameof(ImageryLayerService),
                $"WFS snapshot feature-class creation failed: preset={preset.Id}; geojson={outputGeoJsonPath}",
                exception);
            return new ImageryAddResult(
                ImageryAddOutcome.Failed,
                $"WFS 快照已生成，但写入本地矢量图层失败：{exception.Message}");
        }

        if (createdLayer is not null && MapView.Active is not null)
        {
            try
            {
                await MapView.Active.ZoomToAsync(createdLayer, false, null, false);
            }
            catch
            {
                // Ignore zoom errors after successful snapshot import.
            }
        }

        DiagnosticsLogger.Write(
            nameof(ImageryLayerService),
            $"WFS snapshot success: preset={preset.Id}; dataset={datasetName}; featureCount={featureCount}; geometryType={snapshotPayload.GeometryType}; output={outputFeatureClassPath ?? outputGeoJsonPath}; geojson={outputGeoJsonPath}");

        return new ImageryAddResult(
            ImageryAddOutcome.Success,
            $"已导入当前范围内 {featureCount} 个矢量要素：{layerName}",
            layerName);
    }

    private static Uri BuildTianDiTuGeoJsonRequestUri(
        string serviceUrl,
        string datasetName,
        int maxFeatures,
        string bbox,
        string credential)
    {
        var parameters = new Dictionary<string, string>
        {
            ["service"] = "WFS",
            ["request"] = "GetFeature",
            ["version"] = "2.0.0",
            ["typeNames"] = datasetName,
            ["count"] = Math.Max(1, maxFeatures).ToString(CultureInfo.InvariantCulture),
            ["bbox"] = bbox,
            ["outputFormat"] = "application/json"
        };

        if (!string.IsNullOrWhiteSpace(credential))
        {
            parameters["tk"] = credential.Trim();
        }

        var query = string.Join(
            "&",
            parameters.Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));

        return new Uri($"{serviceUrl}?{query}");
    }

    private static bool TryReadGeoJsonSnapshot(
        string geoJsonText,
        out SnapshotGeoJsonPayload snapshotPayload,
        out string parseError)
    {
        snapshotPayload = SnapshotGeoJsonPayload.Empty;
        parseError = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(geoJsonText);
            if (!document.RootElement.TryGetProperty("features", out var features) ||
                features.ValueKind != JsonValueKind.Array)
            {
                parseError = "GeoJSON 中未找到 features 数组。";
                return false;
            }

            if (features.GetArrayLength() == 0)
            {
                snapshotPayload = new SnapshotGeoJsonPayload(GeometryType.Point, new List<SnapshotFeatureRecord>());
                return true;
            }

            var records = new List<SnapshotFeatureRecord>();
            GeometryType? detectedGeometryType = null;
            var spatialReference = SpatialReferences.WGS84;

            foreach (var feature in features.EnumerateArray())
            {
                if (!feature.TryGetProperty("geometry", out var geometryElement) ||
                    geometryElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryParseGeoJsonGeometry(geometryElement, spatialReference, out var geometry, out var geometryType, out var geometryError))
                {
                    parseError = geometryError;
                    return false;
                }

                if (detectedGeometryType is null)
                {
                    detectedGeometryType = geometryType;
                }
                else if (detectedGeometryType != geometryType)
                {
                    parseError = "当前 GeoJSON 同时包含多种几何类型，暂不支持混合写入同一图层。";
                    return false;
                }

                var properties = feature.TryGetProperty("properties", out var propertiesElement) &&
                                 propertiesElement.ValueKind == JsonValueKind.Object
                    ? propertiesElement
                    : default;

                var sourceId = feature.TryGetProperty("id", out var idElement)
                    ? GetGeoJsonScalarText(idElement)
                    : string.Empty;
                var displayName = TryExtractDisplayName(properties);
                var attributesJson = properties.ValueKind == JsonValueKind.Object
                    ? properties.GetRawText()
                    : "{}";

                records.Add(new SnapshotFeatureRecord(
                    geometry,
                    sourceId,
                    displayName,
                    attributesJson));
            }

            if (records.Count == 0)
            {
                snapshotPayload = new SnapshotGeoJsonPayload(GeometryType.Point, records);
                return true;
            }

            snapshotPayload = new SnapshotGeoJsonPayload(detectedGeometryType ?? GeometryType.Point, records);
            return true;
        }
        catch (Exception exception)
        {
            parseError = exception.Message;
            return false;
        }
    }

    private static bool TryParseGeoJsonGeometry(
        JsonElement geometryElement,
        SpatialReference spatialReference,
        out Geometry geometry,
        out GeometryType geometryType,
        out string error)
    {
        geometry = MapPointBuilderEx.CreateMapPoint(0, 0, spatialReference);
        geometryType = GeometryType.Unknown;
        error = string.Empty;

        if (!geometryElement.TryGetProperty("type", out var typeElement))
        {
            error = "GeoJSON 中缺少 geometry.type。";
            return false;
        }

        if (!geometryElement.TryGetProperty("coordinates", out var coordinatesElement))
        {
            error = "GeoJSON 中缺少 geometry.coordinates。";
            return false;
        }

        switch (typeElement.GetString())
        {
            case "Point":
                if (!TryReadCoordinate2D(coordinatesElement, out var pointCoordinate, out error))
                {
                    return false;
                }

                geometry = MapPointBuilderEx.CreateMapPoint(pointCoordinate, spatialReference);
                geometryType = GeometryType.Point;
                return true;

            case "MultiPoint":
                if (!TryReadCoordinateArray(coordinatesElement, out var multiPointCoordinates, out error))
                {
                    return false;
                }

                geometry = MultipointBuilderEx.CreateMultipoint(multiPointCoordinates, spatialReference);
                geometryType = GeometryType.Multipoint;
                return true;

            case "LineString":
                if (!TryReadCoordinateArray(coordinatesElement, out var lineCoordinates, out error))
                {
                    return false;
                }

                geometry = GeometryEngine.Instance.SimplifyAsFeature(
                    PolylineBuilderEx.CreatePolyline(lineCoordinates, spatialReference));
                geometryType = GeometryType.Polyline;
                return true;

            case "MultiLineString":
                if (!TryBuildMultipartPolyline(coordinatesElement, spatialReference, out geometry, out error))
                {
                    return false;
                }

                geometryType = GeometryType.Polyline;
                return true;

            case "Polygon":
                if (!TryBuildPolygon(coordinatesElement, spatialReference, out geometry, out error))
                {
                    return false;
                }

                geometryType = GeometryType.Polygon;
                return true;

            case "MultiPolygon":
                if (!TryBuildMultiPolygon(coordinatesElement, spatialReference, out geometry, out error))
                {
                    return false;
                }

                geometryType = GeometryType.Polygon;
                return true;

            default:
                error = $"暂不支持的 GeoJSON 几何类型：{typeElement.GetString()}";
                return false;
        }
    }

    private static bool TryBuildMultipartPolyline(
        JsonElement coordinatesElement,
        SpatialReference spatialReference,
        out Geometry geometry,
        out string error)
    {
        var builder = new PolylineBuilderEx(spatialReference);
        foreach (var partElement in coordinatesElement.EnumerateArray())
        {
            if (!TryReadCoordinateArray(partElement, out var partCoordinates, out error))
            {
                geometry = PolylineBuilderEx.CreatePolyline(spatialReference);
                return false;
            }

            builder.AddPart(partCoordinates);
        }

        geometry = GeometryEngine.Instance.SimplifyAsFeature(builder.ToGeometry());
        error = string.Empty;
        return true;
    }

    private static bool TryBuildPolygon(
        JsonElement coordinatesElement,
        SpatialReference spatialReference,
        out Geometry geometry,
        out string error)
    {
        var builder = new PolygonBuilderEx(spatialReference);
        foreach (var ringElement in coordinatesElement.EnumerateArray())
        {
            if (!TryReadCoordinateArray(ringElement, out var ringCoordinates, out error))
            {
                geometry = PolygonBuilderEx.CreatePolygon(spatialReference);
                return false;
            }

            builder.AddPart(ringCoordinates);
        }

        geometry = GeometryEngine.Instance.SimplifyAsFeature(builder.ToGeometry());
        error = string.Empty;
        return true;
    }

    private static bool TryBuildMultiPolygon(
        JsonElement coordinatesElement,
        SpatialReference spatialReference,
        out Geometry geometry,
        out string error)
    {
        var builder = new PolygonBuilderEx(spatialReference);
        foreach (var polygonElement in coordinatesElement.EnumerateArray())
        {
            foreach (var ringElement in polygonElement.EnumerateArray())
            {
                if (!TryReadCoordinateArray(ringElement, out var ringCoordinates, out error))
                {
                    geometry = PolygonBuilderEx.CreatePolygon(spatialReference);
                    return false;
                }

                builder.AddPart(ringCoordinates);
            }
        }

        geometry = GeometryEngine.Instance.SimplifyAsFeature(builder.ToGeometry());
        error = string.Empty;
        return true;
    }

    private static bool TryReadCoordinateArray(
        JsonElement coordinatesElement,
        out IReadOnlyList<Coordinate2D> coordinates,
        out string error)
    {
        var list = new List<Coordinate2D>();
        foreach (var coordinateElement in coordinatesElement.EnumerateArray())
        {
            if (!TryReadCoordinate2D(coordinateElement, out var coordinate, out error))
            {
                coordinates = Array.Empty<Coordinate2D>();
                return false;
            }

            list.Add(coordinate);
        }

        if (list.Count == 0)
        {
            error = "GeoJSON 坐标数组为空。";
            coordinates = Array.Empty<Coordinate2D>();
            return false;
        }

        coordinates = list;
        error = string.Empty;
        return true;
    }

    private static bool TryReadCoordinate2D(
        JsonElement coordinateElement,
        out Coordinate2D coordinate,
        out string error)
    {
        coordinate = default;
        error = string.Empty;

        if (coordinateElement.ValueKind != JsonValueKind.Array || coordinateElement.GetArrayLength() < 2)
        {
            error = "GeoJSON 坐标格式无效。";
            return false;
        }

        if (!coordinateElement[0].TryGetDouble(out var x) || !coordinateElement[1].TryGetDouble(out var y))
        {
            error = "GeoJSON 坐标值不是有效数字。";
            return false;
        }

        coordinate = new Coordinate2D(x, y);
        return true;
    }

    private static string GetGeoJsonScalarText(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText()
        };
    }

    private static string TryExtractDisplayName(JsonElement propertiesElement)
    {
        if (propertiesElement.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var preferredName in new[] { "name", "NAME", "Name", "MC", "mc", "标识名称", "标准名称" })
        {
            if (propertiesElement.TryGetProperty(preferredName, out var propertyValue))
            {
                return GetGeoJsonScalarText(propertyValue);
            }
        }

        foreach (var property in propertiesElement.EnumerateObject())
        {
            var value = GetGeoJsonScalarText(property.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static (FeatureLayer Layer, string FeatureClassPath) CreateSnapshotFeatureLayer(
        Map map,
        ImageryServicePreset preset,
        string layerName,
        string tempDirectory,
        string timestamp,
        SnapshotGeoJsonPayload snapshotPayload)
    {
        var snapshotGeodatabasePath = Path.Combine(tempDirectory, "snapshot-wfs.gdb");
        DiagnosticsLogger.Write(
            nameof(ImageryLayerService),
            $"Snapshot stage: ensure geodatabase; path={snapshotGeodatabasePath}");
        EnsureSnapshotGeodatabase(snapshotGeodatabasePath);

        var featureClassName = BuildSnapshotFeatureClassName(preset.ReferenceCode, timestamp);
        var featureClassPath = Path.Combine(snapshotGeodatabasePath, featureClassName);

        using var geodatabase = OpenSnapshotGeodatabase(snapshotGeodatabasePath);
        DiagnosticsLogger.Write(
            nameof(ImageryLayerService),
            $"Snapshot stage: create feature class; path={featureClassPath}; geometryType={snapshotPayload.GeometryType}; featureCount={snapshotPayload.Features.Count}");
        CreateSnapshotFeatureClass(geodatabase, featureClassName, snapshotPayload.GeometryType);
        using var featureClass = geodatabase.OpenDataset<FeatureClass>(featureClassName);
        DiagnosticsLogger.Write(
            nameof(ImageryLayerService),
            $"Snapshot stage: write features; featureClass={featureClassPath}; featureCount={snapshotPayload.Features.Count}");
        WriteSnapshotFeatures(featureClass, snapshotPayload.Features);

        var layerParams = new FeatureLayerCreationParams(featureClass)
        {
            Name = layerName,
            MapMemberPosition = GetMapMemberPosition(preset.LayerRole)
        };

        DiagnosticsLogger.Write(
            nameof(ImageryLayerService),
            $"Snapshot stage: create layer; featureClass={featureClassPath}; layerName={layerName}");
        var layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, map);
        return (layer, featureClassPath);
    }

    private static void EnsureSnapshotGeodatabase(string geodatabasePath)
    {
        if (Directory.Exists(geodatabasePath))
        {
            return;
        }

        var connectionPath = new FileGeodatabaseConnectionPath(new Uri(geodatabasePath, UriKind.Absolute));
        using var _ = SchemaBuilder.CreateGeodatabase(connectionPath);
    }

    private static Geodatabase OpenSnapshotGeodatabase(string geodatabasePath)
    {
        var connectionPath = new FileGeodatabaseConnectionPath(new Uri(geodatabasePath, UriKind.Absolute));
        return new Geodatabase(connectionPath);
    }

    private static void CreateSnapshotFeatureClass(
        Geodatabase geodatabase,
        string featureClassName,
        GeometryType geometryType)
    {
        var shapeDescription = new ShapeDescription(geometryType, SpatialReferences.WGS84);
        var fields = new List<ArcGIS.Core.Data.DDL.FieldDescription>
        {
            ArcGIS.Core.Data.DDL.FieldDescription.CreateObjectIDField(),
            ArcGIS.Core.Data.DDL.FieldDescription.CreateStringField("source_id", 128),
            ArcGIS.Core.Data.DDL.FieldDescription.CreateStringField("display_name", 255),
            ArcGIS.Core.Data.DDL.FieldDescription.CreateStringField("properties_json", 4000)
        };

        var featureClassDescription = new FeatureClassDescription(featureClassName, fields, shapeDescription);
        var schemaBuilder = new SchemaBuilder(geodatabase);
        schemaBuilder.Create(featureClassDescription);
        if (!schemaBuilder.Build())
        {
            throw new InvalidOperationException(
                $"创建快照要素类失败：{string.Join(" | ", schemaBuilder.ErrorMessages)}");
        }
    }

    private static void WriteSnapshotFeatures(
        FeatureClass featureClass,
        IReadOnlyList<SnapshotFeatureRecord> records)
    {
        foreach (var record in records)
        {
            using var rowBuffer = featureClass.CreateRowBuffer();
            rowBuffer["SHAPE"] = record.Geometry;
            rowBuffer["source_id"] = TrimToLength(record.SourceId, 128);
            rowBuffer["display_name"] = TrimToLength(record.DisplayName, 255);
            rowBuffer["properties_json"] = TrimToLength(record.PropertiesJson, 4000);

            using var feature = featureClass.CreateRow(rowBuffer);
        }
    }

    private static string TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string BuildSnapshotFeatureClassName(string referenceCode, string timestamp)
    {
        var normalized = SanitizeName($"wfs_{referenceCode}_{timestamp}");
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "wfs_snapshot";
        }

        if (!char.IsLetter(normalized[0]))
        {
            normalized = $"f_{normalized}";
        }

        return normalized.Length <= 48 ? normalized : normalized[..48];
    }

    private sealed record SnapshotGeoJsonPayload(
        GeometryType GeometryType,
        IReadOnlyList<SnapshotFeatureRecord> Features)
    {
        internal static SnapshotGeoJsonPayload Empty { get; } =
            new(GeometryType.Point, Array.Empty<SnapshotFeatureRecord>());
    }

    private sealed record SnapshotFeatureRecord(
        Geometry Geometry,
        string SourceId,
        string DisplayName,
        string PropertiesJson);

    private static string SanitizeName(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        return builder.ToString();
    }

    private string BuildCacheFilePath(ImageryServicePreset preset, string seed)
    {
        var fileName = ImageryServiceCatalog.BuildCacheFileName(preset, seed);
        return Path.Combine(_cacheDirectory, fileName);
    }

    private static MapMemberPosition GetMapMemberPosition(ImageryLayerRole role)
    {
        return role switch
        {
            ImageryLayerRole.Base or ImageryLayerRole.Terrain => MapMemberPosition.AddToBottom,
            _ => MapMemberPosition.AddToTop
        };
    }

    private static IReadOnlyList<string> BuildWfsDatasetCandidates(string datasetName, ImageryServicePreset preset)
    {
        var candidates = new List<string>();

        void AddCandidate(string? value)
        {
            var normalized = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            if (candidates.Any(existing => existing.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            candidates.Add(normalized);
        }

        AddCandidate(datasetName);

        var shortNameIndex = datasetName.IndexOf(':');
        if (shortNameIndex > 0 && shortNameIndex < datasetName.Length - 1)
        {
            AddCandidate(datasetName[(shortNameIndex + 1)..]);
        }

        AddCandidate(preset.ReferenceCode);
        AddCandidate(preset.DisplayName);

        return candidates;
    }
}
