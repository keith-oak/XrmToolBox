// Bulk Data Updater — functional port (single-attribute string update).
//
// Workflow: paste FetchXML → Preview → review record list and target attribute
// → Update → progress + per-record status. Hardcoded-safe: single text
// attribute, no delete / setstate / assign yet (those are separate dialogs in
// the original; they each get their own follow-up).

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

namespace Cinteros.XTB.BulkDataUpdater;

public sealed class BulkDataUpdaterControl : IXrmToolBoxPluginControl
{
    private const string DefaultFetch = """
        <fetch top="50">
          <entity name="account">
            <attribute name="name" />
          </entity>
        </fetch>
        """;

    private readonly Grid _root;
    private readonly TextBlock _connectionLabel;
    private readonly TextBox _fetchEditor;
    private readonly Button _previewButton;
    private readonly Button _updateButton;
    private readonly Button _exportButton;
    private readonly TextBox _attributeName;
    private readonly TextBox _attributeValue;
    private readonly TextBlock _statusLabel;
    private readonly TextBlock _summaryLabel;
    private readonly ProgressBar _progress;
    private readonly ListBox _recordList;

    public ObservableCollection<RecordRow> Records { get; } = new();

    private IOrganizationService? _service;
    private string? _entityLogicalName;

    public event EventHandler? OnCloseTool;
    public event EventHandler? OnRequestConnection;

    public IOrganizationService? Service => _service;
    public string DisplayName => "Bulk Data Updater";

    public BulkDataUpdaterControl()
    {
        _connectionLabel = new TextBlock { Text = "Not connected.", FontWeight = FontWeight.SemiBold };
        _statusLabel = new TextBlock { Text = string.Empty, FontSize = 11, Opacity = 0.7 };
        _summaryLabel = new TextBlock { Text = string.Empty, FontWeight = FontWeight.SemiBold };

        _fetchEditor = new TextBox
        {
            Text = DefaultFetch,
            AcceptsReturn = true,
            AcceptsTab = true,
            FontFamily = "ui-monospace,SFMono-Regular,Menlo,monospace",
            FontSize = 12,
            Height = 180,
            TextWrapping = TextWrapping.NoWrap,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_fetchEditor, ScrollBarVisibility.Auto);

        _previewButton = new Button
        {
            Content = "Preview",
            Padding = new Thickness(14, 6),
            IsEnabled = false,
            Classes = { "primary" },
        };
        _previewButton.Click += async (_, _) => await PreviewAsync();

        _updateButton = new Button
        {
            Content = "Run update",
            Padding = new Thickness(14, 6),
            IsEnabled = false,
            Classes = { "primary" },
        };
        _updateButton.Click += async (_, _) => await UpdateAsync();

        _exportButton = new Button
        {
            Content = "Export CSV…",
            Padding = new Thickness(12, 6),
            IsEnabled = false,
            Classes = { "tertiary" },
        };
        _exportButton.Click += async (_, _) => await ExportCsvAsync();

        _attributeName = new TextBox { Watermark = "logical name (e.g. name)", Width = 220 };
        _attributeValue = new TextBox { Watermark = "new value", Width = 280 };

        _progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 6,
            IsVisible = false,
        };

