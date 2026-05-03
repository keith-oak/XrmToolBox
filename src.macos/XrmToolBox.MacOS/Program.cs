using Avalonia;
using Avalonia.ReactiveUI;
using XrmToolBox.MacOS.Plugins;

namespace XrmToolBox.MacOS;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--probe")
        {
            return Probe();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();

    private static int Probe()
    {
        var pm = new PluginManager();
        Console.WriteLine($"Plugins folder: {pm.PluginsDirectory}");
        pm.LoadPlugins();
        var entries = pm.GetPluginEntries();
        Console.WriteLine($"Discovered {entries.Count} plugin(s):");
        foreach (var e in entries)
        {
            Console.WriteLine($" - {e.Metadata.Name} :: {e.Plugin.GetType().FullName} :: v{e.Plugin.GetVersion()}");
        }
        return entries.Count > 0 ? 0 : 2;
    }
}
