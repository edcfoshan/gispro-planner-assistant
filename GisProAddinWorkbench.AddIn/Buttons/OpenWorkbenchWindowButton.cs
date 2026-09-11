using System;
using System.Windows;
using ArcGIS.Desktop.Framework.Contracts;
using ArcMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using GisProAddinWorkbench.AddIn.DockPanes;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class OpenWorkbenchWindowButton : Button
{
    protected override void OnClick()
    {
        DiagnosticsLogger.Write(
            nameof(OpenWorkbenchWindowButton),
            $"Clicked. Thread={Environment.CurrentManagedThreadId}");

        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                DiagnosticsLogger.Write(nameof(OpenWorkbenchWindowButton), "Dispatcher 为空。");
                ArcMessageBox.Show($"Dispatcher 为空。日志路径：{DiagnosticsLogger.CurrentLogPath}", "备用窗口");
                return;
            }

            dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    WorkbenchWindow.ShowWindow();
                    DiagnosticsLogger.Write(nameof(OpenWorkbenchWindowButton), "备用窗口已通过 Dispatcher 打开。");
                }
                catch (Exception ex)
                {
                    DiagnosticsLogger.Write(nameof(OpenWorkbenchWindowButton), "Dispatcher 内打开窗口失败。", ex);
                    ArcMessageBox.Show(
                        $"打开备用窗口失败。{Environment.NewLine}{ex.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                        "备用窗口");
                }
            }));
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(OpenWorkbenchWindowButton), "OnClick 出现异常。", exception);
            ArcMessageBox.Show(
                $"备用窗口按钮执行失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "备用窗口");
        }
    }
}
