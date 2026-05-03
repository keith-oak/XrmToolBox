using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace XrmToolBox.SamplePlugin;

[Export(typeof(IXrmToolBoxPlugin))]
[ExportMetadata("Name", "Sample Tool")]
[ExportMetadata("Description", "Reference implementation showing the cross-platform XrmToolBox plugin pattern.")]
[ExportMetadata("SmallImageBase64", "")]
[ExportMetadata("BigImageBase64", "")]
[ExportMetadata("BackgroundColor", "#0078D4")]
[ExportMetadata("PrimaryFontColor", "White")]
[ExportMetadata("SecondaryFontColor", "WhiteSmoke")]
public sealed class SamplePlugin : PluginBase, IGitHubPlugin, IHelpPlugin
{
    public override IXrmToolBoxPluginControl GetControl() => new SamplePluginControl();

    public string UserName => "MscrmTools";
    public string RepositoryName => "XrmToolBox";
    public string HelpUrl => "https://www.xrmtoolbox.com/documentation/";
}
