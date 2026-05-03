using System.Xml.Linq;
using XrmToolBox.Extensibility;

namespace XrmToolBox.MacOS.Connection;

/// <summary>
/// Reads upstream <c>MscrmTools.ConnectionsList.xml</c> files (plus any sibling
/// per-file XML the user has) and converts them into our catalogue schema. The
/// upstream XML is tolerant and slightly under-specified, so we read by
/// element name and shrug at unknown nodes.
/// </summary>
public sealed class ConnectionsListXmlImporter
{
    public sealed record Result(int Files, int Connections, int NeedsAttention);

    public Result Import(string xmlPath, ConnectionCatalogueStore catalogue)
    {
        if (!File.Exists(xmlPath)) return new Result(0, 0, 0);

        var doc = XDocument.Load(xmlPath);
        var root = doc.Root;
        if (root is null) return new Result(0, 0, 0);

        // Two upstream shapes: ConnectionsList containing Files containing
        // FileInfo elements that point at sibling XML, and the per-file shape
        // where the document itself is a Connections collection. Handle both.
        var connectionElements = root.Descendants("ConnectionDetail").ToList();
        if (connectionElements.Count == 0)
        {
            connectionElements = root.Descendants("Connection").ToList();
        }

        var addedFiles = 0;
        var addedConnections = 0;
        var needsAttention = 0;

        // Group by the source-file name we infer from the element's ancestor
        // FileInfo, otherwise everything goes into a single "Imported" file.
        IEnumerable<IGrouping<string, XElement>> groups = connectionElements
            .GroupBy(e =>
            {
                var fileInfo = e.Ancestors().FirstOrDefault(a => a.Name.LocalName == "FileInfo");
                var name = fileInfo?.Element("Name")?.Value
                           ?? fileInfo?.Attribute("Name")?.Value
                           ?? Path.GetFileNameWithoutExtension(xmlPath);
                return string.IsNullOrEmpty(name) ? "Imported" : name;
            });

        foreach (var group in groups)
        {
            var fileName = ResolveCollidingName(catalogue, group.Key);
            var file = catalogue.EnsureFile(fileName);
            addedFiles++;

            foreach (var node in group)
            {
                var detail = MapConnection(node, out var unsupported);
                if (detail is null) continue;
                detail.ParentFileId = file.Id;
                file.Connections.Add(detail);
                addedConnections++;
                if (unsupported) needsAttention++;
            }
        }

        catalogue.Save();
        return new Result(addedFiles, addedConnections, needsAttention);
    }

    private static string ResolveCollidingName(ConnectionCatalogueStore catalogue, string desired)
    {
        var existing = catalogue.Current.Files.FirstOrDefault(f =>
            string.Equals(f.Name, desired, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return desired;
        return desired + " (imported)";
    }

    private static ConnectionDetail? MapConnection(XElement node, out bool unsupported)
    {
        unsupported = false;
        string? Pick(params string[] names)
        {
            foreach (var n in names)
            {
                var v = node.Element(n)?.Value;
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            }
            return null;
        }

        var url = Pick("OriginalUrl", "ServerName", "Url", "OrganizationServiceUrl", "WebApplicationUrl");
        if (string.IsNullOrWhiteSpace(url)) return null;

        // Strip any path; we want the org base URL.
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            url = parsed.GetLeftPart(UriPartial.Authority);
        }

        var authMode = Pick("AuthType", "AuthenticationProviderType", "ConnectionType");
        var resolvedAuth = ResolveAuthMode(authMode, out var authIsUnsupported);
        unsupported = authIsUnsupported;

        var d = new ConnectionDetail
        {
            ConnectionId = Guid.TryParse(Pick("ConnectionId"), out var id) ? id : Guid.NewGuid(),
            ConnectionName = Pick("ConnectionName") ?? url,
            Url = url,
            UserName = Pick("UserName"),
            OrganizationFriendlyName = Pick("OrganizationFriendlyName", "Organization"),
            OrganizationVersion = Pick("OrganizationVersion"),
            OrganizationDataServiceUrl = Pick("OrganizationDataServiceUrl"),
            AuthMode = resolvedAuth,
            AzureAdAppId = Pick("AzureAdAppId", "ClientId"),
            Tenant = Pick("Tenant", "TenantId"),
            CertificateThumbprint = Pick("CertificateThumbprint", "Thumbprint"),
            Note = authIsUnsupported ? $"unsupported auth mode {authMode}; convert to OAuth before use" : null,
            LastUsed = DateTimeOffset.TryParse(Pick("LastUsedOn", "LastUsed"), out var dt) ? dt : DateTimeOffset.MinValue,
        };

        return d;
    }

    private static AuthMode ResolveAuthMode(string? authMode, out bool unsupported)
    {
        unsupported = false;
        if (string.IsNullOrEmpty(authMode)) return AuthMode.OAuth;

        var normalised = authMode.Trim().ToLowerInvariant();
        if (normalised.Contains("oauth")) return AuthMode.OAuth;
        if (normalised.Contains("device")) return AuthMode.DeviceCode;
        if (normalised.Contains("clientsecret") || normalised.Contains("client_secret") || normalised.Contains("s2s"))
            return AuthMode.ClientSecret;
        if (normalised.Contains("certificate")) return AuthMode.Certificate;

        // ifd, ad, onpremises, livefederation: unsupported on Mac.
        unsupported = true;
        return AuthMode.OAuth;
    }
}
