param(
    [switch]$Setup
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $PSScriptRoot "RollerSkate\RollerSkate.uproject"
$editor = & (Join-Path $root "tools\find_ue54.ps1")
if (-not $editor) { throw "Unreal Engine 5.4 not found." }

if ($Setup) {
    $cmd = $editor -replace "UnrealEditor.exe", "UnrealEditor-Cmd.exe"
    if (-not (Test-Path $cmd)) { $cmd = $editor }
    $py = Join-Path $PSScriptRoot "RollerSkate\Content\Python\setup_preview.py"
    Write-Host "Setting up preview map..."
    & $cmd (Resolve-Path $project).Path -unattended -nopause -nosplash -NullRHI -log -ExecutePythonScript="$py"
}

Write-Host "Opening editor: $editor"
Start-Process -FilePath $editor -ArgumentList @(
    (Resolve-Path $project).Path,
    "/Game/ModPreview/Preview"
)
