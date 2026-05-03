using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XrmToolBox.MacOS.Settings;

public sealed class AppSettings
{
    [JsonPropertyName("$schema")]
    public int Schema { get; set; } = 1;

    public string ThemeOverride { get; set; } = "auto"; // auto | light | dark
    public string DefaultDataverseUrl { get; set; } = "https://yourorg.crm.dynamics.com";

    public WindowPlacement? Window { get; set; }

    public List<RecentConnection> RecentConnections { get; set; } = new();

    public List<string> LastOpenedPlugins { get; set; } = new();

    public List<Favourite> Favourites { get; set; } = new();

    public bool ConnectAtStartup { get; set; }
}

public sealed class Favourite
{
    public string PluginTypeName { get; set; } = string.Empty;
    public Guid? ConnectionId { get; set; }
}

public sealed class WindowPlacement
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 1100;
    public double Height { get; set; } = 720;
    public bool IsMaximised { get; set; }
}

public sealed class RecentConnection
{
    public string Url { get; set; } = string.Empty;
    public string OrganizationFriendlyName { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "OAuth";
    public DateTimeOffset LastUsed { get; set; } = DateTimeOffset.UtcNow;

    public string DisplayLabel => string.IsNullOrEmpty(OrganizationFriendlyName)
        ? Url
        : $"{OrganizationFriendlyName} — {Url}";
}
