[CmdletBinding()]
param(
    [string]$SourceManifestPath = (Join-Path $PSScriptRoot '../../Design/FairyGUI/GDK_FGUI/generated/GDKFairyManifest.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '../../Unity/Assets/Res/UI/FairyGUI'),
    [string]$ManifestPath = (Join-Path $OutputPath 'GDKFairyManifest.json'),
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Get-NormalizedText {
    param([Parameter(Mandatory)][string]$Text)

    $normalized = ($Text -replace "`r`n", "`n") -replace "`r", "`n"
    return $normalized.TrimEnd([char[]]"`n") + "`n"
}

function Convert-ToAssetPath {
    param([Parameter(Mandatory)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $cursor = [System.IO.DirectoryInfo](Split-Path -Parent $fullPath)
    while ($null -ne $cursor -and $cursor.Name -cne 'Assets') {
        $cursor = $cursor.Parent
    }
    if ($null -eq $cursor) {
        throw "FairyGUI runtime asset is not below a Unity Assets directory: $fullPath"
    }

    $relative = [System.IO.Path]::GetRelativePath($cursor.FullName, $fullPath).Replace('\', '/')
    return "Assets/$relative"
}

function Get-AssetKind {
    param([Parameter(Mandatory)][string]$Path)

    switch ([System.IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        '.png' { return 'texture' }
        '.jpg' { return 'texture' }
        '.jpeg' { return 'texture' }
        '.webp' { return 'texture' }
        '.wav' { return 'audio' }
        '.mp3' { return 'audio' }
        '.ogg' { return 'audio' }
        '.ttf' { return 'font' }
        '.otf' { return 'font' }
        '.bytes' { return 'binary' }
        default { return 'asset' }
    }
}

$sourceManifestFull = [System.IO.Path]::GetFullPath($SourceManifestPath)
$outputRoot = [System.IO.Path]::GetFullPath($OutputPath)
$manifestFull = [System.IO.Path]::GetFullPath($ManifestPath)
if (-not (Test-Path -LiteralPath $sourceManifestFull -PathType Leaf)) {
    throw "FairyGUI source manifest is missing: $sourceManifestFull"
}
if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) {
    throw "FairyGUI publish output directory is missing: $outputRoot"
}

$source = Get-Content -Raw -LiteralPath $sourceManifestFull | ConvertFrom-Json
if ($source.schemaVersion -ne 1 -or @($source.packages).Count -eq 0) {
    throw "FairyGUI source manifest must use schema 1 and contain packages: $sourceManifestFull"
}

$packages = @($source.packages | Sort-Object { [string]$_.id } | ForEach-Object {
    $package = $_
    $packageName = [string]$package.name
    if ([string]::IsNullOrWhiteSpace($packageName)) {
        throw 'FairyGUI source manifest contains a package without a name.'
    }

    $descriptorFile = Join-Path $outputRoot ($packageName + '_fui.bytes')
    if (-not (Test-Path -LiteralPath $descriptorFile -PathType Leaf) -or
        (Get-Item -LiteralPath $descriptorFile).Length -le 0) {
        throw "Published FairyGUI package descriptor is missing or empty: $descriptorFile"
    }

    $runtimeAssets = @(Get-ChildItem -LiteralPath $outputRoot -File |
        Where-Object {
            $_.Name.StartsWith($packageName + '_', [System.StringComparison]::Ordinal) -and
            $_.FullName -cne $descriptorFile -and
            $_.Extension -cne '.meta'
        } |
        Sort-Object Name |
        ForEach-Object {
            [pscustomobject][ordered]@{
                path = Convert-ToAssetPath $_.FullName
                kind = Get-AssetKind $_.FullName
                sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
            }
        })

    [pscustomobject][ordered]@{
        id = [string]$package.id
        name = $packageName
        dependencies = @($package.dependencies | ForEach-Object { [string]$_ } | Sort-Object)
        descriptorAsset = Convert-ToAssetPath $descriptorFile
        descriptorSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $descriptorFile).Hash.ToLowerInvariant()
        runtimeAssets = $runtimeAssets
    }
})

$runtimeManifest = [pscustomobject][ordered]@{
    schemaVersion = 2
    project = $source.project
    sourceHash = [string]$source.sourceHash
    contractHash = [string]$source.contractHash
    packages = $packages
}
$manifestBytes = $utf8NoBom.GetBytes((Get-NormalizedText ($runtimeManifest | ConvertTo-Json -Depth 100)))

if ($Check) {
    if (-not (Test-Path -LiteralPath $manifestFull -PathType Leaf)) {
        throw "FairyGUI runtime manifest is missing: $manifestFull"
    }
    $actualBytes = [System.IO.File]::ReadAllBytes($manifestFull)
    if (-not [System.Linq.Enumerable]::SequenceEqual[byte]($actualBytes, $manifestBytes)) {
        throw "FairyGUI runtime manifest is stale: $manifestFull"
    }
}
else {
    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $manifestFull)) | Out-Null
    [System.IO.File]::WriteAllBytes($manifestFull, $manifestBytes)
}

[pscustomobject][ordered]@{
    success = $true
    schemaVersion = 2
    packageCount = $packages.Count
    manifestPath = $manifestFull
    checked = [bool]$Check
} | ConvertTo-Json -Compress
