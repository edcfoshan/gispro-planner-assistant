using System.Collections.ObjectModel;
using System.Diagnostics;
using ArcGIS.Desktop.Framework.Contracts;
using GisProAddinWorkbench.AddIn.Core.Modules;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Logs;
using GisProAddinWorkbench.AddIn.Models;
using GisProAddinWorkbench.AddIn.Modules.Imagery;
using GisProAddinWorkbench.AddIn.Services;

namespace GisProAddinWorkbench.AddIn.Settings;

internal sealed class GlobalSettingsWindowViewModel : PropertyChangedBase
{
    private readonly JsonModuleSettingsStore _moduleSettingsStore = new();
    private string _settingsFilePath = string.Empty;
    private string _imagerySettingsFilePath = string.Empty;
    private string _defaultOutputGdb = string.Empty;
    private string _adminAreaToolPath = string.Empty;
    private string _amapPoiToolPath = string.Empty;
    private string _baiduPoiToolPath = string.Empty;
    private string _addressGeocodeToolPath = SurveyToolCatalog.BuildToolPath("GeocodeToWgs84Points");
    private string _reverseGeocodeToolPath = string.Empty;
    private string _coordTransformToolPath = string.Empty;
    private string _baiduAkBaseText = string.Empty;
    private string _baiduAkSupplementalText = string.Empty;
    private string _amapKeyBaseText = string.Empty;
    private string _amapKeySupplementalText = string.Empty;
    private string _tianDiTuBaseKeysText = string.Empty;
    private string _tianDiTuSupplementalKeysText = string.Empty;
    private string _jilinApiBaseKeysText = string.Empty;
    private string _jilinApiSupplementalKeysText = string.Empty;
    private int _uiDensityPercent = 100;
    private int _uiLeftPaneWidthPercent = 100;
    private int _uiRightPaneMinWidthPercent = 100;
    private int _savedUiDensityPercent = 100;
    private int _savedUiLeftPaneWidthPercent = 100;
    private int _savedUiRightPaneMinWidthPercent = 100;
    private bool _isLoadingAppearanceSnapshot;
    private bool _hasUnsavedAppearancePreview;
    private bool _isAdminUnlocked;
    private string _statusMessage = "请先加载设置。";
    private SelectionOption<string>? _selectedCategoryOption;
    private ImageryModuleSettings _loadedImagerySettings = new();

    internal GlobalSettingsWindowViewModel(Action closeAction)
    {
        CategoryOptions =
        [
            new SelectionOption<string>("外观设置", "appearance"),
            new SelectionOption<string>("配置设置", "config"),
            new SelectionOption<string>("系统日志", "systemlog")
        ];

        _selectedCategoryOption = CategoryOptions[0];

        ReloadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        OpenConfigFileCommand = new RelayCommand(OpenConfigFile);
        OpenImageryConfigFileCommand = new AsyncRelayCommand(OpenImageryConfigFileAsync);
        OpenSystemLogWindowCommand = new RelayCommand(OpenSystemLogWindow);
        CloseCommand = new RelayCommand(closeAction);
    }

    public ObservableCollection<SelectionOption<string>> CategoryOptions { get; }

    public string SettingsFilePath
    {
        get => _settingsFilePath;
        private set => SetProperty(ref _settingsFilePath, value, nameof(SettingsFilePath));
    }

    public string ImagerySettingsFilePath
    {
        get => _imagerySettingsFilePath;
        private set => SetProperty(ref _imagerySettingsFilePath, value, nameof(ImagerySettingsFilePath));
    }

    public string DefaultOutputGdb
    {
        get => _defaultOutputGdb;
        set => SetProperty(ref _defaultOutputGdb, value, nameof(DefaultOutputGdb));
    }

    public string AdminAreaToolPath
    {
        get => _adminAreaToolPath;
        set => SetProperty(ref _adminAreaToolPath, value, nameof(AdminAreaToolPath));
    }

    public string AmapPoiToolPath
    {
        get => _amapPoiToolPath;
        set => SetProperty(ref _amapPoiToolPath, value, nameof(AmapPoiToolPath));
    }

    public string BaiduPoiToolPath
    {
        get => _baiduPoiToolPath;
        set => SetProperty(ref _baiduPoiToolPath, value, nameof(BaiduPoiToolPath));
    }

