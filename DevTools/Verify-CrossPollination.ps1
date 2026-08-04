param([string]$Root = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
$checks = @(
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'DefaultCrossPollinationChance = 0.007f'; Name = '0.7 percent fallback' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'DefaultMinimumDonorGrowth = 0.50f'; Name = '50 percent donor threshold' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'GenRadial.RadialCellsAround'; Name = 'bounded donor scan' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'donorPlant.Spawned'; Name = 'spawned donor check' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'donorPlant.Destroyed'; Name = 'destroyed donor check' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'donorPlant.HitPoints > 0'; Name = 'healthy donor check' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'donorPlant.sown'; Name = 'sown donor check' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'dormant = donorPlant.GrowthRateFactor_Temperature <= 0f'; Name = 'dormant donor check' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'GrowthRateFactor_Temperature > 0f'; Name = 'growable donor check' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'CrossPollinationDonorWeight'; Name = 'local donor weighting' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'StringComparer.Ordinal.Compare'; Name = 'deterministic cultivar aggregation' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'CrossPollinationChancePasses'; Name = 'fixed total cross chance helper' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'CrossPollinationSlotPasses'; Name = 'mechanical slot gates' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'MechanicalCrossPollinationTraitCount'; Name = 'cosmetic/mechanical budget split' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'ColorTraitFactory.Cross'; Name = 'independent color inheritance' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'PercentageTraitFactory.Cross'; Name = 'generated nutrition inheritance' },
    @{ File = 'Source/Settings.cs'; Pattern = 'minimumDonorGrowth'; Name = 'donor threshold serialization' },
    @{ File = 'Source/Settings.cs'; Pattern = 'secondCrossPollinationTraitChance'; Name = 'second slot serialization' },
    @{ File = 'Source/Settings.cs'; Pattern = 'laterCrossPollinationTraitChance'; Name = 'later slot serialization' },
    @{ File = 'Source/Settings.cs'; Pattern = 'Scribe_Values.Look(ref globalCrossPollinationChance'; Name = 'existing rate serialization' },
    @{ File = 'Source/Settings.cs'; Pattern = 'globalCrossPollinationChance = other.globalCrossPollinationChance'; Name = 'profile rate migration' },
    @{ File = 'Source/SettingsProfiles.cs'; Pattern = 'ApplyFrom'; Name = 'profile application path' },
    @{ File = 'Source/CrossPollinationRegression.cs'; Pattern = 'DonorEligibilityRegression'; Name = 'eligibility regression coverage' },
    @{ File = 'Source/CrossPollinationRegression.cs'; Pattern = 'SettingsRegression'; Name = 'settings migration regression coverage' },
    @{ File = 'DevTools/BridgeAdapter/HorticultureBridgeAdapter.cs'; Pattern = 'HNS_CROSS_REGRESSIONS'; Name = 'runtime regression bridge' }
)

$failed = @()
foreach ($check in $checks) {
    $path = Join-Path $Root $check.File
    if (!(Test-Path -LiteralPath $path) -or !(Select-String -Path $path -SimpleMatch $check.Pattern -Quiet)) {
        $failed += $check.Name
    }
}

$defaults = Get-Content (Join-Path $Root '1.6/Defaults/DefaultConfiguration.xml') -Raw
if ($defaults -notmatch '<globalCrossPollinationChance>0\.007</globalCrossPollinationChance>') { $failed += 'bundled 0.7 percent default' }
if ($defaults -notmatch '<minimumDonorGrowth>0\.5</minimumDonorGrowth>') { $failed += 'bundled donor threshold default' }
if ($defaults -match '0\.00689673889|minimumDonorGrowth>0\.8') { $failed += 'obsolete bundled defaults' }

$source = Get-Content (Join-Path $Root 'Source/NovelSeedUtility.cs') -Raw
if ($source -match 'GenRadial\.RadialCellsAround[\s\S]{0,1200}\.Where\(') { $failed += 'LINQ-heavy radial donor allocation' }

if ($failed.Count -gt 0) {
    throw 'Cross-pollination regression checks failed: ' + ($failed -join ', ')
}

Write-Host ('Cross-pollination regression checks passed ({0} checks).' -f $checks.Count)
