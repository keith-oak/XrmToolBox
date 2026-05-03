# 002 — Agentic Plugin Porter (WinForms → Avalonia, .NET Framework → .NET 10)

## Priority: HIGH

## Status: INCOMPLETE

## Description

Build an automated porting facility that takes an existing XrmToolBox plugin (`.NET Framework 4.8` + WinForms `UserControl` + `MscrmTools.Xrm.Connection` consumer) and produces a candidate macOS-native version against `XrmToolBox.Extensibility.Core`. The porter has two cooperating layers:

1. **`tools/PluginPorter` CLI** — deterministic Roslyn + XML rewriter that does everything mechanical: csproj retargeting, `using` rewrites, base-class swaps, type-table substitutions, `.Designer.cs` → AXAML translation for the cases it can handle, port-report generation. Pure code, no LLM, idempotent, fast.
2. **GitHub Copilot agentic workflow** — a `.github/workflows/port-plugin.yml` GitHub Agentic Workflow that, given a plugin repo URL, runs the CLI, then dispatches a Copilot coding agent to clean up everything the deterministic pass marked `// TODO_PORT:`. The agent opens a PR back to the source repo (or a fork) with the migrated code, the port report, and a checklist for the human reviewer.

Together: the deterministic part covers ~70% of plugins to "compiles, mostly renders"; the agentic part takes that to "actually works for the documented happy path", with a human reviewer as the final gate.

## Why

The existing community ecosystem is hundreds of plugins, all WinForms / .NET Framework. Manually porting each is uneconomic. A pure LLM porter is unreliable. A pure deterministic porter can't handle DataGridView semantics or custom-drawn controls. Splitting the labour — deterministic for the boring 70%, agentic for the judgement calls — is what makes the macOS shell ecosystem-viable instead of a tech demo.

## Acceptance Criteria

### CLI: `tools/PluginPorter`

- [ ] New project `tools/PluginPorter/PluginPorter.csproj` (net10.0, console exe), single binary
- [ ] Invocation: `dotnet run --project tools/PluginPorter -- <path-to-plugin.csproj> [--out <dir>] [--report <file.md>]`
- [ ] Reads source plugin via Roslyn `MSBuildWorkspace`; never modifies the original tree
- [ ] **csproj rewrite** produces a new SDK-style csproj at `<out>/<Name>.MacOS.csproj` with:
  - `<TargetFramework>net10.0</TargetFramework>`
  - `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`
  - `<ProjectReference>` to `XrmToolBox.Extensibility.Core`
  - `<PackageReference>` to `Avalonia` 11.2.x and `Avalonia.ReactiveUI`
  - All original WinForms-only `<Reference>` entries dropped
  - Original `<PackageReference>` entries (Newtonsoft.Json, etc.) preserved if they target netstandard
  - MSBuild target that copies output DLL into `src.macos/XrmToolBox.MacOS/bin/$(Configuration)/$(TargetFramework)/Plugins/<Name>/`
- [ ] **C# rewrite** with documented mappings (table below) — applies via Roslyn `SyntaxRewriter`:
  - `using System.Windows.Forms;` → `using Avalonia.Controls;` (+ `using Avalonia.Layout;` where layout types used)
  - Base class `: UserControl` (WinForms) → `: UserControl` (Avalonia) — no namespace ambiguity since old `using` is removed
  - `: PluginBase` continues to work because `XrmToolBox.Extensibility.Core` matches the legacy SDK signature
  - Type substitutions per the **Type Map** section below
  - `MessageBox.Show(...)` → `await MessageBoxManager.GetMessageBoxStandard(...).ShowAsync()` (keep call sites async-safe by walking up to enclosing method and adding `async` modifier where needed)
  - `BackgroundWorker` patterns → `Task.Run(...)` + `Dispatcher.UIThread.Post(...)`
  - `OnPaint` overrides → emit `// TODO_PORT: custom drawing — port to Avalonia OnRender / Visual.Render` and leave method body untouched
  - Win32 P/Invoke (`DllImport("user32.dll")` etc.) → emit `// TODO_PORT: Win32 P/Invoke — not portable; rewrite or guard with OperatingSystem.IsWindows()`
  - Registry access (`Microsoft.Win32.Registry...`) → emit `// TODO_PORT: registry access — replace with cross-platform settings store (use Paths.SettingsFile)`
