using System.IO;
using System.Windows;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

internal sealed class ImageryThumbnailService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly string _cacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GuotuGiserToolbox",
        "imagery-thumbnails");

    internal async Task<ImageSource> GetThumbnailAsync(
        ImageryServicePreset preset,
        ImageryModuleSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var credential = ImageryServiceCatalog.GetActiveCredential(settings, preset.CredentialKind);
            if (preset.CredentialKind != ImageryCredentialKind.None && string.IsNullOrWhiteSpace(credential))
            {
                return CreateFallbackThumbnail(preset);
            }

            var cachePath = BuildCachePath(preset, credential ?? string.Empty);
            if (File.Exists(cachePath))
            {
                return LoadBitmapImage(cachePath);
            }

            var previewUrl = ImageryServiceCatalog.BuildThumbnailUrl(preset, credential ?? string.Empty);
            if (string.IsNullOrWhiteSpace(previewUrl))
            {
                return CreateFallbackThumbnail(preset);
            }

            Directory.CreateDirectory(_cacheDirectory);
            var bytes = await HttpClient.GetByteArrayAsync(previewUrl, cancellationToken);
            await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
            return LoadBitmapImage(cachePath);
        }
        catch
        {
            return CreateFallbackThumbnail(preset);
        }
    }

    private string BuildCachePath(ImageryServicePreset preset, string credential)
    {
        var seed = string.IsNullOrWhiteSpace(credential) ? preset.Id : credential;
        var hash = BuildCredentialHash(seed);
        return Path.Combine(_cacheDirectory, $"{preset.Id}-{hash}.png");
    }

    private static string BuildCredentialHash(string credential)
    {
        var normalized = credential.Trim();
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }

    private static ImageSource LoadBitmapImage(string filePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static ImageSource CreateFallbackThumbnail(ImageryServicePreset preset)
    {
        var group = new DrawingGroup();

        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            null,
            new RectangleGeometry(new Rect(0, 0, 256, 144), 14, 14)));

        group.Children.Add(new GeometryDrawing(
            new LinearGradientBrush(
                Color.FromArgb(40, 255, 255, 255),
                Color.FromArgb(20, 255, 255, 255),
                new Point(0, 0),
                new Point(1, 1)),
            null,
            new RectangleGeometry(new Rect(0, 0, 256, 144), 14, 14)));

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(55, 100, 116, 139)), 1);
        for (var i = 1; i < 5; i++)
        {
            var x = 256d * i / 5d;
            group.Children.Add(new GeometryDrawing(
                null,
                gridPen,
                new LineGeometry(new Point(x, 8), new Point(x, 136))));
        }

        for (var i = 1; i < 3; i++)
        {
            var y = 144d * i / 3d;
            group.Children.Add(new GeometryDrawing(
                null,
                gridPen,
                new LineGeometry(new Point(8, y), new Point(248, y))));
        }

        var routePen = new Pen(preset.AccentBrush, 4)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        group.Children.Add(new GeometryDrawing(
            null,
            routePen,
            new PathGeometry(new[]
            {
                new PathFigure(
                    new Point(18, 110),
                    [
                        new BezierSegment(new Point(58, 70), new Point(92, 92), new Point(128, 54), true),
                        new BezierSegment(new Point(160, 24), new Point(192, 72), new Point(228, 34), true)
                    ],
                    false)
            })));

        group.Children.Add(new GeometryDrawing(
            preset.AccentBrush,
            null,
            new EllipseGeometry(new Point(192, 46), 8, 8)));

        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
            null,
            new EllipseGeometry(new Point(192, 46), 17, 17)));

        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}

