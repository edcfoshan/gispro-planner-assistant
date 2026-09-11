using System.Windows;
using System.Windows.Threading;
using GisProAddinWorkbench.AddIn.Infrastructure;

namespace GisProAddinWorkbench.AddIn.Modules.Imagery;

public partial class WfsLayerOptionsWindow : Window
{
    internal WfsLayerOptionsWindowViewModel ViewModel { get; }

    internal WfsLayerOptionsWindow(
        ImageryServicePreset preset,
        WfsLayerOptions initialOptions,
        WfsExtentContext extentContext)
    {
        InitializeComponent();

        ViewModel = new WfsLayerOptionsWindowViewModel(
            preset,
            initialOptions,
            extentContext,
            result =>
            {
                DialogResult = result;
                Close();
            });
        DataContext = ViewModel;
    }

    internal static WfsLayerOptions? ShowDialog(
        ImageryServicePreset preset,
        WfsLayerOptions initialOptions,
        WfsExtentContext extentContext)
    {
        WfsLayerOptionsWindow? window = null;
        DispatcherUnhandledExceptionEventHandler? dispatcherHandler = null;

        try
        {
            window = new WfsLayerOptionsWindow(preset, initialOptions, extentContext);

            if (Application.Current is not null)
            {
                dispatcherHandler = (_, args) =>
                {
                    DiagnosticsLogger.Write(
                        nameof(WfsLayerOptionsWindow),
                        "WFS 参数窗口发生未处理异常。",
                        args.Exception);

                    args.Handled = true;

                    try
                    {
                        if (window?.IsVisible == true)
                        {
                            window.Close();
                        }
                    }
                    catch
                    {
                        // Keep the guard best-effort.
                    }
                };

                Application.Current.DispatcherUnhandledException += dispatcherHandler;
            }

            return window.ShowDialog() == true ? window.ViewModel.ResultOptions : null;
        }
        catch (Exception exception)
        {
            DiagnosticsLogger.Write(nameof(WfsLayerOptionsWindow), "打开 WFS 参数窗口失败。", exception);
            return null;
        }
        finally
        {
            if (dispatcherHandler is not null && Application.Current is not null)
            {
                Application.Current.DispatcherUnhandledException -= dispatcherHandler;
            }
        }
    }
}
