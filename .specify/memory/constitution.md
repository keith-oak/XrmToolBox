# XrmToolBox macOS-Native Constitution

## Identity

XrmToolBox is a Dataverse / Dynamics 365 plugin host. The macOS-native fork (`src/`) replaces the Windows-only WinForms shell with a cross-platform Avalonia + .NET 10 stack so the same binary runs on macOS (primary), Windows, and Linux.

## Core Values

1. **Native feel first.** macOS users must not feel they're running a Windows app. Apple HIG conformance is non-negotiable on macOS; the same theme falls back gracefully on Windows / Linux.
2. **Plugin contract is sacred.** The `IXrmToolBoxPlugin` SDK surface (`src/XrmToolBox.Extensibility.Core`) is the long-lived contract. Nothing in the shell may break a plugin that compiled cleanly against a previous SDK minor version.
3. **Ecosystem matters.** The hundreds of existing community plugins are .NET Framework + WinForms. Automated porting is a force multiplier, not a magic wand — output must be honest about what worked and what needs human review.
4. **No `any`, no excuses.** C# nullable reference types enabled, `TreatWarningsAsErrors` on, no `#pragma warning disable` without a comment explaining why.
5. **Verification beats assertion.** A spec is not done until `dotnet build`, the probe mode, and the documented manual UI checks all pass. "Should work" doesn't count.

## Autonomy Settings

- **YOLO Mode:** ENABLED — Ralph may make local refactors, restructure files, and commit autonomously when a spec passes its quality gates.
- **Git Autonomy:** ENABLED for commits on `macos-native-port`. Pushing to remote requires explicit user approval.
- **Skip Tests:** DISABLED. Where a UI test framework exists (Avalonia.Headless), use it; otherwise the verification step is a documented manual check the user can run.
- **Plugin SDK Breaking Changes:** DISABLED without an explicit spec authorising the break.

## Quality Standards

Inherited from `strict-development`. Concretely for this repo:

```bash
# All four must exit 0 before <promise>DONE</promise>
dotnet build src/XrmToolBox.MacOS.slnx -nologo -warnaserror > /dev/null 2>&1
src/XrmToolBox.MacOS/bin/Debug/net10.0/XrmToolBox --probe   # must list ≥1 plugin
dotnet format src/XrmToolBox.MacOS.slnx --verify-no-changes
# Manual UI verification step documented in the spec must be ticked off
```

Token efficiency rules from `ralph-autonomous` apply: truncate logs, use `--nologo`, `| tail -N`, exit codes not full output. Commit messages describe the change and why, never mention AI assistance.

## Out of Scope (constitutionally)

- Resurrecting the legacy Windows WinForms shell at the repo root. It stays as a reference, untouched.
- Re-implementing the connection-string GUI from `MscrmTools.Xrm.Connection`. We use Microsoft's `Microsoft.PowerPlatform.Dataverse.Client` end-to-end.
- Plugin sandboxing / signature verification. Out of scope for v2.0; revisit when ecosystem trust model is decided.
