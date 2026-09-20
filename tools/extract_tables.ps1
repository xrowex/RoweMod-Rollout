param(
    [string]$GamePaks = "C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate\Content\Paks",
    [string]$OutDir = (Join-Path $PSScriptRoot "..\dumps\legacy")
)
$ErrorActionPreference = "Stop"
$retoc = Join-Path $PSScriptRoot "retoc\retoc.exe"
if (-not (Test-Path $retoc)) { throw "retoc missing at $retoc. See tools/README.md." }
if (-not (Test-Path $GamePaks)) { throw "game paks missing: $GamePaks" }

if (-not (Test-Path $GamePaks)) { throw "game paks missing: $GamePaks" }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Write-Host "Extracting customization DataTables from $GamePaks -> $OutDir"
# Directory input is required so retoc can resolve ScriptObjects. Skip *_P overlays
# by filtering to stock DT-* names; the live overlay only patches tables we write later.
$filters = @(
    "DT-upper", "DT-lower", "DT-hats", "DT-glasses", "DT-hair", "DT-beard",
    "DT-bodytypes", "DT-skin", "DT-eyes", "DT-boot", "DT-frames", "DT-wheels"
)
foreach ($name in $filters) {
    & $retoc to-legacy $GamePaks $OutDir --filter $name --version UE5_4
    if ($LASTEXITCODE -ne 0) { Write-Warning "retoc $name exit $LASTEXITCODE" }
}
Get-ChildItem $OutDir -Recurse -Filter "DT-*.uasset" | Select-Object -ExpandProperty FullName
