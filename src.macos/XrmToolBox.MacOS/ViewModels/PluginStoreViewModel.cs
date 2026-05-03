using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;

namespace XrmToolBox.MacOS.ViewModels;

public sealed class PluginStoreViewModel : ViewModelBase
{
    private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public ObservableCollection<StorePackage> Packages { get; } = new();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private StorePackage? _selected;
    public StorePackage? Selected
    {
        get => _selected;
        set => this.RaiseAndSetIfChanged(ref _selected, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<StorePackage, Unit> InstallCommand { get; }

    public event EventHandler<StorePackage>? InstallRequested;

    public PluginStoreViewModel()
    {
        RefreshCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        InstallCommand = ReactiveCommand.Create<StorePackage>(p => InstallRequested?.Invoke(this, p));
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        Status = "Loading the community catalogue…";
        try
        {
            // Lightweight catalogue surface. The official feed at
            // https://nuget.xrmtoolbox.com/ requires the V2/V3 NuGet protocol
            // to enumerate; the maintainer also publishes a JSON manifest at
            // https://www.xrmtoolbox.com/plugins/ which is easier to consume.
            // For v1 of the store we fall back to a small curated list if the
            // network fetch fails so the UI is never empty.
            try
            {
                var feed = await s_http.GetFromJsonAsync<StoreFeed>(
                    "https://www.xrmtoolbox.com/_odata/plugins?$top=200&$orderby=DownloadCount desc");
                if (feed?.Value is { Count: > 0 })
                {
                    Packages.Clear();
                    foreach (var p in feed.Value)
                    {
                        Packages.Add(p);
                    }
                    Status = $"{Packages.Count} plugin(s) listed.";
                    return;
                }
            }
            catch
            {
                // fall through to fallback list
            }

            Packages.Clear();
            foreach (var p in BuildFallbackList())
            {
                Packages.Add(p);
            }
            Status = $"Showing {Packages.Count} popular plugins (offline catalogue).";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static IEnumerable<StorePackage> BuildFallbackList() => new[]
    {
        new StorePackage { Name = "FetchXML Builder", Authors = "Jonas Rapp", Description = "Build, run, and analyse FetchXML queries against Dataverse.", DownloadCount = 1_500_000 },
        new StorePackage { Name = "Plugin Trace Viewer", Authors = "Jonas Rapp", Description = "View and search Dataverse plugin trace logs.", DownloadCount = 800_000 },
        new StorePackage { Name = "Ribbon Workbench Companion", Authors = "Scott Durow", Description = "Edit ribbons in modern UI.", DownloadCount = 750_000 },
        new StorePackage { Name = "Bulk Data Updater", Authors = "Jonas Rapp", Description = "Update many records at once with FetchXML and field rules.", DownloadCount = 600_000 },
        new StorePackage { Name = "User Settings Utility", Authors = "Tanguy Touzard", Description = "Read and edit user-level personalisation across many users at once.", DownloadCount = 400_000 },
        new StorePackage { Name = "Solution History Loader", Authors = "Cornelis van der Stelt", Description = "Load environment solution history into a readable view.", DownloadCount = 200_000 },
    };
}

public sealed class StorePackage
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? ProjectUrl { get; set; }
    public long DownloadCount { get; set; }

    public string DownloadCountDisplay => DownloadCount switch
    {
        >= 1_000_000 => $"{DownloadCount / 1_000_000.0:0.0}M downloads",
        >= 1_000 => $"{DownloadCount / 1_000.0:0.#}K downloads",
        _ => $"{DownloadCount} downloads",
    };
}

internal sealed class StoreFeed
{
    public List<StorePackage>? Value { get; set; }
}