- [ ] **`.Designer.cs` → AXAML translation** for the simple cases:
  - `InitializeComponent` parsed via Roslyn into a control tree
  - Simple flat layouts (`Panel` / `GroupBox` containing `Label` / `TextBox` / `Button` / `ComboBox` / `ListBox` / `CheckBox` / `RadioButton`) emit a sibling `.axaml` file with equivalent Avalonia controls and a partial class hooking up event handlers by name
  - Complex layouts (TableLayoutPanel deeply nested, custom-drawn anything, DataGridView, ListView with image lists) emit `// TODO_PORT_DESIGNER: ...` comment in the new `.axaml.cs`, copy the original `.Designer.cs` content as a comment block for reference, and flag in the port report
  - The original `.resx` is converted to `AvaloniaResource` items in the new csproj only if it contains image resources; otherwise dropped
- [ ] **Port report** at `<out>/PORT_REPORT.md` with sections:
  - Summary (X files converted, Y manual edits required)
  - Confidence rating (HIGH / MEDIUM / LOW) based on heuristics: presence of DataGridView, custom drawing, P/Invoke, registry, third-party WinForms libraries
  - Per-file findings: file path, line, what was rewritten, what needs human review
  - Suggested next steps for the reviewer
- [ ] **Idempotency**: re-running the porter against an already-ported tree produces zero diff
- [ ] **Determinism**: same input always produces identical output (file ordering, member ordering preserved from source)
- [ ] **Self-tests**: `tools/PluginPorter.Tests` xunit project with at least these fixtures (input .csproj + expected output .csproj/.cs/.axaml diffs):
  - `Fixtures/MinimalPlugin/` — single button, no connection — must port to compiling Avalonia
  - `Fixtures/SamplePluginLegacy/` — port the existing `Plugins/MsCrmTools.SampleTool` from the repo root and verify the result compiles and matches the hand-written `src.macos/Plugins/SampleTool` semantically
  - `Fixtures/DataGridViewPlugin/` — must emit `TODO_PORT_DESIGNER` and produce a LOW confidence report, but still produce a building skeleton
  - `Fixtures/Pinvoke/` — must emit `TODO_PORT` and not silently delete the `DllImport`

### Type Map (deterministic substitutions)

| WinForms | Avalonia | Notes |
| --- | --- | --- |
| `Form` | `Window` | Only when the type is a top-level form, not a UserControl |
| `UserControl` | `UserControl` | Different namespace — handled by `using` rewrite |
| `Label` | `TextBlock` | Map `.Text` → `.Text`; map `.AutoSize` → drop (Avalonia auto by default) |
| `Button` | `Button` | Direct, drop `.FlatStyle`, `.UseVisualStyleBackColor` |
| `TextBox` | `TextBox` | Map `.Multiline=true` → `AcceptsReturn="True"`, `.PasswordChar` → `PasswordChar` |
| `CheckBox` | `CheckBox` | Map `.Checked` → `IsChecked` |
| `RadioButton` | `RadioButton` | Add `GroupName` from parent container name |
| `ComboBox` | `ComboBox` | Map `.Items` → `Items`; flag `.DataSource` for review |
| `ListBox` | `ListBox` | Direct |
| `Panel` | `StackPanel` (default) or `Grid` if mixed anchors | Heuristic: if all children have same `Dock`, use `StackPanel`; if anchored to multiple sides, use `Grid` |
| `GroupBox` | `HeaderedContentControl` | Header bound from `.Text` |
| `TabControl` | `TabControl` | Tabs become `TabItem` children |
| `TabPage` | `TabItem` | Header from `.Text` |
| `MenuStrip` / `ToolStrip` | `Menu` / `StackPanel` of `Button` | Best-effort |
| `StatusStrip` | `Border` containing `TextBlock` | |
| `DataGridView` | (none) | Emit `TODO_PORT: DataGridView → Avalonia DataGrid (semantically different — review item template)` |
| `ListView` (Details) | `DataGrid` | Emit `TODO_PORT` if `View=Tile` or image list used |
| `TreeView` | `TreeView` | Emit `TODO_PORT` if image list used |
| `OpenFileDialog` / `SaveFileDialog` | `StorageProvider` API | Emit `TODO_PORT: file dialogs are async — call site needs to become async` |
| `Color` (System.Drawing) | `Avalonia.Media.Color` | Map known names; otherwise via `.FromArgb` |
| `Font` (System.Drawing) | `FontFamily` + `FontSize` props | |
| `Image.FromFile` | `new Bitmap(path)` (Avalonia) | |

### GitHub Copilot Agentic Workflow

