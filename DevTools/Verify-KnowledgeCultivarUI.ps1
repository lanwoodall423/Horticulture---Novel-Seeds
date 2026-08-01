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
$mutation = Read-ModFile 'Horticulture - Novel Seeds\Source\NovelSeedUtility.cs'
$core = Read-ModFile 'Horticulture - Novel Seeds\Source\ModCore.cs'

$requirements = [ordered]@{
    'colony detail hides expertise' = $menu -match 'if \(!colony\)[\s\S]*?DrawProgressBar'
    'colony navigation describes knowledge only' = $menu -match 'Shared subject knowledge across the colony\.'
    'aquaculture branches before expertise aggregation' = $aquaculture -match 'return FishingKnowledgeModel\(colonyRecords, null, true\)' -and $aquaculture -match 'if \(colony\)[\s\S]*?title = "Colony Fishing Knowledge"'
    'wildlife branches before expertise aggregation' = $wildlife -match 'if \(colony\)[\s\S]*?Colony Wildlife Knowledge'
    'horticulture delegates to a colony-safe framework provider' = $registry -match 'HorticultureSharedKnowledgeIntegration\.Menu\(pawn, colony\)' -and $knowledge -match 'if \(colony\) return new KnowledgeMenuModel[\s\S]*?Colony Horticulture Knowledge'
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
    'plants use the framework domain service' = $knowledge -match 'KnowledgeDomainRegistry\.RegisterDomain' -and $knowledge -match 'KnowledgeService\.Award' -and $registry -match 'KnowledgeService\.GetColonyKnowledgeRank'
    'colony and expertise remain isolated' = $knowledge -match 'colonyKnowledge = amount' -and $knowledge -match 'expertise = amount' -and $registry -match 'GetPawnExpertiseRank'
}

$failed = @($requirements.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count -gt 0) {
    throw 'Knowledge/Cultivar UI verification failed: ' + (($failed | ForEach-Object Key) -join ', ')
}
Write-Output ("Knowledge/Cultivar UI verification passed ({0} checks)." -f $requirements.Count)
