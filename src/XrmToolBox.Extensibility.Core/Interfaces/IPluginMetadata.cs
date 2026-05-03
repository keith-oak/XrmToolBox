namespace XrmToolBox.Extensibility.Interfaces;

public interface IPluginMetadata
{
    string Name { get; }
    string Description { get; }
    string SmallImageBase64 { get; }
    string BigImageBase64 { get; }
    string BackgroundColor { get; }
    string PrimaryFontColor { get; }
    string SecondaryFontColor { get; }
}
