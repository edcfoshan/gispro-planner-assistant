namespace GisProAddinWorkbench.AddIn.Guides;

internal static class GuideWindowHost
{
    internal static void Show(GuideImageData data)
    {
        Show(new[] { data });
    }

    internal static void Show(IReadOnlyList<GuideImageData> pages)
    {
        var window = new GuideImageWindow(pages);
        window.Show();
    }
}
