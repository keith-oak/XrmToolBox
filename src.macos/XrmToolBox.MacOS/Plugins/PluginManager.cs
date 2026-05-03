using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using XrmToolBox.Extensibility.Interfaces;

namespace XrmToolBox.MacOS.Plugins;

public sealed class PluginManager
{
    private CompositionContainer? _container;

    [ImportMany(typeof(IXrmToolBoxPlugin))]
    public IEnumerable<Lazy<IXrmToolBoxPlugin, IPluginMetadata>> Plugins { get; private set; } =
        Array.Empty<Lazy<IXrmToolBoxPlugin, IPluginMetadata>>();

    public string PluginsDirectory { get; }

    public PluginManager()
    {
        var baseDir = AppContext.BaseDirectory;
        PluginsDirectory = Path.Combine(baseDir, "Plugins");
        Directory.CreateDirectory(PluginsDirectory);
    }

    public void LoadPlugins()
    {
        var catalog = new AggregateCatalog();

        foreach (var dll in Directory.EnumerateFiles(PluginsDirectory, "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                catalog.Catalogs.Add(new AssemblyCatalog(dll));
            }
            catch
            {
                // Skip plugin DLLs that fail to load — keep host alive
            }
        }

        _container = new CompositionContainer(catalog);
        _container.ComposeParts(this);
    }

    public IReadOnlyList<PluginEntry> GetPluginEntries() =>
        Plugins
            .Select(p => new PluginEntry(p.Value, p.Metadata))
            .OrderBy(p => p.Metadata.Name)
            .ToList();
}

public sealed record PluginEntry(IXrmToolBoxPlugin Plugin, IPluginMetadata Metadata);
