param([string]$ModsRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))

$ErrorActionPreference = 'Stop'
function Read-ModFile([string]$relativePath) {
    $path = Join-Path $ModsRoot $relativePath
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing required file: $relativePath" }
    return Get-Content -Raw -LiteralPath $path
}

$menu = Read-ModFile 'KnowledgeFramework\Source\KnowledgeMenuUI.cs'
$aquaculture = Read-ModFile 'AquacultureFishing\Source\AquacultureJournal.cs'
$wildlife = Read-ModFile 'Wildlife\Source\Herds\HuntingKnowledge.cs'
$registry = Read-ModFile 'Horticulture - Novel Seeds\Source\CultivarRegistry.cs'
$knowledge = Read-ModFile 'Horticulture - Novel Seeds\Source\PlantKnowledge.cs'
$adapter = Read-ModFile 'Horticulture - Novel Seeds\Source\HorticultureKnowledgeAdapter.cs'
$router = Read-ModFile 'Horticulture - Novel Seeds\Source\HorticultureEventRouter.cs'
$mutation = Read-ModFile 'Horticulture - Novel Seeds\Source\NovelSeedUtility.cs'
$core = Read-ModFile 'Horticulture - Novel Seeds\Source\ModCore.cs'

$requirements = [ordered]@{
    'colony detail hides expertise' = $menu -match 'if \(!colony\)[\s\S]*?DrawProgressBar'
    'colony navigation describes knowledge only' = $menu -match 'KnowledgeFramework_ColonyDescription' -and $menu -match 'if \(state\.scope == KnowledgeMenuScope\.Colony\)[\s\S]*?return'
    'aquaculture branches before expertise aggregation' = $aquaculture -match 'return FishingKnowledgeModel\(colonyRecords, null, true\)' -and $aquaculture -match 'if \(colony\)[\s\S]*?title = "Colony Fishing Knowledge"'
    'wildlife branches before expertise aggregation' = $wildlife -match 'WildlifeProficiencyCoverage' -and $wildlife -match 'WildlifeProficiencyLevel' -and $wildlife -match 'ModelFor\(Pawn pawn, bool colony\)'
    'horticulture delegates to a colony-safe framework provider' = $registry -match 'HorticultureKnowledgeAdapter\.Menu\(pawn, colony\)' -and $adapter -match 'colony \? "Colony Horticulture Knowledge"'
    'no knowledge mutation factor API' = $knowledge -notmatch 'MutationChanceFactor'
    'mutation does not query plant knowledge' = $mutation -notmatch 'PlantKnowledgeUtility\.(Mutation|Experience|Rank)'
    'personal plant work factor retained' = $knowledge -match 'PlantWorkSpeedFactor\(Pawn pawn, ThingDef cropDef\)'
    'registry field-journal pages' = $registry -match 'RegistryPage \{ Plants, Cultivars, Knowledge, Compare \}'
    'discovered plants page' = $registry -match 'Discovered Plants'
    'undiscovered entries are masked' = $registry -match 'Undiscovered plant'
    'comparison requires two entries' = $registry -match 'CanCompareCount\(int count\) => count >= 2' -and $registry -match 'CanCompareCount\(comparisonIds\.Count\)'
    'side-by-side comparison' = $registry -match 'DrawComparisonTable'
    'no breeding program page' = $registry -notmatch 'RightPage|DrawPrograms|Dialog_CreateBreedingProgram|RegistryPrograms|RegistryNewProgram'
    'variety save keys retained' = $core -match 'parentVarietyIds' -and $core -match 'firstDiscoveredTick'
    'breeding programs are load-only legacy data' = $core -match 'class BreedingProgramRecord' -and $core -match 'Scribe\.mode != LoadSaveMode\.Saving[\s\S]*?"breedingPrograms"' -and $core -notmatch 'NotifyMatchingBreedingPrograms'
    'knowledge migrates from the legacy key without new writes' = $core -match 'Scribe\.mode != LoadSaveMode\.Saving[\s\S]*?"horticultureKnowledge"' -and $core -match 'KnowledgeService\.ImportMinimum'
    'plants use the framework domain service' = $adapter -match 'KnowledgeRegistry\.RegisterDomain' -and $adapter -match 'KnowledgeEngine\.Submit' -and $registry -match 'HorticultureKnowledgeAdapter\.ColonyKnowledge'
    'colony and expertise remain isolated' = $adapter -match 'targetColony = true' -and $adapter -match 'directExpertise = expert' -and $registry -match 'HorticultureKnowledgeAdapter\.ExpertiseRank'
    'completed gameplay events use one observation router' = $router -match 'SowingCompleted' -and $router -match 'NovelSeedDiscovered' -and $router -match 'CultivarDocumented'
}

$failed = @($requirements.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count -gt 0) {
    throw 'Knowledge/Cultivar UI verification failed: ' + (($failed | ForEach-Object Key) -join ', ')
}
Write-Output ("Knowledge/Cultivar UI verification passed ({0} checks)." -f $requirements.Count)
