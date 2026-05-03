// Auto-ported from daryllabar/DLaB.Xrm.XrmToolBoxTools@c977ffd4 — EarlyBoundGeneratorPlugin.cs
using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace DLaB.EarlyBoundGeneratorV2;

[Export(typeof(IXrmToolBoxPlugin))]
[ExportMetadata("Name", "Early Bound Generator V2")]
[ExportMetadata("Description", "Adds advanced features and configuration to the generation of Early Bound Dataverse Tables.")]
[ExportMetadata("SmallImageBase64", "")]
[ExportMetadata("BigImageBase64", "")]
[ExportMetadata("BackgroundColor", "White")]
[ExportMetadata("PrimaryFontColor", "#000000")]
[ExportMetadata("SecondaryFontColor", "DarkGray")]
public sealed class EarlyBoundGeneratorPluginEntry : PluginBase, IGitHubPlugin, IHelpPlugin, IPayPalPlugin
{
    public override IXrmToolBoxPluginControl GetControl() => new EarlyBoundGeneratorControl();

    public string UserName => "daryllabar";
    public string RepositoryName => "DLaB.Xrm.XrmToolBoxTools";
    public string HelpUrl => "https://github.com/daryllabar/DLaB.Xrm.XrmToolBoxTools/wiki/Early-Bound-Generator";

    public string DonationDescription => "Support Development for the Early Bound Generator!";
    public string EmailAccount => "daryl.labar@gmail.com";
}
