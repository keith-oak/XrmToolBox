namespace XrmToolBox.Extensibility.Interfaces;

public interface IXrmToolBoxPlugin
{
    string GetCompany();
    string GetVersion();
    string GetAssemblyQualifiedName();
    IXrmToolBoxPluginControl GetControl();
}
