using System.Collections.ObjectModel;
using System.Reactive;
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

    private OpenedPluginViewModel? _activePlugin;
    public OpenedPluginViewModel? ActivePlugin
    {
        get => _activePlugin;
        set => this.RaiseAndSetIfChanged(ref _activePlugin, value);
    }

    public ObservableCollection<PluginEntry> AvailablePlugins { get; } = new();
    public ObservableCollection<OpenedPluginViewModel> OpenedPlugins { get; } = new();

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<PluginEntry, Unit> OpenPluginCommand { get; }
    public ReactiveCommand<OpenedPluginViewModel, Unit> CloseTabCommand { get; }

    public MainWindowViewModel(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;

        foreach (var entry in _pluginManager.GetPluginEntries())
        {
            AvailablePlugins.Add(entry);
        }

        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync);
        OpenPluginCommand = ReactiveCommand.Create<PluginEntry>(OpenPlugin);
        CloseTabCommand = ReactiveCommand.Create<OpenedPluginViewModel>(CloseTab);
    }

    private async Task ConnectAsync()
    {
        ConnectionStatus = "Connecting...";
        var (success, error) = await _connectionService.ConnectInteractiveAsync(DataverseUrl);
        ConnectionStatus = success
            ? $"Connected: {_connectionService.CurrentConnection?.OrganizationFriendlyName} ({_connectionService.CurrentConnection?.OrganizationVersion})"
            : $"Failed: {error}";

        if (success)
        {
            foreach (var opened in OpenedPlugins)
            {
                opened.PushConnection(_connectionService);
            }
        }
    }

    private void OpenPlugin(PluginEntry entry)
    {
        var control = entry.Plugin.GetControl();
        var opened = new OpenedPluginViewModel(entry, control);
        opened.CloseRequested += (_, _) => CloseTab(opened);

        if (_connectionService.Client != null)
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
