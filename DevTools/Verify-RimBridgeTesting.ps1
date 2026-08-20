$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Get-Content -Raw (Join-Path $root 'DevTools\BridgeTools\HorticultureNovelSeeds.BridgeTools.csproj')
$tool = Get-Content -Raw (Join-Path $root 'DevTools\BridgeTools\HorticultureBridgeTools.cs')
$runner = Get-Content -Raw (Join-Path $root 'DevTools\Run-RimBridgeTests.ps1')
$checks = [ordered]@{
    'companion targets net48' = $project -match '<TargetFramework>net48</TargetFramework>'
    'companion references SDK compile-only' = $project -match 'RimBridgeServer\.Sdk' -and $project -match '<Private>false</Private>' -and $project -match '<ExcludeAssets>runtime</ExcludeAssets>'
    'companion uses tool attributes and injected context' = $tool -match '\[Tool\("horticulture/run_suite"' -and $tool -match 'IRimBridgeContext context' -and $tool -match 'CancellationToken cancellationToken'
    'companion advances real game ticks' = $tool -match 'RunForTicksAsync'
    'companion uses bridge tool discovery' = $tool -match 'context\.Tools\.List' -and $tool -match 'context\.Tools\.Exists'
    'companion returns evidence' = $tool -match 'RimBridgeEvidenceManifest' -and $tool -match 'RimBridgeEvidence\.Complete'
    'runner routes through RimTest' = $runner -match '\$RimTest' -and $runner -match 'run horticulture-in-game-smoke --json'
    'runner never controls RimWorld directly' = $runner -notmatch 'DevBridge\.cmd|RimWorldWin64\.exe|Start-Process|ModsConfig'
    'old file harness absent' = -not (Test-Path (Join-Path $root 'DevTools\RuntimeTests')) -and -not (Test-Path (Join-Path $root 'DevTools\Run-RuntimeTests.ps1'))
}
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
$checks.GetEnumerator() | ForEach-Object { '{0}: {1}' -f ($(if ($_.Value) { 'PASS' } else { 'FAIL' }), $_.Key) }
if ($failed.Count -gt 0) { throw "$($failed.Count) RimBridge testing checks failed." }
Write-Output ('RimBridge testing verification passed ({0} checks).' -f $checks.Count)
