// Auto-ported from rappen/BulkDataUpdater@40158af2 — PluginDescription.cs
// Mechanical port per spec 002. Original kept verbatim where possible; differences
// are confined to (a) namespaces and (b) capability interface surface.
using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace Cinteros.XTB.BulkDataUpdater;

[Export(typeof(IXrmToolBoxPlugin))]
[ExportMetadata("Name", "Bulk Data Updater")]
[ExportMetadata("Description", "BDU can update one or multiple columns, for one or a gazillion records in Microsoft Dataverse! Empower yourself to achieve more.")]
[ExportMetadata("SmallImageBase64", "")]
[ExportMetadata("BigImageBase64", "")]
[ExportMetadata("BackgroundColor", "#FFFFC0")]
[ExportMetadata("PrimaryFontColor", "#0000C0")]
[ExportMetadata("SecondaryFontColor", "#0000FF")]
public sealed class BulkDataUpdaterTool : PluginBase, IPayPalPlugin, IGitHubPlugin, IHelpPlugin
{
    public override IXrmToolBoxPluginControl GetControl() => new BulkDataUpdaterControl();

    // TODO_PORT: original SaveImageFromBase64 wiring depends on UrlUtils + on-disk
    // icon paths used by the legacy XrmToolBox shell. The macOS shell renders icons
    // straight from the metadata so this method is intentionally unimplemented.

    public string DonationDescription => "Bulk Data Updater Fan Club";
    public string EmailAccount => "jonas@rappen.net";

    public string UserName => "rappen";
    public string RepositoryName => "BulkDataUpdater";
    public string HelpUrl => "https://jonasr.app/BDU/";
}
