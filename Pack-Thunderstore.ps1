#Requires -Version 5.1
<#
.SYNOPSIS
  Build a Valheim mod and produce a Thunderstore-ready ZIP.

.DESCRIPTION
  Copies thunderstore/manifest.json, icon.png, CHANGELOG.md, a player-facing
  README (thunderstore/README.md if present, else mods/<Mod>/README.md), and the
  Release DLL (plus TeflonTed.Common.dll when CopyCommonOnDeploy) into a staging
  folder, then zips files at the archive root (not nested in a parent folder).

  Collided package slugs use a Thunderstore-only _TT suffix (local names unchanged):
    CraftFromChests, FuelFromChests, AutoEat, AutoRepair, FarmGrid

.EXAMPLE
  .\Pack-Thunderstore.ps1 -Mod FightingFish
  .\Pack-Thunderstore.ps1 -Mod EternalLights -SkipBuild
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $Mod,

    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$ModDir = Join-Path $Root "mods\$Mod"
$TsDir = Join-Path $ModDir "thunderstore"
$Csproj = Get-ChildItem -Path $ModDir -Filter "TeflonTed.*.csproj" | Select-Object -First 1

if (-not (Test-Path $ModDir)) { throw "Mod folder not found: $ModDir" }
if (-not $Csproj) { throw "No TeflonTed.*.csproj in $ModDir" }
if (-not (Test-Path (Join-Path $TsDir "manifest.json"))) { throw "Missing $TsDir\manifest.json" }
if (-not (Test-Path (Join-Path $TsDir "icon.png"))) { throw "Missing $TsDir\icon.png (approve icon first)" }

# Thunderstore slug overrides (manifest.name is source of truth; this is documentation).
$SlugOverrides = @{
    "CraftFromChests" = "CraftFromChests_TT"
    "FuelFromChests"  = "FuelFromChests_TT"
    "AutoEat"         = "AutoEat_TT"
    "AutoRepair"      = "AutoRepair_TT"
    "FarmGrid"        = "FarmGrid_TT"
}

$manifest = Get-Content (Join-Path $TsDir "manifest.json") -Raw | ConvertFrom-Json
$packageName = $manifest.name
$version = $manifest.version_number

if ($SlugOverrides.ContainsKey($Mod) -and $packageName -ne $SlugOverrides[$Mod]) {
    Write-Warning "Expected Thunderstore name '$($SlugOverrides[$Mod])' for $Mod but manifest has '$packageName'"
}

if (-not $SkipBuild) {
    Write-Host "Building $($Csproj.Name) Release..."
    dotnet build $Csproj.FullName -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
}

$dllName = [IO.Path]::GetFileNameWithoutExtension($Csproj.Name) + ".dll"
$dllPath = Join-Path $ModDir "bin\Release\$dllName"
if (-not (Test-Path $dllPath)) { throw "DLL not found: $dllPath" }

$stage = Join-Path $Root "dist\thunderstore\$packageName-$version"
$plugins = Join-Path $stage "plugins"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $plugins | Out-Null

Copy-Item (Join-Path $TsDir "manifest.json") $stage
Copy-Item (Join-Path $TsDir "icon.png") $stage
$playerReadme = Join-Path $TsDir "README.md"
if (-not (Test-Path $playerReadme)) { $playerReadme = Join-Path $ModDir "README.md" }
if (-not (Test-Path $playerReadme)) { throw "Missing player README (thunderstore/README.md or mods/$Mod/README.md)" }
Copy-Item $playerReadme (Join-Path $stage "README.md")
if (Test-Path (Join-Path $TsDir "CHANGELOG.md")) {
    Copy-Item (Join-Path $TsDir "CHANGELOG.md") $stage
}
Copy-Item $dllPath $plugins

$csprojText = Get-Content $Csproj.FullName -Raw
if ($csprojText -match "CopyCommonOnDeploy") {
    $common = Join-Path $ModDir "bin\Release\TeflonTed.Common.dll"
    if (-not (Test-Path $common)) {
        $common = Join-Path $Root "common\TeflonTed.Common\bin\Release\TeflonTed.Common.dll"
    }
    if (-not (Test-Path $common)) { throw "TeflonTed.Common.dll required but not found" }
    Copy-Item $common $plugins
}

$zipPath = Join-Path $Root "dist\$packageName-$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

# Zip contents of stage at archive root (not the stage folder itself).
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host ""
Write-Host "Package ready: $zipPath"
Write-Host "Thunderstore:  TeflonTed-$packageName-$version"
Write-Host "Upload:        https://thunderstore.io/package/create/  (team TeflonTed, community Valheim)"
Write-Host "Validate:      https://thunderstore.io/tools/manifest-v1-validator/"
