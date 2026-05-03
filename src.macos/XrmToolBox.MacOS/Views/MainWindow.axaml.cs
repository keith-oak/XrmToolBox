using Avalonia.Controls;
using XrmToolBox.MacOS.Plugins;
using XrmToolBox.MacOS.ViewModels;

namespace XrmToolBox.MacOS.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ApplyPlatformWindowChrome();
    }

    private void ApplyPlatformWindowChrome()
    {
        if (!OperatingSystem.IsMacOS())
        {
            ExtendClientAreaToDecorationsHint = false;
            ExtendClientAreaTitleBarHeightHint = 0;
            TransparencyLevelHint = new[] { Avalonia.Controls.WindowTransparencyLevel.None };
        }
    }

    private void OnPaletteItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox lb || DataContext is not MainWindowViewModel vm)
        {
            return;
        }
        if (lb.SelectedItem is PluginEntry entry)
        {
            vm.OpenPluginCommand.Execute(entry).Subscribe();
            vm.IsCommandPaletteOpen = false;
            lb.SelectedItem = null;
        }
    }
}
