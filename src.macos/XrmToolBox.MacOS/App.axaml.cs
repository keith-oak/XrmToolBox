using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using XrmToolBox.MacOS.Plugins;
using XrmToolBox.MacOS.Settings;
using XrmToolBox.MacOS.ViewModels;
using XrmToolBox.MacOS.Views;

namespace XrmToolBox.MacOS;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new SettingsService();
            ApplyTheme(settings);

            var pluginManager = new PluginManager();
            pluginManager.LoadPlugins();

            var vm = new MainWindowViewModel(pluginManager, settings);
            var window = new MainWindow { DataContext = vm };
            ApplyWindowPlacement(window, settings);
            window.Closing += (_, _) =>
            {
                vm.PersistSession(window.Position.X, window.Position.Y, window.Width, window.Height,
                    window.WindowState == Avalonia.Controls.WindowState.Maximized);
            };
            window.Opened += (_, _) => vm.RestoreOpenedPluginsFromSettings();

            desktop.MainWindow = window;
            BuildNativeMenu(desktop, vm);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyTheme(SettingsService settings)
    {
        if (Current is null) return;
        Current.RequestedThemeVariant = settings.Current.ThemeOverride switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private static void ApplyWindowPlacement(MainWindow window, SettingsService settings)
    {
        var p = settings.Current.Window;
        if (p is null) return;
        if (p.Width >= 600 && p.Height >= 400)
        {
            window.Width = p.Width;
            window.Height = p.Height;
        }
        if (p.X != 0 || p.Y != 0)
        {
            window.Position = new Avalonia.PixelPoint((int)p.X, (int)p.Y);
        }
        if (p.IsMaximised)
        {
            window.WindowState = Avalonia.Controls.WindowState.Maximized;
        }
    }

    private static void BuildNativeMenu(IClassicDesktopStyleApplicationLifetime desktop, MainWindowViewModel vm)
    {
        var appMenu = new Avalonia.Controls.NativeMenu();

        var fileMenu = new Avalonia.Controls.NativeMenuItem("File")
        {
            Menu = new Avalonia.Controls.NativeMenu(),
        };
        fileMenu.Menu.Items.Add(new Avalonia.Controls.NativeMenuItem("Browse Tool Library…")
        {
            Command = vm.ToggleStoreCommand,
        });
        fileMenu.Menu.Items.Add(new Avalonia.Controls.NativeMenuItemSeparator());
        fileMenu.Menu.Items.Add(new Avalonia.Controls.NativeMenuItem("Disconnect")
        {
            Command = vm.DisconnectCommand,
            Gesture = new Avalonia.Input.KeyGesture(Avalonia.Input.Key.D, Avalonia.Input.KeyModifiers.Meta | Avalonia.Input.KeyModifiers.Shift),
        });

        var viewMenu = new Avalonia.Controls.NativeMenuItem("View")
        {
            Menu = new Avalonia.Controls.NativeMenu(),
        };
        viewMenu.Menu.Items.Add(new Avalonia.Controls.NativeMenuItem("Command Palette")
        {
            Command = vm.ToggleCommandPaletteCommand,
            Gesture = new Avalonia.Input.KeyGesture(Avalonia.Input.Key.K, Avalonia.Input.KeyModifiers.Meta),
        });
        viewMenu.Menu.Items.Add(new Avalonia.Controls.NativeMenuItem("Reload Plugins")
        {
            Command = vm.ReloadPluginsCommand,
            Gesture = new Avalonia.Input.KeyGesture(Avalonia.Input.Key.R, Avalonia.Input.KeyModifiers.Meta),
        });

        var helpMenu = new Avalonia.Controls.NativeMenuItem("Help")
        {
            Menu = new Avalonia.Controls.NativeMenu(),
        };
        helpMenu.Menu.Items.Add(new Avalonia.Controls.NativeMenuItem("About XrmToolBox")
        {
            Command = vm.ToggleAboutCommand,
        });

        appMenu.Items.Add(fileMenu);
        appMenu.Items.Add(viewMenu);
        appMenu.Items.Add(helpMenu);

        if (desktop.MainWindow is not null)
        {
            Avalonia.Controls.NativeMenu.SetMenu(desktop.MainWindow, appMenu);
        }
    }
}
