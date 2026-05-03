using System.Threading.Tasks;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using XrmToolBox.Extensibility;

namespace XrmToolBox.MacOS.Connection;

public sealed class DataverseConnectionService
{
    // Dataverse public client app id used by SDK samples + XrmToolBox upstream.
    private const string PublicClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
    private const string MacKeychainServiceName = "com.lucidlabs.pacdtoolbox.tokencache";
    private const string MacKeychainAccountName = "msal-cache";
    private const string CacheFileName = "msal.cache";

    public ServiceClient? Client { get; private set; }
    public ConnectionDetail? CurrentConnection { get; private set; }

    private string? _currentAccessToken;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;
    private string? _currentResourceUri;
    private IPublicClientApplication? _msal;
    private IAccount? _account;

    public async Task<(bool Success, string? Error)> ConnectInteractiveAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (false, "Enter a Dataverse environment URL.");
        }

        url = url.Trim().TrimEnd('/');
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        try
        {
            var resourceUri = url + "/.default";
            _currentResourceUri = url;

            // MSAL public client.
            _msal = PublicClientApplicationBuilder
                .Create(PublicClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, "organizations")
                .WithRedirectUri("http://localhost")
                .Build();

            // Persist the token cache in the macOS Keychain. If the user
            // denies the keychain prompt, RegisterCacheAsync throws — we let
            // that fail the whole sign-in instead of falling back to a
            // plaintext file.
            await AttachKeychainCacheAsync(_msal.UserTokenCache);

            // Prefer silent acquisition when an account is cached.
            AuthenticationResult result;
            var accounts = await _msal.GetAccountsAsync();
            _account = accounts.FirstOrDefault();
            try
            {
                result = await _msal.AcquireTokenSilent(new[] { resourceUri }, _account)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                result = await _msal
                    .AcquireTokenInteractive(new[] { resourceUri })
                    .WithUseEmbeddedWebView(false)
                    .ExecuteAsync();
                _account = result.Account;
            }
            catch (NullReferenceException)
            {
                // No cached account.
                result = await _msal
                    .AcquireTokenInteractive(new[] { resourceUri })
                    .WithUseEmbeddedWebView(false)
                    .ExecuteAsync();
                _account = result.Account;
            }

            _currentAccessToken = result.AccessToken;
            _accessTokenExpiresAt = result.ExpiresOn;

            // Hand the token to ServiceClient via its delegate constructor.
            var instanceUri = new Uri(url);
            var client = new ServiceClient(
                instanceUrl: instanceUri,
                tokenProviderFunction: _ => RefreshAccessTokenAsync(),
                useUniqueInstance: true);

            if (!client.IsReady)
            {
                client.Dispose();
                return (false, client.LastError ?? "Failed to connect to Dataverse.");
            }

            // End-to-end probe.
            try
            {
                _ = await Task.Run(() => client.Execute(new WhoAmIRequest()));
            }
            catch (Exception probe)
            {
                client.Dispose();
                return (false, $"Connected but the environment refused the request: {probe.Message}");
            }

            Client = client;
            CurrentConnection = new ConnectionDetail
            {
                Url = url,
                ConnectionName = client.ConnectedOrgFriendlyName,
                OrganizationFriendlyName = client.ConnectedOrgFriendlyName,
                OrganizationVersion = client.ConnectedOrgVersion?.ToString(),
                AuthMode = AuthMode.OAuth,
            };
            return (true, null);
        }
        catch (MsalServiceException ex) when (ex.ErrorCode == "authorization_declined" || ex.ErrorCode == "access_denied")
        {
            return (false, "Sign-in was declined.");
        }
        catch (MsalClientException ex) when (
            ex.ErrorCode == "authentication_canceled" ||
            ex.ErrorCode == "user_canceled" ||
            ex.ErrorCode is not null && ex.ErrorCode.Contains("cache", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Sign-in was cancelled or Keychain access was denied. The token cache could not be created securely; sign-in was aborted.");
        }
        catch (MsalException ex)
        {
            return (false, $"Sign-in failed: {ex.Message}");
        }
        catch (Exception ex) when (ex.Message.Contains("keychain", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Keychain access was denied. XrmToolBox needs Keychain access to securely store your sign-in token.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<string> RefreshAccessTokenAsync()
    {
        if (_currentAccessToken is not null && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _currentAccessToken;
        }

        if (_msal is null || _currentResourceUri is null)
        {
            throw new InvalidOperationException("Not connected — call ConnectInteractiveAsync first.");
        }

        var resourceUri = _currentResourceUri + "/.default";
        var result = await _msal
            .AcquireTokenSilent(new[] { resourceUri }, _account)
            .ExecuteAsync();
        _currentAccessToken = result.AccessToken;
        _accessTokenExpiresAt = result.ExpiresOn;
        return _currentAccessToken;
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

        // VerifyPersistence will throw if the keychain is unavailable or the
        // user denied access — we propagate so ConnectInteractiveAsync's
        // catch turns it into a visible failure instead of silently
        // falling back.
        var helper = await MsalCacheHelper.CreateAsync(storageProperties);
        helper.VerifyPersistence();
        helper.RegisterCache(cache);
    }

    public IOrganizationService? GetOrganizationService() => Client;

    public void Disconnect()
    {
        Client?.Dispose();
        Client = null;
        CurrentConnection = null;
        _currentAccessToken = null;
        _accessTokenExpiresAt = DateTimeOffset.MinValue;
        _account = null;
        _msal = null;
    }
}
