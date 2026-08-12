param(
    [string]$DevBridgeRoot = (Join-Path $PSScriptRoot '..\..\DevBridge2'),
    [ValidateSet('complete', 'startup', 'clean-default', 'ordinary-crop', 'sowable-tree', 'cross-pollination', 'produce-processing', 'knowledge', 'save-reload', 'negative', 'long-running', 'ux-discovery', 'registry-scale', 'rc-performance', 'auto-mask-suite', 'auto-mask-export')]
    [string]$Scenario = 'complete',
    [int]$TimeoutSeconds = 300,
    [switch]$SkipRestart,
    [switch]$SkipGameplayBuild,
    [string]$BundleOutputPath = '',
    [switch]$RegenerateAutoMasks,
    [string]$ResultArchiveDirectory = (Join-Path $PSScriptRoot 'Staged\RuntimeResults')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$bridgeRoot = [IO.Path]::GetFullPath($DevBridgeRoot)
$bridge = Join-Path $bridgeRoot 'DevBridge.cmd'
$runtime = Join-Path $bridgeRoot 'Runtime'
$requestPath = Join-Path $runtime 'Horticulture.RuntimeTest.request.json'
$tracePath = Join-Path $runtime 'Horticulture.RuntimeTest.trace.log'
$assembly = Join-Path $root '1.6\Assemblies\HorticultureNovelSeeds.RuntimeTests.v12.dll'
$playerLog = if ($env:RIMWORLD_DATA_PATH) { Join-Path $env:RIMWORLD_DATA_PATH 'Player.log' } else {
    Join-Path $env:LOCALAPPDATA '..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log'
}

if (-not (Test-Path -LiteralPath $bridge -PathType Leaf)) { throw "DevBridge.cmd not found: $bridge" }
& "$PSScriptRoot\Build-RuntimeTests.ps1" -SkipGameplayBuild:$SkipGameplayBuild -SkipDeploy:$SkipRestart
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw "Runtime test assembly not found: $assembly" }

function Invoke-DevBridge([string[]]$Arguments) {
    $output = @(& $bridge @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw (($output | Out-String).Trim()) }
    return $output
}

$launchId = $null
$baselineLines = 0
if (Test-Path -LiteralPath $playerLog -PathType Leaf) {
    try { $baselineLines = @(Get-Content -LiteralPath $playerLog).Count } catch { $baselineLines = 0 }
}

if (-not $SkipRestart) {
    $statusBeforeRestart = Invoke-DevBridge @('status') | Out-String
    $previousLaunchMatch = [regex]::Match($statusBeforeRestart, 'Launch ID:\s*(?<id>[^\s\r\n]+)')
    $previousLaunchId = if ($previousLaunchMatch.Success) { $previousLaunchMatch.Groups['id'].Value } else { '' }
    if (Test-Path -LiteralPath $requestPath) { Remove-Item -LiteralPath $requestPath -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $tracePath) { Remove-Item -LiteralPath $tracePath -Force -ErrorAction SilentlyContinue }

    $restartStdout = Join-Path $runtime 'Horticulture.DevBridge.restart.stdout.log'
    $restartStderr = Join-Path $runtime 'Horticulture.DevBridge.restart.stderr.log'
    Remove-Item -LiteralPath $restartStdout,$restartStderr -Force -ErrorAction SilentlyContinue
    Start-Process -FilePath 'cmd.exe' -ArgumentList @('/c', $bridge, 'restart') -WindowStyle Hidden

    $launchDeadline = (Get-Date).AddSeconds([Math]::Max(60, $TimeoutSeconds))
    while ((Get-Date) -lt $launchDeadline) {
        try {
            $candidateStatus = (Invoke-DevBridge @('status') | Out-String)
            $candidateLaunchMatch = [regex]::Match($candidateStatus, 'Launch ID:\s*(?<id>[^\s\r\n]+)')
            $candidateLaunchId = if ($candidateLaunchMatch.Success) { $candidateLaunchMatch.Groups['id'].Value } else { '' }
            $hasNewLaunch = -not [string]::IsNullOrEmpty($candidateLaunchId) -and (
                [string]::IsNullOrEmpty($previousLaunchId) -or $candidateLaunchId -ne $previousLaunchId)
            $isLoadingOrReady = $candidateStatus -match '(?m)^State:\s*(LOADING|READY)\s*$'
            if ($hasNewLaunch -and $isLoadingOrReady) {
                $launchId = $candidateLaunchId
                break
            }
        }
        catch { }
        Start-Sleep -Seconds 1
    }
    if ([string]::IsNullOrEmpty($launchId)) { throw 'DevBridge2 did not expose the new launch ID before the Horticulture test deadline.' }
}
else {
    Invoke-DevBridge @('wait-ready') | Write-Host
    $status = Invoke-DevBridge @('status')
    $statusText = ($status | Out-String)
    $launchMatch = [regex]::Match($statusText, 'Launch ID:\s*(?<id>[^\s\r\n]+)')
    if (-not $launchMatch.Success) { throw 'DevBridge2 did not report a launch ID.' }
    $launchId = $launchMatch.Groups['id'].Value
}