    public string AddressGeocodeToolPath
    {
        get => _addressGeocodeToolPath;
        set => SetProperty(ref _addressGeocodeToolPath, value, nameof(AddressGeocodeToolPath));
    }

    public string ReverseGeocodeToolPath
    {
        get => _reverseGeocodeToolPath;
        set => SetProperty(ref _reverseGeocodeToolPath, value, nameof(ReverseGeocodeToolPath));
    }

    public string CoordTransformToolPath
    {
        get => _coordTransformToolPath;
        set => SetProperty(ref _coordTransformToolPath, value, nameof(CoordTransformToolPath));
    }

    public string BaiduAkBaseText
    {
        get => GetBaseKeyDisplayText(_baiduAkBaseText);
        set => SetProperty(ref _baiduAkBaseText, value, nameof(BaiduAkBaseText));
    }

    public string BaiduAkSupplementalText
    {
        get => _baiduAkSupplementalText;
        set => SetProperty(ref _baiduAkSupplementalText, value, nameof(BaiduAkSupplementalText));
    }

    public string AmapKeyBaseText
    {
        get => GetBaseKeyDisplayText(_amapKeyBaseText);
        set => SetProperty(ref _amapKeyBaseText, value, nameof(AmapKeyBaseText));
    }

    public string AmapKeySupplementalText
    {
        get => _amapKeySupplementalText;
        set => SetProperty(ref _amapKeySupplementalText, value, nameof(AmapKeySupplementalText));
    }

    public string TianDiTuBaseKeysText
    {
        get => GetBaseKeyDisplayText(_tianDiTuBaseKeysText);
        set => SetProperty(ref _tianDiTuBaseKeysText, value, nameof(TianDiTuBaseKeysText));
    }

    public string TianDiTuSupplementalKeysText
    {
        get => _tianDiTuSupplementalKeysText;
        set => SetProperty(ref _tianDiTuSupplementalKeysText, value, nameof(TianDiTuSupplementalKeysText));
    }

    public string JilinApiBaseKeysText
    {
        get => GetBaseKeyDisplayText(_jilinApiBaseKeysText);
        set => SetProperty(ref _jilinApiBaseKeysText, value, nameof(JilinApiBaseKeysText));
    }

    public string JilinApiSupplementalKeysText
    {
        get => _jilinApiSupplementalKeysText;
        set => SetProperty(ref _jilinApiSupplementalKeysText, value, nameof(JilinApiSupplementalKeysText));
    }

