using Avalonia.Controls;
using Avalonia.Input;
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

    private void OnPluginDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is PluginEntry entry &&
            DataContext is MainWindowViewModel vm)
        {
            vm.OpenPluginCommand.Execute(entry).Subscribe();
        }
    }
}
