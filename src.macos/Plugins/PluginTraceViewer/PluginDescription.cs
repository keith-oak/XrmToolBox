// Auto-ported from rappen/XrmToolBox.PluginTraceViewer@e6eda110 — PluginDescription.cs
using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace Cinteros.XTB.PluginTraceViewer;

[Export(typeof(IXrmToolBoxPlugin))]
[ExportMetadata("Name", "Plugin Trace Viewer")]
[ExportMetadata("Description", "Investigate the Plugin Trace Log with easy filtering and display possibilities")]
[ExportMetadata("SmallImageBase64", "")]
[ExportMetadata("BigImageBase64", "")]
[ExportMetadata("BackgroundColor", "#FFFFC0")]
[ExportMetadata("PrimaryFontColor", "#0000C0")]
[ExportMetadata("SecondaryFontColor", "#0000FF")]
public sealed class PluginTraceViewerPlugin : PluginBase, IPayPalPlugin, IGitHubPlugin, IHelpPlugin
{
    public override IXrmToolBoxPluginControl GetControl() => new PluginTraceViewerControl();

    public string DonationDescription => "Plugin Trace Viewer Fan Club";
    public string EmailAccount => "jonas@rappen.net";

    public string UserName => "rappen";
    public string RepositoryName => "XrmToolBox.PluginTraceViewer";
    public string HelpUrl => "https://jonasr.app/PTV";
}
