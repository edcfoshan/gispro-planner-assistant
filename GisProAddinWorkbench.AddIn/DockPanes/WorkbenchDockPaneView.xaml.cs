using System.Windows.Controls;
using GisProAddinWorkbench.AddIn.Settings;

namespace GisProAddinWorkbench.AddIn.DockPanes;

public partial class WorkbenchDockPaneView : UserControl
{
    public WorkbenchDockPaneView()
    {
        UiDensityService.Initialize();
        InitializeComponent();
    }
}
