using Microsoft.Xrm.Sdk;

namespace XrmToolBox.Extensibility.Interfaces;

public interface IXrmToolBoxPluginControl
{
    event EventHandler? OnCloseTool;
    event EventHandler? OnRequestConnection;

    IOrganizationService? Service { get; }

    string DisplayName { get; }

    void ClosingPlugin(PluginCloseInfo info);
    void UpdateConnection(IOrganizationService newService, ConnectionDetail connectionDetail, string actionName = "", object? parameter = null);

    /// <summary>
    /// Called by the shell when the active connection has been disposed.
    /// Plugins should drop their cached IOrganizationService reference and
    /// disable any UI that depends on it.
    /// Default implementation does nothing.
    /// </summary>
    void ResetConnection() { }

    object GetView();
}
