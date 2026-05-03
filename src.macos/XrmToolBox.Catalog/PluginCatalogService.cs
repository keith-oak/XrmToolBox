using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace XrmToolBox.Catalog;

public sealed class PluginCatalogService
{
    public const string DefaultFeedUrl = "https://www.xrmtoolbox.com/_odata/plugins?$format=json";
    private const int MaxPages = 100;

    private readonly HttpClient _http;
    private readonly string _feedUrl;
    private readonly string _cachePath;
    private readonly TimeSpan _staleness;

    public PluginCatalogService(
        HttpClient? http = null,
        string? feedUrl = null,
        string? cachePath = null,
        TimeSpan? staleness = null)
    {
        _http = http ?? CreateDefaultHttpClient();
        _feedUrl = feedUrl ?? DefaultFeedUrl;
        _cachePath = cachePath ?? DefaultCachePath();
        _staleness = staleness ?? TimeSpan.FromHours(24);
    }

    public string CachePath => _cachePath;

    public async Task<IReadOnlyList<CatalogPlugin>> GetPluginsAsync(
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (!forceRefresh && TryReadCache(out var cached))
            return cached!;

        var fresh = await FetchAsync(ct).ConfigureAwait(false);
        WriteCache(fresh);
        return fresh;
    }

    public async Task<IReadOnlyList<CatalogPlugin>> FetchAsync(CancellationToken ct = default)
    {
        var all = new List<CatalogPlugin>();
        string? next = _feedUrl;
        var pageNum = 0;

        while (next is not null)
        {
            if (++pageNum > MaxPages)
                throw new InvalidOperationException(
                    $"Catalogue feed exceeded {MaxPages} pages — possible loop.");

            using var resp = await _http.GetAsync(next, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var page = await JsonSerializer.DeserializeAsync<CatalogPage>(
                stream, JsonOpts, ct).ConfigureAwait(false)
                ?? throw new InvalidDataException("Catalogue feed returned null page.");

            all.AddRange(page.Value);
            next = page.NextLink;
        }

        return all;
    }

    private bool TryReadCache(out IReadOnlyList<CatalogPlugin>? plugins)
    {
        plugins = null;
        try
        {
            if (!File.Exists(_cachePath)) return false;
            var info = new FileInfo(_cachePath);
            if (DateTime.UtcNow - info.LastWriteTimeUtc > _staleness) return false;

            using var s = File.OpenRead(_cachePath);
            var page = JsonSerializer.Deserialize<CatalogPage>(s, JsonOpts);
            if (page is null) return false;
            plugins = page.Value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void WriteCache(IReadOnlyList<CatalogPlugin> plugins)
    {
        try
        {
            var dir = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var page = new CatalogPage { Value = plugins.ToList() };
            using var s = File.Create(_cachePath);
            JsonSerializer.Serialize(s, page, JsonOpts);
        }
        catch
        {
            // cache write is best-effort
        }
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("PACdToolbox-Catalog", "1.0"));
        c.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return c;
    }

    private static string DefaultCachePath()
    {
        // Cross-platform: %LOCALAPPDATA% on Windows, ~/.local/share on Linux,
        // ~/Library/Application Support on macOS via Environment.SpecialFolder.LocalApplicationData.
        var baseDir = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);
        return Path.Combine(baseDir, "PACdToolbox", "catalog.json");
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
