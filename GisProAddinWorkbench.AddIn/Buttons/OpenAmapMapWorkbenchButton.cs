namespace GisProAddinWorkbench.AddIn.Buttons;

public sealed class OpenAmapMapWorkbenchButton : MapJumpButtonBase
{
    protected override string ButtonName => "高德地图作业台打开";

    protected override string TargetToolId => "GisProAddinWorkbench_AddIn_OpenAmapMapWorkbenchTool";

    protected override string TargetToolName => "高德地图作业台";
}
