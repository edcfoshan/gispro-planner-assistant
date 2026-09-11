using System.Collections.ObjectModel;
using System.Windows;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using GisProAddinWorkbench.AddIn.Controls;
using GisProAddinWorkbench.AddIn.Core.Modules;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Models;
using GisProAddinWorkbench.AddIn.Services;

namespace GisProAddinWorkbench.AddIn.DockPanes;

internal sealed class WorkbenchShellViewModel : PropertyChangedBase
{
    private readonly ModuleContext _moduleContext;
    private readonly Dictionary<string, FrameworkElement> _workspaceCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ModuleNavItem> _allModules = [];
    private readonly List<ModuleNavItem> _recentHistory = [];
    private MapPageBrowser? _mapPageBrowser;
    private string _searchText = string.Empty;
    private ModuleNavItem? _selectedModule;
    private FrameworkElement? _currentWorkspace;
    private string _workspaceTitle = "请选择模块";
    private string _workspaceSummary = "在左侧选择模块后开始作业。";
    private string _shellStatus = "待开始";
    private bool _showWorkspaceHeader = true;
    private bool _isFullscreenWorkspaceMode;
    private int _workflowStage;

    internal WorkbenchShellViewModel()
    {
        FavoriteModules = [];
        RecentModules = [];
        VisibleModules = [];

        StartExecutionCommand = new RelayCommand(StartExecutionWithAutoChecks);
        OpenModuleCommand = new RelayCommand<ModuleNavItem>(OpenModule);
        ToggleFavoriteCommand = new RelayCommand<ModuleNavItem>(ToggleFavorite);

        _moduleContext = new ModuleContext(
            JsonModuleSettingsStore.GetSettingsDirectory(),
            Project.Current?.URI ?? string.Empty,
            Project.Current?.DefaultGeodatabasePath ?? string.Empty,
            AppendLog,
            PublishNotification);

        LoadModules();
    }

    public ObservableCollection<ModuleNavItem> FavoriteModules { get; }

    public ObservableCollection<ModuleNavItem> RecentModules { get; }

    public ObservableCollection<ModuleNavItem> VisibleModules { get; }

    public ObservableCollection<ResearchLogEntry> ShellLogs => ModuleLogStore.Entries;

    public RelayCommand StartExecutionCommand { get; }

    public RelayCommand<ModuleNavItem> OpenModuleCommand { get; }

