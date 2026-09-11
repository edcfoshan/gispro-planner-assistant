using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GisProAddinWorkbench.AddIn.Guides;

internal static class GuideImageRenderer
{
    internal static BitmapSource Render(GuideImageData data)
    {
        const int width = 1100;
        const int height = 740;
        const double dpi = 96d;
        const double padding = 48d;

        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();

        var background = new LinearGradientBrush(
            Color.FromRgb(245, 248, 252),
            Color.FromRgb(233, 241, 251),
            90);
        dc.DrawRectangle(background, null, new Rect(0, 0, width, height));

        var titleTypeface = new Typeface(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var bodyTypeface = new Typeface(new FontFamily("Microsoft YaHei"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        DrawText(dc, data.Title, titleTypeface, 38, Brushes.DarkSlateBlue, new Point(padding, padding), width - padding * 2);
        DrawText(dc, data.Subtitle, bodyTypeface, 22, Brushes.SlateGray, new Point(padding, 108), width - padding * 2);

        var y = 190d;
        for (var i = 0; i < data.Steps.Count; i++)
        {
            var stepBackground = i % 2 == 0
                ? new SolidColorBrush(Color.FromRgb(255, 255, 255))
                : new SolidColorBrush(Color.FromRgb(248, 252, 255));
            dc.DrawRoundedRectangle(stepBackground, new Pen(new SolidColorBrush(Color.FromRgb(210, 223, 239)), 1), new Rect(padding, y, width - padding * 2, 130), 10, 10);

            DrawText(dc, $"步骤 {i + 1}", titleTypeface, 24, Brushes.Teal, new Point(padding + 20, y + 16), width - padding * 2 - 40);
            DrawText(dc, data.Steps[i], bodyTypeface, 21, Brushes.Black, new Point(padding + 20, y + 54), width - padding * 2 - 40);
            y += 146;
        }

        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(223, 245, 238)),
            new Pen(new SolidColorBrush(Color.FromRgb(155, 214, 196)), 1),
            new Rect(padding, height - 100, width - padding * 2, 56),
            8,
            8);

        DrawText(dc, data.Footer, bodyTypeface, 20, Brushes.DarkGreen, new Point(padding + 16, height - 84), width - padding * 2 - 32);

        var bitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawText(
        DrawingContext dc,
        string text,
        Typeface typeface,
        double size,
        Brush brush,
        Point point,
        double maxWidth)
    {
        var formattedText = new FormattedText(
            text,
            CultureInfo.GetCultureInfo("zh-CN"),
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush,
            1.0)
        {
            MaxTextWidth = maxWidth
        };

        dc.DrawText(formattedText, point);
    }
}
