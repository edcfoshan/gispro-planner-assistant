using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Framework.Contracts;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.FaceToTxt;

internal sealed partial class FaceToTxtWorkbenchViewModel : PropertyChangedBase
{
    private static readonly FaceToTxtService Service = new();

    internal static FaceToTxtWorkbenchViewModel Shared { get; } = new();

    private FaceToTxtWorkspaceState _workspaceState = FaceToTxtSettingsStore.CreateDefaultState();
    private FaceToTxtPresetState _selectedPreset = FaceToTxtSettingsStore.CreateDefaultState().Presets[0];
    private FaceToTxtSourceSnapshot _sourceSnapshot = FaceToTxtSourceSnapshot.Empty("正在加载配置...");

    private readonly ObservableCollection<SelectionOption<string>> _presetOptions = [];
    private readonly ObservableCollection<SelectionOption<string>> _fieldOptions = [];
    private readonly ObservableCollection<FaceToTxtValidationItem> _validationItems = [];

    private SelectionOption<string>? _selectedPresetOption;
    private SelectionOption<FaceToTxtInputMode>? _selectedInputModeOption;
    private SelectionOption<FaceToTxtTxtMode>? _selectedTxtModeOption;
    private SelectionOption<FaceToTxtFileNameMode>? _selectedFileNameModeOption;
    private SelectionOption<FaceToTxtAreaUnit>? _selectedAreaUnitOption;
    private string _presetName = "默认配置";
    private string _browseDatasetPath = string.Empty;
    private string _outputDirectory = FaceToTxtSettingsStore.GetDefaultOutputDirectory();
    private string _namingField = string.Empty;
    private string _parcelIdField = string.Empty;
    private string _parcelNameField = string.Empty;
    private string _parcelUseField = string.Empty;
    private string _landCodeField = string.Empty;
    private string _areaField = string.Empty;
    private string _mapSheetField = string.Empty;
    private string _productionUnit = string.Empty;
    private string _productionDateText = DateTime.Today.ToString("yyyy-MM-dd");
    private string _customDescriptionText = string.Empty;
    private string _coordinatePrefix = "J";
    private string _statusText = "请先加载面转 TXT 配置。";
    private string _previewText = "配置加载后会在这里显示 TXT 结构预览。";
    private int _bandAdjustment;
    private int _coordinateDecimals = 4;
    private int _areaDecimals = 4;
    private int _pointNumberDigits = 2;
    private bool _autoMapSheet = true;
    private bool _useNewStandard = true;
    private bool _swapXY = true;
    private bool _includeClosingPoint = true;
    private bool _repairGeometry = true;
    private bool _numberHoleRings = true;
    private bool _reverseLength;
    private bool _customDescriptionEnabled;
    private bool _isInitialized;
    private bool _isLoading;
    private bool _isRefreshingSource;
    private bool _isDirty;

