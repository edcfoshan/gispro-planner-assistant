namespace GisProAddinWorkbench.AddIn.Buttons;

public sealed class OpenBaiduMapJumpButton : MapJumpButtonBase
{
    protected override string ButtonName => "百度地图网页打开";

    protected override string TargetToolId => "GisProAddinWorkbench_AddIn_OpenBaiduMapJumpTool";

    protected override string TargetToolName => "百度地图网页";
}
