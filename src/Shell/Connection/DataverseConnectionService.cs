using System.Threading.Tasks;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using XrmToolBox.Extensibility;

namespace XrmToolBox.MacOS.Connection;

public enum ConnectErrorCategory
{
    Cancelled,
    KeychainDenied,
    SecretMissing,
    NetworkError,
    AuthError,
    EnvironmentRefused,
}

public sealed record ConnectError(ConnectErrorCategory Category, string Message);

public sealed record ActiveConnection(
    ServiceClient Client,
    ConnectionDetail Detail);

public sealed class DataverseConnectionService
{
    public const string DataversePublicClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
    private const string MacKeychainServiceName = "com.lucidlabs.pacdtoolbox.tokencache";
    private const string MacKeychainAccountName = "msal-cache";
    private const string CacheFileName = "msal.cache";

    private readonly SecretStore _secrets;

    /// <summary>The session's "active" connection — what new tabs inherit.</summary>
    public ActiveConnection? Active { get; private set; }

    /// <summary>Every connection that's currently signed in — keyed by ConnectionId.</summary>
    private readonly Dictionary<Guid, ActiveConnection> _live = new();

    public IReadOnlyDictionary<Guid, ActiveConnection> Live => _live;

    public DataverseConnectionService(SecretStore secrets)
    {
        _secrets = secrets;
    }

    public bool IsLive(Guid connectionId) => _live.ContainsKey(connectionId);

    public ActiveConnection? GetLive(Guid connectionId) =>
        _live.TryGetValue(connectionId, out var live) ? live : null;

    public async Task<(ActiveConnection? Active, ConnectError? Error)> ConnectAsync(
        ConnectionDetail detail,
        Func<DeviceCodeResult, Task>? onDeviceCode = null)
    {
        if (string.IsNullOrWhiteSpace(detail.Url))
        {
            return (null, new ConnectError(ConnectErrorCategory.AuthError, "Connection has no URL."));
        }

        // Already live — just promote to active.
        if (_live.TryGetValue(detail.ConnectionId, out var existing))
        {
            Active = existing;
            return (existing, null);
        }

        var url = NormaliseUrl(detail.Url);

        try
        {
            var client = detail.AuthMode switch
            {
                AuthMode.OAuth => await ConnectOAuthAsync(url, detail),
                AuthMode.DeviceCode => await ConnectDeviceCodeAsync(url, detail, onDeviceCode),
                AuthMode.ClientSecret => await ConnectClientSecretAsync(url, detail),
                AuthMode.Certificate => await ConnectCertificateAsync(url, detail),
                _ => throw new InvalidOperationException($"Unsupported auth mode: {detail.AuthMode}"),
            };

            if (client is null || !client.IsReady)
            {
                client?.Dispose();
                return (null, new ConnectError(ConnectErrorCategory.EnvironmentRefused,
                    client?.LastError ?? "Failed to connect to Dataverse."));
            }

            // End-to-end probe so a busted environment shows up immediately.
            try
            {
                _ = await Task.Run(() => client.Execute(new WhoAmIRequest()));
            }
            catch (Exception probe)
            {
                client.Dispose();
                return (null, new ConnectError(ConnectErrorCategory.EnvironmentRefused,
                    $"Connected but the environment refused the request: {probe.Message}"));
            }

            detail.Url = url;
            detail.OrganizationFriendlyName = client.ConnectedOrgFriendlyName;
            detail.OrganizationVersion = client.ConnectedOrgVersion?.ToString();
            detail.LastUsed = DateTimeOffset.UtcNow;

            var active = new ActiveConnection(client, detail);
            _live[detail.ConnectionId] = active;
            Active = active;
            return (active, null);
        }
        catch (MsalServiceException ex) when (ex.ErrorCode is "authorization_declined" or "access_denied")
        {
            return (null, new ConnectError(ConnectErrorCategory.Cancelled, "Sign-in was declined."));
        }
        catch (MsalClientException ex) when (
            ex.ErrorCode is "authentication_canceled" or "user_canceled" ||
            (ex.ErrorCode is not null && ex.ErrorCode.Contains("cache", StringComparison.OrdinalIgnoreCase)))
        {
            return (null, new ConnectError(ConnectErrorCategory.KeychainDenied,
                "Sign-in was cancelled or Keychain access was denied."));
        }
        catch (MsalException ex)
        {
            return (null, new ConnectError(ConnectErrorCategory.AuthError, $"Sign-in failed: {ex.Message}"));
        }
        catch (Exception ex) when (ex.Message.Contains("keychain", StringComparison.OrdinalIgnoreCase))
        {
            return (null, new ConnectError(ConnectErrorCategory.KeychainDenied,
                "Keychain access was denied. PAC'd Toolbox needs Keychain access to store sign-in tokens."));
        }
        catch (Exception ex)
        {
            return (null, new ConnectError(ConnectErrorCategory.NetworkError, ex.Message));
        }
    }

