namespace XrmToolBox.Catalog;

public enum CatalogSort
{
    Downloads,
    Newest,
    Name,
    Rating,
}

public sealed record CatalogQuery(
    string? Search = null,
    bool OpenSourceOnly = true,
    CatalogSort Sort = CatalogSort.Downloads)
{
    public IEnumerable<CatalogPlugin> Apply(IEnumerable<CatalogPlugin> source)
    {
        var q = source;
        if (OpenSourceOnly)
            q = q.Where(p => p.IsOpenSource);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            q = q.Where(p =>
                Contains(p.Name, s) ||
                Contains(p.NugetId, s) ||
                Contains(p.Description, s) ||
                Contains(p.Authors, s));
        }

        return Sort switch
        {
            CatalogSort.Newest => q.OrderByDescending(p => p.LatestReleaseDate ?? DateTimeOffset.MinValue),
            CatalogSort.Name => q.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase),
            CatalogSort.Rating => q.OrderByDescending(p => ParseDecimal(p.AverageRating)),
            _ => q.OrderByDescending(p => p.TotalDownloads),
        };
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static decimal ParseDecimal(string? s) =>
        decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;
}
