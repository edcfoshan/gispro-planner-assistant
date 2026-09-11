using System.Globalization;
using ArcGIS.Core.Geometry;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal enum WfsConnectionMode
{
    Snapshot,
    OnDemand
}

internal enum WfsExtentSource
{
    CurrentView,
    AllLayers,
    SpecificLayer
}

internal sealed class WfsLayerOptions
{
    public WfsConnectionMode ConnectionMode { get; set; } = WfsConnectionMode.Snapshot;

    public int MaxFeatures { get; set; } = 3000;

    public int PageSize { get; set; } = 0;

    public bool UseSpatialExtent { get; set; }

    public WfsExtentSource ExtentSource { get; set; } = WfsExtentSource.CurrentView;

    public string ExtentLayerName { get; set; } = string.Empty;

    public double? MinX { get; set; }

    public double? MinY { get; set; }

    public double? MaxX { get; set; }

    public double? MaxY { get; set; }

    public bool SwapXY { get; set; } = false;

    public bool SwapXYFilter { get; set; }

    internal static WfsLayerOptions CreateDefault(Envelope? currentExtent)
    {
        var options = new WfsLayerOptions();
        if (currentExtent is null)
        {
            options.UseSpatialExtent = false;
            return options;
        }

        options.MinX = currentExtent.XMin;
        options.MinY = currentExtent.YMin;
        options.MaxX = currentExtent.XMax;
        options.MaxY = currentExtent.YMax;
        return options;
    }

    internal static WfsLayerOptions FromSettings(ImageryModuleSettings settings, WfsExtentContext? extentContext)
    {
        var options = new WfsLayerOptions
        {
            ConnectionMode = ParseConnectionMode(settings.WfsConnectionMode),
            MaxFeatures = settings.WfsMaxFeatures > 0 ? settings.WfsMaxFeatures : 3000,
            PageSize = Math.Max(0, settings.WfsPageSize),
            UseSpatialExtent = settings.WfsUseSpatialExtent,
            ExtentSource = ParseExtentSource(settings.WfsExtentSource),
            ExtentLayerName = string.IsNullOrWhiteSpace(settings.WfsExtentLayerName)
                ? string.Empty
                : settings.WfsExtentLayerName.Trim(),
            SwapXY = settings.WfsSwapXY,
            SwapXYFilter = settings.WfsSwapXYFilter
        };

        if (options.ExtentSource == WfsExtentSource.SpecificLayer &&
            extentContext is not null &&
            extentContext.FindLayer(options.ExtentLayerName) is null)
        {
            options.ExtentLayerName = extentContext.LayerCandidates.FirstOrDefault()?.Label ?? string.Empty;
        }

        var resolvedExtent = ResolvePresetExtent(options.ExtentSource, extentContext, options.ExtentLayerName);
        if (resolvedExtent is not null)
        {
            SetExtent(options, resolvedExtent);
        }
        else
        {
            options.MinX = settings.WfsCustomMinX;
            options.MinY = settings.WfsCustomMinY;
            options.MaxX = settings.WfsCustomMaxX;
            options.MaxY = settings.WfsCustomMaxY;
        }

        return options;
    }

    internal void ApplyTo(ImageryModuleSettings settings)
    {
        settings.WfsConnectionMode = ConnectionMode.ToString();
        settings.WfsMaxFeatures = MaxFeatures > 0 ? MaxFeatures : 3000;
        settings.WfsPageSize = Math.Max(0, PageSize);
        settings.WfsUseSpatialExtent = UseSpatialExtent;
        settings.WfsExtentSource = ExtentSource.ToString();
        settings.WfsExtentLayerName = ExtentLayerName;
        settings.WfsSwapXY = SwapXY;
        settings.WfsSwapXYFilter = SwapXYFilter;
        settings.WfsCustomMinX = MinX;
        settings.WfsCustomMinY = MinY;
        settings.WfsCustomMaxX = MaxX;
        settings.WfsCustomMaxY = MaxY;
    }

    internal Dictionary<string, object> BuildCapabilitiesParameters(string? credential = null)
    {
        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["MODE"] = ConnectionMode == WfsConnectionMode.Snapshot ? "SNAPSHOT" : "ONDEMAND",
            ["MAXFEATURES"] = MaxFeatures > 0 ? MaxFeatures.ToString(CultureInfo.InvariantCulture) : "3000",
            ["SWAPXY"] = SwapXY ? "TRUE" : "FALSE",
            ["SWAPXYFILTER"] = SwapXYFilter ? "TRUE" : "FALSE"
        };

        if (PageSize > 0)
        {
            parameters["PAGESIZE"] = PageSize.ToString(CultureInfo.InvariantCulture);
        }

        var extent = ResolveExtent();
        if (extent is not null && ConnectionMode == WfsConnectionMode.Snapshot && UseSpatialExtent)
        {
            parameters["BBOX"] = BuildBBoxValue(extent.Value, SwapXYFilter);
        }

        if (!string.IsNullOrWhiteSpace(credential))
        {
            parameters["tk"] = credential.Trim();
        }

