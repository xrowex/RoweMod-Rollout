# Resolve Rollout Inline Content\Paks via Steam libraries, appmanifest, and common drives.
param()

$AppId = "4464990"

function Test-Paks([string]$folder) {
    if (-not $folder -or -not (Test-Path -LiteralPath $folder)) { return $false }
    return @(Get-ChildItem -LiteralPath $folder -Filter "*.utoc" -ErrorAction SilentlyContinue).Count -gt 0 `
        -or @(Get-ChildItem -LiteralPath $folder -Filter "*.pak" -ErrorAction SilentlyContinue).Count -gt 0
}

function Get-SteamInstall {
    foreach ($pair in @(
            @{ Key = "HKCU:\Software\Valve\Steam"; Names = @("SteamPath", "InstallPath") },
            @{ Key = "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam"; Names = @("InstallPath", "SteamPath") },
            @{ Key = "HKLM:\SOFTWARE\Valve\Steam"; Names = @("InstallPath", "SteamPath") }
        )) {
        if (-not (Test-Path $pair.Key)) { continue }
        $props = Get-ItemProperty $pair.Key -ErrorAction SilentlyContinue
        if (-not $props) { continue }
        foreach ($n in $pair.Names) {
            $steam = $props.$n
            if ($steam) {
                $steam = $steam -replace '/', '\'
                if (Test-Path -LiteralPath $steam) {
                    return (Resolve-Path -LiteralPath $steam).Path
                }
            }
        }
    }
    try {
        $proc = Get-Process -Name steam -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($proc -and $proc.Path) {
            $dir = Split-Path $proc.Path -Parent
            if (Test-Path -LiteralPath $dir) { return (Resolve-Path -LiteralPath $dir).Path }
        }
    } catch { }
    foreach ($guess in @(
            "${env:ProgramFiles(x86)}\Steam",
            "$env:ProgramFiles\Steam",
            "C:\Program Files (x86)\Steam",
            "C:\Steam",
            "D:\Steam",
            "D:\SteamLibrary",
            "E:\Steam",
            "E:\SteamLibrary"
        )) {
        if (Test-Path (Join-Path $guess "steam.exe")) {
            return (Resolve-Path -LiteralPath $guess).Path
        }
    }
    return $null
}

function Get-SteamLibraries {
    $libs = [System.Collections.Generic.List[string]]::new()
    $add = {
        param($p)
        if (-not $p) { return }
        if (-not (Test-Path -LiteralPath $p)) { return }
        $full = (Resolve-Path -LiteralPath $p).Path
        if (-not $libs.Contains($full)) { $libs.Add($full) }
    }

    & $add (Get-SteamInstall)

    Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue | ForEach-Object {
        foreach ($name in @("SteamLibrary", "Steam", "Program Files (x86)\Steam", "Program Files\Steam", "Games\Steam", "Games\SteamLibrary")) {
            & $add (Join-Path $_.Root $name)
        }
    }

    foreach ($root in @($libs.ToArray())) {
        $vdf = Join-Path $root "steamapps\libraryfolders.vdf"
        if (-not (Test-Path -LiteralPath $vdf)) { continue }
        foreach ($line in Get-Content -LiteralPath $vdf) {
            if ($line -match '"path"\s+"([^"]+)"') {
                $p = $Matches[1] -replace '\\\\', '\'
                & $add $p
            }
        }
    }
    return $libs
}

function Get-PaksFromManifest([string]$libraryRoot) {
    $manifest = Join-Path $libraryRoot "steamapps\appmanifest_$AppId.acf"
    if (-not (Test-Path -LiteralPath $manifest)) { return $null }
    $installdir = $null
    foreach ($line in Get-Content -LiteralPath $manifest) {
        if ($line -match '"installdir"\s+"([^"]+)"') {
            $installdir = $Matches[1]
            break
        }
    }
    if (-not $installdir) { return $null }
    $common = Join-Path $libraryRoot "steamapps\common\$installdir"
    foreach ($candidate in @(
            (Join-Path $common "RollerSkate\Content\Paks"),
            (Join-Path $common "Content\Paks"),
            (Join-Path $common "Paks")
        )) {
        if (Test-Paks $candidate) { return (Resolve-Path -LiteralPath $candidate).Path }
    }
    try {
        $hit = Get-ChildItem -LiteralPath $common -Directory -Filter "Paks" -Recurse -ErrorAction SilentlyContinue |
            Where-Object { Test-Paks $_.FullName } |
            Select-Object -First 1
        if ($hit) { return $hit.FullName }
    } catch { }
    return $null
}

function Find-RolloutPaks {
    if ($env:ROWE_GAME_PAKS -and (Test-Paks $env:ROWE_GAME_PAKS)) {
        return (Resolve-Path -LiteralPath $env:ROWE_GAME_PAKS).Path
    }
    $settings = Join-Path $env:APPDATA "RoweMod\settings.json"
    if (Test-Path -LiteralPath $settings) {
        try {
            $j = Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json
            if ($j.GamePaks -and (Test-Paks $j.GamePaks)) {
                return (Resolve-Path -LiteralPath $j.GamePaks).Path
            }
        } catch { }
    }

    $folderNames = @("RolloutInline", "Rollout Inline", "RolloutInlineDemo", "RollerSkate")
    foreach ($lib in Get-SteamLibraries) {
        $fromManifest = Get-PaksFromManifest $lib
        if ($fromManifest) { return $fromManifest }
        foreach ($folder in $folderNames) {
            $paks = Join-Path $lib "steamapps\common\$folder\RollerSkate\Content\Paks"
            if (Test-Paks $paks) { return (Resolve-Path -LiteralPath $paks).Path }
        }
    }

    $fallback = "C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate\Content\Paks"
    if (Test-Paks $fallback) { return (Resolve-Path -LiteralPath $fallback).Path }
    return $null
}

$found = Find-RolloutPaks
if (-not $found) { exit 1 }
Write-Output $found
exit 0
