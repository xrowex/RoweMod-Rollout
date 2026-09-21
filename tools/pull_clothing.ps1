param(
    [string]$GamePaks = "",
    [string]$OutDir = ""
)
$ErrorActionPreference = "Stop"
$tools = $PSScriptRoot
$repo = (Resolve-Path (Join-Path $tools "..")).Path
if (-not $GamePaks) {
    $GamePaks = & (Join-Path $tools "Find-RolloutPaks.ps1")
}
if (-not $GamePaks -or -not (Test-Path $GamePaks)) {
    throw "Rollout Inline Paks folder not found."
}
$game = (Resolve-Path (Join-Path $GamePaks "..\..")).Path
if (-not $OutDir) { $OutDir = Join-Path $repo "dumps\game-clothing" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$log = Join-Path $OutDir "pull.log"

Write-Host "Pull clothing from $game"
Write-Host "Local only -> $OutDir  (gitignored, do not upload)"

$proj = Join-Path $tools "RolloutExtractor\RolloutExtractor.csproj"
$exe = Join-Path $tools "RolloutExtractor\bin\Release\net8.0\RolloutExtractor.exe"
if (-not (Test-Path $exe)) {
    dotnet build $proj -c Release
    if ($LASTEXITCODE -ne 0) { throw "RolloutExtractor build failed" }
}

$usmap = Join-Path $repo "dumps\mappings.usmap"
$invoke = @("--game", $game, "--out", $OutDir)
if (Test-Path $usmap) { $invoke += @("--usmap", $usmap) }

Write-Host "EXE $exe"
Write-Host "INVOKE $($invoke -join ' ')"
& $exe @invoke 2>&1 | Tee-Object -FilePath $log
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Warning "Pull exit $code. If meshes failed, drop dumps/mappings.usmap from UE4SS DumpUSMAP and re-run."
    exit $code
}
Write-Host "Pull clothing done"
exit 0
