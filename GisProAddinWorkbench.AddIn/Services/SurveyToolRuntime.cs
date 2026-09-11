using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GisProAddinWorkbench.AddIn.Services;

internal static class SurveyToolRuntime
{
    private const string VendorFolderName = "Vendored";
    private const string SourceFolderName = "codex_arcgis-geocode-toolbox";
    private const string ToolboxFileName = "百度高德Geocode.pyt";
    private const string ConfigFolderName = "config";
    private const string LegacySourceRoot = @"C:\Users\Administrator\Documents\codex_arcgis-geocode-toolbox";

    internal static string GetAddInRootDirectory()
    {
        var assemblyLocation = typeof(SurveyToolRuntime).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
        {
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                return assemblyDirectory;
            }
        }

        return AppContext.BaseDirectory;
    }

    internal static string GetBundledToolboxRelativeFilePath()
    {
        return Path.Combine(VendorFolderName, SourceFolderName, ToolboxFileName);
    }

    internal static string GetBundledToolboxRoot()
    {
        return Path.Combine(GetAddInRootDirectory(), VendorFolderName, SourceFolderName);
    }

    internal static string GetRuntimeToolboxRoot()
    {
        return Path.Combine(ResearchSettingsStore.GetSettingsDirectory(), SourceFolderName);
    }

    internal static string GetBundledToolboxFilePath()
    {
        return Path.Combine(GetBundledToolboxRoot(), ToolboxFileName);
    }

    internal static string GetRuntimeToolboxFilePath()
    {
        return Path.Combine(GetRuntimeToolboxRoot(), ToolboxFileName);
    }

    internal static string NormalizeConfiguredToolPath(string? toolPath)
    {
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            return string.Empty;
        }

        var normalized = toolPath.Trim().Replace('/', '\\');
        if (!Path.IsPathRooted(normalized))
        {
            normalized = Path.GetFullPath(Path.Combine(GetAddInRootDirectory(), normalized));
        }

        return ReplaceManagedRoot(normalized);
    }

    internal static string ToPersistedConfiguredToolPath(string? toolPath)
    {
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            return string.Empty;
        }

        var normalized = toolPath.Trim().Replace('/', '\\');
        foreach (var managedRoot in GetManagedSourceRoots())
        {
            if (!normalized.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = normalized[managedRoot.Length..].TrimStart('\\');
            return string.IsNullOrWhiteSpace(suffix)
                ? GetBundledToolboxRelativeFilePath()
                : Path.Combine(VendorFolderName, SourceFolderName, suffix);
        }

        return normalized;
    }

    internal static async Task EnsureWorkspaceAsync(
        ResearchSettings? settings = null,
        bool requireKeys = false)
    {
        settings ??= await ResearchSettingsStore.LoadAsync();

        var bundledRoot = GetBundledToolboxRoot();
        if (!Directory.Exists(bundledRoot))
        {
            throw new DirectoryNotFoundException($"未找到内置前期调研工具箱目录：{bundledRoot}");
        }

        var runtimeRoot = GetRuntimeToolboxRoot();
        Directory.CreateDirectory(runtimeRoot);

        CopyDirectory(bundledRoot, runtimeRoot);
        SyncKeyFiles(runtimeRoot, settings, requireKeys);
    }

    internal static (string[] BaiduAk, string[] AmapKey) LoadSeedKeys()
    {
        var bundled = LoadKeyFilesFromRoot(GetBundledToolboxRoot());
        var legacy = LoadKeyFilesFromRoot(LegacySourceRoot);

        return (
            ChooseSeed(bundled.BaiduAk, legacy.BaiduAk),
            ChooseSeed(bundled.AmapKey, legacy.AmapKey));
    }

    private static string ReplaceManagedRoot(string toolPath)
    {
        var runtimeToolbox = GetRuntimeToolboxFilePath();
        foreach (var managedSource in GetManagedSourceToolboxFilePaths())
        {
            if (toolPath.StartsWith(managedSource, StringComparison.OrdinalIgnoreCase))
            {
                return runtimeToolbox + toolPath[managedSource.Length..];
            }
        }

        return toolPath;
    }

    private static IEnumerable<string> GetManagedSourceRoots()
    {
        yield return GetRuntimeToolboxRoot();
        yield return GetBundledToolboxRoot();
        yield return LegacySourceRoot;
    }

    private static IEnumerable<string> GetManagedSourceToolboxFilePaths()
    {
        yield return GetBundledToolboxFilePath();
        yield return GetLegacyToolboxFilePath();
    }

    private static string GetLegacyToolboxFilePath()
    {
        return Path.Combine(LegacySourceRoot, ToolboxFileName);
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativeDirectory = Path.GetRelativePath(sourceRoot, directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relativeDirectory));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativeFile = Path.GetRelativePath(sourceRoot, file);
            if (ShouldSkipCopiedFile(relativeFile))
            {
                continue;
            }

            var targetFile = Path.Combine(destinationRoot, relativeFile);
            var targetDirectory = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(targetFile))
            {
                var sourceInfo = new FileInfo(file);
                var targetInfo = new FileInfo(targetFile);
                if (targetInfo.Length == sourceInfo.Length &&
                    targetInfo.LastWriteTimeUtc >= sourceInfo.LastWriteTimeUtc)
                {
                    continue;
                }
            }

            File.Copy(file, targetFile, true);
            File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(file));
        }
    }

    private static bool ShouldSkipCopiedFile(string relativePath)
    {
        var normalized = relativePath.Replace('/', '\\');
        if (normalized.EndsWith(".pyc", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.Contains(@"\__pycache__\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("__pycache__\\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static void SyncKeyFiles(
        string runtimeRoot,
        ResearchSettings settings,
        bool requireKeys)
    {
        var (baiduKeys, amapKeys) = ResolveKeys(settings);

        var baiduPath = Path.Combine(runtimeRoot, ConfigFolderName, "baidu_keys.txt");
        var amapPath = Path.Combine(runtimeRoot, ConfigFolderName, "amap_keys.txt");
        WriteKeyFile(baiduPath, baiduKeys);
        WriteKeyFile(amapPath, amapKeys);

        if (requireKeys && (baiduKeys.Count == 0 || amapKeys.Count == 0))
        {
            throw new InvalidOperationException(
                "前期调研的百度/高德 Key 尚未配置，请先在系统设置中填写后再打开工具箱。");
        }
    }

    private static (List<string> BaiduAk, List<string> AmapKey) ResolveKeys(ResearchSettings settings)
    {
        var seedKeys = LoadSeedKeys();

        var baiduKeys = MergeKeyLists(
            NormalizeKeyList(settings.BaiduAkSupplemental),
            NormalizeKeyList(settings.BaiduAkBase),
            NormalizeKeyList(settings.LegacyBaiduAk));
        if (baiduKeys.Count == 0)
        {
            baiduKeys = NormalizeKeyList(seedKeys.BaiduAk);
        }

        var amapKeys = MergeKeyLists(
            NormalizeKeyList(settings.AmapKeySupplemental),
            NormalizeKeyList(settings.AmapKeyBase),
            NormalizeKeyList(settings.LegacyAmapKey));
        if (amapKeys.Count == 0)
        {
            amapKeys = NormalizeKeyList(seedKeys.AmapKey);
        }

        return (baiduKeys, amapKeys);
    }

    private static List<string> NormalizeKeyList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> MergeKeyLists(
        IEnumerable<string>? baseKeys,
        IEnumerable<string>? supplementalKeys,
        IEnumerable<string>? legacyKeys = null)
    {
        var merged = NormalizeKeyList(baseKeys);
        foreach (var value in NormalizeKeyList(supplementalKeys))
        {
            if (merged.Any(existing => existing.Equals(value, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            merged.Add(value);
        }

        foreach (var value in NormalizeKeyList(legacyKeys))
        {
            if (merged.Any(existing => existing.Equals(value, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            merged.Add(value);
        }

        return merged;
    }

    private static void WriteKeyFile(string path, IReadOnlyCollection<string> keys)
    {
        if (keys.Count == 0)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(path, keys);
    }

    private static (string[] BaiduAk, string[] AmapKey) LoadKeyFilesFromRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return ([], []);
        }

        return (
            ReadKeyLines(Path.Combine(root, ConfigFolderName, "baidu_keys.txt")),
            ReadKeyLines(Path.Combine(root, ConfigFolderName, "amap_keys.txt")));
    }

    private static string[] ReadKeyLines(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string[] ChooseSeed(IReadOnlyList<string> primary, IReadOnlyList<string> fallback)
    {
        return primary.Count > 0 ? primary.ToArray() : fallback.ToArray();
    }
}
