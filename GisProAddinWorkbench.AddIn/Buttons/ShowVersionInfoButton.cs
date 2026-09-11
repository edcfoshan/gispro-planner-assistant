using System.IO;
using System.Reflection;
using ArcGIS.Desktop.Framework.Contracts;
using GisProAddinWorkbench.AddIn.Info;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class ShowVersionInfoButton : Button
{
    protected override void OnClick()
    {
        var assembly = typeof(ShowVersionInfoButton).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "未知";
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? version;
        var assemblyPath = assembly.Location;
        var updatedAt = File.Exists(assemblyPath)
            ? File.GetLastWriteTime(assemblyPath).ToString("yyyy-MM-dd HH:mm")
            : DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        DiagnosticsLogger.Write(nameof(ShowVersionInfoButton), "版本与声明按钮点击。");

        var window = new VersionStatementWindow(
            version,
            informationalVersion,
            updatedAt,
            DiagnosticsLogger.CurrentLogPath);
        window.ShowDialog();
    }
}
