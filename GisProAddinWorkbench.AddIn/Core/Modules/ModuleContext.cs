namespace GisProAddinWorkbench.AddIn.Core.Modules;

internal sealed record ModuleContext(
    string SettingsRootDirectory,
    string CurrentProjectPath,
    string DefaultGeodatabasePath,
    Action<string, string> PublishLog,
    Action<string, string> PublishNotification);
