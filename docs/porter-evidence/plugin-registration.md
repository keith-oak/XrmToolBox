# Plugin Registration — port report

**Upstream:** `Biznamics/PluginRegistration@2313168f75387b04d6638925ee300f040f907e44`
**Output:** `src/Plugins/PluginRegistration/`
**Confidence:** LOW

## Summary

3 files emitted. The plugin entry is faithful. The main control is a placeholder.
This is the only top-5 plugin that derives from the legacy
`XrmToolBox.Extensibility.PluginControlBase` (i.e. NOT the Rappen base) — so the
lifecycle wiring is closer to what the macOS extensibility core supports today.

## Confidence inputs (from inventory)

- 85 `.cs`, 20 `.Designer.cs`, 21 `.resx`, 2 `.csproj`
- 4 `DataGridView` usages
- 2 `BackgroundWorker`
- 0 `OnPaint`, 0 `DllImport`, 0 Registry ✅
- Hard third-party deps: none beyond standard CRM SDK + WinForms.
- Capability interfaces required but absent from macOS core:
  `IStatusBarMessenger`, `IShortcutReceiver`, `IAboutPlugin`.
- Notable: `AppDomainContext.cs` + `AssemblyResolver.cs` exist to side-load the
  user's plugin assembly into a separate AppDomain so its types can be reflected
  without polluting the host. **Not portable** to .NET 10 (AppDomain.CreateDomain
  removed). The replacement is `AssemblyLoadContext`.

## Per-file findings (top-level only)

| Source file | Lines | Status | Notes |
|---|---:|---|---|
| `Plugin.cs` | 24 | ported | Plain. |
| `MainControl.cs` (+ Designer) | – | placeholder | Master tree + tabs + property grid. |
| `Wrappers/Crm*.cs` (12 files) | – | not emitted | Pure logic — eligible for direct port. |
| `Forms/PluginRegistrationForm.cs` (+ Designer) | – | not emitted | TODO_PORT_DESIGNER. |
| `Forms/AttributeSelectionForm.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `Forms/ImageRegistrationForm.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `Forms/SearchForm.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `Forms/AssembliesFilterForm.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `Forms/PackageRegistrationForm.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `Forms/StepRegistrationForm.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `Forms/ConnectionsRegistrationForm.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `Forms/WebHookForm.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `Forms/ServiceEndpointRegistrationForm.cs` | – | not emitted | TODO_PORT_DESIGNER. |
| `AppDomainContext.cs`, `AssemblyResolver.cs`, `AssemblyReader.cs` | – | not emitted | NOT PORTABLE — TODO_PORT marker would replace AppDomain.CreateDomain with AssemblyLoadContext. |
| `DeviceidManager.cs` | – | not emitted | Legacy device-id auth — replaced by macOS auth subsystem. |
| `OrganizationServiceExtensions.cs`, `CrmServiceEndpoint.cs`, `Settings.cs`, `ProgressIndicator.cs` | – | not emitted | Pure logic / settings persistence — eligible for direct port. |

## Suggested next steps

1. Port the Wrappers/* tree first — they are pure CRM-SDK and form the data
   model for everything else.
2. Replace `AppDomainContext` with `System.Runtime.Loader.AssemblyLoadContext`.
3. Build the master TreeView + detail pane in Avalonia. Of all 5 ports, this
   one's UI is closest to "translatable" because it's mostly tree + grid +
   modal dialogs, no docking and no custom-drawn controls.
4. Lift `IStatusBarMessenger`, `IShortcutReceiver`, `IAboutPlugin` into the
   macOS core.
