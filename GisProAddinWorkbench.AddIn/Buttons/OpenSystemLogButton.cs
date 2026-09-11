using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Logs;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class OpenSystemLogButton : Button
{
    protected override void OnClick()
    {
        DiagnosticsLogger.Write(nameof(OpenSystemLogButton), "系统日志按钮点击。");
        try
        {
            SystemLogWindowHost.ShowWindow();
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(OpenSystemLogButton), "打开系统日志窗口失败。", exception);
            MessageBox.Show(
                $"打开系统日志窗口失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "系统日志");
        }
    }
}
