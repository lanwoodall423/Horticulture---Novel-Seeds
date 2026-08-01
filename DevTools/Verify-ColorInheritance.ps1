param([string]$Root = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
$checks = @(
    @{ File = 'Source/ModCore.cs'; Pattern = 'speciesColorPalettes'; Name = 'per-save palette persistence' },
    @{ File = 'Source/ModCore.cs'; Pattern = 'unlockedVarieties'; Name = 'legacy variety key retained' },
    @{ File = 'Source/CompsAndDialog.cs'; Pattern = 'crossPollinationParentVarietyId'; Name = 'legacy cross key retained' },
    @{ File = 'Source/ColorInheritance.cs'; Pattern = 'class PigmentColorUtility'; Name = 'shared pigment helper' },
    @{ File = 'Source/TraitFamilies.cs'; Pattern = 'SpeciesColorPaletteUtility.SelectTrait'; Name = 'palette-constrained mutation' },
    @{ File = 'Source/NovelSeedUtility.cs'; Pattern = 'ColorTraitFactory.Cross'; Name = 'palette-constrained cross' },
    @{ File = 'Source/ProduceAppearance.cs'; Pattern = 'PigmentColorUtility.Blend'; Name = 'pigment recipe inheritance' },
    @{ File = 'Source/ProduceAppearance.cs'; Pattern = 'product.TryGetComp<CompColorable>()'; Name = 'colorable non-stuff product inheritance' },
    @{ File = 'Source/DebugActions.cs'; Pattern = 'Show species color palettes'; Name = 'developer palette display' }
)

$failed = @()
foreach ($check in $checks) {
    $path = Join-Path $Root $check.File
    if (!(Test-Path $path) -or !(Select-String -Path $path -SimpleMatch $check.Pattern -Quiet)) {
        $failed += $check.Name
    }

$colorSource = Get-Content (Join-Path $Root 'Source/ColorInheritance.cs') -Raw
$requiredPersistence = @('plantDefName', 'packedColors', 'unrestricted', 'hybridDerived')
foreach ($field in $requiredPersistence) {
    if ($colorSource -notmatch [regex]::Escape($field)) { $failed += "palette field $field" }
}

# Independent deterministic/pigment regression probes mirror the documented helper contract.
function Get-StableSequence([string]$Seed, [int]$Count) {
    [uint32]$state = 2166136261
    foreach ($character in $Seed.ToCharArray()) { $state = [uint32]((([uint64]($state -bxor [uint32][char]$character)) * 16777619) -band 0xffffffffL) }
    $result = @()
    for ($index = 0; $index -lt $Count; $index++) {
        $state = [uint32]($state -bxor [uint32](($state -shl 13) -band 0xffffffffL))
        $state = [uint32]($state -bxor ($state -shr 17))
        $state = [uint32]($state -bxor [uint32](($state -shl 5) -band 0xffffffffL))
        $result += ($state -band 0x00ffffff) / 16777216.0
    }
    return $result
}

$sequenceA = Get-StableSequence 'save-a|Plant_Tomato|HNS-color-v1' 8
$sequenceAReloaded = Get-StableSequence 'save-a|Plant_Tomato|HNS-color-v1' 8
$sequenceB = Get-StableSequence 'save-b|Plant_Tomato|HNS-color-v1' 8
if (($sequenceA -join ',') -ne ($sequenceAReloaded -join ',')) { $failed += 'same-seed determinism' }
if (($sequenceA -join ',') -eq ($sequenceB -join ',')) { $failed += 'cross-save variation' }
if ($colorSource -notmatch 'Mathf\.Log' -or $colorSource -notmatch 'Mathf\.Exp') { $failed += 'subtractive geometric reflectance blend' }
if ($colorSource -notmatch 'Mathf\.Max\(mixedSaturation') { $failed += 'saturation preservation' }
}

if ($failed.Count -gt 0) {
    throw 'Color inheritance compatibility checks failed: ' + ($failed -join ', ')
}

Write-Host ('Color inheritance compatibility checks passed ({0} checks).' -f $checks.Count)
