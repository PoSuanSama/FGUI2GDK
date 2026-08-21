[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Status', 'ToEditor', 'FromEditor')]
    [string]$Mode = 'Status',
    [string]$SourceProjectPath = (Join-Path $PSScriptRoot '../../Design/FairyGUI/GDK_FGUI'),
    [string]$EditorProjectPath = 'D:\Unity\Project\GDK_FGUI',
    [string]$OutputPath = (Join-Path $PSScriptRoot '../../Unity/Assets/Res/UI/FairyGUI'),
    [string]$CodeOutputPath = (Join-Path $PSScriptRoot '../../Unity/Assets/Scripts/Game/Hot/Code/Generate/FairyGUI'),
    [switch]$Initialize
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Get-NormalizedText {
    param([Parameter(Mandatory)][string]$Text)

    $normalized = ($Text -replace "`r`n", "`n") -replace "`r", "`n"
    return $normalized.TrimEnd([char[]]"`n") + "`n"
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function ConvertTo-StableValue {
    param($Value)

    if ($null -eq $Value) {
        return $null
    }
    if ($Value -is [System.Collections.IDictionary]) {
        $result = [ordered]@{}
        foreach ($key in @($Value.Keys | Sort-Object)) {
            $result[[string]$key] = ConvertTo-StableValue $Value[$key]
        }
        return $result
    }
    if ($Value -is [pscustomobject]) {
        $result = [ordered]@{}
        foreach ($property in @($Value.PSObject.Properties | Sort-Object Name)) {
            $result[$property.Name] = ConvertTo-StableValue $property.Value
        }
        return $result
    }
    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        $items = @()
        foreach ($item in $Value) {
            $items += ,(ConvertTo-StableValue $item)
        }
        return ,$items
    }
    return $Value
}

function Resolve-ProjectDirectory {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Label directory does not exist: $Path"
    }
    return (Resolve-Path -LiteralPath $Path).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
}

function Assert-PathInsideRoot {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Path
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $prefix = $resolvedRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes FairyGUI project root '$resolvedRoot': $resolvedPath"
    }
    return $resolvedPath
}

function Get-RelativeSlashPath {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Path
    )

    return [System.IO.Path]::GetRelativePath($Root, $Path).Replace('\', '/')
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

function Get-ProjectId {
    param([Parameter(Mandatory)][string]$Root)

    $projectFile = Join-Path $Root 'GDK_FGUI.fairy'
    if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
        throw "FairyGUI project file is missing: $projectFile"
    }
    $project = Read-SafeXml $projectFile
    if ($project.DocumentElement.GetAttribute('type') -ne 'Unity') {
        throw "FairyGUI project must use the Unity type: $projectFile"
    }
    $id = $project.DocumentElement.GetAttribute('id')
    if ([string]::IsNullOrWhiteSpace($id)) {
        throw "FairyGUI project id is missing: $projectFile"
    }
    return $id
}

function Get-AuthoringRelativePaths {
    param([Parameter(Mandatory)][string]$Root)

    $paths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($required in @('GDK_FGUI.fairy', 'settings/Publish.json')) {
        $path = Join-Path $Root $required
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required FairyGUI authoring file is missing: $path"
        }
        $null = $paths.Add($required.Replace('\', '/'))
    }

    $assetsRoot = Join-Path $Root 'assets'
    if (-not (Test-Path -LiteralPath $assetsRoot -PathType Container)) {
        throw "FairyGUI assets directory is missing: $assetsRoot"
    }
    foreach ($file in Get-ChildItem -LiteralPath $assetsRoot -Recurse -File) {
        $null = $paths.Add((Get-RelativeSlashPath $Root $file.FullName))
    }
    return @($paths | Sort-Object)
}

function Get-CanonicalFileBytes {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$RelativePath
    )

    if ($RelativePath -eq 'settings/Publish.json') {
        $settings = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
        $settings.path = '${GDK_PUBLISH_OUTPUT}'
        if ($null -ne $settings.codeGeneration) {
            $settings.codeGeneration.codePath = '${GDK_CODE_OUTPUT}'
        }
        $stable = ConvertTo-StableValue $settings
        $json = $stable | ConvertTo-Json -Depth 100 -Compress
        return $utf8NoBom.GetBytes((Get-NormalizedText $json))
    }

    $extension = [System.IO.Path]::GetExtension($RelativePath).ToLowerInvariant()
    if ($extension -in @('.fairy', '.xml', '.json', '.js', '.lua')) {
        return $utf8NoBom.GetBytes((Get-NormalizedText ([System.IO.File]::ReadAllText($Path))))
    }
    return [System.IO.File]::ReadAllBytes($Path)
}

