# FetchXML Builder — port report

**Upstream:** `rappen/fetchxmlbuilder@71ad86219285401a7795e4a4e2415dec3a798457`
**Output:** `src.macos/Plugins/FetchXmlBuilder/`
**Confidence:** LOW

## Summary

3 files emitted. The plugin entry is faithful. The main control is a placeholder.
This is by far the largest of the five (117 `.cs` / 29 `.Designer.cs` / 31 `.resx`
across 2 csprojs). The original `FetchXmlBuilder` class is composed across SEVEN
partial-class files (FXBGui, FXBQueries, FXBMetadata, FXBInterfaces,
FetchXmlBuilder.cs, FetchXmlBuilder.Designer.cs, plus the plugin-export class).

## Confidence inputs (from inventory)

- 117 `.cs`, 29 `.Designer.cs`, 31 `.resx`, 2 `.csproj`
- 2 `DataGridView` usages
- 1 `BackgroundWorker`
- 0 `OnPaint`, 0 `DllImport`, 0 Registry ✅
- Hard third-party deps:
  - `WeifenLuo.WinFormsUI.Docking` ★
  - `RappPluginControlBase`, `Rappen.XRM.Helpers`, `Rappen.XTB.Helpers` (closed)
  - `ScintillaNET` (FetchXML + SQL editors)
  - **`MarkMpn.Sql4Cds.Engine`** (closed-source NuGet — central to SQL pane)
  - `XrmToolBox.Constants`
- Capability interfaces required but absent from macOS core:
  `IMessageBusHost`, `IShortcutReceiver`, `IAboutPlugin`, `IStatusBarMessenger`,
  `IDuplicatableTool`, `ISettingsPlugin`.

## Per-file findings (top-level only)

| Source file | Lines | Status | Notes |
|---|---:|---|---|
| `FetchXMLBuilderPlugin.cs` | 61 | ported | Plugin export class — straightforward. |
| `FetchXmlBuilder.cs` | 982 | placeholder | Main control body — partial 1/7. |
| `FXBGui.cs` | – | not emitted | Partial 2/7 — UI wiring. |
| `FXBQueries.cs` | – | not emitted | Partial 3/7 — query execution. |
| `FXBMetadata.cs` | – | not emitted | Partial 4/7 — metadata cache. |
| `FXBInterfaces.cs` | – | not emitted | Partial 5/7 — capability interface implementations. |
| `FetchXmlBuilder.Designer.cs` | – | not emitted | Partial 6/7 — Designer-generated. |
| 27 dialog `Forms/*.cs` (+ Designer) | – | not emitted | TODO_PORT_DESIGNER each. |
| 11 dock panes `*Builder/*.cs` (+ Designer) | – | not emitted | DockContent — see PluginTraceViewer for the same problem. |
| AppCode helpers (TreeNodeBuilder, FetchXMLBuilder XML parser, etc.) | – | not emitted | Pure logic — eligible for direct port. |
| `Properties/AssemblyInfo.cs` | — | not emitted | Replaced by SDK-style csproj. |

## Suggested next steps

1. Lift the `IMessageBusHost`, `IShortcutReceiver`, `IAboutPlugin`,
   `IStatusBarMessenger`, `IDuplicatableTool`, `ISettingsPlugin` interfaces into
   `XrmToolBox.Extensibility.Core` first. Without these every Jonas Rapp tool
   stays at LOW confidence.
2. Decide on the SQL 4 CDS replacement strategy. Options: (a) ship FXB without
   the SQL pane initially, (b) wait for MarkMpn to publish a netstandard build,
   (c) ship our own Dataverse-Web-API-only translator.
3. Flatten the 7-partial composition into a single Avalonia control for easier
   reasoning. The partials existed to keep VS Designer synchronisation tractable
   — irrelevant on Avalonia.
4. Use AvaloniaEdit for the FetchXML / SQL / OData / JSON editor panes.
