using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using GisProAddinWorkbench.AddIn.DockPanes;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class OpenWorkbenchButton : Button
{
    protected override void OnClick()
    {
        DiagnosticsLogger.Write(nameof(OpenWorkbenchButton), "工作台按钮点击。");
        try
        {
            WorkbenchDockPaneViewModel.Show();
            DiagnosticsLogger.Write(nameof(OpenWorkbenchButton), "DockPane 已打开。");
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(OpenWorkbenchButton), "打开失败。", exception);
            MessageBox.Show(
                $"打开“城乡规划作业台”失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "城乡规划作业台");
        }
    }
}
