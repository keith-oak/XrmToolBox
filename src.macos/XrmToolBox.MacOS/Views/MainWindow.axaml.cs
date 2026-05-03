using Avalonia.Controls;

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
}
