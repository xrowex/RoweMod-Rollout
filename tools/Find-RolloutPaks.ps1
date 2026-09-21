# Resolve Rollout Inline Content\Paks. Prints the path and exits 0, or exits 1.
param()

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
    foreach ($guess in @(
            "${env:ProgramFiles(x86)}\Steam",
            "$env:ProgramFiles\Steam",
            "C:\Program Files (x86)\Steam"
        )) {
        if (Test-Path (Join-Path $guess "steam.exe")) {
            return (Resolve-Path -LiteralPath $guess).Path
        }
    }
    return $null
}

function Get-SteamLibraries {
    $libs = [System.Collections.Generic.List[string]]::new()
    $steam = Get-SteamInstall
    if ($steam) { $libs.Add($steam) }
    if ($steam) {
        $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
        if (Test-Path -LiteralPath $vdf) {
            foreach ($line in Get-Content -LiteralPath $vdf) {
                if ($line -match '"path"\s+"([^"]+)"') {
                    $p = $Matches[1] -replace '\\\\', '\'
                    if (Test-Path -LiteralPath $p) {
                        $libs.Add((Resolve-Path -LiteralPath $p).Path)
                    }
                }
            }
        }
    }
    return $libs | Select-Object -Unique
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
    $rel = "steamapps\common\RolloutInline\RollerSkate\Content\Paks"
    foreach ($lib in Get-SteamLibraries) {
        $paks = Join-Path $lib $rel
        if (Test-Paks $paks) { return (Resolve-Path -LiteralPath $paks).Path }
    }
    $fallback = "C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate\Content\Paks"
    if (Test-Paks $fallback) { return (Resolve-Path -LiteralPath $fallback).Path }
    return $null
}

$found = Find-RolloutPaks
if (-not $found) { exit 1 }
Write-Output $found
exit 0
