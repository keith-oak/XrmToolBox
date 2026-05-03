using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace XrmToolBox.SamplePlugin;

public sealed class SamplePluginControl : IXrmToolBoxPluginControl
{
    private IOrganizationService? _service;
    private ConnectionDetail? _connection;
    private readonly TextBlock _connectionLabel;
    private readonly TextBlock _resultLabel;
    private readonly Button _loadAccountsButton;
    private readonly StackPanel _root;

    public event EventHandler? OnCloseTool;
    public event EventHandler? OnRequestConnection;

    public IOrganizationService? Service => _service;

    public string DisplayName => "Sample Tool";

    public SamplePluginControl()
    {
        _connectionLabel = new TextBlock
        {
            Text = "No connection.",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
        };

        _loadAccountsButton = new Button
        {
            Content = "Load 10 Accounts",
            Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _loadAccountsButton.Click += LoadAccounts;

        _resultLabel = new TextBlock
        {
            Text = string.Empty,
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };

        var closeButton = new Button
        {
            Content = "Close tool",
            Padding = new Thickness(12, 6),
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        closeButton.Click += (_, _) => OnCloseTool?.Invoke(this, EventArgs.Empty);

        _root = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = "Sample XrmToolBox plugin (macOS)",
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                },
                _connectionLabel,
                _loadAccountsButton,
                _resultLabel,
                closeButton,
            },
        };
    }

    public object GetView() => _root;

    public void ClosingPlugin(PluginCloseInfo info)
    {
        // Sample tool has no unsaved state.
    }

    public void UpdateConnection(IOrganizationService newService, ConnectionDetail connectionDetail, string actionName = "", object? parameter = null)
    {
        _service = newService;
        _connection = connectionDetail;
        _connectionLabel.Text = $"Connected to {connectionDetail.OrganizationFriendlyName ?? connectionDetail.Url} ({connectionDetail.OrganizationVersion})";
    }

    private async void LoadAccounts(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_service is null)
        {
            _resultLabel.Foreground = Brushes.OrangeRed;
            _resultLabel.Text = "Connect to a Dataverse environment first.";
            OnRequestConnection?.Invoke(this, EventArgs.Empty);
            return;
        }

        _resultLabel.Foreground = Brushes.Gray;
        _resultLabel.Text = "Loading...";
        try
        {
            var query = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet("name"),
                TopCount = 10,
            };
            var result = await Task.Run(() => _service.RetrieveMultiple(query));
            var names = string.Join("\n", result.Entities.Select(en => " • " + en.GetAttributeValue<string>("name")));
            _resultLabel.Foreground = Brushes.ForestGreen;
            _resultLabel.Text = $"Loaded {result.Entities.Count} accounts:\n{names}";
        }
        catch (Exception ex)
        {
            _resultLabel.Foreground = Brushes.OrangeRed;
            _resultLabel.Text = $"Error: {ex.Message}";
        }
    }
}
