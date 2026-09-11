using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GisProAddinWorkbench.AddIn.FaceToTxt;

internal static class FaceToTxtSettingsStore
{
    private const string SettingsFileName = "face-to-txt-workspace.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    internal static string GetSettingsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GuotuGiserToolbox");
    }

    internal static string GetSettingsPath()
    {
        return Path.Combine(GetSettingsDirectory(), SettingsFileName);
    }

    internal static async Task EnsureTemplateExistsAsync()
    {
        var directory = GetSettingsDirectory();
        Directory.CreateDirectory(directory);

        var path = GetSettingsPath();
        if (File.Exists(path))
        {
            return;
        }

        await SaveAsync(CreateDefaultState());
    }

    internal static async Task<FaceToTxtWorkspaceState> LoadAsync()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            var defaultState = CreateDefaultState();
            await SaveAsync(defaultState);
            return defaultState;
        }

        await using var stream = File.OpenRead(path);
        var state = await JsonSerializer.DeserializeAsync<FaceToTxtWorkspaceState>(stream, SerializerOptions);
        return Normalize(state ?? CreateDefaultState());
    }

    internal static async Task SaveAsync(FaceToTxtWorkspaceState state)
    {
        var normalized = Normalize(state);

        var directory = GetSettingsDirectory();
        Directory.CreateDirectory(directory);

        await using var stream = File.Create(GetSettingsPath());
        await JsonSerializer.SerializeAsync(stream, normalized, SerializerOptions);
    }

    internal static void Save(FaceToTxtWorkspaceState state)
    {
        var normalized = Normalize(state);

        var directory = GetSettingsDirectory();
        Directory.CreateDirectory(directory);

        using var stream = File.Create(GetSettingsPath());
        JsonSerializer.Serialize(stream, normalized, SerializerOptions);
    }

    internal static FaceToTxtWorkspaceState CreateDefaultState()
    {
        var preset = new FaceToTxtPresetState
        {
            Name = "默认配置",
            Settings = CreateDefaultSettings()
        };

        return new FaceToTxtWorkspaceState
        {
            LastPresetId = preset.Id,
            Presets = [preset]
        };
    }

    internal static FaceToTxtExportSettings CreateDefaultSettings()
    {
        return new FaceToTxtExportSettings
        {
            InputMode = FaceToTxtInputMode.CurrentSelection,
            OutputDirectory = GetDefaultOutputDirectory(),
            TxtMode = FaceToTxtTxtMode.StandardHeader,
            FileNameMode = FaceToTxtFileNameMode.LayerName,
            AutoMapSheet = true,
            UseNewStandard = true,
            SwapXY = true,
            IncludeClosingPoint = true,
            RepairGeometry = true,
            NumberHoleRings = true,
            ProductionDateText = DateTime.Today.ToString("yyyy-MM-dd"),
            CoordinateSystemName = "2000国家大地坐标系",
            ProjectionTypeName = "高斯克吕格",
            UnitName = "米",
            CoordinateDecimals = 4,
            AreaDecimals = 4,
            PointNumberDigits = 2,
            CoordinatePrefix = "J",
            AreaUnit = FaceToTxtAreaUnit.Hectare
        };
    }

    internal static string GetDefaultOutputDirectory()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrWhiteSpace(desktop))
        {
            return desktop;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private static FaceToTxtWorkspaceState Normalize(FaceToTxtWorkspaceState? state)
    {
        var normalized = state ?? CreateDefaultState();
        normalized.Presets ??= [];

        if (normalized.Presets.Count == 0)
        {
            return CreateDefaultState();
        }

        foreach (var preset in normalized.Presets)
        {
            preset.Id = string.IsNullOrWhiteSpace(preset.Id)
                ? Guid.NewGuid().ToString("N")
                : preset.Id.Trim();
            preset.Name = string.IsNullOrWhiteSpace(preset.Name)
                ? "默认配置"
                : preset.Name.Trim();
            preset.Settings = Normalize(preset.Settings);
        }

        if (normalized.Presets.All(preset => !preset.Id.Equals(normalized.LastPresetId, StringComparison.OrdinalIgnoreCase)))
        {
            normalized.LastPresetId = normalized.Presets[0].Id;
        }

        return normalized;
    }

    private static FaceToTxtExportSettings Normalize(FaceToTxtExportSettings? settings)
    {
        var normalized = settings?.Clone() ?? CreateDefaultSettings();
        normalized.OutputDirectory = string.IsNullOrWhiteSpace(normalized.OutputDirectory)
            ? GetDefaultOutputDirectory()
            : normalized.OutputDirectory.Trim();
        normalized.BrowseDatasetPath = normalized.BrowseDatasetPath?.Trim() ?? string.Empty;
        normalized.NamingField = normalized.NamingField?.Trim() ?? string.Empty;
        normalized.ParcelIdField = normalized.ParcelIdField?.Trim() ?? string.Empty;
        normalized.ParcelNameField = normalized.ParcelNameField?.Trim() ?? string.Empty;
        normalized.ParcelUseField = normalized.ParcelUseField?.Trim() ?? string.Empty;
        normalized.LandCodeField = normalized.LandCodeField?.Trim() ?? string.Empty;
        normalized.AreaField = normalized.AreaField?.Trim() ?? string.Empty;
        normalized.MapSheetField = normalized.MapSheetField?.Trim() ?? string.Empty;
        normalized.CustomDescriptionText = normalized.CustomDescriptionText?.Trim() ?? string.Empty;
        normalized.ProductionUnit = normalized.ProductionUnit?.Trim() ?? string.Empty;
        normalized.ProductionDateText = string.IsNullOrWhiteSpace(normalized.ProductionDateText)
            ? DateTime.Today.ToString("yyyy-MM-dd")
            : normalized.ProductionDateText.Trim();
        normalized.CoordinateSystemName = string.IsNullOrWhiteSpace(normalized.CoordinateSystemName)
            ? "2000国家大地坐标系"
            : normalized.CoordinateSystemName.Trim();
        normalized.ProjectionTypeName = string.IsNullOrWhiteSpace(normalized.ProjectionTypeName)
            ? "高斯克吕格"
            : normalized.ProjectionTypeName.Trim();
        normalized.UnitName = string.IsNullOrWhiteSpace(normalized.UnitName)
            ? "米"
            : normalized.UnitName.Trim();
        normalized.CoordinatePrefix = string.IsNullOrWhiteSpace(normalized.CoordinatePrefix)
            ? "J"
            : normalized.CoordinatePrefix.Trim();
        normalized.CoordinateDecimals = Math.Max(0, normalized.CoordinateDecimals);
        normalized.AreaDecimals = Math.Max(0, normalized.AreaDecimals);
        normalized.PointNumberDigits = Math.Max(1, normalized.PointNumberDigits);

        return normalized;
    }
}
