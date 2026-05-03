# Upstream inventory (porter evidence)

Captured 2026-05-03. Each upstream pinned to the SHA below. The scratch
clones live at `~/Documents/GitHub/_porter-scratch/<repo>` and are NEVER
modified — they are read-only reference for the manual port.

| # | Plugin | Upstream | SHA |
|---|---|---|---|
| 1 | Bulk Data Updater | `rappen/BulkDataUpdater` | `40158af2445f55d0b9a32f83a916ea138f1f1196` |
| 2 | Plugin Trace Viewer | `rappen/XrmToolBox.PluginTraceViewer` | `e6eda11063cbff9d6cc229d40e49cebdf16b7673` |
| 3 | FetchXML Builder | `rappen/fetchxmlbuilder` | `71ad86219285401a7795e4a4e2415dec3a798457` |
| 4 | Plugin Registration | `Biznamics/PluginRegistration` | `2313168f75387b04d6638925ee300f040f907e44` |
| 5 | Early Bound Generator V2 | `daryllabar/DLaB.Xrm.XrmToolBoxTools` | `c977ffd4d8f4e8c43bcf1d2ed1d6530e2325c733` |

## Heuristics that drive confidence ratings

Counts capture every signal spec 002 lists for confidence rating.

| Plugin | .cs | .Designer.cs | .resx | .csproj | DataGridView | OnPaint | DllImport | Registry | BackgroundWorker |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| BulkDataUpdater | 37 | 7 | 8 | 2 | 3 | 0 | 0 | 0 | 5 |
| PluginTraceViewer | 28 | 9 | 10 | 1 | 3 | 0 | 0 | 0 | 0 |
| fetchxmlbuilder | 117 | 29 | 31 | 2 | 2 | 0 | 0 | 0 | 1 |
| PluginRegistration | 85 | 20 | 21 | 2 | 4 | 0 | 0 | 0 | 2 |
| DLaB.XrmToolBoxTools (full repo) | 1241 | 32 | 35 | 18 | 8 | 0 | 2 | 1 | 3 |

DLaB is a multi-tool repo. The relevant project for this exercise is
`DLaB.EarlyBoundGeneratorV2/` plus its dependencies (`DLaB.EarlyBoundGeneratorV2.Api`,
`DLaB.EarlyBoundGeneratorV2.Logic`, `DLaB.XrmToolBoxCommon`,
`DLaB.Xrm.Entities.XrmToolBoxCommon`, `DLaB.Log`). The other 12 csprojs
are unrelated XrmToolBox tools (Outlook Timesheet Calculator,
VSSolutionAccelerator, AttributeManager, ModelBuilderExtensions, ...).

## Plugin entry-class metadata (verbatim from upstreams)

Captured for the deterministic copy-forward into each `*Plugin.cs`:

```csharp
// Bulk Data Updater
[Export(typeof(IXrmToolBoxPlugin)),
 ExportMetadata("Name", "Bulk Data Updater"),
 ExportMetadata("Description", "BDU can update one or multiple columns, for one or a gazillion records in Microsoft Dataverse! Empower yourself to achieve more."),
 ExportMetadata("BackgroundColor", "#FFFFC0"),
 ExportMetadata("PrimaryFontColor", "#0000C0"),
 ExportMetadata("SecondaryFontColor", "#0000FF")]
public class BulkDataUpdaterTool : PluginBase, IPayPalPlugin
// (also implements IGitHubPlugin, IHelpPlugin)

// Plugin Trace Viewer
[Export(typeof(IXrmToolBoxPlugin)),
 ExportMetadata("Name", "Plugin Trace Viewer"),
 ExportMetadata("Description", "Investigate the Plugin Trace Log with easy filtering and display possibilities"),
 ExportMetadata("BackgroundColor", "#FFFFC0"),
 ExportMetadata("PrimaryFontColor", "#0000C0"),
 ExportMetadata("SecondaryFontColor", "#0000FF")]
public class PluginTraceViewerPlugin : PluginBase, IPayPalPlugin

// FetchXML Builder
[Export(typeof(IXrmToolBoxPlugin)),
 ExportMetadata("Name", "FetchXML Builder"),
 ExportMetadata("Description", "Build queries for Microsoft Dataverse. Run them. Get code. Let AI fix what you can't. Empower yourself to achieve more."),
 ExportMetadata("BackgroundColor", "#FFFFC0"),
 ExportMetadata("PrimaryFontColor", "#0000C0"),
 ExportMetadata("SecondaryFontColor", "#0000FF")]
public partial class FetchXMLBuilderPlugin : PluginBase, IPayPalPlugin

// Plugin Registration
[Export(typeof(IXrmToolBoxPlugin)),
 ExportMetadata("Name", "Plugin Registration"),
 ExportMetadata("Description", "Classic plugin registration, modernized for Dataverse / Dynamics 365"),
 ExportMetadata("BackgroundColor", "Lavender"),
 ExportMetadata("PrimaryFontColor", "#000000"),
 ExportMetadata("SecondaryFontColor", "DarkGray")]
public class Plugin : PluginBase

// Early Bound Generator V2
[Export(typeof(IXrmToolBoxPlugin)),
 ExportMetadata("Name", "Early Bound Generator V2"),
 ExportMetadata("Description", "Adds advanced features and configuration to the generation of Early Bound Dataverse Tables."),
 ExportMetadata("BackgroundColor", "White"),
 ExportMetadata("PrimaryFontColor", "#000000"),
 ExportMetadata("SecondaryFontColor", "DarkGray")]
public class EarlyBoundGeneratorPlugin : PluginBase
// (also implements IHelpPlugin via override HelpUrl)
```

## Capability interfaces required

The macOS extensibility core ships `IGitHubPlugin`, `IHelpPlugin`,
`INoConnectionRequired`, `IPluginMetadata`. Three of five upstreams
implement `IPayPalPlugin`, which the porter must therefore add to
`XrmToolBox.Extensibility.Core/Interfaces/`. Other capability interfaces
referenced across the five upstreams (status: not needed for the MVP
ports — each is a `// TODO_PORT:` if hit):

- `IStatusBarMessenger`, `IStatusBarMessager` (legacy typo) — status writes
- `IMessageBusHost` — cross-plugin messaging
- `IDuplicatableTool` — multi-instance
- `IShortcutReceiver` — keyboard shortcut delivery
- `ISettingsPlugin` — persistent settings hook
