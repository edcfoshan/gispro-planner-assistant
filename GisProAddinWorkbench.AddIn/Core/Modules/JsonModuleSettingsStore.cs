using System.Text.Json;
using System.IO;

namespace GisProAddinWorkbench.AddIn.Core.Modules;

internal sealed class JsonModuleSettingsStore : IModuleSettingsStore
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string GetSettingsPath(string moduleId)
    {
        return Path.Combine(GetSettingsDirectory(), $"module.{moduleId}.json");
    }

    public async Task<TSettings> LoadAsync<TSettings>(
        string moduleId,
        Func<TSettings> defaultFactory,
        CancellationToken cancellationToken = default)
        where TSettings : class, IModuleSettings
    {
        var path = GetSettingsPath(moduleId);
        if (!File.Exists(path))
        {
            var fallback = defaultFactory();
            await SaveAsync(moduleId, fallback, cancellationToken);
            return fallback;
        }

        await using var stream = File.OpenRead(path);
        var settings = await JsonSerializer.DeserializeAsync<TSettings>(stream, _serializerOptions, cancellationToken);
        return settings ?? defaultFactory();
    }

    public async Task SaveAsync<TSettings>(
        string moduleId,
        TSettings settings,
        CancellationToken cancellationToken = default)
        where TSettings : class, IModuleSettings
    {
        Directory.CreateDirectory(GetSettingsDirectory());
        var path = GetSettingsPath(moduleId);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, _serializerOptions, cancellationToken);
    }

    internal static string GetSettingsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GuotuGiserToolbox",
            "modules");
    }
}
