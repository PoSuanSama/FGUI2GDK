[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '../../Design/FairyGUI/GDK_FGUI')
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$syncScript = Join-Path $PSScriptRoot 'Sync-GDKDemoToEditor.ps1'
$lintScript = Join-Path $PSScriptRoot 'Test-GDKProject.ps1'
$publishScript = Join-Path $PSScriptRoot 'Publish-GDKDemo.ps1'
$descriptorScript = Join-Path $PSScriptRoot 'Generate-FairyUIFormDescriptors.ps1'
$runtimeManifestScript = Join-Path $PSScriptRoot 'Generate-FairyRuntimeManifest.ps1'
$lubanUiFormData = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../Unity/Assets/Res/Editor/Luban/dtuiform.json')).Path
$sourceProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('gdk-fairygui-tools-' + [Guid]::NewGuid().ToString('N'))
$script:assertionCount = 0

function Assert-True {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    $script:assertionCount++
    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

function Assert-Equal {
    param(
        $Expected,
        $Actual,
        [Parameter(Mandatory)][string]$Message
    )

    Assert-True ($Expected -ceq $Actual) "$Message Expected '$Expected', found '$Actual'."
}

function New-ProjectCopy {
    param([Parameter(Mandatory)][string]$Name)

    $destination = Join-Path $testRoot $Name
    Copy-Item -LiteralPath $sourceProject -Destination $destination -Recurse
    return $destination
}

function Invoke-Tool {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][hashtable]$Parameters
    )

    try {
        $output = & $Path @Parameters *>&1 | Out-String
        return [pscustomobject]@{ Success = $true; Output = $output }
    }
    catch {
        return [pscustomobject]@{ Success = $false; Output = ($_ | Out-String) }
    }
}

function Get-SyncParameters {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Editor,
        [Parameter(Mandatory)][string]$Mode
    )

    return @{
        SourceProjectPath = $Repository
        EditorProjectPath = $Editor
        OutputPath = Join-Path $testRoot 'runtime-output'
        CodeOutputPath = Join-Path $testRoot 'code-output'
        Mode = $Mode
    }
}

function Get-SyncStatus {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Editor
    )

    $result = Invoke-Tool $syncScript (Get-SyncParameters $Repository $Editor 'Status')
    Assert-True $result.Success "Status failed: $($result.Output)"
    $jsonLine = @($result.Output -split "`r?`n" | Where-Object { $_.TrimStart().StartsWith('{') })[-1]
    return $jsonLine | ConvertFrom-Json
}

function Add-XmlMarker {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Marker
    )

    $text = [System.IO.File]::ReadAllText($Path)
    [System.IO.File]::WriteAllText($Path, $text.TrimEnd() + "`n<!-- $Marker -->`n", $utf8NoBom)
}

function Replace-Text {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Old,
        [Parameter(Mandatory)][string]$New
    )

    $text = [System.IO.File]::ReadAllText($Path)
    if (-not $text.Contains($Old)) {
        throw "Test fixture text was not found in '$Path': $Old"
    }
    [System.IO.File]::WriteAllText($Path, $text.Replace($Old, $New), $utf8NoBom)
}

function Get-SentinelState {
    param([Parameter(Mandatory)][string]$Editor)

    $paths = @(
        (Join-Path $Editor 'plugins/agent-bridge/sentinel.txt'),
        (Join-Path $Editor 'plugins/unknown-plugin/sentinel.txt'),
        (Join-Path $Editor '.agent/sentinel.txt')
    )
    return @($paths | ForEach-Object {
        [pscustomobject]@{
            path = $_
            exists = Test-Path -LiteralPath $_ -PathType Leaf
            hash = if (Test-Path -LiteralPath $_ -PathType Leaf) { (Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash } else { $null }
        }
    })
}

function Assert-SentinelState {
    param(
        [Parameter(Mandatory)]$Expected,
        [Parameter(Mandatory)][string]$Editor,
        [Parameter(Mandatory)][string]$Message
    )

    $actual = Get-SentinelState $Editor
    Assert-Equal ($Expected | ConvertTo-Json -Compress) ($actual | ConvertTo-Json -Compress) $Message
}

function Invoke-LintFailureCase {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Mutate,
        [Parameter(Mandatory)][string]$ExpectedMessage
    )

    $project = New-ProjectCopy $Name
    & $Mutate $project
    $result = Invoke-Tool $lintScript @{
        ProjectPath = $project
        ManifestPath = Join-Path $project 'generated/TestManifest.json'
    }
    Assert-True (-not $result.Success) "$Name should fail lint."
    Assert-True $result.Output.Contains($ExpectedMessage) "$Name did not report '$ExpectedMessage': $($result.Output)"
}

