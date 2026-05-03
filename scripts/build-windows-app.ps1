<#
.SYNOPSIS
    Build PAC'd Toolbox for Windows.

.DESCRIPTION
    Produces a self-contained Windows build of the macOS-port shell. The same
    .NET 10 + Avalonia 11 codebase that runs on macOS runs natively on Windows;
    this script just packages it into a folder + optional zip.

.PARAMETER Target
    Runtime identifier. Defaults to win-x64. Use win-arm64 for ARM64 Windows.

.PARAMETER SelfContained
    Bundle the .NET runtime so the user doesn't need .NET 10 installed.
    Default: true.

.PARAMETER Zip
    Produce dist\PACdToolbox-<version>-<rid>.zip alongside the publish folder.

.EXAMPLE
    .\scripts\build-windows-app.ps1
    .\scripts\build-windows-app.ps1 -Target win-arm64 -Zip
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('win-x64', 'win-arm64', 'win-x86')]
    [string]$Target = 'win-x64',

    [bool]$SelfContained = $true,

    [switch]$Zip
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$ShellProj  = Join-Path $RepoRoot 'src\XrmToolBox.MacOS\XrmToolBox.MacOS.csproj'
$DistDir    = Join-Path $RepoRoot 'dist'
$Slnx       = Join-Path $RepoRoot 'src\XrmToolBox.MacOS.slnx'

if (-not (Test-Path $ShellProj)) {
    throw "Shell project not found: $ShellProj"
}

# Read version from csproj
$csprojXml = [xml](Get-Content -Raw -LiteralPath $ShellProj)
$Version = ($csprojXml.Project.PropertyGroup |
    Where-Object { $_.Version } |
    Select-Object -First 1).Version
if (-not $Version) { $Version = '0.0.0' }

$PublishDir = Join-Path $DistDir "PACdToolbox-$Version-$Target"

Write-Host "==> Restoring solution" -ForegroundColor Cyan
dotnet restore $Slnx
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

Write-Host "==> Running tests" -ForegroundColor Cyan
$TestProj = Join-Path $RepoRoot 'src\XrmToolBox.Catalog.Tests\XrmToolBox.Catalog.Tests.csproj'
dotnet test $TestProj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "tests failed" }

Write-Host "==> Publishing shell ($Target, self-contained=$SelfContained)" -ForegroundColor Cyan
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

$publishArgs = @(
    'publish', $ShellProj,
    '-c', 'Release',
    '-r', $Target,
    "--self-contained=$($SelfContained.ToString().ToLower())",
    '-p:PublishSingleFile=false',
    '-p:DebugType=embedded',
    '-o', $PublishDir
)
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Build & copy plugins next to the shell EXE
$PluginsDir = Join-Path $PublishDir 'Plugins'
New-Item -ItemType Directory -Force -Path $PluginsDir | Out-Null

$PluginProjects = Get-ChildItem -Path (Join-Path $RepoRoot 'src\Plugins') -Recurse -Filter '*.csproj'
foreach ($p in $PluginProjects) {
    Write-Host "    publishing plugin: $($p.BaseName)"
    $pluginOut = Join-Path $PluginsDir $p.BaseName
    dotnet publish $p.FullName -c Release -r $Target --self-contained=false -o $pluginOut
    if ($LASTEXITCODE -ne 0) { throw "plugin publish failed: $($p.Name)" }
}

if ($Zip) {
    $ZipPath = Join-Path $DistDir "PACdToolbox-$Version-$Target.zip"
    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
    Write-Host "==> Zipping $ZipPath" -ForegroundColor Cyan
    Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath
}

Write-Host ""
Write-Host "Done. Run: $PublishDir\PACdToolbox.exe" -ForegroundColor Green
