using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ArcGIS.Desktop.Framework.Contracts;
using GisProAddinWorkbench.AddIn.Guides;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Models;
using GisProAddinWorkbench.AddIn.Services;
using GisProAddinWorkbench.AddIn.Settings;

namespace GisProAddinWorkbench.AddIn.DockPanes;

internal sealed class SurveyWorkbenchViewModel : PropertyChangedBase
{
    private static readonly Brush DefaultAccentBrush = CreateBrush("#2563EB");
    private const string EmptyStreetViewStatus = "使用“百度街景跳转”后，在地图上点选一个位置，这里会显示街景。";
    private const string EmptyStreetViewCoordinate = "等待地图点选。";

    internal static SurveyWorkbenchViewModel Shared { get; } = new();

    private readonly PytToolLauncher _pytToolLauncher = new();
    private readonly Dictionary<string, SurveyToolItem> _toolLookup = new(StringComparer.OrdinalIgnoreCase);
    private ResearchSettings _settings = new();
    private SurveyToolItem? _selectedTool;
    private string _useToolStatusText = "请选择一个前期调研工具，然后查看说明或直接打开工具。";
    private string _baiduStreetViewHtmlContent = string.Empty;
    private string _baiduStreetViewStatusText = EmptyStreetViewStatus;
    private string _baiduStreetViewCoordinateText = EmptyStreetViewCoordinate;
    private bool _isOpeningTool;
    private bool _isInitialized;

