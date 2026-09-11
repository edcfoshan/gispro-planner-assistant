using ArcGIS.Desktop.Framework.Contracts;

namespace GisProAddinWorkbench.AddIn.Menus;

public sealed class AmapMapJumpMenu : DynamicMenu
{
    protected override void OnPopup()
    {
        AddReference("GisProAddinWorkbench_AddIn_OpenAmapMapJumpButton");
        AddReference("GisProAddinWorkbench_AddIn_OpenAmapMapWorkbenchButton");
    }
}
