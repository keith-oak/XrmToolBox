// Auto-ported from Biznamics/PluginRegistration@2313168f — Plugin.cs
using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace Xrm.Sdk.PluginRegistration;

[Export(typeof(IXrmToolBoxPlugin))]
[ExportMetadata("Name", "Plugin Registration")]
[ExportMetadata("Description", "Classic plugin registration, modernized for Dataverse / Dynamics 365")]
[ExportMetadata("SmallImageBase64", "")]
[ExportMetadata("BigImageBase64", "")]
[ExportMetadata("BackgroundColor", "Lavender")]
[ExportMetadata("PrimaryFontColor", "#000000")]
[ExportMetadata("SecondaryFontColor", "DarkGray")]
public sealed class Plugin : PluginBase, IGitHubPlugin
{
    public override IXrmToolBoxPluginControl GetControl() => new MainControl();

    public string UserName => "Biznamics";
    public string RepositoryName => "PluginRegistration";
}
