# Resolve Rollout Inline Content\Paks. Prints the path and exits 0, or exits 1.
param()

function Get-SteamLibraries {
    $libs = [System.Collections.Generic.List[string]]::new()
    $steam = $null
    foreach ($key in @(
            "HKCU:\Software\Valve\Steam",
            "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam"
        )) {
        if (Test-Path $key) {
            $steam = (Get-ItemProperty $key -ErrorAction SilentlyContinue).SteamPath
            if ($steam) { break }
        }
    }
    if (-not $steam) {
        foreach ($guess in @(
                "${env:ProgramFiles(x86)}\Steam",
                "$env:ProgramFiles\Steam",
                "C:\Program Files (x86)\Steam"
            )) {
            if (Test-Path (Join-Path $guess "steam.exe")) { $steam = $guess; break }
        }
    }
    if ($steam) { $libs.Add((Resolve-Path $steam).Path) }
    $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
    if ($steam -and (Test-Path $vdf)) {
        foreach ($line in Get-Content $vdf) {
            if ($line -match '"path"\s+"([^"]+)"') {
                $p = $Matches[1] -replace '\\\\', '\'
                if (Test-Path $p) { $libs.Add($p) }
            }
        }
    }
    return $libs | Select-Object -Unique
}

function Find-RolloutPaks {
    if ($env:ROWE_GAME_PAKS -and (Test-Path $env:ROWE_GAME_PAKS)) {
        return (Resolve-Path $env:ROWE_GAME_PAKS).Path
    }
    $rel = "steamapps\common\RolloutInline\RollerSkate\Content\Paks"
    foreach ($lib in Get-SteamLibraries) {
        $paks = Join-Path $lib $rel
        if (Test-Path $paks) { return (Resolve-Path $paks).Path }
    }
    $fallback = "C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate\Content\Paks"
    if (Test-Path $fallback) { return (Resolve-Path $fallback).Path }
    return $null
}

$found = Find-RolloutPaks
if (-not $found) { exit 1 }
Write-Output $found
exit 0
