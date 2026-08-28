[CmdletBinding()]
param(
    [string]$SourceProjectPath = (Join-Path $PSScriptRoot '../../Design/FairyGUI/GDK_FGUI'),
    [string]$ManifestPath = (Join-Path $PSScriptRoot '../../Design/FairyGUI/GDK_FGUI/generated/GDKFairyManifest.json'),
    [Alias('DtuiformPath', 'LubanPath', 'UIFormDataPath', 'UIFormPath')]
    [string]$LubanUIFormPath = (Join-Path $PSScriptRoot '../../Unity/Assets/Res/Editor/Luban/dtuiform.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '../../Unity/Assets/Res/UI/FairyGUI'),
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Get-NormalizedText {
    param([Parameter(Mandatory)][string]$Text)
    $normalized = ($Text -replace "`r`n", "`n") -replace "`r", "`n"
    return $normalized.TrimEnd([char[]]"`n") + "`n"
}

function Test-JsonProperty {
    param(
        [Parameter(Mandatory)]$InputObject,
        [Parameter(Mandatory)][string]$Name
    )

    return $null -ne $InputObject.PSObject.Properties[$Name]
}

function Get-RequiredString {
    param(
        [Parameter(Mandatory)]$InputObject,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Context
    )

    if (-not (Test-JsonProperty $InputObject $Name)) {
        throw "$Context is missing required property '$Name'."
    }

    $value = $InputObject.PSObject.Properties[$Name].Value
    if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) {
        throw "$Context property '$Name' must be a non-empty string."
    }

    return [string]$value
}

function Get-RequiredBoolean {
    param(
        [Parameter(Mandatory)]$InputObject,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Context
    )

    if (-not (Test-JsonProperty $InputObject $Name)) {
        throw "$Context is missing required property '$Name'."
    }

    $value = $InputObject.PSObject.Properties[$Name].Value
    if ($value -isnot [bool]) {
        throw "$Context property '$Name' must be a boolean."
    }

    return [bool]$value
}

function Get-RequiredPositiveInt32 {
    param(
        [Parameter(Mandatory)]$InputObject,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Context
    )

    if (-not (Test-JsonProperty $InputObject $Name)) {
        throw "$Context is missing required property '$Name'."
    }

    $value = $InputObject.PSObject.Properties[$Name].Value
    if (($value -isnot [int] -and $value -isnot [long]) -or
        [long]$value -le 0 -or [long]$value -gt [int]::MaxValue) {
        throw "$Context property '$Name' must be a positive 32-bit integer."
    }

    return [int]$value
}

function Get-OrdinalSortedStrings {
    param([Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Values)

    $sorted = [string[]]@($Values)
    [System.Array]::Sort($sorted, [System.StringComparer]::Ordinal)
    return $sorted
}
function Get-OptionalString {
    param(
        [Parameter(Mandatory)]$InputObject,
        [Parameter(Mandatory)][string]$Name
    )

    if (-not (Test-JsonProperty $InputObject $Name)) {
        return ''
    }

    $value = $InputObject.PSObject.Properties[$Name].Value
    if ($null -eq $value) {
        return ''
    }

    return [string]$value
}

$sourceRoot = [System.IO.Path]::GetFullPath($SourceProjectPath)
$manifestFull = [System.IO.Path]::GetFullPath($ManifestPath)
$lubanFull = [System.IO.Path]::GetFullPath($LubanUIFormPath)
$outputRoot = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($OutputPath))

$publishPath = Join-Path $sourceRoot 'settings/Publish.json'
foreach ($path in @($publishPath, $manifestFull, $lubanFull)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required input is missing: $path"
    }
}

try {
    $publish = Get-Content -Raw -LiteralPath $publishPath | ConvertFrom-Json
    $manifest = Get-Content -Raw -LiteralPath $manifestFull | ConvertFrom-Json
    $lubanRows = Get-Content -Raw -LiteralPath $lubanFull | ConvertFrom-Json -NoEnumerate
}
catch {
    throw "Descriptor input JSON is invalid: $($_.Exception.Message)"
}

