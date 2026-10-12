# Self-contained win-x64 publish of ExitPlan.UI, then a Velopack release.
#
# One command from the repo root (Windows, or Linux with the .NET 8 SDK):
#   .\publish.ps1
#
# Output in artifacts\:
#   ExitPlan-win-Setup.exe
#   ExitPlan-<version>-full.nupkg
#   ExitPlan-<version>-delta.nupkg   (only when a previous Velopack release is present)
#   releases.win.json
#
# Folder publish (not single-file): WPF + LiveCharts/SkiaSharp + AvalonEdit load
# native libraries, and the shipped TaxData / RMDData / MarketData / Data defaults
# are copied into Documents\ExitPlan on first launch. See docs\DEPLOY.md.
#
# Releases are hosted on the public repo https://github.com/pwscho/ExitPlan.
# vpk download uses that URL and does not send a token. A failed download
# (no prior Velopack release, or offline) does not fail the pack.
# Pass -SkipDownload when a caller already ran `vpk download github`.

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipDownload
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$VpkVersion = "1.2.161"
# Public releases repo. Not the private source repository.
$RepoUrl = "https://github.com/pwscho/ExitPlan"
$PackId = "ExitPlan"

function Test-IsWindows {
    if ($PSVersionTable.PSEdition -eq "Core") {
        return [bool]$IsWindows
    }
    return $env:OS -eq "Windows_NT"
}

function Get-ProjectVersion([string]$CsprojPath) {
    $text = Get-Content -LiteralPath $CsprojPath -Raw
    $match = [regex]::Match($text, "<Version>([^<]+)</Version>")
    if (-not $match.Success) {
        throw "Could not read <Version> from $CsprojPath"
    }
    return $match.Groups[1].Value.Trim()
}

function Get-HomeDir {
    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        return $env:USERPROFILE
    }
    if (-not [string]::IsNullOrWhiteSpace($env:HOME)) {
        return $env:HOME
    }
    throw "Could not locate the user profile directory."
}

function Install-Vpk {
    & dotnet tool install --global vpk --version $VpkVersion
    if ($LASTEXITCODE -eq 0) {
        return
    }

    & dotnet tool update --global vpk --version $VpkVersion
    if ($LASTEXITCODE -ne 0) {
        throw "Could not install vpk $VpkVersion. Install the .NET 8 SDK, then re-run.\nhttps://docs.velopack.io"
    }
}

function Get-VpkExe {
    $tools = Join-Path (Get-HomeDir) ".dotnet"
    $tools = Join-Path $tools "tools"
    $name = "vpk"
    if (Test-IsWindows) {
        $name = "vpk.exe"
    }

    $full = Join-Path $tools $name
    if (Test-Path -LiteralPath $full) {
        return $full
    }

    $cmd = Get-Command vpk -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    throw "vpk $VpkVersion was installed but not found at $full"
}

function Get-VpkTargetArgs {
    # vpk on Linux/macOS defaults to that OS. [win] switches the CLI to the
    # Windows packer so the setup exe can be built from a non-Windows machine.
    # Build the array here. A function that returns a one-element array is
    # unwrapped by PowerShell, and splatting that string passes each character.
    $argsForTarget = @()
    if (-not (Test-IsWindows)) {
        $argsForTarget = @("[win]")
    }
    return ,$argsForTarget
}

$repoRoot = $PSScriptRoot
$project = Join-Path $repoRoot "ExitPlan.UI\ExitPlan.UI.csproj"
$artifacts = Join-Path $repoRoot "artifacts"
$outputDir = Join-Path $artifacts "win-x64"
$icon = Join-Path $repoRoot "ExitPlan.UI\Assets\ExitPlan.ico"

if (-not (Test-Path -LiteralPath $project)) {
    throw "UI project not found: $project"
}
if (-not (Test-Path -LiteralPath $icon)) {
    throw "Application icon not found: $icon"
}

$version = Get-ProjectVersion $project

Write-Host "Publishing ExitPlan.UI $version ($Configuration, $Runtime, self-contained folder)..."
Write-Host "Project: $project"
Write-Host "Output:  $outputDir"

