[CmdletBinding()]
param(
    [string]$AgentExecutable,
    [string]$SourceProjectPath = (Join-Path $PSScriptRoot '../../Design/FairyGUI/GDK_FGUI'),
    [string]$EditorProjectPath = 'D:\Unity\Project\GDK_FGUI',
    [string]$PackageName = 'Package1',
    [string]$OutputPath = (Join-Path $PSScriptRoot '../../Unity/Assets/Res/UI/FairyGUI'),
    [ValidateRange(1, 3600)]
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Resolve-FullPath {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Label)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "$Label does not exist: $fullPath"
    }
    return $fullPath
}

function Resolve-AgentExecutable {
    param([string]$ExplicitPath)

    $candidate = $ExplicitPath
    if ([string]::IsNullOrWhiteSpace($candidate)) { $candidate = $env:FGUI_AGENT_EXE }
    if (-not [string]::IsNullOrWhiteSpace($candidate)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw "fgui-agent executable does not exist: $candidate"
        }
        return (Resolve-Path -LiteralPath $candidate).Path
    }
    $command = Get-Command 'fgui-agent' -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw 'fgui-agent was not found. Pass -AgentExecutable, set FGUI_AGENT_EXE, or add fgui-agent to PATH.'
    }
    return $command.Source
}

function Invoke-AgentJson {
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$Stage,
        [Parameter(Mandatory)][int]$Timeout
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = $utf8NoBom
    $startInfo.StandardErrorEncoding = $utf8NoBom
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }

    $process = $null
    try {
        $process = [System.Diagnostics.Process]::Start($startInfo)
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($Timeout * 1000)) {
            try { $process.Kill($true) } catch { try { $process.Kill() } catch {} }
            $process.WaitForExit()
            throw "fgui-agent '$Stage' timed out after $Timeout seconds."
        }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            $detail = if ([string]::IsNullOrWhiteSpace($stderr)) { $stdout.Trim() } else { $stderr.Trim() }
            throw "fgui-agent '$Stage' failed with exit code $($process.ExitCode): $detail"
        }
        try { $json = $stdout | ConvertFrom-Json }
        catch { throw "fgui-agent '$Stage' returned invalid JSON: $($_.Exception.Message)" }
        if ($null -eq $json) { throw "fgui-agent '$Stage' returned an empty JSON value." }
        return $json
    }
    finally {
        if ($null -ne $process) { $process.Dispose() }
    }
}

function Get-AgentResult {
    param([Parameter(Mandatory)]$Response, [Parameter(Mandatory)][string]$Stage)

    $okProperty = $Response.PSObject.Properties['ok']
    if ($null -eq $okProperty) { return $Response }
    if ($Response.ok -ne $true) {
        $message = if ($null -ne $Response.error) { [string]$Response.error.message } else { 'unknown bridge error' }
        throw "fgui-agent '$Stage' reported failure: $message"
    }
    if ($null -eq $Response.PSObject.Properties['result']) {
        throw "fgui-agent '$Stage' response has no result field."
    }
    return $Response.result
}

function Get-ArtifactSnapshot {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [ordered]@{ path = $Path; exists = $false; size = 0; mtimeUtc = $null; sha256 = $null }
    }
    $file = Get-Item -LiteralPath $Path
    return [ordered]@{
        path = $Path
        exists = $true
        size = [int64]$file.Length
        mtimeUtc = $file.LastWriteTimeUtc.ToString('o')
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
    }
}

function Assert-PathInsideRoot {
    param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$Path)

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    $prefix = $rootFull + [System.IO.Path]::DirectorySeparatorChar
    if (-not $pathFull.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Published artifact path escapes output root '$rootFull': $pathFull"
    }
    return $pathFull
}

if ([string]::IsNullOrWhiteSpace($PackageName)) { throw 'PackageName cannot be empty.' }
$sourceRoot = Resolve-FullPath $SourceProjectPath 'Repository FairyGUI project'
$editorRoot = Resolve-FullPath $EditorProjectPath 'Editor FairyGUI project'
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
$artifact = Assert-PathInsideRoot $resolvedOutput (Join-Path $resolvedOutput ($PackageName + '_fui.bytes'))
$runtimeManifest = Assert-PathInsideRoot $resolvedOutput (Join-Path $resolvedOutput 'GDKFairyManifest.json')
$agent = Resolve-AgentExecutable $AgentExecutable
$manifestPath = Join-Path $sourceRoot 'generated/GDKFairyManifest.json'
$syncScript = Join-Path $PSScriptRoot 'Sync-GDKDemoToEditor.ps1'
$validator = Join-Path $PSScriptRoot 'Test-GDKProject.ps1'
$runtimeManifestGenerator = Join-Path $PSScriptRoot 'Generate-FairyRuntimeManifest.ps1'

