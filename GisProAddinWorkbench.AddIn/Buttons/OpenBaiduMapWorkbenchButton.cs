namespace GisProAddinWorkbench.AddIn.Buttons;

public sealed class OpenBaiduMapWorkbenchButton : MapJumpButtonBase
{
    protected override string ButtonName => "百度地图作业台打开";

    protected override string TargetToolId => "GisProAddinWorkbench_AddIn_OpenBaiduMapWorkbenchTool";

    protected override string TargetToolName => "百度地图作业台";
}
