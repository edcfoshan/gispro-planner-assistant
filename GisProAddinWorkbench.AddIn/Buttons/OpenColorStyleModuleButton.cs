using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using GisProAddinWorkbench.AddIn.DockPanes;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class OpenColorStyleModuleButton : Button
{
    protected override void OnClick()
    {
        DiagnosticsLogger.Write(nameof(OpenColorStyleModuleButton), "配色按钮点击。");
        try
        {
            WorkbenchDockPaneViewModel.Show("color-style");
            DiagnosticsLogger.Write(nameof(OpenColorStyleModuleButton), "配色模块已打开。");
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(OpenColorStyleModuleButton), "打开配色模块失败。", exception);
            MessageBox.Show(
                $"打开配色模块失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "配色");
        }
    }
}
