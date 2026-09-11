using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using GisProAddinWorkbench.AddIn.Core.Modules;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal sealed class ImageryWorkspaceViewModel : PropertyChangedBase
{
    private const string AllFilterKey = "all";

    private readonly ModuleContext _context;
    private readonly IModuleSettingsStore _settingsStore;
    private readonly ImageryLayerService _layerService = new();
    private readonly ImageryThumbnailService _thumbnailService = new();
    private readonly ICollectionView _galleryView;
    private readonly IReadOnlyList<SelectionOption<string>> _providerFilterOptions;
    private readonly IReadOnlyList<SelectionOption<string>> _contentTypeFilterOptions;
    private ImageryModuleSettings _settings = new();
    private ImageryPresetCardViewModel? _selectedPreset;
    private string _selectedProviderFilterKey = AllFilterKey;
    private string _selectedContentTypeFilterKey = AllFilterKey;
    private string _statusText = "地图服务图库已就绪，可通过顶部两个下拉筛选服务商和类型，点击卡片即可添加到当前地图。";
    private bool _isLoading;

    internal ImageryWorkspaceViewModel(ModuleContext context, IModuleSettingsStore settingsStore)
    {
        _context = context;
        _settingsStore = settingsStore;

        _providerFilterOptions = BuildFilterOptions(ImageryServiceCatalog.GetProviderFilterLabels());
        _contentTypeFilterOptions = BuildFilterOptions(ImageryServiceCatalog.GetContentTypeFilterLabels());

        Presets = new ObservableCollection<ImageryPresetCardViewModel>(
            ImageryServiceCatalog.GetPresets().Select(preset => new ImageryPresetCardViewModel(preset)));

        _galleryView = CollectionViewSource.GetDefaultView(Presets);
        _galleryView.Filter = FilterPreset;
        _galleryView.SortDescriptions.Add(new SortDescription(nameof(ImageryPresetCardViewModel.SortOrder), ListSortDirection.Ascending));
        _galleryView.SortDescriptions.Add(new SortDescription(nameof(ImageryPresetCardViewModel.TitleLine), ListSortDirection.Ascending));

        AddPresetCommand = new AsyncRelayCommand<ImageryPresetCardViewModel>(
            AddPresetAsync,
            preset => preset is not null && !_isLoading);

        ApplyFallbackVisualState();
        _ = InitializeAsync();
    }

    public ObservableCollection<ImageryPresetCardViewModel> Presets { get; }

    public IReadOnlyList<SelectionOption<string>> ProviderFilterOptions => _providerFilterOptions;

    public IReadOnlyList<SelectionOption<string>> ContentTypeFilterOptions => _contentTypeFilterOptions;

    public ICollectionView GalleryView => _galleryView;

    public ImageryPresetCardViewModel? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (!SetProperty(ref _selectedPreset, value, nameof(SelectedPreset)))
            {
                return;
            }

            foreach (var item in Presets)
            {
                item.IsSelected = ReferenceEquals(item, value);
            }

            NotifyPropertyChanged(nameof(SelectedPresetTitle));
            NotifyPropertyChanged(nameof(SelectedPresetSummary));

            if (_isLoading || value is null)
            {
                return;
            }

            StatusText = $"已选中：{value.TitleLine}";
            _context.PublishLog("信息", StatusText);
        }
    }

    public string SelectedPresetTitle => SelectedPreset?.TitleLine ?? "请选择一个服务卡片";

    public string SelectedPresetSummary => SelectedPreset?.Summary ?? "点击任意卡片即可添加对应服务到当前地图。";

    public string SelectedProviderFilterKey
    {
        get => _selectedProviderFilterKey;
        set
        {
            var normalized = NormalizeFilterKey(value, ProviderFilterOptions);
            if (!SetProperty(ref _selectedProviderFilterKey, normalized, nameof(SelectedProviderFilterKey)))
            {
                return;
            }

            OnFilterChanged();
        }
    }

    public string SelectedContentTypeFilterKey
    {
        get => _selectedContentTypeFilterKey;
        set
        {
            var normalized = NormalizeFilterKey(value, ContentTypeFilterOptions);
            if (!SetProperty(ref _selectedContentTypeFilterKey, normalized, nameof(SelectedContentTypeFilterKey)))
            {
                return;
            }

            OnFilterChanged();
        }
    }

    public string SelectedProviderFilterLabel => GetFilterLabel(SelectedProviderFilterKey, ProviderFilterOptions);

    public string SelectedContentTypeFilterLabel => GetFilterLabel(SelectedContentTypeFilterKey, ContentTypeFilterOptions);

    public string FilterSummaryText => $"服务商：{SelectedProviderFilterLabel} · 类型：{SelectedContentTypeFilterLabel}";

    public string VisiblePresetCountText => GetVisiblePresetCount() == 0
        ? "当前筛选无结果"
        : $"共 {GetVisiblePresetCount()} 个预设";

    public string CredentialSummaryText => ImageryServiceCatalog.BuildStatusSummary(_settings);

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value, nameof(StatusText));
    }

    public AsyncRelayCommand<ImageryPresetCardViewModel> AddPresetCommand { get; }

    private async Task InitializeAsync()
    {
        try
        {
            _isLoading = true;
            AddPresetCommand.RaiseCanExecuteChanged();

            _settings = NormalizeImagerySettings(await _settingsStore.LoadAsync(
                "imagery",
                () => new ImageryModuleSettings()));

            _settings.TianDiTuBaseKeys = NormalizeList(_settings.TianDiTuBaseKeys);
            _settings.TianDiTuSupplementalKeys = NormalizeList(_settings.TianDiTuSupplementalKeys);
            _settings.JilinApiBaseKeys = NormalizeList(_settings.JilinApiBaseKeys);
            _settings.JilinApiSupplementalKeys = NormalizeList(_settings.JilinApiSupplementalKeys);
            _settings.AddedPresetIds = NormalizeList(_settings.AddedPresetIds);

            SelectedProviderFilterKey = NormalizeFilterKey(_settings.LastProviderFilterKey, ProviderFilterOptions);
            SelectedContentTypeFilterKey = NormalizeFilterKey(_settings.LastContentTypeFilterKey, ContentTypeFilterOptions);

            SelectedPreset = Presets.FirstOrDefault(card =>
                                 card.Id.Equals(_settings.LastSelectedPresetId, StringComparison.OrdinalIgnoreCase) &&
                                 MatchesCurrentFilters(card))
                             ?? Presets.FirstOrDefault(MatchesCurrentFilters)
                             ?? Presets.FirstOrDefault();

            await SyncCardStatesFromCurrentMapAsync(persistSnapshot: true);
            _ = LoadThumbnailsAsync();

            StatusText = $"已加载：{FilterSummaryText}，{VisiblePresetCountText}";
            _context.PublishLog("信息", "在线瓦片服务图库已加载。");
        }
        catch (Exception exception)
        {
            StatusText = $"在线瓦片服务初始化失败：{exception.Message}";
            DiagnosticsLogger.Write(nameof(ImageryWorkspaceViewModel), "加载在线瓦片服务图库失败。", exception);
        }
        finally
        {
            _isLoading = false;
            AddPresetCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task LoadThumbnailsAsync()
    {
        try
        {
            var thumbnailTasks = Presets.Select(async card =>
            {
                card.Thumbnail = await _thumbnailService.GetThumbnailAsync(card.Preset, _settings);
            });

            await Task.WhenAll(thumbnailTasks);
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(ImageryWorkspaceViewModel), "加载影像缩略图失败。", exception);
        }
    }

    private async Task SyncCardStatesFromCurrentMapAsync(bool persistSnapshot)
    {
        var activeMap = MapView.Active?.Map;
        if (activeMap is null)
        {
            ApplySettingsToCards();
            return;
        }

        var presetLayers = Presets
            .Select(card => (Id: card.Id, LayerName: ImageryServiceCatalog.BuildLayerName(card.Preset)))
            .ToArray();

        HashSet<string> addedIds = [];

        try
        {
            await QueuedTask.Run(() =>
            {
                foreach (var preset in presetLayers)
                {
                    if (activeMap.FindLayers(preset.LayerName, true).Any())
                    {
                        addedIds.Add(preset.Id);
                    }
                }
            });
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(ImageryWorkspaceViewModel), "同步在线瓦片服务状态失败。", exception);
            ApplySettingsToCards();
            return;
        }

        foreach (var card in Presets)
        {
            card.HasCredential = _settings.HasCredentialFor(card.Preset.CredentialKind);
            card.IsAdded = addedIds.Contains(card.Id);
        }

        NotifyPropertyChanged(nameof(CredentialSummaryText));
        NotifyPropertyChanged(nameof(VisiblePresetCountText));

        if (persistSnapshot)
        {
            await PersistSettingsAsync();
        }
    }

    private async Task AddPresetAsync(ImageryPresetCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        SelectedPreset = card;

        try
        {
            if (card.Preset.ServiceKind == ImageryServiceKind.Wfs)
            {
                var credential = ImageryServiceCatalog.GetActiveCredential(_settings, card.Preset.CredentialKind) ?? string.Empty;

                if (card.Preset.CredentialKind != ImageryCredentialKind.None && string.IsNullOrWhiteSpace(credential))
                {
                    var missingCredentialMessage =
                        $"未配置 {ImageryServiceCatalog.GetCredentialLabel(card.Preset.CredentialKind)}，请先到设置里补充。";
                    StatusText = missingCredentialMessage;
                    _context.PublishNotification("在线瓦片服务", missingCredentialMessage);
                    _context.PublishLog("警告", missingCredentialMessage);
                    return;
                }

                var extentContext = await WfsExtentContext.CreateAsync();
                var initialOptions = WfsLayerOptions.FromSettings(_settings, extentContext);
                if (ImageryServiceCatalog.ShouldSwapWfsCoordinates(card.Preset))
                {
                    initialOptions.SwapXY = true;
                }

                if (ImageryServiceCatalog.ShouldSwapWfsFilterAxes(card.Preset))
                {
                    initialOptions.SwapXYFilter = true;
                }

                _context.PublishLog("信息", $"正在打开 WFS 参数窗口：{card.TitleLine}");
                DiagnosticsLogger.Write(nameof(ImageryWorkspaceViewModel), $"Open WFS dialog: {card.Id}");
                var selectedOptions = WfsLayerOptionsWindow.ShowDialog(card.Preset, initialOptions, extentContext);
                if (selectedOptions is null)
                {
                    StatusText = $"已取消添加：{card.TitleLine}";
                    _context.PublishLog("信息", StatusText);
                    return;
                }

                _context.PublishLog("信息", $"WFS 参数已确认：{card.TitleLine}");
                selectedOptions.ApplyTo(_settings);
                var wfsResult = await _layerService.AddWfsLayerAsync(card.Preset, selectedOptions, credential);
                StatusText = wfsResult.Message;

                if (wfsResult.IsSuccess)
                {
                    card.IsAdded = true;
                    if (_settings.AddedPresetIds.All(existing => !existing.Equals(card.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        _settings.AddedPresetIds.Add(card.Id);
                    }

                    _context.PublishLog("信息", wfsResult.Message);
                    _context.PublishNotification("在线瓦片服务", wfsResult.Message);
                }
                else if (wfsResult.Outcome is ImageryAddOutcome.AlreadyExists)
                {
                    card.IsAdded = true;
                    _context.PublishNotification("在线瓦片服务", wfsResult.Message);
                    _context.PublishLog("信息", wfsResult.Message);
                }
                else
                {
                    _context.PublishNotification("在线瓦片服务", wfsResult.Message);
                    _context.PublishLog("错误", wfsResult.Message);
                }

                await PersistSettingsAsync();
                return;
            }

            var result = await _layerService.AddPresetAsync(card.Preset, _settings);
            StatusText = result.Message;

            if (result.IsSuccess)
            {
                card.IsAdded = true;
                if (_settings.AddedPresetIds.All(existing => !existing.Equals(card.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    _settings.AddedPresetIds.Add(card.Id);
                }

                _context.PublishLog("信息", result.Message);
                _context.PublishNotification("在线瓦片服务", result.Message);
                await PersistSettingsAsync();
            }
            else if (result.Outcome is ImageryAddOutcome.AlreadyExists)
            {
                card.IsAdded = true;
                _context.PublishNotification("在线瓦片服务", result.Message);
                _context.PublishLog("信息", result.Message);
            }
            else if (result.Outcome is ImageryAddOutcome.MissingCredential or ImageryAddOutcome.NoActiveMap)
            {
                _context.PublishNotification("在线瓦片服务", result.Message);
                _context.PublishLog("警告", result.Message);
            }
            else
            {
                _context.PublishNotification("在线瓦片服务", result.Message);
                _context.PublishLog("错误", result.Message);
            }
        }
        catch (Exception exception)
        {
            StatusText = $"添加服务失败：{exception.Message}";
            DiagnosticsLogger.Write(nameof(ImageryWorkspaceViewModel), "添加在线瓦片服务失败。", exception);
            _context.PublishNotification("在线瓦片服务", StatusText);
        }
    }

    private void ApplySettingsToCards()
    {
        foreach (var card in Presets)
        {
            card.HasCredential = _settings.HasCredentialFor(card.Preset.CredentialKind);
            card.IsAdded = _settings.AddedPresetIds.Any(id => id.Equals(card.Id, StringComparison.OrdinalIgnoreCase));
        }

        NotifyPropertyChanged(nameof(CredentialSummaryText));
        NotifyPropertyChanged(nameof(VisiblePresetCountText));
    }

    private void ApplyFallbackVisualState()
    {
        foreach (var card in Presets)
        {
            card.HasCredential = false;
            card.IsAdded = false;
            card.IsSelected = false;
        }
    }

    private ImageryModuleSettings BuildSnapshotSettings(ImageryModuleSettings current)
    {
        var normalized = NormalizeImagerySettings(current);
        return new ImageryModuleSettings
        {
            TianDiTuBaseKeys = normalized.TianDiTuBaseKeys,
            TianDiTuSupplementalKeys = normalized.TianDiTuSupplementalKeys,
            JilinApiBaseKeys = normalized.JilinApiBaseKeys,
            JilinApiSupplementalKeys = normalized.JilinApiSupplementalKeys,
            LegacyTianDiTuKeys = null,
            LegacyJilinApiKeys = null,
            AddedPresetIds = Presets
                .Where(card => card.IsAdded)
                .Select(card => card.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            LastSelectedPresetId = SelectedPreset?.Id ?? current.LastSelectedPresetId,
            LastFilterKey = current.LastFilterKey,
            LastProviderFilterKey = SelectedProviderFilterKey,
            LastContentTypeFilterKey = SelectedContentTypeFilterKey,
            LastClassificationMode = current.LastClassificationMode,
            LastLocalPath = current.LastLocalPath,
            LastServiceUrl = current.LastServiceUrl,
            AutoRenameLayer = current.AutoRenameLayer,
            LastSelectedToolId = current.LastSelectedToolId,
            WfsConnectionMode = _settings.WfsConnectionMode,
            WfsMaxFeatures = _settings.WfsMaxFeatures,
            WfsPageSize = _settings.WfsPageSize,
            WfsUseSpatialExtent = _settings.WfsUseSpatialExtent,
            WfsExtentSource = _settings.WfsExtentSource,
            WfsExtentLayerName = _settings.WfsExtentLayerName,
            WfsCustomMinX = _settings.WfsCustomMinX,
            WfsCustomMinY = _settings.WfsCustomMinY,
            WfsCustomMaxX = _settings.WfsCustomMaxX,
            WfsCustomMaxY = _settings.WfsCustomMaxY,
            WfsSwapXY = _settings.WfsSwapXY,
            WfsSwapXYFilter = _settings.WfsSwapXYFilter
        };
    }

    private async Task PersistSettingsAsync()
    {
        try
        {
            var current = await _settingsStore.LoadAsync(
                "imagery",
                () => new ImageryModuleSettings());
            _settings = BuildSnapshotSettings(current);
            await _settingsStore.SaveAsync("imagery", _settings);
            NotifyPropertyChanged(nameof(CredentialSummaryText));
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(ImageryWorkspaceViewModel), "保存在线瓦片服务配置失败。", exception);
        }
    }

    private ImageryModuleSettings NormalizeImagerySettings(ImageryModuleSettings settings)
    {
        var legacySettings = ImagerySettingsMigration.LoadLegacySettings();

        var tiandituBaseKeys = NormalizeList(settings.TianDiTuBaseKeys);
        if (tiandituBaseKeys.Count == 0 && legacySettings is not null)
        {
            tiandituBaseKeys = NormalizeList(legacySettings.LegacyTianDiTuKeys);
        }
        if (tiandituBaseKeys.Count == 0)
        {
            tiandituBaseKeys = new List<string>(ImageryCredentialDefaults.TianDiTuKeys);
        }

        var tiandituSupplementalKeys = NormalizeList(settings.TianDiTuSupplementalKeys);
        if (tiandituSupplementalKeys.Count > 0)
        {
            tiandituSupplementalKeys = tiandituSupplementalKeys
                .Where(value => tiandituBaseKeys.All(existing => !existing.Equals(value, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var jilinBaseKeys = NormalizeList(settings.JilinApiBaseKeys);
        if (jilinBaseKeys.Count == 0 && legacySettings is not null)
        {
            jilinBaseKeys = NormalizeList(legacySettings.LegacyJilinApiKeys);
        }
        if (jilinBaseKeys.Count == 0)
        {
            jilinBaseKeys = new List<string>(ImageryCredentialDefaults.JilinApiKeys);
        }

        var jilinSupplementalKeys = NormalizeList(settings.JilinApiSupplementalKeys);
        if (jilinSupplementalKeys.Count > 0)
        {
            jilinSupplementalKeys = jilinSupplementalKeys
                .Where(value => jilinBaseKeys.All(existing => !existing.Equals(value, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return new ImageryModuleSettings
        {
            TianDiTuBaseKeys = tiandituBaseKeys,
            TianDiTuSupplementalKeys = tiandituSupplementalKeys,
            JilinApiBaseKeys = jilinBaseKeys,
            JilinApiSupplementalKeys = jilinSupplementalKeys,
            LegacyTianDiTuKeys = null,
            LegacyJilinApiKeys = null,
            AddedPresetIds = NormalizeList(settings.AddedPresetIds),
            LastSelectedPresetId = string.IsNullOrWhiteSpace(settings.LastSelectedPresetId)
                ? "tdt-vec"
                : settings.LastSelectedPresetId,
            LastFilterKey = string.IsNullOrWhiteSpace(settings.LastFilterKey)
                ? AllFilterKey
                : settings.LastFilterKey,
            LastProviderFilterKey = string.IsNullOrWhiteSpace(settings.LastProviderFilterKey)
                ? AllFilterKey
                : settings.LastProviderFilterKey,
            LastContentTypeFilterKey = string.IsNullOrWhiteSpace(settings.LastContentTypeFilterKey)
                ? AllFilterKey
                : settings.LastContentTypeFilterKey,
            LastClassificationMode = string.IsNullOrWhiteSpace(settings.LastClassificationMode)
                ? "provider"
                : settings.LastClassificationMode,
            LastLocalPath = settings.LastLocalPath ?? string.Empty,
            LastServiceUrl = settings.LastServiceUrl ?? string.Empty,
            AutoRenameLayer = settings.AutoRenameLayer,
            LastSelectedToolId = string.IsNullOrWhiteSpace(settings.LastSelectedToolId)
                ? "imagery-load"
                : settings.LastSelectedToolId,
            WfsConnectionMode = string.IsNullOrWhiteSpace(settings.WfsConnectionMode)
                ? nameof(WfsConnectionMode.Snapshot)
                : settings.WfsConnectionMode,
            WfsMaxFeatures = settings.WfsMaxFeatures > 0 ? settings.WfsMaxFeatures : 3000,
            WfsPageSize = settings.WfsPageSize,
            WfsUseSpatialExtent = settings.WfsUseSpatialExtent,
            WfsExtentSource = string.IsNullOrWhiteSpace(settings.WfsExtentSource)
                ? "CurrentView"
                : settings.WfsExtentSource,
            WfsExtentLayerName = settings.WfsExtentLayerName ?? string.Empty,
            WfsCustomMinX = settings.WfsCustomMinX,
            WfsCustomMinY = settings.WfsCustomMinY,
            WfsCustomMaxX = settings.WfsCustomMaxX,
            WfsCustomMaxY = settings.WfsCustomMaxY,
            WfsSwapXY = settings.WfsSwapXY,
            WfsSwapXYFilter = settings.WfsSwapXYFilter
        };
    }

    private void OnFilterChanged()
    {
        GalleryView.Refresh();
        NotifyPropertyChanged(nameof(VisiblePresetCountText));
        NotifyPropertyChanged(nameof(SelectedProviderFilterLabel));
        NotifyPropertyChanged(nameof(SelectedContentTypeFilterLabel));
        NotifyPropertyChanged(nameof(FilterSummaryText));

        if (_isLoading)
        {
            return;
        }

        StatusText = $"已筛选：{FilterSummaryText}，{VisiblePresetCountText}";
        _ = PersistSettingsAsync();
    }

    private bool FilterPreset(object item)
    {
        return item is ImageryPresetCardViewModel card && MatchesCurrentFilters(card);
    }

    private bool MatchesCurrentFilters(ImageryPresetCardViewModel card)
    {
        return MatchesProviderFilter(card) && MatchesContentTypeFilter(card);
    }

    private bool MatchesProviderFilter(ImageryPresetCardViewModel card)
    {
        return string.Equals(SelectedProviderFilterKey, AllFilterKey, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ImageryServiceCatalog.GetProviderLabel(card.Preset), SelectedProviderFilterKey, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesContentTypeFilter(ImageryPresetCardViewModel card)
    {
        return string.Equals(SelectedContentTypeFilterKey, AllFilterKey, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ImageryServiceCatalog.GetContentTypeLabel(card.Preset), SelectedContentTypeFilterKey, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<SelectionOption<string>> BuildFilterOptions(IEnumerable<string> labels)
    {
        return
        [
            new SelectionOption<string>("全部", AllFilterKey),
            .. labels.Select(label => new SelectionOption<string>(label, label))
        ];
    }

    private static string NormalizeFilterKey(string? value, IReadOnlyList<SelectionOption<string>> options)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AllFilterKey;
        }

        var candidate = value.Trim();
        return options.Any(option => option.Value.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            ? candidate
            : AllFilterKey;
    }

    private static string GetFilterLabel(string key, IReadOnlyList<SelectionOption<string>> options)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Equals(AllFilterKey, StringComparison.OrdinalIgnoreCase))
        {
            return "全部";
        }

        return options.FirstOrDefault(option => option.Value.Equals(key, StringComparison.OrdinalIgnoreCase))?.Label ?? key;
    }

    private static List<string> NormalizeList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    private int GetVisiblePresetCount()
    {
        return Presets.Count(MatchesCurrentFilters);
    }
}