if ($lubanRows -isnot [System.Array]) {
    throw "Luban UI form data must be a JSON array: $lubanFull"
}

$codeNamespace = [string]$publish.codeGeneration.packageName
$classNamePrefix = [string]$publish.codeGeneration.classNamePrefix
if ([string]::IsNullOrWhiteSpace($codeNamespace)) {
    throw "Publish codeGeneration.packageName is required: $publishPath"
}

$packageByName = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
$packageIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($pkg in @($manifest.packages)) {
    $packageName = Get-RequiredString $pkg 'name' 'Manifest package'
    $packageId = Get-RequiredString $pkg 'id' "Manifest package '$packageName'"
    if (-not $packageIds.Add($packageId)) {
        throw "Manifest contains duplicate package id '$packageId'."
    }
    if ($packageByName.ContainsKey($packageName)) {
        throw "Manifest contains duplicate package name '$packageName'."
    }
    $packageByName.Add($packageName, $pkg)
}

$seenUiIds = [System.Collections.Generic.HashSet[int]]::new()
$seenCsNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$seenAssetNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($row in @($lubanRows)) {
    $rowContext = 'Luban UI row'
    $uiId = Get-RequiredPositiveInt32 $row 'Id' $rowContext
    $csName = Get-RequiredString $row 'CSName' "Luban UI row '$uiId'"
    $assetName = Get-RequiredString $row 'AssetName' "Luban UI row '$csName'"
    $null = Get-RequiredString $row 'UIGroupName' "Luban UI row '$csName'"
    $null = Get-RequiredBoolean $row 'AllowMultiInstance' "Luban UI row '$csName'"
    $null = Get-RequiredBoolean $row 'PauseCoveredUIForm' "Luban UI row '$csName'"

    if (-not $seenUiIds.Add($uiId)) {
        throw "Duplicate Luban UI Id '$uiId'."
    }
    if (-not $seenCsNames.Add($csName)) {
        throw "Duplicate Luban UI CSName '$csName'."
    }
    if (-not $seenAssetNames.Add($assetName)) {
        throw "Duplicate Luban UI AssetName '$assetName'."
    }
}

