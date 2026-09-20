$candidates = @(
    "E:\unreal\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
    "E:\EpicGames\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
    "C:\Program Files\Epic Games\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
    "D:\Epic Games\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe",
    "C:\UE_5.4\Engine\Binaries\Win64\UnrealEditor.exe"
)
foreach ($p in $candidates) {
    if (Test-Path $p) {
        Write-Output $p
        exit 0
    }
}
$searchRoots = @(
    "E:\unreal",
    "E:\EpicGames",
    "C:\Program Files\Epic Games",
    "D:\Epic Games"
)
foreach ($root in $searchRoots) {
    if (-not (Test-Path $root)) { continue }
    $found = Get-ChildItem -Path $root -Filter "UnrealEditor.exe" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "UE_5\.4" } |
        Select-Object -First 1
    if ($found) { Write-Output $found.FullName; exit 0 }
}
exit 1
