namespace GisProAddinWorkbench.AddIn.Core.Modules;

internal interface IModuleDefinition
{
    string ModuleId { get; }

    string Name { get; }

    string Category { get; }

    int Order { get; }

    string IconGlyph { get; }

    IReadOnlyList<string> Capabilities { get; }
}
