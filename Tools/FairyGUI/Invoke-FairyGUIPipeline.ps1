[CmdletBinding()]
param(
    [string]$RepoRoot = (Join-Path $PSScriptRoot '..\..'),
    [string]$UnityProject = 'Unity',
    [switch]$SkipLuban
)

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
$binDir = Join-Path $repoRoot 'Bin'
$toolExe = Join-Path $binDir 'Tool.exe'
$descriptorGenerator = Join-Path $PSCmdlet.MyInvocation.MyCommand.Path '..\Generate-FairyUIFormDescriptors.ps1'
$runtimeManifestGenerator = Join-Path $PSCmdlet.MyInvocation.MyCommand.Path '..\Generate-FairyRuntimeManifest.ps1'
$localizationGenerator = Join-Path $PSCmdlet.MyInvocation.MyCommand.Path '..\Generate-FairyLocalizationXml.ps1'
$unityProject = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $UnityProject))
$bridgeSession = Join-Path $repoRoot '.agents/skills/gdk-development-workflow/scripts/bridge_session.py'

if (-not $SkipLuban) {
    if (-not (Test-Path -LiteralPath $toolExe -PathType Leaf)) {
        throw "Tool.exe not found: $toolExe"
    }

    Push-Location $binDir
    try {
        & $toolExe --AppType=ExcelExporter --Console=1
        if ($LASTEXITCODE -ne 0) {
            throw "Luban ExcelExporter failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path -LiteralPath $descriptorGenerator -PathType Leaf)) {
    throw "Descriptor generator not found: $descriptorGenerator"
}
& $descriptorGenerator

if (-not (Test-Path -LiteralPath $runtimeManifestGenerator -PathType Leaf)) {
    throw "Runtime manifest generator not found: $runtimeManifestGenerator"
}
& $runtimeManifestGenerator

if (-not (Test-Path -LiteralPath $localizationGenerator -PathType Leaf)) {
    throw "Localization XML generator not found: $localizationGenerator"
}
& $localizationGenerator

if (-not (Test-Path -LiteralPath $bridgeSession -PathType Leaf)) {
    throw "Unity Agent Bridge session script not found: $bridgeSession"
}
if (-not (Test-Path -LiteralPath (Join-Path $unityProject 'Assets') -PathType Container)) {
    throw "Unity project not found: $unityProject"
}

$bridgeInput = @(
    '{"command":"list_commands","params":{}}',
    '{"command":"refresh","params":{}}',
    '{"action":"quit"}'
) -join "`n"
$bridgeInput | python $bridgeSession --project $unityProject --ack-contract
if ($LASTEXITCODE -ne 0) {
    throw "Unity Agent Bridge refresh failed with exit code $LASTEXITCODE."
}

[pscustomobject][ordered]@{
    success = $true
    repoRoot = $repoRoot
    lubanSkipped = [bool]$SkipLuban
    descriptorGenerator = $descriptorGenerator
    runtimeManifestGenerator = $runtimeManifestGenerator
    unityProject = $unityProject
} | ConvertTo-Json -Depth 100
