using System;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Buttons;

public abstract class MapJumpButtonBase : Button
{
    protected abstract string ButtonName { get; }

    protected abstract string TargetToolId { get; }

    protected abstract string TargetToolName { get; }

    protected override async void OnClick()
    {
        DiagnosticsLogger.Write(GetType().Name, $"{ButtonName}按钮点击。");

        try
        {
            await FrameworkApplication.SetCurrentToolAsync(TargetToolId);
            DiagnosticsLogger.Write(GetType().Name, $"已激活 {TargetToolName}。");
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(GetType().Name, $"激活 {TargetToolName} 失败。", exception);
            MessageBox.Show(
                $"激活 {TargetToolName} 失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                TargetToolName);
        }
    }
}
