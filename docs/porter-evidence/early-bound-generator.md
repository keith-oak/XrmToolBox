# Early Bound Generator V2 — port report

**Upstream:** `daryllabar/DLaB.Xrm.XrmToolBoxTools@c977ffd4d8f4e8c43bcf1d2ed1d6530e2325c733`
**Output:** `src.macos/Plugins/EarlyBoundGenerator/`
**Confidence:** LOW

## Summary

3 files emitted. The plugin entry is faithful. The main control is a placeholder.
EBG is the only plugin in the top 5 that depends on a Windows-only external tool
(`CrmSvcUtil.exe` distributed in `Microsoft.CrmSdk.CoreTools`) — that whole
code-generation path needs replacement before the plugin is functional, which
makes this the longest tail of the five.

## Confidence inputs (from inventory)

DLaB is a multi-tool repo (18 csprojs). For EBG specifically the relevant tree:

- `DLaB.EarlyBoundGeneratorV2` — UI, ~12 .cs files
- `DLaB.EarlyBoundGeneratorV2.Api` — orchestration
- `DLaB.EarlyBoundGeneratorV2.Logic` — code-gen logic
- `DLaB.XrmToolBoxCommon` — shared base + PropertyGrid wrapper
- `DLaB.Xrm.Entities.XrmToolBoxCommon` — early-bound types for the shared base
- `DLaB.Log` — logger
- `DLaB.ModelBuilderExtensions`, `DLaB.CrmSvcUtilExtensions` — code-gen extensions
- `EarlyBoundSettingsGenerator.SettingsUpdater` — settings migration

Combined ≈ 8 of 18 csprojs in the upstream are in scope.

- 8 `DataGridView` usages across the whole repo (some in unrelated tools)
- 2 `DllImport` (in unrelated tools — `OutlookTimesheetCalculator`, `VSSolutionAccelerator`)
- 1 Registry usage (`VSSolutionAccelerator` — unrelated)
- Hard third-party deps:
  - `Microsoft.CrmSdk.CoreTools` (CrmSvcUtil.exe Windows-only) ★
  - `DLaBPluginControlBase` (custom shared base — net462, WCF-bound)
  - `System.Media.SoundPlayer` (Windows-only)
  - `WinForms PropertyGrid` (no Avalonia equivalent — heavy customisation in DLaB)
  - Application Insights (cross-plat — keep)

## Per-file findings (top-level only)

| Source file | Lines | Status | Notes |
|---|---:|---|---|
| `EarlyBoundGeneratorPlugin.cs` | ~700 | placeholder | Long file — the ExportMetadata block is at line 607. |
| `EarlyBoundGeneratorPlugin.Designer.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `Settings/SettingsMap.cs` | – | not emitted | Pure data model — eligible for direct port. |
| `Settings/SettingsMapDescriptor.cs` | – | not emitted | Pure data model — eligible. |
| `Settings/ConnectionSettings.cs` | – | not emitted | Pure data model — eligible. |
| `AttributeCaseSpecifierDialog.cs` (+ Designer) | – | not emitted | TODO_PORT_DESIGNER. |
| `AttributesToEnumMapperDialog.cs` (+ Designer) | – | not emitted | TODO_PORT_DESIGNER. |
| `AttributesToEnumMapperEditor.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `AttributeToEnumMapperDialog.cs` (+ Designer) | – | not emitted | TODO_PORT_DESIGNER. |
| `SpecifyAttributeNamesDialog.cs` (+ Designer) | – | not emitted | TODO_PORT_DESIGNER. |
| `SpecifyAttributesCaseEditor.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `SpecifyEntityNameEditor.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `SpecifyStringValuesDialog.cs` (+ Designer) | – | not emitted | TODO_PORT_DESIGNER. |

## Suggested next steps

1. Replace the CrmSvcUtil.exe code-gen path with a programmatic
   `Microsoft.PowerPlatform.Dataverse.ModelBuilder` invocation on the macOS side
   (cross-platform NuGet, .NET 6+).
2. Re-author the WinForms `PropertyGrid` heart of the UI as a typed Avalonia
   form. The settings map already encodes the metadata for it.
3. Replace `System.Media.SoundPlayer` with a no-op or Avalonia notification.
4. Port the 3 `Settings/*` files verbatim — they are clean .NET Standard.
