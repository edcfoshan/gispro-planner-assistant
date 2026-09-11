using System.Windows;
using GisProAddinWorkbench.AddIn.Settings;

namespace GisProAddinWorkbench.AddIn.FaceToTxt;

internal sealed class FaceToTxtWorkbenchWindow : Window
{
    private static FaceToTxtWorkbenchWindow? _instance;

    private FaceToTxtWorkbenchWindow()
    {
        UiDensityService.Initialize();
        Title = "面状要素转 TXT";
        SetResourceReference(WidthProperty, "CompactFaceToTxtWindowWidth");
        SetResourceReference(HeightProperty, "CompactFaceToTxtWindowHeight");
        SetResourceReference(MinWidthProperty, "CompactFaceToTxtMinWidth");
        SetResourceReference(MinHeightProperty, "CompactFaceToTxtMinHeight");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = new FaceToTxtWorkbenchView
        {
            DataContext = FaceToTxtWorkbenchViewModel.Shared
        };

        Closing += (_, _) =>
        {
            FaceToTxtWorkbenchViewModel.Shared.FlushChanges();
        };
    }

    internal static void ShowWindow()
    {
        if (_instance is null || !_instance.IsLoaded)
        {
            _instance = new FaceToTxtWorkbenchWindow();
            _instance.Closed += (_, _) => _instance = null;
            _instance.Show();
            _ = FaceToTxtWorkbenchViewModel.Shared.EnsureInitializedAsync();
            return;
        }

        if (_instance.WindowState == WindowState.Minimized)
        {
            _instance.WindowState = WindowState.Normal;
        }

        _instance.Activate();
        _ = FaceToTxtWorkbenchViewModel.Shared.EnsureInitializedAsync();
    }
}
