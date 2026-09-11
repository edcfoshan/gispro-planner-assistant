using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GisProAddinWorkbench.AddIn.Infrastructure;
using GisProAddinWorkbench.AddIn.Services;
using Microsoft.Web.WebView2.Core;

namespace GisProAddinWorkbench.AddIn.Controls;

public partial class BaiduStreetViewBrowser : UserControl
{
    public static readonly DependencyProperty HtmlContentProperty =
        DependencyProperty.Register(
            nameof(HtmlContent),
            typeof(string),
            typeof(BaiduStreetViewBrowser),
            new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty NavigateUrlProperty =
        DependencyProperty.Register(
            nameof(NavigateUrl),
            typeof(string),
            typeof(BaiduStreetViewBrowser),
            new PropertyMetadata(string.Empty, OnContentChanged));

    private readonly Task<CoreWebView2Environment> _environmentTask;

    public BaiduStreetViewBrowser()
    {
        InitializeComponent();
        _environmentTask = WebView2ProfileStore.CreateEnvironmentAsync(nameof(BaiduStreetViewBrowser));
        Browser.NavigationStarting += OnNavigationStarting;
        Browser.NavigationCompleted += OnNavigationCompleted;
        Loaded += OnLoaded;
    }

    public string HtmlContent
    {
        get => (string)GetValue(HtmlContentProperty);
        set => SetValue(HtmlContentProperty, value);
    }

    public string NavigateUrl
    {
        get => (string)GetValue(NavigateUrlProperty);
        set => SetValue(NavigateUrlProperty, value);
    }

    private static async void OnContentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs _)
    {
        if (dependencyObject is BaiduStreetViewBrowser control)
        {
            await control.NavigateAsync();
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await NavigateAsync();
    }

    private async Task NavigateAsync()
    {
        if (!IsLoaded)
        {
            return;
        }

        var userDataFolder = WebView2ProfileStore.GetUserDataFolder(nameof(BaiduStreetViewBrowser));
        try
        {
            if (string.IsNullOrWhiteSpace(HtmlContent) && string.IsNullOrWhiteSpace(NavigateUrl))
            {
                ShowPlaceholder("等待地图点选后加载百度页面。");
                if (Browser.CoreWebView2 is not null)
                {
                    Browser.CoreWebView2.Navigate("about:blank");
                }

                return;
            }

            var environment = await _environmentTask;
            await Browser.EnsureCoreWebView2Async(environment);

            if (!string.IsNullOrWhiteSpace(NavigateUrl))
            {
                ShowPlaceholder("正在加载百度地图页面...");
                Browser.CoreWebView2.Navigate("about:blank");
                Browser.CoreWebView2.Navigate(NavigateUrl);
                return;
            }

            ShowPlaceholder("正在初始化百度页面...");
            Browser.CoreWebView2.NavigateToString(HtmlContent);
        }
        catch (Exception exception)
        {
            ShowPlaceholder($"百度页面初始化失败：{exception.Message}");
            DiagnosticsLogger.Write(
                nameof(BaiduStreetViewBrowser),
                $"初始化百度页面浏览器失败。UserDataFolder={userDataFolder}",
                exception);
        }
    }

    private void OnNavigationStarting(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
    {
        ShowPlaceholder(string.IsNullOrWhiteSpace(NavigateUrl)
            ? "正在初始化百度页面..."
            : "正在加载百度地图页面...");
    }

    private void OnNavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            HidePlaceholder();
            return;
        }

        ShowPlaceholder($"百度页面加载失败：{e.WebErrorStatus}");
        DiagnosticsLogger.Write(
            nameof(BaiduStreetViewBrowser),
            $"百度页面加载失败：{e.WebErrorStatus}; Url={NavigateUrl}");
    }

    private void ShowPlaceholder(string message)
    {
        PlaceholderPanel.Visibility = Visibility.Visible;
        PlaceholderText.Text = message;
    }

    private void HidePlaceholder()
    {
        PlaceholderPanel.Visibility = Visibility.Collapsed;
    }
}
