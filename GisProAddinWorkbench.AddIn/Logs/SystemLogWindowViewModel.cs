using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Logs;

internal sealed class SystemLogWindowViewModel : PropertyChangedBase
{
    internal SystemLogWindowViewModel()
    {
        ClearCommand = new RelayCommand(ClearLogs);
        OpenDiagnosticsFolderCommand = new RelayCommand(OpenDiagnosticsFolder);
        OpenDiagnosticsFileCommand = new RelayCommand(OpenDiagnosticsFile);
    }

    public ObservableCollection<ResearchLogEntry> Logs => ModuleLogStore.Entries;

    public string DiagnosticsPath => DiagnosticsLogger.CurrentLogPath;

    public RelayCommand ClearCommand { get; }

    public RelayCommand OpenDiagnosticsFolderCommand { get; }

    public RelayCommand OpenDiagnosticsFileCommand { get; }

    private static void ClearLogs()
    {
        ModuleLogStore.Clear();
    }

    private static void OpenDiagnosticsFolder()
    {
        try
        {
            var folderPath = Path.GetDirectoryName(DiagnosticsLogger.CurrentLogPath);
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new InvalidOperationException("未找到日志目录。");
            }

            Directory.CreateDirectory(folderPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(SystemLogWindowViewModel), "打开诊断日志目录失败。", exception);
            MessageBox.Show(
                $"打开诊断日志目录失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "系统日志");
        }
    }

    private static void OpenDiagnosticsFile()
    {
        try
        {
            var logPath = DiagnosticsLogger.CurrentLogPath;
            var folderPath = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            if (!File.Exists(logPath))
            {
                File.WriteAllText(logPath, string.Empty);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = logPath,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(SystemLogWindowViewModel), "打开诊断日志文件失败。", exception);
            MessageBox.Show(
                $"打开诊断日志文件失败。{Environment.NewLine}{exception.Message}{Environment.NewLine}日志路径：{DiagnosticsLogger.CurrentLogPath}",
                "系统日志");
        }
    }
}
