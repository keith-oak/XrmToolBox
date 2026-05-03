using System.Text.Json;
using XrmToolBox.Extensibility;
using XrmToolBox.MacOS.Settings;

namespace XrmToolBox.MacOS.Connection;

public sealed class ConnectionCatalogueStore
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Path { get; }
    public ConnectionsCatalogue Current { get; private set; } = new();

    public event EventHandler? Changed;

    public ConnectionCatalogueStore(string configRoot)
    {
        Path = System.IO.Path.Combine(configRoot, "connections.json");
        Load();
    }

    public void Load()
    {
        if (!File.Exists(Path))
        {
            Current = new ConnectionsCatalogue();
            return;
        }

        try
        {
            using var stream = File.OpenRead(Path);
            Current = JsonSerializer.Deserialize<ConnectionsCatalogue>(stream, s_json) ?? new ConnectionsCatalogue();
        }
        catch
        {
            // Older schema or corrupted file — start fresh.
            Current = new ConnectionsCatalogue();
        }
    }

    public void Save()
    {
        var tmp = Path + ".tmp";
        using (var stream = File.Create(tmp))
        {
            JsonSerializer.Serialize(stream, Current, s_json);
        }
        File.Move(tmp, Path, overwrite: true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public ConnectionFile EnsureFile(string name)
    {
        var existing = Current.Files.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        var file = new ConnectionFile { Name = name };
        Current.Files.Add(file);
        return file;
    }

    public void AddOrUpdate(ConnectionDetail detail, Guid fileId)
    {
        var file = Current.Files.FirstOrDefault(f => f.Id == fileId)
                   ?? EnsureFile("Personal");

        // If detail already lives in some file, remove from there first.
        foreach (var f in Current.Files)
        {
            f.Connections.RemoveAll(c => c.ConnectionId == detail.ConnectionId);
        }

        detail.ParentFileId = file.Id;
        file.Connections.Add(detail);
        Save();
    }

    public void Remove(Guid connectionId)
    {
        foreach (var f in Current.Files)
        {
            f.Connections.RemoveAll(c => c.ConnectionId == connectionId);
        }
        if (Current.DefaultConnectionId == connectionId) Current.DefaultConnectionId = null;
        Save();
    }

    public void SetDefault(Guid? connectionId)
    {
        Current.DefaultConnectionId = connectionId;
        Save();
    }

    public void RecordLastUsed(Guid connectionId)
    {
        var detail = Current.FindById(connectionId);
        if (detail is null) return;
        detail.LastUsed = DateTimeOffset.UtcNow;
        Save();
    }

    /// <summary>
    /// One-time migration: if the catalogue is empty but settings.json has a
    /// flat RecentConnections list, seed a "Recent" file from it.
    /// </summary>
    public void MigrateFromLegacyRecent(IEnumerable<RecentConnection> legacyRecent)
    {
        if (Current.Files.Count > 0) return;
        var list = legacyRecent.ToList();
        if (list.Count == 0) return;

        var file = EnsureFile("Recent");
        foreach (var r in list)
        {
            if (string.IsNullOrWhiteSpace(r.Url)) continue;
            file.Connections.Add(new ConnectionDetail
            {
                Url = r.Url,
                ConnectionName = string.IsNullOrEmpty(r.OrganizationFriendlyName) ? r.Url : r.OrganizationFriendlyName,
                OrganizationFriendlyName = r.OrganizationFriendlyName,
                AuthMode = Enum.TryParse<AuthMode>(r.AuthMode, out var m) ? m : AuthMode.OAuth,
                LastUsed = r.LastUsed,
                ParentFileId = file.Id,
            });
        }
        Save();
    }
}