if (Test-Path -LiteralPath $outputDir) {
    Remove-Item -LiteralPath $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

$publishArgs = @(
    "publish", $project,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", "true",
    "--output", $outputDir,
    "-p:PublishSingleFile=false",
    "-p:PublishTrimmed=false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$required = @(
    "ExitPlan.UI.exe",
    "LICENSE.md",
    "Data\AppState.json",
    "TaxData\Tax2025.json",
    "TaxData\Tax2026.json",
    "RMDData\RMD2025.json",
    "MarketData\MarketData.json"
)

$missing = @()
foreach ($rel in $required) {
    $full = Join-Path $outputDir $rel
    if (-not (Test-Path -LiteralPath $full)) {
        $missing += $rel
    }
}

if ($missing.Count -gt 0) {
    throw "Publish succeeded but required offline files are missing:`n  $($missing -join "`n  ")"
}

Write-Host ""
Write-Host "Publish is complete. Shipped defaults are present:"
foreach ($rel in $required) {
    Write-Host "  $rel"
}

Write-Host ""
Write-Host "Installing Velopack CLI vpk $VpkVersion ..."
Install-Vpk
$vpk = Get-VpkExe
Write-Host "vpk: $vpk"

if (-not $SkipDownload) {
    Write-Host "Downloading the previous public release ($RepoUrl) so vpk can build a delta..."
    $downloadPrefix = Get-VpkTargetArgs
    & $vpk @downloadPrefix download github --repoUrl $RepoUrl --outputDir $artifacts --channel win
    if ($LASTEXITCODE -ne 0) {
        Write-Host "vpk download github exited $LASTEXITCODE. Continuing with a full package only."
    }
}

# Re-packing the same version should replace that version's packages and keep older ones for deltas.
$staleNames = @(
    "${PackId}-win-Setup.exe",
    "$PackId-$version-full.nupkg",
    "$PackId-$version-delta.nupkg",
    "$PackId-Portable.zip",
    "assets.win.json"
)
foreach ($name in $staleNames) {
    $stalePath = Join-Path $artifacts $name
    if (Test-Path -LiteralPath $stalePath) {
        Remove-Item -LiteralPath $stalePath -Force
    }
}

$notesPath = Join-Path $artifacts "release-notes.md"
$notes = @'
Self-contained Windows installer for ExitPlan __VERSION__.

Download **ExitPlan-win-Setup.exe**. You do not need Visual Studio or the .NET Desktop Runtime.

The installer is per-user (no administrator prompt) and puts the program in `%LocalAppData%\ExitPlan`. Start Menu and desktop shortcuts are created. Later versions are offered inside the app; ExitPlan does not install an update unless you choose Yes.

Scenarios and edited tax, RMD, and market files are stored in `Documents\ExitPlan`, not beside the program, so an update does not erase them.

**SmartScreen:** Windows may show "Windows protected your PC" until this download has a reputation. Choose **More info**, then **Run anyway**.

This build is not Authenticode-signed in CI. Sign on a local PC before a wide release and paste the SHA256 here:

SHA256: (add after signing)
'@
$notes = $notes.Replace("__VERSION__", $version)
Set-Content -LiteralPath $notesPath -Value $notes -Encoding utf8

Write-Host ""
Write-Host "Running vpk pack for $version ..."
$packArgs = @(
    "pack",
    "--packId", $PackId,
    "--packVersion", $version,
    "--packDir", $outputDir,
    "--mainExe", "ExitPlan.UI.exe",
    "--packTitle", "ExitPlan",
    "--packAuthors", "Paul Schofield",
    "--icon", $icon,
    "--runtime", "win-x64",
    "--channel", "win",
    "--outputDir", $artifacts,
    "--releaseNotes", $notesPath,
    "--noPortable"
)
$packPrefix = Get-VpkTargetArgs
& $vpk @packPrefix @packArgs
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed with exit code $LASTEXITCODE"
}

$setupPath = Join-Path $artifacts "${PackId}-win-Setup.exe"
$fullPkg = Join-Path $artifacts "$PackId-$version-full.nupkg"
$feed = Join-Path $artifacts "releases.win.json"
$delta = Join-Path $artifacts "$PackId-$version-delta.nupkg"

foreach ($path in @($setupPath, $fullPkg, $feed)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "vpk pack finished but missing $path"
    }
}

Write-Host ""
Write-Host "Velopack release is in ${artifacts}:"
Write-Host "  $setupPath"
Write-Host "  $fullPkg"
Write-Host "  $feed"
if (Test-Path -LiteralPath $delta) {
    Write-Host "  $delta"
}
else {
    Write-Host "  (no delta: no earlier Velopack package was in $artifacts)"
}
Write-Host ""
Write-Host "Install: $setupPath"
Write-Host "Public download after a GitHub Release: $RepoUrl/releases/latest"
