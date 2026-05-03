// Auto-ported from rappen/fetchxmlbuilder@71ad8621 — FetchXMLBuilderPlugin.cs
using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace Rappen.XTB.FetchXmlBuilder;

[Export(typeof(IXrmToolBoxPlugin))]
[ExportMetadata("Name", "FetchXML Builder")]
[ExportMetadata("Description", "Build queries for Microsoft Dataverse. Run them. Get code. Let AI fix what you can't. Empower yourself to achieve more.")]
[ExportMetadata("SmallImageBase64", "")]
[ExportMetadata("BigImageBase64", "")]
[ExportMetadata("BackgroundColor", "#FFFFC0")]
[ExportMetadata("PrimaryFontColor", "#0000C0")]
[ExportMetadata("SecondaryFontColor", "#0000FF")]
public sealed class FetchXMLBuilderPlugin : PluginBase, IPayPalPlugin, IGitHubPlugin, IHelpPlugin
{
    public override IXrmToolBoxPluginControl GetControl() => new FetchXmlBuilderControl();

    public string DonationDescription => "FetchXML Builder Fan Club";
    public string EmailAccount => "jonas@rappen.net";

    public string UserName => "rappen";
    public string RepositoryName => "fetchxmlbuilder";
    public string HelpUrl => "https://fetchxmlbuilder.com/";
}
