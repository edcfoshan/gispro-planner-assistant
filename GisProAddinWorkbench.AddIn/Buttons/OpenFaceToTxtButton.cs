using System;
using System.Windows;
using ArcGIS.Desktop.Framework.Contracts;
using ArcMessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using GisProAddinWorkbench.AddIn.FaceToTxt;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class OpenFaceToTxtButton : Button
{
    protected override void OnClick()
    {
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                ArcMessageBox.Show("应用程序调度器不可用，无法打开面转 TXT 工作台。", "面转 TXT");
                return;
            }

            dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    FaceToTxtWorkbenchWindow.ShowWindow();
                    DiagnosticsLogger.Write(nameof(OpenFaceToTxtButton), "已打开面转 TXT 工作台。");
                }
                catch (Exception exception)
                {
                    DiagnosticsLogger.Write(nameof(OpenFaceToTxtButton), "打开面转 TXT 工作台失败。", exception);
                    ArcMessageBox.Show(
                        $"打开面转 TXT 工作台失败：{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                        "面转 TXT");
                }
            }));
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(OpenFaceToTxtButton), "OnClick 出现异常。", exception);
            ArcMessageBox.Show(
                $"面转 TXT 按钮执行失败：{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "面转 TXT");
        }
    }
}

