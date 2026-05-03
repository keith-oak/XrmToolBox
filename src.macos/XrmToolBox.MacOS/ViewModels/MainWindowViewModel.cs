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

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly PluginManager _pluginManager;
    private readonly DataverseConnectionService _connectionService = new();
    public SettingsService SettingsService { get; }

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

    public ObservableCollection<PluginEntry> AvailablePlugins { get; } = new();
    public ObservableCollection<OpenedPluginViewModel> OpenedPlugins { get; } = new();
    public ObservableCollection<RecentConnection> RecentConnections { get; } = new();

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<PluginEntry, Unit> OpenPluginCommand { get; }
    public ReactiveCommand<OpenedPluginViewModel, Unit> CloseTabCommand { get; }
    public ReactiveCommand<RecentConnection, Unit> SelectRecentCommand { get; }
    public ReactiveCommand<RecentConnection, Unit> ForgetRecentCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommandPaletteCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleAboutCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleStoreCommand { get; }
    public ReactiveCommand<Unit, Unit> ReloadPluginsCommand { get; }
    public ReactiveCommand<Unit, Unit> QuitCommand { get; }

    public MainWindowViewModel(PluginManager pluginManager, SettingsService settings)
    {
        _pluginManager = pluginManager;
        SettingsService = settings;

        _dataverseUrl = settings.Current.DefaultDataverseUrl;

        foreach (var entry in _pluginManager.GetPluginEntries())
        {
            AvailablePlugins.Add(entry);
        }

        foreach (var c in settings.Current.RecentConnections)
        {
            RecentConnections.Add(c);
        }

        _connectButtonCaption = this.WhenAnyValue(
                vm => vm.IsConnected,
                vm => vm.IsConnecting,
                (connected, connecting) => connecting ? "Connecting…" : connected ? "Disconnect" : "Connect")
            .ToProperty(this, vm => vm.ConnectButtonCaption);

        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectOrDisconnectAsync);
        DisconnectCommand = ReactiveCommand.Create(Disconnect);
        OpenPluginCommand = ReactiveCommand.Create<PluginEntry>(OpenPlugin);
        CloseTabCommand = ReactiveCommand.Create<OpenedPluginViewModel>(CloseTab);
        SelectRecentCommand = ReactiveCommand.Create<RecentConnection>(c => DataverseUrl = c.Url);
        ForgetRecentCommand = ReactiveCommand.Create<RecentConnection>(ForgetRecent);
        ToggleCommandPaletteCommand = ReactiveCommand.Create(() => { IsCommandPaletteOpen = !IsCommandPaletteOpen; });
        ToggleSettingsCommand = ReactiveCommand.Create(() => { IsSettingsOpen = !IsSettingsOpen; });
        ToggleAboutCommand = ReactiveCommand.Create(() => { IsAboutOpen = !IsAboutOpen; });
        ToggleStoreCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            IsStoreOpen = !IsStoreOpen;
            if (IsStoreOpen && Store.Packages.Count == 0)
            {
                await Store.LoadAsync();
            }
        });
        ReloadPluginsCommand = ReactiveCommand.Create(ReloadPlugins);
        QuitCommand = ReactiveCommand.Create(Quit);
    }

    private async Task ConnectOrDisconnectAsync()
    {
        if (IsConnected)
        {
            Disconnect();
            return;
        }

        IsConnecting = true;
        ConnectionStatus = "Connecting…";
        try
        {
            var (success, error) = await _connectionService.ConnectInteractiveAsync(DataverseUrl);
            if (success)
            {
                IsConnected = true;
                var c = _connectionService.CurrentConnection;
                ConnectionStatus = $"Connected: {c?.OrganizationFriendlyName} ({c?.OrganizationVersion})";
                if (c is not null)
                {
                    SettingsService.RecordConnection(c.Url, c.OrganizationFriendlyName ?? string.Empty, c.AuthMode.ToString());
                    SyncRecentConnections();
                }
                foreach (var opened in OpenedPlugins)
                {
                    opened.PushConnection(_connectionService);
                }
            }
            else
            {
                IsConnected = false;
                ConnectionStatus = $"Failed: {error}";
            }
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private void Disconnect()
    {
        _connectionService.Disconnect();
        IsConnected = false;
        ConnectionStatus = "Not connected";
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
        var existing = OpenedPlugins.FirstOrDefault(o => o.Entry == entry);
        if (existing is not null)
        {
            ActivePlugin = existing;
            return;
        }

        var control = entry.Plugin.GetControl();
        var opened = new OpenedPluginViewModel(entry, control);
        opened.CloseRequested += (_, _) => CloseTab(opened);

        if (_connectionService.Client is not null)
        {
            opened.PushConnection(_connectionService);
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

    public void ApplyThemeFromSettings()
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant = SettingsService.Current.ThemeOverride switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}

public sealed class OpenedPluginViewModel : ViewModelBase
{
    public PluginEntry Entry { get; }
    public IXrmToolBoxPluginControl Control { get; }
    public object View { get; }
    public string Title => Entry.Metadata.Name;

    public event EventHandler? CloseRequested;

    public OpenedPluginViewModel(PluginEntry entry, IXrmToolBoxPluginControl control)
    {
        Entry = entry;
        Control = control;
        View = control.GetView();
        Control.OnCloseTool += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void PushConnection(DataverseConnectionService svc)
    {
        if (svc.Client is null || svc.CurrentConnection is null) return;
        Control.UpdateConnection(svc.Client, svc.CurrentConnection);
    }
}