    public bool IsAdminUnlocked
    {
        get => _isAdminUnlocked;
        private set
        {
            if (!SetProperty(ref _isAdminUnlocked, value, nameof(IsAdminUnlocked)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(CanEditBaseKeys));
            NotifyPropertyChanged(nameof(AdminUnlockButtonText));
            NotifyPropertyChanged(nameof(BaseKeysStatusText));
            NotifyPropertyChanged(nameof(BaiduAkBaseText));
            NotifyPropertyChanged(nameof(AmapKeyBaseText));
            NotifyPropertyChanged(nameof(TianDiTuBaseKeysText));
            NotifyPropertyChanged(nameof(JilinApiBaseKeysText));
        }
    }

    public string BaseKeysStatusText => IsAdminUnlocked
        ? "管理员基础密钥已解锁，可编辑基础区。"
        : "管理员基础密钥已锁定，普通用户只能编辑补充区。";

    public string AdminUnlockButtonText => IsAdminUnlocked ? "锁定基础密钥" : "解锁基础密钥";

    public bool CanEditBaseKeys => IsAdminUnlocked;

    public string ConfigIntroText => "基础密钥需要管理员密码解锁后才能查看完整内容并修改；补充密钥可直接添加，运行时补充优先、基础兜底。";

    public string ImageryIntroText => "天地图与吉林一号的基础密钥同样受管理员解锁控制，补充密钥对普通用户开放，运行时补充优先、基础兜底。";

    public string ResearchKeySummaryText => "前期调研 Key 已分成基础密钥与补充密钥两层。";

    public string ImageryKeySummaryText => "在线瓦片服务密钥已分成基础密钥与补充密钥两层。";

    public string AdminPasswordPlaceholder => "输入固定管理员密码";

    public string AdminLockHintText => "解锁后可编辑基础区；关闭窗口会自动回锁。";

    public string BaseKeyHintText => "基础区为管理员管理密钥，锁定时仅显示前四位和后四位，普通用户只读。";

    public string SupplementalKeyHintText => "补充区可直接编辑，每行一个密钥，运行时会优先于基础区。";

    public int UiDensityPercent
    {
        get => _uiDensityPercent;
        set
        {
            var normalized = UiDensityService.NormalizePercent(value);
            if (!SetProperty(ref _uiDensityPercent, normalized, nameof(UiDensityPercent)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(UiDensityPercentText));
            if (_isLoadingAppearanceSnapshot)
            {
                return;
            }

            ApplyRuntimePreview();
            UpdateAppearanceDirtyState();
        }
    }

    public int UiLeftPaneWidthPercent
    {
        get => _uiLeftPaneWidthPercent;
        set
        {
            var normalized = UiDensityService.NormalizePanePercent(value);
            if (!SetProperty(ref _uiLeftPaneWidthPercent, normalized, nameof(UiLeftPaneWidthPercent)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(UiLeftPaneWidthPercentText));
            if (_isLoadingAppearanceSnapshot)
            {
                return;
            }

            ApplyRuntimePreview();
            UpdateAppearanceDirtyState();
        }
    }

    public int UiRightPaneMinWidthPercent
    {
        get => _uiRightPaneMinWidthPercent;
        set
        {
            var normalized = UiDensityService.NormalizePanePercent(value);
            if (!SetProperty(ref _uiRightPaneMinWidthPercent, normalized, nameof(UiRightPaneMinWidthPercent)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(UiRightPaneMinWidthPercentText));
            if (_isLoadingAppearanceSnapshot)
            {
                return;
            }

            ApplyRuntimePreview();
            UpdateAppearanceDirtyState();
        }
    }

    public string UiDensityPercentText => $"{UiDensityPercent}%";

    public string UiLeftPaneWidthPercentText => $"{UiLeftPaneWidthPercent}%";

    public string UiRightPaneMinWidthPercentText => $"{UiRightPaneMinWidthPercent}%";

    public SelectionOption<string>? SelectedCategoryOption
    {
        get => _selectedCategoryOption;
        set
        {
            if (!SetProperty(ref _selectedCategoryOption, value, nameof(SelectedCategoryOption)))
            {
                return;
            }

            NotifyPropertyChanged(nameof(IsConfigCategorySelected));
            NotifyPropertyChanged(nameof(IsAppearanceCategorySelected));
            NotifyPropertyChanged(nameof(IsSystemLogCategorySelected));
        }
    }

    public bool IsConfigCategorySelected => SelectedCategoryOption?.Value == "config";

    public bool IsAppearanceCategorySelected => SelectedCategoryOption?.Value == "appearance";

    public bool IsSystemLogCategorySelected => SelectedCategoryOption?.Value == "systemlog";

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value, nameof(StatusMessage));
    }

    public AsyncRelayCommand ReloadCommand { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand OpenConfigFileCommand { get; }

    public AsyncRelayCommand OpenImageryConfigFileCommand { get; }

    public RelayCommand OpenSystemLogWindowCommand { get; }

    public RelayCommand CloseCommand { get; }

    internal async Task InitializeAsync()
    {
        await LoadAsync();
    }

    internal bool TryUnlockAdmin(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            StatusMessage = "请输入管理员密码后再解锁基础密钥。";
            return false;
        }

        if (!ImageryCredentialVault.TryUnlock(password))
        {
            StatusMessage = "管理员密码错误，基础密钥仍处于锁定状态。";
            return false;
        }

        IsAdminUnlocked = true;
        StatusMessage = "管理员基础密钥已解锁，可编辑基础区。";

        // 解锁后内置密钥才可用，重载基础密钥区让解密结果立即显示。
        _loadedImagerySettings = NormalizeImagerySettings(_loadedImagerySettings);
        TianDiTuBaseKeysText = JoinLines(_loadedImagerySettings.TianDiTuBaseKeys);
        JilinApiBaseKeysText = JoinLines(_loadedImagerySettings.JilinApiBaseKeys);
        return true;
    }

    internal void LockAdmin()
    {
        IsAdminUnlocked = false;
        ImageryCredentialVault.Lock();
        StatusMessage = "管理员基础密钥已锁定。";
    }

    private async Task LoadAsync()
    {
        try
        {
            await ResearchSettingsStore.EnsureTemplateExistsAsync();
            var settings = await ResearchSettingsStore.LoadAsync();
            var imagerySettings = await _moduleSettingsStore.LoadAsync(
                "imagery",
                () => new ImageryModuleSettings());

            SettingsFilePath = ResearchSettingsStore.GetSettingsPath();
            ImagerySettingsFilePath = _moduleSettingsStore.GetSettingsPath("imagery");
            DefaultOutputGdb = string.IsNullOrWhiteSpace(settings.DefaultOutputGdb)
                ? ResearchSettingsStore.GetDefaultOutputGdb()
                : settings.DefaultOutputGdb;

            var defaultMappings = SurveyToolCatalog.BuildDefaultMappings();
            AdminAreaToolPath = GetMapping(settings, defaultMappings, SurveyToolCatalog.AdminAreaBoundaryToolId);
            AmapPoiToolPath = GetMapping(settings, defaultMappings, SurveyToolCatalog.AmapPoiToolId);
            BaiduPoiToolPath = GetMapping(settings, defaultMappings, SurveyToolCatalog.BaiduPoiToolId);
            AddressGeocodeToolPath = GetMapping(settings, defaultMappings, SurveyToolCatalog.AddressGeocodeToolId);
            ReverseGeocodeToolPath = GetMapping(settings, defaultMappings, SurveyToolCatalog.ReverseGeocodeToolId);
            CoordTransformToolPath = GetMapping(settings, defaultMappings, SurveyToolCatalog.CoordTransformToolId);
            BaiduAkBaseText = JoinLines(settings.BaiduAkBase);
            BaiduAkSupplementalText = JoinLines(settings.BaiduAkSupplemental);
            AmapKeyBaseText = JoinLines(settings.AmapKeyBase);
            AmapKeySupplementalText = JoinLines(settings.AmapKeySupplemental);

            _loadedImagerySettings = NormalizeImagerySettings(imagerySettings);
            TianDiTuBaseKeysText = JoinLines(_loadedImagerySettings.TianDiTuBaseKeys);
            TianDiTuSupplementalKeysText = JoinLines(_loadedImagerySettings.TianDiTuSupplementalKeys);
            JilinApiBaseKeysText = JoinLines(_loadedImagerySettings.JilinApiBaseKeys);
            JilinApiSupplementalKeysText = JoinLines(_loadedImagerySettings.JilinApiSupplementalKeys);

            var loadedDensity = UiDensityService.NormalizePercent(settings.UiDensityPercent);
            var loadedLeftPane = UiDensityService.NormalizePanePercent(settings.UiLeftPaneWidthPercent);
            var loadedRightPane = UiDensityService.NormalizePanePercent(settings.UiRightPaneMinWidthPercent);

            _isLoadingAppearanceSnapshot = true;
            UiDensityPercent = loadedDensity;
            UiLeftPaneWidthPercent = loadedLeftPane;
            UiRightPaneMinWidthPercent = loadedRightPane;
            _isLoadingAppearanceSnapshot = false;

            _savedUiDensityPercent = loadedDensity;
            _savedUiLeftPaneWidthPercent = loadedLeftPane;
            _savedUiRightPaneMinWidthPercent = loadedRightPane;
            _hasUnsavedAppearancePreview = false;

            UiDensityService.ApplyRuntimeSettings(loadedDensity, loadedLeftPane, loadedRightPane);

            StatusMessage = "设置已加载，基础密钥默认锁定，普通用户可编辑补充区。";
        }
        catch (Exception exception)
        {
            SettingsFilePath = ResearchSettingsStore.GetSettingsPath();
            ImagerySettingsFilePath = _moduleSettingsStore.GetSettingsPath("imagery");
            StatusMessage = $"加载设置失败：{exception.Message}";
            DiagnosticsLogger.Write(nameof(GlobalSettingsWindowViewModel), "加载设置失败。", exception);
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            await ResearchSettingsStore.EnsureTemplateExistsAsync();

            ResearchSettings existing;
            try
            {
                existing = await ResearchSettingsStore.LoadAsync();
            }
            catch
            {
                existing = new ResearchSettings();
            }

            var timeoutSeconds = existing.RequestTimeoutSeconds > 0
                ? existing.RequestTimeoutSeconds
                : 30;
            var defaultPageSize = existing.DefaultPageSize > 0
                ? existing.DefaultPageSize
                : 20;

            var settings = new ResearchSettings
            {
                BaiduAkBase = ParseLines(_baiduAkBaseText).ToArray(),
                BaiduAkSupplemental = ParseLines(BaiduAkSupplementalText).ToArray(),
                AmapKeyBase = ParseLines(_amapKeyBaseText).ToArray(),
                AmapKeySupplemental = ParseLines(AmapKeySupplementalText).ToArray(),
                DefaultOutputGdb = DefaultOutputGdb.Trim(),
                RequestTimeoutSeconds = timeoutSeconds,
                DefaultPageSize = defaultPageSize,
                UiDensityPercent = UiDensityService.NormalizePercent(UiDensityPercent),
                UiLeftPaneWidthPercent = UiDensityService.NormalizePanePercent(UiLeftPaneWidthPercent),
                UiRightPaneMinWidthPercent = UiDensityService.NormalizePanePercent(UiRightPaneMinWidthPercent),
                PytToolMappings = MergeMappings(existing.PytToolMappings)
            };

            await ResearchSettingsStore.SaveAsync(settings);

            var imageryCurrent = await _moduleSettingsStore.LoadAsync(
                "imagery",
                () => new ImageryModuleSettings());
            var imagerySettings = NormalizeImagerySettings(imageryCurrent);
            imagerySettings.TianDiTuBaseKeys = ParseLines(_tianDiTuBaseKeysText);
            imagerySettings.TianDiTuSupplementalKeys = ParseLines(TianDiTuSupplementalKeysText);
            imagerySettings.JilinApiBaseKeys = ParseLines(_jilinApiBaseKeysText);
            imagerySettings.JilinApiSupplementalKeys = ParseLines(JilinApiSupplementalKeysText);
            _loadedImagerySettings = imagerySettings;
            await _moduleSettingsStore.SaveAsync("imagery", imagerySettings);

            _savedUiDensityPercent = UiDensityPercent;
            _savedUiLeftPaneWidthPercent = UiLeftPaneWidthPercent;
            _savedUiRightPaneMinWidthPercent = UiRightPaneMinWidthPercent;
            _hasUnsavedAppearancePreview = false;
            StatusMessage = "设置保存成功。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"保存设置失败：{exception.Message}";
            DiagnosticsLogger.Write(nameof(GlobalSettingsWindowViewModel), "保存设置失败。", exception);
        }
    }

    private async Task OpenImageryConfigFileAsync()
    {
        try
        {
            var imagerySettings = await _moduleSettingsStore.LoadAsync(
                "imagery",
                () => new ImageryModuleSettings());
            _loadedImagerySettings = NormalizeImagerySettings(imagerySettings);
            ImagerySettingsFilePath = _moduleSettingsStore.GetSettingsPath("imagery");

            var processInfo = new ProcessStartInfo
            {
                FileName = ImagerySettingsFilePath,
                UseShellExecute = true
            };

            Process.Start(processInfo);
            StatusMessage = "已打开影像配置文件。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"打开影像配置文件失败：{exception.Message}";
            DiagnosticsLogger.Write(nameof(GlobalSettingsWindowViewModel), "打开影像配置文件失败。", exception);
        }
    }

    private void OpenConfigFile()
    {
        try
        {
            ResearchSettingsStore.EnsureTemplateExists();
            SettingsFilePath = ResearchSettingsStore.GetSettingsPath();

            var startInfo = new ProcessStartInfo
            {
                FileName = SettingsFilePath,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            StatusMessage = "已打开配置文件。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"打开配置文件失败：{exception.Message}";
            DiagnosticsLogger.Write(nameof(GlobalSettingsWindowViewModel), "打开配置文件失败。", exception);
        }
    }

    private void OpenSystemLogWindow()
    {
        try
        {
            SystemLogWindowHost.ShowWindow();
            StatusMessage = "已打开系统日志窗口。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"打开系统日志窗口失败：{exception.Message}";
            DiagnosticsLogger.Write(nameof(GlobalSettingsWindowViewModel), "打开系统日志窗口失败。", exception);
        }
    }

    internal void RestoreSavedAppearanceIfNeeded()
    {
        if (!_hasUnsavedAppearancePreview)
        {
            return;
        }

        try
        {
            _isLoadingAppearanceSnapshot = true;
            UiDensityPercent = _savedUiDensityPercent;
            UiLeftPaneWidthPercent = _savedUiLeftPaneWidthPercent;
            UiRightPaneMinWidthPercent = _savedUiRightPaneMinWidthPercent;
            _isLoadingAppearanceSnapshot = false;

            UiDensityService.ApplyRuntimeSettings(
                _savedUiDensityPercent,
                _savedUiLeftPaneWidthPercent,
                _savedUiRightPaneMinWidthPercent);
            _hasUnsavedAppearancePreview = false;
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(GlobalSettingsWindowViewModel), "恢复未保存外观预览失败。", exception);
        }
    }

    private void ApplyRuntimePreview()
    {
        try
        {
            UiDensityService.ApplyRuntimeSettings(
                UiDensityService.NormalizePercent(UiDensityPercent),
                UiDensityService.NormalizePanePercent(UiLeftPaneWidthPercent),
                UiDensityService.NormalizePanePercent(UiRightPaneMinWidthPercent));
        }
        catch (Exception exception)
        {
            StatusMessage = $"实时应用外观失败：{exception.Message}";
            DiagnosticsLogger.Write(nameof(GlobalSettingsWindowViewModel), "实时应用外观失败。", exception);
        }
    }

    private void UpdateAppearanceDirtyState()
    {
        _hasUnsavedAppearancePreview =
            UiDensityPercent != _savedUiDensityPercent ||
            UiLeftPaneWidthPercent != _savedUiLeftPaneWidthPercent ||
            UiRightPaneMinWidthPercent != _savedUiRightPaneMinWidthPercent;
    }

    private Dictionary<string, string> MergeMappings(IReadOnlyDictionary<string, string> existing)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in existing)
        {
            mappings[pair.Key] = pair.Value ?? string.Empty;
        }

        mappings[SurveyToolCatalog.AdminAreaBoundaryToolId] = AdminAreaToolPath.Trim();
        mappings[SurveyToolCatalog.AmapPoiToolId] = AmapPoiToolPath.Trim();
        mappings[SurveyToolCatalog.BaiduPoiToolId] = BaiduPoiToolPath.Trim();
        mappings[SurveyToolCatalog.AddressGeocodeToolId] = AddressGeocodeToolPath.Trim();
        mappings[SurveyToolCatalog.ReverseGeocodeToolId] = ReverseGeocodeToolPath.Trim();
        mappings[SurveyToolCatalog.CoordTransformToolId] = CoordTransformToolPath.Trim();
        foreach (var key in mappings.Keys.ToList())
        {
            mappings[key] = SurveyToolRuntime.NormalizeConfiguredToolPath(mappings[key]);
        }

        return mappings;
    }

