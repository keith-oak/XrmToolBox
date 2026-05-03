using System.Text.Json.Serialization;

namespace XrmToolBox.Catalog;

public sealed record CatalogPlugin
{
    [JsonPropertyName("mctools_pluginid")] public string? PluginId { get; init; }
    [JsonPropertyName("mctools_nugetid")] public string? NugetId { get; init; }
    [JsonPropertyName("mctools_name")] public string? Name { get; init; }
    [JsonPropertyName("mctools_version")] public string? Version { get; init; }
    [JsonPropertyName("mctools_description")] public string? Description { get; init; }
    [JsonPropertyName("mctools_authors")] public string? Authors { get; init; }
    [JsonPropertyName("mctools_categorieslist")] public string? Categories { get; init; }
    [JsonPropertyName("mctools_totaldownloadcount")] public long TotalDownloads { get; init; }
    [JsonPropertyName("mctools_averagedownloadcount")] public string? AverageDownloads { get; init; }
    [JsonPropertyName("mctools_averagefeedbackratingallversions")] public string? AverageRating { get; init; }
    [JsonPropertyName("mctools_totalfeedbackallversion")] public int TotalFeedback { get; init; }
    [JsonPropertyName("mctools_downloadurl")] public string? DownloadUrl { get; init; }
    [JsonPropertyName("mctools_files")] public string? Files { get; init; }
    [JsonPropertyName("mctools_firstreleasedate")] public DateTimeOffset? FirstReleaseDate { get; init; }
    [JsonPropertyName("mctools_latestreleasedate")] public DateTimeOffset? LatestReleaseDate { get; init; }
    [JsonPropertyName("mctools_isopensource")] public bool IsOpenSource { get; init; }
    [JsonPropertyName("mctools_logourl")] public string? LogoUrl { get; init; }
    [JsonPropertyName("mctools_projecturl")] public string? ProjectUrl { get; init; }
    [JsonPropertyName("mctools_licenseurl")] public string? LicenseUrl { get; init; }
    [JsonPropertyName("mctools_xrmtoolboxversiondependency")] public string? XrmToolBoxVersionDependency { get; init; }
    [JsonPropertyName("mctools_latestreleasenote")] public string? LatestReleaseNote { get; init; }
    [JsonPropertyName("mctools_requirelicenseacceptance")] public bool RequireLicenseAcceptance { get; init; }

    public string DisplayName => Name ?? NugetId ?? "(unknown)";
}

public sealed record CatalogPage
{
    [JsonPropertyName("odata.metadata")] public string? OdataV3Metadata { get; init; }
    [JsonPropertyName("@odata.metadata")] public string? OdataV4Metadata { get; init; }
    [JsonPropertyName("odata.nextLink")] public string? OdataV3NextLink { get; init; }
    [JsonPropertyName("@odata.nextLink")] public string? OdataV4NextLink { get; init; }
    [JsonPropertyName("value")] public List<CatalogPlugin> Value { get; init; } = new();

    public string? NextLink => OdataV4NextLink ?? OdataV3NextLink;
    public string? Metadata => OdataV4Metadata ?? OdataV3Metadata;
}
