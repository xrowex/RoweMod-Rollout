param(
    [Parameter(Mandatory=$true)][string]$Zip,
    [Parameter(Mandatory=$true)][string]$Output
)
$ErrorActionPreference = 'Stop'
$Zip = (Resolve-Path -LiteralPath $Zip).Path
$Output = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $Output) { throw 'Use a new test folder so existing work is preserved.' }
New-Item -ItemType Directory -Path $Output | Out-Null
Expand-Archive -LiteralPath $Zip -DestinationPath (Join-Path $Output 'clean room')
$kit = Join-Path $Output 'clean room\RoweMod'
$report = Join-Path $Output 'report'
New-Item -ItemType Directory -Path $report | Out-Null

function Run-Isolated([string]$exe, [string]$arguments, [string]$name, [int]$expected = 0) {
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $exe
    $start.Arguments = $arguments
    $start.WorkingDirectory = $kit
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.EnvironmentVariables['PATH'] = "$env:SystemRoot\System32;$env:SystemRoot;$env:SystemRoot\System32\WindowsPowerShell\v1.0"
    $start.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $Output 'no-dotnet'
    $start.EnvironmentVariables['DOTNET_ROOT_X64'] = Join-Path $Output 'no-dotnet'
    $start.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $start.EnvironmentVariables['COREHOST_TRACE'] = '1'
    $start.EnvironmentVariables['COREHOST_TRACEFILE'] = Join-Path $report ($name + '-host.log')
    $process = [Diagnostics.Process]::Start($start)
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw "$name timed out" }
    if ($process.ExitCode -ne $expected) { throw "$name exit $($process.ExitCode); see $report" }
    Write-Host "PASS $name without dotnet on PATH"
}
Run-Isolated (Join-Path $kit 'dist\RoweMod.exe') ('--check-portable "' + $report + '"') 'app'
Run-Isolated (Join-Path $kit 'dist\patcher\DtPatcher.exe') '--all-items' 'patcher'
Run-Isolated (Join-Path $kit 'dist\extractor\RolloutExtractor.exe') '--game missing-test-game' 'extractor' 2
foreach ($relative in @('dist\RoweMod.runtimeconfig.json', 'dist\RoweMod.Play.runtimeconfig.json',
    'dist\patcher\DtPatcher.runtimeconfig.json', 'dist\extractor\RolloutExtractor.runtimeconfig.json')) {
    $config = Get-Content -LiteralPath (Join-Path $kit $relative) -Raw | ConvertFrom-Json
    if ($config.runtimeOptions.framework -or $config.runtimeOptions.frameworks -or -not $config.runtimeOptions.includedFrameworks) {
        throw "Runtime is not bundled: $relative"
    }
}
Write-Host "PASS all four executables have bundled runtime configurations. Reports: $report"
