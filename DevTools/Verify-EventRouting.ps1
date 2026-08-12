$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'Source'
$policy = Get-Content (Join-Path $source 'HorticulturePlantPolicy.cs') -Raw
$identity = Get-Content (Join-Path $source 'HorticultureKnowledgeEventIdentity.cs') -Raw
$diagnostics = Get-Content (Join-Path $source 'HorticultureKnowledgeEventDiagnostics.cs') -Raw
$router = Get-Content (Join-Path $source 'HorticultureEventRouter.cs') -Raw
$adapter = Get-Content (Join-Path $source 'HorticultureKnowledgeAdapter.cs') -Raw
$patches = Get-Content (Join-Path $source 'Patches.cs') -Raw
$passed = 0

function Assert-Contract([bool] $condition, [string] $name) {
    if (-not $condition) { throw "FAIL: $name" }
    $script:passed++
    Write-Output "PASS: $name"
}

Assert-Contract ($policy -match 'plantDef\.plant\.Sowable' -and $policy -match 'IsSowableTree') 'canonical policy includes sowable trees'
Assert-Contract ($policy -notmatch '!plantDef\.plant\.IsTree') 'canonical policy has no blanket tree exclusion'
Assert-Contract ($identity -match 'StableHash' -and $identity -match 'MaxIdentityLength' -and $identity -match 'thingIDNumber') 'event identity is bounded and semantic'
Assert-Contract ($router -notmatch 'CurrentTick\(|TicksGame' -and $router -match 'HorticultureKnowledgeEventIdentity') 'router does not use tick-only identities'
Assert-Contract ($router -match 'CuttingCompleted' -and $patches -match 'CuttingCompleted') 'supported tree cutting has one routed event'
Assert-Contract ($patches -match 'Plant_TickLong_Knowledge_Patch' -and $patches -match 'GrowthObserved\(null, __instance\)') 'growth buckets route from plant growth ticks'
Assert-Contract ($router -match 'Processing\(worker, source, crop\)' -and $router -match 'Distinct\(\)') 'processing identity is batch and source-crop specific'
Assert-Contract ($router -match 'traitList' -and $router -notmatch 'TraitInheritance, map') 'discovery and lineage share one routed transaction'
Assert-Contract ($diagnostics -match 'MaxRecentEvents = 1024' -and $diagnostics -match 'ResetForGameTransition') 'dedupe cache is bounded and game-scoped'
Assert-Contract ($adapter -match 'HorticultureKnowledgeEventDiagnostics\.Accept' -and $adapter -match 'SubmittedEvent') 'adapter enforces integration-boundary deduplication'
Assert-Contract ($adapter -match 'uniquePerSourceInstance = true' -and $adapter -match 'stateLimit = 4096') 'framework accrual enforces stable source idempotency'
Assert-Contract ($adapter -match 'InvalidateSubjects\(' -and $adapter -match 'catch \(InvalidOperationException\)' -and
    $adapter -match 'InvalidateDomain\(DomainId\)' ) 'normal cultivar registration uses targeted invalidation with safe fallback'
Assert-Contract ($router -match 'HorticultureKnowledgeEventIdentity' -and $diagnostics -match 'Snapshot') 'runtime diagnostics expose executable routing state'
Assert-Contract ($source | ForEach-Object { $files = Get-ChildItem $_ -Filter '*.cs' -Recurse; -not (($files | ForEach-Object { Get-Content $_.FullName -Raw }) -match 'GameComponent_KnowledgeFramework\.Current|KnowledgeRegistry\.BuildDefSchemas\(\)|KnowledgeService\.') }) 'gameplay source avoids direct framework implementation/lifecycle calls'

Write-Output "event-routing-verification=PASS checks=$passed"
