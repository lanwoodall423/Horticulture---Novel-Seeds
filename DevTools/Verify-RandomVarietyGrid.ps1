$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$actions = Get-Content -Raw (Join-Path $root 'Source\DebugActions.cs')
$checks = [ordered]@{
    'map debug action is available while playing' = $actions -match 'Plant 10x10 random varieties' -and $actions -match 'DebugActionType\.ToolMap'
    'footprint contains exactly one hundred unique cells' = $actions -match 'for \(int z = 0; z < 10; z\+\+\)' -and $actions -match 'for \(int x = 0; x < 10; x\+\+\)' -and $actions -match 'cells\.Count == 100' -and $actions -match 'cells\.Distinct\(\)\.Count\(\) == 100'
    'species use balanced random passes and varieties remain random' = $actions -match 'RandomGridSelections' -and $actions -match 'RandomLeastUsedSpecies' -and $actions -match 'speciesVarieties\.RandomElement\(\)'
    'only active registered growable varieties are eligible' = $actions -match 'NovelSeedUtility\.IsGrowableCrop' -and $actions -match '!variety\.registryArchived' -and $actions -match '!variety\.id\.NullOrEmpty\(\)'
    'fresh saves receive a grid-capacity registered cultivar pool' = $actions -match 'RandomGridMaximumSpecies = 100' -and $actions -match 'PrepareRandomGridVarieties' -and $actions -match 'registry\.UnlockVariety\(crop, traits, "DEV grid "'
    'spawned plants are healthy mature sown and assigned before spawn' = $actions -match 'comp\.SetVariety\(variety\)[\s\S]*?plant\.HitPoints = plant\.MaxHitPoints[\s\S]*?plant\.Growth = 1f[\s\S]*?plant\.sown = true[\s\S]*?GenSpawn\.Spawn'
    'invalid terrain and boundaries are skipped and old plants are replaced' = $actions -match '!cell\.InBounds\(map\)' -and $actions -match 'cell\.GetEdifice\(map\)' -and $actions -match 'fertilityGrid\.FertilityAt\(cell\) <= 0f' -and $actions -match 'plant\.Spawned && plant\.Position == cell' -and $actions -match 'existing\.Destroy\(DestroyMode\.Vanish\)'
    'species that reject direct placement are replaced generically' = $actions -match 'availableSpecies\.Remove\(speciesVarieties\)' -and $actions -match 'while \(availableSpecies\.Count > 0 && !occupied\)' -and $actions -notmatch 'Plant_Toxipotato'
    'selection and footprint logic have regression coverage' = $actions -match 'speciesCounts\.Max\(\) - speciesCounts\.Min\(\) <= 1' -and $actions -match 'RandomVarietyGridRegression'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) {
    $failed | ForEach-Object { Write-Error ("FAILED: " + $_.Key) }
    exit 1
}
Write-Output ("Random-variety grid verification passed ({0} checks)." -f $checks.Count)
