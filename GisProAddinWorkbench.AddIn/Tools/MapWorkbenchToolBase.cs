using System.Windows;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using GisProAddinWorkbench.AddIn.DockPanes;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Tools;

internal abstract class MapWorkbenchToolBase : MapTool
{
    protected MapWorkbenchToolBase()
    {
        IsSketchTool = true;
        SketchType = SketchGeometryType.Point;
        SketchOutputMode = SketchOutputMode.Map;
        UseSnapping = false;
    }

    protected abstract string ToolName { get; }

    protected abstract string JumpTargetName { get; }

    protected abstract Task<MapWorkspaceRequest> BuildWorkspaceRequestAsync(MapPoint mapPoint);

    protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
    {
        if (geometry is not MapPoint mapPoint)
        {
            MapToolInteractionHelper.ScheduleRestoreDefaultBrowseTool();
            return true;
        }

        try
        {
            var request = await BuildWorkspaceRequestAsync(mapPoint);

            DiagnosticsLogger.Write(
                ToolName,
                $"准备在作业台中打开 {JumpTargetName}. Point=X={mapPoint.X:G17}, Y={mapPoint.Y:G17}; Url={request.NavigateUrl}");

            WorkbenchDockPaneViewModel.ShowMapWorkspace(request);
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(ToolName, $"打开 {JumpTargetName} 失败。", exception);
            MessageBox.Show(
                $"打开 {JumpTargetName} 失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                JumpTargetName,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            MapToolInteractionHelper.ScheduleRestoreDefaultBrowseTool();
        }

        return true;
    }
}
