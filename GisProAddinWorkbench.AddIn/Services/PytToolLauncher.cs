using System.IO;
using System.Linq;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.GeoProcessing;

namespace GisProAddinWorkbench.AddIn.Services;

internal sealed class PytToolLauncher
{
    private const string CatalogDockPaneId = "esri_core_projectDockPane";

    internal async Task OpenToolboxAsync(string toolboxPath)
    {
        await SurveyToolRuntime.EnsureWorkspaceAsync(requireKeys: false);

        if (string.IsNullOrWhiteSpace(toolboxPath))
        {
            throw new InvalidOperationException("工具箱路径为空，无法打开工具箱。");
        }

        var project = Project.Current ?? throw new InvalidOperationException("当前没有打开 ArcGIS 项目。");
        var normalizedPath = SurveyToolRuntime.NormalizeConfiguredToolPath(toolboxPath);
        var filePath = SurveyToolCatalog.GetToolboxFilePath(normalizedPath);
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException($"未找到工具箱文件：{filePath}", filePath);
        }

        var toolboxItem = await QueuedTask.Run(() => ResolveToolboxItem(project, filePath));

        FrameworkApplication.DockPaneManager.Find(CatalogDockPaneId)?.Activate();

        if (Project.GetCatalogPane(true) is not IProjectWindow catalogWindow)
        {
            throw new InvalidOperationException("无法打开 ArcGIS 目录窗口。");
        }

        await catalogWindow.SelectItemAsync(
            toolboxItem,
            true,
            true,
            null);
    }

    internal async Task OpenAsync(
        string toolPath,
        Action<string, string>? onStatusChanged = null)
    {
        await SurveyToolRuntime.EnsureWorkspaceAsync(requireKeys: true);

        if (string.IsNullOrWhiteSpace(toolPath))
        {
            throw new InvalidOperationException("工具路径为空，无法打开 PYT 工具。");
        }

        var normalizedPath = SurveyToolRuntime.NormalizeConfiguredToolPath(toolPath);
        var toolboxPath = SurveyToolCatalog.GetToolboxFilePath(normalizedPath);
        if (string.IsNullOrWhiteSpace(toolboxPath) || !File.Exists(toolboxPath))
        {
            throw new FileNotFoundException($"未找到工具箱文件：{toolboxPath}", toolboxPath);
        }

        Geoprocessing.OpenToolDialog(
            normalizedPath,
            Geoprocessing.MakeValueArray(),
            null,
            newSubPane: false,
            callback: (eventName, payload) =>
            {
                switch (eventName)
                {
                    case "OnBeginExecute":
                        onStatusChanged?.Invoke("信息", "工具已开始执行，请查看 Geoprocessing 面板。");
                        break;
                    case "OnEndExecute":
                        if (payload is IGPResult result)
                        {
                            onStatusChanged?.Invoke(
                                result.IsFailed ? "错误" : "信息",
                                result.IsFailed
                                    ? "工具执行失败，请检查 Geoprocessing 面板中的错误详情。"
                                    : "工具执行完成。");
                        }

                        break;
                    case "OnClose":
                        onStatusChanged?.Invoke("信息", "工具窗口已关闭。");
                        break;
                }
            });
    }

    private static Item ResolveToolboxItem(Project project, string filePath)
    {
        var item = project.FindItem(filePath);
        if (item is not null)
        {
            return item;
        }

        var toolboxFolderPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(toolboxFolderPath))
        {
            throw new FileNotFoundException($"无法解析工具箱所在目录：{filePath}", filePath);
        }

        var folderConnection = FindFolderConnection(project, toolboxFolderPath);
        if (folderConnection is null)
        {
            folderConnection = CreateFolderConnection(project, toolboxFolderPath);
        }

        if (folderConnection is null)
        {
            throw new FileNotFoundException($"无法定位工具箱文件夹连接：{toolboxFolderPath}", toolboxFolderPath);
        }

        folderConnection.Refresh();

        item = folderConnection
            .GetItems()
            .FirstOrDefault(existing =>
                PathEquals(existing.Path, filePath) ||
                PathEquals(existing.PhysicalPath, filePath) ||
                string.Equals(existing.Name, Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase));

        item ??= project.FindItem(filePath);

        if (item is null)
        {
            throw new FileNotFoundException($"无法定位工具箱项：{filePath}", filePath);
        }

        return item;
    }

    private static FolderConnectionProjectItem? FindFolderConnection(Project project, string folderPath)
    {
        return project
            .GetItems<FolderConnectionProjectItem>()
            .FirstOrDefault(existing =>
                PathEquals(existing.Path, folderPath) ||
                PathEquals(existing.PhysicalPath, folderPath));
    }

    private static FolderConnectionProjectItem? CreateFolderConnection(Project project, string folderPath)
    {
        var folderItem = ItemFactory.Instance.Create(folderPath);
        if (folderItem is not IProjectItem projectItem)
        {
            throw new FileNotFoundException($"无法创建工具箱文件夹连接：{folderPath}", folderPath);
        }

        project.AddItem(projectItem);
        return FindFolderConnection(project, folderPath);
    }

    private static bool PathEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            left.Trim().Replace('/', '\\'),
            right.Trim().Replace('/', '\\'),
            StringComparison.OrdinalIgnoreCase);
    }
}
