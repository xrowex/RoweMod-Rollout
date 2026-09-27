param()
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $repo 'dist\patcher\DtPatcher.exe'
if (Test-Path $exe) {
    & $exe --all-items
} else {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The patcher is missing. Extract the complete RoweMod-Windows-x64.zip and try again.'
    }
    dotnet run --project (Join-Path $PSScriptRoot 'DtPatcher\DtPatcher.csproj') -c Release -- --all-items
}
if ($LASTEXITCODE -ne 0) { throw "Item patch failed ($LASTEXITCODE)" }
