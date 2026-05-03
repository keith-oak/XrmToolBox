using System.IO;
using System.Linq;
using System.Text.Json;

namespace XrmToolBox.MacOS.Settings;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string SettingsPath { get; }
    public string LogsDirectory { get; }
    public string PluginsDirectory { get; }

    public AppSettings Current { get; private set; } = new();

    public event EventHandler? Changed;

    public SettingsService()
    {
        var configRoot = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "XrmToolBox")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XrmToolBox");

        Directory.CreateDirectory(configRoot);
        SettingsPath = Path.Combine(configRoot, "settings.json");

        LogsDirectory = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "XrmToolBox")
            : Path.Combine(configRoot, "Logs");
        Directory.CreateDirectory(LogsDirectory);

        PluginsDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins");
        Directory.CreateDirectory(PluginsDirectory);

        Load();
    }

    public void Load()
    {
        if (!File.Exists(SettingsPath))
        {
            Current = new AppSettings();
            return;
        }

        try
        {
            using var stream = File.OpenRead(SettingsPath);
            Current = JsonSerializer.Deserialize<AppSettings>(stream, s_json) ?? new AppSettings();
        }
        catch
        {
            // Corrupt or older schema → start fresh, original file kept on disk.
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        var tmp = SettingsPath + ".tmp";
        using (var stream = File.Create(tmp))
        {
            JsonSerializer.Serialize(stream, Current, s_json);
        }
        File.Move(tmp, SettingsPath, overwrite: true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RecordConnection(string url, string organizationFriendlyName, string authMode)
    {
        var existing = Current.RecentConnections
            .FirstOrDefault(c => c.Url.Equals(url, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.OrganizationFriendlyName = organizationFriendlyName;
            existing.AuthMode = authMode;
            existing.LastUsed = DateTimeOffset.UtcNow;
        }
        else
        {
            Current.RecentConnections.Add(new RecentConnection
            {
                Url = url,
                OrganizationFriendlyName = organizationFriendlyName,
                AuthMode = authMode,
            });
        }

        Current.RecentConnections = Current.RecentConnections
            .OrderByDescending(c => c.LastUsed)
            .Take(10)
            .ToList();

        Save();
    }

    public void ForgetConnection(RecentConnection connection)
    {
        Current.RecentConnections.Remove(connection);
        Save();
    }
}
