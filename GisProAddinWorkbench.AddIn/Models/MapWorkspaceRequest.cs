namespace GisProAddinWorkbench.AddIn.Models;

internal sealed record MapWorkspaceRequest(
    MapWorkspaceProvider Provider,
    string NavigateUrl,
    string WorkspaceTitle,
    string WorkspaceSummary,
    string ShellStatus);
