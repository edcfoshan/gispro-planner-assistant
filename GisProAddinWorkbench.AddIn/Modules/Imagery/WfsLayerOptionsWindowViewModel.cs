using System.Globalization;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal sealed class WfsLayerOptionsWindowViewModel : PropertyChangedBase
{
    private readonly Action<bool?> _closeAction;
    private readonly WfsExtentContext _extentContext;
    private SelectionOption<WfsConnectionMode>? _selectedConnectionMode;
    private SelectionOption<WfsExtentSource>? _selectedExtentSource;
    private SelectionOption<string>? _selectedExtentLayer;
    private bool _useSpatialExtent;
    private string _maxFeaturesText = "3000";
    private string _pageSizeText = string.Empty;
    private string _extentMinXText = string.Empty;
    private string _extentMinYText = string.Empty;
    private string _extentMaxXText = string.Empty;
    private string _extentMaxYText = string.Empty;
    private bool _swapXY = true;
    private bool _swapXYFilter;
    private string _statusText = "请先确认连接模式、范围与高级参数。";
    private bool _isApplyingOptions;
    private WfsLayerOptions? _resultOptions;

    internal WfsLayerOptionsWindowViewModel(
        ImageryServicePreset preset,
        WfsLayerOptions initialOptions,
        WfsExtentContext extentContext,
        Action<bool?> closeAction)
    {
        _closeAction = closeAction;
        _extentContext = extentContext;

        ConnectionModeOptions =
        [
            new SelectionOption<WfsConnectionMode>("快照", WfsConnectionMode.Snapshot),
            new SelectionOption<WfsConnectionMode>("按需", WfsConnectionMode.OnDemand)
        ];

        ExtentSourceOptions =
        [
            new SelectionOption<WfsExtentSource>("当前可见范围", WfsExtentSource.CurrentView),
            new SelectionOption<WfsExtentSource>("所有图层数据的范围", WfsExtentSource.AllLayers),
            new SelectionOption<WfsExtentSource>("图层的范围", WfsExtentSource.SpecificLayer)
        ];

        ExtentLayerOptions = _extentContext.LayerCandidates
            .Select(candidate => new SelectionOption<string>(candidate.Label, candidate.Label))
            .ToArray();

        PresetTitle = $"{preset.Provider} · {preset.DisplayName}";
        PresetSummary = $"{preset.Category} · {ImageryServiceCatalog.GetServiceKindLabel(preset.ServiceKind)}";
        ServiceUrlText = preset.SourceUrl;
        LayerNameText = ImageryServiceCatalog.BuildLayerName(preset);
        ServiceKindText = ImageryServiceCatalog.GetServiceKindLabel(preset.ServiceKind);
        DisplayUnitsText = "显示单位：地图显示（十进制度）";

        ApplyOptions(initialOptions);

        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(() => _closeAction(false));
    }

    public string PresetTitle { get; }

    public string PresetSummary { get; }

    public string ServiceUrlText { get; }

    public string LayerNameText { get; }

    public string ServiceKindText { get; }

    public string DisplayUnitsText { get; }

    public IReadOnlyList<SelectionOption<WfsConnectionMode>> ConnectionModeOptions { get; }

    public IReadOnlyList<SelectionOption<WfsExtentSource>> ExtentSourceOptions { get; }

    public IReadOnlyList<SelectionOption<string>> ExtentLayerOptions { get; }

    public SelectionOption<WfsConnectionMode>? SelectedConnectionMode
    {
        get => _selectedConnectionMode;
        set
        {
            if (!SetProperty(ref _selectedConnectionMode, value, nameof(SelectedConnectionMode)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(IsSnapshotMode));
            NotifyPropertyChanged(nameof(IsSpatialExtentEnabled));
            NotifyPropertyChanged(nameof(IsLayerExtentEnabled));
        }
    }

    public SelectionOption<WfsExtentSource>? SelectedExtentSource
    {
        get => _selectedExtentSource;
        set
        {
            if (!SetProperty(ref _selectedExtentSource, value, nameof(SelectedExtentSource)))
            {
                return;
            }

            EnsureLayerSelection();
            SyncExtentFieldsFromSelectedSource();
            NotifyPropertyChanged(nameof(IsLayerExtentEnabled));
            NotifyPropertyChanged(nameof(SelectedExtentText));
        }
    }

    public SelectionOption<string>? SelectedExtentLayer
    {
        get => _selectedExtentLayer;
        set
        {
            if (!SetProperty(ref _selectedExtentLayer, value, nameof(SelectedExtentLayer)))
            {
                return;
            }

            SyncExtentFieldsFromSelectedSource();
            NotifyPropertyChanged(nameof(SelectedExtentText));
        }
    }

    public bool UseSpatialExtent
    {
        get => _useSpatialExtent;
        set
        {
            if (!SetProperty(ref _useSpatialExtent, value, nameof(UseSpatialExtent)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(IsSpatialExtentEnabled));
            NotifyPropertyChanged(nameof(IsLayerExtentEnabled));
        }
    }

    public string MaxFeaturesText
    {
        get => _maxFeaturesText;
        set => SetProperty(ref _maxFeaturesText, value ?? string.Empty, nameof(MaxFeaturesText));
    }

    public string PageSizeText
    {
        get => _pageSizeText;
        set => SetProperty(ref _pageSizeText, value ?? string.Empty, nameof(PageSizeText));
    }

    public string ExtentMinXText
    {
        get => _extentMinXText;
        set
        {
            if (!SetProperty(ref _extentMinXText, value ?? string.Empty, nameof(ExtentMinXText)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(SelectedExtentText));
        }
    }

    public string ExtentMinYText
    {
        get => _extentMinYText;
        set
        {
            if (!SetProperty(ref _extentMinYText, value ?? string.Empty, nameof(ExtentMinYText)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(SelectedExtentText));
        }
    }

    public string ExtentMaxXText
    {
        get => _extentMaxXText;
        set
        {
            if (!SetProperty(ref _extentMaxXText, value ?? string.Empty, nameof(ExtentMaxXText)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(SelectedExtentText));
        }
    }

    public string ExtentMaxYText
    {
        get => _extentMaxYText;
        set
        {
            if (!SetProperty(ref _extentMaxYText, value ?? string.Empty, nameof(ExtentMaxYText)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(SelectedExtentText));
        }
    }

    public bool SwapXY
    {
        get => _swapXY;
        set => SetProperty(ref _swapXY, value, nameof(SwapXY));
    }

    public bool SwapXYFilter
    {
        get => _swapXYFilter;
        set => SetProperty(ref _swapXYFilter, value, nameof(SwapXYFilter));
    }

    public bool IsSnapshotMode => SelectedConnectionMode?.Value == WfsConnectionMode.Snapshot;

    public bool IsSpatialExtentEnabled => UseSpatialExtent && IsSnapshotMode;

    public bool IsLayerExtentEnabled => IsSpatialExtentEnabled && SelectedExtentSource?.Value == WfsExtentSource.SpecificLayer;

    public string SelectedExtentText => BuildSelectedExtentText();

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value, nameof(StatusText));
    }

    public WfsLayerOptions? ResultOptions => _resultOptions;

    public RelayCommand ConfirmCommand { get; }

    public RelayCommand CancelCommand { get; }

    private void ApplyOptions(WfsLayerOptions options)
    {
        _isApplyingOptions = true;
        try
        {
            SelectedConnectionMode = ConnectionModeOptions.FirstOrDefault(option => option.Value == options.ConnectionMode)
                                     ?? ConnectionModeOptions.First();
            SelectedExtentSource = ExtentSourceOptions.FirstOrDefault(option => option.Value == options.ExtentSource)
                                   ?? ExtentSourceOptions.First();

            if (!string.IsNullOrWhiteSpace(options.ExtentLayerName))
            {
                var matchedLayer = ExtentLayerOptions.FirstOrDefault(option =>
                    option.Value.Equals(options.ExtentLayerName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (matchedLayer is not null)
                {
                    SelectedExtentLayer = matchedLayer;
                }
            }

            UseSpatialExtent = options.UseSpatialExtent;
            MaxFeaturesText = options.MaxFeatures > 0 ? options.MaxFeatures.ToString(CultureInfo.InvariantCulture) : "3000";
            PageSizeText = options.PageSize > 0 ? options.PageSize.ToString(CultureInfo.InvariantCulture) : string.Empty;
            SwapXY = options.SwapXY;
            SwapXYFilter = options.SwapXYFilter;

            if (TryResolveBorrowedExtent(out var extent))
            {
                ApplyExtentToFields(extent);
            }
            else if (options.MinX is not null &&
                     options.MinY is not null &&
                     options.MaxX is not null &&
                     options.MaxY is not null)
            {
                ExtentMinXText = options.MinX.Value.ToString("G17", CultureInfo.InvariantCulture);
                ExtentMinYText = options.MinY.Value.ToString("G17", CultureInfo.InvariantCulture);
                ExtentMaxXText = options.MaxX.Value.ToString("G17", CultureInfo.InvariantCulture);
                ExtentMaxYText = options.MaxY.Value.ToString("G17", CultureInfo.InvariantCulture);
            }

            EnsureLayerSelection();
        }
        finally
        {
            _isApplyingOptions = false;
        }

        NotifyPropertyChanged(nameof(SelectedExtentText));
        NotifyPropertyChanged(nameof(IsLayerExtentEnabled));
    }

    private void Confirm()
    {
        if (!TryBuildOptions(out var options))
        {
            return;
        }

        _resultOptions = options;
        _closeAction(true);
    }

    private bool TryBuildOptions(out WfsLayerOptions options)
    {
        options = new WfsLayerOptions();

        if (!TryParsePositiveInt(MaxFeaturesText, out var maxFeatures))
        {
            StatusText = "最大要素数请输入有效的正整数。";
            return false;
        }

        if (!TryParseOptionalNonNegativeInt(PageSizeText, out var pageSize))
        {
            StatusText = "页面大小请输入非负整数，留空表示使用默认值。";
            return false;
        }

        var selectedMode = SelectedConnectionMode?.Value ?? WfsConnectionMode.Snapshot;
        var selectedExtentSource = SelectedExtentSource?.Value ?? WfsExtentSource.CurrentView;

        double? minX = null;
        double? minY = null;
        double? maxX = null;
        double? maxY = null;

        if (UseSpatialExtent && selectedMode == WfsConnectionMode.Snapshot)
        {
            if (selectedExtentSource == WfsExtentSource.SpecificLayer && SelectedExtentLayer is null)
            {
                StatusText = "请选择一个图层作为范围来源。";
                return false;
            }

            if (!TryParseDouble(ExtentMinXText, out var minXValue) ||
                !TryParseDouble(ExtentMinYText, out var minYValue) ||
                !TryParseDouble(ExtentMaxXText, out var maxXValue) ||
                !TryParseDouble(ExtentMaxYText, out var maxYValue))
            {
                StatusText = "空间范围请填写完整且有效的数值。";
                return false;
            }

            minX = minXValue;
            minY = minYValue;
            maxX = maxXValue;
            maxY = maxYValue;

            if (minX >= maxX || minY >= maxY)
            {
                StatusText = "空间范围的最小值必须小于最大值。";
                return false;
            }
        }

        options.ConnectionMode = selectedMode;
        options.MaxFeatures = maxFeatures;
        options.PageSize = pageSize;
        options.UseSpatialExtent = UseSpatialExtent && selectedMode == WfsConnectionMode.Snapshot;
        options.ExtentSource = selectedExtentSource;
        options.ExtentLayerName = selectedExtentSource == WfsExtentSource.SpecificLayer
            ? SelectedExtentLayer?.Value ?? string.Empty
            : string.Empty;
        options.MinX = minX;
        options.MinY = minY;
        options.MaxX = maxX;
        options.MaxY = maxY;
        options.SwapXY = SwapXY;
        options.SwapXYFilter = SwapXYFilter;
        return true;
    }

    private void EnsureLayerSelection()
    {
        if (SelectedExtentSource?.Value != WfsExtentSource.SpecificLayer)
        {
            return;
        }

        if (SelectedExtentLayer is not null &&
            ExtentLayerOptions.Any(option =>
                option.Value.Equals(SelectedExtentLayer.Value, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedExtentLayer = ExtentLayerOptions.FirstOrDefault();
    }

    private void SyncExtentFieldsFromSelectedSource()
    {
        if (_isApplyingOptions || !IsSpatialExtentEnabled)
        {
            return;
        }

        if (!TryResolveBorrowedExtent(out var extent))
        {
            return;
        }

        ApplyExtentToFields(extent);
        NotifyPropertyChanged(nameof(SelectedExtentText));
    }

    private bool TryResolveBorrowedExtent(out Envelope extent)
    {
        extent = null!;

        var selectedSource = SelectedExtentSource?.Value ?? WfsExtentSource.CurrentView;
        Envelope? resolved = selectedSource switch
        {
            WfsExtentSource.CurrentView => _extentContext.CurrentVisibleExtent,
            WfsExtentSource.AllLayers => _extentContext.AllLayersExtent,
            WfsExtentSource.SpecificLayer => ResolveSelectedLayerExtent(),
            _ => null
        };

        if (resolved is null)
        {
            return false;
        }

        extent = resolved;
        return true;
    }

    private Envelope? ResolveSelectedLayerExtent()
    {
        var selectedLayerLabel = SelectedExtentLayer?.Value;
        if (string.IsNullOrWhiteSpace(selectedLayerLabel))
        {
            return null;
        }

        return _extentContext.FindLayer(selectedLayerLabel)?.Extent;
    }

    private void ApplyExtentToFields(Envelope extent)
    {
        ExtentMinXText = extent.XMin.ToString("G17", CultureInfo.InvariantCulture);
        ExtentMinYText = extent.YMin.ToString("G17", CultureInfo.InvariantCulture);
        ExtentMaxXText = extent.XMax.ToString("G17", CultureInfo.InvariantCulture);
        ExtentMaxYText = extent.YMax.ToString("G17", CultureInfo.InvariantCulture);
    }

    private string BuildSelectedExtentText()
    {
        if (!TryParseDouble(ExtentMinXText, out var minX) ||
            !TryParseDouble(ExtentMinYText, out var minY) ||
            !TryParseDouble(ExtentMaxXText, out var maxX) ||
            !TryParseDouble(ExtentMaxYText, out var maxY))
        {
            return "范围数值尚未完整，请先确认来源或手动填写坐标。";
        }

        var normalizedExtent = NormalizeExtent(minX, minY, maxX, maxY);
        return $"{GetSelectedExtentSourceLabel()}\n{BuildExtentCoordinateText(normalizedExtent)}";
    }

    private string GetSelectedExtentSourceLabel()
    {
        return (SelectedExtentSource?.Value ?? WfsExtentSource.CurrentView) switch
        {
            WfsExtentSource.CurrentView => "当前可见范围",
            WfsExtentSource.AllLayers => "所有图层数据的范围",
            WfsExtentSource.SpecificLayer => SelectedExtentLayer?.Label is { Length: > 0 } label
                ? $"图层的范围：{label}"
                : "图层的范围",
            _ => "所选范围"
        };
    }

    private static string BuildExtentCoordinateText(Envelope extent)
    {
        return $"顶部 {FormatCoordinate(extent.YMax, true)}\n" +
               $"左侧 {FormatCoordinate(extent.XMin, false)}\n" +
               $"右侧 {FormatCoordinate(extent.XMax, false)}\n" +
               $"底部 {FormatCoordinate(extent.YMin, true)}";
    }

    private static Envelope NormalizeExtent(double minX, double minY, double maxX, double maxY)
    {
        var normalizedMinX = Math.Min(minX, maxX);
        var normalizedMaxX = Math.Max(minX, maxX);
        var normalizedMinY = Math.Min(minY, maxY);
        var normalizedMaxY = Math.Max(minY, maxY);
        return EnvelopeBuilderEx.CreateEnvelope(
            normalizedMinX,
            normalizedMinY,
            normalizedMaxX,
            normalizedMaxY,
            SpatialReferences.WGS84);
    }

    private static string FormatCoordinate(double value, bool latitude)
    {
        var direction = latitude
            ? (value >= 0 ? "北" : "南")
            : (value >= 0 ? "东" : "西");

        return $"{Math.Abs(value):G17}°{direction}";
    }

    private static bool TryParsePositiveInt(string? value, out int result)
    {
        if (int.TryParse(value, out result) && result > 0)
        {
            return true;
        }

        result = 0;
        return false;
    }

    private static bool TryParseOptionalNonNegativeInt(string? value, out int result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = 0;
            return true;
        }

        if (int.TryParse(value, out result) && result >= 0)
        {
            return true;
        }

        result = 0;
        return false;
    }

    private static bool TryParseDouble(string? value, out double result)
    {
        return double.TryParse(value, out result) ||
               double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
