namespace XrmToolBox.Extensibility;

public sealed class ConnectionDetail
{
    public string ConnectionName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? OrganizationFriendlyName { get; set; }
    public string? OrganizationDataServiceUrl { get; set; }
    public string? OrganizationVersion { get; set; }
    public Guid ConnectionId { get; set; } = Guid.NewGuid();
    public AuthMode AuthMode { get; set; } = AuthMode.OAuth;

    public override string ToString() =>
        string.IsNullOrEmpty(ConnectionName) ? Url : $"{ConnectionName} ({Url})";
}

public enum AuthMode
{
    OAuth,
    ClientSecret,
    Certificate,
    DeviceCode,
}
