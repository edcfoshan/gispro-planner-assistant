using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Buttons;

public class CheckForUpdatesButton : Button
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/edcfoshan/gispro-planner-assistant/releases?per_page=20";
    private const string ReleasesPage =
        "https://github.com/edcfoshan/gispro-planner-assistant/releases";
    private static readonly HttpClient Http = CreateHttpClient();

    protected override async void OnClick()
    {
        DiagnosticsLogger.Write(nameof(CheckForUpdatesButton), "检查更新按钮点击。");
        try
        {
            var currentText = GetCurrentVersion();
            using var response = await Http.GetAsync(LatestReleaseApi);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var release = json.RootElement.EnumerateArray().FirstOrDefault();
            if (release.ValueKind != JsonValueKind.Object)
            {
                MessageBox.Show("Releases 页面尚无公开版本，请稍后再试。", "检查更新");
                return;
            }
            var latestText = release.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? string.Empty;
            var releaseUrl = release.TryGetProperty("html_url", out var url)
                ? url.GetString() ?? ReleasesPage
                : ReleasesPage;

            if (!SemanticVersionComparer.TryParse(currentText, out var current) ||
                !SemanticVersionComparer.TryParse(latestText, out var latest))
            {
                MessageBox.Show(
                    $"当前版本：{currentText}{Environment.NewLine}最新版本：{latestText}{Environment.NewLine}请到 Release 页面查看。",
                    "检查更新");
                OpenBrowser(ReleasesPage);
                return;
            }

            if (SemanticVersionComparer.Compare(latest, current) <= 0)
            {
                MessageBox.Show(
                    $"当前已是最新 Demo 版本：{currentText}",
                    "检查更新");
                return;
            }

            MessageBox.Show(
                $"发现新版本：{latestText}{Environment.NewLine}当前版本：{currentText}{Environment.NewLine}{Environment.NewLine}" +
                "将打开 Release 页面。下载新的 .esriAddinX 后双击安装，并重启 ArcGIS Pro 完成升级。",
                "发现更新");
            OpenBrowser(releaseUrl);
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(CheckForUpdatesButton), "检查更新失败。", exception);
            MessageBox.Show(
                $"检查更新失败：{exception.Message}{Environment.NewLine}{Environment.NewLine}" +
                $"可以手动打开：{ReleasesPage}",
                "检查更新");
        }
    }

    private static string GetCurrentVersion()
    {
        var assembly = typeof(CheckForUpdatesButton).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("JisigG-Planner-Workbench");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