    private static string GetMapping(
        ResearchSettings settings,
        IReadOnlyDictionary<string, string> defaults,
        string key)
    {
        if (settings.PytToolMappings.TryGetValue(key, out var configured) &&
            !string.IsNullOrWhiteSpace(configured))
        {
            return SurveyToolRuntime.NormalizeConfiguredToolPath(configured);
        }

        return defaults.TryGetValue(key, out var fallback)
            ? SurveyToolRuntime.NormalizeConfiguredToolPath(fallback)
            : string.Empty;
    }

    private static ImageryModuleSettings NormalizeImagerySettings(ImageryModuleSettings settings)
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
                ? "all"
                : settings.LastFilterKey,
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
            WfsPageSize = Math.Max(0, settings.WfsPageSize),
            WfsUseSpatialExtent = settings.WfsUseSpatialExtent,
            WfsExtentSource = string.IsNullOrWhiteSpace(settings.WfsExtentSource)
                ? nameof(WfsExtentSource.CurrentView)
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

    private static List<string> ParseLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(['\r', '\n', ',', '，', ';', '；', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string JoinLines(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return string.Empty;
        }

        var items = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();

        return string.Join(Environment.NewLine, items);
    }

    private string GetBaseKeyDisplayText(string value)
    {
        return IsAdminUnlocked ? value : MaskKeyLines(value);
    }

    private static string MaskKeyLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var values = ParseLines(text);
        if (values.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            values.Select(MaskKeyValue));
    }

    private static string MaskKeyValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length <= 8)
        {
            return new string('*', Math.Max(trimmed.Length, 4));
        }

        return $"{trimmed[..4]}{new string('*', trimmed.Length - 8)}{trimmed[^4..]}";
    }
}

