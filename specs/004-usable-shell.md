# 004 — Usable Shell: Settings, Recent Connections, Plugin Store, Command Palette

## Priority: HIGH

## Status: COMPLETE (v1.1)

> v1 shipped: settings persistence + window state, recent connections (AutoCompleteBox + forget), settings panel, About panel, Plugin Store with online catalogue + offline fallback + honest "needs porting" install message, command palette (overlay), native macOS menu bar with File/View/Help, Cmd-K / Cmd-, / Cmd-R bindings.
>
> **v1.1 (this branch) adds**: rebrand to PAC'd Toolbox + brand visual system (purple #6C5CE7, Inter Tight, 8/12 corner radii, sidebar nav with seven sections), inline Settings + About panes (no more modal dialogs), live-apply theme via segmented `Auto / Light / Dark` control with stable geometry (no layout shift on toggle), draggable toolbar (BeginMoveDrag wired on PointerPressed for the extended client-area chrome), separate **Manage Tools** pane distinct from the **Tools** runner so users can install/uninstall without touching Finder, MSAL + `MsalCacheHelper.WithMacKeyChain()` with verified persistence so deny actually fails sign-in, sample-tool entity pre-flight guard, ResetConnection plumbing, sidebar emoji icons centred via `Viewbox`, About brand-essence card removed, default `ListBoxItem:selected` no longer overrides nested `TextBlock` foregrounds (so secondary text + chips stay legible).
>
> v2 follow-ups: agentic plugin porter (spec 002), command palette fuzzy filter, MSAL token-cache cleanup on Forget, About-panel link buttons, "Open logs folder" via OS shell, command palette opening focuses the search box.

## Description

Right now the shell does the bare minimum: connect, list plugins, open one. It needs the boring-but-essential parts of any app — persisted settings, recent connections, a plugin store to install new tools without touching Finder, an About panel, a command palette, the macOS menu bar. Everything that turns "tech demo" into "a tool I leave open all day."

## Why

After the theme spec (001) and bundle spec (003), the app *looks* native but doesn't yet *behave* native. Mac users expect Cmd-, for Settings, Cmd-K for command palette, a populated menu bar with About / Settings / Quit, sticky window placement, and an obvious way to install new tools. Without these, the app is sparse and unfriendly regardless of how nice the chrome is.

## Acceptance Criteria

### Persisted settings store

- [ ] `XrmToolBox.MacOS.Settings.SettingsService` reads + writes JSON at `~/Library/Application Support/XrmToolBox/settings.json` (macOS), `%LOCALAPPDATA%\XrmToolBox\settings.json` (Windows), `~/.config/XrmToolBox/settings.json` (Linux)
- [ ] Atomic writes (temp file + rename), schema versioned (`{"$schema": 1, ...}`)
- [ ] Settings include: theme variant override (auto/light/dark), default Dataverse URL, window size + position, recent connections, last opened plugin
- [ ] Service is constructor-injected into `MainWindowViewModel` and any future view-models that need it

### Recent connections

- [ ] After a successful connect, the URL + organisation friendly name + auth mode is persisted to `recentConnections` in settings (max 10 entries, MRU order)
- [ ] Toolbar URL `TextBox` becomes a `ComboBox` (or `AutoCompleteBox`) backed by recent connections; typing filters; selecting one prefills the URL
- [ ] Context-menu on a recent entry: "Forget this connection" → removes from list, also clears the matching MSAL token cache entry
- [ ] Connections store the `OrganizationFriendlyName` so the dropdown shows "Lucid Labs Sales — lucidlabs.crm6.dynamics.com" instead of just the URL

### Settings dialog (Cmd-,)

- [ ] Modal `SettingsWindow` opened by Cmd-, on macOS, Ctrl-, on Windows/Linux, or via menu bar > XrmToolBox > Settings
- [ ] Sections: General (theme override, default URL), Connections (manage recent, clear token cache), Plugins (open plugins folder, refresh plugin list), Advanced (logs folder, reset all settings)
- [ ] Settings persist immediately on change (no "Apply" button)
- [ ] Theme override takes effect live — switching to Dark in settings flips the running app

### Command palette (Cmd-K / Ctrl-K)

- [ ] `CommandPaletteWindow` opens centred over the main window, backdrop dimmed
- [ ] Lists all available actions: open each plugin, connect / disconnect, open settings, open plugins folder, quit, plus a fuzzy search field
- [ ] Up/Down arrows navigate, Enter executes, Esc dismisses
- [ ] Closes automatically on action execution

