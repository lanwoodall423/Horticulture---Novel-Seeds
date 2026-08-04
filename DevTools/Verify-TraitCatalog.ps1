$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$xmlPath = Join-Path $root '1.6\Defs\VarietyTraitDefs.xml'
$modCorePath = Join-Path $root 'Source\ModCore.cs'
$utilityPath = Join-Path $root 'Source\NovelSeedUtility.cs'
$expandedPath = Join-Path $root 'Source\ExpandedTraitPatches.cs'
$familiesPath = Join-Path $root 'Source\TraitFamilies.cs'
$validationPath = Join-Path $root 'Source\TraitCatalogValidation.cs'
$regressionPath = Join-Path $root 'Source\TraitCatalogRegression.cs'
$patchesPath = Join-Path $root 'Source\Patches.cs'
$debugPath = Join-Path $root 'Source\DebugActions.cs'
$bridgePath = Join-Path $root 'DevTools\BridgeAdapter\HorticultureBridgeAdapter.cs'
$englishPath = Join-Path $root '1.6\Languages\English\Keyed\HorticultureNovelSeeds.xml'

$xml = [xml](Get-Content -LiteralPath $xmlPath -Raw)
$modCore = Get-Content -LiteralPath $modCorePath -Raw
$utility = Get-Content -LiteralPath $utilityPath -Raw
$expanded = Get-Content -LiteralPath $expandedPath -Raw
$families = Get-Content -LiteralPath $familiesPath -Raw
$validation = Get-Content -LiteralPath $validationPath -Raw
$regression = Get-Content -LiteralPath $regressionPath -Raw
$patches = Get-Content -LiteralPath $patchesPath -Raw
$debug = Get-Content -LiteralPath $debugPath -Raw
$bridge = Get-Content -LiteralPath $bridgePath -Raw
$english = Get-Content -LiteralPath $englishPath -Raw

$passed = 0
$failed = [System.Collections.Generic.List[string]]::new()
function Check([bool]$condition, [string]$name) {
    if ($condition) { $script:passed++ } else { $script:failed.Add($name) }
}

$defs = @($xml.Defs.ChildNodes | Where-Object { $_.defName })
$mechanical = @($defs | Where-Object { $_.configFamily -notmatch 'Color' -and $_.defName -notmatch 'Color' })
Check ($defs.Count -eq 49) 'named XML trait count changed'
Check ((@($mechanical | Where-Object { $_.balanceValueExplicit -ne 'true' })).Count -eq 0) 'mechanical XML traits lack explicit balance values'
Check ($modCore -match 'public float growthRateFactor = 1f' -and $modCore -match 'public float synergyAbsentFactor = 1f') 'neutral factor fields/defaults missing'
Check ($modCore -match 'public bool balanceValueExplicit') 'explicit balance field missing'
Check ($utility -match 'if \(trait\.balanceValueExplicit\) return trait\.balanceValue') 'explicit zero balance is not honored'
Check ($utility -match 'GrowthRateFactor\(IEnumerable') 'growth factor helper missing'
Check ($expanded -match 'SynergyFactorValue' -and $expanded -match 'ApplyDiseaseResistanceFactor') 'synergy absent/disease helper missing'
Check ($families -match 'synergyAbsentFactor' -and $families -match 'harvestWorkFactor = 1f \+ percent / 100f') 'generated tradeoff inheritance missing'
Check ($validation -match 'no mechanical benefit' -and $validation -match 'no mechanical cost' -and $validation -match 'does not inherit') 'catalog validation coverage missing'
Check ($regression -match 'GrowthYieldWorkRegression' -and $regression -match 'PerennialRegression' -and $regression -match 'SynergyRegression') 'focused regression coverage missing'
Check ($patches -match 'TraitCatalogValidation\.Run\(\)' -and $patches -match 'EffectiveHarvestDestroys\(bool baseValue, bool regularHarvest') 'runtime validation/perennial helper missing'
Check ($debug -match 'TraitCatalogRegression' -and $bridge -match 'HNS_TRAIT_CATALOG_REGRESSIONS') 'runtime regression bridge missing'
Check ($english -match 'HNS_StatGrowthRate' -and $english -match 'without the companion') 'growth/synergy stat localization missing'
Check ($xml.OuterXml -match 'HNS_Perennial.*<perennial>true</perennial>' -and $xml.OuterXml -match 'HNS_Nutritious') 'perennial or nutrition catalog changes missing'
Check ($xml.OuterXml -match '<defName>HNS_Burnresin</defName>.*<heatGrowthOffset>2</heatGrowthOffset>' -and $xml.OuterXml -match '<defName>HNS_Toxresin</defName>.*<blightChanceFactor>0.90</blightChanceFactor>') 'resin tradeoffs missing'

if ($failed.Count -gt 0) { throw ('Trait catalog verification failed: ' + ($failed -join ', ')) }
"Trait catalog verification passed ($passed checks)."
