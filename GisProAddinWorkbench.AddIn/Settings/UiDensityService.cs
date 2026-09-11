using System.IO;
using System.Text.Json;
using System.Windows;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Services;

namespace GisProAddinWorkbench.AddIn.Settings;

internal static class UiDensityService
{
    private static readonly Uri CompactDensityUri =
        new("/GisProAddinWorkbench.AddIn;component/Styles/CompactDensity.xaml", UriKind.Relative);

    private const int DefaultDensityPercent = 100;
    private const int DefaultLeftPaneWidthPercent = 100;
    private const int DefaultRightPaneMinWidthPercent = 100;

    private static readonly object SyncLock = new();
    private static ResourceDictionary? _compactDictionary;
    private static bool _initialized;
    private static int _currentDensityPercent = DefaultDensityPercent;
    private static int _currentLeftPaneWidthPercent = DefaultLeftPaneWidthPercent;
    private static int _currentRightPaneMinWidthPercent = DefaultRightPaneMinWidthPercent;

    internal static int NormalizePercent(int value)
    {
        var rounded = (int)Math.Round(value / 5d) * 5;
        return Math.Clamp(rounded, 80, 120);
    }

    internal static int NormalizePanePercent(int value)
    {
        var rounded = (int)Math.Round(value / 5d) * 5;
        return Math.Clamp(rounded, 50, 200);
    }

    internal static void Initialize()
    {
        lock (SyncLock)
        {
            if (_initialized)
            {
                return;
            }

            if (EnsureCompactDictionary() is null)
            {
                return;
            }

            var runtime = TryReadSavedRuntimeSettings();
            _currentDensityPercent = runtime.DensityPercent;
            _currentLeftPaneWidthPercent = runtime.LeftPaneWidthPercent;
            _currentRightPaneMinWidthPercent = runtime.RightPaneMinWidthPercent;

            ApplyCurrentSettings();
            _initialized = true;
        }
    }

    internal static void ApplyDensityPercent(int densityPercent)
    {
        ApplyRuntimeSettings(
            NormalizePercent(densityPercent),
            _currentLeftPaneWidthPercent,
            _currentRightPaneMinWidthPercent);
    }

    internal static void ApplyPaneWidthPercents(int leftPaneWidthPercent, int rightPaneMinWidthPercent)
    {
        ApplyRuntimeSettings(
            _currentDensityPercent,
            NormalizePanePercent(leftPaneWidthPercent),
            NormalizePanePercent(rightPaneMinWidthPercent));
    }

