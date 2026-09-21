param(
    [string]$GamePaks = ""
)
$ErrorActionPreference = "Stop"
$tools = $PSScriptRoot
$repo = (Resolve-Path (Join-Path $tools "..")).Path

Write-Host "RoweMod setup"
Write-Host "repo $repo"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Warning ".NET SDK is not on PATH. RoweMod.exe can still pack in-process. CLI pack_mod.ps1 needs https://dotnet.microsoft.com/download"
} else {
    $ver = (& dotnet --version)
    Write-Host "dotnet $ver"
    if ($ver -notmatch '^8\.') {
        Write-Warning "DtPatcher targets net8.0; found $ver. Continue anyway."
    }
}

if (-not $GamePaks) {
    $GamePaks = & (Join-Path $tools "Find-RolloutPaks.ps1")
}
if (-not $GamePaks -or -not (Test-Path $GamePaks)) {
    throw "Rollout Inline Paks folder not found. Install the game on Steam, then re-run Setup."
}
Write-Host "Paks $GamePaks"
$env:ROWE_GAME_PAKS = $GamePaks

$retocDir = Join-Path $tools "retoc"
$retoc = Join-Path $retocDir "retoc.exe"
if (-not (Test-Path $retoc)) {
    Write-Host "Downloading retoc..."
    $dl = Join-Path $tools "_downloads"
    New-Item -ItemType Directory -Force -Path $dl, $retocDir | Out-Null
    $headers = @{ "User-Agent" = "RoweMod-Rollout" }
    $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/trumank/retoc/releases/latest" -Headers $headers
    $asset = $rel.assets | Where-Object {
        $_.name -match 'windows' -or $_.name -match 'pc-windows' -or $_.name -match 'win64' -or $_.name -like '*.zip'
    } | Select-Object -First 1
    if (-not $asset) { throw "Could not find a Windows retoc release asset." }
    $zip = Join-Path $dl $asset.name
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -Headers $headers
    Write-Host "fetched $($asset.name)"
    Expand-Archive -Path $zip -DestinationPath $dl -Force
    $exe = Get-ChildItem $dl -Recurse -Filter "retoc.exe" | Select-Object -First 1
    if (-not $exe) { throw "retoc.exe missing inside $($asset.name)" }
    Copy-Item $exe.FullName $retoc -Force
}
Write-Host "retoc $retoc"

& (Join-Path $tools "extract_tables.ps1") -GamePaks $GamePaks
if ($LASTEXITCODE -ne 0) { throw "extract_tables failed" }
Write-Host "Setup extract ok"