    private async Task<ServiceClient?> ConnectOAuthAsync(string url, ConnectionDetail detail)
    {
        var resourceUri = url + "/.default";
        var msal = PublicClientApplicationBuilder
            .Create(string.IsNullOrWhiteSpace(detail.AzureAdAppId) ? DataversePublicClientId : detail.AzureAdAppId)
            .WithAuthority(AzureCloudInstance.AzurePublic, string.IsNullOrWhiteSpace(detail.Tenant) ? "organizations" : detail.Tenant)
            .WithRedirectUri("http://localhost")
            .Build();

        await AttachKeychainCacheAsync(msal.UserTokenCache);

        AuthenticationResult result;
        var accounts = await msal.GetAccountsAsync();
        var account = accounts.FirstOrDefault();
        try
        {
            result = await msal.AcquireTokenSilent(new[] { resourceUri }, account).ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            result = await msal.AcquireTokenInteractive(new[] { resourceUri })
                .WithUseEmbeddedWebView(false)
                .ExecuteAsync();
        }
        catch (NullReferenceException)
        {
            result = await msal.AcquireTokenInteractive(new[] { resourceUri })
                .WithUseEmbeddedWebView(false)
                .ExecuteAsync();
        }

        var token = result.AccessToken;
        var tokenExpiry = result.ExpiresOn;

        Func<string, Task<string>> tokenFunc = async _ =>
        {
            if (tokenExpiry > DateTimeOffset.UtcNow.AddMinutes(2)) return token;
            var refreshed = await msal.AcquireTokenSilent(new[] { resourceUri }, account).ExecuteAsync();
            token = refreshed.AccessToken;
            tokenExpiry = refreshed.ExpiresOn;
            return token;
        };

        return new ServiceClient(new Uri(url), tokenFunc, useUniqueInstance: true);
    }

    private async Task<ServiceClient?> ConnectDeviceCodeAsync(string url, ConnectionDetail detail, Func<DeviceCodeResult, Task>? onDeviceCode)
    {
        var resourceUri = url + "/.default";
        var msal = PublicClientApplicationBuilder
            .Create(string.IsNullOrWhiteSpace(detail.AzureAdAppId) ? DataversePublicClientId : detail.AzureAdAppId)
            .WithAuthority(AzureCloudInstance.AzurePublic, string.IsNullOrWhiteSpace(detail.Tenant) ? "organizations" : detail.Tenant)
            .Build();

        await AttachKeychainCacheAsync(msal.UserTokenCache);

        var result = await msal.AcquireTokenWithDeviceCode(new[] { resourceUri }, async dc =>
        {
            if (onDeviceCode is not null) await onDeviceCode(dc);
        }).ExecuteAsync();

        var token = result.AccessToken;
        var tokenExpiry = result.ExpiresOn;
        var account = result.Account;

        Func<string, Task<string>> tokenFunc = async _ =>
        {
            if (tokenExpiry > DateTimeOffset.UtcNow.AddMinutes(2)) return token;
            var refreshed = await msal.AcquireTokenSilent(new[] { resourceUri }, account).ExecuteAsync();
            token = refreshed.AccessToken;
            tokenExpiry = refreshed.ExpiresOn;
            return token;
        };

        return new ServiceClient(new Uri(url), tokenFunc, useUniqueInstance: true);
    }

    private Task<ServiceClient?> ConnectClientSecretAsync(string url, ConnectionDetail detail)
    {
        if (string.IsNullOrEmpty(detail.AzureAdAppId))
        {
            throw new InvalidOperationException("Client-secret auth requires AzureAdAppId.");
        }
        if (string.IsNullOrEmpty(detail.ClientSecretSecretRef))
        {
            throw new InvalidOperationException("Client-secret reference is missing — set the secret first.");
        }

        var secret = _secrets.Get(detail.ClientSecretSecretRef);
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("Client secret not found in Keychain.");
        }

        var connectionString =
            $"AuthType=ClientSecret;Url={url};ClientId={detail.AzureAdAppId};ClientSecret={secret};RequireNewInstance=true";
        var client = new ServiceClient(connectionString);
        return Task.FromResult<ServiceClient?>(client);
    }

    private Task<ServiceClient?> ConnectCertificateAsync(string url, ConnectionDetail detail)
    {
        if (string.IsNullOrEmpty(detail.AzureAdAppId))
        {
            throw new InvalidOperationException("Certificate auth requires AzureAdAppId.");
        }
        if (string.IsNullOrEmpty(detail.CertificateThumbprint))
        {
            throw new InvalidOperationException("Certificate auth requires a thumbprint.");
        }

        var connectionString =
            $"AuthType=Certificate;Url={url};ClientId={detail.AzureAdAppId};Thumbprint={detail.CertificateThumbprint};RequireNewInstance=true";
        var client = new ServiceClient(connectionString);
        return Task.FromResult<ServiceClient?>(client);
    }

    public void Disconnect(Guid connectionId)
    {
        if (_live.TryGetValue(connectionId, out var active))
        {
            active.Client.Dispose();
            _live.Remove(connectionId);
            if (Active?.Detail.ConnectionId == connectionId) Active = null;
        }
    }

    public void DisconnectAll()
    {
        foreach (var live in _live.Values) live.Client.Dispose();
        _live.Clear();
        Active = null;
    }

    public void SetActive(Guid connectionId)
    {
        if (_live.TryGetValue(connectionId, out var active)) Active = active;
    }

    private static string NormaliseUrl(string url)
    {
        url = url.Trim().TrimEnd('/');
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
        return url;
    }

    private static async Task AttachKeychainCacheAsync(ITokenCache cache)
    {
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "Application Support",
            "PACdToolbox",
            "TokenCache");
        Directory.CreateDirectory(cacheDir);

        var storageProperties = new StorageCreationPropertiesBuilder(CacheFileName, cacheDir)
            .WithMacKeyChain(MacKeychainServiceName, MacKeychainAccountName)
            .WithLinuxUnprotectedFile()
            .Build();

        var helper = await MsalCacheHelper.CreateAsync(storageProperties);
        helper.VerifyPersistence();
        helper.RegisterCache(cache);
    }
}
