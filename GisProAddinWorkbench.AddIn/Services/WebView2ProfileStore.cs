using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace GisProAddinWorkbench.AddIn.Services;

internal static class WebView2ProfileStore
{
    private const string RootFolderName = "GuotuGiserToolbox";
    private const string ProfilesFolderName = "WebView2";

    internal static string GetUserDataFolder(string profileName)
    {
        var sanitizedProfileName = SanitizeProfileName(profileName);
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            RootFolderName,
            ProfilesFolderName,
            sanitizedProfileName);
    }

    internal static Task<CoreWebView2Environment> CreateEnvironmentAsync(string profileName)
    {
        var userDataFolder = GetUserDataFolder(profileName);
        Directory.CreateDirectory(userDataFolder);

        return CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder,
            options: null);
    }

    private static string SanitizeProfileName(string profileName)
    {
        var value = string.IsNullOrWhiteSpace(profileName)
            ? "Default"
            : profileName.Trim();

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '_');
        }

        return value;
    }
}