function Get-ProjectSnapshot {
    param([Parameter(Mandatory)][string]$Root)

    $relativePaths = @(Get-AuthoringRelativePaths $Root)
    $fingerprintLines = foreach ($relativePath in $relativePaths) {
        $path = Assert-PathInsideRoot $Root (Join-Path $Root $relativePath)
        $contentHash = Get-Sha256Hex (Get-CanonicalFileBytes $path $relativePath)
        "$relativePath`0$contentHash"
    }
    $fingerprint = $fingerprintLines -join "`n"
    return [pscustomobject]@{
        Hash = Get-Sha256Hex ($utf8NoBom.GetBytes($fingerprint))
        RelativePaths = $relativePaths
    }
}

function Get-SyncState {
    param(
        [Parameter(Mandatory)][string]$StatePath,
        [Parameter(Mandatory)][string]$ProjectId
    )

    if (-not (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
        return $null
    }
    try {
        $state = Get-Content -Raw -LiteralPath $StatePath | ConvertFrom-Json
    }
    catch {
        throw "Invalid FairyGUI sync state '$StatePath': $($_.Exception.Message)"
    }
    if ($state.schemaVersion -ne 1 -or $state.projectId -ne $ProjectId -or
        [string]::IsNullOrWhiteSpace([string]$state.lastCommonHash)) {
        throw "FairyGUI sync state is incompatible with project '$ProjectId': $StatePath"
    }
    return $state
}

function Get-SyncClassification {
    param(
        [Parameter(Mandatory)][string]$RepositoryHash,
        [Parameter(Mandatory)][string]$EditorHash,
        $State
    )

    if ($RepositoryHash -eq $EditorHash) {
        return 'Equal'
    }
    if ($null -eq $State) {
        return 'UninitializedDifferent'
    }

    $repositoryChanged = $RepositoryHash -ne $State.lastCommonHash
    $editorChanged = $EditorHash -ne $State.lastCommonHash
    if ($repositoryChanged -and $editorChanged) {
        return 'Conflict'
    }
    if ($repositoryChanged) {
        return 'RepositoryChanged'
    }
    if ($editorChanged) {
        return 'EditorChanged'
    }
    throw 'FairyGUI sync state is inconsistent with the current project hashes.'
}

function Convert-PublishSettings {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][ValidateSet('Repository', 'Editor')][string]$Target
    )

    $settings = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    if ($null -eq $settings.codeGeneration) {
        throw "FairyGUI Publish.json has no codeGeneration section: $Path"
    }

    $settings.binaryFormat = $true
    $settings.codeGeneration.allowGenCode = $true
    $settings.codeGeneration.classNamePrefix = 'UI_'
    $settings.codeGeneration.codeType = ''
    $settings.codeGeneration.getMemberByName = $true
    $settings.codeGeneration.ignoreNoname = $true
    $settings.codeGeneration.memberNamePrefix = 'm_'
    $settings.codeGeneration.packageName = 'Game.Hot.FairyGUI'
    if ($Target -eq 'Editor') {
        $settings.path = $resolvedOutput.Replace('\', '/')
        $settings.codeGeneration.codePath = $resolvedCodeOutput.Replace('\', '/')
    }
    else {
        $settings.path = ([System.IO.Path]::GetRelativePath($sourceRoot, $resolvedOutput)).Replace('\', '/')
        $settings.codeGeneration.codePath = ([System.IO.Path]::GetRelativePath($sourceRoot, $resolvedCodeOutput)).Replace('\', '/')
    }
    return Get-NormalizedText ($settings | ConvertTo-Json -Depth 100)
}

function Write-BytesAtomically {
    param(
        [Parameter(Mandatory)][string]$TargetPath,
        [Parameter(Mandatory)][byte[]]$Bytes,
        [Parameter(Mandatory)][string]$Action
    )

    if (-not $PSCmdlet.ShouldProcess($TargetPath, $Action)) {
        return
    }
    $directory = Split-Path -Parent $TargetPath
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $temporaryPath = Join-Path $directory ('.gdk-sync-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [System.IO.File]::WriteAllBytes($temporaryPath, $Bytes)
        [System.IO.File]::Move($temporaryPath, $TargetPath, $true)
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Sync-AuthoringFiles {
    param(
        [Parameter(Mandatory)][string]$FromRoot,
        [Parameter(Mandatory)][string]$ToRoot,
        [Parameter(Mandatory)][ValidateSet('Repository', 'Editor')][string]$TargetKind
    )

    $sourcePaths = @(Get-AuthoringRelativePaths $FromRoot)
    $sourceSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($sourceRelativePath in $sourcePaths) {
        $null = $sourceSet.Add($sourceRelativePath)
    }
    $targetPaths = @(Get-AuthoringRelativePaths $ToRoot)
    foreach ($relativePath in $targetPaths) {
        if ($sourceSet.Contains($relativePath)) {
            continue
        }
        $targetPath = Assert-PathInsideRoot $ToRoot (Join-Path $ToRoot $relativePath)
        if ($PSCmdlet.ShouldProcess($targetPath, 'Delete removed FairyGUI authoring file')) {
            Remove-Item -LiteralPath $targetPath -Force
        }
    }

    foreach ($relativePath in $sourcePaths) {
        $sourcePath = Assert-PathInsideRoot $FromRoot (Join-Path $FromRoot $relativePath)
        $targetPath = Assert-PathInsideRoot $ToRoot (Join-Path $ToRoot $relativePath)
        if ($relativePath -eq 'settings/Publish.json') {
            $publishText = Convert-PublishSettings $sourcePath $TargetKind
            $bytes = $utf8NoBom.GetBytes($publishText)
        }
        else {
            $bytes = [System.IO.File]::ReadAllBytes($sourcePath)
        }
        Write-BytesAtomically $targetPath $bytes 'Synchronize FairyGUI authoring file'
    }
}

function Sync-RepositoryContract {
    $contractPath = Join-Path $sourceRoot 'settings/GDK.json'
    if (-not (Test-Path -LiteralPath $contractPath -PathType Leaf)) {
        throw "GDK FairyGUI contract is missing: $contractPath"
    }
    $targetPath = Assert-PathInsideRoot $editorRoot (Join-Path $editorRoot 'settings/GDK.json')
    Write-BytesAtomically $targetPath ([System.IO.File]::ReadAllBytes($contractPath)) 'Synchronize FairyGUI contract'
}

function Assert-RepositoryContract {
    $contractPath = Join-Path $sourceRoot 'settings/GDK.json'
    if (-not (Test-Path -LiteralPath $contractPath -PathType Leaf)) {
        throw "GDK FairyGUI contract is missing: $contractPath"
    }
    [void][System.IO.File]::ReadAllBytes($contractPath)
}

function Test-IncomingProject {
    param([Parameter(Mandatory)][string]$IncomingRoot)

    $temporaryBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
    $stagingRoot = Join-Path $temporaryBase ('gdk-fairygui-preflight-' + [Guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($stagingRoot) | Out-Null
    try {
        foreach ($relativePath in Get-AuthoringRelativePaths $IncomingRoot) {
            $sourcePath = Assert-PathInsideRoot $IncomingRoot (Join-Path $IncomingRoot $relativePath)
            $targetPath = Assert-PathInsideRoot $stagingRoot (Join-Path $stagingRoot $relativePath)
            [System.IO.Directory]::CreateDirectory((Split-Path -Parent $targetPath)) | Out-Null
            if ($relativePath -eq 'settings/Publish.json') {
                $publishText = Convert-PublishSettings $sourcePath 'Repository'
                [System.IO.File]::WriteAllText($targetPath, $publishText, $utf8NoBom)
            }
            else {
                [System.IO.File]::WriteAllBytes($targetPath, [System.IO.File]::ReadAllBytes($sourcePath))
            }
        }

        $contractSource = Assert-PathInsideRoot $sourceRoot (Join-Path $sourceRoot 'settings/GDK.json')
        $contractTarget = Assert-PathInsideRoot $stagingRoot (Join-Path $stagingRoot 'settings/GDK.json')
        [System.IO.Directory]::CreateDirectory((Split-Path -Parent $contractTarget)) | Out-Null
        [System.IO.File]::WriteAllBytes($contractTarget, [System.IO.File]::ReadAllBytes($contractSource))

        $validator = Join-Path $PSScriptRoot 'Test-GDKProject.ps1'
        if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
            throw "FairyGUI project validator is missing: $validator"
        }
        $manifestPath = Assert-PathInsideRoot $stagingRoot (Join-Path $stagingRoot 'generated/GDKFairyManifest.json')
        try {
            $null = & $validator -ProjectPath $stagingRoot -ManifestPath $manifestPath
        }
        catch {
            throw "Incoming FairyGUI project failed preflight validation: $($_.Exception.Message)"
        }
    }
    finally {
        $resolvedStagingRoot = [System.IO.Path]::GetFullPath($stagingRoot)
        $temporaryPrefix = $temporaryBase + [System.IO.Path]::DirectorySeparatorChar
        if ($resolvedStagingRoot.StartsWith($temporaryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
            [System.IO.Path]::GetFileName($resolvedStagingRoot).StartsWith('gdk-fairygui-preflight-', [System.StringComparison]::Ordinal) -and
            (Test-Path -LiteralPath $resolvedStagingRoot -PathType Container)) {
            [System.IO.Directory]::Delete($resolvedStagingRoot, $true)
        }
    }
}

function Write-SyncState {
    param(
        [Parameter(Mandatory)][string]$StatePath,
        [Parameter(Mandatory)][string]$ProjectId,
        [Parameter(Mandatory)][string]$Hash
    )

    $state = [pscustomobject][ordered]@{
        schemaVersion = 1
        projectId = $ProjectId
        lastCommonHash = $Hash
    }
    $bytes = $utf8NoBom.GetBytes((Get-NormalizedText ($state | ConvertTo-Json)))
    Write-BytesAtomically $StatePath $bytes 'Record FairyGUI common sync state'
}

$sourceRoot = Resolve-ProjectDirectory $SourceProjectPath 'Repository FairyGUI project'
$editorRoot = Resolve-ProjectDirectory $EditorProjectPath 'Editor FairyGUI project'
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$resolvedCodeOutput = [System.IO.Path]::GetFullPath($CodeOutputPath)
$sourceProjectId = Get-ProjectId $sourceRoot
$editorProjectId = Get-ProjectId $editorRoot
if ($sourceProjectId -ne $editorProjectId) {
    throw "Repository and Editor FairyGUI project IDs do not match: '$sourceProjectId' != '$editorProjectId'."
}

$statePath = Assert-PathInsideRoot $editorRoot (Join-Path $editorRoot '.gdk-sync-state.json')
$repositorySnapshot = Get-ProjectSnapshot $sourceRoot
$editorSnapshot = Get-ProjectSnapshot $editorRoot
$syncState = Get-SyncState $statePath $sourceProjectId
$classification = Get-SyncClassification $repositorySnapshot.Hash $editorSnapshot.Hash $syncState

$result = [ordered]@{
    mode = $Mode
    state = $classification
    projectId = $sourceProjectId
    repositoryHash = $repositorySnapshot.Hash
    editorHash = $editorSnapshot.Hash
    lastCommonHash = if ($null -eq $syncState) { $null } else { $syncState.lastCommonHash }
    applied = $false
}

if ($Mode -eq 'Status') {
    [pscustomobject]$result | ConvertTo-Json -Compress
    return
}

$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$editorIsTemporary = $editorRoot.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase)
if (-not $WhatIfPreference -and -not $editorIsTemporary -and
    (Get-Process -Name 'FairyGUI-Editor' -ErrorAction SilentlyContinue)) {
    throw 'FairyGUI-Editor is running. Save and close it before synchronizing project files.'
}

if ($classification -eq 'Conflict') {
    throw 'Both FairyGUI projects changed since the last sync. Resolve the XML conflict before synchronizing.'
}
if ($classification -eq 'UninitializedDifferent' -and -not $Initialize) {
    throw 'FairyGUI projects differ and have no common sync state. Choose ToEditor or FromEditor and pass -Initialize.'
}
if ($Mode -eq 'ToEditor' -and $classification -eq 'EditorChanged') {
    throw 'The Editor FairyGUI project contains the only changes. Use FromEditor to preserve them.'
}
if ($Mode -eq 'FromEditor' -and $classification -eq 'RepositoryChanged') {
    throw 'The repository FairyGUI project contains the only changes. Use ToEditor to preserve them.'
}

Assert-RepositoryContract
if ($Mode -eq 'ToEditor') {
    [void](Convert-PublishSettings (Join-Path $sourceRoot 'settings/Publish.json') 'Editor')
    Test-IncomingProject $sourceRoot
}
else {
    [void](Convert-PublishSettings (Join-Path $editorRoot 'settings/Publish.json') 'Repository')
    Test-IncomingProject $editorRoot
}

if ($Mode -eq 'ToEditor') {
    Sync-AuthoringFiles $sourceRoot $editorRoot 'Editor'
}
else {
    Sync-AuthoringFiles $editorRoot $sourceRoot 'Repository'
    # Reapply repository-owned publish invariants and machine-local paths to the Editor copy.
    Sync-AuthoringFiles $sourceRoot $editorRoot 'Editor'
}
Sync-RepositoryContract
if (-not $WhatIfPreference) {
    $repositoryAfter = Get-ProjectSnapshot $sourceRoot
    $editorAfter = Get-ProjectSnapshot $editorRoot
    if ($repositoryAfter.Hash -ne $editorAfter.Hash) {
        throw 'FairyGUI synchronization did not converge; sync state was not updated.'
    }
    Write-SyncState $statePath $sourceProjectId $repositoryAfter.Hash
    $result.state = 'Equal'
    $result.repositoryHash = $repositoryAfter.Hash
    $result.editorHash = $editorAfter.Hash
    $result.lastCommonHash = $repositoryAfter.Hash
    $result.applied = $true
}

[pscustomobject]$result | ConvertTo-Json -Compress
