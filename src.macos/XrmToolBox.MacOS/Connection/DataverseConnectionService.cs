using System.Threading.Tasks;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using XrmToolBox.Extensibility;

namespace XrmToolBox.MacOS.Connection;

public sealed class DataverseConnectionService
{
    public ServiceClient? Client { get; private set; }
    public ConnectionDetail? CurrentConnection { get; private set; }

    public async Task<(bool Success, string? Error)> ConnectInteractiveAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (false, "Enter a Dataverse environment URL.");
        }

        ServiceClient? client = null;
        try
        {
            var connectionString =
                $"AuthType=OAuth;Url={url};LoginPrompt=Auto;TokenCacheStorePath={GetTokenCachePath()};RedirectUri=http://localhost;";
            client = await Task.Run(() => new ServiceClient(connectionString));

            if (!client.IsReady)
            {
                client.Dispose();
                var msg = client.LastError;
                if (string.IsNullOrWhiteSpace(msg))
                {
                    msg = "Authentication did not complete. If you denied a Keychain prompt or cancelled the sign-in, click Connect to try again.";
                }
                return (false, msg);
            }

            // Verify the session is genuinely usable end-to-end. ServiceClient
            // sometimes reports IsReady=true even when the underlying token grant
            // was denied; a real Dataverse round-trip surfaces that immediately.
            try
            {
                _ = await Task.Run(() => (WhoAmIResponse)client.Execute(new WhoAmIRequest()));
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
        catch (Exception ex)
        {
            client?.Dispose();
            // MSAL/Keychain denial typically surfaces here as a cancelled
            // operation. Translate to something a user can act on.
            var msg = ex.Message;
            if (msg.Contains("cancelled", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("canceled", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("MsalClientException", StringComparison.OrdinalIgnoreCase))
            {
                msg = "Sign-in was cancelled or the Keychain access was denied. Click Connect to try again.";
            }
            return (false, msg);
        }
    }

    public IOrganizationService? GetOrganizationService() => Client;

    public void Disconnect()
    {
        Client?.Dispose();
        Client = null;
        CurrentConnection = null;
    }

    private static string GetTokenCachePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XrmToolBox",
            "TokenCache");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "msal.cache");
    }
}
