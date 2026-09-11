using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Services;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class OpenSurveyModuleButton : Button
{
    protected override async void OnClick()
    {
        DiagnosticsLogger.Write(nameof(OpenSurveyModuleButton), "数据获取按钮点击。");
        try
        {
            var launcher = new PytToolLauncher();
            await launcher.OpenToolboxAsync(SurveyToolCatalog.ToolboxPath);
            DiagnosticsLogger.Write(nameof(OpenSurveyModuleButton), "前期调研工具箱已在目录窗格中打开。");
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(OpenSurveyModuleButton), "打开前期调研工具箱失败。", exception);
            MessageBox.Show(
                $"打开前期调研工具箱失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "数据获取");
        }
    }
}