$requestId = [guid]::NewGuid().ToString('N')
$resultPath = Join-Path $runtime ('Horticulture.RuntimeTest.' + $requestId + '.json')
$knowledgeDll = Join-Path $root '..\KnowledgeFramework\1.6\Assemblies\KnowledgeFramework.dll'
$warmupTicks = if ($Scenario -in @('complete', 'save-reload', 'auto-mask-suite', 'auto-mask-export')) { 180 } else { 60 }
$request = [ordered]@{
    schemaVersion = '1'
    requestId = $requestId
    launchId = $launchId
    scenario = $Scenario
    warmupTicks = $warmupTicks
    timeoutTicks = [Math]::Max(60, $TimeoutSeconds * 60)
    resultPath = $resultPath
    horticultureCommit = ((git -C $root rev-parse HEAD) | Out-String).Trim()
    horticultureDllSha256 = (Get-FileHash (Join-Path $root '1.6\Assemblies\HorticultureNovelSeeds.dll') -Algorithm SHA256).Hash
    knowledgeFrameworkDllSha256 = if (Test-Path -LiteralPath $knowledgeDll) { (Get-FileHash $knowledgeDll -Algorithm SHA256).Hash } else { '' }
    knowledgeFrameworkRelease = if (Test-Path -LiteralPath $knowledgeDll) { [Diagnostics.FileVersionInfo]::GetVersionInfo($knowledgeDll).ProductVersion } else { '' }
    knowledgeFrameworkApiGeneration = 3
    playerLogPath = [IO.Path]::GetFullPath($playerLog)
    playerLogBaselineLines = $baselineLines
    autoMaskBundleOutputPath = if ($BundleOutputPath) { [IO.Path]::GetFullPath($BundleOutputPath) } else { '' }
    autoMaskRegenerate = [bool]$RegenerateAutoMasks
}

function Write-HorticultureRequest {
    $temporaryRequest = $requestPath + '.tmp'
    $request | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $temporaryRequest -Encoding UTF8
    if (Test-Path -LiteralPath $requestPath) { Remove-Item -LiteralPath $requestPath -Force }
    if (Test-Path -LiteralPath $tracePath) { Remove-Item -LiteralPath $tracePath -Force -ErrorAction SilentlyContinue }
    Move-Item -LiteralPath $temporaryRequest -Destination $requestPath -Force
}

function Wait-ForReadyStatus {
    $deadline = (Get-Date).AddSeconds([Math]::Max(60, $TimeoutSeconds))
    while ((Get-Date) -lt $deadline) {
        $currentStatus = (Invoke-DevBridge @('status') | Out-String)
        if ($currentStatus -match '(?m)^State:\s*READY\s*$') { return }
        Start-Sleep -Seconds 1
    }
    throw 'DevBridge2 did not reach READY before the Horticulture test deadline.'
}

$leaseId = $null
$exitCode = 1
try {
    if (-not $SkipRestart) {
        Write-HorticultureRequest
        Wait-ForReadyStatus
    }

    $begin = Invoke-DevBridge @('test', 'begin')
    $beginText = ($begin | Out-String)
    $leaseMatch = [regex]::Match($beginText, 'Test lease acquired:\s*(?<id>[^\s\r\n]+)')
    if (-not $leaseMatch.Success) { throw 'DevBridge2 did not return a test lease.' }
    $leaseId = $leaseMatch.Groups['id'].Value

    if ($SkipRestart) { Write-HorticultureRequest }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $report = $null
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $resultPath -PathType Leaf) {
            try {
                $candidate = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
                 if ($candidate.requestId -eq $requestId -and $candidate.status -in @('PASS', 'FAIL', 'BLOCKED')) {
                     $report = $candidate
                    if (-not [string]::IsNullOrWhiteSpace($ResultArchiveDirectory)) {
                        $archiveDirectory = [IO.Path]::GetFullPath($ResultArchiveDirectory)
                        New-Item -ItemType Directory -Path $archiveDirectory -Force | Out-Null
                        $archivePath = Join-Path $archiveDirectory ($Scenario + '.' + $requestId + '.json')
                        $candidate | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $archivePath -Encoding UTF8
                    }
                     break
                 }
            } catch { }
        }
        Start-Sleep -Milliseconds 500
    }
    if ($null -eq $report) { throw "Timed out waiting for Horticulture scenario '$Scenario' result: $resultPath" }
    $report | ConvertTo-Json -Depth 12 | Write-Host
    if ($report.status -eq 'PASS') { $exitCode = 0 }
}
finally {
    if ($leaseId) {
        try { Invoke-DevBridge @('test', 'end', $leaseId) | Write-Host } catch { Write-Warning $_ }
    }
    if (Test-Path -LiteralPath $requestPath) { Remove-Item -LiteralPath $requestPath -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $tracePath) { Remove-Item -LiteralPath $tracePath -Force -ErrorAction SilentlyContinue }
    # A skipped-restart run shares the already loaded runner with follow-up
    # scenarios. Keep that assembly available until the generation is restarted.
    if (-not $SkipRestart -and (Test-Path -LiteralPath $assembly)) {
        Remove-Item -LiteralPath $assembly -Force -ErrorAction SilentlyContinue
    }
}
exit $exitCode
