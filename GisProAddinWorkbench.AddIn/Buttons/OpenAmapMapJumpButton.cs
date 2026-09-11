namespace GisProAddinWorkbench.AddIn.Buttons;

public sealed class OpenAmapMapJumpButton : MapJumpButtonBase
{
    protected override string ButtonName => "高德地图网页打开";

    protected override string TargetToolId => "GisProAddinWorkbench_AddIn_OpenAmapMapJumpTool";

    protected override string TargetToolName => "高德地图网页";
}