        _recordList = new ListBox
        {
            ItemsSource = Records,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_recordList, ScrollBarVisibility.Auto);
        _recordList.ItemTemplate = new FuncDataTemplate<RecordRow>((row, _) =>
        {
            if (row is null) return new TextBlock();
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(2, 4) };
            stack.Children.Add(new TextBlock { Text = row.Glyph, Width = 18 });
            stack.Children.Add(new TextBlock
            {
                Text = row.Display,
                Width = 360,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            stack.Children.Add(new TextBlock
            {
                Text = row.Status,
                Opacity = 0.7,
                FontSize = 11,
                Foreground = row.Failed ? Brushes.OrangeRed : (row.Updated ? Brushes.ForestGreen : Brushes.Gray),
            });
            return stack;
        });

        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                LabelStack("Attribute", _attributeName),
                LabelStack("New value", _attributeValue),
                LabelStack(string.Empty, _updateButton),
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
                        new TextBlock { Text = "Bulk Data Updater", FontSize = 20, FontWeight = FontWeight.SemiBold },
                        _connectionLabel,
                    },
                },
                _previewButton,
            },
        };
        Grid.SetColumn(_previewButton, 1);

        var summaryStack = new StackPanel { Spacing = 4, Children = { _summaryLabel, _statusLabel, _progress } };
        DockPanel.SetDock(summaryStack, Dock.Top);
        var resultsDock = new DockPanel { LastChildFill = true };
        resultsDock.Children.Add(summaryStack);
        resultsDock.Children.Add(_recordList);
        var resultsBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Child = resultsDock,
        };

        _root = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
        };
        headerRow.Margin = new Thickness(0, 0, 0, 14);
        _fetchEditor.Margin = new Thickness(0, 0, 0, 8);
        actionRow.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(headerRow, 0);
        Grid.SetRow(_fetchEditor, 1);
        Grid.SetRow(actionRow, 2);
        Grid.SetRow(resultsBorder, 3);
        _root.Children.Add(headerRow);
        _root.Children.Add(_fetchEditor);
        _root.Children.Add(actionRow);
        _root.Children.Add(resultsBorder);
    }

    public object GetView() => _root;

    public void ClosingPlugin(PluginCloseInfo info) { }

    public void UpdateConnection(IOrganizationService newService, ConnectionDetail connectionDetail, string actionName = "", object? parameter = null)
    {
        _service = newService;
        _connectionLabel.Text = $"Connected to {connectionDetail.OrganizationFriendlyName ?? connectionDetail.Url}";
        _connectionLabel.Foreground = Brushes.ForestGreen;
        _previewButton.IsEnabled = true;
    }

    public void ResetConnection()
    {
        _service = null;
        _connectionLabel.Text = "Not connected.";
        _connectionLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _previewButton.IsEnabled = false;
        _updateButton.IsEnabled = false;
        _exportButton.IsEnabled = false;
        Records.Clear();
        _summaryLabel.Text = string.Empty;
        _statusLabel.Text = string.Empty;
    }

    private async Task PreviewAsync()
    {
        if (_service is null) return;
        Records.Clear();
        _summaryLabel.Text = string.Empty;
        _statusLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _statusLabel.Text = "Running query…";
        _previewButton.IsEnabled = false;
        _updateButton.IsEnabled = false;
        _exportButton.IsEnabled = false;
        try
        {
            var fetch = _fetchEditor.Text ?? string.Empty;
            var (entityName, rows) = await Task.Run(() =>
            {
                var coll = _service.RetrieveMultiple(new FetchExpression(fetch));
                return (coll.EntityName, coll.Entities.ToList());
            });
            _entityLogicalName = entityName;
            foreach (var e in rows)
            {
                var primary = e.Attributes.Values.OfType<string>().FirstOrDefault() ?? e.Id.ToString("D");
                Records.Add(new RecordRow
                {
                    Id = e.Id,
                    EntityLogical = entityName,
                    Display = $"{entityName} · {primary}",
                    Status = "ready",
                });
            }
            _summaryLabel.Text = $"{Records.Count} record(s) matched ({entityName}).";
            _statusLabel.Text = "Set the attribute logical name + new value, then Run update.";
            _updateButton.IsEnabled = Records.Count > 0;
            _exportButton.IsEnabled = Records.Count > 0;
        }
        catch (Exception ex)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _previewButton.IsEnabled = true;
        }
    }

    private async Task UpdateAsync()
    {
        if (_service is null || Records.Count == 0 || _entityLogicalName is null) return;
        var attr = (_attributeName.Text ?? string.Empty).Trim();
        var value = _attributeValue.Text ?? string.Empty;
        if (string.IsNullOrEmpty(attr))
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = "Attribute logical name is required.";
            return;
        }

        _previewButton.IsEnabled = false;
        _updateButton.IsEnabled = false;
        _progress.IsVisible = true;
        _progress.Maximum = Records.Count;
        _progress.Value = 0;
        _statusLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));

        int ok = 0, fail = 0;
        for (int i = 0; i < Records.Count; i++)
        {
            var row = Records[i];
            row.Status = "updating…";
            row.Glyph = "•";
            try
            {
                var update = new Entity(row.EntityLogical, row.Id);
                update[attr] = value;
                await Task.Run(() => _service.Update(update));
                row.Updated = true;
                row.Status = "updated";
                row.Glyph = "✓";
                ok++;
            }
            catch (Exception ex)
            {
                row.Failed = true;
                row.Status = ex.Message;
                row.Glyph = "✗";
                fail++;
            }
            _progress.Value = i + 1;
            _statusLabel.Text = $"{i + 1}/{Records.Count} processed ({ok} ok, {fail} failed)";
            // force re-template
            var idx = Records.IndexOf(row);
            Records.Remove(row);
            Records.Insert(idx, row);
            await Task.Yield();
        }

        _summaryLabel.Text = $"Done. {ok} updated, {fail} failed.";
        _statusLabel.Foreground = fail == 0 ? Brushes.ForestGreen : Brushes.OrangeRed;
        _previewButton.IsEnabled = true;
        _updateButton.IsEnabled = true;
    }

    private async Task ExportCsvAsync()
    {
        if (Records.Count == 0) return;
        var top = TopLevel.GetTopLevel(_root);
        if (top is null) return;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var entityHint = string.IsNullOrEmpty(_entityLogicalName) ? "bulkdata" : _entityLogicalName;
        var file = await top.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save bulk update audit",
            SuggestedFileName = $"{entityHint}_{stamp}.csv",
            DefaultExtension = "csv",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("CSV (Comma-separated)") { Patterns = new[] { "*.csv" } },
            },
        });
        if (file is null) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("entity,id,display,status,updated,failed");
        foreach (var r in Records)
        {
            sb.Append(EscapeCsv(r.EntityLogical)).Append(',')
              .Append(r.Id.ToString("D")).Append(',')
              .Append(EscapeCsv(r.Display)).Append(',')
              .Append(EscapeCsv(r.Status)).Append(',')
              .Append(r.Updated ? "true" : "false").Append(',')
              .Append(r.Failed ? "true" : "false")
              .AppendLine();
        }
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(sb.ToString());
        _statusLabel.Foreground = Brushes.ForestGreen;
        _statusLabel.Text = $"Exported {Records.Count} row(s) to {file.Name}.";
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

public sealed class RecordRow
{
    public Guid Id { get; set; }
    public string EntityLogical { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Glyph { get; set; } = "○";
    public bool Updated { get; set; }
    public bool Failed { get; set; }
}
