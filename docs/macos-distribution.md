# macOS distribution

## Build the app bundle

```bash
./scripts/build-macos-app.sh                 # ARM64 (M1/M2/M3/M4), unsigned
./scripts/build-macos-app.sh --target osx-x64  # Intel, unsigned
./scripts/build-macos-app.sh --zip            # also produce dist/XrmToolBox-<version>-<rid>.zip
```

The output is `dist/XrmToolBox.app`. Drag it to `/Applications` (or run from `dist/`).

## Running an unsigned bundle

The script ad-hoc signs by default. macOS Gatekeeper still complains the first time someone double-clicks an unsigned bundle. Workaround:

1. **Right-click** `XrmToolBox.app` in Finder → **Open** → confirm in the dialog
2. Or: `xattr -dr com.apple.quarantine dist/XrmToolBox.app` before launching

After the first run, normal double-click works.

## Codesigning + notarising (for distribution)

You need:

- An Apple Developer Program membership ($99/year)
- A `Developer ID Application` certificate in your login keychain
- An app-specific password for `notarytool`

```bash
# 1. Build + codesign in one step
./scripts/build-macos-app.sh --sign "Developer ID Application: Your Name (TEAMID)" --zip

# 2. Notarise the zip (Apple's servers must approve)
xcrun notarytool submit dist/XrmToolBox-2.0.0-osx-arm64.zip \
  --apple-id you@example.com \
  --team-id TEAMID \
  --password "app-specific-password" \
  --wait

# 3. Staple the notarisation ticket to the bundle so it works offline
xcrun stapler staple dist/XrmToolBox.app
```

After stapling, re-zip the bundle to ship it. Recipients can double-click without any Gatekeeper prompt.

## Universal binary (ARM64 + Intel)

```bash
./scripts/build-macos-app.sh --target osx-arm64
mv dist/XrmToolBox.app dist/XrmToolBox-arm64.app

./scripts/build-macos-app.sh --target osx-x64
mv dist/XrmToolBox.app dist/XrmToolBox-x64.app

# Combine the executables into a fat binary
mkdir -p dist/XrmToolBox.app
cp -R dist/XrmToolBox-arm64.app/. dist/XrmToolBox.app/
lipo -create \
  dist/XrmToolBox-arm64.app/Contents/MacOS/XrmToolBox \
  dist/XrmToolBox-x64.app/Contents/MacOS/XrmToolBox \
  -output dist/XrmToolBox.app/Contents/MacOS/XrmToolBox
codesign --force --deep --sign - dist/XrmToolBox.app
```

## Why this isn't yet automated end-to-end

CI workflow that runs `build-macos-app.sh --sign --zip --notarise` on a `macos-latest` runner is straightforward to add — it's spec 005 territory once we have a Developer ID. The current script is intentionally local-first so contributors without a Developer ID can still ship working ad-hoc bundles.
