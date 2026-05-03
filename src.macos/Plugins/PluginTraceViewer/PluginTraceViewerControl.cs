// Plugin Trace Viewer — functional port (master / detail).
//
// Reads plugintracelog rows from the connected environment, filterable by
// plugin type name + message name + date range. Click a row to see the full
// message + exception. No grouping, no statistics, no docking — those are
// each separate sub-controls in the original and follow later.

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace Cinteros.XTB.PluginTraceViewer;

public sealed class PluginTraceViewerControl : IXrmToolBoxPluginControl
{
    private readonly Grid _root;
    private readonly TextBlock _connectionLabel;
    private readonly TextBox _typeFilter;
    private readonly TextBox _messageFilter;
    private readonly NumericUpDown _hours;
    private readonly Button _refreshButton;
    private readonly Button _exportButton;
    private readonly TextBlock _statusLabel;
    private readonly TextBlock _summaryLabel;
    private readonly ListBox _list;
    private readonly TextBlock _emptyHint;
    private readonly TextBlock _detailHeader;
    private readonly TextBlock _detailBody;
    private readonly TextBlock _detailException;

    public ObservableCollection<TraceRow> Rows { get; } = new();

    private IOrganizationService? _service;

    public event EventHandler? OnCloseTool;
    public event EventHandler? OnRequestConnection;

    public IOrganizationService? Service => _service;
    public string DisplayName => "Plugin Trace Viewer";

    public PluginTraceViewerControl()
    {
        _connectionLabel = new TextBlock { Text = "Not connected.", FontWeight = FontWeight.SemiBold };
        _statusLabel = new TextBlock { Text = string.Empty, FontSize = 11, Opacity = 0.7 };
        _summaryLabel = new TextBlock { Text = string.Empty, FontWeight = FontWeight.SemiBold };

        _typeFilter = new TextBox { Watermark = "type name contains…", Width = 240 };
        _messageFilter = new TextBox { Watermark = "message contains…", Width = 200 };
        _hours = new NumericUpDown { Value = 24, Minimum = 1, Maximum = 168, Increment = 1, FormatString = "0", Width = 140, MinWidth = 140 };

        _refreshButton = new Button
        {
            Content = "Refresh",
            Padding = new Thickness(14, 6),
            IsEnabled = false,
            Classes = { "primary" },
        };
        _refreshButton.Click += async (_, _) => await ReloadAsync();

        _exportButton = new Button
        {
            Content = "Export CSV…",
            Padding = new Thickness(12, 6),
            IsEnabled = false,
            Classes = { "tertiary" },
        };
        _exportButton.Click += async (_, _) => await ExportCsvAsync();

        _list = new ListBox
        {
            ItemsSource = Rows,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_list, ScrollBarVisibility.Auto);
        _list.ItemTemplate = new FuncDataTemplate<TraceRow>((row, _) =>
        {
            if (row is null) return new TextBlock();
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(2, 4) };
            stack.Children.Add(new TextBlock
            {
                Text = row.HasException ? "⚠" : "•",
                Width = 18,
                Foreground = row.HasException ? Brushes.OrangeRed : Brushes.Gray,
            });
            stack.Children.Add(new TextBlock { Text = row.CreatedOn.ToString("HH:mm:ss"), Width = 80, Opacity = 0.7, FontSize = 11 });
            stack.Children.Add(new TextBlock { Text = row.MessageName, Width = 110, FontWeight = FontWeight.Medium });
            stack.Children.Add(new TextBlock { Text = row.TypeName, Width = 280, TextTrimming = TextTrimming.CharacterEllipsis });
            stack.Children.Add(new TextBlock { Text = $"{row.DurationMs} ms", Width = 80, Opacity = 0.7, FontSize = 11 });
            return stack;
        });
        _list.SelectionChanged += (_, _) => ShowDetail(_list.SelectedItem as TraceRow);

