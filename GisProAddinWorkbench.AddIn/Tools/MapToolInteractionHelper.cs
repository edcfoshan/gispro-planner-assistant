using ArcGIS.Desktop.Framework;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Tools;

internal static class MapToolInteractionHelper
{
    private const string DefaultBrowseToolId = "esri_mapping_exploreTool";

    internal static void ScheduleRestoreDefaultBrowseTool()
    {
        _ = RestoreDefaultBrowseToolAsync();
    }

    internal static async Task RestoreDefaultBrowseToolAsync()
    {
        try
        {
            await Task.Yield();
            await Task.Delay(100);

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await FrameworkApplication.SetCurrentToolAsync(DefaultBrowseToolId);
                    DiagnosticsLogger.Write(
                        nameof(MapToolInteractionHelper),
                        $"Restored default browse tool. Attempt={attempt}");
                    return;
                }
                catch (Exception exception)
                {
                    if (attempt == 3)
                    {
                        throw;
                    }

                    DiagnosticsLogger.Write(
                        nameof(MapToolInteractionHelper),
                        $"Restore default browse tool failed on attempt {attempt}, will retry.",
                        exception);
                    await Task.Delay(100 * attempt);
                }
            }
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(
                nameof(MapToolInteractionHelper),
                "Failed to restore default browse tool.",
                exception);
        }
    }
}
