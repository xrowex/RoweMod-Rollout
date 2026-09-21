param(
    [string]$CookedContent = (Join-Path $PSScriptRoot "..\ue\RollerSkate\Saved\Cooked\Windows\RollerSkate\Content"),
    [string]$Staging = (Join-Path $PSScriptRoot "..\dumps\mod-staging"),
    [string]$GamePaks = "",
    [switch]$SkipPatch
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$retoc = Join-Path $PSScriptRoot "retoc\retoc.exe"
if (-not (Test-Path $retoc)) { throw "retoc missing at $retoc. Run Setup / tools/bootstrap.ps1 first." }

if (-not $GamePaks) {
    $GamePaks = & (Join-Path $PSScriptRoot "Find-RolloutPaks.ps1")
}
if (-not $GamePaks -or -not (Test-Path $GamePaks)) {
    throw "Rollout Inline Paks folder not found."
}

Get-Process RollerSkate -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

if (-not $SkipPatch) {
    Write-Host "Patching all items..."
    dotnet run --project (Join-Path $PSScriptRoot "DtPatcher\DtPatcher.csproj") -c Release -- --all-items
    if ($LASTEXITCODE -ne 0) { throw "DtPatcher failed" }
}

$mods = Join-Path $GamePaks "~mods"
New-Item -ItemType Directory -Force -Path $mods | Out-Null
if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
$stageContent = Join-Path $Staging "RollerSkate\Content"
New-Item -ItemType Directory -Force -Path $stageContent | Out-Null

$dtDst = Join-Path $stageContent "MainFolder\UI\customization\data"
New-Item -ItemType Directory -Force -Path $dtDst | Out-Null
$patched = Join-Path $repo "dumps\patched"
if (-not (Test-Path $patched)) { throw "missing $patched. Run DtPatcher / Setup first." }
Get-ChildItem $patched -File | ForEach-Object {
    Copy-Item $_.FullName $dtDst -Force
    Write-Host "staged $($_.Name)"
}

$missingCooked = [System.Collections.Generic.List[string]]::new()

function Stage-CookedAsset([string]$gamePath) {
    if ([string]::IsNullOrWhiteSpace($gamePath)) { return $true }
    if (-not $gamePath.StartsWith("/Game/")) { return $true }
    if (-not (Test-Path $CookedContent)) {
        $script:missingCooked.Add($gamePath)
        return $false
    }
    $cookedRoot = (Resolve-Path $CookedContent).Path
    $rel = $gamePath.Substring("/Game/".Length).Replace("/", "\")
    $name = Split-Path $rel -Leaf
    $srcDir = Join-Path $cookedRoot (Split-Path $rel -Parent)
    if (-not (Test-Path $srcDir)) {
        $script:missingCooked.Add($gamePath)
        return $false
    }
    $dstDir = Join-Path $stageContent (Split-Path $rel -Parent)
    New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
    $copied = $false
    Get-ChildItem $srcDir -File | Where-Object {
        $_.BaseName -eq $name -and $_.BaseName -notmatch "_Skeleton$" -and $_.BaseName -notmatch "PhysicsAsset"
    } | ForEach-Object {
        Copy-Item $_.FullName $dstDir -Force
        Write-Host "staged $($_.Name)"
        $copied = $true
    }
    if (-not $copied) { $script:missingCooked.Add($gamePath) }
    return $copied
}

$itemsDir = Join-Path $repo "items"
Get-ChildItem $itemsDir -Filter "*.json" -Recurse | ForEach-Object {
    $spec = Get-Content $_.FullName -Raw | ConvertFrom-Json
    if ($spec.sample -eq $true) { return }
    foreach ($field in @("previewImage", "albedo", "normal", "roughness", "upperMale", "lowerMale", "eyeAlbedo")) {
        if ($spec.$field) { [void](Stage-CookedAsset ([string]$spec.$field)) }
    }
    if ($spec.refs) {
        $spec.refs.PSObject.Properties | ForEach-Object { [void](Stage-CookedAsset ([string]$_.Value)) }
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

if ($missingCooked.Count -gt 0) {
    Write-Warning "Track B cooked assets missing (run Cook, then Pack and Play):"
    $missingCooked | Select-Object -Unique | ForEach-Object { Write-Warning "  $_" }
}

$outName = "RollerSkate-Windows_P.utoc"
$outUtoc = Join-Path $GamePaks $outName
Write-Host "Packing $Staging -> $outUtoc"
& $retoc to-zen $Staging $outUtoc --version UE5_4
if ($LASTEXITCODE -ne 0) { throw "retoc to-zen failed" }
Get-ChildItem $GamePaks -Filter "RollerSkate-Windows_P.*" | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $mods $_.Name) -Force
}

$editor = & (Join-Path $PSScriptRoot "find_ue54.ps1")
$unrealPak = $null
if ($editor) {
    $unrealPak = Join-Path (Split-Path $editor) "UnrealPak.exe"
}
if ($unrealPak -and (Test-Path $unrealPak)) {
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
} else {
    Write-Warning "UnrealPak not found; skipped ClothingMod_P.pak. IoStore overlay is enough."
}

Write-Host "OVERLAY $outUtoc"
Get-ChildItem $GamePaks, $mods -Filter "RollerSkate-Windows_P.*" | Select-Object Name, Length, DirectoryName
