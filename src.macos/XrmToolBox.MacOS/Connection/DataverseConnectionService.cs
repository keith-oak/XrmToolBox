using System.Threading.Tasks;
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
        try
        {
            var connectionString =
                $"AuthType=OAuth;Url={url};LoginPrompt=Auto;TokenCacheStorePath={GetTokenCachePath()};RedirectUri=http://localhost;";
            var client = await Task.Run(() => new ServiceClient(connectionString));

            if (!client.IsReady)
            {
                return (false, client.LastError ?? "ServiceClient not ready");
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
            return (false, ex.Message);
        }
    }

    public IOrganizationService? GetOrganizationService() => Client;

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
