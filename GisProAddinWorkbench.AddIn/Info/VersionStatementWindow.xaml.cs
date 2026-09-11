using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Info;

public partial class VersionStatementWindow : Window
{
    public VersionStatementWindow(
        string assemblyVersion,
        string informationalVersion,
        string updatedAt,
        string diagnosticsPath)
    {
        InitializeComponent();
        DataContext = this;

        PluginName = "插件：国土giser工具箱";
        PublicVersionText = $"对外展示版本：v{ExtractPublicVersion(informationalVersion, assemblyVersion)}";
        AssemblyVersionText = $"程序集版本：{assemblyVersion}";
        InformationalVersionText = $"版本标识：{informationalVersion}";
        UpdatedAtText = $"更新时间：{updatedAt}";
        SoftwareIntroText = "软件介绍：国土giser工具箱面向自然资源与国土空间从业者，围绕数据获取、在线瓦片服务、批量处理、结果落图等高频任务提供统一作业入口，减少重复点击与参数切换，帮助在 ArcGIS Pro 中更高效地完成分析处理与成果生产。";
        AuthorText = "作者：极思G工作室";
        QqGroupQrText = "扫码加入交流群";
        WechatQrText = "极思G工作室\n公众号二维码";
        QqGroupQrImage = BuildQrImageSource("qq-group.jpg");
        WechatQrImage = BuildQrImageSource("wechat-official.jpg");
        InspirationText = "借鉴创意与开源项目：\ncctool\nicogis_ArcGISPro_AddIn_StreetView";
        DiagnosticsPathText = $"日志路径：{diagnosticsPath}";
    }

    public string PluginName { get; }

    public string PublicVersionText { get; }

    public string AssemblyVersionText { get; }

    public string InformationalVersionText { get; }

    public string UpdatedAtText { get; }

    public string SoftwareIntroText { get; }

    public string AuthorText { get; }

    public string QqGroupQrText { get; }

    public string WechatQrText { get; }

    public ImageSource? QqGroupQrImage { get; }

    public ImageSource? WechatQrImage { get; }

    public string InspirationText { get; }

    public string DiagnosticsPathText { get; }

    private static string ExtractPublicVersion(string informationalVersion, string assemblyVersion)
    {
        var source = string.IsNullOrWhiteSpace(informationalVersion)
            ? assemblyVersion
            : informationalVersion;

        var marker = source.IndexOf('+');
        if (marker >= 0)
        {
            source = source[..marker];
        }

        return string.IsNullOrWhiteSpace(source) ? "未知" : source.Trim();
    }

    private static ImageSource? BuildQrImageSource(string fileName)
    {
        var embeddedPath = $"pack://application:,,,/GisProAddinWorkbench.AddIn;component/Images/QRCodes/{fileName}";
        var embeddedImage = TryLoadImage(embeddedPath);
        if (embeddedImage is not null)
        {
            return embeddedImage;
        }

        foreach (var root in GetCandidateRoots())
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var filePath = Path.Combine(root, "Images", "QRCodes", fileName);
            if (!File.Exists(filePath))
            {
                continue;
            }

            var image = TryLoadImage(filePath);
            if (image is not null)
            {
                return image;
            }
        }

        DiagnosticsLogger.Write(
            nameof(VersionStatementWindow),
            $"二维码图片加载失败：{fileName}");

        return null;
    }

    private static IEnumerable<string> GetCandidateRoots()
    {
        var assemblyLocation = typeof(VersionStatementWindow).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
        {
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                yield return assemblyDirectory;
            }
        }

        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
        {
            yield return AppContext.BaseDirectory;
        }
    }

    private static ImageSource? TryLoadImage(string uriOrPath)
    {
        try
        {
            var sourceUri = Uri.TryCreate(uriOrPath, UriKind.Absolute, out var absoluteUri)
                ? absoluteUri
                : new Uri(uriOrPath, UriKind.RelativeOrAbsolute);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = sourceUri;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
