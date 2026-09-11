using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Settings;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class OpenSettingsButton : Button
{
    protected override void OnClick()
    {
        DiagnosticsLogger.Write(nameof(OpenSettingsButton), "设置按钮点击。");
        try
        {
            GlobalSettingsWindowHost.ShowWindow();
            DiagnosticsLogger.Write(nameof(OpenSettingsButton), "设置窗口已打开。");
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(OpenSettingsButton), "打开设置窗口失败。", exception);
            MessageBox.Show(
                $"打开设置窗口失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "设置");
        }
    }
}