- [ ] `.github/workflows/port-plugin.yml` Agentic Workflow with `workflow_dispatch` inputs:
  - `plugin_repo` (string, required) — `owner/repo` of the plugin to port
  - `plugin_branch` (string, optional, default `main`)
  - `target_repo` (string, optional, default `<actor>/<plugin-repo>-macos`) — where the PR is opened
- [ ] Workflow steps:
  1. Checkout `<plugin_repo>@<plugin_branch>` into `./source`
  2. Checkout this repo (the porter) into `./porter`
  3. `dotnet run --project porter/tools/PluginPorter -- ./source/<found-csproj> --out ./ported --report ./PORT_REPORT.md`
  4. Verify the ported tree compiles: `dotnet build ./ported`
  5. Push `./ported` to `target_repo` on a `port/<sha>` branch
  6. Dispatch a Copilot Coding Agent issue assignment on `target_repo` titled "Finish auto-port: resolve TODO_PORT markers" with body = contents of `PORT_REPORT.md` plus a checklist of every `TODO_PORT*` location, instructing the agent to fix them, add a smoke test, and open a PR
- [ ] The workflow's `.lock.yml` is regenerated (per `gh aw` convention) and committed alongside the `.md`
- [ ] **Permissions**: workflow declares the minimum scopes (`contents:write`, `pull-requests:write`, `issues:write`); no broad `write-all`
- [ ] **Safety rails**:
  - Workflow refuses to run against repos owned by `MscrmTools` or `microsoft` orgs (don't auto-fork upstream-of-record)
  - Cap on number of Copilot iterations (default 5) to prevent runaway cost
  - PR description includes "Generated by automated port — review every `TODO_PORT` carefully before merging"

### Documentation

- [ ] `tools/PluginPorter/README.md` covering: usage, type map, what's automated vs manual, exit codes, how to add a new fixture
- [ ] Update `src.macos/README.md` with a short "Porting a legacy plugin" section pointing at the CLI and the workflow

### Code quality

- [ ] All porter code in C# net10.0, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- [ ] xunit + Verify (snapshot testing) for fixture-based tests so regenerating expected output is `dotnet test --update-snapshots`-style trivial
- [ ] No reflection-based magic; all Roslyn rewrites use named visitor methods so the diff is reviewable

## Technical Requirements

- [ ] `dotnet build tools/PluginPorter` exits 0 with `-warnaserror`
- [ ] `dotnet test tools/PluginPorter.Tests` exits 0
- [ ] Running the porter against `Plugins/MsCrmTools.SampleTool` (the legacy WinForms sample at the repo root) produces a building project
- [ ] The Agentic Workflow validates with `gh aw lint` (or `actionlint`) and the lock file is consistent

## Manual Verification Steps

```bash
# 1. Build the porter
dotnet build tools/PluginPorter -nologo -warnaserror
# 2. Run the porter against the in-tree legacy sample
dotnet run --project tools/PluginPorter -- Plugins/MsCrmTools.SampleTool/SampleTool.csproj --out /tmp/ported-sample
# 3. Compile the result
dotnet build /tmp/ported-sample
# 4. Drop the resulting DLL into the macOS shell's Plugins folder and probe
cp /tmp/ported-sample/bin/**/MsCrmTools.SampleTool.dll src.macos/XrmToolBox.MacOS/bin/Debug/net10.0/Plugins/AutoPorted/
src.macos/XrmToolBox.MacOS/bin/Debug/net10.0/XrmToolBox --probe
#    → expect 2 plugins listed (the hand-written sample + the auto-ported one)
# 5. Workflow lint (Agentic Workflows installed)
gh aw lint .github/workflows/port-plugin.md
```

## Out of Scope

- Porting plugins that depend on third-party WinForms component suites (DevExpress, Telerik, Krypton). Porter detects them, marks LOW confidence, leaves them for human rewrite.
- Translating XAML / WPF plugins (almost no XrmToolBox plugins are WPF — handle when one shows up).
- A web-based porter UI. CLI + GitHub workflow only.
- Auto-merging the resulting PRs. The human reviewer always has final say.
- Sandboxing or signature-checking the ported plugins (covered by a future security spec).
- Compatibility shims that let unported WinForms plugin DLLs run under the macOS shell. The whole point is genuine native; we don't ship a WinForms-on-Avalonia trampoline.

## Dependencies / Sequencing

- This spec depends on `001-apple-design-system-theme.md` only insofar as the porter's smoke test launches the shell. It does not block on 001 — both can be implemented independently.
- `gh aw` (GitHub Agentic Workflows extension) must be installed on the maintainer's machine for spec verification, but is **not** a runtime dependency for users running the CLI standalone.
