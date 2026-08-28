[CmdletBinding()]
param(
    [string]$SourceManifestPath = (Join-Path $PSScriptRoot '../../Design/FairyGUI/GDK_FGUI/generated/GDKFairyManifest.json'),
    [string]$SourceProjectPath = (Join-Path $PSScriptRoot '../../Design/FairyGUI/GDK_FGUI'),
    [string]$MappingPath = (Join-Path $PSScriptRoot '../../Design/FairyGUI/GDK_FGUI/settings/FairyLocalization.json'),
    [string]$LocalizationDictionaryPath = (Join-Path $PSScriptRoot '../../Unity/Assets/Res/Localization'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '../../Unity/Assets/Res/UI/FairyGUI'),
    [string]$ManifestPath = (Join-Path $OutputPath 'GDKFairyLocalizationManifest.json'),
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
        throw "FairyGUI localization asset is not below a Unity Assets directory: $fullPath"
    }

    $relative = [System.IO.Path]::GetRelativePath($cursor.FullName, $fullPath).Replace('\', '/')
    return "Assets/$relative"
}

$sourceManifestFull = [System.IO.Path]::GetFullPath($SourceManifestPath)
$sourceProjectFull = [System.IO.Path]::GetFullPath($SourceProjectPath)
$mappingFull = [System.IO.Path]::GetFullPath($MappingPath)
$dictionaryRootFull = [System.IO.Path]::GetFullPath($LocalizationDictionaryPath)
$outputRoot = [System.IO.Path]::GetFullPath($OutputPath)
$manifestFull = [System.IO.Path]::GetFullPath($ManifestPath)
if (-not (Test-Path -LiteralPath $sourceManifestFull -PathType Leaf)) {
    throw "FairyGUI source manifest is missing: $sourceManifestFull"
}
if (-not (Test-Path -LiteralPath $mappingFull -PathType Leaf)) {
    throw "FairyGUI localization mapping is missing: $mappingFull"
}
if (-not (Test-Path -LiteralPath $dictionaryRootFull -PathType Container)) {
    throw "Localization dictionary directory is missing: $dictionaryRootFull"
}
if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) {
    throw "FairyGUI publish output directory is missing: $outputRoot"
}

$source = Get-Content -Raw -LiteralPath $sourceManifestFull | ConvertFrom-Json
if ($source.schemaVersion -ne 1 -or @($source.packages).Count -eq 0) {
    throw "FairyGUI source manifest must use schema 1 and contain packages: $sourceManifestFull"
}

# 映射事实来源(settings/FairyLocalization.json):{package, componentId, elementId, key}。
# 不把映射放进组件 XML(编辑器回写会剥离未知属性),由 GDK 侧设置文件持有。
$mapping = Get-Content -Raw -LiteralPath $mappingFull | ConvertFrom-Json
if ($mapping.schemaVersion -ne 1 -or @($mapping.entries).Count -eq 0) {
    throw "FairyGUI localization mapping must use schema 1 and contain entries: $mappingFull"
}

# 包名 -> 包内组件 id 集合(供映射校验,防止 componentId 打错)。
$componentIds = @{}
foreach ($package in @($source.packages)) {
    $packageName = [string]$package.name
    $ids = @{}
    foreach ($resource in @($package.resources)) {
        if ([string]$resource.type -eq 'component' -and -not [string]::IsNullOrWhiteSpace([string]$resource.id)) {
            $ids[[string]$resource.id] = $true
        }
    }
    $componentIds[$packageName] = $ids
}

