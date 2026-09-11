using System.IO;
using System.Text.Json;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal static class ImagerySettingsMigration
{
    private const string LegacyRootFolderName = "GisProAddinWorkbench";
    private const string ModuleFolderName = "modules";
    private const string ModuleFileName = "module.imagery.json";

    internal static ImageryModuleSettings? LoadLegacySettings()
    {
        var legacyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            LegacyRootFolderName,
            ModuleFolderName,
            ModuleFileName);

        if (!File.Exists(legacyPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(legacyPath);
            return JsonSerializer.Deserialize<ImageryModuleSettings>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch
        {
            return null;
        }
    }
}
