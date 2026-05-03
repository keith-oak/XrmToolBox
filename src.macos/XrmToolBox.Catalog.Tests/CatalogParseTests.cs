using System.Text.Json;
using Xunit;

namespace XrmToolBox.Catalog.Tests;

public class CatalogParseTests
{
    private static readonly string FixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Theory]
    [InlineData("Cinteros.Xrm.FetchXmlBuilder.json", "Jonas Rapp", true)]
    [InlineData("Cinteros.XrmToolBox.PluginTraceViewer.json", "Jonas Rapp", true)]
    [InlineData("Cinteros.XrmToolBox.BulkDataUpdater.json", "Jonas Rapp", true)]
    [InlineData("DLaB.Xrm.EarlyBoundGeneratorV2.json", "Daryl LaBar", true)]
    [InlineData("Xrm.Sdk.PluginRegistration.json", "Microsoft", true)]
    public void Parses_OData_Page_For_Each_Ported_Plugin(
        string fixture, string authorContains, bool expectedOpenSource)
    {
        var path = Path.Combine(FixturesDir, fixture);
        Assert.True(File.Exists(path), $"missing fixture: {path}");

        var json = File.ReadAllText(path);
        var page = JsonSerializer.Deserialize<CatalogPage>(json, Opts);
        Assert.NotNull(page);
        Assert.NotEmpty(page!.Value);

        var p = page.Value[0];
        Assert.False(string.IsNullOrWhiteSpace(p.NugetId));
        Assert.False(string.IsNullOrWhiteSpace(p.Authors));
        Assert.Contains(authorContains, p.Authors!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedOpenSource, p.IsOpenSource);
    }

    [Fact]
    public void Query_Filters_To_Open_Source_Only_By_Default()
    {
        var plugins = new[]
        {
            new CatalogPlugin { NugetId = "a", Name = "A", IsOpenSource = true, TotalDownloads = 100 },
            new CatalogPlugin { NugetId = "b", Name = "B", IsOpenSource = false, TotalDownloads = 999 },
        };

        var result = new CatalogQuery().Apply(plugins).ToList();

        Assert.Single(result);
        Assert.Equal("a", result[0].NugetId);
    }

    [Fact]
    public void Query_Sort_By_Downloads_Default()
    {
        var plugins = new[]
        {
            new CatalogPlugin { NugetId = "low", IsOpenSource = true, TotalDownloads = 10 },
            new CatalogPlugin { NugetId = "high", IsOpenSource = true, TotalDownloads = 1000 },
            new CatalogPlugin { NugetId = "mid", IsOpenSource = true, TotalDownloads = 100 },
        };

        var result = new CatalogQuery().Apply(plugins).Select(p => p.NugetId).ToList();

        Assert.Equal(new[] { "high", "mid", "low" }, result);
    }

    [Fact]
    public void Query_Search_Matches_Name_Description_Author()
    {
        var plugins = new[]
        {
            new CatalogPlugin { NugetId = "x", Name = "FetchXML Builder", IsOpenSource = true, TotalDownloads = 1 },
            new CatalogPlugin { NugetId = "y", Name = "Other", Description = "fetchxml is great", IsOpenSource = true, TotalDownloads = 2 },
            new CatalogPlugin { NugetId = "z", Name = "Other", Authors = "Jonas Rapp", IsOpenSource = true, TotalDownloads = 3 },
            new CatalogPlugin { NugetId = "miss", Name = "Other", IsOpenSource = true, TotalDownloads = 4 },
        };

        var result = new CatalogQuery(Search: "fetch").Apply(plugins).Select(p => p.NugetId).ToList();
        Assert.Equal(new[] { "y", "x" }, result); // y has higher downloads

        var byAuthor = new CatalogQuery(Search: "rapp").Apply(plugins).Select(p => p.NugetId).ToList();
        Assert.Equal(new[] { "z" }, byAuthor);
    }

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
