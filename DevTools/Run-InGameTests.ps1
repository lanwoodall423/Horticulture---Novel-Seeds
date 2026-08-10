param(
    [string]$DevBridgeRoot = (Join-Path $PSScriptRoot '..\..\DevBridge2'),
    [int]$TimeoutSeconds = 180,
    [switch]$SkipRestart
)

$ErrorActionPreference = 'Stop'
$bridge = Join-Path ([IO.Path]::GetFullPath($DevBridgeRoot)) 'DevBridge.cmd'
$resultPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'TestResults\Horticulture-InGameTests.json'))
$runtimeResultPath = [IO.Path]::GetFullPath((Join-Path $DevBridgeRoot 'Runtime\Horticulture-InGameTests.json'))
$resultPaths = @($resultPath, $runtimeResultPath) | Select-Object -Unique

if (-not (Test-Path -LiteralPath $bridge -PathType Leaf)) { throw "DevBridge.cmd not found: $bridge" }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resultPath) | Out-Null
$env:HNS_TEST_RESULTS = $resultPath

function Invoke-Bridge([string[]]$Arguments) {
    $output = @(& $bridge @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw (($output | Out-String).Trim()) }
    return $output
}

try {
    if (-not $SkipRestart) {
        Invoke-Bridge @('restart') | Write-Host
    }

    $status = Invoke-Bridge @('status')
    $statusText = ($status | Out-String)
    $launchMatch = [regex]::Match($statusText, 'Launch ID:\s*(?<id>[^\s\r\n]+)')
    if (-not $launchMatch.Success) { throw "DevBridge did not report a launch ID." }
    $expectedLaunchId = $launchMatch.Groups['id'].Value

    $begin = Invoke-Bridge @('test', 'begin')
    $beginText = ($begin | Out-String)
    $leaseMatch = [regex]::Match($beginText, 'Test lease acquired:\s*(?<id>[^\s\r\n]+)')
    if (-not $leaseMatch.Success) { throw "DevBridge did not return a test lease." }
    $leaseId = $leaseMatch.Groups['id'].Value

    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while ([DateTime]::UtcNow -lt $deadline) {
            foreach ($candidatePath in $resultPaths) {
                if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
                    try {
                        $report = Get-Content -LiteralPath $candidatePath -Raw | ConvertFrom-Json
                        if ($report.launchId -eq $expectedLaunchId) {
                            $report | ConvertTo-Json -Depth 8 | Write-Host
                            if ($report.passed -ne $true) { exit 1 }
                            exit 0
                        }
                    }
                    catch {
                        # The mod writes atomically; a transient read is harmless.
                    }
                }
            }
            Start-Sleep -Milliseconds 500
        }
        throw "Timed out waiting for the Horticulture-owned result for launch ${expectedLaunchId}: $($resultPaths -join ', ')"
    }
    finally {
        Invoke-Bridge @('test', 'end', $leaseId) | Write-Host
    }
}
finally {
    Remove-Item Env:HNS_TEST_RESULTS -ErrorAction SilentlyContinue
}
