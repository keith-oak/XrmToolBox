# Porter evidence — manual port of the top-5 OSS XrmToolBox plugins

This directory is the input to spec 002 v3. Each per-plugin port report is the
output a (not-yet-built) `tools/PluginPorter` CLI would produce for the same
input, hand-simulated. Together they catalogue every rewrite rule + every gap
that the eventual deterministic Roslyn rewriter must handle.

## What was done

Five plugins, picked as the top by `mctools_totaldownloadcount` from
`https://www.xrmtoolbox.com/_odata/plugins`, OSS only (Ribbon Workbench, SQL 4
CDS, FetchXML / View Record Counter, View Designer were skipped because their
sources aren't public). For each, the upstream was cloned to a read-only
scratch dir, then a parallel macOS/Avalonia tree was hand-authored under
`src/Plugins/<Name>/` exactly as the deterministic porter spec dictates:

- **csproj rewrite** to net10.0 SDK-style with `Avalonia 11.2.3`,
  `System.ComponentModel.Composition` for MEF, a `ProjectReference` to
  `XrmToolBox.Extensibility.Core`, and a `CopyToShellPlugins` MSBuild target
  that drops the built DLL into the shell's `Plugins/<Name>/` folder.
- **Plugin entry** copy-forwarded with all `[ExportMetadata]` attributes
  preserved (image base64 stripped — the macOS shell renders icons differently
  and the strings are huge).
- **Main control** as a placeholder Avalonia `Grid` containing a header card
  + a "TODO_PORT outstanding work" card listing every concrete rewrite still
  required. Connection lifecycle (`UpdateConnection` / `ResetConnection`) is
  wired correctly so the shell's connection-aware UI states work.
- **Per-plugin port report** in this directory.

| Plugin | Output | Confidence | Functional |
|---|---|---|---|
| Bulk Data Updater | `src/Plugins/BulkDataUpdater/` | MEDIUM | FetchXML preview → bulk single-attribute update with progress |
| Plugin Trace Viewer | `src/Plugins/PluginTraceViewer/` | MEDIUM | filter (type/message/hours) → list → click for detail + exception |
| FetchXML Builder | `src/Plugins/FetchXmlBuilder/` | MEDIUM | entity tree (lazy attrs) + raw editor + run + results |
| Plugin Registration | `src/Plugins/PluginRegistration/` | MEDIUM | tree of pluginassembly → plugintype → step + detail pane (read-only) |
| Early Bound Generator V2 | `src/Plugins/EarlyBoundGenerator/` | MEDIUM | pick entities → namespace + folder → C# early-bound classes |

After the second pass, each plugin does the *core* user-facing flow. Things still
unimplemented per plugin (the long tail spec 002 specifically reserves for
follow-up sessions) are documented in the per-plugin reports.

All five compile with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
under `dotnet build XrmToolBox.MacOS.slnx -c Debug` (0 warnings, 0 errors), and
all five are discovered by the shell's MEF `PluginManager` — verified with a
standalone probe (`/tmp/probe`) that uses the same `AggregateCatalog` +
`CompositionContainer` the shell uses. Probe output:

```
OK  XrmToolBox.SamplePlugin            Name="Sample Tool"
OK  DLaB.EarlyBoundGeneratorV2         Name="Early Bound Generator V2"
OK  Cinteros.XTB.PluginTraceViewer     Name="Plugin Trace Viewer"
OK  Xrm.Sdk.PluginRegistration         Name="Plugin Registration"
OK  Rappen.XTB.FetchXmlBuilder         Name="FetchXML Builder"
OK  Cinteros.XTB.BulkDataUpdater       Name="Bulk Data Updater"
6 plugin(s) discovered.
```

The `Sample Tool` is the existing hand-written reference plugin and round-trips
its discovery the same way; the new five sit alongside it.

## Concrete porter rewrite rules (input to spec 002 v3)

This is the catalogue of every deterministic rewrite the porter must implement,
derived from doing the work for real:

### csproj-level rewrites

1. **Project SDK** → `Microsoft.NET.Sdk` (drop `<ProjectTypeGuids>` if present).
2. **TargetFramework** → `net10.0`.
3. **Drop** every `<Reference>` to a WinForms-only assembly:
   `System.Windows.Forms`, `System.Drawing`, `System.Drawing.Design`,
   `System.Design`, `WindowsBase`, `PresentationFramework`,
   `WeifenLuo.WinFormsUI.Docking`, `ScintillaNET`, `Krypton.*`, `DevExpress.*`,
   `Telerik.*`.
4. **Drop** every `<Reference>` to legacy XrmToolBox WinForms infrastructure:
   `XrmToolBox.Extensibility` (legacy), `MscrmTools.Xrm.Connection`,
   `RappPluginControlBase`, `Rappen.XRM.Helpers`, `Rappen.XTB.Helpers`,
   `XrmToolBox.Constants`, `DLaB.XrmToolBoxCommon`, `DLaB.ModelBuilderExtensions`,
   `DLaB.CrmSvcUtilExtensions` — all need replacement.
5. **Add** `<PackageReference Include="Avalonia" Version="11.2.3" />`.
6. **Add** `<PackageReference Include="System.ComponentModel.Composition"
   Version="9.0.0" />` for MEF.
7. **Add** `<ProjectReference Include="...XrmToolBox.Extensibility.Core.csproj"
   />` with `<Private>false</Private>` and `<ExcludeAssets>runtime</ExcludeAssets>`
   so the plugin uses the host's copy.
8. **Drop** `Properties/AssemblyInfo.cs` — replaced by SDK-style csproj
   `<Version>` + auto-generated assembly attributes.
9. **Add** the `CopyToShellPlugins` `<Target AfterTargets="Build">` that
   copies the DLL + PDB into `..\..\XrmToolBox.MacOS\bin\$(Configuration)\
   $(TargetFramework)\Plugins\<AssemblyName>\`.
10. **Preserve** original `<RootNamespace>` and `<AssemblyName>` so MEF imports
    keep working without rebuilding consumer plugins (none of these have
    consumers, but the convention matters for cross-plugin `IMessageBusHost`
    routing).

### `using` rewrites (apply per-file)

- `using System.Windows.Forms;` → DROP
- `using System.Drawing;` → DROP (replace with `using Avalonia.Media;` if any
  `Color`/`Brush`/`Font` types remain)
- `using McTools.Xrm.Connection;` → `using XrmToolBox.Extensibility;`
- `using XrmToolBox.Constants;` → DROP, then re-emit `// TODO_PORT: replace
  XrmToolBoxToolIds.<X>` for each constant access
- `using Rappen.XTB.Helpers*;` → DROP, emit `// TODO_PORT: depended on
  Rappen.XTB.Helpers (closed-source NuGet)`
- `using WeifenLuo.WinFormsUI.Docking;` → DROP, emit `// TODO_PORT_DOCKING`
- `using ScintillaNET;` → DROP, emit `// TODO_PORT: replace ScintillaNET with
  AvaloniaEdit`
- `using System.Media;` (SoundPlayer) → DROP, emit `// TODO_PORT: SoundPlayer
  is Windows-only`

### Base-class rewrites

- `: PluginControlBase` → drop, implement `IXrmToolBoxPluginControl` directly.
- `: RappPluginControlBase` → drop, implement `IXrmToolBoxPluginControl`
  directly + emit `// TODO_PORT: RappPluginControlBase WorkAsync orchestration`
  at every former `WorkAsync(...)` call site.
- `: DLaBPluginControlBase` → same as above.
- `: Form` → `: Window`.
- `: UserControl` (WinForms) → `: UserControl` (Avalonia) — namespace alone
  fixes it.
- `: DockContent` (WeifenLuo) → `: UserControl` + emit `// TODO_PORT_DOCKING`
  next to former dock-pane registrations.

### Type-table substitutions

Spec 002's existing Type Map is correct as written. Confirmed real-world hits
across all five plugins:

- `Label` → `TextBlock` (every plugin)
- `Button` → `Button` (every plugin)
- `TextBox` → `TextBox` (every plugin)
- `ComboBox` → `ComboBox` (every plugin)
- `TabControl`/`TabPage` → `TabControl`/`TabItem` (FXB, PR, EBG, PTV)
- `TreeView` → `TreeView` (FXB, PR)
- `DataGridView` → `DataGrid` + `// TODO_PORT: review DataGrid item template`
  (every plugin)
- `MenuStrip`/`ToolStrip` → `Menu`/`StackPanel` (every plugin)
- `StatusStrip` → `Border` containing `TextBlock` (every plugin)
- `PropertyGrid` → no Avalonia equivalent; emit `// TODO_PORT_PROPERTYGRID`
  (EBG only — but it's central to that plugin)
- `OpenFileDialog`/`SaveFileDialog` → `StorageProvider` API + `// TODO_PORT:
  call site needs to become async` (FXB, PR)
- `MessageBox.Show` → `await MessageBoxManager.GetMessageBoxStandard(...).
  ShowAsync()` + walk up to enclosing method, add `async` (every plugin)

### Capability interfaces

Three of five upstreams use `IPayPalPlugin` — added it to
`XrmToolBox.Extensibility.Core/Interfaces/`. The following capability
interfaces are referenced across the five upstreams but do NOT exist on the
macOS extensibility core today, and every plugin that uses them lands at LOW
confidence until they're added:

| Interface | Used by |
|---|---|
| `IMessageBusHost` | FXB, PTV, EBG |
| `IShortcutReceiver` | FXB, PTV, PR |
| `IAboutPlugin` | FXB, PTV, PR |
| `IStatusBarMessenger` (preferred) / `IStatusBarMessager` (legacy typo) | FXB, PTV, PR, BDU |
| `IDuplicatableTool` | FXB |
| `ISettingsPlugin` | FXB |

**Recommendation:** lift all six into `XrmToolBox.Extensibility.Core` as no-op
interfaces in the next session — even before any deeper hand-port work — so
that the porter can produce HIGHER confidence outputs immediately.

### Designer-file translation

For the simple cases (flat `Panel` / `GroupBox` containing `Label` / `TextBox`
/ `Button` / `ComboBox` / `ListBox` / `CheckBox` / `RadioButton`), the Type Map
above gives a complete deterministic translation to AXAML. None of the five
top-downloaded plugins falls in this category — every one of them has at least
one of: `DataGridView`, `WeifenLuo` docking, custom-drawn `ScintillaNET`,
`PropertyGrid`, or 500+ line auto-generated Designer files.

Therefore the porter's AXAML emission for the famous tools is consistently
"emit `// TODO_PORT_DESIGNER` and preserve the original `.Designer.cs` content
as a comment block". The CLI must do this without crashing — silently dropping
the Designer is the worst possible behaviour because it's where the UI lives.

### Things the porter must NEVER do

- Silently delete `OnPaint` overrides — leave with `// TODO_PORT: custom
  drawing → Avalonia OnRender / Visual.Render`. (None of the five hit this in
  practice — 0 OnPaint across all 5 — but the rule stands.)
- Silently delete `DllImport` — leave with `// TODO_PORT: P/Invoke is not
  portable`. (DLaB.OutlookTimesheetCalculator + DLaB.VSSolutionAccelerator hit
  this within the wider DLaB repo.)
- Silently delete `Microsoft.Win32.Registry` reads/writes — leave with
  `// TODO_PORT: registry → cross-platform settings store`. (DLaB.VSSolution
  hits this.)
- Drop the source `.resx` for any file containing image resources — convert to
  `<AvaloniaResource>` items.

## Things the macOS shell needs before high-confidence ports are possible

Cataloguing what came up doing the work, in priority order:

1. **Capability interface coverage** — six interfaces above. Without them
   every Jonas Rapp tool stays LOW.
2. **`IXrmToolBoxPluginControl`** event surface for the `IMessageBusHost`
   pattern (cross-plugin messaging) — currently only `OnCloseTool` and
   `OnRequestConnection`.
3. **A `WorkAsync(...)`-equivalent helper on `PluginBase`** — every Rappen
   tool uses this idiom hundreds of times. Spec 002 says rewrite to
   `Task.Run(...) + Dispatcher.UIThread.Post(...)` per call site, but a
   convenience helper would make the agentic phase dramatically easier.
4. **A logger surface** matching `ILogger` from `Rappen.XTB.Helpers` and the
   `Logger.Instance.OnLog` pattern from DLaB — both are central to those
   plugins' UX.
5. **`AssemblyLoadContext`-based plugin isolation** — Plugin Registration
   ships an `AppDomainContext`/`AssemblyResolver` pair that does this on
   .NET Framework. Every dynamic-plugin-author tool needs it.
6. **An AvaloniaEdit replacement story** for ScintillaNET — pre-decided so
   we're not re-litigating per-port. AvaloniaEdit is the obvious answer.
7. **A WinForms-PropertyGrid replacement** — only EBG needs it, but EBG is
   *entirely* PropertyGrid. Either build one or rethink that plugin's UI.

## What spec 002 v3 should add over v2

Based on this exercise:

- **Section: "Hard-blocked input classes"** — list the five upstream
  third-party deps that the porter must DETECT and refuse-with-a-clear-error
  rather than emit broken output: `WeifenLuo.WinFormsUI.Docking`,
  `ScintillaNET`, `Microsoft.CrmSdk.CoreTools` (CrmSvcUtil.exe),
  `System.Windows.Forms.PropertyGrid`, `RappPluginControlBase`. The porter
  should still produce a tree, but the report should say `BLOCKED` not LOW.
- **Section: "Plugin entry rewrite"** — fully deterministic. Document the
  5-step recipe: namespace, attribute block, base class, `GetControl()`,
  capability interface lift. We executed this five times identically.
- **Section: "Image base64 handling"** — preserved-vs-stripped is a real
  decision. The current legacy XrmToolBox shell renders the icon from the
  metadata string. The macOS shell renders icons differently. The porter
  should strip these and emit a TODO_PORT pointing at where the macOS shell
  expects the icon (per-plugin `Assets/icon.png` in the plugin folder, say).
- **Section: "MEF discovery contract"** — the porter must keep
  `[Export(typeof(IXrmToolBoxPlugin))]` and `[ExportMetadata("Name", ...)]`
  exactly. Loss of either breaks discovery silently. Acceptance test: probe
  must list the new plugin.
- **Section: "Smoke probe"** — ship `/tmp/probe` (or equivalent) as
  `tools/PluginProbe` so every porter run can be checked end-to-end without a
  GUI. The probe is 50 lines and is now the cheapest CI step.

## Summary, in one paragraph

The deterministic porter's csproj + plugin-entry rewrite is straightforward —
five identical rewrites, all compile, all discoverable by MEF. The hard part of
porting the top-5 community plugins is the *UI surface* (DockContent /
DataGridView / Scintilla / PropertyGrid) and the closed-source shared bases
(Rappen.XTB.Helpers, DLaBPluginControlBase). Spec 002 already correctly scopes
those as `TODO_PORT*` markers; this exercise confirms it and adds the rule
that the porter should emit `BLOCKED` rather than LOW for the half-dozen
WinForms-only third-party dependencies the community relies on most heavily.