# 按包分组映射条目并校验引用。
$entriesByPackage = @{}
foreach ($entry in @($mapping.entries)) {
    $packageName = [string]$entry.package
    $componentId = [string]$entry.componentId
    $elementId = [string]$entry.elementId
    $key = [string]$entry.key
    if ([string]::IsNullOrWhiteSpace($packageName) -or [string]::IsNullOrWhiteSpace($componentId) -or
        [string]::IsNullOrWhiteSpace($elementId) -or [string]::IsNullOrWhiteSpace($key)) {
        throw "FairyGUI localization mapping contains an entry with empty fields."
    }
    if (-not $componentIds.ContainsKey($packageName)) {
        throw "FairyGUI localization mapping references unknown package: $packageName"
    }
    if (-not $componentIds[$packageName].ContainsKey($componentId)) {
        throw "FairyGUI localization mapping references unknown component '$componentId' in package '$packageName'."
    }
    if (-not $entriesByPackage.ContainsKey($packageName)) {
        $entriesByPackage[$packageName] = @()
    }
    $entriesByPackage[$packageName] += $entry
}

if ($entriesByPackage.Count -eq 0) {
    throw 'FairyGUI localization mapping has no valid entries.'
}

# Luban 多语言字典二进制(与 LubanLocalizationHelper/LubanLib ByteBuf 格式一致:
# WriteString = WriteSize(len) + utf8,WriteSize/WriteUint 为变长编码:
# len < 0x80 单字节;len < 0x4000 两字节(首字节 |0x80);其余多字节)。
function Read-LubanDictionary {
    param([Parameter(Mandatory)][string]$LanguageName)

    $bytesFile = Join-Path $dictionaryRootFull ($LanguageName + '/Localization.bytes')
    if (-not (Test-Path -LiteralPath $bytesFile -PathType Leaf)) {
        throw "Localization dictionary is missing: $bytesFile"
    }

    $bytes = [System.IO.File]::ReadAllBytes($bytesFile)
    $script:position = 0

    function Read-VarUint {
        if ($script:position -ge $bytes.Length) {
            throw "Corrupted localization dictionary (varint) in: $bytesFile"
        }
        $b0 = $bytes[$script:position]
        $script:position++
        if ($b0 -lt 0x80) {
            return [int]$b0
        }
        if ($script:position -ge $bytes.Length) {
            throw "Corrupted localization dictionary (varint) in: $bytesFile"
        }
        $b1 = $bytes[$script:position]
        $script:position++
        if (($b0 -band 0xC0) -eq 0x80) {
            return [int]((($b0 -band 0x3F) -shl 8) -bor $b1)
        }
        if ($script:position -ge $bytes.Length) {
            throw "Corrupted localization dictionary (varint) in: $bytesFile"
        }
        $b2 = $bytes[$script:position]
        $script:position++
        if (($b0 -band 0xE0) -eq 0xC0) {
            return [int]((($b0 -band 0x1F) -shl 16) -bor ($b1 -shl 8) -bor $b2)
        }
        if ($script:position -ge $bytes.Length) {
            throw "Corrupted localization dictionary (varint) in: $bytesFile"
        }
        $b3 = $bytes[$script:position]
        $script:position++
        return [int]((($b0 -band 0x0F) -shl 24) -bor ($b1 -shl 16) -bor ($b2 -shl 8) -bor $b3)
    }

    $entries = @{}
    while ($script:position -lt $bytes.Length) {
        $keyLen = Read-VarUint
        if ($script:position + $keyLen -gt $bytes.Length) {
            throw "Corrupted localization dictionary (key) in: $bytesFile"
        }
        $key = [System.Text.Encoding]::UTF8.GetString($bytes, $script:position, $keyLen)
        $script:position += $keyLen

        $valueLen = Read-VarUint
        if ($script:position + $valueLen -gt $bytes.Length) {
            throw "Corrupted localization dictionary (value) in: $bytesFile"
        }
        $value = [System.Text.Encoding]::UTF8.GetString($bytes, $script:position, $valueLen)
        $script:position += $valueLen

        $entries[$key] = $value
    }

    return $entries
}

# 语言集合来自字典目录;无 ChineseSimplified 目录时直接失败(回退语言必须存在)。
$languages = @(Get-ChildItem -LiteralPath $dictionaryRootFull -Directory |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'Localization.bytes') -PathType Leaf } |
    Sort-Object Name |
    ForEach-Object { [string]$_.Name })