$descriptorFiles = [System.Collections.Generic.List[object]]::new()
$expectedFileNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($row in @($lubanRows)) {
    $uiId = Get-RequiredPositiveInt32 $row 'Id' 'Luban UI row'
    $csName = Get-RequiredString $row 'CSName' "Luban UI row '$uiId'"
    $uiAssetName = Get-RequiredString $row 'AssetName' "Luban UI row '$csName'"
    $uiGroupName = Get-RequiredString $row 'UIGroupName' "Luban UI row '$csName'"
    $allowMultiInstance = Get-RequiredBoolean $row 'AllowMultiInstance' "Luban UI row '$csName'"
    $pauseCoveredUIForm = Get-RequiredBoolean $row 'PauseCoveredUIForm' "Luban UI row '$csName'"

    $packageName = Get-OptionalString $row 'PackageName'
    $componentName = Get-OptionalString $row 'ComponentName'
    if ([string]::IsNullOrWhiteSpace($packageName) -and [string]::IsNullOrWhiteSpace($componentName)) {
        continue
    }
    if ([string]::IsNullOrWhiteSpace($packageName) -or [string]::IsNullOrWhiteSpace($componentName)) {
        throw "Luban UI row '$csName' must provide both PackageName and ComponentName for a FairyGUI form."
    }

    if (-not $packageByName.ContainsKey($packageName)) {
        throw "Luban UI row '$csName' references unknown package '$packageName'."
    }
    $pkg = $packageByName[$packageName]

    $components = @($pkg.resources | Where-Object {
        [string]$_.type -ceq 'component' -and [string]$_.name -ceq ($componentName + '.xml')
    })
    if ($components.Count -ne 1) {
        throw "Luban UI row '$csName' references unknown component '$componentName' in package '$packageName'."
    }
    $component = $components[0]
    $componentId = Get-RequiredString $component 'id' "Manifest component '$packageName/$componentName'"

    $bindingType = "$codeNamespace.$packageName.$classNamePrefix$componentName"

    $dependencies = @(Get-OrdinalSortedStrings ([string[]]@($pkg.dependencies | ForEach-Object { [string]$_ })))
    $fileBaseName = [System.IO.Path]::GetFileName($uiAssetName)
    if ([string]::IsNullOrWhiteSpace($fileBaseName)) {
        throw "Luban UI AssetName '$uiAssetName' for '$csName' has no descriptor basename."
    }
    $fileName = $fileBaseName + '.json'
    if (-not $expectedFileNames.Add($fileName)) {
        throw "Descriptor output basename collision for '$fileName'."
    }

    $descriptor = [ordered]@{
        schemaVersion       = 1
        uiId                = $uiId
        csName              = $csName
        uiAssetName         = $uiAssetName
        uiGroupName         = $uiGroupName
        allowMultiInstance  = $allowMultiInstance
        pauseCoveredUIForm  = $pauseCoveredUIForm
        packageId           = [string]$pkg.id
        packageName         = $packageName
        componentId         = $componentId
        componentName       = $componentName
        bindingType         = $bindingType
        dependencies        = $dependencies
    }

    $json = Get-NormalizedText ($descriptor | ConvertTo-Json -Depth 100)
    $target = [System.IO.Path]::GetFullPath((Join-Path $outputRoot $fileName))
    $outputPrefix = $outputRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $target.StartsWith($outputPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Descriptor path escapes output root '$outputRoot': $target"
    }
    $descriptorFiles.Add([pscustomobject][ordered]@{
        fileName = $fileName
        target = $target
        bytes = $utf8NoBom.GetBytes($json)
        descriptor = [pscustomobject]$descriptor
    })
}

$written = 0
if ($Check -and (Test-Path -LiteralPath $outputRoot -PathType Container)) {
    foreach ($actualFile in @(Get-ChildItem -LiteralPath $outputRoot -Filter '*.json' -File -Recurse)) {
        # 本地化生成器与描述符生成器共用同一输出根;两个 manifest 与四语言 strings 属于
        # 本地化批次,不归描述符检查管辖。
        if ($actualFile.Name -ceq 'GDKFairyManifest.json' -or
            $actualFile.Name -ceq 'GDKFairyLocalizationManifest.json') {
            continue
        }
        if (-not $expectedFileNames.Contains($actualFile.Name) -or
            -not [System.IO.Path]::GetFullPath($actualFile.DirectoryName).Equals(
                $outputRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Obsolete descriptor file exists: $($actualFile.FullName)"
        }
    }
}

foreach ($descriptorFile in $descriptorFiles) {
    if ($Check) {
        if (-not (Test-Path -LiteralPath $descriptorFile.target -PathType Leaf)) {
            throw "Descriptor is missing: $($descriptorFile.target)"
        }
        $actualBytes = [System.IO.File]::ReadAllBytes($descriptorFile.target)
        if (-not [System.Linq.Enumerable]::SequenceEqual[byte]($actualBytes, $descriptorFile.bytes)) {
            throw "Descriptor is stale: $($descriptorFile.target)"
        }
    }
    else {
        [System.IO.Directory]::CreateDirectory((Split-Path -Parent $descriptorFile.target)) | Out-Null
        [System.IO.File]::WriteAllBytes($descriptorFile.target, $descriptorFile.bytes)
        $written++
    }
}

[pscustomobject][ordered]@{
    success     = $true
    count       = $descriptorFiles.Count
    written     = $written
    outputPath  = $outputRoot
    checked     = [bool]$Check
    descriptors = @($descriptorFiles | ForEach-Object { $_.descriptor })
} | ConvertTo-Json -Depth 100
