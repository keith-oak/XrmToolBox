# XrmToolBox for macOS — native build

Cross-platform port of XrmToolBox running natively on macOS (and Linux/Windows). Replaces the Windows-only WinForms shell with [Avalonia](https://avaloniaui.net/) on .NET 10.

## Layout

| Project | Purpose |
| --- | --- |
| `XrmToolBox.Extensibility.Core` | UI-agnostic plugin SDK contracts (`IXrmToolBoxPlugin`, `IXrmToolBoxPluginControl`, `PluginBase`, `ConnectionDetail`, `IPluginMetadata`, …) |
| `XrmToolBox.MacOS` | Avalonia shell — connection management, plugin discovery (MEF), tabbed plugin host |
| `Plugins/SampleTool` | Reference plugin demonstrating the cross-platform pattern |

The legacy Windows WinForms shell at the repository root is kept untouched; this directory tree is the parallel native build.

## Build & run

```bash
dotnet build src.macos/XrmToolBox.MacOS.slnx
dotnet run --project src.macos/XrmToolBox.MacOS
```

Probe plugin discovery without launching the UI:

```bash
src.macos/XrmToolBox.MacOS/bin/Debug/net10.0/XrmToolBox --probe
```

## Plugin model

Plugins are discovered via MEF (`System.ComponentModel.Composition`) at runtime from `<shell-bin>/Plugins/**/*.dll`. A plugin exports `IXrmToolBoxPlugin` with mandatory `IPluginMetadata` attributes:

```csharp
[Export(typeof(IXrmToolBoxPlugin))]
[ExportMetadata("Name", "My Tool")]
[ExportMetadata("Description", "...")]
[ExportMetadata("BackgroundColor", "#0078D4")]
[ExportMetadata("PrimaryFontColor", "White")]
[ExportMetadata("SecondaryFontColor", "WhiteSmoke")]
[ExportMetadata("SmallImageBase64", "")]
[ExportMetadata("BigImageBase64", "")]
public sealed class MyPlugin : PluginBase
{
    public override IXrmToolBoxPluginControl GetControl() => new MyPluginControl();
}
```

`IXrmToolBoxPluginControl.GetView()` returns an Avalonia `Control` (typed as `object` so the SDK assembly stays UI-agnostic — only the plugin assembly references Avalonia).

## Connection layer

The Windows-only `MscrmTools.Xrm.Connection` is replaced with `Microsoft.PowerPlatform.Dataverse.Client`. OAuth interactive sign-in uses MSAL, with the token cache in `~/Library/Application Support/XrmToolBox/TokenCache/`.
