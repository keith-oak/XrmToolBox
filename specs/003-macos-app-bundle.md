# 003 — macOS `.app` Bundle, Icon, Codesigning

## Priority: HIGH

## Status: COMPLETE

## Description

Package the Avalonia shell as a proper macOS application bundle (`XrmToolBox.app`) so the dock, app switcher, and Mission Control all show "XrmToolBox" with a real icon — not "Avalonia Application" with a generic terminal glyph. Produce a single double-clickable artifact that runs without `dotnet`. Optional but documented: codesigning + notarising for distribution.

## Why

Right now the executable shows up as "Avalonia Application" with no icon because we're running the raw `dotnet` binary out of `bin/Debug/net10.0/`. A native-feeling app must look native in every macOS surface — dock, Cmd-Tab, Force Quit, About panel. This is non-negotiable for the shell to be taken seriously by Mac users.

## Acceptance Criteria

### Bundle structure

- [ ] `dist/XrmToolBox.app/Contents/Info.plist` with at minimum:
  - `CFBundleName = XrmToolBox`
  - `CFBundleDisplayName = XrmToolBox`
  - `CFBundleIdentifier = com.lucidlabs.xrmtoolbox`
  - `CFBundleVersion` from project `<Version>`
  - `CFBundleShortVersionString` from `<Version>`
  - `CFBundleExecutable = XrmToolBox`
  - `CFBundleIconFile = AppIcon`
  - `CFBundlePackageType = APPL`
  - `LSMinimumSystemVersion = 14.0`
  - `NSHighResolutionCapable = true`
  - `NSPrincipalClass = NSApplication`
  - `NSRequiresAquaSystemAppearance = false` (so dark mode works)
- [ ] `dist/XrmToolBox.app/Contents/MacOS/XrmToolBox` is the published executable
- [ ] `dist/XrmToolBox.app/Contents/Resources/AppIcon.icns` ships an icon at 16, 32, 64, 128, 256, 512, 1024 px (each at 1x and 2x)
- [ ] `dist/XrmToolBox.app/Contents/Resources/Plugins/` contains the sample plugin (and any others present at build time)
- [ ] Double-clicking `XrmToolBox.app` in Finder launches the shell with no terminal window, dock shows the icon, Cmd-Tab shows "XrmToolBox"

### Icon

- [ ] Source artwork at `assets/icon/icon.svg` (1024×1024, vector) — distinctive XrmToolBox glyph in macOS Sequoia rounded-square style. If a usable upstream logo exists in the repo, derive from that; otherwise produce a simple placeholder (a stylised "X" inside a Mac-style squircle with the accent blue).
- [ ] `assets/icon/icon.iconset/` containing the 10 PNGs Apple's `iconutil` expects (`icon_16x16.png`, `icon_16x16@2x.png`, …, `icon_512x512@2x.png`)
- [ ] `assets/icon/AppIcon.icns` generated from the iconset via `iconutil -c icns` (committed; regeneration documented in the build script)

### Build script

- [ ] `scripts/build-macos-app.sh` (executable) that:
  1. Runs `dotnet publish src.macos/XrmToolBox.MacOS -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=false`
  2. Assembles `dist/XrmToolBox.app/Contents/{MacOS,Resources}` from `Info.plist`, the published output, the icon, and the plugin DLLs
  3. Marks `Contents/MacOS/XrmToolBox` executable and clears the macOS quarantine attribute on the bundle (`xattr -dr com.apple.quarantine`)
  4. Optional `--sign "Developer ID Application: …"` flag that codesigns with hardened runtime + timestamp; default behaviour is unsigned (ad-hoc)
  5. Optional `--zip` flag that produces `dist/XrmToolBox-<version>-osx-arm64.zip` ready for transfer
  6. Optional `--target osx-x64` flag that builds an Intel bundle in addition to ARM64
- [ ] Script runs end-to-end with no manual steps required for the unsigned flow
- [ ] Script is idempotent — re-running produces the same bundle without leftover files

### MSBuild integration

- [ ] `src.macos/XrmToolBox.MacOS/XrmToolBox.MacOS.csproj` declares the bundle metadata so `dotnet publish` picks up the version + icon path:
  - `<ApplicationIcon>` pointing at the `.icns` for tooling that wants it
  - `<AssemblyTitle>` and `<Product>` set to "XrmToolBox" so any displayed assembly metadata is consistent

### Distribution path (documented, not all required)

- [ ] `docs/macos-distribution.md` covering:
  - How to run the unsigned bundle locally (Right-click → Open the first time)
  - What codesigning requires (Apple Developer ID, certificate in Keychain)
  - What notarising adds (`xcrun notarytool submit … --wait`, `xcrun stapler staple`)
  - Universal binary (osx-arm64 + osx-x64 → `lipo -create`)

## Technical Requirements

- [ ] All shell scripts pass `shellcheck`
- [ ] `dotnet build` and the bundle script both succeed on a clean checkout (`rm -rf dist && ./scripts/build-macos-app.sh`)
- [ ] Bundle launches and the dock title reads "XrmToolBox"

## Manual Verification Steps

```bash
# 1. Build the bundle
./scripts/build-macos-app.sh
# 2. Launch via Finder OR:
open dist/XrmToolBox.app
# 3. Confirm in the dock that the icon and name read "XrmToolBox" (not "Avalonia Application")
# 4. Cmd-Tab → confirm same
# 5. Right-click app icon in dock → Options → Show in Finder → bundle should be in dist/
# 6. Quit and re-run XrmToolBox --probe still works on the bundle's binary:
dist/XrmToolBox.app/Contents/MacOS/XrmToolBox --probe
```

## Out of Scope

- App Store distribution / sandboxing
- Auto-update mechanism (Sparkle, Squirrel, or custom). Out for v2.0.
- DMG installer with custom background. The zip is enough; pretty installer can come later.
- Windows MSIX / Linux AppImage. Cross-platform packaging is a separate spec.
- Universal binary as a default — produce per-arch by default, document the `lipo` recipe.
