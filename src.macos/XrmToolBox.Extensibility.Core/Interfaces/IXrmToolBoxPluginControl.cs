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

    object GetView();
}
