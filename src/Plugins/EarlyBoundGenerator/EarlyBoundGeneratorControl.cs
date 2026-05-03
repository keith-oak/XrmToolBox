// Early Bound Generator — functional port (cross-platform code-gen).
//
// Pick entities from a checklist → choose output folder + namespace →
// Generate. Produces a single C# file per entity with strongly-typed property
// wrappers (string, int, decimal, datetime, EntityReference, OptionSetValue,
// Money) plus const-string logical name + attribute names.
//
// Replaces the upstream's reliance on CrmSvcUtil.exe with a direct
// EntityMetadata read via the standard SDK (Microsoft.PowerPlatform.Dataverse.Client),
// which is cross-platform and already referenced by XrmToolBox.Extensibility.Core.

using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace DLaB.EarlyBoundGeneratorV2;

public sealed class EarlyBoundGeneratorControl : IXrmToolBoxPluginControl
{
    private readonly Grid _root;
    private readonly TextBlock _connectionLabel;
    private readonly TextBox _filterBox;
    private readonly TextBox _namespaceBox;
    private readonly TextBox _outputDirBox;
    private readonly Button _browseButton;
    private readonly Button _refreshButton;
    private readonly Button _generateButton;
    private readonly TextBlock _statusLabel;
    private readonly TextBlock _summaryLabel;
    private readonly ListBox _entityList;

    public ObservableCollection<EntityChoice> Entities { get; } = new();
    public ObservableCollection<EntityChoice> FilteredEntities { get; } = new();

    private IOrganizationService? _service;

    public event EventHandler? OnCloseTool;
    public event EventHandler? OnRequestConnection;

    public IOrganizationService? Service => _service;
    public string DisplayName => "Early Bound Generator V2";

