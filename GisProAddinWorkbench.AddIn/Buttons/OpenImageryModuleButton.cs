using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using GisProAddinWorkbench.AddIn.DockPanes;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class OpenImageryModuleButton : Button
{
    protected override void OnClick()
    {
        DiagnosticsLogger.Write(nameof(OpenImageryModuleButton), "在线瓦片服务按钮点击。");
        try
        {
            WorkbenchDockPaneViewModel.Show("imagery");
            DiagnosticsLogger.Write(nameof(OpenImageryModuleButton), "在线瓦片服务模块已打开。");
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(OpenImageryModuleButton), "打开在线瓦片服务模块失败。", exception);
            MessageBox.Show(
                $"打开在线瓦片服务模块失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "在线瓦片服务");
        }
    }
}