    internal SurveyWorkbenchViewModel()
    {
        Tools = [];
        ToolCollectionView = CollectionViewSource.GetDefaultView(Tools);
        ToolCollectionView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SurveyToolItem.GroupLabel)));

        OpenToolCommand = new AsyncRelayCommand(OpenCurrentPytToolAsync, CanOpenTool);
        ShowToolGuideCommand = new RelayCommand(ShowSelectedToolGuide, () => SelectedTool is not null);
        ShowOverviewGuideCommand = new RelayCommand(() => GuideWindowHost.Show(GuideCatalog.SurveyGuide()));
        ShowQuickReferenceGuideCommand = new RelayCommand(() => GuideWindowHost.Show(GuideCatalog.SurveyQuickReferenceGuide()));
        OpenSettingsCommand = new RelayCommand(GlobalSettingsWindowHost.ShowWindow);
        ClearBaiduStreetViewCommand = new RelayCommand(ClearBaiduStreetView, () => HasBaiduStreetView);

        _ = EnsureInitializedAsync();
    }

    public ObservableCollection<SurveyToolItem> Tools { get; }

    public ICollectionView ToolCollectionView { get; }

    public AsyncRelayCommand OpenToolCommand { get; }

    public RelayCommand ShowToolGuideCommand { get; }

    public RelayCommand ShowOverviewGuideCommand { get; }

    public RelayCommand ShowQuickReferenceGuideCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand ClearBaiduStreetViewCommand { get; }

    public SurveyToolItem? SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (!SetProperty(ref _selectedTool, value, nameof(SelectedTool)))
            {
                return;
            }

            RefreshCurrentToolState();
        }
    }

    public string OverviewTitle => "前期调研";

    public string OverviewSummary => "围绕 4 类场景组织 6 个实用工具。先选工具，再查看说明和路径后执行。";

    public string ToolCountLabel => $"{Tools.Count} 个工具";

    public string ToolGroupCountLabel =>
        $"{Tools.Select(tool => tool.GroupLabel).Distinct(StringComparer.OrdinalIgnoreCase).Count()} 个场景";

    public string CurrentToolTitle => SelectedTool?.DisplayName ?? "请选择工具";

    public string CurrentToolSummary =>
        SelectedTool?.Summary ?? "左侧选中一个工具后，这里会显示它的用途、输入、输出和流程说明。";

    public string CurrentToolBadge => SelectedTool?.Badge ?? "工具";

    public string CurrentToolStatusLabel => SelectedTool?.StatusLabel ?? "待选择";

    public string CurrentToolScenario =>
        SelectedTool?.Scenario ?? "建议先从“前期调研总览图”开始。";

    public string CurrentToolInputs => SelectedTool?.Inputs ?? "-";

    public string CurrentToolOutputs => SelectedTool?.Outputs ?? "-";

    public string CurrentToolParameters => SelectedTool?.KeyParameters ?? "-";

    public string CurrentToolNotes => SelectedTool?.Notes ?? "-";

    public IReadOnlyList<string> CurrentFlowSteps =>
        SelectedTool?.FlowSteps ?? ["请先在左侧选择工具。"];

    public Brush CurrentToolAccentBrush =>
        SelectedTool is null ? DefaultAccentBrush : CreateBrush(SelectedTool.AccentHex);

    public string CurrentToolMappingPath => ResolveCurrentToolPath();

    public string CurrentToolPathDisplay =>
        IsToolPathConfigured
            ? CurrentToolMappingPath
            : "尚未配置工具路径，可在设置页维护。";

    public bool IsToolPathConfigured => !string.IsNullOrWhiteSpace(CurrentToolMappingPath);

    public bool HasSurveyKeysConfigured =>
        _settings.EffectiveBaiduAk.Count > 0 && _settings.EffectiveAmapKey.Count > 0;

    public bool HasValidToolPath => SurveyToolCatalog.ToolPathLooksValid(CurrentToolMappingPath);

    public string CurrentToolPathStatus
    {
        get
        {
            if (SelectedTool is null)
            {
                return "请先选择一个工具。";
            }

            if (!IsToolPathConfigured)
            {
                return "未配置工具路径，请先到设置页维护。";
            }

            if (!HasSurveyKeysConfigured)
            {
                return "请先在设置页填写百度/高德 Key 后再打开工具箱。";
            }

            if (!HasValidToolPath)
            {
                return "路径已填写，但未找到对应的 .pyt 工具箱文件。";
            }

            return "工具路径已检测通过，可以直接打开。";
        }
    }

    public string UseToolStatusText
    {
        get => _useToolStatusText;
        private set => SetProperty(ref _useToolStatusText, value, nameof(UseToolStatusText));
    }

    public string CurrentIllustrationPath =>
        SelectedTool?.IllustrationPath ?? string.Empty;

    public bool HasCurrentIllustration =>
        !string.IsNullOrWhiteSpace(CurrentIllustrationPath) &&
        File.Exists(CurrentIllustrationPath);

    public string BaiduStreetViewHtmlContent
    {
        get => _baiduStreetViewHtmlContent;
        private set => SetProperty(ref _baiduStreetViewHtmlContent, value, nameof(BaiduStreetViewHtmlContent));
    }

    public string BaiduStreetViewStatusText
    {
        get => _baiduStreetViewStatusText;
        private set => SetProperty(ref _baiduStreetViewStatusText, value, nameof(BaiduStreetViewStatusText));
    }

    public string BaiduStreetViewCoordinateText
    {
        get => _baiduStreetViewCoordinateText;
        private set => SetProperty(ref _baiduStreetViewCoordinateText, value, nameof(BaiduStreetViewCoordinateText));
    }

    public bool HasBaiduStreetView => !string.IsNullOrWhiteSpace(BaiduStreetViewHtmlContent);

    internal void ShowBaiduStreetView(
        double wgsLongitude,
        double wgsLatitude,
        double bdLongitude,
        double bdLatitude,
        string ak)
    {
        var htmlContent = BaiduStreetViewPageBuilder.Build(
            ak,
            wgsLongitude,
            wgsLatitude,
            bdLongitude,
            bdLatitude);

        DispatchToUi(() =>
        {
            UpdateBaiduStreetViewState(
                htmlContent,
                "已在作业台中加载百度街景，可以继续点图刷新位置。",
                $"WGS84：{wgsLongitude:G17}, {wgsLatitude:G17} | BD09：{bdLongitude:G17}, {bdLatitude:G17}");
            UseToolStatusText = "已在作业台中打开百度街景。";
        });
    }

    internal async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            await ResearchSettingsStore.EnsureTemplateExistsAsync();
            await SurveyToolHelpStore.EnsureTemplateExistsAsync();

            _settings = await ResearchSettingsStore.LoadAsync();
            await SurveyToolRuntime.EnsureWorkspaceAsync(_settings, requireKeys: false);

            var (toolLookup, warning) = await SurveyToolHelpStore.LoadWithFallbackAsync();

            _toolLookup.Clear();
            Tools.Clear();

            foreach (var tool in toolLookup.Values.OrderBy(tool => tool.GroupId).ThenBy(tool => tool.DisplayName))
            {
                _toolLookup[tool.ToolId] = tool;
                Tools.Add(tool);
            }

            NotifyPropertyChanged(nameof(ToolCountLabel));
            NotifyPropertyChanged(nameof(ToolGroupCountLabel));

            SelectedTool ??= Tools.FirstOrDefault();
            _isInitialized = true;

            if (!string.IsNullOrWhiteSpace(warning))
            {
                UseToolStatusText = warning;
            }

            RefreshCurrentToolState();
        }
        catch (Exception exception)
        {
            UseToolStatusText = $"初始化前期调研模块失败：{exception.Message}";
            DiagnosticsLogger.Write(nameof(SurveyWorkbenchViewModel), "初始化前期调研模块失败。", exception);
        }
    }

    private bool CanOpenTool()
    {
        return !_isOpeningTool &&
               SelectedTool is not null &&
               HasSurveyKeysConfigured &&
               HasValidToolPath;
    }

    private async Task OpenCurrentPytToolAsync()
    {
        if (SelectedTool is null)
        {
            UseToolStatusText = "请先选择一个工具。";
            return;
        }

        try
        {
            _isOpeningTool = true;
            OpenToolCommand.RaiseCanExecuteChanged();

            _settings = await ResearchSettingsStore.LoadAsync();
            await SurveyToolRuntime.EnsureWorkspaceAsync(_settings, requireKeys: false);
            RefreshCurrentToolState();

            var toolPath = ResolveCurrentToolPath();
            if (string.IsNullOrWhiteSpace(toolPath))
            {
                UseToolStatusText = "当前工具还没有配置路径，请先打开设置页维护。";
                return;
            }

            if (!HasSurveyKeysConfigured)
            {
                UseToolStatusText = "请先在设置页填写百度/高德 Key。";
                return;
            }

            if (!SurveyToolCatalog.ToolPathLooksValid(toolPath))
            {
                UseToolStatusText = "当前工具路径无效，请检查 .pyt 文件是否存在，或到设置页更新路径。";
                return;
            }

            UseToolStatusText = $"正在打开工具：{SelectedTool.DisplayName}";
            await _pytToolLauncher.OpenAsync(
                toolPath,
                (level, message) =>
                {
                    DispatchToUi(() =>
                    {
                        UseToolStatusText = message;
                        if (level.Equals("错误", StringComparison.OrdinalIgnoreCase))
                        {
                            DiagnosticsLogger.Write(nameof(SurveyWorkbenchViewModel), message);
                        }
                    });
                });
        }
        catch (Exception exception)
        {
            UseToolStatusText = $"打开工具失败：{exception.Message}";
            DiagnosticsLogger.Write(nameof(SurveyWorkbenchViewModel), "打开前期调研工具失败。", exception);
        }
        finally
        {
            _isOpeningTool = false;
            OpenToolCommand.RaiseCanExecuteChanged();
        }
    }

    private void ShowSelectedToolGuide()
    {
        if (SelectedTool is null)
        {
            return;
        }

        GuideWindowHost.Show(GuideCatalog.SurveyToolGuide(SelectedTool));
    }

    private void ClearBaiduStreetView()
    {
        UpdateBaiduStreetViewState(string.Empty, EmptyStreetViewStatus, EmptyStreetViewCoordinate);
    }

    private void RefreshCurrentToolState()
    {
        NotifyPropertyChanged(nameof(CurrentToolTitle));
        NotifyPropertyChanged(nameof(CurrentToolSummary));
        NotifyPropertyChanged(nameof(CurrentToolBadge));
        NotifyPropertyChanged(nameof(CurrentToolStatusLabel));
        NotifyPropertyChanged(nameof(CurrentToolScenario));
        NotifyPropertyChanged(nameof(CurrentToolInputs));
        NotifyPropertyChanged(nameof(CurrentToolOutputs));
        NotifyPropertyChanged(nameof(CurrentToolParameters));
        NotifyPropertyChanged(nameof(CurrentToolNotes));
        NotifyPropertyChanged(nameof(CurrentFlowSteps));
        NotifyPropertyChanged(nameof(CurrentToolAccentBrush));
        NotifyPropertyChanged(nameof(CurrentToolMappingPath));
        NotifyPropertyChanged(nameof(CurrentToolPathDisplay));
        NotifyPropertyChanged(nameof(IsToolPathConfigured));
        NotifyPropertyChanged(nameof(HasSurveyKeysConfigured));
        NotifyPropertyChanged(nameof(HasValidToolPath));
        NotifyPropertyChanged(nameof(CurrentToolPathStatus));
        NotifyPropertyChanged(nameof(CurrentIllustrationPath));
        NotifyPropertyChanged(nameof(HasCurrentIllustration));

        if (!_isOpeningTool)
        {
            UseToolStatusText = CurrentToolPathStatus;
        }

        OpenToolCommand.RaiseCanExecuteChanged();
        ShowToolGuideCommand.RaiseCanExecuteChanged();
    }

    private void UpdateBaiduStreetViewState(string htmlContent, string statusText, string coordinateText)
    {
        BaiduStreetViewHtmlContent = htmlContent;
        BaiduStreetViewStatusText = statusText;
        BaiduStreetViewCoordinateText = coordinateText;
        NotifyPropertyChanged(nameof(HasBaiduStreetView));
        ClearBaiduStreetViewCommand.RaiseCanExecuteChanged();
    }

    private string ResolveCurrentToolPath()
    {
        if (SelectedTool is null)
        {
            return string.Empty;
        }

        if (_settings.PytToolMappings.TryGetValue(SelectedTool.ToolPathKey, out var configuredPath) &&
            !string.IsNullOrWhiteSpace(configuredPath))
        {
            return SurveyToolRuntime.NormalizeConfiguredToolPath(configuredPath);
        }

        return string.Empty;
    }

    private static void DispatchToUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
