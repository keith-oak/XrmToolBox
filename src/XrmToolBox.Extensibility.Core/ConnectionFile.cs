namespace XrmToolBox.Extensibility;

/// <summary>
/// A named group of <see cref="ConnectionDetail"/>s. Mirrors the upstream
/// "connection file" concept used by MscrmTools.Xrm.Connection.
/// </summary>
public sealed class ConnectionFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconBase64 { get; set; }
    public List<ConnectionDetail> Connections { get; set; } = new();
}

/// <summary>
/// Top-level catalogue of every known connection grouped by file. Pure data;
/// I/O lives in <c>ConnectionCatalogueStore</c>.
/// </summary>
public sealed class ConnectionsCatalogue
{
    public int Schema { get; set; } = 1;
    public List<ConnectionFile> Files { get; set; } = new();
    public Guid? DefaultConnectionId { get; set; }

    public ConnectionDetail? FindById(Guid id) =>
        Files.SelectMany(f => f.Connections).FirstOrDefault(c => c.ConnectionId == id);

    public ConnectionFile? FindFileForConnection(Guid connectionId) =>
        Files.FirstOrDefault(f => f.Connections.Any(c => c.ConnectionId == connectionId));
}
