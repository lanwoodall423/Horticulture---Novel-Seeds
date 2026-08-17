$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Read-ModFile([string]$relativePath) {
    Get-Content -LiteralPath (Join-Path $root $relativePath) -Raw
}

$patches = Read-ModFile 'Source\Patches.cs'
$work = Read-ModFile 'Source\ExpandedTraitWorkPatches.cs'
$nice = Read-ModFile 'Source\NicePlantsMenuCompat.cs'
$bridge = Read-ModFile 'DevTools\BridgeTools\HorticultureBridgeTools.cs'
$checks = [ordered]@{
    'sow closures use structural discovery' = $patches -match 'GetNestedTypes' -and $patches -match 'ReadsField\(method, SowWorkField\)' -and $patches -notmatch '<MakeNewToils>'
    'all sow-work reads are substituted' = $patches -match 'TargetMethods\(\)[\s\S]*?SowWorkMethods' -and $patches -match 'AdjustedSowWorkMethod'
    'sowing effects require threshold crossing' = $patches -match 'priorWork >= __state.requiredWork' -and $patches -match 'SowWorkDone\(__state.driver\) < __state.requiredWork'
    'skill reads are substituted without definition writes' = $work -match 'EffectiveSowMinSkill' -and $work -notmatch '\.sowMinSkill\s*='
    'perennial harvest keeps and resets only the plant instance' = $patches -match 'EffectiveHarvestDestroys\(bool baseValue, Plant plant, PlantDestructionMode mode\)' -and $patches -match 'mode != HarvestMode' -and $patches -match '__instance\.Growth = Mathf\.Max' -and $patches -notmatch '\.harvestAfterGrowth\s*='
    'Nice Plants Menu never rewrites shared definitions' = $nice -notmatch 'NicePlantsInfoOverrideState|DrawInfoPrefix|DrawInfoFinalizer|cachedLabelCap' -and $nice -notmatch '\.(label|description|sowWork|harvestWork|harvestYield|harvestAfterGrowth|minGrowthTemperature|minOptimalGrowthTemperature|maxOptimalGrowthTemperature|maxGrowthTemperature|statBases)\s*[+*/-]?='
    'Nice Plants Menu retains dedicated variety panel' = $nice -match 'DrawNovelSeedsInfo' -and $nice -match 'StatChangeLines' -and $nice -match 'DrawNicePlantsTraitRow'
    'Nice Plants Menu constructor receives requested growers and restores selection' = $nice -match 'CreateDialogForGrowers' -and $nice -match 'List<object> previous = selected\.ToList\(\)' -and $nice -match 'finally[\s\S]*?selected\.AddRange\(previous\)'
    'bridge fixtures are transient and cleaned with vanish semantics' = $bridge -match 'DestroyFixture' -and $bridge -match 'Fixtures\.Clear' -and $bridge -match 'DestroyMode\.Vanish'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
$checks.GetEnumerator() | ForEach-Object { '{0}: {1}' -f ($(if ($_.Value) { 'PASS' } else { 'FAIL' }), $_.Key) }
if ($failed.Count -gt 0) { throw "$($failed.Count) immutable-definition checks failed." }
