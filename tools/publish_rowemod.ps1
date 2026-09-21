param(
    [string]$Output = ""
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $Output) { $Output = Join-Path $root "dist" }
$proj = Join-Path $PSScriptRoot "RoweMod.App\RoweMod.App.csproj"
$play = Join-Path $PSScriptRoot "RoweMod.Play\RoweMod.Play.csproj"
Write-Host "Publishing RoweMod.exe -> $Output"
dotnet publish $proj -c Release -r win-x64 --self-contained false -o $Output
if ($LASTEXITCODE -ne 0) { throw "publish failed" }
Write-Host "Publishing RoweMod.Play.exe -> $Output"
dotnet publish $play -c Release -r win-x64 --self-contained false -o $Output
if ($LASTEXITCODE -ne 0) { throw "publish Play failed" }
Write-Host "Wrote $(Join-Path $Output 'RoweMod.exe') and RoweMod.Play.exe"