        _detailHeader = new TextBlock { FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        _detailBody = new TextBlock { TextWrapping = TextWrapping.Wrap, FontFamily = "ui-monospace,SFMono-Regular,Menlo,monospace", FontSize = 12 };
        _detailException = new TextBlock { TextWrapping = TextWrapping.Wrap, FontFamily = "ui-monospace,SFMono-Regular,Menlo,monospace", FontSize = 12, Foreground = Brushes.OrangeRed };

        var filterRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                LabelStack("Type", _typeFilter),
                LabelStack("Message", _messageFilter),
                LabelStack("Hours back", _hours),
                LabelStack(string.Empty, _refreshButton),
                LabelStack(string.Empty, _exportButton),
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
                        new TextBlock { Text = "Plugin Trace Viewer", FontSize = 20, FontWeight = FontWeight.SemiBold },
                        _connectionLabel,
                    },
                },
            },
        };

        _emptyHint = new TextBlock
        {
            Text = "Connect, then Refresh to load plugin trace logs.\nIf the list stays empty, plugin tracing is likely off in this environment\n(System Settings → Customization → Plugin Trace Settings).",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Opacity = 0.55,
            FontSize = 12,
        };
        var listGrid = new Grid();
        listGrid.Children.Add(_list);
        listGrid.Children.Add(_emptyHint);
        var listBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Child = listGrid,
        };

        var detailStack = new StackPanel { Spacing = 10, Children = { _detailHeader, _detailBody, _detailException } };
        var detailBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12),
            Child = new ScrollViewer { Content = detailStack },
        };

        var splitter = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,Auto,2*") };
        var gap = new Border { Width = 12 };
        Grid.SetColumn(listBorder, 0);
        Grid.SetColumn(gap, 1);
        Grid.SetColumn(detailBorder, 2);
        splitter.Children.Add(listBorder);
        splitter.Children.Add(gap);
        splitter.Children.Add(detailBorder);

        _root = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
        };
        headerRow.Margin = new Thickness(0, 0, 0, 14);
        filterRow.Margin = new Thickness(0, 0, 0, 8);
        var summaryRow = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 10), Children = { _summaryLabel, _statusLabel } };
        Grid.SetRow(headerRow, 0);
        Grid.SetRow(filterRow, 1);
        Grid.SetRow(summaryRow, 2);
        Grid.SetRow(splitter, 3);
        _root.Children.Add(headerRow);
        _root.Children.Add(filterRow);
        _root.Children.Add(summaryRow);
        _root.Children.Add(splitter);
        ShowDetail(null);
    }

    public object GetView() => _root;

    public void ClosingPlugin(PluginCloseInfo info) { }

    public void UpdateConnection(IOrganizationService newService, ConnectionDetail connectionDetail, string actionName = "", object? parameter = null)
    {
        _service = newService;
        _connectionLabel.Text = $"Connected to {connectionDetail.OrganizationFriendlyName ?? connectionDetail.Url}";
        _connectionLabel.Foreground = Brushes.ForestGreen;
        _refreshButton.IsEnabled = true;
        _ = ReloadAsync();
    }

    public void ResetConnection()
    {
        _service = null;
        _connectionLabel.Text = "Not connected.";
        _connectionLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _refreshButton.IsEnabled = false;
        Rows.Clear();
        _summaryLabel.Text = string.Empty;
        ShowDetail(null);
    }

    private async Task ReloadAsync()
    {
        if (_service is null) return;
        _refreshButton.IsEnabled = false;
        _statusLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _statusLabel.Text = "Loading traces…";
        Rows.Clear();
        try
        {
            var typeQ = (_typeFilter.Text ?? string.Empty).Trim();
            var msgQ = (_messageFilter.Text ?? string.Empty).Trim();
            var hours = (int)(_hours.Value ?? 24);
            var since = DateTime.UtcNow.AddHours(-hours);

            var rows = await Task.Run(() =>
            {
                var query = new QueryExpression("plugintracelog")
                {
                    ColumnSet = new ColumnSet("typename", "messagename", "createdon", "performanceexecutionduration", "exceptiondetails", "messageblock", "configuration", "mode"),
                    TopCount = 250,
                    Orders = { new OrderExpression("createdon", OrderType.Descending) },
                };
                query.Criteria.AddCondition("createdon", ConditionOperator.GreaterEqual, since);
                if (!string.IsNullOrEmpty(typeQ))
                    query.Criteria.AddCondition("typename", ConditionOperator.Like, $"%{typeQ}%");
                if (!string.IsNullOrEmpty(msgQ))
                    query.Criteria.AddCondition("messagename", ConditionOperator.Like, $"%{msgQ}%");
                return _service.RetrieveMultiple(query).Entities.ToList();
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var e in rows)
                {
                    var ex = e.GetAttributeValue<string>("exceptiondetails") ?? string.Empty;
                    Rows.Add(new TraceRow
                    {
                        Id = e.Id,
                        TypeName = e.GetAttributeValue<string>("typename") ?? "(unknown)",
                        MessageName = e.GetAttributeValue<string>("messagename") ?? "(unknown)",
                        CreatedOn = e.GetAttributeValue<DateTime>("createdon").ToLocalTime(),
                        DurationMs = e.GetAttributeValue<int>("performanceexecutionduration"),
                        Message = e.GetAttributeValue<string>("messageblock") ?? string.Empty,
                        Exception = ex,
                        HasException = !string.IsNullOrEmpty(ex),
                    });
                }
                _summaryLabel.Text = $"{Rows.Count} trace(s) in the last {hours}h.";
                _statusLabel.Text = string.Empty;
                _exportButton.IsEnabled = Rows.Count > 0;
                _emptyHint.IsVisible = Rows.Count == 0;
                _emptyHint.Text = Rows.Count == 0
                    ? $"No traces in the last {hours}h.\nPlugin tracing might be off (System Settings → Customization\n→ Plugin Trace Settings → All)."
                    : string.Empty;
            });
        }
        catch (Exception ex)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _refreshButton.IsEnabled = true;
        }
    }

    private void ShowDetail(TraceRow? row)
    {
        if (row is null)
        {
            _detailHeader.Text = "Select a trace";
            _detailBody.Text = "Pick a row from the list to see its message + exception.";
            _detailException.Text = string.Empty;
            return;
        }
        _detailHeader.Text = $"{row.MessageName} · {row.TypeName}";
        _detailBody.Text = $"At        : {row.CreatedOn:yyyy-MM-dd HH:mm:ss}\nDuration  : {row.DurationMs} ms\n\n{row.Message}";
        _detailException.Text = row.HasException ? $"\nEXCEPTION\n{row.Exception}" : string.Empty;
    }

    private async Task ExportCsvAsync()
    {
        if (Rows.Count == 0) return;
        var top = TopLevel.GetTopLevel(_root);
        if (top is null) return;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var file = await top.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save plugin traces",
            SuggestedFileName = $"plugintraces_{stamp}.csv",
            DefaultExtension = "csv",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("CSV (Comma-separated)") { Patterns = new[] { "*.csv" } },
            },
        });
        if (file is null) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("createdon,messagename,typename,duration_ms,has_exception,message,exception");
        foreach (var r in Rows)
        {
            sb.Append(EscapeCsv(r.CreatedOn.ToString("s"))).Append(',')
              .Append(EscapeCsv(r.MessageName)).Append(',')
              .Append(EscapeCsv(r.TypeName)).Append(',')
              .Append(r.DurationMs).Append(',')
              .Append(r.HasException ? "true" : "false").Append(',')
              .Append(EscapeCsv(r.Message)).Append(',')
              .Append(EscapeCsv(r.Exception)).AppendLine();
        }
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(sb.ToString());
        _statusLabel.Foreground = Brushes.ForestGreen;
        _statusLabel.Text = $"Exported {Rows.Count} trace(s) to {file.Name}.";
    }

    private static string EscapeCsv(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static StackPanel LabelStack(string label, Control child)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(string.IsNullOrEmpty(label)
            ? new Control { Height = 16 }
            : new TextBlock { Text = label, FontSize = 11, Opacity = 0.65 });
        stack.Children.Add(child);
        return stack;
    }
}

public sealed class TraceRow
{
    public Guid Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string MessageName { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public int DurationMs { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Exception { get; set; } = string.Empty;
    public bool HasException { get; set; }
}
