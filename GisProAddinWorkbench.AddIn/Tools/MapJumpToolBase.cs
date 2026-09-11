using System.Diagnostics;
using System.Windows;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Tools;

internal abstract class MapJumpToolBase : MapTool
{
    protected MapJumpToolBase()
    {
        IsSketchTool = true;
        SketchType = SketchGeometryType.Point;
        SketchOutputMode = SketchOutputMode.Map;
        UseSnapping = false;
    }

    protected abstract string ToolName { get; }

    protected abstract string JumpTargetName { get; }

    protected abstract Task<string> BuildJumpUrlAsync(MapPoint mapPoint);

    protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
    {
        if (geometry is not MapPoint mapPoint)
        {
            MapToolInteractionHelper.ScheduleRestoreDefaultBrowseTool();
            return true;
        }

        try
        {
            var url = await BuildJumpUrlAsync(mapPoint);
            if (string.IsNullOrWhiteSpace(url))
            {
                return true;
            }

            DiagnosticsLogger.Write(
                ToolName,
                $"准备跳转 {JumpTargetName}. Point=X={mapPoint.X:G17}, Y={mapPoint.Y:G17}; Url={url}");

            OpenBrowser(url);
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

    protected static async Task<(double Longitude, double Latitude)> ProjectToWgs84Async(MapPoint mapPoint)
    {
        return await MapJumpToolHelpers.ProjectToWgs84Async(mapPoint);
    }

    protected static void OpenBrowser(string url)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };

        Process.Start(processInfo);
    }
}
