using ArcGIS.Desktop.Framework.Contracts;

namespace GisProAddinWorkbench.AddIn.Menus;

public sealed class BaiduMapJumpMenu : DynamicMenu
{
    protected override void OnPopup()
    {
        AddReference("GisProAddinWorkbench_AddIn_OpenBaiduMapJumpButton");
        AddReference("GisProAddinWorkbench_AddIn_OpenBaiduMapWorkbenchButton");
    }
}
