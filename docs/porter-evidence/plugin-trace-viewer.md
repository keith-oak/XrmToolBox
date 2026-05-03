# Plugin Trace Viewer — port report

**Upstream:** `rappen/XrmToolBox.PluginTraceViewer@e6eda11063cbff9d6cc229d40e49cebdf16b7673`
**Output:** `src/Plugins/PluginTraceViewer/`
**Confidence:** LOW

## Summary

3 files emitted (`*.csproj`, `PluginDescription.cs`, `PluginTraceViewerControl.cs`).
The plugin entry is faithful. Main control body is a placeholder — the original
1,492-line `PluginTraceViewer.cs` derives from `RappPluginControlBase` and uses
`WeifenLuo.WinFormsUI.Docking.DockContent` for its sub-panes, which is a
WinForms-only library with no Avalonia equivalent.

## Confidence inputs (from inventory)

- 28 `.cs` files, 9 `.Designer.cs`, 10 `.resx`, 1 `.csproj`
- 3 `DataGridView` usages (GridControl, ExceptionControl, PluginStatistics)
- 0 `BackgroundWorker` (uses `RappPluginControlBase.WorkAsync`)
- 0 `OnPaint`, 0 `DllImport`, 0 Registry ✅
- Hard third-party deps:
  - `WeifenLuo.WinFormsUI.Docking` ★ — DockContent-derived sub-panes
  - `RappPluginControlBase` (closed-source NuGet)
  - `Rappen.XRM.Helpers` / `Rappen.XTB.Helpers` (closed-source NuGet)
  - `XrmToolBox.Constants` (legacy shell)
  - `Microsoft.Xrm.Sdk.Metadata` (available)
  - `McTools.Xrm.Connection` (replaced)
- Capability interfaces required but absent from macOS core: `IMessageBusHost`,
  `IShortcutReceiver`, `IAboutPlugin`, `IStatusBarMessenger`.

## Per-file findings

| Source file | Lines | Status | Notes |
|---|---:|---|---|
| `PluginDescription.cs` | 43 | ported | All metadata preserved; image base64 stripped. |
| `PluginTraceViewer.cs` (+ Designer) | 1492 | placeholder | Docking shell — every pane is `DockContent`. |
| `Controls/FilterControl.cs` (+ Designer) | not opened | not emitted | Filter pane (entity, plugin name, message, mode, date range, exception). |
| `Controls/GridControl.cs` (+ Designer) | not opened | not emitted | Trace results DataGridView. |
| `Controls/TraceControl.cs` (+ Designer) | not opened | not emitted | Trace detail pane (multi-line log + correlation). |
| `Controls/ExceptionControl.cs` (+ Designer) | not opened | not emitted | Exception detail. |
| `Controls/StatsControl.cs` (+ Designer) | not opened | not emitted | Aggregate stats. |
| `Controls/RecordLinks.cs` (+ Designer) | not opened | not emitted | Hyperlink list to related rows. |
| `PluginStatistics.cs` (+ Designer) | 13 + ~? | not emitted | Modal stats window with DataGridView. |
| `About.cs` (+ Designer) | 11 + ~? | not emitted | Replaced by macOS shell About pane. |
| `Const.cs`, `PTVFilter.cs`, `PluginStatistics.cs`, `Settings.cs`, `Link.cs`, `Record.cs`, `Extensions.cs`, `FindTextHandler.cs` | n/a | not emitted | Pure-logic helpers — eligible for direct port. |
| `Properties/AssemblyInfo.cs` | — | not emitted | Replaced by SDK-style csproj. |

## Suggested next steps

1. Pick the new pane layout: split on the macOS-shell side using a vertical
   `Grid` with two rows (filter at top, results+detail tab strip below) instead
   of resurrecting the docking suite.
2. Translate the menu strip + toolbar in `PluginTraceViewer.designer.cs` to a
   single Avalonia `Menu` + `StackPanel` toolbar.
3. Re-author each sub-control as an Avalonia `UserControl`. Order:
   FilterControl → GridControl → TraceControl. ExceptionControl, StatsControl,
   RecordLinks can wait.
4. Add `IMessageBusHost`, `IShortcutReceiver`, `IAboutPlugin`,
   `IStatusBarMessenger` to the macOS extensibility core (their absence is the
   single biggest blocker for porting Jonas Rapp's tools — they all use them).
5. Replace `WorkAsync(...)` calls with the spec-002 `Task.Run + Dispatcher`
   pattern.
