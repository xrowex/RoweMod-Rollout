param(
    [string]$Project = (Join-Path $PSScriptRoot "RollerSkate\RollerSkate.uproject"),
    [string]$ImportScript = (Join-Path $PSScriptRoot "RollerSkate\Content\Python\import_shirt.py"),
    [switch]$SkipPack
)
$ErrorActionPreference = "Stop"
$tools = Join-Path (Split-Path $PSScriptRoot -Parent) "tools"
$editor = & (Join-Path $tools "find_ue54.ps1")
if (-not $editor) { throw "Unreal Engine 5.4 not found. Finish installing UE 5.4.4, then re-run." }
$cmd = $editor -replace "UnrealEditor.exe", "UnrealEditor-Cmd.exe"
if (-not (Test-Path $cmd)) { $cmd = $editor }
$project = (Resolve-Path $Project).Path
$py = (Resolve-Path $ImportScript).Path

$previewPy = Join-Path $PSScriptRoot "RollerSkate\Content\Python\import_previews.py"
$itemTexPy = Join-Path $PSScriptRoot "RollerSkate\Content\Python\import_item_textures.py"

Write-Host "Editor: $cmd"
Write-Host "Importing shirt via $py..."
& $cmd $project -unattended -nopause -nosplash -NullRHI -log -ExecutePythonScript="$py"
if ($LASTEXITCODE -ne 0) { Write-Warning "Python import exit $LASTEXITCODE" }

if (Test-Path $previewPy) {
    Write-Host "Importing catalog icons via $previewPy..."
    & $cmd $project -unattended -nopause -nosplash -NullRHI -log -ExecutePythonScript="$previewPy"
    if ($LASTEXITCODE -ne 0) { Write-Warning "Preview import exit $LASTEXITCODE" }
}

if (Test-Path $itemTexPy) {
    Write-Host "Importing item textures via $itemTexPy..."
    & $cmd $project -unattended -nopause -nosplash -NullRHI -log -ExecutePythonScript="$itemTexPy"
    if ($LASTEXITCODE -ne 0) { Write-Warning "Item texture import exit $LASTEXITCODE" }
}

Write-Host "Cooking Windows..."
& $cmd $project -run=Cook -TargetPlatform=Windows -Unversioned -unattended -nopause -nosplash -NullRHI -log
if ($LASTEXITCODE -ne 0) { throw "Cook failed ($LASTEXITCODE)" }

if (-not $SkipPack) {
    dotnet run --project (Join-Path $tools "DtPatcher\DtPatcher.csproj") -c Release -- --all-items
    if ($LASTEXITCODE -ne 0) { throw "DtPatcher failed ($LASTEXITCODE)" }
    & (Join-Path $tools "pack_mod.ps1")
} else {
    Write-Host "SkipPack: cook only. Use Pack and Play to write the overlay."
}
