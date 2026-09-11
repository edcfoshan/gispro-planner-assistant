using System.IO;

namespace GisProAddinWorkbench.AddIn.Infrastructure;

internal static class DiagnosticsLogger
{
    private static readonly object Sync = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GuotuGiserToolbox",
        "addin-diagnostics.log");

    internal static string CurrentLogPath => LogPath;

    internal static void Write(string source, string message, Exception? exception = null)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}] {message}";
            if (exception is not null)
            {
                line += $"{Environment.NewLine}{exception}{Environment.NewLine}";
            }
            else
            {
                line += Environment.NewLine;
            }

            lock (Sync)
            {
                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
            // Keep diagnostics best-effort; never block add-in workflow.
        }
    }
}
