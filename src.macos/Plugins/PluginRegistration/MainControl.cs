// Plugin Registration — functional port (read-only first cut).
//
// Lists every pluginassembly in the connected environment, drilling into
// plugintype and sdkmessageprocessingstep. Selecting a node populates the
// detail pane on the right. No register / unregister / image management yet —
// those follow in the next pass and need editor dialogs.

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

namespace Xrm.Sdk.PluginRegistration;

public sealed class MainControl : IXrmToolBoxPluginControl
{
    private readonly Grid _root;
    private readonly TextBlock _connectionLabel;
    private readonly TextBlock _statusLabel;
    private readonly Button _refreshButton;
    private readonly Button _exportButton;
    private readonly TreeView _tree;
    private readonly StackPanel _detailPanel;
    private readonly TextBlock _detailHeader;
    private readonly TextBlock _detailBody;
    private readonly StackPanel _stepActions;
    private readonly Button _enableStepButton;
    private readonly Button _disableStepButton;
    private readonly Button _deleteStepButton;
    private RegistrationNode? _selected;

    public ObservableCollection<RegistrationNode> Roots { get; } = new();

    private IOrganizationService? _service;
    private ConnectionDetail? _connection;

    public event EventHandler? OnCloseTool;
    public event EventHandler? OnRequestConnection;

    public IOrganizationService? Service => _service;
    public string DisplayName => "Plugin Registration";

    public MainControl()
    {
        _connectionLabel = new TextBlock { Text = "Not connected.", FontWeight = FontWeight.SemiBold };
        _statusLabel = new TextBlock { Text = string.Empty, Opacity = 0.7, FontSize = 11 };

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

        _tree = new TreeView
        {
            ItemsSource = Roots,
            Background = Brushes.Transparent,
        };
        _tree.ItemTemplate = new FuncTreeDataTemplate<RegistrationNode>(
            (node, _) =>
            {
                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                };
                stack.Children.Add(new TextBlock { Text = node?.Glyph ?? string.Empty, Opacity = 0.7 });
                stack.Children.Add(new TextBlock { Text = node?.Title ?? string.Empty, FontWeight = node?.Kind == NodeKind.Assembly ? FontWeight.SemiBold : FontWeight.Normal });
                if (!string.IsNullOrEmpty(node?.Subtitle))
                {
                    stack.Children.Add(new TextBlock { Text = node!.Subtitle, Opacity = 0.55, FontSize = 11 });
                }
                return stack;
            },
            n => n.Children);
        _tree.SelectionChanged += (_, _) => ShowDetail(_tree.SelectedItem as RegistrationNode);

