using System.Reflection;
using XrmToolBox.Extensibility.Interfaces;

namespace XrmToolBox.Extensibility;

public abstract class PluginBase : IXrmToolBoxPlugin
{
    public virtual string GetCompany()
    {
        var asm = GetType().Assembly;
        var attr = asm.GetCustomAttribute<AssemblyCompanyAttribute>();
        return attr?.Company ?? string.Empty;
    }

    public virtual string GetVersion()
    {
        var asm = GetType().Assembly;
        return asm.GetName().Version?.ToString() ?? "0.0.0.0";
    }

    public virtual string GetAssemblyQualifiedName() =>
        GetType().AssemblyQualifiedName ?? GetType().FullName ?? GetType().Name;

    public abstract IXrmToolBoxPluginControl GetControl();
}
