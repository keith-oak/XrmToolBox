# 001 — Apple Design System Theme (cross-platform)

## Priority: HIGH

## Status: COMPLETE

## Description

Replace the default Avalonia FluentTheme in `src/XrmToolBox.MacOS` with a custom theme that follows Apple's Human Interface Guidelines (HIG) so the shell feels native on macOS. The same theme must degrade gracefully on Windows and Linux (no broken visuals, no missing fonts, just "looks tasteful" rather than "looks Mac").

The theme must respect the system's light/dark appearance and accent colour on macOS, and never hard-code a colour that should follow system tokens.

## Why

Right now the shell uses generic Avalonia Fluent — it looks like a Windows app on a Mac. The whole point of the native port is that it stops feeling alien. Per the constitution, "macOS users must not feel they're running a Windows app."

## Acceptance Criteria

### Visual identity (macOS — primary)

- [ ] Window uses native macOS chrome: traffic-light buttons, no custom title bar, content extends under the title bar where appropriate (`SystemDecorations="Full"`, `ExtendClientAreaToDecorationsHint="True"`)
- [ ] Sidebar (left tools list) uses translucent vibrancy effect via `TransparencyLevelHint="Mica,AcrylicBlur"`
- [ ] Body font is **SF Pro / system font** on macOS (`FontFamily="-apple-system,BlinkMacSystemFont,Inter"` or Avalonia equivalent); falls back to Inter (already shipped) elsewhere
- [ ] Body font size baseline 13pt (macOS HIG body), 11pt for secondary text, 17pt for window titles
- [ ] Corner radius: 6px on buttons / inputs, 10px on cards / list items, 12px on the window-level surfaces (matches macOS Sequoia controls)
- [ ] Buttons use HIG hierarchy: primary (filled, accent colour), secondary (bordered), tertiary (text only). Replace generic `<Button>` styles with these three variants.
- [ ] Accent colour follows the macOS system accent (read at startup; default to system blue on Win/Linux)
- [ ] Hover / pressed states match macOS feel — subtle (~6% opacity overlay), not the heavier Fluent ripple
- [ ] List items use macOS-style selection: solid accent fill, white text, rounded corners, inset 8px from sidebar edges

### Light / dark / system

- [ ] App respects `Application.RequestedThemeVariant = ThemeVariant.Default` and follows OS appearance
- [ ] Both light and dark variants render correctly — no white-on-white or black-on-black in either mode
- [ ] Accent colour overrides preserve readability in both modes (computed contrast ratio ≥ 4.5:1 against the background it sits on)

### Cross-platform fallback

- [ ] On Windows the theme renders without errors (vibrancy unavailable → flat tinted background instead, no exception)
- [ ] On Linux the theme renders without errors (same flat tint fallback)
- [ ] No platform-specific code paths in C# beyond a single `OperatingSystem.IsMacOS()` branch for window-chrome hints

### Layout

- [ ] Replace the current `<Border>` connection bar with a proper **toolbar surface** that sits flush under the title bar on macOS (extending `Margin="8,30,8,8"` over the chrome) and looks like a normal toolbar elsewhere
- [ ] Status bar (bottom) becomes a slim 22px translucent strip with monospaced caption text, matching macOS app conventions
- [ ] Sidebar minimum width 220, default 260, resizable via a `GridSplitter` with macOS-style 1px hairline divider
- [ ] Tab strip at the top of the host area uses underline-style selection (not Fluent's filled pill)

### Code quality

- [ ] One central `Themes/AppleTheme.axaml` ResourceDictionary with all colours, brushes, font families, font sizes, corner radii as named resources (no magic numbers in views)
- [ ] All bound colours use `DynamicResource` (so light/dark switching at runtime works)
- [ ] No hardcoded `#RRGGBB` outside `AppleTheme.axaml`
- [ ] `Styles/Buttons.axaml`, `Styles/ListItems.axaml`, `Styles/TextBlocks.axaml` define control variants

## Technical Requirements

- [ ] Avalonia 11.2.x ResourceDictionary + Styles approach (no third-party theme dependency unless justified in the PR)
- [ ] `dotnet build src/XrmToolBox.MacOS.slnx -warnaserror` exits 0
- [ ] `XrmToolBox --probe` still succeeds (theme doesn't break plugin discovery)
- [ ] `dotnet format src/XrmToolBox.MacOS.slnx --verify-no-changes` exits 0
- [ ] Manual verification (documented in spec completion comment): screenshot of macOS light, macOS dark, and the same window on Windows or Linux

## Manual Verification Steps

When marking this spec complete, the implementer must run and report:

```bash
# 1. Build clean
dotnet build src/XrmToolBox.MacOS.slnx -nologo -warnaserror
# 2. Probe
src/XrmToolBox.MacOS/bin/Debug/net10.0/XrmToolBox --probe
# 3. Format check
dotnet format src/XrmToolBox.MacOS.slnx --verify-no-changes
# 4. Launch and confirm visually
src/XrmToolBox.MacOS/bin/Debug/net10.0/XrmToolBox
#    → traffic lights present
#    → sidebar translucent
#    → SF Pro body font
#    → switch macOS to dark mode → app follows
#    → switch system accent (System Settings > Appearance > Accent) → app follows
```

## Out of Scope

- Custom plugin tile artwork / new iconography
- Animations beyond Avalonia defaults (no Lottie, no Apple-spring physics)
- Welcome / onboarding screens
- Per-plugin theming (plugins style themselves; the shell only themes its own chrome)
- Localised typography (RTL / CJK font stacks) — defer until requested
