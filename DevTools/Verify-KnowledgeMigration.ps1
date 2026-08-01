$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$core = Get-Content -Raw (Join-Path $root 'Source\ModCore.cs')
$knowledge = Get-Content -Raw (Join-Path $root 'Source\PlantKnowledge.cs')
$registry = Get-Content -Raw (Join-Path $root 'Source\CultivarRegistry.cs')
$about = Get-Content -Raw (Join-Path $root 'About\About.xml')

$checks = [ordered]@{
    'plants register as framework domain' = $knowledge -match 'KnowledgeDomainDefinition' -and $knowledge -match 'DomainId = "plants"'
    'knowledge and expertise have separate gains' = $knowledge -match 'pawnKnowledge = amount' -and $knowledge -match 'colonyKnowledge = amount' -and $knowledge -match 'expertise = amount'
    'work effects are bounded and domain owned' = $knowledge -match 'PlantKnowledgeEffectProvider' -and $knowledge -match 'Mathf\.Clamp\(bonus, 0f, 0\.15f\)'
    'hot work query uses framework scalar path' = $knowledge -match 'KnowledgeService\.ApplyEffects' -and $knowledge -notmatch 'GameComponent_NovelSeeds\.Instance\?\.KnowledgeFor'
    'old knowledge imports by maximum merge' = $core -match 'ImportLegacyKnowledge' -and $core -match 'KnowledgeService\.ImportMinimum'
    'legacy knowledge clears only after successful import' = $core -match 'if \(ImportLegacyKnowledge\(\)\) legacyHorticultureKnowledge\.Clear\(\)' -and $core -match 'GameComponent_KnowledgeFramework\.Current == null\) return false'
    'legacy import waits for component finalization' = $core -match 'FinalizeInit\(\)[\s\S]*?ImportLegacyKnowledge\(\)' -and $core -notmatch 'PostLoadInit\)[\s\S]{0,900}?ImportLegacyKnowledge\(\)'
    'obsolete knowledge is load only' = $core -match 'Scribe\.mode != LoadSaveMode\.Saving[\s\S]*?"horticultureKnowledge"'
    'obsolete breeding programs are load only' = $core -match 'Scribe\.mode != LoadSaveMode\.Saving[\s\S]*?"breedingPrograms"' -and $core -notmatch 'AddBreedingProgram'
    'controlled cultivar mixes remain' = $core -match 'breedingVarietyIdsByGrower' -and $core -match 'SetBreedingMix'
    'registry queries framework snapshots' = $registry -match 'HorticultureSharedKnowledgeIntegration\.Menu' -and $registry -match 'GetPawnKnowledgeRank' -and $registry -match 'GetColonyKnowledgeRank'
    'content packs are optional load ordering only' = $about -notmatch '<modDependencies>[\s\S]*?<packageId>VanillaExpanded\.VPlants' -and $about -match '<loadAfter>[\s\S]*?VanillaExpanded\.VPlantsE'
}
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) { throw 'Knowledge migration checks failed: ' + (($failed | ForEach-Object Key) -join ', ') }
Write-Output ("Knowledge migration verification passed ({0} checks)." -f $checks.Count)