### macOS menu bar

- [ ] Native menu bar with these menus when running on macOS:
  - **XrmToolBox**: About XrmToolBox, Settings… (Cmd-,), Hide / Show / Quit
  - **File**: Connect… (Cmd-N), Disconnect (Cmd-Shift-D), Close Tab (Cmd-W)
  - **View**: Command Palette (Cmd-K), Reload Plugins (Cmd-R)
  - **Window**: standard Avalonia window items
  - **Help**: XrmToolBox Documentation (opens the upstream docs URL)
- [ ] Avalonia's `NativeMenu` API; falls back gracefully on non-mac
- [ ] Quit (Cmd-Q) saves window position + open tab list to settings before exiting

### Window state restoration

- [ ] Window size, position, and which plugins were open at last shutdown are restored at startup (subject to the screen still being available — fall back to centred if the saved position is off-screen)
- [ ] Last active plugin is reselected after restoration

### About panel

- [ ] Native macOS About panel via the menu bar, populated from `Assembly` metadata: app name, version, "Cross-platform XrmToolBox · native build", credit "Original XrmToolBox by Tanguy Touzard / MscrmTools", link to `https://www.xrmtoolbox.com`
- [ ] On non-macOS: same content via a plain modal dialog reachable from the toolbar overflow menu

### Plugin store (browser + installer stub)

- [ ] **v1 of the store:** an in-app browser pointed at the existing community catalogue. The official feed at `https://nuget.xrmtoolbox.com/api/v3/index.json` lists every plugin as a NuGet package. v1 just lists those packages with name, description, author, version, download count.
- [ ] Selecting a package shows its details and a single button: "Install"
- [ ] **v1 install flow** is honest: because the existing packages are .NET Framework + WinForms and won't work in the macOS shell, the install button shows: "This plugin needs porting before it can run on macOS. Run the porter? (See spec 002.)" — with a button that copies the package URL and opens the porter docs. Don't pretend to install something that won't load.
- [ ] **v2 (out of scope)**: once spec 002 (porter) is shipped, the install button kicks off a port + drop in the local plugins folder. Wired up in spec 002, not here.
- [ ] Store is accessible via toolbar button "Browse tools" + menu bar > File > Browse Tool Library

### Empty-state polish

- [ ] When no plugins are installed at all (not just none open), the empty state offers a "Browse Tool Library" button instead of just text
- [ ] When connected but nothing open, the empty state shows the connected org name + a "Recent Tools" list (last N opened)
- [ ] Subtle difference: "no tool open" vs "no tools installed" vs "no connection"

### Logging

- [ ] All `LogManager` style writes (currently a no-op contract in the SDK) land in `~/Library/Logs/XrmToolBox/xrmtoolbox.log` (macOS) — rolling, max 5 MB
- [ ] Settings dialog "Open logs folder" reveals it in Finder

## Technical Requirements

- [ ] `dotnet build src/XrmToolBox.MacOS.slnx -warnaserror` exits 0
- [ ] `XrmToolBox --probe` still passes
- [ ] `dotnet format src/XrmToolBox.MacOS.slnx --verify-no-changes` exits 0
- [ ] Settings file written by the app round-trips correctly: launch, change theme, quit, relaunch, theme persists
- [ ] Window position round-trips correctly across relaunches

## Manual Verification Steps

```bash
# 1. Build clean
dotnet build src/XrmToolBox.MacOS.slnx -nologo -warnaserror
# 2. Launch via the bundle (after spec 003 is done) or directly:
dotnet run --project src/XrmToolBox.MacOS
# 3. Connect to a Dataverse env → confirm URL is saved as recent
# 4. Cmd-, → Settings opens, switch theme → app updates immediately
# 5. Cmd-K → Command palette opens, fuzzy-find "Sample Tool" → opens it
# 6. Resize and reposition window → quit (Cmd-Q) → relaunch → window is where you left it
# 7. Click "Browse Tool Library" → store opens, lists at least 5 community plugins
# 8. Click Install on one → see the honest "needs porting" message, not a fake success
```

## Out of Scope

- Actual plugin install flow that produces a working tool — covered by spec 002
- Multi-window support (one main window per tenant). Defer.
- Plugin sandboxing
- Remote settings sync / iCloud
- In-app diff / merge of `.json` settings (use OS file explorer for power users)
- Localisation / translations