    public EarlyBoundGeneratorControl()
    {
        _connectionLabel = new TextBlock { Text = "Not connected.", FontWeight = FontWeight.SemiBold };
        _statusLabel = new TextBlock { Text = string.Empty, FontSize = 11, Opacity = 0.7 };
        _summaryLabel = new TextBlock { Text = string.Empty, FontWeight = FontWeight.SemiBold };

        _filterBox = new TextBox { Watermark = "filter (logical name)", Width = 240 };
        _filterBox.TextChanged += (_, _) => ApplyFilter();

        _namespaceBox = new TextBox { Text = "Generated.Dataverse", Width = 260 };
        _outputDirBox = new TextBox { Watermark = "/path/to/output", Width = 360 };

        _browseButton = new Button { Content = "Browse…", Padding = new Thickness(12, 6), Classes = { "tertiary" } };
        _browseButton.Click += async (_, _) => await PickFolderAsync();

        _refreshButton = new Button { Content = "Reload entities", Padding = new Thickness(12, 6), IsEnabled = false, Classes = { "tertiary" } };
        _refreshButton.Click += async (_, _) => await ReloadAsync();

        _generateButton = new Button { Content = "Generate", Padding = new Thickness(14, 6), IsEnabled = false, Classes = { "primary" } };
        _generateButton.Click += async (_, _) => await GenerateAsync();

        _entityList = new ListBox
        {
            ItemsSource = FilteredEntities,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            SelectionMode = SelectionMode.Multiple,
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_entityList, ScrollBarVisibility.Auto);
        _entityList.ItemTemplate = new FuncDataTemplate<EntityChoice>((c, _) =>
        {
            if (c is null) return new TextBlock();
            var cb = new CheckBox
            {
                Content = $"{c.LogicalName}  {(string.IsNullOrEmpty(c.DisplayName) ? string.Empty : "· " + c.DisplayName)}",
                IsChecked = c.IsSelected,
            };
            cb.IsCheckedChanged += (_, _) => c.IsSelected = cb.IsChecked == true;
            return cb;
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
                        new TextBlock { Text = "Early Bound Generator V2", FontSize = 20, FontWeight = FontWeight.SemiBold },
                        _connectionLabel,
                    },
                },
                _refreshButton,
                _generateButton,
            },
        };
        Grid.SetColumn(_refreshButton, 1);
        Grid.SetColumn(_generateButton, 2);
        _refreshButton.Margin = new Thickness(0, 0, 8, 0);

        var settingsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                LabelStack("Namespace", _namespaceBox),
                LabelStack("Output folder", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _outputDirBox, _browseButton } }),
            },
        };

        var filterRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { LabelStack("Filter", _filterBox) },
        };

        var listBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Child = _entityList,
        };

        _root = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"),
        };
        headerRow.Margin = new Thickness(0, 0, 0, 14);
        settingsRow.Margin = new Thickness(0, 0, 0, 8);
        filterRow.Margin = new Thickness(0, 0, 0, 8);
        var summaryRow = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 10), Children = { _summaryLabel, _statusLabel } };
        Grid.SetRow(headerRow, 0);
        Grid.SetRow(settingsRow, 1);
        Grid.SetRow(filterRow, 2);
        Grid.SetRow(summaryRow, 3);
        Grid.SetRow(listBorder, 4);
        _root.Children.Add(headerRow);
        _root.Children.Add(settingsRow);
        _root.Children.Add(filterRow);
        _root.Children.Add(summaryRow);
        _root.Children.Add(listBorder);
    }

    public object GetView() => _root;

    public void ClosingPlugin(PluginCloseInfo info) { }

    public void UpdateConnection(IOrganizationService newService, ConnectionDetail connectionDetail, string actionName = "", object? parameter = null)
    {
        _service = newService;
        _connectionLabel.Text = $"Connected to {connectionDetail.OrganizationFriendlyName ?? connectionDetail.Url}";
        _connectionLabel.Foreground = Brushes.ForestGreen;
        _refreshButton.IsEnabled = true;
        _generateButton.IsEnabled = true;
        _ = ReloadAsync();
    }

    public void ResetConnection()
    {
        _service = null;
        _connectionLabel.Text = "Not connected.";
        _connectionLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _refreshButton.IsEnabled = false;
        _generateButton.IsEnabled = false;
        Entities.Clear();
        FilteredEntities.Clear();
        _summaryLabel.Text = string.Empty;
        _statusLabel.Text = string.Empty;
    }

    private async Task ReloadAsync()
    {
        if (_service is null) return;
        _refreshButton.IsEnabled = false;
        _statusLabel.Text = "Loading entity metadata…";
        Entities.Clear();
        FilteredEntities.Clear();
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
                    .OrderBy(em => em.LogicalName)
                    .ToList();
            });
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var em in ents)
                {
                    Entities.Add(new EntityChoice
                    {
                        LogicalName = em.LogicalName ?? string.Empty,
                        DisplayName = em.DisplayName?.UserLocalizedLabel?.Label ?? string.Empty,
                        IsSelected = false,
                    });
                }
                ApplyFilter();
                _summaryLabel.Text = $"{Entities.Count} entity(ies). Tick the ones you want, set output folder, then Generate.";
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

    private void ApplyFilter()
    {
        FilteredEntities.Clear();
        var f = (_filterBox.Text ?? string.Empty).Trim().ToLowerInvariant();
        foreach (var e in Entities)
        {
            if (f.Length == 0 || e.LogicalName.Contains(f, StringComparison.OrdinalIgnoreCase) || e.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase))
            {
                FilteredEntities.Add(e);
            }
        }
    }

    private async Task PickFolderAsync()
    {
        var top = TopLevel.GetTopLevel(_root);
        if (top is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose output folder" });
        if (folders.Count > 0)
        {
            _outputDirBox.Text = folders[0].Path.LocalPath;
        }
    }

    private async Task GenerateAsync()
    {
        if (_service is null) return;
        var selected = Entities.Where(e => e.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = "Tick at least one entity.";
            return;
        }
        var outDir = (_outputDirBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(outDir))
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = "Pick an output folder.";
            return;
        }
        // Each run lands in its own timestamped subfolder so successive runs
        // never overwrite previous output, and side-by-side diffs are trivial.
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var runDir = Path.Combine(outDir, $"earlybound_{stamp}");
        try { Directory.CreateDirectory(runDir); }
        catch (Exception ex)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = $"Cannot create output folder: {ex.Message}";
            return;
        }

        var ns = (_namespaceBox.Text ?? "Generated.Dataverse").Trim();
        _generateButton.IsEnabled = false;
        _statusLabel.Foreground = new SolidColorBrush(Color.FromArgb(180, 90, 90, 95));
        _statusLabel.Text = $"Fetching attributes for {selected.Count} entity(ies)…";

        try
        {
            int written = 0;
            foreach (var choice in selected)
            {
                var em = await Task.Run(() => ((RetrieveEntityResponse)_service.Execute(new RetrieveEntityRequest
                {
                    LogicalName = choice.LogicalName,
                    EntityFilters = EntityFilters.Attributes,
                    RetrieveAsIfPublished = true,
                })).EntityMetadata);
                var src = EmitClass(em, ns);
                var className = ToPascal(em.LogicalName ?? "Entity");
                var path = Path.Combine(runDir, className + ".cs");
                await File.WriteAllTextAsync(path, src);
                written++;
                _statusLabel.Text = $"Generated {written}/{selected.Count}…";
                await Task.Yield();
            }
            _summaryLabel.Text = $"Generated {written} file(s) to {runDir}.";
            _statusLabel.Foreground = Brushes.ForestGreen;
            _statusLabel.Text = "Done.";
        }
        catch (Exception ex)
        {
            _statusLabel.Foreground = Brushes.OrangeRed;
            _statusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _generateButton.IsEnabled = true;
        }
    }

    private static string EmitClass(EntityMetadata em, string ns)
    {
        var sb = new StringBuilder();
        var className = ToPascal(em.LogicalName ?? "Entity");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by Early Bound Generator (macOS port).");
        sb.AppendLine("using System;");
        sb.AppendLine("using Microsoft.Xrm.Sdk;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {className} : Entity");
        sb.AppendLine("{");
        sb.AppendLine($"    public const string EntityLogicalName = \"{em.LogicalName}\";");
        sb.AppendLine();
        sb.AppendLine($"    public {className}() : base(EntityLogicalName) {{ }}");
        sb.AppendLine($"    public {className}(Guid id) : base(EntityLogicalName, id) {{ }}");
        sb.AppendLine();
        sb.AppendLine("    public static class Fields");
        sb.AppendLine("    {");
        foreach (var a in (em.Attributes ?? Array.Empty<AttributeMetadata>()).OrderBy(a => a.LogicalName))
        {
            if (string.IsNullOrEmpty(a.LogicalName)) continue;
            if (!string.IsNullOrEmpty(a.AttributeOf)) continue;
            sb.AppendLine($"        public const string {ToPascal(a.LogicalName)} = \"{a.LogicalName}\";");
        }
        sb.AppendLine("    }");
        sb.AppendLine();
        foreach (var a in (em.Attributes ?? Array.Empty<AttributeMetadata>()).OrderBy(a => a.LogicalName))
        {
            if (string.IsNullOrEmpty(a.LogicalName)) continue;
            if (!string.IsNullOrEmpty(a.AttributeOf)) continue;
            var net = MapAttribute(a);
            if (net is null) continue;
            var prop = ToPascal(a.LogicalName);
            sb.AppendLine($"    public {net}? {prop}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get => GetAttributeValue<{net}?>(\"{a.LogicalName}\");");
            sb.AppendLine($"        set => this[\"{a.LogicalName}\"] = value;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? MapAttribute(AttributeMetadata a) => a.AttributeType switch
    {
        AttributeTypeCode.String or AttributeTypeCode.Memo => "string",
        AttributeTypeCode.Integer => "int",
        AttributeTypeCode.BigInt => "long",
        AttributeTypeCode.Decimal => "decimal",
        AttributeTypeCode.Double => "double",
        AttributeTypeCode.Boolean => "bool",
        AttributeTypeCode.DateTime => "DateTime",
        AttributeTypeCode.Money => "Money",
        AttributeTypeCode.Lookup or AttributeTypeCode.Customer or AttributeTypeCode.Owner => "EntityReference",
        AttributeTypeCode.Picklist or AttributeTypeCode.State or AttributeTypeCode.Status => "OptionSetValue",
        AttributeTypeCode.Uniqueidentifier => "Guid",
        _ => null,
    };

    private static string ToPascal(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var parts = s.Replace('-', '_').Split('_', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(p[0]));
            if (p.Length > 1) sb.Append(p[1..]);
        }
        var r = sb.ToString();
        return char.IsLetter(r[0]) ? r : "_" + r;
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

public sealed class EntityChoice
{
    public string LogicalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
