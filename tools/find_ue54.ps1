# Locate Unreal Editor 5.4. Honors ROWE_UE54, %AppData%\RoweMod\settings.json,
# Epic LauncherInstalled.dat (retargeted installs), registry builds, then common paths.
$ErrorActionPreference = "Stop"

function Test-Ue54Editor([string]$exe) {
    if (-not $exe -or -not (Test-Path -LiteralPath $exe)) { return $false }
    if ($exe -match '(?i)UE_5\.4[\\/]') { return $true }
    $win64 = Split-Path $exe -Parent
    $binaries = Split-Path $win64 -Parent
    $engine = Split-Path $binaries -Parent
    $ver = Join-Path $engine "Build\Build.version"
    if (-not (Test-Path -LiteralPath $ver)) { return $false }
    try {
        $j = Get-Content -LiteralPath $ver -Raw | ConvertFrom-Json
        return ($j.MajorVersion -eq 5 -and $j.MinorVersion -eq 4)
    } catch {
        return $false
    }
}

function Get-SavedUe54 {
    if ($env:ROWE_UE54 -and (Test-Ue54Editor $env:ROWE_UE54)) {
        return (Resolve-Path -LiteralPath $env:ROWE_UE54).Path
    }
    $settings = Join-Path $env:APPDATA "RoweMod\settings.json"
    if (Test-Path -LiteralPath $settings) {
        try {
            $j = Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json
            $saved = $j.UnrealEditor
            if ($saved -and (Test-Ue54Editor $saved)) {
                return (Resolve-Path -LiteralPath $saved).Path
            }
        } catch { }
    }
    return $null
}

function Get-EpicLauncherUe54 {
    $dat = Join-Path $env:ProgramData "Epic\UnrealEngineLauncher\LauncherInstalled.dat"
    if (-not (Test-Path -LiteralPath $dat)) { return $null }
    try {
        $j = Get-Content -LiteralPath $dat -Raw | ConvertFrom-Json
        foreach ($item in @($j.InstallationList)) {
            $name = "$($item.AppName) $($item.ArtifactId)"
            $loc = $item.InstallLocation
            if (-not $loc) { continue }
            if ($name -notmatch '(?i)UE_5\.4|5\.4' -and $loc -notmatch '(?i)UE_5\.4') { continue }
            $exe = Join-Path $loc "Engine\Binaries\Win64\UnrealEditor.exe"
            if (Test-Ue54Editor $exe) {
                return (Resolve-Path -LiteralPath $exe).Path
            }
        }
    } catch { }
    return $null
}

function Get-RegistryUe54 {
    foreach ($key in @(
            "HKCU:\Software\Epic Games\Unreal Engine\Builds",
            "HKCU:\Software\EpicGames\Unreal Engine\Builds"
        )) {
        if (-not (Test-Path $key)) { continue }
        $props = Get-ItemProperty $key -ErrorAction SilentlyContinue
        if (-not $props) { continue }
        foreach ($name in $props.PSObject.Properties.Name) {
            if ($name -like "PS*") { continue }
            $loc = $props.$name
            if (-not $loc -or -not (Test-Path -LiteralPath $loc)) { continue }
            $exe = Join-Path $loc "Engine\Binaries\Win64\UnrealEditor.exe"
            if (Test-Ue54Editor $exe) {
                return (Resolve-Path -LiteralPath $exe).Path
            }
        }
    }
    return $null
}

$saved = Get-SavedUe54
if ($saved) { Write-Output $saved; exit 0 }

$launcher = Get-EpicLauncherUe54
if ($launcher) { Write-Output $launcher; exit 0 }

$reg = Get-RegistryUe54
if ($reg) { Write-Output $reg; exit 0 }

$candidates = @(
    "E:\unreal\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
    "E:\EpicGames\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
    "C:\Program Files\Epic Games\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
    "D:\Epic Games\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
    "C:\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe"
)
foreach ($p in $candidates) {
    if (Test-Ue54Editor $p) {
        Write-Output (Resolve-Path -LiteralPath $p).Path
        exit 0
    }
}

$searchRoots = @(
    "E:\unreal",
    "E:\EpicGames",
    "C:\Program Files\Epic Games",
    "D:\Epic Games",
    "D:\Unreal",
    "C:\Unreal"
)
foreach ($root in $searchRoots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    $found = Get-ChildItem -Path $root -Filter "UnrealEditor.exe" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { Test-Ue54Editor $_.FullName } |
        Select-Object -First 1
    if ($found) { Write-Output $found.FullName; exit 0 }
}
exit 1
