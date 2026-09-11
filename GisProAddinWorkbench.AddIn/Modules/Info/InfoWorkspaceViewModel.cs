using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using ArcGIS.Desktop.Framework.Contracts;
using GisProAddinWorkbench.AddIn.Core.Modules;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Modules.Info;

internal sealed class InfoWorkspaceViewModel : PropertyChangedBase
{
    private static readonly Brush DefaultAccentBrush = CreateBrush("#0F766E");

    private readonly ModuleContext _context;
    private readonly IModuleSettingsStore _settingsStore;
    private string _keyword = string.Empty;
    private bool _showPinnedOnly;
    private string _statusText = "待筛选";
    private ModuleToolItem? _selectedTool;

    internal InfoWorkspaceViewModel(ModuleContext context, IModuleSettingsStore settingsStore)
    {
        _context = context;
        _settingsStore = settingsStore;
        Bulletins = [];
        Tools =
        [
            new ModuleToolItem("info-search", "资讯检索", "按关键词筛选政策与行业资讯。", "搜", CreateBrush("#0F766E")),
            new ModuleToolItem("info-pinned", "置顶筛选", "优先查看置顶条目与关键通知。", "顶", CreateBrush("#0D9488")),
            new ModuleToolItem("info-refresh", "列表刷新", "刷新当前资讯列表并同步状态。", "刷", CreateBrush("#14B8A6")),
            new ModuleToolItem("info-policy", "政策库联动", "即将上线：按地区联动政策知识库。", "政", CreateBrush("#2DD4BF"), true),
            new ModuleToolItem("info-alert", "更新提醒", "即将上线：关注词订阅与变更提醒。", "提醒", CreateBrush("#5EEAD4"), true),
            new ModuleToolItem("info-guide", "案例教学", "即将上线：教程与案例导航。", "教", CreateBrush("#99F6E4"), true)
        ];

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsPlaceholderTool);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        SelectedTool = Tools[0];

        _ = InitializeAsync();
    }

    public ObservableCollection<InfoBulletin> Bulletins { get; }

    public ObservableCollection<ModuleToolItem> Tools { get; }

    public ModuleToolItem? SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (!SetProperty(ref _selectedTool, value, nameof(SelectedTool)))
            {
                return;
            }

            NotifyToolChanged();
            RefreshCommand.RaiseCanExecuteChanged();
            if (value is not null)
            {
                StatusText = value.IsPlaceholder
                    ? $"已切换到工具：{value.Label}（即将上线）"
                    : $"已切换到工具：{value.Label}";
                _context.PublishLog("信息", StatusText);
            }
        }
    }

    public string Keyword
    {
        get => _keyword;
        set => SetProperty(ref _keyword, value, nameof(Keyword));
    }

    public bool ShowPinnedOnly
    {
        get => _showPinnedOnly;
        set => SetProperty(ref _showPinnedOnly, value, nameof(ShowPinnedOnly));
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value, nameof(StatusText));
    }

    public string CurrentToolTitle => SelectedTool?.Label ?? "请选择工具";

    public string CurrentToolSummary => SelectedTool?.Summary ?? "请先在左侧选择工具。";

    public string CurrentToolTag => SelectedTool?.Tag ?? "资";

    public Brush CurrentToolAccentBrush => SelectedTool?.AccentBrush ?? DefaultAccentBrush;

    public bool IsPlaceholderTool => SelectedTool?.IsPlaceholder ?? false;

    public Visibility PlaceholderVisibility => IsPlaceholderTool ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FormVisibility => IsPlaceholderTool ? Visibility.Collapsed : Visibility.Visible;

    public string PlaceholderText => SelectedTool is null
        ? "请选择工具。"
        : $"{SelectedTool.Label} 正在开发中，当前版本暂不可用。";

    public string RefreshButtonText => SelectedTool?.Id switch
    {
        "info-pinned" => "筛选置顶",
        "info-refresh" => "刷新列表",
        _ => "执行检索"
    };

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand SaveSettingsCommand { get; }

    private async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync(
            "info",
            () => new InfoModuleSettings());

        Keyword = settings.PreferredKeyword;
        ShowPinnedOnly = settings.ShowPinnedOnly;
        SelectToolById(settings.LastSelectedToolId);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await Task.CompletedTask;
        if (SelectedTool?.IsPlaceholder == true)
        {
            StatusText = $"{SelectedTool.Label} 即将上线。";
            _context.PublishLog("警告", StatusText);
            return;
        }

        var data = new[]
        {
            new InfoBulletin("国土空间规划实施评估工作提示", "规划资讯中心", DateTimeOffset.Now.AddDays(-2), true),
            new InfoBulletin("存量建设用地盘活典型案例", "城乡更新周报", DateTimeOffset.Now.AddDays(-5), false),
            new InfoBulletin("省级重大项目选址审查要点", "项目审批简报", DateTimeOffset.Now.AddDays(-8), true)
        };

        var filtered = data.Where(item =>
        {
            if (ShowPinnedOnly && !item.IsPinned)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(Keyword) ||
                   item.Title.Contains(Keyword, StringComparison.OrdinalIgnoreCase) ||
                   item.Source.Contains(Keyword, StringComparison.OrdinalIgnoreCase);
        });

        Bulletins.Clear();
        foreach (var item in filtered)
        {
            Bulletins.Add(item);
        }

        StatusText = $"已筛选 {Bulletins.Count} 条资讯。";
        _context.PublishLog("信息", $"资讯模块执行：{SelectedTool?.Label ?? "资讯检索"}");
    }

    private async Task SaveSettingsAsync()
    {
        await _settingsStore.SaveAsync(
            "info",
            new InfoModuleSettings
            {
                PreferredKeyword = Keyword.Trim(),
                ShowPinnedOnly = ShowPinnedOnly,
                LastSelectedToolId = SelectedTool?.Id ?? "info-search"
            });

        StatusText = "资讯模块配置已保存。";
        _context.PublishLog("信息", $"资讯模块配置已保存：{_settingsStore.GetSettingsPath("info")}");
    }

    private void SelectToolById(string? toolId)
    {
        SelectedTool = Tools.FirstOrDefault(tool => tool.Id.Equals(toolId, StringComparison.OrdinalIgnoreCase))
                       ?? Tools.FirstOrDefault();
    }

    private void NotifyToolChanged()
    {
        NotifyPropertyChanged(nameof(CurrentToolTitle));
        NotifyPropertyChanged(nameof(CurrentToolSummary));
        NotifyPropertyChanged(nameof(CurrentToolTag));
        NotifyPropertyChanged(nameof(CurrentToolAccentBrush));
        NotifyPropertyChanged(nameof(IsPlaceholderTool));
        NotifyPropertyChanged(nameof(PlaceholderVisibility));
        NotifyPropertyChanged(nameof(FormVisibility));
        NotifyPropertyChanged(nameof(PlaceholderText));
        NotifyPropertyChanged(nameof(RefreshButtonText));
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
