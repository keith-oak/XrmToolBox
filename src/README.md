# PAC'd Toolbox — native macOS build

Native macOS port of XrmToolBox, rebranded as **PAC'd Toolbox**. Cross-platform (macOS / Linux / Windows) shell built on [Avalonia](https://avaloniaui.net/) targeting .NET 10. Replaces the Windows-only WinForms shell with a modern, Apple HIG-aligned UI.

## Layout

| Project | Purpose |
| --- | --- |
| `XrmToolBox.Extensibility.Core` | UI-agnostic plugin SDK contracts (`IXrmToolBoxPlugin`, `IXrmToolBoxPluginControl`, `PluginBase`, `ConnectionDetail`, `IPluginMetadata`, …) |
| `XrmToolBox.MacOS` | Avalonia shell — branded UI, MSAL/Keychain auth, MEF plugin discovery, tabbed tool host |
| `Plugins/SampleTool` | Reference tool demonstrating the cross-platform plugin pattern |

The legacy Windows WinForms shell at the repository root is kept untouched; this directory is the parallel native build.

## Build & run

The proper way to launch on macOS is via the bundled `.app` — `dotnet run` does not produce a bundle, so the dock icon and `CFBundleName` won't be picked up by macOS LaunchServices.

```bash
# From repo root: build the .app bundle (osx-arm64 by default)
./scripts/build-macos-app.sh

# Launch via Finder/LaunchServices so the icon and name register
open "dist/PAC'd Toolbox.app"
```

For fast iteration during development you can still run the raw build (no icon, no native menu binding):

```bash
dotnet build src/Shell/XrmToolBox.MacOS.csproj
dotnet run  --project src/Shell/XrmToolBox.MacOS.csproj
```

Probe plugin discovery without launching the UI:

```bash
src/Shell/bin/Debug/net10.0/PACdToolbox --probe
```

For Intel Macs / signed builds / zipped distribution, see [`docs/macos-distribution.md`](../docs/macos-distribution.md).

## Shell features

The shell ships a modern macOS-style experience:

- **Sidebar nav** — Home (welcome + quick actions + recent tools), Tools (run installed tools), Manage Tools (install / uninstall / reload), Environments, Connections, Settings, About
- **Toolbar** — connect/disconnect with state-aware caption, recent-connections autocomplete, "Browse Tools" library overlay, draggable surface for window movement
- **Theme** — Auto / Light / Dark segmented control in Settings; live application via `Application.Current.RequestedThemeVariant`, persisted to settings
- **Command palette** — ⌘K to fuzzy-pick a tool from anywhere
- **Native menu bar** — File / View / Help with About, Settings (⌘,), Reload (⌘R), Quit
- **Window** — extended client area with traffic lights, persisted size + position
- **Status bar** — version, settings shortcut, open-tab count

## Plugin model

Plugins (a.k.a. *tools*) are discovered via MEF (`System.ComponentModel.Composition`) at runtime from `<shell-bin>/Plugins/**/*.dll`. A plugin exports `IXrmToolBoxPlugin` with mandatory `IPluginMetadata` attributes:

```csharp
[Export(typeof(IXrmToolBoxPlugin))]
[ExportMetadata("Name", "My Tool")]
[ExportMetadata("Description", "...")]
[ExportMetadata("BackgroundColor", "#6C5CE7")]
[ExportMetadata("PrimaryFontColor", "White")]
[ExportMetadata("SecondaryFontColor", "WhiteSmoke")]
[ExportMetadata("SmallImageBase64", "")]
[ExportMetadata("BigImageBase64", "")]
public sealed class MyPlugin : PluginBase
{
    public override IXrmToolBoxPluginControl GetControl() => new MyPluginControl();
}
```

`IXrmToolBoxPluginControl.GetView()` returns an Avalonia `Control` (typed as `object` so the SDK assembly stays UI-agnostic — only the plugin assembly references Avalonia).

The control should also implement `ResetConnection()` so the host can propagate disconnect events without disposing the tool, and gate any backend work on a non-null `IOrganizationService`.

## Connection layer

The Windows-only `MscrmTools.Xrm.Connection` is replaced with `Microsoft.PowerPlatform.Dataverse.Client`. Interactive sign-in uses MSAL (`Microsoft.Identity.Client`) with `MsalCacheHelper.WithMacKeyChain()` for token persistence in the macOS Keychain. Denying the keychain prompt now genuinely fails the sign-in (verified via `VerifyPersistence()`); previous flat-file fallback was removed.

Connection details (URL, friendly name, auth mode) are persisted to `~/Library/Application Support/PACdToolbox/settings.json` for the recents list. "Forget" removes the entry from settings (the keychain token is left for MSAL to evict on next sign-in).

## Brand

- **Primary**: `#6C5CE7` (electric purple)
- **Secondary**: `#0964E3`, `#00CEC9`, `#2ECC71`
- **Type**: Inter Tight, falling back to Inter (shipped via `Avalonia.Fonts.Inter`) and then system sans
- **Geometry**: 8px controls, 12px cards
- **Both Light and Dark variants** wired through `AppleTheme.axaml`

See [`Themes/AppleTheme.axaml`](XrmToolBox.MacOS/Themes/AppleTheme.axaml) for the full palette + tokens.

## Specs

Implementation history is tracked in [`specs/`](../specs/):

- `001-apple-design-system-theme.md` — **COMPLETE**
- `002-agentic-plugin-porter.md` — pending; auto-converts WinForms community tools
- `003-macos-app-bundle.md` — **COMPLETE**
- `004-usable-shell.md` — **COMPLETE (v1)**
