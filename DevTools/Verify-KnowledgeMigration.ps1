$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$core = Get-Content -Raw (Join-Path $root 'Source\ModCore.cs')
$knowledge = Get-Content -Raw (Join-Path $root 'Source\PlantKnowledge.cs')
$adapter = Get-Content -Raw (Join-Path $root 'Source\HorticultureKnowledgeAdapter.cs')
$snapshots = Get-Content -Raw (Join-Path $root 'Source\HorticultureKnowledgeSnapshots.cs')
$router = Get-Content -Raw (Join-Path $root 'Source\HorticultureEventRouter.cs')
$registry = Get-Content -Raw (Join-Path $root 'Source\CultivarRegistry.cs')
$workspace = Get-Content -Raw (Join-Path $root 'Source\HorticultureWorkspaceDocument.cs')
$presentationPolicy = Get-Content -Raw (Join-Path $root 'Source\HorticulturePresentationPolicy.cs')
$about = Get-Content -Raw (Join-Path $root 'About\About.xml')
$contract = Get-Content -Raw (Join-Path $root 'Source\HorticultureKnowledgeContract.cs')
$compatibility = Get-Content -Raw (Join-Path $root 'Source\HorticultureKnowledgeCompatibility.cs')
$registration = Get-Content -Raw (Join-Path $root 'Source\HorticultureKnowledgeRegistration.cs')
$migration = Get-Content -Raw (Join-Path $root 'Source\HorticultureKnowledgeMigration.cs')
$policy = Get-Content -Raw (Join-Path $root 'Source\HorticulturePlantPolicy.cs')
$identity = Get-Content -Raw (Join-Path $root 'Source\HorticultureKnowledgeEventIdentity.cs')
$diagnostics = Get-Content -Raw (Join-Path $root 'Source\HorticultureKnowledgeEventDiagnostics.cs')

$checks = [ordered]@{
    'namespaced domain and permanent legacy ID are centralized' = $contract -match 'DomainId = PackageId \+ "\.plants"' -and $contract -match 'LegacyDomainId = "plants"'
    'safe registration uses compatibility and non-replacing ownership checks' = $compatibility -match 'RequiredCapabilities' -and $registration -match 'InspectDomainRegistration' -and $registration -match 'KnowledgeRegistrationConflict\.Reject'
    'knowledge and expertise have separate gains' = ($knowledge -match 'pawnKnowledge = amount' -and $knowledge -match 'colonyKnowledge = amount' -and $knowledge -match 'expertise = amount') -or ($adapter -match 'directKnowledge' -and $adapter -match 'directExpertise' -and $adapter -match 'targetColony = true')
    'work effects are bounded and domain owned' = $knowledge -match 'PlantKnowledgeEffectProvider' -and $knowledge -match 'Mathf\.Clamp\(bonus, 0f, 0\.15f\)'
    'hot work query uses framework scalar path' = $knowledge -match 'HorticultureKnowledgeAdapter\.PlantWorkSpeedFactor' -and $adapter -match 'KnowledgeQuery\.Expertise'
    'legacy knowledge imports through versioned framework migration' = $migration -match 'KnowledgeMigrationService\.Import' -and $migration -match 'MigrationVersion'
    'legacy alias is registered before migrated content' = $migration -match 'RegisterDomainAlias' -and $registration -match 'RegisterLegacyAlias'
    'legacy knowledge clears only after successful migration' = $core -match '!HorticultureKnowledgeAdapter\.TryMigrateLegacy\(legacyHorticultureKnowledge\)' -and $core -match 'TryMigrateLegacy\(legacyHorticultureKnowledge\)[\s\S]{0,500}legacyHorticultureKnowledge\?\.Clear\(\)'
    'legacy migration has guarded recovery retry' = $core -match 'knowledgeIntegrationRetryScheduled' -and $core -match 'ExecuteWhenFinished'
    'Horticulture has no direct framework component or schema lifecycle dependency' = (($adapter + $core + $knowledge) -notmatch 'GameComponent_KnowledgeFramework\.Current|BuildDefSchemas\(\)|KnowledgeService\.|KnowledgeDomainRegistry\.')
    'obsolete knowledge is load only' = $core -match 'Scribe\.mode != LoadSaveMode\.Saving[\s\S]*?"horticultureKnowledge"'
    'obsolete breeding programs are load only' = $core -match 'Scribe\.mode != LoadSaveMode\.Saving[\s\S]*?"breedingPrograms"' -and $core -notmatch 'AddBreedingProgram'
    'controlled cultivar mixes remain' = $core -match 'breedingVarietyIdsByGrower' -and $core -match 'SetBreedingMix'
    'registry queries framework snapshots through the adapter' = $registry -match 'HorticultureKnowledgeAdapter\.Menu' -and $workspace -match 'HorticulturePresentationPolicy\.ForCultivar' -and $presentationPolicy -match 'HorticultureKnowledgeSnapshots\.(Claim|Facet|Subject)' -and $snapshots -match 'HorticultureKnowledgeAdapter\.KnowledgeRevision'
    'completed events route through one semantic router' = $router -match 'SowingCompleted' -and $router -match 'HarvestCompleted' -and $router -match 'ProduceProcessed' -and $router -match 'CuttingCompleted' -and $router -notmatch 'CurrentTick\(|TicksGame'
    'canonical policy includes sowable trees' = $policy -match 'plantDef\.plant\.Sowable' -and $policy -match 'IsSowableTree' -and $policy -notmatch '!plantDef\.plant\.IsTree'
    'stable identities and bounded dedupe are present' = $identity -match 'StableHash' -and $identity -match 'MaxIdentityLength' -and $diagnostics -match 'MaxRecentEvents = 1024'
    'normal cultivar registration invalidates affected subjects with safe fallback' = $adapter -match 'InvalidateSubjects\(' -and
        $adapter -match 'catch \(InvalidOperationException\)' -and $adapter -match 'InvalidateDomain\(DomainId\)'
    'content packs are optional load ordering only' = $about -notmatch '<modDependencies>[\s\S]*?<packageId>VanillaExpanded\.VPlants' -and $about -match '<loadAfter>[\s\S]*?VanillaExpanded\.VPlantsE'
}
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) { throw 'Knowledge migration checks failed: ' + (($failed | ForEach-Object Key) -join ', ') }
Write-Output ("Knowledge migration verification passed ({0} checks)." -f $checks.Count)
