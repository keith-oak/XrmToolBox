// FetchXML Builder — functional port (entity tree + raw editor + run + results).
//
// Two-pane: left is a tree of entities (lazy-load attributes on expand), right
// is a tabbed editor (FetchXML | Results). Run executes the editor's FetchXML
// against the connected environment and dumps results into the grid as
// attribute/value rows. Skips SQL / OData / Power* / JSON / signature / SQL-4-CDS
// — those are separate panes in the original.

using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace Rappen.XTB.FetchXmlBuilder;

public sealed class FetchXmlBuilderControl : IXrmToolBoxPluginControl
{
    private const string DefaultFetch = """
        <fetch top="50">
          <entity name="account">
            <attribute name="name" />
            <attribute name="accountid" />
          </entity>
        </fetch>
        """;

    private readonly Grid _root;
    private readonly TextBlock _connectionLabel;
    private readonly TextBox _fetchEditor;
    private readonly Button _runButton;
    private readonly Button _refreshMetaButton;
    private readonly TextBlock _statusLabel;
    private readonly TextBlock _summaryLabel;
    private readonly TreeView _tree;
    private readonly ListBox _resultsList;
    private readonly TextBox _entityFilter;
    private readonly List<EntityNode> _allEntities = new();
    private readonly TabControl _rightTabs;
    private readonly TabItem _resultsTab;
    private readonly Button _copyCsvButton;
    private readonly Button _saveCsvButton;
    private readonly List<string[]> _resultsRows = new();
    private readonly List<string> _resultsHeader = new();

    public ObservableCollection<EntityNode> EntityNodes { get; } = new();
    public ObservableCollection<ResultRow> ResultRows { get; } = new();

    private IOrganizationService? _service;

    public event EventHandler? OnCloseTool;
    public event EventHandler? OnRequestConnection;

    public IOrganizationService? Service => _service;
    public string DisplayName => "FetchXML Builder";