    public RelayCommand<ModuleNavItem> ToggleFavoriteCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value, nameof(SearchText)))
            {
                RefreshVisibleModules();
            }
        }
    }

    public ModuleNavItem? SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (SetProperty(ref _selectedModule, value, nameof(SelectedModule)) && value is not null)
            {
                OpenModule(value);
            }
        }
    }

    public FrameworkElement? CurrentWorkspace
    {
        get => _currentWorkspace;
        private set => SetProperty(ref _currentWorkspace, value, nameof(CurrentWorkspace));
    }

    public string WorkspaceTitle
    {
        get => _workspaceTitle;
        private set => SetProperty(ref _workspaceTitle, value, nameof(WorkspaceTitle));
    }

    public string WorkspaceSummary
    {
        get => _workspaceSummary;
        private set => SetProperty(ref _workspaceSummary, value, nameof(WorkspaceSummary));
    }

    public bool ShowWorkspaceHeader
    {
        get => _showWorkspaceHeader;
        private set => SetProperty(ref _showWorkspaceHeader, value, nameof(ShowWorkspaceHeader));
    }

    public string ShellStatus
    {
        get => _shellStatus;
        private set => SetProperty(ref _shellStatus, value, nameof(ShellStatus));
    }

    public bool IsFullscreenWorkspaceMode
    {
        get => _isFullscreenWorkspaceMode;
        private set => SetProperty(ref _isFullscreenWorkspaceMode, value, nameof(IsFullscreenWorkspaceMode));
    }

    public string VersionStepState => _workflowStage >= 1 ? "已完成" : "待完成";

    public string SettingsStepState => _workflowStage >= 2 ? "已完成" : "待完成";

    public string ExecutionStepState => _workflowStage >= 3 ? "进行中" : "未开始";

    private void LoadModules()
    {
        _allModules.Clear();
        _allModules.AddRange(
            ModuleRegistry.CreateDefault()
                .Select(descriptor => new ModuleNavItem(descriptor)));

        foreach (var module in _allModules)
        {
            if (module.ModuleId.Equals("poi", StringComparison.OrdinalIgnoreCase))
            {
                module.IsFavorite = true;
            }
        }

        RefreshVisibleModules();
        SelectedModule = VisibleModules.FirstOrDefault();
    }

    private void RefreshCollections(IEnumerable<ModuleNavItem> visibleModules)
    {
        var moduleList = visibleModules.ToList();

        VisibleModules.Clear();
        foreach (var module in moduleList)
        {
            VisibleModules.Add(module);
        }

        FavoriteModules.Clear();
        foreach (var module in _allModules.Where(item => item.IsFavorite))
        {
            FavoriteModules.Add(module);
        }
    }

    private void RefreshVisibleModules()
    {
        var filtered = _allModules.Where(item =>
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            return item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   item.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        });

        RefreshCollections(filtered);

        if (SelectedModule is null || !VisibleModules.Contains(SelectedModule))
        {
            SelectedModule = VisibleModules.FirstOrDefault();
        }
    }

    private void OpenModule(ModuleNavItem? module)
    {
        if (module is null)
        {
            return;
        }

        SetProperty(ref _selectedModule, module, nameof(SelectedModule));

        var moduleId = module.ModuleId;
        if (!_workspaceCache.TryGetValue(moduleId, out var workspace))
        {
            workspace = module.Descriptor.Workspace.CreateWorkspace(_moduleContext);
            _workspaceCache[moduleId] = workspace;
        }

        IsFullscreenWorkspaceMode = false;
        CurrentWorkspace = workspace;
        WorkspaceTitle = module.Name;
        WorkspaceSummary = module.ModuleId.Equals("poi", StringComparison.OrdinalIgnoreCase)
            ? "按场景整理好的工具作业台。先选工具，再确认说明和路径后执行。"
            : $"分类：{module.Category}；能力：{string.Join("、", module.Descriptor.Definition.Capabilities)}";
        ShowWorkspaceHeader = !module.ModuleId.Equals("imagery", StringComparison.OrdinalIgnoreCase);

        AddRecent(module);
        PublishNotification("信息", $"已切换模块：{module.Name}");
    }

    internal void ActivateModule(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return;
        }

        SearchText = string.Empty;
        var target = _allModules.FirstOrDefault(
            item => item.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            PublishNotification("警告", $"未找到模块：{moduleId}");
            return;
        }

        OpenModule(target);
    }

    internal void ShowMapWorkspace(MapWorkspaceRequest request)
    {
        _mapPageBrowser ??= new MapPageBrowser
        {
            HtmlContent = string.Empty
        };

        _mapPageBrowser.NavigateUrl = request.NavigateUrl;
        CurrentWorkspace = _mapPageBrowser;
        WorkspaceTitle = request.WorkspaceTitle;
        WorkspaceSummary = request.WorkspaceSummary;
        ShowWorkspaceHeader = false;
        ShellStatus = request.ShellStatus;
        IsFullscreenWorkspaceMode = true;
        AppendLog("信息", $"已打开{request.WorkspaceTitle}：{request.WorkspaceSummary}");
    }

    private void AddRecent(ModuleNavItem module)
    {
        _recentHistory.RemoveAll(item => item.ModuleId == module.ModuleId);
        _recentHistory.Insert(0, module);
        if (_recentHistory.Count > 5)
        {
            _recentHistory.RemoveRange(5, _recentHistory.Count - 5);
        }

        RecentModules.Clear();
        foreach (var item in _recentHistory)
        {
            RecentModules.Add(item);
        }
    }

    private void ToggleFavorite(ModuleNavItem? module)
    {
        if (module is null)
        {
            return;
        }

        module.IsFavorite = !module.IsFavorite;
        RefreshVisibleModules();
        NotifyPropertyChanged(nameof(SelectedModule));
        PublishNotification("信息", module.IsFavorite
            ? $"已收藏模块：{module.Name}"
            : $"已取消收藏：{module.Name}");
    }

    private void StartExecutionWithAutoChecks()
    {
        try
        {
            EnsureVersionConfirmed();
            EnsureSettingsConfirmed();

            _workflowStage = 3;
            RaiseWorkflowChanged("自动检查完成，已进入执行阶段。");
            PublishNotification("信息", "自动流程检查通过，已进入执行阶段。");
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(WorkbenchShellViewModel), "自动流程检查失败。", exception);
            PublishNotification("错误", $"自动流程检查失败：{exception.Message}");
        }
    }

    private void EnsureVersionConfirmed()
    {
        if (_workflowStage < 1)
        {
            _workflowStage = 1;
            NotifyPropertyChanged(nameof(VersionStepState));
            PublishNotification("信息", "已自动完成版本与声明确认。");
        }
    }

    private void EnsureSettingsConfirmed()
    {
        ResearchSettingsStore.EnsureTemplateExists();
        _workflowStage = Math.Max(_workflowStage, 2);
        NotifyPropertyChanged(nameof(SettingsStepState));
        PublishNotification("信息", $"已自动完成设置校验：{ResearchSettingsStore.GetSettingsPath()}");
    }

    private void RaiseWorkflowChanged(string status)
    {
        ShellStatus = status;
        NotifyPropertyChanged(nameof(VersionStepState));
        NotifyPropertyChanged(nameof(SettingsStepState));
        NotifyPropertyChanged(nameof(ExecutionStepState));
    }

    private void PublishNotification(string level, string message)
    {
        AppendLog(level, message);
        ShellStatus = message;
    }

    private static void AppendLog(string level, string message)
    {
        ModuleLogStore.Append(level, message);
    }
}
