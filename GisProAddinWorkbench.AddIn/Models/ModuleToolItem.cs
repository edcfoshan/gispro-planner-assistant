using System.Windows.Media;

namespace GisProAddinWorkbench.AddIn.Models;

internal sealed record ModuleToolItem(
    string Id,
    string Label,
    string Summary,
    string Tag,
    Brush AccentBrush,
    bool IsPlaceholder = false);