if ($languages.Count -eq 0) {
    throw "No per-language localization dictionaries found under: $dictionaryRootFull"
}
if (-not ($languages -contains 'ChineseSimplified')) {
    throw "ChineseSimplified fallback dictionary is missing under: $dictionaryRootFull"
}

$fallbackDict = Read-LubanDictionary 'ChineseSimplified'
$localizationEntries = @()
foreach ($packageName in @($entriesByPackage.Keys | Sort-Object)) {
    $packageEntries = $entriesByPackage[$packageName]
    foreach ($language in $languages) {
        $dict = if ($language -eq 'ChineseSimplified') { $fallbackDict } else { Read-LubanDictionary $language }
        $strings = [System.Text.StringBuilder]::new()
        [void]$strings.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
        [void]$strings.AppendLine('<stringtable>')

        foreach ($entry in $packageEntries) {
            $key = [string]$entry.key
            $value = $dict[$key]
            if ($null -eq $value) {
                if ($language -ne 'ChineseSimplified') {
                    $value = $fallbackDict[$key]
                    if ($null -ne $value) {
                        Write-Warning "Localization key '$key' missing in '$language', falling back to ChineseSimplified."
                    }
                }
                if ($null -eq $value) {
                    throw "Localization key '$key' is missing in '$language' and ChineseSimplified."
                }
            }

            $escaped = $value -replace '&', '&amp;' -replace '<', '&lt;' -replace '>', '&gt;'
            # FairyGUI 字符串表 key:componentId-elementId(与 TranslationHelper.LoadFromXML 一致)。
            [void]$strings.AppendLine(('  <string name="{0}-{1}">{2}</string>' -f [string]$entry.componentId, [string]$entry.elementId, $escaped))
        }

        [void]$strings.AppendLine('</stringtable>')
        $bytes = $utf8NoBom.GetBytes((Get-NormalizedText $strings.ToString()))
        $outputFile = Join-Path $outputRoot ($packageName + '_strings_' + $language + '.xml')
        if ($Check) {
            if (-not (Test-Path -LiteralPath $outputFile -PathType Leaf)) {
                throw "FairyGUI localization XML is missing: $outputFile"
            }
            $actual = [System.IO.File]::ReadAllBytes($outputFile)
            if (-not [System.Linq.Enumerable]::SequenceEqual[byte]($actual, $bytes)) {
                throw "FairyGUI localization XML is stale: $outputFile"
            }
        }
        else {
            [System.IO.File]::WriteAllBytes($outputFile, $bytes)
        }

        $localizationEntries += [pscustomobject][ordered]@{
            package = $packageName
            language = $language
            asset = Convert-ToAssetPath $outputFile
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $outputFile).Hash.ToLowerInvariant()
            keyCount = $packageEntries.Count
        }
    }
}

$localizationManifest = [pscustomobject][ordered]@{
    schemaVersion = 1
    packages = @($entriesByPackage.Keys | Sort-Object)
    languages = $languages
    entries = $localizationEntries
    sourceHash = [string]$source.sourceHash
    contractHash = [string]$source.contractHash
    mappingPath = $MappingPath
}
$manifestBytes = $utf8NoBom.GetBytes((Get-NormalizedText ($localizationManifest | ConvertTo-Json -Depth 100)))

if ($Check) {
    if (-not (Test-Path -LiteralPath $manifestFull -PathType Leaf)) {
        throw "FairyGUI localization manifest is missing: $manifestFull"
    }
    $actual = [System.IO.File]::ReadAllBytes($manifestFull)
    if (-not [System.Linq.Enumerable]::SequenceEqual[byte]($actual, $manifestBytes)) {
        throw "FairyGUI localization manifest is stale: $manifestFull"
    }
}
else {
    [System.IO.File]::WriteAllBytes($manifestFull, $manifestBytes)
}

[pscustomobject][ordered]@{
    success = $true
    schemaVersion = 1
    languageCount = $languages.Count
    packageCount = $entriesByPackage.Count
    manifestPath = $manifestFull
    checked = [bool]$Check
} | ConvertTo-Json -Compress
