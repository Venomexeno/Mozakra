<#
.SYNOPSIS
    Builds a local Velopack installer for testing on this machine (no upload).

.DESCRIPTION
    Does what .github/workflows/release.yml does, minus GitHub:
      1. dotnet publish (Release, win-x64, self-contained) into artifacts/publish
      2. vpk pack into artifacts/releases  -> Mozakra-win-Setup.exe, the .nupkg files, releases.win.json
    vpk is installed into artifacts/tools (pinned version), so nothing global changes.

.EXAMPLE
    ./build/pack.ps1                 # version from Directory.Build.props
    ./build/pack.ps1 -Version 1.0.0-local.1
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Keep in sync with the Velopack package in src/Mozakra.App/Mozakra.App.csproj and VPK_VERSION in release.yml.
$VpkVersion = '1.2.161'

$root      = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$publish   = Join-Path $artifacts 'publish'
$releases  = Join-Path $artifacts 'releases'
$tools     = Join-Path $artifacts 'tools'
$icon      = Join-Path $root 'src/Mozakra.App/Assets/mozakra.ico'

function Invoke-Step([string]$name, [scriptblock]$block) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $block
    if ($LASTEXITCODE -ne 0) { throw "$name failed (exit code $LASTEXITCODE)." }
}

if (-not $Version) {
    [xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
    $Version = @($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
    if (-not $Version) { throw 'No -Version given and none found in Directory.Build.props.' }
}
Write-Host "Version: $Version"

Push-Location $root
try {
    if (-not $SkipTests) {
        Invoke-Step 'Test' { dotnet test -c Release }
    }

    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    Invoke-Step 'Publish' {
        dotnet publish src/Mozakra.App -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o $publish
    }

    $vpk = Join-Path $tools 'vpk.exe'
    $installed = (Test-Path $vpk) -and
        [bool](dotnet tool list --tool-path $tools | Select-String -SimpleMatch " $VpkVersion ")
    if (-not $installed) {
        if (Test-Path $tools) { Remove-Item $tools -Recurse -Force }
        Invoke-Step "Install vpk $VpkVersion" { dotnet tool install vpk --version $VpkVersion --tool-path $tools }
    }

    # A fresh output folder each time: a local pack must not build deltas against an older local pack.
    if (Test-Path $releases) { Remove-Item $releases -Recurse -Force }
    Invoke-Step 'Pack' {
        & $vpk pack -u Mozakra -v $Version -r win-x64 -p $publish -e Mozakra.exe `
            --packTitle 'Mozakra' --packAuthors 'Nadeem Ibrahim' --icon $icon -o $releases
    }

    $setup = Get-ChildItem $releases -Filter '*Setup.exe' | Select-Object -First 1
    if (-not $setup) { throw "vpk pack finished but no Setup.exe was found in $releases." }
    Write-Host ''
    Write-Host "Installer: $($setup.FullName)" -ForegroundColor Green
}
finally {
    Pop-Location
}
