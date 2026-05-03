using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using XrmToolBox.MacOS.Plugins;
using XrmToolBox.MacOS.ViewModels;

namespace XrmToolBox.MacOS.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
