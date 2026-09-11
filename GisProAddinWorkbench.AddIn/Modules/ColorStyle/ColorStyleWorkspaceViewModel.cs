using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using ArcGIS.Desktop.Framework.Contracts;
using GisProAddinWorkbench.AddIn.Core.Modules;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Modules.ColorStyle;

internal sealed class ColorStyleWorkspaceViewModel : PropertyChangedBase
{
    private static readonly Brush DefaultAccentBrush = CreateBrush("#F97316");

    private readonly ModuleContext _context;
    private readonly IModuleSettingsStore _settingsStore;
    private SelectionOption<string>? _selectedPaletteOption;
    private bool _applyToCurrentMap = true;
    private string _statusText = "待应用";
    private ModuleToolItem? _selectedTool;

    internal ColorStyleWorkspaceViewModel(ModuleContext context, IModuleSettingsStore settingsStore)
    {
        _context = context;
        _settingsStore = settingsStore;

        PaletteOptions =
        [
            new SelectionOption<string>("规划蓝绿", "规划蓝绿"),
            new SelectionOption<string>("耕地暖色", "耕地暖色"),
            new SelectionOption<string>("生态冷色", "生态冷色"),
            new SelectionOption<string>("报批对比", "报批对比")
        ];

        Tools =
        [
            new ModuleToolItem("colorstyle-apply", "配色应用", "按预设色板快速套用图层样式。", "配", CreateBrush("#F97316")),
            new ModuleToolItem("colorstyle-legend", "图例模板", "统一图例模板样式并预览应用效果。", "例", CreateBrush("#FB923C")),
            new ModuleToolItem("colorstyle-export", "样式导出", "导出当前样式参数，便于跨项目复用。", "导", CreateBrush("#F59E0B")),
            new ModuleToolItem("colorstyle-rules", "规则配色", "即将上线：按地类/属性规则自动配色。", "规", CreateBrush("#FDBA74"), true),
            new ModuleToolItem("colorstyle-batch", "批量套色", "即将上线：批量图层一键套用色板。", "批", CreateBrush("#FED7AA"), true),
            new ModuleToolItem("colorstyle-audit", "一致性检查", "即将上线：配色偏差扫描与整改建议。", "检", CreateBrush("#FDBA74"), true)
        ];

        ExecuteToolCommand = new AsyncRelayCommand(ExecuteToolAsync, () => !IsPlaceholderTool);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        SelectedTool = Tools[0];

        _ = InitializeAsync();
    }

    public IReadOnlyList<SelectionOption<string>> PaletteOptions { get; }

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
            ExecuteToolCommand.RaiseCanExecuteChanged();
            if (value is not null)
            {
                StatusText = value.IsPlaceholder
                    ? $"已切换到工具：{value.Label}（即将上线）"
                    : $"已切换到工具：{value.Label}";
                _context.PublishLog("信息", StatusText);
            }
        }
    }

    public SelectionOption<string>? SelectedPaletteOption
    {
        get => _selectedPaletteOption;
        set => SetProperty(ref _selectedPaletteOption, value, nameof(SelectedPaletteOption));
    }

    public bool ApplyToCurrentMap
    {
        get => _applyToCurrentMap;
        set => SetProperty(ref _applyToCurrentMap, value, nameof(ApplyToCurrentMap));
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value, nameof(StatusText));
    }

    public string CurrentToolTitle => SelectedTool?.Label ?? "请选择工具";

    public string CurrentToolSummary => SelectedTool?.Summary ?? "请先在左侧选择工具。";

    public string CurrentToolTag => SelectedTool?.Tag ?? "配";

    public Brush CurrentToolAccentBrush => SelectedTool?.AccentBrush ?? DefaultAccentBrush;

    public bool IsPlaceholderTool => SelectedTool?.IsPlaceholder ?? false;

    public Visibility PlaceholderVisibility => IsPlaceholderTool ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FormVisibility => IsPlaceholderTool ? Visibility.Collapsed : Visibility.Visible;

    public string PlaceholderText => SelectedTool is null
        ? "请选择工具。"
        : $"{SelectedTool.Label} 正在开发中，当前版本暂不可用。";

    public string ExecuteButtonText => SelectedTool?.Id switch
    {
        "colorstyle-legend" => "应用模板",
        "colorstyle-export" => "导出样式",
        _ => "应用配色"
    };

    public AsyncRelayCommand ExecuteToolCommand { get; }

    public AsyncRelayCommand SaveSettingsCommand { get; }

    private async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync(
            "color-style",
            () => new ColorStyleModuleSettings());

        SelectedPaletteOption =
            PaletteOptions.FirstOrDefault(option => option.Value == settings.PaletteName) ??
            PaletteOptions.First();
        ApplyToCurrentMap = settings.ApplyToCurrentMap;
        SelectToolById(settings.LastSelectedToolId);
        StatusText = "配色模块配置已加载。";
    }

    private async Task ExecuteToolAsync()
    {
        await Task.CompletedTask;
        if (SelectedTool is null)
        {
            StatusText = "请先选择工具。";
            return;
        }

        if (SelectedTool.IsPlaceholder)
        {
            StatusText = $"{SelectedTool.Label} 即将上线。";
            _context.PublishLog("警告", StatusText);
            return;
        }

        var palette = SelectedPaletteOption?.Value ?? "规划蓝绿";
        StatusText = SelectedTool.Id switch
        {
            "colorstyle-legend" => $"已应用图例模板（色板：{palette}，示例流程）。",
            "colorstyle-export" => $"已导出样式参数（色板：{palette}，示例流程）。",
            _ => $"已应用配色预设（色板：{palette}，示例流程）。"
        };

        _context.PublishLog("信息", $"配色模块执行：{SelectedTool.Label}");
    }

    private async Task SaveSettingsAsync()
    {
        await _settingsStore.SaveAsync(
            "color-style",
            new ColorStyleModuleSettings
            {
                PaletteName = SelectedPaletteOption?.Value ?? "规划蓝绿",
                ApplyToCurrentMap = ApplyToCurrentMap,
                LastSelectedToolId = SelectedTool?.Id ?? "colorstyle-apply"
            });

        StatusText = "配色模块配置已保存。";
        _context.PublishLog("信息", $"配色模块配置已保存：{_settingsStore.GetSettingsPath("color-style")}");
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
        NotifyPropertyChanged(nameof(ExecuteButtonText));
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