[System.IO.Directory]::CreateDirectory($testRoot) | Out-Null
try {
    $repository = New-ProjectCopy 'sync-repository'
    $editor = New-ProjectCopy 'sync-editor'
    $mainRelativePath = 'assets/Package1/MainView.xml'
    $repositoryMain = Join-Path $repository $mainRelativePath
    $editorMain = Join-Path $editor $mainRelativePath

    $status = Get-SyncStatus $repository $editor
    Assert-Equal 'Equal' $status.state 'Identical projects should compare equal without state.'

    Add-XmlMarker $editorMain 'editor-initial-change'
    foreach ($sentinelPath in @(
        (Join-Path $editor 'plugins/agent-bridge/sentinel.txt'),
        (Join-Path $editor 'plugins/unknown-plugin/sentinel.txt'),
        (Join-Path $editor '.agent/sentinel.txt')
    )) {
        [System.IO.Directory]::CreateDirectory((Split-Path -Parent $sentinelPath)) | Out-Null
        [System.IO.File]::WriteAllText($sentinelPath, 'preserve-me', $utf8NoBom)
    }
    $sentinelsBefore = Get-SentinelState $editor
    $status = Get-SyncStatus $repository $editor
    Assert-SentinelState $sentinelsBefore $editor 'Status changed plugin or .agent sentinel.'
    Assert-Equal 'UninitializedDifferent' $status.state 'Different projects without state must require initialization.'

    $repositoryBefore = [System.IO.File]::ReadAllText($repositoryMain)
    $editorBefore = [System.IO.File]::ReadAllText($editorMain)
    $result = Invoke-Tool $syncScript (Get-SyncParameters $repository $editor 'ToEditor')
    Assert-True (-not $result.Success) 'Uninitialized ToEditor must fail without -Initialize.'
    Assert-Equal $repositoryBefore ([System.IO.File]::ReadAllText($repositoryMain)) 'Rejected initialization must not change repository XML.'
    Assert-Equal $editorBefore ([System.IO.File]::ReadAllText($editorMain)) 'Rejected initialization must not change Editor XML.'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $editor '.gdk-sync-state.json'))) 'Rejected initialization must not create sync state.'

    $parameters = Get-SyncParameters $repository $editor 'FromEditor'
    $parameters.Initialize = $true
    $result = Invoke-Tool $syncScript $parameters
    Assert-True $result.Success "FromEditor initialization failed: $($result.Output)"
    Assert-SentinelState $sentinelsBefore $editor 'FromEditor initialization changed plugin or .agent sentinel.'
    Assert-Equal 'Equal' (Get-SyncStatus $repository $editor).state 'FromEditor initialization must establish common state.'
    Assert-True ([System.IO.File]::ReadAllText($repositoryMain)).Contains('editor-initial-change') 'Editor XML was not imported.'

    $repositoryPublish = Get-Content -Raw -LiteralPath (Join-Path $repository 'settings/Publish.json') | ConvertFrom-Json
    $editorPublish = Get-Content -Raw -LiteralPath (Join-Path $editor 'settings/Publish.json') | ConvertFrom-Json
    Assert-True (-not [System.IO.Path]::IsPathRooted([string]$repositoryPublish.path)) 'Repository publish path must be relative.'
    Assert-True (-not [System.IO.Path]::IsPathRooted([string]$repositoryPublish.codeGeneration.codePath)) 'Repository code path must be relative.'
    Assert-True ([System.IO.Path]::IsPathRooted([string]$editorPublish.path)) 'Editor publish path must be absolute.'
    Assert-True ([System.IO.Path]::IsPathRooted([string]$editorPublish.codeGeneration.codePath)) 'Editor code path must be absolute.'
    Assert-True ($editorPublish.codeGeneration.allowGenCode -and $editorPublish.codeGeneration.getMemberByName) 'Editor binding generation must stay enabled.'

    Add-XmlMarker $repositoryMain 'repository-only-change'
    Assert-Equal 'RepositoryChanged' (Get-SyncStatus $repository $editor).state 'Repository-only changes must be detected.'
    $repositoryOnlyHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $repositoryMain).Hash
    $editorBeforeWrongDirection = (Get-FileHash -Algorithm SHA256 -LiteralPath $editorMain).Hash
    $result = Invoke-Tool $syncScript (Get-SyncParameters $repository $editor 'FromEditor')
    Assert-True (-not $result.Success) 'Repository-only changes must reject FromEditor.'
    Assert-Equal $repositoryOnlyHash (Get-FileHash -Algorithm SHA256 -LiteralPath $repositoryMain).Hash 'Rejected FromEditor changed repository XML.'
    Assert-Equal $editorBeforeWrongDirection (Get-FileHash -Algorithm SHA256 -LiteralPath $editorMain).Hash 'Rejected FromEditor changed Editor XML.'
    $result = Invoke-Tool $syncScript (Get-SyncParameters $repository $editor 'ToEditor')
    Assert-True $result.Success "ToEditor failed: $($result.Output)"
    Assert-SentinelState $sentinelsBefore $editor 'ToEditor changed plugin or .agent sentinel.'
    Assert-Equal 'Equal' (Get-SyncStatus $repository $editor).state 'ToEditor must converge.'

    Add-XmlMarker $editorMain 'editor-only-change'
    Assert-Equal 'EditorChanged' (Get-SyncStatus $repository $editor).state 'Editor-only changes must be detected.'
    $repositoryBeforeWrongDirection = (Get-FileHash -Algorithm SHA256 -LiteralPath $repositoryMain).Hash
    $editorOnlyHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $editorMain).Hash
    $result = Invoke-Tool $syncScript (Get-SyncParameters $repository $editor 'ToEditor')
    Assert-True (-not $result.Success) 'Editor-only changes must reject ToEditor.'
    Assert-Equal $repositoryBeforeWrongDirection (Get-FileHash -Algorithm SHA256 -LiteralPath $repositoryMain).Hash 'Rejected ToEditor changed repository XML.'
    Assert-Equal $editorOnlyHash (Get-FileHash -Algorithm SHA256 -LiteralPath $editorMain).Hash 'Rejected ToEditor changed Editor XML.'
    $result = Invoke-Tool $syncScript (Get-SyncParameters $repository $editor 'FromEditor')
    Assert-True $result.Success "FromEditor failed: $($result.Output)"
    Assert-SentinelState $sentinelsBefore $editor 'FromEditor changed plugin or .agent sentinel.'
    Assert-Equal 'Equal' (Get-SyncStatus $repository $editor).state 'FromEditor must converge.'

    Add-XmlMarker $repositoryMain 'repository-conflict-change'
    Add-XmlMarker $editorMain 'editor-conflict-change'
    Assert-Equal 'Conflict' (Get-SyncStatus $repository $editor).state 'Two-sided changes must be conflicts.'
    $repositoryConflictHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $repositoryMain).Hash
    $editorConflictHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $editorMain).Hash
    $result = Invoke-Tool $syncScript (Get-SyncParameters $repository $editor 'ToEditor')
    Assert-True (-not $result.Success) 'Conflict must reject ToEditor.'
    Assert-Equal $repositoryConflictHash (Get-FileHash -Algorithm SHA256 -LiteralPath $repositoryMain).Hash 'Conflict changed repository XML.'
    Assert-Equal $editorConflictHash (Get-FileHash -Algorithm SHA256 -LiteralPath $editorMain).Hash 'Conflict changed Editor XML.'

    $whatIfRepository = New-ProjectCopy 'whatif-repository'
    $whatIfEditor = New-ProjectCopy 'whatif-editor'
    foreach ($sentinelPath in @(
        (Join-Path $whatIfEditor 'plugins/agent-bridge/sentinel.txt'),
        (Join-Path $whatIfEditor 'plugins/unknown-plugin/sentinel.txt'),
        (Join-Path $whatIfEditor '.agent/sentinel.txt')
    )) {
        [System.IO.Directory]::CreateDirectory((Split-Path -Parent $sentinelPath)) | Out-Null
        [System.IO.File]::WriteAllText($sentinelPath, 'preserve-me', $utf8NoBom)
    }
    $whatIfSentinels = Get-SentinelState $whatIfEditor
    $result = Invoke-Tool $syncScript (Get-SyncParameters $whatIfRepository $whatIfEditor 'ToEditor')
    Assert-True $result.Success 'Equal ToEditor should establish state.'
    Assert-SentinelState $whatIfSentinels $whatIfEditor 'ToEditor changed plugin or .agent sentinel.'
    $whatIfRepositoryMain = Join-Path $whatIfRepository $mainRelativePath
    $whatIfEditorMain = Join-Path $whatIfEditor $mainRelativePath
    Add-XmlMarker $whatIfRepositoryMain 'what-if-change'
    $editorBeforeWhatIf = (Get-FileHash -Algorithm SHA256 -LiteralPath $whatIfEditorMain).Hash
    $statePath = Join-Path $whatIfEditor '.gdk-sync-state.json'
    $stateBeforeWhatIf = (Get-FileHash -Algorithm SHA256 -LiteralPath $statePath).Hash
    $parameters = Get-SyncParameters $whatIfRepository $whatIfEditor 'ToEditor'
    $parameters.WhatIf = $true
    $result = Invoke-Tool $syncScript $parameters
    Assert-True $result.Success "ToEditor -WhatIf failed: $($result.Output)"
    Assert-SentinelState $whatIfSentinels $whatIfEditor '-WhatIf changed plugin or .agent sentinel.'
    Assert-Equal $editorBeforeWhatIf (Get-FileHash -Algorithm SHA256 -LiteralPath $whatIfEditorMain).Hash '-WhatIf changed Editor XML.'
    Assert-Equal $stateBeforeWhatIf (Get-FileHash -Algorithm SHA256 -LiteralPath $statePath).Hash '-WhatIf changed sync state.'

    $preflightRepository = New-ProjectCopy 'preflight-repository'
    $preflightEditor = New-ProjectCopy 'preflight-editor'
    $preflightRepositoryMain = Join-Path $preflightRepository $mainRelativePath
    $preflightEditorMain = Join-Path $preflightEditor $mainRelativePath
    Add-XmlMarker $preflightRepositoryMain 'preflight-change'
    Remove-Item -LiteralPath (Join-Path $preflightRepository 'settings/GDK.json') -Force
    $preflightEditorHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $preflightEditorMain).Hash
    $parameters = Get-SyncParameters $preflightRepository $preflightEditor 'ToEditor'
    $parameters.Initialize = $true
    $result = Invoke-Tool $syncScript $parameters
    Assert-True (-not $result.Success -and $result.Output.Contains('contract is missing')) 'Missing support files must fail during preflight.'
    Assert-Equal $preflightEditorHash (Get-FileHash -Algorithm SHA256 -LiteralPath $preflightEditorMain).Hash 'Preflight failure changed Editor XML.'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $preflightEditor '.gdk-sync-state.json'))) 'Preflight failure created sync state.'

    $invalidXmlRepository = New-ProjectCopy 'invalid-xml-repository'
    $invalidXmlEditor = New-ProjectCopy 'invalid-xml-editor'
    $invalidXmlRepositoryMain = Join-Path $invalidXmlRepository $mainRelativePath
    $invalidXmlEditorMain = Join-Path $invalidXmlEditor $mainRelativePath
    Replace-Text $invalidXmlRepositoryMain 'src="btn01"' 'src="missing"'
    $invalidXmlEditorHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $invalidXmlEditorMain).Hash
    $parameters = Get-SyncParameters $invalidXmlRepository $invalidXmlEditor 'ToEditor'
    $parameters.Initialize = $true
    $result = Invoke-Tool $syncScript $parameters
    Assert-True (-not $result.Success -and $result.Output.Contains('failed preflight')) "Invalid incoming XML must fail during preflight: $($result.Output)"
    Assert-Equal $invalidXmlEditorHash (Get-FileHash -Algorithm SHA256 -LiteralPath $invalidXmlEditorMain).Hash 'Invalid XML preflight changed Editor XML.'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $invalidXmlEditor '.gdk-sync-state.json'))) 'Invalid XML preflight created sync state.'

    $lintProject = New-ProjectCopy 'lint-good'
    $manifestPath = Join-Path $lintProject 'generated/TestManifest.json'
    $result = Invoke-Tool $lintScript @{ ProjectPath = $lintProject; ManifestPath = $manifestPath }
    Assert-True $result.Success "Valid project lint failed: $($result.Output)"
    $manifestHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestPath).Hash
    $result = Invoke-Tool $lintScript @{ ProjectPath = $lintProject; ManifestPath = $manifestPath }
    Assert-True $result.Success 'Second manifest generation failed.'
    Assert-Equal $manifestHash (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestPath).Hash 'Manifest generation is not byte deterministic.'
    $result = Invoke-Tool $lintScript @{ ProjectPath = $lintProject; ManifestPath = $manifestPath; Check = $true }
    Assert-True $result.Success "Manifest -Check failed: $($result.Output)"
    $manifestText = [System.IO.File]::ReadAllText($manifestPath).Replace("`n", "`r`n")
    [System.IO.File]::WriteAllText($manifestPath, $manifestText, $utf8NoBom)
    $result = Invoke-Tool $lintScript @{ ProjectPath = $lintProject; ManifestPath = $manifestPath; Check = $true }
    Assert-True (-not $result.Success -and $result.Output.Contains('manifest is stale')) 'Manifest -Check accepted non-canonical line endings.'
    $result = Invoke-Tool $lintScript @{ ProjectPath = $lintProject; ManifestPath = $manifestPath }
    Assert-True $result.Success 'Manifest regeneration after byte drift failed.'
    $manifestBytes = [System.IO.File]::ReadAllBytes($manifestPath)
    $bomBytes = [byte[]]::new($manifestBytes.Length + 3)
    $bomBytes[0] = 0xEF
    $bomBytes[1] = 0xBB
    $bomBytes[2] = 0xBF
    [System.Array]::Copy($manifestBytes, 0, $bomBytes, 3, $manifestBytes.Length)
    [System.IO.File]::WriteAllBytes($manifestPath, $bomBytes)
    $result = Invoke-Tool $lintScript @{ ProjectPath = $lintProject; ManifestPath = $manifestPath; Check = $true }
    Assert-True (-not $result.Success -and $result.Output.Contains('manifest is stale')) 'Manifest -Check accepted a UTF-8 BOM.'
    $result = Invoke-Tool $lintScript @{ ProjectPath = $lintProject; ManifestPath = $manifestPath }
    Assert-True $result.Success 'Manifest regeneration after BOM drift failed.'
    Add-XmlMarker (Join-Path $lintProject $mainRelativePath) 'stale-manifest-change'
    $result = Invoke-Tool $lintScript @{ ProjectPath = $lintProject; ManifestPath = $manifestPath; Check = $true }
    Assert-True (-not $result.Success -and $result.Output.Contains('manifest is stale')) 'Stale manifest was not rejected.'

    Invoke-LintFailureCase 'lint-missing-reference' {
        param($project)
        Replace-Text (Join-Path $project 'assets/Package1/MainView.xml') 'src="btn01"' 'src="missing"'
    } 'Unknown resource reference'
    Invoke-LintFailureCase 'lint-package-without-source' {
        param($project)
        Replace-Text (Join-Path $project 'assets/Package1/MainView.xml') 'src="btn01"' 'pkg="oozeu71h"'
    } 'requires src'
    Invoke-LintFailureCase 'lint-duplicate-id' {
        param($project)
        Replace-Text (Join-Path $project 'assets/Package1/MainView.xml') 'id="statuslabel"' 'id="status"'
    } 'Duplicate display member id'
    Invoke-LintFailureCase 'lint-duplicate-name' {
        param($project)
        Replace-Text (Join-Path $project 'assets/Package1/MainView.xml') 'name="statusLabel"' 'name="statusText"'
    } 'Duplicate display member name'
    Invoke-LintFailureCase 'lint-duplicate-resource-name' {
        param($project)
        Replace-Text (Join-Path $project 'assets/Package1/package.xml') 'name="MainView.xml"' 'name="RefreshButton.xml"'
    } 'Duplicate resource name'
    Invoke-LintFailureCase 'lint-controller' {
        param($project)
        Replace-Text (Join-Path $project 'assets/Package1/RefreshButton.xml') 'gearDisplay controller="button"' 'gearDisplay controller="missing"'
    } 'Unknown controller'
    Invoke-LintFailureCase 'lint-relation' {
        param($project)
        Replace-Text (Join-Path $project 'assets/Package1/MainView.xml') 'relation target=""' 'relation target="missing"'
    } 'Unknown relation target'
    Invoke-LintFailureCase 'lint-contract' {
        param($project)
        Replace-Text (Join-Path $project 'settings/GDK.json') '"refreshButton"' '"renamedRefreshButton"'
    } 'Required member'
    Invoke-LintFailureCase 'lint-component-path-escape' {
        param($project)
        Replace-Text (Join-Path $project 'assets/Package1/package.xml') 'name="RefreshButton.xml" path="/"' 'name="outside.xml" path="../../../"'
        $outsideFile = Join-Path (Split-Path -Parent $project) 'outside.xml'
        Copy-Item -LiteralPath (Join-Path $project 'assets/Package1/RefreshButton.xml') -Destination $outsideFile
    } 'escapes the FairyGUI project root'
    Invoke-LintFailureCase 'lint-package-cycle' {
        param($project)
        $package1View = Join-Path $project 'assets/Package1/MainView.xml'
        Replace-Text $package1View '</displayList>' '<component id="other" name="other" pkg="pkg2" src="c2"/></displayList>'
        $package2 = Join-Path $project 'assets/Package2'
        [System.IO.Directory]::CreateDirectory($package2) | Out-Null
        [System.IO.File]::WriteAllText(
            (Join-Path $package2 'package.xml'),
            "<?xml version=`"1.0`" encoding=`"utf-8`"?>`n<packageDescription id=`"pkg2`"><resources><component id=`"c2`" name=`"Other.xml`" path=`"/`"/></resources><publish name=`"Package2`"/></packageDescription>`n",
            $utf8NoBom)
        [System.IO.File]::WriteAllText(
            (Join-Path $package2 'Other.xml'),
            "<?xml version=`"1.0`" encoding=`"utf-8`"?>`n<component size=`"10,10`"><displayList><component id=`"back`" name=`"back`" pkg=`"oozeu71h`" src=`"btn01`"/></displayList></component>`n",
            $utf8NoBom)
    } 'Package dependency cycle'

    $publishRepository = New-ProjectCopy 'publish-repository'
    $publishEditor = New-ProjectCopy 'publish-editor'
    $publishOutput = Join-Path $testRoot 'Assets'
    $fakeAgent = Join-Path $testRoot 'fake-agent.cmd'
    $fakeLog = Join-Path $testRoot 'fake-agent.log'
    $fakeScript = @'
