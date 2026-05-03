using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using XrmToolBox.Extensibility;

namespace XrmToolBox.MacOS.ViewModels;

/// <summary>
/// Backs the "Add connection" overlay. Three-step flow: pick auth mode → fill
/// details → test/save. Pure data + commands; no Avalonia types.
/// </summary>
public sealed class AddConnectionViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;

    public AddConnectionViewModel(MainWindowViewModel shell, ConnectionDetail? editing = null)
    {
        _shell = shell;

        if (editing is not null)
        {
            _existingId = editing.ConnectionId;
            ConnectionName = editing.ConnectionName;
            Url = editing.Url;
            AuthMode = editing.AuthMode;
            AzureAdAppId = editing.AzureAdAppId ?? string.Empty;
            Tenant = editing.Tenant ?? string.Empty;
            CertificateThumbprint = editing.CertificateThumbprint ?? string.Empty;
            ClientSecretSecretRef = editing.ClientSecretSecretRef ?? string.Empty;
            FileName = shell.Catalogue.Current.FindFileForConnection(editing.ConnectionId)?.Name ?? "Personal";
            IsAuthModeSelected = true;
        }

        SelectAuthModeCommand = ReactiveCommand.Create<string>(mode =>
        {
            AuthMode = Enum.Parse<AuthMode>(mode);
            IsAuthModeSelected = true;
        });

        BackCommand = ReactiveCommand.Create(() => { IsAuthModeSelected = false; });

        TestCommand = ReactiveCommand.CreateFromTask(TestAsync);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        SaveAndConnectCommand = ReactiveCommand.CreateFromTask(SaveAndConnectAsync);
        SetSecretCommand = ReactiveCommand.Create<string>(value =>
        {
            // The TextBox in the modal binds the plain-text value here, then
            // we hand it to the SecretStore once the user clicks "Set secret".
            // We never persist the value into the catalogue.
            if (string.IsNullOrEmpty(value)) return;
            var refKey = string.IsNullOrEmpty(ClientSecretSecretRef) ? Guid.NewGuid().ToString("N") : ClientSecretSecretRef;
            shell.SecretStore.Set(refKey, value);
            ClientSecretSecretRef = refKey;
            PendingSecret = string.Empty;
            TestResult = "Secret saved to Keychain.";
        });
    }

    private readonly Guid? _existingId;

    private bool _isAuthModeSelected;
    public bool IsAuthModeSelected
    {
        get => _isAuthModeSelected;
        set => this.RaiseAndSetIfChanged(ref _isAuthModeSelected, value);
    }

    private AuthMode _authMode = AuthMode.OAuth;
    public AuthMode AuthMode
    {
        get => _authMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _authMode, value);
            this.RaisePropertyChanged(nameof(IsOAuth));
            this.RaisePropertyChanged(nameof(IsDeviceCode));
            this.RaisePropertyChanged(nameof(IsClientSecret));
            this.RaisePropertyChanged(nameof(IsCertificate));
            this.RaisePropertyChanged(nameof(NeedsAppId));
        }
    }

    public bool IsOAuth => AuthMode == AuthMode.OAuth;
    public bool IsDeviceCode => AuthMode == AuthMode.DeviceCode;
    public bool IsClientSecret => AuthMode == AuthMode.ClientSecret;
    public bool IsCertificate => AuthMode == AuthMode.Certificate;
    public bool NeedsAppId => AuthMode is AuthMode.ClientSecret or AuthMode.Certificate;

    private string _connectionName = string.Empty;
    public string ConnectionName
    {
        get => _connectionName;
        set => this.RaiseAndSetIfChanged(ref _connectionName, value);
    }

    private string _url = string.Empty;
    public string Url
    {
        get => _url;
        set => this.RaiseAndSetIfChanged(ref _url, value);
    }

    private string _fileName = "Personal";
    public string FileName
    {
        get => _fileName;
        set => this.RaiseAndSetIfChanged(ref _fileName, value);
    }

    private string _azureAdAppId = string.Empty;
    public string AzureAdAppId
    {
        get => _azureAdAppId;
        set => this.RaiseAndSetIfChanged(ref _azureAdAppId, value);
    }

    private string _tenant = string.Empty;
    public string Tenant
    {
        get => _tenant;
        set => this.RaiseAndSetIfChanged(ref _tenant, value);
    }

    private string _certificateThumbprint = string.Empty;
    public string CertificateThumbprint
    {
        get => _certificateThumbprint;
        set => this.RaiseAndSetIfChanged(ref _certificateThumbprint, value);
    }

    private string _clientSecretSecretRef = string.Empty;
    public string ClientSecretSecretRef
    {
        get => _clientSecretSecretRef;
        set
        {
            this.RaiseAndSetIfChanged(ref _clientSecretSecretRef, value);
            this.RaisePropertyChanged(nameof(SecretIsSet));
        }
    }
    public bool SecretIsSet => !string.IsNullOrEmpty(ClientSecretSecretRef);

    private string _pendingSecret = string.Empty;
    public string PendingSecret
    {
        get => _pendingSecret;
        set => this.RaiseAndSetIfChanged(ref _pendingSecret, value);
    }

    private string _testResult = string.Empty;
    public string TestResult
    {
        get => _testResult;
        set => this.RaiseAndSetIfChanged(ref _testResult, value);
    }

    private bool _isWorking;
    public bool IsWorking
    {
        get => _isWorking;
        set => this.RaiseAndSetIfChanged(ref _isWorking, value);
    }

    public ReactiveCommand<string, Unit> SelectAuthModeCommand { get; }
    public ReactiveCommand<Unit, Unit> BackCommand { get; }
    public ReactiveCommand<Unit, Unit> TestCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAndConnectCommand { get; }
    public ReactiveCommand<string, Unit> SetSecretCommand { get; }

    public event EventHandler? RequestClose;

    private ConnectionDetail Build()
    {
        var detail = new ConnectionDetail
        {
            ConnectionId = _existingId ?? Guid.NewGuid(),
            ConnectionName = string.IsNullOrWhiteSpace(ConnectionName) ? Url : ConnectionName,
            Url = Url,
            AuthMode = AuthMode,
            AzureAdAppId = string.IsNullOrWhiteSpace(AzureAdAppId) ? null : AzureAdAppId,
            Tenant = string.IsNullOrWhiteSpace(Tenant) ? null : Tenant,
            CertificateThumbprint = string.IsNullOrWhiteSpace(CertificateThumbprint) ? null : CertificateThumbprint,
            ClientSecretSecretRef = string.IsNullOrWhiteSpace(ClientSecretSecretRef) ? null : ClientSecretSecretRef,
        };
        return detail;
    }

    private async Task TestAsync()
    {
        IsWorking = true;
        TestResult = "Testing…";
        try
        {
            var detail = Build();
            var (active, error) = await _shell.ConnectionService.ConnectAsync(detail, async dc =>
            {
                if (_shell.ShowDeviceCodeAsync is not null)
                {
                    await _shell.ShowDeviceCodeAsync(new DeviceCodePrompt(dc.UserCode, dc.VerificationUrl, dc.Message));
                }
            });

            TestResult = active is not null
                ? $"OK — {active.Detail.OrganizationFriendlyName} ({active.Detail.OrganizationVersion})"
                : $"Failed — {error?.Message}";

            if (active is not null)
            {
                ConnectionName = active.Detail.OrganizationFriendlyName ?? ConnectionName;
            }
        }
        finally
        {
            IsWorking = false;
        }
    }

    private Task SaveAsync()
    {
        var detail = Build();
        var fileId = _shell.Catalogue.EnsureFile(string.IsNullOrWhiteSpace(FileName) ? "Personal" : FileName).Id;
        _shell.Catalogue.AddOrUpdate(detail, fileId);
        _shell.NotifyCatalogueChanged();
        RequestClose?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private Task SaveAndConnectAsync()
    {
        var detail = Build();
        var fileId = _shell.Catalogue.EnsureFile(string.IsNullOrWhiteSpace(FileName) ? "Personal" : FileName).Id;
        _shell.Catalogue.AddOrUpdate(detail, fileId);
        _shell.NotifyCatalogueChanged();
        _shell.ConnectToCatalogueCommand.Execute(detail).Subscribe();
        RequestClose?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
