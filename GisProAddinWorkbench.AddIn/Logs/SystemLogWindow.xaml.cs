using System.Windows;

namespace GisProAddinWorkbench.AddIn.Logs;

public partial class SystemLogWindow : Window
{
    public SystemLogWindow()
    {
        InitializeComponent();
        DataContext = new SystemLogWindowViewModel();
    }
}
