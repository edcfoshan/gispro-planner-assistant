using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using GisProAddinWorkbench.AddIn.Guides;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class OpenColorStyleGuideButton : Button
{
    protected override void OnClick()
    {
        DiagnosticsLogger.Write(nameof(OpenColorStyleGuideButton), "配色步骤引导按钮点击。");
        try
        {
            GuideWindowHost.Show(GuideCatalog.ColorStyleGuide());
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(OpenColorStyleGuideButton), "打开配色步骤引导失败。", exception);
            MessageBox.Show(
                $"打开配色步骤引导失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "步骤引导");
        }
    }
}
