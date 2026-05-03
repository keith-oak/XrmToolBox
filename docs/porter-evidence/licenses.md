# Licence audit — ported plugins

**Authoritative source.** The official XrmToolBox plugin catalogue is published as an OData feed by the XrmToolBox project (maintainer: Tanguy Touzard, see `https://github.com/MscrmTools/XrmToolBox`). Every plugin record carries a self-declared `mctools_isopensource` boolean. Plugin authors submit their plugins to this catalogue via the XrmToolBox Plugins Store, and the catalogue is consumed by the in-tool installer and by community catalogues like `https://github.com/jukkan/xrmtoolbox-plugin-catalog`.

We treat `mctools_isopensource: true` in this feed as the citable open-source declaration for redistribution purposes. Snapshots of each plugin's OData record are checked into this repo at `odata-snapshots/<nugetid>.json` so the evidence is preserved if the live feed changes.

Feed URL: `https://www.xrmtoolbox.com/_odata/plugins`

## Per-plugin findings

| Plugin | NuGet ID | Author | OData `isopensource` | Project URL | Repo `LICENSE` | Effective licence |
|---|---|---|---|---|---|---|
| FetchXML Builder | `Cinteros.Xrm.FetchXmlBuilder` | Jonas Rapp | `true` | https://fetchxmlbuilder.com/ | GPL-3.0 (`https://github.com/rappen/fetchxmlbuilder`) | **GPL-3.0** |
| Plugin Trace Viewer | `Cinteros.XrmToolBox.PluginTraceViewer` | Jonas Rapp | `true` | https://jonasr.app/PTV | GPL-3.0 (`https://github.com/rappen/XrmToolBox.PluginTraceViewer`) | **GPL-3.0** |
| Bulk Data Updater | `Cinteros.XrmToolBox.BulkDataUpdater` | Jonas Rapp | `true` | https://jonasr.app/BDU/ | GPL-3.0 (`https://github.com/rappen/BulkDataUpdater`) | **GPL-3.0** |
| Early Bound Generator | `DLaB.Xrm.EarlyBoundGeneratorV2` | Daryl LaBar | `true` | https://github.com/daryllabar/DLaB.Xrm.XrmToolBoxTools | MIT, "Copyright (c) 2015 Daryl LaBar" | **MIT** |
| Plugin Registration | `Xrm.Sdk.PluginRegistration` | "Microsoft, Alexey, Jonas, Imran and the Power Platform community" | `true` | https://github.com/Biznamics/PluginRegistration | (no LICENSE file in repo) | **Open source per catalogue declaration** — see notes below |

## Our shell

The macOS shell (`src/Shell`) is a re-implementation of the upstream `MscrmTools/XrmToolBox` Windows shell. The upstream is **GPL-3.0** (`LICENSE.txt` at repo root). Our derivative work inherits GPL-3.0. All ported plugins above are compatible: GPL-3.0 (Rappen × 3) is identical, MIT (DLaB) is GPL-3 compatible, and the Biznamics catalogue declaration is open source.

## Plugin Registration — additional notes

The Biznamics/PluginRegistration repository does not contain an explicit `LICENSE` file. ~40 of its ~83 source files retain the original Microsoft Dynamics CRM SDK code-samples header ("Copyright (C) Microsoft Corporation. All rights reserved."). The README acknowledges the project is a fork of "the classical Plugin Registration Tool provided by Microsoft as CRM SDK code sample". The maintainers (Innofactor / Biznamics AB) actively publish releases to NuGet and to the official XrmToolBox catalogue, where it is declared `Open Source: true`.

For our purposes:

1. We rely on the catalogue's `mctools_isopensource: true` declaration, which is the same declaration used by the in-XrmToolBox installer to make the plugin available to end users worldwide.
2. We have opened a tracking issue at `Biznamics/PluginRegistration` requesting an explicit `LICENSE` file to remove ambiguity. See `docs/biznamics-issue/issue.md`.
3. We preserve the original Microsoft "AS IS" comment headers in any file ported with structural similarity to the upstream.

## Attribution preserved in our ports

Every plugin folder under `src/Plugins/<Name>/` ships a `LICENSE-UPSTREAM` file copied from the upstream repository's licence (where one exists), plus a `NOTICE.md` recording the upstream copyright holder, project URL, and the fact that we ported (not vendored) the source. The shell itself displays attribution in **About → Acknowledgements** at runtime, listing every ported plugin with its upstream licence and project URL.

## Snapshots (evidence)

Captured `2026-05-03` from `https://www.xrmtoolbox.com/_odata/plugins?$format=json&$filter=...`:

- `odata-snapshots/Cinteros.Xrm.FetchXmlBuilder.json`
- `odata-snapshots/Cinteros.XrmToolBox.PluginTraceViewer.json`
- `odata-snapshots/Cinteros.XrmToolBox.BulkDataUpdater.json`
- `odata-snapshots/DLaB.Xrm.EarlyBoundGeneratorV2.json`
- `odata-snapshots/Xrm.Sdk.PluginRegistration.json`
