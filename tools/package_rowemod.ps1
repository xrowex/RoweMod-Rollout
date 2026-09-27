param([string]$Output = '')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $Output) { $Output = Join-Path $repo 'releases' }
$Output = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$zip = Join-Path $Output 'RoweMod-Windows-x64.zip'
if (Test-Path -LiteralPath $zip) { throw "Already exists: $zip. Choose a new output folder." }
# Use a new directory; never collect the developer's dist, dumps, local items or artwork.
$stage = Join-Path $Output ('build-' + [Guid]::NewGuid().ToString('N'))
$kit = Join-Path $stage 'RoweMod'
New-Item -ItemType Directory -Path $kit | Out-Null
& (Join-Path $PSScriptRoot 'publish_rowemod.ps1') -Output (Join-Path $kit 'dist')

function Copy-KitFile([string]$relative) {
    $source = Join-Path $repo $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing release file: $relative" }
    $dest = Join-Path $kit $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $dest -Parent) | Out-Null
    Copy-Item -LiteralPath $source -Destination $dest
}
foreach ($file in @(
    'RoweMod.cmd', 'RoweMod.Play.cmd', 'README.md', 'items/README.md',
    'tools/bootstrap.ps1', 'tools/extract_tables.ps1', 'tools/Find-RolloutPaks.ps1',
    'tools/find_ue54.ps1', 'tools/pull_clothing.ps1', 'tools/pack_mod.ps1', 'tools/patch_items.ps1',
    'ue/cook_mod.ps1', 'ue/RollerSkate/RollerSkate.uproject', 'art/rig/main-rig.blend'
)) { Copy-KitFile $file }
foreach ($folder in @('art', 'ue/RollerSkate/Content/Python', 'ue/RollerSkate/Config', 'docs')) {
    $extensions = if ($folder -eq 'ue/RollerSkate/Config') { @('.ini') } elseif ($folder -eq 'docs') { @('.md') } else { @('.py') }
    Get-ChildItem -LiteralPath (Join-Path $repo $folder) -File | Where-Object Extension -in $extensions | ForEach-Object {
        Copy-KitFile ($folder + '/' + $_.Name)
    }
}
if (Test-Path -LiteralPath (Join-Path $repo 'art/skate_reference_transforms.json')) {
    Copy-KitFile 'art/skate_reference_transforms.json'
}
Get-ChildItem -LiteralPath $repo -File | Where-Object Name -Like 'LICENSE*' | ForEach-Object { Copy-KitFile $_.Name }
@'
ROWE MOD - WINDOWS
1. Extract this entire folder somewhere writable, such as Documents.
2. Double-click RoweMod.cmd. Keep the dist, tools, art, items and ue folders together.
3. Run Setup once, then Gallery > Subscribe > Play.

No .NET, Git, or developer tools are needed to run RoweMod.
Blender is only for editing shapes. Unreal 5.4.4 is only for cooking your creations.
Start with Gallery for the published shirt and jeans. Local mods and extracted game files are not included.

For updates, extract the new release into a separate folder. Keep the old folder until you
have copied your own art, items, dumps and Unreal project across; do not overwrite them blindly.
'@ | Set-Content -LiteralPath (Join-Path $kit 'START HERE.txt') -Encoding UTF8
Compress-Archive -LiteralPath $kit -DestinationPath $zip -CompressionLevel Optimal
(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash | Set-Content -LiteralPath ($zip + '.sha256')
Write-Host "Release ZIP: $zip"
Write-Output $kit
