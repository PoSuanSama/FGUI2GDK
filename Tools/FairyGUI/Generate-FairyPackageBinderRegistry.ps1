[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '../../Design/FairyGUI/GDK_FGUI'),
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$ManifestPath,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Assert-CSharpIdentifier {
    param(
        [Parameter(Mandatory)][string]$Value,
        [Parameter(Mandatory)][string]$Label
    )

    if ($Value -notmatch '^[A-Za-z_][A-Za-z0-9_]*$' -or $script:csharpKeywords.Contains($Value)) {
        throw "$Label '$Value' is not a supported C# identifier."
    }
}

function Assert-CSharpNamespace {
    param([Parameter(Mandatory)][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw 'FairyGUI Publish.json codeGeneration.packageName cannot be empty.'
    }

    foreach ($segment in $Value.Split('.')) {
        Assert-CSharpIdentifier $segment 'FairyGUI namespace segment'
    }
}

function Get-NormalizedText {
    param([Parameter(Mandatory)][string]$Text)

    $normalized = ($Text -replace "`r`n", "`n") -replace "`r", "`n"
    return $normalized.TrimEnd([char[]]"`n") + "`n"
}

$script:csharpKeywords = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
@(
    'abstract', 'as', 'base', 'bool', 'break', 'byte', 'case', 'catch', 'char', 'checked', 'class',
    'const', 'continue', 'decimal', 'default', 'delegate', 'do', 'double', 'else', 'enum', 'event',
    'explicit', 'extern', 'false', 'finally', 'fixed', 'float', 'for', 'foreach', 'goto', 'if',
    'implicit', 'in', 'int', 'interface', 'internal', 'is', 'lock', 'long', 'namespace', 'new',
    'null', 'object', 'operator', 'out', 'override', 'params', 'private', 'protected', 'public',
    'readonly', 'ref', 'return', 'sbyte', 'sealed', 'short', 'sizeof', 'stackalloc', 'static',
    'string', 'struct', 'switch', 'this', 'throw', 'true', 'try', 'typeof', 'uint', 'ulong',
    'unchecked', 'unsafe', 'ushort', 'using', 'virtual', 'void', 'volatile', 'while'
) | ForEach-Object { $null = $script:csharpKeywords.Add($_.Trim()) }

$projectRoot = [System.IO.Path]::GetFullPath($ProjectPath)
$repositoryRoot = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($RepositoryRoot))
if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) {
    throw "FairyGUI project directory is missing: $projectRoot"
}

$publishPath = Join-Path $projectRoot 'settings/Publish.json'
if (-not (Test-Path -LiteralPath $publishPath -PathType Leaf)) {
    throw "FairyGUI Publish.json is missing: $publishPath"
}
try {
    $publishSettings = [System.IO.File]::ReadAllText($publishPath) | ConvertFrom-Json
}
catch {
    throw "FairyGUI Publish.json is invalid: $($_.Exception.Message)"
}

$codeGeneration = $publishSettings.codeGeneration
if ($null -eq $codeGeneration) {
    throw "FairyGUI Publish.json has no codeGeneration section: $publishPath"
}
if ($codeGeneration.allowGenCode -ne $true) {
    throw "FairyGUI Publish.json must enable codeGeneration.allowGenCode: $publishPath"
}

$codePath = [string]$codeGeneration.codePath
if ([string]::IsNullOrWhiteSpace($codePath) -or [System.IO.Path]::IsPathRooted($codePath)) {
    throw "FairyGUI Publish.json codeGeneration.codePath must be a non-empty relative path: $publishPath"
}
$rootNamespace = [string]$codeGeneration.packageName
Assert-CSharpNamespace $rootNamespace
$codeRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $codePath))
$repositoryPrefix = $repositoryRoot.TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
if (-not $codeRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "FairyGUI Publish.json codeGeneration.codePath escapes repository root '$repositoryRoot': $codeRoot"
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $projectRoot 'generated/GDKFairyManifest.json'
}
$manifestFullPath = [System.IO.Path]::GetFullPath($ManifestPath)
if (-not (Test-Path -LiteralPath $manifestFullPath -PathType Leaf)) {
    throw "FairyGUI source manifest is missing: $manifestFullPath"
}
try {
    $manifest = [System.IO.File]::ReadAllText($manifestFullPath) | ConvertFrom-Json
}
catch {
    throw "FairyGUI source manifest is invalid: $($_.Exception.Message)"
}
if ($manifest.schemaVersion -ne 1) {
    throw "FairyGUI source manifest must use schema 1: $manifestFullPath"
}

$sourcePackages = @($manifest.packages)
if ($sourcePackages.Count -eq 0) {
    throw "FairyGUI source manifest contains no packages: $manifestFullPath"
}

$packageIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$packageNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$packageRows = [System.Collections.Generic.List[object]]::new()
foreach ($package in $sourcePackages) {
    if ($null -eq $package) {
        throw 'FairyGUI source manifest contains a null package entry.'
    }

    $packageId = [string]$package.id
    $packageName = [string]$package.name
    if ([string]::IsNullOrWhiteSpace($packageId) -or [string]::IsNullOrWhiteSpace($packageName)) {
        throw 'FairyGUI source manifest package entries must contain non-empty id and name values.'
    }
    Assert-CSharpIdentifier $packageName 'FairyGUI package name'
    if (-not $packageIds.Add($packageId)) {
        throw "Duplicate FairyGUI package id '$packageId' in source manifest."
    }
    if (-not $packageNames.Add($packageName)) {
        throw "Duplicate FairyGUI package name '$packageName' in source manifest."
    }

    $binderPath = Join-Path (Join-Path $codeRoot $packageName) ($packageName + 'Binder.cs')
    if (-not (Test-Path -LiteralPath $binderPath -PathType Leaf)) {
        throw "FairyGUI binder source is missing for package '$packageName': $binderPath"
    }

    $binderSource = [System.IO.File]::ReadAllText($binderPath)
    $binderNamespace = "$rootNamespace.$packageName"
    $namespacePattern = '(?m)^\s*namespace\s+' + [System.Text.RegularExpressions.Regex]::Escape($binderNamespace) + '(?:\s|$)'
    $binderClassPattern = '\bclass\s+' + [System.Text.RegularExpressions.Regex]::Escape($packageName + 'Binder') + '\b'
    if ($binderSource -notmatch $namespacePattern -or
        $binderSource -notmatch $binderClassPattern -or
        $binderSource -notmatch '\bstatic\s+void\s+BindAll\s*\(') {
        throw "FairyGUI binder source for package '$packageName' does not declare '$binderNamespace.$($packageName)Binder.BindAll()': $binderPath"
    }

    $packageRows.Add([pscustomobject]@{
        Id = $packageId
        Name = $packageName
    })
}

$packageNameOrder = [string[]]::new($packageRows.Count)
for ($index = 0; $index -lt $packageRows.Count; $index++) {
    $packageNameOrder[$index] = $packageRows[$index].Name
}
[System.Array]::Sort($packageNameOrder, [System.StringComparer]::Ordinal)

$source = [System.Text.StringBuilder]::new()
$null = $source.AppendLine('// <auto-generated />')
$null = $source.AppendLine('// Generated by Tools/FairyGUI/Generate-FairyPackageBinderRegistry.ps1. Do not edit.')
$null = $source.AppendLine()
$null = $source.AppendLine('using Game;')
$null = $source.AppendLine('using GameFramework;')
$null = $source.AppendLine()
$null = $source.AppendLine("namespace $rootNamespace")
$null = $source.AppendLine('{')
$null = $source.AppendLine('    public static class FairyPackageBinderRegistryGenerated')
$null = $source.AppendLine('    {')
$null = $source.AppendLine('        public static void BindAllPackages(FairyUIFormDescriptor descriptor)')
$null = $source.AppendLine('        {')
$null = $source.AppendLine('            switch (descriptor.PackageName)')
$null = $source.AppendLine('            {')
foreach ($packageName in $packageNameOrder) {
    $null = $source.AppendLine("                case `"$packageName`":")
}
$null = $source.AppendLine('                    break;')
$null = $source.AppendLine('                default:')
$null = $source.AppendLine('                    throw new GameFrameworkException(')
$null = $source.AppendLine('                        $"No FairyGUI package binder is registered for UI ''{descriptor.CsName}'' " +')
$null = $source.AppendLine('                        $"(id {descriptor.UiId}, package ''{descriptor.PackageName}'').");')
$null = $source.AppendLine('            }')
$null = $source.AppendLine()
foreach ($packageName in $packageNameOrder) {
    $null = $source.AppendLine("            global::$rootNamespace.$packageName.$($packageName)Binder.BindAll();")
}
$null = $source.AppendLine('        }')
$null = $source.AppendLine('    }')
$null = $source.AppendLine('}')
$sourceText = Get-NormalizedText $source.ToString()
$sourceBytes = $utf8NoBom.GetBytes($sourceText)
$outputPath = Join-Path $codeRoot 'FairyPackageBinderRegistry.Generated.cs'

if ($Check) {
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        throw "Generated FairyGUI package binder registry is missing: $outputPath"
    }
    $actualBytes = [System.IO.File]::ReadAllBytes($outputPath)
    if (-not [System.Linq.Enumerable]::SequenceEqual[byte]($actualBytes, $sourceBytes)) {
        throw "Generated FairyGUI package binder registry is stale: $outputPath"
    }
}
else {
    if ((Test-Path -LiteralPath $outputPath -PathType Leaf) -and
        [System.Linq.Enumerable]::SequenceEqual[byte]([System.IO.File]::ReadAllBytes($outputPath), $sourceBytes)) {
        [pscustomobject][ordered]@{
            success = $true
            packageCount = $packageNameOrder.Count
            outputPath = $outputPath
            checked = $false
            changed = $false
        } | ConvertTo-Json -Compress
        return
    }

    [System.IO.Directory]::CreateDirectory($codeRoot) | Out-Null
    $temporaryPath = Join-Path $codeRoot ('.FairyPackageBinderRegistry.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [System.IO.File]::WriteAllBytes($temporaryPath, $sourceBytes)
        [System.IO.File]::Move($temporaryPath, $outputPath, $true)
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

[pscustomobject][ordered]@{
    success = $true
    packageCount = $packageNameOrder.Count
    outputPath = $outputPath
    checked = [bool]$Check
    changed = -not [bool]$Check
} | ConvertTo-Json -Compress