        return parameters;
    }

    internal string BuildWorkspaceConnectionString(string serviceUrl, string? credential = null)
    {
        var parts = new List<string>
        {
            $"SWAPXY={(SwapXY ? "TRUE" : "FALSE")}",
            $"SWAPXYFILTER={(SwapXYFilter ? "TRUE" : "FALSE")}",
            $"URL={serviceUrl}",
            "VERSION=2.0.0",
            $"MODE={(ConnectionMode == WfsConnectionMode.Snapshot ? "SNAPSHOT" : "ONDEMAND")}",
            $"MAXFEATURES={(MaxFeatures > 0 ? MaxFeatures : 3000).ToString(CultureInfo.InvariantCulture)}"
        };

        if (!string.IsNullOrWhiteSpace(credential))
        {
            parts.Add($"CAPABILITIES_PARAMETERS=tk={Uri.EscapeDataString(credential.Trim())}");
        }

        if (PageSize > 0)
        {
            parts.Add($"PAGESIZE={PageSize.ToString(CultureInfo.InvariantCulture)}");
        }

        var extent = ResolveExtent();
        if (extent is not null && ConnectionMode == WfsConnectionMode.Snapshot && UseSpatialExtent)
        {
            parts.Add($"BBOX={BuildBBoxValue(extent.Value, SwapXYFilter)}");
        }

        return string.Join(";", parts);
    }

    internal string? BuildAppliedBboxValue()
    {
        var extent = ResolveExtent();
        if (extent is null || ConnectionMode != WfsConnectionMode.Snapshot || !UseSpatialExtent)
        {
            return null;
        }

        return BuildBBoxValue(extent.Value, SwapXYFilter);
    }

    internal Envelope? BuildExpectedExtentEnvelope()
    {
        var extent = ResolveExtent();
        if (extent is null || ConnectionMode != WfsConnectionMode.Snapshot || !UseSpatialExtent)
        {
            return null;
        }

        return EnvelopeBuilderEx.CreateEnvelope(
            extent.Value.MinX,
            extent.Value.MinY,
            extent.Value.MaxX,
            extent.Value.MaxY,
            SpatialReferences.WGS84);
    }

    internal string GetExtentSummary()
    {
        var extent = ResolveExtent();
        if (extent is null)
        {
            return "未设置范围";
        }

        return $"XMin={FormatNumber(extent.Value.MinX)}, YMin={FormatNumber(extent.Value.MinY)}, XMax={FormatNumber(extent.Value.MaxX)}, YMax={FormatNumber(extent.Value.MaxY)}";
    }

    internal bool HasValidCustomExtent()
    {
        var extent = ResolveExtent();
        return extent is not null &&
               extent.Value.MinX < extent.Value.MaxX &&
               extent.Value.MinY < extent.Value.MaxY;
    }

    private (double MinX, double MinY, double MaxX, double MaxY)? ResolveExtent()
    {
        if (!UseSpatialExtent)
        {
            return null;
        }

        if (MinX is null || MinY is null || MaxX is null || MaxY is null)
        {
            return null;
        }

        var minX = Math.Min(MinX.Value, MaxX.Value);
        var maxX = Math.Max(MinX.Value, MaxX.Value);
        var minY = Math.Min(MinY.Value, MaxY.Value);
        var maxY = Math.Max(MinY.Value, MaxY.Value);
        return (minX, minY, maxX, maxY);
    }

    private static WfsConnectionMode ParseConnectionMode(string? value)
    {
        return Enum.TryParse(value, true, out WfsConnectionMode parsed)
            ? parsed
            : WfsConnectionMode.Snapshot;
    }

    private static WfsExtentSource ParseExtentSource(string? value)
    {
        if (Enum.TryParse(value, true, out WfsExtentSource parsed))
        {
            return parsed;
        }

        if (string.Equals(value?.Trim(), "Custom", StringComparison.OrdinalIgnoreCase))
        {
            return WfsExtentSource.CurrentView;
        }

        return WfsExtentSource.CurrentView;
    }

    private static void SetExtent(WfsLayerOptions options, Envelope extent)
    {
        options.MinX = extent.XMin;
        options.MinY = extent.YMin;
        options.MaxX = extent.XMax;
        options.MaxY = extent.YMax;
    }

    private static string BuildBBoxValue((double MinX, double MinY, double MaxX, double MaxY) extent, bool swapXYFilter)
    {
        return swapXYFilter
            ? string.Join(
                ",",
                FormatNumber(extent.MinY),
                FormatNumber(extent.MinX),
                FormatNumber(extent.MaxY),
                FormatNumber(extent.MaxX))
            : string.Join(
                ",",
                FormatNumber(extent.MinX),
                FormatNumber(extent.MinY),
                FormatNumber(extent.MaxX),
                FormatNumber(extent.MaxY));
    }

    private static Envelope? ResolvePresetExtent(
        WfsExtentSource source,
        WfsExtentContext? extentContext,
        string? layerName)
    {
        return source switch
        {
            WfsExtentSource.CurrentView => extentContext?.CurrentVisibleExtent,
            WfsExtentSource.AllLayers => extentContext?.AllLayersExtent,
            WfsExtentSource.SpecificLayer => extentContext?.FindLayer(layerName)?.Extent,
            _ => null
        };
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }
}