    private FaceToTxtWorkbenchViewModel()
    {
        InputModeOptions =
        [
            new SelectionOption<FaceToTxtInputMode>("当前选择集", FaceToTxtInputMode.CurrentSelection),
            new SelectionOption<FaceToTxtInputMode>("浏览数据集", FaceToTxtInputMode.BrowseDataset)
        ];

        TxtModeOptions =
        [
            new SelectionOption<FaceToTxtTxtMode>("标准头部", FaceToTxtTxtMode.StandardHeader),
            new SelectionOption<FaceToTxtTxtMode>("仅明细", FaceToTxtTxtMode.DetailOnly)
        ];

        FileNameModeOptions =
        [
            new SelectionOption<FaceToTxtFileNameMode>("图层名称", FaceToTxtFileNameMode.LayerName),
            new SelectionOption<FaceToTxtFileNameMode>("命名字段", FaceToTxtFileNameMode.NamingField),
            new SelectionOption<FaceToTxtFileNameMode>("图层 + 命名字段", FaceToTxtFileNameMode.LayerNameAndNamingField)
        ];

        AreaUnitOptions =
        [
            new SelectionOption<FaceToTxtAreaUnit>("公顷", FaceToTxtAreaUnit.Hectare),
            new SelectionOption<FaceToTxtAreaUnit>("平方米", FaceToTxtAreaUnit.SquareMeter)
        ];

        RefreshSourceCommand = new AsyncRelayCommand(RefreshSourceAsync, () => _isInitialized);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => CanExport);
        SavePresetCommand = new AsyncRelayCommand(() => SaveCurrentPresetAsync(), () => _isInitialized);
        NewPresetCommand = new AsyncRelayCommand(CreatePresetAsync, () => _isInitialized);
        DeletePresetCommand = new AsyncRelayCommand(DeletePresetAsync, () => CanDeletePreset);
        BrowseDatasetCommand = new RelayCommand(BrowseDataset);
        BrowseOutputCommand = new RelayCommand(BrowseOutputDirectory);
    }

    public ObservableCollection<SelectionOption<string>> PresetOptions => _presetOptions;

    public ObservableCollection<SelectionOption<string>> FieldOptions => _fieldOptions;

    public ObservableCollection<FaceToTxtValidationItem> ValidationItems => _validationItems;

    public IReadOnlyList<SelectionOption<FaceToTxtInputMode>> InputModeOptions { get; }

    public IReadOnlyList<SelectionOption<FaceToTxtTxtMode>> TxtModeOptions { get; }

    public IReadOnlyList<SelectionOption<FaceToTxtFileNameMode>> FileNameModeOptions { get; }

    public IReadOnlyList<SelectionOption<FaceToTxtAreaUnit>> AreaUnitOptions { get; }

    public SelectionOption<string>? SelectedPresetOption
    {
        get => _selectedPresetOption;
        set
        {
            if (!SetProperty(ref _selectedPresetOption, value, nameof(SelectedPresetOption)))
            {
                return;
            }

            if (!_isLoading)
            {
                _ = SwitchPresetAsync(value?.Value);
            }
        }
    }

    public SelectionOption<FaceToTxtInputMode>? SelectedInputModeOption
    {
        get => _selectedInputModeOption;
        set
        {
            if (!SetProperty(ref _selectedInputModeOption, value, nameof(SelectedInputModeOption)))
            {
                return;
            }

            if (value is not null)
            {
                _selectedPreset.Settings.InputMode = value.Value;
            }

            OnSettingsChanged(refreshSource: true);
        }
    }

    public SelectionOption<FaceToTxtTxtMode>? SelectedTxtModeOption
    {
        get => _selectedTxtModeOption;
        set
        {
            if (!SetProperty(ref _selectedTxtModeOption, value, nameof(SelectedTxtModeOption)))
            {
                return;
            }

            if (value is not null)
            {
                _selectedPreset.Settings.TxtMode = value.Value;
            }

            OnSettingsChanged();
        }
    }

    public SelectionOption<FaceToTxtFileNameMode>? SelectedFileNameModeOption
    {
        get => _selectedFileNameModeOption;
        set
        {
            if (!SetProperty(ref _selectedFileNameModeOption, value, nameof(SelectedFileNameModeOption)))
            {
                return;
            }

            if (value is not null)
            {
                _selectedPreset.Settings.FileNameMode = value.Value;
            }

            OnSettingsChanged();
        }
    }

    public SelectionOption<FaceToTxtAreaUnit>? SelectedAreaUnitOption
    {
        get => _selectedAreaUnitOption;
        set
        {
            if (!SetProperty(ref _selectedAreaUnitOption, value, nameof(SelectedAreaUnitOption)))
            {
                return;
            }

            if (value is not null)
            {
                _selectedPreset.Settings.AreaUnit = value.Value;
            }

            OnSettingsChanged();
        }
    }

    public string PresetName
    {
        get => _presetName;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _presetName, normalized, nameof(PresetName)))
            {
                return;
            }

            if (_isLoading)
            {
                return;
            }

            _selectedPreset.Name = string.IsNullOrWhiteSpace(normalized) ? "默认配置" : normalized;
            RebuildPresetOptions(selectCurrent: true);
            MarkDirty();
        }
    }

    public string BrowseDatasetPath
    {
        get => _browseDatasetPath;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _browseDatasetPath, normalized, nameof(BrowseDatasetPath)))
            {
                return;
            }

            _selectedPreset.Settings.BrowseDatasetPath = normalized;
            OnSettingsChanged(refreshSource: SelectedInputMode == FaceToTxtInputMode.BrowseDataset);
        }
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _outputDirectory, normalized, nameof(OutputDirectory)))
            {
                return;
            }

            _selectedPreset.Settings.OutputDirectory = normalized;
            OnSettingsChanged();
        }
    }

    public string NamingField
    {
        get => _namingField;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _namingField, normalized, nameof(NamingField)))
            {
                return;
            }

            _selectedPreset.Settings.NamingField = normalized;
            OnSettingsChanged();
        }
    }

    public string ParcelIdField
    {
        get => _parcelIdField;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _parcelIdField, normalized, nameof(ParcelIdField)))
            {
                return;
            }

            _selectedPreset.Settings.ParcelIdField = normalized;
            OnSettingsChanged();
        }
    }

    public string ParcelNameField
    {
        get => _parcelNameField;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _parcelNameField, normalized, nameof(ParcelNameField)))
            {
                return;
            }

            _selectedPreset.Settings.ParcelNameField = normalized;
            OnSettingsChanged();
        }
    }

    public string ParcelUseField
    {
        get => _parcelUseField;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _parcelUseField, normalized, nameof(ParcelUseField)))
            {
                return;
            }

            _selectedPreset.Settings.ParcelUseField = normalized;
            OnSettingsChanged();
        }
    }

    public string LandCodeField
    {
        get => _landCodeField;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _landCodeField, normalized, nameof(LandCodeField)))
            {
                return;
            }

            _selectedPreset.Settings.LandCodeField = normalized;
            OnSettingsChanged();
        }
    }

    public string AreaField
    {
        get => _areaField;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _areaField, normalized, nameof(AreaField)))
            {
                return;
            }

            _selectedPreset.Settings.AreaField = normalized;
            OnSettingsChanged();
        }
    }

    public string MapSheetField
    {
        get => _mapSheetField;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _mapSheetField, normalized, nameof(MapSheetField)))
            {
                return;
            }

            _selectedPreset.Settings.MapSheetField = normalized;
            OnSettingsChanged();
        }
    }

    public string ProductionUnit
    {
        get => _productionUnit;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _productionUnit, normalized, nameof(ProductionUnit)))
            {
                return;
            }

            _selectedPreset.Settings.ProductionUnit = normalized;
            OnSettingsChanged();
        }
    }

    public string ProductionDateText
    {
        get => _productionDateText;
        set
        {
            var normalized = NormalizeText(value);
            if (!SetProperty(ref _productionDateText, normalized, nameof(ProductionDateText)))
            {
                return;
            }

            _selectedPreset.Settings.ProductionDateText = string.IsNullOrWhiteSpace(normalized)
                ? DateTime.Today.ToString("yyyy-MM-dd")
                : normalized;
            OnSettingsChanged();
        }
    }

    public string CustomDescriptionText
    {
        get => _customDescriptionText;
        set
        {
            var normalized = value ?? string.Empty;
            if (!SetProperty(ref _customDescriptionText, normalized, nameof(CustomDescriptionText)))
            {
                return;
            }

            _selectedPreset.Settings.CustomDescriptionText = normalized;
            OnSettingsChanged();
        }
    }

    public string CoordinatePrefix
    {
        get => _coordinatePrefix;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "J" : value.Trim();
            if (!SetProperty(ref _coordinatePrefix, normalized, nameof(CoordinatePrefix)))
            {
                return;
            }

            _selectedPreset.Settings.CoordinatePrefix = normalized;
            OnSettingsChanged();
        }
    }

    public int BandAdjustment
    {
        get => _bandAdjustment;
        set
        {
            if (!SetProperty(ref _bandAdjustment, value, nameof(BandAdjustment)))
            {
                return;
            }

            _selectedPreset.Settings.BandAdjustment = value;
            OnSettingsChanged();
        }
    }

    public int CoordinateDecimals
    {
        get => _coordinateDecimals;
        set
        {
            var normalized = Math.Max(0, value);
            if (!SetProperty(ref _coordinateDecimals, normalized, nameof(CoordinateDecimals)))
            {
                return;
            }

            _selectedPreset.Settings.CoordinateDecimals = normalized;
            OnSettingsChanged();
        }
    }

    public int AreaDecimals
    {
        get => _areaDecimals;
        set
        {
            var normalized = Math.Max(0, value);
            if (!SetProperty(ref _areaDecimals, normalized, nameof(AreaDecimals)))
            {
                return;
            }

            _selectedPreset.Settings.AreaDecimals = normalized;
            OnSettingsChanged();
        }
    }

    public int PointNumberDigits
    {
        get => _pointNumberDigits;
        set
        {
            var normalized = Math.Max(1, value);
            if (!SetProperty(ref _pointNumberDigits, normalized, nameof(PointNumberDigits)))
            {
                return;
            }

            _selectedPreset.Settings.PointNumberDigits = normalized;
            OnSettingsChanged();
        }
    }

    public bool AutoMapSheet
    {
        get => _autoMapSheet;
        set
        {
            if (!SetProperty(ref _autoMapSheet, value, nameof(AutoMapSheet)))
            {
                return;
            }

            _selectedPreset.Settings.AutoMapSheet = value;
            OnSettingsChanged(refreshSource: true);
        }
    }

    public bool UseNewStandard
    {
        get => _useNewStandard;
        set
        {
            if (!SetProperty(ref _useNewStandard, value, nameof(UseNewStandard)))
            {
                return;
            }

            _selectedPreset.Settings.UseNewStandard = value;
            OnSettingsChanged();
        }
    }

    public bool SwapXY
    {
        get => _swapXY;
        set
        {
            if (!SetProperty(ref _swapXY, value, nameof(SwapXY)))
            {
                return;
            }

            _selectedPreset.Settings.SwapXY = value;
            OnSettingsChanged();
        }
    }

    public bool IncludeClosingPoint
    {
        get => _includeClosingPoint;
        set
        {
            if (!SetProperty(ref _includeClosingPoint, value, nameof(IncludeClosingPoint)))
            {
                return;
            }

            _selectedPreset.Settings.IncludeClosingPoint = value;
            OnSettingsChanged();
        }
    }

    public bool RepairGeometry
    {
        get => _repairGeometry;
        set
        {
            if (!SetProperty(ref _repairGeometry, value, nameof(RepairGeometry)))
            {
                return;
            }

            _selectedPreset.Settings.RepairGeometry = value;
            OnSettingsChanged();
        }
    }

    public bool NumberHoleRings
    {
        get => _numberHoleRings;
        set
        {
            if (!SetProperty(ref _numberHoleRings, value, nameof(NumberHoleRings)))
            {
                return;
            }

            _selectedPreset.Settings.NumberHoleRings = value;
            OnSettingsChanged();
        }
    }

    public bool ReverseLength
    {
        get => _reverseLength;
        set
        {
            if (!SetProperty(ref _reverseLength, value, nameof(ReverseLength)))
            {
                return;
            }

            _selectedPreset.Settings.ReverseLength = value;
            OnSettingsChanged();
        }
    }

    public bool CustomDescriptionEnabled
    {
        get => _customDescriptionEnabled;
        set
        {
            if (!SetProperty(ref _customDescriptionEnabled, value, nameof(CustomDescriptionEnabled)))
            {
                return;
            }

            _selectedPreset.Settings.CustomDescriptionEnabled = value;
            OnSettingsChanged();
        }
    }

    public string SourceSummaryText => _sourceSnapshot.SummaryText;

    public string SourceDetailText => _sourceSnapshot.DetailText;

    public string CoordinateSystemName => _selectedPreset.Settings.CoordinateSystemName;

    public string ProjectionTypeName => _selectedPreset.Settings.ProjectionTypeName;

    public string UnitName => _selectedPreset.Settings.UnitName;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value, nameof(StatusText));
    }

    public string PreviewText
    {
        get => _previewText;
        private set => SetProperty(ref _previewText, value, nameof(PreviewText));
    }

    public bool CanDeletePreset => _workspaceState.Presets.Count > 1;

    public bool HasUnsavedChanges => _isDirty;

    public bool CanExport => !_isRefreshingSource && _sourceSnapshot.HasItems && _validationItems.All(item => item.Severity != FaceToTxtValidationSeverity.Error);

    public AsyncRelayCommand RefreshSourceCommand { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public AsyncRelayCommand SavePresetCommand { get; }

    public AsyncRelayCommand NewPresetCommand { get; }

    public AsyncRelayCommand DeletePresetCommand { get; }

    public RelayCommand BrowseDatasetCommand { get; }

    public RelayCommand BrowseOutputCommand { get; }

    internal async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isLoading = true;
        try
        {
            await FaceToTxtSettingsStore.EnsureTemplateExistsAsync();
            _workspaceState = await FaceToTxtSettingsStore.LoadAsync();
            if (_workspaceState.Presets.Count == 0)
            {
                _workspaceState = FaceToTxtSettingsStore.CreateDefaultState();
            }

            _selectedPreset = _workspaceState.Presets.FirstOrDefault(preset => preset.Id.Equals(_workspaceState.LastPresetId, StringComparison.OrdinalIgnoreCase))
                              ?? _workspaceState.Presets[0];
            ApplyPresetState(_selectedPreset);
            RebuildPresetOptions(selectCurrent: true);
            RefreshPreviewAndValidation();
            _isInitialized = true;
            StatusText = $"已加载 {PresetName}，配置保存在 {FaceToTxtSettingsStore.GetSettingsPath()}。";
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(FaceToTxtWorkbenchViewModel), "初始化面转 TXT 工作台失败。", exception);
            StatusText = $"初始化失败：{exception.Message}";
            _workspaceState = FaceToTxtSettingsStore.CreateDefaultState();
            _selectedPreset = _workspaceState.Presets[0];
            ApplyPresetState(_selectedPreset);
            RebuildPresetOptions(selectCurrent: true);
            RefreshPreviewAndValidation();
        }
        finally
        {
            _isLoading = false;
            RefreshSourceCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
            SavePresetCommand.RaiseCanExecuteChanged();
            NewPresetCommand.RaiseCanExecuteChanged();
            DeletePresetCommand.RaiseCanExecuteChanged();
        }
    }

    internal void FlushChanges()
    {
        if (!_isInitialized)
        {
            return;
        }

        try
        {
            _selectedPreset.Name = string.IsNullOrWhiteSpace(PresetName) ? "默认配置" : PresetName.Trim();
            _selectedPreset.Settings = CaptureCurrentSettings();
            _selectedPreset.UpdatedAt = DateTimeOffset.Now;
            _workspaceState.LastPresetId = _selectedPreset.Id;
            FaceToTxtSettingsStore.Save(_workspaceState);
            _isDirty = false;
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(FaceToTxtWorkbenchViewModel), "关闭窗口时保存面转 TXT 配置失败。", exception);
        }
    }

    private FaceToTxtInputMode SelectedInputMode => _selectedInputModeOption?.Value ?? FaceToTxtInputMode.CurrentSelection;

    private FaceToTxtTxtMode SelectedTxtMode => _selectedTxtModeOption?.Value ?? FaceToTxtTxtMode.StandardHeader;

    private FaceToTxtFileNameMode SelectedFileNameMode => _selectedFileNameModeOption?.Value ?? FaceToTxtFileNameMode.LayerName;

    private FaceToTxtAreaUnit SelectedAreaUnit => _selectedAreaUnitOption?.Value ?? FaceToTxtAreaUnit.Hectare;

    private async Task SwitchPresetAsync(string? presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
        {
            return;
        }

        var nextPreset = _workspaceState.Presets.FirstOrDefault(preset => preset.Id.Equals(presetId, StringComparison.OrdinalIgnoreCase));
        if (nextPreset is null || ReferenceEquals(nextPreset, _selectedPreset))
        {
            return;
        }

        await SaveCurrentPresetAsync(persistToDisk: true);

        _isLoading = true;
        try
        {
            _selectedPreset = nextPreset;
            ApplyPresetState(nextPreset);
            RebuildPresetOptions(selectCurrent: true);
            await RefreshSourceAsync();
            StatusText = $"已切换到配置：{PresetName}";
        }
        finally
        {
            _isLoading = false;
            RefreshPreviewAndValidation();
        }
    }

    private async Task CreatePresetAsync()
    {
        await SaveCurrentPresetAsync(persistToDisk: true);

        var clone = new FaceToTxtPresetState
        {
            Name = BuildNewPresetName(),
            Settings = CaptureCurrentSettings(),
            UpdatedAt = DateTimeOffset.Now
        };

        _workspaceState.Presets.Add(clone);
        _workspaceState.LastPresetId = clone.Id;
        _selectedPreset = clone;
        ApplyPresetState(clone);
        RebuildPresetOptions(selectCurrent: true);
        await PersistWorkspaceStateAsync();
        RefreshPreviewAndValidation();
        StatusText = $"已新建配置：{PresetName}";
        DeletePresetCommand.RaiseCanExecuteChanged();
        NewPresetCommand.RaiseCanExecuteChanged();
    }

    private async Task DeletePresetAsync()
    {
        if (_workspaceState.Presets.Count <= 1)
        {
            StatusText = "至少需要保留一个配置。";
            return;
        }

        var confirm = MessageBox.Show(
            $"确定删除配置“{PresetName}”吗？",
            "删除配置",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var currentId = _selectedPreset.Id;
        _workspaceState.Presets.Remove(_selectedPreset);
        _selectedPreset = _workspaceState.Presets.First();
        _workspaceState.LastPresetId = _selectedPreset.Id;
        ApplyPresetState(_selectedPreset);
        RebuildPresetOptions(selectCurrent: true);
        await PersistWorkspaceStateAsync();
        RefreshPreviewAndValidation();
        StatusText = $"已删除配置：{currentId}";
        DeletePresetCommand.RaiseCanExecuteChanged();
    }

    private async Task SaveCurrentPresetAsync(bool persistToDisk = true)
    {
        if (!_isInitialized)
        {
            return;
        }

        _selectedPreset.Name = string.IsNullOrWhiteSpace(PresetName) ? "默认配置" : PresetName.Trim();
        _selectedPreset.Settings = CaptureCurrentSettings();
        _selectedPreset.UpdatedAt = DateTimeOffset.Now;
        _workspaceState.LastPresetId = _selectedPreset.Id;
        RebuildPresetOptions(selectCurrent: true);

        if (persistToDisk)
        {
            await PersistWorkspaceStateAsync();
        }

        _isDirty = false;
        StatusText = $"配置已保存：{PresetName}";
    }

    private async Task PersistWorkspaceStateAsync()
    {
        await FaceToTxtSettingsStore.SaveAsync(_workspaceState);
    }

    private async Task RefreshSourceAsync()
    {
        if (!_isInitialized && !_isLoading)
        {
            return;
        }

        _isRefreshingSource = true;
        try
        {
            if (SelectedInputMode == FaceToTxtInputMode.CurrentSelection)
            {
                _sourceSnapshot = await Service.InspectCurrentSelectionAsync();
            }
            else
            {
                _sourceSnapshot = string.IsNullOrWhiteSpace(BrowseDatasetPath)
                    ? FaceToTxtSourceSnapshot.Empty("请先浏览并选择一个面状要素类。")
                    : await Service.InspectDatasetAsync(BrowseDatasetPath);
            }

            SyncFieldOptions(_sourceSnapshot.FieldOptions);
            ApplyAutoMappings();
            NotifyPropertyChanged(nameof(SourceSummaryText));
            NotifyPropertyChanged(nameof(SourceDetailText));
            RefreshPreviewAndValidation();
            StatusText = _sourceSnapshot.SummaryText;
        }
        finally
        {
            _isRefreshingSource = false;
            RefreshPreviewAndValidation();
            RefreshSourceCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task ExportAsync()
    {
        await RefreshSourceAsync();
        RefreshPreviewAndValidation();

        if (!CanExport)
        {
            StatusText = "当前配置未通过校验，无法导出。";
            return;
        }

        var currentSettings = CaptureCurrentSettings();
        var existingFiles = BuildTargetFiles(currentSettings).Where(File.Exists).ToList();
        if (existingFiles.Count > 0)
        {
            var message = string.Join(Environment.NewLine, existingFiles.Select(path => $"- {path}"));
            var answer = MessageBox.Show(
                $"下列文件已存在，是否覆盖？{Environment.NewLine}{message}",
                "确认覆盖",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                StatusText = "已取消导出。";
                return;
            }
        }

        var result = await Service.ExportAsync(currentSettings, _sourceSnapshot);
        if (result.Success)
        {
            StatusText = result.Message;
            if (result.OutputPaths.Count > 0)
            {
                StatusText += $" 首个文件：{result.OutputPaths[0]}";
            }

            if (result.Warnings.Count > 0)
            {
                StatusText += $" 警告：{result.Warnings[0]}";
            }

            _isDirty = false;
            await SaveCurrentPresetAsync(persistToDisk: true);
            MessageBox.Show(result.Message, "面转 TXT");
            return;
        }

        StatusText = result.Message;
        MessageBox.Show(result.Message, "面转 TXT", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void BrowseDataset()
    {
        try
        {
            var dialog = new OpenItemDialog
            {
                Title = "选择面状要素类",
                Filter = ItemFilters.GeodatabaseItems_All,
                MultiSelect = false
            };

            if (dialog.ShowDialog() != true || dialog.Items.Count == 0)
            {
                return;
            }

            var selected = dialog.Items[0];
            BrowseDatasetPath = selected.Path ?? string.Empty;
            SelectedInputModeOption = InputModeOptions.First(option => option.Value == FaceToTxtInputMode.BrowseDataset);
            StatusText = $"已选择数据集：{selected.Name}";
            _ = RefreshSourceAsync();
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(FaceToTxtWorkbenchViewModel), "浏览面状要素类失败。", exception);
            MessageBox.Show($"浏览数据集失败：{exception.Message}", "面转 TXT", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BrowseOutputDirectory()
    {
        try
        {
            var dialog = new OpenItemDialog
            {
                Title = "选择输出文件夹",
                Filter = ItemFilters.Folders,
                MultiSelect = false,
                InitialLocation = string.IsNullOrWhiteSpace(OutputDirectory)
                    ? FaceToTxtSettingsStore.GetDefaultOutputDirectory()
                    : OutputDirectory,
                AlwaysUseInitialLocation = true
            };

            if (dialog.ShowDialog() != true || dialog.Items.Count == 0)
            {
                return;
            }

            var selected = dialog.Items[0];
            OutputDirectory = selected.Path ?? OutputDirectory;
            StatusText = $"输出路径已更新：{OutputDirectory}";
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(FaceToTxtWorkbenchViewModel), "浏览输出文件夹失败。", exception);
            MessageBox.Show($"浏览输出文件夹失败：{exception.Message}", "面转 TXT", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnSettingsChanged(bool refreshSource = false)
    {
        if (_isLoading || _isRefreshingSource)
        {
            return;
        }

        MarkDirty();
        if (refreshSource)
        {
            _ = RefreshSourceAsync();
            return;
        }

        RefreshPreviewAndValidation();
    }

    private void MarkDirty()
    {
        _isDirty = true;
        StatusText = $"配置已修改，尚未保存：{PresetName}";
        ExportCommand.RaiseCanExecuteChanged();
        SavePresetCommand.RaiseCanExecuteChanged();
    }

    private void ApplyPresetState(FaceToTxtPresetState preset)
    {
        _isLoading = true;
        try
        {
            _selectedPresetOption = new SelectionOption<string>(preset.Name, preset.Id);
            _presetName = preset.Name;

            var settings = preset.Settings.Clone();
            _selectedInputModeOption = InputModeOptions.FirstOrDefault(option => option.Value == settings.InputMode) ?? InputModeOptions[0];
            _selectedTxtModeOption = TxtModeOptions.FirstOrDefault(option => option.Value == settings.TxtMode) ?? TxtModeOptions[0];
            _selectedFileNameModeOption = FileNameModeOptions.FirstOrDefault(option => option.Value == settings.FileNameMode) ?? FileNameModeOptions[0];
            _selectedAreaUnitOption = AreaUnitOptions.FirstOrDefault(option => option.Value == settings.AreaUnit) ?? AreaUnitOptions[0];

            _browseDatasetPath = settings.BrowseDatasetPath;
            _outputDirectory = settings.OutputDirectory;
            _namingField = settings.NamingField;
            _parcelIdField = settings.ParcelIdField;
            _parcelNameField = settings.ParcelNameField;
            _parcelUseField = settings.ParcelUseField;
            _landCodeField = settings.LandCodeField;
            _areaField = settings.AreaField;
            _mapSheetField = settings.MapSheetField;
            _productionUnit = settings.ProductionUnit;
            _productionDateText = settings.ProductionDateText;
            _customDescriptionText = settings.CustomDescriptionText;
            _coordinatePrefix = string.IsNullOrWhiteSpace(settings.CoordinatePrefix) ? "J" : settings.CoordinatePrefix;
            _bandAdjustment = settings.BandAdjustment;
            _coordinateDecimals = settings.CoordinateDecimals;
            _areaDecimals = settings.AreaDecimals;
            _pointNumberDigits = settings.PointNumberDigits;
            _autoMapSheet = settings.AutoMapSheet;
            _useNewStandard = settings.UseNewStandard;
            _swapXY = settings.SwapXY;
            _includeClosingPoint = settings.IncludeClosingPoint;
            _repairGeometry = settings.RepairGeometry;
            _numberHoleRings = settings.NumberHoleRings;
            _reverseLength = settings.ReverseLength;
            _customDescriptionEnabled = settings.CustomDescriptionEnabled;
            _selectedPreset.Settings = settings;
            NotifyAllSettingsChanged();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private FaceToTxtExportSettings CaptureCurrentSettings()
    {
        return new FaceToTxtExportSettings
        {
            InputMode = SelectedInputMode,
            BrowseDatasetPath = BrowseDatasetPath,
            OutputDirectory = OutputDirectory,
            TxtMode = SelectedTxtMode,
            FileNameMode = SelectedFileNameMode,
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
            BandAdjustment = BandAdjustment,
            CoordinateDecimals = CoordinateDecimals,
            AreaDecimals = AreaDecimals,
            PointNumberDigits = PointNumberDigits,
            CoordinatePrefix = CoordinatePrefix,
            AreaUnit = SelectedAreaUnit,
            CoordinateSystemName = CoordinateSystemName,
            ProjectionTypeName = ProjectionTypeName,
            UnitName = UnitName
        };
    }

    private void RebuildPresetOptions(bool selectCurrent)
    {
        _presetOptions.Clear();
        foreach (var preset in _workspaceState.Presets.OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase))
        {
            _presetOptions.Add(new SelectionOption<string>(preset.Name, preset.Id));
        }

        NotifyPropertyChanged(nameof(PresetOptions));
        NotifyPropertyChanged(nameof(CanDeletePreset));
        DeletePresetCommand.RaiseCanExecuteChanged();
        NewPresetCommand.RaiseCanExecuteChanged();

        if (selectCurrent)
        {
            _selectedPresetOption = _presetOptions.FirstOrDefault(option => option.Value.Equals(_selectedPreset.Id, StringComparison.OrdinalIgnoreCase));
            NotifyPropertyChanged(nameof(SelectedPresetOption));
        }
    }

    private void NotifyAllSettingsChanged()
    {
        NotifyPropertyChanged(nameof(SelectedPresetOption));
        NotifyPropertyChanged(nameof(PresetName));
        NotifyPropertyChanged(nameof(SelectedInputModeOption));
        NotifyPropertyChanged(nameof(SelectedTxtModeOption));
        NotifyPropertyChanged(nameof(SelectedFileNameModeOption));
        NotifyPropertyChanged(nameof(SelectedAreaUnitOption));
        NotifyPropertyChanged(nameof(BrowseDatasetPath));
        NotifyPropertyChanged(nameof(OutputDirectory));
        NotifyPropertyChanged(nameof(NamingField));
        NotifyPropertyChanged(nameof(ParcelIdField));
        NotifyPropertyChanged(nameof(ParcelNameField));
        NotifyPropertyChanged(nameof(ParcelUseField));
        NotifyPropertyChanged(nameof(LandCodeField));
        NotifyPropertyChanged(nameof(AreaField));
        NotifyPropertyChanged(nameof(MapSheetField));
        NotifyPropertyChanged(nameof(ProductionUnit));
        NotifyPropertyChanged(nameof(ProductionDateText));
        NotifyPropertyChanged(nameof(CustomDescriptionText));
        NotifyPropertyChanged(nameof(CoordinatePrefix));
        NotifyPropertyChanged(nameof(BandAdjustment));
        NotifyPropertyChanged(nameof(CoordinateDecimals));
        NotifyPropertyChanged(nameof(AreaDecimals));
        NotifyPropertyChanged(nameof(PointNumberDigits));
        NotifyPropertyChanged(nameof(AutoMapSheet));
        NotifyPropertyChanged(nameof(UseNewStandard));
        NotifyPropertyChanged(nameof(SwapXY));
        NotifyPropertyChanged(nameof(IncludeClosingPoint));
        NotifyPropertyChanged(nameof(RepairGeometry));
        NotifyPropertyChanged(nameof(NumberHoleRings));
        NotifyPropertyChanged(nameof(ReverseLength));
        NotifyPropertyChanged(nameof(CustomDescriptionEnabled));
        NotifyPropertyChanged(nameof(CoordinateSystemName));
        NotifyPropertyChanged(nameof(ProjectionTypeName));
        NotifyPropertyChanged(nameof(UnitName));
        NotifyPropertyChanged(nameof(SourceSummaryText));
        NotifyPropertyChanged(nameof(SourceDetailText));
    }
    private void RefreshPreviewAndValidation()
    {
        if (_isLoading)
        {
            return;
        }

        _validationItems.Clear();
        var currentSettings = CaptureCurrentSettings();

        if (!_sourceSnapshot.HasItems)
        {
            _validationItems.Add(new FaceToTxtValidationItem(FaceToTxtValidationSeverity.Error, _sourceSnapshot.SummaryText));
        }
        else
        {
            _validationItems.Add(new FaceToTxtValidationItem(FaceToTxtValidationSeverity.Info, _sourceSnapshot.SummaryText));
            _validationItems.Add(new FaceToTxtValidationItem(FaceToTxtValidationSeverity.Info, _sourceSnapshot.DetailText));

            if (_sourceSnapshot.Items.Count > 1)
            {
                _validationItems.Add(new FaceToTxtValidationItem(FaceToTxtValidationSeverity.Warning, "当前选择集中包含多个图层，将按图层逐个生成 TXT。"));
            }
        }

        if (string.IsNullOrWhiteSpace(currentSettings.OutputDirectory))
        {
            _validationItems.Add(new FaceToTxtValidationItem(FaceToTxtValidationSeverity.Error, "请先选择输出路径。"));
        }

        foreach (var item in _sourceSnapshot.Items)
        {
            ValidateRequiredField(item, currentSettings.ParcelIdField, "地块编号");
            ValidateRequiredField(item, currentSettings.ParcelNameField, "地块名称");
            ValidateRequiredField(item, currentSettings.ParcelUseField, "地块用途");
            ValidateRequiredField(item, currentSettings.LandCodeField, "地类编码");

            if (string.IsNullOrWhiteSpace(currentSettings.MapSheetField) && !currentSettings.AutoMapSheet)
            {
                _validationItems.Add(new FaceToTxtValidationItem(
                    FaceToTxtValidationSeverity.Error,
                    $"图层 {item.DisplayName} 未映射图幅号字段，也未启用自动图幅号。"));
            }
        }

        if (currentSettings.BandAdjustment != 0)
        {
            _validationItems.Add(new FaceToTxtValidationItem(
                FaceToTxtValidationSeverity.Info,
                $"坐标加带数当前为 {currentSettings.BandAdjustment}，导出时会影响带号显示。"));
        }

        if (currentSettings.ReverseLength)
        {
            _validationItems.Add(new FaceToTxtValidationItem(
                FaceToTxtValidationSeverity.Info,
                "“反算图形边长”已启用，但当前版本仅保留该配置位，不会写入 TXT。"));
        }

        if (!currentSettings.UseNewStandard)
        {
            _validationItems.Add(new FaceToTxtValidationItem(
                FaceToTxtValidationSeverity.Warning,
                "当前版本固定按 2000 国家大地坐标系 3 度带导出，‘导出新国标’仅保留配置位。"));
        }

        PreviewText = BuildPreviewText(currentSettings);
        NotifyPropertyChanged(nameof(CanExport));
        ExportCommand.RaiseCanExecuteChanged();
        DeletePresetCommand.RaiseCanExecuteChanged();
    }

    private void ValidateRequiredField(FaceToTxtSourceItem item, string fieldName, string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            _validationItems.Add(new FaceToTxtValidationItem(
                FaceToTxtValidationSeverity.Error,
                $"图层 {item.DisplayName} 缺少必需字段：{friendlyName}。"));
            return;
        }

        if (!item.FieldOptions.Any(option => option.Value.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
        {
            _validationItems.Add(new FaceToTxtValidationItem(
                FaceToTxtValidationSeverity.Error,
                $"图层 {item.DisplayName} 中找不到字段“{fieldName}”（{friendlyName}）。"));
        }
    }

    private string BuildPreviewText(FaceToTxtExportSettings settings)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("【配置摘要】");
        builder.AppendLine($"预设：{PresetName}");
        builder.AppendLine($"输入模式：{GetLabel(settings.InputMode)}");
        builder.AppendLine($"TXT 模式：{GetLabel(settings.TxtMode)}");
        builder.AppendLine($"文件名：{GetLabel(settings.FileNameMode)}");
        builder.AppendLine($"输出路径：{settings.OutputDirectory}");
        builder.AppendLine($"来源摘要：{_sourceSnapshot.SummaryText}");
        builder.AppendLine($"坐标系：{CoordinateSystemName} / {ProjectionTypeName} / {UnitName}");
        builder.AppendLine($"字段映射：编号={DisplayField(settings.ParcelIdField)}，名称={DisplayField(settings.ParcelNameField)}，用途={DisplayField(settings.ParcelUseField)}，地类={DisplayField(settings.LandCodeField)}，面积={DisplayField(settings.AreaField)}，图幅号={DisplayField(settings.MapSheetField)}");
        builder.AppendLine($"坐标前缀：{settings.CoordinatePrefix}，编号位数：{settings.PointNumberDigits}，坐标精度：{settings.CoordinateDecimals}，面积精度：{settings.AreaDecimals}");
        builder.AppendLine($"自动图幅号：{YesNo(settings.AutoMapSheet)}，XY 互换：{YesNo(settings.SwapXY)}，界址点总数 +1：{YesNo(settings.IncludeClosingPoint)}");
        builder.AppendLine();
        builder.AppendLine("【TXT 结构预览】");
        if (settings.TxtMode == FaceToTxtTxtMode.StandardHeader)
        {
            builder.AppendLine("[属性描述]");
            builder.AppendLine("格式版本号=1.01");
            builder.AppendLine($"数据生产单位={settings.ProductionUnit}");
            builder.AppendLine($"数据生产日期={settings.ProductionDateText}");
            builder.AppendLine($"坐标系={settings.CoordinateSystemName}");
            builder.AppendLine("几度分带=3");
            builder.AppendLine($"投影类型={settings.ProjectionTypeName}");
            builder.AppendLine($"计量单位={settings.UnitName}");
            builder.AppendLine("带号=自动");
            builder.AppendLine("[地块坐标]");
        }
        else
        {
            builder.AppendLine("[地块坐标]");
        }

        builder.AppendLine("9,0.0180,2003-10,双桥乡地块,界 I-50-77-(22),公共基础设施,011,@");
        builder.AppendLine($"{settings.CoordinatePrefix}01,1,北坐标,东坐标");
        builder.AppendLine($"{settings.CoordinatePrefix}02,1,北坐标,东坐标");
        builder.AppendLine($"{settings.CoordinatePrefix}03,1,北坐标,东坐标");
        builder.AppendLine("...");

        if (settings.CustomDescriptionEnabled && !string.IsNullOrWhiteSpace(settings.CustomDescriptionText))
        {
            builder.AppendLine();
            builder.AppendLine("【自定义描述】");
            builder.AppendLine(settings.CustomDescriptionText);
        }

        return builder.ToString();
    }

    private void SyncFieldOptions(IReadOnlyList<SelectionOption<string>> fieldOptions)
    {
        _fieldOptions.Clear();
        foreach (var option in fieldOptions)
        {
            _fieldOptions.Add(option);
        }

        NotifyPropertyChanged(nameof(FieldOptions));
    }

    private void ApplyAutoMappings()
    {
        if (_fieldOptions.Count == 0)
        {
            return;
        }

        var changed = false;
        changed |= EnsureField(ref _parcelIdField, ["地块编号", "编号", "宗地编号", "DKBH"]);
        changed |= EnsureField(ref _parcelNameField, ["地块名称", "名称", "宗地名称", "DKMC"]);
        changed |= EnsureField(ref _parcelUseField, ["地块用途", "用途", "用地", "用途名称"]);
        changed |= EnsureField(ref _landCodeField, ["地类编码", "编码", "DLBM", "地类"]);
        changed |= EnsureField(ref _areaField, ["面积", "地块面积", "MJ"]);
        if (_autoMapSheet)
        {
            changed |= EnsureField(ref _mapSheetField, ["图幅号", "图幅", "TFH", "图号", "sheet"]);
        }

        changed |= EnsureField(ref _namingField, ["TXT命名字段", "名称", "编号", "地块名称", "地块编号"]);

        if (changed)
        {
            _selectedPreset.Settings.ParcelIdField = _parcelIdField;
            _selectedPreset.Settings.ParcelNameField = _parcelNameField;
            _selectedPreset.Settings.ParcelUseField = _parcelUseField;
            _selectedPreset.Settings.LandCodeField = _landCodeField;
            _selectedPreset.Settings.AreaField = _areaField;
            _selectedPreset.Settings.MapSheetField = _mapSheetField;
            _selectedPreset.Settings.NamingField = _namingField;
        }
    }

    private bool EnsureField(ref string fieldValue, IEnumerable<string> keywords)
    {
        var currentValue = fieldValue;
        if (!string.IsNullOrWhiteSpace(currentValue) &&
            _fieldOptions.Any(option => option.Value.Equals(currentValue, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        foreach (var keyword in keywords)
        {
            var matched = _fieldOptions.FirstOrDefault(option =>
                !string.IsNullOrWhiteSpace(option.Value) &&
                option.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
            {
                fieldValue = matched.Value;
                return true;
            }
        }

        fieldValue = string.Empty;
        return false;
    }

    private string BuildNewPresetName()
    {
        var baseName = "新配置";
        var index = 1;
        var candidate = baseName;

        while (_workspaceState.Presets.Any(preset => preset.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            index++;
            candidate = $"{baseName}{index}";
        }

        return candidate;
    }

    private List<string> BuildTargetFiles(FaceToTxtExportSettings settings)
    {
        if (_sourceSnapshot.Items.Count == 0)
        {
            return [];
        }

        var targetFiles = new List<string>();
        foreach (var item in _sourceSnapshot.Items)
        {
            var stem = SanitizeFileName(item.DisplayName);
            var namingFieldValue = string.Empty;
            if (!string.IsNullOrWhiteSpace(settings.NamingField) &&
                item.SampleTextValues.TryGetValue(settings.NamingField, out var sampleValue))
            {
                namingFieldValue = SanitizeFileName(sampleValue);
            }

            if (settings.FileNameMode == FaceToTxtFileNameMode.NamingField && !string.IsNullOrWhiteSpace(namingFieldValue))
            {
                stem = namingFieldValue;
            }
            else if (settings.FileNameMode == FaceToTxtFileNameMode.LayerNameAndNamingField && !string.IsNullOrWhiteSpace(namingFieldValue))
            {
                stem = string.Join("_", new[] { stem, namingFieldValue }.Where(value => !string.IsNullOrWhiteSpace(value)));
            }

            targetFiles.Add(Path.Combine(settings.OutputDirectory, $"{SanitizeFileName(stem)}.txt"));
        }

        return targetFiles;
    }

    private static string GetLabel(FaceToTxtInputMode value)
    {
        return value switch
        {
            FaceToTxtInputMode.CurrentSelection => "当前选择集",
            FaceToTxtInputMode.BrowseDataset => "浏览数据集",
            _ => value.ToString()
        };
    }

    private static string GetLabel(FaceToTxtTxtMode value)
    {
        return value switch
        {
            FaceToTxtTxtMode.StandardHeader => "标准头部",
            FaceToTxtTxtMode.DetailOnly => "仅明细",
            _ => value.ToString()
        };
    }

    private static string GetLabel(FaceToTxtFileNameMode value)
    {
        return value switch
        {
            FaceToTxtFileNameMode.LayerName => "图层名称",
            FaceToTxtFileNameMode.NamingField => "命名字段",
            FaceToTxtFileNameMode.LayerNameAndNamingField => "图层 + 命名字段",
            _ => value.ToString()
        };
    }

    private static string GetLabel(FaceToTxtAreaUnit value)
    {
        return value switch
        {
            FaceToTxtAreaUnit.Hectare => "公顷",
            FaceToTxtAreaUnit.SquareMeter => "平方米",
            _ => value.ToString()
        };
    }

    private static string DisplayField(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "自动/空" : value;
    }

    private static string YesNo(bool value)
    {
        return value ? "是" : "否";
    }

    private static string NormalizeText(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "txt-export";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "txt-export" : sanitized;
    }
}
