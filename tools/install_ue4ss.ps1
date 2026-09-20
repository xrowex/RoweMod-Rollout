# Copy UE4SS + RolloutClothing Lua into the shipping game (run as Admin if copy fails).
$ErrorActionPreference = "Stop"
$src = Join-Path $PSScriptRoot "UE4SS"
$dst = "C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate\Binaries\Win64"
Copy-Item "$src\dwmapi.dll" "$dst\dwmapi.dll" -Force
Copy-Item "$src\ue4ss" "$dst\ue4ss" -Recurse -Force
Set-Content "$dst\steam_appid.txt" "4464990"
Write-Host "UE4SS installed. Launch the game; RolloutClothing dumps mappings.usmap and adds DT-upper row hoodie-navy-mod."