@echo off
set "args=%*"
if defined FGUI_FAKE_LOG echo %args%>>"%FGUI_FAKE_LOG%"
if /I "%~5"=="status" goto status
if /I "%~5"=="ping" goto ping
if /I "%~5"=="project" goto project
if /I "%~5"=="packages" goto packages
if /I "%~5"=="publish" goto publish
echo {"success":false,"message":"unexpected fake command"}
exit /b 2
:status
if /I "%FGUI_FAKE_MODE%"=="nonzero" (echo fake bridge failure 1>&2 & exit /b 7)
if /I "%FGUI_FAKE_MODE%"=="invalid-json" (echo not-json & exit /b 0)
if /I "%FGUI_FAKE_MODE%"=="offline" (echo {"online":false,"versionMatch":true,"status":{"protocolVersion":"1.0","capabilities":["publish"]}} & exit /b 0)
if /I "%FGUI_FAKE_MODE%"=="version-mismatch" (echo {"online":true,"versionMatch":false,"status":{"protocolVersion":"1.0","capabilities":["publish"]}} & exit /b 0)
if /I "%FGUI_FAKE_MODE%"=="protocol-mismatch" (echo {"online":true,"versionMatch":true,"status":{"protocolVersion":"2.0","capabilities":["publish"]}} & exit /b 0)
if /I "%FGUI_FAKE_MODE%"=="capability-missing" (echo {"online":true,"versionMatch":true,"status":{"protocolVersion":"1.0","capabilities":["ping"]}} & exit /b 0)
echo {"online":true,"versionMatch":true,"status":{"protocolVersion":"1.0","capabilities":["publish"]}}
exit /b 0
:ping
echo {"ok":true,"result":{"pong":true}}
exit /b 0
:project
echo {"ok":true,"result":{"basePath":"%FGUI_FAKE_PROJECT:\=/%"}}
exit /b 0
:packages
if /I "%FGUI_FAKE_MODE%"=="package-missing" (echo {"ok":true,"result":[{"name":"OtherPackage"}]} & exit /b 0)
echo {"ok":true,"result":[{"name":"Package1"}]}
exit /b 0
:publish
if /I "%FGUI_FAKE_MODE%"=="no-artifact" (echo {"ok":true,"result":{"success":true,"packages":[{"name":"Package1"}]}} & exit /b 0)
if not exist "%FGUI_FAKE_OUTPUT%" mkdir "%FGUI_FAKE_OUTPUT%"
echo bytes>"%FGUI_FAKE_OUTPUT%\Package1_fui.bytes"
echo atlas>"%FGUI_FAKE_OUTPUT%\Package1_atlas0.png"
echo {"ok":true,"result":{"success":true,"packages":[{"name":"Package1"}]}}
exit /b 0
'@
    [System.IO.File]::WriteAllText($fakeAgent, $fakeScript, [System.Text.UTF8Encoding]::new($false))
    $null = & $lintScript -ProjectPath $publishRepository -ManifestPath (Join-Path $publishRepository 'generated/GDKFairyManifest.json')
    $oldFakeProject = $env:FGUI_FAKE_PROJECT
    $oldFakeOutput = $env:FGUI_FAKE_OUTPUT
    $oldFakeLog = $env:FGUI_FAKE_LOG
    $oldFakeMode = $env:FGUI_FAKE_MODE
    $oldAgentExecutable = $env:FGUI_AGENT_EXE
    $oldPath = $env:PATH
    try {
        $env:FGUI_FAKE_PROJECT = $publishEditor.Replace('\', '/')
        $env:FGUI_FAKE_OUTPUT = $publishOutput
        $env:FGUI_FAKE_LOG = $fakeLog
        $publishResult = Invoke-Tool $publishScript @{
            AgentExecutable = $fakeAgent
            SourceProjectPath = $publishRepository
            EditorProjectPath = $publishEditor
            OutputPath = $publishOutput
            PackageName = 'Package1'
            TimeoutSeconds = 10
        }
        Assert-True $publishResult.Success "Fake Wilson publish failed: $($publishResult.Output)"
        $publishJson = $publishResult.Output | ConvertFrom-Json
        Assert-True $publishJson.success 'Fake Wilson publish did not return success evidence.'
        Assert-True ($publishJson.artifactAfter.exists -and $publishJson.artifactAfter.size -gt 0) 'Fake Wilson publish artifact evidence is invalid.'
        Assert-True ($publishJson.runtimeManifest.exists -and $publishJson.runtimeManifest.size -gt 0) 'Runtime manifest evidence is invalid.'
        $publishedManifest = Join-Path $publishOutput 'GDKFairyManifest.json'
        $publishedManifestJson = Get-Content -Raw -LiteralPath $publishedManifest | ConvertFrom-Json
        Assert-Equal 2 $publishedManifestJson.schemaVersion 'Published runtime manifest schema is wrong.'
        Assert-Equal 'Assets/Package1_fui.bytes' $publishedManifestJson.packages[0].descriptorAsset 'Runtime package descriptor path is not explicit.'
        Assert-Equal 1 @($publishedManifestJson.packages[0].runtimeAssets).Count 'Runtime external asset list is wrong.'
        Assert-Equal 'Assets/Package1_atlas0.png' $publishedManifestJson.packages[0].runtimeAssets[0].path 'Runtime external asset path is wrong.'
        $runtimeCheck = Invoke-Tool $runtimeManifestScript @{
            SourceManifestPath = (Join-Path $publishRepository 'generated/GDKFairyManifest.json')
            OutputPath = $publishOutput
            ManifestPath = $publishedManifest
            Check = $true
        }
        Assert-True $runtimeCheck.Success "Runtime manifest -Check failed: $($runtimeCheck.Output)"
        $loggedArgs = [System.IO.File]::ReadAllText($fakeLog)
        Assert-True $loggedArgs.Contains('publish --scope packages --package Package1 --publish-timeout 10') 'Publish arguments were not exact.'

        $env:FGUI_AGENT_EXE = $fakeAgent
        $env:FGUI_FAKE_LOG = $null
        $envResult = Invoke-Tool $publishScript @{
            SourceProjectPath = $publishRepository
            EditorProjectPath = $publishEditor
            OutputPath = $publishOutput
            PackageName = 'Package1'
            TimeoutSeconds = 10
        }
        Assert-True $envResult.Success "FGUI_AGENT_EXE discovery failed: $($envResult.Output)"

        Remove-Item Env:FGUI_AGENT_EXE -ErrorAction SilentlyContinue
        Copy-Item -LiteralPath $fakeAgent -Destination (Join-Path $testRoot 'fgui-agent.cmd')
        $env:PATH = $testRoot + [System.IO.Path]::PathSeparator + $oldPath
        $pathResult = Invoke-Tool $publishScript @{
            SourceProjectPath = $publishRepository
            EditorProjectPath = $publishEditor
            OutputPath = $publishOutput
            PackageName = 'Package1'
            TimeoutSeconds = 10
        }
        Assert-True $pathResult.Success "PATH discovery failed: $($pathResult.Output)"

        $env:FGUI_FAKE_MODE = 'invalid-json'
        $invalidJson = Invoke-Tool $publishScript @{
            AgentExecutable = $fakeAgent
            SourceProjectPath = $publishRepository
            EditorProjectPath = $publishEditor
            OutputPath = $publishOutput
            PackageName = 'Package1'
            TimeoutSeconds = 10
        }
        Assert-True (-not $invalidJson.Success -and $invalidJson.Output.Contains('invalid JSON')) 'Invalid CLI JSON was not rejected.'

        foreach ($statusCase in @(
            @{ Mode = 'nonzero'; Message = 'exit code 7' },
            @{ Mode = 'offline'; Message = 'offline or stale' },
            @{ Mode = 'version-mismatch'; Message = 'version mismatch' },
            @{ Mode = 'protocol-mismatch'; Message = 'protocol must be 1.x' },
            @{ Mode = 'capability-missing'; Message = 'publish capability' }
        )) {
            $env:FGUI_FAKE_MODE = $statusCase.Mode
            $statusFailure = Invoke-Tool $publishScript @{
                AgentExecutable = $fakeAgent
                SourceProjectPath = $publishRepository
                EditorProjectPath = $publishEditor
                OutputPath = $publishOutput
                PackageName = 'Package1'
                TimeoutSeconds = 10
            }
            Assert-True (-not $statusFailure.Success -and $statusFailure.Output.Contains($statusCase.Message)) "Status failure '$($statusCase.Mode)' was not rejected."
        }

        $env:FGUI_FAKE_MODE = 'package-missing'
        $missingPackage = Invoke-Tool $publishScript @{
            AgentExecutable = $fakeAgent
            SourceProjectPath = $publishRepository
            EditorProjectPath = $publishEditor
            OutputPath = $publishOutput
            PackageName = 'Package1'
            TimeoutSeconds = 10
        }
        Assert-True (-not $missingPackage.Success -and $missingPackage.Output.Contains('does not contain exactly one package')) 'Missing package was not rejected.'

        $env:FGUI_FAKE_MODE = 'no-artifact'
        Remove-Item -LiteralPath (Join-Path $publishOutput 'Package1_fui.bytes') -Force -ErrorAction SilentlyContinue
        $missingArtifact = Invoke-Tool $publishScript @{
            AgentExecutable = $fakeAgent
            SourceProjectPath = $publishRepository
            EditorProjectPath = $publishEditor
            OutputPath = $publishOutput
            PackageName = 'Package1'
            TimeoutSeconds = 10
        }
        Assert-True (-not $missingArtifact.Success -and $missingArtifact.Output.Contains('missing or empty')) 'Missing publish artifact was not rejected.'

        $env:FGUI_FAKE_MODE = $null
        Clear-Content -LiteralPath $fakeLog
        $divergedEditor = New-ProjectCopy 'publish-diverged-editor'
        Add-XmlMarker (Join-Path $divergedEditor $mainRelativePath) 'publish-sync-gate'
        $syncGate = Invoke-Tool $publishScript @{
            AgentExecutable = $fakeAgent
            SourceProjectPath = $publishRepository
            EditorProjectPath = $divergedEditor
            OutputPath = $publishOutput
            PackageName = 'Package1'
            TimeoutSeconds = 10
        }
        Assert-True (-not $syncGate.Success -and $syncGate.Output.Contains('must be Equal')) 'Non-Equal sync state did not block publish.'
        Assert-True ([string]::IsNullOrWhiteSpace([System.IO.File]::ReadAllText($fakeLog))) 'Sync gate invoked the external CLI.'
    }
    finally {
        $env:FGUI_FAKE_PROJECT = $oldFakeProject
        $env:FGUI_FAKE_OUTPUT = $oldFakeOutput
        $env:FGUI_FAKE_LOG = $oldFakeLog
        $env:FGUI_FAKE_MODE = $oldFakeMode
        $env:FGUI_AGENT_EXE = $oldAgentExecutable
        $env:PATH = $oldPath
    }

    $missingResult = Invoke-Tool $publishScript @{
        AgentExecutable = (Join-Path $testRoot 'missing-agent.exe')
        SourceProjectPath = $publishRepository
        EditorProjectPath = $publishEditor
        OutputPath = $publishOutput
        PackageName = 'Package1'
    }
    Assert-True (-not $missingResult.Success -and $missingResult.Output.Contains('does not exist')) 'Missing CLI was not rejected clearly.'

    $pathEscape = Invoke-Tool $publishScript @{
        AgentExecutable = $fakeAgent
        SourceProjectPath = $publishRepository
        EditorProjectPath = $publishEditor
        OutputPath = $publishOutput
        PackageName = '..\escape'
    }
    Assert-True (-not $pathEscape.Success -and $pathEscape.Output.Contains('escapes output root')) 'Artifact path escape was not rejected.'

    # Descriptor generation tests
    $descriptorSourceRows = Get-Content -Raw -LiteralPath $lubanUiFormData | ConvertFrom-Json -NoEnumerate

    function New-DescriptorFixture {
        param([Parameter(Mandatory)][string]$Name)

        $project = New-ProjectCopy $Name
        $lubanPath = Join-Path $testRoot ($Name + '-dtuiform.json')
        $rows = @($descriptorSourceRows | Where-Object { [string]$_.CSName -cne 'FairyDemoForm' })
        $rows += [pscustomobject][ordered]@{
            Id = 103
            CSName = 'FairyDemoForm'
            Desc = 'FairyGUI descriptor fixture'
            AssetName = 'Hot/FairyDemoForm'
            UIGroupName = 'Default'
            AllowMultiInstance = $false
            PauseCoveredUIForm = $true
            PackageName = 'Package1'
            ComponentName = 'MainView'
        }
        [System.IO.File]::WriteAllText(
            $lubanPath,
            (($rows | ConvertTo-Json -Depth 100) + "`n"),
            $utf8NoBom)

        return [pscustomobject]@{
            Project = $project
            Manifest = Join-Path $project 'generated/GDKFairyManifest.json'
            Luban = $lubanPath
            Output = Join-Path $testRoot ($Name + '-output')
        }
    }

    function Get-DescriptorRows {
        param([Parameter(Mandatory)]$Fixture)
        return Get-Content -Raw -LiteralPath $Fixture.Luban | ConvertFrom-Json -NoEnumerate
    }

    function Set-DescriptorRows {
        param(
            [Parameter(Mandatory)]$Fixture,
            [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Rows
        )
        [System.IO.File]::WriteAllText(
            $Fixture.Luban,
            (($Rows | ConvertTo-Json -Depth 100) + "`n"),
            $utf8NoBom)
    }

    function Invoke-Descriptor {
        param(
            [Parameter(Mandatory)]$Fixture,
            [switch]$Check
        )

        return Invoke-Tool $descriptorScript @{
            SourceProjectPath = $Fixture.Project
            ManifestPath = $Fixture.Manifest
            LubanUIFormPath = $Fixture.Luban
            OutputPath = $Fixture.Output
            Check = $Check
        }
    }

    function Assert-DescriptorFailure {
        param(
            [Parameter(Mandatory)][string]$Name,
            [Parameter(Mandatory)][scriptblock]$Mutate,
            [Parameter(Mandatory)][string]$ExpectedMessage
        )

        $fixture = New-DescriptorFixture $Name
        & $Mutate $fixture
        $result = Invoke-Descriptor $fixture
        Assert-True (-not $result.Success) "$Name should fail descriptor generation."
        Assert-True $result.Output.Contains($ExpectedMessage) "$Name did not report '$ExpectedMessage': $($result.Output)"
    }

    $descriptorFixture = New-DescriptorFixture 'descriptor-good'
    $descriptorResult = Invoke-Descriptor $descriptorFixture
    Assert-True $descriptorResult.Success "Valid descriptor generation failed: $($descriptorResult.Output)"
    $descriptorFile = Join-Path $descriptorFixture.Output 'FairyDemoForm.json'
    Assert-True (Test-Path -LiteralPath $descriptorFile -PathType Leaf) 'Descriptor file was not written.'
    $descriptorJson = Get-Content -Raw -LiteralPath $descriptorFile | ConvertFrom-Json
    Assert-Equal 103 $descriptorJson.uiId 'Descriptor uiId is not sourced from Luban.'
    Assert-Equal 'FairyDemoForm' $descriptorJson.csName 'Descriptor CSName is not sourced from Luban.'
    Assert-Equal 'Hot/FairyDemoForm' $descriptorJson.uiAssetName 'Descriptor AssetName is not sourced from Luban.'
    Assert-Equal 'Default' $descriptorJson.uiGroupName 'Descriptor UIGroupName is not sourced from Luban.'
    Assert-Equal $false $descriptorJson.allowMultiInstance 'Descriptor AllowMultiInstance is not sourced from Luban.'
    Assert-Equal $true $descriptorJson.pauseCoveredUIForm 'Descriptor PauseCoveredUIForm is not sourced from Luban.'
    Assert-Equal 'oozeu71h' $descriptorJson.packageId 'Descriptor packageId is wrong.'
    Assert-Equal '7xe70' $descriptorJson.componentId 'Descriptor componentId is wrong.'
    Assert-Equal 'Game.Hot.FairyGUI.Package1.UIMainView' $descriptorJson.bindingType 'Descriptor bindingType is wrong.'
    Assert-Equal 'Package1' $descriptorJson.packageName 'Descriptor packageName is not sourced from Luban.'
    Assert-Equal 'MainView' $descriptorJson.componentName 'Descriptor componentName is not sourced from Luban.'

    $firstDescriptorBytes = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($descriptorFile))
    $descriptorResult = Invoke-Descriptor $descriptorFixture
    Assert-True $descriptorResult.Success 'Second descriptor generation failed.'
    $secondDescriptorBytes = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($descriptorFile))
    Assert-Equal $firstDescriptorBytes $secondDescriptorBytes 'Second descriptor generation was not byte equivalent.'
    $descriptorResult = Invoke-Descriptor $descriptorFixture -Check
    Assert-True $descriptorResult.Success "Descriptor -Check failed: $($descriptorResult.Output)"

    $descriptorText = [System.IO.File]::ReadAllText($descriptorFile).Replace("`n", "`r`n")
    [System.IO.File]::WriteAllText($descriptorFile, $descriptorText, $utf8NoBom)
    $descriptorResult = Invoke-Descriptor $descriptorFixture -Check
    Assert-True (-not $descriptorResult.Success -and $descriptorResult.Output.Contains('Descriptor is stale')) 'Descriptor -Check accepted non-canonical line endings.'

    $obsoleteFixture = New-DescriptorFixture 'descriptor-obsolete'
    $null = Invoke-Descriptor $obsoleteFixture
    [System.IO.File]::WriteAllText((Join-Path $obsoleteFixture.Output 'Obsolete.json'), "{}`n", $utf8NoBom)
    $obsoleteResult = Invoke-Descriptor $obsoleteFixture -Check
    Assert-True (-not $obsoleteResult.Success -and $obsoleteResult.Output.Contains('Obsolete descriptor file')) 'Descriptor -Check accepted an obsolete descriptor file.'

    $identityFixture = New-DescriptorFixture 'descriptor-identity-drift'
    $null = Invoke-Descriptor $identityFixture
    $identityRows = Get-DescriptorRows $identityFixture
    ($identityRows | Where-Object { $_.CSName -ceq 'FairyDemoForm' }).Id = 999
    Set-DescriptorRows $identityFixture $identityRows
    $identityResult = Invoke-Descriptor $identityFixture -Check
    Assert-True (-not $identityResult.Success -and $identityResult.Output.Contains('Descriptor is stale')) 'Descriptor -Check accepted Luban identity drift.'

    Assert-DescriptorFailure 'descriptor-duplicate-id' {
        param($fixture)
        $rows = Get-DescriptorRows $fixture
        $rows += [pscustomobject]@{ Id = 103; CSName = 'OtherForm'; AssetName = 'Hot/OtherForm'; UIGroupName = 'Default'; AllowMultiInstance = $false; PauseCoveredUIForm = $false }
        Set-DescriptorRows $fixture $rows
    } 'Duplicate Luban UI Id'
    Assert-DescriptorFailure 'descriptor-duplicate-cs-name' {
        param($fixture)
        $rows = Get-DescriptorRows $fixture
        $rows += [pscustomobject]@{ Id = 10003; CSName = 'FairyDemoForm'; AssetName = 'Hot/OtherForm'; UIGroupName = 'Default'; AllowMultiInstance = $false; PauseCoveredUIForm = $false }
        Set-DescriptorRows $fixture $rows
    } 'Duplicate Luban UI CSName'
    Assert-DescriptorFailure 'descriptor-duplicate-asset-name' {
        param($fixture)
        $rows = Get-DescriptorRows $fixture
        $rows += [pscustomobject]@{ Id = 10004; CSName = 'OtherForm'; AssetName = 'Hot/FairyDemoForm'; UIGroupName = 'Default'; AllowMultiInstance = $false; PauseCoveredUIForm = $false }
        Set-DescriptorRows $fixture $rows
    } 'Duplicate Luban UI AssetName'
    Assert-DescriptorFailure 'descriptor-basename-collision' {
        param($fixture)
        $rows = Get-DescriptorRows $fixture
        $rows += [pscustomobject]@{ Id = 10005; CSName = 'OtherFairyForm'; AssetName = 'Other/FairyDemoForm'; UIGroupName = 'Pop'; AllowMultiInstance = $true; PauseCoveredUIForm = $false; PackageName = 'Package1'; ComponentName = 'MainView' }
        Set-DescriptorRows $fixture $rows
    } 'Descriptor output basename collision'
    Assert-DescriptorFailure 'descriptor-unknown-package' {
        param($fixture)
        $rows = Get-DescriptorRows $fixture
        ($rows | Where-Object { $_.CSName -ceq 'FairyDemoForm' }).PackageName = 'MissingPackage'
        Set-DescriptorRows $fixture $rows
    } 'unknown package'
    Assert-DescriptorFailure 'descriptor-unknown-component' {
        param($fixture)
        $rows = Get-DescriptorRows $fixture
        ($rows | Where-Object { $_.CSName -ceq 'FairyDemoForm' }).ComponentName = 'MissingComponent'
        Set-DescriptorRows $fixture $rows
    } 'unknown component'
    Assert-DescriptorFailure 'descriptor-partial-package' {
        param($fixture)
        $rows = Get-DescriptorRows $fixture
        ($rows | Where-Object { $_.CSName -ceq 'FairyDemoForm' }).PackageName = ''
        Set-DescriptorRows $fixture $rows
    } 'must provide both PackageName and ComponentName'
    $summary = [pscustomobject][ordered]@{
        success = $true
        assertions = $script:assertionCount
    }
}
finally {
    $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
    $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if ($resolvedTestRoot.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedTestRoot)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}

$summary | ConvertTo-Json -Compress
