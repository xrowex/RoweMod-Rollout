param(
    [string]$CookedContent = (Join-Path $PSScriptRoot "..\ue\RollerSkate\Saved\Cooked\Windows\RollerSkate\Content"),
    [string]$Staging = (Join-Path $PSScriptRoot "..\dumps\mod-staging"),
    [string]$GamePaks = "C:\Program Files (x86)\Steam\steamapps\common\RolloutInline\RollerSkate\Content\Paks"
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$retoc = Join-Path $PSScriptRoot "retoc\retoc.exe"
if (-not (Test-Path $retoc)) { throw "retoc missing at $retoc" }

Write-Host "Patching all items..."
dotnet run --project (Join-Path $PSScriptRoot "DtPatcher\DtPatcher.csproj") -c Release -- --all-items
if ($LASTEXITCODE -ne 0) { throw "DtPatcher failed" }

$mods = Join-Path $GamePaks "~mods"
New-Item -ItemType Directory -Force -Path $mods | Out-Null
if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
$stageContent = Join-Path $Staging "RollerSkate\Content"
New-Item -ItemType Directory -Force -Path $stageContent | Out-Null

$dtDst = Join-Path $stageContent "MainFolder\UI\customization\data"
New-Item -ItemType Directory -Force -Path $dtDst | Out-Null
Get-ChildItem (Join-Path $repo "dumps\patched") -File -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item $_.FullName $dtDst -Force
    Write-Host "staged $($_.Name)"
}

function Stage-CookedAsset([string]$gamePath) {
    if ([string]::IsNullOrWhiteSpace($gamePath)) { return }
    if (-not $gamePath.StartsWith("/Game/")) { return }
    if (-not (Test-Path $CookedContent)) { return }
    $cookedRoot = (Resolve-Path $CookedContent).Path
    $rel = $gamePath.Substring("/Game/".Length).Replace("/", "\")
    $name = Split-Path $rel -Leaf
    $srcDir = Join-Path $cookedRoot (Split-Path $rel -Parent)
    if (-not (Test-Path $srcDir)) { return }
    $dstDir = Join-Path $stageContent (Split-Path $rel -Parent)
    New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
    Get-ChildItem $srcDir -File | Where-Object {
        $_.BaseName -eq $name -and $_.BaseName -notmatch "_Skeleton$" -and $_.BaseName -notmatch "PhysicsAsset"
    } | ForEach-Object {
        Copy-Item $_.FullName $dstDir -Force
        Write-Host "staged $($_.Name)"
    }
}

$itemsDir = Join-Path $repo "items"
Get-ChildItem $itemsDir -Filter "*.json" -Recurse | ForEach-Object {
    $spec = Get-Content $_.FullName -Raw | ConvertFrom-Json
    foreach ($field in @("previewImage", "albedo", "normal", "roughness", "upperMale", "lowerMale", "eyeAlbedo")) {
        if ($spec.$field) { Stage-CookedAsset ([string]$spec.$field) }
    }
    if ($spec.refs) {
        $spec.refs.PSObject.Properties | ForEach-Object { Stage-CookedAsset ([string]$_.Value) }
    }
    if ($spec.upperMale) {
        $gamePath = [string]$spec.upperMale
        if ($gamePath.StartsWith("/Game/") -and (Test-Path $CookedContent)) {
            $cookedRoot = (Resolve-Path $CookedContent).Path
            $rel = $gamePath.Substring("/Game/".Length).Replace("/", "\")
            $srcDir = Join-Path $cookedRoot (Split-Path $rel -Parent)
            $dstDir = Join-Path $stageContent (Split-Path $rel -Parent)
            if (Test-Path $srcDir) {
                New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
                Get-ChildItem $srcDir -File | Where-Object {
                    ($_.BaseName -eq "M_tshirt-baggy" -or $_.BaseName -like "T_tshirt-baggy-*") -and
                    $_.BaseName -notmatch "_Skeleton$" -and $_.BaseName -notmatch "PhysicsAsset"
                } | ForEach-Object {
                    Copy-Item $_.FullName $dstDir -Force
                    Write-Host "staged $($_.Name)"
                }
            }
        }
    }
}

$outName = "RollerSkate-Windows_P.utoc"
$outUtoc = Join-Path $GamePaks $outName
Write-Host "Packing $Staging -> $outUtoc"
& $retoc to-zen $Staging $outUtoc --version UE5_4
Get-ChildItem $GamePaks -Filter "RollerSkate-Windows_P.*" | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $mods $_.Name) -Force
}

$unrealPak = "E:\unreal\UE_5.4\Engine\Binaries\Win64\UnrealPak.exe"
$stageRoot = (Resolve-Path (Join-Path $Staging "RollerSkate")).Path
$response = Join-Path $Staging "unrealpak-response.txt"
$lines = @()
Get-ChildItem $stageRoot -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($stageRoot.Length).TrimStart("\").Replace("\", "/")
    $src = $_.FullName.Replace("\", "/")
    $lines += "`"$src`" `"RollerSkate/$rel`""
}
Set-Content -Path $response -Value $lines -Encoding ASCII
$legacyPak = Join-Path $GamePaks "ClothingMod_P.pak"
Write-Host "UnrealPak $($lines.Count) files -> $legacyPak"
& $unrealPak $legacyPak "-Create=$response" "-Dest=../../../"
Copy-Item $legacyPak (Join-Path $mods "ClothingMod_P.pak") -Force
Get-ChildItem $GamePaks, $mods -Filter "RollerSkate-Windows_P.*" | Select-Object Name, Length, DirectoryName
