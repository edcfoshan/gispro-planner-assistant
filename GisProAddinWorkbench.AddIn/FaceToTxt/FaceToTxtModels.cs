using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.FaceToTxt;

internal enum FaceToTxtInputMode
{
    CurrentSelection,
    BrowseDataset
}

internal enum FaceToTxtTxtMode
{
    StandardHeader,
    DetailOnly
}

internal enum FaceToTxtFileNameMode
{
    LayerName,
    NamingField,
    LayerNameAndNamingField
}

internal enum FaceToTxtAreaUnit
{
    Hectare,
    SquareMeter
}

internal enum FaceToTxtValidationSeverity
{
    Info,
    Warning,
    Error
}

internal sealed class FaceToTxtWorkspaceState
{
    public string LastPresetId { get; set; } = string.Empty;

    public List<FaceToTxtPresetState> Presets { get; set; } = [];
}

internal sealed class FaceToTxtPresetState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "默认配置";

    public FaceToTxtExportSettings Settings { get; set; } = new();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class FaceToTxtExportSettings
{
    public FaceToTxtInputMode InputMode { get; set; } = FaceToTxtInputMode.CurrentSelection;

    public string BrowseDatasetPath { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = string.Empty;

    public FaceToTxtTxtMode TxtMode { get; set; } = FaceToTxtTxtMode.StandardHeader;

    public FaceToTxtFileNameMode FileNameMode { get; set; } = FaceToTxtFileNameMode.LayerName;

    public string NamingField { get; set; } = string.Empty;

    public string ParcelIdField { get; set; } = string.Empty;

    public string ParcelNameField { get; set; } = string.Empty;

    public string ParcelUseField { get; set; } = string.Empty;

    public string LandCodeField { get; set; } = string.Empty;

    public string AreaField { get; set; } = string.Empty;

    public string MapSheetField { get; set; } = string.Empty;

    public bool AutoMapSheet { get; set; } = true;

    public bool UseNewStandard { get; set; } = true;

    public bool SwapXY { get; set; } = true;

    public bool IncludeClosingPoint { get; set; } = true;

    public bool RepairGeometry { get; set; } = true;

    public bool NumberHoleRings { get; set; } = true;

    public bool ReverseLength { get; set; }

    public bool CustomDescriptionEnabled { get; set; }

    public string CustomDescriptionText { get; set; } = string.Empty;

    public string ProductionUnit { get; set; } = string.Empty;

    public string ProductionDateText { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

    public string CoordinateSystemName { get; set; } = "2000国家大地坐标系";

    public string ProjectionTypeName { get; set; } = "高斯克吕格";

    public string UnitName { get; set; } = "米";

    public int BandAdjustment { get; set; }

    public int CoordinateDecimals { get; set; } = 4;

    public int AreaDecimals { get; set; } = 4;

    public int PointNumberDigits { get; set; } = 2;

    public string CoordinatePrefix { get; set; } = "J";

    public FaceToTxtAreaUnit AreaUnit { get; set; } = FaceToTxtAreaUnit.Hectare;

    public FaceToTxtExportSettings Clone()
    {
        return new FaceToTxtExportSettings
        {
            InputMode = InputMode,
            BrowseDatasetPath = BrowseDatasetPath,
            OutputDirectory = OutputDirectory,
            TxtMode = TxtMode,
            FileNameMode = FileNameMode,
            NamingField = NamingField,
            ParcelIdField = ParcelIdField,
            ParcelNameField = ParcelNameField,
            ParcelUseField = ParcelUseField,
            LandCodeField = LandCodeField,
            AreaField = AreaField,
            MapSheetField = MapSheetField,
            AutoMapSheet = AutoMapSheet,
            UseNewStandard = UseNewStandard,
            SwapXY = SwapXY,
            IncludeClosingPoint = IncludeClosingPoint,
            RepairGeometry = RepairGeometry,
            NumberHoleRings = NumberHoleRings,
            ReverseLength = ReverseLength,
            CustomDescriptionEnabled = CustomDescriptionEnabled,
            CustomDescriptionText = CustomDescriptionText,
            ProductionUnit = ProductionUnit,
            ProductionDateText = ProductionDateText,
            CoordinateSystemName = CoordinateSystemName,
            ProjectionTypeName = ProjectionTypeName,
            UnitName = UnitName,
            BandAdjustment = BandAdjustment,
            CoordinateDecimals = CoordinateDecimals,
            AreaDecimals = AreaDecimals,
            PointNumberDigits = PointNumberDigits,
            CoordinatePrefix = CoordinatePrefix,
            AreaUnit = AreaUnit
        };
    }
}

internal sealed record FaceToTxtValidationItem(
    FaceToTxtValidationSeverity Severity,
    string Message);

internal sealed class FaceToTxtSourceItem
{
    public string DisplayName { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public string DatasetName { get; set; } = string.Empty;

    public FeatureLayer? Layer { get; set; }

    public string? CatalogPath { get; set; }

    public IReadOnlyList<long> ObjectIds { get; set; } = [];

    public IReadOnlyList<SelectionOption<string>> FieldOptions { get; set; } = [];

    public IReadOnlyDictionary<string, string> SampleTextValues { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public GeometryType GeometryType { get; set; } = GeometryType.Unknown;

    public SpatialReference? SpatialReference { get; set; }

    public int FeatureCount { get; set; }
}

internal sealed class FaceToTxtSourceSnapshot
{
    public FaceToTxtInputMode InputMode { get; set; } = FaceToTxtInputMode.CurrentSelection;

    public string SummaryText { get; set; } = string.Empty;

    public string DetailText { get; set; } = string.Empty;

    public IReadOnlyList<FaceToTxtSourceItem> Items { get; set; } = [];

    public IReadOnlyList<SelectionOption<string>> FieldOptions { get; set; } = [];

    public SpatialReference? SpatialReference { get; set; }

    public int TotalFeatureCount => Items.Sum(item => item.FeatureCount);

    public bool HasItems => Items.Count > 0;

    public static FaceToTxtSourceSnapshot Empty(string summary)
    {
        return new FaceToTxtSourceSnapshot
        {
            SummaryText = summary,
            DetailText = summary
        };
    }
}

internal sealed class FaceToTxtExportResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<string> OutputPaths { get; set; } = [];

    public IReadOnlyList<string> Warnings { get; set; } = [];
}
