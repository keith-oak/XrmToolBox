# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

XrmToolBox is a Windows Forms (.NET Framework) host application for tools that connect to the Microsoft Common Data Service for Apps / Dataverse / Dynamics 365 CE. It is fundamentally a **plugin host** — the shell discovers and loads third-party tools at runtime via MEF (Managed Extensibility Framework). The fork at `MscrmTools/XrmToolBox` is the canonical upstream; recent merges in git history come from there.

## Build & Run

This is a **Windows-only Visual Studio solution**. Building from macOS/Linux is not supported (WinForms + .NET Framework 4.8). Use Visual Studio 2022 (`v17.13+` per the .sln) or `msbuild` on Windows.

```powershell
# Restore packages (uses packages.config — old NuGet style, not PackageReference everywhere)
nuget restore XrmToolBox.sln

# Build
msbuild XrmToolBox.sln /p:Configuration=Release /p:Platform="Any CPU"

# Run (after build)
.\XrmToolBox\bin\Release\XrmToolBox.exe
```

There are **no automated tests** in this repo and **no CI config files** beyond the AppVeyor badge in the README (CI lives on the upstream AppVeyor project, not in `.github/workflows/`).

## Solution Structure & Big Picture

Six projects in `XrmToolBox.sln`:

| Project | Type | Target | Role |
|---------|------|--------|------|
| `XrmToolBox` | WinExe | net48 | Main shell app — startup, MDI host, plugin loader, settings UI |
| `XrmToolBox.Extensibility` | Library | net48 | **Public SDK** that plugin authors reference. Contains `PluginBase`, `PluginControlBase`, `IXrmToolBoxPlugin`, all interfaces |
| `XrmToolBox.AutoUpdater` | WinExe | net462 | Standalone updater launched by the shell to swap in new binaries |
| `XrmToolBox.PluginsStore` | Library | net48 | Legacy NuGet-based plugin store (being phased out — see `Commands/NugetCleanup.Command.xml`) |
| `XrmToolBox.ToolLibrary` | Library | net48 | Current tool catalogue / library subsystem (the replacement for PluginsStore) |
| `Plugins/MsCrmTools.SampleTool` | Library | net48 | **Reference implementation** showing how to write a plugin against the SDK |

`Commands/` holds first-run XML "migration" scripts (e.g. `NugetCleanup.Command.xml`) that the shell processes at startup to clean up files left by previous versions. These run via simple `IF EXIST ... DEL` shell commands and are how the project ships breaking-change cleanups to existing installs.

## Plugin Architecture (the bit that matters)

Plugin discovery is **MEF-based, file-system-driven**. Read these together to understand it:

- `XrmToolBox/PluginManagerExtended.cs` — uses `DirectoryCatalog` + `AggregateCatalog` + `CompositionContainer` to scan a folder for plugin DLLs.
- `XrmToolBox.Extensibility/Paths.cs` — defines the convention: plugins live in `<XrmToolBoxPath>/Plugins/`. Each plugin DLL is dropped into that folder and discovered automatically.
- `XrmToolBox.Extensibility/Interfaces/IXrmToolBoxPlugin.cs` — the contract every plugin implements.
- `XrmToolBox.Extensibility/PluginBase.cs` — base class plugin authors derive from.
- `XrmToolBox.Extensibility/PluginControlBase.cs` + `MultipleConnectionsPluginControlBase.cs` — base UserControls that give a plugin its UI surface, async worker (`Worker.cs` / `WorkAsyncInfo.cs`), connection access, settings persistence (`SettingsManager.cs`), and logging (`LogManager.cs`).

**A plugin is exported via MEF attributes** on a class derived from `PluginBase`. Look at `Plugins/MsCrmTools.SampleTool/Plugin.cs` for the canonical pattern:

```csharp
[Export(typeof(IXrmToolBoxPlugin)),
 ExportMetadata("Name", "..."),
 ExportMetadata("Description", "..."),
 ExportMetadata("SmallImageBase64", "..."),
 ExportMetadata("BigImageBase64", "..."),
 ExportMetadata("BackgroundColor", "..."),
 ExportMetadata("PrimaryFontColor", "..."),
 ExportMetadata("SecondaryFontColor", "...")]
public class MyPlugin : PluginBase { ... }
```

`ExportMetadata` keys are **not optional** — the shell's plugin tile UI reads them via `IPluginMetadata` / `IPluginMetadataExt`. If you add a new metadata field, both interfaces and the consuming UI in `XrmToolBox/` need updating in lockstep.

### Optional plugin capability interfaces

The `XrmToolBox.Extensibility/Interfaces/` folder is a menu of opt-in capabilities. A plugin advertises a feature by also implementing the matching interface — there is no central registry. Examples worth knowing:

- `INoConnectionRequired` — plugin doesn't need a Dataverse connection.
- `IGitHubPlugin` / `ICodePlexPlugin` / `IHelpPlugin` / `IPayPalPlugin` / `IAboutPlugin` — surfaces links/menus in the host UI.
- `IMessageBusHost` — lets plugins talk to each other via a typed message bus.
- `IShortcutReceiver` — opt into keyboard shortcut delivery.
- `IStatusBarMessenger` (preferred) / `IStatusBarMessager` (legacy typo, kept for backwards compat) — write to the host status bar.
- `IDuplicatableTool` — host can open multiple instances of the tool.
- `ISettingsPlugin` — persistent settings hook.
- `ICompanion` + `XrmToolBox.Extensibility.UserControls` — declare a "companion" tool that pairs with the main one.

Sample plugin `MsCrmTools.SampleTool/SampleTool.cs` implements many of these together — use it as the reference when adding a new capability interface.

## Conventions & Gotchas

- **Two NuGet styles coexist**: `XrmToolBox.csproj` uses `packages.config` (95 references); `XrmToolBox.Extensibility.csproj` mixes `<Reference>` and `<PackageReference>`. Don't try to "modernise" one without the other — assembly redirects in `app.config` depend on exact versions, and `Program.cs` has manual `RedirectAssembly` logic for `Microsoft.IdentityModel` etc.
- **Connection layer is external**: `MscrmTools.Xrm.Connection` is a separate NuGet package (separate repo). Connectivity bugs belong **there**, not here — see `.github/issue_template.md`.
- **The `Plugins/` solution folder ≠ a runtime directory** — it's just where the sample plugin source lives. At runtime, plugins are loaded from a folder next to the EXE.
- **Recent change pattern (#1424, copying plugins during startup)**: the shell copies plugins out of source locations into the runtime `Plugins/` folder during startup, with version-issue handling. If you touch startup code in `Program.cs` or `PluginManagerExtended.cs`, preserve this behaviour.
- **No CONTRIBUTING.md, no .editorconfig, no lint config.** Match the surrounding code style of whichever file you're editing.
- **Don't add issues here for plugins from other authors** — the issue template explicitly redirects users to plugin-specific repos.

## Where to add what

| You want to... | Edit |
|---|---|
| Add a host-level UI feature | `XrmToolBox/` (Forms, Controls, AppCode) |
| Expose a new capability to plugins | Add an interface to `XrmToolBox.Extensibility/Interfaces/`, then consume it in `XrmToolBox/` |
| Change the plugin contract | `XrmToolBox.Extensibility/PluginBase.cs` + `IXrmToolBoxPlugin.cs` (breaking change — bump assembly version, every third-party plugin will need a rebuild) |
| Ship a startup cleanup for installed users | New XML file in `Commands/` |
| Test a plugin SDK change end-to-end | Build, then run `MsCrmTools.SampleTool` from the Plugins solution folder |
