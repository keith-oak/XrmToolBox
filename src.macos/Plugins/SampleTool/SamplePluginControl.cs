using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace XrmToolBox.SamplePlugin;

public sealed class SamplePluginControl : IXrmToolBoxPluginControl
{
    private static readonly (string Logical, string Display, string PrimaryAttr)[] s_entities =
    {
        ("account", "Accounts", "name"),
        ("contact", "Contacts", "fullname"),
        ("opportunity", "Opportunities", "name"),
        ("lead", "Leads", "fullname"),
        ("systemuser", "Users", "fullname"),
        ("team", "Teams", "name"),
        ("businessunit", "Business units", "name"),
    };

    private IOrganizationService? _service;
    private ConnectionDetail? _connection;

    private readonly ComboBox _entityPicker;
    private readonly NumericUpDown _topCount;
    private readonly Button _runButton;
    private readonly Button _copyButton;
    private readonly TextBlock _connectionLabel;
    private readonly TextBlock _statusLabel;
    private readonly TextBlock _summaryLabel;
    private readonly ListBox _resultsList;
    private readonly StackPanel _root;

    public ObservableCollection<RecordRow> Results { get; } = new();

    public event EventHandler? OnCloseTool;
    public event EventHandler? OnRequestConnection;

    public IOrganizationService? Service => _service;

    public string DisplayName => "Sample Tool";

    public SamplePluginControl()
    {
        _connectionLabel = new TextBlock
        {
            Text = "Not connected.",
            FontWeight = FontWeight.SemiBold,
        };

        _entityPicker = new ComboBox
        {
            Width = 200,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        foreach (var (_, display, _) in s_entities)
        {
            _entityPicker.Items.Add(new ComboBoxItem { Content = display });
        }

        _topCount = new NumericUpDown
        {
            Value = 10,
            Minimum = 1,
            Maximum = 250,
            Increment = 5,
            FormatString = "0",
            Width = 110,
        };

        _runButton = new Button
        {
            Content = "Run query",
            Padding = new Thickness(14, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Classes = { "primary" },
        };
        _runButton.Click += async (_, _) => await RunQueryAsync();

        _copyButton = new Button
        {
            Content = "Copy results",
            Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false,
            Classes = { "tertiary" },
        };
        _copyButton.Click += async (_, _) => await CopyResultsAsync();

        var closeButton = new Button
        {
            Content = "Close tool",
            Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            Classes = { "tertiary" },
        };
        closeButton.Click += (_, _) => OnCloseTool?.Invoke(this, EventArgs.Empty);

        _statusLabel = new TextBlock
        {
            Text = string.Empty,
            FontSize = 11,
        };

        _summaryLabel = new TextBlock
        {
            Text = string.Empty,
            FontWeight = FontWeight.SemiBold,
        };

        _resultsList = new ListBox
        {
            ItemsSource = Results,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MinHeight = 280,
        };
        _resultsList.ItemTemplate = new FuncDataTemplate<RecordRow>((row, _) =>
        {
            if (row is null) return new TextBlock();
            var stack = new StackPanel { Spacing = 2, Margin = new Thickness(4, 6) };
            stack.Children.Add(new TextBlock { Text = row.Display, FontWeight = FontWeight.Medium });
            stack.Children.Add(new TextBlock
            {
                Text = row.Id.ToString("D"),
                FontSize = 10,
                Opacity = 0.6,
                FontFamily = "ui-monospace,SFMono-Regular,Menlo,monospace",
            });
            return stack;
        });

        var queryRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                LabelledColumn("Entity", _entityPicker),
                LabelledColumn("Top N", _topCount),
                new Border { Width = 1, Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), Margin = new Thickness(4, 0) },
                LabelledColumn(string.Empty, _runButton),
                LabelledColumn(string.Empty, _copyButton),
            },
        };

        var headerRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Sample Tool",
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold,
                        },
                        _connectionLabel,
                    },
                },
                closeButton,
            },
        };
        Grid.SetColumn((Control)headerRow.Children[1], 1);

        var resultsCard = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    _summaryLabel,
                    _resultsList,
                },
            },
        };

        _root = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 18,
            Children =
            {
                headerRow,
                queryRow,
                _statusLabel,
                resultsCard,
            },
        };
    }

    public object GetView() => _root;

    public void ClosingPlugin(PluginCloseInfo info)
    {
    }

    public void UpdateConnection(IOrganizationService newService, ConnectionDetail connectionDetail, string actionName = "", object? parameter = null)
    {
        _service = newService;
        _connection = connectionDetail;
        _connectionLabel.Text = $"Connected to {connectionDetail.OrganizationFriendlyName ?? connectionDetail.Url} ({connectionDetail.OrganizationVersion})";
        _connectionLabel.Foreground = Brushes.ForestGreen;
    }

    private async Task RunQueryAsync()
    {
        if (_service is null)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = "Connect to a Dataverse environment first.";
            OnRequestConnection?.Invoke(this, EventArgs.Empty);
            return;
        }

        var entry = s_entities[_entityPicker.SelectedIndex];
        var top = (int)(_topCount.Value ?? 10);

        _runButton.IsEnabled = false;
        _statusLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _statusLabel.Text = $"Querying {entry.Display.ToLowerInvariant()}…";
        Results.Clear();
        _summaryLabel.Text = string.Empty;
        _copyButton.IsEnabled = false;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var query = new QueryExpression(entry.Logical)
            {
                ColumnSet = new ColumnSet(entry.PrimaryAttr),
                TopCount = top,
            };
            var result = await Task.Run(() => _service.RetrieveMultiple(query));
            sw.Stop();

            foreach (var en in result.Entities)
            {
                Results.Add(new RecordRow
                {
                    Id = en.Id,
                    Display = en.GetAttributeValue<string>(entry.PrimaryAttr) ?? "(no name)",
                });
            }

            _statusLabel.Text = $"Done in {sw.ElapsedMilliseconds} ms.";
            _summaryLabel.Text = $"{result.Entities.Count} {entry.Display.ToLowerInvariant()} returned.";
            _copyButton.IsEnabled = result.Entities.Count > 0;
        }
        catch (Exception ex)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _runButton.IsEnabled = true;
        }
    }

    private async Task CopyResultsAsync()
    {
        var clipboard = TopLevel.GetTopLevel(_root)?.Clipboard;
        if (clipboard is null) return;
        var lines = string.Join('\n', Results.Select(r => $"{r.Id}\t{r.Display}"));
        await clipboard.SetTextAsync(lines);
        _statusLabel.Text = $"Copied {Results.Count} row(s) to clipboard.";
    }

    private static StackPanel LabelledColumn(string label, Control content)
    {
        var stack = new StackPanel { Spacing = 4 };
        if (!string.IsNullOrEmpty(label))
        {
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                Opacity = 0.65,
            });
        }
        else
        {
            stack.Children.Add(new Control { Height = 16 });
        }
        stack.Children.Add(content);
        return stack;
    }
}

public sealed class RecordRow
{
    public Guid Id { get; set; }
    public string Display { get; set; } = string.Empty;
}