    public FetchXmlBuilderControl()
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
            TextWrapping = TextWrapping.NoWrap,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_fetchEditor, ScrollBarVisibility.Auto);
        _fetchEditor.TextChanged += (_, _) => SyncCheckmarksFromFetch();

        _runButton = new Button
        {
            Content = "Run",
            Padding = new Thickness(14, 6),
            IsEnabled = false,
            Classes = { "primary" },
        };
        _runButton.Click += async (_, _) => await RunAsync();

        _refreshMetaButton = new Button
        {
            Content = "Reload entities",
            Padding = new Thickness(12, 6),
            IsEnabled = false,
            Classes = { "tertiary" },
        };
        _refreshMetaButton.Click += async (_, _) => await ReloadEntitiesAsync();

        _entityFilter = new TextBox { Watermark = "filter entities…", Margin = new Thickness(0, 0, 0, 6) };
        _entityFilter.TextChanged += (_, _) => ApplyEntityFilter();

        _tree = new TreeView { ItemsSource = EntityNodes, Background = Brushes.Transparent };
        _tree.ItemTemplate = new FuncTreeDataTemplate<EntityNode>(
            (node, _) =>
            {
                if (node is null) return new TextBlock();
                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                var check = new TextBlock
                {
                    Text = node.CheckGlyph,
                    MinWidth = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0x8B, 0xF6)),
                    FontWeight = FontWeight.SemiBold,
                };
                stack.Children.Add(check);
                var primary = new TextBlock
                {
                    Text = node.LogicalName,
                    FontWeight = node.IsAttribute ? FontWeight.Normal : FontWeight.Medium,
                };
                if (node.NameBrush is { } b) primary.Foreground = b;
                stack.Children.Add(primary);
                if (!string.IsNullOrEmpty(node.DisplayName) && node.DisplayName != node.LogicalName)
                {
                    stack.Children.Add(new TextBlock { Text = node.DisplayName, Opacity = 0.55, FontSize = 11 });
                }
                // Manual subscribe so visual updates without disturbing the TreeView's item realisation.
                node.PropertyChanged += (_, _) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        check.Text = node.CheckGlyph;
                        if (node.NameBrush is { } nb) primary.Foreground = nb;
                        else primary.ClearValue(TextBlock.ForegroundProperty);
                    });
                };
                return stack;
            },
            n => n.Children);
        _tree.SelectionChanged += (_, _) =>
        {
            try
            {
                if (_tree.SelectedItem is EntityNode node)
                {
                    if (!node.IsAttribute && node.Children.Count == 0)
                    {
                        _ = LoadAttributesAsync(node);
                    }
                    else if (node.IsAttribute)
                    {
                        ToggleAttribute(node);
                    }
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Foreground = Brushes.OrangeRed;
                _statusLabel.Text = $"UI error: {ex.Message}";
            }
        };
        _tree.DoubleTapped += (_, _) =>
        {
            if (_tree.SelectedItem is EntityNode { IsAttribute: false } entity)
            {
                ScaffoldEntity(entity);
            }
        };

        _resultsList = new ListBox
        {
            ItemsSource = ResultRows,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_resultsList, ScrollBarVisibility.Auto);
        _resultsList.ItemTemplate = new FuncDataTemplate<ResultRow>((row, _) =>
        {
            if (row is null) return new TextBlock();
            var stack = new StackPanel { Spacing = 2, Margin = new Thickness(2, 6) };
            stack.Children.Add(new TextBlock { Text = row.Title, FontWeight = FontWeight.Medium });
            stack.Children.Add(new TextBlock { Text = row.Body, FontSize = 11, Opacity = 0.75, FontFamily = "ui-monospace,SFMono-Regular,Menlo,monospace", TextWrapping = TextWrapping.Wrap });
            return stack;
        });

        var headerRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = "FetchXML Builder", FontSize = 20, FontWeight = FontWeight.SemiBold },
                        _connectionLabel,
                    },
                },
                _refreshMetaButton,
                _runButton,
            },
        };
        Grid.SetColumn(_refreshMetaButton, 1);
        Grid.SetColumn(_runButton, 2);
        _refreshMetaButton.Margin = new Thickness(0, 0, 8, 0);

        DockPanel.SetDock(_entityFilter, Dock.Top);
        var treeDock = new DockPanel { LastChildFill = true };
        treeDock.Children.Add(_entityFilter);
        treeDock.Children.Add(new ScrollViewer { Content = _tree, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
        var treeBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Child = treeDock,
        };

        var editorTab = new TabItem { Header = "FetchXML", Content = new ScrollViewer { Content = _fetchEditor } };

        _copyCsvButton = new Button
        {
            Content = "Copy CSV",
            Padding = new Thickness(10, 4),
            IsEnabled = false,
            Classes = { "tertiary" },
        };
        _copyCsvButton.Click += async (_, _) => await CopyCsvAsync();
        _saveCsvButton = new Button
        {
            Content = "Save CSV…",
            Padding = new Thickness(10, 4),
            IsEnabled = false,
            Classes = { "tertiary" },
        };
        _saveCsvButton.Click += async (_, _) => await SaveCsvAsync();

        var resultsHeaderRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 6),
            Children = { _summaryLabel, _statusLabel, _copyCsvButton, _saveCsvButton },
        };
        DockPanel.SetDock(resultsHeaderRow, Dock.Top);
        var resultsDock = new DockPanel { LastChildFill = true };
        resultsDock.Children.Add(resultsHeaderRow);
        resultsDock.Children.Add(_resultsList);
        _resultsTab = new TabItem
        {
            Header = "Results",
            Content = resultsDock,
        };
        _rightTabs = new TabControl { Items = { editorTab, _resultsTab } };

        var rightBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Child = _rightTabs,
        };

        var splitter = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,Auto,3*") };
        var gap = new Border { Width = 12 };
        Grid.SetColumn(treeBorder, 0);
        Grid.SetColumn(gap, 1);
        Grid.SetColumn(rightBorder, 2);
        splitter.Children.Add(treeBorder);
        splitter.Children.Add(gap);
        splitter.Children.Add(rightBorder);

        _root = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        headerRow.Margin = new Thickness(0, 0, 0, 14);
        Grid.SetRow(headerRow, 0);
        Grid.SetRow(splitter, 1);
        _root.Children.Add(headerRow);
        _root.Children.Add(splitter);
    }

    public object GetView() => _root;

    public void ClosingPlugin(PluginCloseInfo info) { }

    public void UpdateConnection(IOrganizationService newService, ConnectionDetail connectionDetail, string actionName = "", object? parameter = null)
    {
        _service = newService;
        _connectionLabel.Text = $"Connected to {connectionDetail.OrganizationFriendlyName ?? connectionDetail.Url}";
        _connectionLabel.Foreground = Brushes.ForestGreen;
        _runButton.IsEnabled = true;
        _refreshMetaButton.IsEnabled = true;
        _ = ReloadEntitiesAsync();
    }

    public void ResetConnection()
    {
        _service = null;
        _connectionLabel.Text = "Not connected.";
        _connectionLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _runButton.IsEnabled = false;
        _refreshMetaButton.IsEnabled = false;
        _allEntities.Clear();
        EntityNodes.Clear();
        ResultRows.Clear();
        _summaryLabel.Text = string.Empty;
        _statusLabel.Text = string.Empty;
    }

    private async Task ReloadEntitiesAsync()
    {
        if (_service is null) return;
        _refreshMetaButton.IsEnabled = false;
        _statusLabel.Text = "Loading entity metadata…";
        EntityNodes.Clear();
        try
        {
            var ents = await Task.Run(() =>
            {
                var resp = (RetrieveAllEntitiesResponse)_service.Execute(new RetrieveAllEntitiesRequest
                {
                    EntityFilters = EntityFilters.Entity,
                    RetrieveAsIfPublished = true,
                });
                return resp.EntityMetadata
                    .Where(em => em.IsValidForAdvancedFind ?? false)
                    .OrderBy(em => em.LogicalName)
                    .ToList();
            });
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _allEntities.Clear();
                foreach (var em in ents)
                {
                    _allEntities.Add(new EntityNode
                    {
                        LogicalName = em.LogicalName ?? string.Empty,
                        DisplayName = em.DisplayName?.UserLocalizedLabel?.Label ?? string.Empty,
                        IsAttribute = false,
                    });
                }
                ApplyEntityFilter();
                _statusLabel.Text = $"{_allEntities.Count} entity(ies) — double-click to scaffold, type above to filter.";
            });
        }
        catch (Exception ex)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _refreshMetaButton.IsEnabled = true;
        }
    }

    private void ApplyEntityFilter()
    {
        var f = (_entityFilter.Text ?? string.Empty).Trim();
        EntityNodes.Clear();
        IEnumerable<EntityNode> matches = _allEntities;
        if (f.Length > 0)
        {
            matches = _allEntities.Where(n =>
                n.LogicalName.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                n.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var n in matches) EntityNodes.Add(n);
    }

    private async Task LoadAttributesAsync(EntityNode entityNode)
    {
        if (_service is null) return;
        try
        {
            var attrs = await Task.Run(() =>
            {
                var resp = (RetrieveEntityResponse)_service.Execute(new RetrieveEntityRequest
                {
                    LogicalName = entityNode.LogicalName,
                    EntityFilters = EntityFilters.Attributes,
                    RetrieveAsIfPublished = true,
                });
                return resp.EntityMetadata.Attributes
                    .Where(a => (a.IsValidForRead ?? false) && string.IsNullOrEmpty(a.AttributeOf))
                    .OrderBy(a => a.LogicalName)
                    .ToList();
            });
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var a in attrs)
                {
                    entityNode.Children.Add(new EntityNode
                    {
                        LogicalName = a.LogicalName ?? string.Empty,
                        DisplayName = a.DisplayName?.UserLocalizedLabel?.Label ?? string.Empty,
                        IsAttribute = true,
                        ParentEntity = entityNode.LogicalName,
                    });
                }
                SyncCheckmarksFromFetch();
            });
        }
        catch (Exception ex)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = $"Error loading {entityNode.LogicalName}: {ex.Message}";
        }
    }

    private void ScaffoldEntity(EntityNode entity)
    {
        _suppressEditorSync = true;
        _fetchEditor.Text = $"<fetch top=\"50\">\n  <entity name=\"{entity.LogicalName}\">\n    <attribute name=\"{entity.LogicalName}id\" />\n  </entity>\n</fetch>";
        _suppressEditorSync = false;
        SyncCheckmarksFromFetch();
    }

    private void ToggleAttribute(EntityNode attr)
    {
        var doc = TryParseFetch();
        if (doc?.Root is null)
        {
            // Fall back to scaffold of the parent entity if the editor is unparseable.
            if (!string.IsNullOrEmpty(attr.ParentEntity))
            {
                _suppressEditorSync = true;
                _fetchEditor.Text = $"<fetch top=\"50\">\n  <entity name=\"{attr.ParentEntity}\">\n    <attribute name=\"{attr.LogicalName}\" />\n  </entity>\n</fetch>";
                _suppressEditorSync = false;
                SyncCheckmarksFromFetch();
            }
            return;
        }

        var entityElem = doc.Root.Element("entity");
        if (entityElem is null)
        {
            entityElem = new System.Xml.Linq.XElement("entity", new System.Xml.Linq.XAttribute("name", attr.ParentEntity ?? string.Empty));
            doc.Root.Add(entityElem);
        }
        else if (!string.IsNullOrEmpty(attr.ParentEntity) &&
                 !string.Equals((string?)entityElem.Attribute("name"), attr.ParentEntity, StringComparison.OrdinalIgnoreCase))
        {
            // Cross-entity toggle: rebuild fetch around the new entity rather than
            // pollute the previous one with foreign attributes (which Dataverse
            // would reject anyway).
            _suppressEditorSync = true;
            _fetchEditor.Text = $"<fetch top=\"50\">\n  <entity name=\"{attr.ParentEntity}\">\n    <attribute name=\"{attr.LogicalName}\" />\n  </entity>\n</fetch>";
            _suppressEditorSync = false;
            SyncCheckmarksFromFetch();
            return;
        }

        var existing = entityElem.Elements("attribute")
            .FirstOrDefault(x => string.Equals((string?)x.Attribute("name"), attr.LogicalName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Remove();
        }
        else
        {
            // Insert before the first non-attribute child (filter / link-entity / order)
            // so attributes stay grouped at the top.
            var firstNonAttr = entityElem.Elements()
                .FirstOrDefault(x => !string.Equals(x.Name.LocalName, "attribute", StringComparison.OrdinalIgnoreCase));
            var newAttr = new System.Xml.Linq.XElement("attribute", new System.Xml.Linq.XAttribute("name", attr.LogicalName));
            if (firstNonAttr is not null)
                firstNonAttr.AddBeforeSelf(newAttr);
            else
                entityElem.Add(newAttr);
        }

        _suppressEditorSync = true;
        _fetchEditor.Text = SerializeFetch(doc);
        _suppressEditorSync = false;
        SyncCheckmarksFromFetch();
    }

    private System.Xml.Linq.XDocument? TryParseFetch()
    {
        try { return System.Xml.Linq.XDocument.Parse(_fetchEditor.Text ?? string.Empty); }
        catch { return null; }
    }

    private static string SerializeFetch(System.Xml.Linq.XDocument doc)
    {
        var settings = new System.Xml.XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = System.Xml.NewLineHandling.Replace,
        };
        var sb = new StringBuilder();
        using (var writer = System.Xml.XmlWriter.Create(sb, settings))
        {
            doc.Save(writer);
        }
        return sb.ToString();
    }

    private bool _suppressEditorSync;

    private void SyncCheckmarksFromFetch()
    {
        if (_suppressEditorSync) return;
        var doc = TryParseFetch();
        var entityName = doc?.Root?.Element("entity")?.Attribute("name")?.Value;
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (doc?.Root?.Element("entity") is { } ee)
        {
            foreach (var a in ee.Elements("attribute"))
            {
                var n = (string?)a.Attribute("name");
                if (!string.IsNullOrEmpty(n)) present.Add(n!);
            }
        }
        foreach (var ent in _allEntities)
        {
            var thisEntityActive = string.Equals(ent.LogicalName, entityName, StringComparison.OrdinalIgnoreCase);
            foreach (var ch in ent.Children)
            {
                ch.IsInFetch = thisEntityActive && present.Contains(ch.LogicalName);
            }
        }
    }

    private async Task RunAsync()
    {
        if (_service is null) return;
        _runButton.IsEnabled = false;
        ResultRows.Clear();
        _resultsRows.Clear();
        _resultsHeader.Clear();
        _copyCsvButton.IsEnabled = false;
        _saveCsvButton.IsEnabled = false;
        _summaryLabel.Text = string.Empty;
        _statusLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _statusLabel.Text = "Running query…";
        try
        {
            var fetch = _fetchEditor.Text ?? string.Empty;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var coll = await Task.Run(() => _service.RetrieveMultiple(new FetchExpression(fetch)));
            sw.Stop();

            // Build the union of attribute keys for stable CSV columns.
            var keys = new List<string>();
            var keySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in coll.Entities)
                foreach (var k in e.Attributes.Keys)
                    if (keySet.Add(k)) keys.Add(k);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            _resultsHeader.AddRange(keys);

            foreach (var e in coll.Entities)
            {
                var sb = new StringBuilder();
                var rowValues = new string[keys.Count];
                for (int i = 0; i < keys.Count; i++)
                {
                    var v = e.Contains(keys[i]) ? FormatValue(e[keys[i]]) : string.Empty;
                    rowValues[i] = v;
                    sb.Append(keys[i]).Append('=').Append(v).Append("  ");
                }
                _resultsRows.Add(rowValues);
                ResultRows.Add(new ResultRow
                {
                    Title = $"{e.LogicalName}  {e.Id:D}",
                    Body = sb.ToString(),
                });
            }
            _summaryLabel.Text = $"{coll.Entities.Count} record(s) in {sw.ElapsedMilliseconds} ms.";
            _statusLabel.Text = string.Empty;
            _copyCsvButton.IsEnabled = _resultsRows.Count > 0;
            _saveCsvButton.IsEnabled = _resultsRows.Count > 0;
            _rightTabs.SelectedItem = _resultsTab;
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

    private string BuildCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', _resultsHeader.Select(EscapeCsv)));
        foreach (var row in _resultsRows)
            sb.AppendLine(string.Join(',', row.Select(EscapeCsv)));
        return sb.ToString();
    }

    private static string EscapeCsv(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private async Task CopyCsvAsync()
    {
        var top = TopLevel.GetTopLevel(_root);
        if (top?.Clipboard is null) return;
        await top.Clipboard.SetTextAsync(BuildCsv());
        _statusLabel.Foreground = Brushes.ForestGreen;
        _statusLabel.Text = $"Copied {_resultsRows.Count} row(s) to clipboard.";
    }

    private async Task SaveCsvAsync()
    {
        var top = TopLevel.GetTopLevel(_root);
        if (top is null) return;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var entityHint = TryParseFetch()?.Root?.Element("entity")?.Attribute("name")?.Value ?? "fetchxml";
        var suggested = $"{entityHint}_{stamp}.csv";
        var file = await top.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save FetchXML results",
            SuggestedFileName = suggested,
            DefaultExtension = "csv",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("CSV (Comma-separated)") { Patterns = new[] { "*.csv" } },
            },
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(BuildCsv());
        _statusLabel.Foreground = Brushes.ForestGreen;
        _statusLabel.Text = $"Saved {_resultsRows.Count} row(s) to {file.Name}.";
    }

    private static string FormatValue(object? v) => v switch
    {
        null => "(null)",
        EntityReference er => $"{er.LogicalName}#{er.Id:D}",
        OptionSetValue os => $"OS({os.Value})",
        Money m => $"${m.Value}",
        AliasedValue av => FormatValue(av.Value),
        _ => v.ToString() ?? string.Empty,
    };
}

public sealed class EntityNode : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isInFetch;

    public string LogicalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsAttribute { get; set; }
    public string? ParentEntity { get; set; }
    public ObservableCollection<EntityNode> Children { get; } = new();

    public bool IsInFetch
    {
        get => _isInFetch;
        set
        {
            if (_isInFetch == value) return;
            _isInFetch = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsInFetch)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CheckGlyph)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(NameBrush)));
        }
    }

    public string CheckGlyph => IsAttribute && IsInFetch ? "✓" : string.Empty;

    public Avalonia.Media.IBrush? NameBrush =>
        IsAttribute && IsInFetch
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x4F, 0x8B, 0xF6))
            : null;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public sealed class ResultRow
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
