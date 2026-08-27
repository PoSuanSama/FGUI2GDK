[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '../../Design/FairyGUI/GDK_FGUI'),
    [string]$ManifestPath,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'

function Get-NormalizedText {
    param([Parameter(Mandatory)][string]$Text)

    $normalized = ($Text -replace "`r`n", "`n") -replace "`r", "`n"
    return $normalized.TrimEnd([char[]]"`n") + "`n"
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function Get-TextHash {
    param([Parameter(Mandatory)][string]$Path)

    $text = Get-NormalizedText ([System.IO.File]::ReadAllText($Path))
    return Get-Sha256Hex ([System.Text.Encoding]::UTF8.GetBytes($text))
}

function Read-SafeXml {
    param([Parameter(Mandatory)][string]$Path)

    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($Path, $settings)
    try {
        $document = [System.Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
        return $document
    }
    finally {
        $reader.Dispose()
    }
}

function Get-RelativeSlashPath {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Path
    )

    return [System.IO.Path]::GetRelativePath($Root, $Path).Replace('\', '/')
}

function Test-PathInsideRoot {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Path
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $prefix = $resolvedRoot + [System.IO.Path]::DirectorySeparatorChar
    return $resolvedPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Resolve-ComponentFile {
    param(
        [Parameter(Mandatory)][string]$PackageDirectory,
        [Parameter(Mandatory)][string]$ResourcePath,
        [Parameter(Mandatory)][string]$ResourceName
    )

    $relativeDirectory = $ResourcePath.Trim('/', '\')
    if ([string]::IsNullOrEmpty($relativeDirectory)) {
        return Join-Path $PackageDirectory $ResourceName
    }

    return Join-Path (Join-Path $PackageDirectory $relativeDirectory) $ResourceName
}

function Resolve-MemberType {
    param(
        [Parameter(Mandatory)]$Member,
        [Parameter(Mandatory)][hashtable]$ResourceLookup,
        [Parameter(Mandatory)][hashtable]$ComponentLookup,
        [Parameter(Mandatory)][string]$CurrentPackageId
    )

    if (-not [string]::IsNullOrEmpty($Member.Src)) {
        $targetPackageId = if ([string]::IsNullOrEmpty($Member.Pkg)) { $CurrentPackageId } else { $Member.Pkg }
        $key = "$targetPackageId`:$($Member.Src)"
        $resource = $ResourceLookup[$key]
        if ($null -ne $resource -and $resource.Type -eq 'component') {
            $component = $ComponentLookup[$key]
            if ($null -ne $component -and -not [string]::IsNullOrEmpty($component.Extension)) {
                return "G$($component.Extension)"
            }

            return 'GComponent'
        }

        if ($null -ne $resource) {
            switch ($resource.Type.ToLowerInvariant()) {
                'image' { return 'GImage' }
                'movieclip' { return 'GMovieClip' }
                'sound' { return 'NAudioClip' }
            }
        }
    }

    switch ($Member.Tag.ToLowerInvariant()) {
        'component' { return 'GComponent' }
        'graph' { return 'GGraph' }
        'group' { return 'GGroup' }
        'image' { return 'GImage' }
        'inputtext' { return 'GTextInput' }
        'list' { return 'GList' }
        'loader' { return 'GLoader' }
        'loader3d' { return 'GLoader3D' }
        'movieclip' { return 'GMovieClip' }
        'richtext' { return 'GRichTextField' }
        'text' { return 'GTextField' }
        default { return 'GObject' }
    }
}

$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$projectFile = Join-Path $projectRoot 'GDK_FGUI.fairy'
$contractFile = Join-Path $projectRoot 'settings/GDK.json'
$publishFile = Join-Path $projectRoot 'settings/Publish.json'
$assetsRoot = Join-Path $projectRoot 'assets'

foreach ($requiredFile in @($projectFile, $contractFile, $publishFile)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required FairyGUI project file is missing: $requiredFile"
    }
}
if (-not (Test-Path -LiteralPath $assetsRoot -PathType Container)) {
    throw "FairyGUI assets directory is missing: $assetsRoot"
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $projectRoot 'generated/GDKFairyManifest.json'
}
else {
    $ManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
}

$errors = [System.Collections.Generic.List[string]]::new()
$sourceFiles = [System.Collections.Generic.List[object]]::new()
$packages = [System.Collections.Generic.List[object]]::new()
$packageById = @{}
$packageByName = @{}
$resourceLookup = @{}
$componentLookup = @{}
$dependencyMap = @{}

try {
    $projectDocument = Read-SafeXml $projectFile
}
catch {
    throw "Invalid FairyGUI project XML '$projectFile': $($_.Exception.Message)"
}
$projectElement = $projectDocument.DocumentElement
if ($projectElement.LocalName -ne 'projectDescription') {
    $null = $errors.Add('GDK_FGUI.fairy must contain a projectDescription root element.')
}
$projectId = $projectElement.GetAttribute('id')
$projectType = $projectElement.GetAttribute('type')
if ([string]::IsNullOrWhiteSpace($projectId)) {
    $null = $errors.Add('FairyGUI project id is required.')
}
if ($projectType -ne 'Unity') {
    $null = $errors.Add("FairyGUI project type must be Unity, found '$projectType'.")
}
$null = $sourceFiles.Add([pscustomobject]@{
    Path = Get-RelativeSlashPath $projectRoot $projectFile
    Hash = Get-TextHash $projectFile
})

$packageDirectories = @(Get-ChildItem -LiteralPath $assetsRoot -Directory | Sort-Object Name)
foreach ($packageDirectory in $packageDirectories) {
    $packageFile = Join-Path $packageDirectory.FullName 'package.xml'
    if (-not (Test-Path -LiteralPath $packageFile -PathType Leaf)) {
        $null = $errors.Add("Package directory is missing package.xml: $($packageDirectory.FullName)")
        continue
    }

    try {
        $packageDocument = Read-SafeXml $packageFile
    }
    catch {
        $null = $errors.Add("Invalid package XML '$packageFile': $($_.Exception.Message)")
        continue
    }

    $packageElement = $packageDocument.DocumentElement
    if ($packageElement.LocalName -ne 'packageDescription') {
        $null = $errors.Add("Package file must contain a packageDescription root: $packageFile")
        continue
    }

    $packageId = $packageElement.GetAttribute('id')
    $publishElement = [System.Xml.XmlElement]$packageElement.SelectSingleNode('publish')
    $packageName = if ($null -eq $publishElement) { '' } else { $publishElement.GetAttribute('name') }
    if ([string]::IsNullOrWhiteSpace($packageId)) {
        $null = $errors.Add("Package id is required: $packageFile")
    }
    elseif ($packageById.ContainsKey($packageId)) {
        $null = $errors.Add("Duplicate package id '$packageId'.")
    }
    if ([string]::IsNullOrWhiteSpace($packageName)) {
        $null = $errors.Add("Package publish name is required: $packageFile")
    }
    elseif ($packageByName.ContainsKey($packageName)) {
        $null = $errors.Add("Duplicate package name '$packageName'.")
    }

    $resourceIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $resourceNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $resources = [System.Collections.Generic.List[object]]::new()
    $resourcesElement = $packageElement.SelectSingleNode('resources')
    if ($null -eq $resourcesElement) {
        $null = $errors.Add("Package resources element is missing: $packageFile")
    }
    else {
        foreach ($resourceElement in $resourcesElement.ChildNodes) {
            if ($resourceElement.NodeType -ne [System.Xml.XmlNodeType]::Element) {
                continue
            }

            $resourceId = $resourceElement.GetAttribute('id')
            $resourceName = $resourceElement.GetAttribute('name')
            if ([string]::IsNullOrWhiteSpace($resourceId)) {
                $null = $errors.Add("Resource id is required in package '$packageName'.")
                continue
            }
            if (-not $resourceIds.Add($resourceId)) {
                $null = $errors.Add("Duplicate resource id '$resourceId' in package '$packageName'.")
                continue
            }
            if ([string]::IsNullOrWhiteSpace($resourceName)) {
                $null = $errors.Add("Resource name is required for '$resourceId' in package '$packageName'.")
            }
            elseif (-not $resourceNames.Add($resourceName)) {
                $null = $errors.Add("Duplicate resource name '$resourceName' in package '$packageName'.")
            }

            $resource = [pscustomobject]@{
                Type = $resourceElement.LocalName
                Id = $resourceId
                Name = $resourceName
                Path = $resourceElement.GetAttribute('path')
                Exported = $resourceElement.GetAttribute('exported') -eq 'true'
                PackageId = $packageId
                PackageName = $packageName
                PackageDirectory = $packageDirectory.FullName
                File = $null
                Hash = $null
                Members = @()
            }
            $null = $resources.Add($resource)
            $resourceLookup["$packageId`:$resourceId"] = $resource
        }
    }

    $package = [pscustomobject]@{
        Id = $packageId
        Name = $packageName
        Directory = $packageDirectory.FullName
        File = $packageFile
        Hash = Get-TextHash $packageFile
        Resources = $resources
    }
    $null = $packages.Add($package)
    if (-not [string]::IsNullOrWhiteSpace($packageId)) {
        $packageById[$packageId] = $package
        $dependencyMap[$packageId] = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    }
    if (-not [string]::IsNullOrWhiteSpace($packageName)) {
        $packageByName[$packageName] = $package
    }
    $null = $sourceFiles.Add([pscustomobject]@{
        Path = Get-RelativeSlashPath $projectRoot $packageFile
        Hash = $package.Hash
    })
}

foreach ($package in $packages) {
    foreach ($resource in $package.Resources) {
        if ($resource.Type -ne 'component') {
            continue
        }

        $componentFile = [System.IO.Path]::GetFullPath((Resolve-ComponentFile $package.Directory $resource.Path $resource.Name))
        $resource.File = $componentFile
        if (-not (Test-PathInsideRoot $projectRoot $componentFile)) {
            $null = $errors.Add("Component file escapes the FairyGUI project root for '$($package.Name)/$($resource.Name)': $componentFile")
            continue
        }
        if (-not (Test-Path -LiteralPath $componentFile -PathType Leaf)) {
            $null = $errors.Add("Component file is missing for '$($package.Name)/$($resource.Name)': $componentFile")
            continue
        }

        try {
            $componentDocument = Read-SafeXml $componentFile
        }
        catch {
            $null = $errors.Add("Invalid component XML '$componentFile': $($_.Exception.Message)")
            continue
        }

        $componentElement = $componentDocument.DocumentElement
        if ($componentElement.LocalName -ne 'component') {
            $null = $errors.Add("Component file must contain a component root: $componentFile")
            continue
        }

        $controllers = @($componentElement.SelectNodes('controller'))
        $controllerNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($controller in $controllers) {
            $controllerName = $controller.GetAttribute('name')
            if ([string]::IsNullOrWhiteSpace($controllerName)) {
                $null = $errors.Add("Controller name is required in '$($resource.Name)'.")
            }
            elseif (-not $controllerNames.Add($controllerName)) {
                $null = $errors.Add("Duplicate controller name '$controllerName' in '$($resource.Name)'.")
            }
        }

        $memberIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        $memberNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        $members = [System.Collections.Generic.List[object]]::new()
        $displayList = $componentElement.SelectSingleNode('displayList')
        if ($null -ne $displayList) {
            foreach ($memberElement in $displayList.ChildNodes) {
                if ($memberElement.NodeType -ne [System.Xml.XmlNodeType]::Element) {
                    continue
                }

                $memberId = $memberElement.GetAttribute('id')
                $memberName = $memberElement.GetAttribute('name')
                if ([string]::IsNullOrWhiteSpace($memberId)) {
                    $null = $errors.Add("Display member id is required in '$($resource.Name)'.")
                }
                elseif (-not $memberIds.Add($memberId)) {
                    $null = $errors.Add("Duplicate display member id '$memberId' in '$($resource.Name)'.")
                }
                if (-not [string]::IsNullOrWhiteSpace($memberName) -and -not $memberNames.Add($memberName)) {
                    $null = $errors.Add("Duplicate display member name '$memberName' in '$($resource.Name)'.")
                }

                $member = [pscustomobject]@{
                    Tag = $memberElement.LocalName
                    Id = $memberId
                    Name = $memberName
                    Src = $memberElement.GetAttribute('src')
                    Pkg = $memberElement.GetAttribute('pkg')
                    Type = $null
                }
                $null = $members.Add($member)
            }
        }

        $component = [pscustomobject]@{
            Resource = $resource
            Document = $componentDocument
            Extension = $componentElement.GetAttribute('extention')
            Controllers = $controllerNames
            MemberIds = $memberIds
            Members = $members
        }
        $componentLookup["$($package.Id)`:$($resource.Id)"] = $component
        $resource.Hash = Get-TextHash $componentFile
        $resource.Members = $members
        $null = $sourceFiles.Add([pscustomobject]@{
            Path = Get-RelativeSlashPath $projectRoot $componentFile
            Hash = $resource.Hash
        })
    }
}

foreach ($package in $packages) {
    foreach ($resource in $package.Resources) {
        if ($resource.Type -ne 'component' -or $null -eq $resource.File -or -not (Test-Path -LiteralPath $resource.File)) {
            continue
        }

        $component = $componentLookup["$($package.Id)`:$($resource.Id)"]
        if ($null -eq $component) {
            continue
        }

        foreach ($member in $component.Members) {
            if (-not [string]::IsNullOrEmpty($member.Pkg) -and [string]::IsNullOrEmpty($member.Src)) {
                $null = $errors.Add("Package reference '$($member.Pkg)' requires src in '$($resource.Name)' member '$($member.Name)'.")
            }
            if (-not [string]::IsNullOrEmpty($member.Src)) {
                $targetPackageId = if ([string]::IsNullOrEmpty($member.Pkg)) { $package.Id } else { $member.Pkg }
                $referenceKey = "$targetPackageId`:$($member.Src)"
                if (-not $resourceLookup.ContainsKey($referenceKey)) {
                    $null = $errors.Add("Unknown resource reference '$referenceKey' in '$($resource.Name)' member '$($member.Name)'.")
                }
                elseif ($targetPackageId -ne $package.Id) {
                    $null = $dependencyMap[$package.Id].Add($targetPackageId)
                }
            }
            $member.Type = Resolve-MemberType $member $resourceLookup $componentLookup $package.Id
        }

        foreach ($node in $component.Document.DocumentElement.SelectNodes('.//*')) {
            if ($node.LocalName.StartsWith('gear', [System.StringComparison]::OrdinalIgnoreCase)) {
                $controller = $node.GetAttribute('controller')
                if (-not [string]::IsNullOrEmpty($controller)) {
                    $controllerIndex = 0
                    $isIndex = [int]::TryParse($controller, [ref]$controllerIndex)
                    $validIndex = $isIndex -and $controllerIndex -ge 0 -and $controllerIndex -lt $component.Controllers.Count
                    if (-not $validIndex -and -not $component.Controllers.Contains($controller)) {
                        $null = $errors.Add("Unknown controller '$controller' in '$($resource.Name)'.")
                    }
                }
            }
            if ($node.LocalName -eq 'relation') {
                $target = $node.GetAttribute('target')
                if (-not [string]::IsNullOrEmpty($target) -and -not $component.MemberIds.Contains($target)) {
                    $null = $errors.Add("Unknown relation target '$target' in '$($resource.Name)'.")
                }
            }
        }
    }
}

$visitState = @{}
$visitStack = [System.Collections.Generic.List[string]]::new()
function Visit-PackageDependency {
    param([Parameter(Mandatory)][string]$PackageId)

    $state = if ($visitState.ContainsKey($PackageId)) { $visitState[$PackageId] } else { 0 }
    if ($state -eq 2) {
        return
    }
    if ($state -eq 1) {
        $cycleStart = $visitStack.IndexOf($PackageId)
        $cycle = @($visitStack[$cycleStart..($visitStack.Count - 1)]) + $PackageId
        $null = $errors.Add("Package dependency cycle: $($cycle -join ' -> ')")
        return
    }

    $visitState[$PackageId] = 1
    $null = $visitStack.Add($PackageId)
    foreach ($dependency in @($dependencyMap[$PackageId] | Sort-Object)) {
        if (-not $packageById.ContainsKey($dependency)) {
            $null = $errors.Add("Unknown package dependency '$dependency' referenced by '$PackageId'.")
            continue
        }
        Visit-PackageDependency $dependency
    }
    $visitStack.RemoveAt($visitStack.Count - 1)
    $visitState[$PackageId] = 2
}
foreach ($packageIdKey in @($packageById.Keys | Sort-Object)) {
    Visit-PackageDependency $packageIdKey
}

try {
    $contract = Get-Content -Raw -LiteralPath $contractFile | ConvertFrom-Json
}
catch {
    throw "Invalid GDK FairyGUI contract '$contractFile': $($_.Exception.Message)"
}
if ($contract.schemaVersion -ne 1) {
    $null = $errors.Add("Unsupported GDK FairyGUI contract schemaVersion '$($contract.schemaVersion)'.")
}
if ($contract.project.id -ne $projectId) {
    $null = $errors.Add("Project id contract mismatch. Expected '$($contract.project.id)', found '$projectId'.")
}
foreach ($packageContract in @($contract.packages)) {
    $package = $packageById[[string]$packageContract.id]
    if ($null -eq $package -or $package.Name -ne $packageContract.name) {
        $null = $errors.Add("Package contract mismatch for '$($packageContract.name)' ($($packageContract.id)).")
        continue
    }

    $entry = @($package.Resources | Where-Object { $_.Id -eq $packageContract.entry.id })
    if ($entry.Count -ne 1 -or $entry[0].Name -ne $packageContract.entry.name -or $entry[0].Exported -ne [bool]$packageContract.entry.exported) {
        $null = $errors.Add("Entry component contract mismatch for package '$($package.Name)'.")
        continue
    }

    $memberByName = @{}
    foreach ($member in @($entry[0].Members)) {
        if (-not [string]::IsNullOrWhiteSpace($member.Name)) {
            $memberByName[$member.Name] = $member
        }
    }
    foreach ($memberContract in @($packageContract.entry.requiredMembers)) {
        $member = $memberByName[[string]$memberContract.name]
        if ($null -eq $member) {
            $null = $errors.Add("Required member '$($memberContract.name)' is missing from '$($entry[0].Name)'.")
        }
        elseif ($member.Type -ne $memberContract.type) {
            $null = $errors.Add("Required member '$($memberContract.name)' must be '$($memberContract.type)', found '$($member.Type)'.")
        }
    }
}

try {
    $publishSettings = Get-Content -Raw -LiteralPath $publishFile | ConvertFrom-Json
}
catch {
    throw "Invalid FairyGUI publish settings '$publishFile': $($_.Exception.Message)"
}
$expectedCodeSettings = [ordered]@{
    allowGenCode = $true
    classNamePrefix = 'UI'
    codeType = ''
    getMemberByName = $true
    ignoreNoname = $true
    memberNamePrefix = 'm_'
    packageName = 'Game.Hot.FairyGUI'
}
foreach ($setting in $expectedCodeSettings.GetEnumerator()) {
    if ($publishSettings.codeGeneration.($setting.Key) -cne $setting.Value) {
        $null = $errors.Add("Publish codeGeneration.$($setting.Key) must be '$($setting.Value)'.")
    }
}
if ($publishSettings.binaryFormat -ne $true) {
    $null = $errors.Add('Publish binaryFormat must be true for Unity .bytes descriptors.')
}
if ([System.IO.Path]::IsPathRooted([string]$publishSettings.path) -or
    [System.IO.Path]::IsPathRooted([string]$publishSettings.codeGeneration.codePath)) {
    $null = $errors.Add('Repository Publish.json output paths must remain relative.')
}

if ($errors.Count -gt 0) {
    throw "FairyGUI project validation failed:`n - $($errors -join "`n - ")"
}

$contractHash = Get-Sha256Hex ([System.Text.Encoding]::UTF8.GetBytes((Get-NormalizedText ([System.IO.File]::ReadAllText($contractFile)))))
$sourceHashInput = ($sourceFiles | Sort-Object Path | ForEach-Object { "$($_.Path)`0$($_.Hash)" }) -join "`n"
$sourceHash = Get-Sha256Hex ([System.Text.Encoding]::UTF8.GetBytes($sourceHashInput))

$manifestPackages = @($packages | Sort-Object Id | ForEach-Object {
    $package = $_
    $manifestResources = @($package.Resources | Sort-Object Id, Type | ForEach-Object {
        $resource = $_
        $resourceManifest = [ordered]@{
            type = $resource.Type
            id = $resource.Id
            name = $resource.Name
            path = $resource.Path.Replace('\', '/')
            exported = $resource.Exported
        }
        if ($resource.Type -eq 'component') {
            $resourceManifest.file = Get-RelativeSlashPath $projectRoot $resource.File
            $resourceManifest.hash = $resource.Hash
            $resourceManifest.members = @($resource.Members | Sort-Object Id, Name | ForEach-Object {
                [ordered]@{
                    id = $_.Id
                    name = $_.Name
                    type = $_.Type
                    src = $_.Src
                    pkg = $_.Pkg
                }
            })
        }
        [pscustomobject]$resourceManifest
    })

    [pscustomobject][ordered]@{
        id = $package.Id
        name = $package.Name
        file = Get-RelativeSlashPath $projectRoot $package.File
        hash = $package.Hash
        dependencies = @($dependencyMap[$package.Id] | Sort-Object)
        resources = $manifestResources
    }
})

$manifest = [pscustomobject][ordered]@{
    schemaVersion = 1
    project = [pscustomobject][ordered]@{
        id = $projectId
        type = $projectType
        version = $projectElement.GetAttribute('version')
    }
    sourceHash = $sourceHash
    contractHash = $contractHash
    packages = $manifestPackages
}
$manifestText = Get-NormalizedText ($manifest | ConvertTo-Json -Depth 100)
$manifestBytes = [System.Text.UTF8Encoding]::new($false).GetBytes($manifestText)

if ($Check) {
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        throw "FairyGUI manifest is missing: $ManifestPath"
    }
    $actualBytes = [System.IO.File]::ReadAllBytes($ManifestPath)
    if (-not [System.Linq.Enumerable]::SequenceEqual[byte]($actualBytes, $manifestBytes)) {
        throw "FairyGUI manifest is stale: $ManifestPath"
    }
}
else {
    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $ManifestPath)) | Out-Null
    [System.IO.File]::WriteAllBytes($ManifestPath, $manifestBytes)
}

[pscustomobject][ordered]@{
    success = $true
    projectId = $projectId
    packageCount = $packages.Count
    sourceHash = $sourceHash
    manifestPath = $ManifestPath
    checked = [bool]$Check
} | ConvertTo-Json -Compress
