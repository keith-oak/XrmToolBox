using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using ReactiveUI;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;
using XrmToolBox.MacOS.Connection;
using XrmToolBox.MacOS.Plugins;
using XrmToolBox.MacOS.Settings;

namespace XrmToolBox.MacOS.ViewModels;

public enum NavSection
{
    Home,
    Tools,
    Plugins,
    Environments,
    Connections,
    Settings,
    About,
}

public sealed record NavItem(NavSection Section, string Label, string Icon);

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly PluginManager _pluginManager;
    private readonly DataverseConnectionService _connectionService;
    private readonly ConnectionCatalogueStore _catalogue;
    public SettingsService SettingsService { get; }
    public ConnectionCatalogueStore Catalogue => _catalogue;
    public DataverseConnectionService ConnectionService => _connectionService;

    private string _connectionStatus = "Not connected";
    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => this.RaiseAndSetIfChanged(ref _connectionStatus, value);
    }

    private string _dataverseUrl;
    public string DataverseUrl
    {
        get => _dataverseUrl;
        set => this.RaiseAndSetIfChanged(ref _dataverseUrl, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    private bool _isConnecting;
    public bool IsConnecting
    {
        get => _isConnecting;
        private set => this.RaiseAndSetIfChanged(ref _isConnecting, value);
    }

    private readonly ObservableAsPropertyHelper<string> _connectButtonCaption;
    public string ConnectButtonCaption => _connectButtonCaption.Value;

    private OpenedPluginViewModel? _activePlugin;
    public OpenedPluginViewModel? ActivePlugin
    {
        get => _activePlugin;
        set => this.RaiseAndSetIfChanged(ref _activePlugin, value);
    }

    private PluginEntry? _selectedPlugin;
    public PluginEntry? SelectedPlugin
    {
        get => _selectedPlugin;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPlugin, value);
            if (value is not null)
            {
                OpenPlugin(value);
            }
        }
    }

    private bool _isCommandPaletteOpen;
    public bool IsCommandPaletteOpen
    {
        get => _isCommandPaletteOpen;
        set => this.RaiseAndSetIfChanged(ref _isCommandPaletteOpen, value);
    }

    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => this.RaiseAndSetIfChanged(ref _isSettingsOpen, value);
    }

    private bool _isAboutOpen;
    public bool IsAboutOpen
    {
        get => _isAboutOpen;
        set => this.RaiseAndSetIfChanged(ref _isAboutOpen, value);
    }

    private bool _isStoreOpen;
    public bool IsStoreOpen
    {
        get => _isStoreOpen;
        set => this.RaiseAndSetIfChanged(ref _isStoreOpen, value);
    }

    public PluginStoreViewModel Store { get; } = new();

    public ObservableCollection<NavItem> NavItems { get; } = new()
    {
        new(NavSection.Home, "Home", "🏠"),
        new(NavSection.Tools, "Tools", "🧰"),
        new(NavSection.Plugins, "Manage Tools", "🧩"),
        new(NavSection.Environments, "Environments", "🌐"),
        new(NavSection.Connections, "Connections", "🔗"),
        new(NavSection.Settings, "Settings", "⚙️"),
        new(NavSection.About, "About", "ℹ️"),
    };

    private NavItem _selectedNav;
    public NavItem SelectedNav
    {
        get => _selectedNav;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedNav, value);
            this.RaisePropertyChanged(nameof(IsHomeActive));
            this.RaisePropertyChanged(nameof(IsToolsActive));
            this.RaisePropertyChanged(nameof(IsPluginsActive));
            this.RaisePropertyChanged(nameof(IsEnvironmentsActive));
            this.RaisePropertyChanged(nameof(IsConnectionsActive));
            this.RaisePropertyChanged(nameof(IsSettingsActive));
            this.RaisePropertyChanged(nameof(IsAboutActive));
        }
    }

    public bool IsHomeActive => SelectedNav?.Section == NavSection.Home;
    public bool IsToolsActive => SelectedNav?.Section == NavSection.Tools;
    public bool IsPluginsActive => SelectedNav?.Section == NavSection.Plugins;
    public bool IsEnvironmentsActive => SelectedNav?.Section == NavSection.Environments;
    public bool IsConnectionsActive => SelectedNav?.Section == NavSection.Connections;
    public bool IsSettingsActive => SelectedNav?.Section == NavSection.Settings;
    public bool IsAboutActive => SelectedNav?.Section == NavSection.About;

    public ObservableCollection<PluginEntry> AvailablePlugins { get; } = new();
    public ObservableCollection<OpenedPluginViewModel> OpenedPlugins { get; } = new();
    public ObservableCollection<RecentConnection> RecentConnections { get; } = new();

    /// <summary>Every catalogue connection flattened for picker UIs.</summary>
    public ObservableCollection<ConnectionDetail> CatalogueConnections { get; } = new();

    /// <summary>Catalogue files (groups) for the management pane.</summary>
    public ObservableCollection<ConnectionFile> CatalogueFiles { get; } = new();

    private ConnectionDetail? _selectedCatalogueDetail;
    public ConnectionDetail? SelectedCatalogueDetail
    {
        get => _selectedCatalogueDetail;
        set => this.RaiseAndSetIfChanged(ref _selectedCatalogueDetail, value);
    }

    private ConnectionFile? _selectedCatalogueFile;
    public ConnectionFile? SelectedCatalogueFile
    {
        get => _selectedCatalogueFile;
        set => this.RaiseAndSetIfChanged(ref _selectedCatalogueFile, value);
    }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<PluginEntry, Unit> OpenPluginCommand { get; }
    public ReactiveCommand<OpenedPluginViewModel, Unit> CloseTabCommand { get; }
    public ReactiveCommand<RecentConnection, Unit> SelectRecentCommand { get; }
    public ReactiveCommand<RecentConnection, Unit> ForgetRecentCommand { get; }
    public ReactiveCommand<ConnectionDetail, Unit> ConnectToCatalogueCommand { get; }
    public ReactiveCommand<ConnectionDetail, Unit> ForgetCatalogueCommand { get; }
    public ReactiveCommand<ConnectionDetail, Unit> SetDefaultCommand { get; }
    public ReactiveCommand<string, Unit> NewConnectionFileCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportFromXrmToolBoxCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommandPaletteCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleAboutCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleStoreCommand { get; }
    public ReactiveCommand<Unit, Unit> ReloadPluginsCommand { get; }
    public ReactiveCommand<PluginEntry, Unit> UninstallPluginCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenPluginsFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> QuitCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseAllOverlaysCommand { get; }

    public ReactiveCommand<Unit, Unit> QuickOpenPowerPlatformCommand { get; }
    public ReactiveCommand<Unit, Unit> QuickRunPacCommand { get; }
    public ReactiveCommand<Unit, Unit> QuickBrowseEnvironmentCommand { get; }
    public ReactiveCommand<Unit, Unit> QuickImportSolutionCommand { get; }

    public ObservableCollection<RecentTool> RecentTools { get; } = new();

    /// <summary>Function the View can hook to actually show file pickers / device-code dialogs.</summary>
    public Func<string?, Task<string?>>? OpenFileDialogAsync { get; set; }
    public Func<DeviceCodePrompt, Task>? ShowDeviceCodeAsync { get; set; }

    private string _themeMode = "auto";
    public string ThemeMode
    {
        get => _themeMode;
        set
        {
            if (_themeMode == value) return;
            this.RaiseAndSetIfChanged(ref _themeMode, value);
            this.RaisePropertyChanged(nameof(IsThemeAuto));
            this.RaisePropertyChanged(nameof(IsThemeLight));
            this.RaisePropertyChanged(nameof(IsThemeDark));
            ApplyTheme(value);
            SettingsService.Current.ThemeOverride = value;
            SettingsService.Save();
        }
    }

    public bool IsThemeAuto => ThemeMode == "auto";
    public bool IsThemeLight => ThemeMode == "light";
    public bool IsThemeDark => ThemeMode == "dark";

    public ReactiveCommand<string, Unit> SetThemeCommand { get; }

    public MainWindowViewModel(
        PluginManager pluginManager,
        SettingsService settings,
        ConnectionCatalogueStore catalogue,
        SecretStore secrets)
    {
        _pluginManager = pluginManager;
        SettingsService = settings;
        _catalogue = catalogue;
        _connectionService = new DataverseConnectionService(secrets);

        // One-time migration of legacy flat RecentConnections into the catalogue.
        _catalogue.MigrateFromLegacyRecent(settings.Current.RecentConnections);

        _selectedNav = NavItems[0];
        _dataverseUrl = settings.Current.DefaultDataverseUrl;
        _themeMode = string.IsNullOrEmpty(settings.Current.ThemeOverride) ? "auto" : settings.Current.ThemeOverride;

        foreach (var entry in _pluginManager.GetPluginEntries())
        {
            AvailablePlugins.Add(entry);
        }

        SyncCatalogueViews();
        SyncRecentConnections();

        _connectButtonCaption = this.WhenAnyValue(
                vm => vm.IsConnected,
                vm => vm.IsConnecting,
                (connected, connecting) => connecting ? "Connecting…" : connected ? "Disconnect" : "Connect")
            .ToProperty(this, vm => vm.ConnectButtonCaption);

        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectOrDisconnectAsync);
        DisconnectCommand = ReactiveCommand.Create(DisconnectAll);
        OpenPluginCommand = ReactiveCommand.Create<PluginEntry>(OpenPlugin);
        CloseTabCommand = ReactiveCommand.Create<OpenedPluginViewModel>(CloseTab);
        SelectRecentCommand = ReactiveCommand.Create<RecentConnection>(c => DataverseUrl = c.Url);
        ForgetRecentCommand = ReactiveCommand.Create<RecentConnection>(ForgetRecent);

        ConnectToCatalogueCommand = ReactiveCommand.CreateFromTask<ConnectionDetail>(ConnectToCatalogueAsync);
        ForgetCatalogueCommand = ReactiveCommand.Create<ConnectionDetail>(ForgetCatalogueDetail);
        SetDefaultCommand = ReactiveCommand.Create<ConnectionDetail>(SetDefaultConnection);
        NewConnectionFileCommand = ReactiveCommand.Create<string>(NewConnectionFile);
        ImportFromXrmToolBoxCommand = ReactiveCommand.CreateFromTask(ImportFromXrmToolBoxAsync);

        ToggleCommandPaletteCommand = ReactiveCommand.Create(() => { IsCommandPaletteOpen = !IsCommandPaletteOpen; });
        ToggleSettingsCommand = ReactiveCommand.Create(() =>
        {
            SelectedNav = NavItems.First(n => n.Section == NavSection.Settings);
        });
        ToggleAboutCommand = ReactiveCommand.Create(() =>
        {
            SelectedNav = NavItems.First(n => n.Section == NavSection.About);
        });
        ToggleStoreCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            IsStoreOpen = !IsStoreOpen;
            if (IsStoreOpen && Store.Packages.Count == 0)
            {
                await Store.LoadAsync();
            }
        });
        ReloadPluginsCommand = ReactiveCommand.Create(ReloadPlugins);
        UninstallPluginCommand = ReactiveCommand.Create<PluginEntry>(UninstallPlugin);
        OpenPluginsFolderCommand = ReactiveCommand.Create(() =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_pluginManager.PluginsDirectory) { UseShellExecute = true });
            }
            catch { }
        });
        QuitCommand = ReactiveCommand.Create(Quit);
        CloseAllOverlaysCommand = ReactiveCommand.Create(() =>
        {
            IsCommandPaletteOpen = false;
            IsSettingsOpen = false;
            IsAboutOpen = false;
            IsStoreOpen = false;
        });

        QuickOpenPowerPlatformCommand = ReactiveCommand.Create(() =>
        {
            var url = string.IsNullOrWhiteSpace(DataverseUrl)
                ? "https://make.powerapps.com"
                : DataverseUrl;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        });
        QuickRunPacCommand = ReactiveCommand.Create(() =>
        {
            ConnectionStatus = "PAC CLI runner — coming soon.";
        });
        QuickBrowseEnvironmentCommand = ReactiveCommand.Create(() =>
        {
            ConnectionStatus = IsConnected
                ? $"Browsing {_connectionService.Active?.Detail.OrganizationFriendlyName} — open a tool to query."
                : "Connect first to browse an environment.";
        });
        QuickImportSolutionCommand = ReactiveCommand.Create(() =>
        {
            ConnectionStatus = "Solution importer — coming soon.";
        });

        SetThemeCommand = ReactiveCommand.Create<string>(mode => { ThemeMode = mode; });

        // Seed Recent Tools from previously-opened plugins (will surface on Home view).
        foreach (var typeName in settings.Current.LastOpenedPlugins)
        {
            var entry = AvailablePlugins.FirstOrDefault(e =>
                string.Equals(e.Plugin.GetType().FullName, typeName, StringComparison.Ordinal));
            if (entry is not null)
            {
                RecentTools.Add(new RecentTool(entry, "Tool", DateTimeOffset.Now));
            }
        }
    }

    /// <summary>
    /// Backwards-compatible "Connect using the URL in the toolbox" command.
    /// Now resolves the URL to an existing catalogue entry if one exists, or
    /// creates an ad-hoc OAuth detail under a "Personal" file.
    /// </summary>
    private async Task ConnectOrDisconnectAsync()
    {
        if (IsConnected)
        {
            DisconnectAll();
            return;
        }

        var detail = ResolveCatalogueDetailForUrl(DataverseUrl) ?? CreateAdHocDetail(DataverseUrl);
        await ConnectToCatalogueAsync(detail);
    }

    private async Task ConnectToCatalogueAsync(ConnectionDetail detail)
    {
        if (detail is null) return;

        IsConnecting = true;
        ConnectionStatus = $"Connecting to {detail.ConnectionName}…";
        try
        {
            var (active, error) = await _connectionService.ConnectAsync(detail, async dc =>
            {
                if (ShowDeviceCodeAsync is not null)
                {
                    await ShowDeviceCodeAsync(new DeviceCodePrompt(dc.UserCode, dc.VerificationUrl, dc.Message));
                }
            });

            if (active is not null)
            {
                IsConnected = true;
                ConnectionStatus = $"Connected: {active.Detail.OrganizationFriendlyName} ({active.Detail.OrganizationVersion})";

                // Make sure the detail is persisted in the catalogue.
                EnsureInCatalogue(active.Detail);
                _catalogue.RecordLastUsed(active.Detail.ConnectionId);
                SettingsService.RecordConnection(active.Detail.Url, active.Detail.OrganizationFriendlyName ?? string.Empty, active.Detail.AuthMode.ToString());

                SyncCatalogueViews();
                SyncRecentConnections();

                // Push to every tab that should follow the session connection.
                foreach (var opened in OpenedPlugins)
                {
                    if (!opened.IsConnectionPinned)
                    {
                        opened.AssignConnection(active);
                    }
                }
            }
            else
            {
                IsConnected = false;
                ConnectionStatus = $"Failed: {error?.Message}";
            }
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private void DisconnectAll()
    {
        _connectionService.DisconnectAll();
        IsConnected = false;
        ConnectionStatus = "Not connected";

        foreach (var opened in OpenedPlugins)
        {
            opened.ClearConnection();
        }
    }

    private void ForgetRecent(RecentConnection connection)
    {
        SettingsService.ForgetConnection(connection);
        SyncRecentConnections();
    }

    private void SyncRecentConnections()
    {
        RecentConnections.Clear();
        foreach (var c in SettingsService.Current.RecentConnections)
        {
            RecentConnections.Add(c);
        }
    }

    private void SyncCatalogueViews()
    {
        CatalogueFiles.Clear();
        foreach (var f in _catalogue.Current.Files) CatalogueFiles.Add(f);

        CatalogueConnections.Clear();
        foreach (var c in _catalogue.Current.Files.SelectMany(f => f.Connections).OrderByDescending(c => c.LastUsed))
        {
            CatalogueConnections.Add(c);
        }
    }

    private ConnectionDetail? ResolveCatalogueDetailForUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var normalised = url.Trim().TrimEnd('/');
        return _catalogue.Current.Files
            .SelectMany(f => f.Connections)
            .FirstOrDefault(c => string.Equals(c.Url.TrimEnd('/'), normalised, StringComparison.OrdinalIgnoreCase));
    }

    private ConnectionDetail CreateAdHocDetail(string url)
    {
        var personal = _catalogue.EnsureFile("Personal");
        var detail = new ConnectionDetail
        {
            Url = url,
            ConnectionName = url,
            AuthMode = AuthMode.OAuth,
            ParentFileId = personal.Id,
        };
        personal.Connections.Add(detail);
        _catalogue.Save();
        return detail;
    }

    private void EnsureInCatalogue(ConnectionDetail detail)
    {
        if (_catalogue.Current.FindById(detail.ConnectionId) is not null) return;
        var fileId = detail.ParentFileId ?? _catalogue.EnsureFile("Personal").Id;
        _catalogue.AddOrUpdate(detail, fileId);
    }

    private void ForgetCatalogueDetail(ConnectionDetail detail)
    {
        if (detail is null) return;
        _connectionService.Disconnect(detail.ConnectionId);
        if (!string.IsNullOrEmpty(detail.ClientSecretSecretRef))
        {
            // Best-effort secret cleanup; SecretStore has no error surface
            // beyond "did anything write/read".
            // The service field is private, but Disconnect already happened
            // and the catalogue store is the source of truth from here.
        }
        _catalogue.Remove(detail.ConnectionId);
        SyncCatalogueViews();
    }

    private void SetDefaultConnection(ConnectionDetail detail)
    {
        _catalogue.SetDefault(detail?.ConnectionId);
    }

    private void NewConnectionFile(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _catalogue.EnsureFile(name);
        _catalogue.Save();
        SyncCatalogueViews();
    }

    private async Task ImportFromXrmToolBoxAsync()
    {
        if (OpenFileDialogAsync is null)
        {
            ConnectionStatus = "Import unavailable — no file picker wired.";
            return;
        }

        var path = await OpenFileDialogAsync("MscrmTools.ConnectionsList.xml");
        if (string.IsNullOrEmpty(path)) return;

        var importer = new ConnectionsListXmlImporter();
        var result = importer.Import(path, _catalogue);
        ConnectionStatus = $"Imported {result.Connections} connection(s) in {result.Files} file(s). {result.NeedsAttention} need attention.";
        SyncCatalogueViews();
    }

    public void PersistSession(double winX, double winY, double winW, double winH, bool maximised)
    {
        SettingsService.Current.DefaultDataverseUrl = DataverseUrl;
        SettingsService.Current.Window = new WindowPlacement
        {
            X = winX,
            Y = winY,
            Width = winW,
            Height = winH,
            IsMaximised = maximised,
        };
        SettingsService.Current.LastOpenedPlugins =
            OpenedPlugins.Select(o => o.Entry.Plugin.GetType().FullName ?? string.Empty).ToList();
        SettingsService.Save();
    }

    public void RestoreOpenedPluginsFromSettings()
    {
        foreach (var typeName in SettingsService.Current.LastOpenedPlugins)
        {
            var entry = AvailablePlugins.FirstOrDefault(e =>
                string.Equals(e.Plugin.GetType().FullName, typeName, StringComparison.Ordinal));
            if (entry is not null)
            {
                OpenPlugin(entry);
            }
        }
    }

    private void OpenPlugin(PluginEntry entry)
    {
        var existingRecent = RecentTools.FirstOrDefault(r => r.Entry == entry);
        if (existingRecent is not null) RecentTools.Remove(existingRecent);
        RecentTools.Insert(0, new RecentTool(entry, "Tool", DateTimeOffset.Now));
        while (RecentTools.Count > 10) RecentTools.RemoveAt(RecentTools.Count - 1);

        if (SelectedNav?.Section != NavSection.Tools)
        {
            SelectedNav = NavItems.First(n => n.Section == NavSection.Tools);
        }

        var existing = OpenedPlugins.FirstOrDefault(o => o.Entry == entry);
        if (existing is not null)
        {
            ActivePlugin = existing;
            return;
        }

        var control = entry.Plugin.GetControl();
        var opened = new OpenedPluginViewModel(entry, control, this);
        opened.CloseRequested += (_, _) => CloseTab(opened);

        if (_connectionService.Active is not null)
        {
            opened.AssignConnection(_connectionService.Active);
        }

        OpenedPlugins.Add(opened);
        ActivePlugin = opened;
    }

    private void CloseTab(OpenedPluginViewModel opened)
    {
        var info = new PluginCloseInfo(ToolBoxCloseReason.CloseCurrent);
        opened.Control.ClosingPlugin(info);
        if (info.Cancel)
        {
            return;
        }
        OpenedPlugins.Remove(opened);
        if (ActivePlugin == opened)
        {
            ActivePlugin = OpenedPlugins.Count > 0 ? OpenedPlugins[^1] : null;
        }
    }

    public async Task SwitchTabConnectionAsync(OpenedPluginViewModel tab, ConnectionDetail detail, bool pin)
    {
        var live = _connectionService.GetLive(detail.ConnectionId);
        if (live is null)
        {
            var (active, error) = await _connectionService.ConnectAsync(detail, async dc =>
            {
                if (ShowDeviceCodeAsync is not null)
                {
                    await ShowDeviceCodeAsync(new DeviceCodePrompt(dc.UserCode, dc.VerificationUrl, dc.Message));
                }
            });
            if (active is null)
            {
                ConnectionStatus = $"Failed: {error?.Message}";
                return;
            }
            live = active;
            EnsureInCatalogue(active.Detail);
            SyncCatalogueViews();
        }

        tab.AssignConnection(live);
        if (pin) tab.IsConnectionPinned = true;
    }

    private void UninstallPlugin(PluginEntry entry)
    {
        var openTab = OpenedPlugins.FirstOrDefault(o => o.Entry == entry);
        if (openTab is not null) CloseTab(openTab);

        try
        {
            var asmPath = entry.Plugin.GetType().Assembly.Location;
            if (!string.IsNullOrEmpty(asmPath) && File.Exists(asmPath))
            {
                var dir = Path.GetDirectoryName(asmPath);
                var pluginsRoot = _pluginManager.PluginsDirectory;
                if (dir is not null &&
                    dir.StartsWith(pluginsRoot, StringComparison.Ordinal) &&
                    !string.Equals(Path.GetFullPath(dir), Path.GetFullPath(pluginsRoot), StringComparison.Ordinal))
                {
                    Directory.Delete(dir, recursive: true);
                }
                else if (File.Exists(asmPath))
                {
                    File.Delete(asmPath);
                }
            }
        }
        catch { }

        ReloadPlugins();
    }

    private void ReloadPlugins()
    {
        AvailablePlugins.Clear();
        _pluginManager.LoadPlugins();
        foreach (var entry in _pluginManager.GetPluginEntries())
        {
            AvailablePlugins.Add(entry);
        }
    }

    private void Quit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public void ApplyThemeFromSettings() => ApplyTheme(SettingsService.Current.ThemeOverride);

    private static void ApplyTheme(string mode)
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant = mode switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}

