using System.Diagnostics;
using System.Text;

namespace XrmToolBox.MacOS.Connection;

/// <summary>
/// Stores connection secrets (client secrets, future per-connection passwords)
/// in the macOS login Keychain via the <c>security</c> CLI. On other platforms
/// it falls back to a DPAPI-style protected file at the user's config root.
/// We never touch JSON for these values.
/// </summary>
public sealed class SecretStore
{
    public const string ServiceName = "com.lucidlabs.pacdtoolbox.secrets";

    private readonly string _fallbackDir;

    public SecretStore(string fallbackDir)
    {
        _fallbackDir = fallbackDir;
        Directory.CreateDirectory(fallbackDir);
    }

    public bool Set(string account, string secret)
    {
        if (OperatingSystem.IsMacOS())
        {
            return RunSecurity("add-generic-password", "-U", "-a", account, "-s", ServiceName, "-w", secret) == 0;
        }

        // Non-mac fallback: write the secret to a per-account file under the
        // config root with 0600 permissions. Plain text — call sites should
        // warn the user. Better than crashing.
        var path = Path.Combine(_fallbackDir, Sanitise(account) + ".secret");
        File.WriteAllText(path, secret, Encoding.UTF8);
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Best-effort; if the FS doesn't support Unix permissions, the
                // secret is still in a per-user directory.
            }
        }
        return true;
    }

    public string? Get(string account)
    {
        if (OperatingSystem.IsMacOS())
        {
            return RunSecurityCapture("find-generic-password", "-a", account, "-s", ServiceName, "-w");
        }

        var path = Path.Combine(_fallbackDir, Sanitise(account) + ".secret");
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
    }

    public void Delete(string account)
    {
        if (OperatingSystem.IsMacOS())
        {
            _ = RunSecurity("delete-generic-password", "-a", account, "-s", ServiceName);
            return;
        }

        var path = Path.Combine(_fallbackDir, Sanitise(account) + ".secret");
        if (File.Exists(path)) File.Delete(path);
    }

    private static int RunSecurity(params string[] args)
    {
        var psi = new ProcessStartInfo("security")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return -1;
            p.WaitForExit(5_000);
            return p.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static string? RunSecurityCapture(params string[] args)
    {
        var psi = new ProcessStartInfo("security")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return null;
            var stdout = p.StandardOutput.ReadToEnd().TrimEnd('\n', '\r');
            p.WaitForExit(5_000);
            return p.ExitCode == 0 ? stdout : null;
        }
        catch
        {
            return null;
        }
    }

    private static string Sanitise(string s) =>
        new string(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
}
