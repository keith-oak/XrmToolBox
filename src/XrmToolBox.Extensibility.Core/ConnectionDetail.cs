namespace XrmToolBox.Extensibility;

public sealed class ConnectionDetail
{
    public Guid ConnectionId { get; set; } = Guid.NewGuid();
    public string ConnectionName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? OrganizationFriendlyName { get; set; }
    public string? OrganizationDataServiceUrl { get; set; }
    public string? OrganizationVersion { get; set; }

    public AuthMode AuthMode { get; set; } = AuthMode.OAuth;

    /// <summary>
    /// Azure AD app id used for ClientSecret/Certificate flows. For interactive
    /// OAuth this is left null and the shell substitutes the well-known
    /// Dataverse public client id.
    /// </summary>
    public string? AzureAdAppId { get; set; }

    /// <summary>Optional explicit tenant id (GUID or domain). Defaults to "organizations" when null.</summary>
    public string? Tenant { get; set; }

    /// <summary>Thumbprint for Certificate auth, looked up in the user's keychain at sign-in time.</summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Opaque reference into the secret store (Keychain on macOS) for ClientSecret auth.
    /// The secret itself is NEVER persisted to disk.
    /// </summary>
    public string? ClientSecretSecretRef { get; set; }

    public Guid? ImpersonatedUserId { get; set; }

    /// <summary>FK to <see cref="ConnectionFile.Id"/>; null means top-level (rare).</summary>
    public Guid? ParentFileId { get; set; }

    public DateTimeOffset LastUsed { get; set; } = DateTimeOffset.MinValue;

    /// <summary>Free-form note populated by the importer when an upstream auth mode is unsupported.</summary>
    public string? Note { get; set; }

    public override string ToString() =>
        string.IsNullOrEmpty(ConnectionName) ? Url : $"{ConnectionName} ({Url})";
}

public enum AuthMode
{
    OAuth,
    DeviceCode,
    ClientSecret,
    Certificate,
}