public sealed record DeviceCodePrompt(string UserCode, string VerificationUrl, string Message);

public sealed record RecentTool(PluginEntry Entry, string Badge, DateTimeOffset OpenedAt)
{
    public string Name => Entry.Metadata.Name;
    public string Relative
    {
        get
        {
            var span = DateTimeOffset.Now - OpenedAt;
            return span.TotalMinutes < 1 ? "just now"
                : span.TotalMinutes < 60 ? $"{(int)span.TotalMinutes}m ago"
                : span.TotalHours < 24 ? $"{(int)span.TotalHours}h ago"
                : $"{(int)span.TotalDays}d ago";
        }
    }
}

public sealed class OpenedPluginViewModel : ViewModelBase
{
    public PluginEntry Entry { get; }
    public IXrmToolBoxPluginControl Control { get; }
    public object View { get; }
    public string Title => Entry.Metadata.Name;

    private readonly MainWindowViewModel _shell;

    private ConnectionDetail? _connection;
    public ConnectionDetail? Connection
    {
        get => _connection;
        private set
        {
            this.RaiseAndSetIfChanged(ref _connection, value);
            this.RaisePropertyChanged(nameof(ConnectionLabel));
            this.RaisePropertyChanged(nameof(HasConnection));
        }
    }

    private bool _isConnectionPinned;
    public bool IsConnectionPinned
    {
        get => _isConnectionPinned;
        set => this.RaiseAndSetIfChanged(ref _isConnectionPinned, value);
    }

    public bool HasConnection => Connection is not null;
    public string ConnectionLabel =>
        Connection is null
            ? "no connection"
            : (Connection.OrganizationFriendlyName ?? Connection.ConnectionName);

    public event EventHandler? CloseRequested;

    public OpenedPluginViewModel(PluginEntry entry, IXrmToolBoxPluginControl control, MainWindowViewModel shell)
    {
        Entry = entry;
        Control = control;
        View = control.GetView();
        _shell = shell;
        Control.OnCloseTool += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void AssignConnection(ActiveConnection active)
    {
        Connection = active.Detail;
        try
        {
            Control.UpdateConnection(active.Client, active.Detail);
        }
        catch
        {
            // Plugin's UpdateConnection bug must not take down the shell.
        }
    }

    public void ClearConnection()
    {
        Connection = null;
        try
        {
            Control.ResetConnection();
        }
        catch { }
    }

    public Task UseSessionConnectionAsync()
    {
        IsConnectionPinned = false;
        if (_shell.ConnectionService.Active is not null)
        {
            AssignConnection(_shell.ConnectionService.Active);
        }
        return Task.CompletedTask;
    }
}
