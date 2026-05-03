# 005 — Environments & Connections (upstream parity for the macOS shell)

## Priority: HIGH

## Status: COMPLETE (v2)

> v1 shipped: domain types (`ConnectionDetail` extended; `ConnectionFile` + `ConnectionsCatalogue` added to `Extensibility.Core`), `ConnectionCatalogueStore` (atomic JSON, schema-versioned, legacy-recent migration), `SecretStore` (macOS Keychain via `security` CLI; non-mac falls back to per-user file), all four auth flows (`ConnectAsync(ConnectionDetail)` over OAuth interactive / device code / client secret / certificate), `ConnectionsListXmlImporter`, per-tab `Connection` + `IsConnectionPinned` on `OpenedPluginViewModel`, Connections pane (file tree + quick-connect + import), Environments status grid, tab connection chip + pinned indicator, native menu item `File → Import connections from XrmToolBox…`.
>
> v2 shipped: `Add connection…` modal (two-step: pick auth mode → fill details + Test/Save/Save-and-connect), Edit support pre-populating the same modal, in-window device-code prompt (Copy code / Open URL / I've signed in), Keychain-backed secret entry with `Set secret` button, `card-button` references switched to existing `tertiary` style, modal `IsVisible` bindings use `$parent[Window]` to avoid scoped-DataContext resolution issues.
>
> Deferred (not blocking spec close): drag-drop reorder of catalogue, Cmd-Shift-K toolbar connection picker, per-tab connection-swap popover (chip is read-only today; swap happens via the Connections pane), `connections.log` rolling file (logs go to stderr for now via Avalonia trace).

## Description

Bring the macOS shell to feature parity with the upstream Windows app's connection model. Today the shell holds a single active `ConnectionDetail` and a flat MRU. Upstream has:

- A connection **catalogue** persisted to disk with multiple **connection files** (groups), each containing many `ConnectionDetail`s — used as a dev/UAT/prod organiser.
- A **per-tab** connection: every open plugin tab carries its own `ConnectionDetail`. Multiple environments side-by-side at once.
- Multiple **auth modes**: OAuth interactive, device code, client-secret S2S, certificate. (No on-prem AD; that's a non-goal on Mac.)
- A **management UI** for the catalogue (add / edit / remove / group / set-as-default), since the Windows-only `Microsoft.Xrm.Tooling.CrmConnectControl` doesn't exist on Mac.
- **Import** of `MscrmTools.ConnectionsList.xml` so users on Windows can move their environments across.

## Why

Every ported tool will assume "I'm operating against *some* connection — possibly different from the next tab over." Without per-tab connections we either cripple the tools or have to retrofit the model later. The catalogue + management UI is the one piece nobody else can ship for us, and it's what makes the app *useful daily* rather than *demoable*. Without import of the upstream XML, every existing XTB user has to rebuild their connections by hand.

## Acceptance Criteria

### Connection model — domain types

- [ ] `XrmToolBox.Extensibility.Core.ConnectionDetail` extended with the upstream-relevant fields:
  - `Guid ConnectionId`
  - `string ConnectionName`
  - `string Url`, `string? OrganizationDataServiceUrl`
  - `string? OrganizationFriendlyName`, `string? OrganizationVersion`, `string? UserName`
  - `AuthMode AuthMode` (OAuth, DeviceCode, ClientSecret, Certificate)
  - `string? AzureAdAppId` (used for ClientSecret/Certificate; defaults to the well-known Dataverse public client id for OAuth)
  - `string? Tenant` (optional explicit tenant id; default `organizations`)
  - `string? CertificateThumbprint` (for Certificate)
  - `string? ClientSecretSecretRef` (an opaque reference into Keychain, *never* the secret itself)
  - `Guid? ImpersonatedUserId`
  - `Guid? ParentFileId` (FK to a `ConnectionFile.Id`)
  - `DateTimeOffset LastUsed`
- [ ] New `ConnectionFile` type: `{ Guid Id, string Name, string? Description, string? IconBase64, List<ConnectionDetail> Connections }` — pure data, mutable, no behaviour.
- [ ] New `ConnectionsCatalogue` type holds the in-memory tree: `IList<ConnectionFile> Files`, `Guid? DefaultConnectionId`. Pure model. No I/O.
- [ ] All four types live in `XrmToolBox.Extensibility.Core` so a future SDK consumer (i.e. ported plugins) can read connection metadata if it wants to. No behaviour beyond simple validation in the setters.

### Catalogue persistence

- [ ] `ConnectionCatalogueStore` in `XrmToolBox.MacOS.Connection` reads + writes the catalogue to JSON at:
  - macOS: `~/Library/Application Support/PACdToolbox/connections.json`
  - Linux: `~/.config/PACdToolbox/connections.json`
  - Windows: `%LOCALAPPDATA%\PACdToolbox\connections.json`
- [ ] Atomic writes (temp file + rename), schema versioned (`{"$schema": 1, ...}`).
- [ ] **Secrets are never written to JSON.** Client secrets and any password-like material are stored in the macOS Keychain (using the same `MsalCacheHelper`-style helper already in use for the MSAL cache) under service `com.lucidlabs.pacdtoolbox.secrets`, account `<connectionId>`. The JSON only carries a stable reference key.
- [ ] **Migration step on first run**: if `~/Library/Application Support/PACdToolbox/settings.json` has a non-empty `RecentConnections` list and `connections.json` does not yet exist, seed `connections.json` with a single file called *Recent* containing those entries. After successful migration, leave `RecentConnections` in `settings.json` for one release as a read-only fallback.

### Import from upstream `MscrmTools.ConnectionsList.xml`

- [ ] Settings → Connections → "Import connections from XrmToolBox (Windows)…" opens a file picker.
- [ ] Importer reads the upstream XML schema (the format produced by `MscrmTools.Xrm.Connection`'s `ConnectionsList`), maps each `<Connection>` element to a `ConnectionDetail`, and groups by source file name into `ConnectionFile`s.
- [ ] Anything we can't honour (Windows-only auth modes — IFD, AD, OnPrem) is imported with a clear `Note` field set to `"unsupported auth mode <X>; convert to OAuth before use"` and `AuthMode` defaulted to `OAuth`. The detail still appears in the catalogue, just disabled until edited.
- [ ] Import is **non-destructive**: existing files in the catalogue are preserved; imported files are appended with a `(imported)` suffix on the file name if a name collision occurs.
- [ ] The importer emits a summary toast: "Imported N connections in M files. K need attention."

### Auth modes

- [ ] **OAuth interactive** — already works. No change beyond accepting the new `AzureAdAppId` / `Tenant` overrides.
- [ ] **Device code** — same MSAL flow but using `AcquireTokenWithDeviceCode`. Show a modal with the user_code + verification URL, copy-to-clipboard button, "I've signed in" button. Token cache also lives in Keychain.
- [ ] **Client secret (S2S)** — `ConfidentialClientApplication`, secret read from Keychain via `ClientSecretSecretRef`. On `Add Connection` form, a "Set secret…" button writes the secret to Keychain and stores only the ref.
- [ ] **Certificate** — `ConfidentialClientApplication.WithCertificate(...)` resolved from Keychain by `CertificateThumbprint`. (Mac users typically import certs into login.keychain; we look there.)
- [ ] All four modes funnel into a single `Task<(ServiceClient client, string? error)> ConnectAsync(ConnectionDetail)` on `DataverseConnectionService`. No mode-specific call sites in the ViewModels.
- [ ] Failed sign-in always returns a typed `ConnectError` with category (Cancelled, KeychainDenied, SecretMissing, NetworkError, AuthError, EnvironmentRefused) so the UI can render a useful message rather than dumping the raw exception text.

### Per-tab connection (the architecturally-significant change)

- [ ] `OpenedPluginViewModel` gains `ConnectionDetail? Connection` and `ServiceClient? Client`, replacing the implicit "shell-wide active connection".
- [ ] The shell still has a notion of a **session active connection** — the connection used for *new* tabs. That's surfaced in the toolbar and can be changed from the connections pane or the per-tab connection chooser.
- [ ] Each `PluginForm` tab header shows a small chip with the connection's friendly name. Clicking the chip opens a popover: *Use session connection / Pick from catalogue / Disconnect this tab*.
- [ ] When the session connection changes:
  - tabs whose `Connection` was the previous session connection auto-follow (this matches upstream's "ReuseConnections" default = on)
  - tabs whose user explicitly pinned to a different connection do **not** follow
- [ ] When a tab's connection changes (by any path), the plugin receives `UpdateConnection(IOrganizationService newClient, ConnectionDetail newDetail)` exactly once.
- [ ] Closing the last tab using a connection does **not** disconnect that connection — connections are owned by the shell, not by tabs. Disconnect happens from the connections pane or the toolbar.

### Connection management pane

- [ ] Existing `Connections` nav section renders a two-pane layout:
  - Left: the file tree (collapsible, files at top level, connections under each file). Empty state offers "Create your first connection file" + "Import from Windows…".
  - Right: detail pane for the selected connection or file. For a file: name, description, count, "Add connection". For a connection: every editable field, "Test connection", "Set as default", "Connect now", "Forget".
- [ ] **Add connection** flow is a modal with three steps:
  1. **Pick auth mode** — radio list of OAuth / Device code / Client secret / Certificate, each with a one-line "use this when…" explainer.
  2. **Fill details** — fields shown depend on mode. URL is always required and auto-completes from prior connections (URL-only, not full detail). Friendly name is optional and back-filled from the org after first successful connect.
  3. **Verify** — clicking *Test connection* runs the chosen flow, shows success/failure inline. *Save* commits to the catalogue; *Save and connect* additionally sets it as the session active connection.
- [ ] **Edit** opens the same modal pre-populated. Connection id is preserved across edits.
- [ ] **Forget** prompts confirm, removes from the catalogue, deletes the matching MSAL token cache entry (by `HomeAccountId`), and deletes any Keychain secret under `com.lucidlabs.pacdtoolbox.secrets/<connectionId>`.
- [ ] **Drag-and-drop** reorders connections and moves them between files. (Avalonia `DragDrop` API; mouse only on v1.)
- [ ] **Default connection**: starring one entry sets `ConnectionsCatalogue.DefaultConnectionId`. On app launch, if `IsConnectAtStartup` is on (settings), the shell connects to the default automatically. *Set as default* on a connection's detail pane does the same.

### Toolbar connection picker

- [ ] The current toolbar `Connect` button is replaced by a connection chooser:
  - When disconnected: a single button labelled "Connect" that opens a popover listing every catalogue connection grouped by file (last 10 by `LastUsed` at the top under a "Recent" group), plus an "Add connection…" footer item.
  - When connected: the button shows the connection's friendly name, the org version below in a smaller font, and a chevron. Clicking opens the same popover plus a "Disconnect" footer.
- [ ] Selecting a connection from the popover triggers connect (or, if already that connection, no-op). New tabs opened after a connection change inherit the new connection.
- [ ] Cmd-Shift-K opens the connection picker. Cmd-D disconnects the session.

### Environments pane

- [ ] The existing `Environments` nav section is repurposed as a **read-only status view** of every catalogue connection: name, URL, last connected, current status (Connected / Idle / Error), default flag.
- [ ] *Quick connect* button on each row.
- [ ] No editing here — editing happens in *Connections*. This pane is a dashboard.

### MRU + Favourites bound to connection

- [ ] `RecentTool` extended with `Guid? ConnectionId` so the home screen can show "Sample Tool — Lucid Labs Sales".
- [ ] Re-clicking an MRU entry re-opens the tool *and* re-selects the bound connection if it still exists (otherwise opens the tool against the session connection and surfaces a one-line warning).
- [ ] Favourites (`SettingsService.Current.Favourites: List<Favourite>`) — new list with `{ string PluginTypeName, Guid? ConnectionId }`. Favourite + connection pairs surface on Home as quick-launch tiles.

### Per-plugin connection event

- [ ] `XrmToolBox.Extensibility.Core.IXrmToolBoxPluginControl.UpdateConnection(IOrganizationService client, ConnectionDetail detail)` — already exists; ensure it's called every time a tab's connection changes (initial bind, session-follow, manual swap, disconnect → null connection delivers `ResetConnection()` instead).
- [ ] No plugin should ever see a stale `ConnectionDetail`.

### Telemetry / observability

- [ ] Every connect attempt writes a single line to `~/Library/Logs/PACdToolbox/connections.log` (rolling, 5 MB cap): timestamp, connection id, auth mode, success/error category, duration. **No URLs, no tokens, no friendly names** — just the structural fields. Matches the spec 004 logging story.

## Technical Requirements

- [ ] `dotnet build src/XrmToolBox.MacOS.slnx -warnaserror` exits 0
- [ ] `XrmToolBox --probe` still passes and lists at least the sample tool
- [ ] `dotnet format src/XrmToolBox.MacOS.slnx --verify-no-changes` exits 0
- [ ] `connections.json` round-trips: launch, add a connection, quit, relaunch — connection is restored.
- [ ] Importer round-trips: feed it a known-good `MscrmTools.ConnectionsList.xml` fixture (committed under `src/Tests/Fixtures/`), expect N connections in M files, no exceptions.
- [ ] Per-tab connection round-trips: open Sample Tool A on connection X, open Sample Tool B on connection Y, switch session connection to Z, confirm A and B both follow (default reuse), pin A to X explicitly, switch to W, confirm A stayed on X and B moved to W.

## Manual Verification Steps

```bash
dotnet build src/XrmToolBox.MacOS.slnx -nologo -warnaserror
dotnet run --project src/XrmToolBox.MacOS

# 1. With no catalogue, click "Connections" → empty state shows.
# 2. "Import connections from XrmToolBox" → pick a known XML → see file tree.
# 3. Click a connection → detail pane → "Connect now" → toolbar shows friendly name.
# 4. Cmd-N → Sample Tool opens, tab chip shows the connection name.
# 5. Toolbar picker → switch to a different env → tab chip updates, plugin sees UpdateConnection.
# 6. Right-click tab chip → "Pin to <other env>" → switch session again → pinned tab does not follow.
# 7. Add a Device Code connection → device code modal appears with copy + verification URL.
# 8. Add a Client Secret connection → "Set secret…" stores in Keychain (verify via Keychain Access.app: service com.lucidlabs.pacdtoolbox.secrets).
# 9. Forget a connection → confirm Keychain entry is gone (Keychain Access shows it removed).
```

## Out of Scope

- On-prem AD / IFD auth modes. Mac users don't have these; importer will mark them unsupported.
- Connection sharing across multiple Macs (iCloud/Dropbox sync of `connections.json`).
- Sandboxing per-connection token caches into separate Keychain items per environment (single MSAL cache today is fine).
- A connection-string DSL parser (the upstream "build from XrmToolBox connection string" UX). Add later if asked.
- Per-tab impersonation UI. The model supports `ImpersonatedUserId` but the picker UI lands in a follow-up.
- A REST/raw query runner that doesn't use `ServiceClient`. Tools that want raw HTTP can use the access token from the connection — exposed via a small SDK helper, but UI for it isn't in this spec.

## Sequencing

- Depends on spec 004 (settings store, MSAL keychain cache, command palette plumbing). All shipped.
- Independent of spec 002 (porter). The porter doesn't *need* this spec, but ported tools will silently benefit.
- Should land before any large batch of community-tool ports — a ported tool that can only see one connection at a time will need rework if we land per-tab afterwards.
