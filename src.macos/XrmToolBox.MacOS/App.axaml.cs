using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using XrmToolBox.MacOS.Plugins;
using XrmToolBox.MacOS.Views;

namespace XrmToolBox.MacOS;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var pluginManager = new PluginManager();
            pluginManager.LoadPlugins();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new ViewModels.MainWindowViewModel(pluginManager),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
