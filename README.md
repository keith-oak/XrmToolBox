# PAC'd Toolbox

A **modern, cross-platform reimagining of [XrmToolBox](https://www.xrmtoolbox.com/)** — built on Avalonia 11 + .NET 10, runs natively on macOS, Linux and Windows, and aims to host the existing XrmToolBox plugin ecosystem on the platforms it has historically been excluded from.

> **About the brand.** "PAC'd" is a nod to the **P**ower **A**pps **C**LI (`pac`) and the broader Power Platform tooling story. We use it as a working name to clearly distinguish this project from the canonical Windows-only XrmToolBox. The codebase deliberately preserves `XrmToolBox.*` namespaces, assembly identities and MEF metadata so the plugin contract stays familiar.

## What this project is

XrmToolBox by [Tanguy Touzard](https://github.com/MscrmTools) is the de-facto plugin host for the Microsoft Power Platform / Dataverse / Dynamics 365 community — a Windows Forms application on .NET Framework 4.8 that loads third-party tools via MEF. It is brilliant, mature, and Windows-only.

PAC'd Toolbox is an attempt to:

1. **Take the bones that work** — the MEF-based plugin model, the `IXrmToolBoxPlugin` SDK contract, the rich community of plugins authored over the last decade — and
2. **Re-clothe them in a modern, cross-platform shell** — Avalonia 11 UI with Apple HIG styling on macOS, native theming on Windows and Linux, MSAL-based authentication that works on every desktop OS, and a proper macOS `.app` bundle / Windows publish folder.

This repository is a **fork** of `MscrmTools/XrmToolBox`. The upstream Windows shell at the legacy paths (`/XrmToolBox`, `/XrmToolBox.Extensibility`, etc.) is kept intact for reference and to make merging upstream improvements possible. The new cross-platform shell lives under [`src/`](src/).

## Status — early / experimental

| Area | State |
| --- | --- |
| Cross-platform shell (Avalonia, .NET 10) | Working on macOS; Windows + Linux build clean (cross-compiled, not yet smoke-tested in anger) |
| MSAL + Keychain auth (macOS) | Working |
| MEF plugin discovery | Working |
| Native `.app` bundle (mac) | Working — `scripts/build-macos-app.sh` |
| Windows publish (zip) | Working — `scripts/build-windows-app.ps1` |
| 5 ported community plugins (BulkDataUpdater, PluginTraceViewer, FetchXmlBuilder, PluginRegistration, EarlyBoundGenerator) | Functional against real Dataverse environments |
| Plugin catalogue (OData fetch + search) | Implemented + tested; UI integration pending |
| Plugin auto-installer | Not yet — install via manual nupkg unpack into `<shell>/Plugins/` |

This is a personal experiment, not an official Microsoft or Tanguy Touzard project. **For production tooling, use the upstream [XrmToolBox](https://www.xrmtoolbox.com/).**

## Build & run

### macOS

```bash
./scripts/build-macos-app.sh
open "dist/PAC'd Toolbox.app"
```

### Windows

```powershell
.\scripts\build-windows-app.ps1
.\dist\PACdToolbox-2.0.0-win-x64\PACdToolbox.exe
```

### Linux

Cross-platform build also targets `linux-x64` and `linux-arm64`:

```bash
dotnet publish src/Shell/XrmToolBox.MacOS.csproj -c Release -r linux-x64 --self-contained false -o dist/linux-x64
./dist/linux-x64/PACdToolbox
```

### Tests

```bash
./scripts/test-all.sh
```

## Repo layout

```
src/
  Shell/                              # Avalonia shell — main app
  XrmToolBox.Extensibility.Core/      # plugin SDK contracts (UI-agnostic)
  XrmToolBox.Catalog/                 # OData catalogue client
  XrmToolBox.Catalog.Tests/           # xunit tests
  Plugins/                            # ported + sample plugins
  PACdToolbox.slnx                    # solution

specs/                                # spec-driven design docs
docs/
  porter-evidence/                    # licence audit + per-plugin port notes
  biznamics-issue/                    # tracking issue draft for Biznamics
  macos-distribution.md               # signing, notarisation, zip
scripts/                              # build scripts (mac, windows, tests)

XrmToolBox/                           # upstream Windows shell — UNTOUCHED
XrmToolBox.Extensibility/             # upstream plugin SDK     — UNTOUCHED
XrmToolBox.AutoUpdater/               # upstream updater        — UNTOUCHED
XrmToolBox.PluginsStore/              # upstream legacy store   — UNTOUCHED
XrmToolBox.ToolLibrary/               # upstream catalogue      — UNTOUCHED
Plugins/                              # upstream sample tool    — UNTOUCHED
```

## Plugin model

Plugins are discovered via MEF (`System.ComponentModel.Composition`) at runtime from `<shell-bin>/Plugins/**/*.dll`. The contract is unchanged from upstream:

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

The control's `GetView()` returns an Avalonia `Control` (typed as `object` so the SDK assembly stays UI-agnostic — only the plugin assembly references Avalonia).

## Connection layer

The Windows-only `MscrmTools.Xrm.Connection` is replaced with `Microsoft.PowerPlatform.Dataverse.Client`. Interactive sign-in uses MSAL (`Microsoft.Identity.Client`) with platform-specific token caches:

- **macOS** — Keychain via `MsalCacheHelper.WithMacKeyChain()`
- **Windows** — DPAPI via the default cache helper
- **Linux** — libsecret (when present)

## Licence audit

Every plugin we port has its licensing recorded in [`docs/porter-evidence/licenses.md`](docs/porter-evidence/licenses.md), with snapshots of each plugin's official XrmToolBox catalogue OData record kept in [`docs/porter-evidence/odata-snapshots/`](docs/porter-evidence/odata-snapshots/) as authoritative evidence of the `Open Source: true` declaration.

## Credits

This project would not exist without:

- **[Tanguy Touzard](https://github.com/MscrmTools)** and the XrmToolBox community — for the original tool, the SDK design, and a decade of stewardship.
- **[Jonas Rapp](https://jonasr.app/)** — for FetchXML Builder, Plugin Trace Viewer, Bulk Data Updater and the broader plugin ecosystem.
- **[Daryl LaBar](https://github.com/daryllabar)** — for the DLaB.Xrm tooling and Early Bound Generator.
- **The Innofactor / Biznamics team** — for keeping the classic Plugin Registration tool alive.
- **[Jukka Niiranen](https://jukkan.com/)** — for the modern web catalogue at [xrm.jukkan.com](https://xrm.jukkan.com), which inspired our in-app catalogue subsystem.

## Licence

This fork inherits **GPL-3.0** from the upstream `MscrmTools/XrmToolBox` (`LICENSE.txt`). Each ported plugin preserves its upstream licence and attribution — see [`docs/porter-evidence/licenses.md`](docs/porter-evidence/licenses.md).

## Reporting issues with this fork

Issues specific to PAC'd Toolbox (the cross-platform shell, ported plugins, build scripts) should be filed against this repository.

**For issues with the canonical Windows XrmToolBox or with plugins running on the Windows shell, please file at the upstream:** https://github.com/MscrmTools/XrmToolBox/issues
