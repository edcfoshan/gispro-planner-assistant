using System.Collections.ObjectModel;
using System.Windows;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Infrastructure;

internal static class ModuleLogStore
{
    internal static ObservableCollection<ResearchLogEntry> Entries { get; } = [];

    internal static void Append(string level, string message)
    {
        var entry = new ResearchLogEntry(DateTimeOffset.Now, level, message);
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => Entries.Insert(0, entry));
        }
        else
        {
            Entries.Insert(0, entry);
        }

        DiagnosticsLogger.Write(nameof(ModuleLogStore), $"[{level}] {message}");
    }

    internal static void Clear()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Entries.Clear);
            return;
        }

        Entries.Clear();
    }
}