        _detailHeader = new TextBlock { FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        _detailBody = new TextBlock { TextWrapping = TextWrapping.Wrap, FontFamily = "ui-monospace,SFMono-Regular,Menlo,monospace", FontSize = 12, Opacity = 0.85 };

        _enableStepButton = new Button { Content = "Enable step", Padding = new Thickness(12, 6), Classes = { "primary" }, IsVisible = false };
        _enableStepButton.Click += async (_, _) => await SetStepStateAsync(activate: true);
        _disableStepButton = new Button { Content = "Disable step", Padding = new Thickness(12, 6), Classes = { "tertiary" }, IsVisible = false };
        _disableStepButton.Click += async (_, _) => await SetStepStateAsync(activate: false);
        _deleteStepButton = new Button { Content = "Delete step", Padding = new Thickness(12, 6), Classes = { "tertiary" }, IsVisible = false };
        _deleteStepButton.Click += async (_, _) => await DeleteStepAsync();

        _stepActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _enableStepButton, _disableStepButton, _deleteStepButton },
        };

        _detailPanel = new StackPanel
        {
            Spacing = 10,
            Children = { _detailHeader, _stepActions, _detailBody },
        };

        var headerActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _exportButton, _refreshButton },
        };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = "Plugin Registration", FontSize = 20, FontWeight = FontWeight.SemiBold },
                        _connectionLabel,
                        _statusLabel,
                    },
                },
                headerActions,
            },
        };
        Grid.SetColumn(headerActions, 1);

        var splitter = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,Auto,3*"),
        };
        var treeBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Child = new ScrollViewer { Content = _tree, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto },
        };
        var detailBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12),
            Child = new ScrollViewer { Content = _detailPanel },
        };
        var gap = new Border { Width = 12 };
        Grid.SetColumn(treeBorder, 0);
        Grid.SetColumn(gap, 1);
        Grid.SetColumn(detailBorder, 2);
        splitter.Children.Add(treeBorder);
        splitter.Children.Add(gap);
        splitter.Children.Add(detailBorder);

        _root = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        Grid.SetRow(header, 0);
        Grid.SetRow(splitter, 1);
        header.Margin = new Thickness(0, 0, 0, 14);
        _root.Children.Add(header);
        _root.Children.Add(splitter);

        ShowDetail(null);
    }

    public object GetView() => _root;

    public void ClosingPlugin(PluginCloseInfo info) { }

    public void UpdateConnection(IOrganizationService newService, ConnectionDetail connectionDetail, string actionName = "", object? parameter = null)
    {
        _service = newService;
        _connection = connectionDetail;
        _connectionLabel.Text = $"Connected to {connectionDetail.OrganizationFriendlyName ?? connectionDetail.Url}";
        _connectionLabel.Foreground = Brushes.ForestGreen;
        _refreshButton.IsEnabled = true;
        _ = ReloadAsync();
    }

    public void ResetConnection()
    {
        _service = null;
        _connection = null;
        _connectionLabel.Text = "Not connected.";
        _connectionLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _refreshButton.IsEnabled = false;
        Roots.Clear();
        ShowDetail(null);
    }

    private async Task ReloadAsync()
    {
        if (_service is null) return;
        _refreshButton.IsEnabled = false;
        _statusLabel.Text = "Loading registered plugins…";
        Roots.Clear();
        try
        {
            var nodes = await Task.Run(() => LoadTree(_service));
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var n in nodes) Roots.Add(n);
                _statusLabel.Text = $"{nodes.Count} assembly(ies), {nodes.Sum(a => a.Children.Count)} type(s).";
                _exportButton.IsEnabled = Roots.Count > 0;
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

    private static List<RegistrationNode> LoadTree(IOrganizationService service)
    {
        var assemblies = service.RetrieveMultiple(new QueryExpression("pluginassembly")
        {
            ColumnSet = new ColumnSet("name", "version", "publickeytoken", "isolationmode", "sourcetype"),
            TopCount = 250,
            Orders = { new OrderExpression("name", OrderType.Ascending) },
        }).Entities;

        var types = service.RetrieveMultiple(new QueryExpression("plugintype")
        {
            ColumnSet = new ColumnSet("typename", "friendlyname", "name", "pluginassemblyid", "description"),
            TopCount = 5000,
            Orders = { new OrderExpression("typename", OrderType.Ascending) },
        }).Entities;

        var steps = service.RetrieveMultiple(new QueryExpression("sdkmessageprocessingstep")
        {
            ColumnSet = new ColumnSet("name", "stage", "mode", "rank", "filteringattributes", "plugintypeid", "sdkmessageid", "statecode"),
            TopCount = 5000,
            Orders = { new OrderExpression("name", OrderType.Ascending) },
        }).Entities;

        var typeById = types.GroupBy(t => t.GetAttributeValue<EntityReference>("pluginassemblyid")?.Id ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.ToList());
        var stepsByType = steps.GroupBy(s => s.GetAttributeValue<EntityReference>("plugintypeid")?.Id ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.ToList());

        var roots = new List<RegistrationNode>();
        foreach (var asm in assemblies)
        {
            var asmNode = new RegistrationNode
            {
                Kind = NodeKind.Assembly,
                Glyph = "📦",
                Title = asm.GetAttributeValue<string>("name") ?? "(no name)",
                Subtitle = $"v{asm.GetAttributeValue<string>("version")}",
                Detail = FormatAssembly(asm),
                Entity = asm,
            };
            if (typeById.TryGetValue(asm.Id, out var asmTypes))
            {
                foreach (var t in asmTypes)
                {
                    var typeNode = new RegistrationNode
                    {
                        Kind = NodeKind.Type,
                        Glyph = "🧩",
                        Title = t.GetAttributeValue<string>("typename") ?? t.GetAttributeValue<string>("name") ?? "(no type)",
                        Subtitle = t.GetAttributeValue<string>("friendlyname") ?? string.Empty,
                        Detail = FormatType(t),
                        Entity = t,
                    };
                    if (stepsByType.TryGetValue(t.Id, out var typeSteps))
                    {
                        foreach (var s in typeSteps)
                        {
                            typeNode.Children.Add(new RegistrationNode
                            {
                                Kind = NodeKind.Step,
                                Glyph = "⚡",
                                Title = s.GetAttributeValue<string>("name") ?? "(no step)",
                                Subtitle = StageLabel(StageOf(s)),
                                Detail = FormatStep(s),
                                Entity = s,
                            });
                        }
                    }
                    asmNode.Children.Add(typeNode);
                }
            }
            roots.Add(asmNode);
        }
        return roots;
    }

    private void ShowDetail(RegistrationNode? node)
    {
        _selected = node;
        if (node is null)
        {
            _detailHeader.Text = "Select a node";
            _detailBody.Text = "Pick an assembly, plugin type, or step from the tree to see its registration details.";
            _enableStepButton.IsVisible = _disableStepButton.IsVisible = _deleteStepButton.IsVisible = false;
            return;
        }
        _detailHeader.Text = node.Title;
        _detailBody.Text = node.Detail;

        var isStep = node.Kind == NodeKind.Step;
        var isEnabled = isStep && node.Entity is { } e && (e.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? 0) == 0;
        _enableStepButton.IsVisible = isStep && !isEnabled;
        _disableStepButton.IsVisible = isStep && isEnabled;
        _deleteStepButton.IsVisible = isStep;
    }

    private async Task SetStepStateAsync(bool activate)
    {
        if (_service is null || _selected?.Entity is null || _selected.Kind != NodeKind.Step) return;
        var id = _selected.Entity.Id;
        _statusLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _statusLabel.Text = activate ? "Enabling step…" : "Disabling step…";
        try
        {
            await Task.Run(() =>
            {
                var update = new Entity("sdkmessageprocessingstep", id)
                {
                    ["statecode"] = new OptionSetValue(activate ? 0 : 1),
                    ["statuscode"] = new OptionSetValue(activate ? 1 : 2),
                };
                _service.Update(update);
            });
            _statusLabel.Foreground = Brushes.ForestGreen;
            _statusLabel.Text = activate ? "Step enabled." : "Step disabled.";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async Task DeleteStepAsync()
    {
        if (_service is null || _selected?.Entity is null || _selected.Kind != NodeKind.Step) return;
        var id = _selected.Entity.Id;
        var name = _selected.Title;
        // Confirm with a small dialog.
        var top = TopLevel.GetTopLevel(_root);
        if (top is not null)
        {
            var confirmed = await ConfirmAsync(top, "Delete step?", $"This permanently deletes\n\n  {name}\n\nfrom the connected environment. Continue?");
            if (!confirmed) return;
        }
        _statusLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _statusLabel.Text = "Deleting step…";
        try
        {
            await Task.Run(() => _service.Delete("sdkmessageprocessingstep", id));
            _statusLabel.Foreground = Brushes.ForestGreen;
            _statusLabel.Text = "Step deleted.";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private static Task<bool> ConfirmAsync(TopLevel top, string title, string body)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 6), Classes = { "tertiary" } };
        var confirm = new Button { Content = "Delete", Padding = new Thickness(14, 6), Classes = { "primary" } };
        cancel.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        confirm.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.Closed += (_, _) => tcs.TrySetResult(false);
        if (top is Window owner) dialog.ShowDialog(owner);
        else dialog.Show();
        return tcs.Task;
    }

    private static string FormatAssembly(Entity e) =>
        string.Join('\n', new[]
        {
            $"name             : {e.GetAttributeValue<string>("name")}",
            $"version          : {e.GetAttributeValue<string>("version")}",
            $"publickeytoken   : {e.GetAttributeValue<string>("publickeytoken")}",
            $"isolationmode    : {IsolationLabel(e.GetAttributeValue<OptionSetValue>("isolationmode")?.Value)}",
            $"sourcetype       : {SourceTypeLabel(e.GetAttributeValue<OptionSetValue>("sourcetype")?.Value)}",
            $"pluginassemblyid : {e.Id:D}",
        });

    private static string FormatType(Entity e) =>
        string.Join('\n', new[]
        {
            $"typename     : {e.GetAttributeValue<string>("typename")}",
            $"friendlyname : {e.GetAttributeValue<string>("friendlyname")}",
            $"description  : {e.GetAttributeValue<string>("description")}",
            $"plugintypeid : {e.Id:D}",
        });

    private static string FormatStep(Entity e) =>
        string.Join('\n', new[]
        {
            $"name                 : {e.GetAttributeValue<string>("name")}",
            $"stage                : {StageLabel(StageOf(e))}",
            $"mode                 : {ModeLabel(ModeOf(e))}",
            $"rank                 : {e.GetAttributeValue<int>("rank")}",
            $"filteringattributes  : {e.GetAttributeValue<string>("filteringattributes")}",
            $"sdkmessageid         : {e.GetAttributeValue<EntityReference>("sdkmessageid")?.Name}",
            $"state                : {(e.GetAttributeValue<OptionSetValue>("statecode")?.Value == 0 ? "Enabled" : "Disabled")}",
            $"sdkmessageprocessingstepid : {e.Id:D}",
        });

    private static int? StageOf(Entity e) =>
        e.GetAttributeValue<OptionSetValue>("stage")?.Value
        ?? (e.Attributes.TryGetValue("stage", out var raw) ? raw as int? : null);

    private static int? ModeOf(Entity e) =>
        e.GetAttributeValue<OptionSetValue>("mode")?.Value
        ?? (e.Attributes.TryGetValue("mode", out var raw) ? raw as int? : null);

    private static string IsolationLabel(int? v) => v switch { 1 => "None", 2 => "Sandbox", 3 => "External", _ => "(unknown)" };
    private static string SourceTypeLabel(int? v) => v switch { 0 => "Database", 1 => "Disk", 2 => "Normal", _ => "(unknown)" };
    private static string StageLabel(int? v) => v switch
    {
        0 => "Initial pre-operation (CustomApi)",
        5 => "Pre-validation",
        10 => "Pre-operation",
        20 => "Operation",
        30 => "MainOperation",
        40 => "Post-operation",
        50 => "Final post-operation (deprecated)",
        _ => v.HasValue ? $"Stage {v.Value}" : "(unknown)",
    };
    private static string ModeLabel(int? v) => v switch
    {
        0 => "Synchronous",
        1 => "Asynchronous",
        _ => v.HasValue ? $"Mode {v.Value}" : "(unknown)",
    };

    private async Task ExportCsvAsync()
    {
        if (Roots.Count == 0) return;
        var top = TopLevel.GetTopLevel(_root);
        if (top is null) return;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var file = await top.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save plugin registration inventory",
            SuggestedFileName = $"pluginregistration_{stamp}.csv",
            DefaultExtension = "csv",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("CSV (Comma-separated)") { Patterns = new[] { "*.csv" } },
            },
        });
        if (file is null) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("kind,assembly,type,step,stage,mode,state,sdkmessage,filtering_attributes,id");
        foreach (var asm in Roots)
        {
            var asmName = asm.Title;
            sb.Append("assembly,").Append(EscapeCsv(asmName)).Append(",,,,,,,,").Append(asm.Entity?.Id.ToString("D")).AppendLine();
            foreach (var t in asm.Children)
            {
                sb.Append("type,").Append(EscapeCsv(asmName)).Append(',').Append(EscapeCsv(t.Title)).Append(",,,,,,,").Append(t.Entity?.Id.ToString("D")).AppendLine();
                foreach (var s in t.Children)
                {
                    var e = s.Entity;
                    var stage = StageOf(e ?? new Entity());
                    var mode = ModeOf(e ?? new Entity());
                    var state = (e?.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? 0) == 0 ? "Enabled" : "Disabled";
                    var msg = e?.GetAttributeValue<EntityReference>("sdkmessageid")?.Name ?? string.Empty;
                    var fa = e?.GetAttributeValue<string>("filteringattributes") ?? string.Empty;
                    sb.Append("step,")
                      .Append(EscapeCsv(asmName)).Append(',')
                      .Append(EscapeCsv(t.Title)).Append(',')
                      .Append(EscapeCsv(s.Title)).Append(',')
                      .Append(EscapeCsv(StageLabel(stage))).Append(',')
                      .Append(EscapeCsv(ModeLabel(mode))).Append(',')
                      .Append(state).Append(',')
                      .Append(EscapeCsv(msg)).Append(',')
                      .Append(EscapeCsv(fa)).Append(',')
                      .Append(s.Entity?.Id.ToString("D"))
                      .AppendLine();
                }
            }
        }
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(sb.ToString());
        _statusLabel.Foreground = Brushes.ForestGreen;
        _statusLabel.Text = $"Exported inventory to {file.Name}.";
    }

    private static string EscapeCsv(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}

public enum NodeKind { Assembly, Type, Step }

public sealed class RegistrationNode
{
    public NodeKind Kind { get; set; }
    public string Glyph { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public Entity? Entity { get; set; }
    public ObservableCollection<RegistrationNode> Children { get; } = new();
}
