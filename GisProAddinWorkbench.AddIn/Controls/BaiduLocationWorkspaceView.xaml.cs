using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace GisProAddinWorkbench.AddIn.Controls;

public partial class BaiduLocationWorkspaceView : UserControl
{
    public static readonly DependencyProperty SummaryTextProperty =
        DependencyProperty.Register(nameof(SummaryText), typeof(string), typeof(BaiduLocationWorkspaceView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty WgsTextProperty =
        DependencyProperty.Register(nameof(WgsText), typeof(string), typeof(BaiduLocationWorkspaceView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BdTextProperty =
        DependencyProperty.Register(nameof(BdText), typeof(string), typeof(BaiduLocationWorkspaceView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BaiduMapUrlProperty =
        DependencyProperty.Register(nameof(BaiduMapUrl), typeof(string), typeof(BaiduLocationWorkspaceView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BaiduStreetViewUrlProperty =
        DependencyProperty.Register(nameof(BaiduStreetViewUrl), typeof(string), typeof(BaiduLocationWorkspaceView), new PropertyMetadata(string.Empty));

    public BaiduLocationWorkspaceView()
    {
        InitializeComponent();
        DataContext = this;
    }

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    public string WgsText
    {
        get => (string)GetValue(WgsTextProperty);
        set => SetValue(WgsTextProperty, value);
    }

    public string BdText
    {
        get => (string)GetValue(BdTextProperty);
        set => SetValue(BdTextProperty, value);
    }

    public string BaiduMapUrl
    {
        get => (string)GetValue(BaiduMapUrlProperty);
        set => SetValue(BaiduMapUrlProperty, value);
    }

    public string BaiduStreetViewUrl
    {
        get => (string)GetValue(BaiduStreetViewUrlProperty);
        set => SetValue(BaiduStreetViewUrlProperty, value);
    }

    private void OnOpenBaiduMapClick(object sender, RoutedEventArgs e)
    {
        OpenUrl(BaiduMapUrl);
    }

    private void OnOpenStreetViewClick(object sender, RoutedEventArgs e)
    {
        OpenUrl(BaiduStreetViewUrl);
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
