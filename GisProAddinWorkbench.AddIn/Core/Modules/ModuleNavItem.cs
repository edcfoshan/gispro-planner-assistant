namespace GisProAddinWorkbench.AddIn.Core.Modules;

internal sealed class ModuleNavItem
{
    internal ModuleNavItem(ModuleDescriptor descriptor)
    {
        Descriptor = descriptor;
    }

    public ModuleDescriptor Descriptor { get; }

    public string ModuleId => Descriptor.Definition.ModuleId;

    public string Name => Descriptor.Definition.Name;

    public string Category => Descriptor.Definition.Category;

    public string IconGlyph => Descriptor.Definition.IconGlyph;

    public bool IsFavorite { get; set; }
}
