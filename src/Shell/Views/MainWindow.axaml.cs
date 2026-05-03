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

    private void OnToolbarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only fire on the toolbar surface itself, not on bubbled-up events
        // from buttons/inputs inside it. If the original source is interactive,
        // BeginMoveDrag is a no-op anyway.
        var src = e.Source as Avalonia.Visual;
        if (src is Button or TextBox or AutoCompleteBox or ComboBox or ListBox)
        {
            return;
        }
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
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