    internal static void ApplyRuntimeSettings(
        int densityPercent,
        int leftPaneWidthPercent,
        int rightPaneMinWidthPercent)
    {
        try
        {
            if (EnsureCompactDictionary() is null)
            {
                return;
            }

            _currentDensityPercent = NormalizePercent(densityPercent);
            _currentLeftPaneWidthPercent = NormalizePanePercent(leftPaneWidthPercent);
            _currentRightPaneMinWidthPercent = NormalizePanePercent(rightPaneMinWidthPercent);

            ApplyCurrentSettings();
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(UiDensityService), "应用界面密度失败。", exception);
        }
    }

    private static void ApplyCurrentSettings()
    {
        var dictionary = EnsureCompactDictionary();
        if (dictionary is null)
        {
            return;
        }

        var densityScale = _currentDensityPercent / 100d;
        var leftScale = _currentLeftPaneWidthPercent / 100d;
        var rightScale = _currentRightPaneMinWidthPercent / 100d;

        dictionary["CompactFontTitle"] = Scale(18d, densityScale, 14d, 24d);
        dictionary["CompactFontSection"] = Scale(14d, densityScale, 11d, 20d);
        dictionary["CompactFontBody"] = Scale(11d, densityScale, 9d, 16d);
        dictionary["CompactFontCaption"] = Scale(10d, densityScale, 8d, 14d);
        dictionary["CompactInputHeight"] = Scale(30d, densityScale, 24d, 40d);
        dictionary["CompactCornerRadius"] = Scale(12d, densityScale, 8d, 18d);

        dictionary["CompactPaddingCard"] = ScaleThickness(new Thickness(12d, 10d, 12d, 10d), densityScale);
        dictionary["CompactPaddingButton"] = ScaleThickness(new Thickness(10d, 6d, 10d, 6d), densityScale);
        dictionary["CompactPaddingInput"] = ScaleThickness(new Thickness(6d, 3d, 6d, 3d), densityScale);
        dictionary["CompactMarginStack"] = ScaleThickness(new Thickness(0d, 10d, 0d, 0d), densityScale);
        dictionary["CompactMarginSection"] = ScaleThickness(new Thickness(0d, 12d, 0d, 0d), densityScale);
        dictionary["CompactMarginSmall"] = ScaleThickness(new Thickness(0d, 6d, 0d, 0d), densityScale);

        var leftPaneWidth = Scale(230d, densityScale * leftScale, 130d, 620d);
        var paneGapWidth = Scale(12d, densityScale, 8d, 18d);
        var rightPaneMinWidth = Scale(520d, densityScale * rightScale, 240d, 1400d);
        var faceToTxtScale = densityScale * Math.Max(0.85d, (leftScale + rightScale) / 2d);
        var faceToTxtWindowWidth = Scale(1280d, faceToTxtScale, 960d, 1800d);
        var faceToTxtWindowHeight = Scale(820d, densityScale, 640d, 1200d);
        var faceToTxtMinWidth = Scale(980d, faceToTxtScale, 820d, 1500d);
        var faceToTxtMinHeight = Scale(640d, densityScale, 560d, 960d);
        var faceToTxtLeftPaneWidth = Scale(480d, densityScale * leftScale, 340d, 760d);
        var faceToTxtPreviewHeight = Scale(280d, densityScale, 220d, 420d);

        dictionary["CompactLeftPaneWidth"] = new GridLength(leftPaneWidth, GridUnitType.Pixel);
        dictionary["CompactPaneGapWidth"] = new GridLength(paneGapWidth, GridUnitType.Pixel);
        dictionary["CompactRightPaneMinWidth"] = rightPaneMinWidth;
        dictionary["CompactFaceToTxtWindowWidth"] = faceToTxtWindowWidth;
        dictionary["CompactFaceToTxtWindowHeight"] = faceToTxtWindowHeight;
        dictionary["CompactFaceToTxtMinWidth"] = faceToTxtMinWidth;
        dictionary["CompactFaceToTxtMinHeight"] = faceToTxtMinHeight;
        dictionary["CompactFaceToTxtLeftPaneWidth"] = new GridLength(faceToTxtLeftPaneWidth, GridUnitType.Pixel);
        dictionary["CompactFaceToTxtPreviewRowHeight"] = new GridLength(faceToTxtPreviewHeight, GridUnitType.Pixel);
    }

    private static RuntimeSettings TryReadSavedRuntimeSettings()
    {
        try
        {
            ResearchSettingsStore.EnsureTemplateExists();
            var path = ResearchSettingsStore.GetSettingsPath();
            if (!File.Exists(path))
            {
                return RuntimeSettings.Default;
            }

            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var density = NormalizePercent(ReadIntProperty(root, "UiDensityPercent", DefaultDensityPercent));
            var leftPane = NormalizePanePercent(ReadIntProperty(root, "UiLeftPaneWidthPercent", DefaultLeftPaneWidthPercent));
            var rightPane = NormalizePanePercent(ReadIntProperty(root, "UiRightPaneMinWidthPercent", DefaultRightPaneMinWidthPercent));
            return new RuntimeSettings(density, leftPane, rightPane);
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(UiDensityService), "读取界面布局配置失败，已回退到默认值。", exception);
            return RuntimeSettings.Default;
        }
    }

    private static int ReadIntProperty(JsonElement root, string propertyName, int fallback)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number &&
                property.Value.TryGetInt32(out var numberValue))
            {
                return numberValue;
            }

            if (property.Value.ValueKind == JsonValueKind.String &&
                int.TryParse(property.Value.GetString(), out var stringValue))
            {
                return stringValue;
            }

            return fallback;
        }

        return fallback;
    }

    private static ResourceDictionary? EnsureCompactDictionary()
    {
        try
        {
            var app = Application.Current;
            if (app is null)
            {
                return null;
            }

            if (_compactDictionary is not null)
            {
                return _compactDictionary;
            }

            foreach (var dictionary in app.Resources.MergedDictionaries)
            {
                if (dictionary.Source is null)
                {
                    continue;
                }

                var source = dictionary.Source.OriginalString ?? string.Empty;
                if (source.Contains("Styles/CompactDensity.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    _compactDictionary = dictionary;
                    return _compactDictionary;
                }
            }

            var compactDictionary = new ResourceDictionary
            {
                Source = CompactDensityUri
            };

            app.Resources.MergedDictionaries.Add(compactDictionary);
            _compactDictionary = compactDictionary;
            return _compactDictionary;
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(UiDensityService), "加载紧凑密度资源失败。", exception);
            return null;
        }
    }

    private static double Scale(double baseValue, double scale, double min, double max)
    {
        var value = Math.Round(baseValue * scale, 2);
        return Math.Clamp(value, min, max);
    }

    private static Thickness ScaleThickness(Thickness baseThickness, double scale)
    {
        return new Thickness(
            Math.Round(baseThickness.Left * scale, 2),
            Math.Round(baseThickness.Top * scale, 2),
            Math.Round(baseThickness.Right * scale, 2),
            Math.Round(baseThickness.Bottom * scale, 2));
    }

    private sealed record RuntimeSettings(
        int DensityPercent,
        int LeftPaneWidthPercent,
        int RightPaneMinWidthPercent)
    {
        internal static RuntimeSettings Default =>
            new(DefaultDensityPercent, DefaultLeftPaneWidthPercent, DefaultRightPaneMinWidthPercent);
    }
}
