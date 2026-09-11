using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GisProAddinWorkbench.AddIn.Guides;

public partial class GuideImageWindow : Window
{
    private readonly IReadOnlyList<GuideImageData> _pages;
    private int _currentIndex;

    public GuideImageWindow(GuideImageData data)
        : this(new[] { data })
    {
    }

    public GuideImageWindow(IReadOnlyList<GuideImageData> pages)
    {
        InitializeComponent();

        _pages = pages.Count > 0
            ? pages
            : new[]
            {
                new GuideImageData(
                    "步骤引导",
                    "当前没有可显示的引导页。",
                    ["请稍后重试。"],
                    "引导页为空。")
            };

        RenderCurrentPage();
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        if (_currentIndex <= 0)
        {
            return;
        }

        _currentIndex--;
        RenderCurrentPage();
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_currentIndex >= _pages.Count - 1)
        {
            return;
        }

        _currentIndex++;
        RenderCurrentPage();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RenderCurrentPage()
    {
        var page = _pages[_currentIndex];
        Title = $"{page.Title} - 步骤引导";
        GuideImage.Source = LoadImage(page);
        PageIndicatorText.Text = $"第 {_currentIndex + 1} / {_pages.Count} 页 · {page.Title}";
        PreviousButton.IsEnabled = _currentIndex > 0;
        NextButton.IsEnabled = _currentIndex < _pages.Count - 1;
    }

    private static BitmapSource LoadImage(GuideImageData data)
    {
        if (!string.IsNullOrWhiteSpace(data.StaticImagePath) && File.Exists(data.StaticImagePath))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(data.StaticImagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        return GuideImageRenderer.Render(data);
    }
}
