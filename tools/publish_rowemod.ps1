param(
    [string]$Output = ""
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $Output) { $Output = Join-Path $root "dist" }
# Bundle the runtime for every process. Helpers have separate dependency folders.
foreach ($app in @(
    @{ Project = 'RoweMod.App'; Folder = '' },
    @{ Project = 'RoweMod.Play'; Folder = '' },
    @{ Project = 'RolloutExtractor'; Folder = 'extractor' },
    @{ Project = 'DtPatcher'; Folder = 'patcher' }
)) {
    $proj = Join-Path $PSScriptRoot ($app.Project + '\' + $app.Project + '.csproj')
    $dest = if ($app.Folder) { Join-Path $Output $app.Folder } else { $Output }
    Write-Host "Publishing $($app.Project) with runtime -> $dest"
    dotnet build $proj -c Release -r win-x64 --self-contained true --no-incremental -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $($app.Project)" }
    dotnet publish $proj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $dest
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($app.Project)" }
}
Write-Host 'Ready to run without installing .NET. Keep all published files together.'