try {
    $null = & $validator -ProjectPath $sourceRoot -ManifestPath $manifestPath -Check
    $syncOutput = & $syncScript -Mode Status -SourceProjectPath $sourceRoot -EditorProjectPath $editorRoot -OutputPath $resolvedOutput
    $syncJson = @($syncOutput | ForEach-Object { [string]$_ } | Where-Object { $_.TrimStart().StartsWith('{') })[-1] | ConvertFrom-Json
    if ($syncJson.state -ne 'Equal') {
        throw "FairyGUI synchronization state must be Equal before publishing; found '$($syncJson.state)'."
    }

    $commonArgs = @('--project', $editorRoot, '--timeout', [string]$TimeoutSeconds)
    $status = Invoke-AgentJson $agent ($commonArgs + @('status')) 'status' $TimeoutSeconds
    $nestedStatus = if ($null -ne $status.status) { $status.status } else { $status }
    if ($status.online -ne $true) { throw 'fgui-agent status is offline or stale.' }
    if ($status.versionMatch -ne $true) { throw 'fgui-agent/plugin version mismatch.' }
    $protocol = [string]$nestedStatus.protocolVersion
    if (-not $protocol.StartsWith('1.')) { throw "fgui-agent protocol must be 1.x; found '$protocol'." }
    $capabilities = @($nestedStatus.capabilities | ForEach-Object { [string]$_ })
    if ($capabilities -notcontains 'publish') { throw 'fgui-agent status does not advertise the publish capability.' }

    $pingResponse = Invoke-AgentJson $agent ($commonArgs + @('ping')) 'ping' $TimeoutSeconds
    $null = Get-AgentResult $pingResponse 'ping'
    $projectResponse = Invoke-AgentJson $agent ($commonArgs + @('project')) 'project' $TimeoutSeconds
    $project = Get-AgentResult $projectResponse 'project'
    $projectPath = [string]$project.basePath
    if ([string]::IsNullOrWhiteSpace($projectPath)) { $projectPath = [string]$project.projectDir }
    if ([string]::IsNullOrWhiteSpace($projectPath) -or
        [System.IO.Path]::GetFullPath($projectPath).TrimEnd('\', '/') -ne $editorRoot.TrimEnd('\', '/')) {
        throw "fgui-agent project does not match EditorProjectPath '$editorRoot'."
    }
    $packagesResponse = Invoke-AgentJson $agent ($commonArgs + @('packages')) 'packages' $TimeoutSeconds
    $packages = Get-AgentResult $packagesResponse 'packages'
    $packageList = if ($null -ne $packages.packages) { @($packages.packages) } else { @($packages) }
    if (@($packageList | Where-Object { [string]$_.name -ceq $PackageName }).Count -ne 1) {
        throw "fgui-agent project does not contain exactly one package named '$PackageName'."
    }

    $before = Get-ArtifactSnapshot $artifact
    $publishArgs = $commonArgs + @('publish', '--scope', 'packages', '--package', $PackageName, '--publish-timeout', [string]$TimeoutSeconds)
    $publishResponse = Invoke-AgentJson $agent $publishArgs 'publish' $TimeoutSeconds
    $publish = Get-AgentResult $publishResponse 'publish'
    if ($publish.success -ne $true) { throw "fgui-agent publish did not report success for '$PackageName'." }
    $after = Get-ArtifactSnapshot $artifact
    if (-not $after.exists -or $after.size -le 0) { throw "Published artifact is missing or empty: $artifact" }
    $null = & $runtimeManifestGenerator `
        -SourceManifestPath $manifestPath `
        -OutputPath $resolvedOutput `
        -ManifestPath $runtimeManifest
    $runtimeManifestAfter = Get-ArtifactSnapshot $runtimeManifest
    if (-not $runtimeManifestAfter.exists -or $runtimeManifestAfter.size -le 0) {
        throw "Runtime FairyGUI manifest is missing or empty: $runtimeManifest"
    }

    [pscustomobject][ordered]@{
        success = $true
        packageName = $PackageName
        sourceProjectPath = $sourceRoot
        editorProjectPath = $editorRoot
        outputPath = $resolvedOutput
        gates = [ordered]@{ lint = $true; manifestCheck = $true; syncState = $syncJson.state; agentStatus = $true }
        agentExecutable = $agent
        cli = [ordered]@{ status = $status; publish = $publishResponse }
        artifactBefore = $before
        artifactAfter = $after
        runtimeManifest = $runtimeManifestAfter
    } | ConvertTo-Json -Depth 100
}
catch { throw $_ }
