using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using GisProAddinWorkbench.AddIn.Guides;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class OpenImageryGuideButton : Button
{
    protected override void OnClick()
    {
        DiagnosticsLogger.Write(nameof(OpenImageryGuideButton), "在线瓦片服务步骤引导按钮点击。");
        try
        {
            GuideWindowHost.Show(GuideCatalog.ImageryGuide());
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(OpenImageryGuideButton), "打开在线瓦片服务步骤引导失败。", exception);
            MessageBox.Show(
                $"打开在线瓦片服务步骤引导失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "步骤引导");
        }
    }
}
