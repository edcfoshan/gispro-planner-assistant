namespace GisProAddinWorkbench.AddIn.Core.Modules;

internal interface IModuleSettingsStore
{
    string GetSettingsPath(string moduleId);

    Task<TSettings> LoadAsync<TSettings>(
        string moduleId,
        Func<TSettings> defaultFactory,
        CancellationToken cancellationToken = default)
        where TSettings : class, IModuleSettings;

    Task SaveAsync<TSettings>(
        string moduleId,
        TSettings settings,
        CancellationToken cancellationToken = default)
        where TSettings : class, IModuleSettings;
}
