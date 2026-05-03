using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;
using XrmToolBox.MacOS.Connection;
using XrmToolBox.MacOS.Plugins;

namespace XrmToolBox.MacOS.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly PluginManager _pluginManager;
    private readonly DataverseConnectionService _connectionService = new();

    private string _connectionStatus = "Not connected";
    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => this.RaiseAndSetIfChanged(ref _connectionStatus, value);
    }

    private string _dataverseUrl = "https://yourorg.crm.dynamics.com";
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

    public ObservableCollection<PluginEntry> AvailablePlugins { get; } = new();
    public ObservableCollection<OpenedPluginViewModel> OpenedPlugins { get; } = new();

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<PluginEntry, Unit> OpenPluginCommand { get; }
    public ReactiveCommand<OpenedPluginViewModel, Unit> CloseTabCommand { get; }

    public MainWindowViewModel(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;

        foreach (var entry in _pluginManager.GetPluginEntries())
        {
            AvailablePlugins.Add(entry);
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
                ConnectionStatus = $"Connected: {_connectionService.CurrentConnection?.OrganizationFriendlyName} ({_connectionService.CurrentConnection?.OrganizationVersion})";
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
